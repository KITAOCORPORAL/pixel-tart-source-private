param([string]$OutputRoot = '', [switch]$SkipBuild, [switch]$KeepReviewProfile, [switch]$ReplaceExisting)

$ErrorActionPreference = 'Stop'
function Decode([string]$Value) { [Text.Encoding]::UTF8.GetString([Convert]::FromBase64String($Value)) }
$repoRoot = Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
$dotnet = Join-Path (Split-Path $repoRoot -Parent) '.dotnet\dotnet.exe'
$project = Join-Path $repoRoot 'src\RAWSelectionAssistant\RAWSelectionAssistant.csproj'
$executable = Join-Path $repoRoot 'src\RAWSelectionAssistant\bin\x64\Release\net10.0-windows10.0.19041.0\win-x64\KitaoPhotoSelector.UiReview.exe'
$reviewRoot = Join-Path $env:LOCALAPPDATA 'KitaoPhotoSelector.UiReview'
$demoRoot = Join-Path $reviewRoot 'DemoImages'
$statePath = Join-Path $reviewRoot 'ui-review-state.json'
if ([string]::IsNullOrWhiteSpace($OutputRoot)) { $OutputRoot = Join-Path $repoRoot 'artifacts\ui-review\2.3.0-rc2' }
$metadataRoot = Join-Path $OutputRoot 'metadata'

if (Test-Path -LiteralPath $OutputRoot) {
    $existingFrames = @(Get-ChildItem -LiteralPath $OutputRoot -Filter '*.png' -File -ErrorAction SilentlyContinue)
    if ($existingFrames.Count -gt 0 -and -not $ReplaceExisting) { throw "RC2 UI evidence already contains screenshots; refusing to overwrite: $OutputRoot" }
    if ($ReplaceExisting) {
        $expectedRoot = [IO.Path]::GetFullPath((Join-Path $repoRoot 'artifacts\ui-review\2.3.0-rc2'))
        $resolvedRoot = [IO.Path]::GetFullPath($OutputRoot)
        if (-not [string]::Equals($expectedRoot, $resolvedRoot, [StringComparison]::OrdinalIgnoreCase)) { throw "Refusing to replace evidence outside the exact RC2 review directory: $resolvedRoot" }
        $existingFrames | Remove-Item -Force
        if (Test-Path -LiteralPath $metadataRoot) { Get-ChildItem -LiteralPath $metadataRoot -Filter '*.json' -File | Remove-Item -Force }
        foreach ($name in @('evidence-index.json','source-integrity.json','source-assets-sha256.json')) {
            $path = Join-Path $OutputRoot $name
            if (Test-Path -LiteralPath $path) { Remove-Item -LiteralPath $path -Force }
        }
    }
}
foreach ($path in @($OutputRoot, $metadataRoot, $reviewRoot, $demoRoot)) { New-Item -ItemType Directory -Path $path -Force | Out-Null }
if (-not $SkipBuild) {
    & $dotnet build $project -c Release -p:UiReviewBuild=true -p:Platform=x64 --no-restore --nologo
    if ($LASTEXITCODE -ne 0) { throw 'RC2 UI review build failed.' }
}
if (-not (Test-Path -LiteralPath $executable)) { throw "RC2 UI review executable not found: $executable" }

