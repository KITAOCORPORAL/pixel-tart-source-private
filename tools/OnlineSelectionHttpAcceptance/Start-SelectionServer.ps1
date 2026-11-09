param(
    [int]$Port = 17648,
    [string]$PublicBaseUrl = '',
    [string]$DataRoot = ''
)
$ErrorActionPreference = 'Stop'
$taskRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$serviceRoot = Join-Path $taskRoot 'artifacts/online-selection-http/service-final'
if (-not (Test-Path -LiteralPath (Join-Path $serviceRoot 'PixelTart.SelectionApi.Server.dll'))) {
    & dotnet publish (Join-Path $taskRoot 'src/PixelTart.SelectionApi.Server/PixelTart.SelectionApi.Server.csproj') -o $serviceRoot -v:q
    if ($LASTEXITCODE -ne 0) { throw 'Selection service publish failed.' }
}
if (-not $DataRoot) { $DataRoot = Join-Path $taskRoot 'artifacts/online-selection-http/server-final' }
if (-not $PublicBaseUrl) { $PublicBaseUrl = "http://127.0.0.1:$Port" }
Write-Host "Selection service: http://127.0.0.1:$Port"
Write-Host 'Keep this terminal running. No browser will be opened.'
& dotnet (Join-Path $serviceRoot 'PixelTart.SelectionApi.Server.dll') "--PixelTartSelection:Port=$Port" "--PixelTartSelection:Root=$DataRoot" "--PixelTartSelection:PublicBaseUrl=$PublicBaseUrl" "--contentRoot=$serviceRoot"
exit $LASTEXITCODE
