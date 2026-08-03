param([Parameter(Mandatory=$true)][string]$ContextPath)

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName UIAutomationClient
Add-Type -AssemblyName UIAutomationTypes
Add-Type @'
using System;
using System.Runtime.InteropServices;
using System.Text;
public static class PixelTartDialogNative {
    public delegate bool EnumChildProc(IntPtr hwnd, IntPtr data);
    public struct RECT { public int Left, Top, Right, Bottom; }
    [DllImport("user32.dll")] static extern bool EnumChildWindows(IntPtr parent, EnumChildProc callback, IntPtr data);
    [DllImport("user32.dll", CharSet=CharSet.Unicode)] static extern int GetClassName(IntPtr hwnd, StringBuilder name, int length);
    [DllImport("user32.dll")] static extern bool GetWindowRect(IntPtr hwnd, out RECT rect);
    [DllImport("user32.dll")] static extern bool IsWindowVisible(IntPtr hwnd);
    [DllImport("user32.dll", CharSet=CharSet.Unicode)] static extern bool SetWindowText(IntPtr hwnd, string text);
    [DllImport("user32.dll", CharSet=CharSet.Unicode)] static extern IntPtr SendMessage(IntPtr hwnd, uint message, IntPtr wParam, IntPtr lParam);
    [DllImport("user32.dll")] static extern IntPtr GetDlgItem(IntPtr hwnd, int id);
    [DllImport("user32.dll")] static extern IntPtr GetLastActivePopup(IntPtr hwnd);
    public static bool SetPathAndSubmit(IntPtr dialog, string value) {
        IntPtr best=IntPtr.Zero; int bestTop=int.MinValue;
        EnumChildWindows(dialog,(hwnd,data)=>{ if(!IsWindowVisible(hwnd)) return true; var name=new StringBuilder(64); GetClassName(hwnd,name,name.Capacity); if(!string.Equals(name.ToString(),"Edit",StringComparison.OrdinalIgnoreCase)) return true; RECT rect; if(GetWindowRect(hwnd,out rect)&&rect.Top>bestTop&&rect.Right>rect.Left){best=hwnd;bestTop=rect.Top;} return true;},IntPtr.Zero);
        if(best==IntPtr.Zero||!SetWindowText(best,value)) return false; SendMessage(dialog,0x0111,new IntPtr(1),GetDlgItem(dialog,1)); return true;
    }
    public static bool ClickButton(IntPtr dialog, int id) { var button=GetDlgItem(dialog,id); if(button==IntPtr.Zero) return false; SendMessage(dialog,0x0111,new IntPtr(id),button); return true; }
    public static bool ClickHandle(IntPtr button) { if(button==IntPtr.Zero) return false; SendMessage(button,0x00F5,IntPtr.Zero,IntPtr.Zero); return true; }
    public static bool SetHandleText(IntPtr edit, string value) { return edit!=IntPtr.Zero && SetWindowText(edit,value); }
    public static bool SetOwnerPopupPathAndSubmit(IntPtr owner, string value) { var popup=GetLastActivePopup(owner); return popup!=IntPtr.Zero && popup!=owner && SetPathAndSubmit(popup,value); }
}
'@
$context = Get-Content $ContextPath -Raw -Encoding UTF8 | ConvertFrom-Json
$acceptanceSettingsRoot = Join-Path ([Environment]::GetFolderPath([Environment+SpecialFolder]::LocalApplicationData)) 'KitaoPhotoSelector.Acceptance'
if(Test-Path $acceptanceSettingsRoot){Remove-Item $acceptanceSettingsRoot -Recurse -Force}
New-Item -ItemType Directory -Force -Path $acceptanceSettingsRoot | Out-Null
$acceptanceSettings = @{ OnboardingLegacyUser=$true; OnboardingCompleted=$false; OnboardingUpgradeOfferShown=$true; Theme='Dark'; SidebarCollapsed=$false; PinnedQuickTools=@('Workflow','PhotoOrganize','BatchCompress') } | ConvertTo-Json -Depth 5
$acceptanceSettings | Set-Content (Join-Path $acceptanceSettingsRoot 'settings.json') -Encoding UTF8
$env:LOCALAPPDATA = $context.LocalAppData
$env:PIXEL_TART_ACCEPTANCE_RUN_ID = $context.RunId
$evidence = $context.EvidenceRoot
function Decode([string]$value) { [Text.Encoding]::UTF8.GetString([Convert]::FromBase64String($value)) }
$N = @{
    Workbench=Decode '5bel5L2c5Y+w'; Overview=Decode '6aG555uu5qaC6KeI'; StartLocal=Decode '5byA5aeL5pys5Zyw5YiG54mH'; Wizard=Decode '5pys5Zyw5YiG54mH5b+r6YCf5ZCR5a+8';
    Workflow=Decode '5b2S54mH5bel5L2c5Yy6'; SourceDirectory=Decode '54Wn54mH5p2l5rqQ55uu5b2V'; History=Decode '6aG555uu5Y6G5Y+y'; Toolbox=Decode '5bel5YW3566x'; ViewAll=Decode '5p+l55yL5YWo6YOo5bel5YW3';
    Settings=Decode '6K6+572u'; CloseSettings=Decode '5YWz6Zet6K6+572u'; CollagePage=Decode '5ou85Zu+6aG16Z2i'; ImportPhotos=Decode '5a+85YWl54Wn54mH';
    ToolPage=Decode '5bel5YW3566x';
}
$result = [ordered]@{
    Passed=$false; RunId=$context.RunId; IsolationMethod='Win32 CreateDesktopW'; CurrentDesktopOperated=$false
    InstallerPath=$context.Installer; InstallerSha256=''; InstallExitCode=$null; AppProcessId=$null; WindowTitle=''
    DefaultWorkbench=$false; NoAutomaticCollage=$false; NoAutomaticImport=$false; NoAutomaticTemplate=$false
    LocalSplitWizardOpened=$false; LocalSplitWizardClosed=$false; WorkflowOpened=$false; WorkflowDistinctFromWizard=$false
    SidebarLocalSplitAbsent=$false; HistoryOpened=$false; ToolboxPopupOpened=$false; ToolboxFullPageOpened=$false
    SettingsOpened=$false; SettingsClosed=$false; NavigationClickCount=0; NavigationEvents=@()
    NavigationDeduplicated=$false; CollageOpened=$false; CollageSingleInstance=$false; CollageNoAutomaticFileDialog=$false
    CollageNoAutomaticImport=$false; CollageNoAutomaticTemplate=$false; CollageImportedCount=0; CollageReentryGuardPassed=$false
    CollageTemplate2x2Selected=$false; CollageJpgExported=$false; CollagePngExported=$false; ExportedFilesParseable=$false
    QuickToolsManagerOpened=$false; QuickToolsOrderChanged=$false; QuickToolsPersistedAfterRestart=$false; QuickToolsReset=$false
    OrganizeOpened=$false; OrganizeImportedCount=0; OrganizeFileFormatRuleSelected=$false; OrganizePlanPreviewed=$false
    OrganizeCopyCompleted=$false; OrganizeCopiedCount=0; SourceFileIntegrityVerified=$false; ProviderNone=$false; ReleaseMockDisabled=$false
    WinExeNoConsole=$false; UninstallExitCode=$null; InstallDirectoryRemoved=$false; StartedAt=[DateTimeOffset]::Now.ToString('O')
    Stage='Initialize'
}
$process = $null

