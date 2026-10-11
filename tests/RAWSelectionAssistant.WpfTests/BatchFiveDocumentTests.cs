using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using RAWSelectionAssistant.Services;
using RAWSelectionAssistant.Core.Services.Projects;
namespace RAWSelectionAssistant.WpfTests;
[TestClass]
public sealed class BatchFiveDocumentTests
{
    public TestContext TestContext {get;set;}=null!;
    [TestMethod]
    public void MixedContentAndLayoutUndoRedoStayTogether()
    {
        Exception? error=null;var thread=new Thread(()=>
        {
            try
            {
                var editor=new RichTextBox{Document=new FlowDocument(new Paragraph(new Run("中文 English"))),IsUndoEnabled=false};var page=new PlanningPageLayout();IReadOnlyList<string> losses=[];
                var history=new RichDocumentHistory(editor,()=>PlanningDocumentLayout.Capture(editor.Document,page,losses),layout=>{page=layout.Page;losses=layout.ImportLosses;});history.Initialize();
                history.Execute(()=>{editor.Document.Blocks.FirstBlock!.Tag=new PlanningBlockSemantic(0,"Heading1");editor.Document.Blocks.FirstBlock.BreakPageBefore=true;});
                history.Execute(()=>{page=page with{Width=1123,Height=794,Left=70};var table=new Table();var rows=new TableRowGroup();var row=new TableRow();row.Cells.Add(new TableCell(new Paragraph(new Run("表格"))));rows.Rows.Add(row);table.RowGroups.Add(rows);editor.Document.Blocks.Add(table);});
                var final=history.Capture();history.Undo();Assert.AreEqual(794d,page.Width);Assert.AreEqual(1,editor.Document.Blocks.Count);Assert.IsTrue(editor.Document.Blocks.FirstBlock!.BreakPageBefore);
                history.Undo();Assert.AreEqual("Body",(editor.Document.Blocks.FirstBlock!.Tag as PlanningBlockSemantic)!.Style);Assert.IsFalse(editor.Document.Blocks.FirstBlock.BreakPageBefore);
                history.Redo();history.Redo();Assert.AreEqual(1123d,page.Width);Assert.AreEqual(2,editor.Document.Blocks.Count);Assert.AreEqual(System.Text.Json.JsonSerializer.Serialize(final.Layout),System.Text.Json.JsonSerializer.Serialize(history.Capture().Layout));
            }catch(Exception e){error=e;}
        });thread.SetApartmentState(ApartmentState.STA);thread.Start();thread.Join();if(error is not null)throw error;
    }
    [TestMethod]
    public void EveryCompositeActionHasOneJointUndoStepAndFontsAreReported()
    {
        Exception? failure=null;var thread=new Thread(()=>
        {
            try
            {
                var editor=new RichTextBox{IsUndoEnabled=false,Document=new FlowDocument(new Paragraph(new Run("中文 English")))};var page=new PlanningPageLayout();var history=new RichDocumentHistory(editor,()=>PlanningDocumentLayout.Capture(editor.Document,page,[]),layout=>page=layout.Page);history.Initialize();
                var expected=new List<(int Blocks,double Width,string Style)>();
                void Act(Action action){history.Execute(action);expected.Add((editor.Document.Blocks.Count,page.Width,(editor.Document.Blocks.FirstBlock!.Tag as PlanningBlockSemantic)?.Style??"Body"));}
                Act(()=>{editor.Document.Blocks.FirstBlock!.Tag=new PlanningBlockSemantic(0,"Heading2");((Paragraph)editor.Document.Blocks.FirstBlock).TextAlignment=TextAlignment.Center;});
                Act(()=>editor.Document.Blocks.Add(new List(new ListItem(new Paragraph(new Run("list"))))));
                Act(()=>{var t=new Table();var g=new TableRowGroup();var r=new TableRow();r.Cells.Add(new TableCell(new Paragraph(new Run("表"))));g.Rows.Add(r);t.RowGroups.Add(g);editor.Document.Blocks.Add(t);});
                Act(()=>{var t=editor.Document.Blocks.OfType<Table>().Single();t.Columns.Add(new TableColumn{Width=new GridLength(157.5)});t.RowGroups[0].Rows[0].Cells[0].BorderThickness=new Thickness(1);});
                Act(()=>{page=page with{Width=1123,Height=794,Left=55};editor.Document.Blocks.FirstBlock!.BreakPageBefore=true;});
                var canvas=Guid.NewGuid();var revision=Guid.NewGuid();Act(()=>editor.Document.Blocks.Add(new Paragraph(new Run("画布占位")){Tag=new PlanningBlockSemantic(0,CanvasId:canvas,CanvasRevisionId:revision)}));
                for(var index=expected.Count-2;index>=0;index--){history.Undo();Assert.AreEqual(expected[index].Blocks,editor.Document.Blocks.Count);Assert.AreEqual(expected[index].Width,page.Width);Assert.AreEqual(expected[index].Style,(editor.Document.Blocks.FirstBlock!.Tag as PlanningBlockSemantic)?.Style??"Body");}
                history.Undo();Assert.AreEqual(1,editor.Document.Blocks.Count);for(var index=0;index<expected.Count;index++){history.Redo();Assert.AreEqual(expected[index].Blocks,editor.Document.Blocks.Count);}
                var snapshot=history.Capture();var restored=PlanningDocumentLayout.Load(snapshot.Rtf,snapshot.Layout);Assert.AreEqual(canvas,((PlanningBlockSemantic)restored.Blocks.LastBlock!.Tag).CanvasId);
                ((IDocumentPaginatorSource)restored).DocumentPaginator.ComputePageCount();var baselinePages=((IDocumentPaginatorSource)restored).DocumentPaginator.PageCount;var missing="Kitao Definitely Missing Font";var installed=System.Windows.Media.Fonts.SystemFontFamilies.Any(f=>f.Source==missing);Assert.IsFalse(installed);restored.FontFamily=new System.Windows.Media.FontFamily(missing);((IDocumentPaginatorSource)restored).DocumentPaginator.ComputePageCount();Assert.IsTrue(((IDocumentPaginatorSource)restored).DocumentPaginator.PageCount>0);TestContext.WriteLine($"MissingFont={missing};baselinePages={baselinePages};fallbackPages={((IDocumentPaginatorSource)restored).DocumentPaginator.PageCount};layoutMayVary=true");
            }catch(Exception e){failure=e;}
        });thread.SetApartmentState(ApartmentState.STA);thread.Start();thread.Join();if(failure is not null)throw failure;
    }    [TestMethod]
    public void ActualEditorButtonsAndPrecisionCommitUseJointHistory()
    {
        Exception? failure=null;var thread=new Thread(()=>
        {
            try
            {
                PlanningRichLayout? saved=null;string? data=null;
                var view=new RAWSelectionAssistant.Views.PlanningRichEditor(null,"中文 English",(rtf,text,layout)=>{data=rtf;saved=layout;},[]);
                var tools=view.Children.OfType<WrapPanel>().Single();var editor=view.Children.OfType<RichTextBox>().Single();
                void Click(string label)=>tools.Children.OfType<Button>().Single(b=>Equals(b.Content,label)).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                editor.CaretPosition=editor.Document.Blocks.FirstBlock!.ContentStart;Click("标题1");Click("横/竖页");Click("表格");
                Assert.AreEqual("Heading1",saved!.Blocks[0].Style);Assert.AreEqual(1123d,saved.Page.Width);Assert.AreEqual(2,editor.Document.Blocks.Count);
                Click("撤销");Assert.AreEqual(1,editor.Document.Blocks.Count);Click("撤销");Assert.AreEqual(794d,saved.Page.Width);Click("撤销");Assert.AreEqual("Body",saved.Blocks[0].Style);
                Click("重做");Click("重做");Click("重做");var table=editor.Document.Blocks.OfType<Table>().Single();editor.CaretPosition=table.RowGroups[0].Rows[0].Cells[0].ContentStart;
                Click("加行");Assert.AreEqual(4,table.RowGroups[0].Rows.Count);Click("撤销");table=editor.Document.Blocks.OfType<Table>().Single();Assert.AreEqual(3,table.RowGroups[0].Rows.Count);
                editor.CaretPosition=table.RowGroups[0].Rows[0].Cells[0].ContentStart;
                var width=tools.Children.OfType<TextBox>().First();width.Text="42.5";width.RaiseEvent(new System.Windows.Input.KeyboardFocusChangedEventArgs(System.Windows.Input.Keyboard.PrimaryDevice,0,width,editor){RoutedEvent=System.Windows.Input.Keyboard.LostKeyboardFocusEvent});
                Assert.AreEqual(42.5*96/25.4,table.Columns[0].Width.Value,.001);
                width.Text="NaN";width.RaiseEvent(new System.Windows.Input.KeyboardFocusChangedEventArgs(System.Windows.Input.Keyboard.PrimaryDevice,0,width,editor){RoutedEvent=System.Windows.Input.Keyboard.LostKeyboardFocusEvent});Assert.AreEqual(42.5*96/25.4,table.Columns[0].Width.Value,.001);
                view.FlushPending();Assert.IsNotNull(data);Assert.AreEqual(1123d,PlanningDocumentLayout.Load(data,saved).PageWidth);
            }catch(Exception e){failure=e;}
        });thread.SetApartmentState(ApartmentState.STA);thread.Start();thread.Join();if(failure is not null)throw failure;
    }}
