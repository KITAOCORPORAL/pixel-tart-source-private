using System.IO;
using System.Reflection;
using System.Text.Json;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using RAWSelectionAssistant.Core.Services.Projects;
using RAWSelectionAssistant.Core.Utilities;
using RAWSelectionAssistant.Services;
using RAWSelectionAssistant.ViewModels;
using RAWSelectionAssistant.Views;

namespace RAWSelectionAssistant.WpfTests;

[TestClass]
[DoNotParallelize]
public sealed class PlanningAutomationContractTests
{
    [TestMethod]
    public void ProductionPeers_KeyStatesAndReferenceFamily_ExposeRealContracts()
    {
        Exception? failure = null;
        bool completed = false;
        var thread = new Thread(() =>
        {
            try
            {
                Assert.AreEqual("1", Environment.GetEnvironmentVariable("PIXEL_TART_HUMAN_ACCEPTANCE"));
                Assert.IsFalse(string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("PIXEL_TART_ACCEPTANCE_ROOT")));
                var app = new App(); app.InitializeComponent();
                var busy = false;
                var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(200) };
                timer.Tick += async (_, _) =>
                {
                    if (busy || app.MainWindow is not MainWindow { IsLoaded: true, DataContext: MainViewModel vm } window) return;
                    busy = true;
                    try { await Verify(window, vm); completed = true; }
                    catch (Exception e) { failure = e; }
                    finally { timer.Stop(); window.Close(); app.Shutdown(); }
                };
                timer.Start(); app.Run();
            }
            catch (Exception e) { failure = e; }
        }) { IsBackground = true };
        thread.SetApartmentState(ApartmentState.STA); thread.Start();
        Assert.IsTrue(thread.Join(TimeSpan.FromSeconds(180)), "Production peer test timeout");
        if (failure is not null) throw new AssertFailedException(failure.ToString(), failure);
        Assert.IsTrue(completed);
    }

    private sealed record PeerRow(string Path, string Name, string AutomationId, string Type, bool Enabled, bool Focusable, bool Offscreen, string[] Patterns);
    private static IEnumerable<AutomationPeer> Walk(AutomationPeer root)
    {
        yield return root;
        foreach (var child in root.GetChildren() ?? []) foreach (var descendant in Walk(child)) yield return descendant;
    }
    private static AutomationPeer Peer(UIElement element) => UIElementAutomationPeer.CreatePeerForElement(element) ?? throw new InvalidOperationException("Missing peer: " + element.GetType());
    private static IEnumerable<T> Visuals<T>(DependencyObject root) where T : DependencyObject
    {
        if (root is T item) yield return item;
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++) foreach (var item2 in Visuals<T>(VisualTreeHelper.GetChild(root, i))) yield return item2;
    }
    private static async Task Verify(MainWindow window, MainViewModel main)
    {
        var output = Environment.GetEnvironmentVariable("PIXEL_TART_CONTRACT_EVIDENCE") ?? throw new InvalidOperationException("Evidence directory required");
        Directory.CreateDirectory(output);
        var checks = new List<object>();
        var states = new Dictionary<string, List<PeerRow>>();
        async Task Idle()
        {
            await window.Dispatcher.InvokeAsync(window.UpdateLayout, DispatcherPriority.ApplicationIdle);
            foreach (var element in Visuals<UIElement>(window))
            {
                UIElementAutomationPeer.FromElement(element)?.ResetChildrenCache();
                UIElementAutomationPeer.FromElement(element)?.InvalidatePeer();
            }
            await window.Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ApplicationIdle);
            await Task.Delay(100);
        }
        void Check(string id, Action action) { action(); checks.Add(new { Id = id, Status = "PASS" }); File.WriteAllText(Path.Combine(output, "checks.json"), JsonSerializer.Serialize(checks, new JsonSerializerOptions { WriteIndented = true })); }
        void Snapshot(string state, UIElement root)
        {
            var rows = new List<PeerRow>();
            void Add(AutomationPeer p, string parent)
            {
                var path = parent + "/" + p.GetAutomationId() + ":" + p.GetName();
                rows.Add(new(path, p.GetName(), p.GetAutomationId(), p.GetAutomationControlType().ToString(), p.IsEnabled(), p.IsKeyboardFocusable(), p.IsOffscreen(), Enum.GetValues<PatternInterface>().Where(x => p.GetPattern(x) is not null).Select(x => x.ToString()).ToArray()));
                p.ResetChildrenCache();
                foreach (var c in p.GetChildren() ?? []) Add(c, path);
            }
            Add(Peer(root), ""); states[state] = rows;
            File.WriteAllText(Path.Combine(output, "peer-states.json"), JsonSerializer.Serialize(states, new JsonSerializerOptions { WriteIndented = true }));
        }
        void Capture(string name, Visual root)
        {
            var element = (FrameworkElement)root;
            var bitmap = new RenderTargetBitmap((int)Math.Ceiling(element.ActualWidth), (int)Math.Ceiling(element.ActualHeight), 96, 96, PixelFormats.Pbgra32);
            bitmap.Render(root); var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
            using var file = File.Create(Path.Combine(output, name + ".png")); encoder.Save(file);
        }
        main.ForceExitTutorial(); window.WindowState = WindowState.Normal; window.Width = 1920; window.Height = 1080;
        await PlanningHumanAcceptanceDemoSeeder.SeedAsync(new RAWSelectionAssistant.Core.Services.SettingsService(new RAWSelectionAssistant.Core.Services.FileLogService()));
        await Idle(); Snapshot("BOOT", window);
        var nav = (ScrollViewer)window.FindName("SidebarNavigationScroll");
        Check("v2-navigation", () =>
        {
            Assert.AreEqual(AutomationControlType.Pane, Peer(nav).GetAutomationControlType());
            foreach (var id in new[] { "PrimaryNavigationWorkbench", "AssetLibraryNavigationButton", "PrimaryNavigationPlanning", "PrimaryNavigationTether", "PrimaryNavigationOnlineSelection" })
            {
                var match = Walk(Peer(nav)).Single(p => p.GetAutomationId() == id);
                Assert.AreEqual(AutomationControlType.Button, match.GetAutomationControlType()); Assert.IsNotNull(match.GetPattern(PatternInterface.Invoke));
            }
        });
        main.NavigateCommand.Execute("Planning"); await main.PlanningPage!.ShowOverviewAsync(); await Idle();
        var view = Visuals<PlanningCenterView>(window).Single(); var vm = main.PlanningPage;
        Snapshot("PLANNING_LIST", view);
        await vm.ShowCreatePlanningCommand.ExecuteAsync(null); await Idle(); Snapshot("CREATE_PLANNING_MODAL", view);
        var date = Visuals<DatePicker>(view).Single(); date.ApplyTemplate();
        Check("v1-date", () =>
        {
            Assert.AreEqual(AutomationControlType.Custom, Peer(date).GetAutomationControlType());
            var edit = Walk(Peer(date)).Single(p => p.GetAutomationId() == "PART_TextBox");
            Assert.AreEqual(AutomationControlType.Edit, edit.GetAutomationControlType()); Assert.IsNotNull(edit.GetPattern(PatternInterface.Value));
            var button = Walk(Peer(date)).Single(p => p.GetAutomationId() == "PART_Button");
            Assert.AreEqual(AutomationControlType.Button, button.GetAutomationControlType()); Assert.IsNotNull(button.GetPattern(PatternInterface.Invoke));
        });
        date.IsDropDownOpen = true; await Idle(); Snapshot("DATE_PICKER", date); date.IsDropDownOpen = false;
        vm.NewPlanningName = "安装验收策划"; vm.NewLocation = "验收影棚"; vm.NewShootDate = new DateTime(2026, 9, 20);
        await vm.CreatePlanningCommand.ExecuteAsync(null); await Idle();
        Check("create-transition", () => { Assert.IsTrue(vm.HasProject); Assert.IsFalse(vm.IsCreateOpen); });
        Snapshot("CREATED_PROJECT", view);
        vm.ContentPage = "文字"; await Idle(); Snapshot("TEXT_READING", view);
        vm.IsDocumentEditing = true; await Idle(); Snapshot("TEXT_EDITOR", view);
        Check("text-editor-value", () => Assert.IsNotNull(Walk(Peer(view)).Single(p => p.GetName() == "策划正文").GetPattern(PatternInterface.Value)));
        vm.DocumentBody = "Installed Acceptance Persistence {shortsha}"; await vm.FlushAsync(); vm.IsDocumentEditing = false; await Idle(); Snapshot("TEXT_SAVED", view);
        foreach(var page in PlanningCenterViewModel.ContentPages) { vm.ContentPage = page; await Idle(); Snapshot("EMPTY-" + page, view); }
        vm.ContentPage = "文字"; vm.IsPreviewMode = true; await Idle(); Snapshot("FRESH_PREVIEW", view); vm.IsPreviewMode = false;
        await vm.NewShotCommand.ExecuteAsync(null);
        var fixture = Path.Combine(AppDataPaths.Root, "contract-reference.png");
        var bitmap = new RenderTargetBitmap(1200, 800, 96, 96, PixelFormats.Pbgra32); var drawing = new DrawingVisual();
        using (var dc = drawing.RenderOpen()) { dc.DrawRectangle(Brushes.DarkSlateGray, null, new Rect(0, 0, 1200, 800)); dc.DrawEllipse(Brushes.Coral, null, new Point(400, 350), 200, 240); }
        bitmap.Render(drawing); var png = new PngBitmapEncoder(); png.Frames.Add(BitmapFrame.Create(bitmap)); using (var stream = File.Create(fixture)) png.Save(stream);
        var idRef = Guid.NewGuid(); await vm.AddDocumentReferencesAsync([new(idRef, ShotReferenceKind.General, ExternalReference: fixture, Title: "验收参考")]);
        var reference = vm.AllProjectReferences.Single(x => x.Id == idRef);
        vm.ContentPage = "参考图"; await Idle(); Snapshot("REFERENCE_IMPORTED", view);
        var importedTile = Visuals<PlanningReferenceTile>(view).Single(x => AutomationProperties.GetName(x) == "参考图：验收参考");
        ((IInvokeProvider)Peer(importedTile).GetPattern(PatternInterface.Invoke)!).Invoke(); await Idle();
        Check("reference-invoke", () => Assert.IsTrue(((FrameworkElement)view.FindName("ImagePreview")).IsVisible));
        await view.HandleWorkspaceKeyAsync(new KeyEventArgs(Keyboard.PrimaryDevice, PresentationSource.FromVisual(window)!, 0, Key.Escape) { RoutedEvent = Keyboard.PreviewKeyDownEvent });
        typeof(PlanningReferenceTile).GetMethod("HandleKey", BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(importedTile, [Key.F10, ModifierKeys.Shift]); await Idle(); Snapshot("CONTEXT_MENU", importedTile.ContextMenu!);
        Check("menu-add-moodboard", () => Assert.HasCount(1, Walk(Peer(importedTile.ContextMenu!)).Where(p => p.GetName() == "加入情绪板" && p.GetAutomationControlType() == AutomationControlType.MenuItem).ToArray()));
        importedTile.ContextMenu!.IsOpen = false;
        await vm.UpdateReferenceAsync(reference, "情绪板");
        foreach (var page in new[] { "参考图", "情绪板", "灯光图", "服化道" })
        {
            if (page is "灯光图" or "服化道") await vm.UpdateReferenceAsync(vm.AllProjectReferences.Single(x => x.Id == idRef), "分类", page == "灯光图" ? "灯光" : "造型");
            vm.ContentPage = page; view.Render(); await Idle();
            var tile = Visuals<PlanningReferenceTile>(view).Single(x => AutomationProperties.GetName(x) == "参考图：验收参考");
            var peer = Peer(tile);
            Check(page + "-peer", () => { Assert.AreEqual(AutomationControlType.Button, peer.GetAutomationControlType()); Assert.IsTrue(peer.IsKeyboardFocusable()); Assert.IsTrue(peer.IsEnabled()); Assert.IsNotNull(peer.GetPattern(PatternInterface.Invoke)); });
            tile.Focus(); Check(page + "-focus", () => Assert.IsTrue(tile.IsKeyboardFocused));
            Snapshot(page, view); Capture(page + "-focused", window);
            var key = new KeyEventArgs(Keyboard.PrimaryDevice, PresentationSource.FromVisual(window)!, 0, Key.Enter) { RoutedEvent = Keyboard.KeyDownEvent };
            tile.RaiseEvent(key); await Idle();
            Check(page + "-enter-preview", () => { Assert.IsTrue(key.Handled); Assert.IsTrue(((FrameworkElement)view.FindName("ImagePreview")).IsVisible); Assert.AreEqual(1200, ((BitmapSource)((Image)view.FindName("PreviewImage")).Source).PixelWidth); });
            Snapshot("QUICK_PREVIEW", view); Capture(page + "-preview", window);
            await view.HandleWorkspaceKeyAsync(new KeyEventArgs(Keyboard.PrimaryDevice, PresentationSource.FromVisual(window)!, 0, Key.Escape) { RoutedEvent = Keyboard.PreviewKeyDownEvent }); await Idle();
            Check(page + "-escape-return", () => { Assert.IsFalse(((FrameworkElement)view.FindName("ImagePreview")).IsVisible); Assert.IsTrue(tile.IsKeyboardFocused); });
            // Invoke the real key handler with modifiers; never SendInput/OS coordinates.
            var handled = (bool)typeof(PlanningReferenceTile).GetMethod("HandleKey", BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(tile, [Key.F10, ModifierKeys.Shift])!;
            await Idle();
            Check(page + "-context", () =>
            {
                Assert.IsTrue(handled); Assert.IsTrue(tile.ContextMenu!.IsOpen); Assert.AreSame(tile, tile.ContextMenu.PlacementTarget);
                foreach (var name in new[] { "打开来源", "移出情绪板", "用于当前镜头", "用于灯光", "用于造型", "用于参考仿色", "从策划中移除" })
                { var item = Walk(Peer(tile.ContextMenu)).Single(p => p.GetName() == name && p.GetAutomationControlType() == AutomationControlType.MenuItem); Assert.AreEqual(AutomationControlType.MenuItem, item.GetAutomationControlType()); }
            });
            Snapshot("CONTEXT_MENU-" + page, tile.ContextMenu!); Capture(page + "-menu", tile.ContextMenu!);
            tile.ContextMenu!.IsOpen = false; await Idle(); Check(page + "-menu-return", () => Assert.IsTrue(tile.IsKeyboardFocused));
        }
        foreach (var page in PlanningCenterViewModel.ContentPages) { vm.ContentPage = page; await Idle(); Snapshot("MODULE-" + page, view); }
        vm.ContentPage = "镜头清单"; await Idle();
        var shotRow = Visuals<PlanningShotRow>(view).Single(row => AutomationProperties.GetName(row).StartsWith("镜头 1：", StringComparison.Ordinal));
        Check("shot-peer", () => { Assert.AreEqual(AutomationControlType.Button, Peer(shotRow).GetAutomationControlType()); Assert.IsNotNull(Peer(shotRow).GetPattern(PatternInterface.Invoke)); Assert.IsTrue(Peer(shotRow).IsKeyboardFocusable()); });
        ((IInvokeProvider)Peer(shotRow).GetPattern(PatternInterface.Invoke)!).Invoke(); await Idle(); Check("shot-invoke", () => Assert.IsTrue(vm.IsShotDrawerOpen)); vm.IsShotDrawerOpen = false;
        vm.ContentPage = "文字"; vm.IsPreviewMode = true; await Idle(); Snapshot("PREVIEW_MODE", view); vm.IsPreviewMode = false;
        await vm.ShowBookingCommand.ExecuteAsync(null); await Idle(); Snapshot("BOOKING_PICKER", view); vm.CancelPlanningModalCommand.Execute(null);
        var export = Visuals<Button>(view).Single(x => AutomationProperties.GetName(x) == "导出策划案 PDF"); export.RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); await Idle(); Snapshot("EXPORT_DIALOG", view);
        Check("export-quality", () => { var quality = Walk(Peer(view)).Single(p => p.GetName() == "PDF 导出质量"); Assert.AreEqual(AutomationControlType.ComboBox, quality.GetAutomationControlType()); Assert.IsNotNull(quality.GetPattern(PatternInterface.ExpandCollapse)); });
        var combo = Visuals<ComboBox>(view).Single(x => AutomationProperties.GetName(x) == "PDF 导出质量");
        combo.IsDropDownOpen = true; await Idle(); Snapshot("EXPORT_QUALITY_POPUP", combo);
        Check("quality-item-selection", () => Assert.IsNotNull(Walk(Peer(combo)).Single(p => p.GetName() == "高质量 · 300 DPI" && p.GetAutomationControlType() == AutomationControlType.ListItem).GetPattern(PatternInterface.SelectionItem)));
        combo.IsDropDownOpen = false;
        await view.HandleWorkspaceKeyAsync(new KeyEventArgs(Keyboard.PrimaryDevice, PresentationSource.FromVisual(window)!, 0, Key.Escape) { RoutedEvent = Keyboard.PreviewKeyDownEvent });
        await vm.EnterTetherCommand.ExecuteAsync(null); await Idle(); Snapshot("TETHER", window);
        await main.ReturnToPlanningCommand.ExecuteAsync(null); await Idle(); Check("return-planning", () => Assert.IsTrue(main.IsPlanningPage));
        main.NavigateCommand.Execute("OnlineSelection"); await Idle(); Snapshot("ONLINE_SELECTION", window);
        Check("online-route", () => Assert.AreEqual("OnlineSelection", main.CurrentPage));
        File.WriteAllText(Path.Combine(output, "result.json"), JsonSerializer.Serialize(new { Status = "PASS", Checks = checks.Count, EvidenceKind = "IN_PROCESS_PRODUCTION_WPF_PEERS", InstalledAcceptance = false, NativeFileDialog = "NOT_TESTED", Source = Environment.GetEnvironmentVariable("PIXEL_TART_CONTRACT_SOURCE") }));
    }
}
