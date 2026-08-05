param(
    [string]$OutputRoot = '',
    [switch]$SkipBuild,
    [switch]$KeepReviewProfile
)

$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
$workspaceRoot = Split-Path $repoRoot -Parent
$dotnet = Join-Path $workspaceRoot '.dotnet\dotnet.exe'
if (-not (Test-Path -LiteralPath $dotnet)) { throw "Bundled .NET SDK not found: $dotnet" }
$project = Join-Path $repoRoot 'src\RAWSelectionAssistant\RAWSelectionAssistant.csproj'
$executable = Join-Path $repoRoot 'src\RAWSelectionAssistant\bin\x64\Release\net10.0-windows10.0.19041.0\win-x64\KitaoPhotoSelector.UiReview.exe'
$reviewRoot = Join-Path $env:LOCALAPPDATA 'KitaoPhotoSelector.UiReview'
$demoRoot = Join-Path $reviewRoot 'DemoImages'
$statePath = Join-Path $reviewRoot 'ui-review-state.json'
if ([string]::IsNullOrWhiteSpace($OutputRoot)) { $OutputRoot = Join-Path $repoRoot 'artifacts\ui-review\2.3.0-stage-c' }
$metadataRoot = Join-Path $OutputRoot 'metadata'

foreach ($path in @($OutputRoot, $metadataRoot, $reviewRoot, $demoRoot)) {
    New-Item -ItemType Directory -Path $path -Force | Out-Null
}

if (-not $SkipBuild) {
    & $dotnet build $project -c Release -p:UiReviewBuild=true -p:Platform=x64 --no-restore
    if ($LASTEXITCODE -ne 0) { throw 'Stage C UI review build failed.' }
}
if (-not (Test-Path -LiteralPath $executable)) { throw "Stage C UI review executable not found: $executable" }

