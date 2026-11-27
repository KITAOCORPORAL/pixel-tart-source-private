[CmdletBinding()]
param(
    [Parameter(Mandatory)][int]$DesktopPid,
    [Parameter(Mandatory)][string]$DesktopExe,
    [Parameter(Mandatory)][long]$StartedUtcTicks,
    [Parameter(Mandatory)][guid]$ProjectId,
    [Parameter(Mandatory)][string]$PlanTitle,
    [Parameter(Mandatory)][string]$OutputDirectory,
    [string]$DesktopTitle,
    [switch]$ResumeOnly,
    [string]$EditNativeBeforePublish,
    [string]$EditLocalDraft,
    [string]$ExpectedWorkbenchText,
    [string]$ExpectedWorkbenchSection,
    [string]$ExpectedWorkbenchComment,
    [switch]$ExpectSaveFailure,
    [ValidateRange(5,180)][int]$TimeoutSeconds = 45
)
$ErrorActionPreference = 'Stop'
if ($ProjectId -eq [guid]::Empty -or ($EditLocalDraft -and !$ResumeOnly) -or ($EditNativeBeforePublish -and $ResumeOnly) -or ($ExpectSaveFailure -and $ResumeOnly)) { throw 'Invalid acceptance mode.' }

Add-Type -AssemblyName UIAutomationClient,UIAutomationTypes,WindowsBase,PresentationCore
if (-not ('PlanningShareAcceptanceWindows' -as [type])) { Add-Type -TypeDefinition @'
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

public static class PlanningShareAcceptanceWindows
{
    private delegate bool Callback(IntPtr handle, IntPtr value);
    [DllImport("user32.dll")] private static extern bool EnumWindows(Callback callback, IntPtr value);
    [DllImport("user32.dll")] private static extern bool IsWindowVisible(IntPtr handle);
    [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(IntPtr handle, out uint pid);
    [DllImport("user32.dll")] private static extern bool GetWindowRect(IntPtr handle, out Rect rect);
    [DllImport("user32.dll")] private static extern bool PrintWindow(IntPtr handle, IntPtr target, uint flags);
    [DllImport("user32.dll")] private static extern IntPtr GetDC(IntPtr handle);
    [DllImport("user32.dll")] private static extern int ReleaseDC(IntPtr handle, IntPtr target);
    [DllImport("gdi32.dll")] private static extern IntPtr CreateCompatibleDC(IntPtr target);
    [DllImport("gdi32.dll")] private static extern IntPtr CreateCompatibleBitmap(IntPtr target, int width, int height);
    [DllImport("gdi32.dll")] private static extern IntPtr SelectObject(IntPtr target, IntPtr value);
    [DllImport("gdi32.dll")] private static extern bool DeleteDC(IntPtr target);
    [DllImport("gdi32.dll")] public static extern bool DeleteObject(IntPtr value);
    [StructLayout(LayoutKind.Sequential)] private struct Rect { public int Left, Top, Right, Bottom; }

    public static IntPtr[] OwnedVisible(int pid)
    {
        var windows = new List<IntPtr>();
        EnumWindows((handle, _) => { uint owner; GetWindowThreadProcessId(handle, out owner);
            if (owner == pid && IsWindowVisible(handle)) windows.Add(handle); return true; }, IntPtr.Zero);
        return windows.ToArray();
    }

    public static IntPtr Capture(int pid, IntPtr handle)
    {
        uint owner;
        GetWindowThreadProcessId(handle, out owner);
        if (owner != pid || !IsWindowVisible(handle)) throw new InvalidOperationException("Screenshot window ownership changed.");
        Rect rect;
        if (!GetWindowRect(handle, out rect)) throw new InvalidOperationException("Cannot measure candidate window.");
        int w = rect.Right - rect.Left, h = rect.Bottom - rect.Top;
        if (w < 100 || h < 100 || w > 6000 || h > 6000) throw new InvalidOperationException("Candidate window has invalid dimensions.");
        IntPtr screen = GetDC(IntPtr.Zero), memory = IntPtr.Zero, bitmap = IntPtr.Zero;
        try
        {
            if (screen == IntPtr.Zero || (memory = CreateCompatibleDC(screen)) == IntPtr.Zero ||
                (bitmap = CreateCompatibleBitmap(screen, w, h)) == IntPtr.Zero)
                throw new InvalidOperationException("Candidate window capture allocation failed.");
            IntPtr previous = SelectObject(memory, bitmap);
            try
            {
                if (!PrintWindow(handle, memory, 2)) throw new InvalidOperationException("Candidate window capture failed.");
            }
            finally { SelectObject(memory, previous); }
            IntPtr result = bitmap; bitmap = IntPtr.Zero; return result;
        }
        finally
        {
            if (bitmap != IntPtr.Zero) DeleteObject(bitmap);
            if (memory != IntPtr.Zero) DeleteDC(memory);
            if (screen != IntPtr.Zero) ReleaseDC(IntPtr.Zero, screen);
        }
    }
}
'@
}

$expectedExe = (Get-Item -LiteralPath $DesktopExe).FullName
function Confirm-Instance {
    $p = Get-Process -Id $DesktopPid -ErrorAction Stop
    if ($p.Path -ne $expectedExe -or $p.StartTime.ToUniversalTime().Ticks -ne $StartedUtcTicks) { throw 'Desktop identity mismatch.' }
}
Confirm-Instance
$output = [IO.Path]::GetFullPath($OutputDirectory)
[void](New-Item -ItemType Directory -Path $output -Force)
$stamp = if ($ExpectSaveFailure) { 'save-failure' } elseif ($ResumeOnly) { 'resume' } else { 'publish' }
$managedDll = [IO.Path]::ChangeExtension($expectedExe, '.dll')
$checks = [ordered]@{ mode = $stamp; pid = $DesktopPid; startUtcTicks = $StartedUtcTicks; projectId = $ProjectId; desktopSha256 = (Get-FileHash -LiteralPath $expectedExe).Hash; outcome = 'FAIL' }
if (Test-Path -LiteralPath $managedDll) { $checks['desktopManagedSha256'] = (Get-FileHash -LiteralPath $managedDll).Hash }
$stage = 'candidate-window'
$main = $null

function Get-Window([string]$title, [string]$automationId) {
    Confirm-Instance
    foreach ($handle in [PlanningShareAcceptanceWindows]::OwnedVisible($DesktopPid)) {
        try {
            $element = [System.Windows.Automation.AutomationElement]::FromHandle($handle)
            if ($element.Current.ProcessId -eq $DesktopPid -and (($title -and $element.Current.Name -eq $title) -or
                ($automationId -and $element.Current.AutomationId -eq $automationId))) { return $element }
        } catch [System.Windows.Automation.ElementNotAvailableException] {}
    }
}

function Find-Uia($root, [string]$id, [string]$name, [string]$kind) {
    if (!$root) { return }
    $ae = [System.Windows.Automation.AutomationElement]
    $conditions = [System.Collections.Generic.List[System.Windows.Automation.Condition]]::new()
    if ($id) { $conditions.Add([System.Windows.Automation.PropertyCondition]::new($ae::AutomationIdProperty, $id)) }
    if ($name) { $conditions.Add([System.Windows.Automation.PropertyCondition]::new($ae::NameProperty, $name)) }
    if ($kind) {
        $type = [System.Windows.Automation.ControlType].GetField($kind).GetValue($null)
        $conditions.Add([System.Windows.Automation.PropertyCondition]::new($ae::ControlTypeProperty, $type))
    }
    $condition = if ($conditions.Count -eq 1) { $conditions[0] } else { [System.Windows.Automation.AndCondition]::new($conditions.ToArray()) }
    $element = $root.FindFirst([System.Windows.Automation.TreeScope]::Descendants, $condition)
    if ($null -ne $element) { return $element }
}

function Wait-For([string]$label, [scriptblock]$probe) {
    $limit = [DateTime]::UtcNow.AddSeconds($TimeoutSeconds)
    do {
        Confirm-Instance
        try { $hit = & $probe; if ($hit) { return $hit } }
        catch [System.Windows.Automation.ElementNotAvailableException] {}
        Start-Sleep -Milliseconds 200
    } while ([DateTime]::UtcNow -lt $limit)
    throw "Candidate UI did not reach stage: $label."
}

function Invoke-Uia($element) {
    if (!$element -or !$element.Current.IsEnabled) { throw 'Candidate action unavailable.' }
    $element.GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern).Invoke()
}