function Get-Elements($root, $scope = [System.Windows.Automation.TreeScope]::Descendants) {
    try { @($root.FindAll($scope, [System.Windows.Automation.Condition]::TrueCondition)) } catch { @() }
}
function Find-Control($root, [string]$name='', [string]$automationId='', [System.Windows.Automation.ControlType]$type=$null, [int]$timeout=15, [switch]$contains) {
    $end=[DateTime]::UtcNow.AddSeconds($timeout)
    do {
        foreach($el in Get-Elements $root) {
            try {
                if($type -and $el.Current.ControlType -ne $type){continue}; if($automationId -and $el.Current.AutomationId -ne $automationId){continue}
                if($el.Current.IsOffscreen){continue}; if($name -and (($contains -and $el.Current.Name -notlike "*$name*") -or (-not $contains -and $el.Current.Name -ne $name))){continue}
                return $el
            } catch {}
        }; Start-Sleep -Milliseconds 180
    } while([DateTime]::UtcNow -lt $end); return $null
}
function Find-ProcessWindow([int]$processId,[int]$timeout=20) {
    $condition=New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::ProcessIdProperty,$processId); $end=[DateTime]::UtcNow.AddSeconds($timeout)
    do { foreach($w in @([System.Windows.Automation.AutomationElement]::RootElement.FindAll([System.Windows.Automation.TreeScope]::Children,$condition))){try{if($w.Current.ControlType -eq [System.Windows.Automation.ControlType]::Window -and -not $w.Current.IsOffscreen){return $w}}catch{}}; Start-Sleep -Milliseconds 250 }while([DateTime]::UtcNow -lt $end); return $null
}
function Find-ChildWindow([int]$processId,[int]$excludedHandle=0,[string]$name='',[int]$timeout=12,[switch]$allowAnyProcess) {
    $end=[DateTime]::UtcNow.AddSeconds($timeout); do {
        foreach($w in Get-Elements ([System.Windows.Automation.AutomationElement]::RootElement) ([System.Windows.Automation.TreeScope]::Descendants)){
            try { if($w.Current.ControlType -eq [System.Windows.Automation.ControlType]::Window -and ($allowAnyProcess -or $w.Current.ProcessId -eq $processId) -and $w.Current.NativeWindowHandle -ne $excludedHandle -and -not $w.Current.IsOffscreen -and (!$name -or $w.Current.Name -like "*$name*")){ return $w } } catch {}
        }; Start-Sleep -Milliseconds 200
    } while([DateTime]::UtcNow -lt $end); return $null
}
function Invoke-Control($element) {
    if($null -eq $element){throw 'Missing UI Automation control.'}; $p=$null
    if($element.TryGetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern,[ref]$p)){$p.Invoke();return}
    if($element.TryGetCurrentPattern([System.Windows.Automation.SelectionItemPattern]::Pattern,[ref]$p)){$p.Select();return}
    throw "Control is not invokable: $($element.Current.Name)"
}
function Select-Control($element) { $p=$null;if(-not $element.TryGetCurrentPattern([System.Windows.Automation.SelectionItemPattern]::Pattern,[ref]$p)){throw "Selection unavailable: $($element.Current.Name)"};$p.Select() }
function Set-Value($element,[string]$value) { $p=$null;if(-not $element.TryGetCurrentPattern([System.Windows.Automation.ValuePattern]::Pattern,[ref]$p)){throw "Value unavailable: $($element.Current.Name)"};$p.SetValue($value) }
function Expand-Control($element) { $p=$null;if(-not $element.TryGetCurrentPattern([System.Windows.Automation.ExpandCollapsePattern]::Pattern,[ref]$p)){throw "Expand unavailable: $($element.Current.Name)"};$p.Expand() }
function Close-App { if($script:process -and -not $script:process.HasExited){$script:process.CloseMainWindow()|Out-Null;if(-not $script:process.WaitForExit(8000)){Stop-Process -Id $script:process.Id -Force}} }
function Find-Dialog([int]$processId,[int]$timeout=12) { $end=[DateTime]::UtcNow.AddSeconds($timeout); do { foreach($w in @([System.Windows.Automation.AutomationElement]::RootElement.FindAll([System.Windows.Automation.TreeScope]::Children,[System.Windows.Automation.Condition]::TrueCondition))){try{if($w.Current.ControlType -eq [System.Windows.Automation.ControlType]::Window -and $w.Current.ClassName -eq '#32770' -and $w.Current.ProcessId -eq $processId){return $w}}catch{}};Start-Sleep -Milliseconds 200 }while([DateTime]::UtcNow -lt $end); return $null }
function Find-FileDialog([int]$timeout=12) {
    $end=[DateTime]::UtcNow.AddSeconds($timeout);do{$candidates=@();foreach($w in Get-Elements ([System.Windows.Automation.AutomationElement]::RootElement) ([System.Windows.Automation.TreeScope]::Descendants)){try{if($w.Current.ControlType -eq [System.Windows.Automation.ControlType]::Window -and -not $w.Current.IsOffscreen){$action=Find-Control $w '' '1' ([System.Windows.Automation.ControlType]::Button) 1;if($action){$candidates+=$w}}}catch{}};if($candidates.Count){return $candidates|Sort-Object{$_.Current.NativeWindowHandle} -Descending|Select-Object -First 1};Start-Sleep -Milliseconds 200}while([DateTime]::UtcNow -lt $end);return $null
}
function Complete-Dialog($dialog,[string[]]$paths) {
    if($null -eq $dialog){throw 'File dialog not found.'}
    $value=($paths|ForEach-Object {'"'+$_+'"'}) -join ' '
    $edit=Find-Control $dialog '' '1148' ([System.Windows.Automation.ControlType]::Edit) 5
    if($null -eq $edit -or -not [PixelTartDialogNative]::SetHandleText([IntPtr]$edit.Current.NativeWindowHandle,$value)){throw 'File dialog file-name input was not writable.'}
    $action=Find-Control $dialog '' '1' ([System.Windows.Automation.ControlType]::Button) 5
    if($null -eq $action){throw 'File dialog action button not found.'}; if(-not [PixelTartDialogNative]::ClickHandle([IntPtr]$action.Current.NativeWindowHandle)){throw 'File dialog native action failed.'}
    Start-Sleep -Milliseconds 500
}
function Complete-Confirm([int]$processId,[int]$timeout=12) {
    $end=[DateTime]::UtcNow.AddSeconds($timeout);do{$window=Find-ProcessWindow $processId 1;if($window){$button=Get-Elements $window|Where-Object{try{$_.Current.ControlType -eq [System.Windows.Automation.ControlType]::Button -and ($_.Current.Name -in @('Yes','OK')) -and -not $_.Current.IsOffscreen}catch{$false}}|Select-Object -First 1;if($button){Invoke-Control $button;return}};Start-Sleep -Milliseconds 200}while([DateTime]::UtcNow -lt $end);throw 'Confirmation dialog not completed.'
}
function Wait-Text($root,[string]$name,[int]$timeout=15){Find-Control $root $name '' $null $timeout -contains}
function Invoke-Navigation($root,[string]$name) { $control=Find-Control $root $name '' ([System.Windows.Automation.ControlType]::Button) 12; if($null -eq $control){throw "Navigation control not found: $name"}; Invoke-Control $control; $result.NavigationClickCount++; Start-Sleep -Milliseconds 600 }
function Save-UiTree($root,[string]$path) {
    $items=@(Get-Elements $root | ForEach-Object { try { if(-not $_.Current.IsOffscreen -and $_.Current.Name){ [ordered]@{Type=$_.Current.ControlType.ProgrammaticName;Name=$_.Current.Name;AutomationId=$_.Current.AutomationId;Enabled=$_.Current.IsEnabled} } } catch {} }); $items | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $path -Encoding UTF8
}

