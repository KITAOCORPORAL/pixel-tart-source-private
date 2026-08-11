using RAWSelectionAssistant.Core.Models;
using RAWSelectionAssistant.Core.Services;
using System.Text.Json;

namespace RAWSelectionAssistant.Tests;

[TestClass]
public sealed class ProductToolboxPolicyTests
{
    [TestMethod]
    public void ProductionCatalog_HasFocusedFourToolsInRequiredOrder()
    {
        CollectionAssert.AreEqual(new[]
        {
            ToolId.PhotoOrganize, ToolId.RawToJpeg, ToolId.BatchCompress, ToolId.Collage
        }, ProductToolboxPolicy.ProductionCatalog.Select(item => item.Id).ToArray());
        CollectionAssert.AreEqual(new[]
        {
            "整理图片", "RAW 转 JPG", "批量压缩", "拼图"
        }, ProductToolboxPolicy.ProductionCatalog.Select(item => item.DisplayName).ToArray());
    }

    [TestMethod]
    public void Defaults_HaveFourPersistentPinnableTools()
    {
        CollectionAssert.AreEqual(new[]
        {
            "PhotoOrganize", "RawToJpeg", "BatchCompress", "Collage"
        }, ProductToolboxPolicy.DefaultPinnedTools.ToArray());
        CollectionAssert.AreEqual(ProductToolboxPolicy.DefaultPinnedTools.ToArray(),
            ProductToolboxPolicy.Normalize(ProductToolboxPolicy.DefaultPinnedTools).ToArray());
    }

    [TestMethod]
    public void PreviewWatermark_RemainsVisibleButNeverDefaultPinned()
    {
        var watermark = ProductToolboxPolicy.Get(ToolId.Watermark);
        Assert.AreEqual(FeatureAvailability.Preview, watermark.Availability);
        Assert.IsTrue(ProductToolboxPolicy.Catalog.Contains(watermark));
        Assert.DoesNotContain(watermark.SettingsId, ProductToolboxPolicy.DefaultPinnedTools);
        CollectionAssert.AreEqual(new[] { "Collage" },
            ProductToolboxPolicy.Normalize(["Watermark", "unknown", "Collage", "Collage"]));
    }

    [TestMethod]
    public void LegacyQuickToolsContract_RemainsUnchanged()
    {
        CollectionAssert.AreEqual(new[] { "Workflow", "PhotoOrganize", "BatchCompress" },
            QuickToolsService.DefaultPinnedTools.ToArray());
    }

    [TestMethod]
    public async Task SettingsService_PreservesProductFourToolLayoutAcrossRestart()
    {
        var directory = Path.Combine(Path.GetTempPath(), "PixelTart.ProductQuickTools", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            var path = Path.Combine(directory, "settings.json");
            var service = new SettingsService(new TestLogService(), path);
            var settings = new AppSettings();
            settings.ProductQuickToolLayout.OrderedToolIds = ProductToolboxPolicy.DefaultPinnedTools.ToList();
            await service.SaveAsync(settings);

            var loaded = await new SettingsService(new TestLogService(), path).LoadAsync();
            CollectionAssert.AreEqual(ProductToolboxPolicy.DefaultPinnedTools.ToArray(),
                loaded.ProductQuickToolLayout.OrderedToolIds.ToArray());
        }
        finally
        {
            try { if (Directory.Exists(directory)) Directory.Delete(directory, true); }
            catch { }
        }
    }

    [TestMethod]
    public async Task SettingsService_RepeatedSaveKeepsRawToJpegInSerializedProductLayout()
    {
        var directory = Path.Combine(Path.GetTempPath(), "PixelTart.ProductQuickTools", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            var path = Path.Combine(directory, "settings.json");
            var service = new SettingsService(new TestLogService(), path);
            var settings = new AppSettings();

            await service.SaveAsync(settings);
            settings.Appearance.Density = InterfaceDensity.Compact;
            await service.SaveAsync(settings);

            using var document = JsonDocument.Parse(await File.ReadAllTextAsync(path));
            var serialized = document.RootElement
                .GetProperty(nameof(AppSettings.ProductQuickToolLayout))
                .GetProperty(nameof(ProductQuickToolLayout.OrderedToolIds))
                .EnumerateArray()
                .Select(item => item.GetString()!)
                .ToArray();
            CollectionAssert.AreEqual(ProductToolboxPolicy.DefaultPinnedTools.ToArray(), serialized);
            Assert.Contains("RawToJpeg", serialized);
        }
        finally
        {
            try { if (Directory.Exists(directory)) Directory.Delete(directory, true); }
            catch { }
        }
    }

    [TestMethod]
    public async Task SettingsService_ProductPinOrderSurvivesRestartAndNeverExceedsFour()
    {
        var directory = Path.Combine(Path.GetTempPath(), "PixelTart.ProductQuickTools", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            var expected = new[] { "Collage", "RawToJpeg", "PhotoOrganize", "BatchCompress" };
            var path = Path.Combine(directory, "settings.json");
            var service = new SettingsService(new TestLogService(), path);
            var settings = new AppSettings
            {
                ProductQuickToolLayout = new ProductQuickToolLayout
                {
                    OrderedToolIds = [.. expected, "Watermark", "Collage"]
                }
            };

            await service.SaveAsync(settings);
            var restarted = await new SettingsService(new TestLogService(), path).LoadAsync();

            CollectionAssert.AreEqual(expected, restarted.ProductQuickToolLayout.OrderedToolIds.ToArray());
            Assert.HasCount(ProductToolboxPolicy.MaximumPinnedTools,
                restarted.ProductQuickToolLayout.OrderedToolIds);
            Assert.Contains("RawToJpeg", restarted.ProductQuickToolLayout.OrderedToolIds);
        }
        finally
        {
            try { if (Directory.Exists(directory)) Directory.Delete(directory, true); }
            catch { }
        }
    }
}
