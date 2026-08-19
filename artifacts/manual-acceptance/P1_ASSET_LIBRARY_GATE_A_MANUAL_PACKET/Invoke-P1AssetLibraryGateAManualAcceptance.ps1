[CmdletBinding()]
param(
    [ValidateSet('DryRun', 'Run', 'RecoveryTest')]
    [string]$Mode = 'DryRun',

    [string]$OutputRoot,

    [string]$ExecutablePath,

    [ValidatePattern('^[0-9a-f]{40}$')]
    [string]$SourceHead = '3b5ff13bb4c5b4c2001f978cb6ab31f5715cd7af'
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$repositoryRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\..\..'))
$temporaryRoot = [IO.Path]::GetFullPath([IO.Path]::GetTempPath()).TrimEnd([IO.Path]::DirectorySeparatorChar) + [IO.Path]::DirectorySeparatorChar
if ([string]::IsNullOrWhiteSpace($OutputRoot)) {
    $OutputRoot = Join-Path $temporaryRoot ("PixelTart-P1-GateA-Manual-{0}-{1}" -f [DateTimeOffset]::Now.ToString('yyyyMMdd-HHmmss'), [Guid]::NewGuid().ToString('N'))
}
$runRoot = [IO.Path]::GetFullPath($OutputRoot)
if (-not $runRoot.StartsWith($temporaryRoot, [StringComparison]::OrdinalIgnoreCase) -or $runRoot.TrimEnd('\') -eq $temporaryRoot.TrimEnd('\')) {
    throw "OutputRoot must be a new child directory below the Windows temporary root."
}
if (Test-Path -LiteralPath $runRoot) { throw "OutputRoot already exists; refusing to overwrite evidence: $runRoot" }

$defaultExecutable = Join-Path $repositoryRoot 'src\RAWSelectionAssistant\bin\Debug\net10.0-windows10.0.19041.0\win-x64\PixelTart_ModularHarness_V1_DevPreview.exe'
if ([string]::IsNullOrWhiteSpace($ExecutablePath)) { $ExecutablePath = $defaultExecutable }
$resolvedExecutable = [IO.Path]::GetFullPath($ExecutablePath)
$captureTool = Join-Path $repositoryRoot 'tools\AssetLibraryP1Acceptance\Capture-AssetLibraryP1WindowEvidence.ps1'
$validatorSource = Join-Path $repositoryRoot 'tools\AssetLibraryP1Acceptance\Test-AssetLibraryP1GateAEvidence.ps1'
$contractSource = Join-Path $repositoryRoot 'tools\AssetLibraryP1Acceptance\gate-a-evidence-contract.json'
$windowTitle = [Text.Encoding]::UTF8.GetString([Convert]::FromBase64String('5YOP57Sg6JuL5oyeIFtNb2R1bGFyIEhhcm5lc3MgRGV2XQ=='))
$displayMatrixStarted = $false

function Write-Utf8NoBom([string]$Path, [string]$Content) {
    [IO.File]::WriteAllText($Path, $Content, [Text.UTF8Encoding]::new($false))
}

function Write-Manifest([string]$Status, [hashtable]$Additional = @{}) {
    $manifest = [ordered]@{
        schema = 'pixel-tart-p1-gate-a-manual-packet/v1'
        status = $Status
        mode = $Mode
        source_head = $SourceHead
        run_root = $runRoot
        executable_path = $resolvedExecutable
        executable_sha256 = if (Test-Path $resolvedExecutable) { (Get-FileHash $resolvedExecutable -Algorithm SHA256).Hash } else { $null }
        synthetic_fixture_only = $true
        customer_media_allowed = $false
        eagle_library_write_allowed = $false
        created_at = [DateTimeOffset]::Now
    }
    foreach ($key in $Additional.Keys) { $manifest[$key] = $Additional[$key] }
    Write-Utf8NoBom (Join-Path $runRoot 'manual-run-manifest.json') ($manifest | ConvertTo-Json -Depth 8)
}

function Read-Action([string]$Instruction) {
    Write-Host ""
    Write-Host $Instruction -ForegroundColor Cyan
    Write-Host "Press Enter after this one action. Enter Q to cancel safely."
    $answer = Read-Host
    if ([string]::Equals($answer, 'q', [StringComparison]::OrdinalIgnoreCase)) { throw [OperationCanceledException]::new('Manual acceptance canceled by the user.') }
}

function Start-StateSession([string]$Name, [string]$Scenario) {
    $sessionRoot = Join-Path $runRoot (Join-Path 'sessions' $Name)
    [IO.Directory]::CreateDirectory($sessionRoot) | Out-Null
    foreach ($suffix in @('', '-wal', '-shm')) {
        if (Test-Path (Join-Path $sessionRoot ("Data\asset-library-v16.db{0}" -f $suffix))) { throw "State session is not fresh: $Name" }
    }
    $environment = @{
        PIXEL_TART_ACCEPTANCE_ROOT = $sessionRoot
        PIXEL_TART_ASSET_LIBRARY_P1_STATE_ACCEPTANCE = $Scenario
        PIXEL_TART_ASSET_LIBRARY_P1_START_ROUTE = 'asset-library'
        PIXEL_TART_ASSET_LIBRARY_P1_HEAD = $SourceHead
        PIXEL_TART_PHYSICAL_POINTER_DIAGNOSTICS = '1'
    }
    $previous = @{}
    foreach ($key in $environment.Keys) { $previous[$key] = [Environment]::GetEnvironmentVariable($key, 'Process'); [Environment]::SetEnvironmentVariable($key, $environment[$key], 'Process') }
    try {
        $process = Start-Process -FilePath $resolvedExecutable -PassThru
        $deadline = [DateTimeOffset]::Now.AddSeconds(30)
        while ([DateTimeOffset]::Now -lt $deadline -and [string]::IsNullOrWhiteSpace((Get-Process -Id $process.Id).MainWindowTitle)) { Start-Sleep -Milliseconds 250 }
        return [pscustomobject]@{ Name=$Name; Scenario=$Scenario; Root=$sessionRoot; Process=$process; Previous=$previous }
    } catch {
        foreach ($key in $previous.Keys) { [Environment]::SetEnvironmentVariable($key, $previous[$key], 'Process') }
        throw
    }
}

function Start-RegularSession([string]$SessionRoot, [string]$DemoDirectory = '') {
    [IO.Directory]::CreateDirectory($SessionRoot) | Out-Null
    $keys = @(
        'PIXEL_TART_ACCEPTANCE_ROOT', 'PIXEL_TART_ASSET_LIBRARY_DEMO_DIR',
        'PIXEL_TART_ASSET_LIBRARY_P1_STATE_ACCEPTANCE', 'PIXEL_TART_ASSET_LIBRARY_P1_START_ROUTE',
        'PIXEL_TART_ASSET_LIBRARY_P1_HEAD', 'PIXEL_TART_PHYSICAL_POINTER_DIAGNOSTICS')
    $previous = @{}
    foreach ($key in $keys) { $previous[$key] = [Environment]::GetEnvironmentVariable($key, 'Process') }
    [Environment]::SetEnvironmentVariable('PIXEL_TART_ACCEPTANCE_ROOT', $SessionRoot, 'Process')
    [Environment]::SetEnvironmentVariable('PIXEL_TART_ASSET_LIBRARY_DEMO_DIR', $(if ($DemoDirectory) { $DemoDirectory } else { $null }), 'Process')
    [Environment]::SetEnvironmentVariable('PIXEL_TART_ASSET_LIBRARY_P1_STATE_ACCEPTANCE', $null, 'Process')
    [Environment]::SetEnvironmentVariable('PIXEL_TART_ASSET_LIBRARY_P1_START_ROUTE', $null, 'Process')
    [Environment]::SetEnvironmentVariable('PIXEL_TART_ASSET_LIBRARY_P1_HEAD', $null, 'Process')
    [Environment]::SetEnvironmentVariable('PIXEL_TART_PHYSICAL_POINTER_DIAGNOSTICS', '1', 'Process')
    try {
        $process = Start-Process -FilePath $resolvedExecutable -PassThru
        $deadline = [DateTimeOffset]::Now.AddSeconds(30)
        while ([DateTimeOffset]::Now -lt $deadline -and [string]::IsNullOrWhiteSpace((Get-Process -Id $process.Id).MainWindowTitle)) { Start-Sleep -Milliseconds 250 }
        return [pscustomobject]@{ Name='regular'; Scenario='regular'; Root=$SessionRoot; Process=$process; Previous=$previous }
    } catch {
        foreach ($key in $previous.Keys) { [Environment]::SetEnvironmentVariable($key, $previous[$key], 'Process') }
        throw
    }
}

function Stop-StateSession($Session) {
    try {
        if ($Session.Process -and -not $Session.Process.HasExited) { Stop-Process -Id $Session.Process.Id; Wait-Process -Id $Session.Process.Id -Timeout 10 -ErrorAction SilentlyContinue }
    } finally {
        foreach ($key in $Session.Previous.Keys) { [Environment]::SetEnvironmentVariable($key, $Session.Previous[$key], 'Process') }
    }
}

function Capture-State($Session, [string]$Name) {
    & $captureTool -ProcessId $Session.Process.Id -ExecutablePath $resolvedExecutable -WindowTitle $windowTitle -OutputRoot $runRoot -CaptureName $Name -CaptureMethod PrintWindow
    if ($LASTEXITCODE) { throw "Window evidence capture failed: $Name" }
}

[IO.Directory]::CreateDirectory($runRoot) | Out-Null
try {
    foreach ($required in @($resolvedExecutable, $captureTool, $validatorSource, $contractSource)) { if (-not (Test-Path -LiteralPath $required -PathType Leaf)) { throw "Required file is missing: $required" } }
    if (@(Get-Process -Name 'PixelTart_ModularHarness_V1_DevPreview' -ErrorAction SilentlyContinue).Count -gt 0) { throw 'Close every existing Modular Harness Dev Preview process before starting.' }

    if ($Mode -eq 'DryRun') {
        Write-Manifest 'dry-run-passed' @{ ui_started=$false; display_changed=$false; validator_started=$false }
        Write-Output (Join-Path $runRoot 'manual-run-manifest.json')
        return
    }
    if ($Mode -eq 'RecoveryTest') { throw [OperationCanceledException]::new('Bounded recovery-path test.') }

    $displayBefore = Get-CimInstance Win32_VideoController | Select-Object -First 1 CurrentHorizontalResolution,CurrentVerticalResolution,CurrentRefreshRate
    Write-Manifest 'running' @{ display_before=$displayBefore }

    $first = Start-StateSession 'first-empty' 'first-empty/v1'
    try {
        Read-Action 'Confirm the same MainWindow opened directly on Asset Library and the first-empty state is visible. Do not click primary navigation.'
        Capture-State $first '08-first-empty-manual'
    } finally { Stop-StateSession $first }

    $retry = Start-StateSession 'retry' 'loading-error-retry-empty/v1'
    try {
        Read-Action 'Confirm the real Loading state is visible.'
        Capture-State $retry '09-loading-manual'
        $scenarioManifest = Get-Content (Join-Path $retry.Root 'InputDiagnostics\AssetLibraryP1StateAcceptance\scenario-manifest.json') -Raw | ConvertFrom-Json
        Write-Utf8NoBom ([string]$scenarioManifest.releaseFile) 'release'
        Read-Action 'Confirm recoverable error is visible. Use only real Tab or Shift+Tab until focus reaches RetryAssetLibraryLoad.'
        Capture-State $retry '10-recoverable-error-manual'
        Read-Action 'Press Enter or Space exactly once on RetryAssetLibraryLoad, then wait for first-empty recovery.'
        Capture-State $retry '11-retry-recovered-manual'
    } finally { Stop-StateSession $retry }

    $fixtureRoot = Join-Path $runRoot 'synthetic-fixture'
    & (Join-Path $repositoryRoot 'tools\ModularHarnessV1Acceptance\New-ModularHarnessSyntheticFixture.ps1') -OutputRoot $fixtureRoot | Out-Null
    $regularRoot = Join-Path $runRoot 'regular'
    $script:manualStep = 0
    $regular = Start-RegularSession $regularRoot (Join-Path $fixtureRoot 'images')
    try {
        Read-Action 'Click AssetLibraryNavigationButton once. Confirm 12 synthetic cards are visible.'
        Capture-State $regular 'keyboard-splitters-start'
        foreach ($step in @(
            'Focus AssetOrganizationSplitter, place it at minimum, then press Left once.',
            'Focus AssetOrganizationSplitter, place it at maximum, then press Right once.',
            'Focus AssetOrganizationSplitter at a middle width, then press Right once.',
            'Keep focus on AssetOrganizationSplitter and press Left once.',
            'Activate ToggleAssetOrganizationPane once to collapse.',
            'Activate ToggleAssetOrganizationPane once to expand.',
            'Focus AssetInspectorSplitter, place it at minimum, then press Right once.',
            'Focus AssetInspectorSplitter, place it at maximum, then press Left once.',
            'Focus AssetInspectorSplitter at a middle width, then press Left once.',
            'Keep focus on AssetInspectorSplitter and press Right once.',
            'Activate ToggleAssetInspectorPane once to collapse.',
            'Activate ToggleAssetInspectorPane once to expand.',
            'Focus AssetThumbnailSizeSlider and press Right once.')) {
            Read-Action $step
            Capture-State $regular ("step-{0:D2}" -f (++$script:manualStep))
        }
    } finally { Stop-StateSession $regular }
    $diagnostic = Join-Path $regularRoot 'InputDiagnostics\physical-pointer-session.json'
    $importDiagnostic = Join-Path $regularRoot 'InputDiagnostics\asset-library-import.json'
    if (-not (Test-Path $diagnostic) -or -not (Test-Path $importDiagnostic)) { throw 'Regular session did not produce its physical diagnostic and synthetic import diagnostic.' }
    Copy-Item $diagnostic (Join-Path $runRoot 'physical-pointer-keyboard-session.json')
    Copy-Item $importDiagnostic (Join-Path $runRoot 'initial-import-0-to-12.json')

    $restart = Start-RegularSession $regularRoot
    try {
        Read-Action 'Click AssetLibraryNavigationButton once and confirm both pane widths, collapse state, and thumbnail size restored after restart.'
        Capture-State $restart 'keyboard-splitters-restart-restored'
        foreach ($tuple in @(
            @{ Token='1366x768-100pct'; Instruction='Set Windows display to 1366x768 at 100%, return to and maximize the app.' },
            @{ Token='1920x1080-125pct'; Instruction='Set Windows display to 1920x1080 at 125%, return to and maximize the app.' },
            @{ Token='1920x1080-150pct'; Instruction='Set Windows display to 1920x1080 at 150%, return to and maximize the app.' },
            @{ Token='2560x1440-175pct'; Instruction='Set Windows display to 2560x1440 at 175%, return to and maximize the app.' })) {
            $displayMatrixStarted = $true
            Read-Action $tuple.Instruction
            Capture-State $restart ("dpi-{0}-default" -f $tuple.Token)
            Read-Action 'Use real Tab/Shift+Tab to focus a splitter, then press one Left or Right arrow key.'
            Capture-State $restart ("dpi-{0}-interaction" -f $tuple.Token)
        }
        Read-Action 'Restore 3840x2160@60Hz/150%, return to the app, and confirm the baseline is restored.'
        Capture-State $restart 'restore-baseline-3840x2160-150pct-final'
        $displayMatrixStarted = $false
    } finally { Stop-StateSession $restart }

    $displayAfter = Get-CimInstance Win32_VideoController | Select-Object -First 1 CurrentHorizontalResolution,CurrentVerticalResolution,CurrentRefreshRate
    $validationToolRoot = Join-Path $runRoot 'validator'
    [IO.Directory]::CreateDirectory($validationToolRoot) | Out-Null
    Copy-Item -LiteralPath $validatorSource -Destination (Join-Path $validationToolRoot 'Test-AssetLibraryP1GateAEvidence.ps1')
    Copy-Item -LiteralPath $contractSource -Destination (Join-Path $validationToolRoot 'gate-a-evidence-contract.json')
    $validationContract = Get-Content (Join-Path $validationToolRoot 'gate-a-evidence-contract.json') -Raw | ConvertFrom-Json
    $validationContract.capture_status = 'captured'
    Write-Utf8NoBom (Join-Path $validationToolRoot 'gate-a-evidence-contract.json') ($validationContract | ConvertTo-Json -Depth 20)
    $validatorOutput = & (Join-Path $validationToolRoot 'Test-AssetLibraryP1GateAEvidence.ps1') -RunRoot $runRoot 2>&1 | Out-String
    Write-Utf8NoBom (Join-Path $runRoot 'validator-output.txt') $validatorOutput
    Write-Manifest ($(if ($LASTEXITCODE -eq 0) { 'validation-passed' } else { 'validation-failed' })) @{ display_before=$displayBefore; display_after=$displayAfter; validator_exit_code=$LASTEXITCODE }
    if ($LASTEXITCODE -ne 0) { throw "Strict Gate A validator failed. See validator-output.txt." }
} catch [OperationCanceledException] {
    if ($displayMatrixStarted) { Read-Action 'The run was canceled during the display matrix. Restore 3840x2160@60Hz/150% in Windows Settings before pressing Enter.' }
    Write-Manifest 'canceled' @{ recovery='No display change is automated by this packet; restore prompt precedes validation.' }
    throw
} catch {
    if ($displayMatrixStarted) { Read-Action 'The run failed during the display matrix. Restore 3840x2160@60Hz/150% in Windows Settings before pressing Enter.' }
    Write-Manifest 'failed' @{ error=$_.Exception.Message; recovery='If display settings were changed, restore 3840x2160@60Hz/150% in Windows Settings.' }
    throw
}
