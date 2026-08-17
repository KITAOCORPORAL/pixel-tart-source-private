using System.Buffers.Binary;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Xml.Linq;
using Microsoft.Data.Sqlite;

namespace RAWSelectionAssistant.WpfTests;

[TestClass]
public sealed class ModularHarnessEmbeddedEvidenceContractTests
{
    private static readonly string[] RequiredEvidenceFiles =
    [
        "01_workbench.png",
        "02_toolbox_asset_library.png",
        "03_embedded_asset_library.png",
        "04_visual_analysis_palette.png",
        "05_visual_analysis_histogram.png",
        "06_visual_analysis_tone.png",
        "07_visual_filter.png",
        "08_visual_similarity.png",
        "09_return_workbench.png",
        "10_module_diagnostics.png"
    ];

    [TestMethod]
    public void ShellContainsOneEmbeddedAssetHostAndOnlyTheTwoIntentionalToolboxAssetEntries()
    {
        var document = XDocument.Load(PathAt("src/RAWSelectionAssistant/MainWindow.xaml"));

        var assetHosts = document.Descendants()
            .Where(element => element.Name.LocalName == "ModuleWorkspaceHost")
            .Where(element => Attribute(element, "Route") == "asset-library")
            .ToArray();
        Assert.HasCount(1, assetHosts);

        var assetNavigation = document.Descendants()
            .Where(element => element.Name.LocalName == "Button")
            .Where(element => Attribute(element, "AutomationProperties.AutomationId") is "ToolboxAssetLibraryEntry" or "ToolboxPageAssetLibraryEntry")
            .ToArray();
        Assert.HasCount(2, assetNavigation);
        Assert.IsTrue(assetNavigation.All(element => Attribute(element, "AutomationProperties.Name") == "素材库"));
        Assert.IsTrue(assetNavigation.All(element => Attribute(element, "CommandParameter") == "AssetLibrary"));
        Assert.IsFalse(document.Descendants().Any(element => Attribute(element, "AutomationProperties.AutomationId") == "AssetLibraryNavigationButton"));

        Assert.IsFalse(document.Descendants().Any(element => Attribute(element, "Name") == "LegacyAssetLibraryWorkspace"));
    }

    [TestMethod]
    public void ProductCompositionUsesModuleRegistryWithoutStandaloneAssetPreviewLaunch()
    {
        var app = Read("src/RAWSelectionAssistant/App.xaml.cs");
        var window = Read("src/RAWSelectionAssistant/MainWindow.xaml.cs");
        var host = Read("src/RAWSelectionAssistant/Views/ModuleWorkspaceHost.cs");
        var module = Read("src/PixelTart.Modules.AssetLibrary/AssetLibraryModule.cs");
        var page = Read("src/PixelTart.Modules.AssetLibrary/AssetLibraryPage.cs");
        var relevantSource = string.Join(Environment.NewLine, app, window, host, module, page);

        ContainsAll(app,
            "PixelTartModuleRegistry",
            "registry.Register(new AssetLibraryModule(() =>",
            "return new PixelTart.Modules.AssetLibrary.AssetLibraryPage(",
            "Path.Combine(AppDataPaths.DataDirectory, \"asset-library-v16.db\")",
            "BuildModuleDiagnostics(registry)",
            "registry.Register(new RawToolModule())",
            "registry.Register(new OnlineSelectionModule())");
        ContainsAll(window, "AssetLibraryWorkspace.ModuleRegistry = app.ModuleRegistry");
        ContainsAll(host, "descriptor.ViewFactory()", "Content =");
        Assert.DoesNotContain("PixelTart_AssetLibrary_V1_6_Preview", relevantSource, StringComparison.Ordinal);
        Assert.DoesNotContain("Process.Start(", module + page + host, StringComparison.Ordinal);
    }

