param([Parameter(Mandatory=$true)][string]$ContextPath)

$ErrorActionPreference='Stop'
Add-Type -AssemblyName UIAutomationClient
Add-Type -AssemblyName UIAutomationTypes
$context=Get-Content $ContextPath -Raw -Encoding UTF8|ConvertFrom-Json
$env:LOCALAPPDATA=$context.LocalAppData
$appDataRoot=Join-Path $context.LocalAppData 'KitaoPhotoSelector.Acceptance'
$resolved=[IO.Path]::GetFullPath($appDataRoot)
$local=[IO.Path]::GetFullPath($context.LocalAppData).TrimEnd([IO.Path]::DirectorySeparatorChar)
if(-not$resolved.StartsWith($local+[IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase)){throw 'Acceptance app data escaped the isolated root.'}
$env:PIXEL_TART_ACCEPTANCE_ROOT=$appDataRoot
New-Item -ItemType Directory -Force -Path $appDataRoot|Out-Null
$settings=@{Appearance=@{Theme=2;SidebarCollapsed=$false};PinnedQuickTools=@('Workflow','PhotoOrganize','BatchCompress');onboardingCompleted=$true;onboardingVersion='2.3.0';onboardingLegacyUser=$true;onboardingUpgradeOfferShown=$true}|ConvertTo-Json -Depth 6
[IO.File]::WriteAllText((Join-Path $appDataRoot 'settings.json'),$settings,[Text.UTF8Encoding]::new($true))
function Decode([string]$value){[Text.Encoding]::UTF8.GetString([Convert]::FromBase64String($value))}
$names=@{
    Workbench=Decode '5bel5L2c5Y+w';Calendar=Decode '5bel5L2c5pel5Y6G';Finance=Decode '5pGE5b2x5pS25pSv';Tether=Decode '6IGU5py65ouN5pGE';Toolbox=Decode '5bel5YW3566x'
    Unshot=Decode '5pyq5ouN5pGE';Shot=Decode '5bey5ouN5pGE';Return=Decode '5b6F6L+U5Zu+';CreateBooking=Decode '5Yib5bu65ouN5pGE5Lu75Yqh';Income=Decode '5paw5aKe5pS25YWl';Expense=Decode '5paw5aKe5pSv5Ye6';TetherEmpty=Decode '5bCa5pyq5byA5aeL6IGU5py65ouN5pGE'
}
$result=[ordered]@{
    Passed=$false;Mode=$context.Mode;RunId=$context.RunId;IsolationMethod='Win32 CreateDesktopW';LocalAppData=$context.LocalAppData
    OldInstallExitCode=$null;OldVersion='';Rc2Seed=$null;Rc3InstallExitCode=$null;Rc3Version='';WindowObserved=$false;WorkbenchVisible=$false
    ThreeLegendsVisible=$false;PinnedToolsVisible=$false;CalendarOpened=$false;FinanceOpened=$false;TetherOpened=$false;TetherEmptyVisible=$false
    DarkThemeProfileApplied=$true;Probe=$null;Restarted=$false;SourceUnchanged=$false;UninstallExitCode=$null;InstallDirectoryRemoved=$false;UserDataRetained=$false
    StartedAt=[DateTimeOffset]::Now.ToString('O')
}
$process=$null
function Get-Root([int]$pid,[int]$timeout=25){$end=[DateTime]::UtcNow.AddSeconds($timeout);do{$p=Get-Process -Id $pid -ErrorAction SilentlyContinue;if($p){$p.Refresh();if($p.MainWindowHandle-ne[IntPtr]::Zero){return [System.Windows.Automation.AutomationElement]::FromHandle($p.MainWindowHandle)}};Start-Sleep -Milliseconds 250}while([DateTime]::UtcNow-lt$end);$null}
function Find-Element($root,[string]$name,[int]$timeout=10,[switch]$contains){$end=[DateTime]::UtcNow.AddSeconds($timeout);do{foreach($el in @($root.FindAll([System.Windows.Automation.TreeScope]::Descendants,[System.Windows.Automation.Condition]::TrueCondition))){try{if(-not$el.Current.IsOffscreen-and(($contains-and$el.Current.Name-like"*$name*")-or(-not$contains-and$el.Current.Name-eq$name))){return $el}}catch{}};Start-Sleep -Milliseconds 180}while([DateTime]::UtcNow-lt$end);$null}
function Invoke-Element($element){if($null-eq$element){throw 'Required UI element was not found.'};$pattern=$null;if($element.TryGetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern,[ref]$pattern)){$pattern.Invoke();return};if($element.TryGetCurrentPattern([System.Windows.Automation.SelectionItemPattern]::Pattern,[ref]$pattern)){$pattern.Select();return};throw 'UI element cannot be invoked.'}
function Close-App{if($process-and-not$process.HasExited){$process.CloseMainWindow()|Out-Null;if(-not$process.WaitForExit(10000)){Stop-Process -Id $process.Id -Force};$script:process=$null}}
try{
    $sourceBefore=(Get-FileHash $context.SourceFile -Algorithm SHA256).Hash
    if($context.Mode-eq'Upgrade'){
        $old=Start-Process $context.Rc2Installer -ArgumentList @('/VERYSILENT','/SUPPRESSMSGBOXES','/NORESTART',('/DIR="'+$context.InstallRoot+'"'),'/NOICONS') -Wait -PassThru
        $result.OldInstallExitCode=$old.ExitCode;if($old.ExitCode-ne0){throw 'RC2 install failed.'}
        $installed=Join-Path $context.InstallRoot 'KitaoPhotoSelector.exe';$result.OldVersion=(Get-Item $installed).VersionInfo.ProductVersion
        $seedOutput=& $context.DotnetPath run --project (Join-Path $context.RepoRoot 'tools\ReleaseSmoke\RC3UpgradeSeed\RC3UpgradeSeed.csproj') -c Release -- $appDataRoot $context.SourceFile $context.WatchRoot 2>&1
        $seedLine=@($seedOutput|Where-Object{$_-match'^\{.*\}$'}|Select-Object -Last 1);if(-not$seedLine){throw($seedOutput-join[Environment]::NewLine)};$result.Rc2Seed=$seedLine|ConvertFrom-Json
        if(-not$result.Rc2Seed.Passed-or$result.Rc2Seed.SchemaVersion-ne3){throw 'RC2 controlled data seed failed.'}
    }
    $install=Start-Process $context.Rc3Installer -ArgumentList @('/VERYSILENT','/SUPPRESSMSGBOXES','/NORESTART',('/DIR="'+$context.InstallRoot+'"'),'/NOICONS') -Wait -PassThru
    $result.Rc3InstallExitCode=$install.ExitCode;if($install.ExitCode-ne0){throw 'RC3 install failed.'}
    $installed=Join-Path $context.InstallRoot 'KitaoPhotoSelector.exe';$result.Rc3Version=(Get-Item $installed).VersionInfo.ProductVersion
    $acceptance=Join-Path $context.InstallRoot 'KitaoPhotoSelector.Acceptance.exe';Copy-Item $installed $acceptance -Force
    $script:process=Start-Process $acceptance -PassThru
    $root=Get-Root $process.Id;if($null-eq$root){throw 'RC3 main window was not observed.'};$result.WindowObserved=$true
    $result.WorkbenchVisible=$null-ne(Find-Element $root $names.Workbench 8)
    $result.ThreeLegendsVisible=($null-ne(Find-Element $root $names.Unshot 4))-and($null-ne(Find-Element $root $names.Shot 4))-and($null-ne(Find-Element $root $names.Return 4))
    $result.PinnedToolsVisible=($null-ne(Find-Element $root $names.Toolbox 4))
    Invoke-Element (Find-Element $root $names.Calendar 8);$result.CalendarOpened=$null-ne(Find-Element $root $names.CreateBooking 10 -contains)
    Invoke-Element (Find-Element $root $names.Finance 8);$result.FinanceOpened=($null-ne(Find-Element $root $names.Income 10 -contains))-and($null-ne(Find-Element $root $names.Expense 5 -contains))
    Invoke-Element (Find-Element $root $names.Tether 8);$result.TetherOpened=$true;$result.TetherEmptyVisible=$null-ne(Find-Element $root $names.TetherEmpty 10 -contains)
    Close-App
    $db=Join-Path $appDataRoot 'Data\pixel-tart.db'
    $probeOutput=& $context.DotnetPath run --project (Join-Path $context.RepoRoot 'tools\ReleaseSmoke\RC3InstalledDataProbe\RC3InstalledDataProbe.csproj') -c Release ("-p:InstalledAppRoot="+$context.InstallRoot) -- $context.Mode.ToLowerInvariant() $db $appDataRoot $context.SourceFile 2>&1
    $probeLine=@($probeOutput|Where-Object{$_-match'^\{.*\}$'}|Select-Object -Last 1);if(-not$probeLine){throw($probeOutput-join[Environment]::NewLine)};$result.Probe=$probeLine|ConvertFrom-Json
    if(-not$result.Probe.Passed){throw 'Installed RC3 data probe failed.'}
    $script:process=Start-Process $acceptance -PassThru;$root=Get-Root $process.Id;if($null-eq$root){throw 'RC3 restart failed.'};$result.Restarted=$true;Close-App
    $sourceAfter=(Get-FileHash $context.SourceFile -Algorithm SHA256).Hash;$result.SourceUnchanged=$sourceBefore-eq$sourceAfter
    $uninstaller=Join-Path $context.InstallRoot 'unins000.exe';$uninstall=Start-Process $uninstaller -ArgumentList @('/VERYSILENT','/SUPPRESSMSGBOXES','/NORESTART') -Wait -PassThru
    $result.UninstallExitCode=$uninstall.ExitCode;$result.InstallDirectoryRemoved=-not(Test-Path $context.InstallRoot);$result.UserDataRetained=Test-Path $db
    $result.Passed=$result.Rc3InstallExitCode-eq0-and$result.Rc3Version-like'2.3.0*'-and$result.WindowObserved-and$result.WorkbenchVisible-and$result.ThreeLegendsVisible-and$result.PinnedToolsVisible-and$result.CalendarOpened-and$result.FinanceOpened-and$result.TetherOpened-and$result.TetherEmptyVisible-and$result.Probe.Passed-and$result.Restarted-and$result.SourceUnchanged-and$result.UninstallExitCode-eq0-and$result.InstallDirectoryRemoved-and$result.UserDataRetained
}catch{$result.Error=$_.Exception.ToString()}finally{Close-App;$result.CompletedAt=[DateTimeOffset]::Now.ToString('O');[IO.File]::WriteAllText((Join-Path $context.EvidenceRoot 'result.json'),($result|ConvertTo-Json -Depth 15),[Text.UTF8Encoding]::new($true))}
Get-Content (Join-Path $context.EvidenceRoot 'result.json') -Raw -Encoding UTF8
if(-not$result.Passed){exit 1}
