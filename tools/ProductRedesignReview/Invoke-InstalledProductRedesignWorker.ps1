param(
    [Parameter(Mandatory=$true)][string]$ContextPath,
    [switch]$FailuresOnly
)

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName UIAutomationClient
Add-Type -AssemblyName UIAutomationTypes
Add-Type -AssemblyName System.Drawing
Add-Type @'
using System;
using System.Runtime.InteropServices;
public static class PixelTartInstalledUiNative {
    [DllImport("user32.dll", CharSet=CharSet.Unicode)] static extern bool SetWindowText(IntPtr hwnd, string text);
    [DllImport("user32.dll", CharSet=CharSet.Unicode)] static extern IntPtr SendMessage(IntPtr hwnd, uint message, IntPtr wParam, IntPtr lParam);
    [DllImport("user32.dll", SetLastError=true)] static extern bool PostMessage(IntPtr hwnd, uint message, IntPtr wParam, IntPtr lParam);
    [DllImport("user32.dll", SetLastError=true)] static extern bool IsWindow(IntPtr hwnd);
    [DllImport("user32.dll", SetLastError=true)] static extern bool SetCursorPos(int x, int y);
    [DllImport("user32.dll", SetLastError=true)] static extern bool SetForegroundWindow(IntPtr hwnd);
    [DllImport("user32.dll")] static extern void mouse_event(uint flags, uint dx, uint dy, uint data, UIntPtr extraInfo);
    public static bool SetText(IntPtr edit, string value) { return edit != IntPtr.Zero && SetWindowText(edit, value); }
    public static bool Click(IntPtr button) { if (button == IntPtr.Zero) return false; SendMessage(button, 0x00F5, IntPtr.Zero, IntPtr.Zero); return true; }
    public static bool OpenContextMenu(IntPtr hwnd, int x, int y) {
        long packed = ((long)(y & 0xffff) << 16) | (uint)(x & 0xffff);
        return PostMessage(hwnd, 0x007B, hwnd, new IntPtr(packed));
    }
    public static bool OpenContextMenuFromKeyboard(IntPtr hwnd) {
        return PostMessage(hwnd, 0x007B, hwnd, new IntPtr(-1));
    }
    public static bool RightClick(IntPtr hwnd, int x, int y) {
        if (hwnd == IntPtr.Zero || !SetForegroundWindow(hwnd) || !SetCursorPos(x, y)) return false;
        mouse_event(0x0008, 0, 0, 0, UIntPtr.Zero);
        mouse_event(0x0010, 0, 0, 0, UIntPtr.Zero);
        return true;
    }
    public static bool PressEnter(IntPtr hwnd) {
        return hwnd != IntPtr.Zero
            && PostMessage(hwnd, 0x0100, new IntPtr(0x0D), IntPtr.Zero)
            && PostMessage(hwnd, 0x0101, new IntPtr(0x0D), IntPtr.Zero);
    }
    public static bool IsNativeWindow(IntPtr hwnd) { return hwnd != IntPtr.Zero && IsWindow(hwnd); }
}
'@

$context = Get-Content -LiteralPath $ContextPath -Raw -Encoding UTF8 | ConvertFrom-Json
$env:PIXEL_TART_ISOLATED_RUNTIME = '1'
$env:PIXEL_TART_ISOLATED_RUNTIME_ROOT = [IO.Path]::GetFullPath($context.RuntimeRoot)

