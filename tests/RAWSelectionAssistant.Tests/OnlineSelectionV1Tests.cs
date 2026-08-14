using System.Text.Json;
using RAWSelectionAssistant.Core.Models;
using RAWSelectionAssistant.Core.Services;
using RAWSelectionAssistant.Core.Services.OnlineSelection;

namespace RAWSelectionAssistant.Tests;

[TestClass]
public sealed class OnlineSelectionV1Tests
{
    [TestMethod]
    public void DefaultProvider_IsNoneAndNeverPretendsConfigured()
    {
        var provider = OnlineSelectionProviderFactory.CreateDefault();
        Assert.IsInstanceOfType<NoneOnlineSelectionProvider>(provider);
        Assert.AreEqual(OnlineSelectionProviderKind.None, provider.Kind);
        Assert.IsFalse(provider.IsConfigured);
        Assert.AreEqual("未配置", provider.DisplayName);
    }

    [TestMethod]
    public async Task NoneProvider_AllOperationsReturnExplicitNotConfigured()
    {
        var provider = new NoneOnlineSelectionProvider();
        var project = Project();
        var create = await provider.CreateProjectAsync(project);
        var upload = await provider.UploadAssetAsync(project.Id, Asset(project.Id), new MemoryStream([1, 2, 3]));
        var progress = await provider.GetSelectionProgressAsync(project.Id);
        Assert.IsFalse(create.Success); Assert.AreEqual(OnlineSelectionErrorCodes.ProviderNotConfigured, create.ErrorCode);
        Assert.IsFalse(upload.Success); Assert.AreEqual(OnlineSelectionErrorCodes.ProviderNotConfigured, upload.ErrorCode);
        Assert.IsFalse(progress.Success); Assert.AreEqual("在线选片服务尚未配置。", progress.Message);
    }

    [TestMethod]
    public void ProjectFactory_GeneratesOpaquePublicIdWithoutCustomerData()
    {
        var project = SelectionProjectFactory.CreateDraft("秋季婚礼", "林女士", 30);
        Assert.AreEqual(32, project.PublicId.Length);
        Assert.DoesNotContain("林", project.PublicId, StringComparison.Ordinal);
        Assert.AreNotEqual(Guid.Empty, project.Id);
    }

    [TestMethod]
    [DataRow(SelectionProjectStatus.Draft, "草稿")]
    [DataRow(SelectionProjectStatus.Uploading, "上传中")]
    [DataRow(SelectionProjectStatus.Ready, "待发布")]
    [DataRow(SelectionProjectStatus.Published, "已发布")]
    [DataRow(SelectionProjectStatus.Selecting, "客户选片中")]
    [DataRow(SelectionProjectStatus.ClientConfirmed, "客户已确认")]
    [DataRow(SelectionProjectStatus.Closed, "已关闭")]
    [DataRow(SelectionProjectStatus.Archived, "已归档")]
    public void ProjectStatus_HasChinesePresentation(SelectionProjectStatus status, string expected) =>
        Assert.AreEqual(expected, SelectionDisplayText.ProjectStatus(status));

    [TestMethod]
    [DataRow(SelectionAssetStatus.LocalOnly, "仅本地")]
    [DataRow(SelectionAssetStatus.Queued, "等待上传")]
    [DataRow(SelectionAssetStatus.Uploading, "上传中")]
    [DataRow(SelectionAssetStatus.Ready, "已就绪")]
    [DataRow(SelectionAssetStatus.Failed, "上传失败")]
    [DataRow(SelectionAssetStatus.DeletedCloudCopy, "云端副本已删除")]
    public void AssetStatus_HasChinesePresentation(SelectionAssetStatus status, string expected) =>
        Assert.AreEqual(expected, SelectionDisplayText.AssetStatus(status));

