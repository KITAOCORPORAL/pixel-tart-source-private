using System.IO;
using System.Xml.Linq;

namespace RAWSelectionAssistant.WpfTests;

[TestClass]
public sealed class AssetLibraryP1StateSeamContractTests
{
    [TestMethod]
    public void DedicatedBuildPropertyIsDevPreviewOnlyAndRejectedForAcceptanceAndRelease()
    {
        var project = XDocument.Load(Path("src/RAWSelectionAssistant/RAWSelectionAssistant.csproj"));
        var define = project.Descendants("DefineConstants")
            .Single(element => element.Value.Contains("ASSET_LIBRARY_P1_STATE_ACCEPTANCE", StringComparison.Ordinal));
        var defineCondition = (string?)define.Attribute("Condition") ?? string.Empty;
        ContainsAll(
            defineCondition,
            "'$(ModularHarnessDevPreview)' == 'true'",
            "'$(AssetLibraryP1StateAcceptance)' == 'true'",
            "'$(AcceptanceBuild)' != 'true'",
            "'$(Configuration)' != 'Release'");

        var validation = project.Descendants("Target")
            .Single(element => string.Equals((string?)element.Attribute("Name"), "ValidateAssetLibraryP1StateAcceptanceBuild", StringComparison.Ordinal));
        Assert.AreEqual("PrepareForBuild", (string?)validation.Attribute("BeforeTargets"));
        Assert.AreEqual("'$(AssetLibraryP1StateAcceptance)' == 'true'", (string?)validation.Attribute("Condition"));
        var errors = validation.Elements("Error").ToArray();
        Assert.HasCount(3, errors);
        Assert.IsTrue(errors.Any(error => ((string?)error.Attribute("Condition"))?.Contains("'$(ModularHarnessDevPreview)' != 'true'", StringComparison.Ordinal) == true));
        Assert.IsTrue(errors.Any(error => ((string?)error.Attribute("Condition"))?.Contains("'$(AcceptanceBuild)' == 'true'", StringComparison.Ordinal) == true));
        Assert.IsTrue(errors.Any(error => ((string?)error.Attribute("Condition"))?.Contains("'$(Configuration)' == 'Release'", StringComparison.Ordinal) == true));
    }

    [TestMethod]
    public void RuntimeControllerHasExactOptInProcessAndIsolatedRootAllowlist()
    {
        var controller = Text("src/RAWSelectionAssistant/Services/AssetLibraryP1AcceptanceStateController.cs");
        Assert.IsTrue(controller.TrimStart().StartsWith("#if ASSET_LIBRARY_P1_STATE_ACCEPTANCE", StringComparison.Ordinal));
        ContainsAll(
            controller,
            "PIXEL_TART_ASSET_LIBRARY_P1_STATE_ACCEPTANCE",
            "first-empty/v1",
            "loading-error-retry-empty/v1",
            "PixelTart_ModularHarness_V1_DevPreview",
            "StringComparison.Ordinal",
            "PIXEL_TART_ACCEPTANCE_ROOT",
            "Path.IsPathFullyQualified",
            "release-loading.gate",
            "requires a fresh isolated evidence directory",
            "BeforeRepositoryInitializationAsync",
            "ExecuteInitialQueryAsync",
            "await realQuery(cancellationToken)",
            "recoverable-query-error-injected",
            "real-repository-query-entered",
            "real-repository-query-completed",
            "asset-library-v16.db",
            "_databasePath + \"-wal\"",
            "_databasePath + \"-shm\"",
            "databasePath = _databasePath",
            "freshDatabaseVerified = true",
            "repositorySource = _repositorySource",
            "repositoryImplementation = _repositoryImplementation",
            "repositorySchemaVersion = _repositorySchemaVersion",
            "repositoryAssetCount = _repositoryAssetCount",
            "exception.Data[AssetLibraryLoadStateExceptionMetadata.InjectionIdDataKey]",
            "throw exception");
        Assert.DoesNotContain("Visibility", controller, StringComparison.Ordinal);
        Assert.DoesNotContain("File.Delete", controller, StringComparison.Ordinal);
        Assert.DoesNotContain("repositorySchemaVersion = 6", controller, StringComparison.Ordinal);
        Assert.DoesNotContain("repositoryAssetCount = 0", controller, StringComparison.Ordinal);
        Assert.DoesNotContain("repositorySource = \"SqliteAssetLibraryRepository\"", controller, StringComparison.Ordinal);

        var app = Text("src/RAWSelectionAssistant/App.xaml.cs");
        var gateStart = app.IndexOf("#if ASSET_LIBRARY_P1_STATE_ACCEPTANCE", StringComparison.Ordinal);
        var factoryCall = app.IndexOf("AssetLibraryP1AcceptanceStateController.TryCreate", StringComparison.Ordinal);
        var gateEnd = app.IndexOf("#endif", gateStart, StringComparison.Ordinal);
        Assert.IsGreaterThanOrEqualTo(0, gateStart);
        Assert.IsGreaterThan(gateStart, factoryCall);
        Assert.IsGreaterThan(factoryCall, gateEnd);
    }

