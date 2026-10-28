#requires -Version 7.5
[CmdletBinding()]
param([Parameter(Mandatory=$true)][string]$Destination)
$ErrorActionPreference='Stop'
if(-not[IO.Path]::IsPathFullyQualified($Destination)){throw '请选择绝对路径。'}
if(Test-Path -LiteralPath $Destination){throw '仅允许全新目录，不覆盖已有项目。'}
[IO.Directory]::CreateDirectory($Destination)|Out-Null
$source=Join-Path $PSScriptRoot 'samples/graph-original-evidence'
$image=Join-Path ([IO.Path]::GetFullPath($Destination)) 'synthetic.png'
Copy-Item -LiteralPath (Join-Path $source 'synthetic.png') -Destination $image
$project=Get-Content -LiteralPath (Join-Path $source 'independent.ptstudio.json') -Raw -Encoding UTF8|ConvertFrom-Json -DateKind String
$project.targets[0].path=$image
$output=Join-Path $Destination 'R54-home-synthetic.ptstudio.json'
[IO.File]::WriteAllText($output,($project|ConvertTo-Json -Depth 100),[Text.UTF8Encoding]::new($false))
Write-Output ('暗室 → 文件 → 打开项目：'+$output)
Write-Output '仅复制提供的合成图片并重绑source路径；路由/ID/蒙版/配方/日期字符串保留。未修改原始证据。'