function Capture-Uia($window, [string]$label) {
    try {
        Confirm-Instance
        $bitmap = [PlanningShareAcceptanceWindows]::Capture($DesktopPid, [IntPtr]$window.Current.NativeWindowHandle)
        try {
            $source = [System.Windows.Interop.Imaging]::CreateBitmapSourceFromHBitmap($bitmap, [IntPtr]::Zero,
                [System.Windows.Int32Rect]::Empty, [System.Windows.Media.Imaging.BitmapSizeOptions]::FromEmptyOptions())
            $encoder = [System.Windows.Media.Imaging.PngBitmapEncoder]::new()
            $encoder.Frames.Add([System.Windows.Media.Imaging.BitmapFrame]::Create($source))
            $stream = [IO.File]::Open((Join-Path $output "$stamp-$label.png"), [IO.FileMode]::Create)
            try { $encoder.Save($stream) } finally { $stream.Dispose() }
        } finally { [void][PlanningShareAcceptanceWindows]::DeleteObject($bitmap) }
        $checks["${label}Capture"] = $true
    } catch { $checks["${label}Capture"] = $false }
}

function Find-Text($root, [string]$fragment) {
    if (!$root) { return }
    $ae = [System.Windows.Automation.AutomationElement]
    $needle = [System.Windows.Automation.OrCondition]::new(
        [System.Windows.Automation.PropertyCondition]::new($ae::ControlTypeProperty, [System.Windows.Automation.ControlType]::Text),
        [System.Windows.Automation.PropertyCondition]::new($ae::ControlTypeProperty, [System.Windows.Automation.ControlType]::Edit))
    foreach ($entry in $root.FindAll([System.Windows.Automation.TreeScope]::Descendants, $needle)) {
        if ($entry.Current.Name.Contains($fragment)) { return $true }
        $pattern = $null
        if ($entry.TryGetCurrentPattern([System.Windows.Automation.ValuePattern]::Pattern, [ref]$pattern) -and $pattern.Current.Value.Contains($fragment)) { return $true }
    }
}

