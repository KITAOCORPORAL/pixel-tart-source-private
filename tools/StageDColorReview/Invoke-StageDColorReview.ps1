param([string]$OutputRoot = '', [switch]$SkipBuild, [switch]$KeepReviewProfile)

$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
$workspaceRoot = Split-Path $repoRoot -Parent
$dotnet = Join-Path $workspaceRoot '.dotnet\dotnet.exe'
$project = Join-Path $repoRoot 'src\RAWSelectionAssistant\RAWSelectionAssistant.csproj'
$executable = Join-Path $repoRoot 'src\RAWSelectionAssistant\bin\x64\Release\net10.0-windows10.0.19041.0\win-x64\KitaoPhotoSelector.UiReview.exe'
$reviewRoot = Join-Path $env:LOCALAPPDATA 'KitaoPhotoSelector.UiReview'
$demoRoot = Join-Path $reviewRoot 'DemoImages'
$statePath = Join-Path $reviewRoot 'ui-review-state.json'
if ([string]::IsNullOrWhiteSpace($OutputRoot)) { $OutputRoot = Join-Path $repoRoot 'artifacts\ui-review\2.3.0-stage-d' }
$metadataRoot = Join-Path $OutputRoot 'metadata'
foreach ($path in @($OutputRoot, $metadataRoot, $reviewRoot, $demoRoot)) { New-Item -ItemType Directory -Path $path -Force | Out-Null }
if (-not $SkipBuild) { & $dotnet build $project -c Release -p:UiReviewBuild=true -p:Platform=x64 --no-restore; if ($LASTEXITCODE -ne 0) { throw 'Stage D UI review build failed.' } }
if (-not (Test-Path -LiteralPath $executable)) { throw "Stage D UI review executable not found: $executable" }

function New-StageDAssets {
    param([string]$Directory)
    Add-Type -AssemblyName System.Drawing
    $definitions = @(
        @{ Bg='#101722'; A='#F1B950'; B='#2B6E9D'; Label='LUT ORIGINAL'; Shape='Landscape' },
        @{ Bg='#2C1620'; A='#FFBE8B'; B='#8D3458'; Label='WARM PREVIEW'; Shape='Portrait' },
        @{ Bg='#12312F'; A='#8DE0CC'; B='#DDA83B'; Label='CLIENT SELECT'; Shape='Landscape' },
        @{ Bg='#272141'; A='#C9A8F5'; B='#4BB7D3'; Label='DISPLAY P3'; Shape='Landscape' },
        @{ Bg='#3F2D18'; A='#F5D47A'; B='#7FB6D9'; Label='ICC CHECK'; Shape='Portrait' },
        @{ Bg='#102C42'; A='#E8EDF0'; B='#EB705A'; Label='FOLLOW LATEST'; Shape='Landscape' },
        @{ Bg='#33202F'; A='#EAC8F1'; B='#6AD2DD'; Label='CLIENT NOTE'; Shape='Portrait' },
        @{ Bg='#19362A'; A='#B8E0A6'; B='#EBA36D'; Label='LOCKED FRAME'; Shape='Landscape' },
        @{ Bg='#1E2632'; A='#F3F5F7'; B='#D75555'; Label='FALLBACK sRGB'; Shape='Landscape' },
        @{ Bg='#402122'; A='#F6B0A4'; B='#FFD269'; Label='DISCONNECTED'; Shape='Portrait' },
        @{ Bg='#153639'; A='#94E3DF'; B='#EDA62F'; Label='RECONNECTED'; Shape='Landscape' },
        @{ Bg='#32213B'; A='#E8C5F0'; B='#6AD1DD'; Label='FINAL MONITOR'; Shape='Landscape' }
    )
    for ($index = 0; $index -lt $definitions.Count; $index++) {
        $definition = $definitions[$index]; $width = if ($definition.Shape -eq 'Portrait') { 900 } else { 1400 }; $height = if ($definition.Shape -eq 'Portrait') { 1200 } else { 900 }
        $path = Join-Path $Directory ('STAGEC_{0:00}.png' -f ($index + 1)); $bitmap = New-Object System.Drawing.Bitmap($width, $height); $graphics = [System.Drawing.Graphics]::FromImage($bitmap)
        try {
            $graphics.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias; $background = [System.Drawing.ColorTranslator]::FromHtml($definition.Bg); $accent = [System.Drawing.ColorTranslator]::FromHtml($definition.A); $secondary = [System.Drawing.ColorTranslator]::FromHtml($definition.B)
            $gradient = New-Object System.Drawing.Drawing2D.LinearGradientBrush((New-Object System.Drawing.Rectangle(0,0,$width,$height)),$background,$secondary,38); $accentBrush = New-Object System.Drawing.SolidBrush($accent); $secondaryBrush = New-Object System.Drawing.SolidBrush($secondary); $softBrush = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(70,$accent)); $pen = New-Object System.Drawing.Pen($accent,12); $font = New-Object System.Drawing.Font('Segoe UI',42,[System.Drawing.FontStyle]::Bold); $small = New-Object System.Drawing.Font('Segoe UI',20,[System.Drawing.FontStyle]::Regular)
            try { $graphics.FillRectangle($gradient,0,0,$width,$height); $graphics.FillEllipse($softBrush,[int]($width*.13),[int]($height*.11),[int]($width*.74),[int]($height*.72)); $graphics.FillEllipse($accentBrush,[int]($width*.38),[int]($height*.18),[int]($width*.24),[int]($height*.28)); $graphics.FillRectangle($secondaryBrush,[int]($width*.29),[int]($height*.47),[int]($width*.42),[int]($height*.34)); $graphics.DrawLine($pen,0,[int]($height*.8),$width,[int]($height*.3)); $graphics.DrawString($definition.Label,$font,$accentBrush,38,$height-112); $graphics.DrawString(('STAGE D / {0:00}' -f ($index+1)),$small,$secondaryBrush,42,34) }
            finally { $small.Dispose(); $font.Dispose(); $pen.Dispose(); $softBrush.Dispose(); $secondaryBrush.Dispose(); $accentBrush.Dispose(); $gradient.Dispose() }
            $bitmap.Save($path,[System.Drawing.Imaging.ImageFormat]::Png)
        }
        finally { $graphics.Dispose(); $bitmap.Dispose() }
    }
    [System.IO.File]::WriteAllBytes((Join-Path $Directory 'STAGEC_RAW.nef'),[byte[]](0x49,0x49,0x2A,0x00,0x08,0x00,0x00,0x00))
    @'
