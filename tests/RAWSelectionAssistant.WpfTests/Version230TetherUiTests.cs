using System.IO;
using System.Threading;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using RAWSelectionAssistant.Core.Models;
using RAWSelectionAssistant.Services;
using RAWSelectionAssistant.Views;

namespace RAWSelectionAssistant.WpfTests;

[TestClass]
public sealed class Version230TetherUiTests
{
    [TestMethod]
    public void MainNavigation_ExposesIndependentTetherPage()
    {
        var xaml = Text("src/RAWSelectionAssistant/MainWindow.xaml");
        Contains(xaml, "Content=\"联机拍摄\"", "CommandParameter=\"Tether\"", "<views:TetherCaptureView", "DataContext=\"{Binding TetherPage}\"", "IsTetherPage");
    }

    [TestMethod]
    public void TetherPage_ClearlyLabelsWatchFolderNotUsbDirect()
    {
        var xaml = TetherXaml();
        Contains(xaml, "当前为看守文件夹模式，并非相机USB直连", "不提供实时取景、遥控快门、相机参数控制", "ProviderText");
    }

    [TestMethod]
    public void TetherPage_RequiresExplicitStartAndShowsNonRecursiveBoundary()
    {
        var xaml = TetherXaml();
        Contains(xaml, "StartButtonText", "AutomationProperties.Name=\"明确启动或恢复看守\"", "IncludeSubdirectories 固定为 False", "不会递归扫描文件夹");
    }

    [TestMethod]
    public void TetherPage_ExposesImportCountPreviewAndDefaultSafeCopyChoices()
    {
        var xaml = TetherXaml();
        Contains(xaml, "导入启动前已有文件", "预览数量", "复制到项目资料目录", "复制到独立备份目录", "复制固定使用复制、新建文件和自动编号");
    }

    [TestMethod]
    public void TetherPage_ShowsRawPlaceholderPairingAndAttentionStates()
    {
        var combined = TetherXaml() + Text("src/RAWSelectionAssistant/ViewModels/TetherCaptureViewModel.cs");
        Contains(combined, "安全占位预览", "JPG/RAW 已配对", "部分完成", "需要处理", "稳定检测中");
    }

    [TestMethod]
    public void TetherPage_UsesReservedAnnotationsForStageCLocalEditor()
    {
        var combined = TetherXaml() + Text("src/RAWSelectionAssistant/ViewModels/TetherCaptureViewModel.cs");
        Contains(combined, "SaveNotesCommand", "客户收藏", "快速拒绝", "IsRejected", "AutomationProperties.Name=\"联机文件列表\"");
        Assert.IsFalse(combined.Contains("File.Delete", StringComparison.Ordinal));
    }

