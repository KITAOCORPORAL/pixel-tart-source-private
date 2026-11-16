param([Parameter(Mandatory=$true)][string]$Context,[Parameter(Mandatory=$true)][string]$Label,[ValidateSet('Observe','Invoke','SetValue','Select','InvokeMenuChild','Minimize','Restore')][string]$Action='Observe',[string]$RuntimeId='',[string]$Value='')
$ErrorActionPreference='Stop'
Add-Type -AssemblyName UIAutomationClient,UIAutomationTypes
$c=Get-Content -LiteralPath $Context -Raw | ConvertFrom-Json
$p=Get-Process -Id $c.workflowAppPid
if($p.Path -ine [IO.Path]::GetFullPath($c.workflowAppExecutable) -or $p.MainModule.FileVersionInfo.ProductVersion -ne ('2.3.0+'+$c.workflowAppProductSource)){throw 'Process/source binding mismatch'}
$outdir=Join-Path $c.deliveryRoot 'native-continuation-uia'
New-Item -ItemType Directory -Force -Path $outdir | Out-Null
$outfile=Join-Path $outdir ($Label+'.json')
if(Test-Path -LiteralPath $outfile){throw 'Receipt exists; select fresh label'}
function Elements {
 $roots=[System.Windows.Automation.AutomationElement]::RootElement.FindAll([System.Windows.Automation.TreeScope]::Children,(New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::ProcessIdProperty,$p.Id)))
 $seen=New-Object 'System.Collections.Generic.HashSet[string]'
 foreach($r in $roots){foreach($e in @($r)+@($r.FindAll([System.Windows.Automation.TreeScope]::Descendants,[System.Windows.Automation.Condition]::TrueCondition))){$id=[string]::Join(':',$e.GetRuntimeId());if($seen.Add($id)){$e}}}
}
function Snapshot {
 foreach($e in @(Elements)){
  $q=$e.Current;$v=$null;$sel=$null;$window=$null
  $pat=$null;if($e.TryGetCurrentPattern([System.Windows.Automation.ValuePattern]::Pattern,[ref]$pat)){$v=$pat.Current.Value}
  $pat=$null;if($e.TryGetCurrentPattern([System.Windows.Automation.RangeValuePattern]::Pattern,[ref]$pat)){$v=$pat.Current.Value}
  $pat=$null;if($e.TryGetCurrentPattern([System.Windows.Automation.SelectionPattern]::Pattern,[ref]$pat)){$sel=@($pat.Current.GetSelection() | ForEach-Object {$_.Current.Name})}
  $pat=$null;if($e.TryGetCurrentPattern([System.Windows.Automation.WindowPattern]::Pattern,[ref]$pat)){$window=[string]$pat.Current.WindowVisualState}
  $ancestor=@();$at=$e
  for($i=0;$i -lt 4;$i++){$at=[System.Windows.Automation.TreeWalker]::ControlViewWalker.GetParent($at);if($null -eq $at){break};$ancestor+=($at.Current.ClassName+'|'+$at.Current.AutomationId+'|'+$at.Current.Name)}
  [pscustomobject]@{name=$q.Name;id=$q.AutomationId;runtimeId=[string]::Join(':',$e.GetRuntimeId());type=$q.ControlType.ProgrammaticName;class=$q.ClassName;enabled=$q.IsEnabled;offscreen=$q.IsOffscreen;rect=$q.BoundingRectangle.ToString();nativeHandle=$q.NativeWindowHandle;value=$v;selection=$sel;visualState=$window;ancestors=$ancestor;patterns=@($e.GetSupportedPatterns().ProgrammaticName)}
 }
}
$before=@(Snapshot)
[pscustomobject]@{atUtc=[DateTimeOffset]::UtcNow.ToString('O');action=$Action;target=$RuntimeId;before=$before} | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath ($outfile+'.started.json') -Encoding utf8
if($Action -ne 'Observe'){
 $targets=@(Elements | Where-Object {[string]::Join(':',$_.GetRuntimeId()) -eq $RuntimeId})
 if($targets.Count -ne 1){throw 'Observed runtime identity unavailable or ambiguous'}
 $e=$targets[0]
 if($e.Current.ProcessId -ne $p.Id -or !$e.Current.IsEnabled){throw 'Control owner/enabled state mismatch'}
 if($e.Current.IsOffscreen -and $Action -ne 'Restore'){throw 'Control offscreen; reobserve'}
 switch($Action){
  Invoke{$e.GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern).Invoke()}
  SetValue{$pat=$null;if($e.TryGetCurrentPattern([System.Windows.Automation.RangeValuePattern]::Pattern,[ref]$pat)){$pat.SetValue([double]$Value)}else{$e.GetCurrentPattern([System.Windows.Automation.ValuePattern]::Pattern).SetValue($Value)}}
  Select{$e.GetCurrentPattern([System.Windows.Automation.SelectionItemPattern]::Pattern).Select()}
  InvokeMenuChild{$e.GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern).Invoke();$deadline=[DateTime]::UtcNow.AddSeconds(5);do{$items=@(Elements | Where-Object {$_.Current.ControlType -eq [System.Windows.Automation.ControlType]::MenuItem -and $_.Current.Name -eq $Value -and $_.Current.IsEnabled -and !$_.Current.IsOffscreen});if($items.Count -eq 0){Start-Sleep -Milliseconds 50}}while($items.Count -eq 0 -and [DateTime]::UtcNow -lt $deadline);if($items.Count -ne 1){throw ('Live menu child ambiguous/unavailable: '+$items.Count)};$items[0].GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern).Invoke()}
  Minimize{$e.GetCurrentPattern([System.Windows.Automation.WindowPattern]::Pattern).SetWindowVisualState([System.Windows.Automation.WindowVisualState]::Minimized)}
  Restore{$e.GetCurrentPattern([System.Windows.Automation.WindowPattern]::Pattern).SetWindowVisualState([System.Windows.Automation.WindowVisualState]::Normal)}
 }
}
[pscustomobject]@{atUtc=[DateTimeOffset]::UtcNow.ToString('O');action=$Action;target=$RuntimeId;actionReturned=$true} | ConvertTo-Json | Set-Content -LiteralPath ($outfile+'.returned.json') -Encoding utf8
$after=@(Snapshot)
[pscustomobject]@{atUtc=[DateTimeOffset]::UtcNow.ToString('O');authority='Native UI Automation in bound ordinary product process; no ViewModel command injection, no screen-pixel claim';pid=$p.Id;source=$c.workflowAppProductSource;app=$p.Path;action=$Action;targetRuntimeId=$RuntimeId;requestedValue=$Value;before=$before;after=$after} | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $outfile -Encoding utf8
Write-Output $outfile