    [TestMethod]
    public void DevPreviewStartsAtWorkbenchAndRemovesPhotoOrganizeFromUserFacingCatalog()
    {
        var source = Read("src/RAWSelectionAssistant/ViewModels/MainViewModel.cs");
        ContainsAll(source,
            "private static IReadOnlyList<ToolDefinition> ProductCatalogForCurrentBuild()",
            "MODULAR_HARNESS_DEV_PREVIEW",
            "public async Task InitializeAsync()",
            "Settings.OnboardingLegacyUser = true;",
            "Settings.OnboardingUpgradeOfferShown = true;",
            "return ProductToolboxPolicy.Catalog.Where(definition => definition.Id != ToolId.PhotoOrganize).ToArray();",
            "normalized.RemoveAll(id => string.Equals(id, ToolId.PhotoOrganize.ToString(), StringComparison.OrdinalIgnoreCase));");
    }

    [TestMethod]
    public void DevPreviewHasExactExecutableTitleAndExplicitAcceptanceRootContract()
    {
        var project = Read("src/RAWSelectionAssistant/RAWSelectionAssistant.csproj");
        var window = Read("src/RAWSelectionAssistant/MainWindow.xaml.cs");
        var paths = Read("src/RAWSelectionAssistant.Core/Utilities/AppDataPaths.cs");

        ContainsAll(project,
            "'$(ModularHarnessDevPreview)' == 'true'",
            "PixelTart_ModularHarness_V1_DevPreview",
            "MODULAR_HARNESS_DEV_PREVIEW");
        ContainsAll(window,
            "#if MODULAR_HARNESS_DEV_PREVIEW",
            "Title = \"像素蛋挞 [Modular Harness Dev]\"");
        ContainsAll(paths,
            "PixelTart_ModularHarness_V1_DevPreview",
            "PIXEL_TART_ACCEPTANCE_ROOT",
            "IsModularHarnessDevPreview");
    }

