param([Parameter(Mandatory=$true)][string]$OutputDirectory,[string[]]$Scenarios=@('01','02','03','04','05','06','07','08','09','10','11','12','13','14','15','16','17','18'))
$ErrorActionPreference='Stop'
$repo=Split-Path $PSScriptRoot -Parent
$exe=Join-Path $repo 'src/RAWSelectionAssistant/bin/x64/Release/net10.0-windows10.0.19041.0/win-x64/KitaoPhotoSelector.exe'
$names=@('PROFESSIONAL_MODE','ADJUSTMENT_STACK','REFERENCE_NODE','COLOR_RANGE_NODE','EYEDROPPER_ACTIVE','POSITIVE_NEGATIVE_SAMPLES','SHOW_SELECTION','KEEP_ORIGINAL_LUMINANCE','NODE_REORDER','UNDO_REDO','SIMPLE_PRO_ROUNDTRIP','BATCH_SYNC_SELECTED_NODES','SCHEME_SAVE_APPLY','PROCESSING_CANCEL','SPLIT_COMPARE','POPUP_MENU','COLOR_STUDIO_1180x720','COLOR_STUDIO_200_DPI_LOGICAL')
$manifest=@()
$names+=@('SCHEME_UNSAVED','SCHEME_DELETE_CONFIRM','BATCH_SYNC_TOAST','LINKED_ZOOM_PAN','BATCH_SYNC_200_DPI_LOGICAL','NODE_OVERFLOW')
if(Test-Path (Join-Path $OutputDirectory 'COLOR_STUDIO_PHASE1_SCREENSHOT_MANIFEST.json')) {
 $manifest=@(Get-Content -Raw (Join-Path $OutputDirectory 'COLOR_STUDIO_PHASE1_SCREENSHOT_MANIFEST.json') | ConvertFrom-Json | Where-Object { $_.Scenario -notin $Scenarios })
}
$oldIsolation=$env:PIXEL_TART_ISOLATED_RUNTIME
$oldRoot=$env:PIXEL_TART_ISOLATED_RUNTIME_ROOT
try {
 foreach($scenario in $Scenarios) {
  $runtime=Join-Path $repo ("artifacts/color-studio-final/runs/"+[Guid]::NewGuid().ToString('N'))
  $env:PIXEL_TART_ISOLATED_RUNTIME='1';$env:PIXEL_TART_ISOLATED_RUNTIME_ROOT=$runtime
  $process=Start-Process -FilePath $exe -ArgumentList @('--acceptance-color-studio',"--studio-scenario=$scenario") -PassThru -WindowStyle Hidden
  try {
   $watch=[Diagnostics.Stopwatch]::StartNew()
   $ready=Join-Path $runtime 'ColorStudioFixture/ready.txt'
   while(-not(Test-Path -LiteralPath $ready)) {
    $process.Refresh()
    if($process.HasExited){throw "Scenario $scenario exited: $($process.ExitCode)"}
    if($watch.Elapsed.TotalSeconds -gt 60){throw "Scenario $scenario exceeded 60 seconds"}
    Start-Sleep -Milliseconds 200
   }
   Start-Sleep -Milliseconds 400
   $name=$scenario+'_'+$names[[int]$scenario-1]
   $state=Get-Content -Raw (Join-Path $runtime 'ColorStudioFixture/state.json') | ConvertFrom-Json
   if($state.ProductSourceSha -notmatch '^[0-9a-f]{40}$'){throw 'Production binary has no source SHA. Build with -p:IncludeSourceRevisionInInformationalVersion=true before capture.'}
   $capture=& (Join-Path $PSScriptRoot 'Capture-ColorStudioWindow.ps1') -TargetProcessId $process.Id -OutputDirectory $OutputDirectory -Name $name -MainWindowHandle $state.MainWindowHandle | ConvertFrom-Json
   foreach($item in @($capture)) {
    $record=[ordered]@{}
    foreach($property in $state.PSObject.Properties){$record[$property.Name]=$property.Value}
    foreach($property in $item.PSObject.Properties){$record[$property.Name]=$property.Value}
    $record['SHA256']=(Get-FileHash -LiteralPath (Join-Path $OutputDirectory $item.file) -Algorithm SHA256).Hash
    $record['FullSizeReview']='PENDING'
    $manifest+= $record
   }
   Write-Host "Captured $name"
  } finally {
   $process.Refresh()
   if(-not $process.HasExited -and $process.Path -eq $exe){Stop-Process -Id $process.Id}
  }
 }
 $manifest | ConvertTo-Json -Depth 8 | Set-Content -Encoding utf8 (Join-Path $OutputDirectory 'COLOR_STUDIO_PHASE1_SCREENSHOT_MANIFEST.json')
} finally {$env:PIXEL_TART_ISOLATED_RUNTIME=$oldIsolation;$env:PIXEL_TART_ISOLATED_RUNTIME_ROOT=$oldRoot}
