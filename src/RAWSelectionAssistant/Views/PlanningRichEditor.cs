using System.Windows.Threading;
using RAWSelectionAssistant.Core.Services.Projects;
using RAWSelectionAssistant.Services;
using System.Windows.Input;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Microsoft.Win32;
namespace RAWSelectionAssistant.Views;
public sealed class PlanningRichEditor : DockPanel
{
    private readonly RichTextBox _editor=new(){MinHeight=420,AcceptsTab=true,VerticalScrollBarVisibility=ScrollBarVisibility.Auto};
    private PlanningPageLayout _page=new();
    private IReadOnlyList<string> _losses=[];
    private Action? _persist;
    private readonly DispatcherTimer _saveTimer=new(){Interval=TimeSpan.FromMilliseconds(350)};
    public PlanningRichEditor(string? data,string text,Action<string,string,PlanningRichLayout> save,IEnumerable<PlanningCanvasRevision> canvasImages,PlanningRichLayout? layout=null)
    {
        var tools=new WrapPanel();SetDock(tools,Dock.Top);Children.Add(tools);Children.Add(_editor);
        if(data is not null){using var input=new MemoryStream(Convert.FromBase64String(data));new TextRange(_editor.Document.ContentStart,_editor.Document.ContentEnd).Load(input,DataFormats.Rtf);}
        else _editor.Document=new FlowDocument(new Paragraph(new Run(text)));
        _page=layout?.Page??new();_losses=layout?.ImportLosses??[];PlanningDocumentLayout.Apply(_editor.Document,layout);
        void Add(string label,Action action){var button=new Button{Content=label,Margin=new Thickness(2),Padding=new Thickness(6,3,6,3)};button.Click+=(_,_)=>{try {action();_persist?.Invoke();_editor.Focus();}catch(Exception ex) when(ex is IOException or NotSupportedException or System.Xml.XmlException or InvalidDataException){ThemedMessageDialog.Show(Window.GetWindow(this),"文档操作未完成",ex.Message,ThemedMessageKind.Error);}};tools.Children.Add(button);}
        void Command(string label,System.Windows.Input.RoutedUICommand command)=>Add(label,()=>command.Execute(null,_editor));
        Command("加粗",EditingCommands.ToggleBold);Command("斜体",EditingCommands.ToggleItalic);Command("下划线",EditingCommands.ToggleUnderline);
        void Style(string style){if(_editor.CaretPosition.Paragraph is not {} p)return;p.Tag=new PlanningBlockSemantic(0,style,p.BreakPageBefore);p.FontSize=style switch{"Heading1"=>26,"Heading2"=>21,"Heading3"=>17,_=>14};p.FontWeight=style=="Body"?FontWeights.Normal:FontWeights.Bold;}
        Add("正文",()=>Style("Body"));Add("标题1",()=>Style("Heading1"));Add("标题2",()=>Style("Heading2"));Add("标题3",()=>Style("Heading3"));
        var outline=new ComboBox{Width=130,ToolTip="标题大纲"};tools.Children.Add(outline);
        outline.DropDownOpened+=(_,_)=>outline.ItemsSource=_editor.Document.Blocks.OfType<Paragraph>().Where(p=>(p.Tag as PlanningBlockSemantic)?.Style.StartsWith("Heading")==true).Select(p=>new{Text=new TextRange(p.ContentStart,p.ContentEnd).Text.Trim(),Paragraph=p}).ToArray();outline.DisplayMemberPath="Text";
        outline.SelectionChanged+=(_,_)=>{if(outline.SelectedItem is {} selected && selected.GetType().GetProperty("Paragraph")?.GetValue(selected) is Paragraph p){_editor.CaretPosition=p.ContentStart;p.BringIntoView();}};
        Add("分页",()=>{if(_editor.CaretPosition.Paragraph is {} p)p.BreakPageBefore=!p.BreakPageBefore;});
        Add("横/竖页",()=>{_page=_page with{Width=_page.Height,Height=_page.Width};PlanningDocumentLayout.Apply(_editor.Document,PlanningDocumentLayout.Capture(_editor.Document,_page,_losses));});
        Add("A4/Letter",()=>{_page=_page.Width==794?new(816,1056):new();PlanningDocumentLayout.Apply(_editor.Document,PlanningDocumentLayout.Capture(_editor.Document,_page,_losses));});
        Add("页边距",()=>{var margin=_page.Left==48?72:48;_page=_page with{Left=margin,Top=margin,Right=margin,Bottom=margin};PlanningDocumentLayout.Apply(_editor.Document,PlanningDocumentLayout.Capture(_editor.Document,_page,_losses));});        Command("左对齐",EditingCommands.AlignLeft);Command("居中",EditingCommands.AlignCenter);Command("右对齐",EditingCommands.AlignRight);Command("列表",EditingCommands.ToggleBullets);
        Command("撤销",ApplicationCommands.Undo);Command("重做",ApplicationCommands.Redo);
        Add("表格",()=>{_editor.BeginChange();try{var table=new Table();var group=new TableRowGroup();table.RowGroups.Add(group);for(var r=0;r<3;r++){var row=new TableRow();group.Rows.Add(row);for(var c=0;c<3;c++)row.Cells.Add(new TableCell(new Paragraph(new Run("单元格"))){BorderBrush=Brushes.Gray,BorderThickness=new Thickness(1),Padding=new Thickness(4)});}_editor.Document.Blocks.Add(table);}finally{_editor.EndChange();}});
        TableCell? Cell(){DependencyObject? item=_editor.CaretPosition.Parent;while(item is not null){if(item is TableCell c)return c;item=(item as FrameworkContentElement)?.Parent;}return null;}
        Add("加行",()=>{if(Cell()?.Parent is TableRow row && row.Parent is TableRowGroup group){var added=new TableRow();foreach(var c in row.Cells)added.Cells.Add(new TableCell(new Paragraph(new Run(""))));group.Rows.Insert(group.Rows.IndexOf(row)+1,added);}});
        Add("删行",()=>{if(Cell()?.Parent is TableRow row && row.Parent is TableRowGroup group && group.Rows.Count>1)group.Rows.Remove(row);});
        Add("加列",()=>{if(Cell()?.Parent is TableRow row && row.Parent is TableRowGroup group)foreach(var r in group.Rows)r.Cells.Add(new TableCell(new Paragraph(new Run(""))));});
        Add("删列",()=>{if(Cell() is {} cell && cell.Parent is TableRow row && row.Parent is TableRowGroup group){var index=row.Cells.IndexOf(cell);if(row.Cells.Count>1)foreach(var r in group.Rows)if(r.Cells.Count>index)r.Cells.RemoveAt(index);}});
        Add("列宽",()=>{if(Cell() is {} cell && cell.Parent is TableRow row && row.Parent is TableRowGroup group && group.Parent is Table table){while(table.Columns.Count<row.Cells.Count)table.Columns.Add(new TableColumn{Width=new GridLength(120)});var column=table.Columns[row.Cells.IndexOf(cell)];column.Width=new GridLength(column.Width.Value==120?180:120);}});
        Add("表格对齐",()=>{if(Cell() is {} cell)cell.TextAlignment=cell.TextAlignment==TextAlignment.Left?TextAlignment.Center:TextAlignment.Left;});
        Add("边框",()=>{if(Cell() is {} cell){cell.BorderBrush=Brushes.Gray;cell.BorderThickness=cell.BorderThickness.Left==0?new Thickness(1):new Thickness(0);}});
        void Image(byte[] bytes){_editor.BeginChange();try{using var stream=new MemoryStream(bytes);var bitmap=new BitmapImage();bitmap.BeginInit();bitmap.CacheOption=BitmapCacheOption.OnLoad;bitmap.StreamSource=stream;bitmap.EndInit();bitmap.Freeze();_editor.Document.Blocks.Add(new BlockUIContainer(new Image{Source=bitmap,Width=480,Stretch=Stretch.Uniform}));}finally{_editor.EndChange();}}
        Add("图片",()=>{var dialog=new OpenFileDialog{Filter="图片|*.png;*.jpg;*.jpeg"};if(dialog.ShowDialog()==true)Image(File.ReadAllBytes(dialog.FileName));});
        var canvasPicker=new ComboBox{Width=140,ItemsSource=canvasImages.Where(v=>v.PreviewPngBase64 is not null).GroupBy(v=>v.CanvasId).Select(g=>g.Last()).ToArray(),DisplayMemberPath="EditableSnapshot.Name",ToolTip="选择需插入或更新的画布"};tools.Children.Add(canvasPicker);canvasPicker.SelectedIndex=0;
        Add("画布快照",()=>{var last=canvasPicker.SelectedItem as PlanningCanvasRevision;if(last is not null){var existing=_editor.Document.Blocks.FirstOrDefault(b=>(b.Tag as PlanningBlockSemantic)?.CanvasId==last.CanvasId);Image(Convert.FromBase64String(last.PreviewPngBase64!));var inserted=_editor.Document.Blocks.LastBlock!;inserted.Tag=new PlanningBlockSemantic(0,CanvasId:last.CanvasId,CanvasRevisionId:last.RevisionId);if(existing is not null){_editor.Document.Blocks.Remove(inserted);_editor.Document.Blocks.InsertBefore(existing,inserted);_editor.Document.Blocks.Remove(existing);}}else ThemedMessageDialog.Show(Window.GetWindow(this),"画布快照","请先在情绪板插入或更新画布版本。",ThemedMessageKind.Information);});
        Add("分页预览",()=>{var viewer=new FlowDocumentPageViewer{Document=Clone()};new Window{Title="Kitao Studio 光镜 · 分页预览",Content=viewer,Width=900,Height=760,Owner=Window.GetWindow(this)}.Show();});
        Add("打印 / PDF",()=>{var dialog=new PrintDialog();if(dialog.ShowDialog()==true)dialog.PrintDocument(((IDocumentPaginatorSource)Clone()).DocumentPaginator,"Kitao Studio 光镜 策划");});
        Add("导出 DOCX",()=>{var dialog=new SaveFileDialog{Filter="DOCX|*.docx"};if(dialog.ShowDialog()==true)Services.PlanningDocxAdapter.Export(_editor.Document,dialog.FileName);});
        Add("导入 DOCX",()=>{var dialog=new OpenFileDialog{Filter="DOCX|*.docx"};if(dialog.ShowDialog()!=true)return;var result=Services.PlanningDocxAdapter.Import(dialog.FileName);if(result.Losses.Count>0&&!ThemedMessageDialog.Show(Window.GetWindow(this),"导入损失报告",string.Join("\n",result.Losses)+"\n是否导入支持的内容？原文件不会修改。",ThemedMessageKind.Question,true))return;_losses=result.Losses;_editor.Document=result.Document;_page=new(double.IsFinite(result.Document.PageWidth)?result.Document.PageWidth:794,double.IsFinite(result.Document.PageHeight)?result.Document.PageHeight:1123,result.Document.PagePadding.Left,result.Document.PagePadding.Top,result.Document.PagePadding.Right,result.Document.PagePadding.Bottom);PlanningDocumentLayout.Apply(_editor.Document,PlanningDocumentLayout.Capture(_editor.Document,_page,_losses));});
        _persist=()=>{using var stream=new MemoryStream();new TextRange(_editor.Document.ContentStart,_editor.Document.ContentEnd).Save(stream,DataFormats.Rtf);save(Convert.ToBase64String(stream.ToArray()),new TextRange(_editor.Document.ContentStart,_editor.Document.ContentEnd).Text,PlanningDocumentLayout.Capture(_editor.Document,_page,_losses));};
        _saveTimer.Tick+=(_,_)=>{_saveTimer.Stop();_persist();};
        _editor.TextChanged+=(_,_)=>{_saveTimer.Stop();_saveTimer.Start();};
        Unloaded+=(_,_)=>{_saveTimer.Stop();_persist();};
    }
    public void FlushPending(){_saveTimer.Stop();_persist?.Invoke();}
    private FlowDocument Clone(){using var stream=new MemoryStream();new TextRange(_editor.Document.ContentStart,_editor.Document.ContentEnd).Save(stream,DataFormats.Rtf);stream.Position=0;var doc=new FlowDocument();new TextRange(doc.ContentStart,doc.ContentEnd).Load(stream,DataFormats.Rtf);PlanningDocumentLayout.Apply(doc,PlanningDocumentLayout.Capture(_editor.Document,_page,_losses));return doc;}
}
