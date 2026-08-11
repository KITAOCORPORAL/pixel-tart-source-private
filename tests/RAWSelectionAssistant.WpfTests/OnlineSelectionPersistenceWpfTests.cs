using System.IO;
using System.Text.RegularExpressions;
using RAWSelectionAssistant.Core.Models;
using RAWSelectionAssistant.Core.Services;
using RAWSelectionAssistant.Core.Services.OnlineSelection;
using RAWSelectionAssistant.ViewModels;

namespace RAWSelectionAssistant.WpfTests;

[TestClass]
public sealed class OnlineSelectionPersistenceWpfTests
{
    [TestMethod]
    public async Task ConcurrentProjectSaves_PreserveEachProjectsAssetsRulesAndFinalResults()
    {
        var store = new SnapshotBeforeDelayStore();
        var first = Project("项目甲", 12);
        var second = Project("项目乙", 24);
        var firstAsset = Asset(first.Id, "A001.JPG");
        var secondAsset = Asset(second.Id, "B001.JPG");
        var firstResult = Result(first, firstAsset, selected: true, favorite: false, "保留");
        var secondResult = Result(second, secondAsset, selected: false, favorite: true, "备选");
        var firstPage = Page(store);
        var secondPage = Page(store);
        await firstPage.OpenProjectAsync(first, SelectionRule.Default(first.Id, first.TargetCount), [firstAsset]);
        await secondPage.OpenProjectAsync(second, SelectionRule.Default(second.Id, second.TargetCount), [secondAsset]);

        await Task.WhenAll(
            firstPage.ApplyFinalResultAsync(firstResult),
            secondPage.ApplyFinalResultAsync(secondResult));

        var snapshot = await store.LoadAsync();
        CollectionAssert.AreEquivalent(new[] { first.Id, second.Id }, snapshot.Projects.Select(item => item.Id).ToArray());
        CollectionAssert.AreEquivalent(new[] { firstAsset.Id, secondAsset.Id }, snapshot.Assets.Select(item => item.Id).ToArray());
        CollectionAssert.AreEquivalent(new[] { first.Id, second.Id }, snapshot.Rules.Select(item => item.ProjectId).ToArray());
        CollectionAssert.AreEquivalent(new[] { first.Id, second.Id }, snapshot.FinalResults.Select(item => item.SelectionProjectId).ToArray());
    }

    [TestMethod]
    public async Task ReopenProject_RestoresFinalResultAndAssetChoices()
    {
        var project = Project("重开恢复", 8) with { Status = SelectionProjectStatus.Selecting };
        var asset = Asset(project.Id, "RESTORE.JPG");
        var result = Result(project, asset, selected: true, favorite: true, "客户备注");
        var store = new InMemorySelectionWorkspaceStore(new(
            [project],
            [asset],
            [SelectionRule.Default(project.Id, project.TargetCount)],
            [result]));
        var workspace = new OnlineSelectionViewModel(store: store);

        await workspace.RefreshAsync();
        var item = AssertExactlyOne(workspace.Projects);
        await workspace.OpenProjectAsync(item);

        Assert.AreEqual("客户选片中", item.StatusText);
        Assert.AreSame(result, workspace.ProjectPage.FinalResult);
        var reopenedAsset = AssertExactlyOne(workspace.ProjectPage.Assets);
        Assert.IsTrue(reopenedAsset.IsSelected);
        Assert.IsTrue(reopenedAsset.IsFavorite);
        Assert.AreEqual("客户备注", reopenedAsset.CustomerNote);
        Assert.AreEqual("已选 1/8", workspace.ProjectPage.SelectionSummary);
    }

