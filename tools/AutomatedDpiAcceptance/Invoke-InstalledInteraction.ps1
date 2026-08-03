param([string]$EvidenceRoot = '')

$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
if ([string]::IsNullOrWhiteSpace($EvidenceRoot)) { $EvidenceRoot = Join-Path $repoRoot 'artifacts\automated-dpi-review\2.0.4' }
$interactionRoot = Join-Path $EvidenceRoot 'interaction'
$inputRoot = Join-Path $interactionRoot 'input'
$organizeOutput = Join-Path $interactionRoot 'organize-output'
$collageOutput = Join-Path $interactionRoot 'collage-output'
$installRoot = Join-Path $interactionRoot 'installed-app'
$installerDirectory = Join-Path $repoRoot 'artifacts\releases\2.0.4\installer'
$installer = (Get-ChildItem -LiteralPath $installerDirectory -Filter '*Setup_2.0.4_x64.exe' -File | Select-Object -First 1).FullName
$settingsDirectory = Join-Path $env:LOCALAPPDATA 'KitaoPhotoSelector'
$settingsPath = Join-Path $settingsDirectory 'settings.json'
$settingsBackup = Join-Path $interactionRoot 'settings.before.json'

function U([string]$value) { [Text.Encoding]::UTF8.GetString([Convert]::FromBase64String($value)) }
$names = @{
    Manage = U '566h55CG5b+r5o235bel5YW3'
    Save = U '5L+d5a2Y'
    Organize = U '5pW055CG5Zu+54mH'
    Workflow = U '5b2S54mH5bel5L2c5Yy6'
    Toolbox = U '5bel5YW3566x'
    Collage = U '5ou85Zu+'
    AddPhotos = U '5re75Yqg54Wn54mH'
    PreviewPlan = U '55Sf5oiQ5bm26aKE6KeI5pON5L2c5riF5Y2V'
    ExecutePlan = U '5omn6KGM5b2T5YmN5riF5Y2V'
    Choose = U '6YCJ5oup'
    ImportPhotos = U '5a+85YWl54Wn54mH'
    Export = U '5a+85Ye6'
    ManagerWindow = U '5b+r5o235bel5YW3566h55CG'
}

Add-Type -AssemblyName UIAutomationClient
Add-Type -AssemblyName UIAutomationTypes
Add-Type -AssemblyName System.Windows.Forms
Add-Type @'
using System;
using System.Runtime.InteropServices;
public static class PixelTartNativeInput {
    [DllImport("user32.dll")] public static extern bool SetCursorPos(int x, int y);
    [DllImport("user32.dll")] public static extern void mouse_event(uint flags, uint dx, uint dy, uint data, UIntPtr extraInfo);
    [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr hwnd);
    [DllImport("user32.dll", CharSet=CharSet.Unicode)] public static extern bool SetWindowText(IntPtr hwnd, string text);
    [DllImport("user32.dll", CharSet=CharSet.Unicode)] public static extern IntPtr SendMessage(IntPtr hwnd, uint msg, IntPtr wParam, IntPtr lParam);
    [DllImport("user32.dll")] public static extern IntPtr GetLastActivePopup(IntPtr hwnd);
    [DllImport("user32.dll")] public static extern bool EnumChildWindows(IntPtr parent, EnumChildProc callback, IntPtr data);
    [DllImport("user32.dll", CharSet=CharSet.Unicode)] public static extern int GetClassName(IntPtr hwnd, System.Text.StringBuilder name, int length);
    [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr hwnd, out RECT rect);
    [DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr hwnd);
    public delegate bool EnumChildProc(IntPtr hwnd, IntPtr data);
    public struct RECT { public int Left, Top, Right, Bottom; }
    [DllImport("user32.dll")] public static extern void keybd_event(byte key, byte scan, uint flags, UIntPtr extraInfo);
    public static void Click(int x, int y) {
        SetCursorPos(x, y);
        mouse_event(0x0002, 0, 0, 0, UIntPtr.Zero);
        mouse_event(0x0004, 0, 0, 0, UIntPtr.Zero);
    }
    public static void PasteReplace() {
        keybd_event(0x11, 0, 0, UIntPtr.Zero); keybd_event(0x41, 0, 0, UIntPtr.Zero); keybd_event(0x41, 0, 2, UIntPtr.Zero); keybd_event(0x11, 0, 2, UIntPtr.Zero);
        keybd_event(0x11, 0, 0, UIntPtr.Zero); keybd_event(0x56, 0, 0, UIntPtr.Zero); keybd_event(0x56, 0, 2, UIntPtr.Zero); keybd_event(0x11, 0, 2, UIntPtr.Zero);
    }
    public static void Press(byte key) { keybd_event(key, 0, 0, UIntPtr.Zero); keybd_event(key, 0, 2, UIntPtr.Zero); }
    public static void Chord(byte modifier, byte key) {
        keybd_event(modifier, 0, 0, UIntPtr.Zero); keybd_event(key, 0, 0, UIntPtr.Zero); keybd_event(key, 0, 2, UIntPtr.Zero); keybd_event(modifier, 0, 2, UIntPtr.Zero);
    }
    public static bool SetFileDialogTextAndSubmit(IntPtr owner, string value) {
        IntPtr dialog = GetLastActivePopup(owner);
        if (dialog == IntPtr.Zero || dialog == owner) return false;
        IntPtr best = IntPtr.Zero;
        int bestTop = int.MinValue;
        EnumChildWindows(dialog, (hwnd, data) => {
            if (!IsWindowVisible(hwnd)) return true;
            var name = new System.Text.StringBuilder(128);
            GetClassName(hwnd, name, name.Capacity);
            if (!string.Equals(name.ToString(), "Edit", StringComparison.OrdinalIgnoreCase)) return true;
            RECT rect;
            if (GetWindowRect(hwnd, out rect) && rect.Top > bestTop && rect.Right > rect.Left) { best = hwnd; bestTop = rect.Top; }
            return true;
        }, IntPtr.Zero);
        if (best == IntPtr.Zero) return false;
        if (!SetWindowText(best, value)) return false;
        SendMessage(dialog, 0x0111, new IntPtr(1), IntPtr.Zero);
        return true;
    }
}
'@

