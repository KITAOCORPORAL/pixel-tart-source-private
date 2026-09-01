using System.Diagnostics;
using System.IO;
using System.Text.Json;

namespace RAWSelectionAssistant.WpfTests;

[TestClass]
public sealed class AssetLibraryP2AutomatedEvidenceContractTests
{
    private static readonly string[] Scenarios =
    [
        "fixture-integrity/v1", "organization-browser/v1", "smart-tag-query/v1",
        "four-views-query-sort/v1", "selection-large/v1", "metadata-drag-command/v1",
        "inspector-states/v1", "resilience-states/v1", "restart-persistence/v1",
        "layout-dpi-performance/v1"
    ];

    private static readonly string[] NegativeFixtures =
    [
        "missing-screenshot", "mutated-hash", "wrong-scenario-order", "fixture-count-mismatch",
        "fixture-content-hash-mismatch", "fixture-display-name-not-chinese", "fixture-missing-count-mismatch",
        "fixture-visual-outcome-mismatch", "fixture-schema-marker-mismatch",
        "fixture-path-escape", "folder-cycle-accepted", "duplicate-automation-id", "smart-result-mismatch",
        "query-plan-divergence", "stale-cancelled-query", "view-state-lost", "virtualization-realizes-all",
        "sort-unstable", "selection-truncated", "invalid-drop-accepted", "undo-mismatch",
        "prohibited-command-present", "inspector-mode-mismatch", "restart-identity-reused", "dpi-overflow",
        "performance-threshold-exceeded", "ui-block-exceeded", "user-source-write", "eagle-write",
        "residual-process", "database-not-v6", "cross-run-splice", "runner-session-splice",
        "process-session-splice", "binary-hash-mismatch", "input-tree-mutated"
    ];

    [TestMethod]
    public void ContractIsIndependentP2FailClosedContract()
    {
        using var document = JsonDocument.Parse(Read("tools/AssetLibraryP2AutomatedAcceptance/automated-acceptance-contract.json"));
        var root = document.RootElement;
        Assert.AreEqual("pixel-tart-asset-library-p2-automated-acceptance-contract/v1", root.GetProperty("schema").GetString());
        Assert.AreEqual("automated", root.GetProperty("validation_mode").GetString());
        Assert.AreEqual("waived", root.GetProperty("owner_manual_ux_smoke").GetString());
        Assert.IsFalse(root.GetProperty("manual_evidence_claimed").GetBoolean());
        Assert.AreEqual(11, root.GetProperty("required_runner_session_count").GetInt32());
        CollectionAssert.AreEqual(Scenarios, root.GetProperty("required_scenario_order").EnumerateArray().Select(item => item.GetString()).ToArray());
    }

    [TestMethod]
    public void FixtureAndDpiAndThresholdsAreFixed()
    {
        using var document = JsonDocument.Parse(Read("tools/AssetLibraryP2AutomatedAcceptance/automated-acceptance-contract.json"));
        var root = document.RootElement;
        var fixture = root.GetProperty("fixture");
        Assert.AreEqual(512, fixture.GetProperty("total_count").GetInt32());
        Assert.AreEqual(500, fixture.GetProperty("active_count").GetInt32());
        Assert.AreEqual(12, fixture.GetProperty("archived_count").GetInt32());
        Assert.AreEqual(6, fixture.GetProperty("schema_version").GetInt32());
        Assert.AreEqual(512, fixture.GetProperty("display_name_count").GetInt32());
        Assert.AreEqual("zh-CN", fixture.GetProperty("display_name_language").GetString());
        Assert.AreEqual(512, fixture.GetProperty("content_hash_count").GetInt32());
        Assert.AreEqual("sha256", fixture.GetProperty("content_hash_algorithm").GetString());
        Assert.IsTrue(fixture.GetProperty("content_hash_deterministic").GetBoolean());
        Assert.AreEqual(32, fixture.GetProperty("missing_count").GetInt32());
        var visual = fixture.GetProperty("visual_feature_counts");
        Assert.AreEqual("visual-analysis-v2", visual.GetProperty("analysis_version").GetString());
        Assert.AreEqual(128, visual.GetProperty("valid").GetInt32());
        Assert.AreEqual(64, visual.GetProperty("failed").GetInt32());
        Assert.AreEqual(320, visual.GetProperty("not_analyzed").GetInt32());
        Assert.AreEqual(192, visual.GetProperty("feature_rows").GetInt32());
        var dpi = root.GetProperty("required_dpi_matrix").EnumerateArray()
            .Select(item => (item.GetProperty("width").GetInt32(), item.GetProperty("height").GetInt32(), item.GetProperty("scale_percent").GetInt32())).ToArray();
        CollectionAssert.AreEqual(new[] { (1366, 768, 100), (1920, 1080, 125), (1920, 1080, 150), (2560, 1440, 175) }, dpi);
        var limits = root.GetProperty("performance_thresholds_ms");
        CollectionAssert.AreEqual(new[] { 1500, 250, 350, 250, 750, 100 }, limits.EnumerateObject().Select(item => item.Value.GetInt32()).ToArray());
    }

