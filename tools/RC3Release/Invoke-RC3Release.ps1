param([switch]$SkipBuild)

$ErrorActionPreference='Stop'
$repoRoot=Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
$releaseRoot=Join-Path $repoRoot 'artifacts\releases\2.3.0'
$publishRoot=Join-Path $releaseRoot 'publish\win-x64'
$installerRoot=Join-Path $releaseRoot 'installer'
$productName=[Text.Encoding]::UTF8.GetString([Convert]::FromBase64String('5YOP57Sg6JuL5oye'))
$installer=Join-Path $installerRoot ($productName+'_Setup_2.3.0_RC3_x64.exe')
$rc1=Join-Path $installerRoot ($productName+'_Setup_2.3.0_RC1_x64.exe')
$rc2=Join-Path $installerRoot ($productName+'_Setup_2.3.0_RC2_x64.exe')
$expectedRc1='7C9AD2689BBCC5960D7B20396D8951D63F012A615447FEAE453BC6CABD588A2C'
$expectedRc2='E26050081A9D1AC45D5A4B6B7B43FFE0F835B1898350CCA85DF53B1F33EBFA91'

function Get-ScopedRelativePath([string]$BasePath,[string]$Path){
    $base=[IO.Path]::GetFullPath($BasePath).TrimEnd([IO.Path]::DirectorySeparatorChar)
    $full=[IO.Path]::GetFullPath($Path)
    if(-not $full.StartsWith($base+[IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase)){throw "Path escaped expected release root: $full"}
    $full.Substring($base.Length+1).Replace('\','/')
}

foreach($path in @($releaseRoot,$installerRoot)){New-Item -ItemType Directory -Force -Path $path|Out-Null}
foreach($accepted in @(@($rc1,$expectedRc1,'RC1'),@($rc2,$expectedRc2,'RC2'))){
    if(-not(Test-Path -LiteralPath $accepted[0])){throw "$($accepted[2]) installer is missing."}
    $hash=(Get-FileHash -LiteralPath $accepted[0] -Algorithm SHA256).Hash
    if($hash -ne $accepted[1]){throw "$($accepted[2]) installer hash changed."}
}
$acceptedBefore=@{
    Rc1=@{Bytes=(Get-Item $rc1).Length;Sha256=(Get-FileHash $rc1 -Algorithm SHA256).Hash}
    Rc2=@{Bytes=(Get-Item $rc2).Length;Sha256=(Get-FileHash $rc2 -Algorithm SHA256).Hash}
}
if(Test-Path -LiteralPath $installer){throw 'RC3 installer already exists; refusing to overwrite a candidate build.'}

if(-not $SkipBuild){
    & (Join-Path $repoRoot 'build_release.ps1')
    if($LASTEXITCODE-ne 0){exit $LASTEXITCODE}
}
if(-not(Test-Path -LiteralPath (Join-Path $publishRoot 'KitaoPhotoSelector.exe'))){throw '2.3.0 publish output is missing.'}

$iscc=@(
    (Join-Path $env:LOCALAPPDATA 'Programs\Inno Setup 7\ISCC.exe'),
    (Join-Path $env:LOCALAPPDATA 'Programs\Inno Setup 6\ISCC.exe'),
    'C:\Program Files (x86)\Inno Setup 6\ISCC.exe',
    'C:\Program Files\Inno Setup 6\ISCC.exe'
)|Where-Object{Test-Path -LiteralPath $_}|Select-Object -First 1
if(-not $iscc){throw 'Inno Setup was not found.'}
& $iscc '/DCandidateBuild' '/DCandidateRc3' (Join-Path $repoRoot 'installer\RAWSelectionAssistant.iss')
if($LASTEXITCODE-ne 0){exit $LASTEXITCODE}
if(-not(Test-Path -LiteralPath $installer)){throw 'RC3 installer was not generated with the required name.'}

$forbidden=@(Get-ChildItem $publishRoot -Recurse -File|Where-Object{$_.Name -match '(?i)(testhost|\.Tests\.|UiReview|Acceptance|Sony.*\.dll|Canon.*\.dll|Nikon.*\.dll|Fujifilm.*\.dll|\.pdb$|\.log$|\.db$|\.cube$|\.icc$|\.icm$)'})
if($forbidden.Count){throw ('Release scan found forbidden files: '+(($forbidden.FullName)-join ', '))}
$configs=Get-ChildItem $publishRoot -Recurse -File|Where-Object{$_.Extension -in @('.json','.config','.xml','.txt')}
if(@($configs|Select-String -Pattern 'localhost|127\.0\.0\.1' -ErrorAction SilentlyContinue).Count){throw 'Release scan found localhost or 127.0.0.1.'}
$license=Get-Content (Join-Path $publishRoot 'appsettings.license.json') -Raw -Encoding UTF8
if($license -notmatch '"Provider"\s*:\s*"None"' -or $license -match 'Mock|Fake Camera'){throw 'Release provider gate failed.'}
$version=(Get-Item (Join-Path $publishRoot 'KitaoPhotoSelector.exe')).VersionInfo
if($version.ProductVersion -notlike '2.3.0*' -or $version.FileVersion -notlike '2.3.0.0*'){throw 'Published version gate failed.'}

$acceptedAfter=@{
    Rc1=@{Bytes=(Get-Item $rc1).Length;Sha256=(Get-FileHash $rc1 -Algorithm SHA256).Hash}
    Rc2=@{Bytes=(Get-Item $rc2).Length;Sha256=(Get-FileHash $rc2 -Algorithm SHA256).Hash}
}
if($acceptedBefore.Rc1.Bytes-ne$acceptedAfter.Rc1.Bytes -or $acceptedBefore.Rc1.Sha256-ne$acceptedAfter.Rc1.Sha256 -or $acceptedBefore.Rc2.Bytes-ne$acceptedAfter.Rc2.Bytes -or $acceptedBefore.Rc2.Sha256-ne$acceptedAfter.Rc2.Sha256){throw 'RC1 or RC2 changed while producing RC3.'}

$signature=Get-AuthenticodeSignature $installer
$files=@(Get-ChildItem $publishRoot -Recurse -File)+@(Get-Item $installer)
$manifest=@($files|ForEach-Object{[ordered]@{Path=(Get-ScopedRelativePath $releaseRoot $_.FullName);Bytes=$_.Length;Sha256=(Get-FileHash $_.FullName -Algorithm SHA256).Hash}}|Sort-Object Path)
$manifest|ConvertTo-Json -Depth 5|Set-Content (Join-Path $releaseRoot 'file-manifest-rc3.json') -Encoding UTF8
$manifest|Export-Csv (Join-Path $releaseRoot 'file-manifest-rc3.csv') -NoTypeInformation -Encoding UTF8
$manifest|ForEach-Object{"$($_.Sha256) *$($_.Path)"}|Set-Content (Join-Path $releaseRoot 'SHA256SUMS-RC3.txt') -Encoding UTF8

$uiIndex=Join-Path $repoRoot 'artifacts\ui-review\2.3.0-rc3\evidence-index.json'
if(-not(Test-Path $uiIndex)){throw 'RC3 UI evidence index is missing.'}
Copy-Item $uiIndex (Join-Path $releaseRoot 'ui-evidence-index-rc3.json') -Force

$migration=[ordered]@{
    FromSchema=3;ToSchema=4;Migration='BookingPeopleAndFinanceMvp';NewTables=@('BookingContacts','BookingStaffMembers','FinanceCategories','FinanceTransactions')
    ExistingTablesPreserved=$true;BinaryPayloadColumnsAdded=$false;RollbackProtected=$true;IntegrityCheckRequired=$true
}
$migration|ConvertTo-Json -Depth 8|Set-Content (Join-Path $releaseRoot 'migration-manifest-rc3.json') -Encoding UTF8
$knownLimitations=@(
    '# Pixel Tart 2.3.0 RC3 known limitations',
    '',
    '- Physical dual-monitor acceptance remains pending until the user connects a second real display.',
    '- Office documents use a safe information card and system-open action; document bodies are not parsed in-app.',
    '- Finance is a local transaction-recording MVP without payments, invoices, cloud sync, or a general ledger.',
    '- The installer is unsigned because no production signing certificate is configured.'
) -join [Environment]::NewLine
$knownLimitations|Set-Content (Join-Path $releaseRoot 'known-limitations-rc3.md') -Encoding UTF8

$releaseManifest=[ordered]@{
    Product=$productName;Version='2.3.0';FileVersion='2.3.0.0';SchemaVersion=4;Candidate='RC3';Provider='None'
    PhysicalSecondMonitorTested=$false;GitCommit=(& git -C $repoRoot rev-parse HEAD).Trim();OutputType='WinExe';SelfContained=$true;Runtime='win-x64'
    Installer=(Get-ScopedRelativePath $releaseRoot $installer);InstallerSha256=(Get-FileHash $installer -Algorithm SHA256).Hash;InstallerBytes=(Get-Item $installer).Length
    SignatureStatus=$signature.Status.ToString();Rc1Preserved=$true;Rc1Sha256=$acceptedAfter.Rc1.Sha256;Rc2Preserved=$true;Rc2Sha256=$acceptedAfter.Rc2.Sha256
    ReleaseScan=[ordered]@{Passed=$true;NoTests=$true;NoVendorSdk=$true;NoLocalhost=$true;NoLogs=$true;NoDatabases=$true;NoSyntheticAssets=$true;ProviderNone=$true;NoFakeCamera=$true}
    UiEvidence='ui-evidence-index-rc3.json';MigrationManifest='migration-manifest-rc3.json';KnownLimitations='known-limitations-rc3.md';GeneratedAt=[DateTimeOffset]::Now.ToString('O')
}
$releaseManifest|ConvertTo-Json -Depth 10|Set-Content (Join-Path $releaseRoot 'release-manifest-rc3.json') -Encoding UTF8
$releaseManifest|ConvertTo-Json -Depth 10
