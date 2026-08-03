param(
    [string]$OutputRoot = '',
    [switch]$SkipBuild,
    [switch]$KeepReviewProfile
)

$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
$dotnet = Join-Path $env:LOCALAPPDATA 'Microsoft\dotnet-sdk-10\dotnet.exe'
$project = Join-Path $repoRoot 'src\RAWSelectionAssistant\RAWSelectionAssistant.csproj'
$executable = Join-Path $repoRoot 'src\RAWSelectionAssistant\bin\x64\Release\net10.0-windows10.0.19041.0\win-x64\KitaoPhotoSelector.UiReview.exe'
$reviewRoot = Join-Path $env:LOCALAPPDATA 'KitaoPhotoSelector.UiReview'
$statePath = Join-Path $reviewRoot 'ui-review-state.json'
$sourceCommit = (& git -C $repoRoot rev-parse HEAD).Trim()
$env:PIXEL_TART_SOURCE_COMMIT = $sourceCommit

if ([string]::IsNullOrWhiteSpace($OutputRoot)) {
    $OutputRoot = Join-Path $repoRoot 'artifacts\automated-dpi-review\2.0.4'
}

$screenshotsRoot = Join-Path $OutputRoot 'screenshots'
$metadataRoot = Join-Path $OutputRoot 'metadata'
$reportsRoot = Join-Path $OutputRoot 'reports'
$interactionRoot = Join-Path $OutputRoot 'interaction'
foreach ($path in @($OutputRoot, $screenshotsRoot, $metadataRoot, $reportsRoot, $interactionRoot)) {
    New-Item -ItemType Directory -Path $path -Force | Out-Null
}

if (-not $SkipBuild) {
    & $dotnet build $project -c Release -p:UiReviewBuild=true -p:Platform=x64 --no-restore
    if ($LASTEXITCODE -ne 0) { throw 'UI acceptance build failed.' }
}
if (-not (Test-Path -LiteralPath $executable)) { throw "UI acceptance executable not found: $executable" }

function New-IsolatedTestImages {
    param([string]$Directory)
    Add-Type -AssemblyName System.Drawing
    New-Item -ItemType Directory -Path $Directory -Force | Out-Null
    Get-ChildItem -LiteralPath $Directory -Filter 'DPI_TEST_*.png' -ErrorAction SilentlyContinue | Remove-Item -Force

    $definitions = @(
        @{ Name='DPI_TEST_01.png'; Width=1200; Height=800; Background=[System.Drawing.Color]::FromArgb(18,68,112); Accent=[System.Drawing.Color]::FromArgb(248,190,64); Label='LANDSCAPE 01' },
        @{ Name='DPI_TEST_02.png'; Width=800; Height=1200; Background=[System.Drawing.Color]::FromArgb(118,36,52); Accent=[System.Drawing.Color]::FromArgb(246,224,184); Label='PORTRAIT 02' },
        @{ Name='DPI_TEST_03.png'; Width=1000; Height=1000; Background=[System.Drawing.Color]::FromArgb(24,92,68); Accent=[System.Drawing.Color]::FromArgb(130,224,174); Label='SQUARE 03' },
        @{ Name='DPI_TEST_04.png'; Width=1400; Height=700; Background=[System.Drawing.Color]::FromArgb(64,42,104); Accent=[System.Drawing.Color]::FromArgb(240,142,212); Label='WIDE 04' }
    )
    foreach ($definition in $definitions) {
        $bitmap = New-Object System.Drawing.Bitmap($definition.Width, $definition.Height)
        $graphics = [System.Drawing.Graphics]::FromImage($bitmap)
        try {
            $graphics.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
            $graphics.Clear($definition.Background)
            $accentBrush = New-Object System.Drawing.SolidBrush($definition.Accent)
            $softBrush = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(85, $definition.Accent))
            $pen = New-Object System.Drawing.Pen($definition.Accent, [Math]::Max(8, [int]($definition.Width / 90)))
            $font = New-Object System.Drawing.Font('Arial', [Math]::Max(28, [int]($definition.Width / 24)), [System.Drawing.FontStyle]::Bold)
            try {
                $graphics.DrawLine($pen, 0, 0, $definition.Width, $definition.Height)
                $graphics.DrawLine($pen, $definition.Width, 0, 0, $definition.Height)
                for ($index = 0; $index -lt 5; $index++) {
                    $size = [int]([Math]::Min($definition.Width, $definition.Height) * (0.10 + $index * 0.05))
                    $x = [int](($index + 1) * $definition.Width / 7 - $size / 2)
                    $y = [int](($index % 2 + 1) * $definition.Height / 4 - $size / 2)
                    $graphics.FillEllipse($softBrush, $x, $y, $size, $size)
                    $graphics.DrawEllipse($pen, $x, $y, $size, $size)
                }
                $graphics.DrawString($definition.Label, $font, $accentBrush, 34, $definition.Height - $font.Height - 34)
            }
            finally {
                $font.Dispose(); $pen.Dispose(); $softBrush.Dispose(); $accentBrush.Dispose()
            }
            $bitmap.Save((Join-Path $Directory $definition.Name), [System.Drawing.Imaging.ImageFormat]::Png)
        }
        finally { $graphics.Dispose(); $bitmap.Dispose() }
    }
}

