param([string]$OutputRoot = '', [switch]$SkipBuild, [switch]$KeepReviewProfile)

$ErrorActionPreference = 'Stop'
function Decode([string]$Value) { [Text.Encoding]::UTF8.GetString([Convert]::FromBase64String($Value)) }
$repoRoot = Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
$dotnet = Join-Path (Split-Path $repoRoot -Parent) '.dotnet\dotnet.exe'
$project = Join-Path $repoRoot 'src\RAWSelectionAssistant\RAWSelectionAssistant.csproj'
$executable = Join-Path $repoRoot 'src\RAWSelectionAssistant\bin\x64\Release\net10.0-windows10.0.19041.0\win-x64\KitaoPhotoSelector.UiReview.exe'
$reviewRoot = Join-Path $env:LOCALAPPDATA 'KitaoPhotoSelector.UiReview'
$demoRoot = Join-Path $reviewRoot 'DemoImages'
$statePath = Join-Path $reviewRoot 'ui-review-state.json'
if ([string]::IsNullOrWhiteSpace($OutputRoot)) { $OutputRoot = Join-Path $repoRoot 'artifacts\ui-review\2.3.0-stage-e' }
$metadataRoot = Join-Path $OutputRoot 'metadata'
foreach ($path in @($OutputRoot, $metadataRoot, $reviewRoot, $demoRoot)) { New-Item -ItemType Directory -Path $path -Force | Out-Null }
if (-not $SkipBuild) { & $dotnet build $project -c Release -p:UiReviewBuild=true -p:Platform=x64 --no-restore; if ($LASTEXITCODE -ne 0) { throw 'Stage E UI review build failed.' } }
if (-not (Test-Path -LiteralPath $executable)) { throw "Stage E UI review executable not found: $executable" }

Add-Type -AssemblyName System.Drawing
$palette = @(
    @('#101722','#F1B950','#2B6E9D'), @('#2C1620','#FFBE8B','#8D3458'), @('#12312F','#8DE0CC','#DDA83B'),
    @('#272141','#C9A8F5','#4BB7D3'), @('#3F2D18','#F5D47A','#7FB6D9'), @('#102C42','#E8EDF0','#EB705A'),
    @('#33202F','#EAC8F1','#6AD2DD'), @('#19362A','#B8E0A6','#EBA36D'), @('#1E2632','#F3F5F7','#D75555'),
    @('#402122','#F6B0A4','#FFD269'), @('#153639','#94E3DF','#EDA62F'), @('#32213B','#E8C5F0','#6AD1DD')
)
for ($index = 0; $index -lt $palette.Count; $index++) {
    $width = if ($index % 4 -eq 1) { 900 } else { 1400 }; $height = if ($width -eq 900) { 1200 } else { 900 }
    $bitmap = New-Object System.Drawing.Bitmap($width, $height); $graphics = [System.Drawing.Graphics]::FromImage($bitmap)
    $background = [System.Drawing.ColorTranslator]::FromHtml($palette[$index][0]); $accent = [System.Drawing.ColorTranslator]::FromHtml($palette[$index][1]); $secondary = [System.Drawing.ColorTranslator]::FromHtml($palette[$index][2])
    $gradient = New-Object System.Drawing.Drawing2D.LinearGradientBrush((New-Object System.Drawing.Rectangle(0,0,$width,$height)),$background,$secondary,35)
    $accentBrush = New-Object System.Drawing.SolidBrush($accent); $secondaryBrush = New-Object System.Drawing.SolidBrush($secondary); $font = New-Object System.Drawing.Font('Segoe UI',42,[System.Drawing.FontStyle]::Bold)
    try { $graphics.SmoothingMode=[System.Drawing.Drawing2D.SmoothingMode]::AntiAlias; $graphics.FillRectangle($gradient,0,0,$width,$height); $graphics.FillEllipse($accentBrush,[int]($width*.17),[int]($height*.14),[int]($width*.36),[int]($height*.44)); $graphics.FillRectangle($secondaryBrush,[int]($width*.47),[int]($height*.37),[int]($width*.38),[int]($height*.36)); $graphics.DrawString(('STAGE E / {0:00}' -f ($index+1)),$font,$accentBrush,38,$height-105); $bitmap.Save((Join-Path $demoRoot ('STAGEC_{0:00}.png' -f ($index+1))),[System.Drawing.Imaging.ImageFormat]::Png) }
    finally { $font.Dispose(); $secondaryBrush.Dispose(); $accentBrush.Dispose(); $gradient.Dispose(); $graphics.Dispose(); $bitmap.Dispose() }
}
[System.IO.File]::WriteAllBytes((Join-Path $demoRoot 'STAGEC_RAW.nef'),[byte[]](0x49,0x49,0x2A,0x00,0x08,0x00,0x00,0x00))
@'
TITLE "Pixel Tart Stage E 1D"
LUT_1D_SIZE 2
0 0 0
1 0.94 0.82
'@ | Set-Content -LiteralPath (Join-Path $demoRoot 'STAGEE_1D.cube') -Encoding UTF8
@'
TITLE "Pixel Tart Stage E 3D"
LUT_3D_SIZE 2
0 0 0
1 0.05 0.02
0.03 1 0.02
1 1 0.08
0.02 0.04 1
1 0.08 1
0.06 1 1
1 0.94 0.82
'@ | Set-Content -LiteralPath (Join-Path $demoRoot 'STAGEE_3D.cube') -Encoding UTF8