    [TestMethod]
    public void EvidenceManifestIsSyntheticOnlyAndMapsTheExactTenRequestedScenes()
    {
        using var document = JsonDocument.Parse(Read("tools/ModularHarnessV1Acceptance/evidence-contract.json"));
        var root = document.RootElement;

        Assert.AreEqual("pixel-tart-modular-harness-v1-evidence/v1", root.GetProperty("schema").GetString());
        Assert.IsTrue(root.GetProperty("synthetic_only").GetBoolean());
        Assert.IsFalse(root.GetProperty("customer_media_allowed").GetBoolean());
        Assert.IsFalse(root.GetProperty("standalone_asset_preview_allowed").GetBoolean());
        Assert.AreEqual("PixelTart_ModularHarness_V1_DevPreview.exe", root.GetProperty("expected_executable").GetString());
        Assert.AreEqual("像素蛋挞 [Modular Harness Dev]", root.GetProperty("expected_window_title").GetString());
        Assert.AreEqual(1, root.GetProperty("expected_gui_process_count").GetInt32());
        Assert.IsTrue(root.GetProperty("capture_status").GetString() is "not_captured" or "captured");

        var scenes = root.GetProperty("scenes").EnumerateArray().ToArray();
        CollectionAssert.AreEqual(
            RequiredEvidenceFiles,
            scenes.Select(scene => scene.GetProperty("file_name").GetString()).ToArray());
        Assert.IsTrue(scenes.All(scene => scene.GetProperty("required_automation_targets").GetArrayLength() > 0));
        Assert.IsTrue(scenes.All(scene => !string.IsNullOrWhiteSpace(scene.GetProperty("visible_assertion").GetString())));
        Assert.IsTrue(scenes.All(scene => scene.GetProperty("expected_gui_process_count").GetInt32() == 1));

        using var foreground = JsonDocument.Parse(Read("tools/ModularHarnessV1Acceptance/foreground-result.template.json"));
        Assert.AreEqual(string.Empty, foreground.RootElement.GetProperty("executable_sha256").GetString());
        Assert.AreEqual(string.Empty, foreground.RootElement.GetProperty("asset_module_sha256").GetString());
        Assert.AreEqual(string.Empty, foreground.RootElement.GetProperty("synthetic_fixture_manifest_path").GetString());
        Assert.AreEqual(string.Empty, foreground.RootElement.GetProperty("asset_import_diagnostics_path").GetString());
        Assert.AreEqual(0, foreground.RootElement.GetProperty("synthetic_fixture_imported_count").GetInt32());
        Assert.IsFalse(foreground.RootElement.GetProperty("user_verified").GetBoolean());
        foreach (var field in new[]
                 {
                     "visual_smart_folder_verified",
                     "color_similarity_verified",
                     "palette_similarity_verified",
                     "task_center_foreground_triggered",
                     "task_center_database_isolated_root_verified",
                     "task_center_queued_transition_persisted_verified",
                     "task_center_running_transition_persisted_verified",
                     "task_center_completed_transition_persisted_verified",
                     "task_center_queued_foreground_observed",
                     "task_center_running_foreground_observed",
                     "task_center_completed_foreground_observed",
                     "global_task_center_lifecycle_verified",
                     "global_task_center_queued_verified",
                     "global_task_center_running_verified",
                     "global_task_center_completed_verified",
                     "global_task_center_verified"
                 })
            Assert.IsFalse(foreground.RootElement.GetProperty(field).GetBoolean(), $"{field} must default to false.");

        var runner = Read("tools/ModularHarnessV1Acceptance/Invoke-ModularHarnessAcceptance.ps1");
        ContainsAll(runner,
            "function Read-SyntheticFixtureManifest",
            "function Read-AssetImportDiagnostics",
            "PixelTart.Modules.AssetLibrary.dll",
            "asset_module_sha256",
            "$publishIdentityVerified",
            "$foreground.synthetic_fixture_imported_count -eq 12",
            "$syntheticFixture.verified",
            "$assetImportDiagnostics.verified",
            "$visualSmartFolder = $foreground.visual_smart_folder_verified -eq $true",
            "$colorSimilarity = $foreground.color_similarity_verified -eq $true",
            "$paletteSimilarity = $foreground.palette_similarity_verified -eq $true",
            "$taskCenterLifecycle = Read-TaskCenterLifecycle -Foreground $foreground",
            "$taskCenterLifecycle.queued_transition_persisted_verified",
            "$taskCenterLifecycle.running_transition_persisted_verified",
            "$taskCenterLifecycle.completed_transition_persisted_verified",
            "$taskCenterLifecycle.completed_foreground_observed",
            "$taskCenterLifecycle.verified -and",
            "$foreground.global_task_center_verified -eq $true -and",
            "$globalTaskCenterQueued -and",
            "$globalTaskCenterRunning -and",
            "$globalTaskCenterCompleted",
            "visual_smart_folder = [bool]$visualSmartFolder",
            "color_similarity = [bool]$colorSimilarity",
            "palette_similarity = [bool]$paletteSimilarity",
            "global_task_center_queued = [bool]$globalTaskCenterQueued",
            "global_task_center_running = [bool]$globalTaskCenterRunning",
            "global_task_center_completed = [bool]$globalTaskCenterCompleted",
            "visual_smart_folder_verified = [bool]$visualSmartFolder",
            "color_similarity_verified = [bool]$colorSimilarity",
            "palette_similarity_verified = [bool]$paletteSimilarity",
            "global_task_center_queued_verified = [bool]$globalTaskCenterQueued",
            "global_task_center_running_verified = [bool]$globalTaskCenterRunning",
            "global_task_center_completed_verified = [bool]$globalTaskCenterCompleted",
            "global_task_center_verified = [bool]$globalTaskCenter");

        var readme = Read("tools/ModularHarnessV1Acceptance/README.md");
        ContainsAll(readme,
            "`visual_smart_folder_verified`, `color_similarity_verified`, and `palette_similarity_verified` independently",
            "persistent `CreatedAt`, `StartedAt`, and `CompletedAt` values",
            "Queued and Running were persistence-verified rather than visually observed",
            "set them true only after the database-backed lifecycle gate passes");

        var foregroundResultPath = Environment.GetEnvironmentVariable("PIXEL_TART_MODULAR_HARNESS_FOREGROUND_RESULT_PATH");
        if (!string.IsNullOrWhiteSpace(foregroundResultPath))
            AssertTaskCenterLifecycleMatchesDatabase(foregroundResultPath);
    }

