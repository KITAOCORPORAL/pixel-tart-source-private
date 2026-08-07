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
if ([string]::IsNullOrWhiteSpace($OutputRoot)) { $OutputRoot = Join-Path $repoRoot 'artifacts\ui-review\2.3.0-rc5-mini-calendar-hotfix' }
$runtimeRoot = Join-Path $OutputRoot 'runtime-data'
$env:PIXEL_TART_ISOLATED_RUNTIME = '1'
$env:PIXEL_TART_ISOLATED_RUNTIME_ROOT = $runtimeRoot

New-Item -ItemType Directory -Path $OutputRoot -Force | Out-Null
New-Item -ItemType Directory -Path $reviewRoot -Force | Out-Null
New-Item -ItemType Directory -Path $runtimeRoot -Force | Out-Null
@{
    Appearance = @{ Theme = 2; SidebarCollapsed = $false }
    PinnedQuickTools = @('Workflow','PhotoOrganize','BatchCompress')
    QuickToolLayout = @{ SchemaVersion='1.0'; OrderedToolIds=@('Workflow','PhotoOrganize','BatchCompress') }
    WindowWidth = 1600; WindowHeight = 900; WindowMaximized = $false
    OnboardingCompleted = $true; OnboardingCurrentStep = 22; OnboardingLegacyUser = $true; OnboardingUpgradeOfferShown = $true
} | ConvertTo-Json -Depth 20 | Set-Content -LiteralPath (Join-Path $runtimeRoot 'settings.json') -Encoding UTF8

if (-not $SkipBuild) {
    & $dotnet build $project -c Release -p:UiReviewBuild=true -p:Platform=x64 --no-restore
    if ($LASTEXITCODE -ne 0) { throw 'Mini calendar hotfix UI review build failed.' }
}
if (-not (Test-Path -LiteralPath $executable)) { throw "UI review executable not found: $executable" }

$scenarios = @(
    @{ File='01_MiniCalendar_Default.png'; State='WorkbenchCalendarHotfixDefault'; Width=1600; Height=900; Scale=1.0; PW=1600; PH=900 },
    @{ File='02_MiniCalendar_AllStates.png'; State='WorkbenchCalendarHotfixAllStates'; Width=1600; Height=900; Scale=1.0; PW=1600; PH=900 },
    @{ File='03_MiniCalendar_DoubleDigits.png'; State='WorkbenchCalendarHotfixDoubleDigits'; Width=1600; Height=900; Scale=1.0; PW=1600; PH=900 },
    @{ File='04_MiniCalendar_Today.png'; State='WorkbenchCalendarHotfixToday'; Width=1600; Height=900; Scale=1.0; PW=1600; PH=900 },
    @{ File='05_MiniCalendar_Selected.png'; State='WorkbenchCalendarHotfixSelected'; Width=1600; Height=900; Scale=1.0; PW=1600; PH=900 },
    @{ File='06_MiniCalendar_LastRow.png'; State='WorkbenchCalendarHotfixLastRow'; Width=1600; Height=900; Scale=1.0; PW=1600; PH=900 },
    @{ File='07_MiniCalendar_6Weeks.png'; State='WorkbenchCalendarHotfixSixWeeks'; Width=1600; Height=900; Scale=1.0; PW=1600; PH=900 },
    @{ File='08_MiniCalendar_1280x720.png'; State='WorkbenchCalendarHotfixSixWeeks'; Width=1280; Height=720; Scale=1.0; PW=1280; PH=720 },
    @{ File='09_MiniCalendar_1280x768.png'; State='WorkbenchCalendarHotfixSixWeeks'; Width=1280; Height=768; Scale=1.0; PW=1280; PH=768 },
    @{ File='10_MiniCalendar_1600x900.png'; State='WorkbenchCalendarHotfixSixWeeks'; Width=1600; Height=900; Scale=1.0; PW=1600; PH=900 },
    @{ File='11_MiniCalendar_1920x1080.png'; State='WorkbenchCalendarHotfixSixWeeks'; Width=1920; Height=1080; Scale=1.0; PW=1920; PH=1080 },
    @{ File='12_MiniCalendar_Dpi150.png'; State='WorkbenchCalendarHotfixDoubleDigits'; Width=1600; Height=900; Scale=1.5; PW=2400; PH=1350 },
    @{ File='13_MiniCalendar_Dpi200.png'; State='WorkbenchCalendarHotfixDoubleDigits'; Width=1280; Height=720; Scale=2.0; PW=2560; PH=1440 },
    @{ File='14_MonthNavigation.png'; State='WorkbenchCalendarHotfixNavigation'; Width=1600; Height=900; Scale=1.0; PW=1600; PH=900 }
)

