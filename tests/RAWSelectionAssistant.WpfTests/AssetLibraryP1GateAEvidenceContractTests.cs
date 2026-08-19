using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace RAWSelectionAssistant.WpfTests;

[TestClass]
public sealed class AssetLibraryP1GateAEvidenceContractTests
{
    [TestMethod]
    public void ContractStartsUncapturedAndDeclaresEveryRawGateARequirement()
    {
        var contractText = Read("tools/AssetLibraryP1Acceptance/gate-a-evidence-contract.json");
        using var document = JsonDocument.Parse(contractText);
        var root = document.RootElement;

        Assert.AreEqual("pixel-tart-asset-library-p1-gate-a-evidence-contract/v1", root.GetProperty("schema").GetString());
        Assert.AreEqual("not_captured", root.GetProperty("capture_status").GetString());
        var route = root.GetProperty("acceptance_start_route");
        Assert.AreEqual("PIXEL_TART_ASSET_LIBRARY_P1_START_ROUTE", route.GetProperty("environment_variable").GetString());
        Assert.AreEqual("asset-library", route.GetProperty("route").GetString());
        Assert.AreEqual("AssetLibrary", route.GetProperty("current_page").GetString());
        Assert.AreEqual("3b5ff13bb4c5b4c2001f978cb6ab31f5715cd7af", route.GetProperty("source_head").GetString());
        Assert.IsTrue(root.GetProperty("synthetic_fixture_only").GetBoolean());
        Assert.IsFalse(root.GetProperty("customer_media_allowed").GetBoolean());
        Assert.IsTrue(root.GetProperty("runtime_evidence_may_contain_machine_paths").GetBoolean());
        Assert.IsFalse(root.GetProperty("portable_output").GetProperty("machine_paths_allowed").GetBoolean());
        Assert.AreEqual("PixelTart_ModularHarness_V1_DevPreview.exe", root.GetProperty("expected_executable_name").GetString());
        Assert.AreEqual(1, root.GetProperty("expected_gui_process_count").GetInt32());

        var state = root.GetProperty("state_chain");
        Assert.AreEqual("pixel-tart-asset-library-p1-state/v1", state.GetProperty("scenario_protocol").GetString());
        CollectionAssert.AreEqual(
            new[] { "first-empty", "loading", "recoverable-error", "retry-recovered" },
            state.GetProperty("scenes").EnumerateArray().Select(scene => scene.GetProperty("id").GetString()).ToArray());
        CollectionAssert.AreEqual(
            new[] { "08-first-empty-", "09-loading-", "10-recoverable-error-", "11-retry-recovered-" },
            state.GetProperty("scenes").EnumerateArray().Select(scene => scene.GetProperty("capture_name_prefix").GetString()).ToArray());
        CollectionAssert.AreEqual(
            new[] { "first-empty-session", "retry-session" },
            state.GetProperty("sessions").EnumerateArray().Select(session => session.GetProperty("id").GetString()).ToArray());
        CollectionAssert.AreEqual(
            new[] { "first-empty/v1", "loading-error-retry-empty/v1" },
            state.GetProperty("sessions").EnumerateArray().Select(session => session.GetProperty("scenario").GetString()).ToArray());
        CollectionAssert.AreEqual(
            new[] { "first-empty" },
            state.GetProperty("sessions")[0].GetProperty("scene_ids").EnumerateArray().Select(value => value.GetString()).ToArray());
        CollectionAssert.AreEqual(
            new[] { "loading", "recoverable-error", "retry-recovered" },
            state.GetProperty("sessions")[1].GetProperty("scene_ids").EnumerateArray().Select(value => value.GetString()).ToArray());
        Assert.AreEqual("controller-events.jsonl", state.GetProperty("controller_event_file").GetString());
        CollectionAssert.AreEqual(
            new[] { "first-empty", "retry-recovered" },
            state.GetProperty("duplicate_hash_allowed_scene_sets")[0].EnumerateArray().Select(value => value.GetString()).ToArray());
        Assert.AreEqual("real-repository", state.GetProperty("repository_source").GetString());
        Assert.AreEqual("SqliteAssetLibraryRepository", state.GetProperty("repository_implementation").GetString());
        Assert.AreEqual(6, state.GetProperty("repository_schema_version").GetInt32());
        Assert.AreEqual(0, state.GetProperty("repository_asset_count").GetInt32());
        Assert.AreEqual("System.IO.IOException", state.GetProperty("recoverable_exception_type").GetString());
        Assert.AreEqual("asset-library-p1-initial-query-io-once/v1", state.GetProperty("recoverable_injection_id").GetString());
        var retryTimeline = state.GetProperty("sessions")[1].GetProperty("required_timeline").EnumerateArray().ToArray();
        Assert.IsTrue(retryTimeline.Any(entry =>
            entry.GetProperty("stream").GetString() == "controller" &&
            entry.GetProperty("stage").GetString() == "loading-barrier-waiting" &&
            entry.GetProperty("attempt").GetInt32() == 1));
        Assert.IsTrue(retryTimeline.Any(entry =>
            entry.GetProperty("stream").GetString() == "controller" &&
            entry.GetProperty("stage").GetString() == "recoverable-query-error-injected" &&
            entry.GetProperty("attempt").GetInt32() == 1));
        Assert.IsTrue(retryTimeline.Any(entry =>
            entry.GetProperty("stream").GetString() == "controller" &&
            entry.GetProperty("stage").GetString() == "real-repository-query-entered" &&
            entry.GetProperty("attempt").GetInt32() == 2));
        Assert.IsTrue(retryTimeline.Any(entry =>
            entry.GetProperty("stream").GetString() == "controller" &&
            entry.GetProperty("stage").GetString() == "real-repository-query-completed" &&
            entry.GetProperty("attempt").GetInt32() == 2));
        Assert.IsTrue(retryTimeline.Any(entry =>
            entry.GetProperty("stream").GetString() == "snapshot" &&
            entry.GetProperty("stage").GetString() == "ready" &&
            entry.GetProperty("attempt").GetInt32() == 2));

        var retry = root.GetProperty("retry_physical_activation");
        Assert.AreEqual("RetryAssetLibraryLoad", retry.GetProperty("automation_id").GetString());
        CollectionAssert.AreEqual(new[] { "mouse", "keyboard" }, retry.GetProperty("allowed_modes").EnumerateArray().Select(item => item.GetString()).ToArray());
        CollectionAssert.AreEqual(new[] { "Enter", "Space" }, retry.GetProperty("keyboard").GetProperty("allowed_keys").EnumerateArray().Select(item => item.GetString()).ToArray());
        var splitter = root.GetProperty("splitter_keyboard");
        CollectionAssert.AreEqual(
            new[] { "PreviewKeyDown", "KeyDown", "PreviewKeyUp", "KeyUp" },
            splitter.GetProperty("required_layer2_events").EnumerateArray().Select(value => value.GetString()).ToArray());
        CollectionAssert.AreEqual(
            new[] { "AssetOrganizationSplitter", "AssetInspectorSplitter" },
            splitter.GetProperty("controls").EnumerateArray().Select(control => control.GetProperty("automation_id").GetString()).ToArray());
        Assert.IsTrue(splitter.GetProperty("controls").EnumerateArray().All(control =>
            control.TryGetProperty("minimum", out _) &&
            control.TryGetProperty("maximum", out _) &&
            control.TryGetProperty("minimum_boundary_key", out _) &&
            control.TryGetProperty("maximum_boundary_key", out _)));

        var matrix = root.GetProperty("dpi_matrix").EnumerateArray()
            .Select(tuple => (
                tuple.GetProperty("width").GetInt32(),
                tuple.GetProperty("height").GetInt32(),
                tuple.GetProperty("scale_percent").GetInt32(),
                tuple.GetProperty("dpi").GetInt32()))
            .ToArray();
        CollectionAssert.AreEqual(
            new[] { (1366, 768, 100, 96), (1920, 1080, 125, 120), (1920, 1080, 150, 144), (2560, 1440, 175, 168) },
            matrix);
        CollectionAssert.AreEqual(
            new[] { "default", "interaction" },
            root.GetProperty("dpi_capture_kinds").EnumerateArray().Select(value => value.GetString()).ToArray());
        Assert.IsTrue(root.GetProperty("dpi_interaction_requires_physical_action").GetBoolean());
        Assert.IsTrue(root.GetProperty("dpi_tuple_order_is_contract_order").GetBoolean());

        var restore = root.GetProperty("restore_baseline");
        Assert.AreEqual(3840, restore.GetProperty("width").GetInt32());
        Assert.AreEqual(2160, restore.GetProperty("height").GetInt32());
        Assert.AreEqual(60, restore.GetProperty("refresh_rate_hz").GetInt32());
        Assert.AreEqual(150, restore.GetProperty("scale_percent").GetInt32());
        Assert.AreEqual(144, restore.GetProperty("dpi").GetInt32());

        Assert.IsFalse(Regex.IsMatch(contractText, "(?i)(?:[a-z]:[\\\\/]|\\\\\\\\[^\\\\])"),
            "The portable contract must not contain a machine-specific absolute path.");
    }

