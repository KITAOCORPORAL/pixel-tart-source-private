param([Parameter(Mandatory=$true)][string]$Root)
$ErrorActionPreference='Stop';$Root=[IO.Path]::GetFullPath($Root);$pub=Join-Path $Root 'Guangjing-BusinessFlow-Diagnostic-win-x64';$source='38dd3f92bb2db7238f7f058008887929ec971c93'
$env:GUANGJING_DIAGNOSTIC_DATA_ROOT=Join-Path $Root 'UserData-os-smoke';$env:PIXEL_TART_ACCEPTANCE='1'
if(Test-Path -LiteralPath $env:GUANGJING_DIAGNOSTIC_DATA_ROOT){throw 'Existing OS smoke fixture preserved; choose a new explicit root'}
$null=New-Item -ItemType Directory -Path $env:GUANGJING_DIAGNOSTIC_DATA_ROOT
Copy-Item -LiteralPath (Join-Path $Root 'UserData-release-final2/settings.json') -Destination (Join-Path $env:GUANGJING_DIAGNOSTIC_DATA_ROOT 'settings.json')
$exe=Join-Path $pub 'KitaoPhotoSelector.exe';$before=@(Get-CimInstance Win32_Process -Filter "Name = 'KitaoPhotoSelector.exe'"|Where-Object{$_.ExecutablePath -eq $exe}|Select-Object -ExpandProperty ProcessId)
$launcher=Start-Process -FilePath $env:ComSpec -ArgumentList @('/d','/c','Start-Guangjing-Diagnostic.cmd') -WorkingDirectory $pub -WindowStyle Hidden -PassThru
$app=$null;$deadline=(Get-Date).AddSeconds(40)
do{Start-Sleep -Milliseconds 100;foreach($candidate in @(Get-CimInstance Win32_Process -Filter "Name = 'KitaoPhotoSelector.exe'"|Where-Object{$_.ExecutablePath -eq $exe -and $_.ParentProcessId -eq $launcher.Id -and $_.ProcessId -notin $before})){$process=Get-Process -Id $candidate.ProcessId -ErrorAction SilentlyContinue;if($process){$null=$process.Handle;$app=$process;break}}}while(-not $app -and (Get-Date) -lt $deadline)
if(-not $app){throw 'Own diagnostic app did not launch'}
while($app.MainWindowHandle -eq 0 -and (Get-Date) -lt $deadline){Start-Sleep -Milliseconds 100;$app.Refresh()}
if($app.MainWindowHandle -eq 0){throw 'Own diagnostic app has no main window'}
$title=$app.MainWindowTitle;$appPid=$app.Id;$closed=$app.CloseMainWindow();if(-not $app.WaitForExit(25000)){throw 'Own app WM_CLOSE timeout; no unrelated app will be stopped'};$app.WaitForExit();if(-not $launcher.WaitForExit(10000)){throw 'Own launcher timeout'};$launcher.WaitForExit()
$log=(Get-ChildItem -LiteralPath (Join-Path $env:GUANGJING_DIAGNOSTIC_DATA_ROOT 'Logs') -Filter 'app-*.log' -File|ForEach-Object{Get-Content -LiteralPath $_.FullName -Raw}) -join [Environment]::NewLine
$readback=$log.Contains("ProductSourceSha=$source")
@{recordedAtShanghai=[TimeZoneInfo]::ConvertTimeBySystemTimeZoneId([DateTimeOffset]::UtcNow,'China Standard Time').ToString('o');appPid=$appPid;launcherPid=$launcher.Id;title=$title;closeRequested=$closed;exitCode=$app.ExitCode;launcherExitCode=$launcher.ExitCode;startupProductShaReadback=$readback;exeSha256=(Get-FileHash -LiteralPath $exe).Hash;cmdSha256=(Get-FileHash -LiteralPath (Join-Path $pub 'Start-Guangjing-Diagnostic.cmd')).Hash;isolatedRoot=$env:GUANGJING_DIAGNOSTIC_DATA_ROOT;scope='Own CMD/EXE process launch and WM_CLOSE only; NOT mouse, DPI, or independent Office acceptance'}|ConvertTo-Json|Set-Content (Join-Path $Root 'OS_CMD_SMOKE.json')
if(-not $readback -or -not $closed -or $app.ExitCode -ne 0 -or $launcher.ExitCode -ne 0){throw 'OS smoke failed'}
Add-Type -AssemblyName System.IO.Compression.FileSystem;$zip="$pub.zip";if(Test-Path -LiteralPath $zip){throw 'Existing ZIP preserved; refusing overwrite'}
[IO.Compression.ZipFile]::CreateFromDirectory($pub,$zip,[IO.Compression.CompressionLevel]::Optimal,$true)
$files=@(Get-ChildItem -LiteralPath $pub -Recurse -File|ForEach-Object{@{path=[IO.Path]::GetRelativePath($pub,$_.FullName).Replace([char]92,[char]47);bytes=$_.Length;sha256=(Get-FileHash -LiteralPath $_.FullName).Hash}})
$archive=[IO.Compression.ZipFile]::OpenRead($zip)
try{if($archive.Entries.Count -ne $files.Count){throw 'ZIP entry count mismatch'};foreach($file in $files){$entry=$archive.GetEntry((Split-Path $pub -Leaf)+'/'+$file.path);if(-not $entry -or $entry.Length -ne $file.bytes){throw 'ZIP entry missing or length mismatch'};$stream=$entry.Open();try{if([Convert]::ToHexString([Security.Cryptography.SHA256]::HashData($stream)) -ne $file.sha256){throw 'ZIP SHA mismatch'}}finally{$stream.Dispose()}}}finally{$archive.Dispose()}
$identity=@{zip=$zip;sha256=(Get-FileHash -LiteralPath $zip).Hash;fileCount=$files.Count;zipBytes=(Get-Item -LiteralPath $zip).Length;uncompressedBytes=($files|Measure-Object bytes -Sum).Sum;WholeAppDiagnosticBuild=$true;PhotographerTrialBuild=$false;productSource=$source;testSupport='d0c88e27291c432ace5dace5db8f1b3a6a8c5772';directoryAndZipEveryFileReadback='PASS';files=$files}
$identity|ConvertTo-Json -Depth 6|Set-Content (Join-Path $Root 'ZIP_IDENTITY.json');$identity|Select-Object fileCount,zipBytes,sha256|Format-List
Get-Content -LiteralPath (Join-Path $Root 'OS_CMD_SMOKE.json')
