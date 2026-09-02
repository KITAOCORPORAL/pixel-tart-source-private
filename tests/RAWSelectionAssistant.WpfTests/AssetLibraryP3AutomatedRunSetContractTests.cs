using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.Json;

namespace RAWSelectionAssistant.WpfTests;

[TestClass]
public sealed class AssetLibraryP3AutomatedRunSetContractTests
{
    private const string Head = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";

    [TestMethod]
    public void RunSetScriptPinsThreeValidatedDisjointRunsAndExternalOutput()
    {
        var script = Read("tools/AssetLibraryP3AutomatedAcceptance/Test-P3AssetLibraryAutomatedRunSet.ps1");
        ContainsAll(script,
            "$RunRoots.Count -ne 3", "exactly 3 run roots are required",
            "Test-P3AssetLibraryAutomatedEvidence.ps1", "Invoke-NormalValidator",
            "runner\\acceptance-inputs\\Test-P3AssetLibraryAutomatedEvidence.ps1",
            "sealed normal validator is missing", "$validatorHashes.Count -ne 1",
            "pixel-tart-p3-automated-validation-result/v1", "the three runs do not use the same HEAD",
            "run_id is reused across the three runs", "process identity is reused across the three runs",
            "process_session_id is reused across the three runs", "evidence file path is reused across runs",
            "OutputDirectory must be outside every run root", "pixel-tart-p3-automated-run-set/v1");

        var escapedPath = Path("tools/AssetLibraryP3AutomatedAcceptance/Test-P3AssetLibraryAutomatedRunSet.ps1")
            .Replace("'", "''", StringComparison.Ordinal);
        var parse = Start("powershell.exe",
            ["-NoProfile", "-NonInteractive", "-Command",
                $"$t=$null;$e=$null;[Management.Automation.Language.Parser]::ParseFile('{escapedPath}',[ref]$t,[ref]$e)|Out-Null;if(@($e).Count){{$e|% Message;exit 1}}"]);
        Assert.AreEqual(0, parse.ExitCode, parse.Output + parse.Error);
    }

    [TestMethod]
    public void RunSetScriptWritesPassedSummaryOutsideAllThreeRunRoots()
    {
        using var package = FakePackage();
        var roots = Enumerable.Range(1, 3).Select(index => CreateRun(package.Root, index)).ToArray();
        var output = System.IO.Path.Combine(package.Root, "aggregate-output");

        var result = Invoke(package.ScriptPath, roots, output);

        Assert.AreEqual(0, result.ExitCode, result.Output + result.Error);
        using var stdout = JsonDocument.Parse(result.Output);
        var payload = stdout.RootElement;
        Assert.AreEqual("pixel-tart-p3-automated-run-set/v1", payload.GetProperty("schema").GetString());
        Assert.AreEqual("passed", payload.GetProperty("status").GetString());
        Assert.AreEqual(3, payload.GetProperty("run_count").GetInt32());
        Assert.AreEqual(3, payload.GetProperty("process_identity_count").GetInt32());
        Assert.AreEqual(3, payload.GetProperty("process_session_count").GetInt32());
        Assert.IsTrue(payload.GetProperty("evidence_paths_disjoint").GetBoolean());
        var summaryPath = payload.GetProperty("summary_path").GetString()!;
        Assert.IsTrue(File.Exists(summaryPath));
        Assert.IsTrue(IsInside(summaryPath, output));
        foreach (var root in roots) Assert.IsFalse(IsInside(summaryPath, root));
        using var persisted = JsonDocument.Parse(File.ReadAllText(summaryPath));
        Assert.AreEqual(Head, persisted.RootElement.GetProperty("source_head").GetString());
        Assert.AreEqual(3, persisted.RootElement.GetProperty("runs").GetArrayLength());
    }

