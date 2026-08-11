using System.IO;
using RAWSelectionAssistant.Core.Models;
using RAWSelectionAssistant.Core.Services;

namespace RAWSelectionAssistant.WpfTests;

[TestClass]
public sealed class ProductQuickToolsRuntimeTests
{
    [TestMethod]
    public async Task ProductLayout_RemainsFourWhenLegacyCompatibilityFieldIsNormalizedDuringSave()
    {
        var directory = Path.Combine(Path.GetTempPath(), "PixelTart.ProductQuickTools.Runtime", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            var settings = new AppSettings
            {
                PinnedQuickTools = ProductToolboxPolicy.DefaultPinnedTools.ToList(),
                ProductQuickToolLayout = new ProductQuickToolLayout
                {
                    OrderedToolIds = ProductToolboxPolicy.DefaultPinnedTools.ToList()
                }
            };
            var service = new SettingsService(new TestLogService(), Path.Combine(directory, "settings.json"));

            await service.SaveAsync(settings);

            Assert.HasCount(QuickToolsService.MaximumPinnedTools, settings.PinnedQuickTools);
            CollectionAssert.AreEqual(
                ProductToolboxPolicy.DefaultPinnedTools.ToArray(),
                ProductToolboxPolicy.Normalize(settings.ProductQuickToolLayout.OrderedToolIds).ToArray());
        }
        finally
        {
            try { Directory.Delete(directory, true); } catch { }
        }
    }

    [TestMethod]
    public void WorkbenchRuntime_UsesProductLayoutForEveryPinReadAndPublishesRuntimeIds()
    {
        var viewModel = Read("src/RAWSelectionAssistant/ViewModels/MainViewModel.cs");
        StringAssert.Contains(viewModel, "PinnedToolboxItems => CurrentProductQuickTools()");
        StringAssert.Contains(viewModel, "IsToolPinned(string id) => TryGetProductTool(id, out var definition)");
        StringAssert.Contains(viewModel, "var productQuickTools = CurrentProductQuickTools();");
        StringAssert.Contains(viewModel, "ProductToolboxPolicy.Move(current, id, offset)");
        StringAssert.Contains(viewModel, "ManageQuickTools(CurrentProductQuickTools())");

        var acceptance = Read("src/RAWSelectionAssistant/MainWindow.AutomatedDpiAcceptance.cs");
        StringAssert.Contains(acceptance, "pinnedToolboxItemIds");
        StringAssert.Contains(acceptance, "displayedPinnedToolboxItemIds");
        StringAssert.Contains(acceptance, "workbenchQuickToolsPassed");
    }

    private static string Read(string relativePath) =>
        File.ReadAllText(Path.Combine(RepositoryRoot(), relativePath.Replace('/', Path.DirectorySeparatorChar)));

    private static string RepositoryRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
            if (File.Exists(Path.Combine(directory.FullName, "RAWSelectionAssistant.sln"))) return directory.FullName;
        throw new DirectoryNotFoundException();
    }

    private sealed class TestLogService : ILogService
    {
        public void Info(string message) { }
        public void Error(string message, Exception? exception = null) { }
    }
}
