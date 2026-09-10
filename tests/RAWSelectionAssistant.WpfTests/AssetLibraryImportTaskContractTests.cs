using System.IO;

namespace RAWSelectionAssistant.WpfTests;

[TestClass]
public sealed class AssetLibraryImportTaskContractTests
{
    [TestMethod]
    public void OrdinaryImportUsesTaskBridgeFrozenLibraryIdentityAndShutdownDrain()
    {
        var source = Read("src/PixelTart.Modules.AssetLibrary/AssetLibraryViewModel.cs");
        foreach (var token in new[]
                 {
                     "RunTrackedImportAsync", "_taskOperationBridge.RunAsync", "TargetLibraryId = _targetLibraryId",
                     "ResolveLibraryId(_databasePath)", "CreateLinkedTokenSource(engineToken, _lifetimeCancellation.Token)",
                     "await DrainP3OperationsAsync()", "await _repository.DisposeAsync()", "AssetImportStage.ReadingMetadata",
                     "AssetImportStage.Materializing", "正在完成…", "BuildImportTaskExecutionResult(result, summary)",
                     "MediaTaskFailurePayload.Serialize(detail)"
                 })
            StringAssert.Contains(source, token);

        Assert.IsLessThan(source.IndexOf("await _repository.DisposeAsync()", StringComparison.Ordinal),
            source.IndexOf("await DrainP3OperationsAsync()", StringComparison.Ordinal));
    }

    [TestMethod]
    public void PickerThumbnailAnalysisAndUnsupportedUxShareCapabilityRegistry()
    {
        var viewModel = Read("src/PixelTart.Modules.AssetLibrary/AssetLibraryViewModel.cs");
        var thumbnail = Read("src/PixelTart.Modules.AssetLibrary/AsyncThumbnail.cs");
        var repository = Read("src/RAWSelectionAssistant.Core/Services/AssetLibrary/SqliteAssetLibraryRepository.cs");
        StringAssert.Contains(viewModel, "AssetFormatCapabilityRegistry.Default.BuildPickerFilter()");
        StringAssert.Contains(viewModel, "CanAnalyze(AssetItem asset)");
        StringAssert.Contains(thumbnail, "if (!capability.CanThumbnail)");
        StringAssert.Contains(repository, "_formatCapabilities.GetOrUnknown(sourcePath)");
        StringAssert.Contains(viewModel, "不支持 {result.UnsupportedCount");
    }

    [TestMethod]
    public void ProductionImportsCannotBypassAnUnattachedTaskCenterBridge()
    {
        var source = Read("src/PixelTart.Modules.AssetLibrary/AssetLibraryViewModel.cs");
        StringAssert.Contains(source, "source == \"synthetic-directory-recursive\" && !_taskOperationBridge.IsAttached");
        StringAssert.Contains(source, "Production picker/session imports always require Task Center");
        Assert.IsFalse(source.Contains("source == \"file-picker\" && !_taskOperationBridge.IsAttached", StringComparison.Ordinal));
        Assert.IsFalse(source.Contains("source == \"session\" && !_taskOperationBridge.IsAttached", StringComparison.Ordinal));
    }

    private static string Read(string relative)
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
            if (File.Exists(Path.Combine(directory.FullName, "RAWSelectionAssistant.sln")))
                return File.ReadAllText(Path.Combine(directory.FullName, relative.Replace('/', Path.DirectorySeparatorChar)));
        throw new DirectoryNotFoundException();
    }
}