Add-Type -AssemblyName System.Drawing
$palette = @(
    @('#0F172A','#F2B84B','#2563EB'), @('#291827','#F5A3C7','#7C3AED'), @('#12302C','#88DDC4','#E4A33A'),
    @('#25203D','#D3B5F6','#38BDF8'), @('#3B2A18','#F5D16F','#60A5FA'), @('#0E2A3D','#E2E8F0','#F97360'),
    @('#30202E','#EBC8F1','#22D3EE'), @('#173429','#B7E2A5','#FB923C'), @('#1E293B','#F8FAFC','#EF4444'),
    @('#3B2020','#FDB4A8','#FACC15'), @('#123438','#99E5DF','#F59E0B'), @('#302039','#E9C8F0','#67E8F9')
)
for ($index = 0; $index -lt $palette.Count; $index++) {
    $width = if ($index % 4 -eq 1) { 900 } else { 1400 }
    $height = if ($width -eq 900) { 1200 } else { 900 }
    $bitmap = New-Object System.Drawing.Bitmap($width, $height)
    $graphics = [System.Drawing.Graphics]::FromImage($bitmap)
    $background = [System.Drawing.ColorTranslator]::FromHtml($palette[$index][0])
    $accent = [System.Drawing.ColorTranslator]::FromHtml($palette[$index][1])
    $secondary = [System.Drawing.ColorTranslator]::FromHtml($palette[$index][2])
    $gradient = New-Object System.Drawing.Drawing2D.LinearGradientBrush((New-Object System.Drawing.Rectangle(0,0,$width,$height)),$background,$secondary,35)
    $accentBrush = New-Object System.Drawing.SolidBrush($accent)
    $secondaryBrush = New-Object System.Drawing.SolidBrush($secondary)
    $font = New-Object System.Drawing.Font('Segoe UI',42,[System.Drawing.FontStyle]::Bold)
    try {
        $graphics.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
        $graphics.FillRectangle($gradient,0,0,$width,$height)
        $graphics.FillEllipse($accentBrush,[int]($width*.17),[int]($height*.14),[int]($width*.36),[int]($height*.44))
        $graphics.FillRectangle($secondaryBrush,[int]($width*.47),[int]($height*.37),[int]($width*.38),[int]($height*.36))
        $graphics.DrawString(('RC2 / {0:00}' -f ($index+1)),$font,$accentBrush,38,$height-105)
        $stagePath = Join-Path $demoRoot ('STAGEC_{0:00}.png' -f ($index+1))
        $dpiPath = Join-Path $demoRoot ('DPI_TEST_{0:00}.png' -f ($index+1))
        $bitmap.Save($stagePath,[System.Drawing.Imaging.ImageFormat]::Png)
        $bitmap.Save($dpiPath,[System.Drawing.Imaging.ImageFormat]::Png)
    }
    finally { $font.Dispose(); $secondaryBrush.Dispose(); $accentBrush.Dispose(); $gradient.Dispose(); $graphics.Dispose(); $bitmap.Dispose() }
}
[System.IO.File]::WriteAllBytes((Join-Path $demoRoot 'STAGEC_RAW.nef'),[byte[]](0x49,0x49,0x2A,0x00,0x08,0x00,0x00,0x00))

@{
    Appearance = @{ Theme = 2; SidebarCollapsed = $false }
    PinnedQuickTools = @('Workflow','PhotoOrganize','BatchCompress')
    QuickToolLayout = @{ SchemaVersion = '1.0'; OrderedToolIds = @('Workflow','PhotoOrganize','BatchCompress') }
    WindowWidth = 1600; WindowHeight = 920; OnboardingLegacyUser = $true; OnboardingUpgradeOfferShown = $true
} | ConvertTo-Json -Depth 20 | Set-Content -LiteralPath (Join-Path $reviewRoot 'settings.json') -Encoding UTF8

