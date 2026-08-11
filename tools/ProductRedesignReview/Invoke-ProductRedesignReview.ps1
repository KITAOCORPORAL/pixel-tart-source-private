param(
    [string]$OutputRoot = '',
    [switch]$SkipBuild,
    [int]$MaxScenarios = 0
)

$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
$workspaceRoot = Split-Path $repoRoot -Parent
$dotnet = Join-Path $workspaceRoot '.dotnet\dotnet.exe'
$project = Join-Path $repoRoot 'src\RAWSelectionAssistant\RAWSelectionAssistant.csproj'
$executable = Join-Path $repoRoot 'src\RAWSelectionAssistant\bin\x64\Release\net10.0-windows10.0.19041.0\win-x64\KitaoPhotoSelector.UiReview.exe'
if ([string]::IsNullOrWhiteSpace($OutputRoot)) {
    $OutputRoot = Join-Path $repoRoot 'artifacts\ui-review\product-redesign'
}
$OutputRoot = [IO.Path]::GetFullPath($OutputRoot)
New-Item -ItemType Directory -Path $OutputRoot -Force | Out-Null

if (-not $SkipBuild) {
    & $dotnet build $project -c Release -p:UiReviewBuild=true -p:Platform=x64 --no-restore
    if ($LASTEXITCODE -ne 0) { throw 'Product redesign UI review build failed.' }
}
if (-not (Test-Path -LiteralPath $executable)) { throw "UI review executable not found: $executable" }

function New-SyntheticImages([string]$directory) {
    New-Item -ItemType Directory -Path $directory -Force | Out-Null
    Add-Type -AssemblyName System.Drawing
    $colors = @(
        @([Drawing.Color]::FromArgb(21, 70, 82), [Drawing.Color]::FromArgb(24, 168, 140)),
        @([Drawing.Color]::FromArgb(72, 42, 56), [Drawing.Color]::FromArgb(215, 154, 50)),
        @([Drawing.Color]::FromArgb(37, 50, 75), [Drawing.Color]::FromArgb(78, 143, 212)),
        @([Drawing.Color]::FromArgb(54, 64, 45), [Drawing.Color]::FromArgb(66, 184, 131)),
        @([Drawing.Color]::FromArgb(62, 45, 75), [Drawing.Color]::FromArgb(224, 173, 67)),
        @([Drawing.Color]::FromArgb(71, 48, 43), [Drawing.Color]::FromArgb(216, 90, 90))
    )
    for ($index = 0; $index -lt $colors.Count; $index++) {
        $bitmap = [Drawing.Bitmap]::new(1200, 800)
        $graphics = [Drawing.Graphics]::FromImage($bitmap)
        $background = [Drawing.SolidBrush]::new($colors[$index][0])
        $accent = [Drawing.SolidBrush]::new($colors[$index][1])
        $font = [Drawing.Font]::new('Segoe UI', 44, [Drawing.FontStyle]::Bold)
        try {
            $graphics.FillRectangle($background, 0, 0, 1200, 800)
            $graphics.FillEllipse($accent, 110 + $index * 26, 110, 450, 450)
            $graphics.FillRectangle($accent, 650, 300 - $index * 12, 380, 250)
            $graphics.DrawString(('PIXEL TART / {0:00}' -f ($index + 1)), $font, [Drawing.Brushes]::White, 56, 690)
            $path = Join-Path $directory ('DPI_TEST_{0:00}.png' -f ($index + 1))
            $bitmap.Save($path, [Drawing.Imaging.ImageFormat]::Png)
            $bitmap.Save((Join-Path $directory ('STAGEC_{0:00}.png' -f ($index + 1))), [Drawing.Imaging.ImageFormat]::Png)
        }
        finally {
            $font.Dispose(); $accent.Dispose(); $background.Dispose(); $graphics.Dispose(); $bitmap.Dispose()
        }
    }
    [IO.File]::WriteAllBytes((Join-Path $directory 'SYNTHETIC_REVIEW.NEF'), [byte[]](0x49,0x49,0x2A,0x00,0x08,0x00,0x00,0x00))
    [IO.File]::WriteAllBytes((Join-Path $directory 'STAGEC_RAW.nef'), [byte[]](0x49,0x49,0x2A,0x00,0x08,0x00,0x00,0x00))
}

