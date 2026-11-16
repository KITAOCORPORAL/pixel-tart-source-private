param([Parameter(Mandatory=$true)][string]$Context,[Parameter(Mandatory=$true)][string]$Label,[ValidateSet('Observe','Invoke','SetValue','Toggle','Select','Expand','SelectItem','ReadValue','OpenMenuItem')][string]$Action='Observe',[string]$AutomationId='',[string]$Name='',[string]$Value='',[string]$ScopeAutomationId='',[string]$ScopeName='',[long]$ScopeWindowHandle=0)
$ErrorActionPreference='Stop'
Add-Type -AssemblyName UIAutomationClient,UIAutomationTypes
$c=Get-Content -LiteralPath $Context -Raw | ConvertFrom-Json
$p=Get-Process -Id $c.workflowAppPid
if($p.ProcessName -ne [IO.Path]::GetFileNameWithoutExtension($c.workflowAppExecutable)){throw 'Unexpected process; no action taken'}
$expectedPath=[IO.Path]::GetFullPath($(if($c.workflowAppExecutable){$c.workflowAppExecutable}else{Join-Path $c.package 'App/KitaoPhotoSelector.exe'}))
if($p.Path -ine $expectedPath -or $p.MainModule.FileVersionInfo.ProductVersion -ne ('2.3.0+'+$c.workflowAppProductSource)){throw 'Product executable or frozen source identity changed; no action taken'}
$root=[System.Windows.Automation.AutomationElement]::FromHandle($p.MainWindowHandle)
if($root.Current.ProcessId -ne $p.Id){throw 'Unexpected window process'}
$folder=Join-Path $c.deliveryRoot 'native-mainline-uia'
New-Item -ItemType Directory -Force -Path $folder | Out-Null
$path=Join-Path $folder ($Label+'.json')
if(Test-Path -LiteralPath $path){throw 'Preserve prior UI evidence; choose a fresh label'}
function ProductElements {
 $roots=[System.Windows.Automation.AutomationElement]::RootElement.FindAll([System.Windows.Automation.TreeScope]::Children,(New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::ProcessIdProperty,$p.Id)))
 $seen=New-Object 'System.Collections.Generic.HashSet[string]'
 foreach($r in $roots){foreach($candidate in @($r)+@($r.FindAll([System.Windows.Automation.TreeScope]::Descendants,[System.Windows.Automation.Condition]::TrueCondition))){$runtime=[string]::Join(':',$candidate.GetRuntimeId());if(!$runtime){throw 'Control runtime identity unavailable'};if($seen.Add($runtime)){$candidate}}}
}
function ControlIdentity($element) {
 $at=$element;$hostHandle=0
 while($null -ne $at){$hostHandle=$at.Current.NativeWindowHandle;if($hostHandle){break};$at=[System.Windows.Automation.TreeWalker]::ControlViewWalker.GetParent($at)}
 if(!$hostHandle){return ('runtime:'+([string]::Join(':',$element.GetRuntimeId())))}
 return ([string]$hostHandle+'|'+$element.Current.ControlType.Id+'|'+$element.Current.Name+'|'+$element.Current.BoundingRectangle.ToString())
}
function Snapshot {
 ProductElements | ForEach-Object {
  $current=$_.Current
  [pscustomobject]@{name=$current.Name;id=$current.AutomationId;type=$current.ControlType.ProgrammaticName;enabled=$current.IsEnabled;offscreen=$current.IsOffscreen;patterns=@($_.GetSupportedPatterns().ProgrammaticName);nativeHandle=$current.NativeWindowHandle;menuHost=if($current.ControlType -eq [System.Windows.Automation.ControlType]::MenuItem){try{(ControlIdentity $_).Split('|')[0]}catch{$null}}else{$null}}
 }
}
$before=@(Snapshot)
if($Action -ne 'Observe'){
 if($AutomationId){$condition=New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::AutomationIdProperty,$AutomationId)}
 elseif($Name){$condition=New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::NameProperty,$Name)}
 else{throw 'A unique observed identifier is required'}
 $actionElements=@(ProductElements)
 if($ScopeWindowHandle){$windowScopes=@($actionElements | Where-Object {$_.Current.ControlType -eq [System.Windows.Automation.ControlType]::Window -and $_.Current.NativeWindowHandle -eq $ScopeWindowHandle} | Group-Object {$_.Current.NativeWindowHandle} | ForEach-Object {$_.Group[0]});if($windowScopes.Count -ne 1){throw 'Native window scope is unavailable or ambiguous'};$actionElements=@($windowScopes[0].FindAll([System.Windows.Automation.TreeScope]::Descendants,[System.Windows.Automation.Condition]::TrueCondition))}
 if($ScopeAutomationId -or $ScopeName){$scopes=@($actionElements | Where-Object {$_.Current.IsEnabled -and !$_.Current.IsOffscreen -and $(if($ScopeAutomationId){$_.Current.AutomationId -eq $ScopeAutomationId}else{$_.Current.ControlType -eq [System.Windows.Automation.ControlType]::Group -and $_.Current.Name -eq $ScopeName})});if($scopes.Count -ne 1){throw ('Expected one live control scope; found '+$scopes.Count)};$actionElements=@($scopes[0].FindAll([System.Windows.Automation.TreeScope]::Descendants,[System.Windows.Automation.Condition]::TrueCondition))}
 $matches=@(($actionElements | Where-Object {$condition.Property -and $_.GetCurrentPropertyValue($condition.Property) -eq $condition.Value}) | Where-Object {
  if($Action -eq 'Invoke'){$_.Current.ControlType -in @([System.Windows.Automation.ControlType]::Button,[System.Windows.Automation.ControlType]::MenuItem)}elseif($Action -in @('SetValue','ReadValue')){$_.Current.ControlType -eq [System.Windows.Automation.ControlType]::Edit}else{$true}
 })
 $matches=@($matches | Where-Object {($Action -eq 'ReadValue' -or $_.Current.IsEnabled) -and !$_.Current.IsOffscreen})
 if($matches.Count -gt 1){$logical=@{};foreach($candidate in $matches){$identity=ControlIdentity $candidate;if(!$logical.ContainsKey($identity)){$logical[$identity]=$candidate}};$matches=@($logical.Values)}
 if($matches.Count -ne 1){throw ('Expected one observed control; found '+$matches.Count)}
 $e=$matches[0]
 if(($Action -ne 'ReadValue' -and !$e.Current.IsEnabled) -or $e.Current.IsOffscreen){throw 'Control is disabled or offscreen'}
 switch($Action){
  Invoke{$e.GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern).Invoke()}
  SetValue{$e.GetCurrentPattern([System.Windows.Automation.ValuePattern]::Pattern).SetValue($Value)}
  OpenMenuItem{$e.SetFocus();$e.GetCurrentPattern([System.Windows.Automation.ExpandCollapsePattern]::Pattern).Expand();$deadline=[DateTime]::UtcNow.AddSeconds(3);do{$items=@(ProductElements | Where-Object {$_.Current.ControlType -eq [System.Windows.Automation.ControlType]::MenuItem -and $_.Current.Name -eq $Value -and $_.Current.IsEnabled});if($items.Count -eq 0){Start-Sleep -Milliseconds 50}}while($items.Count -eq 0 -and [DateTime]::UtcNow -lt $deadline);if($items.Count -ne 1){throw ('Exact menu child ambiguous/unavailable: '+$items.Count)};$items[0].GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern).Invoke()}
  ReadValue{$readValue=$e.GetCurrentPattern([System.Windows.Automation.ValuePattern]::Pattern).Current.Value}
  Toggle{$e.GetCurrentPattern([System.Windows.Automation.TogglePattern]::Pattern).Toggle()}
  Select{$e.GetCurrentPattern([System.Windows.Automation.SelectionItemPattern]::Pattern).Select()}
  SelectItem{$e.GetCurrentPattern([System.Windows.Automation.ExpandCollapsePattern]::Pattern).Expand();$items=@($e.FindAll([System.Windows.Automation.TreeScope]::Descendants,[System.Windows.Automation.Condition]::TrueCondition) | Where-Object {$_.Current.ControlType -eq [System.Windows.Automation.ControlType]::ListItem -and $_.Current.Name -eq $Value -and $_.Current.IsEnabled -and !$_.Current.IsOffscreen});if($items.Count -ne 1){throw ('Expected one visible item after expansion; found '+$items.Count)};$items[0].GetCurrentPattern([System.Windows.Automation.SelectionItemPattern]::Pattern).Select()}
  Expand{$e.GetCurrentPattern([System.Windows.Automation.ExpandCollapsePattern]::Pattern).Expand()}
 }
}
$after=@(Snapshot)
[pscustomobject]@{atUtc=[DateTimeOffset]::UtcNow.ToString('O');pid=$p.Id;app=$p.Path;authority='Running production window, UI Automation patterns; no injected ViewModel commands or pointer input';action=$Action;automationId=$AutomationId;name=$Name;observedValue=$readValue;before=$before;after=$after} | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $path -Encoding UTF8
$after | Where-Object {$_.name -and ($_.type -ne 'ControlType.Text' -or $_.name -match '完成|扫描|索引|已添加|已保存|匹配|失败|处理|JPG|RAW|合成项目')} | Select-Object name,id,type,enabled | ConvertTo-Json -Compress
