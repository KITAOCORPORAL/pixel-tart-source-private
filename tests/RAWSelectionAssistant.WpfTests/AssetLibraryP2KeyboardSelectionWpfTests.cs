using System.Reflection;
using System.IO;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using PixelTart.Modules.AssetLibrary;
using RAWSelectionAssistant.Core.Models;
using RAWSelectionAssistant.Core.Services.Tasks;
using AssetLibraryPageControl = PixelTart.Modules.AssetLibrary.AssetLibraryPage;

namespace RAWSelectionAssistant.WpfTests;

[TestClass]
public sealed class AssetLibraryP2KeyboardSelectionWpfTests
{
    [TestMethod]
    public void AssetGridDeclaresKeyboardNavigationAndMarqueeSelectionContracts()
    {
        var pageSource = File.ReadAllText(FindRepositoryFile("src", "PixelTart.Modules.AssetLibrary", "AssetLibraryPage.cs"));
        var xamlSource = File.ReadAllText(FindRepositoryFile("src", "PixelTart.Modules.AssetLibrary", "AssetLibraryPage.xaml"));
        var dragSource = File.ReadAllText(FindRepositoryFile("src", "PixelTart.Modules.AssetLibrary", "AssetLibraryDragDropBehavior.cs"));

        foreach (var token in new[]
        {
            "Key.Home", "Key.End", "Key.PageUp", "Key.PageDown", "ModifierKeys.Control",
            "SelectAllVisibleAssets", "NavigateAssetGrid", "GetPageTargetIndex", "SyncVisibleSelection",
            "PreviewMouseLeftButtonDown", "PreviewMouseLeftButtonUp", "GetIntersectingCards",
            "AssetSelectionMarquee", "CancelMarqueeSelection", "_pendingSelectionSync",
            "DispatcherPriority.DataBind", "FlushAssetGridSelection"
        })
            StringAssert.Contains(pageSource, token);

        foreach (var token in new[]
        {
            "SelectionMode=\"Extended\"", "Background=\"Transparent\"",
            "AutomationProperties.AutomationId=\"AssetSelectionMarquee\"",
            "AutomationProperties.AutomationId=\"AssetSelectionMarqueeLayer\""
        })
            StringAssert.Contains(xamlSource, token);

        StringAssert.Contains(dragSource, "Only an actual card may initiate the metadata-only asset drag.");
    }

    [TestMethod]
    public async Task KeyboardNavigationUsesLayoutPagesAndCtrlASelectsAllVisibleCards()
    {
        var root = Path.Combine(Path.GetTempPath(), "PixelTart-P2Keyboard", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            await RunSta(() =>
            {
                var page = new AssetLibraryPageControl(Path.Combine(root, "browser.db"), new TaskOperationBridge(), []);
                try
                {
                    page.ViewModel.InitializeAsync().GetAwaiter().GetResult();
                    for (var index = 0; index < 80; index++)
                    {
                        var width = 420 + index % 7 * 100;
                        var height = 360 + index % 9 * 80;
                        page.ViewModel.AssetCards.Add(new(CreateAsset(Guid.NewGuid(), width, height)) { Owner = page.ViewModel });
                    }

                    page.Measure(new Size(1280, 760));
                    page.Arrange(new Rect(0, 0, 1280, 760));
                    page.UpdateLayout();
                    PumpDispatcher();

                    var grid = FindVisualByAutomationId<ListBox>(page, "AssetGrid");
                    var panel = FindVisualChild<VirtualizingAssetPanel>(grid);
                    Assert.IsNotNull(panel);

                    InvokePrivate(page, "NavigateAssetGrid", Key.Home);
                    Assert.AreEqual(0, grid.SelectedIndex, "Home must select the first card.");

                    var pageDownTarget = panel!.GetPageTargetIndex(0, forward: true);
                    Assert.IsGreaterThan(0, pageDownTarget, "PageDown must advance by at least one item.");
                    InvokePrivate(page, "NavigateAssetGrid", Key.PageDown);
                    Assert.AreEqual(pageDownTarget, grid.SelectedIndex, "PageDown must use the panel's layout geometry.");

                    InvokePrivate(page, "NavigateAssetGrid", Key.PageUp);
                    Assert.IsLessThan(pageDownTarget, grid.SelectedIndex, "PageUp must move toward the first card.");

                    InvokePrivate(page, "NavigateAssetGrid", Key.End);
                    Assert.AreEqual(page.ViewModel.AssetCards.Count - 1, grid.SelectedIndex, "End must select the last card.");

                    InvokePrivate(page, "SelectAllVisibleAssets");
                    Assert.AreEqual(page.ViewModel.AssetCards.Count, page.ViewModel.SelectionCount,
                        "Ctrl+A's implementation must select every card in the current query.");
                    Assert.IsNull(page.ViewModel.SelectedAsset, "Select-all must expose the multiple-selection inspector state.");
                    Assert.HasCount(page.ViewModel.AssetCards.Count, grid.SelectedItems);
                }
                finally
                {
                    page.DisposeAsync().AsTask().GetAwaiter().GetResult();
                }
            });
        }
        finally
        {
            try { Directory.Delete(root, recursive: true); } catch { }
        }
    }