$scenarios = @(
    @{ File='01_Workbench_Dark_1600x900.png'; State='WorkbenchDarkExpanded'; Theme='Dark'; Width=1600; Height=900; Scale=1.0 },
    @{ File='02_Workbench_Light_1600x900.png'; State='WorkbenchLight'; Theme='Light'; Width=1600; Height=900; Scale=1.0 },
    @{ File='03_Workbench_HighContrast_1600x900.png'; State='WorkbenchHighContrast'; Theme='HighContrast'; Width=1600; Height=900; Scale=1.0 },
    @{ File='04_Workbench_1280x720.png'; State='Workbench1280'; Theme='Dark'; Width=1280; Height=720; Scale=1.0 },
    @{ File='05_Workbench_1280x768.png'; State='Workbench1280'; Theme='Dark'; Width=1280; Height=768; Scale=1.0 },
    @{ File='06_Workbench_1366x768.png'; State='WorkbenchDarkExpanded'; Theme='Dark'; Width=1366; Height=768; Scale=1.0 },
    @{ File='07_Workbench_1440x900.png'; State='WorkbenchDarkExpanded'; Theme='Dark'; Width=1440; Height=900; Scale=1.0 },
    @{ File='08_Workbench_1920x1080.png'; State='WorkbenchDarkExpanded'; Theme='Dark'; Width=1920; Height=1080; Scale=1.0 },
    @{ File='09_Workbench_2560x1440.png'; State='WorkbenchDarkExpanded'; Theme='Dark'; Width=2560; Height=1440; Scale=1.0 },
    @{ File='10_Calendar_StatusColors.png'; State='CalendarStatusColors'; Theme='Dark'; Width=1600; Height=900; Scale=1.0 },
    @{ File='11_Calendar_ContextMenu.png'; State='CalendarContextMenu'; Theme='Dark'; Width=1600; Height=900; Scale=1.0 },
    @{ File='12_Calendar_ClosedDay.png'; State='CalendarDayClosed'; Theme='Dark'; Width=1600; Height=900; Scale=1.0 },
    @{ File='13_Booking_QuickCreate.png'; State='BookingQuickCreate'; Theme='Dark'; Width=1600; Height=900; Scale=1.0; LayoutRoot='BookingEditorModalSurface' },
    @{ File='14_Booking_QuickEdit_Drawer.png'; State='BookingQuickEdit'; Theme='Dark'; Width=1600; Height=900; Scale=1.0; LayoutRoot='BookingEditorDrawerSurface' },
    @{ File='15_Booking_FullPlanning.png'; State='BookingFullPlanning'; Theme='Dark'; Width=1600; Height=900; Scale=1.0; LayoutRoot='BookingEditorPlanningSurface' },
    @{ File='16_Toolbox.png'; State='ToolboxFullPage'; Theme='Dark'; Width=1600; Height=900; Scale=1.0 },
    @{ File='17_Toolbox_Pin_Unpinned.png'; State='RuntimeToolboxUnpinned'; Theme='Dark'; Width=1600; Height=900; Scale=1.0 },
    @{ File='18_Toolbox_Pin_Pinned_400.png'; State='RuntimeToolboxPinned'; Theme='Dark'; Width=640; Height=360; Scale=4.0; PhysicalWidth=2560; PhysicalHeight=1440 },
    @{ File='19_RAW_To_JPEG_Modal.png'; State='RawToJpeg'; Theme='Dark'; Width=1600; Height=900; Scale=1.0 },
    @{ File='20_Batch_Compression_Modal.png'; State='BatchCompression'; Theme='Dark'; Width=1600; Height=900; Scale=1.0 },
    @{ File='21_OnlineSelection_Home.png'; State='OnlineSelectionHome'; Theme='Dark'; Width=1600; Height=900; Scale=1.0 },
    @{ File='22_OnlineSelection_Project.png'; State='OnlineSelectionProject'; Theme='Dark'; Width=1600; Height=900; Scale=1.0 },
    @{ File='23_OnlineSelection_Create.png'; State='OnlineSelectionCreate'; Theme='Dark'; Width=1600; Height=900; Scale=1.0 },
    @{ File='24_EmptyState_Archive.png'; State='RuntimeCollectionEmpty'; Theme='Dark'; Width=1600; Height=900; Scale=1.0 },
    @{ File='25_DPI_100.png'; State='WorkbenchDpi150'; Theme='Dark'; Width=1280; Height=720; Scale=1.0; PhysicalWidth=1280; PhysicalHeight=720 },
    @{ File='26_DPI_125.png'; State='WorkbenchDpi150'; Theme='Dark'; Width=1280; Height=720; Scale=1.25; PhysicalWidth=1600; PhysicalHeight=900 },
    @{ File='27_DPI_150.png'; State='WorkbenchDpi150'; Theme='Dark'; Width=1280; Height=720; Scale=1.5; PhysicalWidth=1920; PhysicalHeight=1080 },
    @{ File='28_DPI_175.png'; State='WorkbenchDpi150'; Theme='Dark'; Width=1280; Height=720; Scale=1.75; PhysicalWidth=2240; PhysicalHeight=1260 },
    @{ File='29_DPI_200.png'; State='WorkbenchDpi200'; Theme='Dark'; Width=1280; Height=720; Scale=2.0; PhysicalWidth=2560; PhysicalHeight=1440 },
    @{ File='30_Tether_Workspace.png'; State='TetherReview'; Theme='Dark'; Width=1600; Height=900; Scale=1.0 }
)

