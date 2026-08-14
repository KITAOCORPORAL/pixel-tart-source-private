param(
    [int]$Port = 5127,
    [string]$RuntimeRoot = (Join-Path ([System.IO.Path]::GetTempPath()) 'PixelTart_OnlineSelection_LocalDev_Preview'),
    [switch]$NoBuild,
    [switch]$NoWait
)

$ErrorActionPreference = 'Stop'
$repoRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$runtimeRootFull = [System.IO.Path]::GetFullPath($RuntimeRoot)
$dotnet = Join-Path (Split-Path (Split-Path $repoRoot -Parent) -Parent) '.dotnet\dotnet.exe'
if (-not (Test-Path -LiteralPath $dotnet)) { $dotnet = 'dotnet' }
$serverProject = Join-Path $repoRoot 'src\PixelTart.SelectionApi.Server\PixelTart.SelectionApi.Server.csproj'
$previewProject = Join-Path $repoRoot 'src\PixelTart.OnlineSelection.LocalDevPreview\PixelTart.OnlineSelection.LocalDevPreview.csproj'
$serverDll = Join-Path $repoRoot 'src\PixelTart.SelectionApi.Server\bin\Debug\net10.0-windows10.0.19041.0\PixelTart.SelectionApi.Server.dll'
$previewExe = Join-Path $repoRoot 'src\PixelTart.OnlineSelection.LocalDevPreview\bin\Debug\net10.0-windows10.0.19041.0\win-x64\PixelTart_OnlineSelection_LocalDev_Preview.exe'
$serverRoot = Join-Path $runtimeRootFull 'Server'
$desktopRoot = Join-Path $runtimeRootFull 'Desktop'
$ownershipFile = Join-Path $runtimeRootFull 'launcher-processes.json'
$endpoint = "http://127.0.0.1:$Port"

New-Item -ItemType Directory -Path $serverRoot,$desktopRoot -Force | Out-Null
if (-not $NoBuild) {
    & $dotnet build $serverProject -c Debug --nologo
    if ($LASTEXITCODE -ne 0) { throw 'LocalDev server build failed.' }
    & $dotnet build $previewProject -c Debug --nologo
    if ($LASTEXITCODE -ne 0) { throw 'LocalDev preview build failed.' }
}

$serverEnvironment = @{
    ASPNETCORE_ENVIRONMENT = 'Development'
    PIXELTART_SELECTION_LOCALDEV_ROOT = $serverRoot
    PIXELTART_SELECTION_LOCALDEV_PORT = "$Port"
    PIXELTART_SELECTION_LOCALDEV_ENDPOINT = $endpoint
    PIXELTART_SELECTION_PREVIEW_ROOT = $desktopRoot
    PIXELTART_SELECTION_LOCALDEV_ACCESS_STORE = (Join-Path $desktopRoot 'localdev-access.dpapi')
}
$previous = @{}
foreach ($entry in $serverEnvironment.GetEnumerator()) { $previous[$entry.Key] = [Environment]::GetEnvironmentVariable($entry.Key); [Environment]::SetEnvironmentVariable($entry.Key, $entry.Value) }
$server = $null
$preview = $null
$startupCompleted = $false
try {
    $server = Start-Process -FilePath $dotnet -ArgumentList ('"{0}"' -f $serverDll) -PassThru -WindowStyle Hidden
    $ready = $false
    foreach ($attempt in 1..100) {
        if ($server.HasExited) { throw "LocalDev server exited with code $($server.ExitCode)." }
        try { $health = Invoke-RestMethod -Uri "$endpoint/health/ready" -TimeoutSec 1; if ($health.ready) { $ready = $true; break } } catch { }
        Start-Sleep -Milliseconds 100
    }
    if (-not $ready) { throw 'LocalDev server did not become ready.' }

    $preview = Start-Process -FilePath $previewExe -PassThru
    @{ serverPid = $server.Id; previewPid = $preview.Id; endpoint = $endpoint; createdAt = [DateTimeOffset]::UtcNow.ToString('O') } |
        ConvertTo-Json | Set-Content -LiteralPath $ownershipFile -Encoding utf8
    Write-Output "ONLINE_SELECTION_LOCALDEV_READY $endpoint SERVER_PID=$($server.Id) PREVIEW_PID=$($preview.Id)"
    $startupCompleted = $true
    if (-not $NoWait) { $preview.WaitForExit() }
}
finally {
    foreach ($entry in $previous.GetEnumerator()) { [Environment]::SetEnvironmentVariable($entry.Key, $entry.Value) }
    if ((-not $NoWait -or -not $startupCompleted) -and $server -and -not $server.HasExited) {
        $server.CloseMainWindow() | Out-Null
        if (-not $server.WaitForExit(3000)) { $server.Kill($true) }
    }
}
