using System.IO;
using System.Threading;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using RAWSelectionAssistant.Core.Models;
using RAWSelectionAssistant.Core.Services.Database;
using RAWSelectionAssistant.Core.Services.Tethering;
using RAWSelectionAssistant.Services;
using RAWSelectionAssistant.ViewModels;

namespace RAWSelectionAssistant.WpfTests;

[TestClass]
public sealed class Version230StageCLiveMonitorWpfTests
{
    [TestMethod]
    [DataRow("Grid.ColumnDefinitions")]
    [DataRow("ThumbnailColumn")]
    [DataRow("MinWidth=\"640\"")]
    [DataRow("InspectorColumn")]
    [DataRow("InspectorDrawer")]
    [DataRow("VirtualizingPanel.IsVirtualizing=\"True\"")]
    [DataRow("VirtualizationMode=\"Recycling\"")]
    [DataRow("ScrollUnit=\"Pixel\"")]
    [DataRow("ThumbnailItem_Loaded")]
    [DataRow("ThumbnailItem_Unloaded")]
    public void Workspace_XamlContainsThreeColumnAndVirtualizationContract(string token) => StringAssert.Contains(TetherXaml(), token);

    [TestMethod]
    [DataRow("Content=\"Fit\"")]
    [DataRow("Content=\"Fill\"")]
    [DataRow("Content=\"100%\"")]
    [DataRow("MouseWheel=\"PreviewViewport_MouseWheel\"")]
    [DataRow("PreviewImage_MouseLeftButtonDown")]
    [DataRow("Content=\"上一张  ←\"")]
    [DataRow("Content=\"下一张  →\"")]
    [DataRow("AutomationProperties.Name=\"全屏监看\"")]
    public void Workspace_XamlContainsPreviewNavigationContract(string token) => StringAssert.Contains(TetherXaml(), token);

    [TestMethod]
    public void Workspace_ReadOnlyPreviewProgressBindingIsOneWay()
    {
        var xaml = TetherXaml();
        StringAssert.Contains(xaml, "Value=\"{Binding PreviewProgress, Mode=OneWay}\"");
        Assert.DoesNotContain("Value=\"{Binding PreviewProgress}\"", xaml, StringComparison.Ordinal);
        StringAssert.Contains(xaml, "x:Name=\"MonitorToolbar\"");
        StringAssert.Contains(xaml, "<ColumnDefinition Width=\"*\" /><ColumnDefinition Width=\"Auto\" />");
    }

    [TestMethod]
    [DataRow("RGB直方图")]
    [DataRow("基于监看代理图；不是RAW显影结果")]
    [DataRow("高光警告")]
    [DataRow("阴影警告")]
    [DataRow("并排比较")]
    [DataRow("重叠比较")]
    [DataRow("闪烁对比")]
    [DataRow("参考图叠加")]
    [DataRow("三分法")]
    [DataRow("9:16")]
    public void Workspace_XamlContainsAnalysisCompareReferenceAndGuideContract(string token) => StringAssert.Contains(TetherXaml() + ViewModelSource(), token);

    [TestMethod]
    [DataRow("摄影师备注")]
    [DataRow("客户备注")]
    [DataRow("客户收藏")]
    [DataRow("快速拒绝 / 取消")]
    [DataRow("不进入回收站")]
    [DataRow("不调用UndoJournal")]
    [DataRow("SetRatingCommand")]
    [DataRow("SetColorLabelCommand")]
    public void Workspace_XamlContainsLocalAnnotationContract(string token) => StringAssert.Contains(TetherXaml() + ViewModelSource(), token);

    [TestMethod]
    public Task PreviewLoader_LoadsProxyAndReleasesProxyFile() => RunSta(async () =>
    {
        using var temp = new TempDirectory(); var source = CreateImage(temp.Combine("source.png"), 96, 64); var cache = new TetherProxyCacheService(temp.Combine("cache")); var asset = Asset(source);
        var loader = new PreviewImageLoader(cache, new AssetRepositoryStub(asset)); var result = await loader.LoadAsync(asset);
        Assert.IsNotNull(result.Image); Assert.IsFalse(result.IsPlaceholder);
        var proxy = cache.ResolvePath(await cache.GetOrCreateAsync(asset))!; using var exclusive = new FileStream(proxy, FileMode.Open, FileAccess.ReadWrite, FileShare.None); Assert.IsTrue(exclusive.CanRead);
    });

