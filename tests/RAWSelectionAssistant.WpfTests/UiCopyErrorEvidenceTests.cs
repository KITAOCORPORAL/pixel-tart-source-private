using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using PixelTart.Modules.AssetLibrary;
using PixelTart.Modules.AssetLibrary.FreeCanvas;
using RAWSelectionAssistant.Core.Services.FreeCanvas;
using RAWSelectionAssistant.Core.Services.Projects;
using RAWSelectionAssistant.Views;
using static RAWSelectionAssistant.WpfTests.RuntimeCorrectionWpfTests;
namespace RAWSelectionAssistant.WpfTests;
[TestClass][DoNotParallelize]public sealed class UiCopyErrorEvidenceTests
{
    [TestMethod]public Task SyntheticOwnedMissingShowsShortStateAndRetainsDetails()=>RunSta(async()=>
    {
        var output=Environment.GetEnvironmentVariable("KITAO_COPY_ERROR_EVIDENCE");if(string.IsNullOrWhiteSpace(output)){Assert.Inconclusive("Explicit synthetic error evidence output required");return;}Directory.CreateDirectory(output);EnsureTestApplication();using var f=await PlanningFoundationWpfTests.Fixture.Create();await f.Vm.LoadAsync(f.Id);var file=Path.Combine(f.Root,"synthetic.png");await File.WriteAllBytesAsync(file,PlanningRichFlowFixture.ReferencePng(0));await f.Vm.AddDocumentReferencesAsync([new(Guid.NewGuid(),ShotReferenceKind.General,ExternalReference:file,Title:"合成参考")],true);await f.Vm.SendToCanvasAsync(f.Vm.AllProjectReferences.Single());var canvas=f.Vm.ProjectCanvases.Single();var copy=f.Vm.AllProjectReferences.Single().Link.ProjectCopy!;File.Delete(PlanningProjectCopyStorage.Resolve(copy));await f.Vm.RefreshDocumentReferencesAsync();var row=f.Vm.AllProjectReferences.Single();Assert.IsFalse(row.SourceAvailable);Assert.IsNull(row.PreviewPath);Assert.IsNotNull(row.PreviewDetails);Assert.IsFalse(row.Availability.Contains("Exception"));
        var view=new PlanningCenterView{DataContext=f.Vm};await f.Vm.SelectContentSafelyAsync("参考图");view.Render();Capture(view,Path.Combine(output,"planning-missing-owned-OFFSCREEN.png"),1400,850);view.DataContext=null;
        var surface=new FreeCanvasSurface(new CanvasEditor(canvas),new WpfAssetThumbnailProvider());await surface.LoadPreviewsAsync();Assert.IsEmpty(surface.LoadedPreviews);Assert.IsNotEmpty(surface.SourceErrors);Capture(surface,Path.Combine(output,"canvas-missing-owned-OFFSCREEN.png"),900,650);
    });
    static void Capture(FrameworkElement view,string path,int w,int h){view.Measure(new Size(w,h));view.Arrange(new Rect(0,0,w,h));view.UpdateLayout();var bitmap=new RenderTargetBitmap(w,h,96,96,PixelFormats.Pbgra32);bitmap.Render(view);using var f=File.Create(path);var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(bitmap));encoder.Save(f);}
}