function Decode([string]$value) { [Text.Encoding]::UTF8.GetString([Convert]::FromBase64String($value)) }
$N = @{
    Workbench=Decode '5bel5L2c5Y+w'; WorkbenchCalendar=Decode '5bel5L2c5Y+w5Y+z5L6n5bel5L2c5pel5Y6G'; StartLocal=Decode '5byA5aeL5pys5Zyw5YiG54mH'
    Calendar=Decode '5bel5L2c5pel5Y6G'; NewBooking=Decode '5paw5bu65ouN5pGE5o6S5pyf'; QuickEditor=Decode '5ouN5pGE5b+r6YCf57yW6L6R5Zmo'
    ProjectName=Decode '6aG555uu5ZCN56ew'; ContinuePlanning=Decode '5L+d5a2Y5bm257un57ut5a6M5pW05ouN5pGE562W5YiS'; FullEditor=Decode '5ouN5pGE5o6S5pyf5YiG5q2l57yW6L6R5Zmo'
    Basic=Decode '5Z+656GA5L+h5oGv'; TimeWeather=Decode '5pe26Ze05aSp5rCU'; Planning=Decode '562W5YiS6LWE5paZ'; PeopleFinance=Decode '5Lq65ZGY5pS25pSv'; Cancel=Decode '5Y+W5raI'
    Details=Decode '5ouN5pGE5o6S5pyf6K+m5oOF'; QuickEdit=Decode '5b+r6YCf57yW6L6R5o6S5pyf'; CloseQuick=Decode '5YWz6Zet5ouN5pGE5b+r6YCf57yW6L6R5Zmo'
    CloseDay=Decode '5YWz6Zet5pys5pel5qGj5pyf'; OpenDay=Decode '5byA5pS+5pys5pel5qGj5pyf'; Closed=Decode '5bey5YWz6Zet'
    Toolbox=Decode '5bel5YW3566x'; Raw=Decode 'UkFXIOi9rCBKUEc='; Batch=Decode '5om56YeP5Y6L57yp'; Organize=Decode '5pW055CG5Zu+54mH'; Collage=Decode '5ou85Zu+'
    RawPinned=Decode 'UkFXIOi9rCBKUEfvvIzlt7Llm7rlrprvvIzku47lt6XkvZzlj7Dlj5bmtojlm7rlrpo='; RawUnpinned=Decode 'UkFXIOi9rCBKUEfvvIzmnKrlm7rlrprvvIzlm7rlrprliLDlt6XkvZzlj7A='
    StartRaw=Decode '5byA5aeL6L2s5o2i'; StartBatch=Decode '5byA5aeL5Y6L57yp'; OrganizePage=Decode '5pW055CG5Zu+54mH6aG16Z2i'; CollagePage=Decode '5ou85Zu+6aG16Z2i'
    Finance=Decode '5pGE5b2x5pS25pSv'; NewIncome=Decode '5paw5bu65pS25YWl'; SaveFinance=Decode '5L+d5a2Y5pS25pSv6K6w5b2V'; CancelFinance=Decode '5Y+W5raI57yW6L6R5pS25pSv6K6w5b2V'
    Online=Decode '5Zyo57q/6YCJ54mH'; ProviderNone=Decode '5Zyo57q/6YCJ54mH5pyN5Yqh5bCa5pyq6YWN572u'; CreateSelection=Decode '5Yib5bu66YCJ54mH6aG555uu'
    SelectionName=Decode '6YCJ54mH6aG555uu5ZCN'; SelectionClient=Decode '6YCJ54mH5a6i5oi3'; SelectionTarget=Decode '55uu5qCH6YCJ54mH5pWw6YeP'; CreateImport=Decode '5Yib5bu65bm25a+85YWl54Wn54mH'
    Photos=Decode '54Wn54mH'; ClientSelection=Decode '5a6i5oi36YCJ54mH'; Settings=Decode '6K6+572u'; Delivery=Decode '5Lqk5LuY57uT5p6c'; AddPhotos=Decode '5re75Yqg54Wn54mH'; Rules=Decode '6YCJ54mH6KeE5YiZ'; Sync=Decode '5ZCM5q2l5b2S54mH5bel5L2c5Yy6'
    Tether=Decode '6IGU5py65ouN5pGE'; TetherStart=Decode '6IGU5py65ouN5pGE5ZCv5Yqo6aG1'
}

$completedAt = [DateTimeOffset]::UtcNow
$completedAtText = $completedAt.ToString('O')
$completionValue = "KitaoPhotoSelector-Onboarding-1.2.0-Completion|2.3.0|$completedAtText"
$sha256 = [Security.Cryptography.SHA256]::Create()
try { $completionProof = ([BitConverter]::ToString($sha256.ComputeHash([Text.Encoding]::UTF8.GetBytes($completionValue)))).Replace('-', '') }
finally { $sha256.Dispose() }
$settings = [ordered]@{
    Appearance=[ordered]@{Theme=2;SidebarCollapsed=$false}
    PinnedQuickTools=@('PhotoOrganize','RawToJpeg','BatchCompress','Collage')
    QuickToolLayout=[ordered]@{SchemaVersion='1.0';OrderedToolIds=@('PhotoOrganize','RawToJpeg','BatchCompress','Collage')}
    ProductQuickToolLayout=[ordered]@{SchemaVersion='1.0';OrderedToolIds=@('PhotoOrganize','RawToJpeg','BatchCompress','Collage')}
    WindowWidth=1600;WindowHeight=900;WindowMaximized=$false
    onboardingCompleted=$true;onboardingVersion='2.3.0';onboardingCompletedAt=$completedAtText;onboardingCurrentStep=22
    onboardingLegacyUser=$false;onboardingUpgradeOfferShown=$true;onboardingCompletionProof=$completionProof
}
New-Item -ItemType Directory -Force -Path $context.RuntimeRoot | Out-Null
$settings | ConvertTo-Json -Depth 12 | Set-Content -LiteralPath (Join-Path $context.RuntimeRoot 'settings.json') -Encoding UTF8

