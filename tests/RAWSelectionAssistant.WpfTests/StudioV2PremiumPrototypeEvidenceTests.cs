using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using RAWSelectionAssistant.ViewModels;

namespace RAWSelectionAssistant.WpfTests;

[TestClass]
[DoNotParallelize]
public sealed class StudioV2PremiumPrototypeEvidenceTests
{
    [TestMethod]
    public void PremiumPrototype_ThreeRealPages_RenderReviewSet()
    {
        if (Environment.GetEnvironmentVariable("PIXEL_TART_STUDIO_V2_EVIDENCE") != "1")
        {
            Assert.Inconclusive("Opt-in visual evidence is not enabled in this bounded regression run.");
            return;
        }

        Exception? failure = null;
        var complete = false;
        var thread = new Thread(() =>
        {
            try
            {
                var rootPath = Environment.GetEnvironmentVariable("PIXEL_TART_ACCEPTANCE_ROOT")
                    ?? throw new InvalidOperationException("PIXEL_TART_ACCEPTANCE_ROOT is required.");
                var output = Environment.GetEnvironmentVariable("PIXEL_TART_STUDIO_V2_OUTPUT")
                    ?? Path.Combine(rootPath, "studio-v2-premium-prototype");
                Directory.CreateDirectory(output);
                void Stage(string value) => File.WriteAllText(Path.Combine(output, "evidence-stage.txt"), value);
                Stage("app-init");
                var app = new App(); app.InitializeComponent();
                var active = false;
                var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(200) };
                timer.Tick += async (_, _) =>
                {
                    if (active || app.MainWindow is not MainWindow { IsLoaded: true, DataContext: MainViewModel vm } window) return;
                    active = true;
                    try
                    {
                        Stage("window-ready");
                        vm.ForceExitTutorial();
                        vm.Settings.AssetLibraryPortable.AutoFocusWorkspace = false;
                        window.WindowState = WindowState.Normal;
                        window.MinWidth = 0; window.MinHeight = 0;
                        await Arrange(window, 1920, 1080, 1);

                        var fixtureDirectory = Path.Combine(rootPath, "SyntheticAssets");
                        Directory.CreateDirectory(fixtureDirectory);
                        for (var i = 0; i < 12; i++) CreateFixture(Path.Combine(fixtureDirectory, $"premium-shot-{i + 1:00}.png"), i);

                        Stage("workbench");
                        vm.NavigateCommand.Execute("Workbench");
                        await Capture(window, output, "workbench-default.png");
                        await Capture(window, output, "workbench-content.png");
                        vm.SelectedDensity = RAWSelectionAssistant.Core.Models.InterfaceDensity.Compact;
                        await Capture(window, output, "workbench-compact.png");
                        vm.SelectedDensity = RAWSelectionAssistant.Core.Models.InterfaceDensity.Comfortable;

                        Stage("asset-start");
                        vm.NavigateCommand.Execute("AssetLibrary"); await Task.Delay(450);
                        var host = Descendants<PixelTart.Modules.AssetLibrary.AssetLibraryWorkspaceHost>(window).Single();
                        var containerService = new RAWSelectionAssistant.Core.Services.AssetLibrary.AssetLibraryContainerService();
                        var containerPath = Path.Combine(rootPath, "StudioV2VisualLibrary.ptlibrary");
                        var container = Directory.Exists(containerPath)
                            ? await containerService.OpenAsync(containerPath)
                            : await containerService.CreateAsync(containerPath, "Studio v2 视觉素材库");
                        Stage("asset-switch");
                        await host.SwitchToContainerAsync(container.ContainerPath);
                        Stage("asset-import");
                        await host.CurrentPage!.ViewModel.ImportDemoDirectoryAsync(fixtureDirectory);
                        await Capture(window, output, "asset-grid.png");
                        var gallery = Descendants<ListBox>(host).Single(x => x.Name == "AssetGrid");
                        gallery.SelectedIndex = 0; await Capture(window, output, "asset-selected.png");
                        await Capture(window, output, "asset-inspector.png");

                        Stage("reference-start");
                        vm.NavigateCommand.Execute("ReferenceColor"); await Task.Delay(400);
                        await vm.ReferenceColorPage.LoadTargetAsync(Path.Combine(fixtureDirectory, "premium-shot-01.png"));
                        var reference = new BitmapImage(new Uri(Path.Combine(fixtureDirectory, "premium-shot-02.png")));
                        reference.Freeze();
                        await vm.ReferenceColorPage.AcceptContextAsync(null, null, reference);
                        vm.ReferenceColorPage.Editor.WorkspaceMode = "简洁";
                        vm.ReferenceColorPage.Editor.WorkspaceSection = "仿色";
                        await Capture(window, output, "reference-simple.png");
                        vm.ReferenceColorPage.Editor.WorkspaceMode = "专业";
                        await Capture(window, output, "reference-pro.png");
                        await StudioVisualEvidence.AccentComparison((FrameworkElement)window.Content, output);
                        vm.ReferenceColorPage.Editor.WorkspaceSection = "胶片";
                        vm.ReferenceColorPage.Editor.FilmEnabled = true;
                        vm.ReferenceColorPage.Editor.FilmGrainAmount = 35;
                        vm.ReferenceColorPage.Editor.FilmHalationAmount = 28;
                        vm.ReferenceColorPage.Editor.FilmBloomAmount = 22;
                        vm.ReferenceColorPage.Editor.FilmTextureId = "Paper";
                        vm.ReferenceColorPage.Editor.FilmSurfaceAmount = 32;
                        vm.ReferenceColorPage.Editor.FilmTextureAmount = 45;
                        await vm.ReferenceColorPage.Editor.ApplyCommand.ExecuteAsync(null);
                        await Capture(window, output, "reference-film.png");

                        vm.ReferenceColorPage.Editor.WorkspaceSection = "仿色";
                        Stage("dpi");
                        foreach (var scale in new[] { 1d, 1.25d, 1.5d, 2d })
                        {
                            await Arrange(window, 1920, 1080, scale);
                            await Capture(window, output, $"reference-dpi-{(int)(scale * 100)}.png", scale);
                        }
                        Stage("complete");
                    }
                    catch (Exception exception)
                    {
                        failure = exception;
                        Stage("failed-" + exception.GetType().Name);
                    }
                    finally
                    {
                        timer.Stop(); complete = true; app.Shutdown();
                    }
                };
                timer.Start(); app.Run();
            }
            catch (Exception exception) { failure = exception; complete = true; }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        Assert.IsTrue(thread.Join(TimeSpan.FromMinutes(8)), "Premium prototype evidence timed out.");
        Assert.IsTrue(complete);
        if (failure is not null) throw new AssertFailedException(failure.ToString());
    }

    private static async Task Arrange(Window window, int pixelsWide, int pixelsHigh, double scale)
    {
        var root = (FrameworkElement)window.Content;
        window.Width = pixelsWide / scale; window.Height = pixelsHigh / scale;
        await window.Dispatcher.InvokeAsync(window.UpdateLayout, DispatcherPriority.ApplicationIdle);
        root.Width = pixelsWide / scale; root.Height = pixelsHigh / scale;
        root.Measure(new Size(root.Width, root.Height)); root.Arrange(new Rect(0, 0, root.Width, root.Height));
        await window.Dispatcher.InvokeAsync(root.UpdateLayout, DispatcherPriority.ApplicationIdle);
        if (window.DataContext is MainViewModel vm) vm.UpdateSidebarForWidth(root.Width);
        root.Measure(new Size(root.Width, root.Height)); root.Arrange(new Rect(0, 0, root.Width, root.Height));
        await window.Dispatcher.InvokeAsync(root.UpdateLayout, DispatcherPriority.ApplicationIdle);
        await Task.Delay(160);
    }

    private static async Task Capture(Window window, string output, string name, double scale = 1)
    {
        var root = (FrameworkElement)window.Content;
        await window.Dispatcher.InvokeAsync(root.UpdateLayout, DispatcherPriority.ApplicationIdle);
        await Task.Delay(180);
        StudioVisualEvidence.AssertNoShellCloseCollision(root, name, output);
        StudioVisualEvidence.AuditGeometry(root, name, scale, output);
        StudioVisualEvidence.Png(root, Path.Combine(output, name), scale);
    }

    private static void CreateFixture(string path, int index)
    {
        if (File.Exists(path)) return;
        const int width = 1200, height = 800;
        var visual = new DrawingVisual();
        using (var dc = visual.RenderOpen())
        {
            var palettes = new[] { (42, 35, 52), (58, 44, 71), (73, 49, 79), (35, 50, 60) };
            var p = palettes[index % palettes.Length];
            dc.DrawRectangle(new SolidColorBrush(Color.FromRgb((byte)p.Item1, (byte)p.Item2, (byte)p.Item3)), null, new Rect(0, 0, width, height));
            dc.DrawEllipse(new SolidColorBrush(Color.FromRgb((byte)(178 + index * 4), (byte)(126 + index * 3), 118)), null, new Point(465, 405), 250, 315);
            dc.DrawRectangle(new SolidColorBrush(Color.FromArgb(210, 220, 183, 130)), null, new Rect(760, 80, 175, 640));
            dc.DrawText(new FormattedText($"PIXEL TART / FRAME {index + 1:00}", System.Globalization.CultureInfo.InvariantCulture,
                FlowDirection.LeftToRight, new Typeface("Segoe UI Variable"), 24, Brushes.White, 1), new Point(28, 26));
        }
        var bitmap = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32); bitmap.Render(visual);
        var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var stream = File.Create(path); encoder.Save(stream);
    }

    private static IEnumerable<T> Descendants<T>(DependencyObject root) where T : DependencyObject
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);
            if (child is T match) yield return match;
            foreach (var nested in Descendants<T>(child)) yield return nested;
        }
    }
}
