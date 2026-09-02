using System.Diagnostics;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace RAWSelectionAssistant.WpfTests;

[TestClass]
public sealed class AssetLibraryP3RunSealContractTests
{
    private const string Head = "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb";
    private static readonly string[] InputFiles =
    [
        "Invoke-P3AssetLibraryAutomatedAcceptance.ps1",
        "Test-P3AssetLibraryAutomatedEvidence.ps1",
        "Test-P3AssetLibraryAutomatedRunSet.ps1",
        "New-P3SyntheticFixture.py",
        "Invoke-P3NegativeEvidenceProofs.py",
        "automated-acceptance-contract.json",
        "README.md"
    ];

    [TestMethod]
    public void RunnerAndValidatorPinRunOwnedInputsSealInventoryAndReadOnlyState()
    {
        using var contract = JsonDocument.Parse(Read("tools/AssetLibraryP3AutomatedAcceptance/automated-acceptance-contract.json"));
        var root = contract.RootElement;
        Assert.AreEqual("pixel-tart-p3-acceptance-input-snapshot/v1", root.GetProperty("acceptance_input_snapshot_schema").GetString());
        Assert.AreEqual("runner/acceptance-inputs", root.GetProperty("acceptance_input_directory").GetString());
        CollectionAssert.AreEqual(InputFiles, root.GetProperty("required_acceptance_input_files").EnumerateArray().Select(item => item.GetString()).ToArray());
        Assert.AreEqual("pixel-tart-p3-run-seal/v1", root.GetProperty("run_seal_schema").GetString());
        Assert.AreEqual("runner/run-seal.json", root.GetProperty("run_seal_file").GetString());
        Assert.IsTrue(root.GetProperty("run_seal_inventory_excludes_seal_file").GetBoolean());
        Assert.IsTrue(root.GetProperty("run_seal_requires_read_only").GetBoolean());

        var runner = Read("tools/AssetLibraryP3AutomatedAcceptance/Invoke-P3AssetLibraryAutomatedAcceptance.ps1");
        ContainsAll(runner,
            "function New-AcceptanceInputSnapshot", "function Assert-AcceptanceInputSnapshot",
            "function Get-SealedAcceptanceInputPath", "function New-RunSeal",
            "function Get-RunTreeStateFingerprint", "$stateFingerprintBefore = Get-RunTreeStateFingerprint",
            "$stateFingerprintAfter = Get-RunTreeStateFingerprint",
            "runner\\acceptance-inputs", "copy_verified_before_execution", "files_read_only_before_execution",
            "inventory_excludes_seal_file", "Get-CanonicalFileTreeSha256",
            "ast.parse", "compile(t,str(p),\"exec\")",
            "$acceptanceInputs = New-AcceptanceInputSnapshot $activeRunRoot",
            "$fixture = New-P3SyntheticFixture $activeRunRoot $acceptanceInputs",
            "[void](New-RunSeal $activeRunRoot $runId $sourceHead)", "$runSealed = $true",
            "if (-not $runSealed)", "Get-SealedAcceptanceInputPath $targetRoot");
        AssertOrdered(runner,
            "$acceptanceInputs = New-AcceptanceInputSnapshot $activeRunRoot",
            "$fixture = New-P3SyntheticFixture $activeRunRoot $acceptanceInputs",
            "[void](New-RunSeal $activeRunRoot $runId $sourceHead)",
            "[void](Invoke-Validator $activeRunRoot $validatorLogDirectory)");

        var validator = Read("tools/AssetLibraryP3AutomatedAcceptance/Test-P3AssetLibraryAutomatedEvidence.ps1");
        ContainsAll(validator,
            "function Assert-SealedRun", "acceptance input recomputed tree hash",
            "executing sealed validator path", "run seal exact file inventory differs",
            "run seal recomputed tree hash", "run seal inventory file is not read-only",
            "$sealedRun = Assert-SealedRun $root $contract");
    }

