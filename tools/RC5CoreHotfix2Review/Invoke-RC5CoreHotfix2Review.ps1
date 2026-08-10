param([string]$OutputRoot = '', [switch]$SkipBuild, [switch]$Force)

$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
$workspaceRoot = Split-Path $repoRoot -Parent
$dotnet = Join-Path $workspaceRoot '.dotnet\dotnet.exe'
$project = Join-Path $repoRoot 'src\RAWSelectionAssistant\RAWSelectionAssistant.csproj'
$executable = Join-Path $repoRoot 'src\RAWSelectionAssistant\bin\x64\Release\net10.0-windows10.0.19041.0\win-x64\KitaoPhotoSelector.UiReview.exe'
$reviewRoot = Join-Path $env:LOCALAPPDATA 'KitaoPhotoSelector.UiReview'
$statePath = Join-Path $reviewRoot 'ui-review-state.json'
if ([string]::IsNullOrWhiteSpace($OutputRoot)) { $OutputRoot = Join-Path $repoRoot 'artifacts\ui-review\2.3.0-rc5-core-hotfix2' }
$runtimeRoot = Join-Path $OutputRoot 'runtime-data'
$env:PIXEL_TART_ISOLATED_RUNTIME = '1'
$env:PIXEL_TART_ISOLATED_RUNTIME_ROOT = $runtimeRoot

New-Item -ItemType Directory -Path $OutputRoot,$reviewRoot,$runtimeRoot -Force | Out-Null
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
    if ($LASTEXITCODE -ne 0) { throw 'CoreHotfix2 UI review build failed.' }
}
if (-not (Test-Path -LiteralPath $executable)) { throw "UI review executable not found: $executable" }

$scenarios = @(
    @{ File='01_MiniCalendar_ContextMenu.png'; State='WorkbenchCalendarContextMenu'; Width=1600; Height=920; Scale=1.0 },
    @{ File='02_MiniCalendar_ViewDetailsResult.png'; State='CalendarDayDetails'; Width=1600; Height=920; Scale=1.0 },
    @{ File='03_FullCalendar_SelectedDate.png'; State='CalendarSelectedDay'; Width=1600; Height=920; Scale=1.0 },
    @{ File='04_FullCalendar_ScheduledRed.png'; State='CalendarStatusColors'; Width=1600; Height=920; Scale=1.0 },
    @{ File='05_FullCalendar_ShotGreen.png'; State='CalendarStatusColors'; Width=1600; Height=920; Scale=1.0 },
    @{ File='06_FullCalendar_PendingYellow.png'; State='CalendarStatusColors'; Width=1600; Height=920; Scale=1.0 },
    @{ File='07_FullCalendar_ReturnedBlue.png'; State='CalendarStatusColors'; Width=1600; Height=920; Scale=1.0 },
    @{ File='08_FullCalendar_NoBottomUnderline.png'; State='CalendarStatusColors'; Width=1600; Height=920; Scale=1.0 },
    @{ File='09_FullCalendar_ClosedDay.png'; State='CalendarDayClosed'; Width=1600; Height=920; Scale=1.0 },
    @{ File='10_FullCalendar_ClosedBookingDay.png'; State='CalendarClosedBookingDay'; Width=1600; Height=920; Scale=1.0 },
    @{ File='11_Booking_EditEntry.png'; State='CalendarBookingContextMenu'; Width=1600; Height=920; Scale=1.0 },
    @{ File='12_Booking_EditLoaded.png'; State='CreateShootBasic'; Width=1600; Height=920; Scale=1.0 },
    @{ File='13_Booking_EditSaved.png'; State='CreateShootSaved'; Width=1600; Height=920; Scale=1.0 },
    @{ File='14_Booking_EditSameId.png'; State='CreateShootSaved'; Width=1600; Height=920; Scale=1.0 },
    @{ File='15_Booking_StepperClean.png'; State='CreateShootStep2'; Width=1600; Height=920; Scale=1.0 },
    @{ File='16_Booking_Dpi150.png'; State='CreateShootStep2'; Width=1706; Height=960; Scale=1.5; PhysicalWidth=2560; PhysicalHeight=1440 },
    @{ File='17_Booking_Dpi200.png'; State='CreateShootStep2'; Width=1280; Height=720; Scale=2.0; PhysicalWidth=2560; PhysicalHeight=1440 },
    @{ File='18_Toolbox_Unpinned.png'; State='RuntimeToolboxUnpinned'; Width=1600; Height=920; Scale=1.0 },
    @{ File='19_Toolbox_Pinned.png'; State='RuntimeToolboxPinned'; Width=1600; Height=920; Scale=1.0 },
    @{ File='20_Toolbox_Pin400Percent.png'; State='RuntimeToolboxPinned'; Width=640; Height=368; Scale=4.0; PhysicalWidth=2560; PhysicalHeight=1472 },
    @{ File='21_Toolbox_WorkbenchSync.png'; State='WorkbenchPinStates'; Width=1600; Height=920; Scale=1.0 },
    @{ File='22_ContextMenu_Date.png'; State='CalendarContextMenu'; Width=1600; Height=920; Scale=1.0 },
    @{ File='23_ContextMenu_Booking.png'; State='CalendarBookingContextMenu'; Width=1600; Height=920; Scale=1.0 }
)

$results = [System.Collections.Generic.List[object]]::new()
foreach ($scenario in $scenarios) {
    $outputPath = Join-Path $OutputRoot $scenario.File
    $metadataPath = $outputPath + '.json'
    if (-not $Force -and (Test-Path -LiteralPath $outputPath) -and (Test-Path -LiteralPath $metadataPath)) {
        $metadata = Get-Content -LiteralPath $metadataPath -Raw | ConvertFrom-Json
        $file = Get-Item -LiteralPath $outputPath
        $results.Add([ordered]@{ File=$scenario.File; State=$scenario.State; Bytes=$file.Length; Sha256=(Get-FileHash -LiteralPath $outputPath -Algorithm SHA256).Hash; LayoutPassed=[bool]$metadata.passed; BlockingIssues=[int]$metadata.layout.BlockingIssueCount; EvidenceLevel='AutomatedReview' })
        continue
    }
    Remove-Item -LiteralPath $outputPath,$metadataPath -Force -ErrorAction SilentlyContinue
    $physicalWidth = if ($scenario.PhysicalWidth) { $scenario.PhysicalWidth } else { [int]($scenario.Width * $scenario.Scale) }
    $physicalHeight = if ($scenario.PhysicalHeight) { $scenario.PhysicalHeight } else { [int]($scenario.Height * $scenario.Scale) }
    $state = [ordered]@{ State=$scenario.State; Theme='Dark'; Width=$scenario.Width; Height=$scenario.Height; SidebarCollapsed=$false; OutputPath=$outputPath; MetadataPath=$metadataPath; DpiScale=$scenario.Scale; PhysicalWidth=$physicalWidth; PhysicalHeight=$physicalHeight }
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
    $results.Add([ordered]@{ File=$scenario.File; State=$scenario.State; Bytes=$file.Length; Sha256=(Get-FileHash -LiteralPath $outputPath -Algorithm SHA256).Hash; LayoutPassed=[bool]$metadata.passed; BlockingIssues=[int]$metadata.layout.BlockingIssueCount; EvidenceLevel='AutomatedReview' })
}

$results | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath (Join-Path $OutputRoot 'ui-evidence-index.json') -Encoding UTF8
$results | Format-Table File,Bytes,LayoutPassed,BlockingIssues -AutoSize
