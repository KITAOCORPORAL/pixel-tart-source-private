param(
    [string]$Installer = '',
    [string]$EvidenceRoot = ''
)

$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
if ([string]::IsNullOrWhiteSpace($Installer))
{
    $installerDirectory = Join-Path $repoRoot 'artifacts\releases\2.0.4\installer'
    $Installer = (Get-ChildItem -LiteralPath $installerDirectory -Filter '*Setup_2.0.4_x64.exe' -File | Select-Object -First 1).FullName
}
if ([string]::IsNullOrWhiteSpace($EvidenceRoot)) { $EvidenceRoot = Join-Path $repoRoot 'artifacts\diagnostics\2.0.4\isolated-install-smoke' }
$installRoot = Join-Path $EvidenceRoot 'installed'
$settingsPath = Join-Path $env:LOCALAPPDATA 'KitaoPhotoSelector\settings.json'
$settingsBackup = Join-Path $EvidenceRoot 'settings.before.json'
New-Item -ItemType Directory -Force -Path $EvidenceRoot | Out-Null
if (Test-Path -LiteralPath $settingsPath) { Copy-Item -LiteralPath $settingsPath -Destination $settingsBackup -Force }
$result = [ordered]@{
    Passed = $false
    Installer = [IO.Path]::GetFullPath($Installer)
    InstallerExists = Test-Path -LiteralPath $Installer
    InstallerSha256 = if (Test-Path -LiteralPath $Installer) { (Get-FileHash -LiteralPath $Installer -Algorithm SHA256).Hash } else { '' }
    InstallRoot = [IO.Path]::GetFullPath($installRoot)
    InstallExitCode = $null
    ExecutableExists = $false
    ProcessStarted = $false
    MainWindowHandleObserved = $false
    WindowTitle = ''
    IsWinExe = $false
    CloseExitCode = $null
    UninstallExitCode = $null
    InstallDirectoryRemoved = $false
    NavigationInteraction = 'not-run; covered by source tests to avoid installed-window automation'
    StartedAt = [DateTimeOffset]::Now.ToString('O')
}
$process = $null
try {
    if (-not $result.InstallerExists) { throw 'Installer missing.' }
    if (Test-Path -LiteralPath $installRoot) { Remove-Item -LiteralPath $installRoot -Recurse -Force }
    $install = Start-Process -FilePath $result.Installer -ArgumentList @('/VERYSILENT','/SUPPRESSMSGBOXES','/NORESTART',('/DIR="' + $installRoot + '"'),'/NOICONS') -Wait -PassThru
    $result.InstallExitCode = $install.ExitCode
    $exe = Join-Path $installRoot 'KitaoPhotoSelector.exe'
    $result.ExecutableExists = Test-Path -LiteralPath $exe
    if ($result.InstallExitCode -ne 0 -or -not $result.ExecutableExists) { throw 'Isolated installation did not produce the executable.' }
    $result.IsWinExe = -not (Test-Path -LiteralPath (Join-Path $installRoot 'KitaoPhotoSelector.console.exe'))
    $process = Start-Process -FilePath $exe -PassThru
    $result.ProcessStarted = $true
    $deadline = [DateTime]::UtcNow.AddSeconds(20)
    do {
        Start-Sleep -Milliseconds 250
        $process.Refresh()
        if ($process.MainWindowHandle -ne [IntPtr]::Zero) { break }
    } while ([DateTime]::UtcNow -lt $deadline -and -not $process.HasExited)
    $result.MainWindowHandleObserved = $process.MainWindowHandle -ne [IntPtr]::Zero
    $result.WindowTitle = $process.MainWindowTitle
    if (-not $result.MainWindowHandleObserved) { throw 'Main window handle was not observed.' }
    $process.CloseMainWindow() | Out-Null
    if (-not $process.WaitForExit(10000)) { Stop-Process -Id $process.Id -Force }
    $result.CloseExitCode = $process.ExitCode
    $process = $null
    $uninstaller = Join-Path $installRoot 'unins000.exe'
    if (-not (Test-Path -LiteralPath $uninstaller)) { throw 'Uninstaller missing.' }
    $uninstall = Start-Process -FilePath $uninstaller -ArgumentList @('/VERYSILENT','/SUPPRESSMSGBOXES','/NORESTART') -Wait -PassThru
    $result.UninstallExitCode = $uninstall.ExitCode
    $result.InstallDirectoryRemoved = -not (Test-Path -LiteralPath $installRoot)
    $result.Passed = $result.InstallExitCode -eq 0 -and $result.MainWindowHandleObserved -and $result.IsWinExe -and $result.UninstallExitCode -eq 0 -and $result.InstallDirectoryRemoved
}
catch { $result.Error = $_.Exception.Message }
finally {
    if ($process -and -not $process.HasExited) { Stop-Process -Id $process.Id -Force -ErrorAction SilentlyContinue }
    $uninstaller = Join-Path $installRoot 'unins000.exe'
    if (Test-Path -LiteralPath $uninstaller) { Start-Process -FilePath $uninstaller -ArgumentList @('/VERYSILENT','/SUPPRESSMSGBOXES','/NORESTART') -Wait -ErrorAction SilentlyContinue }
    if (Test-Path -LiteralPath $settingsBackup) { New-Item -ItemType Directory -Force -Path (Split-Path $settingsPath) | Out-Null; Copy-Item -LiteralPath $settingsBackup -Destination $settingsPath -Force }
    $result.CompletedAt = [DateTimeOffset]::Now.ToString('O')
    $result | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath (Join-Path $EvidenceRoot 'result.json') -Encoding UTF8
}
Get-Content -LiteralPath (Join-Path $EvidenceRoot 'result.json') -Raw -Encoding UTF8
if (-not $result.Passed) { exit 1 }
