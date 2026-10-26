#requires -Version 7.5
[CmdletBinding()]
param([Parameter(Mandatory=$true)][string]$Destination)
$ErrorActionPreference='Stop'
if(-not [IO.Path]::IsPathFullyQualified($Destination)){throw 'Destination 必须为绝对路径。'}
$Destination=[IO.Path]::GetFullPath($Destination)
if(Test-Path -LiteralPath $Destination){throw '只允许全新样例目录，不覆盖已有文件。'}
[IO.Directory]::CreateDirectory($Destination)|Out-Null
$source=Join-Path $PSScriptRoot 'samples/r53-synthetic.png'
$image=Join-Path $Destination 'r53-synthetic.png'
Copy-Item -LiteralPath $source -Destination $image
$document=Get-Content -LiteralPath (Join-Path $PSScriptRoot 'samples/original-evidence/r53-stage-a.ptstudio.json') -Raw -Encoding UTF8|ConvertFrom-Json -DateKind String
$document.targets[0].path=$image
$session=Join-Path $Destination 'r53-stage-a.ptstudio.json'
$json=$document|ConvertTo-Json -Depth 100
[IO.File]::WriteAllText($session,$json,[Text.UTF8Encoding]::new($false))
Write-Output ('请在完整光镜应用：暗室 → 文件 → 打开项目，选择 '+$session)
Write-Output '仅重绑所提供合成图路径，不复制私人照片，不改写原始验证证据。'