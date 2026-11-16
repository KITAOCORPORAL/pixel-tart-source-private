param([ValidateSet('Inspect','SetFile','Confirm')][string]$Action='Inspect',[Parameter(Mandatory=$true)][string]$DialogTitle,[Parameter(Mandatory=$true)][string]$TargetPath,[ValidateSet('Open','Save','Folder')][string]$Mode='Open',[Parameter(Mandatory=$true)][string]$ReceiptPrefix,[string]$Context='E:/Kitao-Deliveries/product-functional-integration-20261113/INTEGRATED_WORKFLOW_CONTEXT.json')
$ErrorActionPreference='Stop'
Add-Type -AssemblyName UIAutomationClient,UIAutomationTypes
Add-Type @'
using System;using System.Collections.Generic;using System.Runtime.InteropServices;using System.Text;
public static class MainlineNativeDialog {
 public delegate bool EnumProc(IntPtr w,IntPtr p);
 [DllImport("user32.dll")]public static extern bool EnumChildWindows(IntPtr p,EnumProc cb,IntPtr l);
 [DllImport("user32.dll")]public static extern int GetDlgCtrlID(IntPtr w);
 [DllImport("user32.dll")]public static extern IntPtr GetParent(IntPtr w);
 [DllImport("user32.dll")]public static extern uint GetWindowThreadProcessId(IntPtr w,out uint p);
 [DllImport("user32.dll",CharSet=CharSet.Unicode)]static extern int GetClassName(IntPtr w,StringBuilder b,int n);
 [DllImport("user32.dll",CharSet=CharSet.Unicode)]static extern int GetWindowText(IntPtr w,StringBuilder b,int n);
 [DllImport("user32.dll",CharSet=CharSet.Unicode,SetLastError=true)]public static extern IntPtr SendMessageTimeout(IntPtr h,uint m,IntPtr w,string l,uint f,uint t,out IntPtr r);
 [DllImport("user32.dll",SetLastError=true)]public static extern IntPtr SendMessageTimeout(IntPtr h,uint m,IntPtr w,IntPtr l,uint f,uint t,out IntPtr r);
 [DllImport("user32.dll",CharSet=CharSet.Unicode,EntryPoint="SendMessageTimeoutW",SetLastError=true)]static extern IntPtr SendTextMessage(IntPtr h,uint m,IntPtr w,StringBuilder b,uint f,uint t,out IntPtr r);
 public static string EditText(IntPtr h){var b=new StringBuilder(4096);IntPtr r;var ok=SendTextMessage(h,0x000D,(IntPtr)b.Capacity,b,2,1000,out r);if(ok==IntPtr.Zero)throw new InvalidOperationException("Filename readback timeout");return b.ToString();}
 public static string Class(IntPtr h){var b=new StringBuilder(256);GetClassName(h,b,b.Capacity);return b.ToString();}
 public static string Text(IntPtr h){var b=new StringBuilder(4096);GetWindowText(h,b,b.Capacity);return b.ToString();}
 public static IntPtr[] Children(IntPtr h){var r=new List<IntPtr>();EnumChildWindows(h,(w,p)=>{r.Add(w);return true;},IntPtr.Zero);return r.ToArray();}
}
'@
$delivery='E:/Kitao-Deliveries/company-functional-20261116'
$c=Get-Content -LiteralPath $Context -Raw | ConvertFrom-Json
$p=Get-Process -Id $c.workflowAppPid
$expected=[IO.Path]::GetFullPath($(if($c.workflowAppExecutable){$c.workflowAppExecutable}else{Join-Path $c.package 'App/KitaoPhotoSelector.exe'}))
if($p.Path -ine $expected -or $p.Id -ne $c.workflowAppPid){throw 'Target product process identity changed'}
$title=$DialogTitle
$roots=[System.Windows.Automation.AutomationElement]::RootElement.FindAll([System.Windows.Automation.TreeScope]::Children,(New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::ProcessIdProperty,$p.Id)))
$all=@(foreach($r in $roots){$r;$r.FindAll([System.Windows.Automation.TreeScope]::Descendants,[System.Windows.Automation.Condition]::TrueCondition)})
$dialogs=@($all | Where-Object {$_.Current.Name -eq $title -and $_.Current.ControlType -eq [System.Windows.Automation.ControlType]::Window} | Group-Object { $_.Current.NativeWindowHandle } | ForEach-Object {$_.Group[0]})
if($dialogs.Count -ne 1){throw ('Expected exactly one observed import dialog, found '+$dialogs.Count)}
$h=[IntPtr]$dialogs[0].Current.NativeWindowHandle
$children=@([MainlineNativeDialog]::Children($h))
$items=@($children | ForEach-Object {[pscustomobject]@{handle=$_.ToInt64();id=[MainlineNativeDialog]::GetDlgCtrlID($_);class=[MainlineNativeDialog]::Class($_);text=[MainlineNativeDialog]::Text($_)}})
$filenameControlId=if($Mode -eq 'Save'){1001}elseif($Mode -eq 'Folder'){1152}else{1148}
$edits=@($children | Where-Object {if([MainlineNativeDialog]::Class($_) -ne 'Edit'){return $false};$at=$_;$found=$false;while($at -ne [IntPtr]::Zero -and $at -ne $h){if([MainlineNativeDialog]::GetDlgCtrlID($at) -eq $filenameControlId){$found=$true;break};$at=[MainlineNativeDialog]::GetParent($at)};$found})
if($edits.Count -ne 1){$items | Where-Object {$_.id -in @(1001,1148) -or $_.class -match 'Edit'} | ConvertTo-Json -Depth 4 | Write-Output;throw ('Expected one filename edit in observed control 1148, found '+$edits.Count)}
$edit=$edits[0];[uint32]$editOwner=0;$null=[MainlineNativeDialog]::GetWindowThreadProcessId($edit,[ref]$editOwner)
if($editOwner -ne $p.Id){throw 'Filename edit belongs to another process'}
$file=[IO.Path]::GetFullPath($TargetPath)
$sourceRoot=[IO.Path]::GetFullPath($(if($c.sourceDirectory){$c.sourceDirectory}else{'E:/Kitao-Deliveries/product-functional-closure-20261112/synthetic-inputs/source-jpegs'}))+[IO.Path]::DirectorySeparatorChar
$outputRoot=[IO.Path]::GetFullPath($(if($c.outputDirectory){$c.outputDirectory}else{'E:/Kitao-Deliveries/product-functional-closure-20261112/workflow-output/c5bc4616-66eb-43eb-8fb6-70fbcc7f0e0b'}))+[IO.Path]::DirectorySeparatorChar
$admitted=$file.StartsWith($sourceRoot,[StringComparison]::OrdinalIgnoreCase)-or$file.StartsWith($outputRoot,[StringComparison]::OrdinalIgnoreCase)-or($Mode -eq 'Folder' -and $file -ieq $sourceRoot.TrimEnd([IO.Path]::DirectorySeparatorChar))
if(!$admitted -or ($Mode -ne 'Folder' -and $file -notmatch '\.jpe?g$' -and $file -notmatch '\.ptstudio\.json$')){throw 'Only authorized synthetic JPEGs and project-owned session/output paths are admitted'}
if($Mode -eq 'Save' -and (!(Test-Path -LiteralPath ([IO.Path]::GetDirectoryName($file))) -or (Test-Path -LiteralPath $file))){throw 'Save requires an existing project-owned parent and a fresh output path'}
if($Mode -eq 'Open' -and !(Test-Path -LiteralPath $file -PathType Leaf)){throw 'Open target is not an existing admitted file'}
if($Mode -eq 'Folder' -and ((!$file.StartsWith($outputRoot,[StringComparison]::OrdinalIgnoreCase) -and $file -ine $sourceRoot.TrimEnd([IO.Path]::DirectorySeparatorChar)) -or !(Test-Path -LiteralPath $file -PathType Container))){throw 'Folder must be an existing project-owned output directory'}
$hash=if($Mode -eq 'Open'){(Get-FileHash -LiteralPath $file -Algorithm SHA256).Hash}else{$null}
$before=[MainlineNativeDialog]::EditText($edit);$requestResult=[IntPtr]::Zero
if($Action -eq 'SetFile'){$ok=[MainlineNativeDialog]::SendMessageTimeout($edit,0x000C,[IntPtr]::Zero,$file,2,2000,[ref]$requestResult);if($ok -eq [IntPtr]::Zero){throw 'Filename native message was not acknowledged'}}
$after=[MainlineNativeDialog]::EditText($edit)
if($Action -in @('SetFile','Confirm') -and $after -ine $file){throw 'Live filename readback does not match the authorized JPEG'}
if($Action -eq 'Confirm'){
 $buttons=@($children | Where-Object {[MainlineNativeDialog]::GetDlgCtrlID($_) -eq 1 -and [MainlineNativeDialog]::Class($_) -eq 'Button' -and [MainlineNativeDialog]::Text($_) -match $(if($Mode -eq 'Open'){'打开|Open'}elseif($Mode -eq 'Folder'){'选择文件夹|Select folder'}else{'保存|Save'})})
 if($buttons.Count -ne 1){throw 'Open button identity is ambiguous'}
 [uint32]$buttonOwner=0;$null=[MainlineNativeDialog]::GetWindowThreadProcessId($buttons[0],[ref]$buttonOwner);if($buttonOwner -ne $p.Id){throw 'Open button belongs to another process'}
 if($Mode -eq 'Folder'){$ok=[MainlineNativeDialog]::SendMessageTimeout($buttons[0],0x00F5,[IntPtr]::Zero,[IntPtr]::Zero,2,2000,[ref]$requestResult)}else{$ok=[MainlineNativeDialog]::SendMessageTimeout($h,0x0111,[IntPtr]1,$buttons[0],2,2000,[ref]$requestResult)}
}
$r=[pscustomobject]@{atUtc=[DateTimeOffset]::UtcNow.ToString('O');pid=$p.Id;app=$p.Path;productVersion=$p.MainModule.FileVersionInfo.ProductVersion;authority='Standard controls of the existing native file dialog; no ViewModel injection or pointer claim';action=$Action;dialog=$title;dialogHandle=$h.ToInt64();before=$before;filenameReadback=$after;fileSha256=$hash;nativeDialogChildren=$items;openAcknowledged=if($Action -eq 'Confirm'){$ok -ne [IntPtr]::Zero}else{$null}}
$out=Join-Path $delivery ('native-files/'+$ReceiptPrefix+'_'+$Action+'.json');if(Test-Path -LiteralPath $out){throw 'Receipt path exists; preserve it'};$r | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath $out -Encoding utf8
$r | Select-Object pid,action,filenameReadback,fileSha256,openAcknowledged | ConvertTo-Json
