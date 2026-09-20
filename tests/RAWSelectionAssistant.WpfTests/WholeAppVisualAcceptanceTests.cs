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
                        var routes = new[] { ("01_workbench", "Workbench"), ("02_asset-library", "AssetLibrary"), ("03_ingest", "Workflow"), ("04_calendar", "WorkCalendar"), ("05_planning", "Planning"), ("06_tether", "Tether"), ("07_online-selection", "OnlineSelection"), ("08_finance", "Finance"), ("09_history", "History"), ("10_toolbox", "Toolbox"), ("11_reference-color", "ReferenceColor"), ("12_publish", "Publishing"), ("13_raw-jpg", "RawToJpeg"), ("14_organize", "PhotoGrouping"), ("15_collage", "Collage"), ("16_settings", "Settings"), ("17_license", "Activation") };
                        async Task Capture(string module, string state, FrameworkElement? popup = null)
                        {
                            await window.Dispatcher.InvokeAsync(window.UpdateLayout, DispatcherPriority.ApplicationIdle);
                            await Task.Delay(350);
                            var root = popup ?? (FrameworkElement)window.Content;
                            root.UpdateLayout();
                            for (var attempt = 0; (root.ActualWidth <= 0 || root.ActualHeight <= 0) && attempt < 10; attempt++)
                            { await Task.Delay(100); root.UpdateLayout(); }
                            if (root.ActualWidth <= 0 || root.ActualHeight <= 0) throw new InvalidOperationException($"Unrendered production surface: {module}/{state}");
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
                                await ((RAWSelectionAssistant.Utilities.AsyncRelayCommand)publisher.RefreshPreviewCommand).ExecuteAsync(null); await Capture(module, "content");
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
                                await host.CurrentPage!.ViewModel.ImportDemoDirectoryAsync(Path.Combine(Environment.GetEnvironmentVariable("PIXEL_TART_ACCEPTANCE_ROOT")!, "SyntheticAssets"));
                                await Capture(module, "content");
                                var context = host.CurrentPage.OpenContextMenuForProductHarness();
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
                                    Assert.AreEqual(7, headings.Length, "Calendar must retain all weekday labels");
                                    Assert.IsTrue(headings.All(x => Descendants<TextBlock>(x).Any(t => !string.IsNullOrWhiteSpace(t.Text))), "Calendar weekday template must be visible");
                                }
                                date.IsDropDownOpen = false; vm.PlanningPage.CancelPlanningModalCommand.Execute(null);
                                vm.PlanningPage.ContentPage = "参考图";
                                var tile = Descendants<RAWSelectionAssistant.Views.PlanningReferenceTile>(window).First(x => x.IsVisible);
                                tile.Focus(); tile.HandleKey(System.Windows.Input.Key.F10, System.Windows.Input.ModifierKeys.Shift);
                                await Capture(module, "references-selected");
                                await Capture("18_global-popups", "planning-reference", tile.ContextMenu); tile.ContextMenu.IsOpen = false;
                            }
                            if (route == "ReferenceColor")
                            {
                                var drawing = new DrawingVisual(); using (var context = drawing.RenderOpen()) { context.DrawRectangle(Brushes.SlateGray, null, new Rect(0, 0, 1200, 800)); context.DrawEllipse(Brushes.Coral, null, new Point(400, 400), 160, 240); }
                                var fixture = new RenderTargetBitmap(1200, 800, 96, 96, PixelFormats.Pbgra32); fixture.Render(drawing); fixture.Freeze();
                                await vm.ReferenceColorPage.AcceptContextAsync(RAWSelectionAssistant.Services.PlanningHumanAcceptanceDemoSeeder.ProjectId, null, fixture);
                                if (vm.ReferenceColorPage.Editor.SelectedLook is null || vm.ReferenceColorPage.Editor.ReferenceSources.Count == 0) throw new InvalidOperationException("Catalog refresh lost the selected scheme or references");
                                await Capture(module, "loaded-synthetic");
                                foreach (var mode in vm.ReferenceColorPage.Editor.ViewModes) { vm.ReferenceColorPage.Editor.ViewMode = mode; await Capture(module, mode); }
                                var renderStarted = new TaskCompletionSource(); var releaseRender = new TaskCompletionSource();
                                vm.ReferenceColorPage.Editor.PostProcessor = async (value, token) => { renderStarted.TrySetResult(); await releaseRender.Task.WaitAsync(token); return value; };
                                var render = vm.ReferenceColorPage.Editor.ApplyCommand.ExecuteAsync(null);
                                try { await renderStarted.Task.WaitAsync(TimeSpan.FromSeconds(20)); if (!vm.ReferenceColorPage.Editor.IsBusy) throw new InvalidOperationException("Loading feedback missing"); await Capture(module, "loading"); }
                                finally { releaseRender.TrySetResult(); await render; vm.ReferenceColorPage.Editor.PostProcessor = null; }
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
                                item.IsSubmenuOpen = true; await Task.Delay(150);
                                if ((item.Template.FindName("PART_Popup", item) as System.Windows.Controls.Primitives.Popup)?.Child is FrameworkElement child) await Capture("18_global-popups", "main-" + mainMenu.Items.IndexOf(item), child);
                                item.IsSubmenuOpen = false;
                            }
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
                        complete = true;
                    }
                    catch (Exception ex) { failure = ex; }
                    finally { timer.Stop(); window.Close(); app.Shutdown(); }
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