$sourceImage = Join-Path $context.InputRoot 'PT_ACCEPTANCE.jpg'
$bitmap = [Drawing.Bitmap]::new(1600,1000)
$graphics = [Drawing.Graphics]::FromImage($bitmap)
try {
    $graphics.Clear([Drawing.Color]::FromArgb(18,28,36))
    $brush = [Drawing.SolidBrush]::new([Drawing.Color]::FromArgb(24,168,140))
    try { $graphics.FillRectangle($brush,160,160,1280,680) } finally { $brush.Dispose() }
    $bitmap.Save($sourceImage,[Drawing.Imaging.ImageFormat]::Jpeg)
}
finally { $graphics.Dispose();$bitmap.Dispose() }

$checkNames = @(
    'RuntimeIsolation','InstalledCandidateStarted','Workbench','WorkbenchMiniCalendar','QuickCreate','FullPlanning','QuickEdit','Calendar','CalendarContextMenu','ClosedDay','ClosedDayPersisted',
    'Toolbox','PinRoundTrip','RawTool','BatchCompressionTool','OrganizeTool','CollageTool','Finance','OnlineSelection','ProviderNone','SelectionProjectCreated','SelectionProxyReady','SelectionFourTabs','SelectionResultSync','Tether','GracefulExit'
)
$checks = [ordered]@{}
foreach($name in $checkNames){$checks[$name]=$false}
$result = [ordered]@{
    Passed=$false;RunId=$context.RunId;IsolationMethod='Win32 CreateDesktopW + PIXEL_TART_ISOLATED_RUNTIME';CurrentDesktopOperated=$false
    InstalledBinarySha256=(Get-FileHash -LiteralPath $context.AppExe -Algorithm SHA256).Hash
    Checks=$checks;Steps=@();InstalledUiVerified=[ordered]@{};Unverified=@();Error='';StartedAt=[DateTimeOffset]::Now.ToString('O')
    VerificationBasis=[ordered]@{
        BookingAndTools='Actual clicks against the installed candidate on an isolated Win32 desktop.'
        SelectionProjectAndProxy='Actual create/import clicks plus installed-binary persistence and proxy-file probes inside the isolated runtime.'
        SelectionResultSync='A final-result fixture is injected only into the isolated persisted workspace; Delivery-tab and sync-folder actions are actual installed UI clicks.'
        ClosedDay='Actual calendar context-menu click followed by a restart persistence probe.'
    }
    FixtureInjections=[ordered]@{SelectionFinalResult=$false}
}
$script:process=$null;$script:main=$null;$script:mainHandle=0