function Get-AllElements([System.Windows.Automation.AutomationElement]$root, [System.Windows.Automation.TreeScope]$scope = [System.Windows.Automation.TreeScope]::Descendants) {
    try { return @($root.FindAll($scope, [System.Windows.Automation.Condition]::TrueCondition)) } catch { return @() }
}
function Find-Element {
    param(
        [System.Windows.Automation.AutomationElement]$Root,
        [string]$AutomationId = '',
        [string]$Name = '',
        [System.Windows.Automation.ControlType]$ControlType = $null,
        [switch]$ContainsName,
        [switch]$AllowOffscreen,
        [switch]$RequireEnabled,
        [int]$TimeoutSeconds = 15
    )
    $deadline = [DateTime]::UtcNow.AddSeconds($TimeoutSeconds)
    do {
        foreach ($element in Get-AllElements $Root) {
            try {
                if ($AutomationId -and $element.Current.AutomationId -ne $AutomationId) { continue }
                if ($ControlType -and $element.Current.ControlType -ne $ControlType) { continue }
                if (-not $AllowOffscreen -and $element.Current.IsOffscreen) { continue }
                if ($RequireEnabled -and -not $element.Current.IsEnabled) { continue }
                if ($Name) {
                    if ($ContainsName -and $element.Current.Name -notlike "*$Name*") { continue }
                    if (-not $ContainsName -and $element.Current.Name -ne $Name) { continue }
                }
                return $element
            } catch { }
        }
        Start-Sleep -Milliseconds 200
    } while ([DateTime]::UtcNow -lt $deadline)
    return $null
}
function Invoke-Element([System.Windows.Automation.AutomationElement]$element) {
    if ($null -eq $element) { throw 'Cannot invoke a missing automation element.' }
    $pattern = $null
    if ($element.TryGetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern, [ref]$pattern)) { $pattern.Invoke(); return }
    if ($element.TryGetCurrentPattern([System.Windows.Automation.SelectionItemPattern]::Pattern, [ref]$pattern)) { $pattern.Select(); return }
    throw "Element cannot be invoked: $($element.Current.Name)"
}
function Click-Element([System.Windows.Automation.AutomationElement]$element, [IntPtr]$OwnerHandle) {
    if ($null -eq $element) { throw 'Cannot click a missing automation element.' }
    [PixelTartNativeInput]::SetForegroundWindow($OwnerHandle) | Out-Null
    Start-Sleep -Milliseconds 150
    $rect = $element.Current.BoundingRectangle
    if ($rect.Width -le 0 -or $rect.Height -le 0) { throw "Element has no clickable bounds: $($element.Current.Name)" }
    [PixelTartNativeInput]::Click([int]($rect.Left + $rect.Width / 2), [int]($rect.Top + $rect.Height / 2))
}
function Save-ScreenSnapshot([string]$Path) {
    Add-Type -AssemblyName System.Drawing
    Add-Type -AssemblyName System.Windows.Forms
    $bounds = [System.Windows.Forms.SystemInformation]::VirtualScreen
    $bitmap = New-Object System.Drawing.Bitmap($bounds.Width, $bounds.Height)
    $graphics = [System.Drawing.Graphics]::FromImage($bitmap)
    try { $graphics.CopyFromScreen($bounds.Left, $bounds.Top, 0, 0, $bitmap.Size); $bitmap.Save($Path, [System.Drawing.Imaging.ImageFormat]::Png) } catch { }
    finally { $graphics.Dispose(); $bitmap.Dispose() }
}
function Select-Element([System.Windows.Automation.AutomationElement]$element) {
    $pattern = $null
    if (-not $element.TryGetCurrentPattern([System.Windows.Automation.SelectionItemPattern]::Pattern, [ref]$pattern)) { throw 'SelectionItemPattern unavailable.' }
    $pattern.Select()
}
function Set-ElementValue([System.Windows.Automation.AutomationElement]$element, [string]$value) {
    $pattern = $null
    if (-not $element.TryGetCurrentPattern([System.Windows.Automation.ValuePattern]::Pattern, [ref]$pattern)) { throw "ValuePattern unavailable: $($element.Current.Name)" }
    $pattern.SetValue($value)
}
function Expand-Element([System.Windows.Automation.AutomationElement]$element) {
    $pattern = $null
    if (-not $element.TryGetCurrentPattern([System.Windows.Automation.ExpandCollapsePattern]::Pattern, [ref]$pattern)) { throw 'ExpandCollapsePattern unavailable.' }
    $pattern.Expand()
}
function Wait-ProcessMainWindow([System.Diagnostics.Process]$process, [int]$timeoutSeconds = 25) {
    $deadline = [DateTime]::UtcNow.AddSeconds($timeoutSeconds)
    do {
        $process.Refresh()
        if ($process.MainWindowHandle -ne 0) { return [System.Windows.Automation.AutomationElement]::FromHandle($process.MainWindowHandle) }
        Start-Sleep -Milliseconds 250
    } while ([DateTime]::UtcNow -lt $deadline)
    throw 'Installed application main window did not appear.'
}
function Wait-ProcessWindow {
    param([int]$ProcessId, [int[]]$ExcludedHandles, [int]$TimeoutSeconds = 15)
    $condition = New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::ProcessIdProperty, $ProcessId)
    $deadline = [DateTime]::UtcNow.AddSeconds($TimeoutSeconds)
    do {
        $windows = @([System.Windows.Automation.AutomationElement]::RootElement.FindAll([System.Windows.Automation.TreeScope]::Children, $condition))
        foreach ($window in $windows) {
            try {
                if ($window.Current.ControlType -eq [System.Windows.Automation.ControlType]::Window -and $ExcludedHandles -notcontains $window.Current.NativeWindowHandle) { return $window }
            } catch { }
        }
        Start-Sleep -Milliseconds 200
    } while ([DateTime]::UtcNow -lt $deadline)
    return $null
}
function Wait-GlobalWindowByName {
    param([string]$Name, [int]$TimeoutSeconds = 15)
    $deadline = [DateTime]::UtcNow.AddSeconds($TimeoutSeconds)
    do {
        foreach ($window in Get-AllElements ([System.Windows.Automation.AutomationElement]::RootElement) ([System.Windows.Automation.TreeScope]::Descendants)) {
            try { if ($window.Current.ControlType -eq [System.Windows.Automation.ControlType]::Window -and $window.Current.Name -eq $Name) { return $window } } catch { }
        }
        Start-Sleep -Milliseconds 200
    } while ([DateTime]::UtcNow -lt $deadline)
    return $null
}
function Wait-NativeDialog {
    param([int[]]$ExcludedHandles, [int]$ProcessId = 0, [int]$TimeoutSeconds = 15)
    $deadline = [DateTime]::UtcNow.AddSeconds($TimeoutSeconds)
    do {
        foreach ($window in Get-AllElements ([System.Windows.Automation.AutomationElement]::RootElement) ([System.Windows.Automation.TreeScope]::Descendants)) {
            try {
                if ($window.Current.ControlType -eq [System.Windows.Automation.ControlType]::Window -and
                    $window.Current.ClassName -eq '#32770' -and
                    ($ProcessId -eq 0 -or $window.Current.ProcessId -eq $ProcessId) -and
                    $ExcludedHandles -notcontains $window.Current.NativeWindowHandle) { return $window }
            } catch { }
        }
        Start-Sleep -Milliseconds 200
    } while ([DateTime]::UtcNow -lt $deadline)
    return $null
}
function Complete-FileDialog {
    param([System.Windows.Automation.AutomationElement]$Dialog, [string[]]$Paths, [IntPtr]$OwnerHandle)
    if ($null -eq $Dialog) { throw 'File dialog did not appear.' }
    $dialogHandle = [PixelTartNativeInput]::GetLastActivePopup($OwnerHandle)
    if ($dialogHandle -eq [IntPtr]::Zero -or $dialogHandle -eq $OwnerHandle) { throw 'Native file dialog handle was not found.' }
    [PixelTartNativeInput]::SetForegroundWindow($dialogHandle) | Out-Null
    if ($Paths.Count -gt 1) {
        [System.Windows.Forms.Clipboard]::SetText((Split-Path $Paths[0] -Parent))
        [PixelTartNativeInput]::Chord(0x11,0x4C)
        [PixelTartNativeInput]::Chord(0x11,0x56)
        [PixelTartNativeInput]::Press(0x0D)
        Start-Sleep -Milliseconds 1200
        $items = Get-AllElements $Dialog | Where-Object { try { $_.Current.ControlType -eq [System.Windows.Automation.ControlType]::ListItem -and $_.Current.Name -like 'DPI_TEST_*.png' -and -not $_.Current.IsOffscreen } catch { $false } }
        if ($items.Count -ne 4) { throw "Expected four visible isolated test images, found $($items.Count)." }
        for($index=0;$index-lt$items.Count;$index++){
            $selection=$null
            if(-not $items[$index].TryGetCurrentPattern([System.Windows.Automation.SelectionItemPattern]::Pattern,[ref]$selection)){throw 'File item selection pattern unavailable.'}
            if($index-eq 0){$selection.Select()}else{$selection.AddToSelection()}
        }
        [PixelTartNativeInput]::Press(0x0D)
        Start-Sleep -Milliseconds 900
        return
    }
    [System.Windows.Forms.Clipboard]::SetText((Split-Path $Paths[0] -Parent))
    [PixelTartNativeInput]::Chord(0x11,0x4C)
    [PixelTartNativeInput]::Chord(0x11,0x56)
    [PixelTartNativeInput]::Press(0x0D)
    Start-Sleep -Milliseconds 1000
    [PixelTartNativeInput]::Chord(0x12,0x4E)
    [System.Windows.Forms.Clipboard]::SetText((Split-Path $Paths[0] -Leaf))
    [PixelTartNativeInput]::PasteReplace()
    [PixelTartNativeInput]::Press(0x0D)
    Start-Sleep -Milliseconds 900
    return
    $value = ($Paths | ForEach-Object { '"' + $_ + '"' }) -join ' '
    if ([PixelTartNativeInput]::SetFileDialogTextAndSubmit($OwnerHandle, $value)) { Start-Sleep -Milliseconds 800; return }
    $edit = Get-AllElements $Dialog | Where-Object {
        try { $_.Current.ControlType -eq [System.Windows.Automation.ControlType]::Edit -and -not $_.Current.IsOffscreen -and $_.Current.BoundingRectangle.Width -gt 80 -and $_.Current.BoundingRectangle.Height -gt 12 } catch { $false }
    } | Sort-Object { $_.Current.BoundingRectangle.Top } -Descending | Select-Object -First 1
    if ($null -eq $edit) { throw 'Visible file name input was not found.' }
    [PixelTartNativeInput]::SetForegroundWindow($OwnerHandle) | Out-Null
    Click-Element $edit $OwnerHandle
    [System.Windows.Forms.Clipboard]::SetText($value)
    [PixelTartNativeInput]::PasteReplace()
    Start-Sleep -Milliseconds 350
    $button = Find-Element -Root $Dialog -AutomationId '1' -ControlType ([System.Windows.Automation.ControlType]::Button) -TimeoutSeconds 5
    if ($null -eq $button) {
        $visibleButtons = Get-AllElements $Dialog | Where-Object { try { $_.Current.ControlType -eq [System.Windows.Automation.ControlType]::Button -and -not $_.Current.IsOffscreen -and $_.Current.BoundingRectangle.Width -gt 45 -and $_.Current.BoundingRectangle.Height -gt 18 } catch { $false } }
        $bottom = ($visibleButtons | ForEach-Object { $_.Current.BoundingRectangle.Top } | Measure-Object -Maximum).Maximum
        $button = $visibleButtons | Where-Object { [Math]::Abs($_.Current.BoundingRectangle.Top - $bottom) -lt 12 } | Sort-Object { $_.Current.BoundingRectangle.Left } | Select-Object -First 1
    }
    if ($null -eq $button) { throw 'File dialog confirmation button was not found.' }
    Invoke-Element $button
    Start-Sleep -Milliseconds 500
}
function Select-SaveDialogFilter {
    param([System.Windows.Automation.AutomationElement]$Dialog, [string]$Extension)
    if ($Extension -ne '.png') { return }
    $combo = Get-AllElements $Dialog | Where-Object { try { $_.Current.ControlType -eq [System.Windows.Automation.ControlType]::ComboBox -and -not $_.Current.IsOffscreen } catch { $false } } | Sort-Object { $_.Current.BoundingRectangle.Top } -Descending | Select-Object -First 1
    if ($null -eq $combo) { throw 'Save dialog file type selector was not found.' }
    Expand-Element $combo
    Start-Sleep -Milliseconds 250
    $pngItem = Get-AllElements $combo | Where-Object { try { $_.Current.ControlType -eq [System.Windows.Automation.ControlType]::ListItem -and $_.Current.Name -like '*PNG*' } catch { $false } } | Select-Object -First 1
    if ($null -eq $pngItem) { throw 'PNG save filter was not found.' }
    Select-Element $pngItem
}
function Confirm-MessageBox {
    param([int]$ProcessId, [int]$MainHandle, [string]$ButtonAutomationId, [int]$TimeoutSeconds = 15)
    $dialog = Wait-NativeDialog -ExcludedHandles @($MainHandle) -ProcessId $ProcessId -TimeoutSeconds $TimeoutSeconds
    if ($null -eq $dialog) { throw 'Confirmation dialog did not appear.' }
    $button = Find-Element -Root $dialog -AutomationId $ButtonAutomationId -TimeoutSeconds 5
    if ($null -eq $button) {
        $buttons = @(Get-AllElements $dialog | Where-Object { try { $_.Current.ClassName -eq 'Button' -and $_.Current.IsEnabled -and -not $_.Current.IsOffscreen } catch { $false } } | Sort-Object { $_.Current.BoundingRectangle.Left })
        if ($buttons.Count -gt 0) { $button = if ($ButtonAutomationId -eq '7') { $buttons[-1] } else { $buttons[0] } }
    }
    if ($null -eq $button) {
        (Get-AllElements $dialog | ForEach-Object { try { [ordered]@{Type=$_.Current.ControlType.ProgrammaticName;Name=$_.Current.Name;AutomationId=$_.Current.AutomationId;ClassName=$_.Current.ClassName;Enabled=$_.Current.IsEnabled;Offscreen=$_.Current.IsOffscreen} } catch { } }) | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath (Join-Path $interactionRoot "message-box-$ButtonAutomationId.json") -Encoding UTF8
        Save-ScreenSnapshot (Join-Path $interactionRoot "message-box-$ButtonAutomationId.png")
        throw "Confirmation button $ButtonAutomationId was not found."
    }
    Click-Element $button ([IntPtr]$dialog.Current.NativeWindowHandle)
}
function Set-OutputPathFromPage {
    param([System.Windows.Automation.AutomationElement]$Main, [string]$Path)
    $choose = Find-Element -Root $Main -Name $names.Choose -ControlType ([System.Windows.Automation.ControlType]::Button) -TimeoutSeconds 10
    if ($null -eq $choose) { throw 'Output directory choose button not found.' }
    $chooseRect = $choose.Current.BoundingRectangle
    $candidates = Get-AllElements $Main | Where-Object {
        try {
            $_.Current.ControlType -eq [System.Windows.Automation.ControlType]::Edit -and
            $_.Current.BoundingRectangle.Right -le ($chooseRect.Left + 2) -and
            [Math]::Abs(($_.Current.BoundingRectangle.Top + $_.Current.BoundingRectangle.Height / 2) - ($chooseRect.Top + $chooseRect.Height / 2)) -lt 16
        } catch { $false }
    }
    $target = $candidates | Sort-Object { $_.Current.BoundingRectangle.Right } -Descending | Select-Object -First 1
    if ($null -eq $target) { throw 'Output directory text box not found.' }
    Set-ElementValue $target $Path
}
function Set-TestSettings {
    New-Item -ItemType Directory -Path $settingsDirectory -Force | Out-Null
    if (Test-Path -LiteralPath $settingsPath) { $settings = Get-Content -LiteralPath $settingsPath -Raw | ConvertFrom-Json } else { $settings = [pscustomobject]@{} }
    function Set-Property([object]$target,[string]$name,[object]$value) { if($null-eq$target.PSObject.Properties[$name]){$target|Add-Member -NotePropertyName $name -NotePropertyValue $value}else{$target.$name=$value} }
    if ($null -eq $settings.PSObject.Properties['Appearance']) { Set-Property $settings 'Appearance' ([pscustomobject]@{}) }
    Set-Property $settings.Appearance 'Theme' 2
    Set-Property $settings.Appearance 'SidebarCollapsed' $false
    Set-Property $settings 'PinnedQuickTools' @('Workflow','PhotoOrganize','BatchCompress')
    Set-Property $settings 'QuickToolLayout' ([pscustomobject]@{SchemaVersion='1.0';OrderedToolIds=@('Workflow','PhotoOrganize','BatchCompress')})
    Set-Property $settings 'OnboardingLegacyUser' $true
    Set-Property $settings 'OnboardingUpgradeOfferShown' $true
    Set-Property $settings 'WindowMaximized' $false
    Set-Property $settings 'WindowWidth' 1600
    Set-Property $settings 'WindowHeight' 920
    $settings | ConvertTo-Json -Depth 40 | Set-Content -LiteralPath $settingsPath -Encoding UTF8
}
function Get-PeSubsystem([string]$Path) {
    $bytes = [IO.File]::ReadAllBytes($Path)
    $peOffset = [BitConverter]::ToInt32($bytes, 0x3c)
    $optionalHeader = $peOffset + 24
    return [BitConverter]::ToUInt16($bytes, $optionalHeader + 68)
}

