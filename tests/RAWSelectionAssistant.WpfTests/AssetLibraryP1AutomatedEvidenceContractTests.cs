using Microsoft.Data.Sqlite;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace RAWSelectionAssistant.WpfTests;

[TestClass]
public sealed class AssetLibraryP1AutomatedEvidenceContractTests
{
    private static readonly string[] ScenarioIds =
    [
        "first-empty/v1",
        "loading-error-retry-recovered/v1",
        "organization-splitter/v1",
        "inspector-splitter/v1",
        "pane-collapse-expand/v1",
        "thumbnail-slider/v1",
        "selection-navigation-restart/v1",
        "navigation-ime/v1",
        "layout-dpi-buttons/v1"
    ];

    [TestMethod]
    public void AutomatedContractIsIndependentFailClosedAndNeverClaimsManualEvidence()
    {
        var contract = Read("tools/AssetLibraryP1AutomatedAcceptance/automated-acceptance-contract.json");
        var validator = Read("tools/AssetLibraryP1AutomatedAcceptance/Test-P1AssetLibraryAutomatedEvidence.ps1");
        var readme = Read("tools/AssetLibraryP1AutomatedAcceptance/README.md");

        ContainsAll(contract,
            "pixel-tart-asset-library-p1-automated-acceptance-contract/v2",
            "\"validation_mode\": \"automated\"",
            "\"owner_manual_ux_smoke\": \"waived\"",
            "\"manual_evidence_claimed\": false",
            "\"automated_capture_status\": \"captured\"",
            "first-empty/v1",
            "layout-dpi-buttons/v1",
            "missing-screenshot",
            "mutated-hash",
            "wrong-scenario-order",
            "retry-twice",
            "direct-width-mutation",
            "direct-settings-mutation",
            "cross-run-splice",
            "wrong-pid-or-hwnd",
            "dll-hash-mismatch",
            "database-not-v6",
            "import-contamination",
            "uncleared-process",
            "dpi-overflow",
            "runner-session-splice",
            "process-session-splice",
            "pre-cleanup-audit-hash",
            "cleanup-path-splice",
            "build-log-hash",
            "sealed-binary-mutation",
            "application-hash-mismatch",
            "sealed-application-mutation",
            "sealed-dependency-mutation",
            "binary-tree-manifest-mismatch",
            "manifest-product-version-forgery",
            "actual-product-version-mismatch");
        ContainsAll(validator,
            "events.ndjson",
            "summary.json",
            "Security.Cryptography.SHA256",
            "SQLite format 3",
            "AssetLibrarySchemaInfo",
            "in-process-wpf-route",
            "RetryAssetLibraryLoad",
            "Historical Manual Gate A: NOT_CLOSED");
        ContainsAll(readme,
            "与历史 `P1_ASSET_LIBRARY_GATE_A_MANUAL_PACKET` 分离",
            "不表示真实 Windows 显示设置被切换",
            "只读输入树",
            "无人值守运行");

        Assert.DoesNotContain("Test-AssetLibraryP1GateAEvidence.ps1", validator, StringComparison.OrdinalIgnoreCase);
        Assert.IsFalse(System.Text.RegularExpressions.Regex.IsMatch(contract, "(?m)^\\s*\"capture_status\"\\s*:"));
        foreach (var forbidden in new[] { "SendInput", "SetForegroundWindow", "System.Windows.Automation", "Read-Host", "ChangeDisplaySettings" })
            Assert.DoesNotContain(forbidden, validator, StringComparison.OrdinalIgnoreCase);
    }

    [TestMethod]
    public void IndependentValidatorAcceptsCompleteApplicationEvidence()
    {
        using var fixture = new EvidenceFixture();
        var before = fixture.TreeFingerprint();
        var result = RunValidator(fixture.Root);

        Assert.AreEqual(0, result.ExitCode, result.Output);
        StringAssert.Contains(result.Output, "P1 Automated Acceptance evidence: PASS");
        Assert.AreEqual(before, fixture.TreeFingerprint(), "A successful validation changed its input tree.");
    }

    [TestMethod]
    public void ValidateExistingRunWrapperKeepsTheSealedRunTreeImmutable()
    {
        using var fixture = new EvidenceFixture();
        var before = fixture.TreeFingerprint();
        var result = RunValidateExistingWrapper(fixture.Root);

        Assert.AreEqual(0, result.ExitCode, result.Output);
        Assert.AreEqual(before, fixture.TreeFingerprint(), "ValidateExistingRun changed the sealed run tree.");
    }

    [TestMethod]
    public void IndependentValidatorUsesRunOwnedBinariesAfterRepositoryBuildOutputChanges()
    {
        using var fixture = new EvidenceFixture();
        fixture.OverwriteMutableBuildOutputs();
        var before = fixture.TreeFingerprint();

        var result = RunValidator(fixture.Root);

        Assert.AreEqual(0, result.ExitCode, result.Output);
        Assert.AreEqual(before, fixture.TreeFingerprint(), "Revalidation after a later build changed the sealed run tree.");
    }

    [TestMethod]
    [DataRow("missing-screenshot")]
    [DataRow("mutated-hash")]
    [DataRow("wrong-scenario-order")]
    [DataRow("retry-twice")]
    [DataRow("direct-width-mutation")]
    [DataRow("direct-settings-mutation")]
    [DataRow("cross-run-splice")]
    [DataRow("wrong-pid-or-hwnd")]
    [DataRow("dll-hash-mismatch")]
    [DataRow("database-not-v6")]
    [DataRow("import-contamination")]
    [DataRow("uncleared-process")]
    [DataRow("dpi-overflow")]
    [DataRow("runner-session-splice")]
    [DataRow("process-session-splice")]
    [DataRow("pre-cleanup-audit-hash")]
    [DataRow("cleanup-path-splice")]
    [DataRow("build-log-hash")]
    [DataRow("sealed-binary-mutation")]
    [DataRow("application-hash-mismatch")]
    [DataRow("sealed-application-mutation")]
    [DataRow("sealed-dependency-mutation")]
    [DataRow("binary-tree-manifest-mismatch")]
    [DataRow("manifest-product-version-forgery")]
    [DataRow("actual-product-version-mismatch")]
    public void IndependentValidatorRejectsRequiredFaultWithoutChangingInputTree(string fault)
    {
        using var fixture = new EvidenceFixture();
        fixture.Inject(fault);
        var before = fixture.TreeFingerprint();

        var result = RunValidator(fixture.Root);

        Assert.AreNotEqual(0, result.ExitCode, $"Fault '{fault}' was accepted.\n{result.Output}");
        if (fault is "manifest-product-version-forgery" or "actual-product-version-mismatch")
            StringAssert.Contains(result.Output, "ProductVersion", $"Fault '{fault}' did not exercise the sealed-file ProductVersion validator.");
        Assert.AreEqual(before, fixture.TreeFingerprint(), $"Validation of '{fault}' changed its input tree.");
    }

    [TestMethod]
    [DataRow("first-empty/v1")]
    [DataRow("loading-error-retry-recovered/v1")]
    [DataRow("organization-splitter/v1")]
    [DataRow("inspector-splitter/v1")]
    [DataRow("pane-collapse-expand/v1")]
    [DataRow("thumbnail-slider/v1")]
    [DataRow("selection-navigation-restart/v1")]
    [DataRow("navigation-ime/v1")]
    [DataRow("layout-dpi-buttons/v1")]
    public void IndependentValidatorRejectsEveryScenarioFaultWithoutChangingInputTree(string scenarioId)
    {
        using var fixture = new EvidenceFixture();
        fixture.InjectScenarioFailure(scenarioId);
        var before = fixture.TreeFingerprint();

        var result = RunValidator(fixture.Root);

        Assert.AreNotEqual(0, result.ExitCode, $"Scenario fault '{scenarioId}' was accepted.\n{result.Output}");
        Assert.AreEqual(before, fixture.TreeFingerprint(), $"Validation of scenario fault '{scenarioId}' changed its input tree.");
    }

    [TestMethod]
    public void IndependentValidatorRejectsFabricatedLiveButtonProbeWithoutChangingInputTree()
    {
        using var fixture = new EvidenceFixture();
        fixture.InjectButtonProbeFailure();
        var before = fixture.TreeFingerprint();

        var result = RunValidator(fixture.Root);

        Assert.AreNotEqual(0, result.ExitCode, result.Output);
        Assert.AreEqual(before, fixture.TreeFingerprint(), "Validation of a fabricated WPF button probe changed its input tree.");
    }

    [TestMethod]
    public void IndependentValidatorRejectsLegacyCamelCaseButtonRowWithoutChangingInputTree()
    {
        using var fixture = new EvidenceFixture();
        fixture.InjectLegacyCamelCaseButtonRow();
        var before = fixture.TreeFingerprint();

        var result = RunValidator(fixture.Root);

        Assert.AreNotEqual(0, result.ExitCode, result.Output);
        Assert.AreEqual(before, fixture.TreeFingerprint(), "Validation of a legacy camel-case button row changed its input tree.");
    }

    [TestMethod]
    public void IndependentValidatorRejectsScrollDecorationInMustFitBoundsWithoutChangingInputTree()
    {
        using var fixture = new EvidenceFixture();
        fixture.InjectBoundsDecorationFailure();
        var before = fixture.TreeFingerprint();

        var result = RunValidator(fixture.Root);

        Assert.AreNotEqual(0, result.ExitCode, result.Output);
        Assert.AreEqual(before, fixture.TreeFingerprint(), "Validation of a non-critical bounds decoration changed its input tree.");
    }

