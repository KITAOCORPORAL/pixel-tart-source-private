using System.IO;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using RAWSelectionAssistant.Core.Models;
using RAWSelectionAssistant.Services;
using RAWSelectionAssistant.ViewModels;
using RAWSelectionAssistant.Views;

namespace RAWSelectionAssistant.WpfTests;

[TestClass]
public sealed class ReferenceWorkspaceDpiEvidenceTests
{
    [TestMethod]
    public void FiveHundredTargetsRealizeOnlyVisibleFilmstripContainers()
    {
        Exception? error = null;
        var thread = new Thread(() =>
        {
            try
            {
                if (Application.Current is null) { var app = new App(); app.InitializeComponent(); }
                using var workspace = new ReferenceColorWorkspaceViewModel(new EvidenceDialogs());
                foreach (var index in Enumerable.Range(0, 500)) workspace.Targets.Add(new ReferenceTargetItem($"{index:000}.jpg"));
                var view = new ReferenceColorWorkspaceView { DataContext = workspace, Width = 1180, Height = 720 };
                var clock = Stopwatch.StartNew();
                view.Measure(new Size(1180, 720)); view.Arrange(new Rect(0, 0, 1180, 720)); view.UpdateLayout();
                clock.Stop();
                var realized = Count<ListBoxItem>(view);
                Assert.IsLessThan(30, realized, $"realized={realized} of 500");
                TestContext?.WriteLine($"filmstrip500_layout_ms={clock.Elapsed.TotalMilliseconds:F1}; realized_containers={realized}; total=500; process_peak_mb={Process.GetCurrentProcess().PeakWorkingSet64 / 1048576d:F2}");
            }
            catch (Exception exception) { error = exception; }
        });
        thread.SetApartmentState(ApartmentState.STA); thread.Start(); Assert.IsTrue(thread.Join(TimeSpan.FromSeconds(20)));
        if (error is not null) throw error;
    }
    public TestContext? TestContext { get; set; }

    private static int Count<T>(DependencyObject root) where T : DependencyObject
    {
        var count = root is T ? 1 : 0;
        for (var index = 0; index < VisualTreeHelper.GetChildrenCount(root); index++) count += Count<T>(VisualTreeHelper.GetChild(root, index));
        return count;
    }

    [TestMethod]
    [TestCategory("VisualEvidence")]
    public void CurrentReferenceWorkspaceRendersAtFourScalesAndNarrowResolution()
    {
        var output = Environment.GetEnvironmentVariable("PIXEL_TART_PHOTOGRAPHY_DPI_OUTPUT");
        if (string.IsNullOrWhiteSpace(output)) Assert.Inconclusive("Set an isolated current-run output directory.");
        Directory.CreateDirectory(output);
        Exception? error = null;
        var thread = new Thread(() =>
        {
            try
            {
                if (Application.Current is null) { var app = new App(); app.InitializeComponent(); }
                using var workspace = new ReferenceColorWorkspaceViewModel(new EvidenceDialogs());
                var thumbnail = BitmapSource.Create(64, 48, 96, 96, PixelFormats.Bgra32, null,
                    Enumerable.Repeat(new byte[] { 85, 105, 145, 255 }, 64 * 48).SelectMany(pixel => pixel).ToArray(), 64 * 4);
                thumbnail.Freeze();
                for (var index = 0; index < 6; index++)
                    workspace.Targets.Add(new ReferenceTargetItem($"拍摄_{index + 1:00}.jpg")
                    { Thumbnail = thumbnail, Rating = index % 5 + 1, IsSelected = index is 1 or 2, IsActive = index == 1 });
                var view = new ReferenceColorWorkspaceView { DataContext = workspace };
                foreach (var percent in new[] { 100, 125, 150, 200 })
                    Capture(view, Path.Combine(output!, $"ReferenceBatch-1180x720-{percent}.png"), 1180, 720, percent / 100d);
            }
            catch (Exception exception) { error = exception; }
        });
        thread.SetApartmentState(ApartmentState.STA); thread.Start();
        Assert.IsTrue(thread.Join(TimeSpan.FromSeconds(40)), "In-process DPI render exceeded 40 seconds.");
        if (error is not null) throw error;
        foreach (var percent in new[] { 100, 125, 150, 200 })
            Assert.IsTrue(File.Exists(Path.Combine(output!, $"ReferenceBatch-1180x720-{percent}.png")));
    }

    private static void Capture(FrameworkElement view, string path, int width, int height, double scale)
    {
        view.Width = width; view.Height = height;
        view.Measure(new Size(width, height)); view.Arrange(new Rect(0, 0, width, height)); view.UpdateLayout();
        var bitmap = new RenderTargetBitmap((int)(width * scale), (int)(height * scale), 96 * scale, 96 * scale, PixelFormats.Pbgra32);
        bitmap.Render(view); bitmap.Freeze();
        var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var file = File.Create(path); encoder.Save(file);
    }

    private sealed class EvidenceDialogs : IDialogService
    {
        public string? ChooseFolder(string title, string? initialDirectory = null) => null;
        public IReadOnlyList<string> ChooseFiles(string title, string filter, bool multiselect = true) => [];
        public string? ChooseSaveFile(string title, string filter, string defaultExtension, string? suggestedFileName = null) => null;
        public IReadOnlyList<string>? ManageQuickTools(IReadOnlyList<string> currentToolIds) => null;
        public void ShowInfo(string message) { }
        public void ShowError(string message) { }
        public bool Confirm(string message, string title) => false;
        public HelpAction ShowHelp() => HelpAction.None;
        public void ShowFeedback() { }
        public RawFileEntry? ChooseRawCandidate(IReadOnlyList<RawFileEntry> candidates) => null;
        public bool ShowMediaDetails(MediaSelectionItem item, bool showAdvancedDetails) => false;
        public void RevealFile(string path) { }
    }
}