    [TestMethod]
    public void ValidatorReadsRawJsonAndHashesButContainsNoEvidenceGenerationOrMutation()
    {
        var script = Read("tools/AssetLibraryP1Acceptance/Test-AssetLibraryP1GateAEvidence.ps1");

        ContainsAll(script,
            "[string]$RunRoot",
            "gate-a-evidence-contract.json",
            "Get-Content -LiteralPath $Path -Raw -Encoding UTF8 | ConvertFrom-Json",
            "$contract.state_chain.snapshot_file",
            "$contract.state_chain.controller_event_file",
            "$line | ConvertFrom-Json",
            "Get-FileHash -LiteralPath $screenshotPath -Algorithm SHA256",
            "[IO.File]::ReadAllBytes($screenshotPath)",
            "Get-PngCrc32",
            "IHDR",
            "IDAT",
            "EnumDisplaySettingsExW(ENUM_CURRENT_SETTINGS)",
            "GetScaleFactorForMonitor",
            "GetDpiForWindow",
            "RetryAssetLibraryLoad",
            "layer1_win32",
            "layer2_wpf",
            "layer3_target",
            "layer4_action",
            "repositorySchemaVersion",
            "repositoryAssetCount",
            "repositoryImplementation",
            "repositoryProofRecordedAt",
            "boundary_no_op_confirmed",
            "workspace_restore_snapshots",
            "restart_settings_match_previous_session",
            "previousFinalSnapshot",
            "physicalActions",
            "previousTupleInteractionAt",
            "synthetic-directory-recursive");

        foreach (var forbidden in new[]
                 {
                     "Set-Content", "Add-Content", "Clear-Content", "Out-File", "New-Item", "Remove-Item", "Copy-Item", "Move-Item",
                     "Start-Process", "SendInput", "mouse_event", "keybd_event", "SetCursorPos", "PostMessage",
                     "SendMessage", "System.Windows.Automation", "Microsoft.Win32.Registry", "File.Write", "Directory.CreateDirectory",
                     "Directory.Delete", "Invoke-WebRequest", "Invoke-RestMethod", "System.Net.Http"
                 })
            Assert.DoesNotContain(forbidden, script, StringComparison.OrdinalIgnoreCase);

        Assert.DoesNotContain("IsPathFullyQualified", script, StringComparison.Ordinal);
    }

    [TestMethod]
    public void ValidatorRejectsPassedSelfReportWhenRawWindowJsonAndPngAreMissing()
    {
        var temporaryRoot = Path.Combine(Path.GetTempPath(), $"pixel-tart-gatea-contract-{Guid.NewGuid():N}");
        var toolRoot = Path.Combine(temporaryRoot, "tool");
        var runRoot = Path.Combine(temporaryRoot, "run");
        Directory.CreateDirectory(toolRoot);
        Directory.CreateDirectory(runRoot);
        try
        {
            var sourceScript = PathAt("tools/AssetLibraryP1Acceptance/Test-AssetLibraryP1GateAEvidence.ps1");
            var sourceContract = PathAt("tools/AssetLibraryP1Acceptance/gate-a-evidence-contract.json");
            var scriptPath = Path.Combine(toolRoot, Path.GetFileName(sourceScript));
            var contractPath = Path.Combine(toolRoot, Path.GetFileName(sourceContract));
            File.Copy(sourceScript, scriptPath);

            var contract = JsonNode.Parse(File.ReadAllText(sourceContract, Encoding.UTF8))!.AsObject();
            contract["capture_status"] = "captured";
            File.WriteAllText(contractPath, contract.ToJsonString(new JsonSerializerOptions { WriteIndented = true }), new UTF8Encoding(false));

            File.WriteAllText(
                Path.Combine(runRoot, "08-first-empty-self-reported.window-evidence.json"),
                """
                {
                  "schema": "pixel-tart-asset-library-p1-window-evidence/v1",
                  "capture_name": "08-first-empty-self-reported",
                  "ui_input_generated": false,
                  "synthetic_ui_events_generated": false,
                  "screenshot": {
                    "file_name": "08-first-empty-self-reported.png",
                    "sha256": "SELF_REPORTED_ONLY",
                    "png_signature_verified": true
                  },
                  "verification": {
                    "exact_pid_path_title_verified": true,
                    "single_product_main_window_verified": true,
                    "single_global_matching_process_verified": true,
                    "exact_window_foreground_verified": true,
                    "window_stable_during_capture": true,
                    "display_mode_and_scale_stable_during_capture": true,
                    "passed": true
                  },
                  "passed": true
                }
                """,
                new UTF8Encoding(false));

            var result = RunPowerShell(scriptPath, runRoot);
            Assert.AreNotEqual(0, result.ExitCode, "A self-reported pass must never satisfy the raw evidence validator.");
            StringAssert.Contains(result.Output, "08-first-empty-self-reported.window-evidence.json");
            StringAssert.Contains(result.Output, "PNG is missing");
            StringAssert.Contains(result.Output, "executable path identity mismatch");
        }
        finally
        {
            if (Directory.Exists(temporaryRoot)) Directory.Delete(temporaryRoot, recursive: true);
        }
    }

