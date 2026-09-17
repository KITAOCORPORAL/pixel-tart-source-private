[CmdletBinding()]
param(
    [string]$OutputRoot = '',
    [string]$StatePattern = '*',
    [switch]$SkipBuild
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$repoRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\..'))
$dotnet = [IO.Path]::GetFullPath((Join-Path $repoRoot '..\..\.dotnet\dotnet.exe'))
$project = Join-Path $repoRoot 'src\RAWSelectionAssistant\RAWSelectionAssistant.csproj'
$executable = Join-Path $repoRoot 'src\RAWSelectionAssistant\bin\x64\Release\net10.0-windows10.0.19041.0\win-x64\KitaoPhotoSelector.UiReview.exe'
$sourceCommit = (& git -C $repoRoot rev-parse HEAD).Trim()
if ([string]::IsNullOrWhiteSpace($OutputRoot)) { $OutputRoot = Join-Path $repoRoot 'artifacts\rc12-product-visual' }
$OutputRoot = [IO.Path]::GetFullPath($OutputRoot)
$screenshotsRoot = Join-Path $OutputRoot 'screenshots'
$dpiRoot = Join-Path $OutputRoot 'dpi-current'
$resolutionRoot = Join-Path $OutputRoot 'asset-library-resolutions'
$closureRoot = Join-Path $OutputRoot 'asset-library-ux-closure'
$ratioRoot = Join-Path $OutputRoot 'aspect-ratio-comparison'
$profilesRoot = Join-Path $OutputRoot 'isolated-profiles'
$fixtureRoot = Join-Path $OutputRoot 'synthetic-assets'
foreach ($path in @($OutputRoot,$screenshotsRoot,$dpiRoot,$resolutionRoot,$closureRoot,$ratioRoot,$profilesRoot,$fixtureRoot)) {
    [IO.Directory]::CreateDirectory($path) | Out-Null
}

$baselinePath = Join-Path $ratioRoot 'before_previous_head.png'
$previousManifestPath = Join-Path $OutputRoot 'rc12-product-visual-evidence.json'
$previousGridPath = Join-Path $screenshotsRoot '01_asset_library_grid.png'
$baselineCommit = $null
if (-not (Test-Path -LiteralPath $baselinePath) -and (Test-Path -LiteralPath $previousManifestPath) -and (Test-Path -LiteralPath $previousGridPath)) {
    $previousManifest = Get-Content -LiteralPath $previousManifestPath -Raw | ConvertFrom-Json
    if ($previousManifest.source_commit -ne $sourceCommit) {
        Copy-Item -LiteralPath $previousGridPath -Destination $baselinePath
        $baselineCommit = $previousManifest.source_commit
        Set-Content -LiteralPath (Join-Path $ratioRoot 'before_previous_head.txt') -Value $baselineCommit -Encoding UTF8
    }
}
elseif (Test-Path -LiteralPath (Join-Path $ratioRoot 'before_previous_head.txt')) {
    $baselineCommit = (Get-Content -LiteralPath (Join-Path $ratioRoot 'before_previous_head.txt') -Raw).Trim()
}

if (-not $SkipBuild) {
    & $dotnet build $project -c Release -p:UiReviewBuild=true -p:Platform=x64 --no-restore -warnaserror "-p:SourceRevisionId=$sourceCommit"
    if ($LASTEXITCODE -ne 0) { throw 'RC12 product visual application build failed.' }
}
if (-not (Test-Path -LiteralPath $executable -PathType Leaf)) { throw "Product visual executable not found: $executable" }

function New-SyntheticAssets {
    param([string]$Directory)
    Add-Type -AssemblyName System.Drawing
    $palette = @(
        @('#172337','#E7A85E'), @('#422837','#F1D7B2'), @('#183D38','#8ED6C0'), @('#33284A','#DA94C8'),
        @('#24304A','#E8C879'), @('#46262B','#ECA6A0'), @('#183749','#86BEDC'), @('#4A3823','#E9C59A'),
        @('#272A31','#F0ECE4'), @('#1D3C30','#A8D7A8'), @('#432744','#E4A2E1'), @('#29384B','#BCD4EE')
    )
    for ($index = 0; $index -lt $palette.Count; $index++) {
        $width = if ($index -eq 0) { 2400 } elseif ($index -eq 1) { 600 } elseif ($index % 3 -eq 0) { 1400 } elseif ($index % 3 -eq 1) { 900 } else { 1200 }
        $height = if ($index -eq 0) { 600 } elseif ($index -eq 1) { 2400 } elseif ($index % 3 -eq 0) { 900 } elseif ($index % 3 -eq 1) { 1350 } else { 1200 }
        $path = Join-Path $Directory ('RC12_SYNTHETIC_{0:00}.jpg' -f ($index + 1))
        $bitmap = [Drawing.Bitmap]::new($width,$height)
        $graphics = [Drawing.Graphics]::FromImage($bitmap)
        try {
            $background = [Drawing.ColorTranslator]::FromHtml($palette[$index][0])
            $accent = [Drawing.ColorTranslator]::FromHtml($palette[$index][1])
            $graphics.SmoothingMode = [Drawing.Drawing2D.SmoothingMode]::AntiAlias
            $graphics.Clear($background)
            $brush = [Drawing.SolidBrush]::new($accent)
            $soft = [Drawing.SolidBrush]::new([Drawing.Color]::FromArgb(88,$accent))
            $pen = [Drawing.Pen]::new($accent,[Math]::Max(6,[int]($width/140)))
            $font = [Drawing.Font]::new('Segoe UI',[Math]::Max(26,[int]($width/28)),[Drawing.FontStyle]::Bold)
            try {
                $graphics.FillRectangle($soft,[int]($width*.08),[int]($height*.10),[int]($width*.58),[int]($height*.68))
                $graphics.DrawEllipse($pen,[int]($width*.42),[int]($height*.18),[int]($width*.42),[int]($height*.42))
                $graphics.DrawLine($pen,0,$height,$width,0)
                $graphics.DrawString(('PIXEL TART  {0:00}' -f ($index + 1)),$font,$brush,34,$height-$font.Height-34)
            }
            finally { $font.Dispose(); $pen.Dispose(); $soft.Dispose(); $brush.Dispose() }
            $codec = [Drawing.Imaging.ImageCodecInfo]::GetImageEncoders() | Where-Object MimeType -eq 'image/jpeg'
            $parameters = [Drawing.Imaging.EncoderParameters]::new(1)
            $parameters.Param[0] = [Drawing.Imaging.EncoderParameter]::new([Drawing.Imaging.Encoder]::Quality,92L)
            try { $bitmap.Save($path,$codec,$parameters) } finally { $parameters.Dispose() }
        }
        finally { $graphics.Dispose(); $bitmap.Dispose() }
    }
}

Get-ChildItem -LiteralPath $fixtureRoot -File -Filter '*.jpg' -ErrorAction SilentlyContinue | Remove-Item -Force
New-SyntheticAssets $fixtureRoot
$sourceBefore = @(Get-ChildItem -LiteralPath $fixtureRoot -File | Sort-Object Name | ForEach-Object {
    [ordered]@{ name=$_.Name; bytes=$_.Length; sha256=(Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash }
})

$script:captures = [Collections.Generic.List[object]]::new()
$script:captureIndex = 0
function Invoke-ProductCapture {
    param([string]$State,[string]$OutputPath,[double]$Scale,[int]$PhysicalWidth,[int]$PhysicalHeight,[string]$Group)
    if ($State -notlike $StatePattern) { return }
    $script:captureIndex++
    $profile = Join-Path $profilesRoot ('capture-{0:000}' -f $script:captureIndex)
    [IO.Directory]::CreateDirectory($profile) | Out-Null
    $metadataPath = $OutputPath + '.json'
    [IO.Directory]::CreateDirectory((Split-Path -Parent $OutputPath)) | Out-Null
    Remove-Item -LiteralPath $OutputPath,$metadataPath -Force -ErrorAction SilentlyContinue
    $logicalWidth = $PhysicalWidth / $Scale
    $logicalHeight = $PhysicalHeight / $Scale
    [ordered]@{
        State=$State; Theme='Dark'; Width=$logicalWidth; Height=$logicalHeight; SidebarCollapsed=$false
        OutputPath=$OutputPath; MetadataPath=$metadataPath; DpiScale=$Scale
        DpiX=96*$Scale; DpiY=96*$Scale; PhysicalWidth=$PhysicalWidth; PhysicalHeight=$PhysicalHeight
    } | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath (Join-Path $profile 'ui-review-state.json') -Encoding UTF8

    $previousRoot = $env:PIXEL_TART_ACCEPTANCE_ROOT
    $previousHarness = $env:PIXEL_TART_RC12_PRODUCT_HARNESS
    $previousDemo = $env:PIXEL_TART_ASSET_LIBRARY_DEMO_DIR
    $previousCommit = $env:PIXEL_TART_SOURCE_COMMIT
    $previousDotnet = $env:DOTNET_ROOT
    $env:PIXEL_TART_ACCEPTANCE_ROOT = $profile
    $env:PIXEL_TART_RC12_PRODUCT_HARNESS = '1'
    $env:PIXEL_TART_ASSET_LIBRARY_DEMO_DIR = $fixtureRoot
    $env:PIXEL_TART_SOURCE_COMMIT = $sourceCommit
    $env:DOTNET_ROOT = Split-Path -Parent $dotnet
    $process = $null
    try {
        $process = Start-Process -FilePath $executable -PassThru -WindowStyle Hidden
        $startedAt = [DateTimeOffset]::Now
        $deadline = [DateTime]::UtcNow.AddSeconds(60)
        while ((-not (Test-Path -LiteralPath $OutputPath) -or -not (Test-Path -LiteralPath $metadataPath)) -and [DateTime]::UtcNow -lt $deadline) {
            if ($process.HasExited) { throw "Product visual process exited before capture: $State ($($process.ExitCode))" }
            Start-Sleep -Milliseconds 200
        }
        if (-not (Test-Path -LiteralPath $OutputPath) -or -not (Test-Path -LiteralPath $metadataPath)) { throw "Product visual capture timed out: $State" }
        $metadata = Get-Content -LiteralPath $metadataPath -Raw | ConvertFrom-Json
        if (-not [bool]$metadata.passed) { throw "Product visual layout validation failed: $State" }
        $file = Get-Item -LiteralPath $OutputPath
        if ($file.Length -lt 4096) { throw "Product visual screenshot is unexpectedly small: $State" }
        $script:captures.Add([ordered]@{
            group=$Group; state=$State; file_name=$file.Name; path=$file.FullName; metadata_path=$metadataPath
            process_id=$process.Id; process_started_at=$startedAt.ToString('O'); process_exited_before_next=$false
            scale=$Scale; dpi_percent=[int][Math]::Round($Scale*100); physical_width=$PhysicalWidth; physical_height=$PhysicalHeight
            bytes=$file.Length; sha256=(Get-FileHash -LiteralPath $file.FullName -Algorithm SHA256).Hash; passed=$true
        })
    }
    finally {
        if ($null -ne $process) {
            if (-not $process.HasExited) { [void]$process.CloseMainWindow(); [void]$process.WaitForExit(5000) }
            if (-not $process.HasExited) { $process.Kill(); [void]$process.WaitForExit(10000) }
            if ($script:captures.Count -gt 0 -and $script:captures[$script:captures.Count-1].process_id -eq $process.Id)
                { $script:captures[$script:captures.Count-1].process_exited_before_next = $process.HasExited }
            $process.Dispose()
        }
        $env:PIXEL_TART_ACCEPTANCE_ROOT = $previousRoot
        $env:PIXEL_TART_RC12_PRODUCT_HARNESS = $previousHarness
        $env:PIXEL_TART_ASSET_LIBRARY_DEMO_DIR = $previousDemo
        $env:PIXEL_TART_SOURCE_COMMIT = $previousCommit
        $env:DOTNET_ROOT = $previousDotnet
    }
}

$scenes = @(
    @('AssetLibraryGrid','01_asset_library_grid.png'), @('AssetLibraryMasonry','02_asset_library_masonry.png'),
    @('AssetFilter','03_asset_filter.png'), @('AssetContextMenu','04_asset_context_menu.png'),
    @('AssetInspectorProject','05_asset_inspector_project.png'), @('AssetViewer','06_viewer.png'),
    @('CalendarBookingAssets','07_calendar_booking_assets.png'), @('AssetInspirationTray','08_inspiration_tray.png'),
    @('AssetInspirationCollection','09_inspiration_collection.png'), @('AssetRecentLibraries','10_recent_library_switcher.png'),
    @('AssetOfflineCachedPreview','11_offline_cached_preview.png'), @('AssetProjectBookingPicker','12_project_booking_picker.png')
)
foreach ($scene in $scenes) { Invoke-ProductCapture $scene[0] (Join-Path $screenshotsRoot $scene[1]) 1.0 1920 1080 'product-screenshot' }

$uxScreenshotsRoot = Join-Path $OutputRoot 'ux-simplification'
[IO.Directory]::CreateDirectory($uxScreenshotsRoot) | Out-Null
$uxScenes = @(
    @('ToolboxFullPage','01_toolbox.png'), @('OrganizeNoOverlap','02_organize_simple.png'),
    @('OrganizeManifest','03_organize_preview.png'), @('RawToJpeg','04_raw_to_jpg_simple.png'),
    @('RawToJpegAdvanced','05_raw_to_jpg_advanced.png'), @('AssetLibraryGrid','06_asset_library_clean.png'),
    @('AssetSmartFolder','07_smart_folder_human.png'), @('AssetInspectorProject','08_inspector_clean.png'),
    @('WorkbenchTaskCenterClean','09_task_center_clean.png'), @('WorkbenchErrorDetailsCollapsed','10_error_details_collapsed.png')
)
foreach ($scene in $uxScenes) { Invoke-ProductCapture $scene[0] (Join-Path $uxScreenshotsRoot $scene[1]) 1.0 1920 1080 'ux-simplification' }

$closureScenes = @(
    @('AssetLibraryGrid','01_clean.png'), @('AssetContextMenu','02_context_menu.png'),
    @('AssetContextSubmenu','03_submenu.png'), @('AssetFolderTree','04_folder_tree.png'),
    @('AssetFilterColor','05_filter_color.png'), @('AssetInspectorRating','06_inspector_rating.png'),
    @('AssetInspirationBoard','07_inspiration_board.png'), @('AssetQuickLoupeIdle','08_loupe_idle.png'),
    @('AssetQuickLoupeActive','09_loupe_active.png'), @('AssetFullPreview','10_full_preview.png'),
    @('AssetQuickLoupeClosed','11_loupe_closed.png')
)
foreach ($scene in $closureScenes) { Invoke-ProductCapture $scene[0] (Join-Path $closureRoot $scene[1]) 1.0 1920 1080 'asset-library-ux-closure' }
Invoke-ProductCapture 'AssetAspectRatiosAfter' (Join-Path $ratioRoot 'after_current_head.png') 1.0 1920 1080 'asset-library-aspect-ratio'

$canvasRoot = Join-Path $OutputRoot 'free-canvas'
[IO.Directory]::CreateDirectory($canvasRoot) | Out-Null
$canvasScenes = @(
    @('CanvasInitial','01_canvas_initial.png'), @('CanvasFreeLayout','02_canvas_free_layout.png'),
    @('CanvasMultiSelect','03_canvas_multi_select.png'), @('CanvasCrop','04_canvas_crop.png'),
    @('CanvasRotateFlip','05_canvas_rotate_flip.png'), @('CanvasGroup','06_canvas_group.png'),
    @('CanvasLocked','07_canvas_locked.png'), @('CanvasText','08_canvas_text.png'),
    @('CanvasInspirationDrawer','09_canvas_inspiration_drawer.png'), @('CanvasProjectLink','10_canvas_project_link.png')
)
foreach ($scene in $canvasScenes) { Invoke-ProductCapture $scene[0] (Join-Path $canvasRoot $scene[1]) 1.0 1920 1080 'free-canvas' }
foreach ($state in @('AssetInspectorNone','AssetInspectorMulti')) { Invoke-ProductCapture $state (Join-Path $canvasRoot ($state + '.png')) 1.0 1920 1080 'contextual-inspector' }

$dpiStates = @('MainWindow','AssetLibraryGrid','AssetFilter','AssetContextMenu','AssetViewer','CalendarBookingAssets','AssetInspirationCollection','AssetRecentLibraries')
foreach ($scale in @(1.0,1.25,1.5,2.0)) {
    foreach ($state in $dpiStates) {
        $file = '{0}_{1}.png' -f ([int]($scale*100)),$state
        # Keep a 1920x1080 logical work area at every scale. This exercises WPF's
        # current per-monitor DPI math without inventing a viewport narrower than
        # the product's documented minimum window size.
        Invoke-ProductCapture $state (Join-Path $dpiRoot $file) $scale ([int](1920*$scale)) ([int](1080*$scale)) 'dpi-current'
    }
}

$resolutions = @(
    @('1920x1080_100',1.0,1920,1080), @('2560x1440_100',1.0,2560,1440), @('3840x2160_100',1.0,3840,2160),
    @('1920x1080_125',1.25,1920,1080), @('1920x1080_150',1.5,1920,1080), @('3840x2160_200',2.0,3840,2160)
)
foreach ($resolution in $resolutions) {
    Invoke-ProductCapture 'AssetLibraryGrid' (Join-Path $resolutionRoot ($resolution[0] + '.png')) ([double]$resolution[1]) ([int]$resolution[2]) ([int]$resolution[3]) 'asset-library-resolution'
}

$sourceAfter = @(Get-ChildItem -LiteralPath $fixtureRoot -File | Sort-Object Name | ForEach-Object {
    [ordered]@{ name=$_.Name; bytes=$_.Length; sha256=(Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash }
})
$sourceSafe = ($sourceBefore | ConvertTo-Json -Compress) -ceq ($sourceAfter | ConvertTo-Json -Compress)
$allExited = @($script:captures | Where-Object { -not $_.process_exited_before_next }).Count -eq 0
$uniquePids = @($script:captures.process_id | Sort-Object -Unique).Count -eq $script:captures.Count
$manifest = [ordered]@{
    schema='pixel-tart-rc12-product-visual/v1'; product_version='2.3.0-RC12'; source_commit=$sourceCommit
    real_app_xaml=$true; real_main_window=$true; pixel_tart_dark_theme=$true; synthetic_assets_only=$true
    process_per_fixture=$true; application_singleton_shared=$false; source_files_unchanged=$sourceSafe
    required_product_screenshot_count=12; product_screenshot_count=@($script:captures | Where-Object group -eq 'product-screenshot').Count
    required_ux_screenshot_count=10; ux_screenshot_count=@($script:captures | Where-Object group -eq 'ux-simplification').Count
    required_dpi_percentages=@(100,125,150,200); dpi_capture_count=@($script:captures | Where-Object group -eq 'dpi-current').Count
    resolution_capture_count=@($script:captures | Where-Object group -eq 'asset-library-resolution').Count
    ux_closure_capture_count=@($script:captures | Where-Object group -eq 'asset-library-ux-closure').Count
    aspect_ratio_capture_count=@($script:captures | Where-Object group -eq 'asset-library-aspect-ratio').Count
    canvas_capture_count=@($script:captures | Where-Object group -eq 'free-canvas').Count
    contextual_inspector_capture_count=@($script:captures | Where-Object group -eq 'contextual-inspector').Count
    aspect_ratio_baseline=[ordered]@{ source_commit=$baselineCommit; path=$baselinePath; exists=(Test-Path -LiteralPath $baselinePath) }
    # Windows may reuse a PID after its owner exits. Lifecycle separation is proven
    # by the exit acknowledgement on every capture; PID uniqueness is diagnostic only.
    unique_process_id_per_capture=$uniquePids; lifecycle_isolated_per_capture=$allExited; all_processes_exited_before_next=$allExited
    executable_sha256=(Get-FileHash -LiteralPath $executable -Algorithm SHA256).Hash
    generated_at=[DateTimeOffset]::Now.ToString('O'); captures=$script:captures; source_before=$sourceBefore; source_after=$sourceAfter
}
$manifest | ConvertTo-Json -Depth 12 | Set-Content -LiteralPath (Join-Path $OutputRoot 'rc12-product-visual-evidence.json') -Encoding UTF8
if (-not $sourceSafe) { throw 'Synthetic source assets changed during the RC12 visual run.' }
if (-not $allExited) { throw 'Process-per-fixture lifecycle isolation was not proven.' }
if ($StatePattern -eq '*' -and ($manifest.canvas_capture_count -ne 10 -or $manifest.contextual_inspector_capture_count -ne 2)) { throw 'Creative workflow evidence set is incomplete.' }
if ($StatePattern -eq '*' -and ($manifest.product_screenshot_count -ne 12 -or $manifest.ux_screenshot_count -ne 10 -or $manifest.dpi_capture_count -ne 32 -or $manifest.resolution_capture_count -ne 6 -or $manifest.ux_closure_capture_count -ne 11 -or $manifest.aspect_ratio_capture_count -ne 1)) { throw 'RC12 visual evidence set is incomplete.' }
[pscustomobject]$manifest | Select-Object product_version,source_commit,product_screenshot_count,ux_screenshot_count,dpi_capture_count,resolution_capture_count,ux_closure_capture_count,aspect_ratio_capture_count,lifecycle_isolated_per_capture,unique_process_id_per_capture,source_files_unchanged | ConvertTo-Json
