param([int]$ProcessId,[string]$ButtonName,[string]$ItemName,[string]$Receipt,[ValidateSet("Button","MenuItem")][string]$TriggerType="Button")
$ErrorActionPreference='Stop'
Add-Type -AssemblyName UIAutomationClient
Add-Type -AssemblyName UIAutomationTypes
$c=Get-Content (Join-Path $PSScriptRoot '../../artifacts/home-tether-20261123/CANDIDATE.json') -Raw|ConvertFrom-Json
$p=Get-Process -Id $ProcessId;if($p.Path -ine (Join-Path $c.package 'App/KitaoPhotoSelector.exe')){throw 'Wrong owned process'}
$pidCondition=[System.Windows.Automation.PropertyCondition]::new([System.Windows.Automation.AutomationElement]::ProcessIdProperty,$ProcessId)
$windows=[System.Windows.Automation.AutomationElement]::RootElement.FindAll([System.Windows.Automation.TreeScope]::Children,$pidCondition)
$root=@($windows|Where-Object {$_.Current.Name -like '光镜*完整范围诊断*'});if($root.Count -ne 1){throw 'Main identity not unique'}
$elements=$root[0].FindAll([System.Windows.Automation.TreeScope]::Descendants,[System.Windows.Automation.Condition]::TrueCondition)
$button=@($elements|Where-Object {$_.Current.ControlType -eq [System.Windows.Automation.ControlType]::$TriggerType -and $_.Current.Name -eq $ButtonName -and -not $_.Current.IsOffscreen}|Group-Object {($_.GetRuntimeId()) -join ','}|ForEach-Object {$_.Group[0]});if($button.Count -ne 1){throw 'File button not unique'}
if($TriggerType -eq "MenuItem"){([System.Windows.Automation.ExpandCollapsePattern]$button[0].GetCurrentPattern([System.Windows.Automation.ExpandCollapsePattern]::Pattern)).Expand()}else{([System.Windows.Automation.InvokePattern]$button[0].GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern)).Invoke()}
$watch=[Diagnostics.Stopwatch]::StartNew();$found=@()
do{
 $windows=[System.Windows.Automation.AutomationElement]::RootElement.FindAll([System.Windows.Automation.TreeScope]::Children,$pidCondition)
 $rawFound=@(foreach($w in $windows){$all=$w.FindAll([System.Windows.Automation.TreeScope]::Subtree,[System.Windows.Automation.Condition]::TrueCondition);$all|Where-Object {$_.Current.ControlType -eq [System.Windows.Automation.ControlType]::MenuItem -and $_.Current.Name -eq $ItemName -and -not $_.Current.IsOffscreen}});$found=@($rawFound|Group-Object {($_.GetRuntimeId()) -join ','}|ForEach-Object {$_.Group[0]})
 if($found.Count -eq 0){Start-Sleep -Milliseconds 50}
}until($found.Count -gt 0 -or $watch.Elapsed.TotalSeconds -gt 3)
if($found.Count -ne 1){throw ('Menu item identity not unique: '+$found.Count)}
([System.Windows.Automation.InvokePattern]$found[0].GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern)).Invoke()
$record=@{pid=$ProcessId;path=$p.Path;button=$ButtonName;menuItem=$ItemName;menuRuntimeId=($found[0].GetRuntimeId() -join ',');visibleUniqueMatches=$found.Count;authority='Single native menu open and InvokePattern leaf through ordinary UI; no product mutation';delivery='Invoked once; read product dialog/result'}
$record|ConvertTo-Json -Depth 5|Set-Content -LiteralPath $Receipt -Encoding utf8;$record|ConvertTo-Json -Depth 5