    [TestMethod]
    public Task FullResolutionLoader_UsesOnLoadAndReleasesSourceFile() => RunSta(async () =>
    {
        using var temp = new TempDirectory(); var source = CreateImage(temp.Combine("source.png"), 192, 128); var asset = Asset(source); var memory = new PreviewMemoryManager();
        var loader = new FullResolutionImageLoader(new AssetRepositoryStub(asset), memory); var result = await loader.LoadAsync(asset);
        Assert.IsNotNull(result.Image); Assert.AreEqual(192, result.Image.PixelWidth); using var exclusive = new FileStream(source, FileMode.Open, FileAccess.ReadWrite, FileShare.None); Assert.IsTrue(exclusive.CanWrite);
    });

    [TestMethod]
    public Task RawPreview_UsesPairedJpgWithoutDecodingRaw() => RunSta(async () =>
    {
        using var temp = new TempDirectory(); var jpgPath = CreateImage(temp.Combine("paired.jpg"), 80, 60); var rawPath = temp.CreateFile("paired.nef", [10, 20, 30]);
        var jpg = Asset(jpgPath); var raw = Asset(rawPath, TetherMediaKind.Raw) with { PairedAssetId = jpg.Id }; var repository = new AssetRepositoryStub(jpg, raw); var cache = new TetherProxyCacheService(temp.Combine("cache"));
        var result = await new PreviewImageLoader(cache, repository).LoadAsync(raw);
        Assert.IsNotNull(result.Image); Assert.IsTrue(result.UsedPairedPreview); Assert.IsTrue(File.Exists(rawPath));
    });

    [TestMethod]
    public Task RawPreview_WithoutPairReturnsPlaceholder() => RunSta(async () =>
    {
        using var temp = new TempDirectory(); var raw = Asset(temp.CreateFile("alone.nef", [1, 2, 3]), TetherMediaKind.Raw);
        var result = await new PreviewImageLoader(new ProxyCacheStub(), new AssetRepositoryStub(raw)).LoadAsync(raw);
        Assert.IsNull(result.Image); Assert.IsTrue(result.IsPlaceholder); Assert.AreEqual("RawPreviewUnavailable", result.ErrorCode);
    });

    [TestMethod]
    public void PreviewRequestCoordinator_CancelsOldRequestAndRejectsOldWriteback()
    {
        using var coordinator = new PreviewRequestCoordinator(); var firstId = Guid.NewGuid(); var secondId = Guid.NewGuid();
        var first = coordinator.Begin(firstId); var second = coordinator.Begin(secondId);
        Assert.IsTrue(first.Token.IsCancellationRequested); Assert.IsFalse(coordinator.IsCurrent(firstId, first.Version)); Assert.IsTrue(coordinator.IsCurrent(secondId, second.Version));
    }

    [TestMethod]
    public void PreviewRequestCoordinator_FiftyFastSwitchesLeaveOnlyNewestCurrent()
    {
        using var coordinator = new PreviewRequestCoordinator(); PreviewRequest latest = default!;
        for (var index = 0; index < 50; index++) latest = coordinator.Begin(Guid.NewGuid());
        Assert.IsTrue(coordinator.IsCurrent(latest.AssetId, latest.Version));
    }

    [TestMethod]
    public Task Histogram_ComputesRgbAndLuminanceOffUiThreadCompatibleBitmap() => RunSta(async () =>
    {
        var bitmap = SolidBitmap(4, 3, 10, 20, 240); var result = await new HistogramService().CalculateAsync(bitmap, true);
        Assert.AreEqual(12, result.Red[240]); Assert.AreEqual(12, result.Green[20]); Assert.AreEqual(12, result.Blue[10]); Assert.AreEqual(12, result.Luminance.Sum()); Assert.IsTrue(result.BasedOnProxy);
    });

    [TestMethod]
    public Task Histogram_HonorsCancellation() => RunSta(async () =>
    {
        using var cancellation = new CancellationTokenSource(); cancellation.Cancel();
        await Assert.ThrowsExactlyAsync<TaskCanceledException>(() => new HistogramService().CalculateAsync(SolidBitmap(20, 20, 1, 2, 3), true, cancellation.Token));
    });