    [TestMethod]
    public void ValidatorAcceptsCompleteRawEvidenceOnWindowsPowerShellAndDoesNotMutateAnyInput()
    {
        using var fixture = CapturedFixture.Create();
        var before = TreeFingerprint(fixture.Root);

        var result = RunPowerShell(fixture.ScriptPath, fixture.RunRoot);

        Assert.AreEqual(0, result.ExitCode, result.Output);
        StringAssert.Contains(result.Output, "pixel-tart-asset-library-p1-gate-a-validation/v1");
        Assert.AreEqual(before, TreeFingerprint(fixture.Root), "The read-only validator changed its contract, script, or run evidence.");
    }

    [TestMethod]
    public void ValidatorRejectsControllerEventTimelineTamperingEvenWhenSnapshotsRemainValid()
    {
        using var fixture = CapturedFixture.Create();
        var eventPath = Path.Combine(fixture.RunRoot, "sessions", "retry", "controller-events.jsonl");
        var lines = File.ReadAllLines(eventPath, Encoding.UTF8);
        var target = Array.FindIndex(lines, line => line.Contains("real-repository-query-entered", StringComparison.Ordinal));
        Assert.IsGreaterThanOrEqualTo(0, target);
        lines[target] = lines[target].Replace("real-repository-query-entered", "self-reported-repository-pass", StringComparison.Ordinal);
        File.WriteAllLines(eventPath, lines, new UTF8Encoding(false));

        var result = RunPowerShell(fixture.ScriptPath, fixture.RunRoot);

        Assert.AreNotEqual(0, result.ExitCode);
        StringAssert.Contains(result.Output, "controller:real-repository-query-entered:2");
    }

    [TestMethod]
    public void ValidatorRejectsPngWhoseIdatCrcDoesNotMatchItsBytes()
    {
        using var fixture = CapturedFixture.Create();
        var pngPath = Path.Combine(fixture.RunRoot, "dpi-1366x768-100pct-default.png");
        var bytes = File.ReadAllBytes(pngPath);
        var idatType = FindAscii(bytes, "IDAT");
        Assert.IsGreaterThanOrEqualTo(4, idatType);
        Assert.IsLessThan(bytes.Length, idatType + 4);
        bytes[idatType + 4] ^= 0x01;
        File.WriteAllBytes(pngPath, bytes);

        var result = RunPowerShell(fixture.ScriptPath, fixture.RunRoot);

        Assert.AreNotEqual(0, result.ExitCode);
        StringAssert.Contains(result.Output, "PNG chunk 'IDAT' has a CRC mismatch");
    }

    [TestMethod]
    public void ValidatorAcceptsCompleteRetryKeyboardActivationAndRejectsMissingKeyUp()
    {
        using var fixture = CapturedFixture.Create();
        fixture.UseRetryKeyboardActivation("Enter");
        var accepted = RunPowerShell(fixture.ScriptPath, fixture.RunRoot);
        Assert.AreEqual(0, accepted.ExitCode, accepted.Output);

        fixture.RemoveRetryKeyboardKeyUp();
        var rejected = RunPowerShell(fixture.ScriptPath, fixture.RunRoot);
        Assert.AreNotEqual(0, rejected.ExitCode, rejected.Output);
        StringAssert.Contains(rejected.Output, "exactly one verified physical mouse or keyboard activation");
    }

    private sealed class CapturedFixture : IDisposable
    {
        private const string Protocol = "pixel-tart-asset-library-p1-state/v1";
        private const string PointerProtocol = "pixel-tart-physical-pointer/v1";
        private const string ExecutableName = "PixelTart_ModularHarness_V1_DevPreview.exe";
        private const string WindowTitle = "像素蛋挞 [Modular Harness Dev]";
        private int _pngSeed = 1;

        private CapturedFixture(string root)
        {
            Root = root;
            ToolRoot = Path.Combine(root, "tool");
            RunRoot = Path.Combine(root, "run");
            Directory.CreateDirectory(ToolRoot);
            Directory.CreateDirectory(RunRoot);
            ScriptPath = Path.Combine(ToolRoot, "Test-AssetLibraryP1GateAEvidence.ps1");
            ContractPath = Path.Combine(ToolRoot, "gate-a-evidence-contract.json");
        }

        public string Root { get; }
        public string ToolRoot { get; }
        public string RunRoot { get; }
        public string ScriptPath { get; }
        public string ContractPath { get; }
        private string ExecutablePath => Path.Combine(RunRoot, ExecutableName);
        private string ExecutableHash { get; set; } = string.Empty;

        public void UseRetryKeyboardActivation(string key)
        {
            var path = Path.Combine(RunRoot, "retry-physical-pointer.json");
            var root = JsonNode.Parse(File.ReadAllText(path))!.AsObject();
            var startedAt = new DateTimeOffset(2026, 8, 19, 0, 1, 11, TimeSpan.Zero);
            root["attempts"] = new JsonArray();
            root["key_attempts"] = new JsonArray(JsonSerializer.SerializeToNode(RetryKeyAttempt("key-retry", "RetryAssetLibraryLoad", key, startedAt)));
            File.WriteAllText(path, root.ToJsonString(new JsonSerializerOptions { WriteIndented = true }), new UTF8Encoding(false));
        }

