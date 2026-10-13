using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using RAWSelectionAssistant.Core.Services.Projects;
using RAWSelectionAssistant.Services;
using RAWSelectionAssistant.ViewModels;
using RAWSelectionAssistant.Views;
using static RAWSelectionAssistant.WpfTests.RuntimeCorrectionWpfTests;

namespace RAWSelectionAssistant.WpfTests;

[TestClass]
public sealed class CanvasToolbarTests
{
    private static BitmapSource Image()
    {
        var image = BitmapSource.Create(2, 1, 96, 96, PixelFormats.Rgb24, null, new byte[] { 255, 0, 0, 0, 0, 255 }, 6);
        image.Freeze(); return image;
    }
    private static ReferenceColorWorkspaceView View(ReferenceColorWorkspaceViewModel workspace)
    { EnsureTestApplication(); return new() { DataContext = workspace }; }
    private static byte[] Pixels(BitmapSource image)
    { var b = new FormatConvertedBitmap(image, PixelFormats.Bgra32, null, 0); var data = new byte[b.PixelWidth * b.PixelHeight * 4]; b.CopyPixels(data, b.PixelWidth * 4, 0); return data; }
    private static ColorAdjustmentStack Stack() => new([new(Guid.NewGuid(), ColorStudioNodeType.ColorRange, "范围", NumericParameters: new Dictionary<string, double> { ["lightness"] = 20 })], ProcessingVersion: 2);

    [TestMethod]
    public Task ButtonsAndShortcutScopeAreRealAndNoImageIsDisabled() => RunSta(async () =>
    {
        using var w = new ReferenceColorWorkspaceViewModel(new Dialogs()); var v = View(w);
        Assert.IsFalse(v.SelectCanvasTool(ColorStudioCanvasTool.Hand));
        Assert.IsFalse(((ToggleButton)v.FindName("CanvasHandButton")).IsEnabled);
        await w.Editor.SetSourceAsync(Guid.NewGuid(), Image());
        var hand = (ToggleButton)v.FindName("CanvasHandButton");
        hand.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
        Assert.AreEqual(ColorStudioCanvasTool.Hand, v.ActiveCanvasTool); Assert.IsTrue(hand.IsChecked);
        Assert.AreEqual(Cursors.Hand, ((Grid)v.FindName("PreviewCanvas")).Cursor);
        Assert.IsTrue(v.CanvasShortcutScope((Grid)v.FindName("PreviewCanvas")));
        Assert.IsTrue(v.CanvasShortcutScope(hand));
        Assert.IsFalse(v.CanvasShortcutScope(new TextBox())); Assert.IsFalse(v.CanvasShortcutScope(new RichTextBox()));
        Assert.IsFalse(v.CanvasShortcutScope(new Slider())); Assert.IsFalse(v.CanvasShortcutScope((ListBox)v.FindName("Filmstrip")));
        Assert.IsFalse(v.ExecuteCanvasShortcut(Key.H, ModifierKeys.Control));
        Assert.IsFalse(v.ExecuteCanvasShortcut(Key.C, ModifierKeys.None), "No pretend crop command.");
        Assert.IsTrue(v.ExecuteCanvasShortcut(Key.V, ModifierKeys.None));
        Assert.AreEqual(ColorStudioCanvasTool.Select, v.ActiveCanvasTool);
        Assert.AreEqual(0, Grid.GetRow((UIElement)v.FindName("PhotoToolsBand")));
        Assert.AreEqual(1, Grid.GetRow((UIElement)v.FindName("PreviewCanvas")));
                w.Editor.IndependentRawMode = true;
        Assert.IsFalse(v.SelectCanvasTool(ColorStudioCanvasTool.ColorSample), "An independent PV4/5/6 definition cannot accept legacy range edits.");
        v.SelectCanvasTool(ColorStudioCanvasTool.Select);
        Assert.IsFalse(((Button)v.FindName("CanvasUndoButton")).IsEnabled);
        v.DataContext = null;
    });

    [TestMethod]
    public Task NavigationComparisonAndResetNeverMutateProcessingOrExport() => RunSta(async () =>
    {
        using var w = new ReferenceColorWorkspaceViewModel(new Dialogs());
        w.Editor.ApplyTargetSnapshot(null, null, Stack(), false); await w.Editor.SetSourceAsync(Guid.NewGuid(), Image());
        var v = View(w); var definition = JsonSerializer.Serialize(w.Editor.AdjustmentStack);
        var before = Pixels(ColorStudioBitmapRenderer.Render(Image(), w.Editor.AdjustmentStack, null));
        var state = ((ColorStudioImageViewport)v.FindName("ImageViewport")).State;
        state.Configure(new Size(500,400), new Size(1000,800));
        Assert.IsTrue(v.ExecuteCanvasShortcut(Key.Z, ModifierKeys.None));
        v.ZoomCanvasAt(new Point(250,200), false); Assert.AreEqual(.625, state.Zoom, 1e-9);
        v.ZoomCanvasAt(new Point(250,200), true); Assert.AreEqual(.5, state.Zoom, 1e-9);
        Assert.IsTrue(v.ExecuteCanvasShortcut(Key.D1, ModifierKeys.Control)); Assert.AreEqual(1 / VisualTreeHelper.GetDpi((ColorStudioImageViewport)v.FindName("ImageViewport")).DpiScaleX, state.Zoom, 1e-9);
        state.PanBy(new Vector(10,20));
        Assert.IsTrue(v.ExecuteCanvasShortcut(Key.D0, ModifierKeys.Control)); Assert.IsTrue(state.IsFit);
        w.Editor.ViewMode = "左右对比";
        v.SetCanvasOriginalHeld(true); Assert.AreEqual("原片", w.Editor.EffectiveViewMode);
        v.SetCanvasOriginalHeld(false); Assert.AreEqual("左右对比", w.Editor.EffectiveViewMode);
        ((ToggleButton)v.FindName("CanvasCompareButton")).RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
        Assert.AreEqual("原片", w.Editor.ViewMode);
        v.ToggleCanvasOriginal(); Assert.AreEqual("左右对比", w.Editor.ViewMode);
        v.SetCanvasOriginalHeld(true); v.ResetCanvasInteraction(); Assert.AreEqual("左右对比", w.Editor.EffectiveViewMode);
        Assert.AreEqual(definition, JsonSerializer.Serialize(w.Editor.AdjustmentStack));
        CollectionAssert.AreEqual(before, Pixels(ColorStudioBitmapRenderer.Render(Image(), w.Editor.AdjustmentStack, null)));
        v.DataContext = null;
    });

