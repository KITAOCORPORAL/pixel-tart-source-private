param([Parameter(Mandatory)][string]$Delivery,[Parameter(Mandatory)][string]$Output)
$ErrorActionPreference='Stop';Add-Type -AssemblyName UIAutomationClient,UIAutomationTypes
$private=Join-Path $Delivery 'private-review';$desk=Get-Content (Join-Path $private 'desktop-process.json') -Raw|ConvertFrom-Json;$proc=Get-Process -Id $desk.pid
if($proc.Path -ne (Get-Item (Join-Path $Delivery 'Desktop/KitaoPhotoSelector.exe')).FullName -or $proc.StartTime.ToUniversalTime().Ticks -ne $desk.started){throw 'Wrong desktop instance'}
$root=[System.Windows.Automation.AutomationElement]::FromHandle($proc.MainWindowHandle)
function Elements {$root.FindAll([System.Windows.Automation.TreeScope]::Descendants,[System.Windows.Automation.Condition]::TrueCondition)}
function Control([string]$name,[string]$kind='Button') { (Elements)|Where-Object {$_.Current.Name -eq $name -and $_.Current.ControlType.ProgrammaticName -eq "ControlType.$kind"}|Select-Object -First 1 }
function Press($element) {if(!$element){throw 'Native control missing'};$element.GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern).Invoke();Start-Sleep -Milliseconds 350}
if(Control '取消'){Press (Control '取消')}
if(!(Control '批注分镜：细节 · 衣领纹理')){Press (Control '原编辑器选区');Start-Sleep -Milliseconds 500}
$body=(Elements)|Where-Object {$_.Current.AutomationId -eq 'PlanningRichDocumentBody'}|Select-Object -First 1
$range=$body.GetCurrentPattern([System.Windows.Automation.TextPattern]::Pattern).DocumentRange.FindText('秋冬外套',$false,$false);if(!$range){throw 'Original rich text selection missing'};$range.Select();Press (Control '批注所选文字')
if(!((Elements)|Where-Object {$_.Current.Name -eq '拍摄简报 / 秋冬外套'})){throw 'Native text range did not reach discussion'}
Press (Control '取消');Press (Control '原编辑器选区');Start-Sleep -Milliseconds 400;Press (Control '批注分镜：细节 · 衣领纹理');if(!((Elements)|Where-Object {$_.Current.Name -eq '分镜 / 细节 · 衣领纹理'})){throw 'Native shot did not reach discussion'};Press (Control '取消')
$jump=Control '参考图 / autumn-collar';Press $jump
@{host='WPF PlanningCenterView + WebView2';process=$proc.Id;nativeRichTextSelection='秋冬外套';nativeOriginalShot='细节 · 衣领纹理';sharedPanel='PASS';returnedTo='resolved collar note';binary=(Get-FileHash -LiteralPath (Join-Path $Delivery 'Desktop/KitaoPhotoSelector.dll')).Hash}|ConvertTo-Json|Set-Content -LiteralPath $Output