$scenarios = @(
    @('01_Workbench_Default.png','WorkbenchDarkExpanded','Dark',1600,920,1.0,1600,920,$false),
    @('02_Workbench_CalendarRightTop.png','WorkbenchCalendarRightTop','Dark',1440,900,1.0,1440,900,$false),
    @('03_Calendar_EmptyMonth.png','CalendarEmptyMonth','Dark',1600,920,1.0,1600,920,$false),
    @('04_Calendar_StatusColors.png','CalendarStatusColors','Dark',1600,920,1.0,1600,920,$false),
    @('05_Calendar_SelectedDay.png','CalendarSelectedDay','Dark',1440,900,1.0,1440,900,$false),
    @('06_Calendar_CreateButton.png','CalendarCreateButton','Dark',1366,900,1.0,1366,900,$false),
    @('07_CreateShoot_Basic.png','CreateShootBasic','Dark',1600,920,1.0,1600,920,$false),
    @('08_CreateShoot_TimeLocation.png','CreateShootTimeLocation','Dark',1600,920,1.0,1600,920,$false),
    @('09_CreateShoot_Weather.png','CreateShootWeather','Dark',1600,920,1.0,1600,920,$false),
    @('10_CreateShoot_Documents.png','CreateShootDocuments','Dark',1600,920,1.0,1600,920,$false),
    @('11_CreateShoot_Staff.png','CreateShootStaff','Dark',1600,920,1.0,1600,920,$false),
    @('12_CreateShoot_Conflict.png','CreateShootConflict','Dark',1600,920,1.0,1600,920,$false),
    @('13_CreateShoot_Saved.png','CreateShootSaved','Dark',1600,920,1.0,1600,920,$false),
    @('14_Calendar_DayDetails.png','CalendarDayDetails','Dark',1600,920,1.0,1600,920,$false),
    @('15_Calendar_Completed.png','CalendarCompleted','Dark',1440,900,1.0,1440,900,$false),
    @('16_Calendar_Archived.png','CalendarArchived','Dark',1366,900,1.0,1366,900,$false),
    @('17_Tether_Empty.png','TetherEmpty','Dark',1600,920,1.0,1600,920,$false),
    @('18_Tether_Active.png','TetherAssets','Dark',1600,920,1.0,1600,920,$false),
    @('19_Tether_Browser.png','TetherBrowser','Dark',1600,920,1.0,1600,920,$false),
    @('20_Tether_Viewer.png','TetherViewer','Dark',1600,920,1.0,1600,920,$false),
    @('21_Tether_Inspector.png','TetherInspector','Dark',1600,920,1.0,1600,920,$false),
    @('22_Tether_Histogram.png','TetherHistogram','Dark',1600,920,1.0,1600,920,$false),
    @('23_Tether_ExifCollapsed.png','TetherExifCollapsed','Dark',1600,920,1.0,1600,920,$false),
    @('24_Tether_LutCollapsed.png','TetherLutCollapsed','Dark',1600,920,1.0,1600,920,$false),
    @('25_Tether_ClientSingleMonitor.png','TetherClientSingleMonitor','Dark',1600,920,1.0,1600,920,$false),
    @('26_Tether_TaskDrawer.png','TetherTaskCenter','Dark',1600,920,1.0,1600,920,$false),
    @('27_Tether_Compact1280.png','TetherCompact1280Closed','Dark',1280,820,1.0,1280,820,$false),
    @('28_Tether_1600x920.png','Tether1600','Dark',1600,920,1.0,1600,920,$false),
    @('29_Tether_1920x1080.png','Tether1920','Dark',1920,1080,1.0,1920,1080,$false),
    @('30_Collection_NoOverlap.png','CollectionNoOverlap','Dark',1440,900,1.0,1440,900,$false),
    @('31_Organize_NoOverlap.png','OrganizeNoOverlap','Dark',1440,900,1.0,1440,900,$false),
    @('32_Compress_NoOverlap.png','CompressNoOverlap','Dark',1440,900,1.0,1440,900,$false),
    @('33_Watermark_NoOverlap.png','WatermarkNoOverlap','Dark',1440,900,1.0,1440,900,$false),
    @('34_License_NoOverlap.png','LicenseNoOverlap','Dark',1440,900,1.0,1440,900,$false),
    @('35_Toolbox_ClosedAfterSelection.png','ToolboxClosedAfterSelection','Dark',1440,900,1.0,1440,900,$false),
    @('36_Settings_CloseButton.png','SettingsDialog','Dark',1440,900,1.0,1440,900,$false),
    @('37_DarkTheme.png','WorkbenchDarkExpanded','Dark',1920,1080,1.0,1920,1080,$false),
    @('38_LightTheme.png','WorkbenchLight','Light',1600,920,1.0,1600,920,$false),
    @('39_HighContrast.png','WorkbenchHighContrast','HighContrast',1600,920,1.0,1600,920,$false),
    @('40_Dpi150.png','WorkbenchDpi150','Dark',1600,920,1.5,2400,1380,$false),
    @('41_Dpi200.png','WorkbenchDpi200','Dark',1280,820,2.0,2560,1640,$false),
    @('42_SidebarCollapsed.png','WorkbenchDarkCollapsed','Dark',1600,920,1.0,1600,920,$true)
)