    [TestMethod]
    public void RunSetScriptRejectsWrongCountInternalOutputAndCrossRunIdentityReuse()
    {
        using var package = FakePackage();
        var roots = Enumerable.Range(1, 3).Select(index => CreateRun(package.Root, index)).ToArray();
        var output = System.IO.Path.Combine(package.Root, "aggregate-output");

        var wrongCount = Invoke(package.ScriptPath, roots[..2], output);
        Assert.AreNotEqual(0, wrongCount.ExitCode);
        StringAssert.Contains(wrongCount.Output + wrongCount.Error, "exactly 3 run roots are required");

        var internalOutput = Invoke(package.ScriptPath, roots, System.IO.Path.Combine(roots[0], "forbidden-output"));
        Assert.AreNotEqual(0, internalOutput.ExitCode);
        StringAssert.Contains(internalOutput.Output + internalOutput.Error, "OutputDirectory must be outside every run root");

        RewriteManifestIdentity(roots[2], runId: "p3-run-2", processSessionId: null);
        var reusedRun = Invoke(package.ScriptPath, roots, output);
        Assert.AreNotEqual(0, reusedRun.ExitCode);
        StringAssert.Contains(reusedRun.Output + reusedRun.Error, "run_id is reused across the three runs");

        RewriteManifestIdentity(roots[2], runId: "p3-run-3", processSessionId: $"{2:x32}");
        RewriteSummaryIdentity(roots[2], "p3-run-3");
        var reusedSession = Invoke(package.ScriptPath, roots, output);
        Assert.AreNotEqual(0, reusedSession.ExitCode);
        StringAssert.Contains(reusedSession.Output + reusedSession.Error, "process_session_id is reused across the three runs");
    }

    private static TemporaryPackage FakePackage()
    {
        var root = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"pixel-tart-p3-run-set-{Guid.NewGuid():N}");
        var tools = System.IO.Path.Combine(root, "tools");
        Directory.CreateDirectory(tools);
        var scriptPath = System.IO.Path.Combine(tools, "Test-P3AssetLibraryAutomatedRunSet.ps1");
        File.Copy(Path("tools/AssetLibraryP3AutomatedAcceptance/Test-P3AssetLibraryAutomatedRunSet.ps1"), scriptPath);
        File.WriteAllText(System.IO.Path.Combine(tools, "Test-P3AssetLibraryAutomatedEvidence.ps1"),
            """
            [CmdletBinding()]
            param([Parameter(Mandatory = $true)][string]$RunRoot)
            $manifest = Get-Content -LiteralPath (Join-Path $RunRoot 'run-manifest.json') -Raw -Encoding UTF8 | ConvertFrom-Json
            [pscustomobject]@{
                schema = 'pixel-tart-p3-automated-validation-result/v1'
                status = 'passed'
                negative_proofs_skipped = $false
                run_root = [IO.Path]::GetFullPath($RunRoot).TrimEnd('\','/')
                run_id = [string]$manifest.run_id
                source_head = [string]$manifest.source_head
            } | ConvertTo-Json -Compress
            """, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        return new TemporaryPackage(root, scriptPath);
    }

    private static string CreateRun(string packageRoot, int index)
    {
        var root = System.IO.Path.Combine(packageRoot, $"run-{index}");
        var evidence = System.IO.Path.Combine(root, "app", "evidence");
        var logs = System.IO.Path.Combine(root, "logs");
        var plans = System.IO.Path.Combine(root, "plans");
        var inputs = System.IO.Path.Combine(root, "runner", "acceptance-inputs");
        Directory.CreateDirectory(evidence);
        Directory.CreateDirectory(logs);
        Directory.CreateDirectory(plans);
        Directory.CreateDirectory(inputs);
        File.Copy(System.IO.Path.Combine(packageRoot, "tools", "Test-P3AssetLibraryAutomatedEvidence.ps1"),
            System.IO.Path.Combine(inputs, "Test-P3AssetLibraryAutomatedEvidence.ps1"));
        var files = new Dictionary<string, string>
        {
            [System.IO.Path.Combine(logs, "session.stdout.log")] = "stdout",
            [System.IO.Path.Combine(logs, "session.stderr.log")] = string.Empty,
            [System.IO.Path.Combine(plans, "session.json")] = "{}",
            [System.IO.Path.Combine(evidence, "phase.json")] = "{}",
            [System.IO.Path.Combine(evidence, "events.ndjson")] = "{}\n",
            [System.IO.Path.Combine(evidence, "summary.ndjson")] = "{}\n",
            [System.IO.Path.Combine(evidence, "artifact.json")] = "{}",
            [System.IO.Path.Combine(evidence, "screen.png")] = "png",
            [System.IO.Path.Combine(evidence, "bounds.json")] = "{}",
            [System.IO.Path.Combine(evidence, "database.db")] = "db"
        };
        foreach (var (path, content) in files) File.WriteAllText(path, content);

        var runId = $"p3-run-{index}";
        var processSessionId = $"{index:x32}";
        File.WriteAllText(System.IO.Path.Combine(root, "run-manifest.json"), JsonSerializer.Serialize(new
        {
            run_root = root,
            run_id = runId,
            source_head = Head,
            sessions = new[]
            {
                new
                {
                    process_session_id = processSessionId,
                    pid = 5000 + index,
                    hwnd = $"0x{6000 + index:x}",
                    started_at = $"2026-09-02T00:00:0{index}+00:00",
                    stdout = System.IO.Path.Combine(logs, "session.stdout.log"),
                    stderr = System.IO.Path.Combine(logs, "session.stderr.log"),
                    plan_path = System.IO.Path.Combine(plans, "session.json"),
                    phase_summary_path = System.IO.Path.Combine(evidence, "phase.json")
                }
            }
        }));
        WriteSummary(root, runId);
        return root;
    }

