using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.Json;

namespace RAWSelectionAssistant.WpfTests;

[TestClass]
public sealed class AssetLibraryP1ManualPacketV2ContractTests
{
    private const string RelativePacketDirectory = "artifacts/manual-acceptance/P1_ASSET_LIBRARY_GATE_A_MANUAL_PACKET";
    private const string EntryName = "Invoke-P1AssetLibraryGateAManualAcceptance.ps1";

    [TestMethod]
    public void PacketDirectoryHasOneBomEncodedPowerShellEntryAndNoBat()
    {
        var directory = Path(RelativePacketDirectory);
        var files = Directory.GetFiles(directory);
        Assert.HasCount(1, files);
        Assert.AreEqual(EntryName, System.IO.Path.GetFileName(files[0]));
        Assert.HasCount(0, Directory.GetFiles(directory, "*.bat"));

        var bytes = File.ReadAllBytes(files[0]);
        CollectionAssert.AreEqual(Encoding.UTF8.Preamble.ToArray(), bytes[..Encoding.UTF8.Preamble.Length].ToArray());
    }

    [TestMethod]
    public void RunUsesBackgroundStateGatesWithoutTerminalAcknowledgementOrSyntheticInput()
    {
        var script = Text();
        Assert.DoesNotContain("Read-Host", script, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("SetForegroundWindow", script, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("SendInput", script, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("explorer.exe", script, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("IsPathFullyQualified", script, StringComparison.Ordinal);
        ContainsAll(
            script,
            "function Test-WindowsAbsolutePath",
            "function Wait-ForStep",
            "function Wait-ForStableEmptyProcessSnapshot",
            "function Wait-ForNoExistingDevPreview",
            "Get-DevPreviewProcessSnapshot",
            "Get-CimInstance -ClassName Win32_Process",
            "-OperationTimeoutSec 2",
            "Wait-ForNoExistingDevPreview -Context \"关闭 PID $($Session.Process.Id) 后\"",
            "Wait-ForNoExistingDevPreview -Context \"启动 $expectedProcessName 前\"",
            "全局进程表已连续稳定清零",
            "$window.Foreground",
            "$ForegroundStableMilliseconds",
            "Capture-AssetLibraryP1WindowEvidence.ps1",
            "-CaptureMethod', 'ScreenPixels'",
            "loading-barrier-waiting",
            "error-visible",
            "RetryAssetLibraryLoad",
            "Test-StateTimelineComplete",
            "Get-QualifiedRetryActivations",
            "Get-RetrySessionContamination",
            "retry-error-ready",
            "尚未触发 Retry，尚未导入素材",
            "两种方式只能选一种",
            "完成后不要切回 PowerShell 或聊天",
            "本轮已失去 synthetic-only 资格",
            "Write-NewUtf8NoBom $releaseFile 'release'");
        Assert.DoesNotContain("retry-error-focused", script, StringComparison.Ordinal);
    }

    [TestMethod]
    public void RunUsesDedicatedBuildDynamicHeadAndExactFourSessionIdentityRecords()
    {
        var script = Text();
        ContainsAll(
            script,
            "Get-CurrentSourceHead",
            "git -C $repositoryRoot",
            "--porcelain=v1",
            "--untracked-files=all",
            "worktree has non-ignored tracked or untracked changes",
            "Assert-TrackedCleanAndHead",
            "-p:ModularHarnessDevPreview=true",
            "-p:AssetLibraryP1StateAcceptance=true",
            "-p:InputRoutingDiagnostics=true",
            "-p:TreatWarningsAsErrors=true",
            "-p:UseSharedCompilation=false",
            "Invoke-WithEnvironment @{ MSBUILDDISABLENODEREUSE = '1' }",
            "msbuild_node_reuse_disabled = $true",
            "shared_compilation_disabled = $true",
            "pixel-tart-p1-gate-a-build-manifest/v1",
            "source_head_is_current_head",
            "repository_tracked_clean",
            "dedicated_build_succeeded",
            "informational_version",
            "first-empty-session",
            "retry-session",
            "keyboard-session",
            "restart-dpi-session");
        Assert.DoesNotContain("3b5ff13bb4c5b4c2001f978cb6ab31f5715cd7af", script, StringComparison.Ordinal);
    }

    [TestMethod]
    public void DedicatedBuildDisablesNodeAndCompilerReuseInsideWaitedProcess()
    {
        var script = Text();
        var hiddenProcess = Slice(script, "function Invoke-HiddenProcess", "function Invoke-WithEnvironment");
        var dedicatedBuild = Slice(script, "function Invoke-DedicatedBuild", "function Get-StateSessionData");
        const string exactOverride = "MSBUILDDISABLENODEREUSE = '1'";

        ContainsAll(
            hiddenProcess,
            "Start-Process -FilePath $FilePath",
            "-PassThru -Wait",
            "-RedirectStandardOutput $StdOutPath",
            "-RedirectStandardError $StdErrPath",
            "return [int]$process.ExitCode");
        ContainsAll(
            dedicatedBuild,
            "$exitCode = Invoke-WithEnvironment @{ MSBUILDDISABLENODEREUSE = '1' }",
            "Invoke-HiddenProcess $dotnet $arguments",
            "if ($exitCode -ne 0)",
            "-p:UseSharedCompilation=false",
            "msbuild_node_reuse_disabled = $true",
            "shared_compilation_disabled = $true");
        Assert.AreEqual(1, CountOccurrences(script, exactOverride));
        Assert.AreEqual(1, CountOccurrences(script, "-p:UseSharedCompilation=false"));
        Assert.DoesNotContain("-nodeReuse:false", dedicatedBuild, StringComparison.OrdinalIgnoreCase);
    }

    [TestMethod]
    public void CaptureDelegatesBoundedForegroundRestabilizationToOneStrictHelperInvocation()
    {
        var script = Text();
        var capture = Slice(script, "function Capture-WindowEvidence", "function Get-NewTransitionWidth");

        ContainsAll(
            capture,
            "'-ExpectedWindowHwnd', $Session.Hwnd",
            "'-PreCaptureTimeoutSeconds', [string]$StepTimeoutSeconds",
            "'-PreCaptureStableMilliseconds', [string]$ForegroundStableMilliseconds",
            "只读等待同一窗口恢复前台并连续稳定；不会自动抢焦点",
            "Invoke-HiddenProcess $shellPath $arguments",
            "if ($exitCode -ne 0)",
            "Get-NestedValue $record @('expected', 'window_hwnd')",
            "Get-NestedValue $record @('window_after_capture', 'hwnd')",
            "Get-NestedValue $record @('verification', 'passed')",
            "Test-PropertyPresent $record 'ui_input_generated'",
            "Test-PropertyPresent $record 'synthetic_ui_events_generated'",
            "Test-PropertyPresent $preCaptureGate 'passed'",
            "Test-PropertyPresent $preCaptureGate 'exact_original_hwnd_required'",
            "Test-PropertyPresent $preCaptureGate 'ui_activation_attempted'",
            "Get-PropertyValue $preCaptureGate 'timeout_seconds') -ne $StepTimeoutSeconds",
            "Get-PropertyValue $preCaptureGate 'required_stable_milliseconds') -ne $ForegroundStableMilliseconds");
        Assert.AreEqual(1, CountOccurrences(capture, "Invoke-HiddenProcess $shellPath $arguments"));
        Assert.DoesNotContain("Remove-Item", capture, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("SetForegroundWindow", capture, StringComparison.OrdinalIgnoreCase);
    }

    [TestMethod]
    public void CaptureBoundStateGatesLeaveForegroundStabilityToTheStrictHelper()
    {
        var script = Text();
        var waitForStep = Slice(script, "function Wait-ForStep", "function Wait-ForExpectedClose");

        ContainsAll(
            waitForStep,
            "[bool]$RequireWindow = $true",
            "[bool]$RequireForeground = $true",
            "Get-WindowObservation $Session.Process.Id",
            "if ($RequireForeground) { $windowReady = $windowReady -and $window.Foreground }",
            "[string]::Equals($window.Path, $script:resolvedExecutable, [StringComparison]::OrdinalIgnoreCase)",
            "$window.Hwnd -ceq $Session.Hwnd",
            "-not $window.Minimized");

        foreach (var step in new[]
                 {
                     "Wait-ForStep 'first-empty-ready'",
                     "Wait-ForStep 'retry-loading-held'",
                     "Wait-ForStep 'retry-error-ready'",
                     "Wait-ForStep 'retry-activate-once'",
                     "Wait-ForStep 'regular-direct-import'",
                     "Wait-ForStep 'thumbnail-right'",
                     "Wait-ForStep 'restart-restored'",
                     "Wait-ForStep \"dpi-$tupleIndex-observe\"",
                     "Wait-ForStep \"dpi-$tupleIndex-key\"",
                     "Wait-ForStep 'restore-final-foreground'"
                 })
        {
            var start = script.IndexOf(step, StringComparison.Ordinal);
            Assert.IsGreaterThanOrEqualTo(0, start, $"Missing state gate: {step}");
            var probe = script.IndexOf("-Probe {", start, StringComparison.Ordinal);
            Assert.IsGreaterThan(start, probe, $"Missing probe for state gate: {step}");
            var invocation = script[start..probe];
            StringAssert.Contains(invocation, "-RequireForeground:$false", $"{step} must delegate foreground stability to capture helper.");
            Assert.DoesNotContain("-RequireWindow:$false", invocation, StringComparison.Ordinal);
        }
    }

    [TestMethod]
    public void LoadingEvidenceMustCompleteBeforeTheRetryReleaseGateIsWritten()
    {
        var script = Text();
        var loadingCapture = script.IndexOf("Capture-WindowEvidence $retry '09-loading-manual-v2'", StringComparison.Ordinal);
        var releaseWrite = script.IndexOf("Write-NewUtf8NoBom $releaseFile 'release'", StringComparison.Ordinal);

        Assert.IsGreaterThanOrEqualTo(0, loadingCapture);
        Assert.IsGreaterThan(loadingCapture, releaseWrite);
    }

    [TestMethod]
    public void RegularAndRestartSessionsUseAcceptanceRouteWithoutPrimaryNavigationClick()
    {
        var script = Text();
        ContainsAll(
            script,
            "PIXEL_TART_ASSET_LIBRARY_P1_START_ROUTE = 'asset-library'",
            "PIXEL_TART_ASSET_LIBRARY_P1_HEAD = $SourceHead",
            "PIXEL_TART_ASSET_LIBRARY_P1_STATE_ACCEPTANCE",
            "AssetLibraryP1RouteAcceptance\\current-route-session.json",
            "Test-RouteSessionApplied",
            "Test-SyntheticImportComplete",
            "普通验收直达");
        Assert.DoesNotContain("AssetLibraryNavigationButton", script, StringComparison.Ordinal);
    }

    [TestMethod]
    public void DpiPathObservesRealDisplayAndKeyboardTransitionWithoutChangingDisplay()
    {
        var script = Text();
        ContainsAll(
            script,
            "EnumDisplaySettingsExW",
            "GetScaleFactorForMonitor",
            "GetDpiForWindow",
            "Get-NewKeyTransitionMatches",
            "layer1_win32_confirmed",
            "layer2_wpf_confirmed",
            "layer3_target_confirmed",
            "layer4_action_confirmed",
            "settings_write_back_confirmed",
            "$startedAt -gt $defaultAt",
            "Invoke-RecoveryCheck",
            "display_restored");
        Assert.DoesNotContain("ChangeDisplaySettings", script, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("SetDisplayConfig", script, StringComparison.OrdinalIgnoreCase);
    }

    [TestMethod]
    public void DragStepsConsumeTheExactMouseDragKindProducedAndValidatedByTheEvidenceContract()
    {
        var script = Text();
        var dragGate = Slice(script, "function Wait-DragStep", "function Wait-PaneToggleStep");
        var producer = File.ReadAllText(Path("src/RAWSelectionAssistant/MainWindow.PhysicalPointerDiagnostics.cs"), Encoding.UTF8);
        var diagnosticService = File.ReadAllText(Path("src/RAWSelectionAssistant/Services/PhysicalPointerDiagnosticSession.cs"), Encoding.UTF8);
        var validator = File.ReadAllText(Path("tools/AssetLibraryP1Acceptance/Test-AssetLibraryP1GateAEvidence.ps1"), Encoding.UTF8);

        StringAssert.Contains(dragGate, "[string](Get-PropertyValue $_ 'input_kind') -ceq 'MouseDrag'");
        Assert.DoesNotContain("-ceq 'Mouse'", dragGate, StringComparison.Ordinal);
        StringAssert.Contains(producer, "BeginControlStateTransition(state, \"MouseDrag\"");
        StringAssert.Contains(producer, "_pendingControlStateTransition?.InputKind != \"MouseDrag\"");
        StringAssert.Contains(diagnosticService, "safeInputKind == \"MouseDrag\"");
        StringAssert.Contains(validator, "(Test-ExactString (Get-PropertyValue $_ 'input_kind') 'MouseDrag')");
    }

    [TestMethod]
    public void RetryEnterGateUsesNativeUpFinalizationAndOneFixedAttemptTwoWriteWindow()
    {
        var script = Text();
        var localKeyValidator = Slice(script, "function Test-KeyLayersLocal", "function Get-QualifiedRetryActivations");
        var retryStep = Slice(script, "Wait-ForStep 'retry-activate-once'", "Capture-WindowEvidence $retry '11-retry-recovered-manual-v2'");

        ContainsAll(localKeyValidator,
            "Get-PropertyState",
            "Test-StrictScalarPropertyValue",
            "activation_completed_on_key_down",
            "activation_finalized_at_native_key_up",
            "target_available_at_native_key_up",
            "actual_focused_automation_id_at_native_key_up",
            "actual_focused_element_is_original_target_at_native_key_up",
            "actual_focused_element_available_at_native_key_up",
            "$focusedElementAvailableAtUp",
            "$focusedElementUnavailableAtUp",
            "$focusStateAtUpIsValid",
            "$focusedElementSnapshotStateAtUp.IsArray",
            "$focusedIdStateAtUp.IsArray",
            "$focusedElementIdStateAtUp.IsArray",
            "'PreviewKeyDown'; 'KeyDown'");
        var retryQualification = Slice(script, "function Get-QualifiedRetryActivations", "function Get-RetrySessionContamination");
        ContainsAll(retryQualification,
            "$allowedKeys.Count -eq 1",
            "native_key_up_focus_policy",
            "Test-StrictScalarPropertyValue $keyboardContract 'native_key_up_focus_policy'",
            "if ($usesKeyDownNativeUpFinalization)",
            "Test-KeyLayersLocal $attempt 'RetryAssetLibraryLoad' $key $true");
        ContainsAll(retryStep,
            "只按一次 Enter",
            "$attemptThreeOrLater",
            "$attemptTwoAt = [DateTimeOffset]::MaxValue",
            "([DateTimeOffset]::Now - $attemptTwoAt).TotalSeconds -le 3");
        Assert.DoesNotContain("Enter 或 Space", retryStep, StringComparison.Ordinal);
        Assert.DoesNotContain("updated_at", retryStep, StringComparison.Ordinal);
    }

    [TestMethod]
    public async Task DryRunAndRecoveryTestAreExecutableAndIsolated()
    {
        var before = Process.GetProcessesByName("PixelTart_ModularHarness_V1_DevPreview").Length;
        var dry = await InvokeModeAsync("DryRun");
        var recovery = await InvokeModeAsync("RecoveryTest");
        var after = Process.GetProcessesByName("PixelTart_ModularHarness_V1_DevPreview").Length;

        Assert.AreEqual(0, dry.ExitCode, dry.Error);
        Assert.AreEqual(0, recovery.ExitCode, recovery.Error);
        Assert.AreEqual(before, after);
        AssertManifest(dry.OutputRoot, "dry-run-passed");
        AssertManifest(recovery.OutputRoot, "recovery-test-passed");

        using var recoveryManifest = JsonDocument.Parse(File.ReadAllText(System.IO.Path.Combine(recovery.OutputRoot, "manual-run-manifest.json")));
        var root = recoveryManifest.RootElement;
        Assert.IsFalse(root.GetProperty("ui_started").GetBoolean());
        Assert.IsFalse(root.GetProperty("display_changed").GetBoolean());
        Assert.IsFalse(root.GetProperty("validator_started").GetBoolean());
        Assert.IsFalse(root.GetProperty("build_started").GetBoolean());
        Assert.IsTrue(root.GetProperty("recovery_test").GetProperty("environment_restored").GetBoolean());
        Assert.IsTrue(root.GetProperty("recovery_test").GetProperty("msbuild_node_reuse_override_observed").GetBoolean());
        Assert.IsTrue(root.GetProperty("recovery_test").GetProperty("msbuild_node_reuse_environment_restored").GetBoolean());
        Assert.IsTrue(root.GetProperty("recovery_test").GetProperty("helper_process_cleanup_verified").GetBoolean());
        Assert.IsTrue(root.GetProperty("recovery_test").GetProperty("devpreview_process_count_unchanged").GetBoolean());
        Assert.IsTrue(root.GetProperty("recovery_test").GetProperty("devpreview_residual_then_stable_zero_verified").GetBoolean());
        Assert.IsTrue(root.GetProperty("recovery_test").GetProperty("devpreview_reappearance_resets_stability_verified").GetBoolean());
        Assert.IsTrue(root.GetProperty("recovery_test").GetProperty("devpreview_persistent_nonzero_rejected").GetBoolean());
        Assert.IsTrue(root.GetProperty("recovery_test").GetProperty("devpreview_live_process_tables_stable_zero_verified").GetBoolean());
        Assert.IsTrue(root.GetProperty("recovery_test").GetProperty("retry_guard_clean_attempt_one_allowed").GetBoolean());
        Assert.IsTrue(root.GetProperty("recovery_test").GetProperty("retry_guard_early_attempt_two_rejected").GetBoolean());
        Assert.IsTrue(root.GetProperty("recovery_test").GetProperty("retry_guard_file_picker_import_rejected").GetBoolean());
    }

    private static void AssertManifest(string outputRoot, string expectedStatus)
    {
        using var document = JsonDocument.Parse(File.ReadAllText(System.IO.Path.Combine(outputRoot, "manual-run-manifest.json")));
        var root = document.RootElement;
        Assert.AreEqual("pixel-tart-p1-gate-a-manual-packet/v1", root.GetProperty("schema").GetString());
        Assert.AreEqual(expectedStatus, root.GetProperty("status").GetString());
        StringAssert.Matches(root.GetProperty("source_head").GetString()!, new("^[0-9a-f]{40}$"));
        Assert.IsFalse(root.GetProperty("ui_started").GetBoolean());
        Assert.IsFalse(root.GetProperty("validator_started").GetBoolean());
    }

    private static async Task<(int ExitCode, string Output, string Error, string OutputRoot)> InvokeModeAsync(string mode)
    {
        var outputRoot = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"PixelTart-P1-ManualPacketV2-Test-{mode}-{Guid.NewGuid():N}");
        var start = new ProcessStartInfo("powershell.exe")
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };
        if (mode == "RecoveryTest") start.Environment["MSBUILDDISABLENODEREUSE"] = "recovery-parent-sentinel";
        foreach (var argument in new[]
                 {
                     "-NoProfile", "-NonInteractive", "-ExecutionPolicy", "Bypass", "-File", Path($"{RelativePacketDirectory}/{EntryName}"),
                     "-Mode", mode, "-OutputRoot", outputRoot
                 }) start.ArgumentList.Add(argument);

        using var process = Process.Start(start) ?? throw new InvalidOperationException("PowerShell process did not start.");
        var outputTask = process.StandardOutput.ReadToEndAsync();
        var errorTask = process.StandardError.ReadToEndAsync();
        using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(60));
        await process.WaitForExitAsync(cancellation.Token);
        return (process.ExitCode, await outputTask, await errorTask, outputRoot);
    }

    private static void ContainsAll(string text, params string[] values)
    {
        foreach (var value in values) StringAssert.Contains(text, value);
    }

    private static string Slice(string text, string start, string end)
    {
        var startIndex = text.IndexOf(start, StringComparison.Ordinal);
        var endIndex = text.IndexOf(end, startIndex + start.Length, StringComparison.Ordinal);
        Assert.IsGreaterThanOrEqualTo(0, startIndex);
        Assert.IsGreaterThan(startIndex, endIndex);
        return text[startIndex..endIndex];
    }

    private static int CountOccurrences(string text, string value) =>
        (text.Length - text.Replace(value, string.Empty, StringComparison.Ordinal).Length) / value.Length;

    private static string Text() => File.ReadAllText(Path($"{RelativePacketDirectory}/{EntryName}"), Encoding.UTF8);

    private static string Path(string relativePath) => System.IO.Path.Combine(FindRepositoryRoot(), relativePath.Replace('/', System.IO.Path.DirectorySeparatorChar));

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !Directory.Exists(System.IO.Path.Combine(directory.FullName, "src"))) directory = directory.Parent;
        return directory?.FullName ?? throw new DirectoryNotFoundException("Repository root not found.");
    }
}