    private sealed class EvidenceFixture : IDisposable
    {
        private const string RunId = "p1-auto-0123456789abcdef0123456789abcdef";
        private static readonly string FixtureVersionedBinary = typeof(AssetLibraryP1AutomatedEvidenceContractTests).Assembly.Location;
        private static readonly string FixtureProductVersion = FileVersionInfo.GetVersionInfo(FixtureVersionedBinary).ProductVersion
            ?? throw new InvalidOperationException("The evidence fixture assembly has no ProductVersion.");
        private static readonly string Head = ProductVersionHead(FixtureProductVersion);
        private static readonly byte[] Png = Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mNk+A8AAQUBAScY42YAAAAASUVORK5CYII=");
        private readonly string _summaryPath;
        private readonly string _eventsPath;
        private readonly string _summaryJournalPath;
        private readonly string _manifestPath;
        private readonly string _fixtureBuildDirectory;
        private readonly string _mutableExePath;
        private readonly string _mutableApplicationPath;
        private readonly string _mutableDllPath;
        private readonly string _exeHash;
        private readonly string _applicationHash;
        private readonly string _dllHash;
        private readonly string _binaryTreeHash;

        public EvidenceFixture()
        {
            Root = Path.Combine(AssetLibraryP1AutomatedEvidenceContractTests.Root(), ".validation", "PixelTart-P1-Automated-Contract-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Root);
            var repositoryRoot = AssetLibraryP1AutomatedEvidenceContractTests.Root();
            var buildDirectory = Path.Combine(repositoryRoot, "src", "RAWSelectionAssistant", "bin", "Debug", "AutomatedEvidenceContractFixture-" + Guid.NewGuid().ToString("N"));
            _fixtureBuildDirectory = buildDirectory;
            Directory.CreateDirectory(buildDirectory);
            _mutableExePath = Path.Combine(buildDirectory, "PixelTart_ModularHarness_V1_DevPreview.exe");
            _mutableApplicationPath = Path.Combine(buildDirectory, "PixelTart_ModularHarness_V1_DevPreview.dll");
            _mutableDllPath = Path.Combine(buildDirectory, "PixelTart.Modules.AssetLibrary.dll");
            var mutableDepsPath = Path.Combine(buildDirectory, "PixelTart_ModularHarness_V1_DevPreview.deps.json");
            File.Copy(FixtureVersionedBinary, _mutableExePath);
            File.Copy(FixtureVersionedBinary, _mutableApplicationPath);
            File.Copy(FixtureVersionedBinary, _mutableDllPath);
            File.WriteAllBytes(mutableDepsPath, Encoding.UTF8.GetBytes("{}"));
            var binaryDirectory = Path.Combine(Root, "binaries");
            Directory.CreateDirectory(binaryDirectory);
            var exePath = Path.Combine(binaryDirectory, Path.GetFileName(_mutableExePath));
            var applicationPath = Path.Combine(binaryDirectory, Path.GetFileName(_mutableApplicationPath));
            var dllPath = Path.Combine(binaryDirectory, Path.GetFileName(_mutableDllPath));
            var depsPath = Path.Combine(binaryDirectory, Path.GetFileName(mutableDepsPath));
            File.Copy(_mutableExePath, exePath);
            File.Copy(_mutableApplicationPath, applicationPath);
            File.Copy(_mutableDllPath, dllPath);
            File.Copy(mutableDepsPath, depsPath);
            _exeHash = Sha(exePath);
            _applicationHash = Sha(applicationPath);
            _dllHash = Sha(dllPath);
            _binaryTreeHash = BinaryTreeHash(binaryDirectory);

            var markers = Markers();
            var build = Clone(markers);
            build["schema_version"] = "pixel-tart-p1-automated-build/v2";
            build["run_id"] = RunId;
            build["source_head"] = Head;
            build["configuration"] = "Debug";
            build["repository_clean"] = true;
            build["executable_path"] = exePath;
            build["executable_sha256"] = _exeHash;
            build["application_path"] = applicationPath;
            build["application_sha256"] = _applicationHash;
            build["asset_module_path"] = dllPath;
            build["asset_module_sha256"] = _dllHash;
            build["build_source_executable_path"] = _mutableExePath;
            build["build_source_executable_sha256"] = _exeHash;
            build["build_source_application_path"] = _mutableApplicationPath;
            build["build_source_application_sha256"] = _applicationHash;
            build["build_source_asset_module_path"] = _mutableDllPath;
            build["build_source_asset_module_sha256"] = _dllHash;
            build["binary_snapshot"] = BinarySnapshot(buildDirectory, binaryDirectory);
            build["executable_product_version"] = FixtureProductVersion;
            build["application_product_version"] = FixtureProductVersion;
            build["asset_module_product_version"] = FixtureProductVersion;
            build["restore"] = BuildProcessResult("fixture-restore", new[] { "restore", "RAWSelectionAssistant.sln", "-nodeReuse:false", "-p:UseSharedCompilation=false" });
            build["build"] = BuildProcessResult("fixture-build", new[] { "build", "src/RAWSelectionAssistant/RAWSelectionAssistant.csproj", "-c", "Debug", "--no-restore", "-t:Rebuild", "-nodeReuse:false", "-p:UseSharedCompilation=false", "-p:TreatWarningsAsErrors=true", "-p:ModularHarnessDevPreview=true", "-p:InputRoutingDiagnostics=true", "-p:AssetLibraryP1AutomatedAcceptance=true", "-p:ContinuousIntegrationBuild=true", $"-p:SourceRevisionId={Head}", $"-p:BaseOutputPath={buildDirectory}\\" });
            build["source_audit"] = SourceAudit(repositoryRoot);
            WriteJson(Path.Combine(Root, "build-manifest.json"), build);

            var summary = Clone(markers);
            summary["schema_version"] = "pixel-tart-p1-automated-summary/v2";
            summary["status"] = "completed";
            summary["run_id"] = RunId;
            summary["source_head"] = Head;
            summary["executable_path"] = exePath;
            summary["executable_sha256"] = _exeHash;
            summary["application_path"] = applicationPath;
            summary["application_sha256"] = _applicationHash;
            summary["asset_module_path"] = dllPath;
            summary["asset_module_sha256"] = _dllHash;
            summary["executable"] = BinaryIdentity(exePath, _exeHash);
            summary["application"] = BinaryIdentity(applicationPath, _applicationHash);
            summary["module"] = BinaryIdentity(dllPath, _dllHash);
            summary["scenario_ids"] = new JsonArray(ScenarioIds.Select(item => JsonValue.Create(item)).ToArray());
            summary["scenarios"] = new JsonArray();
            summary["artifacts"] = new JsonArray();
            summary["safety"] = Safety();
            summary["process_cleanup"] = AppCleanup();

            var events = new List<JsonObject>();
            var eventSequence = 0;
            for (var index = 0; index < ScenarioIds.Length; index++)
                AddScenario(summary, events, ref eventSequence, index);

            var evidenceDirectory = Path.Combine(Root, "app", "evidence");
            Directory.CreateDirectory(evidenceDirectory);
            _summaryPath = Path.Combine(evidenceDirectory, "summary.json");
            _eventsPath = Path.Combine(evidenceDirectory, "events.ndjson");
            _summaryJournalPath = Path.Combine(evidenceDirectory, "summary.ndjson");
            WriteJson(_summaryPath, summary);
            var terminalEventHash = WriteEventChain(events);
            summary["event_journal"] = new JsonObject
            {
                ["path"] = "app/evidence/events.ndjson",
                ["event_count"] = eventSequence,
                ["last_event_hash"] = terminalEventHash,
                ["append_only"] = true
            };
            WriteJson(_summaryPath, summary);
            WriteSummaryChain(summary);

            var manifest = Clone(markers);
            manifest["schema_version"] = "pixel-tart-p1-automated-run/v2";
            manifest["run_id"] = RunId;
            manifest["run_root"] = Root;
            manifest["branch"] = "feature/modular-harness-v1-p1";
            manifest["source_head"] = Head;
            manifest["repository_root"] = repositoryRoot;
            manifest["sessions"] = BuildRunnerSessions(summary);
            var preCleanup = BuildPreCleanupAudit(summary);
            manifest["pre_cleanup_database_audit"] = preCleanup.Pointer;
            manifest["safety"] = Safety();
            manifest["process_cleanup"] = Cleanup(preCleanup.RemovedPaths);
            _manifestPath = Path.Combine(Root, "run-manifest.json");
            WriteJson(_manifestPath, manifest);
        }

        public string Root { get; }

        public void OverwriteMutableBuildOutputs()
        {
            File.WriteAllText(_mutableExePath, "later-build-executable", Encoding.UTF8);
            File.WriteAllText(_mutableApplicationPath, "later-build-application", Encoding.UTF8);
            File.WriteAllText(_mutableDllPath, "later-build-module", Encoding.UTF8);
        }

        public void Inject(string fault)
        {
            var summary = ReadJson(_summaryPath);
            var scenarios = summary["scenarios"]!.AsArray();
            var artifacts = summary["artifacts"]!.AsArray();
            var events = File.ReadAllLines(_eventsPath).Select(line => JsonNode.Parse(line)!.AsObject()).ToList();

            switch (fault)
            {
                case "missing-screenshot":
                    File.Delete(Absolute(scenarios[0]!["screenshot_paths"]![0]!.GetValue<string>()));
                    break;
                case "mutated-hash":
                    File.AppendAllText(Absolute(scenarios[8]!["screenshot_paths"]![0]!.GetValue<string>()), "tampered", Encoding.UTF8);
                    break;
                case "wrong-scenario-order":
                    var seventh = summary["scenario_ids"]![7]!.GetValue<string>();
                    var eighth = summary["scenario_ids"]![8]!.GetValue<string>();
                    summary["scenario_ids"]![7] = eighth;
                    summary["scenario_ids"]![8] = seventh;
                    WriteJson(_summaryPath, summary);
                    break;
                case "retry-twice":
                    scenarios[1]!["retry_command_count"] = 2;
                    var retry = Clone(events.Single(item => item["event_type"]!.GetValue<string>() == "retry-command"));
                    retry["event_sequence"] = events.Max(item => item["event_sequence"]!.GetValue<int>()) + 1;
                    events.Add(retry);
                    WriteJson(_summaryPath, summary);
                    WriteEvents(events);
                    break;
                case "direct-width-mutation":
                    events.First(item => item["scenario_id"]!.GetValue<string>() == "organization-splitter/v1")["direct_mutation"] = true;
                    WriteEvents(events);
                    break;
                case "direct-settings-mutation":
                    summary["safety"]!["direct_settings_mutation_count"] = 1;
                    WriteJson(_summaryPath, summary);
                    break;
                case "cross-run-splice":
                    MutateBounds(summary, artifacts, 4, bounds => bounds["run_id"] = "p1-auto-ffffffffffffffffffffffffffffffff");
                    break;
                case "wrong-pid-or-hwnd":
                    var inspectorEvent = events.First(item => item["scenario_id"]!.GetValue<string>() == "inspector-splitter/v1");
                    inspectorEvent["pid"] = 999999;
                    inspectorEvent["hwnd"] = "0xBAD";
                    WriteEvents(events);
                    break;
                case "dll-hash-mismatch":
                    events.First(item => item["scenario_id"]!.GetValue<string>() == "selection-navigation-restart/v1")["asset_module_sha256"] = new string('f', 64);
                    WriteEvents(events);
                    break;
                case "database-not-v6":
                    scenarios[0]!["database"]!["schema_version"] = 5;
                    WriteJson(_summaryPath, summary);
                    break;
                case "import-contamination":
                    scenarios[4]!["import_events"]!.AsArray().Add(new JsonObject { ["source_kind"] = "user", ["user_source"] = true });
                    WriteJson(_summaryPath, summary);
                    break;
                case "uncleared-process":
                    var manifest = ReadJson(_manifestPath);
                    manifest["process_cleanup"]!["devpreview_get_process_count_after"] = 1;
                    WriteJson(_manifestPath, manifest);
                    break;
                case "dpi-overflow":
                    MutateBounds(summary, artifacts, 8, bounds => bounds["has_overflow"] = true);
                    break;
                case "runner-session-splice":
                    var sessionManifest = ReadJson(_manifestPath);
                    sessionManifest["sessions"]![0]!["pid"] = 999999;
                    WriteJson(_manifestPath, sessionManifest);
                    break;
                case "process-session-splice":
                    var processSessionManifest = ReadJson(_manifestPath);
                    processSessionManifest["sessions"]![0]!["process_session_id"] = new string('f', 32);
                    WriteJson(_manifestPath, processSessionManifest);
                    break;
                case "pre-cleanup-audit-hash":
                    var auditManifest = ReadJson(_manifestPath);
                    auditManifest["pre_cleanup_database_audit"]!["sha256"] = new string('0', 64);
                    WriteJson(_manifestPath, auditManifest);
                    break;
                case "cleanup-path-splice":
                    var cleanupManifest = ReadJson(_manifestPath);
                    cleanupManifest["process_cleanup"]!["runtime_database_cleanup"]!["removed_paths"]![0] = _summaryPath;
                    WriteJson(_manifestPath, cleanupManifest);
                    break;
                case "build-log-hash":
                    var buildManifest = ReadJson(Path.Combine(Root, "build-manifest.json"));
                    File.AppendAllText(buildManifest["build"]!["stdout"]!.GetValue<string>(), "tampered", Encoding.UTF8);
                    break;
                case "sealed-binary-mutation":
                    var sealedBuildManifest = ReadJson(Path.Combine(Root, "build-manifest.json"));
                    File.AppendAllText(sealedBuildManifest["executable_path"]!.GetValue<string>(), "tampered", Encoding.UTF8);
                    break;
                case "application-hash-mismatch":
                    events.First(item => item["scenario_id"]!.GetValue<string>() == "selection-navigation-restart/v1")["application_sha256"] = new string('e', 64);
                    WriteEvents(events);
                    break;
                case "sealed-application-mutation":
                    var sealedApplicationManifest = ReadJson(Path.Combine(Root, "build-manifest.json"));
                    File.AppendAllText(sealedApplicationManifest["application_path"]!.GetValue<string>(), "tampered", Encoding.UTF8);
                    break;
                case "sealed-dependency-mutation":
                    var sealedDependencyManifest = ReadJson(Path.Combine(Root, "build-manifest.json"));
                    var dependencyRow = sealedDependencyManifest["binary_snapshot"]!["files"]!.AsArray()
                        .Single(item => item!["path"]!.GetValue<string>().EndsWith(".deps.json", StringComparison.Ordinal));
                    File.AppendAllText(Path.Combine(Root, "binaries", dependencyRow!["path"]!.GetValue<string>()), "tampered", Encoding.UTF8);
                    break;
                case "binary-tree-manifest-mismatch":
                    var treeManifest = ReadJson(Path.Combine(Root, "build-manifest.json"));
                    treeManifest["binary_snapshot"]!["tree_sha256"] = new string('f', 64);
                    WriteJson(Path.Combine(Root, "build-manifest.json"), treeManifest);
                    break;
                case "manifest-product-version-forgery":
                    var forgedVersionManifest = ReadJson(Path.Combine(Root, "build-manifest.json"));
                    forgedVersionManifest["executable_product_version"] = "9.9.9+" + Head;
                    WriteJson(Path.Combine(Root, "build-manifest.json"), forgedVersionManifest);
                    break;
                case "actual-product-version-mismatch":
                    var actualVersionManifest = ReadJson(Path.Combine(Root, "build-manifest.json"));
                    File.WriteAllText(actualVersionManifest["executable_path"]!.GetValue<string>(), "not-a-versioned-binary", Encoding.UTF8);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(fault), fault, null);
            }
        }