        public void RemoveRetryKeyboardKeyUp()
        {
            var path = Path.Combine(RunRoot, "retry-physical-pointer.json");
            var root = JsonNode.Parse(File.ReadAllText(path))!.AsObject();
            var attempt = root["key_attempts"]!.AsArray()[0]!.AsObject();
            attempt["layer1_win32"]!["key_up_received"] = false;
            attempt["layer1_win32"]!["events"] = new JsonArray(attempt["layer1_win32"]!["events"]!.AsArray()[0]!.DeepClone());
            File.WriteAllText(path, root.ToJsonString(new JsonSerializerOptions { WriteIndented = true }), new UTF8Encoding(false));
        }

        public static CapturedFixture Create()
        {
            var fixture = new CapturedFixture(Path.Combine(Path.GetTempPath(), $"pixel-tart-gatea-success-{Guid.NewGuid():N}"));
            try
            {
                File.Copy(PathAt("tools/AssetLibraryP1Acceptance/Test-AssetLibraryP1GateAEvidence.ps1"), fixture.ScriptPath);
                var contract = JsonNode.Parse(Read("tools/AssetLibraryP1Acceptance/gate-a-evidence-contract.json"))!.AsObject();
                contract["capture_status"] = "captured";
                File.WriteAllText(fixture.ContractPath, contract.ToJsonString(new JsonSerializerOptions { WriteIndented = true }), new UTF8Encoding(false));
                fixture.Build(contract);
                return fixture;
            }
            catch
            {
                fixture.Dispose();
                throw;
            }
        }

        private void Build(JsonObject contract)
        {
            File.WriteAllBytes(ExecutablePath, Encoding.ASCII.GetBytes("fixture executable identity\n"));
            ExecutableHash = Sha256(File.ReadAllBytes(ExecutablePath));

            var firstBase = new DateTimeOffset(2026, 8, 19, 0, 0, 0, TimeSpan.Zero);
            var retryBase = firstBase.AddMinutes(1);
            BuildStateSession(contract, "first-empty-session", "first", 100, firstBase);
            BuildStateSession(contract, "retry-session", "retry", 200, retryBase);

            WriteWindowEvidence("08-first-empty-fixture", firstBase.AddSeconds(8), 100, "0x100", 144, 1920, 1080, 150, 60);
            WriteWindowEvidence("09-loading-fixture", retryBase.AddSeconds(2.5), 200, "0x200", 144, 1920, 1080, 150, 60);
            WriteWindowEvidence("10-recoverable-error-fixture", retryBase.AddSeconds(10), 200, "0x200", 144, 1920, 1080, 150, 60);
            WriteWindowEvidence("11-retry-recovered-fixture", retryBase.AddSeconds(18), 200, "0x200", 144, 1920, 1080, 150, 60);

            var retryAttemptAt = retryBase.AddSeconds(11);
            WriteJson(Path.Combine(RunRoot, "retry-physical-pointer.json"), D(
                ("protocol", PointerProtocol),
                ("diagnostic_id", "retry-diagnostic"),
                ("process_id", 200),
                ("process_started_at", retryBase.AddSeconds(-5).ToString("O")),
                ("started_at", retryBase.AddSeconds(-5).ToString("O")),
                ("updated_at", retryAttemptAt.AddSeconds(1).ToString("O")),
                ("attempts", new[] { PointerAttempt("pointer-retry", "RetryAssetLibraryLoad", retryAttemptAt) }),
                ("key_attempts", Array.Empty<object>()),
                ("control_state_transitions", Array.Empty<object>()),
                ("workspace_restore_snapshots", Array.Empty<object>())));

            var matrixBase = firstBase.AddHours(1);
            var matrix = new[]
            {
                (Name: "1366x768-100pct", Width: 1366, Height: 768, Scale: 100, Dpi: 96),
                (Name: "1920x1080-125pct", Width: 1920, Height: 1080, Scale: 125, Dpi: 120),
                (Name: "1920x1080-150pct", Width: 1920, Height: 1080, Scale: 150, Dpi: 144),
                (Name: "2560x1440-175pct", Width: 2560, Height: 1440, Scale: 175, Dpi: 168)
            };
            var dpiAttempts = new List<Dictionary<string, object?>>();
            for (var index = 0; index < matrix.Length; index++)
            {
                var tuple = matrix[index];
                var defaultAt = matrixBase.AddSeconds(index * 10);
                var actionAt = defaultAt.AddSeconds(1);
                var interactionAt = defaultAt.AddSeconds(3);
                WriteWindowEvidence($"dpi-{tuple.Name}-default", defaultAt, 300, "0x300", tuple.Dpi, tuple.Width, tuple.Height, tuple.Scale, 60);
                dpiAttempts.Add(PointerAttempt($"pointer-dpi-{index + 1}", index % 2 == 0 ? "ToggleAssetOrganizationPane" : "ToggleAssetInspectorPane", actionAt));
                WriteWindowEvidence($"dpi-{tuple.Name}-interaction", interactionAt, 300, "0x300", tuple.Dpi, tuple.Width, tuple.Height, tuple.Scale, 60);
            }

            var keyboardStart = firstBase.AddMinutes(30);
            var keyAttempts = new List<Dictionary<string, object?>>();
            var transitions = new List<Dictionary<string, object?>>();
            AddRegularKey(keyAttempts, transitions, "key-org-left", "transition-org-left", "AssetOrganizationSplitter", "OrganizationPaneWidth", "Left", "decrease", 310, 300, 180, 420, keyboardStart.AddSeconds(1));
            AddRegularKey(keyAttempts, transitions, "key-org-right", "transition-org-right", "AssetOrganizationSplitter", "OrganizationPaneWidth", "Right", "increase", 290, 300, 180, 420, keyboardStart.AddSeconds(2));
            AddRegularKey(keyAttempts, transitions, "key-inspector-left", "transition-inspector-left", "AssetInspectorSplitter", "InspectorPaneWidth", "Left", "increase", 290, 300, 260, 520, keyboardStart.AddSeconds(3));
            AddRegularKey(keyAttempts, transitions, "key-inspector-right", "transition-inspector-right", "AssetInspectorSplitter", "InspectorPaneWidth", "Right", "decrease", 310, 300, 260, 520, keyboardStart.AddSeconds(4));
            AddBoundaryKey(keyAttempts, transitions, "key-org-min", "transition-org-min", "AssetOrganizationSplitter", "Left", 180, keyboardStart.AddSeconds(5));
            AddBoundaryKey(keyAttempts, transitions, "key-org-max", "transition-org-max", "AssetOrganizationSplitter", "Right", 420, keyboardStart.AddSeconds(6));
            AddBoundaryKey(keyAttempts, transitions, "key-inspector-min", "transition-inspector-min", "AssetInspectorSplitter", "Right", 260, keyboardStart.AddSeconds(7));
            AddBoundaryKey(keyAttempts, transitions, "key-inspector-max", "transition-inspector-max", "AssetInspectorSplitter", "Left", 520, keyboardStart.AddSeconds(8));

            var restoreSnapshots = new[]
            {
                WorkspaceSnapshot(keyboardStart.AddSeconds(10), 300, true, 300, false, 200),
                WorkspaceSnapshot(keyboardStart.AddSeconds(11), 300, false, 300, false, 200),
                WorkspaceSnapshot(keyboardStart.AddSeconds(12), 300, false, 300, true, 200),
                WorkspaceSnapshot(keyboardStart.AddSeconds(13), 300, false, 300, false, 200)
            };
            var keyboardUpdated = matrixBase.AddSeconds(33);
            WriteJson(Path.Combine(RunRoot, "keyboard-physical-pointer.json"), D(
                ("protocol", PointerProtocol),
                ("diagnostic_id", "keyboard-diagnostic"),
                ("process_id", 300),
                ("process_started_at", keyboardStart.AddSeconds(-10).ToString("O")),
                ("started_at", keyboardStart.AddSeconds(-10).ToString("O")),
                ("updated_at", keyboardUpdated.ToString("O")),
                ("attempts", dpiAttempts),
                ("key_attempts", keyAttempts),
                ("control_state_transitions", transitions),
                ("workspace_restore_snapshots", restoreSnapshots)));

            var restartAt = matrixBase.AddSeconds(40);
            var restartSnapshot = WorkspaceSnapshot(restartAt.AddSeconds(1), 300, false, 300, false, 200);
            restartSnapshot["restore_confirmed"] = true;
            restartSnapshot["restart_comparison_performed"] = true;
            restartSnapshot["restart_settings_match_previous_session"] = true;
            restartSnapshot["previous_diagnostic_id"] = "keyboard-diagnostic";
            WriteJson(Path.Combine(RunRoot, "restart-physical-pointer.json"), D(
                ("protocol", PointerProtocol),
                ("diagnostic_id", "restart-diagnostic"),
                ("process_id", 301),
                ("process_started_at", restartAt.ToString("O")),
                ("started_at", restartAt.ToString("O")),
                ("updated_at", restartAt.AddSeconds(2).ToString("O")),
                ("previous_session", D(
                    ("diagnostic_id", "keyboard-diagnostic"),
                    ("process_id", 300),
                    ("has_workspace_state", true),
                    ("organization_persisted_width", 300d),
                    ("inspector_persisted_width", 300d),
                    ("thumbnail_persisted_width", 200d),
                    ("organization_collapsed", false),
                    ("inspector_collapsed", false))),
                ("attempts", Array.Empty<object>()),
                ("key_attempts", Array.Empty<object>()),
                ("control_state_transitions", Array.Empty<object>()),
                ("workspace_restore_snapshots", new[] { restartSnapshot })));

            WriteWindowEvidence("restore-baseline-3840x2160-150pct-fixture", matrixBase.AddSeconds(45), 301, "0x301", 144, 3840, 2160, 150, 60);
            WriteJson(Path.Combine(RunRoot, "initial-import-0-to-12.json"), D(
                ("source_kind", "synthetic-directory-recursive"),
                ("selected_file_count", 12),
                ("imported_count", 12),
                ("failed_count", 0),
                ("repository_asset_count_before", 0),
                ("repository_asset_count_after", 12),
                ("picker_accepted", true),
                ("import_command_entered", true),
                ("import_service_entered", true)));
        }

