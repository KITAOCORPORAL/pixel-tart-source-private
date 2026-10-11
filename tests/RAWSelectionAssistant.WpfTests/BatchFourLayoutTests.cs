using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Documents;
using RAWSelectionAssistant.Services;
using RAWSelectionAssistant.Core.Services.Projects;
namespace RAWSelectionAssistant.WpfTests;
[TestClass]
public sealed class BatchFourLayoutTests
{
    public TestContext TestContext {get;set;}=null!;
    [TestMethod]
    public void PageSemanticsAndLayoutSurviveRtfAndMeasuredLargeDocument()
    {
        Exception? failure=null;string metrics="";var thread=new Thread(()=>
        {
            try
            {
                var document=new FlowDocument();for(var i=0;i<500;i++)document.Blocks.Add(new Paragraph(new Run("摄影文档性能样本 "+i+" 内容段落。")));
                document.Blocks.FirstBlock!.Tag=new PlanningBlockSemantic(0,"Heading1");document.Blocks.ElementAt(250).BreakPageBefore=true;
                var layout=PlanningDocumentLayout.Capture(document,new(1123,794,60,40,60,40),["保留的导入损失"]);PlanningDocumentLayout.Apply(document,layout);
                var watch=Stopwatch.StartNew();using var memory=new MemoryStream();new TextRange(document.ContentStart,document.ContentEnd).Save(memory,DataFormats.Rtf);var save=watch.ElapsedMilliseconds;
                watch.Restart();var reopened=PlanningDocumentLayout.Load(Convert.ToBase64String(memory.ToArray()),layout);var restore=watch.ElapsedMilliseconds;
                var paginator=((IDocumentPaginatorSource)reopened).DocumentPaginator;watch.Restart();paginator.ComputePageCount();var paginate=watch.ElapsedMilliseconds;
                Assert.AreEqual(1123d,reopened.PageWidth);Assert.AreEqual("Heading1",((PlanningBlockSemantic)reopened.Blocks.FirstBlock!.Tag).Style);Assert.IsTrue(reopened.Blocks.ElementAt(250).BreakPageBefore);Assert.IsTrue(paginator.PageCount>1);
                                var editor=new System.Windows.Controls.RichTextBox{Document=reopened};watch.Restart();editor.CaretPosition=reopened.ContentEnd;editor.CaretPosition.InsertTextInRun("undo measurement");var editMs=watch.ElapsedMilliseconds;watch.Restart();editor.Undo();var undoMs=watch.ElapsedMilliseconds;
                watch.Restart();var pixels=new byte[512*512*3];new Random(17).NextBytes(pixels);var bitmap=System.Windows.Media.Imaging.BitmapSource.Create(512,512,96,96,System.Windows.Media.PixelFormats.Rgb24,null,pixels,512*3);reopened.Blocks.Add(new BlockUIContainer(new System.Windows.Controls.Image{Source=bitmap,Width=300}));using var withImage=new MemoryStream();new TextRange(reopened.ContentStart,reopened.ContentEnd).Save(withImage,DataFormats.Rtf);var imageSaveMs=watch.ElapsedMilliseconds;
                var pdf=Path.Combine(Path.GetTempPath(),Guid.NewGuid()+".pdf");try{PlanningProposalPdf.ExportDocument(reopened,pdf);var pdfText=System.Text.Encoding.ASCII.GetString(File.ReadAllBytes(pdf));StringAssert.Contains(pdfText,"/MediaBox [0 0 842.25 595.5]");}finally{if(File.Exists(pdf))File.Delete(pdf);}
                metrics=$"imageBytes={withImage.Length};imageSaveMs={imageSaveMs};editMs={editMs};undoMs={undoMs};paragraphs=500;bytes={memory.Length};saveMs={save};restoreMs={restore};paginateMs={paginate};pages={paginator.PageCount}";
            }catch(Exception e){failure=e;}
        });thread.SetApartmentState(ApartmentState.STA);thread.Start();thread.Join();if(failure is not null)throw failure;TestContext.WriteLine(metrics);
    }
}
