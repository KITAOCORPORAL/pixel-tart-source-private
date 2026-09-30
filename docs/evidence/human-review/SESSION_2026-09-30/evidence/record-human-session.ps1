[CmdletBinding()]
param([Parameter(Mandatory)][string]$RepositoryRoot, [ValidateRange(1,12)][int]$MaximumHours = 4)

# READ ONLY observation transport. No UI automation, command invocation, input or capture.
# Startup uses the existing opt-in fixture. Seeded state is explicitly excluded from acceptance.
$ErrorActionPreference = 'Stop'
$sessionRoot = $PSScriptRoot
$utf8 = [Text.UTF8Encoding]::new($false)
$manifestPath = Join-Path $sessionRoot 'SESSION_MANIFEST.json'
if (Test-Path -LiteralPath $manifestPath) { throw 'Existing session must not be overwritten.' }
function Write-Json([string]$name, $value) {
    $path = Join-Path $sessionRoot $name
    [IO.File]::WriteAllText($path + '.tmp', (ConvertTo-Json -InputObject $value -Depth 40), $utf8)
    [IO.File]::Move($path + '.tmp', $path, $true)
}
function Append-Json([string]$name, $value) {
    [IO.File]::AppendAllText((Join-Path $sessionRoot $name), (ConvertTo-Json -InputObject $value -Depth 40 -Compress) + "`n", $utf8)
}
function Compact($value) { ConvertTo-Json -InputObject $value -Depth 35 -Compress }