    [TestMethod]
    public void EveryNegativeFixtureHasAnExplicitValidatorGuard()
    {
        using var document = JsonDocument.Parse(Read("tools/AssetLibraryP2AutomatedAcceptance/automated-acceptance-contract.json"));
        CollectionAssert.AreEqual(NegativeFixtures, document.RootElement.GetProperty("required_negative_fixtures").EnumerateArray().Select(item => item.GetString()).ToArray());
        var validator = Read("tools/AssetLibraryP2AutomatedAcceptance/Test-P2AssetLibraryAutomatedEvidence.ps1");
        foreach (var fixture in NegativeFixtures)
            StringAssert.Contains(validator, $"'{fixture}'=");
        StringAssert.Contains(validator, "negative fixture list has no exact validator guard map");
    }

    [TestMethod]
    public void RunnerAndValidatorPowerShellParseWithoutErrors()
    {
        foreach (var relative in new[]
                 {
                     "tools/AssetLibraryP2AutomatedAcceptance/Invoke-P2AssetLibraryAutomatedAcceptance.ps1",
                     "tools/AssetLibraryP2AutomatedAcceptance/Test-P2AssetLibraryAutomatedEvidence.ps1"
                 })
        {
            var escapedPath = Path(relative).Replace("'", "''", StringComparison.Ordinal);
            var script = $"$e=$null;[Management.Automation.Language.Parser]::ParseFile('{escapedPath}',[ref]$null,[ref]$e)|Out-Null;if(@($e).Count){{$e|% Message;exit 1}}";
            using var process = Process.Start(new ProcessStartInfo("powershell.exe")
            {
                UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true,
                ArgumentList = { "-NoProfile", "-NonInteractive", "-Command", script }
            })!;
            process.WaitForExit();
            Assert.AreEqual(0, process.ExitCode, $"{relative}: {process.StandardOutput.ReadToEnd()} {process.StandardError.ReadToEnd()}");
        }
    }

    [TestMethod]
    public void ValidatorUsesWindowsPowerShellCompatibleAbsolutePathGuard()
    {
        var validator = Read("tools/AssetLibraryP2AutomatedAcceptance/Test-P2AssetLibraryAutomatedEvidence.ps1");
        StringAssert.Contains(validator, "function Test-AbsolutePath");
        StringAssert.Contains(validator, "Test-AbsolutePath $RunRoot");
        Assert.DoesNotContain("IsPathFullyQualified", validator, StringComparison.Ordinal);
        ContainsAll(validator, "[A-Za-z]:", "GetFullPath", "Relative-Path", "Bytes-ToHex", "Sha256Bytes");
        Assert.DoesNotContain("GetRelativePath", validator, StringComparison.Ordinal);
        Assert.DoesNotContain("HashData", validator, StringComparison.Ordinal);
        Assert.DoesNotContain("ToHexString", validator, StringComparison.Ordinal);
        StringAssert.Contains(validator, ".Replace('\\', '/')");
    }

