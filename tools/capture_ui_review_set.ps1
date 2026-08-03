param(
    [string]$OutputDirectory = '',
    [switch]$SkipBuild
)

$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$dotnet = Join-Path $env:LOCALAPPDATA 'Microsoft\dotnet-sdk-10\dotnet.exe'
$project = Join-Path $root 'src\RAWSelectionAssistant\RAWSelectionAssistant.csproj'
$executable = Join-Path $root 'src\RAWSelectionAssistant\bin\x64\Release\net10.0-windows10.0.19041.0\win-x64\KitaoPhotoSelector.UiReview.exe'
if ([string]::IsNullOrWhiteSpace($OutputDirectory)) {
    $OutputDirectory = Join-Path $root 'artifacts\ui-review\2.0.1'
}

New-Item -ItemType Directory -Force -Path $OutputDirectory | Out-Null
if (-not $SkipBuild) {
    & $dotnet build $project -c Release -p:UiReviewBuild=true -p:Platform=x64 --no-restore
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
}

$captures = @(
    @{ File='01_Workbench_Dark_1600x920.png'; State='Workbench'; Theme='Dark'; Width=1600; Height=920; Collapsed=$false },
    @{ File='02_Workbench_Dark_1920x1080.png'; State='Workbench'; Theme='Dark'; Width=1920; Height=1080; Collapsed=$false },
    @{ File='03_Toolbox_Popup.png'; State='ToolboxPopup'; Theme='Dark'; Width=1600; Height=920; Collapsed=$false },
    @{ File='04_Toolbox_FullPage.png'; State='ToolboxFullPage'; Theme='Dark'; Width=1600; Height=920; Collapsed=$false },
    @{ File='05_RecentProjects.png'; State='RecentProjects'; Theme='Dark'; Width=1600; Height=920; Collapsed=$false },
    @{ File='06_TaskCenter_WithTasks.png'; State='TaskCenterWithTasks'; Theme='Dark'; Width=1600; Height=920; Collapsed=$false },
    @{ File='07_TaskCenter_Empty.png'; State='CompletedProjectsEmpty'; Theme='Dark'; Width=1600; Height=920; Collapsed=$false },
    @{ File='08_Settings_Dark.png'; State='Settings'; Theme='Dark'; Width=1600; Height=920; Collapsed=$false },
    @{ File='09_Workbench_Light.png'; State='Workbench'; Theme='Light'; Width=1600; Height=920; Collapsed=$false },
    @{ File='10_Compact_1280.png'; State='Workbench'; Theme='Dark'; Width=1280; Height=720; Collapsed=$false },
    @{ File='11_Sidebar_Collapsed.png'; State='Workbench'; Theme='Dark'; Width=1600; Height=920; Collapsed=$true },
    @{ File='12_Feedback_Dialog.png'; State='Feedback'; Theme='Dark'; Width=1600; Height=920; Collapsed=$false },
    @{ File='13_OrganizePhotos_Dark.png'; State='OrganizePhotos'; Theme='Dark'; Width=1600; Height=920; Collapsed=$false },
    @{ File='14_Collage_Dark.png'; State='Collage'; Theme='Dark'; Width=1600; Height=920; Collapsed=$false },
    @{ File='15_QuickTools_Overflow_1280.png'; State='QuickToolsOverflow'; Theme='Dark'; Width=1280; Height=720; Collapsed=$false }
)

foreach ($capture in $captures) {
    $outputPath = Join-Path $OutputDirectory $capture.File
    if (Test-Path -LiteralPath $outputPath) { Remove-Item -LiteralPath $outputPath -Force }
    $arguments = @('-NoProfile', '-ExecutionPolicy', 'Bypass', '-File', (Join-Path $PSScriptRoot 'prepare_ui_review.ps1'), '-Theme', $capture.Theme, '-Width', $capture.Width, '-Height', $capture.Height, '-State', $capture.State, '-OutputPath', $outputPath)
    if ($capture.Collapsed) { $arguments += '-SidebarCollapsed' }
    & powershell @arguments | Out-Null
    $process = Start-Process -FilePath $executable -PassThru -WindowStyle Normal
    $deadline = [DateTime]::UtcNow.AddSeconds(25)
    while (-not (Test-Path -LiteralPath $outputPath) -and [DateTime]::UtcNow -lt $deadline) {
        Start-Sleep -Milliseconds 250
    }
    if (-not $process.HasExited) { $process.CloseMainWindow() | Out-Null; Start-Sleep -Milliseconds 300 }
    if (-not $process.HasExited) { Stop-Process -Id $process.Id -Force }
    if (-not (Test-Path -LiteralPath $outputPath)) { throw "UI review capture failed: $($capture.File)" }
}

Get-ChildItem -LiteralPath $OutputDirectory -Filter '*.png' | Where-Object Name -Match '^\d{2}_' | Sort-Object Name | Select-Object Name, Length, LastWriteTime
