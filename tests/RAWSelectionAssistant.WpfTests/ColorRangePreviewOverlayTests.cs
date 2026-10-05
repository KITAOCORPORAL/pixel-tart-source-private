using System.Windows.Media;
using System.Windows.Media.Imaging;
using RAWSelectionAssistant.Core.Services.AssetLibrary.VisualAnalysis;
using RAWSelectionAssistant.Core.Services.Color;
using RAWSelectionAssistant.Core.Services.Projects;
using RAWSelectionAssistant.Services;
using RAWSelectionAssistant.ViewModels;
using static RAWSelectionAssistant.WpfTests.RuntimeCorrectionWpfTests;

namespace RAWSelectionAssistant.WpfTests;

[TestClass]
public sealed class ColorRangePreviewOverlayTests
{
    [TestMethod]
    public Task RangeMaskReadsPrefixWhileHistogramImageAndExportRemainProcessed() => RunSta(async () =>
    {
        using var workspace = new ReferenceColorWorkspaceViewModel(new Dialogs());
        var source = new VisualPixelBuffer(2, 1, new byte[] { 255, 0, 0, 0, 0, 255 });
        var prefix = new ColorAdjustmentStackNode(Guid.NewGuid(), ColorStudioNodeType.BasicTone, "去色", NumericParameters: new Dictionary<string, double> { ["saturation"] = -100 });
        var prefixStack = new ColorAdjustmentStack([prefix], ProcessingVersion: 2);
        var gray = new ColorStudioRenderPipeline().Render(source, ColorStudioProcessingAnalysis.Create(source, prefixStack, null), null, prefixStack).Pixels;
        var sample = new VisualRgb24(gray.Rgb24.Span[0], gray.Rgb24.Span[1], gray.Rgb24.Span[2]);
        var range = new ColorAdjustmentStackNode(Guid.NewGuid(), ColorStudioNodeType.ColorRange, "灰域", NumericParameters: new Dictionary<string, double> { ["range"] = .012, ["softness"] = .003, ["lightness"] = 20 }, Samples: [sample]);
        Assert.AreEqual((byte)0, ColorStudioRenderPipeline.SelectionWeights(source, range)[0], "Before prefix the red source is outside the gray range.");
        var stack = new ColorAdjustmentStack([prefix, range], ProcessingVersion: 2);
        workspace.Editor.ApplyTargetSnapshot(null, null, stack, false);
        await workspace.Editor.SetSourceAsync(Guid.NewGuid(), Image(source));
        workspace.Editor.SelectedAdjustmentNode = workspace.Editor.AdjustmentNodes[1];
        await workspace.RefreshPreviewAnalysisAsync();
        var processed = workspace.Editor.MatchedImage!; var histogram = workspace.PreviewHistogram;
        var exportBefore = ColorStudioBitmapRenderer.Render(Image(source), stack, null);
        workspace.Editor.ShowSelection = true; await workspace.RefreshRangeSelectionAsync();
        Assert.AreEqual((byte)255, workspace.HighlightWeights[0], "The overlay must use the selected node input, after the earlier desaturation.");
        Assert.AreSame(processed, workspace.Editor.MatchedImage); Assert.AreSame(histogram, workspace.PreviewHistogram);
        CollectionAssert.AreEqual(Bytes(exportBefore), Bytes(ColorStudioBitmapRenderer.Render(Image(source), workspace.Editor.AdjustmentStack, null)));
        Assert.IsTrue(await workspace.CompleteRangeSampleAsync(.25, .5));
        Assert.AreEqual(sample, workspace.Editor.SelectedAdjustmentNode!.Samples[^1], "Picking the shifted displayed gray must save its pre-range input gray.");
        var pending = workspace.RefreshRangeSelectionAsync(); workspace.ClearPreviewSelection(); await pending;
        Assert.IsFalse(workspace.Editor.ShowSelection); Assert.IsEmpty(workspace.HighlightWeights);
        await workspace.Editor.SetSourceAsync(Guid.NewGuid(), Image(source));
        await workspace.RefreshPreviewAnalysisAsync();
        workspace.Editor.ShowSelection = true;
        var cloudRace = workspace.RefreshRangeSelectionAsync();
        workspace.HighlightImageSample(new(0, 0, 255));
        var cloudWeights = workspace.HighlightWeights;
        await cloudRace;
        Assert.IsFalse(workspace.Editor.ShowSelection);
        Assert.AreSame(cloudWeights, workspace.HighlightWeights, "Pending range work must not override a subsequent cloud inspection.");
        var count=workspace.Editor.SelectedAdjustmentNode!.Samples.Count;
        workspace.Editor.StartSamplingCommand.Execute(null);
        var samplePending=workspace.CompleteRangeSampleAsync(.25,.5);
        workspace.Editor.CancelSamplingCommand.Execute(null);workspace.ClearPreviewSelection();
        Assert.IsFalse(await samplePending);
        Assert.AreEqual(count,workspace.Editor.SelectedAdjustmentNode!.Samples.Count,"Esc must not allow a late asynchronous sample to dirty the stack.");
    });

