using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using RAWSelectionAssistant.Services;
using RAWSelectionAssistant.ViewModels;
using RAWSelectionAssistant.Views;
using static RAWSelectionAssistant.WpfTests.RuntimeCorrectionWpfTests;

namespace RAWSelectionAssistant.WpfTests;

/// <summary>Opt-in real view render snapshots, explicitly NOT production EXE pointer acceptance.</summary>
[TestClass]
public sealed class StudioRebuildVisualEvidenceTests
{
    [TestMethod]
    public Task RenderCurrentStudioWithSyntheticPhotoAtDocumentedLayoutSizes() => RunSta(async () =>
    {
        var output = Environment.GetEnvironmentVariable("PIXEL_TART_REBUILD_VISUAL_OUTPUT");
        if (string.IsNullOrWhiteSpace(output)) Assert.Inconclusive("Opt-in automatic screenshot evidence; not native UI acceptance.");
        Directory.CreateDirectory(output!); EnsureTestApplication();
        var root = Path.Combine(Path.GetTempPath(), "Studio-public-chart-" + Guid.NewGuid()); Directory.CreateDirectory(root);
        try
        {
            var bytes = new byte[800 * 600 * 3];
            for (var y = 0; y < 600; y++) for (var x = 0; x < 800; x++)
            {
                var offset = (y * 800 + x) * 3;
                bytes[offset] = (byte)(x * 255 / 799); bytes[offset + 1] = (byte)(y * 255 / 599); bytes[offset + 2] = (byte)((799 - x) * 255 / 799);
            }
            var source = BitmapSource.Create(800, 600, 96, 96, PixelFormats.Rgb24, null, bytes, 2400); source.Freeze();
            var path = Path.Combine(root, "PUBLIC_SYNTHETIC_RGB.png"); StudioQuickExport.Encode(source, path, path);
            using var workspace = new ReferenceColorWorkspaceViewModel(new Dialogs()); await workspace.LoadTargetAsync(path);
            var view = new ReferenceColorWorkspaceView { DataContext = workspace };
            view.Measure(new Size(1600, 920)); view.Arrange(new Rect(0, 0, 1600, 920)); view.UpdateLayout();
            var reference = view.FindName("ReferencePage") as FrameworkElement;
            var navigator = Descendants<ReferenceNavigator>(reference!).First();
            navigator.SourcePath = path; await navigator.LoadingTask;
            for (var attempt = 0; attempt < 100 && workspace.PreviewHistogram is null; attempt++) await Task.Delay(20);
            Assert.IsNotNull(workspace.PreviewHistogram);
            Assert.IsNotNull(navigator.Image);
            foreach (var (width, height) in new[] { (1180, 720), (1600, 920), (1920, 1080) })
                foreach (var mode in new[] { 0, 1, 2, 3, 4 })
                {
                    ((ListBox)view.FindName("ToolModes")).SelectedIndex = mode;
                    foreach (var group in Descendants<Expander>((FrameworkElement)view.FindName("ToolPages"))) group.IsExpanded = true;
                    view.Width = width; view.Height = height; view.Measure(new Size(width, height)); view.Arrange(new Rect(0, 0, width, height)); view.UpdateLayout();
                    Assert.IsGreaterThan(200, ((FrameworkElement)view.FindName("PreviewCanvas")).ActualWidth);
                    foreach (var scale in new[] { 1d, 1.25, 1.5, 2d })
                    {
                        var bitmap = new RenderTargetBitmap((int)(width * scale), (int)(height * scale), 96 * scale, 96 * scale, PixelFormats.Pbgra32); bitmap.Render(view);
                        var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
                        using var stream = File.Create(Path.Combine(output!, $"AUTO_{width}x{height}dip_mode{mode}_{scale * 100:0}percent.png")); encoder.Save(stream);
                    }
                }
        }
        finally { Directory.Delete(root, true); }
    });
    private static IEnumerable<T> Descendants<T>(DependencyObject root) where T : DependencyObject
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);
            if (child is T value) yield return value;
            foreach (var nested in Descendants<T>(child)) yield return nested;
        }
    }
    private sealed class Dialogs : IDialogService
    {
        public string? ChooseFolder(string title, string? initialDirectory = null) => null;
        public IReadOnlyList<string> ChooseFiles(string title, string filter, bool multiselect = true) => [];
        public string? ChooseSaveFile(string title, string filter, string defaultExtension, string? suggestedFileName = null) => null;
        public IReadOnlyList<string>? ManageQuickTools(IReadOnlyList<string> currentToolIds) => null;
        public void ShowInfo(string message) { } public void ShowError(string message) { }
        public bool Confirm(string message, string title) => false; public HelpAction ShowHelp() => HelpAction.None; public void ShowFeedback() { }
        public RAWSelectionAssistant.Core.Models.RawFileEntry? ChooseRawCandidate(IReadOnlyList<RAWSelectionAssistant.Core.Models.RawFileEntry> candidates) => null;
        public bool ShowMediaDetails(RAWSelectionAssistant.Core.Models.MediaSelectionItem item, bool showAdvancedDetails) => false;
        public void RevealFile(string path) { }
    }
}
