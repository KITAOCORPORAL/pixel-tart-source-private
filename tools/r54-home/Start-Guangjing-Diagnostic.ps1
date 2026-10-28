[CmdletBinding()]
param([string]$DataRoot)
$ErrorActionPreference='Stop'
$package=[IO.Path]::GetFullPath($PSScriptRoot)
$exe=Join-Path $package 'app/KitaoPhotoSelector.exe'
if(-not(Test-Path -LiteralPath $exe -PathType Leaf)){throw '完整诊断主程序缺失。'}
if([string]::IsNullOrWhiteSpace($DataRoot)){
 $DataRoot=Join-Path $package ('UserData/'+[DateTime]::UtcNow.ToString('yyyyMMdd-HHmmss')+'-'+[Guid]::NewGuid().ToString('N').Substring(0,8))
}
if(-not [IO.Path]::IsPathFullyQualified($DataRoot)){throw 'DataRoot 必须为绝对路径。'}
$DataRoot=[IO.Path]::GetFullPath($DataRoot)
$appRoot=[IO.Path]::GetFullPath((Join-Path $package 'app'))
if($DataRoot.Equals($appRoot,[StringComparison]::OrdinalIgnoreCase)-or $DataRoot.StartsWith($appRoot+[IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase)){throw '数据根不能位于 app 内。'}
$marker=Join-Path $DataRoot 'guangjing-r53-diagnostic-root.json'
if(Test-Path -LiteralPath $DataRoot){
 if(-not(Test-Path -LiteralPath $marker -PathType Leaf)){
  if(@(Get-ChildItem -LiteralPath $DataRoot -Force).Count -gt 0){throw '拒绝使用非空且没有诊断标识的数据根；请选择新的隔离目录。'}
 }
}
[IO.Directory]::CreateDirectory($DataRoot)|Out-Null
if(-not(Test-Path -LiteralPath $marker)){
 [IO.File]::WriteAllText($marker,'{"diagnosticOnly":true,"sourceSha":"14bbb60debdd0b176490adb24747023de59b1dbf"}',[Text.UTF8Encoding]::new($false))
}
$start=[Diagnostics.ProcessStartInfo]::new()
$start.FileName=$exe
$start.WorkingDirectory=Join-Path $package 'app'
$start.UseShellExecute=$false
$start.CreateNoWindow=$true
$start.EnvironmentVariables['PIXEL_TART_ISOLATED_RUNTIME']='1'
$start.EnvironmentVariables['PIXEL_TART_ISOLATED_RUNTIME_ROOT']=$DataRoot
$process=[Diagnostics.Process]::Start($start)
Write-Output ('诊断版（不能发行），PID: '+$process.Id)
Write-Output ('完整应用: '+$exe)
Write-Output ('隔离数据根: '+$DataRoot)