    [TestMethod]
    public void NormalSealedValidatorRejectsHashReadOnlyAndInventoryMutationsBeforeEvidenceChecks()
    {
        var parent = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"pixel-tart-p3-seal-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(parent);
        try
        {
            var validSeal = CreateSealedRoot(parent, "valid");
            var validResult = InvokeValidator(validSeal);
            Assert.AreNotEqual(0, validResult.ExitCode);
            StringAssert.Contains(validResult.Output + validResult.Error, "manifest schema differs");

            var hashMutation = CreateSealedRoot(parent, "hash");
            var hashTarget = System.IO.Path.Combine(hashMutation, "runner", "acceptance-inputs", "README.md");
            MakeWritable(hashTarget);
            var mutatedBytes = File.ReadAllBytes(hashTarget);
            mutatedBytes[0] ^= 0x01;
            File.WriteAllBytes(hashTarget, mutatedBytes);
            MakeReadOnly(hashTarget);
            var hashResult = InvokeValidator(hashMutation);
            Assert.AreNotEqual(0, hashResult.ExitCode);
            StringAssert.Contains(hashResult.Output + hashResult.Error, "acceptance input hash 'README.md' differs");

            var writableMutation = CreateSealedRoot(parent, "writable");
            var writableTarget = System.IO.Path.Combine(writableMutation, "runner", "acceptance-inputs", "README.md");
            MakeWritable(writableTarget);
            var writableResult = InvokeValidator(writableMutation);
            Assert.AreNotEqual(0, writableResult.ExitCode);
            StringAssert.Contains(writableResult.Output + writableResult.Error, "acceptance input is not read-only");

            var inventoryMutation = CreateSealedRoot(parent, "inventory");
            var extra = System.IO.Path.Combine(inventoryMutation, "unsealed-extra.txt");
            File.WriteAllText(extra, "extra");
            MakeReadOnly(extra);
            var inventoryResult = InvokeValidator(inventoryMutation);
            Assert.AreNotEqual(0, inventoryResult.ExitCode);
            StringAssert.Contains(inventoryResult.Output + inventoryResult.Error, "run seal exact live inventory count differs");
        }
        finally
        {
            if (Directory.Exists(parent))
            {
                foreach (var file in Directory.GetFiles(parent, "*", SearchOption.AllDirectories)) MakeWritable(file);
                Directory.Delete(parent, recursive: true);
            }
        }
    }