    [TestMethod]
    public Task ClippingOverlay_MarksHighlightsAndShadowsWithoutChangingSource() => RunSta(async () =>
    {
        var pixels = new byte[] { 255, 255, 255, 255, 0, 0, 0, 255 }; var source = BitmapSource.Create(2, 1, 96, 96, PixelFormats.Bgra32, null, pixels, 8); source.Freeze();
        var overlay = await new ClippingOverlayService().CreateAsync(source, true, 250, true, 5);
        Assert.IsNotNull(overlay); var output = new byte[8]; overlay.CopyPixels(output, 8, 0); Assert.IsGreaterThan(0, output[3]); Assert.IsGreaterThan(0, output[7]);
        var original = new byte[8]; source.CopyPixels(original, 8, 0); CollectionAssert.AreEqual(pixels, original);
    });

    [TestMethod]
    public void FullResolutionMemoryManager_UsesBoundedLruAndReleasesOldImages()
    {
        var memory = new PreviewMemoryManager(2, 64L * 1024 * 1024); var ids = new[] { Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid() };
        memory.Add(ids[0], SolidBitmap(10, 10, 1, 2, 3)); memory.Add(ids[1], SolidBitmap(10, 10, 2, 3, 4)); memory.Add(ids[2], SolidBitmap(10, 10, 3, 4, 5));
        Assert.AreEqual(2, memory.CachedImageCount); Assert.IsFalse(memory.TryGet(ids[0], out _)); Assert.IsTrue(memory.TryGet(ids[2], out _));
        memory.ReleaseExcept(ids[2]); Assert.AreEqual(1, memory.CachedImageCount);
    }

    [TestMethod]
    public Task ExifService_MissingMetadataShowsNotProvidedAndHidesNothingByFabrication() => RunSta(async () =>
    {
        using var temp = new TempDirectory(); var path = CreateImage(temp.Combine("plain.png"), 32, 24); var info = await new TetherExifService().ReadAsync(Asset(path));
        Assert.AreEqual("未提供", info.CameraMake); Assert.AreEqual("未提供", info.Lens); Assert.AreEqual("32 × 24", info.PixelDimensions);
    });

    [TestMethod]
    public async Task ExifService_CorruptMetadataDoesNotThrow()
    {
        using var temp = new TempDirectory(); var path = temp.CreateFile("corrupt.jpg", [1, 2, 3, 4]); var info = await new TetherExifService().ReadAsync(Asset(path));
        Assert.AreEqual("未提供", info.CameraModel); Assert.IsFalse(info.MetadataAvailable);
    }

    [TestMethod]
    public async Task DisplaySettingsStore_RoundTripsReferenceAndGuideWithoutDatabase()
    {
        using var temp = new TempDirectory(); var store = new JsonTetherDisplaySettingsStore(temp.Combine("settings")); var sessionId = Guid.NewGuid();
        await store.SaveAsync(new(sessionId, false, TetherGuideMode.Ratio4x5, TetherCanvasTone.Black, 248, 7, temp.Combine("reference.png"), .6, 1.4, 12, -9, true, true));
        var loaded = await store.LoadAsync(sessionId); Assert.IsFalse(loaded.AutoLatest); Assert.AreEqual(TetherGuideMode.Ratio4x5, loaded.GuideMode); Assert.AreEqual(.6, loaded.ReferenceOpacity); Assert.IsTrue(loaded.ReferenceLocked);
    }

    [TestMethod]
    public Task ViewModel_OneThousandAssetsAreIncrementalAndKeepItemInstances() => RunSta(async () =>
    {
        var preview = new PreviewLoaderStub(); await using var viewModel = CreateViewModel(preview); var assets = Enumerable.Range(0, 1000).Select(index => Asset($"SYNTHETIC_{index:0000}.jpg", id: Guid.NewGuid(), time: DateTimeOffset.UtcNow.AddMilliseconds(index))).ToArray();
        viewModel.ApplyReviewState("TetherAssets", assets); Assert.HasCount(1000, viewModel.Assets); var first = viewModel.Assets[0];
        viewModel.ApplyReviewState("TetherAssets", assets.Select(asset => asset with { UpdatedAtUtc = asset.UpdatedAtUtc.AddSeconds(1) }).ToArray());
        Assert.AreSame(first, viewModel.Assets[0]); Assert.HasCount(1000, viewModel.Assets);
    });

