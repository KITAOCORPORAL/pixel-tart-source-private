$ErrorActionPreference = 'Stop'
$session = Split-Path -Parent $PSScriptRoot
$repo = (Resolve-Path (Join-Path $session '../../..')).Path
$manifest = Get-Content -LiteralPath (Join-Path $session 'SESSION_MANIFEST.json') -Raw | ConvertFrom-Json
$wait = [Diagnostics.Stopwatch]::StartNew()
while ((Get-Process -Id $manifest.RecorderPID -ErrorAction SilentlyContinue) -and $wait.Elapsed.TotalSeconds -lt 8) { Start-Sleep -Milliseconds 100 }
if (Get-Process -Id $manifest.RecorderPID -ErrorAction SilentlyContinue) { throw 'Recorder has not stopped; no competing observer request made.' }
$manifest = Get-Content -LiteralPath (Join-Path $session 'SESSION_MANIFEST.json') -Raw | ConvertFrom-Json
if (-not (Test-Path -LiteralPath (Join-Path $PSScriptRoot 'FROZEN_FILE_INVENTORY.json'))) {
$inventory = [Collections.Generic.List[object]]::new()
$frozen = New-Item -ItemType Directory -Path (Join-Path $PSScriptRoot 'frozen')
$files = Get-ChildItem -LiteralPath $session -Recurse -File | Where-Object {
    -not $_.FullName.StartsWith($PSScriptRoot + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase) -and
    $_.Extension -in '.json','.jsonl','.log','.md','.ps1','.txt'
}
foreach ($file in $files) {
    $relative = [IO.Path]::GetRelativePath($session, $file.FullName)
    $copy = Join-Path $frozen.FullName $relative
    New-Item -ItemType Directory -Path (Split-Path -Parent $copy) -Force | Out-Null
    Copy-Item -LiteralPath $file.FullName -Destination $copy
    $inventory.Add([ordered]@{Path=$relative; Length=$file.Length; LastWriteTimeUtc=$file.LastWriteTimeUtc.ToString('o'); FrozenSha256=(Get-FileHash -LiteralPath $copy -Algorithm SHA256).Hash})
}
}
function NewJson([string]$path, $value) {
    $stream = [IO.File]::Open($path,[IO.FileMode]::CreateNew,[IO.FileAccess]::Write,[IO.FileShare]::Read)
    $writer = [IO.StreamWriter]::new($stream, [Text.UTF8Encoding]::new($false))
    try { $writer.Write((ConvertTo-Json -InputObject $value -Depth 50)) } finally { $writer.Dispose() }
}
if ($null -ne $inventory) { NewJson (Join-Path $PSScriptRoot 'FROZEN_FILE_INVENTORY.json') $inventory.ToArray() }
$product = Get-Process -Id $manifest.PID -ErrorAction SilentlyContinue
$identity = [ordered]@{ RecoveryTime=[DateTimeOffset]::UtcNow; RecorderStopped=$true; ProductRunning=($null -ne $product); SourceHead=$manifest.SourceHead; PID=$manifest.PID; RecordedHWND=$manifest.HWND; ManifestObserverStatus=$manifest.ObserverStatus; NoProductMutation=$true }
if ($product) {
    if ($product.StartTime.ToUniversalTime().Ticks -ne $manifest.ProcessStartTicks -or $product.MainModule.FileName -ne $manifest.BinaryPath) { throw 'Product process identity mismatch.' }
    $identity.CurrentHWND = $product.MainWindowHandle.ToInt64()
    $identity.CurrentBinaryHash = (Get-FileHash -LiteralPath $manifest.BinaryPath -Algorithm SHA256).Hash
    $identity.BinaryHashMatches = $identity.CurrentBinaryHash -eq $manifest.BinaryHash
    # Sole read-only request, after recorder stopped. Preserve protocol files before requesting.
    $finalJson = & (Join-Path $repo 'scripts/read-native-closure-observation.ps1') -FixtureDirectory $manifest.FixtureDirectory -TimeoutSeconds 10
    $final = $finalJson | ConvertFrom-Json
    NewJson (Join-Path $session 'POST_SESSION_FINAL_OBSERVATION.json') $final
    $identity.FinalObservationTimestamp = $final.Timestamp
    $identity.FinalObservationNonce = $final.Nonce
    $identity.FinalObservationSourceHead = $final.ProductSourceSha
}
NewJson (Join-Path $PSScriptRoot 'FREEZE_MANIFEST.json') $identity
$identity | ConvertTo-Json -Depth 6
Get-Content -LiteralPath (Join-Path $session 'HUMAN_NATIVE_SESSION.json')
