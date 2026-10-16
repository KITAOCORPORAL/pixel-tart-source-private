param([string]$Output='docs/evidence/photo-engine-home-20261016/01_inventory')
$ErrorActionPreference='Stop'
New-Item -ItemType Directory -Force $Output|Out-Null
$trees=git -C N:/pixart/pixel-tart-source-private worktree list --porcelain|Where-Object{$_ -like 'worktree *'}|ForEach-Object{$_.Substring(9)}
$state=foreach($tree in $trees){[pscustomobject]@{path=$tree;head=(git -C $tree rev-parse HEAD);status=@(git -C $tree status --short)}}
$state|ConvertTo-Json -Depth 6|Set-Content "$Output/WORKTREES.json" -Encoding utf8
$releasePaths=@(
'N:/pixart/kitao-canvas-toolbar-20261013/artifacts/canvas-release/win-x64',
'N:/pixart/kitao-planning-foundation-20261013/artifacts/planning-release/win-x64',
'N:/pixart/kitao-planning-workflows-20261013/artifacts/workflows-release/win-x64',
'N:/pixart/kitao-planning-r5-ui-20261013/artifacts/r5-release-final/win-x64',
'N:/pixart/kitao-planning-portable-images-home-20261014/artifacts/home-review-final/win-x64',
'N:/pixart/kitao-night-A-portability-20261014/artifacts/A-review-release/win-x64',
'N:/pixart/kitao-night-B-color-science-20261014/artifacts/B-final-review/win-x64')
$releases=foreach($path in $releasePaths){$exe=Join-Path $path 'KitaoPhotoSelector.exe';[pscustomobject]@{directory=$path;exists=(Test-Path $exe);exeSha256=if(Test-Path $exe){(Get-FileHash $exe).Hash}else{$null};productVersion=if(Test-Path $exe){(Get-Item $exe).VersionInfo.ProductVersion}else{$null}}}
$releases|ConvertTo-Json|Set-Content "$Output/EXISTING_RELEASES.json" -Encoding utf8
$expected=@(@{path='N:/pixart/RAW/Real/canon/TNAN8886.CR3';hash='B7D358517DB0CD5B5C54215AA089A5A30C79234AEF50E9381931128493E33401'},@{path='N:/pixart/RAW/Real/fuji/DSCF0347.RAF';hash='52D054ED9A90306DB0B7EB5D6C408A2E006532B976AC88ED792692F9F744E865'},@{path='N:/pixart/RAW/Real/sony/DSC09916.ARW';hash='6F596BB234EE5DE206C66594BC84205349C363FD6A89AE188FF76DAB7C5E53B9'})
$inputs=foreach($value in $expected){$present=Test-Path -LiteralPath $value.path;$hash=if($present){(Get-FileHash -LiteralPath $value.path).Hash}else{$null};[pscustomobject]@{path=$value.path;expectedSha256=$value.hash;exists=$present;actualSha256=$hash;authorizedIdentityMatch=($hash -eq $value.hash);authority='Exact-name read-only check in existing authorized RAW/Real fixture root, no private-photo scan'}}
$inputs|ConvertTo-Json|Set-Content "$Output/AUTHORIZED_INPUTS.json" -Encoding utf8
$os=Get-CimInstance Win32_OperatingSystem|Select-Object Caption,Version,BuildNumber,OSArchitecture
$disks=Get-PSDrive -PSProvider FileSystem|Select-Object Name,Root,Free,Used
$monitors=Get-CimInstance -Namespace root/wmi -ClassName WmiMonitorID -ErrorAction SilentlyContinue|ForEach-Object{[pscustomobject]@{instance=$_.InstanceName;manufacturer=(-join ($_.ManufacturerName|Where-Object{$_ -ne 0}|ForEach-Object{[char]$_}));model=(-join ($_.UserFriendlyName|Where-Object{$_ -ne 0}|ForEach-Object{[char]$_}));active=$_.Active}}
[pscustomobject]@{date='2026-10-16';timezone='Europe/Berlin';windows=$os;disks=$disks;monitors=@($monitors);nativeInteraction='NOT_RUN';dpiChanged=$false;privatePhotosScanned=$false}|ConvertTo-Json -Depth 7|Set-Content "$Output/HOST.json" -Encoding utf8
