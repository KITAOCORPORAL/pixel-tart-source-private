param(
    [string]$OutputRoot = '',
    [switch]$SkipBuild,
    [switch]$KeepReviewProfile,
    [switch]$ReplaceExisting
)

$ErrorActionPreference = 'Stop'
function Decode([string]$Value) { [Text.Encoding]::UTF8.GetString([Convert]::FromBase64String($Value)) }
$repoRoot = Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
$dotnet = Join-Path (Split-Path $repoRoot -Parent) '.dotnet\dotnet.exe'
if (-not (Test-Path -LiteralPath $dotnet)) { $dotnet = (Get-Command dotnet -ErrorAction Stop).Source }
$project = Join-Path $repoRoot 'src\RAWSelectionAssistant\RAWSelectionAssistant.csproj'
$executable = Join-Path $repoRoot 'src\RAWSelectionAssistant\bin\x64\Release\net10.0-windows10.0.19041.0\win-x64\KitaoPhotoSelector.UiReview.exe'
$reviewRoot = Join-Path $env:LOCALAPPDATA 'KitaoPhotoSelector.UiReview'
$demoRoot = Join-Path $reviewRoot 'DemoImages'
$statePath = Join-Path $reviewRoot 'ui-review-state.json'
if ([string]::IsNullOrWhiteSpace($OutputRoot)) { $OutputRoot = Join-Path $repoRoot 'artifacts\ui-review\2.3.0-rc3' }
$metadataRoot = Join-Path $OutputRoot 'metadata'

if (Test-Path -LiteralPath $OutputRoot) {
    $existingFrames = @(Get-ChildItem -LiteralPath $OutputRoot -Filter '*.png' -File -ErrorAction SilentlyContinue)
    if ($existingFrames.Count -gt 0 -and -not $ReplaceExisting) { throw "RC3 UI evidence already exists; refusing to overwrite: $OutputRoot" }
    if ($ReplaceExisting) {
        $expectedRoot = [IO.Path]::GetFullPath((Join-Path $repoRoot 'artifacts\ui-review\2.3.0-rc3'))
        $resolvedRoot = [IO.Path]::GetFullPath($OutputRoot)
        if (-not [string]::Equals($expectedRoot, $resolvedRoot, [StringComparison]::OrdinalIgnoreCase)) { throw "Refusing to replace evidence outside the exact RC3 review directory: $resolvedRoot" }
        $existingFrames | Remove-Item -Force
        if (Test-Path -LiteralPath $metadataRoot) { Get-ChildItem -LiteralPath $metadataRoot -File | Remove-Item -Force }
        foreach ($name in @('evidence-index.json','source-integrity.json','source-assets-sha256.json','interaction-log.jsonl')) {
            $path = Join-Path $OutputRoot $name
            if (Test-Path -LiteralPath $path) { Remove-Item -LiteralPath $path -Force }
        }
    }
}
foreach ($path in @($OutputRoot, $metadataRoot, $reviewRoot, $demoRoot)) { New-Item -ItemType Directory -Path $path -Force | Out-Null }

if (-not $SkipBuild) {
    & $dotnet build $project -c Release -p:UiReviewBuild=true -p:Platform=x64 --no-restore --nologo
    if ($LASTEXITCODE -ne 0) { throw 'RC3 UI review build failed.' }
}
if (-not (Test-Path -LiteralPath $executable)) { throw "RC3 UI review executable not found: $executable" }

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
        $graphics.DrawString(('RC3 / {0:00}' -f ($index+1)),$font,$accentBrush,38,$height-105)
        $bitmap.Save((Join-Path $demoRoot ('STAGEC_{0:00}.png' -f ($index+1))),[System.Drawing.Imaging.ImageFormat]::Png)
        $bitmap.Save((Join-Path $demoRoot ('DPI_TEST_{0:00}.png' -f ($index+1))),[System.Drawing.Imaging.ImageFormat]::Png)
        if ($index -eq 0) { $bitmap.Save((Join-Path $demoRoot (Decode 'UkMzX+i1hOaWmeWbvueJhy5wbmc=')),[System.Drawing.Imaging.ImageFormat]::Png) }
    }
    finally { $font.Dispose(); $secondaryBrush.Dispose(); $accentBrush.Dispose(); $gradient.Dispose(); $graphics.Dispose(); $bitmap.Dispose() }
}
[IO.File]::WriteAllBytes((Join-Path $demoRoot 'STAGEC_RAW.nef'),[byte[]](0x49,0x49,0x2A,0x00,0x08,0x00,0x00,0x00))
[IO.File]::WriteAllBytes((Join-Path $demoRoot (Decode 'UkMzX+aLjeaRhOetluWIki5wZGY=')),[Text.Encoding]::ASCII.GetBytes("%PDF-1.4`n% RC3 synthetic review document`n%%EOF"))
[IO.File]::WriteAllText((Join-Path $demoRoot (Decode 'UkMzX+eOsOWcuuivtOaYji50eHQ=')),"RC3 synthetic shooting notes for isolated UI review.",[Text.UTF8Encoding]::new($false))
[IO.File]::WriteAllText((Join-Path $demoRoot (Decode 'UkMzX+aKpeS7t+WPguiAgy5kb2N4')),"RC3 synthetic Office placeholder",[Text.UTF8Encoding]::new($false))
[IO.File]::WriteAllText((Join-Path $demoRoot (Decode 'UkMzX+acquefpeagvOW8jy54eXo=')),"RC3 unsupported format placeholder",[Text.UTF8Encoding]::new($false))