    [TestMethod]
    public void RunnerUsesWindowsPowerShellCompatibleHashAndValidatorHandshake()
    {
        var runner = Read("tools/AssetLibraryP2AutomatedAcceptance/Invoke-P2AssetLibraryAutomatedAcceptance.ps1");
        Assert.DoesNotContain("GetRelativePath", runner, StringComparison.Ordinal);
        Assert.DoesNotContain("IsPathFullyQualified", runner, StringComparison.Ordinal);
        Assert.DoesNotContain("HashData", runner, StringComparison.Ordinal);
        Assert.DoesNotContain("ToHexString", runner, StringComparison.Ordinal);
        ContainsAll(runner, "function Invoke-LoggedProcess", "if ($result.exit_code -ne 0)",
            "Validator emitted unexpected stderr", "Validator stdout is not valid JSON",
            "Validator stdout failed the result contract", "pixel-tart-p2-automated-validation-result/v1",
            "targetRoot", "targetHead", "Validator log directory must be outside the sealed run root",
            "$validatorLogDirectory = Join-Path (Split-Path -Parent $activeRunRoot)",
            "Invoke-Validator $activeRunRoot $validatorLogDirectory");
    }

    [TestMethod]
    public void WindowsPowerShellRunnerRejectsRelativeRootAndHandshakeDrift()
    {
        var runnerPath = Path("tools/AssetLibraryP2AutomatedAcceptance/Invoke-P2AssetLibraryAutomatedAcceptance.ps1");
        var relative = StartPowerShell(new[]
        {
            "-File", runnerPath, "-Mode", "ValidateExistingRun", "-RunRoot", ".validation\\relative-root"
        }, RepositoryRoot());
        Assert.AreNotEqual(0, relative.ExitCode, "ValidateExistingRun accepted a relative run root.");

        using var temp = new TemporaryDirectory("PixelTart-P2-ValidatorHandshake");
        var extractionScript = System.IO.Path.Combine(temp.Path, "handshake.ps1");
        var copiedValidator = System.IO.Path.Combine(temp.Path, "Test-P2AssetLibraryAutomatedEvidence.ps1");
        File.WriteAllText(copiedValidator, "# stub");
        File.WriteAllText(extractionScript, HandshakeProbeScript);
        var sourceHead = new string('a', 40);
        var manifest = new
        {
            schema_version = "pixel-tart-p2-automated-run/v1",
            source_head = sourceHead
        };
        var runRoot = System.IO.Path.Combine(temp.Path, "run");
        Directory.CreateDirectory(runRoot);
        File.WriteAllText(System.IO.Path.Combine(runRoot, "run-manifest.json"), JsonSerializer.Serialize(manifest));
        foreach (var testCase in new[] { "valid", "stderr", "invalid-json" })
        {
            var probe = StartPowerShell(new[] { "-File", extractionScript }, RepositoryRoot(), new Dictionary<string, string>
            {
                ["P2_RUNNER_PATH"] = runnerPath,
                ["P2_ROOT"] = runRoot,
                ["P2_HEAD"] = sourceHead,
                ["P2_CASE"] = testCase,
                ["P2_LOG"] = System.IO.Path.Combine(temp.Path, "logs", testCase)
            });
            Assert.AreEqual(0, probe.ExitCode, $"Invoke-Validator handshake case '{testCase}' did not produce the expected result. {probe.Output} {probe.Error}");
        }
    }

    [TestMethod]
    public void ValidateExistingRunAcceptsLatestCapturedP2RunWithoutChangingIt()
    {
        var repository = RepositoryRoot();
        var validationRoot = System.IO.Path.Combine(repository, ".validation");
        var candidate = Directory.Exists(validationRoot)
            ? Directory.EnumerateDirectories(validationRoot, "P2-Automated-Acceptance-*")
                .OrderByDescending(Directory.GetLastWriteTimeUtc)
                .FirstOrDefault(path =>
                {
                    try
                    {
                        using var document = JsonDocument.Parse(File.ReadAllText(System.IO.Path.Combine(path, "run-manifest.json")));
                        return document.RootElement.TryGetProperty("automated_capture_status", out var status) &&
                               status.GetString() == "captured";
                    }
                    catch { return false; }
                })
            : null;
        if (candidate is null) Assert.Inconclusive("No captured P2 run root is available for the read-only integration probe.");
        var before = TreeFingerprint(candidate);
        var runner = Path("tools/AssetLibraryP2AutomatedAcceptance/Invoke-P2AssetLibraryAutomatedAcceptance.ps1");
        var result = StartPowerShell(new[] { "-File", runner, "-Mode", "ValidateExistingRun", "-RunRoot", candidate }, repository);
        Assert.AreEqual(0, result.ExitCode, $"ValidateExistingRun rejected a captured run. {result.Output} {result.Error}");
        Assert.AreEqual(before, TreeFingerprint(candidate), "ValidateExistingRun changed the sealed run root.");
    }

