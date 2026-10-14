using System.IO;
using System.Windows;
using System.Windows.Documents;
using DocumentFormat.OpenXml.Packaging;
using RAWSelectionAssistant.Services;
using W=DocumentFormat.OpenXml.Wordprocessing;
using static RAWSelectionAssistant.WpfTests.RuntimeCorrectionWpfTests;
namespace RAWSelectionAssistant.WpfTests;
[TestClass]
public sealed class PlanningR7PageContractTests
{
 [TestMethod]
 public Task A4LetterAndA3LandscapeConvertActualSizeMarginsAndMessage()=>RunSta(async()=>
 {
  foreach(var (w,h,landscape,converted) in new[]{(11906U,16838U,false,false),(12240U,15840U,false,true),(23811U,16838U,true,true)})
  {
   var path=Path.Combine(Path.GetTempPath(),Guid.NewGuid()+".docx");
   try{
    using(var pkg=WordprocessingDocument.Create(path,DocumentFormat.OpenXml.WordprocessingDocumentType.Document))
    {var main=pkg.AddMainDocumentPart();main.Document=new W.Document(new W.Body(new W.Paragraph(new W.Run(new W.Text("页面合同"))),new W.SectionProperties(new W.PageSize{Width=w,Height=h,Orient=landscape?W.PageOrientationValues.Landscape:W.PageOrientationValues.Portrait},new W.PageMargin{Left=1440,Right=720,Top=1080,Bottom=720})));main.Document.Save();}
    var result=PlanningDocxAdapter.Import(path);var tw=landscape?16838d:11906d;var th=landscape?11906d:16838d;
    Assert.AreEqual(tw/15,result.Document.PageWidth,.01);Assert.AreEqual(th/15,result.Document.PageHeight,.01);
    Assert.AreEqual(96*tw/w,result.Document.PagePadding.Left,.01);Assert.AreEqual(72*th/h,result.Document.PagePadding.Top,.01);
    Assert.AreEqual(converted,result.Losses.Any(x=>x.Contains("页面设置将转换为 A4")));
    PlanningDocxAdapter.Export(result.Document,path);using var reopened=WordprocessingDocument.Open(path,false);Assert.IsEmpty(new DocumentFormat.OpenXml.Validation.OpenXmlValidator().Validate(reopened));
   }finally{File.Delete(path);}
  }
  await Task.CompletedTask;
 });
}
