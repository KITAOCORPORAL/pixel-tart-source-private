param()

$ErrorActionPreference = 'Stop'
$dotnet = Join-Path $env:LOCALAPPDATA 'Microsoft\dotnet-sdk-10\dotnet.exe'
if (-not (Test-Path $dotnet)) {
    $workspaceDotnet = Join-Path (Split-Path $PSScriptRoot -Parent) '.dotnet\dotnet.exe'
    $dotnet = if (Test-Path $workspaceDotnet) { $workspaceDotnet } else { (Get-Command dotnet -ErrorAction Stop).Source }
}

& $dotnet build "$PSScriptRoot\RAWSelectionAssistant.sln" -c Debug -p:Platform=x64
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
& $dotnet test "$PSScriptRoot\RAWSelectionAssistant.sln" -c Debug -p:Platform=x64 --no-build
exit $LASTEXITCODE