function New-StageCTestImages {
    param([string]$Directory)
    Add-Type -AssemblyName System.Drawing
    $definitions = @(
        @{ Bg='#191F2B'; A='#E7B35A'; B='#6AA6D9'; Label='KEY LIGHT'; Shape='Portrait' },
        @{ Bg='#3B1E2B'; A='#F7D7B5'; B='#C96771'; Label='BEAUTY CLOSEUP'; Shape='Portrait' },
        @{ Bg='#173A33'; A='#A8E0C0'; B='#D9A441'; Label='WARDROBE'; Shape='Landscape' },
        @{ Bg='#252047'; A='#D5B5F3'; B='#63C5DA'; Label='COLOR STUDY'; Shape='Landscape' },
        @{ Bg='#44331F'; A='#F2CF75'; B='#A7C7E7'; Label='BACK LIGHT'; Shape='Portrait' },
        @{ Bg='#132B43'; A='#F0ECE2'; B='#F27C62'; Label='WIDE FRAME'; Shape='Landscape' },
        @{ Bg='#2C2C2C'; A='#FFFFFF'; B='#050505'; Label='CLIPPING CHECK'; Shape='Landscape' },
        @{ Bg='#4A2020'; A='#F6B5A9'; B='#FED766'; Label='NEEDS ATTENTION'; Shape='Portrait' },
        @{ Bg='#163538'; A='#9CE5E2'; B='#F3A712'; Label='REFERENCE'; Shape='Landscape' },
        @{ Bg='#35223D'; A='#E8C7F1'; B='#73D2DE'; Label='COMPARE A'; Shape='Portrait' },
        @{ Bg='#203A24'; A='#D1F0C2'; B='#E7A977'; Label='COMPARE B'; Shape='Landscape' },
        @{ Bg='#202631'; A='#CED7E0'; B='#E26D5A'; Label='FINAL SELECT'; Shape='Landscape' }
    )
    for ($index = 0; $index -lt $definitions.Count; $index++) {
        $definition = $definitions[$index]
        $width = if ($definition.Shape -eq 'Portrait') { 900 } else { 1400 }
        $height = if ($definition.Shape -eq 'Portrait') { 1200 } else { 900 }
        $path = Join-Path $Directory ('STAGEC_{0:00}.png' -f ($index + 1))
        $bitmap = New-Object System.Drawing.Bitmap($width, $height)
        $graphics = [System.Drawing.Graphics]::FromImage($bitmap)
        try {
            $graphics.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
            $background = [System.Drawing.ColorTranslator]::FromHtml($definition.Bg)
            $accent = [System.Drawing.ColorTranslator]::FromHtml($definition.A)
            $secondary = [System.Drawing.ColorTranslator]::FromHtml($definition.B)
            $gradient = New-Object System.Drawing.Drawing2D.LinearGradientBrush(
                (New-Object System.Drawing.Rectangle(0, 0, $width, $height)), $background, $secondary, 35)
            $graphics.FillRectangle($gradient, 0, 0, $width, $height)
            $accentBrush = New-Object System.Drawing.SolidBrush($accent)
            $secondaryBrush = New-Object System.Drawing.SolidBrush($secondary)
            $softBrush = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(70, $accent))
            $pen = New-Object System.Drawing.Pen($accent, 12)
            $font = New-Object System.Drawing.Font('Segoe UI', 42, [System.Drawing.FontStyle]::Bold)
            $smallFont = New-Object System.Drawing.Font('Segoe UI', 20, [System.Drawing.FontStyle]::Regular)
            try {
                $graphics.FillEllipse($softBrush, [int]($width * .14), [int]($height * .10), [int]($width * .72), [int]($height * .72))
                $graphics.FillEllipse($accentBrush, [int]($width * .39), [int]($height * .20), [int]($width * .22), [int]($height * .27))
                $graphics.FillRectangle($secondaryBrush, [int]($width * .31), [int]($height * .48), [int]($width * .38), [int]($height * .32))
                $graphics.DrawLine($pen, 0, [int]($height * .78), $width, [int]($height * .32))
                $graphics.DrawString($definition.Label, $font, $accentBrush, 38, $height - 112)
                $graphics.DrawString(('STAGE C / {0:00}' -f ($index + 1)), $smallFont, $secondaryBrush, 42, 34)
            }
            finally {
                $smallFont.Dispose(); $font.Dispose(); $pen.Dispose(); $softBrush.Dispose(); $secondaryBrush.Dispose(); $accentBrush.Dispose(); $gradient.Dispose()
            }
            $bitmap.Save($path, [System.Drawing.Imaging.ImageFormat]::Png)
        }
        finally { $graphics.Dispose(); $bitmap.Dispose() }
    }
    [System.IO.File]::WriteAllBytes((Join-Path $Directory 'STAGEC_RAW.nef'), [byte[]](0x49,0x49,0x2A,0x00,0x08,0x00,0x00,0x00))
}

function Initialize-ReviewProfile {
    New-StageCTestImages -Directory $demoRoot
    $settings = [ordered]@{
        Appearance = [ordered]@{ Theme = 2; SidebarCollapsed = $true }
        PinnedQuickTools = @('Workflow','PhotoOrganize','BatchCompress')
        QuickToolLayout = [ordered]@{ SchemaVersion='1.0'; OrderedToolIds=@('Workflow','PhotoOrganize','BatchCompress') }
        WindowWidth = 1600; WindowHeight = 920; WindowLeft = $null; WindowTop = $null; WindowMaximized = $false
        OnboardingLegacyUser = $true; OnboardingUpgradeOfferShown = $true
    }
    $settings | ConvertTo-Json -Depth 30 | Set-Content -LiteralPath (Join-Path $reviewRoot 'settings.json') -Encoding UTF8
}