function Get-Elements($root,$scope=[System.Windows.Automation.TreeScope]::Descendants){try{@($root.FindAll($scope,[System.Windows.Automation.Condition]::TrueCondition))}catch{@()}}
function Find-Control($root,[string]$name='',[string]$automationId='',[System.Windows.Automation.ControlType]$type=$null,[int]$timeout=12,[switch]$contains,[switch]$includeOffscreen){
    $deadline=[DateTime]::UtcNow.AddSeconds($timeout)
    do{
        foreach($element in Get-Elements $root){
            try{
                if($type -and $element.Current.ControlType -ne $type){continue}
                if($automationId -and $element.Current.AutomationId -ne $automationId){continue}
                if(-not $includeOffscreen -and $element.Current.IsOffscreen){continue}
                if($name -and (($contains -and $element.Current.Name -notlike "*$name*") -or (-not $contains -and $element.Current.Name -ne $name))){continue}
                return $element
            }catch{}
        }
        Start-Sleep -Milliseconds 180
    }while([DateTime]::UtcNow -lt $deadline)
    return $null
}
function Find-ProcessWindow([int]$processId,[int]$timeout=25){
    $condition=New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::ProcessIdProperty,$processId)
    $deadline=[DateTime]::UtcNow.AddSeconds($timeout)
    do{
        foreach($window in @([System.Windows.Automation.AutomationElement]::RootElement.FindAll([System.Windows.Automation.TreeScope]::Children,$condition))){
            try{if($window.Current.ControlType -eq [System.Windows.Automation.ControlType]::Window -and -not $window.Current.IsOffscreen){return $window}}catch{}
        }
        Start-Sleep -Milliseconds 250
    }while([DateTime]::UtcNow -lt $deadline)
    return $null
}
function Invoke-Control($element){
    if($null -eq $element){throw 'Required UI control was not found.'}
    if(-not $element.Current.IsEnabled){throw "UI control is disabled: $($element.Current.Name)"}
    $pattern=$null
    if($element.TryGetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern,[ref]$pattern)){$pattern.Invoke();return}
    if($element.TryGetCurrentPattern([System.Windows.Automation.SelectionItemPattern]::Pattern,[ref]$pattern)){$pattern.Select();return}
    throw "UI control is not invokable: $($element.Current.Name)"
}
function Set-ControlValue($element,[string]$value){
    if($null -eq $element){throw 'Required value control was not found.'}
    $pattern=$null
    if(-not $element.TryGetCurrentPattern([System.Windows.Automation.ValuePattern]::Pattern,[ref]$pattern)){throw "Value pattern unavailable: $($element.Current.Name)"}
    $pattern.SetValue($value)
}
function Redact-UiName([string]$name){
    if([string]::IsNullOrWhiteSpace($name)){return ''}
    if($name -match '[A-Za-z]:\\' -or $name -match '\.(jpg|jpeg|png|raw|arw|cr2|cr3|nef|dng|raf|rw2|orf|pef|srw)(\b|$)'){return '[redacted-file-or-path]'}
    return $name
}
function Save-UiTree([string]$name){
    if($null -eq $script:main){return}
    $path=Join-Path $context.EvidenceRoot ($name+'.ui.json')
    $items=@(Get-Elements $script:main|ForEach-Object{try{if($_.Current.Name){[ordered]@{Type=$_.Current.ControlType.ProgrammaticName;Name=(Redact-UiName $_.Current.Name);AutomationId=$_.Current.AutomationId;Enabled=$_.Current.IsEnabled;Offscreen=$_.Current.IsOffscreen}}}catch{}}|Select-Object -First 800)
    $items|ConvertTo-Json -Depth 6|Set-Content -LiteralPath $path -Encoding UTF8
}
function Invoke-Step([string]$name,[scriptblock]$body){
    $started=[DateTimeOffset]::Now
    try{
        & $body
        $result.Steps+=,[ordered]@{Name=$name;Passed=$true;DurationMs=[int]([DateTimeOffset]::Now-$started).TotalMilliseconds;Error=''}
        Save-UiTree $name
        return $true
    }catch{
        $result.Steps+=,[ordered]@{Name=$name;Passed=$false;DurationMs=[int]([DateTimeOffset]::Now-$started).TotalMilliseconds;Error=$_.Exception.Message}
        Save-UiTree ($name+'.failure')
        return $false
    }
}
function Navigate([string]$name){
    $button=Find-Control $script:main $name '' ([System.Windows.Automation.ControlType]::Button) 12
    Invoke-Control $button
    Start-Sleep -Milliseconds 450
}
function Find-Dialog([int]$processId,[int]$timeout=15){
    $deadline=[DateTime]::UtcNow.AddSeconds($timeout)
    do{
        foreach($window in Get-Elements ([System.Windows.Automation.AutomationElement]::RootElement) ([System.Windows.Automation.TreeScope]::Descendants)){
            try{
                if($window.Current.ControlType -ne [System.Windows.Automation.ControlType]::Window -or $window.Current.ProcessId -ne $processId -or $window.Current.NativeWindowHandle -eq $script:mainHandle){continue}
                if(Find-Control $window '' '1' ([System.Windows.Automation.ControlType]::Button) 1 -includeOffscreen){return $window}
            }catch{}
        }
        Start-Sleep -Milliseconds 200
    }while([DateTime]::UtcNow -lt $deadline)
    return $null
}
function Complete-DialogPath($dialog,[string]$path){
    if($null -eq $dialog){throw 'Native file or folder dialog was not found.'}
    $edit=Find-Control $dialog '' '1148' ([System.Windows.Automation.ControlType]::Edit) 5 -includeOffscreen
    if($null -eq $edit){$edit=Get-Elements $dialog|Where-Object{try{$_.Current.ControlType -eq [System.Windows.Automation.ControlType]::Edit}catch{$false}}|Select-Object -Last 1}
    Set-ControlValue $edit $path
    $edit.SetFocus()
    $dialogHandle=[IntPtr]$dialog.Current.NativeWindowHandle
    if(-not [PixelTartInstalledUiNative]::PressEnter($dialogHandle)){throw 'Native dialog Enter key could not be posted.'}
    $deadline=[DateTime]::UtcNow.AddSeconds(8)
    do{if(-not [PixelTartInstalledUiNative]::IsNativeWindow($dialogHandle)){return};Start-Sleep -Milliseconds 180}while([DateTime]::UtcNow -lt $deadline)
    throw 'Native dialog remained open after its action was invoked.'
}
function Open-ContextMenu($element){
    if($null -eq $element){throw 'Calendar day element was not found.'}
    $selection=$null
    try{if($element.TryGetCurrentPattern([System.Windows.Automation.SelectionItemPattern]::Pattern,[ref]$selection)){$selection.Select()}}catch{}
    try{$element.SetFocus()}catch{}
    Start-Sleep -Milliseconds 150
    $bounds=$element.Current.BoundingRectangle
    $x=[int]($bounds.Left+($bounds.Width/2))
    $y=[int]($bounds.Top+($bounds.Height/2))
    if(-not [PixelTartInstalledUiNative]::RightClick([IntPtr]$script:mainHandle,$x,$y)){throw 'Calendar day right-click could not be sent.'}
    Start-Sleep -Milliseconds 400
}
function Find-CalendarDayItem([DateTime]$date){
    $text=Find-Control $script:main $date.Day.ToString([Globalization.CultureInfo]::InvariantCulture) '' ([System.Windows.Automation.ControlType]::Text) 12
    if($null -eq $text){return $null}
    $walker=[System.Windows.Automation.TreeWalker]::ControlViewWalker
    $current=$text
    for($index=0;$index -lt 8 -and $null -ne $current;$index++){
        try{if($current.Current.ControlType -eq [System.Windows.Automation.ControlType]::DataItem){return $current}}catch{}
        $current=$walker.GetParent($current)
    }
    return $null
}
function Start-App(){
    $script:process=Start-Process -FilePath $context.AppExe -PassThru
    $script:main=Find-ProcessWindow $script:process.Id 30
    if($null -eq $script:main){throw 'Installed candidate main window did not appear.'}
    $script:mainHandle=$script:main.Current.NativeWindowHandle
}
function Close-App(){
    if($null -eq $script:process -or $script:process.HasExited){return $true}
    $windowPattern=$null
    try{
        if($script:main -and $script:main.TryGetCurrentPattern([System.Windows.Automation.WindowPattern]::Pattern,[ref]$windowPattern)){$windowPattern.Close()}
        else{$null=$script:process.CloseMainWindow()}
    }catch{$null=$script:process.CloseMainWindow()}
    if($script:process.WaitForExit(10000)){return $true}
    Stop-Process -Id $script:process.Id -Force -ErrorAction SilentlyContinue
    try{$script:process.WaitForExit(5000)|Out-Null}catch{}
    return $false
}

