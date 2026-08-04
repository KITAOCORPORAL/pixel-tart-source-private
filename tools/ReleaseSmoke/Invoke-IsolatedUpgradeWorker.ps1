param([Parameter(Mandatory=$true)][string]$ContextPath)

$ErrorActionPreference = 'Stop'
$context = Get-Content -LiteralPath $ContextPath -Raw -Encoding UTF8 | ConvertFrom-Json
$env:LOCALAPPDATA = $context.LocalAppData
$appDataRoot = Join-Path $context.LocalAppData 'KitaoPhotoSelector.Acceptance'
$resolved = [IO.Path]::GetFullPath($appDataRoot)
$local = [IO.Path]::GetFullPath($context.LocalAppData).TrimEnd([IO.Path]::DirectorySeparatorChar)
if (-not $resolved.StartsWith($local + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) {
    throw 'Upgrade app data escaped isolated root.'
}
$env:PIXEL_TART_ACCEPTANCE_ROOT = $appDataRoot

New-Item -ItemType Directory -Force -Path (Join-Path $appDataRoot 'Projects') | Out-Null
$projectId = [Guid]::NewGuid()
@{
    PinnedQuickTools = @('Workflow','PhotoOrganize','Collage')
    QuickToolLayout = @{ SchemaVersion='1.0'; OrderedToolIds=@('Workflow','PhotoOrganize','BatchCompress') }
    OnboardingLegacyUser = $true
    OnboardingCompleted = $true
} | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath (Join-Path $appDataRoot 'settings.json') -Encoding UTF8

$upgradeProjectName = [Text.Encoding]::UTF8.GetString([Convert]::FromBase64String('Mi4x5Y2H57qn6aqM5pS26aG555uu'))
$projectTimestamp = [DateTimeOffset]::UtcNow.ToString('O')
@"
[
  {
    "Id": "$projectId",
    "Name": "$upgradeProjectName",
    "Status": 0,
    "Category": 2,
    "OutputMode": 0,
    "OutputDirectory": "",
    "SourceDirectories": [],
    "SelectionInputs": ["DPI_TEST_0001.JPG"],
    "CreatedAt": "$projectTimestamp",
    "UpdatedAt": "$projectTimestamp"
  }
]
"@ | Set-Content -LiteralPath (Join-Path $appDataRoot 'Projects\projects.json') -Encoding UTF8

$sourceFile = Join-Path $context.SourceRoot 'upgrade-source.txt'
New-Item -ItemType Directory -Force -Path $context.SourceRoot | Out-Null
'upgrade-source' | Set-Content -LiteralPath $sourceFile -Encoding UTF8
$sourceBefore = (Get-FileHash -LiteralPath $sourceFile -Algorithm SHA256).Hash
$result = [ordered]@{
    Passed=$false; RunId=$context.RunId; IsolationMethod='Win32 CreateDesktopW'
    OldInstallExitCode=$null; OldStarted=$false; OldLaunchSkippedForIsolation=$true; OldInstallVerified=$false; OldProductVersion=''; LegacyDataSeeded=$false
    NewInstallExitCode=$null; NewStarted=$false; NewRestarted=$false; NewProductVersion=''
    Probe=$null; SourceUnchanged=$false; UninstallExitCode=$null; UserDataRetained=$false
    StartedAt=[DateTimeOffset]::Now.ToString('O')
}
$process = $null

function Start-And-Close([string]$exe) {
    $script:process = Start-Process -FilePath $exe -PassThru
    $deadline = [DateTime]::UtcNow.AddSeconds(25)
    do {
        Start-Sleep -Milliseconds 250
        $process.Refresh()
        if ($process.MainWindowHandle -ne [IntPtr]::Zero) { break }
    } while ([DateTime]::UtcNow -lt $deadline -and -not $process.HasExited)
    if ($process.MainWindowHandle -eq [IntPtr]::Zero) { throw 'Application window not observed.' }
    $process.CloseMainWindow() | Out-Null
    if (-not $process.WaitForExit(10000)) { Stop-Process -Id $process.Id -Force }
    $script:process = $null
}

try {
    $old = Start-Process -FilePath $context.OldInstaller -ArgumentList @('/VERYSILENT','/SUPPRESSMSGBOXES','/NORESTART',('/DIR="'+$context.InstallRoot+'"'),'/NOICONS') -Wait -PassThru
    $result.OldInstallExitCode = $old.ExitCode
    if ($old.ExitCode -ne 0) { throw '2.1.0 install failed.' }

    $installed = Join-Path $context.InstallRoot 'KitaoPhotoSelector.exe'
    $acceptance = Join-Path $context.InstallRoot 'KitaoPhotoSelector.Acceptance.exe'
    if (-not (Test-Path -LiteralPath $installed)) { throw '2.1.0 executable missing after install.' }
    $oldVersion = (Get-Item -LiteralPath $installed).VersionInfo
    $result.OldProductVersion = $oldVersion.ProductVersion
    $result.OldInstallVerified = $oldVersion.ProductVersion -like '2.1.0*'
    $result.LegacyDataSeeded = (Test-Path -LiteralPath (Join-Path $appDataRoot 'settings.json')) -and (Test-Path -LiteralPath (Join-Path $appDataRoot 'Projects\projects.json'))
    if (-not $result.OldInstallVerified -or -not $result.LegacyDataSeeded) { throw '2.1.0 installation or isolated legacy data verification failed.' }

    $new = Start-Process -FilePath $context.NewInstaller -ArgumentList @('/VERYSILENT','/SUPPRESSMSGBOXES','/NORESTART',('/DIR="'+$context.InstallRoot+'"'),'/NOICONS') -Wait -PassThru
    $result.NewInstallExitCode = $new.ExitCode
    if ($new.ExitCode -ne 0) { throw '2.2.0 candidate upgrade failed.' }
    $newVersion = (Get-Item -LiteralPath $installed).VersionInfo
    $result.NewProductVersion = $newVersion.ProductVersion
    if ($newVersion.ProductVersion -notlike '2.2.0*') { throw '2.2.0 executable version verification failed.' }
    Copy-Item -LiteralPath $installed -Destination $acceptance -Force
    Start-And-Close $acceptance
    $result.NewStarted = $true
    Start-And-Close $acceptance
    $result.NewRestarted = $true

    $probeProject = Join-Path $context.RepoRoot 'tools\ReleaseSmoke\UpgradeDataProbe\UpgradeDataProbe.csproj'
    $db = Join-Path $appDataRoot 'Data\pixel-tart.db'
    $output = & $context.DotnetPath run --project $probeProject -c Release ("-p:InstalledAppRoot="+$context.InstallRoot) -- $db $appDataRoot $sourceFile 2>&1
    $line = @($output | Where-Object { $_ -match '^\{.*\}$' } | Select-Object -Last 1)
    if (-not $line) { throw ($output -join [Environment]::NewLine) }
    $result.Probe = $line | ConvertFrom-Json
    $sourceAfter = (Get-FileHash -LiteralPath $sourceFile -Algorithm SHA256).Hash
    $result.SourceUnchanged = $sourceBefore -eq $sourceAfter

    $uninstaller = Join-Path $context.InstallRoot 'unins000.exe'
    $uninstall = Start-Process -FilePath $uninstaller -ArgumentList @('/VERYSILENT','/SUPPRESSMSGBOXES','/NORESTART') -Wait -PassThru
    $result.UninstallExitCode = $uninstall.ExitCode
    $result.UserDataRetained = Test-Path -LiteralPath $db
    $result.Passed = $result.OldInstallExitCode -eq 0 -and $result.OldInstallVerified -and $result.LegacyDataSeeded -and $result.OldLaunchSkippedForIsolation -and
        $result.NewInstallExitCode -eq 0 -and $result.NewStarted -and $result.NewRestarted -and [bool]$result.Probe.Passed -and
        $result.SourceUnchanged -and $result.UninstallExitCode -eq 0 -and $result.UserDataRetained
} catch {
    $result.Error = $_.Exception.ToString()
} finally {
    if ($process -and -not $process.HasExited) {
        Stop-Process -Id $process.Id -Force -ErrorAction SilentlyContinue
    }
    $result.CompletedAt = [DateTimeOffset]::Now.ToString('O')
    $result | ConvertTo-Json -Depth 12 | Set-Content -LiteralPath (Join-Path $context.EvidenceRoot 'result.json') -Encoding UTF8
}

Get-Content -LiteralPath (Join-Path $context.EvidenceRoot 'result.json') -Raw
if (-not $result.Passed) { exit 1 }