        private static string ProductVersionHead(string productVersion)
        {
            var separator = productVersion.LastIndexOf('+');
            if (separator <= 0 || separator == productVersion.Length - 1)
                throw new InvalidOperationException($"The evidence fixture ProductVersion '{productVersion}' has no source revision suffix.");
            var head = productVersion[(separator + 1)..];
            if (head.Length != 40 || head.Any(character => !Uri.IsHexDigit(character)))
                throw new InvalidOperationException($"The evidence fixture ProductVersion '{productVersion}' has an invalid source revision suffix.");
            return head.ToLowerInvariant();
        }

        public void InjectScenarioFailure(string scenarioId)
        {
            var summary = ReadJson(_summaryPath);
            var scenario = summary["scenarios"]!.AsArray()
                .Single(item => item!["id"]!.GetValue<string>() == scenarioId)!;
            var events = File.ReadAllLines(_eventsPath).Select(line => JsonNode.Parse(line)!.AsObject()).ToList();
            switch (scenarioId)
            {
                case "first-empty/v1":
                    scenario["checks"]!["asset_count"] = 1;
                    break;
                case "loading-error-retry-recovered/v1":
                    scenario["checks"]!["attempt2_repository_query"] = false;
                    break;
                case "organization-splitter/v1":
                    events.Remove(events.Single(item => item["scenario_id"]!.GetValue<string>() == scenarioId && item["event_type"]!.GetValue<string>() == "splitter-minimum"));
                    WriteEvents(events);
                    return;
                case "inspector-splitter/v1":
                    events.Remove(events.Single(item => item["scenario_id"]!.GetValue<string>() == scenarioId && item["event_type"]!.GetValue<string>() == "splitter-maximum"));
                    WriteEvents(events);
                    return;
                case "pane-collapse-expand/v1":
                    scenario["checks"]!["same_pane_state_after_restart"] = false;
                    break;
                case "thumbnail-slider/v1":
                    scenario["checks"]!["same_thumbnail_width_after_restart"] = false;
                    break;
                case "selection-navigation-restart/v1":
                    scenario["checks"]!["same_selection_after_route_return"] = false;
                    break;
                case "navigation-ime/v1":
                    scenario["checks"]!["routes"]!.AsArray().RemoveAt(6);
                    break;
                case "layout-dpi-buttons/v1":
                    MutateBounds(summary, summary["artifacts"]!.AsArray(), 8, bounds => bounds["has_overflow"] = true);
                    WriteSummaryChain(summary);
                    return;
                default:
                    throw new ArgumentOutOfRangeException(nameof(scenarioId), scenarioId, null);
            }
            WriteJson(_summaryPath, summary);
            WriteSummaryChain(summary);
        }

        public void InjectButtonProbeFailure()
        {
            var summary = ReadJson(_summaryPath);
            var layout = summary["scenarios"]!.AsArray().Single(item => item!["id"]!.GetValue<string>() == "layout-dpi-buttons/v1")!["checks"]!;
            layout["button_state_matrix"]![0]!["source_declaration_probe"] = false;
            WriteJson(_summaryPath, summary);
            WriteSummaryChain(summary);
        }

        public void InjectLegacyCamelCaseButtonRow()
        {
            var summary = ReadJson(_summaryPath);
            var layout = summary["scenarios"]!.AsArray().Single(item => item!["id"]!.GetValue<string>() == "layout-dpi-buttons/v1")!["checks"]!;
            var row = layout["button_state_matrix"]![0]!.AsObject();
            row["buttonIdentity"] = row["button_identity"]!.DeepClone();
            row.Remove("button_identity");
            WriteJson(_summaryPath, summary);
            WriteSummaryChain(summary);
        }

        public void InjectBoundsDecorationFailure()
        {
            var summary = ReadJson(_summaryPath);
            MutateBounds(summary, summary["artifacts"]!.AsArray(), 8, bounds =>
            {
                var decoration = Element("AssetInspectorScrollDecoration", "Border", 20, 20, 20, 20);
                decoration["must_fit"] = false;
                bounds["elements"]!.AsArray().Add(decoration);
            });
            WriteSummaryChain(summary);
        }

