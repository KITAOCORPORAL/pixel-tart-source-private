using System.Windows;
using System.Windows.Documents;
using System.Windows.Media.Imaging;
using RAWSelectionAssistant.Core.Services.FreeCanvas;
using RAWSelectionAssistant.Services;
namespace RAWSelectionAssistant.WpfTests;
[TestClass]
public sealed class BatchTwoDocumentWpfTests
{
    [TestMethod]
    public void SavedCanvasPreviewAndNativePaginationAreReal()
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try
            {
                var canvasId=Guid.NewGuid();var png = PlanningCanvasSnapshot.Render(new CanvasDocument {CanvasId=canvasId, Objects = [new() {CanvasId=canvasId, Text = "可分享测试画布", Width = 300, Height = 100 }] });
                using var stream = new System.IO.MemoryStream(Convert.FromBase64String(png));
                var decoder = new PngBitmapDecoder(stream, BitmapCreateOptions.None, BitmapCacheOption.OnLoad);
                Assert.IsTrue(decoder.Frames[0].PixelWidth > 0);
                var package = new RAWSelectionAssistant.Core.Services.Projects.PlanningDocumentPackage { ProjectId=Guid.NewGuid() }
                    .CaptureCanvas(new CanvasDocument { Objects=[new(){Text="saved"}] },png);
                var saved=System.Text.Json.JsonSerializer.Serialize(package);
                var restored=System.Text.Json.JsonSerializer.Deserialize<RAWSelectionAssistant.Core.Services.Projects.PlanningDocumentPackage>(saved)!;
                Assert.AreEqual(png,restored.CanvasRevisions[0].PreviewPngBase64);
                Assert.Throws<System.IO.IOException>(()=>PlanningCanvasSnapshot.Render(new CanvasDocument {CanvasId=canvasId, Objects=[new(){CanvasId=canvasId,SourcePath="missing-synthetic-fixture.png"}] }));
                var document = new FlowDocument { PageWidth = 400, PageHeight = 500, PagePadding = new Thickness(20) };
                for (var i = 0; i < 150; i++) document.Blocks.Add(new Paragraph(new Run("分页验证 " + i)));
                var paginator = ((IDocumentPaginatorSource)document).DocumentPaginator;
                paginator.PageSize = new Size(400,500); paginator.ComputePageCount();
                Assert.IsTrue(paginator.PageCount > 1);
                Assert.AreNotSame(DocumentPage.Missing, paginator.GetPage(0));
            }
            catch (Exception ex) { failure = ex; }
        });
        thread.SetApartmentState(ApartmentState.STA); thread.Start(); thread.Join();
        if (failure is not null) throw failure;
    }
}
