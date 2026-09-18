param(
    [Parameter(Mandatory)][string]$BuildDirectory,
    [Parameter(Mandatory)][string]$InstallDirectory,
    [Parameter(Mandatory)][string]$DataRoot,
    [string]$WorkingDirectory = 'C:\Windows',
    [int]$NavigationTimeoutSeconds = 300
)
$ErrorActionPreference = 'Stop'
$build = (Resolve-Path -LiteralPath $BuildDirectory).Path
$installed = (Resolve-Path -LiteralPath $InstallDirectory).Path
$testBoundary = [IO.Path]::GetFullPath((Join-Path $env:LOCALAPPDATA 'PixelTart-TestAcceptance')) + '\'
$data = [IO.Path]::GetFullPath($DataRoot)
if (-not $installed.StartsWith($testBoundary, [StringComparison]::OrdinalIgnoreCase) -or -not $data.StartsWith($testBoundary, [StringComparison]::OrdinalIgnoreCase)) {
    throw 'Smoke gate requires isolated installation and data directories.'
}
& (Join-Path $PSScriptRoot 'verify_installed_runtime.ps1') -BuildDirectory $build -InstallDirectory $installed
$env:PIXEL_TART_ACCEPTANCE_ROOT = $data
$started = Get-Date
$process = Start-Process -FilePath (Join-Path $installed 'PixelTart.exe') -WorkingDirectory $WorkingDirectory -WindowStyle Normal -PassThru
$result = [ordered]@{ Pid=$process.Id; Executable=$process.StartInfo.FileName; Start=$started.ToString('O'); MainWindow30Seconds=$false; CleanExit=$false; Navigation=$false; Pass=$false }
try {
    $visibleAt = $null
    $deadline = $started.AddSeconds(60)
    while ((Get-Date) -lt $deadline) {
        $process.Refresh()
        if ($process.HasExited) { throw 'Installed application exited during startup.' }
        if ($process.MainWindowHandle -ne 0) {
            if ($null -eq $visibleAt) { $visibleAt = Get-Date }
            if (((Get-Date)-$visibleAt).TotalSeconds -ge 30) { break }
        } else { $visibleAt = $null }
        Start-Sleep -Milliseconds 500
    }
    if ($null -eq $visibleAt -or ((Get-Date)-$visibleAt).TotalSeconds -lt 30) { throw 'MainWindow was not visible for 30 seconds.' }
    $result.MainWindow30Seconds = $true
    Write-Output 'Main window visible for 30 seconds. Perform installed navigation and close normally; this gate will not infer navigation success.'
    if (-not $process.WaitForExit($NavigationTimeoutSeconds * 1000)) { throw 'Awaiting installed navigation/normal close; gate incomplete.' }
    $result.ExitCode = $process.ExitCode
    $result.CleanExit = $process.ExitCode -eq 0
    $lines = @(Get-ChildItem -LiteralPath (Join-Path $data 'Logs') -Filter 'app-*.log' | Get-Content | Where-Object {
        $_ -match '^\d{4}-\d{2}-\d{2} \d{2}:\d{2}:\d{2}\.\d{3}' -and [datetime]::ParseExact($_.Substring(0,23),'yyyy-MM-dd HH:mm:ss.fff',$null) -ge $started
    })
    $text = $lines -join "`n"
    $result.StartupOk = $text.Contains('STARTUP_OK')
    $result.UnhandledErrors = $text.Contains('[ERROR]')
    $required = 'AssetLibrary','Planning','ReferenceColor','Tether','Workbench'
    $result.MissingRoutes = @($required | Where-Object { $text -notmatch ('-> ' + [regex]::Escape($_) + '(\s|$)') })
    $result.Navigation = $result.MissingRoutes.Count -eq 0
    $result.Pass = $result.MainWindow30Seconds -and $result.CleanExit -and $result.StartupOk -and -not $result.UnhandledErrors -and $result.Navigation
} finally {
    $result | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $build ('installed-smoke-' + $started.ToString('yyyyMMdd-HHmmss') + '.json')) -Encoding UTF8
}
if (-not $result.Pass) { throw 'Installed smoke gate is incomplete or failed. See its recorded evidence.' }