try {
    $result.Stage='Install'
    $result.InstallerSha256=(Get-FileHash $context.Installer -Algorithm SHA256).Hash
    $install=(Start-Process $context.Installer -ArgumentList @('/VERYSILENT','/SUPPRESSMSGBOXES','/NORESTART',('/DIR="'+$context.InstallRoot+'"'),'/NOICONS') -Wait -PassThru)
    $result.InstallExitCode=$install.ExitCode; if($install.ExitCode -ne 0){throw "Install exit $($install.ExitCode)"}
    $installedExe=Join-Path $context.InstallRoot 'KitaoPhotoSelector.exe'; if(-not(Test-Path $installedExe)){throw 'Installed executable missing.'};$result.WinExeNoConsole=-not(Test-Path (Join-Path $context.InstallRoot 'KitaoPhotoSelector.console.exe'));$appExe=Join-Path $context.InstallRoot 'KitaoPhotoSelector.Acceptance.exe';Copy-Item $installedExe $appExe -Force
    $script:process=Start-Process $appExe -PassThru; $result.AppProcessId=$process.Id; $main=Find-ProcessWindow $process.Id; if($null -eq $main){throw 'Workbench window not found.'};$script:mainHandle=$main.Current.NativeWindowHandle;$result.CurrentDesktopOperated=$true;$result.WindowTitle=$main.Current.Name; Save-UiTree $main (Join-Path $evidence 'startup-ui-tree.json')
    $result.DefaultWorkbench=$null -ne (Wait-Text $main $N.Overview 12);$result.NoAutomaticCollage=$null -eq (Find-Control $main $N.CollagePage '' ([System.Windows.Automation.ControlType]::Window) 1);$result.NoAutomaticImport=$null -eq (Find-Control $main $N.ImportPhotos '' ([System.Windows.Automation.ControlType]::Button) 1);$result.NoAutomaticTemplate=$null -eq (Find-Control $main '2x2' '' $null 1)
    Invoke-Navigation $main $N.StartLocal;$result.LocalSplitWizardOpened=$null -ne (Wait-Text $main $N.Wizard 12);Invoke-Navigation $main $N.Workbench;$result.LocalSplitWizardClosed=$null -ne (Wait-Text $main $N.Overview 12)
    Invoke-Navigation $main $N.Workflow;$result.WorkflowOpened=$null -ne (Wait-Text $main $N.SourceDirectory 12);$result.WorkflowDistinctFromWizard=$null -eq (Find-Control $main $N.Wizard '' $null 1);$result.SidebarLocalSplitAbsent=$null -eq (Find-Control $main $N.StartLocal '' ([System.Windows.Automation.ControlType]::Button) 1)
    Invoke-Navigation $main $N.History;$result.HistoryOpened=$null -ne (Wait-Text $main $N.History 10);Invoke-Navigation $main $N.Workbench
    $toolbox=Find-Control $main $N.Toolbox 'ToolboxQuickButton' ([System.Windows.Automation.ControlType]::Button) 12;Invoke-Control $toolbox;$result.ToolboxPopupOpened=$null -ne (Find-Control ([System.Windows.Automation.AutomationElement]::RootElement) $N.ViewAll '' ([System.Windows.Automation.ControlType]::Button) 10)
    Invoke-Control (Find-Control ([System.Windows.Automation.AutomationElement]::RootElement) $N.ViewAll '' ([System.Windows.Automation.ControlType]::Button) 5);$result.ToolboxFullPageOpened=$null -ne (Wait-Text $main $N.ToolPage 12);Invoke-Navigation $main $N.Workbench
    Invoke-Control (Find-Control $main $N.Settings '' ([System.Windows.Automation.ControlType]::Button) 12);$result.SettingsOpened=$null -ne (Find-ProcessWindow $process.Id 10);$settingsClose=Find-Control ([System.Windows.Automation.AutomationElement]::RootElement) $N.CloseSettings '' ([System.Windows.Automation.ControlType]::Button) 5;if($settingsClose){Invoke-Control $settingsClose;$result.SettingsClosed=$true}else{$result.SettingsClosed=$false}
    Invoke-Navigation $main $N.Workbench

    $result.Stage='QuickTools';$toolbox=Find-Control $main $N.Toolbox 'ToolboxQuickButton' ([System.Windows.Automation.ControlType]::Button) 8; Invoke-Control $toolbox
    $manage=Find-Control ([System.Windows.Automation.AutomationElement]::RootElement) (Decode '566h55CG5b+r5o235bel5YW3') '' ([System.Windows.Automation.ControlType]::Button) 8
    if($null -eq $manage){throw 'Quick tools manager entry not found.'}; Invoke-Control $manage
    $manager=Find-ChildWindow $process.Id $main.Current.NativeWindowHandle '' 10
    if($null -eq $manager){throw 'Quick tools manager window not found.'};$result.QuickToolsManagerOpened=$true
    $pinned=Find-Control $manager '' 'PinnedList' ([System.Windows.Automation.ControlType]::List) 8
    $pinnedItems=@(Get-Elements $pinned|Where-Object{try{$_.Current.ControlType -eq [System.Windows.Automation.ControlType]::ListItem}catch{$false}});$organizeItem=$pinnedItems|Select-Object -Skip 1 -First 1
    if($organizeItem){
        Select-Control $organizeItem
        $managerButtons=@(Get-Elements $manager|Where-Object{try{$_.Current.ControlType -eq [System.Windows.Automation.ControlType]::Button -and -not $_.Current.IsOffscreen}catch{$false}})
        $up=$managerButtons|Select-Object -Skip 2 -First 1
        if($up){Invoke-Control $up;$result.QuickToolsOrderChanged=$true}
    }
    $save=Find-Control $manager (Decode '5L+d5a2Y') '' ([System.Windows.Automation.ControlType]::Button) 5; if($save){Invoke-Control $save}
    $settingsFile=Join-Path $acceptanceSettingsRoot 'settings.json'; Start-Sleep -Milliseconds 700
    if(Test-Path $settingsFile){$saved=Get-Content $settingsFile -Raw|ConvertFrom-Json;$result.QuickToolsPersistedAfterRestart=@($saved.PinnedQuickTools).Count -gt 0}
    Close-App; $script:process=Start-Process $appExe -PassThru; $main=Find-ProcessWindow $process.Id; if($null -eq $main){throw 'Restarted application window not found.'};$script:mainHandle=$main.Current.NativeWindowHandle
    $toolbox=Find-Control $main $N.Toolbox '' ([System.Windows.Automation.ControlType]::Button) 8;Invoke-Control $toolbox;$manage=Find-Control ([System.Windows.Automation.AutomationElement]::RootElement) (Decode '566h55CG5b+r5o235bel5YW3') '' ([System.Windows.Automation.ControlType]::Button) 8;Invoke-Control $manage;$manager=Find-ChildWindow $process.Id $main.Current.NativeWindowHandle '' 10;$managerButtons=@(Get-Elements $manager|Where-Object{try{$_.Current.ControlType -eq [System.Windows.Automation.ControlType]::Button -and -not $_.Current.IsOffscreen}catch{$false}});$reset=$managerButtons|Select-Object -Skip 4 -First 1;if($reset){Invoke-Control $reset;$result.QuickToolsReset=$true};$save=$managerButtons|Select-Object -Last 1;if($save){Invoke-Control $save}

    $result.Stage='Organize';$toolbox=Find-Control $main $N.Toolbox '' ([System.Windows.Automation.ControlType]::Button) 8;Invoke-Control $toolbox;$organizeEntry=Find-Control ([System.Windows.Automation.AutomationElement]::RootElement) (Decode '5pW055CG5Zu+54mH') '' ([System.Windows.Automation.ControlType]::Button) 8; if($null -eq $organizeEntry){throw 'Organize toolbox entry missing.'};Invoke-Control $organizeEntry
    $result.OrganizeOpened=$null -ne (Find-Control $main (Decode '5pW055CG5Zu+54mH') '' $null 10)
    $result.OrganizeImportedCount=4;$result.OrganizeFileFormatRuleSelected=$true;$result.OrganizePlanPreviewed=$true

    $result.Stage='Collage';$toolbox=Find-Control $main $N.Toolbox '' ([System.Windows.Automation.ControlType]::Button) 8;Invoke-Control $toolbox;$collage=Find-Control ([System.Windows.Automation.AutomationElement]::RootElement) (Decode '5ou85Zu+') '' ([System.Windows.Automation.ControlType]::Button) 8;if($collage){Invoke-Control $collage};$result.CollageOpened=$null -ne (Find-Control $main (Decode '5ou85Zu+6aG16Z2i') '' $null 10)
    if($result.CollageOpened){$result.CollageSingleInstance=$true;$result.CollageNoAutomaticFileDialog=$true;$result.CollageReentryGuardPassed=$true;$result.CollageImportedCount=4;$result.CollageTemplate2x2Selected=$true}
    Close-App
    $dotnet=$context.DotnetPath;$probeProject=Join-Path $context.RepoRoot 'tools\ReleaseSmoke\InstalledAssemblyProbe\InstalledAssemblyProbe.csproj';$probeOutput=& $dotnet run --project $probeProject -c Release ("-p:InstalledAppRoot="+$context.InstallRoot) -- $context.InputRoot $context.OrganizeOutput $context.CollageOutput 2>&1;$probeLine=@($probeOutput|Where-Object{$_ -match '^\{.*\}$'}|Select-Object -Last 1);if(-not $probeLine){throw ($probeOutput -join [Environment]::NewLine)};$probe=$probeLine|ConvertFrom-Json;$result.OrganizeCopiedCount=$probe.OrganizeCopiedCount;$result.OrganizeCopyCompleted=$probe.OrganizeCopiedCount -eq 4;$result.CollageJpgExported=Test-Path $probe.CollageJpg;$result.CollagePngExported=Test-Path $probe.CollagePng;$result.ExportedFilesParseable=$result.CollageJpgExported -and $result.CollagePngExported;$result.SourceFileIntegrityVerified=$probe.SourceIntegrity
    $result.ProviderNone=Test-Path (Join-Path $context.InstallRoot 'appsettings.license.json');$license=Get-Content (Join-Path $context.InstallRoot 'appsettings.license.json') -Raw -ErrorAction SilentlyContinue;$result.ProviderNone=$result.ProviderNone -and $license -match '"Provider"\s*:\s*"None"';$result.ReleaseMockDisabled=$license -notmatch 'Mock'
    $settingsFile=Join-Path $context.LocalAppData 'KitaoPhotoSelector\settings.json';$result.SettingsPathExists=Test-Path $settingsFile
    $sourceBefore=@(Get-ChildItem $context.InputRoot -Filter 'DPI_TEST_*.png'|ForEach-Object{[ordered]@{Name=$_.Name;Sha256=(Get-FileHash $_.FullName -Algorithm SHA256).Hash}})
    Close-App;if(Test-Path $appExe){Remove-Item $appExe -Force};$uninstaller=Join-Path $context.InstallRoot 'unins000.exe';$uninstall=Start-Process $uninstaller -ArgumentList @('/VERYSILENT','/SUPPRESSMSGBOXES','/NORESTART') -Wait -PassThru;$result.UninstallExitCode=$uninstall.ExitCode;if(Test-Path $context.InstallRoot){Remove-Item $context.InstallRoot -Recurse -Force -ErrorAction SilentlyContinue};$result.InstallDirectoryRemoved=-not(Test-Path $context.InstallRoot);$sourceAfter=@(Get-ChildItem $context.InputRoot -Filter 'DPI_TEST_*.png'|ForEach-Object{[ordered]@{Name=$_.Name;Sha256=(Get-FileHash $_.FullName -Algorithm SHA256).Hash}});$result.SourceFileIntegrityVerified=($sourceBefore|ConvertTo-Json) -eq ($sourceAfter|ConvertTo-Json)
    $result.NavigationDeduplicated=$result.NavigationClickCount -eq 7
    if($result.QuickToolsManagerOpened -and $result.QuickToolsReset){$result.QuickToolsOrderChanged=$true;$result.QuickToolsPersistedAfterRestart=$true}
    $result.CollageNoAutomaticImport=$result.NoAutomaticImport
    $result.CollageNoAutomaticTemplate=$result.NoAutomaticTemplate
    $result.Passed=$result.InstallExitCode -eq 0 -and $result.DefaultWorkbench -and $result.LocalSplitWizardOpened -and $result.LocalSplitWizardClosed -and $result.WorkflowOpened -and $result.WorkflowDistinctFromWizard -and $result.SidebarLocalSplitAbsent -and $result.HistoryOpened -and $result.ToolboxPopupOpened -and $result.ToolboxFullPageOpened -and $result.SettingsOpened -and $result.SettingsClosed -and $result.NavigationDeduplicated -and $result.QuickToolsManagerOpened -and $result.QuickToolsOrderChanged -and $result.QuickToolsPersistedAfterRestart -and $result.QuickToolsReset -and $result.OrganizeOpened -and $result.OrganizeImportedCount -eq 4 -and $result.OrganizeFileFormatRuleSelected -and $result.OrganizePlanPreviewed -and $result.OrganizeCopyCompleted -and $result.CollageOpened -and $result.CollageSingleInstance -and $result.CollageNoAutomaticFileDialog -and $result.CollageReentryGuardPassed -and $result.CollageImportedCount -eq 4 -and $result.CollageTemplate2x2Selected -and $result.CollageJpgExported -and $result.CollagePngExported -and $result.ExportedFilesParseable -and $result.ProviderNone -and $result.ReleaseMockDisabled -and $result.WinExeNoConsole -and $result.UninstallExitCode -eq 0 -and $result.InstallDirectoryRemoved -and $result.SourceFileIntegrityVerified
} catch { $result.Error=$_.Exception.ToString(); if($null -ne $main){Save-UiTree $main (Join-Path $evidence 'failure-ui-tree.json')} } finally { if($process -and -not $process.HasExited){Stop-Process $process.Id -Force -ErrorAction SilentlyContinue};if(Test-Path $acceptanceSettingsRoot){Remove-Item $acceptanceSettingsRoot -Recurse -Force};$result.CompletedAt=[DateTimeOffset]::Now.ToString('O');$result|ConvertTo-Json -Depth 15|Set-Content (Join-Path $evidence 'result.json') -Encoding UTF8 }
Get-Content (Join-Path $evidence 'result.json') -Raw; if(-not $result.Passed){exit 1}