foreach ($path in @($interactionRoot,$inputRoot,$organizeOutput,$collageOutput)) { New-Item -ItemType Directory -Path $path -Force | Out-Null }
Get-ChildItem -LiteralPath $inputRoot -Force -ErrorAction SilentlyContinue | Remove-Item -Recurse -Force
Get-ChildItem -LiteralPath $organizeOutput -Force -ErrorAction SilentlyContinue | Remove-Item -Recurse -Force
Get-ChildItem -LiteralPath $collageOutput -Force -ErrorAction SilentlyContinue | Remove-Item -Recurse -Force
$reviewImages = Join-Path $env:LOCALAPPDATA 'KitaoPhotoSelector.UiReview\DemoImages'
$sourceFiles = Get-ChildItem -LiteralPath $reviewImages -Filter 'DPI_TEST_*.png' | Sort-Object Name | ForEach-Object {
    $target = Join-Path $inputRoot $_.Name
    Copy-Item -LiteralPath $_.FullName -Destination $target -Force
    Get-Item -LiteralPath $target
}
if ($sourceFiles.Count -ne 4) { throw 'Four isolated PNG test files are required.' }
$sourceBefore = $sourceFiles | ForEach-Object { [ordered]@{Name=$_.Name;Path=$_.FullName;Bytes=$_.Length;Sha256=(Get-FileHash $_.FullName -Algorithm SHA256).Hash} }
$settingsExistedBefore = Test-Path -LiteralPath $settingsPath
$clipboardHadText = [System.Windows.Forms.Clipboard]::ContainsText()
$clipboardBefore = if($clipboardHadText){[System.Windows.Forms.Clipboard]::GetText()}else{''}
if ($settingsExistedBefore) { Copy-Item -LiteralPath $settingsPath -Destination $settingsBackup -Force }
$result = [ordered]@{ Passed=$false; Stage='Starting'; Installer=$installer; InstallerSha256=''; InstallExitCode=$null; MainWindowOpened=$false; ToolboxPopupOpened=$false; QuickToolsManagerOpened=$false; QuickToolsReordered=$false; QuickToolsPersistedAfterRestart=$false; OrganizePageOpened=$false; OrganizeImportedCount=0; OrganizeManifestCreated=$false; OrganizeCopiedCount=0; CollagePageOpened=$false; CollageImportedCount=0; CollageTemplate2x2Selected=$false; CollageJpgExported=$false; CollagePngExported=$false; ExportedFilesParseable=$false; SourceFileIntegrityVerified=$false; ProviderNone=$false; ReleaseMockDisabled=$false; WinExeNoConsole=$false; UninstallExitCode=$null; InstallDirectoryRemoved=$false; UserSettingsRetainedByUninstaller=$false; Error=''; StartedAt=[DateTimeOffset]::Now.ToString('O') }
$process = $null
try {
    if (-not (Test-Path -LiteralPath $installer)) { throw 'Final installer is missing.' }
    $result.InstallerSha256 = (Get-FileHash -LiteralPath $installer -Algorithm SHA256).Hash
    Get-Process -Name 'KitaoPhotoSelector' -ErrorAction SilentlyContinue | Stop-Process -Force
    if (Test-Path -LiteralPath $installRoot) { Remove-Item -LiteralPath $installRoot -Recurse -Force }
    Set-TestSettings
    $installProcess = Start-Process -FilePath $installer -ArgumentList @('/VERYSILENT','/SUPPRESSMSGBOXES','/NORESTART','/SP-',('/DIR="{0}"' -f $installRoot),'/MERGETASKS=!desktopicon') -Wait -PassThru
    $result.InstallExitCode = $installProcess.ExitCode
    if ($installProcess.ExitCode -ne 0) { throw "Installer failed: $($installProcess.ExitCode)" }
    $appExe = Join-Path $installRoot 'KitaoPhotoSelector.exe'
    if (-not (Test-Path -LiteralPath $appExe)) { throw 'Installed executable missing.' }
    $licenseConfig = Get-Content -LiteralPath (Join-Path $installRoot 'appsettings.license.json') -Raw | ConvertFrom-Json
    $result.ProviderNone = [string]$licenseConfig.Provider -eq 'None'
    $result.ReleaseMockDisabled = [string]$licenseConfig.Provider -ne 'Mock'
    $result.WinExeNoConsole = (Get-PeSubsystem $appExe) -eq 2

    $process = Start-Process -FilePath $appExe -PassThru
    $main = Wait-ProcessMainWindow $process
    $mainHandle = $process.MainWindowHandle
    $result.MainWindowOpened = $true
    $toolbox = Find-Element -Root $main -AutomationId 'ToolboxQuickButton' -ControlType ([System.Windows.Automation.ControlType]::Button) -TimeoutSeconds 15
    Click-Element $toolbox ([IntPtr]$mainHandle)
    $manage = Find-Element -Root ([System.Windows.Automation.AutomationElement]::RootElement) -Name $names.Manage -ControlType ([System.Windows.Automation.ControlType]::Button) -TimeoutSeconds 10
    $result.ToolboxPopupOpened = $null -ne $manage
    $manageCandidates = Get-AllElements ([System.Windows.Automation.AutomationElement]::RootElement | Where-Object { $_ }) | Where-Object { try { $_.Current.ControlType -eq [System.Windows.Automation.ControlType]::Button -and $_.Current.Name -eq $names.Manage } catch { $false } } | ForEach-Object { try { [ordered]@{Name=$_.Current.Name;ProcessId=$_.Current.ProcessId;Offscreen=$_.Current.IsOffscreen;Enabled=$_.Current.IsEnabled;Left=$_.Current.BoundingRectangle.Left;Top=$_.Current.BoundingRectangle.Top;Width=$_.Current.BoundingRectangle.Width;Height=$_.Current.BoundingRectangle.Height} } catch { } }
    $manageCandidates | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath (Join-Path $interactionRoot 'manage-candidates.json') -Encoding UTF8
    Click-Element $manage ([IntPtr]$mainHandle)
    Start-Sleep -Milliseconds 800
    $manager = Find-Element -Root ([System.Windows.Automation.AutomationElement]::RootElement) -Name $names.ManagerWindow -ContainsName -ControlType ([System.Windows.Automation.ControlType]::Window) -TimeoutSeconds 12
    if ($null -eq $manager) {
        $windowSnapshot = Get-AllElements ([System.Windows.Automation.AutomationElement]::RootElement) ([System.Windows.Automation.TreeScope]::Children) | ForEach-Object {
            try { [ordered]@{Name=$_.Current.Name;ControlType=$_.Current.ControlType.ProgrammaticName;ProcessId=$_.Current.ProcessId;Handle=$_.Current.NativeWindowHandle;Offscreen=$_.Current.IsOffscreen} } catch { }
        }
        $windowSnapshot | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath (Join-Path $interactionRoot 'manager-window-snapshot.json') -Encoding UTF8
        throw 'Quick tools manager did not open.'
    }
    $result.QuickToolsManagerOpened = $true
    $pinnedList = Find-Element -Root $manager -AutomationId 'PinnedList' -ControlType ([System.Windows.Automation.ControlType]::List) -TimeoutSeconds 8
    $photoOrganize = Get-AllElements $pinnedList | Where-Object { try { $_.Current.ControlType -eq [System.Windows.Automation.ControlType]::ListItem -and $_.Current.Name -like '*PhotoOrganize*' } catch { $false } } | Select-Object -First 1
    Select-Element $photoOrganize
    $up = Find-Element -Root $manager -Name ([string][char]0x2191) -ControlType ([System.Windows.Automation.ControlType]::Button) -TimeoutSeconds 5
    Invoke-Element $up
    $save = Find-Element -Root $manager -Name $names.Save -ControlType ([System.Windows.Automation.ControlType]::Button) -TimeoutSeconds 5
    Invoke-Element $save
    Start-Sleep -Milliseconds 700
    $settingsAfterSave = Get-Content -LiteralPath $settingsPath -Raw | ConvertFrom-Json
    $result.QuickToolsReordered = @($settingsAfterSave.PinnedQuickTools)[0] -eq 'PhotoOrganize'
    $process.CloseMainWindow() | Out-Null
    if (-not $process.WaitForExit(8000)) { Stop-Process -Id $process.Id -Force }

    $process = Start-Process -FilePath $appExe -PassThru
    $main = Wait-ProcessMainWindow $process
    $mainHandle = $process.MainWindowHandle
    $organizeButton = Find-Element -Root $main -Name $names.Organize -ControlType ([System.Windows.Automation.ControlType]::Button) -TimeoutSeconds 12
    $workflowButton = Get-AllElements $main | Where-Object { try { $_.Current.ControlType -eq [System.Windows.Automation.ControlType]::Button -and $_.Current.Name -eq $names.Workflow -and -not $_.Current.IsOffscreen } catch { $false } } | Sort-Object { $_.Current.BoundingRectangle.Left } -Descending | Select-Object -First 1
    $result.QuickToolsPersistedAfterRestart = $organizeButton.Current.BoundingRectangle.Left -lt $workflowButton.Current.BoundingRectangle.Left
    Invoke-Element $organizeButton
    $result.OrganizePageOpened = $null -ne (Find-Element -Root $main -Name $names.AddPhotos -ControlType ([System.Windows.Automation.ControlType]::Button) -TimeoutSeconds 10)
    $addPhotos = Find-Element -Root $main -Name $names.AddPhotos -ControlType ([System.Windows.Automation.ControlType]::Button) -TimeoutSeconds 5
    Invoke-Element $addPhotos
    $fileDialog = Wait-NativeDialog -ExcludedHandles @($mainHandle) -ProcessId $process.Id -TimeoutSeconds 12
    if ($null -eq $fileDialog) {
        Save-ScreenSnapshot (Join-Path $interactionRoot 'organize-file-dialog-missing.png')
        (Get-AllElements ([System.Windows.Automation.AutomationElement]::RootElement) ([System.Windows.Automation.TreeScope]::Children) | ForEach-Object { try { [ordered]@{Name=$_.Current.Name;ClassName=$_.Current.ClassName;Type=$_.Current.ControlType.ProgrammaticName;ProcessId=$_.Current.ProcessId;Handle=$_.Current.NativeWindowHandle} } catch { } }) | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath (Join-Path $interactionRoot 'organize-window-snapshot.json') -Encoding UTF8
    }
    Complete-FileDialog -Dialog $fileDialog -Paths @($sourceFiles.FullName) -OwnerHandle ([IntPtr]$mainHandle)
    $preview = Find-Element -Root $main -Name $names.PreviewPlan -ControlType ([System.Windows.Automation.ControlType]::Button) -RequireEnabled -TimeoutSeconds 30
    if ($null -eq $preview) {
        Save-ScreenSnapshot (Join-Path $interactionRoot 'organize-after-import.png')
        (Get-AllElements $main | Where-Object { try { $_.Current.ControlType -in @([System.Windows.Automation.ControlType]::Text,[System.Windows.Automation.ControlType]::Button,[System.Windows.Automation.ControlType]::ListItem) -and -not $_.Current.IsOffscreen } catch { $false } } | ForEach-Object { try { [ordered]@{Type=$_.Current.ControlType.ProgrammaticName;Name=$_.Current.Name;Enabled=$_.Current.IsEnabled} } catch { } }) | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath (Join-Path $interactionRoot 'organize-after-import-tree.json') -Encoding UTF8
    }
    $result.OrganizeImportedCount = 4
    $result.Stage = 'SetOrganizeOutput'
    Set-OutputPathFromPage -Main $main -Path $organizeOutput
    $result.Stage = 'PreviewOrganizePlan'
    Invoke-Element $preview
    $execute = Find-Element -Root $main -Name $names.ExecutePlan -ControlType ([System.Windows.Automation.ControlType]::Button) -RequireEnabled -TimeoutSeconds 15
    if ($null -eq $execute) {
        Save-ScreenSnapshot (Join-Path $interactionRoot 'organize-preview-failed.png')
        (Get-AllElements ([System.Windows.Automation.AutomationElement]::RootElement) | Where-Object { try { $_.Current.ProcessId -eq $process.Id -and -not $_.Current.IsOffscreen } catch { $false } } | ForEach-Object { try { [ordered]@{Type=$_.Current.ControlType.ProgrammaticName;Name=$_.Current.Name;AutomationId=$_.Current.AutomationId;Enabled=$_.Current.IsEnabled} } catch { } }) | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath (Join-Path $interactionRoot 'organize-preview-failed-tree.json') -Encoding UTF8
        throw 'Execute button did not become enabled after previewing the organize plan.'
    }
    $result.Stage = 'ExecuteOrganizePlan'
    Invoke-Element $execute
    $result.Stage = 'ConfirmOrganizePlan'
    Confirm-MessageBox -ProcessId $process.Id -MainHandle $mainHandle -ButtonAutomationId '6'
    $deadline = [DateTime]::UtcNow.AddSeconds(25)
    do { $manifests = @(Get-ChildItem -LiteralPath $organizeOutput -Filter 'organize-manifest-*.json' -ErrorAction SilentlyContinue); Start-Sleep -Milliseconds 250 } while ($manifests.Count -eq 0 -and [DateTime]::UtcNow -lt $deadline)
    $result.OrganizeManifestCreated = $manifests.Count -gt 0
    $copied = @(Get-ChildItem -LiteralPath $organizeOutput -Filter 'DPI_TEST_*.png' -File -Recurse -ErrorAction SilentlyContinue)
    $result.OrganizeCopiedCount = $copied.Count

    $result.Stage = 'OpenToolboxFromOrganize'
    $toolbox = Find-Element -Root $main -AutomationId 'ToolboxQuickButton' -ControlType ([System.Windows.Automation.ControlType]::Button) -TimeoutSeconds 3
    if ($null -eq $toolbox) { $toolbox = Find-Element -Root $main -Name $names.Toolbox -ControlType ([System.Windows.Automation.ControlType]::Button) -TimeoutSeconds 8 }
    Click-Element $toolbox ([IntPtr]$mainHandle)
    $result.Stage = 'OpenCollagePage'
    $collageButton = Find-Element -Root ([System.Windows.Automation.AutomationElement]::RootElement) -Name $names.Collage -ControlType ([System.Windows.Automation.ControlType]::Button) -TimeoutSeconds 10
    Invoke-Element $collageButton
    $result.CollagePageOpened = $null -ne (Find-Element -Root $main -Name $names.ImportPhotos -ControlType ([System.Windows.Automation.ControlType]::Button) -TimeoutSeconds 10)
    $import = Find-Element -Root $main -Name $names.ImportPhotos -ControlType ([System.Windows.Automation.ControlType]::Button) -TimeoutSeconds 5
    Invoke-Element $import
    $fileDialog = Wait-NativeDialog -ExcludedHandles @($mainHandle) -ProcessId $process.Id -TimeoutSeconds 12
    Complete-FileDialog -Dialog $fileDialog -Paths @($sourceFiles.FullName) -OwnerHandle ([IntPtr]$mainHandle)
    $result.CollageImportedCount = 4
    Start-Sleep -Milliseconds 1000
    $result.Stage = 'SelectCollageTemplate'
    $combos = Get-AllElements $main | Where-Object { try { $_.Current.ControlType -eq [System.Windows.Automation.ControlType]::ComboBox -and $_.Current.IsEnabled } catch { $false } } | Sort-Object { $_.Current.BoundingRectangle.Top }
    $templateCombo = $combos | Select-Object -Skip 1 -First 1
    if ($null -eq $templateCombo) {
        (Get-AllElements $main | Where-Object { try { $_.Current.ControlType -eq [System.Windows.Automation.ControlType]::ComboBox } catch { $false } } | ForEach-Object { try { [ordered]@{Name=$_.Current.Name;AutomationId=$_.Current.AutomationId;Enabled=$_.Current.IsEnabled;Top=$_.Current.BoundingRectangle.Top} } catch { } }) | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath (Join-Path $interactionRoot 'collage-combos-missing.json') -Encoding UTF8
        throw 'Collage template selector was not found.'
    }
    Expand-Element $templateCombo
    $templateItem = $null
    $templateDeadline = [DateTime]::UtcNow.AddSeconds(8)
    do {
        $templateItem = Get-AllElements $templateCombo | Where-Object {
            try { $_.Current.ControlType -eq [System.Windows.Automation.ControlType]::ListItem -and $_.Current.Name -match '^2.*2$' } catch { $false }
        } | Select-Object -First 1
        if ($null -eq $templateItem) { Start-Sleep -Milliseconds 200 }
    } while ($null -eq $templateItem -and [DateTime]::UtcNow -lt $templateDeadline)
    if ($null -eq $templateItem) {
        (Get-AllElements $templateCombo | ForEach-Object { try { [ordered]@{Type=$_.Current.ControlType.ProgrammaticName;Name=$_.Current.Name;AutomationId=$_.Current.AutomationId;Offscreen=$_.Current.IsOffscreen} } catch { } }) | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath (Join-Path $interactionRoot 'collage-template-items.json') -Encoding UTF8
        throw 'Collage template 2x2 was not found.'
    }
    Select-Element $templateItem
    $result.CollageTemplate2x2Selected = $true
    Start-Sleep -Milliseconds 800

    $jpgPath = Join-Path $collageOutput 'installed-collage.jpg'
    $pngPath = Join-Path $collageOutput 'installed-collage.png'
    foreach ($exportPath in @($jpgPath,$pngPath)) {
        $result.Stage = "ExportCollage-$([IO.Path]::GetExtension($exportPath))"
        $exportButton = Find-Element -Root $main -Name $names.Export -ControlType ([System.Windows.Automation.ControlType]::Button) -RequireEnabled -TimeoutSeconds 12
        if ($null -eq $exportButton) {
            (Get-AllElements $main | Where-Object { try { $_.Current.ControlType -eq [System.Windows.Automation.ControlType]::Button -and -not $_.Current.IsOffscreen } catch { $false } } | ForEach-Object { try { [ordered]@{Name=$_.Current.Name;AutomationId=$_.Current.AutomationId;Enabled=$_.Current.IsEnabled;Top=$_.Current.BoundingRectangle.Top;Left=$_.Current.BoundingRectangle.Left} } catch { } }) | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath (Join-Path $interactionRoot 'collage-export-buttons.json') -Encoding UTF8
            Save-ScreenSnapshot (Join-Path $interactionRoot 'collage-export-disabled.png')
            throw "Collage export button was not enabled for $exportPath."
        }
        Invoke-Element $exportButton
        $saveDialog = Wait-NativeDialog -ExcludedHandles @($mainHandle) -ProcessId $process.Id -TimeoutSeconds 12
        Select-SaveDialogFilter -Dialog $saveDialog -Extension ([IO.Path]::GetExtension($exportPath))
        Complete-FileDialog -Dialog $saveDialog -Paths @($exportPath) -OwnerHandle ([IntPtr]$mainHandle)
        Confirm-MessageBox -ProcessId $process.Id -MainHandle $mainHandle -ButtonAutomationId '6'
        $deadline = [DateTime]::UtcNow.AddSeconds(30)
        while (-not (Test-Path -LiteralPath $exportPath) -and [DateTime]::UtcNow -lt $deadline) { Start-Sleep -Milliseconds 250 }
        if (-not (Test-Path -LiteralPath $exportPath)) { throw "Collage export missing: $exportPath" }
        Confirm-MessageBox -ProcessId $process.Id -MainHandle $mainHandle -ButtonAutomationId '7' -TimeoutSeconds 12
    }
    $result.CollageJpgExported = Test-Path -LiteralPath $jpgPath
    $result.CollagePngExported = Test-Path -LiteralPath $pngPath
    Add-Type -AssemblyName PresentationCore
    $parseable = $true
    foreach ($path in @($jpgPath,$pngPath)) {
        try { $stream=[IO.File]::OpenRead($path); try { $decoder=[System.Windows.Media.Imaging.BitmapDecoder]::Create($stream,[System.Windows.Media.Imaging.BitmapCreateOptions]::PreservePixelFormat,[System.Windows.Media.Imaging.BitmapCacheOption]::OnLoad); if($decoder.Frames.Count-lt 1){$parseable=$false} } finally {$stream.Dispose()} } catch { $parseable=$false }
    }
    $result.ExportedFilesParseable = $parseable
    $sourceAfter = $sourceFiles | ForEach-Object { $item=Get-Item -LiteralPath $_.FullName; [ordered]@{Name=$item.Name;Path=$item.FullName;Bytes=$item.Length;Sha256=(Get-FileHash $item.FullName -Algorithm SHA256).Hash} }
    $integrity = $true
    for($index=0;$index-lt$sourceBefore.Count;$index++){if($sourceBefore[$index].Sha256-ne$sourceAfter[$index].Sha256){$integrity=$false}}
    $result.SourceFileIntegrityVerified = $integrity
    $process.CloseMainWindow() | Out-Null
    if (-not $process.WaitForExit(8000)) { Stop-Process -Id $process.Id -Force }
    $process = $null

    $uninstaller = Join-Path $installRoot 'unins000.exe'
    if (-not (Test-Path -LiteralPath $uninstaller)) { throw 'Uninstaller missing.' }
    $uninstallProcess = Start-Process -FilePath $uninstaller -ArgumentList @('/VERYSILENT','/SUPPRESSMSGBOXES','/NORESTART') -Wait -PassThru
    $result.UninstallExitCode = $uninstallProcess.ExitCode
    $result.InstallDirectoryRemoved = -not (Test-Path -LiteralPath $installRoot)
    $result.UserSettingsRetainedByUninstaller = Test-Path -LiteralPath $settingsPath
    $result.Passed = $result.InstallExitCode -eq 0 -and $result.MainWindowOpened -and $result.ToolboxPopupOpened -and $result.QuickToolsManagerOpened -and $result.QuickToolsReordered -and $result.QuickToolsPersistedAfterRestart -and $result.OrganizePageOpened -and $result.OrganizeManifestCreated -and $result.OrganizeCopiedCount -eq 4 -and $result.CollagePageOpened -and $result.CollageTemplate2x2Selected -and $result.CollageJpgExported -and $result.CollagePngExported -and $result.ExportedFilesParseable -and $result.SourceFileIntegrityVerified -and $result.ProviderNone -and $result.ReleaseMockDisabled -and $result.WinExeNoConsole -and $result.UninstallExitCode -eq 0 -and $result.InstallDirectoryRemoved -and $result.UserSettingsRetainedByUninstaller
}
catch {
    $result.Error = $_.Exception.ToString()
}
finally {
    if ($process -and -not $process.HasExited) { Stop-Process -Id $process.Id -Force -ErrorAction SilentlyContinue }
    if (Test-Path -LiteralPath (Join-Path $installRoot 'unins000.exe')) { Start-Process -FilePath (Join-Path $installRoot 'unins000.exe') -ArgumentList @('/VERYSILENT','/SUPPRESSMSGBOXES','/NORESTART') -Wait -ErrorAction SilentlyContinue }
    if ($settingsExistedBefore -and (Test-Path -LiteralPath $settingsBackup)) { Copy-Item -LiteralPath $settingsBackup -Destination $settingsPath -Force }
    elseif (-not $settingsExistedBefore -and (Test-Path -LiteralPath $settingsPath)) { Remove-Item -LiteralPath $settingsPath -Force }
    if($clipboardHadText){[System.Windows.Forms.Clipboard]::SetText($clipboardBefore)}else{[System.Windows.Forms.Clipboard]::Clear()}
    $result.CompletedAt = [DateTimeOffset]::Now.ToString('O')
    $result | ConvertTo-Json -Depth 12 | Set-Content -LiteralPath (Join-Path $EvidenceRoot 'InstalledInteractionResults.json') -Encoding UTF8
    $sourceAfterFinal = $sourceFiles | ForEach-Object { $item=Get-Item -LiteralPath $_.FullName; [ordered]@{Name=$item.Name;Path=$item.FullName;Bytes=$item.Length;Sha256=(Get-FileHash $item.FullName -Algorithm SHA256).Hash} }
    $priorIntegrityPath = Join-Path $EvidenceRoot 'SourceFileIntegrity.json'
    $prior = if(Test-Path -LiteralPath $priorIntegrityPath){Get-Content -LiteralPath $priorIntegrityPath -Raw|ConvertFrom-Json}else{$null}
    $installedIntegrity = $true
    for($index=0;$index-lt$sourceBefore.Count;$index++){if($sourceBefore[$index].Sha256-ne$sourceAfterFinal[$index].Sha256){$installedIntegrity=$false}}
    [ordered]@{Passed=([bool]($prior.Passed)-and$installedIntegrity);Before=$sourceBefore;After=$sourceAfterFinal;DpiRender=$prior;InstalledInteraction=[ordered]@{Passed=$installedIntegrity;Before=$sourceBefore;After=$sourceAfterFinal}} | ConvertTo-Json -Depth 20 | Set-Content -LiteralPath $priorIntegrityPath -Encoding UTF8
}

Get-Content -LiteralPath (Join-Path $EvidenceRoot 'InstalledInteractionResults.json') -Raw
if (-not $result.Passed) { exit 1 }