    [TestMethod]
    public void PublishGate_RequiresProjectRuleAndReadyAsset()
    {
        var project = Project();
        var rule = SelectionRule.Default(project.Id, project.TargetCount);
        var noAsset = SelectionProjectValidator.ValidateForPublish(project, rule, []);
        var localOnly = SelectionProjectValidator.ValidateForPublish(project, rule, [Asset(project.Id)]);
        var ready = SelectionProjectValidator.ValidateForPublish(project, rule, [Asset(project.Id) with { Status = SelectionAssetStatus.Ready }]);
        Assert.AreEqual(OnlineSelectionErrorCodes.NoReadyAssets, noAsset.ErrorCode);
        Assert.AreEqual(OnlineSelectionErrorCodes.NoReadyAssets, localOnly.ErrorCode);
        Assert.IsTrue(ready.IsValid);
    }

    [TestMethod]
    public void ClientProjection_HidesPathsCloudIdsAndOperationalState()
    {
        var asset = Asset(Guid.NewGuid()) with
        {
            OriginalFileName = @"C:\客户\婚礼\IMG_0012.JPG",
            LocalSourcePath = @"C:\客户\婚礼\IMG_0012.JPG",
            ProxyJpegPath = @"D:\cache\private.jpg",
            CloudAssetId = "admin-object-key",
            LastErrorCode = "private-error"
        };
        var client = SelectionPrivacyPolicy.ToClientAsset(asset, "https://cdn.invalid/signed");
        Assert.AreEqual("IMG_0012.JPG", client.FileName);
        var json = JsonSerializer.Serialize(client);
        Assert.DoesNotContain("客户", json, StringComparison.Ordinal);
        Assert.DoesNotContain("admin-object-key", json, StringComparison.Ordinal);
        Assert.DoesNotContain("private-error", json, StringComparison.Ordinal);
    }

    [TestMethod]
    public async Task UploadQueue_OneFailureDoesNotBlockOtherAssets()
    {
        var project = Project();
        var provider = new FakeOnlineSelectionProvider { FailUploadWhen = asset => asset.OriginalFileName.Contains("FAIL", StringComparison.Ordinal) };
        var queue = new SelectionUploadQueue(provider);
        var failed = Asset(project.Id, "FAIL.JPG") with { ProxyJpegPath = "FAIL_proxy.jpg" };
        var ready = Asset(project.Id, "OK.JPG") with { ProxyJpegPath = "OK_proxy.jpg" };
        queue.Enqueue(failed, _ => ValueTask.FromResult<Stream>(new MemoryStream([1, 2])));
        queue.Enqueue(ready, _ => ValueTask.FromResult<Stream>(new MemoryStream([1, 2, 3, 4])));
        await queue.RunAsync();
        Assert.AreEqual(SelectionAssetStatus.Failed, queue.Items.Single(item => item.AssetId == failed.Id).State);
        Assert.AreEqual(SelectionAssetStatus.Ready, queue.Items.Single(item => item.AssetId == ready.Id).State);
    }

    [TestMethod]
    public async Task UploadQueue_PauseAndResumeHaveExplicitState()
    {
        var queue = new SelectionUploadQueue(new FakeOnlineSelectionProvider());
        var asset = Asset(Guid.NewGuid()) with { ProxyJpegPath = "queued_proxy.jpg" };
        queue.Enqueue(asset, _ => ValueTask.FromResult<Stream>(new MemoryStream([1])));
        queue.Pause();
        await queue.RunAsync();
        Assert.AreEqual(SelectionUploadQueueState.Paused, queue.State);
        Assert.AreEqual(SelectionAssetStatus.Queued, queue.Items[0].State);
        await queue.ResumeAsync();
        Assert.AreEqual(SelectionAssetStatus.Ready, queue.Items[0].State);
    }

