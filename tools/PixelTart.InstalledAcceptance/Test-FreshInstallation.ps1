param([Parameter(Mandatory)][string]$Installer,[Parameter(Mandatory)][string]$PublishDirectory,[Parameter(Mandatory)][string]$SourceSha)
$ErrorActionPreference='Stop'
if($SourceSha -notmatch '^[a-f0-9]{40}$'){throw 'Full source SHA required'}
$safe=Join-Path $env:LOCALAPPDATA 'PixelTart-TestAcceptance'
foreach($registry in @('HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall','HKLM:\Software\Microsoft\Windows\CurrentVersion\Uninstall','HKLM:\Software\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall')) {
    $key=Join-Path $registry '{3A5A5B1B-8A11-4E85-9C54-1B18D0E5D2F4}_is1'
    if(Test-Path $key){$location=(Get-ItemProperty $key).InstallLocation;if(!$location -or ![IO.Path]::GetFullPath($location).StartsWith($safe+'\',[StringComparison]::OrdinalIgnoreCase)){throw 'Existing non-test installation; will not alter registration'}}
}
if(Get-Process PixelTart -ErrorAction SilentlyContinue){throw 'Existing app; never force-close'}
$root=Join-Path $safe ('OnePass_'+$SourceSha.Substring(0,7)+'_'+[guid]::NewGuid().ToString('N'))
$installed=Join-Path $root 'installed'; $data=Join-Path $root 'fresh-data'
New-Item -ItemType Directory -Path $root,$data|Out-Null
$arguments=@('/VERYSILENT','/SUPPRESSMSGBOXES','/NORESTART','/SP-','/NOICONS','/TASKS=','/NOCLOSEAPPLICATIONS',('/DIR="'+$installed+'"'),('/LOG="'+(Join-Path $root 'install.log')+'"'))
$setup=Start-Process -FilePath ([IO.Path]::GetFullPath($Installer)) -ArgumentList $arguments -PassThru -Wait -WindowStyle Hidden
if($setup.ExitCode -ne 0){throw "Installer failed: $($setup.ExitCode)"}
$mismatch=@(Get-ChildItem -LiteralPath $PublishDirectory -File -Recurse|Where-Object {
    $relative=[IO.Path]::GetRelativePath([IO.Path]::GetFullPath($PublishDirectory),$_.FullName);$target=Join-Path $installed $relative
    !(Test-Path -LiteralPath $target) -or (Get-FileHash -LiteralPath $_.FullName).Hash -ne (Get-FileHash -LiteralPath $target).Hash
})
if($mismatch.Count){throw 'Installed files differ from publish'}
$start=[Diagnostics.ProcessStartInfo]::new((Join-Path $installed 'PixelTart.exe'));$start.UseShellExecute=$false;$start.WindowStyle=[Diagnostics.ProcessWindowStyle]::Hidden
$start.Environment['PIXEL_TART_HUMAN_ACCEPTANCE']='1';$start.Environment['PIXEL_TART_ACCEPTANCE_ROOT']=$data
$app=[Diagnostics.Process]::Start($start)
# No UIA or input; startup logs and process identity only. Leave UI for supported Computer Use.
$started=[datetime]::UtcNow
do {
    if($app.HasExited){throw 'Installed app exited during startup'}
    $logs=@(Get-ChildItem -LiteralPath (Join-Path $data 'Logs') -Filter 'app-*.log' -ErrorAction SilentlyContinue)
    $ready=$logs|Where-Object{Select-String -LiteralPath $_.FullName -Pattern 'STARTUP_OK' -Quiet}
    if($ready){break}
    Start-Sleep -Milliseconds 200
} while(([datetime]::UtcNow-$started).TotalSeconds -lt 40)
if(!$ready){throw 'No STARTUP_OK within 40 seconds'}
$result=[ordered]@{SourceSha=$SourceSha;InstallerSHA256=(Get-FileHash -LiteralPath $Installer).Hash;InstalledPath=$installed;DataRoot=$data;PID=$app.Id;InstallExit=$setup.ExitCode;FileMismatchCount=$mismatch.Count;Startup='PASS';InstalledUI='NOT_TESTED';NormalClose='NOT_TESTED'}
$result|ConvertTo-Json|Set-Content -LiteralPath (Join-Path $root 'installation-result.json') -Encoding utf8
$result|ConvertTo-Json