function Initialize-ReviewProfile {
    param([string]$DemoImages)
    New-Item -ItemType Directory -Path $reviewRoot -Force | Out-Null
    New-Item -ItemType Directory -Path (Join-Path $reviewRoot 'Projects') -Force | Out-Null
    New-IsolatedTestImages -Directory $DemoImages
    $settings = [ordered]@{
        Appearance = [ordered]@{ Theme = 2; SidebarCollapsed = $false }
        PinnedQuickTools = @('Workflow','PhotoOrganize','BatchCompress')
        QuickToolLayout = [ordered]@{ SchemaVersion='1.0'; OrderedToolIds=@('Workflow','PhotoOrganize','BatchCompress') }
        WindowWidth = 1600
        WindowHeight = 920
        WindowLeft = $null
        WindowTop = $null
        WindowMaximized = $false
        OnboardingLegacyUser = $true
        OnboardingUpgradeOfferShown = $true
    }
    $settings | ConvertTo-Json -Depth 30 | Set-Content -LiteralPath (Join-Path $reviewRoot 'settings.json') -Encoding UTF8
    $now = [DateTimeOffset]::UtcNow
    $projects = @([ordered]@{
        Id = [Guid]::NewGuid(); Name='[Automated DPI] Isolated Demo Project'; Status=1
        CreatedAt=$now.AddDays(-2).ToString('O'); UpdatedAt=$now.AddMinutes(-18).ToString('O'); CompletedAt=$null
        Category=2; OutputMode=0; OutputBaseDirectory=(Join-Path $reviewRoot 'Output'); OutputDirectory=(Join-Path $reviewRoot 'Output\Demo')
        SourceDirectories=@($DemoImages); SelectionInputs=@('DPI_TEST_01.png','DPI_TEST_02.png','DPI_TEST_03.png','DPI_TEST_04.png')
        CustomExtensions=@(); SelectionCount=4; MatchedFileCount=8; CopiedFileCount=0
        Summary='Automated logical DPI simulation data'; ExportReports=$false; ExportCsvReport=$true; ExportJsonReport=$false; ExportLogReport=$false
    })
    ConvertTo-Json -InputObject $projects -Depth 20 | Set-Content -LiteralPath (Join-Path $reviewRoot 'Projects\projects.json') -Encoding UTF8
}