try{
    Start-App
    $checks.InstalledCandidateStarted=$true
    $checks.RuntimeIsolation=$env:PIXEL_TART_ISOLATED_RUNTIME -eq '1' -and [IO.Path]::GetFullPath($env:PIXEL_TART_ISOLATED_RUNTIME_ROOT) -eq [IO.Path]::GetFullPath($context.RuntimeRoot)

    if(-not $FailuresOnly){
    if(Invoke-Step '01-workbench' {
        $checks.Workbench=$null -ne (Find-Control $script:main $N.StartLocal '' ([System.Windows.Automation.ControlType]::Button) 15)
        $checks.WorkbenchMiniCalendar=$null -ne (Find-Control $script:main $N.WorkbenchCalendar '' $null 10)
        if(-not($checks.Workbench -and $checks.WorkbenchMiniCalendar)){throw 'Workbench landmarks were incomplete.'}
    }){}

    if(Invoke-Step '02-booking-workflow' {
        Navigate $N.Calendar
        if($null -eq (Find-Control $script:main $N.Calendar '' $null 12)){throw 'Calendar page did not open.'}
        $checks.Calendar=$true
        Invoke-Control (Find-Control $script:main $N.NewBooking '' ([System.Windows.Automation.ControlType]::Button) 10)
        $quick=Find-Control $script:main $N.QuickEditor '' $null 10
        if($null -eq $quick){throw 'Quick create editor did not open.'}
        Set-ControlValue (Find-Control $quick $N.ProjectName '' ([System.Windows.Automation.ControlType]::Edit) 8) 'PT Installed Booking'
        $checks.QuickCreate=$true
        Invoke-Control (Find-Control $quick $N.ContinuePlanning '' ([System.Windows.Automation.ControlType]::Button) 8)
        $full=Find-Control $script:main $N.FullEditor '' $null 20
        if($null -eq $full){throw 'Full planning editor did not open.'}
        foreach($label in @($N.Basic,$N.TimeWeather,$N.Planning,$N.PeopleFinance)){if($null -eq (Find-Control $full $label '' $null 5)){throw "Full planning section missing: $label"}}
        $checks.FullPlanning=$true
        Invoke-Control (Find-Control $full $N.Cancel '' ([System.Windows.Automation.ControlType]::Button) 8)
        Start-Sleep -Milliseconds 500
        $booking=Find-Control $script:main 'PT Installed Booking' '' ([System.Windows.Automation.ControlType]::Button) 15 -contains
        Invoke-Control $booking
        $details=Find-Control $script:main $N.Details '' $null 12
        if($null -eq $details){throw 'Booking details did not open.'}
        Invoke-Control (Find-Control $details $N.QuickEdit '' ([System.Windows.Automation.ControlType]::Button) 8)
        $drawer=Find-Control $script:main $N.QuickEditor '' $null 12
        if($null -eq $drawer){throw 'Quick edit drawer did not open.'}
        $checks.QuickEdit=$true
        Invoke-Control (Find-Control $drawer $N.CloseQuick '' ([System.Windows.Automation.ControlType]::Button) 8)
        Start-Sleep -Milliseconds 450
    }){}
    }

    if(Invoke-Step '03-calendar-close-day' {
        Navigate $N.Calendar
        if($null -eq (Find-Control $script:main $N.Calendar '' $null 12)){throw 'Calendar page did not open.'}
        $checks.Calendar=$true
        $target=[DateTime]::Today.AddDays(2)
        $day=Find-CalendarDayItem $target
        Open-ContextMenu $day
        $menu=Find-Control ([System.Windows.Automation.AutomationElement]::RootElement) $N.CloseDay '' ([System.Windows.Automation.ControlType]::MenuItem) 8 -includeOffscreen
        if($null -eq $menu){throw 'Close-day context menu command did not appear.'}
        $checks.CalendarContextMenu=$true
        Invoke-Control $menu
        Start-Sleep -Milliseconds 900
        $checks.ClosedDay=$null -ne (Find-Control $script:main $N.Closed '' $null 8 -contains)
        if(-not $checks.ClosedDay){throw 'Closed-day marker was not visible.'}
    }){}

    if(-not $FailuresOnly){
    if(Invoke-Step '04-toolbox-pin-and-tools' {
        Navigate $N.Toolbox
        $uiRoot=[System.Windows.Automation.AutomationElement]::RootElement
        $checks.Toolbox=$null -ne (Find-Control $uiRoot $N.Raw '' ([System.Windows.Automation.ControlType]::Button) 10)
        $pin=Find-Control $uiRoot $N.RawPinned '' ([System.Windows.Automation.ControlType]::Button) 8
        Invoke-Control $pin
        $unpinState=Find-Control $uiRoot $N.RawUnpinned '' ([System.Windows.Automation.ControlType]::Button) 8
        Invoke-Control $unpinState
        $checks.PinRoundTrip=$null -ne (Find-Control $uiRoot $N.RawPinned '' ([System.Windows.Automation.ControlType]::Button) 8)
        if(-not $checks.PinRoundTrip){throw 'Pin state did not round-trip.'}

        Invoke-Control (Find-Control $uiRoot $N.Raw '' ([System.Windows.Automation.ControlType]::Button) 8)
        $checks.RawTool=$null -ne (Find-Control $script:main $N.StartRaw '' ([System.Windows.Automation.ControlType]::Button) 10)
        Navigate $N.Toolbox
        Invoke-Control (Find-Control $uiRoot $N.Batch '' ([System.Windows.Automation.ControlType]::Button) 8)
        $checks.BatchCompressionTool=$null -ne (Find-Control $script:main $N.StartBatch '' ([System.Windows.Automation.ControlType]::Button) 10)
        Navigate $N.Toolbox
        Invoke-Control (Find-Control $uiRoot $N.Organize '' ([System.Windows.Automation.ControlType]::Button) 8)
        $checks.OrganizeTool=$null -ne (Find-Control $script:main $N.OrganizePage '' $null 10)
        Navigate $N.Toolbox
        Invoke-Control (Find-Control $uiRoot $N.Collage '' ([System.Windows.Automation.ControlType]::Button) 8)
        $checks.CollageTool=$null -ne (Find-Control $script:main $N.CollagePage '' $null 10)
        if(-not($checks.RawTool -and $checks.BatchCompressionTool -and $checks.OrganizeTool -and $checks.CollageTool)){throw 'One or more product tools did not open.'}
    }){}

    if(Invoke-Step '05-finance' {
        Navigate $N.Finance
        $finance=Find-Control $script:main $N.Finance '' $null 12
        if($null -eq $finance){throw 'Finance page did not open.'}
        Invoke-Control (Find-Control $script:main $N.NewIncome '' ([System.Windows.Automation.ControlType]::Button) 8)
        if($null -eq (Find-Control $script:main $N.SaveFinance '' ([System.Windows.Automation.ControlType]::Button) 8)){throw 'Finance editor did not open.'}
        $checks.Finance=$true
        $cancel=Find-Control $script:main $N.CancelFinance '' ([System.Windows.Automation.ControlType]::Button) 5
        if($cancel){Invoke-Control $cancel}
    }){}
    }

    if(Invoke-Step '06-online-selection-create' {
        Navigate $N.Online
        if($null -eq (Find-Control $script:main $N.Online '' $null 12)){throw 'Online selection page did not open.'}
        $checks.OnlineSelection=$true
        $checks.ProviderNone=$null -ne (Find-Control $script:main $N.ProviderNone '' $null 8 -contains)
        Invoke-Control (Find-Control $script:main $N.CreateSelection '' ([System.Windows.Automation.ControlType]::Button) 8)
        Set-ControlValue (Find-Control $script:main $N.SelectionName '' ([System.Windows.Automation.ControlType]::Edit) 8) 'PT Installed Selection'
        Set-ControlValue (Find-Control $script:main $N.SelectionClient '' ([System.Windows.Automation.ControlType]::Edit) 8) 'Isolated Client'
        Set-ControlValue (Find-Control $script:main $N.SelectionTarget '' ([System.Windows.Automation.ControlType]::Edit) 8) '1'
        Invoke-Control (Find-Control $script:main $N.CreateImport '' ([System.Windows.Automation.ControlType]::Button) 8)
        Complete-DialogPath (Find-Dialog $script:process.Id 15) $sourceImage
        if($null -eq (Find-Control $script:main 'PT Installed Selection' '' $null 35 -contains)){throw 'Created selection project did not open.'}
        $checks.SelectionProjectCreated=$true
        $tabChecks=@()
        Invoke-Control (Find-Control $script:main $N.Photos '' ([System.Windows.Automation.ControlType]::Button) 8);$tabChecks+=($null -ne (Find-Control $script:main $N.AddPhotos '' ([System.Windows.Automation.ControlType]::Button) 8))
        Invoke-Control (Find-Control $script:main $N.ClientSelection '' ([System.Windows.Automation.ControlType]::Button) 8);$tabChecks+=($null -ne (Find-Control $script:main $N.ClientSelection '' $null 8))
        Invoke-Control (Find-Control $script:main $N.Settings '' ([System.Windows.Automation.ControlType]::Button) 8);$tabChecks+=($null -ne (Find-Control $script:main $N.Rules '' $null 8))
        Invoke-Control (Find-Control $script:main $N.Delivery '' ([System.Windows.Automation.ControlType]::Button) 8);$tabChecks+=($null -ne (Find-Control $script:main $N.Sync '' ([System.Windows.Automation.ControlType]::Button) 8 -includeOffscreen))
        $checks.SelectionFourTabs=($tabChecks -notcontains $false)
        if(-not $checks.SelectionFourTabs){throw 'One or more selection project tabs failed.'}
    }){}

    if(-not $FailuresOnly){
    if(Invoke-Step '07-tether' {
        Navigate $N.Tether
        $checks.Tether=$null -ne (Find-Control $script:main '' 'TetherMonitorView' $null 12)
        if(-not $checks.Tether){throw 'Tether start page did not open.'}
    }){}
    }

    $firstClose=Close-App
    $workspacePath=Join-Path $context.RuntimeRoot 'OnlineSelection\workspace.json'
    if(Invoke-Step '08-selection-proxy-fixture' {
        if(-not(Test-Path -LiteralPath $workspacePath)){throw 'Selection workspace was not persisted.'}
        $workspace=Get-Content -LiteralPath $workspacePath -Raw -Encoding UTF8|ConvertFrom-Json
        if(@($workspace.Projects).Count -ne 1 -or @($workspace.Assets).Count -ne 1){throw 'Selection project or asset count was unexpected.'}
        $project=@($workspace.Projects)[0];$asset=@($workspace.Assets)[0]
        $proxy=[string]$asset.ProxyJpegPath
        $checks.SelectionProxyReady=-not[string]::IsNullOrWhiteSpace($proxy) -and (Test-Path -LiteralPath $proxy) -and [IO.Path]::GetFullPath($proxy).StartsWith([IO.Path]::GetFullPath($context.RuntimeRoot),[StringComparison]::OrdinalIgnoreCase)
        if(-not $checks.SelectionProxyReady){throw 'Selection proxy was not ready inside the isolated runtime.'}
        $workspace.FinalResults=@([ordered]@{SelectionProjectId=$project.Id;ConfirmedAtUtc=[DateTimeOffset]::UtcNow.ToString('O');Items=@([ordered]@{SelectionProjectId=$project.Id;ImageId=$asset.Id;OriginalFileName=$asset.OriginalFileName;Selected=$true;Favorite=$false;CustomerNote='isolated-acceptance';ExtraSelected=$false})})
        $workspace|ConvertTo-Json -Depth 30|Set-Content -LiteralPath $workspacePath -Encoding UTF8
        $result.FixtureInjections.SelectionFinalResult=$true
    }){}

    if(Invoke-Step '09-restart-persistence-and-sync' {
        Start-App
        Navigate $N.Calendar
        $checks.ClosedDayPersisted=$null -ne (Find-Control $script:main $N.Closed '' $null 10 -contains)
        Navigate $N.Online
        $projectButton=Find-Control $script:main 'PT Installed Selection' '' ([System.Windows.Automation.ControlType]::Button) 15 -contains
        Invoke-Control $projectButton
        Invoke-Control (Find-Control $script:main $N.Delivery '' ([System.Windows.Automation.ControlType]::Button) 12)
        $sync=Find-Control $script:main $N.Sync '' ([System.Windows.Automation.ControlType]::Button) 10 -includeOffscreen
        Invoke-Control $sync
        Complete-DialogPath (Find-Dialog $script:process.Id 15) $context.SyncRoot
        $deadline=[DateTime]::UtcNow.AddSeconds(20)
        do{$archives=@(Get-ChildItem -LiteralPath $context.SyncRoot -Filter 'selection-*.json' -File -ErrorAction SilentlyContinue);if($archives.Count){break};Start-Sleep -Milliseconds 250}while([DateTime]::UtcNow -lt $deadline)
        $checks.SelectionResultSync=$archives.Count -eq 1
        if(-not($checks.ClosedDayPersisted -and $checks.SelectionResultSync)){throw 'Restart persistence or selection result sync failed.'}
    }){}

    $checks.GracefulExit=Close-App
    $installedProvider=Get-Content -LiteralPath (Join-Path $context.InstalledRoot 'appsettings.license.json') -Raw -ErrorAction SilentlyContinue
    $checks.ProviderNone=$checks.ProviderNone -and $installedProvider -match '"Provider"\s*:\s*"None"'
}
catch{
    $result.Error=$_.Exception.ToString()
}
finally{
    if($script:process -and -not $script:process.HasExited){$checks.GracefulExit=Close-App}
    $checks.RuntimeIsolation=$checks.RuntimeIsolation -and (Test-Path -LiteralPath (Join-Path $context.RuntimeRoot 'Data\pixel-tart.db'))
    foreach($name in $checkNames){$result.InstalledUiVerified[$name]=[bool]$checks[$name];if(-not $checks[$name]){$result.Unverified+=,$name}}
    $result.Passed=@($checkNames|Where-Object{-not $checks[$_]}).Count -eq 0
    $result.CompletedAt=[DateTimeOffset]::Now.ToString('O')
    $result|ConvertTo-Json -Depth 20|Set-Content -LiteralPath (Join-Path $context.EvidenceRoot 'result.json') -Encoding UTF8
}
Get-Content -LiteralPath (Join-Path $context.EvidenceRoot 'result.json') -Raw -Encoding UTF8
if(-not $result.Passed){exit 1}