    [TestMethod]
    public Task ViewModel_HundredIncomingAssetsRemainAvailableForMonitoring() => RunSta(async () =>
    {
        await using var viewModel = CreateViewModel(new PreviewLoaderStub()); var assets = Enumerable.Range(0, 100).Select(index => Asset($"BURST_{index:000}.jpg", id: Guid.NewGuid(), time: DateTimeOffset.UtcNow.AddMilliseconds(index))).ToArray();
        viewModel.ApplyReviewState("TetherAssets", assets); Assert.AreEqual(100, viewModel.DiscoveredCount); Assert.AreEqual(100, viewModel.ReadyCount); Assert.IsNotNull(viewModel.SelectedAsset);
    });

    [TestMethod]
    public void FullscreenImplementation_HidesShellWithoutStoppingWatchSession()
    {
        var main = Text("src/RAWSelectionAssistant/MainWindow.xaml.cs"); var view = Text("src/RAWSelectionAssistant/Views/TetherCaptureView.xaml.cs");
        foreach (var token in new[] { "WindowState.Maximized", "TopMenu.Visibility = Visibility.Collapsed", "SidebarContainer.Visibility = Visibility.Collapsed", "BottomStatusBar.Visibility = Visibility.Collapsed", "Key.F11", "Key.Escape" }) StringAssert.Contains(main + view, token);
        var method = Slice(main, "private void TetherCaptureView_FullScreenChanged", "private void"); Assert.DoesNotContain("StopAsync", method, StringComparison.Ordinal);
    }