        private void BuildStateSession(JsonObject contract, string sessionId, string directoryName, int processId, DateTimeOffset startedAt)
        {
            var session = contract["state_chain"]!["sessions"]!.AsArray()
                .Select(value => value!.AsObject())
                .Single(value => value["id"]!.GetValue<string>() == sessionId);
            var directory = Path.Combine(RunRoot, "sessions", directoryName);
            Directory.CreateDirectory(directory);
            var dataDirectory = Path.Combine(directory, "Data");
            Directory.CreateDirectory(dataDirectory);
            var databasePath = Path.Combine(dataDirectory, "asset-library-v16.db");
            File.WriteAllBytes(databasePath, Encoding.ASCII.GetBytes("SQLite format 3\0fixture"));
            var snapshotPath = Path.Combine(directory, "view-model-snapshots.jsonl");
            var controllerPath = Path.Combine(directory, "controller-events.jsonl");
            var timeline = session["required_timeline"]!.AsArray();
            var readySequence = timeline
                .Select((value, index) => (Value: value!.AsObject(), Sequence: index + 1))
                .Single(item => item.Value["stream"]!.GetValue<string>() == "snapshot" && item.Value["stage"]!.GetValue<string>() == "ready")
                .Sequence;
            var readyAt = startedAt.AddSeconds(readySequence);
            var retry = sessionId == "retry-session";
            WriteJson(Path.Combine(directory, "scenario-manifest.json"), D(
                ("protocol", Protocol),
                ("scenario", session["scenario"]!.GetValue<string>()),
                ("processName", Path.GetFileNameWithoutExtension(ExecutableName)),
                ("processId", processId),
                ("isolatedRoot", directory),
                ("databasePath", databasePath),
                ("freshDatabaseVerified", true),
                ("snapshotFile", snapshotPath),
                ("controllerEventFile", controllerPath),
                ("repositorySource", "real-repository"),
                ("repositoryImplementation", "SqliteAssetLibraryRepository"),
                ("repositorySchemaVersion", 6),
                ("repositoryAssetCount", 0),
                ("repositoryProofStage", "ready"),
                ("repositoryProofRecordedAt", readyAt.ToString("O")),
                ("startRouteSource", "PIXEL_TART_ASSET_LIBRARY_P1_START_ROUTE"),
                ("startRoute", "asset-library"),
                ("startRouteCurrentPage", "AssetLibrary"),
                ("startRouteHead", "3b5ff13bb4c5b4c2001f978cb6ab31f5715cd7af"),
                ("startRouteRecordedAt", startedAt.AddMilliseconds(100).ToString("O")),
                ("exceptionType", retry ? "System.IO.IOException" : null),
                ("injectionId", retry ? "asset-library-p1-initial-query-io-once/v1" : null),
                ("failureAttempt", retry ? 1 : null)));

            var snapshotLines = new List<string>();
            var controllerLines = new List<string>();
            var sequence = 0;
            foreach (var requiredNode in timeline)
            {
                var required = requiredNode!.AsObject();
                sequence++;
                var stream = required["stream"]!.GetValue<string>();
                var stage = required["stage"]!.GetValue<string>();
                var attempt = required["attempt"]!.GetValue<int>();
                var recordedAt = startedAt.AddSeconds(sequence);
                if (stream == "snapshot")
                {
                    var snapshot = D(
                        ("stage", stage),
                        ("attempt", attempt),
                        ("recordedAt", recordedAt.ToString("O")),
                        ("isLoading", stage is not "ready" and not "error-visible" and not "query-error" and not "initial-query-failed"),
                        ("isReady", stage == "ready"),
                        ("hasLoadError", stage is "query-error" or "error-visible" or "initial-query-failed"),
                        ("visibleAssetCount", 0));
                    if (stage is "query-error" or "error-visible" or "initial-query-failed")
                    {
                        snapshot["exceptionType"] = "System.IO.IOException";
                        snapshot["injectionId"] = "asset-library-p1-initial-query-io-once/v1";
                    }
                    if (stage == "ready")
                    {
                        snapshot["repositorySource"] = "real-repository";
                        snapshot["repositoryImplementation"] = "SqliteAssetLibraryRepository";
                        snapshot["repositorySchemaVersion"] = 6;
                        snapshot["repositoryAssetCount"] = 0;
                    }
                    snapshotLines.Add(JsonSerializer.Serialize(D(
                        ("protocol", Protocol),
                        ("sequence", sequence),
                        ("snapshot", snapshot))));
                }
                else
                {
                    controllerLines.Add(JsonSerializer.Serialize(D(
                        ("protocol", Protocol),
                        ("sequence", sequence),
                        ("recordedAt", recordedAt.ToString("O")),
                        ("stage", stage),
                        ("attempt", attempt),
                        ("exceptionType", stage == "recoverable-query-error-injected" ? "System.IO.IOException" : null),
                        ("injectionId", stage == "recoverable-query-error-injected" ? "asset-library-p1-initial-query-io-once/v1" : null),
                        ("repositoryAssetCount", stage == "real-repository-query-completed" ? 0 : null))));
                }
            }
            File.WriteAllLines(snapshotPath, snapshotLines, new UTF8Encoding(false));
            File.WriteAllLines(controllerPath, controllerLines, new UTF8Encoding(false));
        }