    [TestMethod]
    public void TetherPage_UsesDynamicThemeResourcesWithoutLiteralWhiteBackgrounds()
    {
        var xaml = TetherXaml();
        StringAssert.Contains(xaml, "DynamicResource");
        Assert.IsFalse(xaml.Contains("#FFFFFF", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(xaml.Contains("#000000", StringComparison.OrdinalIgnoreCase));
    }

    [TestMethod]
    public void ProxyCache_UsesOnLoadBoundedSizeOpaqueKeysAndNoSqlite()
    {
        var source = Text("src/RAWSelectionAssistant/Services/TetherProxyCacheService.cs");
        Contains(source, "LongestEdge = 2048", "BitmapCacheOption.OnLoad", "SHA256.HashData", "MaximumCacheBytes", "FileShare.Read | FileShare.Delete");
        Assert.IsFalse(source.Contains("Sqlite", StringComparison.OrdinalIgnoreCase));
    }

    [TestMethod]
    public void TetherViewModel_DefaultsCopiesAndImportToFalse()
    {
        var source = Text("src/RAWSelectionAssistant/ViewModels/TetherCaptureViewModel.cs");
        Contains(source, "private bool _importExisting;", "private bool _copyToProject;", "private bool _copyToBackup;", "ProviderText => IsRunning ? \"Watch Folder\" : \"None\"", "恢复目录并继续");
    }

    [TestMethod]
    [DataRow("jpg")]
    [DataRow("png")]
    [DataRow("tiff")]
    public Task ProxyCache_GeneratesSupportedPreviewAndReleasesSourceStream(string format) => RunSta(async () =>
    {
        using var temp = new TempDirectory(); var source = CreateImage(temp, "source." + format, format, 64, 48); var cache = new TetherProxyCacheService(temp.Combine("cache"));
        var key = await cache.GetOrCreateAsync(Asset(source)); var proxy = cache.ResolvePath(key);
        Assert.IsNotNull(proxy); Assert.IsTrue(File.Exists(proxy));
        using var exclusive = new FileStream(source, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
        Assert.IsTrue(exclusive.CanRead);
    });

    [TestMethod]
    public Task ProxyCache_CorruptEntryIsRebuilt() => RunSta(async () =>
    {
        using var temp = new TempDirectory(); var source = CreateImage(temp, "source.jpg", "jpg", 64, 48); var cache = new TetherProxyCacheService(temp.Combine("cache")); var asset = Asset(source);
        var key = await cache.GetOrCreateAsync(asset); var proxy = cache.ResolvePath(key)!; await File.WriteAllBytesAsync(proxy, [1, 2, 3, 4]);
        Assert.AreEqual(key, await cache.GetOrCreateAsync(asset));
        using var stream = File.OpenRead(proxy); Assert.IsNotNull(BitmapFrame.Create(stream, BitmapCreateOptions.None, BitmapCacheOption.OnLoad));
    });

    [TestMethod]
    public Task ProxyCache_LruRemovesOldestEntryAtConfiguredBound() => RunSta(async () =>
    {
        using var temp = new TempDirectory(); var root = temp.Combine("cache"); var firstSource = CreateImage(temp, "first.jpg", "jpg", 80, 60); var secondSource = CreateImage(temp, "second.jpg", "jpg", 80, 60, 140);
        var unlimited = new TetherProxyCacheService(root); var firstKey = await unlimited.GetOrCreateAsync(Asset(firstSource)); var firstProxy = unlimited.ResolvePath(firstKey)!; var firstLength = new FileInfo(firstProxy).Length;
        File.SetLastAccessTimeUtc(firstProxy, DateTime.UtcNow.AddDays(-1));
        var bounded = new TetherProxyCacheService(root, firstLength + 100); var secondKey = await bounded.GetOrCreateAsync(Asset(secondSource));
        Assert.IsNull(bounded.ResolvePath(firstKey)); Assert.IsNotNull(bounded.ResolvePath(secondKey));
        Assert.IsTrue(File.Exists(firstSource)); Assert.IsTrue(File.Exists(secondSource));
    });

    [TestMethod]
    public Task ProxyCache_ConstrainsLongestEdgeAndClearKeepsOriginal() => RunSta(async () =>
    {
        using var temp = new TempDirectory(); var source = CreateImage(temp, "wide.png", "png", 2200, 12); var cache = new TetherProxyCacheService(temp.Combine("cache")); var key = await cache.GetOrCreateAsync(Asset(source)); var proxy = cache.ResolvePath(key)!;
        using (var stream = File.OpenRead(proxy)) { var frame = BitmapFrame.Create(stream, BitmapCreateOptions.None, BitmapCacheOption.OnLoad); Assert.IsLessThanOrEqualTo(2048, Math.Max(frame.PixelWidth, frame.PixelHeight)); }
        await cache.ClearAsync(); Assert.IsFalse(File.Exists(proxy)); Assert.IsTrue(File.Exists(source));
    });

    [TestMethod]
    public void TetherControl_IsPartOfSharedLogicalViewportGate()
    {
        var shared = Text("tests/RAWSelectionAssistant.WpfTests/Version220CalendarUiTests.cs");
        Contains(shared, "new TetherCaptureView()", "new Size(1024, 640)", "new Size(854, 534)", "new Size(720, 480)", "Measure(viewport)", "Arrange(new Rect(viewport))");
    }

    private static string TetherXaml() => Text("src/RAWSelectionAssistant/Views/TetherCaptureView.xaml");
    private static TetherAssetRecord Asset(string path)
    {
        var info = new FileInfo(path); var now = DateTimeOffset.UtcNow;
        return new(Guid.NewGuid(), Guid.NewGuid(), null, path, Path.GetFullPath(path).ToUpperInvariant(), info.Name, info.Extension, TetherMediaKind.PreviewImage, info.Length,
            new DateTimeOffset(info.LastWriteTimeUtc, TimeSpan.Zero), now, TetherStabilityState.Stable, TetherProcessingState.Ready, TetherPreviewState.Pending, now, now);
    }
    private static string CreateImage(TempDirectory temp, string fileName, string format, int width, int height, byte seed = 90)
    {
        var stride = width * 4; var pixels = new byte[stride * height]; for (var index = 0; index < pixels.Length; index += 4) { pixels[index] = seed; pixels[index + 1] = (byte)(255 - seed); pixels[index + 2] = 120; pixels[index + 3] = 255; }
        var bitmap = BitmapSource.Create(width, height, 96, 96, PixelFormats.Bgra32, null, pixels, stride); BitmapEncoder encoder = format switch { "png" => new PngBitmapEncoder(), "tiff" => new TiffBitmapEncoder(), _ => new JpegBitmapEncoder { QualityLevel = 90 } };
        encoder.Frames.Add(BitmapFrame.Create(bitmap)); var path = temp.Combine(fileName); Directory.CreateDirectory(Path.GetDirectoryName(path)!); using var stream = File.Create(path); encoder.Save(stream); return path;
    }
    private static void Contains(string text, params string[] values) { foreach (var value in values) StringAssert.Contains(text, value); }
    private static string Text(string relative) => File.ReadAllText(Path.Combine(Root(), relative.Replace('/', Path.DirectorySeparatorChar)));
    private static string Root() { for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent) if (File.Exists(Path.Combine(directory.FullName, "RAWSelectionAssistant.sln"))) return directory.FullName; throw new DirectoryNotFoundException(); }
    private static Task RunSta(Func<Task> action)
    {
        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var thread = new Thread(async () => { try { await action(); completion.SetResult(); } catch (Exception ex) { completion.SetException(ex); } });
        thread.SetApartmentState(ApartmentState.STA); thread.Start(); return completion.Task;
    }
    private sealed class TempDirectory : IDisposable
    {
        public TempDirectory() { Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "PixelTart.WpfTetherTests", Guid.NewGuid().ToString("N")); Directory.CreateDirectory(Path); }
        public string Path { get; } public string Combine(params string[] parts) => System.IO.Path.Combine([Path, .. parts]);
        public void Dispose() { try { Directory.Delete(Path, true); } catch { } }
    }
}