    [TestMethod]
    public void ActualRunnerFunctionsCreateRunOwnedSnapshotAndReadOnlySealAcceptedByValidator()
    {
        var parent = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"pixel-tart-p3-runner-seal-{Guid.NewGuid():N}");
        var package = System.IO.Path.Combine(parent, "package");
        var runRoot = System.IO.Path.Combine(parent, "run");
        Directory.CreateDirectory(package);
        Directory.CreateDirectory(runRoot);
        try
        {
            foreach (var fileName in InputFiles)
                File.Copy(Path($"tools/AssetLibraryP3AutomatedAcceptance/{fileName}"), System.IO.Path.Combine(package, fileName));
            var runnerSource = File.ReadAllText(System.IO.Path.Combine(package, "Invoke-P3AssetLibraryAutomatedAcceptance.ps1"));
            var executionMarker = runnerSource.IndexOf("$script:repo = Get-RepositoryRoot", StringComparison.Ordinal);
            Assert.IsGreaterThan(0, executionMarker);
            var harness = runnerSource[..executionMarker] +
                """
                $snapshot = New-AcceptanceInputSnapshot $RunRoot
                $manifest = [ordered]@{
                    schema_version = 'intentionally-invalid-after-seal'
                    run_id = 'p3-runner-function-test'
                    source_head = 'bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb'
                    run_root = [IO.Path]::GetFullPath($RunRoot)
                    acceptance_inputs = $snapshot
                    run_seal = [ordered]@{
                        schema = 'pixel-tart-p3-run-seal/v1'
                        path = 'runner/run-seal.json'
                        inventory_excludes_seal_file = $true
                        read_only_required = $true
                    }
                }
                Write-JsonAtomic (Join-Path $RunRoot 'run-manifest.json') $manifest
                [IO.File]::WriteAllText((Join-Path $RunRoot 'evidence.txt'), 'evidence', [Text.UTF8Encoding]::new($false))
                [void](Assert-AcceptanceInputSnapshot $snapshot $RunRoot)
                $seal = New-RunSeal $RunRoot $manifest.run_id $manifest.source_head
                $files = @(Get-ChildItem -LiteralPath $RunRoot -Recurse -Force -File)
                [pscustomobject]@{
                    snapshot_file_count = [int]$snapshot.file_count
                    seal_file_count = [int]$seal.file_count
                    live_file_count = $files.Count
                    readonly_file_count = @($files | Where-Object { ($_.Attributes -band [IO.FileAttributes]::ReadOnly) -ne 0 }).Count
                } | ConvertTo-Json -Compress
                """;
            var harnessPath = System.IO.Path.Combine(package, "SealHarness.ps1");
            File.WriteAllText(harnessPath, harness, new UTF8Encoding(false));

            var result = Start("powershell.exe",
                ["-NoProfile", "-NonInteractive", "-ExecutionPolicy", "Bypass", "-File", harnessPath, "-RunRoot", runRoot]);
            Assert.AreEqual(0, result.ExitCode, result.Output + result.Error);
            using var payload = JsonDocument.Parse(result.Output);
            Assert.AreEqual(InputFiles.Length, payload.RootElement.GetProperty("snapshot_file_count").GetInt32());
            Assert.AreEqual(InputFiles.Length + 2, payload.RootElement.GetProperty("seal_file_count").GetInt32());
            Assert.AreEqual(InputFiles.Length + 3, payload.RootElement.GetProperty("live_file_count").GetInt32());
            Assert.AreEqual(InputFiles.Length + 3, payload.RootElement.GetProperty("readonly_file_count").GetInt32());

            var validation = InvokeValidator(runRoot);
            Assert.AreNotEqual(0, validation.ExitCode);
            StringAssert.Contains(validation.Output + validation.Error, "manifest schema differs");
        }
        finally
        {
            if (Directory.Exists(parent))
            {
                foreach (var file in Directory.GetFiles(parent, "*", SearchOption.AllDirectories)) MakeWritable(file);
                Directory.Delete(parent, recursive: true);
            }
        }
    }

    private static string CreateSealedRoot(string parent, string suffix)
    {
        var runRoot = System.IO.Path.Combine(parent, $"run-{suffix}");
        var inputDirectory = System.IO.Path.Combine(runRoot, "runner", "acceptance-inputs");
        Directory.CreateDirectory(inputDirectory);
        foreach (var fileName in InputFiles)
            File.Copy(Path($"tools/AssetLibraryP3AutomatedAcceptance/{fileName}"), System.IO.Path.Combine(inputDirectory, fileName));

        var inputRows = Rows(inputDirectory);
        var runId = $"p3-seal-{suffix}";
        var manifest = new
        {
            schema_version = "intentionally-invalid-after-seal",
            run_id = runId,
            source_head = Head,
            run_root = runRoot,
            acceptance_inputs = new
            {
                schema = "pixel-tart-p3-acceptance-input-snapshot/v1",
                source_directory = Path("tools/AssetLibraryP3AutomatedAcceptance"),
                directory = inputDirectory,
                file_count = inputRows.Length,
                copy_verified_before_execution = true,
                files_read_only_before_execution = true,
                tree_sha256 = TreeHash(inputRows),
                files = inputRows
            },
            run_seal = new
            {
                schema = "pixel-tart-p3-run-seal/v1",
                path = "runner/run-seal.json",
                inventory_excludes_seal_file = true,
                read_only_required = true
            }
        };
        File.WriteAllText(System.IO.Path.Combine(runRoot, "run-manifest.json"), JsonSerializer.Serialize(manifest), new UTF8Encoding(false));

        var inventoryRows = Rows(runRoot);
        var seal = new
        {
            schema = "pixel-tart-p3-run-seal/v1",
            run_root = runRoot,
            run_id = runId,
            source_head = Head,
            sealed_at = "2026-09-02T00:00:00+00:00",
            seal_file = "runner/run-seal.json",
            inventory_excludes_seal_file = true,
            read_only_required = true,
            file_count = inventoryRows.Length,
            tree_sha256 = TreeHash(inventoryRows),
            files = inventoryRows
        };
        File.WriteAllText(System.IO.Path.Combine(runRoot, "runner", "run-seal.json"), JsonSerializer.Serialize(seal), new UTF8Encoding(false));
        foreach (var file in Directory.GetFiles(runRoot, "*", SearchOption.AllDirectories)) MakeReadOnly(file);
        return runRoot;
    }

    private static InventoryRow[] Rows(string root) => Directory.GetFiles(root, "*", SearchOption.AllDirectories)
        .Select(path => new InventoryRow(
            System.IO.Path.GetRelativePath(root, path).Replace('\\', '/'),
            new FileInfo(path).Length,
            Hash(path)))
        .OrderBy(row => row.path, StringComparer.Ordinal)
        .ToArray();

    private static string TreeHash(IEnumerable<InventoryRow> rows)
    {
        var canonical = string.Join('\n', rows.OrderBy(row => row.path, StringComparer.Ordinal)
            .Select(row => $"{row.path}|{row.byte_length}|{row.sha256}"));
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical))).ToLowerInvariant();
    }

    private static string Hash(string path) => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))).ToLowerInvariant();
    private static void MakeReadOnly(string path) => File.SetAttributes(path, File.GetAttributes(path) | FileAttributes.ReadOnly);
    private static void MakeWritable(string path) => File.SetAttributes(path, File.GetAttributes(path) & ~FileAttributes.ReadOnly);

    private static (int ExitCode, string Output, string Error) InvokeValidator(string runRoot)
    {
        var validator = System.IO.Path.Combine(runRoot, "runner", "acceptance-inputs", "Test-P3AssetLibraryAutomatedEvidence.ps1");
        return Start("powershell.exe",
            ["-NoProfile", "-NonInteractive", "-ExecutionPolicy", "Bypass", "-File", validator, "-RunRoot", runRoot]);
    }

    private static void ContainsAll(string text, params string[] values)
    {
        foreach (var value in values) StringAssert.Contains(text, value);
    }

    private static void AssertOrdered(string text, params string[] values)
    {
        var offset = 0;
        foreach (var value in values)
        {
            var index = text.IndexOf(value, offset, StringComparison.Ordinal);
            Assert.IsGreaterThanOrEqualTo(offset, index, $"Missing or out-of-order: {value}");
            offset = index + value.Length;
        }
    }

    private static (int ExitCode, string Output, string Error) Start(string fileName, IEnumerable<string> arguments)
    {
        var info = new ProcessStartInfo(fileName)
        {
            WorkingDirectory = RepositoryRoot(), UseShellExecute = false, CreateNoWindow = true,
            RedirectStandardOutput = true, RedirectStandardError = true
        };
        foreach (var argument in arguments) info.ArgumentList.Add(argument);
        using var process = Process.Start(info) ?? throw new InvalidOperationException($"Could not start {fileName}.");
        var output = process.StandardOutput.ReadToEndAsync();
        var error = process.StandardError.ReadToEndAsync();
        process.WaitForExit();
        return (process.ExitCode, output.GetAwaiter().GetResult(), error.GetAwaiter().GetResult());
    }

    private static string Read(string relative) => File.ReadAllText(Path(relative));
    private static string Path(string relative) => System.IO.Path.Combine(RepositoryRoot(), relative.Replace('/', System.IO.Path.DirectorySeparatorChar));
    private static string RepositoryRoot()
    {
        var cursor = new DirectoryInfo(AppContext.BaseDirectory);
        while (cursor is not null && !File.Exists(System.IO.Path.Combine(cursor.FullName, "RAWSelectionAssistant.sln"))) cursor = cursor.Parent;
        return cursor?.FullName ?? throw new DirectoryNotFoundException("Repository root not found.");
    }

    private sealed record InventoryRow(string path, long byte_length, string sha256);
}