@{
    Appearance = @{ Theme = 2; SidebarCollapsed = $false }
    PinnedQuickTools = @('Workflow','PhotoOrganize','BatchCompress')
    QuickToolLayout = @{ SchemaVersion = '1.0'; OrderedToolIds = @('Workflow','PhotoOrganize','BatchCompress') }
    WindowWidth = 1600; WindowHeight = 920; OnboardingLegacyUser = $true; OnboardingUpgradeOfferShown = $true
} | ConvertTo-Json -Depth 20 | Set-Content -LiteralPath (Join-Path $reviewRoot 'settings.json') -Encoding UTF8

$scenarios = @(
    @('01_Workbench_Legend.png','WorkbenchLegend','Dark',1600,920,1.0,1600,920,$false),
    @('02_Workbench_PinStates.png','WorkbenchPinStates','Dark',1600,920,1.0,1600,920,$false),
    @('03_LocalSplit_Help.png','LocalSplitHelp','Dark',1440,900,1.0,1440,900,$false),
    @('04_LocalSplit_Hover.png','LocalSplitHover','Dark',1440,900,1.0,1440,900,$false),
    @('05_DatePicker_Dark.png','DatePickerDark','Dark',1440,900,1.0,1440,900,$false),
    @('06_DatePicker_Light.png','DatePickerLight','Light',1440,900,1.0,1440,900,$false),
    @('07_DatePicker_HighContrast.png','DatePickerHighContrast','HighContrast',1440,900,1.0,1440,900,$false),
    @('08_CalendarPopup_Dark.png','CalendarPopupDark','Dark',1366,900,1.0,1366,900,$false),
    @('09_Calendar_FullLayout.png','CalendarFullLayout','Dark',1600,920,1.0,1600,920,$false),
    @('10_Calendar_ViewMenu.png','CalendarViewMenu','Dark',1600,920,1.0,1600,920,$false),
    @('11_Calendar_StatusLegend.png','CalendarStatusLegend','Dark',1440,900,1.0,1440,900,$false),
    @('12_Calendar_EmptyDates.png','CalendarEmptyDates','Dark',1600,920,1.0,1600,920,$false),
    @('13_Calendar_SelectedDay.png','CalendarSelectedDay','Dark',1440,900,1.0,1440,900,$false),
    @('14_Calendar_ContextMenu.png','CalendarContextMenu','Dark',1440,900,1.0,1440,900,$false),
    @('15_Calendar_DayClosed.png','CalendarDayClosed','Dark',1440,900,1.0,1440,900,$false),
    @('16_Calendar_DayDetails.png','CalendarDayDetails','Dark',1600,920,1.0,1600,920,$false),
    @('17_CreateShoot_Step1.png','CreateShootStep1','Dark',1600,920,1.0,1600,920,$false),
    @('18_CreateShoot_Step2.png','CreateShootStep2','Dark',1600,920,1.0,1600,920,$false),
    @('19_CreateShoot_Step3.png','CreateShootStep3','Dark',1600,920,1.0,1600,920,$false),
    @('20_CreateShoot_Step4.png','CreateShootStep4','Dark',1600,920,1.0,1600,920,$false),
    @('21_CreateShoot_Contacts.png','CreateShootContacts','Dark',1600,920,1.0,1600,920,$false),
    @('22_CreateShoot_Staff.png','CreateShootStaff','Dark',1600,920,1.0,1600,920,$false),
    @('23_Documents_Images.png','DocumentsImages','Dark',1600,920,1.0,1600,920,$false),
    @('24_Documents_Pdf.png','DocumentsPdf','Dark',1440,900,1.0,1440,900,$false),
    @('25_Documents_Text.png','DocumentsText','Dark',1600,920,1.0,1600,920,$false),
    @('26_Documents_Unsupported.png','DocumentsUnsupported','Dark',1600,920,1.0,1600,920,$false),
    @('27_Finance_Dashboard.png','FinanceDashboard','Dark',1600,920,1.0,1600,920,$false),
    @('28_Finance_Income.png','FinanceIncome','Dark',1600,920,1.0,1600,920,$false),
    @('29_Finance_Expense.png','FinanceExpense','Dark',1600,920,1.0,1600,920,$false),
    @('30_Finance_ProjectSummary.png','FinanceProjectSummary','Dark',1600,920,1.0,1600,920,$false),
    @('31_Finance_Filters.png','FinanceFilters','Dark',1440,900,1.0,1440,900,$false),
    @('32_Tether_Empty.png','TetherEmpty','Dark',1600,920,1.0,1600,920,$false),
    @('33_Tether_Waiting.png','TetherWaiting','Dark',1600,920,1.0,1600,920,$false),
    @('34_Tether_Ready.png','TetherReady','Dark',1600,920,1.0,1600,920,$false),
    @('35_Tether_CompactToolbar.png','TetherCompactToolbar','Dark',1280,820,1.0,1280,820,$false),
    @('36_Tether_NoPhotoFullscreen.png','TetherNoPhotoFullscreen','Dark',1920,1080,1.0,1920,1080,$false),
    @('37_Toast_Dark.png','ToastDark','Dark',1600,920,1.0,1600,920,$false),
    @('38_ComboBox_Dark.png','ComboBoxDark','Dark',1440,900,1.0,1440,900,$false),
    @('39_ContextMenu_Dark.png','ContextMenuDark','Dark',1440,900,1.0,1440,900,$false),
    @('40_Workbench_1280.png','Workbench1280','Dark',1280,820,1.0,1280,820,$false),
    @('41_Calendar_150Dpi.png','CalendarFullLayout','Dark',1600,920,1.5,2400,1380,$false),
    @('42_Calendar_200Dpi.png','CalendarFullLayout','Dark',1280,820,2.0,2560,1640,$false),
    @('43_LightTheme.png','WorkbenchLight','Light',1600,920,1.0,1600,920,$false),
    @('44_HighContrast.png','WorkbenchHighContrast','HighContrast',1600,920,1.0,1600,920,$false)
)