    [TestMethod]
    public async Task CloudDeleteContract_DoesNotTouchLocalFile()
    {
        using var temp = new TempDirectory();
        var local = temp.CreateFile("IMG_0099.JPG", [1, 2, 3]);
        var provider = new FakeOnlineSelectionProvider();
        var project = Project();
        var asset = Asset(project.Id) with { LocalSourcePath = local };
        await provider.DeleteCloudAssetAsync(project.Id, asset.Id);
        Assert.IsTrue(File.Exists(local));
        CollectionAssert.AreEqual(new byte[] { 1, 2, 3 }, File.ReadAllBytes(local));
    }

    [TestMethod]
    public async Task ProxyJpeg_CreateNewAutoNumbersAndLeavesSourceUntouched()
    {
        using var temp = new TempDirectory();
        var source = temp.CreateFile("IMG_0001.JPG", [1, 2, 3, 4]);
        var output = temp.Combine("proxies"); Directory.CreateDirectory(output);
        await File.WriteAllBytesAsync(Path.Combine(output, "IMG_0001_proxy.jpg"), [9]);
        var service = new SelectionProxyJpegService(new PassThroughJpegProxyRenderer());
        var result = await service.GenerateAsync(source, output);
        Assert.AreEqual(SelectionProxyState.Ready, result.State);
        Assert.EndsWith("IMG_0001_proxy_2.jpg", result.OutputPath, StringComparison.OrdinalIgnoreCase);
        CollectionAssert.AreEqual(new byte[] { 1, 2, 3, 4 }, File.ReadAllBytes(source));
        CollectionAssert.AreEqual(new byte[] { 9 }, File.ReadAllBytes(Path.Combine(output, "IMG_0001_proxy.jpg")));
    }

    [TestMethod]
    public async Task ProxyJpeg_UnsupportedNeverCreatesOutput()
    {
        using var temp = new TempDirectory();
        var source = temp.CreateFile("unsafe.psd", [1]);
        var output = temp.Combine("proxies");
        var result = await new SelectionProxyJpegService(new PassThroughJpegProxyRenderer()).GenerateAsync(source, output);
        Assert.AreEqual(SelectionProxyState.Unsupported, result.State);
        Assert.IsFalse(Directory.Exists(output));
        Assert.IsTrue(File.Exists(source));
    }