    [TestMethod]
    public void MonitoringImplementation_PreservesWatchFolderAndFileSafetyBoundaries()
    {
        var source = ViewModelSource() + Text("src/RAWSelectionAssistant/Services/TetherMonitoringImageServices.cs");
        Assert.DoesNotContain("Thread.Sleep", source, StringComparison.Ordinal); Assert.DoesNotContain("localhost", source, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("File.Delete", ViewModelSource(), StringComparison.Ordinal); Assert.DoesNotContain("Recycle", ViewModelSource(), StringComparison.OrdinalIgnoreCase);
        StringAssert.Contains(Text("src/RAWSelectionAssistant.Core/Services/Tethering/WatchFolderCameraAdapter.cs"), "SearchOption.TopDirectoryOnly");
    }

    [TestMethod]
    public void OrdinaryPagesTaskCompletionAndProviderSafetyRemainWired()
    {
        var app = Text("src/RAWSelectionAssistant/App.xaml.cs"); var main = Text("src/RAWSelectionAssistant/ViewModels/MainViewModel.cs"); var bridge = Text("src/RAWSelectionAssistant.Core/Services/Tasks/TaskOperationBridge.cs") + Text("src/RAWSelectionAssistant.Core/Services/Tethering/TetherSafeCopyService.cs");
        StringAssert.Contains(main, "TetherCaptureViewModel? tetherPage = null"); StringAssert.Contains(app, "allowMockProvider: false"); StringAssert.Contains(Text("src/RAWSelectionAssistant/appsettings.license.json"), "\"Provider\": \"None\"");
        StringAssert.Contains(bridge, "WaitForCompletionAsync"); StringAssert.Contains(bridge, "DrainAsync"); Assert.DoesNotContain("FakeCamera", app, StringComparison.OrdinalIgnoreCase);
    }

    [TestMethod]
    public void MonitoringLogsAndDiagnosticsExcludePrivateContent()
    {
        var core = Text("src/RAWSelectionAssistant.Core/Services/Tethering/TetherMonitoringServices.cs"); var log = Text("src/RAWSelectionAssistant.Core/Services/LogMaintenanceService.cs");
        Assert.DoesNotContain("PhotographerNote}", core, StringComparison.Ordinal); Assert.DoesNotContain("ClientNote}", core, StringComparison.Ordinal);
        Assert.DoesNotContain("TetherProxies", log, StringComparison.OrdinalIgnoreCase); Assert.DoesNotContain("TetherFullResolution", log, StringComparison.OrdinalIgnoreCase);
    }

    private static TetherCaptureViewModel CreateViewModel(IPreviewImageLoader preview)
    {
        var assets = new AssetRepositoryStub(); var memory = new PreviewMemoryManager();
        return new(null!, new SessionRepositoryStub(), assets, new ProxyCacheStub(), new DialogStub(), new AnnotationServiceStub(), preview,
            new FullResolutionImageLoader(assets, memory), new HistogramService(), new ClippingOverlayService(), new PreviewRequestCoordinator(), new ExifServiceStub(), new DisplayStoreStub(), memory);
    }

    private static BitmapSource SolidBitmap(int width, int height, byte blue, byte green, byte red)
    {
        var pixels = new byte[width * height * 4]; for (var index = 0; index < pixels.Length; index += 4) { pixels[index] = blue; pixels[index + 1] = green; pixels[index + 2] = red; pixels[index + 3] = 255; }
        var bitmap = BitmapSource.Create(width, height, 96, 96, PixelFormats.Bgra32, null, pixels, width * 4); bitmap.Freeze(); return bitmap;
    }

    private static string CreateImage(string path, int width, int height)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!); var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(SolidBitmap(width, height, 40, 100, 180))); using var stream = File.Create(path); encoder.Save(stream); return path;
    }

    private static TetherAssetRecord Asset(string path, TetherMediaKind kind = TetherMediaKind.PreviewImage, Guid? id = null, DateTimeOffset? time = null)
    {
        var now = time ?? DateTimeOffset.UtcNow; var full = Path.IsPathFullyQualified(path) ? path : Path.Combine(Path.GetTempPath(), "PixelTart.Synthetic", path);
        return new(id ?? Guid.NewGuid(), Guid.Parse("23000000-0000-0000-0000-000000000003"), null, full, full.ToUpperInvariant(), Path.GetFileName(full), Path.GetExtension(full), kind,
            File.Exists(full) ? new FileInfo(full).Length : 1024, now, now, TetherStabilityState.Stable, TetherProcessingState.Ready, kind == TetherMediaKind.Raw ? TetherPreviewState.Placeholder : TetherPreviewState.Ready, now, now);
    }

    private static string Slice(string text, string start, string next)
    {
        var index = text.IndexOf(start, StringComparison.Ordinal); if (index < 0) return string.Empty; var end = text.IndexOf(next, index + start.Length, StringComparison.Ordinal); return end < 0 ? text[index..] : text[index..end];
    }
    private static string TetherXaml() => Text("src/RAWSelectionAssistant/Views/TetherCaptureView.xaml");
    private static string ViewModelSource() => Text("src/RAWSelectionAssistant/ViewModels/TetherCaptureViewModel.cs");
    private static string Text(string relative) { for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent) if (File.Exists(Path.Combine(directory.FullName, "RAWSelectionAssistant.sln"))) return File.ReadAllText(Path.Combine(directory.FullName, relative.Replace('/', Path.DirectorySeparatorChar))); throw new DirectoryNotFoundException(); }
    private static Task RunSta(Func<Task> action) { var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously); var thread = new Thread(async () => { try { await action(); completion.SetResult(); } catch (Exception ex) { completion.SetException(ex); } }); thread.SetApartmentState(ApartmentState.STA); thread.Start(); return completion.Task; }

    private sealed class AssetRepositoryStub(params TetherAssetRecord[] initial) : ITetherAssetRepository
    {
        private readonly Dictionary<Guid, TetherAssetRecord> _items = initial.ToDictionary(item => item.Id);
        public Task<TetherAssetRecord> UpsertDiscoveredAsync(TetherAssetRecord asset, CancellationToken cancellationToken = default) { _items[asset.Id] = asset; return Task.FromResult(asset); }
        public Task UpdateAsync(TetherAssetRecord asset, CancellationToken cancellationToken = default) { _items[asset.Id] = asset; return Task.CompletedTask; }
        public Task<TetherAssetRecord?> GetAsync(Guid id, CancellationToken cancellationToken = default) => Task.FromResult(_items.GetValueOrDefault(id));
        public Task<TetherAssetRecord?> GetByPathAsync(Guid sessionId, string normalizedPath, CancellationToken cancellationToken = default) => Task.FromResult(_items.Values.FirstOrDefault(item => item.SessionId == sessionId && item.NormalizedSourcePath == normalizedPath));
        public Task<IReadOnlyList<TetherAssetRecord>> ListBySessionAsync(Guid sessionId, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<TetherAssetRecord>>(_items.Values.Where(item => item.SessionId == sessionId).ToArray());
        public Task<bool> PairAsync(Guid sessionId, Guid leftAssetId, Guid rightAssetId, string pairingKey, CancellationToken cancellationToken = default) => Task.FromResult(true);
    }
    private sealed class SessionRepositoryStub : ITetherSessionRepository
    {
        public Task AddAsync(TetherSessionRecord session, CancellationToken cancellationToken = default) => Task.CompletedTask; public Task UpdateAsync(TetherSessionRecord session, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<TetherSessionRecord?> GetAsync(Guid id, CancellationToken cancellationToken = default) => Task.FromResult<TetherSessionRecord?>(null); public Task<IReadOnlyList<TetherSessionRecord>> ListActiveAsync(CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<TetherSessionRecord>>([]);
    }
    private sealed class ProxyCacheStub : ITetherProxyCache { public Task<string?> GetOrCreateAsync(TetherAssetRecord asset, CancellationToken cancellationToken = default) => Task.FromResult<string?>(null); public string? ResolvePath(string? cacheKey) => null; public Task ClearAsync(CancellationToken cancellationToken = default) => Task.CompletedTask; }
    private sealed class PreviewLoaderStub : IPreviewImageLoader { public Task<PreviewImageLoadResult> LoadAsync(TetherAssetRecord asset, int decodePixelWidth = 2048, CancellationToken cancellationToken = default) => Task.FromResult(new PreviewImageLoadResult(asset.Id, SolidBitmap(24, 16, 20, 80, 160), false, asset.MediaKind == TetherMediaKind.Raw)); }
    private sealed class ExifServiceStub : ITetherExifService { public Task<TetherExifInfo> ReadAsync(TetherAssetRecord asset, CancellationToken cancellationToken = default) => Task.FromResult(TetherExifInfo.Unavailable(asset)); }
    private sealed class AnnotationServiceStub : ITetherAnnotationService { public Task<TetherAnnotationRecord?> GetAsync(Guid assetId, CancellationToken cancellationToken = default) => Task.FromResult<TetherAnnotationRecord?>(null); public Task<IReadOnlyDictionary<Guid, TetherAnnotationRecord>> ListBySessionAsync(Guid sessionId, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyDictionary<Guid, TetherAnnotationRecord>>(new Dictionary<Guid, TetherAnnotationRecord>()); public Task<TetherAnnotationSaveResult> SaveAsync(TetherAnnotationRecord annotation, Guid? projectId = null, CancellationToken cancellationToken = default) => Task.FromResult(new TetherAnnotationSaveResult(true, annotation)); }
    private sealed class DisplayStoreStub : ITetherDisplaySettingsStore { public Task<TetherDisplaySettings> LoadAsync(Guid sessionId, CancellationToken cancellationToken = default) => Task.FromResult(new TetherDisplaySettings(sessionId)); public Task SaveAsync(TetherDisplaySettings settings, CancellationToken cancellationToken = default) => Task.CompletedTask; }
    private sealed class DialogStub : IDialogService
    {
        public string? ChooseFolder(string title, string? initialDirectory = null) => null; public IReadOnlyList<string> ChooseFiles(string title, string filter, bool multiselect = true) => []; public string? ChooseSaveFile(string title, string filter, string defaultExtension, string? suggestedFileName = null) => null; public IReadOnlyList<string>? ManageQuickTools(IReadOnlyList<string> currentToolIds) => null;
        public void ShowInfo(string message) { } public void ShowError(string message) { } public bool Confirm(string message, string title) => false; public HelpAction ShowHelp() => HelpAction.None; public void ShowFeedback() { } public RawFileEntry? ChooseRawCandidate(IReadOnlyList<RawFileEntry> candidates) => null; public bool ShowMediaDetails(MediaSelectionItem item, bool showAdvancedDetails) => false; public void RevealFile(string path) { }
    }
    private sealed class TempDirectory : IDisposable
    {
        public TempDirectory() { Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "PixelTart.StageCLiveMonitorWpf", Guid.NewGuid().ToString("N")); Directory.CreateDirectory(Path); }
        public string Path { get; } public string Combine(params string[] parts) => System.IO.Path.Combine([Path, .. parts]); public string CreateFile(string relative, byte[] bytes) { var path = Combine(relative); Directory.CreateDirectory(System.IO.Path.GetDirectoryName(path)!); File.WriteAllBytes(path, bytes); return path; } public void Dispose() { try { Directory.Delete(Path, true); } catch { } }
    }
}
