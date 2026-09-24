using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using RAWSelectionAssistant.Core.Services.Projects;
using RAWSelectionAssistant.Core.Services.AssetLibrary.VisualAnalysis;
using RAWSelectionAssistant.Core.Utilities;
using RAWSelectionAssistant.ViewModels;
using RAWSelectionAssistant.Views;
using System.Windows.Controls;
using System.Windows.Threading;
using System.Text.Json;
using System.Diagnostics;

namespace RAWSelectionAssistant.Services;

/// <summary>Opt-in synthetic data only; uses the production workspace, commands and renderer.</summary>
public static class ColorStudioAcceptanceFixture
{
    public const string Id = "color-studio-still-life-v1";
    public static bool Requested => Environment.GetCommandLineArgs().Contains("--acceptance-color-studio");
    public static void ValidateIsolation()
    {
        if (!Requested) return;
        if (Environment.GetEnvironmentVariable("PIXEL_TART_ISOLATED_RUNTIME") != "1" ||
            !Path.IsPathFullyQualified(Environment.GetEnvironmentVariable("PIXEL_TART_ISOLATED_RUNTIME_ROOT") ?? ""))
            throw new InvalidOperationException("Color Studio fixture requires an explicit isolated runtime root.");
    }
    public static async Task LoadAsync(MainViewModel main, Window window)
    {
        if (!Requested) return;
        ValidateIsolation();
        window.WindowState = WindowState.Normal; window.Width = 1600; window.Height = 1000;
        main.NavigateCommand.Execute("ReferenceColor");
        var workspace = main.ReferenceColorPage; await workspace.InitializeAsync();
        var folder = Path.Combine(AppDataPaths.Root, "ColorStudioFixture"); Directory.CreateDirectory(folder);
        var paths = new List<string>();
        for (var i = 0; i < 4; i++)
        {
            var path = Path.Combine(folder, $"静物-{i + 1}.png");
            CreateImage(path, i); paths.Add(path);
        }
        var reference = await new ReferenceLookPreviewService().AnalyzeExternalReferenceAsync(paths[3]);
        var now = DateTimeOffset.UtcNow;
        var look = new ReferenceLook(Guid.Parse("9b25f1cc-e999-451e-ad4f-d1f9d03d0e10"), "铜色静物", null,
            [reference with { Name = "暖光参考" }], new(MatchStrength: 65), now, now);
        var store = new ReferenceLookStore(Path.Combine(AppDataPaths.DataDirectory, "ProjectVisuals"));
        await store.SaveAsync(look);
        foreach (var path in paths.Take(3)) await workspace.LoadTargetAsync(path);
        await workspace.ActivateTargetCommand.ExecuteAsync(workspace.Targets[0]);
        var editor = workspace.Editor; editor.Looks.Add(look); editor.SelectedLook = look; editor.WorkspaceMode = "专业";
        editor.SelectedAdjustmentNode = editor.AdjustmentNodes.Single(n => n.Type == ColorStudioNodeType.ColorRange);
        editor.RenameSelectedAdjustmentNode("暖橙"); editor.RangeHue = 9; editor.RangeStrength = 72;
        editor.AddDisplayedSample(new VisualRgb24(191, 103, 56));
        editor.SampleMode = "减少取样"; editor.AddDisplayedSample(new VisualRgb24(65, 105, 113));
        editor.SampleMode = "增加取样"; editor.FilmEnabled = true; editor.FilmGrainAmount = 12;
        editor.ColorSchemeName = "暖调静物"; await editor.SaveColorSchemeAsCommand.ExecuteAsync(null);
        editor.CopyCurrentLookTo(workspace.Targets);
        foreach (var target in workspace.Targets) target.IsSelected = true;
        editor.WorkspaceSection = "调色"; editor.ViewMode = "仿色结果";
        await editor.ApplyCommand.ExecuteAsync(null);
        await Task.Delay(650);
        editor.SelectedAdjustmentNode = editor.AdjustmentNodes.Single(n => n.Type == ColorStudioNodeType.ColorRange);
        await PrepareScenarioAsync(workspace, window);
        window.UpdateLayout();
        var dpi = VisualTreeHelper.GetDpi(window);
        var args = Environment.GetCommandLineArgs();
        var scenario = args.FirstOrDefault(a => a.StartsWith("--studio-scenario="))?.Split('=')[1] ?? "01";
        await File.WriteAllTextAsync(Path.Combine(folder, "state.json"), JsonSerializer.Serialize(new
        {
            FixtureId = Id, Scenario = scenario, ProductSourceSha = StartupDiagnostics.ProductSourceSha,
            AppVersion = typeof(App).Assembly.GetName().Version?.ToString(), ProcessId = Environment.ProcessId,
            WindowSize = new[] { window.Width, window.Height }, LogicalDpi = scenario == "18" ? 192 : dpi.PixelsPerInchX,
            PhysicalDpi = dpi.PixelsPerInchX, LogicalDpiSimulation = scenario == "18",
            WorkspaceMode = editor.WorkspaceMode, SelectedNodeType = editor.SelectedAdjustmentNode?.Type.ToString(),
            ViewMode = editor.EffectiveViewMode, Target = workspace.TargetName, HasTarget = workspace.HasTarget,
            Stack = editor.AdjustmentNodes.Select(n => new { n.Name, Type = n.Type.ToString(), n.Enabled }),
            SourcePixelSize = new[] { editor.SourceImage!.PixelWidth, editor.SourceImage.PixelHeight },
            ProcessingStatus = editor.StatusText,
            MainWindowHandle = new System.Windows.Interop.WindowInteropHelper(window).Handle.ToInt64()
        }, new JsonSerializerOptions { WriteIndented = true }));
        await File.WriteAllTextAsync(Path.Combine(folder, "ready.txt"), $"{Id}\n{Environment.ProcessId}\n{window.Width}x{window.Height}");
    }
    private static async Task PrepareScenarioAsync(ReferenceColorWorkspaceViewModel workspace, Window window)
    {
        var scenario = Environment.GetCommandLineArgs().FirstOrDefault(a => a.StartsWith("--studio-scenario="))?.Split('=')[1] ?? "01";
        var editor = workspace.Editor;
        switch (scenario)
        {
            case "02": editor.SelectedAdjustmentNode = editor.AdjustmentNodes.First(); break;
            case "03": editor.SelectedAdjustmentNode = editor.AdjustmentNodes.First(n => n.Type == ColorStudioNodeType.ReferenceMatch); break;
            case "05": editor.StartSamplingCommand.Execute("增加取样"); break;
            case "07": editor.ShowSelection = true; break;
            case "08": editor.KeepOriginalLuminance = true; break;
            case "09":
                var range = editor.SelectedAdjustmentNode!;
                editor.InsertAdjustmentNode(range.Id, editor.AdjustmentNodes[0].Id, false); editor.SelectedAdjustmentNode = editor.AdjustmentNodes[0]; break;
            case "10": editor.RangeHue = 24; editor.UndoAdjustmentCommand.Execute(null); break;
            case "11": editor.WorkspaceMode = "简洁"; editor.MatchStrength = 48; editor.WorkspaceMode = "专业"; editor.SelectedAdjustmentNode = editor.AdjustmentNodes[0]; break;
            case "12": workspace.OpenNodeSyncCommand.Execute(null); break;
            case "13": editor.WorkspaceSection = "预设"; break;
            case "14":
                var render = editor.ApplyCommand.ExecuteAsync(null);
                editor.StopProcessing(); await render; break;
            case "15": editor.ViewMode = "左右对比"; break;
            case "16": editor.ToggleAddAdjustmentCommand.Execute(null); break;
            case "17": window.Width = 1180; window.Height = 720; break;
            case "18":
                window.Width = 1800; window.Height = 1200;
                if (window.Content is FrameworkElement root) root.LayoutTransform = new ScaleTransform(192 / VisualTreeHelper.GetDpi(window).PixelsPerInchX, 192 / VisualTreeHelper.GetDpi(window).PixelsPerInchY);
                break;
            case "19":
                editor.WorkspaceSection = "预设"; editor.RangeHue = 23;
                editor.RequestApplySchemeCommand.Execute(null); break;
            case "20":
                editor.WorkspaceSection = "预设"; editor.RequestDeleteSchemeCommand.Execute(null); break;
            case "21":
                workspace.OpenNodeSyncCommand.Execute(null);
                workspace.NodeSyncChoices[2].Selected = false;
                workspace.ConfirmNodeSyncCommand.Execute(null); break;
            case "22":
                editor.ViewMode = "并排对比"; break;
            case "23":
                window.Width = 1800; window.Height = 1200;
                if (window.Content is FrameworkElement dpiRoot) dpiRoot.LayoutTransform = new ScaleTransform(192 / VisualTreeHelper.GetDpi(window).PixelsPerInchX, 192 / VisualTreeHelper.GetDpi(window).PixelsPerInchY);
                workspace.OpenNodeSyncCommand.Execute(null); break;
        }
        await Task.Delay(1200); // Let delayed interactive/full-quality jobs start before checking idle.
        var deadline = Stopwatch.StartNew();
        while (editor.IsBusy && deadline.Elapsed < TimeSpan.FromSeconds(25)) await Task.Delay(50);
        if (editor.IsBusy) throw new TimeoutException("Fixture preview did not settle.");
        await window.Dispatcher.InvokeAsync(window.UpdateLayout, DispatcherPriority.Render);
        if (scenario is "05" or "06" or "07" or "08")
        {
            foreach (var check in Descendants<CheckBox>(window).Where(x => x.IsVisible && Equals(x.Content, "显示选区"))) check.BringIntoView();
        }
        if (scenario == "13")
            foreach (var text in Descendants<TextBlock>(window).Where(x => x.IsVisible && x.Text == "当前色彩方案")) text.BringIntoView();
        if (scenario is "06" or "08" or "10")
        {
            var rail = Descendants<FrameworkElement>(window).First(x => x.Name == "LeftRail");
            Descendants<ScrollViewer>(rail).First().ScrollToEnd();
        }
        if (scenario == "22")
        {
            var viewport = Descendants<ColorStudioImageViewport>(window).Single();
            viewport.State.SetZoom(1.25); viewport.State.PanBy(new Vector(60, -30));
        }
        if (scenario == "24")
        {
            var row = Descendants<ListBoxItem>(window).First(x => x.DataContext is ColorAdjustmentStackNode n && n.Type == ColorStudioNodeType.ColorRange);
            Descendants<Button>(row).Single(x => x.ContextMenu is not null).RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));
        }
    }
    private static IEnumerable<T> Descendants<T>(DependencyObject root) where T : DependencyObject
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        {
            var child = VisualTreeHelper.GetChild(root, i); if (child is T typed) yield return typed;
            foreach (var result in Descendants<T>(child)) yield return result;
        }
    }
    private static void CreateImage(string path, int variant)
    {
        const int width = 1200, height = 800;
        var visual = new DrawingVisual();
        using (var dc = visual.RenderOpen())
        {
            dc.DrawRectangle(new LinearGradientBrush(Color.FromRgb(36, 57, 65), Color.FromRgb(107, 142, 143), 10), null, new Rect(0, 0, width, height));
            dc.DrawRectangle(new SolidColorBrush(Color.FromRgb(162, 137, 110)), null, new Rect(0, 580, width, 220));
            dc.DrawEllipse(new SolidColorBrush(Color.FromArgb(90, 18, 27, 31)), null, new Point(600, 638), 340, 42);
            dc.DrawRoundedRectangle(new LinearGradientBrush(Color.FromRgb((byte)(183 + variant * 9), 100, 59), Color.FromRgb(97, 48, 36), 0), null, new Rect(370, 270, 225, 345), 75, 75);
            dc.DrawEllipse(new SolidColorBrush(Color.FromRgb(109, 62, 45)), null, new Point(482, 285), 86, 23);
            dc.DrawEllipse(new SolidColorBrush(Color.FromRgb(229, 186, 103)), null, new Point(750, 540), 105, 102);
            dc.DrawRoundedRectangle(new LinearGradientBrush(Color.FromRgb(238, 231, 208), Color.FromRgb(180, 177, 155), 0), null, new Rect(125, 445, 145, 177), 20, 20);
            var stem = new Pen(new SolidColorBrush(Color.FromRgb(51, 75, 49)), 9);
            dc.DrawLine(stem, new Point(480, 285), new Point(550, 107));
            dc.DrawEllipse(new SolidColorBrush(Color.FromRgb(110, 132, 82)), null, new Point(528, 154), 66, 21);
        }
        var bitmap = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32); bitmap.Render(visual); bitmap.Freeze();
        var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var stream = File.Create(path); encoder.Save(stream);
    }
}