    [TestMethod]
    public async Task FinalSelection_MatchesRawByStableBaseNameAndArchivesWithoutRawPath()
    {
        using var temp = new TempDirectory();
        var raw = temp.CreateFile("IMG_0012.ARW", [7, 8]);
        var project = Project();
        var result = new SelectionFinalResult(project.Id, DateTimeOffset.UtcNow,
            [new(project.Id, Guid.NewGuid(), "IMG_0012.JPG", true, true, "精修皮肤", false)]);
        var sync = await new SelectionResultSyncService(new FileNameNormalizer()).SynchronizeAsync(result, [raw], temp.Combine("archive"));
        Assert.AreEqual(SelectionSyncState.Completed, sync.State);
        Assert.AreEqual(raw, sync.Matches.Single().RawPath);
        var archive = await File.ReadAllTextAsync(sync.ArchivePath!);
        Assert.DoesNotContain(temp.Path, archive, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(".ARW", archive, StringComparison.OrdinalIgnoreCase);
        Assert.IsTrue(File.Exists(raw));
    }

    [TestMethod]
    public async Task FinalSelection_IgnoresSameNamedJpegAndOnlyMatchesRawExtensions()
    {
        using var temp = new TempDirectory();
        var jpeg = temp.CreateFile("IMG_0013.JPG", [1, 2, 3]);
        var raw = temp.CreateFile("IMG_0013.NEF", [4, 5, 6]);
        var project = Project();
        var result = new SelectionFinalResult(project.Id, DateTimeOffset.UtcNow,
            [new(project.Id, Guid.NewGuid(), "IMG_0013.JPG", true, false, null, false)]);
        var service = new SelectionResultSyncService(new FileNameNormalizer());

        var matched = await service.SynchronizeAsync(result, [jpeg, raw], temp.Combine("matched"));
        var jpegOnly = await service.SynchronizeAsync(result, [jpeg], temp.Combine("jpeg-only"));

        Assert.AreEqual(SelectionRawMatchStatus.Matched, matched.Matches.Single().Status);
        Assert.AreEqual(raw, matched.Matches.Single().RawPath);
        CollectionAssert.AreEqual(new[] { raw }, matched.Matches.Single().Candidates.ToArray());
        Assert.AreEqual(SelectionRawMatchStatus.NotFound, jpegOnly.Matches.Single().Status);
        Assert.IsNull(jpegOnly.Matches.Single().RawPath);
        Assert.IsTrue(File.Exists(jpeg));
        Assert.IsTrue(File.Exists(raw));
    }

    [TestMethod]
    public async Task FinalSelection_AmbiguousRawRequiresAttentionWithoutCopyMoveDelete()
    {
        using var temp = new TempDirectory();
        var first = temp.CreateFile("A/IMG_0020.NEF", [1]);
        var second = temp.CreateFile("B/IMG_0020.CR3", [2]);
        var project = Project();
        var result = new SelectionFinalResult(project.Id, DateTimeOffset.UtcNow,
            [new(project.Id, Guid.NewGuid(), "IMG_0020.JPG", true, false, null, false)]);
        var sync = await new SelectionResultSyncService(new FileNameNormalizer()).SynchronizeAsync(result, [first, second], temp.Combine("archive"));
        Assert.AreEqual(SelectionSyncState.NeedsAttention, sync.State);
        Assert.AreEqual(SelectionRawMatchStatus.Conflict, sync.Matches.Single().Status);
        Assert.IsTrue(File.Exists(first)); Assert.IsTrue(File.Exists(second));
    }

    [TestMethod]
    public async Task JsonWorkspaceStore_WritesAndReloadsSnapshotInTemporaryDirectory()
    {
        using var temp = new TempDirectory();
        var project = Project();
        var snapshot = new SelectionWorkspaceSnapshot([project], [Asset(project.Id)], [SelectionRule.Default(project.Id, project.TargetCount)], []);
        var store = new JsonSelectionWorkspaceStore(temp.Combine("selection", "workspace.json"));
        await store.SaveAsync(snapshot);
        var loaded = await new JsonSelectionWorkspaceStore(store.FilePath).LoadAsync();
        Assert.HasCount(1, loaded.Projects);
        Assert.AreEqual(project.Id, loaded.Projects[0].Id);
    }

    [TestMethod]
    public async Task JsonWorkspaceStore_RoundTripsMultipleProjectsAssetsRulesAndFinalResults()
    {
        using var temp = new TempDirectory();
        var first = Project();
        var second = Project();
        var firstAsset = Asset(first.Id, "FIRST.JPG");
        var secondAsset = Asset(second.Id, "SECOND.JPG");
        var firstResult = FinalResult(first, firstAsset, selected: true);
        var secondResult = FinalResult(second, secondAsset, selected: false);
        var snapshot = new SelectionWorkspaceSnapshot(
            [first, second],
            [firstAsset, secondAsset],
            [SelectionRule.Default(first.Id, first.TargetCount), SelectionRule.Default(second.Id, second.TargetCount)],
            [firstResult, secondResult]);
        var store = new JsonSelectionWorkspaceStore(temp.Combine("selection", "workspace.json"));

        await store.SaveAsync(snapshot);
        var loaded = await new JsonSelectionWorkspaceStore(store.FilePath).LoadAsync();

        CollectionAssert.AreEquivalent(new[] { first.Id, second.Id }, loaded.Projects.Select(item => item.Id).ToArray());
        CollectionAssert.AreEquivalent(new[] { firstAsset.Id, secondAsset.Id }, loaded.Assets.Select(item => item.Id).ToArray());
        CollectionAssert.AreEquivalent(new[] { first.Id, second.Id }, loaded.Rules.Select(item => item.ProjectId).ToArray());
        CollectionAssert.AreEquivalent(new[] { first.Id, second.Id }, loaded.FinalResults.Select(item => item.SelectionProjectId).ToArray());
    }

    [TestMethod]
    public async Task JsonWorkspaceStore_RoundTripsLocalChoicesAndComments()
    {
        using var temp = new TempDirectory();
        var project = Project();
        var asset = Asset(project.Id);
        var now = DateTimeOffset.UtcNow;
        var snapshot = new SelectionWorkspaceSnapshot([project], [asset], [SelectionRule.Default(project.Id, project.TargetCount)], [])
        {
            Choices = [new SelectionChoice(project.Id, asset.Id, true, true, false, now)],
            Comments = [new SelectionComment(Guid.NewGuid(), project.Id, asset.Id, "本地备注", now, now)]
        };
        var store = new JsonSelectionWorkspaceStore(temp.Combine("selection", "workspace.json"));
        await store.SaveAsync(snapshot);
        var loaded = await store.LoadAsync();
        Assert.IsTrue(loaded.Choices.Single().Selected);
        Assert.AreEqual("本地备注", loaded.Comments.Single().CustomerNote);
    }

    [TestMethod]
    public async Task JsonWorkspaceStore_CorruptSnapshotFailsWithoutReplacingOriginal()
    {
        using var temp = new TempDirectory();
        var path = temp.CreateFile("selection/workspace.json", "{ damaged"u8.ToArray());
        var original = await File.ReadAllBytesAsync(path);
        var store = new JsonSelectionWorkspaceStore(path);

        var exception = await Assert.ThrowsExactlyAsync<InvalidDataException>(() => store.LoadAsync());

        StringAssert.Contains(exception.Message, "数据损坏");
        CollectionAssert.AreEqual(original, await File.ReadAllBytesAsync(path));
        Assert.HasCount(1, Directory.GetFiles(Path.GetDirectoryName(path)!));
    }

    [TestMethod]
    public async Task ProxyJpeg_FailureCleansOnlyOwnedStagingAndPreservesCompetitorFile()
    {
        using var temp = new TempDirectory();
        var source = temp.CreateFile("IMG_0042.JPG", [1, 2, 3]);
        var output = temp.Combine("proxies");
        Directory.CreateDirectory(output);
        var competitor = Path.Combine(output, "IMG_0042_proxy.jpg");
        await File.WriteAllBytesAsync(competitor, [9, 8, 7]);
        var service = new SelectionProxyJpegService(new ThrowingProxyRenderer());

        var result = await service.GenerateAsync(source, output);

        Assert.AreEqual(SelectionProxyState.Failed, result.State);
        CollectionAssert.AreEqual(new byte[] { 9, 8, 7 }, await File.ReadAllBytesAsync(competitor));
        CollectionAssert.AreEqual(new byte[] { 1, 2, 3 }, await File.ReadAllBytesAsync(source));
        Assert.HasCount(1, Directory.GetFiles(output));
    }

    [TestMethod]
    public async Task ResultArchive_CreateNewAutoNumbersFlushesAndNeverOverwritesExistingArchive()
    {
        using var temp = new TempDirectory();
        var raw = temp.CreateFile("IMG_0100.ARW", [5, 4, 3]);
        var project = Project();
        var asset = Asset(project.Id, "IMG_0100.JPG");
        var confirmedAt = new DateTimeOffset(2026, 8, 11, 8, 9, 10, TimeSpan.Zero);
        var result = new SelectionFinalResult(project.Id, confirmedAt,
            [new(project.Id, asset.Id, asset.OriginalFileName, true, false, null, false)]);
        var archiveDirectory = temp.Combine("archive");
        var service = new SelectionResultSyncService(new FileNameNormalizer());

        var first = await service.SynchronizeAsync(result, [raw], archiveDirectory);
        var firstBytes = await File.ReadAllBytesAsync(first.ArchivePath!);
        var second = await service.SynchronizeAsync(result, [raw], archiveDirectory);

        Assert.AreNotEqual(first.ArchivePath, second.ArchivePath);
        Assert.EndsWith("_2.json", second.ArchivePath, StringComparison.OrdinalIgnoreCase);
        CollectionAssert.AreEqual(firstBytes, await File.ReadAllBytesAsync(first.ArchivePath!));
        Assert.IsGreaterThan(0, new FileInfo(second.ArchivePath!).Length);
        Assert.HasCount(2, Directory.GetFiles(archiveDirectory));
    }

    [TestMethod]
    public async Task ResultArchive_RequiresExplicitDestinationDirectory()
    {
        var project = Project();
        var result = new SelectionFinalResult(project.Id, DateTimeOffset.UtcNow, []);
        var service = new SelectionResultSyncService(new FileNameNormalizer());
        await Assert.ThrowsExactlyAsync<ArgumentException>(() => service.SynchronizeAsync(result, [], string.Empty));
    }

    [TestMethod]
    public void SelectionAsset_UsesExistingIdAsStableSelectionAssetIdAndOptionalSourceReference()
    {
        var project = Project();
        var source = Guid.NewGuid();
        var asset = Asset(project.Id) with { SourceAssetId = source };
        Assert.AreEqual(asset.Id, asset.SelectionAssetId);
        Assert.AreEqual(source, asset.SourceAssetId);
        Assert.AreEqual("IMG_0012", asset.OriginalStem);
        var candidate = SelectionAssetFactory.Create(project.Id, new SelectionAssetImportCandidate(@"C:\photos\IMG_0100.JPG", source), 3);
        Assert.AreEqual(source, candidate.SourceAssetId);
        Assert.AreEqual("IMG_0100.JPG", candidate.OriginalFileName);
    }

    [TestMethod]
    public void ClientChoiceMock_ConfirmsVersionedSnapshotAndCanReopen()
    {
        var project = Project();
        var rule = SelectionRule.Default(project.Id, 1);
        var asset = Asset(project.Id);
        var mock = new SelectionClientChoiceMock();
        mock.SetChoice(project.Id, asset.Id, selected: true, favorite: true);
        mock.SetComment(project.Id, asset.Id, "保留这张");
        var snapshot = mock.Confirm(project, [asset], rule);
        Assert.AreEqual(1, snapshot.SelectionVersion);
        Assert.IsTrue(snapshot.IsLocked);
        CollectionAssert.AreEqual(new[] { asset.Id }, snapshot.AssetIds.ToArray());
        Assert.AreEqual("保留这张", snapshot.AssetItems.Single().CustomerNote);
        var state = mock.Reopen(project.Id);
        Assert.IsFalse(state.IsLocked);
        Assert.IsTrue(mock.GetState(project.Id).IsConfirmed);
    }

    [TestMethod]
    public async Task ResultExport_WritesUtf8WithoutBomAndNoLocalPath()
    {
        using var temp = new TempDirectory();
        var project = Project();
        var item = new SelectionFinalItem(project.Id, Guid.NewGuid(), @"C:\客户\IMG_0012.JPG", true, true, "请保留", false);
        var snapshot = new FinalSelectionSnapshot(project.Id, 2, [item], DateTimeOffset.UtcNow);
        var service = new SelectionResultExportService();
        var txt = await service.ExportTxtAsync(snapshot, temp.Path);
        var csv = await service.ExportCsvAsync(snapshot, temp.Path);
        foreach (var path in new[] { txt, csv })
        {
            var bytes = await File.ReadAllBytesAsync(path);
            Assert.IsFalse(bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF);
            var content = await File.ReadAllTextAsync(path);
            Assert.DoesNotContain(temp.Path, content, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("客户", content, StringComparison.Ordinal);
        }
        StringAssert.Contains(await File.ReadAllTextAsync(txt), "IMG_0012.JPG");
        StringAssert.Contains(await File.ReadAllTextAsync(csv), "SelectionAssetId");
    }

    [TestMethod]
    public void ProductionCoreContainsNoHttpLocalhostOrCredentials()
    {
        var root = FindRepoRoot();
        var files = Directory.GetFiles(Path.Combine(root, "src", "RAWSelectionAssistant.Core", "Services", "OnlineSelection"), "*.cs", SearchOption.AllDirectories)
            .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}TestDoubles{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase));
        var text = string.Join('\n', files.Select(File.ReadAllText));
        Assert.DoesNotContain("localhost", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("HttpClient", text, StringComparison.Ordinal);
        Assert.DoesNotContain("Bearer ", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("FakeOnlineSelectionProvider", text, StringComparison.Ordinal);
    }

    [TestMethod]
    public void FakeProvider_IsCompiledOnlyByTestProject()
    {
        var root = FindRepoRoot();
        var productionFiles = Directory.GetFiles(Path.Combine(root, "src"), "*.cs", SearchOption.AllDirectories)
            .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase)
                && !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(productionFiles.Any(path => File.ReadAllText(path).Contains("class FakeOnlineSelectionProvider", StringComparison.Ordinal)));
        Assert.IsTrue(File.Exists(Path.Combine(root, "tests", "RAWSelectionAssistant.Tests", "FakeOnlineSelectionProvider.cs")));
    }

    [TestMethod]
    public void MiniProgramMock_HasExactlyFivePagesAndCentralizedServices()
    {
        var root = FindRepoRoot();
        var mini = Path.Combine(root, "clients", "wechat-mini-program");
        Assert.IsTrue(File.Exists(Path.Combine(mini, "services", "api.ts")));
        Assert.IsTrue(File.Exists(Path.Combine(mini, "services", "selection-store.ts")));
        using var document = JsonDocument.Parse(File.ReadAllText(Path.Combine(mini, "app.json")));
        var pages = document.RootElement.GetProperty("pages").EnumerateArray().Select(item => item.GetString()).ToArray();
        CollectionAssert.AreEqual(new[] { "pages/project/index", "pages/gallery/index", "pages/photo/index", "pages/selected/index", "pages/confirm/index" }, pages);
        var api = File.ReadAllText(Path.Combine(mini, "services", "api.ts"));
        var store = File.ReadAllText(Path.Combine(mini, "services", "selection-store.ts"));
        var app = File.ReadAllText(Path.Combine(mini, "app.ts"));
        var localDevConfig = File.ReadAllText(Path.Combine(mini, "localdev.config.example.ts"));
        Assert.DoesNotContain("appSecret:", api, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("sessionKey:", api, StringComparison.OrdinalIgnoreCase);
        StringAssert.Contains(api, "X-PixelTart-Dev-Token");
        StringAssert.Contains(api, "expectedSelectionVersion");
        StringAssert.Contains(api, "expectedRevision");
        StringAssert.Contains(api, "operationId");
        StringAssert.Contains(store, "wx.setStorageSync");
        StringAssert.Contains(store, "refreshProject");
        StringAssert.Contains(store, "mediaSession");
        StringAssert.Contains(app, "initializeLocalDev");
        StringAssert.Contains(app, "wx.getStorageSync('pixel-tart-localdev-config/v1')");
        StringAssert.Contains(localDevConfig, "enabled: false");
        StringAssert.Contains(localDevConfig, "devAccessToken: ''");
        Assert.DoesNotContain("ProviderNone') this.confirmed = true", store, StringComparison.Ordinal);
        var pageSources = Directory.GetFiles(Path.Combine(mini, "pages"), "*.ts", SearchOption.AllDirectories)
            .Select(File.ReadAllText).ToArray();
        Assert.IsFalse(pageSources.Any(source => source.Contains("wx.request", StringComparison.Ordinal)));
    }

    [TestMethod]
    public void LocalDevLauncher_WaitsForReadyAndStopsOnlyItsServerChild()
    {
        var root = FindRepoRoot();
        var script = File.ReadAllText(Path.Combine(root, "tools", "PixelTart_OnlineSelection_LocalDev_Preview.ps1"));
        var readyIndex = script.IndexOf("$health.ready", StringComparison.Ordinal);
        var previewIndex = script.IndexOf("$preview = Start-Process", StringComparison.Ordinal);
        Assert.IsGreaterThanOrEqualTo(0, readyIndex);
        Assert.IsGreaterThan(readyIndex, previewIndex);
        StringAssert.Contains(script, "$server.Kill($true)");
        StringAssert.Contains(script, "$server.WaitForExit");
        Assert.DoesNotContain("Get-Process", script, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Stop-Process", script, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("-Name", script, StringComparison.OrdinalIgnoreCase);
    }

    [TestMethod]
    public void LocalDevPreview_IsIsolatedAndFormalAppDoesNotSelectLocalDev()
    {
        var root = FindRepoRoot();
        var formalApp = File.ReadAllText(Path.Combine(root, "src", "RAWSelectionAssistant", "App.xaml.cs"));
        var previewApp = File.ReadAllText(Path.Combine(root, "src", "PixelTart.OnlineSelection.LocalDevPreview", "App.xaml.cs"));
        var provider = File.ReadAllText(Path.Combine(root, "src", "PixelTart.OnlineSelection.LocalDevPreview", "LocalDevOnlineSelectionProvider.cs"));
        StringAssert.Contains(formalApp, "OnlineSelectionProviderFactory.CreateDefault()");
        Assert.DoesNotContain("CreateFromEnvironmentOrNone", formalApp, StringComparison.Ordinal);
        StringAssert.Contains(previewApp, "LocalDevOnlineSelectionProvider");
        StringAssert.Contains(previewApp, "WpfSelectionProxyRenderer");
        StringAssert.Contains(previewApp, "LocalDevPreviewDialogService");
        StringAssert.Contains(provider, "ProtectedData.Protect");
        StringAssert.Contains(provider, "DataProtectionScope.CurrentUser");
        Assert.DoesNotContain("LocalDevOnlineSelectionProvider", formalApp, StringComparison.Ordinal);
        Assert.DoesNotContain("localdev-access.json", provider, StringComparison.OrdinalIgnoreCase);
    }

    private static SelectionProject Project() => SelectionProjectFactory.CreateDraft("城市婚礼", "客户", 30);

    private static SelectionAsset Asset(Guid projectId, string name = "IMG_0012.JPG")
    {
        var now = DateTimeOffset.UtcNow;
        return new(Guid.NewGuid(), projectId, name, Path.GetFullPath(name), null, SelectionAssetStatus.LocalOnly, 0, false, now, now);
    }

    private static SelectionFinalResult FinalResult(SelectionProject project, SelectionAsset asset, bool selected) =>
        new(project.Id, DateTimeOffset.UtcNow, [new(project.Id, asset.Id, asset.OriginalFileName, selected, false, null, false)]);

    private sealed class ThrowingProxyRenderer : ISelectionProxyRenderer
    {
        public string Name => "确定性失败渲染器";

        public async Task RenderJpegAsync(string sourcePath, Stream destination, SelectionProxyOptions options, CancellationToken cancellationToken = default)
        {
            await destination.WriteAsync(new byte[] { 6, 6, 6 }, cancellationToken);
            throw new IOException("测试渲染失败");
        }
    }

    private static string FindRepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "RAWSelectionAssistant.sln"))) directory = directory.Parent;
        return directory?.FullName ?? throw new DirectoryNotFoundException("Repository root not found.");
    }
}
