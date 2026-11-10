param([string]$PackageRoot=(Split-Path -Parent $PSScriptRoot))
$ErrorActionPreference='Stop'
$PackageRoot=[IO.Path]::GetFullPath($PackageRoot)
$configFile=Join-Path $PackageRoot 'native-host/capture-host-config.json'
if(!(Test-Path -LiteralPath $configFile)){throw 'Prepare the native host registration files first'}
$config=Get-Content -LiteralPath $configFile -Raw|ConvertFrom-Json
$si=[Diagnostics.ProcessStartInfo]::new();$si.FileName=Join-Path $PackageRoot 'app/KitaoPhotoSelector.exe';$si.WorkingDirectory=Join-Path $PackageRoot 'app';$si.UseShellExecute=$false
$si.EnvironmentVariables['PIXEL_TART_ISOLATED_RUNTIME']='1';$si.EnvironmentVariables['PIXEL_TART_ISOLATED_RUNTIME_ROOT']=$config.dataRoot;$si.EnvironmentVariables['GUANGJING_CAPTURE_PIPE']=$config.pipeName;$si.EnvironmentVariables['GUANGJING_CAPTURE_EXTENSION_ID']=$config.extensionId
$process=[Diagnostics.Process]::Start($si);Write-Output ('Guangjing capture desktop PID: '+$process.Id)