$results = [System.Collections.Generic.List[object]]::new()
foreach ($scenario in $scenarios) {
    $outputPath = Join-Path $OutputRoot $scenario.File
    $metadataPath = $outputPath + '.json'
    Remove-Item -LiteralPath $outputPath,$metadataPath -Force -ErrorAction SilentlyContinue
    @{
        State=$scenario.State; Theme='Dark'; Width=$scenario.Width; Height=$scenario.Height; SidebarCollapsed=$false
        OutputPath=$outputPath; MetadataPath=$metadataPath; DpiScale=$scenario.Scale
        PhysicalWidth=$scenario.PW; PhysicalHeight=$scenario.PH
    } | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $statePath -Encoding UTF8
    $process = Start-Process -FilePath $executable -PassThru -WindowStyle Normal
    $deadline = [DateTime]::UtcNow.AddSeconds(35)
    while ((-not (Test-Path -LiteralPath $outputPath) -or -not (Test-Path -LiteralPath $metadataPath)) -and [DateTime]::UtcNow -lt $deadline) { Start-Sleep -Milliseconds 250 }
    if (-not $process.HasExited) { $process.CloseMainWindow() | Out-Null; Start-Sleep -Milliseconds 250 }
    if (-not $process.HasExited) { Stop-Process -Id $process.Id -Force }
    if (-not (Test-Path -LiteralPath $outputPath) -or -not (Test-Path -LiteralPath $metadataPath)) { throw "Capture failed: $($scenario.File)" }
    $metadata = Get-Content -LiteralPath $metadataPath -Raw | ConvertFrom-Json
    $results.Add(@{
        File=$scenario.File
        Bytes=(Get-Item -LiteralPath $outputPath).Length
        Sha256=(Get-FileHash -LiteralPath $outputPath -Algorithm SHA256).Hash
        LayoutPassed=[bool]$metadata.passed
        MiniCalendarPassed=[bool]$metadata.miniCalendarInspection.Passed
        DayCellActualWidth=[double]$metadata.miniCalendarInspection.DayCellActualWidth
        DayCellActualHeight=[double]$metadata.miniCalendarInspection.DayCellActualHeight
        BadgeActualWidth=[double]$metadata.miniCalendarInspection.DayNumberBadgeActualWidth
        BadgeActualHeight=[double]$metadata.miniCalendarInspection.DayNumberBadgeActualHeight
        TextActualHeight=[double]$metadata.miniCalendarInspection.DayNumberTextActualHeight
        RowToDetailsSpacing=[double]$metadata.miniCalendarInspection.LastRowToDetailsSpacing
        MonthButtonWidth=[double]$metadata.miniCalendarInspection.MonthButtonActualWidth
        MonthButtonHeight=[double]$metadata.miniCalendarInspection.MonthButtonActualHeight
        Issues=@($metadata.miniCalendarInspection.Issues)
    })
}

$results | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath (Join-Path $OutputRoot 'ui-evidence-index.json') -Encoding UTF8
if (($results | Where-Object { -not $_.LayoutPassed -or -not $_.MiniCalendarPassed }).Count -ne 0) { throw 'Mini calendar visual assertions failed.' }
$results | Format-Table File,LayoutPassed,MiniCalendarPassed,DayCellActualWidth,DayCellActualHeight,BadgeActualWidth,BadgeActualHeight,TextActualHeight,RowToDetailsSpacing,MonthButtonWidth,MonthButtonHeight -AutoSize