    [TestMethod]
    public Task RawRangeMaskUsesFloatMasterAndTargetChangeCancelsOldMask() => RunSta(async () =>
    {
        using var workspace = new ReferenceColorWorkspaceViewModel(new Dialogs());
        var range = new ColorAdjustmentStackNode(Guid.NewGuid(), ColorStudioNodeType.ColorRange, "蓝色", NumericParameters: new Dictionary<string, double> { ["range"] = .02, ["softness"] = .01 }, Samples: [new(0, 0, 255)]);
        workspace.Editor.ApplyTargetSnapshot(null, null, new([range], ProcessingVersion: 2), false);
        // Deliberately mismatched fixture proves that the RAW route reads its actual
        // float processing master, never the display BitmapSource as a substitute.
        var redDisplay = Image(new(2, 1, new byte[] { 255, 0, 0, 255, 0, 0 }));
        var master = new HighBitDepthImageBuffer(2, 1, new float[] { 0, 0, 1, 1, 0, 0 });
        await workspace.Editor.SetSourceAsync(Guid.NewGuid(), redDisplay, rawPreviewMaster: master);
        await workspace.RefreshPreviewAnalysisAsync();
        workspace.Editor.ShowSelection = true; await workspace.RefreshRangeSelectionAsync();
        CollectionAssert.AreEqual(new byte[] { 255, 0 }, workspace.HighlightWeights);
        Assert.IsTrue(await workspace.CompleteRangeSampleAsync(.25, .5));
        Assert.AreEqual(new VisualRgb24(0, 0, 255), workspace.Editor.SelectedAdjustmentNode!.Samples[^1]);
        var stale = workspace.RefreshRangeSelectionAsync();
        await workspace.Editor.SetSourceAsync(Guid.NewGuid(), null); await stale; await workspace.RangeSelectionWork;
        Assert.IsEmpty(workspace.HighlightWeights); Assert.IsNull(workspace.PreviewHistogram);
    });
    private static BitmapSource Image(VisualPixelBuffer source)
    { var image = BitmapSource.Create(source.Width, source.Height, 96, 96, PixelFormats.Rgb24, null, source.Rgb24.ToArray(), source.Width * 3); image.Freeze(); return image; }
    private static byte[] Bytes(BitmapSource image)
    { var converted = new FormatConvertedBitmap(image, PixelFormats.Bgra32, null, 0); var bytes = new byte[converted.PixelWidth * converted.PixelHeight * 4]; converted.CopyPixels(bytes, converted.PixelWidth * 4, 0); return bytes; }
    private sealed class Dialogs : IDialogService
    {
        public IReadOnlyList<string> ChooseFiles(string title, string filter, bool multiselect) => [];
        public string? ChooseFolder(string title, string? initialDirectory = null) => null;
        public string? ChooseSaveFile(string title, string filter, string defaultExtension, string? suggestedFileName = null) => null;
        public IReadOnlyList<string>? ManageQuickTools(IReadOnlyList<string> currentToolIds) => currentToolIds;
        public void ShowInfo(string message) { } public void ShowError(string message) { }
        public bool Confirm(string message, string title) => false;
        public HelpAction ShowHelp() => HelpAction.None; public void ShowFeedback() { }
        public RAWSelectionAssistant.Core.Models.RawFileEntry? ChooseRawCandidate(IReadOnlyList<RAWSelectionAssistant.Core.Models.RawFileEntry> candidates) => null;
        public bool ShowMediaDetails(RAWSelectionAssistant.Core.Models.MediaSelectionItem item, bool showAdvancedDetails) => false;
        public void RevealFile(string path) { }
    }
}
