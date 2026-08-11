using System.ComponentModel;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using RAWSelectionAssistant.Core.Models;
using RAWSelectionAssistant.Core.Services;
using RAWSelectionAssistant.Core.Services.OnlineSelection;
using RAWSelectionAssistant.Core.Services.RawToJpeg;
using RAWSelectionAssistant.Services;
using RAWSelectionAssistant.Services.OnlineSelection;
using RAWSelectionAssistant.ViewModels;

namespace RAWSelectionAssistant.WpfTests;

[TestClass]
public sealed class OnlineSelectionProxyClosureWpfTests
{
    [TestMethod]
    [TestCategory("OnlineSelection")]
    public async Task JpegProxy_Is2560SrgbMetadataFreeAndLeavesSourceUnchanged()
    {
        using var temp = new TestDirectory();
        var source = temp.Combine("private-source-name.jpg");
        WriteJpeg(source, 3000, 1000, "private metadata " + source);
        var before = Snapshot(source);
        var service = new SelectionProxyJpegService(new WpfSelectionProxyRenderer(new UnexpectedRawDecoder()));

        var result = await service.GenerateAsync(source, temp.Combine("proxies"));

        Assert.AreEqual(SelectionProxyState.Ready, result.State, result.Message);
        Assert.IsNotNull(result.OutputPath);
        var frame = ReadFrame(result.OutputPath);
        Assert.AreEqual(2560, Math.Max(frame.PixelWidth, frame.PixelHeight));
        Assert.AreEqual(2560, SelectionProxyOptions.OnlineDefault.LongEdge);
        Assert.AreEqual(85, SelectionProxyOptions.OnlineDefault.Quality);
        AssertMetadataFree(frame);
        Assert.DoesNotContain(Path.GetFileName(source), ReadTextForms(result.OutputPath), StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(source, ReadTextForms(result.OutputPath), StringComparison.OrdinalIgnoreCase);
        AssertSnapshotUnchanged(source, before);
    }

    [TestMethod]
    [TestCategory("OnlineSelection")]
    public async Task RawProxy_UsesExistingDecoderWithPrivateOnlineDefaults()
    {
        using var temp = new TestDirectory();
        var source = temp.Write("capture.ARW", [1, 2, 3, 4]);
        var before = Snapshot(source);
        var decoder = new RecordingRawDecoder();
        var service = new SelectionProxyJpegService(new WpfSelectionProxyRenderer(decoder));

        var result = await service.GenerateAsync(source, temp.Combine("proxies"));

        Assert.AreEqual(SelectionProxyState.Ready, result.State, result.Message);
        Assert.AreEqual(1, decoder.CallCount);
        Assert.IsNotNull(decoder.LastOptions);
        Assert.AreEqual(85, decoder.LastOptions.JpegQuality);
        Assert.AreEqual(2560, decoder.LastOptions.LongestEdge);
        Assert.IsFalse(decoder.LastOptions.PreserveExif);
        Assert.IsFalse(decoder.LastOptions.VerifySha256);
        var frame = ReadFrame(result.OutputPath!);
        Assert.AreEqual(2560, Math.Max(frame.PixelWidth, frame.PixelHeight));
        AssertMetadataFree(frame);
        AssertSnapshotUnchanged(source, before);
    }

    [TestMethod]
    [TestCategory("OnlineSelection")]
    public async Task ImportAssets_PersistsReadyAndFailedItemsAndRestoresAfterRestart()
    {
        using var temp = new TestDirectory();
        var good = temp.Combine("GOOD.JPG");
        WriteJpeg(good, 800, 600, "remove me");
        var bad = temp.Write("BAD.JPG", [8, 6, 7, 5, 3, 0, 9]);
        var goodBefore = Snapshot(good);
        var badBefore = Snapshot(bad);
        var store = new JsonSelectionWorkspaceStore(temp.Combine("workspace", "workspace.json"));
        var service = new SelectionProxyJpegService(new WpfSelectionProxyRenderer(new UnexpectedRawDecoder()));
        var project = SelectionProjectFactory.CreateDraft("本地闭环", "客户", 2);
        var page = NewProjectPage(store, service, temp.Combine("proxies"));
        await page.OpenProjectAsync(project);

        await page.ImportAssetsAsync([good, bad]);

        Assert.HasCount(2, page.Assets);
        var ready = page.Assets.Single(asset => asset.OriginalFileName == "GOOD.JPG");
        var failed = page.Assets.Single(asset => asset.OriginalFileName == "BAD.JPG");
        Assert.AreEqual(SelectionAssetStatus.Ready, ready.Status);
        Assert.IsTrue(File.Exists(ready.ProxyJpegPath));
        Assert.AreEqual(SelectionAssetStatus.Failed, failed.Status);
        Assert.AreEqual(OnlineSelectionErrorCodes.ProxyGenerationFailed, failed.ToModel().LastErrorCode);
        AssertSnapshotUnchanged(good, goodBefore);
        AssertSnapshotUnchanged(bad, badBefore);

        var snapshot = await store.LoadAsync();
        var reopened = NewProjectPage(store, service, temp.Combine("proxies"));
        await reopened.OpenProjectAsync(snapshot.Projects.Single(), snapshot.Rules.Single(), snapshot.Assets);

        Assert.AreEqual(SelectionAssetStatus.Ready, reopened.Assets.Single(asset => asset.OriginalFileName == "GOOD.JPG").Status);
        Assert.AreEqual(SelectionAssetStatus.Failed, reopened.Assets.Single(asset => asset.OriginalFileName == "BAD.JPG").Status);
        Assert.IsFalse(Directory.GetFiles(temp.Combine("proxies"), ".selection-proxy-*.tmp", SearchOption.AllDirectories).Any());
    }

    [TestMethod]
    [TestCategory("OnlineSelection")]
    public async Task ImportAssets_PersistsQueuedBeforeRenderingAndReadyAfterCommit()
    {
        using var temp = new TestDirectory();
        var source = temp.Combine("QUEUED.JPG");
        WriteJpeg(source, 80, 60, null);
        var store = new InMemorySelectionWorkspaceStore();
        var renderer = new BlockingProxyRenderer();
        var project = SelectionProjectFactory.CreateDraft("代理恢复", "客户", 1);
        var page = NewProjectPage(store, new SelectionProxyJpegService(renderer), temp.Combine("proxies"));
        await page.OpenProjectAsync(project);

        var importing = page.ImportAssetsAsync([source]);
        await renderer.Entered.Task.WaitAsync(TimeSpan.FromSeconds(10));

        var queued = (await store.LoadAsync()).Assets.Single();
        Assert.AreEqual(SelectionAssetStatus.Queued, queued.Status);
        Assert.IsNull(queued.ProxyJpegPath);

        renderer.Release.TrySetResult();
        await importing;

        var ready = (await store.LoadAsync()).Assets.Single();
        Assert.AreEqual(SelectionAssetStatus.Ready, ready.Status);
        Assert.IsTrue(File.Exists(ready.ProxyJpegPath));
    }

    [TestMethod]
    [TestCategory("OnlineSelection")]
    public async Task OpenProject_ConvertsInterruptedProxyQueueToPersistedRetryableFailure()
    {
        using var temp = new TestDirectory();
        var source = temp.Combine("INTERRUPTED.JPG");
        WriteJpeg(source, 80, 60, null);
        var project = SelectionProjectFactory.CreateDraft("恢复代理", "客户", 1);
        var now = DateTimeOffset.UtcNow;
        var queued = new SelectionAsset(Guid.NewGuid(), project.Id, Path.GetFileName(source), source, null,
            SelectionAssetStatus.Queued, 0, false, now, now);
        var store = new InMemorySelectionWorkspaceStore();
        await store.SaveAsync(new([project], [queued], [SelectionRule.Default(project.Id, 1)], []));
        var page = NewProjectPage(store,
            new SelectionProxyJpegService(new WpfSelectionProxyRenderer(new UnexpectedRawDecoder())),
            temp.Combine("proxies"));

        await page.OpenProjectAsync(project, SelectionRule.Default(project.Id, 1), [queued]);

        var recovered = page.Assets.Single();
        Assert.AreEqual(SelectionAssetStatus.Failed, recovered.Status);
        Assert.AreEqual(OnlineSelectionErrorCodes.ProxyGenerationFailed, recovered.ToModel().LastErrorCode);
        Assert.AreEqual(SelectionAssetStatus.Failed, (await store.LoadAsync()).Assets.Single().Status);
        Assert.Contains("可重试", page.StatusText, StringComparison.Ordinal);
        Assert.IsTrue(page.RetryFailedCommand.CanExecute(null));
        Assert.IsTrue(File.Exists(source));
    }

    [TestMethod]
    [TestCategory("OnlineSelection")]
    public async Task ProxyDirectoryFailure_MarksEveryItemFailedAndContinuesBatch()
    {
        using var temp = new TestDirectory();
        var first = temp.Combine("FIRST.JPG");
        var second = temp.Combine("SECOND.JPG");
        WriteJpeg(first, 40, 30, null);
        WriteJpeg(second, 40, 30, null);
        var blockedRoot = temp.Write("blocked-root", [1]);
        var store = new InMemorySelectionWorkspaceStore();
        var project = SelectionProjectFactory.CreateDraft("目录失败", "客户", 2);
        var page = NewProjectPage(store,
            new SelectionProxyJpegService(new WpfSelectionProxyRenderer(new UnexpectedRawDecoder())), blockedRoot);
        await page.OpenProjectAsync(project);

        await page.ImportAssetsAsync([first, second]);

        Assert.HasCount(2, page.Assets);
        Assert.IsTrue(page.Assets.All(asset => asset.Status == SelectionAssetStatus.Failed));
        Assert.IsTrue(page.Assets.All(asset => asset.ToModel().LastErrorCode == OnlineSelectionErrorCodes.ProxyGenerationFailed));
        Assert.IsTrue(File.Exists(first));
        Assert.IsTrue(File.Exists(second));
        Assert.HasCount(2, (await store.LoadAsync()).Assets);
    }

    [TestMethod]
    [TestCategory("OnlineSelection")]
    public async Task AddAssetsCommand_WithoutParameterUsesSupportedMultiSelectDialog()
    {
        using var temp = new TestDirectory();
        var source = temp.Combine("ADD.JPG");
        WriteJpeg(source, 80, 60, null);
        var dialogs = new RecordingDialogService([source], null);
        var project = SelectionProjectFactory.CreateDraft("按钮导入", "客户", 1);
        var page = new OnlineSelectionProjectViewModel(
            new NoneOnlineSelectionProvider(),
            new InMemorySelectionWorkspaceStore(),
            new SelectionResultSyncService(new FileNameNormalizer()),
            new SelectionProxyJpegService(new WpfSelectionProxyRenderer(new UnexpectedRawDecoder())),
            temp.Combine("proxies"),
            dialogs);
        await page.OpenProjectAsync(project);
        var completed = WaitForStatusAsync(page, "已导入 1 张照片，代理 JPG 已就绪。");

        page.AddAssetsCommand.Execute(null);
        await completed;

        Assert.AreEqual(1, dialogs.ChooseFilesCalls);
        Assert.IsTrue(dialogs.LastMultiSelect);
        foreach (var extension in new[] { "*.jpg", "*.jpeg", "*.png", "*.tif", "*.tiff", "*.arw", "*.cr2", "*.cr3", "*.dng", "*.nef", "*.nrw", "*.orf", "*.pef", "*.raf", "*.rw2", "*.srw" })
            StringAssert.Contains(dialogs.LastFilter, extension);
        Assert.AreEqual(SelectionAssetStatus.Ready, page.Assets.Single().Status);
    }

    [TestMethod]
    [TestCategory("OnlineSelection")]
    public async Task CreateAndImportCommand_ValidatesBeforePickerAndCancelCreatesDraft()
    {
        var invalidDialogs = new RecordingDialogService([], null);
        var invalid = new OnlineSelectionViewModel(dialogService: invalidDialogs)
        {
            ProjectName = string.Empty,
            ClientName = string.Empty,
            TargetCountText = "0"
        };
        var invalidStatus = WaitForStatusAsync(invalid, "目标数量必须是大于零的数字。");
        invalid.CreateAndImportCommand.Execute(null);
        await invalidStatus;
        Assert.AreEqual(0, invalidDialogs.ChooseFilesCalls);
        Assert.IsNull(invalid.SelectedProject);

        var cancelDialogs = new RecordingDialogService([], null);
        var workspace = new OnlineSelectionViewModel(dialogService: cancelDialogs)
        {
            ProjectName = "仅建草稿",
            ClientName = "客户",
            TargetCountText = "12"
        };
        var created = WaitForStatusAsync(workspace, "选片项目已创建为本地草稿；尚未选择照片。");
        workspace.CreateAndImportCommand.Execute(null);
        await created;

        Assert.AreEqual(1, cancelDialogs.ChooseFilesCalls);
        Assert.IsNotNull(workspace.SelectedProject);
        Assert.HasCount(0, workspace.ProjectPage.Assets);
    }

    [TestMethod]
    [TestCategory("OnlineSelection")]
    public async Task SyncResultsCommand_WithoutParameterChoosesArchiveDirectory()
    {
        using var temp = new TestDirectory();
        var source = temp.Write("IMG_0101.ARW", [1, 2, 3]);
        var archive = temp.Combine("archive");
        var dialogs = new RecordingDialogService([], archive);
        var project = SelectionProjectFactory.CreateDraft("同步", "客户", 1);
        var now = DateTimeOffset.UtcNow;
        var asset = new SelectionAsset(Guid.NewGuid(), project.Id, "IMG_0101.JPG", source, null,
            SelectionAssetStatus.Ready, 0, false, now, now);
        var finalResult = new SelectionFinalResult(project.Id, now,
            [new(project.Id, asset.Id, asset.OriginalFileName, true, false, null, false)]);
        var page = new OnlineSelectionProjectViewModel(
            new NoneOnlineSelectionProvider(),
            new InMemorySelectionWorkspaceStore(),
            new SelectionResultSyncService(new FileNameNormalizer()),
            dialogService: dialogs);
        await page.OpenProjectAsync(project, assets: [asset], finalResult: finalResult);
        var completed = WaitForStatusAsync(page, "客户选片结果已同步并归档。");

        page.SyncResultsCommand.Execute(null);
        await completed;

        Assert.AreEqual(1, dialogs.ChooseFolderCalls);
        Assert.HasCount(1, Directory.GetFiles(archive, "selection-*.json"));
        Assert.IsTrue(File.Exists(source));
    }

    [TestMethod]
    [TestCategory("OnlineSelection")]
    [TestCategory("RawProbe")]
    public async Task PublicRawSamples_WhenPresentGeneratePrivateProxiesWithoutChangingSources()
    {
        var probeDirectory = Path.Combine(Path.GetTempPath(), "pixel-tart-raw-probe");
        if (!Directory.Exists(probeDirectory)) return;
        var sources = new[] { ".arw", ".cr2", ".nef" }
            .Select(extension => Directory.GetFiles(probeDirectory)
                .FirstOrDefault(path => string.Equals(Path.GetExtension(path), extension, StringComparison.OrdinalIgnoreCase)))
            .ToArray();
        if (sources.Any(string.IsNullOrWhiteSpace)) return;
        using var output = new TestDirectory();
        var service = new SelectionProxyJpegService(new WpfSelectionProxyRenderer(new LibRawDecoder()));

        foreach (var source in sources.Select(path => path!))
        {
            var before = Snapshot(source);
            var result = await service.GenerateAsync(source, output.Combine("proxies"));
            Assert.AreEqual(SelectionProxyState.Ready, result.State, $"{Path.GetExtension(source)}: {result.Message}");
            var frame = ReadFrame(result.OutputPath!);
            Assert.IsLessThanOrEqualTo(2560, Math.Max(frame.PixelWidth, frame.PixelHeight));
            AssertMetadataFree(frame);
            Assert.DoesNotContain(Path.GetFileName(source), ReadTextForms(result.OutputPath!), StringComparison.OrdinalIgnoreCase);
            AssertSnapshotUnchanged(source, before);
        }
    }

    private static OnlineSelectionProjectViewModel NewProjectPage(
        ISelectionWorkspaceStore store,
        SelectionProxyJpegService service,
        string proxyRoot) =>
        new(new NoneOnlineSelectionProvider(), store, new SelectionResultSyncService(new FileNameNormalizer()), service, proxyRoot);

    private static Task WaitForStatusAsync(OnlineSelectionProjectViewModel viewModel, string expected) =>
        WaitForStatusAsync(viewModel, () => viewModel.StatusText, expected);

    private static Task WaitForStatusAsync(OnlineSelectionViewModel viewModel, string expected) =>
        WaitForStatusAsync(viewModel, () => viewModel.StatusText, expected);

    private static async Task WaitForStatusAsync(INotifyPropertyChanged source, Func<string> readStatus, string expected)
    {
        if (string.Equals(readStatus(), expected, StringComparison.Ordinal)) return;
        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        PropertyChangedEventHandler? handler = null;
        handler = (_, args) =>
        {
            if (args.PropertyName is nameof(OnlineSelectionViewModel.StatusText) or "" &&
                string.Equals(readStatus(), expected, StringComparison.Ordinal))
            {
                source.PropertyChanged -= handler;
                completion.TrySetResult();
            }
        };
        source.PropertyChanged += handler;
        if (string.Equals(readStatus(), expected, StringComparison.Ordinal))
        {
            source.PropertyChanged -= handler;
            return;
        }
        await completion.Task.WaitAsync(TimeSpan.FromSeconds(15));
    }

    private static void WriteJpeg(string path, int width, int height, string? comment)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var stride = checked(width * 3);
        var pixels = new byte[checked(stride * height)];
        Array.Fill(pixels, (byte)117);
        var bitmap = BitmapSource.Create(width, height, 96, 96, PixelFormats.Bgr24, null, pixels, stride);
        BitmapMetadata? metadata = null;
        if (comment is not null)
        {
            metadata = new BitmapMetadata("jpg") { Comment = comment };
            metadata.SetQuery("/app1/ifd/{ushort=274}", (ushort)1);
        }
        var encoder = new JpegBitmapEncoder { QualityLevel = 96 };
        encoder.Frames.Add(BitmapFrame.Create(bitmap, null, metadata, null));
        using var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None);
        encoder.Save(stream);
    }

    private static BitmapFrame ReadFrame(string path)
    {
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        return BitmapDecoder.Create(stream, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad).Frames[0];
    }

    private static void AssertMetadataFree(BitmapFrame frame)
    {
        var metadata = frame.Metadata as BitmapMetadata;
        Assert.IsTrue(metadata is null || !ContainsQuery(metadata, "/app1/ifd"));
        Assert.IsTrue(metadata is null || string.IsNullOrWhiteSpace(metadata.Comment));
        Assert.IsTrue(frame.ColorContexts is null || frame.ColorContexts.Count == 0);
    }

    private static bool ContainsQuery(BitmapMetadata metadata, string query)
    {
        try { return metadata.ContainsQuery(query); }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException or NotSupportedException) { return false; }
    }

    private static string ReadTextForms(string path)
    {
        var bytes = File.ReadAllBytes(path);
        return Encoding.Latin1.GetString(bytes) + Encoding.Unicode.GetString(bytes);
    }

    private static FileSnapshot Snapshot(string path)
    {
        var info = new FileInfo(path);
        using var stream = File.OpenRead(path);
        return new(info.Length, info.LastWriteTimeUtc, Convert.ToHexString(SHA256.HashData(stream)));
    }

    private static void AssertSnapshotUnchanged(string path, FileSnapshot expected)
    {
        var actual = Snapshot(path);
        Assert.AreEqual(expected.Length, actual.Length);
        Assert.AreEqual(expected.LastWriteTimeUtc, actual.LastWriteTimeUtc);
        Assert.AreEqual(expected.Sha256, actual.Sha256);
    }

    private sealed record FileSnapshot(long Length, DateTime LastWriteTimeUtc, string Sha256);

    private sealed class UnexpectedRawDecoder : IRawDecoder
    {
        public RawDecoderCapability GetCapability() => new(false, "not expected", null, [], [], "not expected");

        public Task<RawDecodedImage> DecodeAsync(string sourcePath, RawToJpegOptions options, CancellationToken cancellationToken = default) =>
            throw new AssertFailedException("Ordinary image proxy unexpectedly used the RAW decoder.");
    }

    private sealed class RecordingRawDecoder : IRawDecoder
    {
        public int CallCount { get; private set; }
        public RawToJpegOptions? LastOptions { get; private set; }

        public RawDecoderCapability GetCapability() => new(true, "recording", "1", [".ARW"], [".ARW"]);

        public Task<RawDecodedImage> DecodeAsync(string sourcePath, RawToJpegOptions options, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            CallCount++;
            LastOptions = options;
            const int width = 3000;
            const int height = 100;
            const int stride = width * 3;
            var pixels = new byte[stride * height];
            Array.Fill(pixels, (byte)128);
            return Task.FromResult(new RawDecodedImage(width, height, stride, pixels,
                new RawImageMetadata("test", "test", null, 1, "sRGB")));
        }
    }

    private sealed class BlockingProxyRenderer : ISelectionProxyRenderer
    {
        public string Name => "blocking";
        public TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public async Task RenderJpegAsync(
            string sourcePath,
            Stream destination,
            SelectionProxyOptions options,
            CancellationToken cancellationToken = default)
        {
            Entered.TrySetResult();
            await Release.Task.WaitAsync(cancellationToken);
            var pixels = new byte[] { 90, 110, 130 };
            var bitmap = BitmapSource.Create(1, 1, 96, 96, PixelFormats.Bgr24, null, pixels, 3);
            var encoder = new JpegBitmapEncoder { QualityLevel = options.Quality };
            encoder.Frames.Add(BitmapFrame.Create(bitmap, null, null, null));
            encoder.Save(destination);
        }
    }

    private sealed class RecordingDialogService(IReadOnlyList<string> files, string? folder) : IDialogService
    {
        public int ChooseFilesCalls { get; private set; }
        public int ChooseFolderCalls { get; private set; }
        public string LastFilter { get; private set; } = string.Empty;
        public bool LastMultiSelect { get; private set; }

        public string? ChooseFolder(string title, string? initialDirectory = null)
        {
            ChooseFolderCalls++;
            return folder;
        }

        public IReadOnlyList<string> ChooseFiles(string title, string filter, bool multiselect = true)
        {
            ChooseFilesCalls++;
            LastFilter = filter;
            LastMultiSelect = multiselect;
            return files;
        }

        public string? ChooseSaveFile(string title, string filter, string defaultExtension, string? suggestedFileName = null) => null;
        public IReadOnlyList<string>? ManageQuickTools(IReadOnlyList<string> currentToolIds) => null;
        public void ShowInfo(string message) { }
        public void ShowError(string message) { }
        public bool Confirm(string message, string title) => false;
        public HelpAction ShowHelp() => HelpAction.None;
        public void ShowFeedback() { }
        public RawFileEntry? ChooseRawCandidate(IReadOnlyList<RawFileEntry> candidates) => null;
        public bool ShowMediaDetails(MediaSelectionItem item, bool showAdvancedDetails) => false;
        public void RevealFile(string path) { }
    }

    private sealed class TestDirectory : IDisposable
    {
        public TestDirectory()
        {
            Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "PixelTart.Selection.ProxyTests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path);
        }

        public string Path { get; }
        public string Combine(params string[] parts) => parts.Aggregate(Path, System.IO.Path.Combine);

        public string Write(string name, byte[] bytes)
        {
            var path = Combine(name);
            Directory.CreateDirectory(System.IO.Path.GetDirectoryName(path)!);
            File.WriteAllBytes(path, bytes);
            return path;
        }

        public void Dispose()
        {
            try { Directory.Delete(Path, recursive: true); } catch { }
        }
    }
}