    private static void WriteSummary(string root, string runId)
    {
        File.WriteAllText(System.IO.Path.Combine(root, "app", "evidence", "summary.json"), JsonSerializer.Serialize(new
        {
            run_id = runId,
            source_head = Head,
            artifacts = new[] { new { path = "app/evidence/artifact.json" } },
            scenarios = new[]
            {
                new
                {
                    screenshot_paths = new[] { "app/evidence/screen.png" },
                    bounds_paths = new[] { "app/evidence/bounds.json" },
                    database = new { evidence_paths = new[] { "app/evidence/database.db" } }
                }
            }
        }));
    }

    private static void RewriteManifestIdentity(string root, string runId, string? processSessionId)
    {
        var path = System.IO.Path.Combine(root, "run-manifest.json");
        using var original = JsonDocument.Parse(File.ReadAllText(path));
        var session = original.RootElement.GetProperty("sessions")[0];
        File.WriteAllText(path, JsonSerializer.Serialize(new
        {
            run_root = root,
            run_id = runId,
            source_head = Head,
            sessions = new[]
            {
                new
                {
                    process_session_id = processSessionId ?? session.GetProperty("process_session_id").GetString(),
                    pid = session.GetProperty("pid").GetInt32(),
                    hwnd = session.GetProperty("hwnd").GetString(),
                    started_at = session.GetProperty("started_at").GetString(),
                    stdout = session.GetProperty("stdout").GetString(),
                    stderr = session.GetProperty("stderr").GetString(),
                    plan_path = session.GetProperty("plan_path").GetString(),
                    phase_summary_path = session.GetProperty("phase_summary_path").GetString()
                }
            }
        }));
        RewriteSummaryIdentity(root, runId);
    }

    private static void RewriteSummaryIdentity(string root, string runId) => WriteSummary(root, runId);

    private static (int ExitCode, string Output, string Error) Invoke(string script, string[] roots, string output)
    {
        var rootArray = string.Join(',', roots.Select(root => $"'{Escape(root)}'"));
        var command = $"& '{Escape(script)}' -RunRoots @({rootArray}) -OutputDirectory '{Escape(output)}'";
        return Start("powershell.exe", ["-NoProfile", "-NonInteractive", "-ExecutionPolicy", "Bypass", "-Command", command]);
    }

    private static bool IsInside(string path, string parent)
    {
        var fullPath = System.IO.Path.GetFullPath(path).TrimEnd(System.IO.Path.DirectorySeparatorChar);
        var fullParent = System.IO.Path.GetFullPath(parent).TrimEnd(System.IO.Path.DirectorySeparatorChar);
        return fullPath.Equals(fullParent, StringComparison.OrdinalIgnoreCase) ||
               fullPath.StartsWith(fullParent + System.IO.Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
    }

    private static string Escape(string value) => value.Replace("'", "''", StringComparison.Ordinal);
    private static void ContainsAll(string text, params string[] values)
    {
        foreach (var value in values) StringAssert.Contains(text, value);
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

    private sealed class TemporaryPackage(string root, string scriptPath) : IDisposable
    {
        public string Root { get; } = root;
        public string ScriptPath { get; } = scriptPath;
        public void Dispose()
        {
            if (Directory.Exists(Root)) Directory.Delete(Root, recursive: true);
        }
    }
}