$scenarios = @(
    @{ File='01_TetherMonitor_Empty.png'; State='TetherEmpty'; Theme='Dark'; Width=1600; Height=920; Scale=1.0; PhysicalWidth=1600; PhysicalHeight=920 },
    @{ File='02_TetherMonitor_WithAssets.png'; State='TetherAssets'; Theme='Dark'; Width=1600; Height=920; Scale=1.0; PhysicalWidth=1600; PhysicalHeight=920 },
    @{ File='03_AutoLatest.png'; State='TetherAutoLatest'; Theme='Dark'; Width=1600; Height=920; Scale=1.0; PhysicalWidth=1600; PhysicalHeight=920 },
    @{ File='04_CurrentLocked.png'; State='TetherLocked'; Theme='Dark'; Width=1600; Height=920; Scale=1.0; PhysicalWidth=1600; PhysicalHeight=920 },
    @{ File='05_ExifHistogram.png'; State='TetherExifHistogram'; Theme='Dark'; Width=1600; Height=920; Scale=1.0; PhysicalWidth=1600; PhysicalHeight=920 },
    @{ File='06_HighlightShadowWarning.png'; State='TetherWarnings'; Theme='Dark'; Width=1600; Height=920; Scale=1.0; PhysicalWidth=1600; PhysicalHeight=920 },
    @{ File='07_SideBySideCompare.png'; State='TetherSideBySide'; Theme='Dark'; Width=1600; Height=920; Scale=1.0; PhysicalWidth=1600; PhysicalHeight=920 },
    @{ File='08_OverlayCompare.png'; State='TetherOverlayCompare'; Theme='Dark'; Width=1600; Height=920; Scale=1.0; PhysicalWidth=1600; PhysicalHeight=920 },
    @{ File='09_ReferenceOverlay.png'; State='TetherReference'; Theme='Dark'; Width=1600; Height=920; Scale=1.0; PhysicalWidth=1600; PhysicalHeight=920 },
    @{ File='10_GridGuides.png'; State='TetherGuides'; Theme='Dark'; Width=1600; Height=920; Scale=1.0; PhysicalWidth=1600; PhysicalHeight=920 },
    @{ File='11_Annotations.png'; State='TetherAnnotations'; Theme='Dark'; Width=1600; Height=920; Scale=1.0; PhysicalWidth=1600; PhysicalHeight=920 },
    @{ File='12_Fullscreen.png'; State='TetherFullscreen'; Theme='Dark'; Width=1600; Height=920; Scale=1.0; PhysicalWidth=1920; PhysicalHeight=1080 },
    @{ File='13_DarkTheme.png'; State='TetherDark'; Theme='Dark'; Width=1600; Height=920; Scale=1.0; PhysicalWidth=1600; PhysicalHeight=920 },
    @{ File='14_LightTheme.png'; State='TetherLight'; Theme='Light'; Width=1600; Height=920; Scale=1.0; PhysicalWidth=1600; PhysicalHeight=920 },
    @{ File='15_HighContrast.png'; State='TetherHighContrast'; Theme='HighContrast'; Width=1600; Height=920; Scale=1.0; PhysicalWidth=1600; PhysicalHeight=920 },
    @{ File='16_Compact1280.png'; State='TetherCompact1280'; Theme='Dark'; Width=1280; Height=820; Scale=1.0; PhysicalWidth=1280; PhysicalHeight=820 },
    @{ File='17_Dpi150.png'; State='TetherDpi150'; Theme='Dark'; Width=1600; Height=920; Scale=1.5; PhysicalWidth=2400; PhysicalHeight=1380 },
    @{ File='18_RawPlaceholder.png'; State='TetherRawPlaceholder'; Theme='Dark'; Width=1600; Height=920; Scale=1.0; PhysicalWidth=1600; PhysicalHeight=920 }
)

