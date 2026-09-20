param([string]$SourceSha = '993854ce0fd2437bd1fc05dc36c6bac316305a48')
$ErrorActionPreference = 'Stop'
$root = $PSScriptRoot
$captureRoot = Join-Path $root 'review-closure'
$routes = Get-Content (Join-Path $captureRoot 'WHOLE_APP_SCREENSHOT_MANIFEST.json') -Raw | ConvertFrom-Json
if (@($routes | Where-Object ProductSourceSha -ne $SourceSha).Count) { throw 'Source mismatch' }
$map = @{ '02_asset-library'='asset-library'; '05_planning'='planning'; '11_reference-color'='reference-color'; '06_tether'='tether'; '18_global-popups'='popups' }
$fullReviewed = @('02_asset-library/selected.png','05_planning/文字.png','11_reference-color/multi-reference.png','06_tether/reference-expanded.png','18_global-popups/planning-reference.png','18_global-popups/datepicker.png','16_settings/default.png')
$manifest = [Collections.Generic.List[object]]::new()
Add-Type -AssemblyName System.Drawing
foreach ($file in Get-ChildItem $captureRoot -Recurse -Filter '*.png') {
    $rel = [IO.Path]::GetRelativePath($captureRoot,$file.FullName).Replace('\','/')
    $segments = $rel.Split('/')
    $folder = $segments[0]
    if ($map.ContainsKey($folder)) { $destRel = $map[$folder] + '/' + $segments[1] }
    elseif ($folder -match '^\d\d_') { $destRel = 'whole-app-smoke/' + $folder + '/' + $segments[1] }
    elseif ($segments.Length -eq 1) { $destRel = 'contact-sheets/' + $rel }
    else { $destRel = $rel }
    $dest = Join-Path $root $destRel
    [IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($dest)) | Out-Null
    Copy-Item -LiteralPath $file.FullName -Destination $dest
    $bmp = [Drawing.Image]::FromFile($dest)
    try { $resolution = @($bmp.Width,$bmp.Height) } finally { $bmp.Dispose() }
    $review = if ($folder -eq 'components' -or $fullReviewed -contains $rel) { 'FULL_SIZE_INSPECTED_PARTIAL_GATE' } else { 'CAPTURED_NOT_INDIVIDUALLY_REVIEWED' }
    $kind = if ($folder -eq 'components') { 'PRODUCTION_RESOURCE_GALLERY' } elseif ($destRel.StartsWith('contact-sheets/')) { 'INDEX_ONLY' } else { 'IN_PROCESS_PRODUCTION_WPF_RENDER' }
    $manifest.Add([ordered]@{ProductSourceSha=$SourceSha;Filename=$destRel;CaptureFilename=$rel;SHA256=(Get-FileHash $dest -Algorithm SHA256).Hash;Resolution=$resolution;EvidenceKind=$kind;ReviewStatus=$review;PhysicalDpi='NOT_TESTED';SyntheticContent=$true})
}
$manifest | ConvertTo-Json -Depth 6 | Set-Content (Join-Path $root 'STUDIO_SCREENSHOT_MANIFEST.json') -Encoding utf8
$observations = Get-Content (Join-Path $captureRoot 'geometry-observations.jsonl') | ForEach-Object { ($_ | ConvertFrom-Json).Rows }
[ordered]@{ProductSourceSha=$SourceSha;Status='PARTIAL';FullGatePassed=$false;Reason='Scroll interaction and complete state/DPI coverage remain unverified';Groups=@($observations | Group-Object Disposition | Select-Object Name,Count);Details='review-closure/geometry-observations.jsonl'} | ConvertTo-Json -Depth 5 | Set-Content (Join-Path $root 'TEXT_OVERFLOW_GEOMETRY_AUDIT.json') -Encoding utf8
Write-Output "Packaged $($manifest.Count) PNGs; geometry remains PARTIAL."
