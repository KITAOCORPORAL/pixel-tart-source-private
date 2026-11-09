param(
    [string]$BaseUrl = 'http://127.0.0.1:17649',
    [string]$RunRoot = 'artifacts/online-selection-http/iphone13-safari'
)
$ErrorActionPreference = 'Stop'
$taskRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
if (-not [System.IO.Path]::IsPathFullyQualified($RunRoot)) { $RunRoot = Join-Path $taskRoot $RunRoot }
$RunRoot = [System.IO.Path]::GetFullPath($RunRoot)
$appRoot = Join-Path $taskRoot 'artifacts/online-selection-http/resume-app'
$appExe = Join-Path $appRoot 'KitaoPhotoSelector.exe'
if (-not (Test-Path -LiteralPath $appExe)) {
    & dotnet publish (Join-Path $taskRoot 'src/RAWSelectionAssistant/RAWSelectionAssistant.csproj') -c Release -p:WholeAppDiagnosticBuild=true -p:PhotographerTrialBuild=false -o $appRoot -v:q
    if ($LASTEXITCODE -ne 0) { throw 'Complete app publish failed.' }
}
$health = Invoke-RestMethod -Uri ($BaseUrl.TrimEnd('/') + '/health/ready') -TimeoutSec 5
if (-not $health.ready) { throw 'Selection service is not ready. Start the server first.' }
$env:GUANGJING_SELECTION_BASE_URL = $BaseUrl
$env:PIXEL_TART_ISOLATED_RUNTIME = '1'
$env:PIXEL_TART_ISOLATED_RUNTIME_ROOT = Join-Path $RunRoot 'IsolatedApp'
Write-Host 'Opening the complete app with isolated test data. Select 在线选片, then the phone test project.'
$appArguments = @('--online-selection')
$identityFile = Join-Path $RunRoot 'identity.json'
if (Test-Path -LiteralPath $identityFile) {
    $identity = Get-Content -LiteralPath $identityFile -Raw | ConvertFrom-Json
    $parsedProject = [Guid]::Empty
    if (-not [Guid]::TryParse([string]$identity.projectId, [ref]$parsedProject)) { throw 'Invalid selection project identity.' }
    $appArguments += "--selection-project=$parsedProject"
}
& $appExe @appArguments