    [TestMethod]
    public void EveryEvidenceAutomationTargetExistsInTheCurrentEmbeddedShellMarkup()
    {
        var markup = Read("src/RAWSelectionAssistant/MainWindow.xaml") + Environment.NewLine +
                     Read("src/PixelTart.Modules.AssetLibrary/AssetLibraryPage.xaml");
        var app = Read("src/RAWSelectionAssistant/App.xaml.cs");
        var dynamicDiagnosticIds = new HashSet<string>(StringComparer.Ordinal)
        {
            "AssetLibraryModuleDiagnostic",
            "RawToolModuleDiagnostic",
            "OnlineSelectionModuleDiagnostic"
        };
        StringAssert.Contains(markup, "AutomationProperties.AutomationId=\"{Binding AutomationId}\"");
        ContainsAll(app,
            "BuildModuleDiagnostics(PixelTartModuleRegistry registry)",
            "AssetLibraryModule.ModuleId => \"AssetLibraryModuleDiagnostic\"",
            "RawToolModule.ModuleId => \"RawToolModuleDiagnostic\"",
            "OnlineSelectionModule.ModuleId => \"OnlineSelectionModuleDiagnostic\"");

        using var document = JsonDocument.Parse(Read("tools/ModularHarnessV1Acceptance/evidence-contract.json"));
        foreach (var scene in document.RootElement.GetProperty("scenes").EnumerateArray())
        foreach (var target in scene.GetProperty("required_automation_targets").EnumerateArray())
        {
            var automationId = target.GetString()!;
            if (dynamicDiagnosticIds.Contains(automationId))
                StringAssert.Contains(app, $"\"{automationId}\"");
            else
                StringAssert.Contains(markup, $"AutomationProperties.AutomationId=\"{automationId}\"");
        }
    }

    [TestMethod]
    public void AllRequestedEvidenceExistsAndIsUniqueMetadataFreePngWithNoLocalOrCustomerMarkers()
    {
        var evidenceRoot = Path.Combine(Root(), "ui-review", "modular-harness");
        var actualPngFiles = Directory.Exists(evidenceRoot)
            ? Directory.EnumerateFiles(evidenceRoot, "*.png", SearchOption.TopDirectoryOnly).Select(Path.GetFileName).Order(StringComparer.Ordinal).ToArray()
            : [];
        CollectionAssert.AreEqual(RequiredEvidenceFiles.Order(StringComparer.Ordinal).ToArray(), actualPngFiles);
        var hashes = new HashSet<string>(StringComparer.Ordinal);
        foreach (var fileName in RequiredEvidenceFiles)
        {
            var path = Path.Combine(evidenceRoot, fileName);
            Assert.IsTrue(File.Exists(path), $"Missing required foreground evidence: {fileName}");

            var bytes = File.ReadAllBytes(path);
            Assert.IsGreaterThanOrEqualTo(8, bytes.Length);
            CollectionAssert.AreEqual(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }, bytes[..8]);
            Assert.IsTrue(hashes.Add(Convert.ToHexString(SHA256.HashData(bytes))), $"Duplicate evidence image: {fileName}");
            Assert.IsFalse(ContainsForbiddenMetadataChunk(bytes), $"Textual or EXIF metadata is not allowed: {fileName}");

            var latin = Encoding.Latin1.GetString(bytes);
            foreach (var forbidden in new[] { "C:\\Users\\", "D:\\AI AGENT", "LocalAppData", "GPS", "customer", "token", "DSC0" })
                Assert.DoesNotContain(forbidden, latin, StringComparison.OrdinalIgnoreCase, $"Sensitive marker in {fileName}");
        }

