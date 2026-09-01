using System.IO;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Xml.Linq;
using PixelTart.Modules.AssetLibrary;
using RAWSelectionAssistant.Core.Models;
using RAWSelectionAssistant.Core.Services.Tasks;

namespace RAWSelectionAssistant.WpfTests;

[TestClass]
public sealed class AssetLibraryP2BrowserWpfTests
{
    [TestMethod]
    public void FourLayoutAlgorithmsKeepFiveHundredItemsInsideOneVerticalExtent()
    {
        var ratios = Enumerable.Range(0, 500).Select(index => 0.45d + index % 17 * 0.23d).ToArray();
        var signatures = new HashSet<string>(StringComparer.Ordinal);
        foreach (var mode in Enum.GetValues<AssetLibraryViewMode>())
        {
            var layout = AssetLayoutEngine.Arrange(mode, ratios, 1180d, 176d);
            Assert.HasCount(500, layout.Items, mode.ToString());
            Assert.AreEqual(1180d, layout.Extent.Width, 0.01d, mode.ToString());
            Assert.IsGreaterThan(0d, layout.Extent.Height, mode.ToString());
            Assert.IsTrue(layout.Items.All(rect => rect.Left >= -0.01d && rect.Right <= layout.Extent.Width + 0.01d), $"{mode} produced horizontal overflow.");
            AssertNoOverlap(layout.Items, mode);
            signatures.Add(string.Join("|", layout.Items.Take(12).Select(rect => $"{rect.X:F0},{rect.Y:F0},{rect.Width:F0},{rect.Height:F0}")));
        }
        Assert.HasCount(4, signatures, "The four view modes must use four real layout algorithms.");
    }

    [TestMethod]
    public void XamlKeepsP1IdsAndDeclaresP2TreeViewsSafeContextCommandsAndInspectorStates()
    {
        var document = XDocument.Load(FindRepositoryFile("src", "PixelTart.Modules.AssetLibrary", "AssetLibraryPage.xaml"));
        var allText = document.ToString(SaveOptions.DisableFormatting) +
            File.ReadAllText(FindRepositoryFile("src", "PixelTart.Modules.AssetLibrary", "AssetLibraryViewModel.P2Browser.cs"));
        foreach (var id in new[]
        {
            "AssetLibraryAllAssets", "AssetGrid", "AssetInspectorEmptyState", "AssetVisualContextMenu",
            "AssetViewGrid", "AssetViewMasonry", "AssetViewJustified", "AssetViewList",
            "AssetFolderTree", "AssetOrganizationLoadingState", "AssetOrganizationErrorState", "AssetOrganizationEmptyState",
            "AssetInspectorSingleState", "AssetInspectorMultipleState"
        })
            StringAssert.Contains(allText, id);

        foreach (var header in new[]
        {
            "打开所在位置（仅入口，不自动执行）", "复制路径", "加入当前文件夹", "移出当前文件夹",
            "加入当前标签", "移出当前标签", "评分", "标记缺失", "归档", "恢复", "从当前视图移除", "查看信息"
        })
            StringAssert.Contains(allText, header);
        foreach (var forbidden in new[] { "永久删除", "删除原文件", "覆盖源文件", "Eagle 同步写入" })
            Assert.IsFalse(allText.Contains(forbidden, StringComparison.Ordinal), forbidden);

        StringAssert.Contains(allText, "VirtualizingAssetPanel");
        StringAssert.Contains(allText, "OrganizationFolders");
        StringAssert.Contains(allText, "OrganizationTagGroups");
        StringAssert.Contains(allText, "MoveUpCommand");
        StringAssert.Contains(allText, "MoveDownCommand");
        StringAssert.Contains(allText, "PromoteCommand");
    }