        public string TreeFingerprint()
        {
            var entries = Directory.EnumerateFiles(Root, "*", SearchOption.AllDirectories)
                .OrderBy(path => Path.GetRelativePath(Root, path), StringComparer.Ordinal)
                .Select(path => Path.GetRelativePath(Root, path).Replace('\\', '/') + ":" + Sha(path));
            return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(string.Join("\n", entries))));
        }

        public void Dispose()
        {
            try { Directory.Delete(Root, true); } catch { }
            try { Directory.Delete(_fixtureBuildDirectory, true); } catch { }
        }

        private void AddScenario(JsonObject summary, List<JsonObject> events, ref int eventSequence, int index)
        {
            var id = ScenarioIds[index];
            var number = index + 1;
            var pid = 4100 + number;
            var hwnd = $"0x{0xA000 + number:X}";
            var scenarioRoot = Path.Combine(Root, "runtime", id.Replace("/v1", string.Empty, StringComparison.Ordinal).Replace('/', '-'));
            Directory.CreateDirectory(scenarioRoot);

            var screenshot = Path.Combine(scenarioRoot, "capture.png");
            File.WriteAllBytes(screenshot, Png);
            var screenshotPaths = new List<string> { Relative(screenshot) };
            var boundsPaths = new List<string>();
            var boundsCount = id == "layout-dpi-buttons/v1" ? 4 : 1;
            for (var matrixIndex = 0; matrixIndex < boundsCount; matrixIndex++)
            {
                var boundsPath = Path.Combine(scenarioRoot, $"bounds-{matrixIndex + 1}.json");
                WriteJson(boundsPath, Bounds(id, pid, hwnd, scenarioRoot, "primary", matrixIndex));
                boundsPaths.Add(Relative(boundsPath));
            }

            var hasRestart = id is "pane-collapse-expand/v1" or "thumbnail-slider/v1" or "selection-navigation-restart/v1";
            var databaseEvidenceDirectory = Path.Combine(Root, "app", "evidence", "databases");
            Directory.CreateDirectory(databaseEvidenceDirectory);
            var activeDatabasePath = Path.Combine(scenarioRoot, "app-data", "Data", "asset-library-v16.db");
            Directory.CreateDirectory(Path.GetDirectoryName(activeDatabasePath)!);
            CreateDatabase(activeDatabasePath, id == "selection-navigation-restart/v1" ? 1 : 0);
            var primaryDatabasePath = Path.Combine(databaseEvidenceDirectory, id.Replace('/', '-') + "-primary.db");
            CreateDatabase(primaryDatabasePath, id == "selection-navigation-restart/v1" ? 1 : 0);
            var databasePath = primaryDatabasePath;
            if (hasRestart)
            {
                databasePath = Path.Combine(databaseEvidenceDirectory, id.Replace('/', '-') + "-restart.db");
                File.Copy(primaryDatabasePath, databasePath);
                var restartScreenshot = Path.Combine(scenarioRoot, "capture-restart.png");
                File.WriteAllBytes(restartScreenshot, Png);
                screenshotPaths.Add(Relative(restartScreenshot));
                var restartBounds = Path.Combine(scenarioRoot, "bounds-restart.json");
                WriteJson(restartBounds, Bounds(id, 9900 + number, $"0x{0xF000 + number:X}", scenarioRoot, "restart", 0));
                boundsPaths.Add(Relative(restartBounds));
            }
            var evidencePaths = new JsonArray(Relative(primaryDatabasePath));
            if (hasRestart) evidencePaths.Add(Relative(databasePath));
            var database = Clone(Markers());
            database["path"] = Relative(databasePath);
            database["active_database_path"] = Relative(activeDatabasePath);
            database["active_database_absolute_path"] = activeDatabasePath;
            database["evidence_paths"] = evidencePaths;
            database["sha256"] = Sha(databasePath);
            database["repository_implementation"] = "SqliteAssetLibraryRepository";
            database["real_repository"] = true;
            database["schema_version"] = 6;
            database["schema_query"] = "SELECT MAX(Version) FROM AssetLibrarySchemaInfo;";
            database["asset_count"] = id == "selection-navigation-restart/v1" ? 1 : 0;
            database["wal_present_after_close"] = false;
            database["shm_present_after_close"] = false;
            var scenario = Clone(Markers());
            foreach (var property in new JsonObject
            {
                ["id"] = id,
                ["sequence"] = number,
                ["status"] = "passed",
                ["pid"] = pid,
                ["hwnd"] = hwnd,
                ["primary_process_session_id"] = SessionId(id, "primary"),
                ["restart_pid"] = id is "pane-collapse-expand/v1" or "thumbnail-slider/v1" or "selection-navigation-restart/v1" ? 9900 + number : 0,
                ["restart_hwnd"] = id is "pane-collapse-expand/v1" or "thumbnail-slider/v1" or "selection-navigation-restart/v1" ? $"0x{0xF000 + number:X}" : "0x0",
                ["restart_process_session_id"] = id is "pane-collapse-expand/v1" or "thumbnail-slider/v1" or "selection-navigation-restart/v1" ? SessionId(id, "restart") : string.Empty,
                ["scenario_root"] = scenarioRoot,
                ["retry_command_count"] = id == "loading-error-retry-recovered/v1" ? 1 : 0,
                ["file_picker_count"] = 0,
                ["screenshot_paths"] = new JsonArray(screenshotPaths.Select(item => JsonValue.Create(item)).ToArray()),
                ["bounds_paths"] = new JsonArray(boundsPaths.Select(item => JsonValue.Create(item)).ToArray()),
                ["database"] = database,
                ["import_events"] = new JsonArray(),
                ["checks"] = Checks(id)
            }) scenario[property.Key] = property.Value?.DeepClone();
            if (id == "selection-navigation-restart/v1")
            {
                var fixturePath = Path.Combine(Root, "synthetic-fixture", "fixture.png");
                Directory.CreateDirectory(Path.GetDirectoryName(fixturePath)!);
                File.WriteAllBytes(fixturePath, Png);
                scenario["import_events"]!.AsArray().Add(new JsonObject
                {
                    ["source_kind"] = "synthetic-run-fixture",
                    ["synthetic"] = true,
                    ["application_import_route"] = true,
                    ["user_source"] = false,
                    ["source_path"] = fixturePath
                });
            }
            summary["scenarios"]!.AsArray().Add(scenario);

            AddArtifact(summary, id, pid, hwnd, scenarioRoot, screenshot, "screenshot");
            foreach (var boundsPath in boundsPaths.Where(path => !path.EndsWith("bounds-restart.json", StringComparison.Ordinal))) AddArtifact(summary, id, pid, hwnd, scenarioRoot, Absolute(boundsPath), "bounds");
            if (hasRestart)
            {
                AddArtifact(summary, id, 9900 + number, $"0x{0xF000 + number:X}", scenarioRoot, Absolute(screenshotPaths[^1]), "screenshot", "restart");
                AddArtifact(summary, id, 9900 + number, $"0x{0xF000 + number:X}", scenarioRoot, Absolute(boundsPaths[^1]), "bounds", "restart");
            }
            AddArtifact(summary, id, pid, hwnd, scenarioRoot, primaryDatabasePath, "database", "primary");
            if (hasRestart) AddArtifact(summary, id, 9900 + number, $"0x{0xF000 + number:X}", scenarioRoot, databasePath, "database", "restart");

            AddEvent(events, ref eventSequence, id, pid, hwnd, scenarioRoot, "scenario-observed", "AssetLibraryPage", "ScenarioAcceptanceRoute", "primary");
            if (id is "organization-splitter/v1" or "inspector-splitter/v1")
                foreach (var action in new[] { "minimum", "maximum", "middle", "boundary-no-op", "decrease", "increase" })
                    AddEvent(events, ref eventSequence, id, pid, hwnd, scenarioRoot, "splitter-" + action, id.StartsWith("organization", StringComparison.Ordinal) ? "AssetOrganizationSplitter" : "AssetInspectorSplitter", "GridSplitterKeyboardRoute", "primary");
            if (id == "loading-error-retry-recovered/v1")
                AddEvent(events, ref eventSequence, id, pid, hwnd, scenarioRoot, "retry-command", "RetryAssetLibraryLoad", "RetryLoadCommand", "primary");
            if (id == "selection-navigation-restart/v1")
                AddEvent(events, ref eventSequence, id, 9900 + number, $"0x{0xF000 + number:X}", scenarioRoot, "selection-restored-after-restart", "AssetGrid", "SelectionRestoreRoute", "restart");
            else if (id is "pane-collapse-expand/v1" or "thumbnail-slider/v1")
                AddEvent(events, ref eventSequence, id, 9900 + number, $"0x{0xF000 + number:X}", scenarioRoot, "settings-restored-after-restart", id == "pane-collapse-expand/v1" ? "ToggleAssetOrganizationPane" : "AssetThumbnailSizeSlider", "PersistedSettingsRestoreRoute", "restart");
        }

        private (JsonObject Pointer, string[] RemovedPaths) BuildPreCleanupAudit(JsonObject summary)
        {
            var rows = new JsonArray();
            var removedPaths = new List<string>();
            foreach (var scenarioNode in summary["scenarios"]!.AsArray())
            {
                var scenario = scenarioNode!.AsObject();
                var database = scenario["database"]!.AsObject();
                var activePath = database["active_database_absolute_path"]!.GetValue<string>();
                var evidencePath = Absolute(database["path"]!.GetValue<string>());
                var assetCount = database["asset_count"]!.GetValue<int>();
                JsonObject Inspection(string path) => new()
                {
                    ["path"] = Path.GetFullPath(path), ["sha256"] = Sha(path), ["quick_check"] = "ok",
                    ["schema_version"] = 6, ["asset_count"] = assetCount
                };
                rows.Add(new JsonObject
                {
                    ["scenario_id"] = scenario["id"]!.GetValue<string>(),
                    ["scenario_root"] = scenario["scenario_root"]!.GetValue<string>(),
                    ["status"] = "matched",
                    ["expected_asset_count"] = assetCount,
                    ["active"] = Inspection(activePath),
                    ["evidence"] = Inspection(evidencePath)
                });
                removedPaths.Add(activePath);
            }

            var audit = Clone(Markers());
            audit["schema"] = "pixel-tart-p1-pre-cleanup-database-audit/v1";
            audit["run_id"] = RunId;
            audit["source_head"] = Head;
            audit["status"] = "passed";
            audit["scenario_count"] = rows.Count;
            audit["scenarios"] = rows;
            var auditPath = Path.Combine(Root, "runner", "database-consistency-audit.json");
            WriteJson(auditPath, audit);
            var pointer = new JsonObject
            {
                ["path"] = auditPath,
                ["sha256"] = Sha(auditPath),
                ["scenario_count"] = rows.Count,
                ["result"] = BuildProcessResult("fixture-pre-cleanup-database-audit", new[] { "-I", "-c" })
            };
            foreach (var activePath in removedPaths) File.Delete(activePath);
            return (pointer, removedPaths.ToArray());
        }

        private JsonObject BuildProcessResult(string name, IReadOnlyList<string> arguments)
        {
            var logsDirectory = Path.Combine(Root, "logs");
            Directory.CreateDirectory(logsDirectory);
            var stdout = Path.Combine(logsDirectory, name + ".stdout.log");
            var stderr = Path.Combine(logsDirectory, name + ".stderr.log");
            File.WriteAllText(stdout, name + " passed", new UTF8Encoding(false));
            File.WriteAllText(stderr, string.Empty, new UTF8Encoding(false));
            return new JsonObject
            {
                ["name"] = name,
                ["file"] = Environment.ProcessPath!,
                ["arguments"] = new JsonArray(arguments.Select(value => JsonValue.Create(value)).ToArray()),
                ["started_at"] = DateTimeOffset.UtcNow.AddSeconds(-1).ToString("O"),
                ["finished_at"] = DateTimeOffset.UtcNow.ToString("O"),
                ["duration_ms"] = 1000,
                ["exit_code"] = 0,
                ["stdout"] = stdout,
                ["stderr"] = stderr,
                ["stdout_sha256"] = Sha(stdout),
                ["stderr_sha256"] = Sha(stderr)
            };
        }

        private JsonArray BuildRunnerSessions(JsonObject summary)
        {
            var scenarios = summary["scenarios"]!.AsArray()
                .ToDictionary(item => item!["id"]!.GetValue<string>(), item => item!, StringComparer.Ordinal);
            var ordered = ScenarioIds
                .Select(id => (Id: id, Phase: "primary"))
                .Concat(new[]
                {
                    (Id: "pane-collapse-expand/v1", Phase: "restart"),
                    (Id: "thumbnail-slider/v1", Phase: "restart"),
                    (Id: "selection-navigation-restart/v1", Phase: "restart")
                })
                .ToArray();
            var sessions = new JsonArray();
            var plansDirectory = Path.Combine(Root, "plans");
            var logsDirectory = Path.Combine(Root, "logs");
            Directory.CreateDirectory(plansDirectory);
            Directory.CreateDirectory(logsDirectory);

            for (var index = 0; index < ordered.Length; index++)
            {
                var (id, phase) = ordered[index];
                var scenario = scenarios[id];
                var scenarioRoot = scenario["scenario_root"]!.GetValue<string>();
                var runtimeRoot = Path.Combine(scenarioRoot, "app-data");
                Directory.CreateDirectory(runtimeRoot);
                var sessionName = $"{index + 1:00}-{id.Replace("/v1", string.Empty, StringComparison.Ordinal).Replace('/', '-')}{(phase == "restart" ? "-restart" : string.Empty)}";
                var planPath = Path.Combine(plansDirectory, sessionName + ".json");
                WriteJson(planPath, new JsonObject
                {
                    ["schema_version"] = "pixel-tart-p1-automated-plan/v2",
                    ["validation_mode"] = "automated",
                    ["owner_manual_ux_smoke"] = "waived",
                    ["manual_evidence_claimed"] = false,
                    ["run_id"] = RunId,
                    ["phase"] = phase,
                    ["source_head"] = Head,
                    ["executable_path"] = summary["executable_path"]!.GetValue<string>(),
                    ["executable_sha256"] = _exeHash,
                    ["application_path"] = summary["application_path"]!.GetValue<string>(),
                    ["application_sha256"] = _applicationHash,
                    ["asset_module_path"] = summary["asset_module_path"]!.GetValue<string>(),
                    ["asset_module_sha256"] = _dllHash,
                    ["binary_snapshot_directory"] = Path.Combine(Root, "binaries"),
                    ["binary_snapshot_tree_sha256"] = _binaryTreeHash,
                    ["scenario_ids"] = new JsonArray(id),
                    ["scenario_root"] = scenarioRoot,
                    ["fixture_root"] = id == "selection-navigation-restart/v1" ? Path.Combine(Root, "synthetic-fixture") : null
                });
                var stdout = Path.Combine(logsDirectory, "app-" + sessionName + ".stdout.log");
                var stderr = Path.Combine(logsDirectory, "app-" + sessionName + ".stderr.log");
                File.WriteAllText(stdout, string.Empty, Encoding.UTF8);
                File.WriteAllText(stderr, string.Empty, Encoding.UTF8);
                var processSessionId = SessionId(id, phase);
                var phaseSummaryPath = Path.Combine(Root, "app", "evidence", $"summary-{id.Replace('/', '-')}-{phase}.json");
                var phaseSummary = Clone(summary);
                phaseSummary["phase"] = phase;
                phaseSummary["process_session_id"] = processSessionId;
                WriteJson(phaseSummaryPath, phaseSummary);
                sessions.Add(new JsonObject
                {
                    ["phase"] = phase,
                    ["session_name"] = sessionName,
                    ["scenario_id"] = id,
                    ["pid"] = phase == "primary" ? scenario["pid"]!.GetValue<int>() : scenario["restart_pid"]!.GetValue<int>(),
                    ["hwnd"] = phase == "primary" ? scenario["hwnd"]!.GetValue<string>() : scenario["restart_hwnd"]!.GetValue<string>(),
                    ["process_session_id"] = processSessionId,
                    ["exit_code"] = 0,
                    ["run_id"] = RunId,
                    ["source_head"] = Head,
                    ["started_at"] = DateTimeOffset.UtcNow.AddSeconds(index).ToString("O"),
                    ["finished_at"] = DateTimeOffset.UtcNow.AddSeconds(index + 1).ToString("O"),
                    ["duration_ms"] = 1000,
                    ["runtime_root"] = runtimeRoot,
                    ["scenario_root"] = scenarioRoot,
                    ["plan_path"] = planPath,
                    ["phase_summary_path"] = phaseSummaryPath,
                    ["executable_path"] = summary["executable_path"]!.GetValue<string>(),
                    ["executable_sha256"] = _exeHash,
                    ["application_path"] = summary["application_path"]!.GetValue<string>(),
                    ["application_sha256"] = _applicationHash,
                    ["asset_module_path"] = summary["asset_module_path"]!.GetValue<string>(),
                    ["asset_module_sha256"] = _dllHash,
                    ["binary_snapshot_tree_sha256_before"] = _binaryTreeHash,
                    ["binary_snapshot_tree_sha256_after"] = _binaryTreeHash,
                    ["stdout"] = stdout,
                    ["stderr"] = stderr,
                    ["stdout_sha256"] = Sha(stdout),
                    ["stderr_sha256"] = Sha(stderr)
                });
            }
            return sessions;
        }

        private void AddArtifact(JsonObject summary, string id, int pid, string hwnd, string scenarioRoot, string path, string kind, string phase = "primary")
        {
            var artifact = Clone(Markers());
            artifact["run_id"] = RunId;
            artifact["source_head"] = Head;
            artifact["executable_path"] = Path.Combine(Root, "binaries", "PixelTart_ModularHarness_V1_DevPreview.exe");
            artifact["executable_sha256"] = _exeHash;
            artifact["application_path"] = Path.Combine(Root, "binaries", "PixelTart_ModularHarness_V1_DevPreview.dll");
            artifact["application_sha256"] = _applicationHash;
            artifact["asset_module_path"] = Path.Combine(Root, "binaries", "PixelTart.Modules.AssetLibrary.dll");
            artifact["asset_module_sha256"] = _dllHash;
            artifact["path"] = Relative(path);
            artifact["sha256"] = Sha(path);
            artifact["kind"] = kind;
            artifact["scenario_id"] = id;
            artifact["phase"] = phase;
            artifact["process_session_id"] = SessionId(id, phase);
            artifact["pid"] = pid;
            artifact["hwnd"] = hwnd;
            artifact["scenario_root"] = scenarioRoot;
            summary["artifacts"]!.AsArray().Add(artifact);
        }

        private void AddEvent(List<JsonObject> events, ref int sequence, string id, int pid, string hwnd, string scenarioRoot, string type, string automationId, string route, string phase)
        {
            var item = Clone(Markers());
            item["run_id"] = RunId; item["source_head"] = Head; item["event_sequence"] = ++sequence;
            item["scenario_id"] = id; item["scenario_root"] = scenarioRoot; item["phase"] = phase; item["pid"] = pid; item["hwnd"] = hwnd;
            item["process_session_id"] = SessionId(id, phase);
            item["executable_path"] = Path.Combine(Root, "binaries", "PixelTart_ModularHarness_V1_DevPreview.exe"); item["executable_sha256"] = _exeHash;
            item["application_path"] = Path.Combine(Root, "binaries", "PixelTart_ModularHarness_V1_DevPreview.dll"); item["application_sha256"] = _applicationHash;
            item["asset_module_path"] = Path.Combine(Root, "binaries", "PixelTart.Modules.AssetLibrary.dll"); item["asset_module_sha256"] = _dllHash; item["event_type"] = type;
            item["automation_id"] = automationId; item["route"] = route; item["activation_mode"] = "in-process-wpf-route"; item["direct_mutation"] = false;
            item["before_state"] = new JsonObject { ["value"] = "before" }; item["after_state"] = new JsonObject { ["value"] = "after" }; item["persisted_state"] = new JsonObject { ["value"] = "persisted" };
            events.Add(item);
        }

        private JsonObject Bounds(string id, int pid, string hwnd, string scenarioRoot, string phase, int matrixIndex)
        {
            var result = Clone(Markers());
            result["run_id"] = RunId; result["source_head"] = Head; result["scenario_id"] = id; result["pid"] = pid; result["hwnd"] = hwnd;
            result["scenario_root"] = scenarioRoot; result["phase"] = phase;
            result["process_session_id"] = SessionId(id, phase);
            result["executable_path"] = Path.Combine(Root, "binaries", "PixelTart_ModularHarness_V1_DevPreview.exe"); result["executable_sha256"] = _exeHash;
            result["application_path"] = Path.Combine(Root, "binaries", "PixelTart_ModularHarness_V1_DevPreview.dll"); result["application_sha256"] = _applicationHash;
            result["asset_module_path"] = Path.Combine(Root, "binaries", "PixelTart.Modules.AssetLibrary.dll"); result["asset_module_sha256"] = _dllHash;
            result["viewport"] = new JsonObject { ["width"] = 1000, ["height"] = 700 };
            result["has_overflow"] = false;
            result["elements"] = new JsonArray(
                Element("AssetLibraryPage", "UserControl", 0, 0, 900, 650),
                Element("AssetLibraryThreePaneWorkspace", "Grid", 10, 10, 880, 600),
                Element("AssetOrganizationPane", "Border", 10, 20, 210, 560),
                Element("AssetCollectionPane", "Border", 230, 20, 460, 560),
                Element("AssetInspectorPane", "Border", 710, 20, 180, 560),
                Element("AssetOrganizationSplitter", "GridSplitter", 220, 20, 6, 560),
                Element("AssetInspectorSplitter", "GridSplitter", 700, 20, 6, 560),
                Element("AssetThumbnailSizeSlider", "Slider", 300, 600, 180, 30),
                Element("AssetLibraryImport", "Button", 500, 600, 120, 30));
            if (id == "layout-dpi-buttons/v1")
            {
                var matrix = new[] { (1366, 768, 100), (1920, 1080, 125), (1920, 1080, 150), (2560, 1440, 175) }[matrixIndex];
                result["simulated_layout"] = new JsonObject { ["width"] = matrix.Item1, ["height"] = matrix.Item2, ["scale_percent"] = matrix.Item3 };
            }
            return result;
        }

        private static JsonObject Element(string identity, string type, double x, double y, double width, double height) => new()
        {
            ["identity"] = identity, ["element_type"] = type, ["x"] = x, ["y"] = y, ["width"] = width, ["height"] = height,
            ["must_fit"] = true, ["clipped"] = false, ["overlapped"] = false
        };

        private static JsonObject Checks(string id) => id switch
        {
            "layout-dpi-buttons/v1" => LayoutChecks(),
            "first-empty/v1" => new JsonObject { ["attempt"] = 1, ["final_state"] = "ready", ["asset_count"] = 0, ["empty_state"] = true },
            "loading-error-retry-recovered/v1" => new JsonObject { ["attempt1_state"] = "error", ["attempt2_repository_query"] = true, ["attempt2_final_state"] = "ready", ["asset_count"] = 0 },
            "navigation-ime/v1" => new JsonObject { ["routes"] = new JsonArray("workbench", "asset-library", "raw-workspace", "calendar", "tether", "portfolio", "history"), ["chinese_ime_control_path"] = true, ["search_cleared_and_returned"] = true },
            "selection-navigation-restart/v1" => new JsonObject { ["same_selection_after_route_return"] = true, ["same_selection_after_restart"] = true, ["same_persisted_state_after_restart"] = true },
            "pane-collapse-expand/v1" => new JsonObject { ["same_pane_state_after_restart"] = true },
            "thumbnail-slider/v1" => new JsonObject { ["same_thumbnail_width_after_restart"] = true },
            _ => new JsonObject { ["verified"] = true }
        };

        private static JsonObject LayoutChecks()
        {
            var definitions = new (string Identity, string Role, string Surface, bool Text, bool Active)[]
            {
                ("header-organization-toggle", "AssetLibrarySecondaryButton", "ContentBackgroundBrush", true, false),
                ("header-inspector-toggle", "AssetLibrarySecondaryButton", "ContentBackgroundBrush", true, false),
                ("header-inspector-pin", "AssetLibrarySecondaryButton", "ContentBackgroundBrush", true, false),
                ("header-import", "AssetLibraryPrimaryButton", "ContentBackgroundBrush", true, false),
                ("organization-all-assets", "AssetLibrarySecondaryButton", "WorkbenchCardBrush", true, false),
                ("organization-new-folder", "AssetLibraryIconButton", "WorkbenchCardBrush", true, false),
                ("visual-chip-valid", "AssetLibraryChipButton", "WorkbenchCardBrush", true, false),
                ("visual-chip-not-analyzed", "AssetLibraryChipButton", "WorkbenchCardBrush", true, false),
                ("visual-chip-green", "AssetLibraryChipButton", "WorkbenchCardBrush", true, false),
                ("visual-chip-low-saturation", "AssetLibraryChipButton", "WorkbenchCardBrush", true, false),
                ("visual-chip-low-key", "AssetLibraryChipButton", "WorkbenchCardBrush", true, false),
                ("visual-chip-high-contrast", "AssetLibraryChipButton", "WorkbenchCardBrush", true, false),
                ("visual-chip-warm", "AssetLibraryChipButton", "WorkbenchCardBrush", true, false),
                ("visual-chip-cool", "AssetLibraryChipButton", "WorkbenchCardBrush", true, false),
                ("active-visual-chip-template", "AssetLibraryChipButton", "WorkbenchCardBrush", true, true),
                ("clear-visual-results", "AssetLibrarySecondaryButton", "WorkbenchCardBrush", true, false),
                ("load-error-retry", "AssetLibraryPrimaryButton", "ContentBackgroundBrush", true, false),
                ("empty-state-import", "AssetLibraryPrimaryButton", "ContentBackgroundBrush", true, false),
                ("empty-state-clear-filters", "AssetLibrarySecondaryButton", "ContentBackgroundBrush", true, false),
                ("inspector-reanalyze", "AssetLibrarySecondaryButton", "WorkbenchCardBrush", true, false),
                ("inspector-find-similar", "AssetLibrarySecondaryButton", "WorkbenchCardBrush", true, false),
                ("smart-folder-save", "AssetLibrarySecondaryButton", "WorkbenchCardBrush", true, false),
                ("palette-swatch-template", "AssetLibraryPaletteSwatchButton", "WorkbenchCardBrush", false, false),
                ("palette-find-similar", "AssetLibrarySecondaryButton", "WorkbenchCardBrush", true, false),
                ("color-search", "AssetLibrarySecondaryButton", "WorkbenchCardBrush", true, false),
                ("visual-search-start", "AssetLibraryPrimaryButton", "WorkbenchCardBrush", true, false),
                ("visual-search-cancel", "AssetLibrarySecondaryButton", "WorkbenchCardBrush", true, false),
            };
            var colors = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["AssetLibraryPrimaryForegroundColor"] = "#F5FFFC", ["AssetLibraryPrimaryNormalColor"] = "#0B6658", ["AssetLibraryPrimaryHoverColor"] = "#0D7464", ["AssetLibraryPrimaryPressedColor"] = "#074D43",
                ["AssetLibrarySecondaryForegroundColor"] = "#F2F6F7", ["AssetLibrarySecondaryNormalColor"] = "#192129", ["AssetLibrarySecondaryHoverColor"] = "#24303A", ["AssetLibrarySecondaryPressedColor"] = "#111820",
                ["AssetLibraryChipForegroundColor"] = "#EAF1F3", ["AssetLibraryChipNormalColor"] = "#172027", ["AssetLibraryChipHoverColor"] = "#22313A", ["AssetLibraryChipPressedColor"] = "#0F171D",
                ["AssetLibraryChipActiveNormalColor"] = "#114238", ["AssetLibraryChipActiveHoverColor"] = "#155247", ["AssetLibraryChipActivePressedColor"] = "#0C332C",
                ["AssetLibraryFocusRingColor"] = "#65E6C7", ["AssetLibraryButtonFocusOuterColor"] = "#061311", ["AssetLibraryButtonFocusInnerColor"] = "#F5FFFC", ["AssetLibraryPaletteFocusInnerColor"] = "#061311",
                ["AssetLibraryDisabledBackgroundColor"] = "#252C32", ["AssetLibraryDisabledForegroundColor"] = "#ADB8C0", ["AssetLibraryDisabledBorderColor"] = "#71808C",
                ["AssetLibraryBorderColor"] = "#5F7180", ["AssetLibraryActiveBorderColor"] = "#42CEAF", ["AssetLibraryPaletteBorderColor"] = "#5F7180", ["AssetLibraryPaletteHoverBorderColor"] = "#91B5C2", ["AssetLibraryPalettePressedBorderColor"] = "#55D0B3",
            };
            var systemWindow = System.Windows.SystemColors.WindowColor;
            var systemWindowHex = $"#{systemWindow.R:X2}{systemWindow.G:X2}{systemWindow.B:X2}";
            var surfaces = new Dictionary<string, Dictionary<string, string>>(StringComparer.Ordinal)
            {
                ["dark"] = new(StringComparer.Ordinal) { ["ContentBackgroundBrush"] = "#0D0F12", ["WorkbenchCardBrush"] = "#121519" },
                ["light"] = new(StringComparer.Ordinal) { ["ContentBackgroundBrush"] = "#F4F6F8", ["WorkbenchCardBrush"] = "#FFFFFF" },
                ["high-contrast"] = new(StringComparer.Ordinal) { ["ContentBackgroundBrush"] = systemWindowHex, ["WorkbenchCardBrush"] = systemWindowHex },
            };
            var matrix = new JsonArray();
            foreach (var definition in definitions)
            foreach (var theme in new[] { "dark", "light", "high-contrast" })
            foreach (var state in new[] { "normal", "hover", "pressed", "focus", "disabled" })
            {
                var baseState = state == "focus" ? "Normal" : char.ToUpperInvariant(state[0]) + state[1..];
                var surface = surfaces[theme][definition.Surface];
                var background = definition.Role switch
                {
                    _ when state == "disabled" && definition.Role != "AssetLibraryPaletteSwatchButton" => colors["AssetLibraryDisabledBackgroundColor"],
                    "AssetLibraryPrimaryButton" => colors[$"AssetLibraryPrimary{baseState}Color"],
                    "AssetLibrarySecondaryButton" or "AssetLibraryIconButton" => colors[$"AssetLibrarySecondary{baseState}Color"],
                    "AssetLibraryChipButton" => colors[$"AssetLibraryChip{(definition.Active ? "Active" : string.Empty)}{baseState}Color"],
                    _ => surface,
                };
                var foreground = state == "disabled"
                    ? colors["AssetLibraryDisabledForegroundColor"]
                    : definition.Role == "AssetLibraryChipButton" && !definition.Active
                        ? colors["AssetLibraryChipForegroundColor"]
                        : definition.Role == "AssetLibraryPrimaryButton" || definition.Role == "AssetLibraryChipButton" && definition.Active
                            ? colors["AssetLibraryPrimaryForegroundColor"]
                            : colors["AssetLibrarySecondaryForegroundColor"];
                var border = state == "disabled"
                    ? colors["AssetLibraryDisabledBorderColor"]
                    : definition.Role switch
                    {
                        "AssetLibraryPrimaryButton" => background,
                        "AssetLibrarySecondaryButton" or "AssetLibraryIconButton" => state switch { "hover" => colors["AssetLibraryPaletteHoverBorderColor"], "pressed" => colors["AssetLibraryFocusRingColor"], _ => colors["AssetLibraryBorderColor"] },
                        "AssetLibraryChipButton" when definition.Active => state is "normal" or "focus" ? colors["AssetLibraryActiveBorderColor"] : colors["AssetLibraryFocusRingColor"],
                        "AssetLibraryChipButton" => state switch { "hover" => colors["AssetLibraryPaletteHoverBorderColor"], "pressed" => colors["AssetLibraryFocusRingColor"], _ => colors["AssetLibraryBorderColor"] },
                        _ => state switch { "hover" => colors["AssetLibraryPaletteHoverBorderColor"], "pressed" => colors["AssetLibraryPalettePressedBorderColor"], _ => colors["AssetLibraryPaletteBorderColor"] },
                    };
                var nonTextReference = definition.Role == "AssetLibraryPaletteSwatchButton"
                    ? state switch { "hover" => colors["AssetLibrarySecondaryHoverColor"], "pressed" => colors["AssetLibrarySecondaryPressedColor"], "disabled" => colors["AssetLibraryDisabledBackgroundColor"], _ => colors["AssetLibrarySecondaryNormalColor"] }
                    : background;
                var focusOuter = colors[definition.Role == "AssetLibraryPaletteSwatchButton" ? "AssetLibraryFocusRingColor" : "AssetLibraryButtonFocusOuterColor"];
                var focusInner = colors[definition.Role == "AssetLibraryPaletteSwatchButton" ? "AssetLibraryPaletteFocusInnerColor" : "AssetLibraryButtonFocusInnerColor"];
                var focusContrast = new[] { Contrast(focusOuter, background), Contrast(focusInner, background), Contrast(focusOuter, surface), Contrast(focusInner, surface) }.Max();
                matrix.Add(new JsonObject
                {
                    ["button_identity"] = definition.Identity, ["role"] = definition.Role, ["theme"] = theme, ["state"] = state, ["surface_resource_key"] = definition.Surface,
                    ["surface_color"] = surface, ["background_color"] = background, ["foreground_color"] = foreground, ["border_color"] = border, ["non_text_reference_color"] = nonTextReference,
                    ["focus_outer_color"] = focusOuter, ["focus_inner_color"] = focusInner, ["text_contrast"] = definition.Text ? Contrast(foreground, background) : null,
                    ["non_text_contrast"] = definition.Role != "AssetLibraryPrimaryButton" ? Contrast(border, nonTextReference) : null, ["focus_contrast"] = focusContrast,
                    ["focus_visible"] = focusContrast >= 3, ["text_contrast_applicable"] = definition.Text, ["non_text_contrast_applicable"] = definition.Role != "AssetLibraryPrimaryButton",
                    ["live_wpf_button_instance"] = true, ["source_declaration_probe"] = true, ["template_applied"] = true,
                    ["state_resolution"] = state switch { "normal" => "wpf-effective-value", "disabled" => "wpf-effective-disabled-trigger", "focus" => "wpf-control-template-focus-trigger", _ => "wpf-style-trigger-resolution" },
                });
            }
            return new JsonObject
            {
                ["matrix_kind"] = "simulated-layout-dpi", ["real_display_settings_changed"] = false, ["realized_button_count"] = 27, ["visible_button_instance_count"] = 9,
                ["button_state_matrix_schema"] = "pixel-tart-p1-live-wpf-button-state-matrix/v1", ["button_state_record_count"] = matrix.Count, ["button_state_matrix"] = matrix,
                ["minimum_text_contrast"] = matrix.Where(item => item!["text_contrast_applicable"]!.GetValue<bool>()).Min(item => item!["text_contrast"]!.GetValue<double>()),
                ["minimum_non_text_contrast"] = matrix.Where(item => item!["non_text_contrast_applicable"]!.GetValue<bool>()).Min(item => item!["non_text_contrast"]!.GetValue<double>()),
                ["focus_visible_all_themes"] = true, ["theme_application_mode"] = "ephemeral-live-resource-dictionary-restored",
            };
        }

        private static double Contrast(string first, string second)
        {
            static double Luminance(string value)
            {
                var hex = value.AsSpan(1);
                static double Linear(byte channel)
                {
                    var normalized = channel / 255d;
                    return normalized <= 0.04045 ? normalized / 12.92 : Math.Pow((normalized + 0.055) / 1.055, 2.4);
                }
                return 0.2126 * Linear(Convert.ToByte(hex[..2].ToString(), 16)) +
                       0.7152 * Linear(Convert.ToByte(hex.Slice(2, 2).ToString(), 16)) +
                       0.0722 * Linear(Convert.ToByte(hex.Slice(4, 2).ToString(), 16));
            }
            var firstLuminance = Luminance(first);
            var secondLuminance = Luminance(second);
            return (Math.Max(firstLuminance, secondLuminance) + 0.05) / (Math.Min(firstLuminance, secondLuminance) + 0.05);
        }

        private void MutateBounds(JsonObject summary, JsonArray artifacts, int scenarioIndex, Action<JsonObject> mutate)
        {
            var relative = summary["scenarios"]![scenarioIndex]!["bounds_paths"]![0]!.GetValue<string>();
            var path = Absolute(relative);
            var bounds = ReadJson(path);
            mutate(bounds);
            WriteJson(path, bounds);
            artifacts.Single(item => item!["path"]!.GetValue<string>() == relative)!["sha256"] = Sha(path);
            WriteJson(_summaryPath, summary);
        }

        private void WriteEvents(IEnumerable<JsonObject> events) => WriteEventChain(events);

        private string WriteEventChain(IEnumerable<JsonObject> events)
        {
            var previous = new string('0', 64);
            var lines = new List<string>();
            foreach (var source in events)
            {
                var item = Clone(source);
                item["previous_event_hash"] = previous;
                item["previous_record_sha256"] = previous;
                item.Remove("event_hash");
                item.Remove("record_sha256");
                var canonical = item.ToJsonString(EventJsonOptions);
                var hash = ShaBytes(Encoding.UTF8.GetBytes(canonical));
                item["event_hash"] = hash;
                item["record_sha256"] = hash;
                lines.Add(item.ToJsonString(EventJsonOptions));
                previous = hash;
            }
            File.WriteAllLines(_eventsPath, lines, new UTF8Encoding(false));
            return previous;
        }

        private void WriteSummaryChain(JsonObject summary)
        {
            var previous = new string('0', 64);
            var lines = new List<string>();
            var scenarios = summary["scenarios"]!.AsArray();
            var order = Enumerable.Range(0, 9).Select(index => (Index: index, Phase: "primary"))
                .Concat(new[] { (4, "restart"), (5, "restart"), (6, "restart") });
            foreach (var (index, phase) in order)
            {
                var scenario = scenarios[index]!;
                var item = Clone(Markers());
                item.Remove("automated_capture_status");
                item["run_id"] = RunId;
                item["source_head"] = Head;
                item["scenario_id"] = scenario["id"]!.GetValue<string>();
                item["scenario_root"] = scenario["scenario_root"]!.GetValue<string>();
                item["phase"] = phase;
                item["process_session_id"] = SessionId(scenario["id"]!.GetValue<string>(), phase);
                item["pid"] = phase == "primary" ? scenario["pid"]!.GetValue<int>() : scenario["restart_pid"]!.GetValue<int>();
                item["hwnd"] = phase == "primary" ? scenario["hwnd"]!.GetValue<string>() : scenario["restart_hwnd"]!.GetValue<string>();
                item["executable_path"] = summary["executable_path"]!.GetValue<string>();
                item["executable_sha256"] = _exeHash;
                item["application_path"] = summary["application_path"]!.GetValue<string>();
                item["application_sha256"] = _applicationHash;
                item["asset_module_path"] = summary["asset_module_path"]!.GetValue<string>();
                item["asset_module_sha256"] = _dllHash;
                var embeddedSummary = Clone(summary);
                embeddedSummary["phase"] = phase;
                embeddedSummary["process_session_id"] = SessionId(scenario["id"]!.GetValue<string>(), phase);
                item["summary"] = embeddedSummary;
                item["previous_summary_hash"] = previous;
                item["previous_record_sha256"] = previous;
                var canonical = item.ToJsonString(EventJsonOptions);
                var hash = ShaBytes(Encoding.UTF8.GetBytes(canonical));
                item["summary_hash"] = hash;
                item["record_sha256"] = hash;
                lines.Add(item.ToJsonString(EventJsonOptions));
                previous = hash;
            }
            File.WriteAllLines(_summaryJournalPath, lines, new UTF8Encoding(false));
        }
        private string Relative(string path) => Path.GetRelativePath(Root, path).Replace('\\', '/');
        private string Absolute(string relative) => Path.Combine(Root, relative.Replace('/', Path.DirectorySeparatorChar));
        private static void CreateDatabase(string path, int assetCount)
        {
            using (var connection = new SqliteConnection($"Data Source={path};Pooling=False"))
            {
                connection.Open();
                using (var journal = connection.CreateCommand())
                {
                    journal.CommandText = "PRAGMA journal_mode=WAL;";
                    Assert.AreEqual("wal", Convert.ToString(journal.ExecuteScalar(), System.Globalization.CultureInfo.InvariantCulture));
                }
                using var command = connection.CreateCommand();
                command.CommandText = "CREATE TABLE AssetLibrarySchemaInfo(Version INTEGER PRIMARY KEY); INSERT INTO AssetLibrarySchemaInfo VALUES(6); CREATE TABLE AssetItems(Id TEXT PRIMARY KEY);" + (assetCount == 1 ? "INSERT INTO AssetItems VALUES('synthetic-asset');" : string.Empty);
                command.ExecuteNonQuery();
                using var checkpoint = connection.CreateCommand();
                checkpoint.CommandText = "PRAGMA wal_checkpoint(TRUNCATE);";
                checkpoint.ExecuteNonQuery();
            }
            SqliteConnection.ClearAllPools();
            Assert.IsFalse(File.Exists(path + "-wal"), "The WAL-mode fixture retained a WAL before independent validation.");
            Assert.IsFalse(File.Exists(path + "-shm"), "The WAL-mode fixture retained an SHM before independent validation.");
        }

        private static JsonObject BinarySnapshot(string sourceDirectory, string binaryDirectory)
        {
            var files = Directory.EnumerateFiles(binaryDirectory, "*", SearchOption.AllDirectories)
                .OrderBy(path => Path.GetRelativePath(binaryDirectory, path), StringComparer.Ordinal)
                .Select(path => (JsonNode)new JsonObject
                {
                    ["path"] = Path.GetRelativePath(binaryDirectory, path).Replace('\\', '/'),
                    ["byte_length"] = new FileInfo(path).Length,
                    ["sha256"] = Sha(path),
                })
                .ToArray();
            return new JsonObject
            {
                ["schema"] = "pixel-tart-p1-run-owned-binary-snapshot/v2",
                ["source_directory"] = sourceDirectory,
                ["directory"] = binaryDirectory,
                ["file_count"] = files.Length,
                ["copy_verified_before_execution"] = true,
                ["tree_sha256"] = BinaryTreeHash(binaryDirectory),
                ["files"] = new JsonArray(files),
            };
        }

        private static string BinaryTreeHash(string binaryDirectory)
        {
            var lines = Directory.EnumerateFiles(binaryDirectory, "*", SearchOption.AllDirectories)
                .Select(path => new
                {
                    Path = Path.GetRelativePath(binaryDirectory, path).Replace('\\', '/'),
                    Length = new FileInfo(path).Length,
                    Hash = Sha(path),
                })
                .OrderBy(item => item.Path, StringComparer.Ordinal)
                .Select(item => $"{item.Path}|{item.Length}|{item.Hash}");
            return ShaBytes(Encoding.UTF8.GetBytes(string.Join("\n", lines)));
        }

        private static JsonObject BinaryIdentity(string path, string sha256) => new()
        {
            ["path"] = path,
            ["sha256"] = sha256,
        };

        private static string SessionId(string id, string phase)
        {
            var index = Array.IndexOf(ScenarioIds, id) + 1 + (phase == "restart" ? 100 : 0);
            return index.ToString("x32", System.Globalization.CultureInfo.InvariantCulture);
        }

        private static JsonObject Markers() => new() { ["validation_mode"] = "automated", ["owner_manual_ux_smoke"] = "waived", ["manual_evidence_claimed"] = false, ["historical_manual_gate"] = "not_closed_superseded_as_release_blocker", ["automated_capture_status"] = "captured" };
        private static JsonObject Safety() => new()
        {
            ["desktop_input_injection_count"] = 0, ["real_display_setting_write_count"] = 0, ["eagle_write_count"] = 0, ["user_source_read_count"] = 0,
            ["user_source_write_count"] = 0, ["direct_width_mutation_count"] = 0, ["direct_settings_mutation_count"] = 0, ["direct_sqlite_row_edit_count"] = 0
        };
        private static JsonObject Cleanup(IReadOnlyList<string> removedPaths) => new()
        {
            ["all_scenarios_closed_normally"] = true, ["devpreview_get_process_count_after"] = 0, ["devpreview_cim_count_after"] = 0,
            ["dotnet_residual_pid_count"] = 0, ["db_sidecar_count_after"] = 0, ["runtime_database_count_after"] = 0,
            ["environment_residual_count"] = 0, ["display_settings_unchanged"] = true,
            ["runtime_database_cleanup"] = new JsonObject
            {
                ["removed_count"] = removedPaths.Count,
                ["removed_paths"] = new JsonArray(removedPaths.Select(value => JsonValue.Create(value)).ToArray()),
                ["runtime_database_count_after"] = 0
            }
        };
        private static JsonObject AppCleanup() => new() { ["exit_code"] = 0, ["shutdown_requested"] = true, ["application_exit_hook_reached"] = true, ["residual_process_check_owner"] = "independent-runner-after-process-exit", ["database_wal_present"] = false, ["database_shm_present"] = false };

        private static JsonArray SourceAudit(string repositoryRoot)
        {
            var result = new JsonArray();
            foreach (var path in new[]
                     {
                         "src/PixelTart.Modules.AssetLibrary/AssetLibraryPage.xaml",
                         "src/RAWSelectionAssistant/Resources/DesignSystem/Theme.Dark.xaml",
                         "src/RAWSelectionAssistant/Resources/DesignSystem/Theme.Light.xaml",
                         "src/RAWSelectionAssistant/Resources/DesignSystem/Theme.HighContrast.xaml"
                     })
            {
                var bytes = GitBlob(repositoryRoot, $"{Head}:{path}");
                result.Add(new JsonObject
                {
                    ["path"] = path,
                    ["git_blob_oid"] = GitText(repositoryRoot, "rev-parse", $"{Head}:{path}"),
                    ["sha256"] = ShaBytes(bytes),
                    ["byte_length"] = bytes.Length
                });
            }
            return result;
        }

        private static byte[] GitBlob(string repositoryRoot, string objectName)
        {
            var start = new ProcessStartInfo("git") { WorkingDirectory = repositoryRoot, UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true, CreateNoWindow = true };
            start.ArgumentList.Add("cat-file");
            start.ArgumentList.Add("blob");
            start.ArgumentList.Add(objectName);
            using var process = Process.Start(start) ?? throw new InvalidOperationException("git cat-file could not start.");
            using var memory = new MemoryStream();
            process.StandardOutput.BaseStream.CopyTo(memory);
            var error = process.StandardError.ReadToEnd();
            process.WaitForExit();
            if (process.ExitCode != 0) throw new InvalidOperationException(error);
            return memory.ToArray();
        }

        private static string GitText(string repositoryRoot, params string[] arguments)
        {
            var start = new ProcessStartInfo("git") { WorkingDirectory = repositoryRoot, UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true, CreateNoWindow = true };
            foreach (var argument in arguments) start.ArgumentList.Add(argument);
            using var process = Process.Start(start) ?? throw new InvalidOperationException("git could not start.");
            var output = process.StandardOutput.ReadToEnd();
            var error = process.StandardError.ReadToEnd();
            process.WaitForExit();
            if (process.ExitCode != 0) throw new InvalidOperationException(error);
            return output.Trim();
        }
    }

    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };
    private static readonly JsonSerializerOptions EventJsonOptions = new() { WriteIndented = false };
    private static JsonObject Clone(JsonObject source) => JsonNode.Parse(source.ToJsonString())!.AsObject();
    private static JsonObject ReadJson(string path) => JsonNode.Parse(File.ReadAllText(path, Encoding.UTF8))!.AsObject();
    private static void WriteJson(string path, JsonNode value) { Directory.CreateDirectory(Path.GetDirectoryName(path)!); File.WriteAllText(path, value.ToJsonString(JsonOptions), new UTF8Encoding(false)); }
    private static string Sha(string path) => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))).ToLowerInvariant();
    private static string ShaBytes(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
    private static void ContainsAll(string source, params string[] values) { foreach (var value in values) StringAssert.Contains(source, value); }
    private static string Read(string relativePath) => File.ReadAllText(Path.Combine(Root(), relativePath.Replace('/', Path.DirectorySeparatorChar)), Encoding.UTF8);
    private static string Root() { for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent) if (File.Exists(Path.Combine(directory.FullName, "RAWSelectionAssistant.sln"))) return directory.FullName; throw new DirectoryNotFoundException("Repository root not found."); }

    private static (int ExitCode, string Output) RunValidator(string runRoot)
    {
        var start = new ProcessStartInfo("powershell.exe") { UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true, CreateNoWindow = true };
        foreach (var argument in new[] { "-NoProfile", "-NonInteractive", "-ExecutionPolicy", "Bypass", "-File", Path.Combine(Root(), "tools", "AssetLibraryP1AutomatedAcceptance", "Test-P1AssetLibraryAutomatedEvidence.ps1"), "-RunRoot", runRoot }) start.ArgumentList.Add(argument);
        using var process = Process.Start(start) ?? throw new InvalidOperationException("PowerShell could not start.");
        var stdout = process.StandardOutput.ReadToEndAsync();
        var stderr = process.StandardError.ReadToEndAsync();
        Assert.IsTrue(process.WaitForExit(60_000), "Validator timed out.");
        Task.WaitAll(stdout, stderr);
        return (process.ExitCode, stdout.Result + Environment.NewLine + stderr.Result);
    }

    private static (int ExitCode, string Output) RunValidateExistingWrapper(string runRoot)
    {
        var start = new ProcessStartInfo("powershell.exe") { UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true, CreateNoWindow = true };
        foreach (var argument in new[] { "-NoProfile", "-NonInteractive", "-ExecutionPolicy", "Bypass", "-File", Path.Combine(Root(), "tools", "AssetLibraryP1AutomatedAcceptance", "Invoke-P1AssetLibraryAutomatedAcceptance.ps1"), "-Mode", "ValidateExistingRun", "-RunRoot", runRoot }) start.ArgumentList.Add(argument);
        using var process = Process.Start(start) ?? throw new InvalidOperationException("PowerShell could not start.");
        var stdout = process.StandardOutput.ReadToEndAsync();
        var stderr = process.StandardError.ReadToEndAsync();
        Assert.IsTrue(process.WaitForExit(60_000), "ValidateExistingRun timed out.");
        Task.WaitAll(stdout, stderr);
        return (process.ExitCode, stdout.Result + Environment.NewLine + stderr.Result);
    }
}