$results = [Collections.Generic.List[object]]::new()
if ($MaxScenarios -gt 0) {
    $scenarios = @($scenarios | Select-Object -First $MaxScenarios)
}
foreach ($scenario in $scenarios) {
    $outputPath = Join-Path $OutputRoot $scenario.File
    if (Test-Path -LiteralPath $outputPath) { throw "Evidence already exists; choose a new OutputRoot: $outputPath" }
    $runtimeRoot = Join-Path $OutputRoot ('runtime\' + [IO.Path]::GetFileNameWithoutExtension($scenario.File))
    $demoRoot = Join-Path $runtimeRoot 'DemoImages'
    New-Item -ItemType Directory -Path $runtimeRoot -Force | Out-Null
    New-SyntheticImages $demoRoot
    $settings = [ordered]@{
        Appearance = [ordered]@{ Theme = 2; SidebarCollapsed = $false }
        PinnedQuickTools = @('PhotoOrganize','RawToJpeg','BatchCompress','Collage')
        QuickToolLayout = [ordered]@{ SchemaVersion='1.0'; OrderedToolIds=@('PhotoOrganize','RawToJpeg','BatchCompress','Collage') }
        ProductQuickToolLayout = [ordered]@{ SchemaVersion='1.0'; OrderedToolIds=@('PhotoOrganize','RawToJpeg','BatchCompress','Collage') }
        WindowWidth = $scenario.Width; WindowHeight = $scenario.Height; WindowMaximized = $false
        OnboardingCompleted = $true; OnboardingCurrentStep = 22; OnboardingLegacyUser = $true; OnboardingUpgradeOfferShown = $true
    }
    $settings | ConvertTo-Json -Depth 20 | Set-Content -LiteralPath (Join-Path $runtimeRoot 'settings.json') -Encoding UTF8

    $metadataPath = $outputPath + '.json'
    $physicalWidth = if ($scenario.PhysicalWidth) { $scenario.PhysicalWidth } else { [int]($scenario.Width * $scenario.Scale) }
    $physicalHeight = if ($scenario.PhysicalHeight) { $scenario.PhysicalHeight } else { [int]($scenario.Height * $scenario.Scale) }
    $state = [ordered]@{
        State=$scenario.State; Theme=$scenario.Theme; Width=$scenario.Width; Height=$scenario.Height; SidebarCollapsed=$false
        OutputPath=$outputPath; MetadataPath=$metadataPath; DpiScale=$scenario.Scale; PhysicalWidth=$physicalWidth; PhysicalHeight=$physicalHeight
    }
    if ($scenario.LayoutRoot) { $state.LayoutRoot = $scenario.LayoutRoot }
    $statePath = Join-Path $runtimeRoot 'ui-review-state.json'
    $state | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $statePath -Encoding UTF8
    $env:PIXEL_TART_ISOLATED_RUNTIME = '1'
    $env:PIXEL_TART_ISOLATED_RUNTIME_ROOT = $runtimeRoot
    $process = Start-Process -FilePath $executable -PassThru -WindowStyle Normal
    $deadline = [DateTime]::UtcNow.AddSeconds(45)
    while ((-not (Test-Path -LiteralPath $outputPath) -or -not (Test-Path -LiteralPath $metadataPath)) -and [DateTime]::UtcNow -lt $deadline) {
        Start-Sleep -Milliseconds 250
    }
    if (-not $process.HasExited) { $process.CloseMainWindow() | Out-Null; Start-Sleep -Milliseconds 500 }
    if (-not $process.HasExited) { Stop-Process -Id $process.Id -Force }
    if (-not (Test-Path -LiteralPath $outputPath)) { throw "Capture failed: $($scenario.File)" }
    if (-not (Test-Path -LiteralPath $metadataPath)) { throw "Metadata failed: $($scenario.File)" }
    $metadata = Get-Content -LiteralPath $metadataPath -Raw | ConvertFrom-Json
    $file = Get-Item -LiteralPath $outputPath
    $results.Add([pscustomobject][ordered]@{
        File=$scenario.File; State=$scenario.State; Theme=$scenario.Theme; Width=$physicalWidth; Height=$physicalHeight
        Bytes=$file.Length; Sha256=(Get-FileHash -LiteralPath $outputPath -Algorithm SHA256).Hash
        LayoutPassed=[bool]$metadata.passed; BlockingIssues=[int]$metadata.layout.BlockingIssueCount
        WorkbenchQuickToolsPassed=[bool]$metadata.workbenchQuickToolsPassed
        PinnedToolboxItemIds=@($metadata.pinnedToolboxItemIds)
        DisplayedPinnedToolboxItemIds=@($metadata.displayedPinnedToolboxItemIds)
        EvidenceLevel='AutomatedReview'; IsolatedRuntime=$true
    })
}

$results | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath (Join-Path $OutputRoot 'ui-evidence-index.json') -Encoding UTF8
$results | Format-Table File,Width,Height,LayoutPassed,WorkbenchQuickToolsPassed,BlockingIssues -AutoSize
