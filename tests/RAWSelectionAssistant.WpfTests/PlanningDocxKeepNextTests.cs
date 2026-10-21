using System.IO;
using System.Windows;
using System.Windows.Documents;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Validation;
using W=DocumentFormat.OpenXml.Wordprocessing;
using RAWSelectionAssistant.Services;
using static RAWSelectionAssistant.WpfTests.RuntimeCorrectionWpfTests;
namespace RAWSelectionAssistant.WpfTests;
[TestClass][DoNotParallelize]
public sealed class PlanningDocxKeepNextTests
{
 [TestMethod]public Task ParagraphKeepNextRoundTripsWithoutForcingUnrelatedParagraphs()=>RunSta(()=>
 {
  EnsureTestApplication();var path=Path.Combine(Path.GetTempPath(),Guid.NewGuid()+".docx");
  try
  {
   var document=new FlowDocument();document.Blocks.Add(new Paragraph(new Run("图题")){KeepWithNext=true});document.Blocks.Add(new Paragraph(new Run("图像占位段")){KeepWithNext=true});document.Blocks.Add(new Paragraph(new Run("图注")){KeepWithNext=false});document.Blocks.Add(new Paragraph(new Run("普通正文")){KeepWithNext=false});
   document.Blocks.Add(new Paragraph(new Run("分页图题")){KeepWithNext=true,BreakPageBefore=true,Tag=new RAWSelectionAssistant.Core.Services.Projects.PlanningBlockSemantic(4,"Heading1")});
   PlanningDocxAdapter.Export(document,path);
   using(var package=WordprocessingDocument.Open(path,false))
   {
    Assert.IsEmpty(new OpenXmlValidator().Validate(package));var paragraphs=package.MainDocumentPart!.Document.Body!.Elements<W.Paragraph>().ToArray();
    Assert.IsNotNull(paragraphs[0].ParagraphProperties?.KeepNext,"Image title must carry w:keepNext");Assert.IsNotNull(paragraphs[1].ParagraphProperties?.KeepNext);Assert.IsNull(paragraphs[2].ParagraphProperties?.KeepNext);Assert.IsNull(paragraphs[3].ParagraphProperties?.KeepNext);
   }
   var back=PlanningDocxAdapter.Import(path).Document.Blocks.OfType<Paragraph>().ToArray();Assert.IsTrue(back[0].KeepWithNext);Assert.IsTrue(back[1].KeepWithNext);Assert.IsFalse(back[2].KeepWithNext);Assert.IsFalse(back[3].KeepWithNext);
   return Task.CompletedTask;
  }finally{File.Delete(path);}
 });
}