    [TestMethod]
    public void RunnerExposesFourModesAndOneRestartOnly()
    {
        var runner = Read("tools/AssetLibraryP2AutomatedAcceptance/Invoke-P2AssetLibraryAutomatedAcceptance.ps1");
        StringAssert.Contains(runner, "[ValidateSet('Run', 'DryRun', 'ValidateExistingRun', 'RecoveryTest')]");
        StringAssert.Contains(runner, "$restartScenarios = @('restart-persistence/v1')");
        StringAssert.Contains(runner, "New-P2SyntheticFixture");
        StringAssert.Contains(runner, "Test-P2AssetLibraryAutomatedEvidence.ps1");
    }

    [TestMethod]
    public void FixtureGeneratorCreatesDeterministic512MetadataContract()
    {
        var runner = Read("tools/AssetLibraryP2AutomatedAcceptance/Invoke-P2AssetLibraryAutomatedAcceptance.ps1");
        ContainsAll(runner, "range(512)", "index >= 500", "asset-library-v16.db", "(512, 500, 12)",
            "AssetFolders", "AssetFolderMemberships", "AssetTags", "AssetTagMemberships", "SmartFolders", "SmartFolderRules",
            "uuid.uuid5", "user_source_read_count = 0", "user_source_write_count = 0");
    }

    [TestMethod]
    public void ValidatorIsReadOnlyAndChecksInputTreeFingerprint()
    {
        var validator = Read("tools/AssetLibraryP2AutomatedAcceptance/Test-P2AssetLibraryAutomatedEvidence.ps1");
        ContainsAll(validator, "$fingerprintBefore = Tree-Fingerprint $root", "$fingerprintAfter = Tree-Fingerprint $root",
            "input tree fingerprint", "ComputeHash", "FileMode", "runtime_database_count_after", "display settings unchanged");
        Assert.DoesNotContain("Get-FileHash", validator, StringComparison.Ordinal);
        foreach (var forbidden in new[] { "Set-Content", "Add-Content", "Out-File", "Remove-Item", "Move-Item", "Copy-Item", "WriteAllText", "WriteAllBytes" })
            Assert.IsFalse(validator.Contains(forbidden, StringComparison.OrdinalIgnoreCase), $"validator contains mutator {forbidden}");
    }

    [TestMethod]
    public void P2PackageDoesNotDriveDesktopOrEagle()
    {
        var package = string.Join("\n", Directory.GetFiles(Path("tools/AssetLibraryP2AutomatedAcceptance"), "*", SearchOption.AllDirectories).Select(File.ReadAllText));
        var driver = Read("src/PixelTart.Modules.AssetLibrary/AssetLibraryP2AutomatedAcceptanceDriver.cs");
        foreach (var forbidden in new[] { "SendInput(", "mouse_event(", "keybd_event(", "SetForegroundWindow(", "ChangeDisplaySettings", "AutomationElement", "InvokePattern", "Eagle.exe" })
        {
            Assert.IsFalse(package.Contains(forbidden, StringComparison.OrdinalIgnoreCase));
            Assert.IsFalse(driver.Contains(forbidden, StringComparison.OrdinalIgnoreCase));
        }
    }

    private static void ContainsAll(string text, params string[] values)
    {
        foreach (var value in values) StringAssert.Contains(text, value);
    }

    private static string Read(string relative) => File.ReadAllText(Path(relative));
    private static string Path(string relative) => System.IO.Path.Combine(RepositoryRoot(), relative.Replace('/', System.IO.Path.DirectorySeparatorChar));

    private static (int ExitCode, string Output, string Error) StartPowerShell(
        IEnumerable<string> arguments,
        string workingDirectory,
        IReadOnlyDictionary<string, string>? environment = null)
    {
        var start = new ProcessStartInfo("powershell.exe")
        {
            WorkingDirectory = workingDirectory,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };
        foreach (var argument in arguments) start.ArgumentList.Add(argument);
        if (environment is not null)
            foreach (var pair in environment) start.Environment[pair.Key] = pair.Value;
        using var process = Process.Start(start) ?? throw new InvalidOperationException("PowerShell process did not start.");
        var outputTask = process.StandardOutput.ReadToEndAsync();
        var errorTask = process.StandardError.ReadToEndAsync();
        process.WaitForExit();
        return (process.ExitCode, outputTask.GetAwaiter().GetResult(), errorTask.GetAwaiter().GetResult());
    }