    [TestMethod]
    public void AssetThumbnail_PrefersExistingProxyImage()
    {
        using var temp = new TestDirectory();
        var invalidSource = temp.Write("source.jpg", [1, 2, 3]);
        var validProxy = temp.Write("proxy.jpg", Convert.FromBase64String(
            "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAusB9Wl2n0kAAAAASUVORK5CYII="));
        var asset = Asset(Guid.NewGuid(), "source.jpg") with { LocalSourcePath = invalidSource, ProxyJpegPath = validProxy };

        var viewModel = new OnlineSelectionAssetViewModel(asset);

        Assert.IsNotNull(viewModel.Thumbnail);
        Assert.AreEqual(validProxy, viewModel.ProxyJpegPath);
        CollectionAssert.AreEqual(new byte[] { 1, 2, 3 }, File.ReadAllBytes(invalidSource));
    }

    [TestMethod]
    public void View_UsesChineseStatusPhotoFirstThumbnailAndAv2Resources()
    {
        var root = FindRepoRoot();
        var view = File.ReadAllText(Path.Combine(root, "src", "RAWSelectionAssistant", "Views", "OnlineSelectionView.xaml"));
        StringAssert.Contains(view, "Text=\"{Binding StatusText}\"");
        StringAssert.Contains(view, "Source=\"{Binding Thumbnail}\"");
        StringAssert.Contains(view, "CardSurface");
        StringAssert.Contains(view, "Av2PrimaryButton");
        StringAssert.Contains(view, "Spacing12Thickness");
        Assert.IsFalse(view.Contains("FontSize=\"", StringComparison.Ordinal));
        Assert.IsFalse(Regex.IsMatch(view, "#[0-9A-Fa-f]{6,8}"));
    }

    private static OnlineSelectionProjectViewModel Page(ISelectionWorkspaceStore store) =>
        new(new NoneOnlineSelectionProvider(), store, new SelectionResultSyncService(new FileNameNormalizer()));

    private static SelectionProject Project(string name, int targetCount) =>
        SelectionProjectFactory.CreateDraft(name, "客户", targetCount);

    private static SelectionAsset Asset(Guid projectId, string name)
    {
        var now = DateTimeOffset.UtcNow;
        return new(Guid.NewGuid(), projectId, name, Path.GetFullPath(name), null, SelectionAssetStatus.LocalOnly, 0, false, now, now);
    }

    private static SelectionFinalResult Result(SelectionProject project, SelectionAsset asset, bool selected, bool favorite, string note) =>
        new(project.Id, DateTimeOffset.UtcNow, [new(project.Id, asset.Id, asset.OriginalFileName, selected, favorite, note, false)]);

    private static T AssertExactlyOne<T>(IEnumerable<T> source)
    {
        var items = source.ToArray();
        Assert.HasCount(1, items);
        return items[0];
    }

    private static string FindRepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "RAWSelectionAssistant.sln"))) directory = directory.Parent;
        return directory?.FullName ?? throw new DirectoryNotFoundException("Repository root not found.");
    }

    private sealed class SnapshotBeforeDelayStore : ISelectionWorkspaceStore
    {
        private readonly object _sync = new();
        private SelectionWorkspaceSnapshot _snapshot = SelectionWorkspaceSnapshot.Empty;

        public async Task<SelectionWorkspaceSnapshot> LoadAsync(CancellationToken cancellationToken = default)
        {
            SelectionWorkspaceSnapshot captured;
            lock (_sync) captured = _snapshot;
            await Task.Delay(25, cancellationToken);
            return captured;
        }

        public Task SaveAsync(SelectionWorkspaceSnapshot snapshot, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            lock (_sync) _snapshot = snapshot;
            return Task.CompletedTask;
        }
    }

    private sealed class TestDirectory : IDisposable
    {
        public TestDirectory()
        {
            Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "PixelTart.Selection.WpfTests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path);
        }

        public string Path { get; }

        public string Write(string name, byte[] bytes)
        {
            var path = System.IO.Path.Combine(Path, name);
            File.WriteAllBytes(path, bytes);
            return path;
        }

        public void Dispose()
        {
            try { Directory.Delete(Path, recursive: true); } catch { }
        }
    }
}