        private void WriteWindowEvidence(string captureName, DateTimeOffset capturedAt, int processId, string hwnd, int dpi, int displayWidth, int displayHeight, int scale, int refresh)
        {
            var pngName = captureName + ".png";
            var pngPath = Path.Combine(RunRoot, pngName);
            var png = Png(_pngSeed++);
            File.WriteAllBytes(pngPath, png);
            var rect = D(("left", 0), ("top", 0), ("right", 1), ("bottom", 1), ("width", 1), ("height", 1));
            var window = D(
                ("title", WindowTitle),
                ("hwnd", hwnd),
                ("dpi", dpi),
                ("rect_physical_pixels", rect),
                ("is_foreground", true),
                ("is_minimized", false));
            Dictionary<string, object?> Display() => D(
                ("current_mode_source", "EnumDisplaySettingsExW(ENUM_CURRENT_SETTINGS)"),
                ("scale_factor_source", "GetScaleFactorForMonitor"),
                ("current_width_physical_pixels", displayWidth),
                ("current_height_physical_pixels", displayHeight),
                ("current_refresh_rate_hz", refresh),
                ("scale_factor_percent", scale));
            WriteJson(Path.Combine(RunRoot, captureName + ".window-evidence.json"), D(
                ("schema", "pixel-tart-asset-library-p1-window-evidence/v1"),
                ("capture_name", captureName),
                ("captured_at_utc", capturedAt.ToString("O")),
                ("ui_input_generated", false),
                ("synthetic_ui_events_generated", false),
                ("expected", D(("process_id", processId), ("executable_path", ExecutablePath), ("window_title", WindowTitle))),
                ("process", D(
                    ("process_id", processId),
                    ("executable_path", ExecutablePath),
                    ("executable_name", ExecutableName),
                    ("executable_sha256", ExecutableHash),
                    ("global_matching_name_process_count", 1),
                    ("global_matching_exact_path_process_count", 1),
                    ("global_matching_name_process_count_after_capture", 1),
                    ("global_matching_exact_path_process_count_after_capture", 1))),
                ("window_before_capture", window),
                ("window_after_capture", window),
                ("exact_title_main_window_count_before_capture", 1),
                ("exact_title_main_window_count_after_capture", 1),
                ("unexpected_auxiliary_window_count", 0),
                ("verification", D(
                    ("exact_pid_path_title_verified", true),
                    ("single_product_main_window_verified", true),
                    ("single_global_matching_process_verified", true),
                    ("exact_window_foreground_verified", true),
                    ("window_stable_during_capture", true),
                    ("display_mode_and_scale_stable_during_capture", true),
                    ("passed", true))),
                ("dpi_awareness", D(("observed_dpi_source", "GetDpiForWindow"))),
                ("display", D(("before_capture", Display()), ("after_capture", Display()))),
                ("screenshot", D(
                    ("file_name", pngName),
                    ("absolute_path", pngPath),
                    ("sha256", Sha256(png)),
                    ("png_signature_verified", true),
                    ("width_physical_pixels", 1),
                    ("height_physical_pixels", 1)))));
        }

        private static Dictionary<string, object?> PointerAttempt(string attemptId, string automationId, DateTimeOffset startedAt) => D(
            ("attempt_id", attemptId),
            ("started_at", startedAt.ToString("O")),
            ("updated_at", startedAt.AddSeconds(1).ToString("O")),
            ("origin", "Win32"),
            ("layer1_win32", D(
                ("l_button_down_received", true),
                ("l_button_up_received", true),
                ("down", D(("timestamp", startedAt.ToString("O")))),
                ("up", D(("timestamp", startedAt.AddMilliseconds(200).ToString("O")))))),
            ("layer2_wpf", D(
                ("preview_mouse_down_received", true),
                ("preview_mouse_up_received", true),
                ("events", new[]
                {
                    D(("event_name", "PreviewMouseDown"), ("timestamp", startedAt.AddMilliseconds(50).ToString("O"))),
                    D(("event_name", "PreviewMouseUp"), ("timestamp", startedAt.AddMilliseconds(150).ToString("O")))
                }))),
            ("down_target_automation_id", automationId),
            ("up_target_automation_id", automationId),
            ("down_control_automation_id", automationId),
            ("up_control_automation_id", automationId),
            ("button_instance_same_down_up", true),
            ("layer4_action", D(
                ("button_click_received", true),
                ("physical_target_confirmed", true),
                ("button", D(("automation_id", automationId))))));

