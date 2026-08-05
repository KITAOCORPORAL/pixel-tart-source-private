param([switch]$SkipBuild)

$ErrorActionPreference='Stop'
$repoRoot=Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
$releaseRoot=Join-Path $repoRoot 'artifacts\releases\2.3.0'
$publishRoot=Join-Path $releaseRoot 'publish\win-x64'
$installerRoot=Join-Path $releaseRoot 'installer'
$installer=Join-Path $installerRoot '像素蛋挞_Setup_2.3.0_RC1_x64.exe'
foreach($path in @($releaseRoot,$installerRoot)){New-Item -ItemType Directory -Force -Path $path|Out-Null}
if(-not $SkipBuild){& (Join-Path $repoRoot 'build_release.ps1');if($LASTEXITCODE-ne 0){exit $LASTEXITCODE}}
if(-not(Test-Path -LiteralPath (Join-Path $publishRoot 'KitaoPhotoSelector.exe'))){throw '2.3.0 publish output is missing.'}
$iscc=@((Join-Path $env:LOCALAPPDATA 'Programs\Inno Setup 7\ISCC.exe'),(Join-Path $env:LOCALAPPDATA 'Programs\Inno Setup 6\ISCC.exe'),'C:\Program Files (x86)\Inno Setup 6\ISCC.exe','C:\Program Files\Inno Setup 6\ISCC.exe')|Where-Object{Test-Path -LiteralPath $_}|Select-Object -First 1
if(-not $iscc){throw '未找到 Inno Setup。'}
if(Test-Path -LiteralPath $installer){throw 'RC1 installer already exists; refusing to overwrite a candidate build.'}
& $iscc '/DCandidateBuild' (Join-Path $repoRoot 'installer\RAWSelectionAssistant.iss');if($LASTEXITCODE-ne 0){exit $LASTEXITCODE}
if(-not(Test-Path -LiteralPath $installer)){throw 'RC1 installer was not generated with the required name.'}

$forbiddenNames=@(Get-ChildItem -LiteralPath $publishRoot -Recurse -File|Where-Object{$_.Name -match '(?i)(testhost|\.Tests\.|UiReview|Acceptance|Sony.*\.dll|Canon.*\.dll|Nikon.*\.dll|Fujifilm.*\.dll|\.pdb$|\.log$|\.db$|\.cube$|\.icc$|\.icm$)'})
if($forbiddenNames.Count-gt 0){throw ('Release scan found forbidden files: '+(($forbiddenNames.FullName)-join ', '))}
$configs=Get-ChildItem -LiteralPath $publishRoot -Recurse -File|Where-Object{$_.Extension -in @('.json','.config','.xml','.txt')}
$networkMatches=@($configs|Select-String -Pattern 'localhost|127\.0\.0\.1' -ErrorAction SilentlyContinue)
if($networkMatches.Count-gt 0){throw 'Release scan found localhost or 127.0.0.1.'}
$license=Get-Content (Join-Path $publishRoot 'appsettings.license.json') -Raw -Encoding UTF8
if($license -notmatch '"Provider"\s*:\s*"None"' -or $license -match 'Mock|Fake Camera'){throw 'Release provider gate failed.'}
$version=(Get-Item -LiteralPath (Join-Path $publishRoot 'KitaoPhotoSelector.exe')).VersionInfo
if($version.ProductVersion -notlike '2.3.0*' -or $version.FileVersion -notlike '2.3.0.0*'){throw 'Published version gate failed.'}
$signature=Get-AuthenticodeSignature -LiteralPath $installer
$files=@(Get-ChildItem -LiteralPath $publishRoot -Recurse -File)+@(Get-Item -LiteralPath $installer)
$manifest=@($files|ForEach-Object{[ordered]@{Path=[IO.Path]::GetRelativePath($releaseRoot,$_.FullName).Replace('\','/');Bytes=$_.Length;Sha256=(Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash}}|Sort-Object Path)
$manifest|ConvertTo-Json -Depth 5|Set-Content -LiteralPath (Join-Path $releaseRoot 'file-manifest.json') -Encoding UTF8
$manifest|Export-Csv -LiteralPath (Join-Path $releaseRoot 'file-manifest.csv') -NoTypeInformation -Encoding UTF8
$manifest|ForEach-Object{"$($_.Sha256) *$($_.Path)"}|Set-Content -LiteralPath (Join-Path $releaseRoot 'SHA256SUMS.txt') -Encoding UTF8
$uiIndex=Join-Path $repoRoot 'artifacts\ui-review\2.3.0-stage-e\evidence-index.json';if(Test-Path -LiteralPath $uiIndex){Copy-Item -LiteralPath $uiIndex -Destination (Join-Path $releaseRoot 'ui-evidence-index.json') -Force}
$releaseManifest=[ordered]@{Product='像素蛋挞';Version='2.3.0';FileVersion='2.3.0.0';SchemaVersion=3;Candidate='RC1';Provider='None';PhysicalSecondMonitorTested=$false;GitCommit=(& git -C $repoRoot rev-parse HEAD).Trim();OutputType='WinExe';SelfContained=$true;Runtime='win-x64';Installer=[IO.Path]::GetRelativePath($releaseRoot,$installer).Replace('\','/');InstallerSha256=(Get-FileHash -LiteralPath $installer -Algorithm SHA256).Hash;InstallerBytes=(Get-Item $installer).Length;SignatureStatus=$signature.Status.ToString();ReleaseScan=[ordered]@{Passed=$true;NoTests=$true;NoVendorSdk=$true;NoLocalhost=$true;NoLogs=$true;NoDatabases=$true;NoSyntheticAssets=$true};GeneratedAt=[DateTimeOffset]::Now.ToString('O')}
$releaseManifest|ConvertTo-Json -Depth 8|Set-Content -LiteralPath (Join-Path $releaseRoot 'release-manifest.json') -Encoding UTF8
$releaseManifest|ConvertTo-Json -Depth 8