    private static string TreeFingerprint(string root)
    {
        var fullRoot = System.IO.Path.GetFullPath(root).TrimEnd(System.IO.Path.DirectorySeparatorChar, System.IO.Path.AltDirectorySeparatorChar);
        var rows = Directory.EnumerateFiles(fullRoot, "*", SearchOption.AllDirectories)
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .Select(path =>
            {
                var relative = System.IO.Path.GetRelativePath(fullRoot, path).Replace(System.IO.Path.DirectorySeparatorChar, '/');
                var hash = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(path))).ToLowerInvariant();
                return $"{relative}|{new FileInfo(path).Length}|{hash}";
            });
        return Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(string.Join("\n", rows)))).ToLowerInvariant();
    }

    private sealed class TemporaryDirectory : IDisposable
    {
        public TemporaryDirectory(string prefix)
        {
            Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"{prefix}-{Guid.NewGuid():N}");
            Directory.CreateDirectory(Path);
        }

        public string Path { get; }

        public void Dispose()
        {
            try { Directory.Delete(Path, recursive: true); }
            catch { }
        }
    }

    private const string HandshakeProbeScript = """
$ErrorActionPreference = 'Stop'
$runner = Get-Content -LiteralPath $env:P2_RUNNER_PATH -Raw
$start = $runner.IndexOf('function Invoke-Validator', [StringComparison]::Ordinal)
$end = $runner.IndexOf('function Invoke-RecoveryTest', $start, [StringComparison]::Ordinal)
if ($start -lt 0 -or $end -le $start) { throw 'Could not extract Invoke-Validator.' }
$functionText = $runner.Substring($start, $end - $start)
$functionText = $functionText.Replace('$PSScriptRoot', "'$(Split-Path -Parent $env:P2_RUNNER_PATH)'")
function Test-PathWithin {
    param([string]$Path, [string]$Root)
    $fullPath = [IO.Path]::GetFullPath($Path).TrimEnd('\', '/')
    $fullRoot = [IO.Path]::GetFullPath($Root).TrimEnd('\', '/')
    return $fullPath -eq $fullRoot -or $fullPath.StartsWith($fullRoot + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)
}
Invoke-Expression $functionText
$script:stubCase = $env:P2_CASE
function Invoke-LoggedProcess {
    param([string]$FilePath, [string[]]$Arguments, [string]$Name, [string]$LogDirectory, [int]$Timeout)
    [IO.Directory]::CreateDirectory($LogDirectory) | Out-Null
    $stdout = Join-Path $LogDirectory ($Name + '.stdout.log')
    $stderr = Join-Path $LogDirectory ($Name + '.stderr.log')
    $payload = @{ schema = 'pixel-tart-p2-automated-validation-result/v1'; status = 'passed'; run_root = $env:P2_ROOT; source_head = $env:P2_HEAD } | ConvertTo-Json -Compress
    if ($script:stubCase -eq 'invalid-json') { $payload = 'not-json' }
    $errorText = if ($script:stubCase -eq 'stderr') { 'simulated validator warning' } else { '' }
    [IO.File]::WriteAllText($stdout, $payload)
    [IO.File]::WriteAllText($stderr, $errorText)
    [pscustomobject]@{ exit_code = 0; stdout = $stdout; stderr = $stderr }
}
$root = $env:P2_ROOT
try {
    [void](Invoke-Validator $root $env:P2_LOG 'probe')
    if ($env:P2_CASE -ne 'valid') { throw 'validator handshake unexpectedly accepted malformed output' }
    exit 0
}
catch {
    if ($env:P2_CASE -eq 'valid') { [Console]::Error.WriteLine(($_ | Out-String)); exit 1 }
    exit 0
}
""";

    private static string RepositoryRoot()
    {
        var cursor = new DirectoryInfo(AppContext.BaseDirectory);
        while (cursor is not null && !File.Exists(System.IO.Path.Combine(cursor.FullName, "RAWSelectionAssistant.sln"))) cursor = cursor.Parent;
        return cursor?.FullName ?? throw new DirectoryNotFoundException("Repository root not found.");
    }
}
