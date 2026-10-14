using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using RAWSelectionAssistant.Core.Services.Projects;
using RAWSelectionAssistant.Services;
using RAWSelectionAssistant.Views;
using static RAWSelectionAssistant.WpfTests.RuntimeCorrectionWpfTests;
namespace RAWSelectionAssistant.WpfTests;
[TestClass]
public sealed class PlanningR7ContinuousTests
{
 private static void Save(FrameworkElement view,string path,int width=1200,int height=900)
 {view.Width=width;view.Height=height;view.Measure(new Size(width,height));view.Arrange(new Rect(0,0,width,height));view.UpdateLayout();var b=new RenderTargetBitmap(width,height,96,96,PixelFormats.Pbgra32);b.Render(view);var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(b));using var stream=File.Create(path);encoder.Save(stream);}
 private static IEnumerable<T> Walk<T>(DependencyObject root) where T:DependencyObject
 {foreach(var child in LogicalTreeHelper.GetChildren(root).OfType<DependencyObject>()){if(child is T t)yield return t;foreach(var nested in Walk<T>(child))yield return nested;}}
 [TestMethod]
 public Task TwentyFourShotsSearchPagesCancelFailUndoReopenAndDocx()=>RunSta(async()=>
 {
  EnsureTestApplication();using var f=await PlanningFoundationWpfTests.Fixture.Create();await PlanningRichFlowFixture.Populate(f);PlanningRichFlowFixture.SaveProposal(f.Vm,PlanningRichFlowFixture.Proposal());await f.Vm.FlushAsync();await f.Vm.SwitchPlanningModeAsync(true);
  var view=new PlanningCenterView{DataContext=f.Vm};var panel=(StackPanel)view.FindName("DocumentContent");var editor=panel.Children.OfType<PlanningRichEditor>().Single();var box=editor.Children.OfType<RichTextBox>().Single();
  var output=Environment.GetEnvironmentVariable("KITAO_R7_EVIDENCE");if(output is not null)Directory.CreateDirectory(output);
  var oldBody=f.Vm.DocumentBody;editor.OpenFind(true);if(output is not null){Save(view,Path.Combine(output,"find-wide.png"),1720,1050);Save(view,Path.Combine(output,"find-narrow.png"),900,900);}
  Assert.IsGreaterThan(0,editor.ReplaceBody("现场","片场",false,false,true));await f.Vm.FlushAsync();StringAssert.Contains(f.Vm.DocumentBody,"片场");Assert.HasCount(24,f.Vm.Shots);
  System.Windows.Input.ApplicationCommands.Undo.Execute(null,editor);await f.Vm.FlushAsync();Assert.AreEqual(oldBody,f.Vm.DocumentBody);
  var fields=new PlanningHeaderFooter{Header="R7 合成摄影策划",Footer="内部审阅",FirstHeader="客户提案首页",FirstFooter="合成资料",DifferentFirstPage=true,PageNumbers=true,StartNumber=3};editor.ApplyHeaderFooter(fields);Assert.IsTrue(await f.Vm.FlushAsync());
  var pageButton=Walk<Button>(editor).Single(b=>Equals(b.Content,"页眉页脚 / 页码"));pageButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));if(output is not null)Save(view,Path.Combine(output,"page-settings.png"),1366,1000);
  Walk<Button>(editor).Last(b=>Equals(b.Content,"取消")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));Assert.AreEqual(fields,f.Vm.RichLayout!.HeaderFooter);
  var file=Path.Combine(f.Root,"planning",$"{f.Id:N}.planning.json");var bytes=await File.ReadAllBytesAsync(file);
  using(var held=new FileStream(file,FileMode.Open,FileAccess.ReadWrite,FileShare.None)){editor.ApplyHeaderFooter(fields with{Footer="失败草稿"});Assert.IsFalse(await f.Vm.FlushAsync());}
  CollectionAssert.AreEqual(bytes,await File.ReadAllBytesAsync(file));Assert.AreEqual("失败草稿",f.Vm.RichLayout!.HeaderFooter.Footer);Assert.IsTrue(await f.Vm.FlushAsync());System.Windows.Input.ApplicationCommands.Undo.Execute(null,editor);Assert.IsTrue(await f.Vm.FlushAsync());Assert.AreEqual(fields,f.Vm.RichLayout!.HeaderFooter);
  var docx=Path.Combine(f.Root,"r7.docx");Assert.IsTrue(await f.Vm.ExportPlanningDocxAsync(docx));var review=f.Vm.PreviewPlanningDocxImport(docx);Assert.AreEqual(fields,review.Layout.HeaderFooter);Assert.IsTrue(await f.Vm.ApplyPlanningDocxImportAsync(review,[],true));editor.FlushPending();Assert.AreEqual(fields,f.Vm.RichLayout!.HeaderFooter);
  view.DataContext=null;var reopened=f.NewVm();await reopened.LoadAsync(f.Id);Assert.AreEqual(fields,reopened.RichLayout!.HeaderFooter);Assert.HasCount(24,reopened.Shots);Assert.HasCount(22,reopened.ResourceCatalog.Items);
  var document=PlanningDocumentLayout.Load(reopened.RichTextPackage!,reopened.RichLayout);var paginator=new PlanningPaginator(document);Assert.IsGreaterThanOrEqualTo(5,paginator.PageCount);
  if(output is not null)
  {
   for(var n=0;n<Math.Min(3,paginator.PageCount);n++){var page=new System.Windows.Controls.Primitives.DocumentPageView{DocumentPaginator=paginator,PageNumber=n};Save(page,Path.Combine(output,$"page-{n+1}.png"),794,1123);}
   PlanningProposalPdf.ExportDocument(PlanningDocumentLayout.Load(reopened.RichTextPackage!,reopened.RichLayout),Path.Combine(output,"r7.pdf"),96);File.Copy(docx,Path.Combine(output,"r7.docx"),true);
   await File.WriteAllTextAsync(Path.Combine(output,"flow.json"),JsonSerializer.Serialize(new{shots=24,resources=22,pages=paginator.PageCount,headers=fields,windows="NOT_RUN",wps="NOT_RUN",scope="IN_PROCESS_SYNTHETIC"},new JsonSerializerOptions{WriteIndented=true}));
  }
 });
}