@{ Appearance=@{Theme=2;SidebarCollapsed=$true};PinnedQuickTools=@('Workflow','PhotoOrganize','BatchCompress');QuickToolLayout=@{SchemaVersion='1.0';OrderedToolIds=@('Workflow','PhotoOrganize','BatchCompress')};WindowWidth=1600;WindowHeight=920;OnboardingLegacyUser=$true;OnboardingUpgradeOfferShown=$true } | ConvertTo-Json -Depth 20 | Set-Content -LiteralPath (Join-Path $reviewRoot 'settings.json') -Encoding UTF8
$scenarios = @(
    @('01_TetherMonitor_1600x920.png','TetherAssets','Dark',1600,920,1.0,1600,920),
    @('02_TetherMonitor_1920x1080.png','TetherAssets','Dark',1920,1080,1.0,1920,1080),
    @('03_TetherMonitor_Compact1280.png','TetherCompact1280Closed','Dark',1280,820,1.0,1280,820),
    @('04_TetherMonitor_Assets1000.png','TetherAssets1000','Dark',1600,920,1.0,1600,920),
    @('05_TetherMonitor_Burst.png','TetherBurst','Dark',1600,920,1.0,1600,920),
    @('06_TetherMonitor_NeedsAttention.png','TetherNeedsAttention','Dark',1600,920,1.0,1600,920),
    @('07_TetherMonitor_DirectoryDisconnected.png','TetherDirectoryDisconnected','Dark',1600,920,1.0,1600,920),
    @('08_TetherMonitor_DirectoryRecovered.png','TetherDirectoryRecovered','Dark',1600,920,1.0,1600,920),
    @('09_Lut_1D.png','Lut1D','Dark',1600,920,1.0,1600,920), @('10_Lut_3D.png','Lut3D','Dark',1600,920,1.0,1600,920),
    @('11_Lut_Strength50.png','LutStrength50','Dark',1600,920,1.0,1600,920), @('12_Lut_BeforeAfter.png','LutBeforeAfter','Dark',1600,920,1.0,1600,920),
    @('13_Lut_Split.png','LutSplitView','Dark',1600,920,1.0,1600,920), @('14_Lut_InvalidFallback.png','LutInvalid','Dark',1600,920,1.0,1600,920),
    @('15_Icc_Detected.png','ColorProfileDetected','Dark',1600,920,1.0,1600,920), @('16_Icc_Fallback.png','ColorProfileFallback','Dark',1600,920,1.0,1600,920),
    @('17_ClientMonitor_FollowMain.png','ClientMonitorFollowMain','Dark',1600,920,1.0,1600,920), @('18_ClientMonitor_FollowLatest.png','ClientMonitorFollowLatest','Dark',1600,920,1.0,1600,920),
    @('19_ClientMonitor_Locked.png','ClientMonitorLocked','Dark',1600,920,1.0,1600,920), @('20_ClientMonitor_Privacy.png','ClientMonitorPrivacy','Dark',1600,920,1.0,1600,920),
    @('21_ClientMonitor_Disconnected.png','ClientMonitorDisconnected','Dark',1600,920,1.0,1600,920), @('22_ClientMonitor_Reconnected.png','ClientMonitorReconnected','Dark',1600,920,1.0,1600,920),
    @('23_Annotations.png','TetherAnnotations','Dark',1600,920,1.0,1600,920), @('24_Compare.png','TetherSideBySide','Dark',1600,920,1.0,1600,920),
    @('25_Fullscreen.png','TetherFullscreen','Dark',1600,920,1.0,1600,920), @('26_DarkTheme.png','TetherDark','Dark',1600,920,1.0,1600,920),
    @('27_LightTheme.png','TetherLight','Light',1600,920,1.0,1600,920), @('28_HighContrast.png','TetherHighContrast','HighContrast',1600,920,1.0,1600,920),
    @('29_Dpi150.png','TetherDpi150','Dark',1600,920,1.5,2400,1380), @('30_Dpi200.png','TetherDpi200','Dark',1280,820,2.0,2560,1640),
    @('31_Settings_Color.png','Settings','Dark',1600,920,1.0,1600,920), @('32_TaskCenter_TetherCopy.png','TetherTaskCenter','Dark',1600,920,1.0,1600,920)
)
$sourceBefore = Get-ChildItem $demoRoot -Filter 'STAGE*' | Sort-Object Name | ForEach-Object { @{Name=$_.Name;Bytes=$_.Length;Sha256=(Get-FileHash $_.FullName -Algorithm SHA256).Hash} }
$results = [System.Collections.Generic.List[object]]::new()
foreach ($scenario in $scenarios) {
    $file,$state,$theme,$width,$height,$scale,$physicalWidth,$physicalHeight = $scenario
    $output = Join-Path $OutputRoot $file; $metadata = Join-Path $metadataRoot ($file + '.json')
    Remove-Item -LiteralPath $output,$metadata -Force -ErrorAction SilentlyContinue
    @{State=$state;Theme=$theme;Width=$width;Height=$height;SidebarCollapsed=$true;OutputPath=$output;MetadataPath=$metadata;DpiScale=$scale;DpiX=[int](96*$scale);DpiY=[int](96*$scale);PhysicalWidth=$physicalWidth;PhysicalHeight=$physicalHeight} | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $statePath -Encoding UTF8
    $process=Start-Process -FilePath $executable -PassThru -WindowStyle Hidden; $deadline=[DateTime]::UtcNow.AddSeconds(60)
    while(((-not(Test-Path -LiteralPath $output))-or(-not(Test-Path -LiteralPath $metadata)))-and [DateTime]::UtcNow -lt $deadline){Start-Sleep -Milliseconds 250}
    if(-not $process.HasExited){$process.CloseMainWindow()|Out-Null;Start-Sleep -Milliseconds 300};if(-not $process.HasExited){Stop-Process -Id $process.Id -Force}
    if(-not(Test-Path -LiteralPath $output)){throw "Screenshot capture failed: $file"};if(-not(Test-Path -LiteralPath $metadata)){throw "Screenshot metadata failed: $file"}
    $meta=Get-Content $metadata -Raw -Encoding UTF8|ConvertFrom-Json;$results.Add(@{File=$file;State=$state;Theme=$theme;DpiScale=$scale;Bytes=(Get-Item $output).Length;Sha256=(Get-FileHash $output -Algorithm SHA256).Hash;LayoutPassed=[bool]$meta.passed;BlockingIssueCount=[int]$meta.layout.BlockingIssueCount})
}
$sourceAfter = Get-ChildItem $demoRoot -Filter 'STAGE*' | Sort-Object Name | ForEach-Object { @{Name=$_.Name;Bytes=$_.Length;Sha256=(Get-FileHash $_.FullName -Algorithm SHA256).Hash} }
$sourceIntegrity = ($sourceBefore|ConvertTo-Json -Compress) -eq ($sourceAfter|ConvertTo-Json -Compress)
$evidence=@{EvidenceType='real-wpf-render-target-capture';SourceCommit=(& git -C $repoRoot rev-parse HEAD).Trim();ExpectedScreenshotCount=32;ScreenshotCount=$results.Count;UniqueScreenshotHashes=($results.Sha256|Sort-Object -Unique).Count;SourceFilesUnchanged=$sourceIntegrity;PhysicalSecondMonitorTested=$false;ValidationScope='automated topology, mixed DPI and independent WPF window; one physical monitor detected';GeneratedAt=[DateTimeOffset]::Now.ToString('O');Screenshots=$results}
$evidence|ConvertTo-Json -Depth 12|Set-Content (Join-Path $OutputRoot 'evidence-index.json') -Encoding UTF8; @{Passed=$sourceIntegrity;Before=$sourceBefore;After=$sourceAfter}|ConvertTo-Json -Depth 10|Set-Content (Join-Path $OutputRoot 'source-integrity.json') -Encoding UTF8
$python='<USERPROFILE>\.cache\codex-runtimes\codex-primary-runtime\dependencies\python\python.exe';$sheet=Join-Path $OutputRoot (Decode '5YOP57Sg6JuL5oyeXzIuMy4w6Zi25q61ReacgOe7iFVJ6aqM5pS25oC76KeILnBuZw==');& $python (Join-Path $PSScriptRoot 'create_contact_sheet.py') --input $OutputRoot --output $sheet;if($LASTEXITCODE-ne 0){throw 'Stage E contact sheet failed.'}
if($results.Count-ne 32){throw 'Expected 32 Stage E screenshots.'};if(($results.Sha256|Sort-Object -Unique).Count-ne 32){throw 'Stage E screenshots must be unique.'};if(-not $sourceIntegrity){throw 'Stage E source assets changed.'};if(($results|Where-Object{-not $_.LayoutPassed}).Count-gt 0){throw 'Stage E layout metadata contains failures.'}
if(-not $KeepReviewProfile){Remove-Item -LiteralPath $statePath -Force -ErrorAction SilentlyContinue};$evidence|ConvertTo-Json -Depth 4