    [TestMethod]
    public async Task VisibleSelectionChangesDoNotDiscardSelectedIdsOutsideTheLoadedPage()
    {
        await RunSta(() =>
        {
            var root = Path.Combine(Path.GetTempPath(), "PixelTart-P2Selection", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            try
            {
                var hiddenId = Guid.NewGuid();
                var visible = CreateAsset(Guid.NewGuid(), 1600, 900);
                var settings = new AssetLibraryWorkspaceSettings { SelectedAssetIds = [hiddenId, visible.AssetId] };
                var viewModel = new AssetLibraryViewModel(Path.Combine(root, "browser.db"), new TaskOperationBridge(), workspaceSettings: settings);

                viewModel.SyncVisibleSelection([visible], [visible.AssetId]);
                CollectionAssert.AreEquivalent(new[] { hiddenId, visible.AssetId }, viewModel.SelectedAssetIds.ToArray());
                Assert.AreEqual(2, viewModel.SelectionCount);
                Assert.IsNull(viewModel.SelectedAsset, "Multiple selection must never expose its first item as the single inspector item.");

                viewModel.SyncVisibleSelection([], [visible.AssetId]);
                CollectionAssert.AreEqual(new[] { hiddenId }, viewModel.SelectedAssetIds.ToArray());
                Assert.AreEqual(1, viewModel.SelectionCount);
                Assert.IsNull(viewModel.SelectedAsset, "An unloaded selected id must stay selected without fabricating single-item metadata.");
                viewModel.DisposeAsync().AsTask().GetAwaiter().GetResult();
            }
            finally { try { Directory.Delete(root, true); } catch { } }
        });
    }

    [TestMethod]
    public async Task EveryViewUsesTheSameAssetGridAndRealizesOnlyTheVisibleWindow()
    {
        await RunSta(() =>
        {
            foreach (var mode in Enum.GetValues<AssetLibraryViewMode>())
            {
                var root = Path.Combine(Path.GetTempPath(), "PixelTart-P2Virtualization", Guid.NewGuid().ToString("N"));
                Directory.CreateDirectory(root);
                try
                {
                    var page = new PixelTart.Modules.AssetLibrary.AssetLibraryPage(
                        Path.Combine(root, "browser.db"),
                        new TaskOperationBridge(),
                        [],
                        workspaceSettings: new AssetLibraryWorkspaceSettings { ViewMode = mode });
                    for (var index = 0; index < 500; index++)
                    {
                        var width = 480 + index % 9 * 210;
                        var height = 420 + index % 11 * 130;
                        page.ViewModel.AssetCards.Add(new(CreateAsset(Guid.NewGuid(), width, height)) { Owner = page.ViewModel });
                    }
                    page.Measure(new Size(1366, 768));
                    page.Arrange(new Rect(0, 0, 1366, 768));
                    page.UpdateLayout();
                    PumpDispatcher();

                    var grid = FindVisualByAutomationId<ListBox>(page, "AssetGrid");
                    var panel = FindVisualChild<VirtualizingAssetPanel>(grid);
                    Assert.IsNotNull(panel, mode.ToString());
                    Assert.AreSame(page.ViewModel.AssetCards, grid.ItemsSource, mode.ToString());
                    Assert.IsGreaterThan(0, panel.RealizedItemCount, mode.ToString());
                    Assert.IsLessThan(150, panel.RealizedItemCount, $"{mode} must not create 500 item containers.");
                    Assert.AreEqual(0d, panel.HorizontalOffset, 0.01d, mode.ToString());
                    page.DisposeAsync().AsTask().GetAwaiter().GetResult();
                }
                finally { try { Directory.Delete(root, true); } catch { } }
            }
        });
    }

    private static AssetItem CreateAsset(Guid id, int width, int height) => new(
        id, $"X:\\fixture\\{id:N}.jpg", $"素材-{id:N}.jpg", ".jpg", "image/jpeg", 1234,
        null, width, height, null, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow);

    private static void AssertNoOverlap(IReadOnlyList<Rect> items, AssetLibraryViewMode mode)
    {
        for (var left = 0; left < items.Count; left++)
            for (var right = left + 1; right < items.Count; right++)
            {
                var intersection = Rect.Intersect(items[left], items[right]);
                Assert.IsTrue(intersection.IsEmpty || intersection.Width <= 0.01d || intersection.Height <= 0.01d,
                    $"{mode} items {left} and {right} overlap: {intersection}.");
            }
    }

    private static T FindVisualByAutomationId<T>(DependencyObject root, string automationId) where T : DependencyObject
    {
        var match = FindVisualDescendants(root).OfType<T>()
            .FirstOrDefault(item => System.Windows.Automation.AutomationProperties.GetAutomationId(item) == automationId);
        return match ?? throw new AssertFailedException($"AutomationId '{automationId}' was not found.");
    }

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