$sourceBefore = Get-ChildItem $demoRoot -File | Sort-Object Name | ForEach-Object {
    [ordered]@{ Name=$_.Name; Bytes=$_.Length; Sha256=(Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash }
}
$sourceBefore | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $OutputRoot 'source-assets-sha256.json') -Encoding UTF8
$results = [Collections.Generic.List[object]]::new()
$interactionPath = Join-Path $OutputRoot 'interaction-log.jsonl'
foreach ($scenario in $scenarios) {
    $file,$state,$theme,$width,$height,$scale,$physicalWidth,$physicalHeight,$collapsed = $scenario
    $output = Join-Path $OutputRoot $file
    $metadata = Join-Path $metadataRoot ($file + '.json')
    @{
        State=$state; Theme=$theme; Width=$width; Height=$height; SidebarCollapsed=$collapsed
        OutputPath=$output; MetadataPath=$metadata; DpiScale=$scale; DpiX=[int](96*$scale); DpiY=[int](96*$scale)
        PhysicalWidth=$physicalWidth; PhysicalHeight=$physicalHeight
    } | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $statePath -Encoding UTF8
    $started = [DateTimeOffset]::Now
    $process = Start-Process -FilePath $executable -PassThru -WindowStyle Hidden
    $deadline = [DateTime]::UtcNow.AddSeconds(75)
    while (((-not (Test-Path -LiteralPath $output)) -or (-not (Test-Path -LiteralPath $metadata))) -and [DateTime]::UtcNow -lt $deadline) { Start-Sleep -Milliseconds 200 }
    if (-not $process.HasExited) { $process.CloseMainWindow() | Out-Null; Start-Sleep -Milliseconds 250 }
    if (-not $process.HasExited) { Stop-Process -Id $process.Id -Force }
    if (-not (Test-Path -LiteralPath $output)) { throw "Screenshot capture failed: $file" }
    if (-not (Test-Path -LiteralPath $metadata)) { throw "Screenshot metadata failed: $file" }
    $meta = Get-Content -LiteralPath $metadata -Raw -Encoding UTF8 | ConvertFrom-Json
    $row = [ordered]@{
        File=$file; State=$state; Theme=$theme; DpiScale=$scale; Bytes=(Get-Item -LiteralPath $output).Length
        Sha256=(Get-FileHash -LiteralPath $output -Algorithm SHA256).Hash
        LayoutPassed=[bool]$meta.passed; BlockingIssueCount=[int]$meta.layout.BlockingIssueCount
    }
    $results.Add($row)
    [ordered]@{
        Timestamp=$started.ToString('O'); ProcessId=$process.Id; Application='KitaoPhotoSelector.UiReview.exe'; Scenario=$state
        Page=$meta.scenario; FocusTarget=$meta.layout.FocusTarget; FocusVisible=[bool]$meta.layout.FocusVisible
        PopupExpected=($state -match 'Popup|Menu|Help|Toast|ComboBox|ContextMenu'); Screenshot=$file
        Result=if ($meta.passed) { 'Passed' } else { 'Failed' }; Exception=$null
    } | ConvertTo-Json -Compress | Add-Content -LiteralPath $interactionPath -Encoding UTF8
}

