[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$FixtureDirectory,
    [switch]$WaitForSynchronizedNodes,
    [ValidateRange(1,60)][int]$TimeoutSeconds = 20
)

# Read-only product observer protocol. Never sends UI input or executes product commands.
$ErrorActionPreference = 'Stop'
$folder = (Resolve-Path -LiteralPath $FixtureDirectory).Path
if (-not (Test-Path -LiteralPath (Join-Path $folder 'ready.txt'))) { throw 'Production fixture is not ready.' }
$request = Join-Path $folder 'native-observe-request.txt'
$response = Join-Path $folder 'native-observe-response.json'
$clock = [Diagnostics.Stopwatch]::StartNew()
$last = $null
while ($clock.Elapsed.TotalSeconds -lt $TimeoutSeconds) {
    $nonce = [Guid]::NewGuid().ToString()
    [IO.File]::WriteAllText($request, $nonce)
    do {
        if (Test-Path -LiteralPath $response) {
            try { $last = Get-Content -LiteralPath $response -Raw | ConvertFrom-Json } catch [System.IO.IOException] { }
            if ($last.Nonce -eq $nonce) { break }
        }
        # Bounded protocol polling, not a sleep-based acceptance condition.
        Start-Sleep -Milliseconds 50
    } while ($clock.Elapsed.TotalSeconds -lt $TimeoutSeconds)
    if ($last.Nonce -ne $nonce) { break }
    $visualIds = @($last.Drag.Rows | ForEach-Object { $_.Id })
    $modelIds = @($last.ModelIds)
    $synchronized = $last.ModelRenderSynchronized -and -not $last.Drag.Active -and
        $visualIds.Count -gt 0 -and ($visualIds -join ',') -ceq ($modelIds -join ',')
    if (-not $WaitForSynchronizedNodes -or $synchronized) {
        $last | Add-Member -NotePropertyName VisualModelSynchronized -NotePropertyValue $synchronized
        $last | ConvertTo-Json -Depth 30
        return
    }
}
throw "Native observation timeout after $TimeoutSeconds seconds; no synchronized success claimed. Last nonce: $($last.Nonce)"