function Confirm-HeaderMarker($element, [string]$expected) {
    if (!$element) { return $false }
    return $element.Current.Name.Contains($expected)
}

$short = '#' + $ProjectId.ToString('N').Substring(0,8)
try {
    $main = Wait-For $stage {
        if ($DesktopTitle) { return (Get-Window $DesktopTitle) }
        $native = Get-Window 'Kitao Studio 光镜'
        if ($native) { return $native }
        Get-Window '光镜 · 完整范围诊断预览 · 未完成验证 / 不能发行'
    }
    $stage = 'normal-planning-navigation'
    Invoke-Uia (Wait-For $stage { Find-Uia $main 'PrimaryNavigationPlanning' '' 'Button' })
    $workspace = Wait-For 'planning-workspace' { Find-Uia $main 'PlanningWorkspace' '' '' }
    $stage = 'open-exact-plan'
    $action = Find-Uia $workspace 'PlanningTeamShareAction' '' 'Button'
    $selectedExactPlan = $false
    if (!$action -or $action.Current.IsOffscreen -or !(([string]$action.Current.HelpText).Contains($short))) {
        $direct = Find-Uia $workspace '' "$PlanTitle · $short" 'Button'
        if (!$direct -and !(Find-Uia $workspace '' '策划案列表' 'List')) {
            $picker = Find-Uia $workspace '' '切换策划案' 'Button'
            if ($picker -and $picker.Current.IsEnabled) { Invoke-Uia $picker }
        }
        $selector = Wait-For 'exact-plan-card' {
            $direct = Find-Uia $workspace '' "$PlanTitle · $short" 'Button'
            if ($direct) { return $direct }
            $list = Find-Uia $workspace '' '策划案列表' 'List'
            if ($list) {
                $condition = [System.Windows.Automation.PropertyCondition]::new(
                    [System.Windows.Automation.AutomationElement]::ControlTypeProperty, [System.Windows.Automation.ControlType]::ListItem)
                foreach ($item in $list.FindAll([System.Windows.Automation.TreeScope]::Children, $condition)) {
                    if ((Find-Uia $item '' $short 'Text') -and (Find-Uia $item '' $PlanTitle 'Text')) { return $item }
                }
            }
        }
        if ($selector.Current.ControlType -eq [System.Windows.Automation.ControlType]::ListItem) {
            $selector.GetCurrentPattern([System.Windows.Automation.SelectionItemPattern]::Pattern).Select()
            $close = Find-Uia $main '' '关闭面板并返回' 'Button'
            if ($close) { Invoke-Uia $close }
        } else { Invoke-Uia $selector }
        $selectedExactPlan = $true
    }
    $expectedAction = if ($ResumeOnly) { '继续团队协作' } else { '共享到团队' }
    $stage = 'contextual-header-action'
    $action = Wait-For $stage {
        $button = Find-Uia $workspace 'PlanningTeamShareAction' $expectedAction 'Button'
        if (!$button -or $button.Current.IsOffscreen -or !$button.Current.IsEnabled) { return }
        $context = [string]$button.Current.HelpText
        if (($context.Contains($short) -or ($selectedExactPlan -and !$context.StartsWith('当前策划案 #'))) -and (Find-Uia $workspace '' $PlanTitle 'Text')) { return $button }
    }
    $checks['contextualAction'] = $expectedAction
    $localInput = if ($EditLocalDraft) { $EditLocalDraft } else { $EditNativeBeforePublish }
    if ($localInput) {
        $stage = 'native-local-draft-edit'
        $editor = Find-Uia $workspace 'PlanningEditorDocumentSubtitle' '' 'Edit'
        if (!$editor) {
            $simple = Find-Uia $workspace '' '切换简单版' 'Button'
            if ($simple) { Invoke-Uia $simple }
            $textPage = Find-Uia $workspace '' '文字' 'Button'
            if (!$textPage) {
                $outline = Find-Uia $workspace '' '打开分区导航' 'Button'
                if ($outline) {
                    Invoke-Uia $outline
                    $textPage = Wait-For 'native-text-section' { Find-Uia $workspace '' '文字' 'Button' }
                }
            }
            if ($textPage) { Invoke-Uia $textPage }
            $edit = Wait-For 'local-edit-action' { Find-Uia $workspace '' '编辑' 'Button' }
            Invoke-Uia $edit
            $editor = Wait-For 'native-subtitle-editor' { Find-Uia $workspace 'PlanningEditorDocumentSubtitle' '' 'Edit' }
        }
        $value = $editor.GetCurrentPattern([System.Windows.Automation.ValuePattern]::Pattern)
        $value.SetValue($localInput)
        if ($value.Current.Value -ne $localInput) { throw 'Local editor did not retain the synthetic input.' }
        $checks['nativeSubtitleEdited'] = $true
    }
    if ($EditLocalDraft) {
        $stage = 'visible-local-edits-warning'
        Wait-For $stage {
            $status = Find-Uia $workspace 'PlanningTeamShareStatus' '' 'Text'
            if ((Confirm-HeaderMarker $status '本地有后续修改') -or (Confirm-HeaderMarker $status '本地草稿有未同步修改')) { return $true }
        } | Out-Null
        $checks['localEditsWarning'] = $true
    }

    Capture-Uia $main 'desktop-header'
    $stage = 'native-header-invoke'
    $hadOwner = $null -ne (Get-Window '光镜 · 团队策划')
    if ($hadOwner) { throw 'A team owner window already exists; restart the exact candidate first.' }
    Invoke-Uia (Wait-For $stage { Find-Uia $workspace 'PlanningTeamShareAction' $expectedAction 'Button' })

    if ($ExpectSaveFailure) {
        $stage = 'explicit-save-failure-retains-plan'
        Wait-For $stage {
            $status = Find-Uia $workspace 'PlanningTeamShareStatus' '' 'Text'
            if ($status -and $status.Current.Name.Contains('草稿已保留') -and (Find-Uia $workspace '' $PlanTitle 'Text')) { return $true }
        } | Out-Null
        if (Get-Window '光镜 · 团队策划') { throw 'Team workspace opened despite save failure.' }
        $checks['draftPreservedMessage'] = $true
        $checks['workbenchOpened'] = $false
        if ($localInput) {
            $currentDraft = Find-Uia $workspace 'PlanningEditorDocumentSubtitle' '' 'Edit'
            if (!$currentDraft -or $currentDraft.GetCurrentPattern([System.Windows.Automation.ValuePattern]::Pattern).Current.Value -ne $localInput) {
                throw 'The current editor did not retain the synthetic draft after save failure.'
            }
            $checks['draftStillInActiveEditor'] = $true
        }
        Capture-Uia $main 'save-error'
    } else {
        $stage = 'preflight-or-owner-window'
        $outcome = Wait-For $stage {
            $owner = Get-Window '光镜 · 团队策划'
            if ($owner) { return @{ Owner = $owner } }
            $warning = Get-Window '共享到团队' 'PlanningTeamSharePreflight'
            if ($warning) { return @{ Warning = $warning } }
            $blocked = Get-Window '暂时不能共享' 'PlanningTeamShareBlocked'
            if ($blocked -or (Find-Text (Get-Window '光镜消息对话框') '暂时不能共享')) { throw 'Preflight reported blocking content.' }
        }
        $checks['warningConfirmation'] = [bool]$outcome.Warning
        if ($outcome.Warning) {
            if ($ResumeOnly) { throw 'Bound collaboration asked to publish again.' }
            $stage = 'confirm-specific-plan-warnings'
            if (!(Find-Text $outcome.Warning $PlanTitle)) { throw 'Preflight did not identify selected plan.' }
            Capture-Uia $outcome.Warning 'preflight'
            Invoke-Uia (Wait-For $stage { Find-Uia $outcome.Warning 'PlanningTeamSharePublish' '' 'Button' })
        }
        $stage = 'authenticated-team-workbench'
        $owner = if ($outcome.Owner) { $outcome.Owner } else { Wait-For 'team-owner-window' { Get-Window '光镜 · 团队策划' } }
        Wait-For $stage {
            if ((Find-Uia $owner '' '项目成员' 'Button') -and (Find-Text $owner $PlanTitle)) { return $true }
        } | Out-Null
        $checks['workbenchOpened'] = $true
        $checks['publishedBindingVisible'] = [bool](Wait-For 'bound-desktop-action' { Find-Uia $workspace 'PlanningTeamShareAction' '继续团队协作' 'Button' })

        if ($ExpectedWorkbenchSection) {
            $stage = 'open-updated-web-section'
            Invoke-Uia (Wait-For $stage {
                $result = Find-Uia $owner '' $ExpectedWorkbenchSection 'Button'
                if ($result) { return $result }
                Find-Uia $owner '' "• $ExpectedWorkbenchSection" 'Button'
            })
        }
        if ($ExpectedWorkbenchText) {
            $stage = 'latest-online-content-visible'
            Wait-For $stage { Find-Text $owner $ExpectedWorkbenchText } | Out-Null
            $checks['latestOnlineContentVisible'] = $true
        }
        if ($EditLocalDraft -and $ExpectedWorkbenchSection) {
            $stage = 'local-draft-not-in-online-overview'
            $overview = Wait-For $stage {
                $link = Find-Uia $owner '' '◈ 项目概览' 'Button'
                if ($link) { return $link }
                Find-Uia $owner '' '项目概览' 'Button'
            }
            Invoke-Uia $overview
            Wait-For $stage { Find-Text $owner '项目拍摄信息' } | Out-Null
            if (Find-Text $owner $EditLocalDraft) { throw 'Local draft appeared in the team workbench.' }
            $checks['localDraftAbsentOnline'] = $true
            Invoke-Uia (Wait-For $stage {
                $link = Find-Uia $owner '' $ExpectedWorkbenchSection 'Button'
                if ($link) { return $link }
                Find-Uia $owner '' "• $ExpectedWorkbenchSection" 'Button'
            })
        } elseif ($EditLocalDraft) {
            if (Find-Text $owner $EditLocalDraft) { throw 'Local draft appeared in the team workbench.' }
            $checks['localDraftAbsentOnline'] = $true
        }
        # UIA can observe the new DOM a frame before WebView2 paints it.
        if ($ExpectedWorkbenchSection) { Start-Sleep -Milliseconds 400 }
        Capture-Uia $owner 'owner-workbench'
        if ($ExpectedWorkbenchComment) {
            $stage = 'saved-member-comment-visible'
            Invoke-Uia (Wait-For $stage { Find-Uia $owner '' '讨论' 'Button' })
            Wait-For $stage { Find-Text $owner $ExpectedWorkbenchComment } | Out-Null
            $checks['memberCommentVisible'] = $true
            Start-Sleep -Milliseconds 400
            Capture-Uia $owner 'owner-discussion'
        }
    }
    $checks['outcome'] = 'PASS'
} catch {
    $checks['failedStage'] = $stage
    $checks['failureKind'] = $_.Exception.GetType().Name
    if ($main) { Capture-Uia $main 'failure' }
    throw "Desktop acceptance failed at $stage. See the private evidence directory."
} finally {
    $checks | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath (Join-Path $output "$stamp-desktop.json") -Encoding UTF8
}