TITLE "Pixel Tart Stage D Warm"
LUT_3D_SIZE 2
DOMAIN_MIN 0 0 0
DOMAIN_MAX 1 1 1
0 0 0
1 0.05 0.02
0.03 1 0.02
1 1 0.08
0.02 0.04 1
1 0.08 1
0.06 1 1
1 0.94 0.82
'@ | Set-Content -LiteralPath (Join-Path $Directory 'STAGED_WARM.cube') -Encoding UTF8
}

New-StageDAssets -Directory $demoRoot
$settings = [ordered]@{ Appearance=[ordered]@{Theme=2;SidebarCollapsed=$true};PinnedQuickTools=@('Workflow','PhotoOrganize','BatchCompress');QuickToolLayout=[ordered]@{SchemaVersion='1.0';OrderedToolIds=@('Workflow','PhotoOrganize','BatchCompress')};WindowWidth=1600;WindowHeight=920;OnboardingLegacyUser=$true;OnboardingUpgradeOfferShown=$true }
$settings | ConvertTo-Json -Depth 20 | Set-Content -LiteralPath (Join-Path $reviewRoot 'settings.json') -Encoding UTF8
$scenarios = @(
    @{File='01_Lut_None.png';State='LutNone';Theme='Dark';Width=1600;Height=920;Scale=1.0;PW=1600;PH=920},
    @{File='02_Lut_Imported.png';State='LutImported';Theme='Dark';Width=1600;Height=920;Scale=1.0;PW=1600;PH=920},
    @{File='03_Lut_Strength50.png';State='LutStrength50';Theme='Dark';Width=1600;Height=920;Scale=1.0;PW=1600;PH=920},
    @{File='04_Lut_BeforeAfter.png';State='LutBeforeAfter';Theme='Dark';Width=1600;Height=920;Scale=1.0;PW=1600;PH=920},
    @{File='05_Lut_SplitView.png';State='LutSplitView';Theme='Dark';Width=1600;Height=920;Scale=1.0;PW=1600;PH=920},
    @{File='06_Lut_Invalid.png';State='LutInvalid';Theme='Dark';Width=1600;Height=920;Scale=1.0;PW=1600;PH=920},
    @{File='07_ColorProfile_Detected.png';State='ColorProfileDetected';Theme='Dark';Width=1600;Height=920;Scale=1.0;PW=1600;PH=920},
    @{File='08_ColorProfile_Fallback.png';State='ColorProfileFallback';Theme='Dark';Width=1600;Height=920;Scale=1.0;PW=1600;PH=920},
    @{File='09_ClientMonitor_Selector.png';State='ClientMonitorSelector';Theme='Dark';Width=1600;Height=920;Scale=1.0;PW=1600;PH=920},
    @{File='10_ClientMonitor_FollowMain.png';State='ClientMonitorFollowMain';Theme='Dark';Width=1600;Height=920;Scale=1.0;PW=1600;PH=920},
    @{File='11_ClientMonitor_FollowLatest.png';State='ClientMonitorFollowLatest';Theme='Dark';Width=1600;Height=920;Scale=1.0;PW=1600;PH=920},
    @{File='12_ClientMonitor_Locked.png';State='ClientMonitorLocked';Theme='Dark';Width=1600;Height=920;Scale=1.0;PW=1600;PH=920},
    @{File='13_ClientMonitor_PrivacyDefault.png';State='ClientMonitorPrivacy';Theme='Dark';Width=1600;Height=920;Scale=1.0;PW=1600;PH=920},
    @{File='14_ClientMonitor_FavoriteNote.png';State='ClientMonitorFavoriteNote';Theme='Dark';Width=1600;Height=920;Scale=1.0;PW=1600;PH=920},
    @{File='15_ClientMonitor_Disconnected.png';State='ClientMonitorDisconnected';Theme='Dark';Width=1600;Height=920;Scale=1.0;PW=1600;PH=920},
    @{File='16_ClientMonitor_Reconnected.png';State='ClientMonitorReconnected';Theme='Dark';Width=1600;Height=920;Scale=1.0;PW=1600;PH=920},
    @{File='17_MixedDpi.png';State='MixedDpi';Theme='Dark';Width=1600;Height=920;Scale=1.5;PW=2400;PH=1380},
    @{File='18_DarkTheme.png';State='TetherDark';Theme='Dark';Width=1600;Height=920;Scale=1.0;PW=1600;PH=920},
    @{File='19_LightTheme.png';State='LutImported';Theme='Light';Width=1600;Height=920;Scale=1.0;PW=1600;PH=920},
    @{File='20_HighContrast.png';State='LutImported';Theme='HighContrast';Width=1600;Height=920;Scale=1.0;PW=1600;PH=920},
    @{File='21_Compact1280.png';State='LutStrength50';Theme='Dark';Width=1280;Height=820;Scale=1.0;PW=1280;PH=820},
    @{File='22_Settings_Color.png';State='Settings';Theme='Dark';Width=1600;Height=920;Scale=1.0;PW=1600;PH=920}
)
$sourceBefore = Get-ChildItem $demoRoot -Filter 'STAGE*' | Sort-Object Name | ForEach-Object { [ordered]@{Name=$_.Name;Bytes=$_.Length;Sha256=(Get-FileHash $_.FullName -Algorithm SHA256).Hash} }
$results = [System.Collections.Generic.List[object]]::new()
foreach($scenario in $scenarios){
    $output=Join-Path $OutputRoot $scenario.File; $metadata=Join-Path $metadataRoot ($scenario.File+'.json'); Remove-Item $output,$metadata -Force -ErrorAction SilentlyContinue
    [ordered]@{State=$scenario.State;Theme=$scenario.Theme;Width=$scenario.Width;Height=$scenario.Height;SidebarCollapsed=$true;OutputPath=$output;MetadataPath=$metadata;DpiScale=$scenario.Scale;DpiX=[int](96*$scenario.Scale);DpiY=[int](96*$scenario.Scale);PhysicalWidth=$scenario.PW;PhysicalHeight=$scenario.PH} | ConvertTo-Json -Depth 8 | Set-Content $statePath -Encoding UTF8
    $process=Start-Process -FilePath $executable -PassThru -WindowStyle Hidden; $deadline=[DateTime]::UtcNow.AddSeconds(45)
    while(((-not(Test-Path $output))-or(-not(Test-Path $metadata)))-and [DateTime]::UtcNow -lt $deadline){Start-Sleep -Milliseconds 250}
    if(-not $process.HasExited){$process.CloseMainWindow()|Out-Null;Start-Sleep -Milliseconds 300};if(-not $process.HasExited){Stop-Process -Id $process.Id -Force}
    if(-not(Test-Path $output)){throw "Screenshot capture failed: $($scenario.File)"};if(-not(Test-Path $metadata)){throw "Screenshot metadata failed: $($scenario.File)"}
    $file=Get-Item $output;$meta=Get-Content $metadata -Raw|ConvertFrom-Json;$results.Add([ordered]@{File=$scenario.File;State=$scenario.State;Theme=$scenario.Theme;DpiScale=$scenario.Scale;Bytes=$file.Length;Sha256=(Get-FileHash $output -Algorithm SHA256).Hash;LayoutPassed=[bool]$meta.passed;BlockingIssueCount=[int]$meta.layout.BlockingIssueCount})
}
$sourceAfter=Get-ChildItem $demoRoot -Filter 'STAGE*'|Sort-Object Name|ForEach-Object{[ordered]@{Name=$_.Name;Bytes=$_.Length;Sha256=(Get-FileHash $_.FullName -Algorithm SHA256).Hash}}
$sourceIntegrity=($sourceBefore|ConvertTo-Json -Compress)-eq($sourceAfter|ConvertTo-Json -Compress)
$evidence=[ordered]@{EvidenceType='real-wpf-render-target-capture';IsolatedProfile=$reviewRoot;SourceCommit=(& git -C $repoRoot rev-parse HEAD).Trim();ExpectedScreenshotCount=22;ScreenshotCount=$results.Count;UniqueScreenshotHashes=($results.Sha256|Sort-Object -Unique).Count;SourceFilesUnchanged=$sourceIntegrity;PhysicalSecondMonitorTested=$false;ValidationScope='automated display topology and independent WPF window';GeneratedAt=[DateTimeOffset]::Now.ToString('O');Screenshots=$results}
$evidence|ConvertTo-Json -Depth 12|Set-Content (Join-Path $OutputRoot 'evidence-index.json') -Encoding UTF8;[ordered]@{Passed=$sourceIntegrity;Before=$sourceBefore;After=$sourceAfter}|ConvertTo-Json -Depth 10|Set-Content (Join-Path $OutputRoot 'source-integrity.json') -Encoding UTF8
$python='<USERPROFILE>\.cache\codex-runtimes\codex-primary-runtime\dependencies\python\python.exe';$sheetName="$([char]0x50CF)$([char]0x7D20)$([char]0x86CB)$([char]0x631E)_2.3.0$([char]0x9636)$([char]0x6BB5)D_LUT$([char]0x4E0E)$([char]0x5BA2)$([char]0x6237)$([char]0x76D1)$([char]0x770B)UI$([char]0x603B)$([char]0x89C8).png";$sheet=Join-Path $OutputRoot $sheetName;& $python (Join-Path $PSScriptRoot 'create_contact_sheet.py') --input $OutputRoot --output $sheet;if($LASTEXITCODE-ne 0){throw 'Stage D contact sheet failed.'}
if($results.Count-ne 22){throw 'Expected 22 Stage D screenshots.'};if(($results.Sha256|Sort-Object -Unique).Count-ne 22){throw 'Stage D screenshots must be unique.'};if(-not $sourceIntegrity){throw 'Stage D source assets changed.'};if(($results|Where-Object{-not $_.LayoutPassed}).Count-gt 0){throw 'Stage D layout metadata contains failures.'}
if(-not $KeepReviewProfile){Remove-Item $statePath -Force -ErrorAction SilentlyContinue};$evidence|ConvertTo-Json -Depth 4
