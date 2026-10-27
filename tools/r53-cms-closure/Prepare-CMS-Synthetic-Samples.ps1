#requires -Version 7.5
[CmdletBinding()]
param([Parameter(Mandatory=$true)][string]$Destination)
$ErrorActionPreference='Stop'
if(-not[IO.Path]::IsPathFullyQualified($Destination)){throw 'Destination 必须是绝对路径。'}
$Destination=[IO.Path]::GetFullPath($Destination)
if(Test-Path -LiteralPath $Destination){throw '仅允许全新目录，不覆盖任何现有项目。'}
[IO.Directory]::CreateDirectory($Destination)|Out-Null
foreach($sample in @(@{Name='film-current-edits';Folder='flow-original-evidence';Input='input.tif';Project='workflow.ptstudio.json'},@{Name='wide-linear-v2';Folder='wide-original-evidence';Input='p3.tif';Project='wide.ptstudio.json'})){
 $target=Join-Path $Destination $sample.Name
 [IO.Directory]::CreateDirectory($target)|Out-Null
 $sourceRoot=Join-Path $PSScriptRoot ('samples/'+$sample.Folder)
 $input=Join-Path $target $sample.Input
 Copy-Item -LiteralPath (Join-Path $sourceRoot $sample.Input) -Destination $input
 $document=Get-Content -LiteralPath (Join-Path $sourceRoot $sample.Project) -Raw -Encoding utf8|ConvertFrom-Json -DateKind String
 $document.targets[0].path=$input
 $output=Join-Path $target $sample.Project
 [IO.File]::WriteAllText($output,($document|ConvertTo-Json -Depth 100),[Text.UTF8Encoding]::new($false))
 Write-Output ('暗室 → 文件 → 打开项目: '+$output)
}
Write-Output '只复制提供的合成图并重绑source路径；冻结ICC、版本ID、配方、种子和日期字符串保留。不改历史receipt，不扫描私人目录。'