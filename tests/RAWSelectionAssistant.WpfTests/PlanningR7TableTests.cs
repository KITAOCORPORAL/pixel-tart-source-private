using System.IO;
using System.Windows;
using System.Windows.Documents;
using RAWSelectionAssistant.Services;
using RAWSelectionAssistant.Core.Services.Projects;
using DocumentFormat.OpenXml.Packaging;
using W=DocumentFormat.OpenXml.Wordprocessing;
using static RAWSelectionAssistant.WpfTests.RuntimeCorrectionWpfTests;
namespace RAWSelectionAssistant.WpfTests;
[TestClass]
public sealed class PlanningR7TableTests
{
 internal static Table Make(int rows=3,int columns=3)
 {var t=new Table();var g=new TableRowGroup();t.RowGroups.Add(g);for(var y=0;y<rows;y++){var r=new TableRow();g.Rows.Add(r);for(var x=0;x<columns;x++)r.Cells.Add(new TableCell(new Paragraph(new Run($"R{y}C{x}"))));}return t;}
 [TestMethod]
 public Task RectangleRejectSplitAndNativeMergeDocxRoundtrip()=>RunSta(async()=>
 {
  var t=Make();var cells=t.RowGroups[0].Rows.SelectMany(r=>r.Cells).ToArray();Assert.Throws<InvalidOperationException>(()=>PlanningTableEditing.Merge(t,[cells[0],cells[1],cells[3]]));Assert.HasCount(9,PlanningTableEditing.Map(t));
  var merged=PlanningTableEditing.Merge(t,[cells[0],cells[1],cells[3],cells[4]]);Assert.AreEqual(2,merged.RowSpan);Assert.AreEqual(2,merged.ColumnSpan);StringAssert.Contains(new TextRange(merged.ContentStart,merged.ContentEnd).Text,"R1C1");t.Tag=new PlanningBlockSemantic(0){RepeatHeader=true};
  var doc=new FlowDocument(t);var layout=PlanningDocumentLayout.Capture(doc,new(),[]);using var rtf=new MemoryStream();new TextRange(doc.ContentStart,doc.ContentEnd).Save(rtf,DataFormats.Rtf);var restored=PlanningDocumentLayout.Load(Convert.ToBase64String(rtf.ToArray()),layout).Blocks.OfType<Table>().Single();Assert.AreEqual(2,restored.RowGroups[0].Rows[0].Cells[0].RowSpan);
  var path=Path.Combine(Path.GetTempPath(),Guid.NewGuid()+".docx");try{PlanningDocxAdapter.Export(doc,path);using(var pkg=WordprocessingDocument.Open(path,false)){Assert.IsEmpty(new DocumentFormat.OpenXml.Validation.OpenXmlValidator().Validate(pkg));Assert.HasCount(2,pkg.MainDocumentPart!.Document.Descendants<W.VerticalMerge>());Assert.HasCount(1,pkg.MainDocumentPart.Document.Descendants<W.TableHeader>());}
   var back=PlanningDocxAdapter.Import(path).Document.Blocks.OfType<Table>().Single();Assert.AreEqual(2,back.RowGroups[0].Rows[0].Cells[0].RowSpan);Assert.AreEqual(2,back.RowGroups[0].Rows[0].Cells[0].ColumnSpan);PlanningTableEditing.Split(back,back.RowGroups[0].Rows[0].Cells[0]);Assert.HasCount(9,PlanningTableEditing.Map(back));StringAssert.Contains(new TextRange(back.ContentStart,back.ContentEnd).Text,"R1C1");
  }finally{File.Delete(path);}await Task.CompletedTask;
 });
 [TestMethod]
 public Task UnsupportedCellBlocksRejectBeforeReplacingExistingFile()=>RunSta(async()=>
 {
  var path=Path.Combine(Path.GetTempPath(),Guid.NewGuid()+".docx");await File.WriteAllTextAsync(path,"original");
  try{foreach(var block in new Block[]{Make(1,1),new BlockUIContainer(new System.Windows.Controls.Image())})
   {var table=Make(1,1);table.RowGroups[0].Rows[0].Cells[0].Blocks.Add(block);Assert.Throws<NotSupportedException>(()=>PlanningDocxAdapter.Export(new FlowDocument(table),path));Assert.AreEqual("original",await File.ReadAllTextAsync(path));}
  }finally{File.Delete(path);}
 });
}
