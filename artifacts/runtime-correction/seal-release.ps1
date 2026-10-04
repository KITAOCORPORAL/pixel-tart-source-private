param([string]$Candidate='r9')
$ErrorActionPreference='Stop'
$repo=(Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
$root=(Resolve-Path (Join-Path $repo ('artifacts/releases/runtime-correction-2026-10-04-'+$Candidate+'/publish/win-x64'))).Path
$exe=Join-Path $root 'KitaoPhotoSelector.exe'
$head=(& git -C $repo rev-parse HEAD).Trim()
$version=(Get-Item (Join-Path $root 'KitaoPhotoSelector.dll')).VersionInfo.ProductVersion
if ($version -ne ('2.3.0+'+$head)) { throw ('Release provenance mismatch: '+$version) }
$manifest=[ordered]@{SourceHead=$head;Branch='integration/pixel-tart-developer-preview';Configuration='Release';Platform='x64';Runtime='win-x64';SelfContained=$true;ReleaseExe=$exe;Sha256=(Get-FileHash -LiteralPath $exe).Hash;ApplicationInformationalVersion=$version;Files=@(Get-ChildItem -LiteralPath $root -File -Recurse | Sort-Object FullName | ForEach-Object { [ordered]@{Path=[IO.Path]::GetRelativePath($root,$_.FullName);Length=$_.Length;Sha256=(Get-FileHash -LiteralPath $_.FullName).Hash} });VisualApproved=$false;UserVerified=$false;RuntimeStatus='NOT_RUN'}
$manifest | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath (Join-Path $root '../../release-manifest.json') -Encoding utf8
[pscustomobject]$manifest | Select-Object SourceHead,ReleaseExe,Sha256,ApplicationInformationalVersion