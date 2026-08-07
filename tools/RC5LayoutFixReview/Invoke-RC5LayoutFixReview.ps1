param(
    [string]$OutputRoot = '',
    [switch]$SkipBuild
)

$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
$workspaceRoot = Split-Path $repoRoot -Parent
$dotnet = Join-Path $workspaceRoot '.dotnet\dotnet.exe'
$project = Join-Path $repoRoot 'src\RAWSelectionAssistant\RAWSelectionAssistant.csproj'
$executable = Join-Path $repoRoot 'src\RAWSelectionAssistant\bin\x64\Release\net10.0-windows10.0.19041.0\win-x64\KitaoPhotoSelector.UiReview.exe'
$reviewRoot = Join-Path $env:LOCALAPPDATA 'KitaoPhotoSelector.UiReview'
$statePath = Join-Path $reviewRoot 'ui-review-state.json'
if ([string]::IsNullOrWhiteSpace($OutputRoot)) { $OutputRoot = Join-Path $repoRoot 'artifacts\ui-review\2.3.0-rc5-layout-fix' }
$runtimeRoot = Join-Path $OutputRoot 'runtime-data'
$env:PIXEL_TART_ISOLATED_RUNTIME = '1'
$env:PIXEL_TART_ISOLATED_RUNTIME_ROOT = $runtimeRoot

New-Item -ItemType Directory -Path $OutputRoot -Force | Out-Null
New-Item -ItemType Directory -Path $reviewRoot -Force | Out-Null
New-Item -ItemType Directory -Path $runtimeRoot -Force | Out-Null
$settings = [ordered]@{
    Appearance = [ordered]@{ Theme = 2; SidebarCollapsed = $false }
    PinnedQuickTools = @('Workflow','PhotoOrganize','BatchCompress')
    QuickToolLayout = [ordered]@{ SchemaVersion='1.0'; OrderedToolIds=@('Workflow','PhotoOrganize','BatchCompress') }
    WindowWidth = 1600; WindowHeight = 920; WindowMaximized = $false
    OnboardingCompleted = $true; OnboardingCurrentStep = 22; OnboardingLegacyUser = $true; OnboardingUpgradeOfferShown = $true
}
$settings | ConvertTo-Json -Depth 20 | Set-Content -LiteralPath (Join-Path $runtimeRoot 'settings.json') -Encoding UTF8

if (-not $SkipBuild) {
    & $dotnet build $project -c Release -p:UiReviewBuild=true -p:Platform=x64 --no-restore
    if ($LASTEXITCODE -ne 0) { throw 'RC5 layout UI review build failed.' }
}
if (-not (Test-Path -LiteralPath $executable)) { throw "UI review executable not found: $executable" }

