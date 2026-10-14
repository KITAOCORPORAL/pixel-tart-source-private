using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using RAWSelectionAssistant.Services;
using RAWSelectionAssistant.Views;
using RAWSelectionAssistant.Core.Services.Projects;
using DocumentFormat.OpenXml.Packaging;
using W=DocumentFormat.OpenXml.Wordprocessing;
using static RAWSelectionAssistant.WpfTests.RuntimeCorrectionWpfTests;
namespace RAWSelectionAssistant.WpfTests;
[TestClass]
public sealed class PlanningR7PageExtrasTests
{
 [TestMethod]
 public Task HeaderFooterPersistenceUndoNativeFieldsAndPdfShareContract()=>RunSta(async()=>
 {
  var editor=new PlanningRichEditor(null,"第一页",(_,_,_)=>{},[]);var box=editor.Children.OfType<RichTextBox>().Single();box.Document.Blocks.Add(new Paragraph(new Run("第二页")){BreakPageBefore=true});editor.FlushPending();
  var extras=new PlanningHeaderFooter{Header="通用页眉",Footer="通用页脚",DifferentFirstPage=true,FirstHeader="首页提案",FirstFooter="客户",PageNumbers=true,StartNumber=7};editor.ApplyHeaderFooter(extras);
  var layout=PlanningDocumentLayout.Capture(box.Document,new(),[]);Assert.AreEqual(extras,layout.HeaderFooter);
  System.Windows.Input.ApplicationCommands.Undo.Execute(null,editor);Assert.AreEqual(new PlanningHeaderFooter(),box.Document.Tag as PlanningHeaderFooter??new());System.Windows.Input.ApplicationCommands.Redo.Execute(null,editor);Assert.AreEqual(extras,box.Document.Tag);
  var path=Path.Combine(Path.GetTempPath(),Guid.NewGuid()+".docx");var pdf=path+".pdf";
  try{PlanningDocxAdapter.Export(box.Document,path);using(var pkg=WordprocessingDocument.Open(path,false)){Assert.IsEmpty(new DocumentFormat.OpenXml.Validation.OpenXmlValidator().Validate(pkg));Assert.HasCount(2,pkg.MainDocumentPart!.HeaderParts);Assert.IsTrue(pkg.MainDocumentPart.FooterParts.Any(p=>p.Footer.Descendants<W.SimpleField>().Any(f=>f.Instruction?.Value?.Trim()=="NUMPAGES")));}
   var back=PlanningDocxAdapter.Import(path).Document;Assert.AreEqual(extras,back.Tag);var paginator=new PlanningPaginator(back);Assert.AreEqual(2,paginator.PageCount);Assert.AreEqual("首页提案",paginator.HeaderText(0));StringAssert.Contains(paginator.FooterText(1),"第8页/共2页");PlanningProposalPdf.ExportDocument(back,pdf,96);StringAssert.Contains(await File.ReadAllTextAsync(pdf),"/Count 2");
  }finally{File.Delete(path);File.Delete(pdf);}
 });
 [TestMethod]
 public Task PrintPipelineSerializesSharedPaginatorToXps()=>RunSta(async()=>
 {
  var table=PlanningR7TableTests.Make(70,3);table.Tag=new PlanningBlockSemantic(0){RepeatHeader=true};var doc=new FlowDocument(table){PageWidth=794,PageHeight=1123,PagePadding=new Thickness(54),FontSize=14,Tag=new PlanningHeaderFooter{Header="打印合同",PageNumbers=true,StartNumber=2}};
  var paginator=new PlanningPaginator(doc);var path=Path.Combine(Path.GetTempPath(),Guid.NewGuid()+".xps");
  try{using(var xps=new System.Windows.Xps.Packaging.XpsDocument(path,FileAccess.ReadWrite)){var writer=System.Windows.Xps.Packaging.XpsDocument.CreateXpsDocumentWriter(xps);writer.Write(paginator);}
   using var zip=System.IO.Compression.ZipFile.OpenRead(path);Assert.AreEqual(paginator.PageCount,zip.Entries.Count(e=>e.FullName.EndsWith(".fpage",StringComparison.OrdinalIgnoreCase)));
  }finally{File.Delete(path);}await Task.CompletedTask;
 });
 [TestMethod]
 public Task RepeatHeaderAllocatesSeparateRowsAndRejectsVerticalMerge()=>RunSta(async()=>
 {
  var table=PlanningR7TableTests.Make(80,3);table.Tag=new PlanningBlockSemantic(0){RepeatHeader=true};var doc=new FlowDocument(table){PageWidth=794,PageHeight=1123,PagePadding=new Thickness(48),FontSize=14};
  var paginator=new PlanningPaginator(doc);Assert.IsGreaterThan(1,paginator.PageCount,$"chunks={doc.Blocks.Count};rows={string.Join(",",doc.Blocks.OfType<Table>().Select(t=>t.RowGroups[0].Rows.Count))}");Assert.IsGreaterThan(1,doc.Blocks.OfType<Table>().Count());Assert.AreEqual(doc.Blocks.OfType<Table>().Count(),paginator.PageCount,"Each measured continuation must fit one page with its repeated header.");foreach(var chunk in doc.Blocks.OfType<Table>())StringAssert.Contains(new TextRange(chunk.RowGroups[0].Rows[0].ContentStart,chunk.RowGroups[0].Rows[0].ContentEnd).Text,"R0C0");
  var merged=PlanningR7TableTests.Make();PlanningTableEditing.Merge(merged,[merged.RowGroups[0].Rows[0].Cells[0],merged.RowGroups[0].Rows[1].Cells[0]]);merged.Tag=new PlanningBlockSemantic(0){RepeatHeader=true};Assert.Throws<NotSupportedException>(()=>new PlanningPaginator(new FlowDocument(merged)));await Task.CompletedTask;
 });
}