    [TestMethod]
    public Task SampleCancelHistorySessionReopenAndExportUseExistingPixelContract() => RunSta(async () =>
    {
        using var w = new ReferenceColorWorkspaceViewModel(new Dialogs()); w.Editor.WorkspaceMode = "专业";
        w.Editor.ApplyTargetSnapshot(null, null, Stack(), false); await w.Editor.SetSourceAsync(Guid.NewGuid(), Image());
        w.Editor.SelectedAdjustmentNode = w.Editor.AdjustmentNodes[0]; var v = View(w);
        var initial = JsonSerializer.Serialize(w.Editor.AdjustmentStack);
        Assert.IsTrue(v.SelectCanvasTool(ColorStudioCanvasTool.ColorSample));
        var pending = w.CompleteRangeSampleAsync(.25,.5);
        v.SelectCanvasTool(ColorStudioCanvasTool.Hand);
        Assert.IsFalse(await pending); Assert.AreEqual(initial, JsonSerializer.Serialize(w.Editor.AdjustmentStack));
        Assert.IsTrue(v.SelectCanvasTool(ColorStudioCanvasTool.ColorSample));
        Assert.IsTrue(await w.CompleteRangeSampleAsync(.25,.5));
        Assert.HasCount(1, w.Editor.SelectedAdjustmentNode!.Samples);
        Assert.AreEqual(ColorStudioCanvasTool.Select, v.ActiveCanvasTool);
        var sampled = JsonSerializer.Serialize(w.Editor.AdjustmentStack);
        Assert.AreNotEqual(initial, sampled);
        CollectionAssert.AreNotEqual(Pixels(ColorStudioBitmapRenderer.Render(Image(), Stack(), null)), Pixels(ColorStudioBitmapRenderer.Render(Image(), w.Editor.AdjustmentStack, null)), "Sampling changes real processed pixels, not only a UI marker.");
        Assert.IsTrue(w.Editor.UndoAdjustmentCommand.CanExecute(null)); w.Editor.UndoAdjustmentCommand.Execute(null);
        Assert.AreEqual(initial, JsonSerializer.Serialize(w.Editor.AdjustmentStack));
        w.Editor.RedoAdjustmentCommand.Execute(null); Assert.AreEqual(sampled, JsonSerializer.Serialize(w.Editor.AdjustmentStack));
        var path = Path.Combine(Path.GetTempPath(), "canvas-toolbar-" + Guid.NewGuid() + ".json");
        try
        {
            var id = Guid.NewGuid();
            await ColorStudioSessionStore.SaveAsync(path, new([new(id, "synthetic.jpg", null, true, null, w.Editor.AdjustmentStack, null)], id));
            var reopened = await ColorStudioSessionStore.LoadAsync(path);
            Assert.AreEqual(sampled, JsonSerializer.Serialize(reopened.Targets[0].Stack));
            CollectionAssert.AreEqual(Pixels(ColorStudioBitmapRenderer.Render(Image(), w.Editor.AdjustmentStack, null)), Pixels(ColorStudioBitmapRenderer.Render(Image(), reopened.Targets[0].Stack!, null)));
        }
        finally { File.Delete(path); v.DataContext = null; }
    });

    [TestMethod]
    public Task EscapeSourceChangeAndUnloadReleaseTransientState() => RunSta(async () =>
    {
        using var w = new ReferenceColorWorkspaceViewModel(new Dialogs());
        w.Editor.ApplyTargetSnapshot(null, null, Stack(), false); await w.Editor.SetSourceAsync(Guid.NewGuid(), Image());
        w.Editor.SelectedAdjustmentNode = w.Editor.AdjustmentNodes[0]; var v = View(w);
        v.SelectCanvasTool(ColorStudioCanvasTool.ColorSample); var pending = w.CompleteRangeSampleAsync(.25,.5);
        Assert.IsTrue(v.TryClearTransientInspection()); Assert.IsFalse(await pending); Assert.IsFalse(w.Editor.IsSampling);
        w.Editor.ViewMode = "仿色结果"; v.SetCanvasOriginalHeld(true);
        v.RaiseEvent(new RoutedEventArgs(FrameworkElement.UnloadedEvent));
        Assert.AreEqual("仿色结果", w.Editor.EffectiveViewMode);
        v.SelectCanvasTool(ColorStudioCanvasTool.Hand); await w.Editor.SetSourceAsync(Guid.NewGuid(), null);
        Assert.AreEqual(ColorStudioCanvasTool.Select, v.ActiveCanvasTool);
        Assert.IsFalse(((ToggleButton)v.FindName("CanvasHandButton")).IsEnabled);
        v.DataContext = null;
    });

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