$scenarios = @(
    @{ File='01_Workbench_Calendar_Free.png'; State='WorkbenchCalendarFree'; Theme='Dark'; Width=1600; Height=920; Scale=1.0; PhysicalWidth=1600; PhysicalHeight=920 },
    @{ File='02_Workbench_Calendar_Scheduled.png'; State='WorkbenchCalendarScheduled'; Theme='Dark'; Width=1600; Height=920; Scale=1.0; PhysicalWidth=1600; PhysicalHeight=920 },
    @{ File='03_Workbench_Calendar_Shot.png'; State='WorkbenchCalendarShot'; Theme='Dark'; Width=1600; Height=920; Scale=1.0; PhysicalWidth=1600; PhysicalHeight=920 },
    @{ File='04_Workbench_Calendar_PendingReturn.png'; State='WorkbenchCalendarPendingReturn'; Theme='Dark'; Width=1600; Height=920; Scale=1.0; PhysicalWidth=1600; PhysicalHeight=920 },
    @{ File='05_Workbench_Calendar_Returned.png'; State='WorkbenchCalendarReturned'; Theme='Dark'; Width=1600; Height=920; Scale=1.0; PhysicalWidth=1600; PhysicalHeight=920 },
    @{ File='06_Workbench_Calendar_NumberVisible.png'; State='WorkbenchCalendarNumberVisible'; Theme='Dark'; Width=1600; Height=920; Scale=1.0; PhysicalWidth=1600; PhysicalHeight=920 },
    @{ File='07_Workbench_Calendar_Today.png'; State='WorkbenchCalendarToday'; Theme='Dark'; Width=1600; Height=920; Scale=1.0; PhysicalWidth=1600; PhysicalHeight=920 },
    @{ File='08_Workbench_Calendar_Selected.png'; State='WorkbenchCalendarSelected'; Theme='Dark'; Width=1600; Height=920; Scale=1.0; PhysicalWidth=1600; PhysicalHeight=920 },
    @{ File='09_Workbench_TaskCenter_Empty.png'; State='WorkbenchTaskCenterEmpty'; Theme='Dark'; Width=1600; Height=920; Scale=1.0; PhysicalWidth=1600; PhysicalHeight=920 },
    @{ File='10_Workbench_TaskCenter_5Tasks.png'; State='WorkbenchTaskCenter5Tasks'; Theme='Dark'; Width=1600; Height=920; Scale=1.0; PhysicalWidth=1600; PhysicalHeight=920 },
    @{ File='11_Workbench_TaskCenter_20Tasks.png'; State='WorkbenchTaskCenter20Tasks'; Theme='Dark'; Width=1600; Height=920; Scale=1.0; PhysicalWidth=1600; PhysicalHeight=920 },
    @{ File='12_Workbench_TaskCenter_Scrolled.png'; State='WorkbenchTaskCenterScrolled'; Theme='Dark'; Width=1600; Height=920; Scale=1.0; PhysicalWidth=1600; PhysicalHeight=920 },
    @{ File='13_Workbench_1080.png'; State='WorkbenchCalendarToday'; Theme='Dark'; Width=1920; Height=1080; Scale=1.0; PhysicalWidth=1920; PhysicalHeight=1080 },
    @{ File='14_Workbench_768.png'; State='WorkbenchCalendarToday'; Theme='Dark'; Width=1280; Height=768; Scale=1.0; PhysicalWidth=1280; PhysicalHeight=768 },
    @{ File='15_FullCalendar_Header.png'; State='CalendarHeaderLayout'; Theme='Dark'; Width=1600; Height=920; Scale=1.0; PhysicalWidth=1600; PhysicalHeight=920 },
    @{ File='16_FullCalendar_YearMonthSpacing.png'; State='CalendarYearMonthSpacing'; Theme='Dark'; Width=1600; Height=920; Scale=1.0; PhysicalWidth=1600; PhysicalHeight=920 },
    @{ File='17_FullCalendar_1280.png'; State='CalendarStatusColors'; Theme='Dark'; Width=1280; Height=768; Scale=1.0; PhysicalWidth=1280; PhysicalHeight=768 },
    @{ File='18_FullCalendar_1600.png'; State='CalendarStatusColors'; Theme='Dark'; Width=1600; Height=920; Scale=1.0; PhysicalWidth=1600; PhysicalHeight=920 },
    @{ File='19_FullCalendar_1920.png'; State='CalendarStatusColors'; Theme='Dark'; Width=1920; Height=1080; Scale=1.0; PhysicalWidth=1920; PhysicalHeight=1080 },
    @{ File='20_DarkTheme.png'; State='WorkbenchCalendarDarkTheme'; Theme='Dark'; Width=1600; Height=920; Scale=1.0; PhysicalWidth=1600; PhysicalHeight=920 },
    @{ File='21_LightTheme.png'; State='WorkbenchCalendarScheduled'; Theme='Light'; Width=1600; Height=920; Scale=1.0; PhysicalWidth=1600; PhysicalHeight=920 },
    @{ File='22_HighContrast.png'; State='WorkbenchCalendarScheduled'; Theme='HighContrast'; Width=1600; Height=920; Scale=1.0; PhysicalWidth=1600; PhysicalHeight=920 },
    @{ File='23_Dpi150.png'; State='WorkbenchCalendarNumberVisible'; Theme='Dark'; Width=1706; Height=960; Scale=1.5; PhysicalWidth=2560; PhysicalHeight=1440 },
    @{ File='24_Dpi200.png'; State='WorkbenchCalendarNumberVisible'; Theme='Dark'; Width=1280; Height=720; Scale=2.0; PhysicalWidth=2560; PhysicalHeight=1440 }
)

$results = [System.Collections.Generic.List[object]]::new()
foreach ($scenario in $scenarios) {
    $outputPath = Join-Path $OutputRoot $scenario.File
    $metadataPath = $outputPath + '.json'
    Remove-Item -LiteralPath $outputPath,$metadataPath -Force -ErrorAction SilentlyContinue
    $state = [ordered]@{
        State=$scenario.State; Theme=$scenario.Theme; Width=$scenario.Width; Height=$scenario.Height; SidebarCollapsed=$false
        OutputPath=$outputPath; MetadataPath=$metadataPath; DpiScale=$scenario.Scale
        PhysicalWidth=$scenario.PhysicalWidth; PhysicalHeight=$scenario.PhysicalHeight
    }
    $state | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $statePath -Encoding UTF8
    $process = Start-Process -FilePath $executable -PassThru -WindowStyle Normal
    $deadline = [DateTime]::UtcNow.AddSeconds(35)
    while ((-not (Test-Path -LiteralPath $outputPath) -or -not (Test-Path -LiteralPath $metadataPath)) -and [DateTime]::UtcNow -lt $deadline) { Start-Sleep -Milliseconds 250 }
    if (-not $process.HasExited) { $process.CloseMainWindow() | Out-Null; Start-Sleep -Milliseconds 250 }
    if (-not $process.HasExited) { Stop-Process -Id $process.Id -Force }
    if (-not (Test-Path -LiteralPath $outputPath)) { throw "Capture failed: $($scenario.File)" }
    if (-not (Test-Path -LiteralPath $metadataPath)) { throw "Metadata failed: $($scenario.File)" }
    $metadata = Get-Content -LiteralPath $metadataPath -Raw | ConvertFrom-Json
    $file = Get-Item -LiteralPath $outputPath
    $results.Add([ordered]@{ File=$scenario.File; State=$scenario.State; Bytes=$file.Length; Sha256=(Get-FileHash -LiteralPath $outputPath -Algorithm SHA256).Hash; LayoutPassed=[bool]$metadata.passed; BlockingIssues=[int]$metadata.layout.BlockingIssueCount })
}

$results | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath (Join-Path $OutputRoot 'ui-evidence-index.json') -Encoding UTF8
$python = 'C:\Users\Administrator\.cache\codex-runtimes\codex-primary-runtime\dependencies\python\python.exe'
& $python (Join-Path $PSScriptRoot 'create_contact_sheet.py') --input $OutputRoot
if ($LASTEXITCODE -ne 0) { throw 'Contact sheet generation failed.' }
$results | Format-Table File,Bytes,LayoutPassed,BlockingIssues -AutoSize