        Assert.HasCount(RequiredEvidenceFiles.Length, hashes);
        using var manifest = JsonDocument.Parse(Read("tools/ModularHarnessV1Acceptance/evidence-contract.json"));
        Assert.AreEqual("captured", manifest.RootElement.GetProperty("capture_status").GetString());
    }

    private static bool ContainsForbiddenMetadataChunk(ReadOnlySpan<byte> png)
    {
        var offset = 8;
        while (offset + 12 <= png.Length)
        {
            var length = BinaryPrimitives.ReadInt32BigEndian(png.Slice(offset, 4));
            if (length < 0 || offset + 12 + length > png.Length) return true;
            var type = Encoding.ASCII.GetString(png.Slice(offset + 4, 4));
            if (type is "tEXt" or "zTXt" or "iTXt" or "eXIf") return true;
            offset += 12 + length;
            if (type == "IEND") return false;
        }
        return true;
    }

    private static void AssertTaskCenterLifecycleMatchesDatabase(string foregroundResultPath)
    {
        Assert.IsTrue(File.Exists(foregroundResultPath), $"Foreground result not found: {foregroundResultPath}");
        using var foreground = JsonDocument.Parse(File.ReadAllText(foregroundResultPath, Encoding.UTF8));
        var root = foreground.RootElement;
        Assert.IsTrue(root.GetProperty("task_center_foreground_triggered").GetBoolean());
        Assert.AreEqual("foreground_action+sqlite_audit", root.GetProperty("task_center_verification_source").GetString());
        Assert.IsTrue(root.GetProperty("task_center_database_isolated_root_verified").GetBoolean());
        Assert.IsTrue(root.GetProperty("task_center_queued_transition_persisted_verified").GetBoolean());
        Assert.IsTrue(root.GetProperty("task_center_running_transition_persisted_verified").GetBoolean());
        Assert.IsTrue(root.GetProperty("task_center_completed_transition_persisted_verified").GetBoolean());
        Assert.IsFalse(root.GetProperty("task_center_queued_foreground_observed").GetBoolean());
        Assert.IsFalse(root.GetProperty("task_center_running_foreground_observed").GetBoolean());
        Assert.IsTrue(root.GetProperty("task_center_completed_foreground_observed").GetBoolean());
        Assert.IsTrue(root.GetProperty("global_task_center_lifecycle_verified").GetBoolean());

        var databasePath = Path.GetFullPath(root.GetProperty("task_center_database_path").GetString()!);
        var acceptanceRoot = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "PixelTart_ModularHarness_V1_Acceptance"));
        Assert.IsTrue(databasePath.StartsWith(acceptanceRoot + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase));
        Assert.AreEqual("pixel-tart.db", Path.GetFileName(databasePath));
        Assert.IsTrue(File.Exists(databasePath));
        Assert.IsTrue(Guid.TryParse(root.GetProperty("task_center_task_id").GetString(), out var taskId));

        var connectionString = new SqliteConnectionStringBuilder { DataSource = databasePath, Mode = SqliteOpenMode.ReadOnly }.ToString();
        using var connection = new SqliteConnection(connectionString);
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT DisplayName, State, Progress, CreatedAt, StartedAt, CompletedAt, InputSnapshot, ResultSummary
            FROM Tasks
            WHERE Id = $id;
            """;
        command.Parameters.AddWithValue("$id", taskId.ToString("D"));
        DateTimeOffset createdAt;
        DateTimeOffset startedAt;
        DateTimeOffset completedAt;
        string persistedSummaryJson;
        using (var reader = command.ExecuteReader())
        {
            Assert.IsTrue(reader.Read(), $"Task {taskId:D} was not found in the isolated Task Center database.");
            Assert.AreEqual("素材库 · 批量视觉分析", reader.GetString(0));
            Assert.AreEqual("Completed", reader.GetString(1));
            Assert.AreEqual(100d, reader.GetDouble(2));
            createdAt = DateTimeOffset.Parse(reader.GetString(3));
            startedAt = DateTimeOffset.Parse(reader.GetString(4));
            completedAt = DateTimeOffset.Parse(reader.GetString(5));
            Assert.IsTrue(createdAt <= startedAt && startedAt <= completedAt);
            Assert.AreEqual("asset-library scope=Current; count=12", reader.GetString(6));

            Assert.AreEqual(root.GetProperty("task_center_task_display_name").GetString(), reader.GetString(0));
            Assert.AreEqual(root.GetProperty("task_center_task_state").GetString(), reader.GetString(1));
            Assert.AreEqual(root.GetProperty("task_center_task_progress").GetDouble(), reader.GetDouble(2));
            Assert.AreEqual(DateTimeOffset.Parse(root.GetProperty("task_center_task_created_at").GetString()!), createdAt);
            Assert.AreEqual(DateTimeOffset.Parse(root.GetProperty("task_center_task_started_at").GetString()!), startedAt);
            Assert.AreEqual(DateTimeOffset.Parse(root.GetProperty("task_center_task_completed_at").GetString()!), completedAt);
            Assert.AreEqual(root.GetProperty("task_center_task_input_snapshot").GetString(), reader.GetString(6));
            persistedSummaryJson = reader.GetString(7);
            Assert.IsFalse(reader.Read(), "Task id must resolve to exactly one Task Center row.");
        }

        using var persistedSummary = JsonDocument.Parse(persistedSummaryJson);
        var expected = root.GetProperty("task_center_task_result_summary");
        var actual = persistedSummary.RootElement;
        Assert.AreEqual(expected.GetProperty("total").GetInt32(), actual.GetProperty("Total").GetInt32());
        Assert.AreEqual(expected.GetProperty("succeeded").GetInt32(), actual.GetProperty("Succeeded").GetInt32());
        Assert.AreEqual(expected.GetProperty("failed").GetInt32(), actual.GetProperty("Failed").GetInt32());
        Assert.AreEqual(expected.GetProperty("skipped").GetInt32(), actual.GetProperty("Skipped").GetInt32());
        Assert.AreEqual(expected.GetProperty("cancelled").GetInt32(), actual.GetProperty("Cancelled").GetInt32());

        using var auditCommand = connection.CreateCommand();
        auditCommand.CommandText = """
            SELECT Timestamp, EventType, SanitizedMessage, CorrelationId
            FROM AuditLogs
            WHERE TaskId = $id AND Category = 'Task'
            ORDER BY Timestamp;
            """;
        auditCommand.Parameters.AddWithValue("$id", taskId.ToString("D"));
        using var auditReader = auditCommand.ExecuteReader();
        var transitions = new List<(DateTimeOffset Timestamp, string EventType, string Message, string CorrelationId)>();
        while (auditReader.Read()) transitions.Add((DateTimeOffset.Parse(auditReader.GetString(0)), auditReader.GetString(1), auditReader.GetString(2), auditReader.GetString(3)));
        var expectedCorrelationId = taskId.ToString("N");
        Assert.IsTrue(transitions.Any(item => item.EventType == "Created" && item.Message == "任务已创建：素材库 · 批量视觉分析"));
        Assert.IsTrue(transitions.All(item => item.CorrelationId == expectedCorrelationId));
        Assert.IsTrue(transitions.Where(item => item.Message.Contains(" -> ", StringComparison.Ordinal)).All(item => item.EventType == "StateTransition"));
        Assert.IsTrue(transitions.Any(item => item.Message == "Pending -> Preparing" && item.Timestamp >= createdAt));
        Assert.IsTrue(transitions.Any(item => item.Message == "Preparing -> Running" && item.Timestamp >= startedAt));
        Assert.IsTrue(transitions.Any(item => item.Message == "Running -> Completed" && item.Timestamp <= completedAt));
        var queued = transitions.Single(item => item.Message == "Pending -> Preparing").Timestamp;
        var running = transitions.Single(item => item.Message == "Preparing -> Running").Timestamp;
        var completed = transitions.Single(item => item.Message == "Running -> Completed").Timestamp;
        Assert.IsTrue(queued <= running && running <= completed);
    }

    private static string? Attribute(XElement element, string localName) =>
        element.Attributes().FirstOrDefault(attribute => attribute.Name.LocalName == localName)?.Value;

    private static void ContainsAll(string source, params string[] values)
    {
        foreach (var value in values) StringAssert.Contains(source, value);
    }

    private static string Read(string relativePath) => File.ReadAllText(PathAt(relativePath), Encoding.UTF8);

    private static string PathAt(string relativePath) =>
        Path.Combine(Root(), relativePath.Replace('/', Path.DirectorySeparatorChar));

    private static string Root()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
            if (File.Exists(Path.Combine(directory.FullName, "RAWSelectionAssistant.sln"))) return directory.FullName;
        throw new DirectoryNotFoundException("Repository root not found.");
    }
}
