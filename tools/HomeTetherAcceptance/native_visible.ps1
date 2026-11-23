param([int]$ProcessId,[string]$Name,[ValidateSet('Button','Edit','ComboBox','MenuItem','ListItem','Slider')][string]$Type='Button',[ValidateSet('Read','Invoke','Set','Select','Range')][string]$Action='Read',[string]$Value='',[string]$Receipt='')
$ErrorActionPreference='Stop'
Add-Type -AssemblyName UIAutomationClient
Add-Type -AssemblyName UIAutomationTypes
$c=Get-Content (Join-Path $PSScriptRoot '../../artifacts/home-tether-20261123/CANDIDATE.json') -Raw|ConvertFrom-Json
$expected=Join-Path $c.package 'App/KitaoPhotoSelector.exe';$process=Get-Process -Id $ProcessId
if($process.Path -ine $expected){throw 'Target is not owned candidate binary'}
$windows=[System.Windows.Automation.AutomationElement]::RootElement.FindAll([System.Windows.Automation.TreeScope]::Children,[System.Windows.Automation.PropertyCondition]::new([System.Windows.Automation.AutomationElement]::ProcessIdProperty,$ProcessId))
$roots=@($windows|Where-Object {$_.Current.Name -like '光镜*完整范围诊断*'});if($roots.Count -ne 1){throw 'Main window identity ambiguous'}
$all=$roots[0].FindAll([System.Windows.Automation.TreeScope]::Descendants,[System.Windows.Automation.Condition]::TrueCondition);$control=[System.Windows.Automation.ControlType]::$Type
$matches=@($all|Where-Object {$_.Current.Name -eq $Name -and $_.Current.ControlType -eq $control -and -not $_.Current.IsOffscreen}|Group-Object {($_.GetRuntimeId()) -join ','}|ForEach-Object {$_.Group[0]});if($matches.Count -ne 1){throw ('Visible control is not unique: '+$matches.Count)}
$record=@{pid=$ProcessId;path=$process.Path;name=$Name;type=$Type;visibleMatches=$matches.Count;runtimeId=($matches[0].GetRuntimeId() -join ',');action=$Action;authority='Normal EXE external native UIA; no product reflection/data injection'}
if($Action -eq 'Invoke'){([System.Windows.Automation.InvokePattern]$matches[0].GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern)).Invoke();$record.delivery='Invoked once; product readback needed'}
if($Action -eq 'Select'){([System.Windows.Automation.SelectionItemPattern]$matches[0].GetCurrentPattern([System.Windows.Automation.SelectionItemPattern]::Pattern)).Select();$record.delivery='SelectionItemPattern once'}
if($Action -eq 'Range'){([System.Windows.Automation.RangeValuePattern]$matches[0].GetCurrentPattern([System.Windows.Automation.RangeValuePattern]::Pattern)).SetValue([double]::Parse($Value,[Globalization.CultureInfo]::InvariantCulture));$record.value=([System.Windows.Automation.RangeValuePattern]$matches[0].GetCurrentPattern([System.Windows.Automation.RangeValuePattern]::Pattern)).Current.Value}
if($Action -eq 'Set'){([System.Windows.Automation.ValuePattern]$matches[0].GetCurrentPattern([System.Windows.Automation.ValuePattern]::Pattern)).SetValue($Value);$record.value=([System.Windows.Automation.ValuePattern]$matches[0].GetCurrentPattern([System.Windows.Automation.ValuePattern]::Pattern)).Current.Value}
if($Receipt){$record|ConvertTo-Json -Depth 5|Set-Content -LiteralPath $Receipt -Encoding utf8};$record|ConvertTo-Json -Depth 5