$sourceAfter = Get-ChildItem $demoRoot -File | Sort-Object Name | ForEach-Object {
    [ordered]@{ Name=$_.Name; Bytes=$_.Length; Sha256=(Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash }
}
$sourceIntegrity = ($sourceBefore | ConvertTo-Json -Compress) -eq ($sourceAfter | ConvertTo-Json -Compress)
$uniqueHashes = ($results.Sha256 | Sort-Object -Unique).Count
$evidence = [ordered]@{
    EvidenceType='real-wpf-runtime-render-and-ui-automation-trace'; IsolatedProfile=$reviewRoot
    SourceCommit=(& git -C $repoRoot rev-parse HEAD).Trim(); ExpectedScreenshotCount=44; ScreenshotCount=$results.Count
    UniqueScreenshotHashes=$uniqueHashes; SourceFilesUnchanged=$sourceIntegrity; PhysicalSecondMonitorTested=$false
    ValidationScope='RC3 runtime theme, professional calendar, booking documents, local finance and tether empty-state acceptance on one physical monitor'
    RecordingGenerated=$false; RecordingReason='No MP4 was fabricated; sequential WPF screenshots, metadata and UI automation traces are provided.'
    GeneratedAt=[DateTimeOffset]::Now.ToString('O'); Screenshots=$results
}
$evidence | ConvertTo-Json -Depth 12 | Set-Content -LiteralPath (Join-Path $OutputRoot 'evidence-index.json') -Encoding UTF8
@{ Passed=$sourceIntegrity; Before=$sourceBefore; After=$sourceAfter } | ConvertTo-Json -Depth 10 | Set-Content -LiteralPath (Join-Path $OutputRoot 'source-integrity.json') -Encoding UTF8

$python = 'C:\Users\Administrator\.cache\codex-runtimes\codex-primary-runtime\dependencies\python\python.exe'
if (-not (Test-Path -LiteralPath $python)) { $python = (Get-Command python -ErrorAction Stop).Source }
$sheet = Join-Path $OutputRoot (Decode '5YOP57Sg6JuL5oyeXzIuMy4wX1JDM+aXpeWOhui1hOaWmeS4juaUtuaUr1VJ5oC76KeILnBuZw==')
& $python (Join-Path $PSScriptRoot 'create_contact_sheet.py') --input $OutputRoot --output $sheet
if ($LASTEXITCODE -ne 0) { throw 'RC3 contact sheet failed.' }
if ($results.Count -ne 44) { throw 'Expected 44 RC3 screenshots.' }
if ($uniqueHashes -ne 44) { throw "RC3 screenshots must have unique hashes; found $uniqueHashes." }
if (-not $sourceIntegrity) { throw 'RC3 synthetic source assets changed during capture.' }
if (($results | Where-Object { -not $_.LayoutPassed }).Count -gt 0) { throw 'RC3 layout metadata contains blocking failures.' }
if (-not $KeepReviewProfile) { Remove-Item -LiteralPath $statePath -Force -ErrorAction SilentlyContinue }
$evidence | ConvertTo-Json -Depth 4