$sourceHead = (& git -C $RepositoryRoot rev-parse HEAD).Trim()
$branch = (& git -C $RepositoryRoot branch --show-current).Trim()
$exe = Join-Path $RepositoryRoot 'src/RAWSelectionAssistant/bin/x64/Release/net10.0-windows10.0.19041.0/win-x64/KitaoPhotoSelector.exe'
$runtime = Join-Path $sessionRoot 'runtime'
$fixture = Join-Path $runtime 'ColorStudioFixture'
$reader = Join-Path $RepositoryRoot 'scripts/read-native-closure-observation.ps1'
$startedAt = [DateTimeOffset]::UtcNow
$start = [Diagnostics.ProcessStartInfo]::new($exe)
$start.UseShellExecute = $false
$start.WorkingDirectory = Split-Path -Parent $exe
$start.ArgumentList.Add('--acceptance-color-studio')
$start.ArgumentList.Add('--studio-scenario=01')
$start.ArgumentList.Add('--native-evidence-observer')
$start.Environment['PIXEL_TART_ISOLATED_RUNTIME'] = '1'
$start.Environment['PIXEL_TART_ISOLATED_RUNTIME_ROOT'] = $runtime
$product = [Diagnostics.Process]::Start($start)
$productStartTicks = $product.StartTime.ToUniversalTime().Ticks
$manifest = [ordered]@{
    SessionId = [Guid]::NewGuid().ToString(); SourceHead = $sourceHead; Branch = $branch
    BinaryPath = $exe; BinaryHash = (Get-FileHash -LiteralPath $exe -Algorithm SHA256).Hash
    ProductAssemblyHash = (Get-FileHash -LiteralPath ([IO.Path]::ChangeExtension($exe, '.dll')) -Algorithm SHA256).Hash
    CoreAssemblyHash = (Get-FileHash -LiteralPath (Join-Path (Split-Path -Parent $exe) 'RAWSelectionAssistant.Core.dll') -Algorithm SHA256).Hash
    StartTime = $startedAt.ToString('o'); PID = $product.Id; ProcessStartTicks = $productStartTicks
    HWND = $null; Resolution = $null; DPI = $null; ObserverStatus = 'STARTING'
    RecorderPID = $PID; RecorderPath = $PSCommandPath; FixtureDirectory = $fixture
    InputSource = 'HUMAN'; InputInjection = $false; AutomatedNative = 'ENVIRONMENT_POLICY_BLOCKED'
    HumanNative = 'PENDING'; ObserverVerified = 'PENDING'; UserVisual = 'PENDING'
    BaselineApproved = $false; VisualApproved = $false; UserVerified = $false
    MaximumHours = $MaximumHours; StopFile = (Join-Path $sessionRoot 'STOP_OBSERVER.txt')
    Build = 'Release x64'; BuildErrors = 0; BuildWarnings = 0
    Setup = 'Existing scenario 01 fixture; setup commands and seeded samples are baseline only, not human evidence.'
}
Write-Json 'SESSION_MANIFEST.json' $manifest
$eye = [Collections.Generic.List[object]]::new()
$nodes = [Collections.Generic.List[object]]::new()
$threeD = [Collections.Generic.List[object]]::new()
$seenSamples = [Collections.Generic.HashSet[string]]::new()
$lastEye = ''; $lastNode = ''; $last3D = ''; $lastRoute = ''; $ordinal = 0; $errors = 0
$clock = [Diagnostics.Stopwatch]::StartNew()
$status = 'STARTING'; $previousCamera = $null; $previousNode = $null
function Save-Evidence {
    $common = @{ SessionId = $manifest.SessionId; SourceHead = $sourceHead; InputSource = 'HUMAN'; GateStatus = 'PENDING_HUMAN_ACTION_AND_REVIEW'; ObservationOnly = $true }
    Write-Json 'EYEDROPPER_HUMAN_NATIVE.json' (@{} + $common + @{ Samples = $eye.ToArray(); BaselineSeedSamplesExcluded = $true })
    Write-Json 'NODE_DRAG_HUMAN_NATIVE.json' (@{} + $common + @{ Transitions = $nodes.ToArray(); TransientMismatchIsNotAutomaticallyFailure = $true })
    Write-Json '3D_HUMAN_NATIVE.json' (@{} + $common + @{ Transitions = $threeD.ToArray(); OnRenderIsNotPresentationFrameTime = $true })
    Write-Json 'HUMAN_NATIVE_SESSION.json' @{
        SessionId = $manifest.SessionId; SourceHead = $sourceHead; Status = $status
        InputSource = 'HUMAN'; ObserverStatus = $manifest.ObserverStatus; ObservationCount = $ordinal
        LastObservationTime = $manifest.LastObservationTime; Errors = $errors
        AutomatedNative = 'ENVIRONMENT_POLICY_BLOCKED'; HumanNative = 'PENDING'; ObserverVerified = 'PENDING'
        UserVisual = 'PENDING'; BaselineApproved = $false; VisualApproved = $false; UserVerified = $false
        RawEvidence = 'observer-state.jsonl'; NoAutomaticGatePass = $true
    }
}
try {
    $readyClock = [Diagnostics.Stopwatch]::StartNew()
    while (-not (Test-Path -LiteralPath (Join-Path $fixture 'ready.txt'))) {
        if ($product.HasExited) { throw "Production EXE exited: $($product.ExitCode)" }
        if ($readyClock.Elapsed.TotalSeconds -gt 60) { throw 'Production fixture readiness timeout.' }
        Start-Sleep -Milliseconds 200
    }
    $before = (& $reader -FixtureDirectory $fixture -TimeoutSeconds 10 | ConvertFrom-Json)
    $product.Refresh()
    if ($product.MainWindowHandle -eq 0 -or $product.MainModule.FileName -ne $exe) { throw 'Production identity/handle unavailable.' }
    if ($before.ProductSourceSha -ne $sourceHead) { throw "Observer source mismatch: $($before.ProductSourceSha)" }
    $manifest.HWND = $product.MainWindowHandle.ToInt64()
    $manifest.Resolution = @{ Width = $before.Window.Width; Height = $before.Window.Height; DipWidth = $before.Window.DipWidth; DipHeight = $before.Window.DipHeight; Unit = 'physical pixels; DIP values separately recorded' }
    $manifest.DPI = @{ X = 96 * $before.Window.DpiScaleX; Y = 96 * $before.Window.DpiScaleY; Source = 'Production WPF VisualTreeHelper.GetDpi read-only observer' }
    $manifest.ObserverStatus = 'RECORDING'
    $manifest['FirstObserverNonce'] = $before.Nonce
    $manifest['LastObservationTime'] = $before.Timestamp
    Write-Json 'BASELINE_OBSERVATION.json' @{ Classification = 'AUTOMATED_FIXTURE_SETUP_NOT_HUMAN_INPUT'; State = $before }
    foreach ($sample in @($before.Viewport.Samples)) {
        if ($null -ne $sample) { [void]$seenSamples.Add("$($sample.Timestamp)|$($sample.Sequence)|$($sample.InputTimestamp)") }
    }
    $previousCamera = $before.Viewport.ColorSpace.Camera
    $previousNode = $before.ModelIds
    $status = 'RECORDING_AWAITING_HUMAN'
    Write-Json 'SESSION_MANIFEST.json' $manifest
    Save-Evidence
    Write-Json 'RECORDER_READY.json' @{ Timestamp = [DateTimeOffset]::UtcNow; PID = $product.Id; HWND = $manifest.HWND; RecorderPID = $PID; ObserverNonce = $before.Nonce; SourceHead = $sourceHead }
    while ($clock.Elapsed.TotalHours -lt $MaximumHours -and -not (Test-Path -LiteralPath (Join-Path $sessionRoot 'STOP_OBSERVER.txt'))) {
        if ($product.HasExited) { $status = 'PRODUCT_EXITED'; break }
        $product.Refresh()
        if ($product.StartTime.ToUniversalTime().Ticks -ne $productStartTicks -or $product.MainModule.FileName -ne $exe) { throw 'Product identity changed.' }
        try {
            $state = (& $reader -FixtureDirectory $fixture -TimeoutSeconds 5 | ConvertFrom-Json)
        } catch {
            $errors++
            Append-Json 'observer-errors.jsonl' @{ Timestamp = [DateTimeOffset]::UtcNow; Classification = 'OBSERVER'; Error = $_.Exception.Message }
            if ($errors -ge 20) { $status = 'OBSERVER_ERROR_LIMIT'; break }
            continue
        }
        $ordinal++
        $manifest.LastObservationTime = $state.Timestamp
        $viewport = $state.Viewport
        $visualIds = @($state.Drag.Rows | ForEach-Object { $_.Id })
        $sync = $state.Settled -and -not $state.HasError -and -not $state.Drag.Active -and @($state.ModelIds).Count -gt 0 -and
            (Compact @($state.ModelIds)) -ceq (Compact @($state.RenderedNodeIds)) -and (Compact @($state.ModelIds)) -ceq (Compact $visualIds)
        $changed = $false
        foreach ($sample in @($viewport.Samples)) {
            if ($null -eq $sample) { continue }
            $key = "$($sample.Timestamp)|$($sample.Sequence)|$($sample.InputTimestamp)"
            if ($seenSamples.Add($key)) {
                $eye.Add(@{ Observation = $ordinal; Timestamp = $state.Timestamp; ProductSample = $sample; InputSource = 'HUMAN'; Review = 'UNASSESSED' })
                $changed = $true
            }
        }
        $eyeState = Compact @{ Sampling = $viewport.IsSampling; Mode = $viewport.Mode; Zoom = $viewport.Zoom; PanX = $viewport.PanX; PanY = $viewport.PanY; IsFit = $viewport.IsFit }
        if ($eyeState -cne $lastEye) {
            Append-Json 'sampling-state.jsonl' @{ Observation = $ordinal; Timestamp = $state.Timestamp; State = ($eyeState | ConvertFrom-Json) }
            $lastEye = $eyeState; $changed = $true
        }
        $nodeState = @{ ModelIds = $state.ModelIds; VisualIds = $visualIds; RenderedNodeIds = $state.RenderedNodeIds; SelectedNode = $state.SelectedNode; Events = $state.Events; UndoCount = $state.UndoCount; RedoCount = $state.RedoCount; IsSettled = $state.Settled; Drag = $state.Drag; Synchronized = [bool]$sync; HasError = $state.HasError }
        $nodeKey = Compact $nodeState
        if ($nodeKey -cne $lastNode) {
            $nodes.Add(@{ Observation = $ordinal; Timestamp = $state.Timestamp; BeforeModelIds = $previousNode; After = $nodeState; Synchronization = $(if ($sync) {'SYNCHRONIZED_OBSERVED'} else {'PENDING_SETTLEMENT_OR_VIEW_NOT_VISIBLE'}); GateStatus = 'UNASSESSED' })
            $previousNode = $state.ModelIds; $lastNode = $nodeKey; $changed = $true
        }
        $cs = $viewport.ColorSpace
        $csKey = Compact @{ Camera = $cs.Camera; IsFit = $cs.IsFit; Bounds = $cs.Bounds; ProjectedBounds = $cs.ProjectedBounds; ModelLoaded = $cs.ModelLoaded; SampleCount = $cs.SampleCount; MouseCaptured = $cs.IsMouseCaptured }
        if ($csKey -cne $last3D) {
            $paddingCheck = $null
            if ($cs.IsFit -and $cs.ModelLoaded -and $cs.SampleCount -gt 0 -and $null -ne $cs.ProjectedBounds -and $cs.Bounds.DipWidth -gt 0 -and $cs.Bounds.DipHeight -gt 0) {
                $b = $cs.ProjectedBounds; $w = $cs.Bounds.DipWidth; $h = $cs.Bounds.DipHeight
                $finite = @($b.MinX,$b.MaxX,$b.MinY,$b.MaxY) | Where-Object { -not [double]::IsFinite([double]$_) }
                $paddingCheck = @($finite).Count -eq 0 -and $b.MinX -ge (.1*$w-1e-8) -and $b.MaxX -le (.9*$w+1e-8) -and $b.MinY -ge (.1*$h-1e-8) -and $b.MaxY -le (.9*$h+1e-8)
            }
            $threeD.Add(@{ Observation = $ordinal; Timestamp = $state.Timestamp; BeforeCamera = $previousCamera; After = $cs; FitPaddingPredicate = $paddingCheck; GateStatus = 'UNASSESSED'; WorkingSet = $product.WorkingSet64; PrivateMemory = $product.PrivateMemorySize64 })
            $previousCamera = $cs.Camera; $last3D = $csKey; $changed = $true
        }
        $routeKey = Compact @{ Route = $state.CurrentPage; Settings = $state.IsSettingsModalOpen; Window = $state.Window }
        if ($routeKey -cne $lastRoute) {
            Append-Json 'route-window-observations.jsonl' @{ Observation = $ordinal; Timestamp = $state.Timestamp; State = ($routeKey | ConvertFrom-Json); MachineGeometry = 'NOT_RUN'; UserVisual = 'PENDING' }
            $lastRoute = $routeKey; $changed = $true
        }
        if ($changed) { Append-Json 'observer-state.jsonl' @{ Observation = $ordinal; State = $state }; Save-Evidence }
        if ($ordinal % 10 -eq 0) { Write-Json 'SESSION_MANIFEST.json' $manifest; Save-Evidence }
        if ($ordinal -ge 20000) { $status = 'OBSERVATION_LIMIT'; break }
        Start-Sleep -Milliseconds 500
    }
    if ($status -eq 'RECORDING_AWAITING_HUMAN') { $status = 'BOUNDED_SESSION_ENDED' }
} catch {
    $status = 'OBSERVER_ERROR'
    $errors++
    Append-Json 'observer-errors.jsonl' @{ Timestamp = [DateTimeOffset]::UtcNow; Classification = 'SESSION_OR_OBSERVER'; Error = $_.Exception.ToString() }
} finally {
    $manifest.ObserverStatus = $status
    $manifest['RecorderEndTime'] = [DateTimeOffset]::UtcNow.ToString('o')
    Write-Json 'SESSION_MANIFEST.json' $manifest
    Save-Evidence
    # Never closes, kills or modifies the product window. The human owns the session.
    $product.Dispose()
}
