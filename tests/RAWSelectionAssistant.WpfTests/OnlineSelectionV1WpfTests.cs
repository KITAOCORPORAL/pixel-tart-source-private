using System.IO;
using RAWSelectionAssistant.Core.Models;
using RAWSelectionAssistant.Core.Services;
using RAWSelectionAssistant.Core.Services.OnlineSelection;
using RAWSelectionAssistant.ViewModels;

namespace RAWSelectionAssistant.WpfTests;

[TestClass]
public sealed class OnlineSelectionV1WpfTests
{
    [TestMethod]
    public void ViewModel_DefaultProviderIsNoneAndLocalPrototypeRemainsUsable()
    {
        var viewModel = new OnlineSelectionViewModel();
        Assert.IsFalse(viewModel.IsServiceConfigured);
        Assert.AreEqual("在线选片服务尚未配置", viewModel.ServiceStatusText);
        Assert.IsTrue(viewModel.CreateProjectCommand.CanExecute(null));
    }

    [TestMethod]
    public async Task CreateProject_OpensLocalFourTabWorkspaceWithoutCloudService()
    {
        var viewModel = new OnlineSelectionViewModel();
        viewModel.ProjectName = "婚礼选片";
        viewModel.ClientName = "客户";
        viewModel.TargetCountText = "20";
        await viewModel.CreateProjectAsync([]);
        Assert.IsNotNull(viewModel.SelectedProject);
        Assert.IsTrue(viewModel.ProjectPage.IsProjectOpen);
        Assert.HasCount(4, viewModel.ProjectPage.Tabs);
        CollectionAssert.AreEqual(new[] { "照片", "客户选片", "设置", "交付结果" }, viewModel.ProjectPage.Tabs.Select(tab => tab.Label).ToArray());
    }

    [TestMethod]
    public async Task ImportPhotos_DeduplicatesPathsAndDoesNotModifyFiles()
    {
        var directory = Path.Combine(Path.GetTempPath(), "PixelTart.Selection.WpfTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var photo = Path.Combine(directory, "IMG_0001.JPG");
        await File.WriteAllBytesAsync(photo, [1, 2, 3]);
        try
        {
            var project = SelectionProjectFactory.CreateDraft("项目", "客户", 1);
            var page = new OnlineSelectionProjectViewModel(new NoneOnlineSelectionProvider(), new InMemorySelectionWorkspaceStore(), new SelectionResultSyncService(new FileNameNormalizer()));
            await page.OpenProjectAsync(project);
            await page.ImportAssetsAsync([photo, photo]);
            Assert.HasCount(1, page.Assets);
            CollectionAssert.AreEqual(new byte[] { 1, 2, 3 }, File.ReadAllBytes(photo));
        }
        finally { try { Directory.Delete(directory, true); } catch { } }
    }

    [TestMethod]
    public async Task LocalClientMock_ConfirmsReopensAndExposesExports()
    {
        using var temp = new TestDirectory();
        var photo = temp.Write("IMG_1001.JPG", [1, 2, 3]);
        var project = SelectionProjectFactory.CreateDraft("本地客户 Mock", "客户", 1);
        var page = new OnlineSelectionProjectViewModel(
            new NoneOnlineSelectionProvider(),
            new InMemorySelectionWorkspaceStore(),
            new SelectionResultSyncService(new FileNameNormalizer()));
        await page.OpenProjectAsync(project);
        await page.ImportAssetsAsync([photo]);
        var asset = page.Assets.Single();
        page.SetClientChoice(asset.Id, selected: true, favorite: true);
        page.SetClientComment(asset.Id, "保留");
        await page.ConfirmClientSelectionAsync();
        Assert.IsNotNull(page.FinalSnapshot);
        Assert.IsTrue(page.IsSelectionLocked);
        Assert.AreEqual(SelectionProjectStatus.ClientConfirmed, page.Project!.Status);
        await page.ReopenClientSelectionAsync();
        Assert.IsFalse(page.IsSelectionLocked);
        Assert.AreEqual(SelectionProjectStatus.Selecting, page.Project!.Status);
    }

    [TestMethod]
    public async Task AssetLibraryReferenceImport_PreservesSourceAssetIdWithoutCopying()
    {
        using var temp = new TestDirectory();
        var source = temp.Write("LIBRARY.JPG", [8, 9]);
        var sourceAssetId = Guid.NewGuid();
        var project = SelectionProjectFactory.CreateDraft("素材库来源", "客户", 1);
        var page = new OnlineSelectionProjectViewModel(new NoneOnlineSelectionProvider(), new InMemorySelectionWorkspaceStore(), new SelectionResultSyncService(new FileNameNormalizer()));
        await page.OpenProjectAsync(project);
        await page.ImportAssetReferencesAsync([new SelectionAssetImportCandidate(source, sourceAssetId)]);
        Assert.AreEqual(sourceAssetId, page.Assets.Single().SourceAssetId);
        CollectionAssert.AreEqual(new byte[] { 8, 9 }, File.ReadAllBytes(source));
    }

    [TestMethod]
    public void ViewDefinesARealFourTabWorkspaceAndNoneProviderMessage()
    {
        var root = FindRepoRoot();
        var text = File.ReadAllText(Path.Combine(root, "src", "RAWSelectionAssistant", "Views", "OnlineSelectionView.xaml"))
            + File.ReadAllText(Path.Combine(root, "src", "RAWSelectionAssistant", "ViewModels", "OnlineSelectionViewModels.cs"));
        StringAssert.Contains(text, "在线选片服务尚未配置");
        foreach (var token in new[] { "照片", "客户选片", "设置", "交付结果", "同步归片工作区", "创建并导入照片", "确认选片", "重新开放", "导出 TXT", "导出 CSV" })
            StringAssert.Contains(text, token);
        Assert.DoesNotContain("localhost", text, StringComparison.OrdinalIgnoreCase);
    }

    [TestMethod]
    public void OnlineSelectionFilesDoNotModifyShellCalendarOrBookingContracts()
    {
        var root = FindRepoRoot();
        var view = File.ReadAllText(Path.Combine(root, "src", "RAWSelectionAssistant", "Views", "OnlineSelectionView.xaml"));
        var vm = File.ReadAllText(Path.Combine(root, "src", "RAWSelectionAssistant", "ViewModels", "OnlineSelectionViewModels.cs"));
        foreach (var forbidden in new[] { "MainWindow", "MainViewModel", "Booking", "CalendarViewModel", "ToolRegistry" })
            Assert.DoesNotContain(forbidden, view + vm, StringComparison.Ordinal);
    }

    private static string FindRepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "RAWSelectionAssistant.sln"))) directory = directory.Parent;
        return directory?.FullName ?? throw new DirectoryNotFoundException("Repository root not found.");
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
            try { Directory.Delete(Path, true); } catch { }
        }
    }
}