$sourceBefore = Get-ChildItem $demoRoot -File | Where-Object { $_.Name -like 'STAGEC_*' -or $_.Name -like 'DPI_TEST_*' } | Sort-Object Name | ForEach-Object {
    [ordered]@{ Name=$_.Name; Bytes=$_.Length; Sha256=(Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash }
}
$sourceBefore | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $OutputRoot 'source-assets-sha256.json') -Encoding UTF8
$results = [System.Collections.Generic.List[object]]::new()
foreach ($scenario in $scenarios) {
    $file,$state,$theme,$width,$height,$scale,$physicalWidth,$physicalHeight,$collapsed = $scenario
    $output = Join-Path $OutputRoot $file
    $metadata = Join-Path $metadataRoot ($file + '.json')
    @{
        State=$state; Theme=$theme; Width=$width; Height=$height; SidebarCollapsed=$collapsed
        OutputPath=$output; MetadataPath=$metadata; DpiScale=$scale; DpiX=[int](96*$scale); DpiY=[int](96*$scale)
        PhysicalWidth=$physicalWidth; PhysicalHeight=$physicalHeight
    } | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $statePath -Encoding UTF8
    $process = Start-Process -FilePath $executable -PassThru -WindowStyle Hidden
    $deadline = [DateTime]::UtcNow.AddSeconds(75)
    while (((-not (Test-Path -LiteralPath $output)) -or (-not (Test-Path -LiteralPath $metadata))) -and [DateTime]::UtcNow -lt $deadline) { Start-Sleep -Milliseconds 250 }
    if (-not $process.HasExited) { $process.CloseMainWindow() | Out-Null; Start-Sleep -Milliseconds 300 }
    if (-not $process.HasExited) { Stop-Process -Id $process.Id -Force }
    if (-not (Test-Path -LiteralPath $output)) { throw "Screenshot capture failed: $file" }
    if (-not (Test-Path -LiteralPath $metadata)) { throw "Screenshot metadata failed: $file" }
    $meta = Get-Content -LiteralPath $metadata -Raw -Encoding UTF8 | ConvertFrom-Json
    $results.Add([ordered]@{
        File=$file; State=$state; Theme=$theme; DpiScale=$scale; Bytes=(Get-Item -LiteralPath $output).Length
        Sha256=(Get-FileHash -LiteralPath $output -Algorithm SHA256).Hash
        LayoutPassed=[bool]$meta.passed; BlockingIssueCount=[int]$meta.layout.BlockingIssueCount
    })
}
$sourceAfter = Get-ChildItem $demoRoot -File | Where-Object { $_.Name -like 'STAGEC_*' -or $_.Name -like 'DPI_TEST_*' } | Sort-Object Name | ForEach-Object {
    [ordered]@{ Name=$_.Name; Bytes=$_.Length; Sha256=(Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash }
}
$sourceIntegrity = ($sourceBefore | ConvertTo-Json -Compress) -eq ($sourceAfter | ConvertTo-Json -Compress)
$evidence = [ordered]@{
    EvidenceType='real-wpf-render-target-capture'; IsolatedProfile=$reviewRoot
    SourceCommit=(& git -C $repoRoot rev-parse HEAD).Trim(); ExpectedScreenshotCount=42; ScreenshotCount=$results.Count
    UniqueScreenshotHashes=($results.Sha256 | Sort-Object -Unique).Count; SourceFilesUnchanged=$sourceIntegrity
    PhysicalSecondMonitorTested=$false; ValidationScope='RC2 navigation, calendar, tether, responsive layout and theme acceptance on one physical monitor'
    RecordingGenerated=$false; RecordingReason='The isolated RenderTarget evidence path is deterministic; no safe desktop recording was required.'
    GeneratedAt=[DateTimeOffset]::Now.ToString('O'); Screenshots=$results
}
$evidence | ConvertTo-Json -Depth 12 | Set-Content -LiteralPath (Join-Path $OutputRoot 'evidence-index.json') -Encoding UTF8
@{ Passed=$sourceIntegrity; Before=$sourceBefore; After=$sourceAfter } | ConvertTo-Json -Depth 10 | Set-Content -LiteralPath (Join-Path $OutputRoot 'source-integrity.json') -Encoding UTF8

$python = '<USERPROFILE>\.cache\codex-runtimes\codex-primary-runtime\dependencies\python\python.exe'
$sheet = Join-Path $OutputRoot (Decode '5YOP57Sg6JuL5oyeXzIuMy4wX1JDMuWvvOiIquaXpeWOhuS4juiBlOacuuebkeeci1VJ5oC76KeILnBuZw==')
& $python (Join-Path $PSScriptRoot 'create_contact_sheet.py') --input $OutputRoot --output $sheet
if ($LASTEXITCODE -ne 0) { throw 'RC2 contact sheet failed.' }
if ($results.Count -ne 42) { throw 'Expected 42 RC2 screenshots.' }
if (($results.Sha256 | Sort-Object -Unique).Count -ne 42) { throw 'RC2 screenshots must have unique hashes.' }
if (-not $sourceIntegrity) { throw 'RC2 synthetic source assets changed during capture.' }
if (($results | Where-Object { -not $_.LayoutPassed }).Count -gt 0) { throw 'RC2 layout metadata contains blocking failures.' }
if (-not $KeepReviewProfile) { Remove-Item -LiteralPath $statePath -Force -ErrorAction SilentlyContinue }
$evidence | ConvertTo-Json -Depth 4