    [TestMethod]
    public void SeamPreservesRealRepositoryStateBindingsAndStableAutomationIds()
    {
        var viewModel = Text("src/PixelTart.Modules.AssetLibrary/AssetLibraryViewModel.cs");
        ContainsAll(
            viewModel,
            "new SqliteAssetLibraryRepository(_database)",
            "BeforeRepositoryInitializationAsync",
            "ExecuteInitialQueryAsync",
            "ct => _repository.QueryAsync(query, ct)",
            "_repository.GetType().Name",
            "_repository is not SqliteAssetLibraryRepository",
            "_repositorySource = \"real-repository\"",
            "SELECT MAX(Version) FROM AssetLibrarySchemaInfo;",
            "SELECT COUNT(*) FROM AssetItems;",
            "AssetLibraryRefreshOutcome",
            "CancellationTokenSource.CreateLinkedTokenSource(_lifetimeCancellation.Token, cancellationToken)",
            "await _initializationGate.WaitAsync(lifetimeToken)",
            "await _initializationGate.WaitAsync();",
            "if (refreshOutcome != AssetLibraryRefreshOutcome.Completed)",
            "private Task RetryLoadAsync() => IsReady ? RefreshAsync() : InitializeAsync();",
            "public int LoadAttempt",
            "DisablePreviewFixtures",
            "if (_enablePreviewFeatures && Folders.Count == 0) await SeedPreviewStructureAsync();");
        Assert.DoesNotContain("Visibility =", viewModel, StringComparison.Ordinal);

        var page = Text("src/PixelTart.Modules.AssetLibrary/AssetLibraryPage.cs");
        ContainsAll(
            page,
            "loadStateController?.DisablePreviewFixtures != true",
            "_demoDirectory = _enablePreviewFeatures ? demoDirectory : null",
            "loadStateController");

        var xaml = Text("src/PixelTart.Modules.AssetLibrary/AssetLibraryPage.xaml");
        ContainsAll(
            xaml,
            "AutomationProperties.AutomationId=\"AssetLibraryLoadingState\"",
            "AutomationProperties.AutomationId=\"AssetLibraryLoadingProgress\"",
            "AutomationProperties.AutomationId=\"AssetLibraryErrorState\"",
            "AutomationProperties.AutomationId=\"AssetLibraryLoadErrorMessage\"",
            "AutomationProperties.AutomationId=\"RetryAssetLibraryLoad\"",
            "AutomationProperties.AutomationId=\"AssetLibraryEmptyState\"",
            "AutomationProperties.AutomationId=\"AssetLibraryEmptyStateTitle\"",
            "AutomationProperties.AutomationId=\"ImportFromEmptyAssetLibrary\"");
    }

    private static void ContainsAll(string text, params string[] expected)
    {
        foreach (var value in expected) StringAssert.Contains(text, value);
    }

    private static string Text(string relativePath) => File.ReadAllText(Path(relativePath));

    private static string Path(string relativePath) => System.IO.Path.Combine(FindRepositoryRoot(), relativePath.Replace('/', System.IO.Path.DirectorySeparatorChar));

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !Directory.Exists(System.IO.Path.Combine(directory.FullName, "src"))) directory = directory.Parent;
        return directory?.FullName ?? throw new DirectoryNotFoundException("Repository root not found.");
    }
}
