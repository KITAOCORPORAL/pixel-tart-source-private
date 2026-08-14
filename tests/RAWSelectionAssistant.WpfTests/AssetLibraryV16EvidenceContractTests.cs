using System.Security.Cryptography;
using System.Text;
using System.IO;

namespace RAWSelectionAssistant.WpfTests;

[TestClass]
public sealed class AssetLibraryV16EvidenceContractTests
{
    private static readonly string[] RequiredEvidenceFiles =
    [
        "07_visual_analysis_color.png",
        "08_visual_analysis_histogram.png",
        "09_visual_analysis_tone.png",
        "10_visual_filter.png",
        "11_visual_smart_folder.png",
        "12_color_similarity_results.png",
        "13_visual_similarity_results.png",
        "14_batch_visual_analysis.png"
    ];

    [TestMethod]
    public void PreviewExposesStableAutomationContractForEveryRequestedEvidenceScene()
    {
        var xaml = Read("src/PixelTart.AssetLibrary.Preview/MainWindow.xaml");
        foreach (var automationId in new[]
        {
            "AssetLibraryV16Window",
            "AssetLibraryImport",
            "AssetGrid",
            "VisualAnalysisTabs",
            "VisualFilterChips",
            "AdvancedVisualFilter",
            "SearchByColor",
            "FindSimilarAssets",
            "AnalyzeVisibleAssets",
            "CancelVisualBatch",
            "VisualBatchStatus",
            "VisualFeatureStatus",
            "VisualMatchScores",
            "ClearVisualResults"
        })
            StringAssert.Contains(xaml, $"AutomationProperties.AutomationId=\"{automationId}\"");

        foreach (var visibleLabel in new[]
        {
            "配色", "直方图", "影调", "视觉筛选", "查找颜色", "查找相似", "Smart Folder", "批量视觉分析"
        })
            StringAssert.Contains(xaml, visibleLabel);
    }

    [TestMethod]
    public void EvidenceManifestMapsOnlyRequestedScenesToExactUiReviewNames()
    {
        var manifest = Read("tools/AssetLibraryV16Acceptance/evidence-scenes.json");
        using var document = System.Text.Json.JsonDocument.Parse(manifest);
        Assert.AreEqual("pixel-tart-asset-library-v16-evidence/v1", document.RootElement.GetProperty("schema").GetString());
        Assert.IsTrue(document.RootElement.GetProperty("synthetic_only").GetBoolean());
        Assert.IsFalse(document.RootElement.GetProperty("customer_media_allowed").GetBoolean());
        CollectionAssert.AreEqual(
            RequiredEvidenceFiles,
            document.RootElement.GetProperty("scenes").EnumerateArray().Select(scene => scene.GetProperty("file_name").GetString()).ToArray());
        foreach (var scene in document.RootElement.GetProperty("scenes").EnumerateArray())
        {
            var target = scene.GetProperty("automation_target").GetString();
            Assert.IsFalse(string.IsNullOrWhiteSpace(target));
            StringAssert.Contains(Read("src/PixelTart.AssetLibrary.Preview/MainWindow.xaml"), $"AutomationProperties.AutomationId=\"{target}\"");
            Assert.IsFalse(string.IsNullOrWhiteSpace(scene.GetProperty("visible_assertion").GetString()));
        }
    }

    [TestMethod]
    public void ExistingEvidenceIsUniquePngAndContainsNoTextualPathOrCustomerMarkers()
    {
        var evidenceRoot = Path.Combine(Root(), "ui-review", "asset-library");
        var hashes = new HashSet<string>(StringComparer.Ordinal);
        foreach (var fileName in RequiredEvidenceFiles)
        {
            var path = Path.Combine(evidenceRoot, fileName);
            if (!File.Exists(path)) continue;
            var bytes = File.ReadAllBytes(path);
            CollectionAssert.AreEqual(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }, bytes[..8]);
            Assert.IsTrue(hashes.Add(Convert.ToHexString(SHA256.HashData(bytes))), $"Duplicate evidence image: {fileName}");
            var latin = Encoding.Latin1.GetString(bytes);
            foreach (var forbidden in new[] { "C:\\Users\\", "D:\\AI AGENT", "GPS", "DSC0", "customer", "token", "LocalAppData" })
                Assert.DoesNotContain(forbidden, latin, StringComparison.OrdinalIgnoreCase, $"Sensitive marker in {fileName}");
            Assert.IsFalse(ContainsTextChunk(bytes), $"PNG textual metadata is not allowed: {fileName}");
        }
    }

    private static bool ContainsTextChunk(ReadOnlySpan<byte> png)
    {
        var offset = 8;
        while (offset + 12 <= png.Length)
        {
            var length = System.Buffers.Binary.BinaryPrimitives.ReadInt32BigEndian(png.Slice(offset, 4));
            if (length < 0 || offset + 12 + length > png.Length) return true;
            var type = Encoding.ASCII.GetString(png.Slice(offset + 4, 4));
            if (type is "tEXt" or "zTXt" or "iTXt" or "eXIf") return true;
            offset += 12 + length;
            if (type == "IEND") return false;
        }
        return true;
    }

    private static string Read(string relativePath) => File.ReadAllText(Path.Combine(Root(), relativePath.Replace('/', Path.DirectorySeparatorChar)));

    private static string Root()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
            if (File.Exists(Path.Combine(directory.FullName, "RAWSelectionAssistant.sln"))) return directory.FullName;
        throw new DirectoryNotFoundException("Repository root not found.");
    }
}