Initialize-ReviewProfile
$sourceBefore = Get-ChildItem -LiteralPath $demoRoot -Filter 'STAGEC_*' | Sort-Object Name | ForEach-Object {
    [ordered]@{ Name=$_.Name; Bytes=$_.Length; Sha256=(Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash }
}
$results = [System.Collections.Generic.List[object]]::new()
foreach ($scenario in $scenarios) {
    $outputPath = Join-Path $OutputRoot $scenario.File
    $metadataPath = Join-Path $metadataRoot ($scenario.File + '.json')
    Remove-Item -LiteralPath $outputPath,$metadataPath -Force -ErrorAction SilentlyContinue
    $state = [ordered]@{
        State=$scenario.State; Theme=$scenario.Theme; Width=$scenario.Width; Height=$scenario.Height
        SidebarCollapsed=$true; OutputPath=$outputPath; MetadataPath=$metadataPath
        DpiScale=$scenario.Scale; DpiX=[int](96 * $scenario.Scale); DpiY=[int](96 * $scenario.Scale)
        PhysicalWidth=$scenario.PhysicalWidth; PhysicalHeight=$scenario.PhysicalHeight
    }
    $state | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $statePath -Encoding UTF8
    $process = Start-Process -FilePath $executable -PassThru -WindowStyle Hidden
    $deadline = [DateTime]::UtcNow.AddSeconds(45)
    while ((-not (Test-Path -LiteralPath $outputPath) -or -not (Test-Path -LiteralPath $metadataPath)) -and [DateTime]::UtcNow -lt $deadline) {
        Start-Sleep -Milliseconds 250
    }
    if (-not $process.HasExited) { $process.CloseMainWindow() | Out-Null; Start-Sleep -Milliseconds 300 }
    if (-not $process.HasExited) { Stop-Process -Id $process.Id -Force }
    if (-not (Test-Path -LiteralPath $outputPath)) { throw "Screenshot capture failed: $($scenario.File)" }
    if (-not (Test-Path -LiteralPath $metadataPath)) { throw "Screenshot metadata failed: $($scenario.File)" }
    $file = Get-Item -LiteralPath $outputPath
    $metadata = Get-Content -LiteralPath $metadataPath -Raw | ConvertFrom-Json
    $results.Add([ordered]@{
        File=$scenario.File; State=$scenario.State; Theme=$scenario.Theme; Width=$scenario.Width; Height=$scenario.Height
        DpiScale=$scenario.Scale; Bytes=$file.Length; Sha256=(Get-FileHash -LiteralPath $file.FullName -Algorithm SHA256).Hash
        LayoutPassed=[bool]$metadata.passed; BlockingIssueCount=[int]$metadata.layout.BlockingIssueCount
    })
}

$sourceAfter = Get-ChildItem -LiteralPath $demoRoot -Filter 'STAGEC_*' | Sort-Object Name | ForEach-Object {
    [ordered]@{ Name=$_.Name; Bytes=$_.Length; Sha256=(Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash }
}
$sourceIntegrity = ($sourceBefore | ConvertTo-Json -Compress) -eq ($sourceAfter | ConvertTo-Json -Compress)
$evidence = [ordered]@{
    EvidenceType='real-wpf-render-target-capture'; IsolatedProfile=$reviewRoot; SourceCommit=(& git -C $repoRoot rev-parse HEAD).Trim()
    ExpectedScreenshotCount=18; ScreenshotCount=$results.Count; UniqueScreenshotHashes=($results.Sha256 | Sort-Object -Unique).Count
    SourceFilesUnchanged=$sourceIntegrity; GeneratedAt=[DateTimeOffset]::Now.ToString('O'); Screenshots=$results
}
$evidence | ConvertTo-Json -Depth 12 | Set-Content -LiteralPath (Join-Path $OutputRoot 'evidence-index.json') -Encoding UTF8
[ordered]@{ Passed=$sourceIntegrity; Before=$sourceBefore; After=$sourceAfter } | ConvertTo-Json -Depth 10 | Set-Content -LiteralPath (Join-Path $OutputRoot 'source-integrity.json') -Encoding UTF8

$python = 'C:\Users\Administrator\.cache\codex-runtimes\codex-primary-runtime\dependencies\python\python.exe'
if (-not (Test-Path -LiteralPath $python)) { throw "Bundled Python not found: $python" }
$contactSheetName = [Text.Encoding]::UTF8.GetString([Convert]::FromBase64String('5YOP57Sg6JuL5oyeXzIuMy4w6Zi25q61Q+eOsOWcuuebkeeci1VJ5oC76KeILnBuZw=='))
$contactSheet = Join-Path $OutputRoot $contactSheetName
& $python (Join-Path $PSScriptRoot 'create_contact_sheet.py') --input $OutputRoot --output $contactSheet
if ($LASTEXITCODE -ne 0) { throw 'Stage C contact sheet generation failed.' }
if ($results.Count -ne 18) { throw 'Expected 18 Stage C screenshots.' }
if (($results.Sha256 | Sort-Object -Unique).Count -ne 18) { throw 'Stage C screenshots must be unique.' }
if (-not $sourceIntegrity) { throw 'Stage C isolated source files changed during review.' }
if (-not $KeepReviewProfile) { Remove-Item -LiteralPath $statePath -Force -ErrorAction SilentlyContinue }
$evidence | ConvertTo-Json -Depth 4
