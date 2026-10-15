param([Parameter(Mandatory=$true)][string]$ReleaseDirectory,[Parameter(Mandatory=$true)][string]$ReleaseManifest,[Parameter(Mandatory=$true)][string]$NewRunDirectory,[switch]$LaunchWhenIdle)
$ErrorActionPreference='Stop'
$release=[IO.Path]::GetFullPath($ReleaseDirectory);$run=[IO.Path]::GetFullPath($NewRunDirectory)
if(Test-Path -LiteralPath $run){throw 'New isolated run directory required; never overwrite'}
$manifest=Get-Content -Raw -LiteralPath $ReleaseManifest|ConvertFrom-Json
foreach($file in $manifest.files){$p=Join-Path $release $file.path;if(!(Test-Path -LiteralPath $p) -or (Get-FileHash -LiteralPath $p -Algorithm SHA256).Hash -ne $file.sha256){throw ('Release identity mismatch: '+$file.path)}}
New-Item -ItemType Directory -Path $run|Out-Null;New-Item -ItemType Directory -Path (Join-Path $run 'runtime')|Out-Null
$items=@('StartupNoBlockingDialog','WorkbenchToPlanning','WorkbenchToColorStudio','TrayImageSimple','TrayImageAdvanced','RepeatedImageReferences','MoveProjectAndOriginalFolderThenReopen','UndoRedo','DocxExportAndReadback','ColorPhotoSwitchCancellation','ColorRecipeSaveReopen','Tiff16Png16JpegExport','PhysicalDPI100','PhysicalDPI125','PhysicalDPI150','PhysicalDPI200','WpsOpenEditSaveReimport')
@{businessTimezone='Asia/Shanghai';release=$release;sourceSha=$manifest.sourceSha;started=$false;items=@($items|ForEach-Object{@{id=$_;backend='SEPARATE';nativeClick='NOT_RUN';visualApproval='NOT_RUN';wpsRoundtrip='NOT_RUN';evidence=@();reason='Await reserved idle desktop'}})}|ConvertTo-Json -Depth 8|Set-Content -LiteralPath (Join-Path $run 'acceptance.json') -Encoding utf8
if(!$LaunchWhenIdle){Write-Output ('Prepared only, no desktop input: '+$run);exit 0}
Add-Type -TypeDefinition @'
using System;using System.Runtime.InteropServices;public static class NativeIdle{[StructLayout(LayoutKind.Sequential)]public struct L{public uint size,tick;}[DllImport("user32.dll")]public static extern bool GetLastInputInfo(ref L p);[DllImport("kernel32.dll")]public static extern uint GetTickCount();}
'@
$x=New-Object NativeIdle+L;$x.size=8;if(![NativeIdle]::GetLastInputInfo([ref]$x)){throw 'Cannot verify idle; do not launch'}
$idle=(([long][NativeIdle]::GetTickCount()-[long]$x.tick+4294967296)%4294967296)
@{idleMs=$idle;observedAt=(Get-Date -Format o);inputSent=$false}|ConvertTo-Json|Set-Content -LiteralPath (Join-Path $run 'idle.json')
if($idle -lt 600000){Write-Output 'NOT_RUN: desktop not reserved/idle10minutes. No launch or input.';exit 2}
$env:PIXEL_TART_ISOLATED_RUNTIME='1';$env:PIXEL_TART_ISOLATED_RUNTIME_ROOT=Join-Path $run 'runtime'
$p=Start-Process -FilePath (Join-Path $release 'KitaoPhotoSelector.exe') -WorkingDirectory $release -PassThru
@{pid=$p.Id;exe=(Join-Path $release 'KitaoPhotoSelector.exe');time=(Get-Date -Format o);notProofOfStartup=$true}|ConvertTo-Json|Set-Content -LiteralPath (Join-Path $run 'process.json')
Write-Output 'Started sealed app in isolated data. Human checklist required. Stop immediately on any user-input detection; record click UNKNOWN, not Passed. Never change physical DPI while another user is active.'
