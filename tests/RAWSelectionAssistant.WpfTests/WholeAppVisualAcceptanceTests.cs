using System.IO;
using System.Security.Cryptography;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using RAWSelectionAssistant.ViewModels;

namespace RAWSelectionAssistant.WpfTests;

[TestClass]
[DoNotParallelize]
public sealed class WholeAppVisualAcceptanceTests
{
    [TestMethod]
    public void ProductionApp_AllRoutes_RenderReviewInventory()
    {
        Exception? failure = null;
        var complete = false;
        var thread = new Thread(() =>
        {
            try
            {
                Assert.AreEqual("1", Environment.GetEnvironmentVariable("PIXEL_TART_HUMAN_ACCEPTANCE"));
                var output = Environment.GetEnvironmentVariable("PIXEL_TART_WHOLE_APP_EVIDENCE");
                // Opt-in evidence location is required: never touch a real workspace.
                Assert.IsFalse(string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("PIXEL_TART_ACCEPTANCE_ROOT")));
                output ??= Path.Combine(Environment.GetEnvironmentVariable("PIXEL_TART_ACCEPTANCE_ROOT")!, "whole-app");
                Directory.CreateDirectory(output);
                var app = new App(); app.InitializeComponent();
                var active = false;
                var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(200) };
                timer.Tick += async (_, _) =>
                {
                    if (active || app.MainWindow is not MainWindow { IsLoaded: true, DataContext: MainViewModel vm } window) return;
                    active = true;
                    try
                    {
                        vm.ForceExitTutorial();
                        vm.Settings.AssetLibraryPortable.AutoFocusWorkspace = false;
                        var fixtureDirectory = Path.Combine(Environment.GetEnvironmentVariable("PIXEL_TART_ACCEPTANCE_ROOT")!, "SyntheticAssets");
                        Directory.CreateDirectory(fixtureDirectory);
                        for (var index = 0; index < 12; index++)
                        {
                            var (width, height) = (index % 4) switch { 0 => (1200, 800), 1 => (800, 1200), 2 => (1800, 500), _ => (500, 1800) };
                            var visual = new DrawingVisual();
                            using (var dc = visual.RenderOpen())
                            {
                                dc.DrawRectangle(new SolidColorBrush(Color.FromRgb((byte)(75 + index * 9), 105, (byte)(145 + index * 7))), null, new Rect(0, 0, width, height));
                                dc.DrawEllipse(Brushes.Coral, null, new Point(width * .4, height * .5), width * .23, height * .31);
                                dc.DrawRectangle(Brushes.Wheat, null, new Rect(width * .72, height * .12, width * .12, height * .64));
                                dc.DrawText(new FormattedText($"SYNTHETIC {index + 1:00} / {width} x {height}", System.Globalization.CultureInfo.InvariantCulture, FlowDirection.LeftToRight, new Typeface("Arial"), 24, Brushes.White, 1), new Point(20, 20));
                            }
                            var bitmap = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32); bitmap.Render(visual);
                            var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
                            using var stream = File.Create(Path.Combine(fixtureDirectory, $"demo-shot-{index + 1:00}.png")); encoder.Save(stream);
                        }
                        await RAWSelectionAssistant.Services.PlanningHumanAcceptanceDemoSeeder.SeedAsync(new RAWSelectionAssistant.Core.Services.SettingsService(new RAWSelectionAssistant.Core.Services.FileLogService()));
                        var planningStore = new RAWSelectionAssistant.Core.Services.Projects.PlanningProjectStore(Path.Combine(RAWSelectionAssistant.Core.Utilities.AppDataPaths.DataDirectory, "ProjectPlanning"));
                        var planning = await planningStore.LoadAsync(RAWSelectionAssistant.Services.PlanningHumanAcceptanceDemoSeeder.ProjectId);
                        await planningStore.SaveAsync(planning with { Document = new RAWSelectionAssistant.Core.Services.Projects.PlanningDocument
                        {
                            Title = "秋日窗光｜身体与留白", Subtitle = "用克制的暖色，记录安静的身体轮廓。", Body = "# 创作方向\n让窗光成为画面中的叙述者。\n保留真实构图，观察光线与肌理的关系。", Status = "进行中",
                            References = Enumerable.Range(0, 6).Select(i => new RAWSelectionAssistant.Core.Services.Projects.PlanningDocumentReference(new RAWSelectionAssistant.Core.Services.Projects.ProjectShotReference(Guid.NewGuid(), RAWSelectionAssistant.Core.Services.Projects.ShotReferenceKind.Pose, ExternalReference: Path.Combine(fixtureDirectory, $"demo-shot-{i + 1:00}.png"), Title: $"合成布局参考 {i + 1:00}"), "主视觉", IsHero: i < 3, IsMoodboard: true)).ToArray()
                        } });
                        window.WindowState = WindowState.Normal; window.Width = 1920; window.Height = 1080;
                        var rows = new List<object>();
                        var images = new List<(string Module, string State, string Path)>();
                        var routes = new[] { ("01_workbench", "Workbench"), ("02_asset-library", "AssetLibrary"), ("03_ingest", "Workflow"), ("04_calendar", "WorkCalendar"), ("05_planning", "Planning"), ("06_tether", "Tether"), ("07_online-selection", "OnlineSelection"), ("08_finance", "Finance"), ("09_history", "History"), ("10_toolbox", "Toolbox"), ("11_reference-color", "ReferenceColor"), ("12_publish", "Publishing"), ("13_raw-jpg", "RawToJpeg"), ("14_organize", "PhotoGrouping"), ("15_collage", "Collage"), ("16_settings", "Settings"), ("17_license", "Activation"), ("18_help", "Help") };
                        async Task Capture(string module, string state, FrameworkElement? popup = null)
                        {
                            await window.Dispatcher.InvokeAsync(window.UpdateLayout, DispatcherPriority.ApplicationIdle);
                            await Task.Delay(350);
                            var root = popup ?? (FrameworkElement)window.Content;
                            root.UpdateLayout();
                            for (var attempt = 0; (root.ActualWidth <= 0 || root.ActualHeight <= 0) && attempt < 10; attempt++)
                            { await Task.Delay(100); root.UpdateLayout(); }
                            // Menu mode may dismiss an OS popup while this offscreen render harness waits.
                            // Arrange the actual production popup subtree, never a reconstructed menu.
                            // This is render evidence only, not proof of desktop popup interaction.
                            if (popup is not null && (root.ActualWidth <= 0 || root.ActualHeight <= 0))
                            {
                                root.Measure(new Size(800, 1000));
                                root.Arrange(new Rect(root.DesiredSize)); root.UpdateLayout();
                            }
                            if (root.ActualWidth <= 0 || root.ActualHeight <= 0) throw new InvalidOperationException($"Unrendered production surface: {module}/{state}");
                            if (popup is null) StudioVisualEvidence.AssertNoShellCloseCollision(root, module + "/" + state, output);
                            StudioVisualEvidence.AuditGeometry(root, module + "/" + state, 1, output);
                            var bitmap = new RenderTargetBitmap((int)Math.Ceiling(root.ActualWidth), (int)Math.Ceiling(root.ActualHeight), 96, 96, PixelFormats.Pbgra32);
                            bitmap.Render(root);
                            var dir = Path.Combine(output, module); Directory.CreateDirectory(dir);
                            var path = Path.Combine(dir, state + ".png");
                            var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
                            using (var stream = File.Create(path)) encoder.Save(stream);
                            images.Add((module, state, path));
                            rows.Add(new { ProductSourceSha = Environment.GetEnvironmentVariable("PIXEL_TART_PRODUCT_SOURCE_SHA") ?? "UNFROZEN_WORKTREE", Module = module, State = state, Filename = Path.GetRelativePath(output, path).Replace('\\', '/'), SHA256 = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))), Resolution = new[] { bitmap.PixelWidth, bitmap.PixelHeight }, LogicalDpi = 96, RealProductionUI = true, EvidenceKind = "IN_PROCESS_WPF_RENDER", ReviewStatus = "NOT_REVIEWED" });
                            File.WriteAllText(Path.Combine(output, "WHOLE_APP_SCREENSHOT_MANIFEST.json"), JsonSerializer.Serialize(rows, new JsonSerializerOptions { WriteIndented = true }));
                        }
                        foreach (var (module, route) in routes)
                        {
                            vm.NavigateCommand.Execute(route);
                            await Task.Delay(500);
                            // Settings is a real overlay and deliberately does not change CurrentPage.
                            if (route != "Settings" && route != vm.CurrentPage) throw new InvalidOperationException("Route failed: " + route + "; actual=" + vm.CurrentPage);
                            await Capture(module, "default");
                            if (route == "Workflow" && Environment.GetEnvironmentVariable("PIXEL_TART_STUDIO_EVIDENCE") == "1")
                            {
                                vm.TextInput = "0001,0002,0003";
                                vm.ParseTextCommand.Execute(null);
                                if (vm.Selections.Count == 0) throw new InvalidOperationException("RAW match fixture has no selections");
                                await StudioVisualEvidence.MeasureOperation(output, "RAW match (empty-index recovery)", () => vm.MatchCommand.ExecuteAsync(null), () => vm.IsBusy);
                            }
                            if (route == "Tether" && Environment.GetEnvironmentVariable("PIXEL_TART_STUDIO_EVIDENCE") == "1")
                            {
                                var tether = vm.TetherPage!;
                                tether.WatchDirectory = fixtureDirectory; tether.ImportExisting = true;
                                tether.CopyToProject = false; tether.CopyToBackup = false;
                                await tether.StartCommand.ExecuteAsync(null);
                                await tether.ReconcileCommand.ExecuteAsync(null);
                                for (var wait = 0; tether.Assets.Count == 0 && wait < 40; wait++) await Task.Delay(250);
                                if (!tether.IsRunning) throw new InvalidOperationException("Synthetic watch-folder session did not start");
                                if (tether.Assets.Count > 0) tether.SelectedAsset = tether.Assets[0];
                                for (var wait = 0; tether.IsPreviewLoading && wait < 120; wait++) await Task.Delay(250);
                                if (tether.IsPreviewLoading) throw new InvalidOperationException("Tether preview did not settle");
                                if (tether.CurrentImage is null) throw new InvalidOperationException("Tether ready state must include a real loaded image");
                                var boundPreview = Descendants<Image>(window).First(x => System.Windows.Data.BindingOperations.GetBinding(x, Image.SourceProperty)?.Path.Path == "ColorSettings.VisibleImage");
                                if (!ReferenceEquals(tether.ColorSettings.VisibleImage, boundPreview.Source)) throw new InvalidOperationException("Tether image binding must update after decode");
                                await Capture(module, "watch-folder-monitor");
                                tether.ReferenceMode.AdvancedExpanded = false; await Capture(module, "reference-collapsed");
                                tether.ReferenceMode.AdvancedExpanded = true; await Capture(module, "reference-expanded");
                                tether.ReferenceMode.AdvancedExpanded = false;
                                await Capture(module, "shot-references");
                                await tether.StopCommand.ExecuteAsync(null); await Capture(module, "session-stopped");
                            }
                            if (route == "Settings")
                            {
                                var tabs = Descendants<TabControl>(window).First(x => x.IsVisible);
                                for (var tab = 0; tab < tabs.Items.Count; tab++)
                                {
                                    tabs.SelectedIndex = tab; await Capture(module, "tab-" + tab);
                                    var dropdown = Descendants<ComboBox>(tabs).FirstOrDefault(x => x.IsVisible && x.Items.Count > 0);
                                    if (dropdown is not null)
                                    {
                                        dropdown.IsDropDownOpen = true; await Task.Delay(150);
                                        if ((dropdown.Template.FindName("PART_Popup", dropdown) as System.Windows.Controls.Primitives.Popup)?.Child is FrameworkElement child) await Capture("18_global-popups", "settings-" + tab, child);
                                        dropdown.IsDropDownOpen = false;
                                    }
                                }
                                vm.CloseSettingsCommand.Execute(null);
                            }
                            if (route == "Publishing")
                            {
                                var publishing = Descendants<RAWSelectionAssistant.Views.PublishingExportView>(window).Single();
                                var publisher = (PublishingExportViewModel)publishing.DataContext;
                                publisher.AddFiles(Directory.GetFiles(fixtureDirectory, "*.png").Take(3));
                                await StudioVisualEvidence.MeasureOperation(output,"Publishing preview",async () => { await ((RAWSelectionAssistant.Utilities.AsyncRelayCommand)publisher.RefreshPreviewCommand).ExecuteAsync(null); while(publisher.IsPreviewing) await Task.Delay(20); },()=>publisher.IsPreviewing, publishing); await Capture(module, "content");
                                if(Environment.GetEnvironmentVariable("PIXEL_TART_STUDIO_EVIDENCE")=="1") {
                                    publisher.DestinationDirectory=Path.Combine(Environment.GetEnvironmentVariable("PIXEL_TART_ACCEPTANCE_ROOT")!,"PublishingOutput");Directory.CreateDirectory(publisher.DestinationDirectory);
                                    await StudioVisualEvidence.MeasureOperation(output,"Publishing export",()=>((RAWSelectionAssistant.Utilities.AsyncRelayCommand)publisher.StartCommand).ExecuteAsync(null),()=>publisher.IsBusy, publishing);
                                }
                                var preset = Descendants<ComboBox>(publishing).Last(x => x.IsVisible && x.Items.Count > 0);
                                preset.IsDropDownOpen = true; await Task.Delay(150);
                                if ((preset.Template.FindName("PART_Popup", preset) as System.Windows.Controls.Primitives.Popup)?.Child is FrameworkElement child) await Capture("18_global-popups", "publish-preset", child);
                                preset.IsDropDownOpen = false;
                            }
                            if (route == "AssetLibrary")
                            {
                                var host = Descendants<PixelTart.Modules.AssetLibrary.AssetLibraryWorkspaceHost>(window).Single();
                                var container = await new RAWSelectionAssistant.Core.Services.AssetLibrary.AssetLibraryContainerService().CreateAsync(Path.Combine(Environment.GetEnvironmentVariable("PIXEL_TART_ACCEPTANCE_ROOT")!, "VisualLibrary.ptlibrary"), "视觉验收素材库");
                                await host.SwitchToContainerAsync(container.ContainerPath);
                                window.Width = 1920; window.Height = 1080;
                                await Capture(module, "empty-library");
                                await StudioVisualEvidence.MeasureOperation(output,"Asset import",()=>host.CurrentPage!.ViewModel.ImportDemoDirectoryAsync(Path.Combine(Environment.GetEnvironmentVariable("PIXEL_TART_ACCEPTANCE_ROOT")!, "SyntheticAssets")),()=>host.CurrentPage!.ViewModel.IsLoading, host);
                                await Capture(module, "content");
                                var context = host.CurrentPage!.OpenContextMenuForProductHarness();
                                if (context is not null)
                                {
                                    await Capture("18_global-popups", "asset-context", context);
                                    foreach (var item in context.Items.OfType<MenuItem>().Where(x => x.HasItems))
                                    {
                                        item.IsSubmenuOpen = true; await Task.Delay(100);
                                        if ((item.Template.FindName("PART_Popup", item) as System.Windows.Controls.Primitives.Popup)?.Child is FrameworkElement child) await Capture("18_global-popups", "asset-" + item.Header, child);
                                        item.IsSubmenuOpen = false;
                                    }
                                    context.IsOpen = false;
                                }
                                var gallery = Descendants<ListBox>(host).Single(x => x.Name == "AssetGrid");
                                gallery.SelectedIndex = 0; await Capture(module, "selected");
                                gallery.SelectAll(); await Capture(module, "multi-selected");
                                if (await host.CurrentPage.OpenQuickLoupeForProductHarnessAsync() && host.CurrentPage.GetQuickLoupeContentForProductHarness() is FrameworkElement loupe) await Capture(module, "quick-loupe", loupe);
                                host.CurrentPage.LeaveQuickLoupeForProductHarness();
                                var viewer = host.CurrentPage.CreateViewerForProductHarness();
                                if (viewer is not null) { viewer.Show(); await Capture(module, "viewer", (FrameworkElement)viewer.Content); viewer.Close(); }
                                var filter = Descendants<Button>(host).Single(x => System.Windows.Automation.AutomationProperties.GetAutomationId(x) == "AssetLibraryColorFilter");
                                filter.RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); await Capture(module, "filter"); filter.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                                host.OpenRecentLibraryMenuForProductHarness(); await Capture("18_global-popups", "asset-library", host.ProductHarnessMenu); host.ProductHarnessMenu.IsOpen = false;
                            }
                            if (route == "Planning")
                            {
                                await vm.OpenPlanningAsync(RAWSelectionAssistant.Services.PlanningHumanAcceptanceDemoSeeder.ProjectId);
                                if (!vm.PlanningPage!.HasProject) throw new InvalidOperationException("Planning content fixture did not load");
                                foreach (var page in PlanningCenterViewModel.ContentPages) { vm.PlanningPage!.ContentPage = page; await Capture(module, page); }
                                vm.PlanningPage!.IsPreviewMode = true; await Capture(module, "preview"); vm.PlanningPage.IsPreviewMode = false;
                                await vm.PlanningPage.ShowCreatePlanningCommand.ExecuteAsync(null); await Capture(module, "create-dialog");
                                var date = Descendants<DatePicker>(window).Single(x => x.IsVisible); date.IsDropDownOpen = true; await Task.Delay(200);
                                if ((date.Template.FindName("PART_Popup", date) as System.Windows.Controls.Primitives.Popup)?.Child is FrameworkElement calendar)
                                {
                                    await Capture("18_global-popups", "datepicker", calendar);
                                    var month = Descendants<Grid>(calendar).Single(x => x.Name == "PART_MonthView");
                                    var headings = month.Children.OfType<FrameworkElement>().Where(x => Grid.GetRow(x) == 0).ToArray();
                                    if (headings.Length != 7 || headings.Any(x => !Descendants<TextBlock>(x).Any(t => !string.IsNullOrWhiteSpace(t.Text))))
                                        throw new InvalidOperationException("Calendar must visibly retain all seven weekday labels");
                                }
                                date.IsDropDownOpen = false; vm.PlanningPage.CancelPlanningModalCommand.Execute(null);
                                vm.PlanningPage.ContentPage = "参考图";
                                var tile = Descendants<RAWSelectionAssistant.Views.PlanningReferenceTile>(window).First(x => x.IsVisible);
                                tile.Focus();
                                tile.ContextMenu!.PlacementTarget = tile; tile.ContextMenu.IsOpen = true;
                                await Capture(module, "references-selected");
                                await Capture("18_global-popups", "planning-reference", tile.ContextMenu); tile.ContextMenu.IsOpen = false;
                            }
                            if (route == "ReferenceColor")
                            {
                                if (Environment.GetEnvironmentVariable("PIXEL_TART_STUDIO_EVIDENCE") == "1")
                                {
                                    await StudioVisualEvidence.MeasureOperation(output, "Reference decode", () => vm.ReferenceColorPage.LoadTargetAsync(Path.Combine(fixtureDirectory, "demo-shot-01.png")), () => vm.ReferenceColorPage.IsLoading, window);
                                    await Capture(module, "target");
                                }
                                var drawing = new DrawingVisual(); using (var context = drawing.RenderOpen()) { context.DrawRectangle(Brushes.SlateGray, null, new Rect(0, 0, 1200, 800)); context.DrawEllipse(Brushes.Coral, null, new Point(400, 400), 160, 240); }
                                var fixture = new RenderTargetBitmap(1200, 800, 96, 96, PixelFormats.Pbgra32); fixture.Render(drawing); fixture.Freeze();
                                await StudioVisualEvidence.MeasureOperation(output,"Reference render",()=>vm.ReferenceColorPage.AcceptContextAsync(RAWSelectionAssistant.Services.PlanningHumanAcceptanceDemoSeeder.ProjectId, null, fixture),()=>vm.ReferenceColorPage.Editor.IsBusy, window);
                                if (vm.ReferenceColorPage.Editor.SelectedLook is null || vm.ReferenceColorPage.Editor.ReferenceSources.Count == 0) throw new InvalidOperationException("Catalog refresh lost the selected scheme or references");
                                await Capture(module, "loaded-synthetic");
                                vm.ReferenceColorPage.Editor.WorkspaceMode = "简洁"; await Capture(module, "reference_simple");
                                vm.ReferenceColorPage.Editor.WorkspaceMode = "专业"; await Capture(module, "reference_pro");
                                vm.ReferenceColorPage.Editor.WorkspaceSection = "胶片"; vm.ReferenceColorPage.Editor.FilmEnabled = true; vm.ReferenceColorPage.Editor.FilmGrainAmount = 35; vm.ReferenceColorPage.Editor.FilmHalationAmount = 28; vm.ReferenceColorPage.Editor.FilmBloomAmount = 22; vm.ReferenceColorPage.Editor.FilmTextureId = "Paper"; vm.ReferenceColorPage.Editor.FilmSurfaceAmount = 32; vm.ReferenceColorPage.Editor.FilmTextureAmount = 45; await vm.ReferenceColorPage.Editor.ApplyCommand.ExecuteAsync(null);
                                vm.ReferenceColorPage.Editor.WorkspaceMode = "简洁"; await Capture(module, "film_simple"); vm.ReferenceColorPage.Editor.WorkspaceMode = "专业"; await Capture(module, "film_pro"); await Capture(module, "reference_film"); await Capture(module, "reference_texture_grid");
                                if (Environment.GetEnvironmentVariable("PIXEL_TART_STUDIO_EVIDENCE") == "1") await StudioVisualEvidence.AccentComparison((FrameworkElement)window.Content, output);
                                vm.ReferenceColorPage.Editor.WorkspaceSection = "仿色";
                                foreach (var mode in vm.ReferenceColorPage.Editor.ViewModes) { vm.ReferenceColorPage.Editor.ViewMode = mode; await Capture(module, mode); }
                                var renderStarted = new TaskCompletionSource(); var releaseRender = new TaskCompletionSource();
                                vm.ReferenceColorPage.Editor.PostProcessor = async (value, token) => { renderStarted.TrySetResult(); await releaseRender.Task.WaitAsync(token); return value; };
                                var render = vm.ReferenceColorPage.Editor.ApplyCommand.ExecuteAsync(null);
                                try { await renderStarted.Task.WaitAsync(TimeSpan.FromSeconds(20)); if (!vm.ReferenceColorPage.Editor.IsBusy) throw new InvalidOperationException("Loading feedback missing"); await Capture(module, "loading"); }
                                finally { releaseRender.TrySetResult(); await render; vm.ReferenceColorPage.Editor.PostProcessor = null; }
                                if (Environment.GetEnvironmentVariable("PIXEL_TART_STUDIO_EVIDENCE") == "1")
                                {
                                    vm.ReferenceColorPage.Editor.AdvancedExpanded = true; await Capture(module, "advanced");
                                    vm.ReferenceColorPage.Editor.AdvancedExpanded = false;
                                    vm.ReferenceColorPage.Editor.PostProcessor = (_, _) => throw new IOException("Synthetic render failure");
                                    await vm.ReferenceColorPage.Editor.ApplyCommand.ExecuteAsync(null);
                                    if (!vm.ReferenceColorPage.Editor.HasError) throw new InvalidOperationException("Error recovery feedback missing");
                                    await Capture(module, "error");
                                    vm.ReferenceColorPage.Editor.PostProcessor = null;
                                    await vm.ReferenceColorPage.Editor.ApplyCommand.ExecuteAsync(null);
                                    if (vm.ReferenceColorPage.Editor.HasError) throw new InvalidOperationException("Retry did not clear error");
                                    await Capture(module, "recovered");
                                    var originalLook = vm.ReferenceColorPage.Editor.SelectedLook!;
                                    var sources = originalLook.ReferenceSources.Take(1).SelectMany(source => Enumerable.Range(0,3).Select(i => source with { Name = "合成参考 " + (i+1), SourcePath = Path.Combine(fixtureDirectory, $"demo-shot-{i+1:00}.png"), ContentHash = "studio-"+i, Weight = 1d/3 })).ToArray();
                                    vm.ReferenceColorPage.Editor.SelectedLook = originalLook with { ReferenceSources = sources };
                                    await vm.ReferenceColorPage.Editor.ApplyCommand.ExecuteAsync(null); await Capture(module,"multi-reference");
                                }
                                window.Width = 1280; window.Height = 900; await Capture(module, "compact_drawer_closed"); vm.ReferenceColorPage.Editor.ContextRailOpen = true; await Capture(module, "compact_drawer_open"); vm.ReferenceColorPage.Editor.ContextRailOpen = false;
                                window.Width = 960; window.Height = 900; await Capture(module, "narrow_canvas_first"); vm.ReferenceColorPage.Editor.FocusView = true; await Capture(module, "focus_view"); vm.ReferenceColorPage.Editor.FocusView = false;
                                window.Width = 1920; window.Height = 1080; vm.ReferenceColorPage.Editor.ContextRailOpen = true;
                                vm.ReferenceColorPage.Editor.WorkspaceSection = "胶片";
                                var combo = Descendants<ComboBox>(window).First(x => x.IsVisible);
                                combo.IsDropDownOpen = true; await Task.Delay(150);
                                if ((combo.Template.FindName("PART_Popup", combo) as System.Windows.Controls.Primitives.Popup)?.Child is FrameworkElement dropdown) await Capture("18_global-popups", "reference-color", dropdown);
                                combo.IsDropDownOpen = false;
                            }
                        }
                        // Capture popup inventory before building sheets so it cannot be omitted.
                        vm.NavigateCommand.Execute("Workbench"); await Task.Delay(300);
                        var mainMenu = Descendants<Menu>(window).FirstOrDefault();
                        if (mainMenu is not null)
                        {
                            foreach (var item in mainMenu.Items.OfType<MenuItem>().Where(x => x.HasItems))
                            {
                                window.Activate(); item.Focus(); item.IsSubmenuOpen = true; await Task.Delay(250);
                                if (item.Template.FindName("PART_Popup", item) is System.Windows.Controls.Primitives.Popup mainPopup)
                                {
                                    mainPopup.IsOpen = true;
                                    await window.Dispatcher.InvokeAsync(window.UpdateLayout, DispatcherPriority.ApplicationIdle);
                                    await Task.Delay(250);
                                }
                                if ((item.Template.FindName("PART_Popup", item) as System.Windows.Controls.Primitives.Popup)?.Child is FrameworkElement child) await Capture("18_global-popups", "main-" + mainMenu.Items.IndexOf(item), child);
                                item.IsSubmenuOpen = false;
                            }
                        }
                        if (Environment.GetEnvironmentVariable("PIXEL_TART_STUDIO_EVIDENCE") == "1")
                        {
                            await StudioVisualEvidence.Gallery(window, output);
                            await StudioVisualEvidence.Dpi(window, vm, output);
                        }
                        var sheetCount = (int)Math.Ceiling(images.Count / 9d);
                        var sheetSize = (int)Math.Ceiling(images.Count / (double)sheetCount);
                        for (var start = 0; start < images.Count; start += sheetSize)
                        {
                            var drawing = new DrawingVisual();
                            using (var dc = drawing.RenderOpen())
                            {
                                dc.DrawRectangle(new SolidColorBrush(Color.FromRgb(16, 19, 23)), null, new Rect(0, 0, 1440, 450 * Math.Ceiling(sheetSize / 2d)));
                                for (var i = start; i < Math.Min(images.Count, start + sheetSize); i++)
                                {
                                    var item = images[i]; var x = (i - start) % 2 * 720; var y = (i - start) / 2 * 450;
                                    var bitmap = new BitmapImage(new Uri(item.Path)); var ratio = Math.Min(1, Math.Min(696d / bitmap.PixelWidth, 394d / bitmap.PixelHeight));
                                    dc.DrawImage(bitmap, new Rect(x + 12, y + 42, bitmap.PixelWidth * ratio, bitmap.PixelHeight * ratio));
                                    dc.DrawText(new FormattedText(item.Module + " / " + item.State + ".png", System.Globalization.CultureInfo.InvariantCulture, FlowDirection.LeftToRight, new Typeface("Microsoft YaHei UI"), 16, Brushes.White, 1), new Point(x + 12, y + 10));
                                }
                            }
                            var sheet = new RenderTargetBitmap(1440, (int)(450 * Math.Ceiling(sheetSize / 2d)), 96, 96, PixelFormats.Pbgra32); sheet.Render(drawing);
                            var png = new PngBitmapEncoder(); png.Frames.Add(BitmapFrame.Create(sheet)); using var stream = File.Create(Path.Combine(output, $"WHOLE_APP_CONTACT_SHEET_{start / sheetSize + 1:00}.png")); png.Save(stream);
                        }
                        var popupImages = images.Where(x => x.Module == "18_global-popups").ToList();
                        var popupDrawing = new DrawingVisual();
                        using (var dc = popupDrawing.RenderOpen())
                        {
                            dc.DrawRectangle(new SolidColorBrush(Color.FromRgb(16, 19, 23)), null, new Rect(0, 0, 1600, Math.Ceiling(popupImages.Count / 4d) * 500));
                            for (var i = 0; i < popupImages.Count; i++)
                            {
                                var item = popupImages[i]; var x = i % 4 * 400; var y = i / 4 * 500;
                                var bitmap = new BitmapImage(new Uri(item.Path)); var scale = Math.Min(1, Math.Min(376d / bitmap.PixelWidth, 450d / bitmap.PixelHeight));
                                dc.DrawImage(bitmap, new Rect(x + 12, y + 38, bitmap.PixelWidth * scale, bitmap.PixelHeight * scale));
                                dc.DrawText(new FormattedText(item.State + ".png", System.Globalization.CultureInfo.InvariantCulture, FlowDirection.LeftToRight, new Typeface("Microsoft YaHei UI"), 14, Brushes.White, 1), new Point(x + 12, y + 10));
                            }
                        }
                        var popupSheet = new RenderTargetBitmap(1600, (int)Math.Ceiling(popupImages.Count / 4d) * 500, 96, 96, PixelFormats.Pbgra32); popupSheet.Render(popupDrawing);
                        var popupEncoder = new PngBitmapEncoder(); popupEncoder.Frames.Add(BitmapFrame.Create(popupSheet)); using (var file = File.Create(Path.Combine(output, "GLOBAL_POPUP_CONTACT_SHEET.png"))) popupEncoder.Save(file);
                        if (Environment.GetEnvironmentVariable("PIXEL_TART_STUDIO_EVIDENCE") == "1")
                        {
                            StudioVisualEvidence.ContactSheet(output,"PIXEL_TART_STUDIO_UI_V1_COMPONENTS",Directory.GetFiles(Path.Combine(output,"components"),"*.png").Order());
                            StudioVisualEvidence.ContactSheet(output,"PIXEL_TART_STUDIO_UI_V1_SYMBOLS",[Path.Combine(output,"components","10_symbols.png")]);
                            StudioVisualEvidence.ContactSheet(output,"PIXEL_TART_STUDIO_UI_V1_CORE_PAGES_01",images.Where(x=>x.Module is "02_asset-library" or "05_planning").Select(x=>x.Path));
                            StudioVisualEvidence.ContactSheet(output,"PIXEL_TART_STUDIO_UI_V1_CORE_PAGES_02",images.Where(x=>x.Module is "06_tether" or "11_reference-color").Select(x=>x.Path));
                            StudioVisualEvidence.ContactSheet(output,"PIXEL_TART_STUDIO_UI_V1_WHOLE_APP_SMOKE",images.Where(x=>x.State=="default").Select(x=>x.Path));
                        }
                        complete = true;
                    }
                    catch (Exception ex) { failure = ex; }
                    finally { timer.Stop(); window.Close(); }
                };
                timer.Start(); app.Run();
            }
            catch (Exception ex) { failure = ex; }
        }) { IsBackground = true };
        thread.SetApartmentState(ApartmentState.STA); thread.Start();
        Assert.IsTrue(thread.Join(TimeSpan.FromMinutes(4)), "Whole-app inventory timeout");
        if (failure is not null) throw new AssertFailedException(failure.ToString(), failure);
        Assert.IsTrue(complete);
    }
    private static IEnumerable<T> Descendants<T>(DependencyObject root) where T : DependencyObject
    {
        if (root is T match) yield return match;
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++) foreach (var child in Descendants<T>(VisualTreeHelper.GetChild(root, i))) yield return child;
    }
}
