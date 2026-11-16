param([Parameter(Mandatory=$true)][string]$Context,[Parameter(Mandatory=$true)][string]$Label,[ValidateSet('导入照片…','打开项目…','保存项目 / 重试保存','导出主变体…','退出暗室')][string]$Item)
$ErrorActionPreference='Stop'
$helper=Join-Path $PSScriptRoot 'Native-MainlineStep.ps1'
$c=Get-Content -LiteralPath $Context -Raw | ConvertFrom-Json
$folder=Join-Path $c.deliveryRoot 'native-mainline-uia'
& $helper -Context $Context -Label ($Label+'-button') -Action Invoke -AutomationId PhotoExport | Out-Null
& $helper -Context $Context -Label ($Label+'-menu') -Action Observe | Out-Null
$before=Get-Content (Join-Path $folder ($Label+'-button.json')) -Raw | ConvertFrom-Json
$after=Get-Content (Join-Path $folder ($Label+'-menu.json')) -Raw | ConvertFrom-Json
$handles=@($before.before | Where-Object {$_.type -eq 'ControlType.Window' -and $_.nativeHandle} | Select-Object -ExpandProperty nativeHandle -Unique)
$items=@($after.after | Where-Object {$_.type -eq 'ControlType.MenuItem' -and $_.name -eq $Item -and $_.enabled -and $_.menuHost -and ([long]$_.menuHost -notin $handles)} | Group-Object menuHost)
if($items.Count -ne 1){throw ('Fresh menu scope ambiguous: '+$items.Count)}
& $helper -Context $Context -Label ($Label+'-action') -Action Invoke -Name $Item -ScopeWindowHandle ([long]$items[0].Name) | Out-Null
