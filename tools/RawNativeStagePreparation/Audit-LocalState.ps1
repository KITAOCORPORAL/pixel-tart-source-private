param(
 [Parameter(Mandatory)][string]$ProductWorktree,
 [Parameter(Mandatory)][string]$ResearchWorktree,
 [Parameter(Mandatory)][string]$SharedLocalRoot,
 [Parameter(Mandatory)][string]$AuthorizedRawPath,
 [Parameter(Mandatory)][string]$OutputDirectory,
 [string[]]$OtherWorktrees=@()
)
$ErrorActionPreference='Stop'
if(Test-Path -LiteralPath $OutputDirectory){throw 'Preserve earlier audits; require NEW output directory'}
$SharedLocalRoot=(Resolve-Path -LiteralPath $SharedLocalRoot).Path
function GitRead([string]$Root,[string[]]$Arguments){
 $answer=@(& git -C $Root @Arguments 2>&1)
 if($LASTEXITCODE -ne 0){throw "Git read failed: $Root / $($Arguments -join ' ') / $($answer -join ' ')"}
 return $answer
}
$trees=foreach($p in (@($ProductWorktree,$ResearchWorktree)+$OtherWorktrees)){
 $resolved=(Resolve-Path -LiteralPath $p).Path
 [ordered]@{path=$resolved;head=(GitRead $resolved @('rev-parse','HEAD')) -join '';branch=(GitRead $resolved @('branch','--show-current')) -join '';status=@(GitRead $resolved @('status','--porcelain'))}
}
# Search ONLY the explicitly supplied diagnostic root, never other disks or photos.
$files=@(Get-ChildItem -LiteralPath $SharedLocalRoot -Recurse -File | Where-Object {$_.Name -match '^(BUILD_STATUS|NO_RAW_SELF_TEST|PREPARATION|HOME_EXECUTION|CHECKS|PUSH_READBACK|calls|result|EXECUTION_PLAN|EXECUTION_RECEIPT)\.json$|\.consumed$|^stages\.jsonl$'} | ForEach-Object {
 [ordered]@{path=$_.FullName;bytes=$_.Length;sha256=(Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash}
})
$tools=[ordered]@{}
foreach($n in @('cl.exe','nmake.exe','dumpbin.exe','dotnet.exe')){$c=Get-Command $n -ErrorAction SilentlyContinue;$tools[$n]=if($c){$c.Source}else{$null}}
$keys=@('HKLM:/SOFTWARE/Microsoft/VisualStudio/SxS/VS7','HKLM:/SOFTWARE/WOW6432Node/Microsoft/VisualStudio/SxS/VS7','HKLM:/SOFTWARE/Microsoft/Windows Kits/Installed Roots','HKLM:/SOFTWARE/WOW6432Node/Microsoft/Windows Kits/Installed Roots')
$registry=foreach($key in $keys){[ordered]@{key=$key;exists=(Test-Path $key);values=if(Test-Path $key){Get-ItemProperty $key|Select-Object * -ExcludeProperty PS*}else{$null}}}
$rawExists=Test-Path -LiteralPath $AuthorizedRawPath -PathType Leaf
$rawSha=if($rawExists){(Get-FileHash -LiteralPath $AuthorizedRawPath -Algorithm SHA256).Hash}else{$null}
$missing=@('cl.exe','nmake.exe','dumpbin.exe','dotnet.exe')|Where-Object {!$tools[$_]}
$r=[ordered]@{
 timestampShanghai=[TimeZoneInfo]::ConvertTimeBySystemTimeZoneId([DateTimeOffset]::Now,'China Standard Time').ToString('o')
 scope='Named local worktrees and diagnostic root only; no company-live or other-machine inference'
 trees=@($trees);receipts=$files;tools=$tools;toolchainRegistry=@($registry)
 rawPresent=$rawExists;rawSha256=$rawSha;rawMatchesAuthorization=($rawSha -eq '52D054ED9A90306DB0B7EB5D6C408A2E006532B976AC88ED792692F9F744E865')
 companyLiveState='UNKNOWN_NOT_ACCESSIBLE'
 localConsumedSlots=@($files|Where-Object path -Match '\.consumed$').Count
 localRunReceipts=@($files|Where-Object path -Match '[/\\](calls|result)\.json$').Count
 localNativeDlls=@(Get-ChildItem -LiteralPath $SharedLocalRoot -Recurse -File | Where-Object Name -in @('raw_r.dll','libraw.dll')).Count
 missingTools=@($missing);nativeOrRawCallsByAudit=0
 nextOperation=if($missing.Count){'Supply x64 MSVC toolset and Windows SDK; then Build-Isolated in a NEW output root, no RAW'}else{'Inspect existing build/self-test and consumption receipts before any execution; audit is not decode authorization'}
}
New-Item -ItemType Directory -Path $OutputDirectory|Out-Null
$r|ConvertTo-Json -Depth 12|Set-Content -LiteralPath (Join-Path $OutputDirectory 'LOCAL_AUDIT.json') -Encoding utf8
Write-Output ([ordered]@{missingTools=@($missing);localConsumedSlots=$r.localConsumedSlots;localRunReceipts=$r.localRunReceipts;localNativeDlls=$r.localNativeDlls;rawMatchesAuthorization=$r.rawMatchesAuthorization;companyLiveState=$r.companyLiveState;nativeOrRawCalls=0;nextOperation=$r.nextOperation}|ConvertTo-Json -Depth 5)
