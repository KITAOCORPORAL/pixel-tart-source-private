using System.IO;

namespace RAWSelectionAssistant.WpfTests;

[TestClass]
public sealed class AssetLibraryPreviewWpfTests
{
    [TestMethod]
    public void PreviewDefinesThreeColumnLibraryGridAndInspector()
    {
        var xaml = Read("src/PixelTart.AssetLibrary.Preview/MainWindow.xaml");
        foreach (var token in new[] { "AssetFolderTree", "SmartFolders", "AssetTags", "AssetGrid", "检查器", "素材网格", "添加文件夹", "应用标签" })
            StringAssert.Contains(xaml, token);
        StringAssert.Contains(xaml, "ColumnDefinition Width=\"262\"");
        StringAssert.Contains(xaml, "ColumnDefinition Width=\"350\"");
    }

    [TestMethod]
    public void PreviewUsesBoundedPagingAndVirtualizedItemContainers()
    {
        var xaml = Read("src/PixelTart.AssetLibrary.Preview/MainWindow.xaml");
        var viewModel = Read("src/PixelTart.AssetLibrary.Preview/AssetLibraryPreviewViewModel.cs");
        StringAssert.Contains(xaml, "VirtualizingPanel.IsVirtualizing=\"True\"");
        StringAssert.Contains(xaml, "VirtualizationMode=\"Recycling\"");
        StringAssert.Contains(xaml, "local:VirtualizingWrapPanel");
        StringAssert.Contains(xaml, "local:AsyncThumbnail.SourcePath");
        StringAssert.Contains(viewModel, "PageSize: 120");
        StringAssert.Contains(viewModel, "LoadMoreCommand");
    }

    [TestMethod]
    public void PreviewStatesReferenceSafetyAndDoesNotUseEagleBranding()
    {
        var all = Read("src/PixelTart.AssetLibrary.Preview/MainWindow.xaml") + Read("src/PixelTart.AssetLibrary.Preview/AssetLibraryPreviewViewModel.cs");
        StringAssert.Contains(all, "默认不移动、不改名、不删除源文件");
        StringAssert.Contains(all, "未修改源文件");
        Assert.DoesNotContain("Eagle", all, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Delete(", all, StringComparison.Ordinal);
    }

    [TestMethod]
    public void PreviewIsIsolatedFromFormalDatabaseAndP0Shell()
    {
        var host = Read("src/PixelTart.AssetLibrary.Preview/MainWindow.xaml.cs") + Read("src/PixelTart.AssetLibrary.Preview/AssetLibraryPreviewViewModel.cs");
        StringAssert.Contains(host, "KitaoPhotoSelector.AssetLibraryV16Preview");
        StringAssert.Contains(host, "PIXEL_TART_ASSET_LIBRARY_ACCEPTANCE_ROOT");
        Assert.DoesNotContain("pixel-tart.db", host, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("RAWSelectionAssistant.MainWindow", host, StringComparison.Ordinal);
        Assert.DoesNotContain("InputRouting", host, StringComparison.Ordinal);
    }

    [TestMethod]
    public void PreviewTextIsUtf8WithoutBomAndUsesReadableChinese()
    {
        var path = Path.Combine(Root(), "src", "PixelTart.AssetLibrary.Preview", "MainWindow.xaml");
        var bytes = File.ReadAllBytes(path);
        Assert.IsGreaterThanOrEqualTo(3, bytes.Length);
        Assert.IsFalse(bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF);
        var text = new System.Text.UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true).GetString(bytes);
        Assert.DoesNotContain("鍍", text, StringComparison.Ordinal);
        StringAssert.Contains(text, "素材库 V1.6");
        StringAssert.Contains(text, "F 分类");
    }

    [TestMethod]
    public void PreviewLocksFClassifierAndShiftDRepeatContracts()
    {
        var xaml = Read("src/PixelTart.AssetLibrary.Preview/MainWindow.xaml");
        var host = Read("src/PixelTart.AssetLibrary.Preview/MainWindow.xaml.cs");
        var viewModel = Read("src/PixelTart.AssetLibrary.Preview/AssetLibraryPreviewViewModel.cs");
        StringAssert.Contains(xaml, "F 分类");
        StringAssert.Contains(xaml, "Shift+D 重复分类");
        StringAssert.Contains(host, "Key.F");
        StringAssert.Contains(host, "OpenClassifier");
        StringAssert.Contains(host, "ModifierKeys.Shift");
        StringAssert.Contains(viewModel, "RepeatLastFolderMembershipAsync");
        StringAssert.Contains(viewModel, "_lastFolderId");
    }

    [TestMethod]
    public void PreviewThumbnailCacheIsBoundedAndUsesDecodeWidth()
    {
        var thumbnail = Read("src/PixelTart.AssetLibrary.Preview/AsyncThumbnail.cs");
        StringAssert.Contains(thumbnail, "MaxCacheBytes");
        StringAssert.Contains(thumbnail, "CancellationTokenSource");
        StringAssert.Contains(thumbnail, "DecodePixelWidth");
        StringAssert.Contains(thumbnail, "Unloaded");
    }

    [TestMethod]
    public void PreviewConnectsVisualAnalysisTabsAndThumbnailSize()
    {
        var xaml = Read("src/PixelTart.AssetLibrary.Preview/MainWindow.xaml");
        foreach (var token in new[] { "VisualAnalysisTabs", "Header=\"配色\"", "Header=\"直方图\"", "Header=\"影调\"", "ThumbnailWidth", "HistogramDrawing" })
            StringAssert.Contains(xaml, token);
    }

    [TestMethod]
    public void PreviewDecoderFingerprintsDecodedProxyPixels()
    {
        var decoder = Read("src/PixelTart.AssetLibrary.Preview/WpfVisualAnalysisDecoder.cs");
        StringAssert.Contains(decoder, "VisualAnalysisFingerprint.Compute(pixels)");
        Assert.DoesNotContain("asset.ContentHash ??", decoder, StringComparison.Ordinal);
    }

    [TestMethod]
    public void PreviewConnectsV16VisualSearchBatchAndTemporaryResultActions()
    {
        var xaml = Read("src/PixelTart.AssetLibrary.Preview/MainWindow.xaml");
        var viewModel = Read("src/PixelTart.AssetLibrary.Preview/AssetLibraryPreviewViewModel.cs");
        foreach (var token in new[] { "AssetLibraryV16Window", "VisualFilterChips", "AdvancedVisualFilter", "SearchByColor", "FindSimilarAssets", "AnalyzeVisibleAssets", "CancelVisualBatch", "VisualFeatureStatus", "ClearVisualResults" })
            StringAssert.Contains(xaml, token);
        foreach (var token in new[] { "_queryGeneration", "VisualResultMode", "FindSimilarAsync", "SearchColorAsync", "ApplyAdvancedVisualFilterAsync", "AnalyzeVisibleAsync", "AssetVisualAnalysisBatchProcessor" })
            StringAssert.Contains(viewModel, token);
    }

    private static string Read(string relative) => File.ReadAllText(Path.Combine(Root(), relative.Replace('/', Path.DirectorySeparatorChar)));
    private static string Root()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
            if (File.Exists(Path.Combine(directory.FullName, "RAWSelectionAssistant.sln"))) return directory.FullName;
        throw new DirectoryNotFoundException("Repository root not found.");
    }
}