function Update-ReviewSettings {
    param([string]$Theme, [bool]$Collapsed, [double]$Width, [double]$Height)
    $path = Join-Path $reviewRoot 'settings.json'
    $settings = Get-Content -LiteralPath $path -Raw | ConvertFrom-Json
    $settings.Appearance.Theme = if ($Theme -eq 'Dark') { 2 } else { 1 }
    $settings.Appearance.SidebarCollapsed = $Collapsed
    $settings.WindowWidth = $Width
    $settings.WindowHeight = $Height
    $settings | ConvertTo-Json -Depth 30 | Set-Content -LiteralPath $path -Encoding UTF8
}

$demoImages = Join-Path $reviewRoot 'DemoImages'
Initialize-ReviewProfile -DemoImages $demoImages
$sourceHashesBefore = Get-ChildItem -LiteralPath $demoImages -Filter 'DPI_TEST_*.png' | Sort-Object Name | ForEach-Object {
    [ordered]@{ File=$_.FullName; Name=$_.Name; Sha256=(Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash; Bytes=$_.Length }
}

$dpis = @(
    [ordered]@{ Label='125'; Dpi=120; Scale=1.25; LogicalWidth=2048.0; LogicalHeight=1152.0 },
    [ordered]@{ Label='150'; Dpi=144; Scale=1.50; LogicalWidth=(2560.0/1.5); LogicalHeight=960.0 },
    [ordered]@{ Label='200'; Dpi=192; Scale=2.00; LogicalWidth=1280.0; LogicalHeight=720.0 }
)
$scenarios = @(
    [ordered]@{ Index=1; State='WorkbenchDarkExpanded'; Slug='Workbench_Dark_Expanded'; Theme='Dark'; Collapsed=$false },
    [ordered]@{ Index=2; State='WorkbenchDarkCollapsed'; Slug='Workbench_Dark_Collapsed'; Theme='Dark'; Collapsed=$true },
    [ordered]@{ Index=3; State='WorkbenchLight'; Slug='Workbench_Light'; Theme='Light'; Collapsed=$false },
    [ordered]@{ Index=4; State='SettingsDialog'; Slug='Settings_Dark'; Theme='Dark'; Collapsed=$false },
    [ordered]@{ Index=5; State='ToolboxPopup'; Slug='Toolbox_Popup_Dark'; Theme='Dark'; Collapsed=$false },
    [ordered]@{ Index=6; State='ToolboxFullPage'; Slug='Toolbox_FullPage_Dark'; Theme='Dark'; Collapsed=$false },
    [ordered]@{ Index=7; State='QuickToolsManager'; Slug='QuickTools_Manager_Dark'; Theme='Dark'; Collapsed=$false },
    [ordered]@{ Index=8; State='OrganizeEmpty'; Slug='Organize_Empty_Dark'; Theme='Dark'; Collapsed=$false },
    [ordered]@{ Index=9; State='OrganizeGrouped'; Slug='Organize_Grouped_Dark'; Theme='Dark'; Collapsed=$false },
    [ordered]@{ Index=10; State='OrganizeManifest'; Slug='Organize_Manifest_Dark'; Theme='Dark'; Collapsed=$false },
    [ordered]@{ Index=11; State='CollageEmpty'; Slug='Collage_Empty_Dark'; Theme='Dark'; Collapsed=$false },
    [ordered]@{ Index=12; State='Collage2x2'; Slug='Collage_2x2_Dark'; Theme='Dark'; Collapsed=$false },
    [ordered]@{ Index=13; State='CollageVertical'; Slug='Collage_Vertical_Dark'; Theme='Dark'; Collapsed=$false },
    [ordered]@{ Index=14; State='CollageExport'; Slug='Collage_Export_Dark'; Theme='Dark'; Collapsed=$false },
    [ordered]@{ Index=15; State='FeedbackDialog'; Slug='Feedback_Dialog_Dark'; Theme='Dark'; Collapsed=$false },
    [ordered]@{ Index=16; State='ConfirmationDialog'; Slug='Confirmation_Dialog_Dark'; Theme='Dark'; Collapsed=$false },
    [ordered]@{ Index=17; State='ContextMenu'; Slug='ContextMenu_Dark'; Theme='Dark'; Collapsed=$false },
    [ordered]@{ Index=18; State='Tooltip'; Slug='Tooltip_Dark'; Theme='Dark'; Collapsed=$false }
)

$captureResults = [System.Collections.Generic.List[object]]::new()
foreach ($dpi in $dpis) {
    $screenshotDirectory = Join-Path $screenshotsRoot $dpi.Label
    $metadataDirectory = Join-Path $metadataRoot $dpi.Label
    New-Item -ItemType Directory -Path $screenshotDirectory -Force | Out-Null
    New-Item -ItemType Directory -Path $metadataDirectory -Force | Out-Null
    foreach ($scenario in $scenarios) {
        $fileName = '{0}_Automated_{1}.png' -f $dpi.Label, $scenario.Slug
        $outputPath = Join-Path $screenshotDirectory $fileName
        $metadataPath = Join-Path $metadataDirectory ($fileName + '.json')
        Remove-Item -LiteralPath $outputPath,$metadataPath -Force -ErrorAction SilentlyContinue
        Update-ReviewSettings -Theme $scenario.Theme -Collapsed ([bool]$scenario.Collapsed) -Width $dpi.LogicalWidth -Height $dpi.LogicalHeight
        $state = [ordered]@{
            State=$scenario.State; Theme=$scenario.Theme; Width=$dpi.LogicalWidth; Height=$dpi.LogicalHeight
            SidebarCollapsed=[bool]$scenario.Collapsed; OutputPath=$outputPath; MetadataPath=$metadataPath
            DpiScale=$dpi.Scale; DpiX=$dpi.Dpi; DpiY=$dpi.Dpi; PhysicalWidth=2560; PhysicalHeight=1440
        }
        $state | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $statePath -Encoding UTF8
        $process = Start-Process -FilePath $executable -PassThru -WindowStyle Normal
        $deadline = [DateTime]::UtcNow.AddSeconds(45)
        while ((-not (Test-Path -LiteralPath $outputPath) -or -not (Test-Path -LiteralPath $metadataPath)) -and [DateTime]::UtcNow -lt $deadline) {
            Start-Sleep -Milliseconds 250
        }
        if (-not $process.HasExited) { $process.CloseMainWindow() | Out-Null; Start-Sleep -Milliseconds 250 }
        if (-not $process.HasExited) { Stop-Process -Id $process.Id -Force }
        if (-not (Test-Path -LiteralPath $outputPath)) { throw "Screenshot capture failed: $fileName" }
        if (-not (Test-Path -LiteralPath $metadataPath)) { throw "Metadata capture failed: $fileName" }
        $metadata = Get-Content -LiteralPath $metadataPath -Raw | ConvertFrom-Json
        $file = Get-Item -LiteralPath $outputPath
        $captureResults.Add([ordered]@{
            DpiPercent=[int]$dpi.Label; Dpi=$dpi.Dpi; Scale=$dpi.Scale; Scenario=$scenario.State; Theme=$scenario.Theme
            Path=$file.FullName; Bytes=$file.Length; Sha256=(Get-FileHash -LiteralPath $file.FullName -Algorithm SHA256).Hash
            MetadataPath=$metadataPath; Passed=[bool]$metadata.passed; BlockingIssueCount=[int]$metadata.layout.BlockingIssueCount
        })
    }
}

$sourceHashesAfter = Get-ChildItem -LiteralPath $demoImages -Filter 'DPI_TEST_*.png' | Sort-Object Name | ForEach-Object {
    [ordered]@{ File=$_.FullName; Name=$_.Name; Sha256=(Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash; Bytes=$_.Length }
}
$sourceIntegrityPassed = $true
for ($index=0; $index -lt $sourceHashesBefore.Count; $index++) {
    if ($sourceHashesBefore[$index].Sha256 -ne $sourceHashesAfter[$index].Sha256) { $sourceIntegrityPassed = $false }
}

$captureResults | Export-Csv -LiteralPath (Join-Path $OutputRoot 'AutomatedDpiScreenshotHashes.csv') -NoTypeInformation -Encoding UTF8
$captureResults | ConvertTo-Json -Depth 10 | Set-Content -LiteralPath (Join-Path $OutputRoot 'AutomatedDpiScreenshotHashes.json') -Encoding UTF8
$allMetadata = Get-ChildItem -LiteralPath $metadataRoot -Filter '*.json' -Recurse | Sort-Object FullName | ForEach-Object { Get-Content -LiteralPath $_.FullName -Raw | ConvertFrom-Json }
$allMetadata | Select-Object scenario,theme,scale,physicalViewport,logicalViewport,layout,auxiliaryLayout,passed | ConvertTo-Json -Depth 20 | Set-Content -LiteralPath (Join-Path $OutputRoot 'LayoutBoundsResults.json') -Encoding UTF8
$allMetadata | Select-Object scenario,theme,scale,themeInspection | ConvertTo-Json -Depth 20 | Set-Content -LiteralPath (Join-Path $OutputRoot 'ThemeResults.json') -Encoding UTF8
[ordered]@{
    ValidationMode='automated-logical-simulation'; PhysicalDpiManuallyTested=$false
    AutomatedDpiCompatibilityPassed=($captureResults.Count -eq 54 -and ($captureResults | Where-Object { -not $_.Passed }).Count -eq 0)
    SourceCommit=$sourceCommit; PhysicalViewport=[ordered]@{ Width=2560; Height=1440 }
    DpiScalesTested=@(125,150,200); ExpectedScreenshotCount=54; ScreenshotCount=$captureResults.Count
    UniqueScreenshotHashes=($captureResults.Sha256 | Sort-Object -Unique).Count
    FailedScenarios=@($captureResults | Where-Object { -not $_.Passed } | Select-Object DpiPercent,Scenario,BlockingIssueCount)
    GeneratedAt=[DateTimeOffset]::Now.ToString('O')
} | ConvertTo-Json -Depth 12 | Set-Content -LiteralPath (Join-Path $OutputRoot 'AutomatedDpiResults.json') -Encoding UTF8
[ordered]@{
    Passed=$sourceIntegrityPassed; Before=$sourceHashesBefore; After=$sourceHashesAfter; GeneratedAt=[DateTimeOffset]::Now.ToString('O')
} | ConvertTo-Json -Depth 12 | Set-Content -LiteralPath (Join-Path $OutputRoot 'SourceFileIntegrity.json') -Encoding UTF8

$python = 'C:\Users\Administrator\.cache\codex-runtimes\codex-primary-runtime\dependencies\python\python.exe'
$contactSheetName = [Text.Encoding]::UTF8.GetString([Convert]::FromBase64String('5YOP57Sg6JuL5oyeXzIuMC40X+iHquWKqOWMlkRQSemqjOaUtuaAu+iniC5wbmc='))
$contactSheet = Join-Path $OutputRoot $contactSheetName
& $python (Join-Path $PSScriptRoot 'create_contact_sheet.py') --input $screenshotsRoot --output $contactSheet
if ($LASTEXITCODE -ne 0) { throw 'Contact sheet generation failed.' }

if (($captureResults.Sha256 | Sort-Object -Unique).Count -ne 54) { throw 'Automated DPI screenshots are not all unique.' }
if (($captureResults | Where-Object { -not $_.Passed }).Count -gt 0) { throw 'One or more automated DPI layout scenarios failed.' }
if (-not $sourceIntegrityPassed) { throw 'Source test images changed during DPI acceptance.' }

if (-not $KeepReviewProfile) { Remove-Item -LiteralPath $statePath -Force -ErrorAction SilentlyContinue }
Get-Content -LiteralPath (Join-Path $OutputRoot 'AutomatedDpiResults.json') -Raw
