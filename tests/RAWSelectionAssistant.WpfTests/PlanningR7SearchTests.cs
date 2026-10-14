using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using RAWSelectionAssistant.Services;
using RAWSelectionAssistant.Views;
using static RAWSelectionAssistant.WpfTests.RuntimeCorrectionWpfTests;
namespace RAWSelectionAssistant.WpfTests;
[TestClass]
public sealed class PlanningR7SearchTests
{
 [TestMethod]
 public Task CrossRunChineseNestedListCellAndSingleUndo()=>RunSta(async()=>
 {
  var editor=new PlanningRichEditor(null,"",(_,_,_)=>{},[]);var box=editor.Children.OfType<RichTextBox>().Single();var p=new Paragraph();
  var first=new Run("保留 中文"){Foreground=Brushes.Red};var second=new Run("镜头 后缀"){FontWeight=FontWeights.Bold};p.Inlines.Add(first);p.Inlines.Add(second);box.Document.Blocks.Clear();box.Document.Blocks.Add(p);
  var table=new Table();var g=new TableRowGroup();var row=new TableRow();row.Cells.Add(new TableCell(new Paragraph(new Run("中文镜头"))));g.Rows.Add(row);table.RowGroups.Add(g);box.Document.Blocks.Add(table);
  var list=new List(new ListItem(new Paragraph(new Run("中文镜头"))));list.ListItems.FirstListItem.Blocks.Add(new List(new ListItem(new Paragraph(new Run("中文镜头")))));box.Document.Blocks.Add(list);editor.FlushPending();
  Assert.HasCount(4,PlanningTextSearch.Find(box.Document,"中文镜头"));Assert.AreEqual(4,editor.ReplaceBody("中文镜头","画面",false,false,true));
  Assert.AreEqual("保留 画面",first.Text);Assert.AreEqual(Brushes.Red,first.Foreground);Assert.AreEqual(" 后缀",second.Text);Assert.AreEqual(FontWeights.Bold,second.FontWeight);
  System.Windows.Input.ApplicationCommands.Undo.Execute(null,editor);Assert.HasCount(4,PlanningTextSearch.Find(box.Document,"中文镜头"));
  Assert.AreEqual(0,editor.ReplaceBody("不存在","x",false,false,true));System.Windows.Input.ApplicationCommands.Redo.Execute(null,editor);Assert.HasCount(4,PlanningTextSearch.Find(box.Document,"画面"));await Task.CompletedTask;
 });
 [TestMethod]
 public Task CaseWholeWordAndAdjacentMatchesAreDeterministic()=>RunSta(async()=>
 {
  var doc=new FlowDocument(new Paragraph(new Run("Cat cat scatter 猫 猫咪 中文 中文")));
  Assert.HasCount(1,PlanningTextSearch.Find(doc,"Cat",true,true));Assert.HasCount(2,PlanningTextSearch.Find(doc,"cat",false,true));Assert.HasCount(1,PlanningTextSearch.Find(doc,"猫",false,true));Assert.HasCount(2,PlanningTextSearch.Find(doc,"中文",false,true));await Task.CompletedTask;
 });
}