        private static Dictionary<string, object?> RetryKeyAttempt(string attemptId, string automationId, string key, DateTimeOffset startedAt)
        {
            var virtualKey = key == "Enter" ? 13 : 32;
            Dictionary<string, object?> Native(string message, DateTimeOffset timestamp) => D(
                ("timestamp", timestamp.ToString("O")), ("message", message), ("virtual_key", virtualKey),
                ("scan_code", key == "Enter" ? 28 : 57), ("repeat_count", 1), ("modifiers", "None"), ("native_message_time", 1));
            var chain = new[] { D(("automation_id", automationId), ("type", "Button")), D(("automation_id", "AssetLibraryPage"), ("type", "AssetLibraryPage")) };
            return D(
                ("attempt_id", attemptId),
                ("started_at", startedAt.ToString("O")),
                ("updated_at", startedAt.AddMilliseconds(500).ToString("O")),
                ("origin", "Win32"),
                ("key", key),
                ("virtual_key", virtualKey),
                ("layer1_win32", D(
                    ("key_down_received", true), ("key_up_received", true),
                    ("events", new[] { Native("WM_KEYDOWN", startedAt), Native("WM_KEYUP", startedAt.AddMilliseconds(200)) }))),
                ("layer2_wpf", D(
                    ("preview_key_down_received", true), ("key_down_received", true), ("preview_key_up_received", true), ("key_up_received", true),
                    ("events", new[] { "PreviewKeyDown", "KeyDown", "PreviewKeyUp", "KeyUp" }.Select((name, index) =>
                        D(("event_name", name), ("key", key), ("timestamp", startedAt.AddMilliseconds(50 * index).ToString("O")))).ToArray()))),
                ("layer3_target", D(
                    ("control_automation_id", automationId), ("control", D(("automation_id", automationId))),
                    ("focused_element_at_down", D(("automation_id", automationId))), ("focused_element_at_up", D(("automation_id", automationId))),
                    ("focused_automation_id_at_down", automationId), ("focused_automation_id_at_up", automationId),
                    ("focus_parent_chain_at_down", chain), ("focus_parent_chain_at_up", chain))),
                ("layer4_action", D(
                    ("button_click_received", true), ("physical_target_confirmed", true),
                    ("button", D(("automation_id", automationId))))));
        }

        private static void AddRegularKey(List<Dictionary<string, object?>> attempts, List<Dictionary<string, object?>> transitions, string attemptId, string transitionId, string automationId, string propertyName, string key, string adjustment, double before, double after, double minimum, double maximum, DateTimeOffset startedAt)
        {
            attempts.Add(KeyAttempt(attemptId, transitionId, automationId, key, before, after, true, false, startedAt));
            transitions.Add(KeyTransition(attemptId, transitionId, automationId, propertyName, key, adjustment, before, after, minimum, maximum, true, false, startedAt));
        }

        private static void AddBoundaryKey(List<Dictionary<string, object?>> attempts, List<Dictionary<string, object?>> transitions, string attemptId, string transitionId, string automationId, string key, double boundary, DateTimeOffset startedAt)
        {
            var propertyName = automationId == "AssetOrganizationSplitter" ? "OrganizationPaneWidth" : "InspectorPaneWidth";
            var minimum = automationId == "AssetOrganizationSplitter" ? 180d : 260d;
            var maximum = automationId == "AssetOrganizationSplitter" ? 420d : 520d;
            attempts.Add(KeyAttempt(attemptId, transitionId, automationId, key, boundary, boundary, false, true, startedAt));
            transitions.Add(KeyTransition(attemptId, transitionId, automationId, propertyName, key, string.Empty, boundary, boundary, minimum, maximum, false, true, startedAt));
        }

        private static Dictionary<string, object?> KeyAttempt(string attemptId, string transitionId, string automationId, string key, double before, double after, bool changed, bool boundary, DateTimeOffset startedAt)
        {
            var virtualKey = key == "Left" ? 37 : 39;
            Dictionary<string, object?> Native(string message, DateTimeOffset timestamp) => D(
                ("timestamp", timestamp.ToString("O")), ("message", message), ("virtual_key", virtualKey),
                ("scan_code", 75), ("repeat_count", 1), ("modifiers", "None"), ("native_message_time", 1));
            var down = Native("WM_KEYDOWN", startedAt);
            var up = Native("WM_KEYUP", startedAt.AddMilliseconds(200));
            var chain = new[] { D(("automation_id", automationId), ("type", "GridSplitter")), D(("automation_id", "AssetLibraryPage"), ("type", "AssetLibraryPage")) };
            return D(
                ("attempt_id", attemptId),
                ("started_at", startedAt.ToString("O")),
                ("updated_at", startedAt.AddMilliseconds(500).ToString("O")),
                ("origin", "Win32"),
                ("key", key),
                ("virtual_key", virtualKey),
                ("layer1_win32", D(("key_down_received", true), ("key_up_received", true), ("events", new[] { down, up }))),
                ("layer2_wpf", D(
                    ("preview_key_down_received", true), ("key_down_received", true), ("preview_key_up_received", true), ("key_up_received", true),
                    ("events", new[] { "PreviewKeyDown", "KeyDown", "PreviewKeyUp", "KeyUp" }.Select((name, index) =>
                        D(("event_name", name), ("key", key), ("timestamp", startedAt.AddMilliseconds(50 * index).ToString("O")))).ToArray()))),
                ("layer3_target", D(
                    ("control_automation_id", automationId),
                    ("control", D(("automation_id", automationId))),
                    ("focused_element_at_down", D(("automation_id", automationId))),
                    ("focused_element_at_up", D(("automation_id", automationId))),
                    ("focused_automation_id_at_down", automationId),
                    ("focused_automation_id_at_up", automationId),
                    ("focus_parent_chain_at_down", chain),
                    ("focus_parent_chain_at_up", chain))),
                ("layer4_action", D(
                    ("control_state_transition_confirmed", true),
                    ("settings_write_back_confirmed", true),
                    ("state_changed", changed),
                    ("boundary_reached", boundary),
                    ("boundary_no_op_confirmed", boundary),
                    ("before_actual_value", before),
                    ("after_actual_value", after),
                    ("after_persisted_value", after),
                    ("completed_at", startedAt.AddMilliseconds(500).ToString("O")),
                    ("transition_id", transitionId))));
        }

