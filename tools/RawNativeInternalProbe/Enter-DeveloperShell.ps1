param([Parameter(Mandatory)][string]$VsPath,[Parameter(Mandatory)][string]$Receipt)
$ErrorActionPreference='Stop'
if(Test-Path -LiteralPath $Receipt){throw 'Preserve earlier developer environment receipt'}
$batch=Join-Path $VsPath 'Common7/Tools/VsDevCmd.bat';if(!(Test-Path -LiteralPath $batch)){throw 'Installed Developer shell missing'}
$before=@{};foreach($n in @('OMP_NUM_THREADS','OMP_DYNAMIC','OMP_THREAD_LIMIT','OMP_SCHEDULE','OMP_PROC_BIND','OMP_PLACES','KMP_AFFINITY','KMP_BLOCKTIME')){$before[$n]=[Environment]::GetEnvironmentVariable($n)}
$cmd='call "'+$batch.Replace('/','\')+'" -no_logo -arch=x64 -host_arch=x64 && set'
$lines=& cmd.exe /d /c $cmd
if($LASTEXITCODE -ne 0){throw 'Developer shell initialization failed'}
$captured=@{};foreach($line in $lines){$at=$line.IndexOf('=');if($at -gt 0){$captured[$line.Substring(0,$at)]=$line.Substring($at+1)}}
$names=@('PATH','INCLUDE','LIB','LIBPATH','WindowsSdkDir','WindowsSDKVersion','WindowsSDKLibVersion','UniversalCRTSdkDir','UCRTVersion','VCToolsInstallDir','VCToolsVersion','VCINSTALLDIR','VSINSTALLDIR','VSCMD_ARG_TGT_ARCH','VSCMD_ARG_HOST_ARCH','VSCMD_VER','CommandPromptType')
foreach($n in $names){if($captured.ContainsKey($n)){[Environment]::SetEnvironmentVariable($n,$captured[$n],'Process')}}
foreach($n in $before.Keys){if([Environment]::GetEnvironmentVariable($n) -cne $before[$n]){throw 'Thread environment changed unexpectedly'}}
$tools=@{};foreach($n in @('cl.exe','nmake.exe','dumpbin.exe')){$tools[$n]=(Get-Command $n -ErrorAction Stop).Source}
@{scope='Current process only, actual installed x64 VsDevCmd';vsPath=$VsPath;target=$env:VSCMD_ARG_TGT_ARCH;host=$env:VSCMD_ARG_HOST_ARCH;tools=$tools;sdk=$env:WindowsSdkDir;sdkVersion=$env:WindowsSDKVersion;vcToolsVersion=$env:VCToolsVersion;threadEnvironmentBefore=$before;threadEnvironmentUnchanged=$true}|ConvertTo-Json -Depth 5|Set-Content -LiteralPath $Receipt