    [TestMethod]
    public async Task BulkGridSelectionIsCoalescedIntoOneDispatcherSynchronization()
    {
        var root = Path.Combine(Path.GetTempPath(), "PixelTart-P2SelectionCoalesce", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            await RunSta(() =>
            {
                var page = new AssetLibraryPageControl(Path.Combine(root, "browser.db"), new TaskOperationBridge(), []);
                try
                {
                    page.ViewModel.InitializeAsync().GetAwaiter().GetResult();
                    for (var index = 0; index < 120; index++)
                        page.ViewModel.AssetCards.Add(new(CreateAsset(Guid.NewGuid(), 640, 480)) { Owner = page.ViewModel });

                    page.Measure(new Size(1280, 760));
                    page.Arrange(new Rect(0, 0, 1280, 760));
                    page.UpdateLayout();
                    PumpDispatcher();
                    var grid = FindVisualByAutomationId<ListBox>(page, "AssetGrid");

                    for (var index = 0; index < 100; index++) grid.SelectedItems.Add(grid.Items[index]);

                    var pendingField = typeof(AssetLibraryPageControl).GetField("_pendingSelectionSync", BindingFlags.Instance | BindingFlags.NonPublic);
                    Assert.IsNotNull(pendingField);
                    Assert.IsNotNull(pendingField!.GetValue(page), "The first SelectionChanged event must schedule one deferred synchronization.");
                    Assert.AreEqual(0, page.ViewModel.SelectionCount, "The view model must not process each synchronous item-add event.");

                    PumpDispatcher();
                    Assert.AreEqual(100, page.ViewModel.SelectionCount, "The deferred synchronization must apply the final 100-item selection.");
                    Assert.IsNull(pendingField.GetValue(page), "The pending synchronization must be cleared after the dispatcher callback.");
                }
                finally
                {
                    page.DisposeAsync().AsTask().GetAwaiter().GetResult();
                }
            });
        }
        finally
        {
            try { Directory.Delete(root, recursive: true); } catch { }
        }
    }

    [TestMethod]
    public void MarqueeGeometryNormalizesReverseDragAndCtrlToggleIsDeterministic()
    {
        var rectangleMethod = typeof(AssetLibraryPageControl).GetMethod("CreateMarqueeRect", BindingFlags.Static | BindingFlags.NonPublic);
        Assert.IsNotNull(rectangleMethod);
        var rectangle = (Rect)rectangleMethod!.Invoke(null, [new Point(100, 90), new Point(10, 20), new Size(80, 60)])!;
        Assert.AreEqual(10d, rectangle.Left, 0.01d);
        Assert.AreEqual(20d, rectangle.Top, 0.01d);
        Assert.AreEqual(70d, rectangle.Width, 0.01d);
        Assert.AreEqual(40d, rectangle.Height, 0.01d);

        var first = Guid.NewGuid();
        var second = Guid.NewGuid();
        var third = Guid.NewGuid();
        var toggleMethod = typeof(AssetLibraryPageControl).GetMethod("ToggleSelection", BindingFlags.Static | BindingFlags.NonPublic);
        Assert.IsNotNull(toggleMethod);
        var toggled = (HashSet<Guid>)toggleMethod!.Invoke(null, [new[] { first, second }, new[] { second, third }])!;
        CollectionAssert.AreEquivalent(new[] { first, third }, toggled.ToArray());
    }

    private static AssetItem CreateAsset(Guid id, int width, int height) => new(
        id, $"X:\\fixture\\{id:N}.jpg", $"素材-{id:N}.jpg", ".jpg", "image/jpeg", 1234,
        null, width, height, null, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow);

    private static void InvokePrivate(object target, string methodName, params object[] arguments)
    {
        var method = target.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new MissingMethodException(target.GetType().FullName, methodName);
        method.Invoke(target, arguments);
    }

    private static T FindVisualByAutomationId<T>(DependencyObject root, string automationId) where T : DependencyObject =>
        FindVisualDescendants(root).OfType<T>().FirstOrDefault(item =>
            System.Windows.Automation.AutomationProperties.GetAutomationId(item) == automationId)
        ?? throw new AssertFailedException($"AutomationId '{automationId}' was not found.");

    private static T? FindVisualChild<T>(DependencyObject root) where T : DependencyObject =>
        FindVisualDescendants(root).OfType<T>().FirstOrDefault();

    private static IEnumerable<DependencyObject> FindVisualDescendants(DependencyObject root)
    {
        yield return root;
        for (var index = 0; index < VisualTreeHelper.GetChildrenCount(root); index++)
            foreach (var child in FindVisualDescendants(VisualTreeHelper.GetChild(root, index)))
                yield return child;
    }

    private static void PumpDispatcher()
    {
        var frame = new System.Windows.Threading.DispatcherFrame();
        _ = System.Windows.Threading.Dispatcher.CurrentDispatcher.BeginInvoke(
            System.Windows.Threading.DispatcherPriority.Loaded,
            new Action(() => frame.Continue = false));
        System.Windows.Threading.Dispatcher.PushFrame(frame);
    }

    private static string FindRepositoryFile(params string[] parts)
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            var candidate = Path.Combine([directory.FullName, .. parts]);
            if (File.Exists(candidate)) return candidate;
        }
        throw new FileNotFoundException(string.Join(Path.DirectorySeparatorChar, parts));
    }

    private static Task RunSta(Action action)
    {
        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var thread = new Thread(() =>
        {
            try { action(); completion.SetResult(); }
            catch (Exception exception) { completion.SetException(exception); }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        return completion.Task;
    }
}