        private static Dictionary<string, object?> KeyTransition(string attemptId, string transitionId, string automationId, string propertyName, string key, string adjustment, double before, double after, double minimum, double maximum, bool changed, bool boundary, DateTimeOffset startedAt) => D(
            ("transition_id", transitionId),
            ("started_at", startedAt.ToString("O")),
            ("completed_at", startedAt.AddMilliseconds(500).ToString("O")),
            ("input_kind", "Keyboard"),
            ("input_key", key),
            ("expected_adjustment", adjustment),
            ("control_kind", "GridSplitter"),
            ("property_name", propertyName),
            ("control", D(("automation_id", automationId))),
            ("before_actual_value", before),
            ("after_actual_value", after),
            ("before_persisted_value", before),
            ("after_persisted_value", after),
            ("minimum_value", minimum),
            ("maximum_value", maximum),
            ("state_changed", changed),
            ("settings_state_changed", changed),
            ("settings_write_back_confirmed", true),
            ("boundary_reached", boundary),
            ("boundary_no_op_confirmed", boundary),
            ("correlated_key_attempt_id", attemptId),
            ("target_matched_at_start", true),
            ("layer1_win32_confirmed", true),
            ("layer2_wpf_confirmed", true),
            ("layer3_target_confirmed", true),
            ("layer4_action_confirmed", true),
            ("result", boundary ? "BoundaryNoOpConfirmed" : "Confirmed"));

        private static Dictionary<string, object?> WorkspaceSnapshot(DateTimeOffset timestamp, double organizationWidth, bool organizationCollapsed, double inspectorWidth, bool inspectorCollapsed, double thumbnailWidth) => D(
            ("timestamp", timestamp.ToString("O")),
            ("organization_actual_width", organizationCollapsed ? 0d : organizationWidth),
            ("organization_persisted_width", organizationWidth),
            ("organization_visible", !organizationCollapsed),
            ("organization_collapsed", organizationCollapsed),
            ("inspector_actual_width", inspectorCollapsed ? 0d : inspectorWidth),
            ("inspector_persisted_width", inspectorWidth),
            ("inspector_visible", !inspectorCollapsed),
            ("inspector_collapsed", inspectorCollapsed),
            ("thumbnail_actual_width", thumbnailWidth),
            ("thumbnail_persisted_width", thumbnailWidth),
            ("thumbnail_restore_confirmed", true));

        private static Dictionary<string, object?> D(params (string Key, object? Value)[] entries) =>
            entries.ToDictionary(entry => entry.Key, entry => entry.Value, StringComparer.Ordinal);

        private static void WriteJson(string path, object value) =>
            File.WriteAllText(path, JsonSerializer.Serialize(value, new JsonSerializerOptions { WriteIndented = true }), new UTF8Encoding(false));

        private static byte[] Png(int seed)
        {
            using var png = new MemoryStream();
            png.Write(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 });
            var ihdr = new byte[13];
            ihdr[3] = 1;
            ihdr[7] = 1;
            ihdr[8] = 8;
            ihdr[9] = 6;
            Chunk(png, "IHDR", ihdr);
            byte[] compressed;
            using (var buffer = new MemoryStream())
            {
                using (var zlib = new ZLibStream(buffer, CompressionLevel.SmallestSize, leaveOpen: true))
                    zlib.Write(new byte[] { 0, (byte)seed, (byte)(seed * 17), (byte)(seed * 31), 255 });
                compressed = buffer.ToArray();
            }
            Chunk(png, "IDAT", compressed);
            Chunk(png, "IEND", Array.Empty<byte>());
            return png.ToArray();
        }

        private static void Chunk(Stream output, string type, byte[] data)
        {
            WriteUInt32(output, (uint)data.Length);
            var typeBytes = Encoding.ASCII.GetBytes(type);
            output.Write(typeBytes);
            output.Write(data);
            var crcInput = new byte[typeBytes.Length + data.Length];
            Buffer.BlockCopy(typeBytes, 0, crcInput, 0, typeBytes.Length);
            Buffer.BlockCopy(data, 0, crcInput, typeBytes.Length, data.Length);
            WriteUInt32(output, Crc32(crcInput));
        }

        private static uint Crc32(byte[] bytes)
        {
            var crc = 0xffffffffu;
            foreach (var value in bytes)
            {
                crc ^= value;
                for (var bit = 0; bit < 8; bit++) crc = (crc & 1) != 0 ? (crc >> 1) ^ 0xedb88320u : crc >> 1;
            }
            return ~crc;
        }

        private static void WriteUInt32(Stream output, uint value) => output.Write(new[]
        {
            (byte)(value >> 24), (byte)(value >> 16), (byte)(value >> 8), (byte)value
        });

        public void Dispose()
        {
            if (Directory.Exists(Root)) Directory.Delete(Root, recursive: true);
        }
    }

    private static (int ExitCode, string Output) RunPowerShell(string scriptPath, string runRoot)
    {
        var executable = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.System),
            "WindowsPowerShell",
            "v1.0",
            "powershell.exe");
        Assert.IsTrue(File.Exists(executable), $"Windows PowerShell was not found at {executable}.");

        var startInfo = new ProcessStartInfo(executable)
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };
        startInfo.ArgumentList.Add("-NoProfile");
        startInfo.ArgumentList.Add("-NonInteractive");
        startInfo.ArgumentList.Add("-ExecutionPolicy");
        startInfo.ArgumentList.Add("Bypass");
        startInfo.ArgumentList.Add("-File");
        startInfo.ArgumentList.Add(scriptPath);
        startInfo.ArgumentList.Add("-RunRoot");
        startInfo.ArgumentList.Add(runRoot);

        using var process = Process.Start(startInfo)!;
        var standardOutput = process.StandardOutput.ReadToEndAsync();
        var standardError = process.StandardError.ReadToEndAsync();
        Assert.IsTrue(process.WaitForExit(30_000), "Gate A validator process did not exit within 30 seconds.");
        return (process.ExitCode, standardOutput.GetAwaiter().GetResult() + standardError.GetAwaiter().GetResult());
    }

    private static string TreeFingerprint(string root)
    {
        var lines = Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories)
            .Select(path => $"{Path.GetRelativePath(root, path).Replace('\\', '/')}:{Sha256(File.ReadAllBytes(path))}")
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
        return Sha256(Encoding.UTF8.GetBytes(string.Join("\n", lines)));
    }

    private static string Sha256(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes));

    private static int FindAscii(byte[] bytes, string text)
    {
        var pattern = Encoding.ASCII.GetBytes(text);
        for (var offset = 0; offset <= bytes.Length - pattern.Length; offset++)
        {
            var matched = true;
            for (var index = 0; index < pattern.Length; index++)
                if (bytes[offset + index] != pattern[index]) matched = false;
            if (matched) return offset;
        }
        return -1;
    }

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
