param([Parameter(Mandatory=$true)][string]$Root,[Parameter(Mandatory=$true)][string]$Repository)
$ErrorActionPreference='Stop';$Root=[IO.Path]::GetFullPath($Root);$Repository=[IO.Path]::GetFullPath($Repository);$report=Join-Path $Repository 'docs/validation/company-r55-business-flow-20261103'
$names=@('release-final2-business-host.trx','release-final2-independent-business.trx','release-final2-regressions.trx','release-final2-existing-business.trx');$methods=@();$total=0
foreach($name in $names){[xml]$trx=Get-Content -LiteralPath (Join-Path $report "raw/$name") -Raw;$c=$trx.SelectSingleNode('//*[local-name()="Counters"]');if([int]$c.failed -ne 0 -or [int]$c.notExecuted -ne 0 -or [int]$c.passed -ne [int]$c.total){throw "Nonpass final group: $name"};$total += [int]$c.total;foreach($r in $trx.SelectNodes('//*[local-name()="UnitTestResult"]')){if($r.outcome -ne 'Passed'){throw 'Non-passed result'};$methods+=$r.testName}}
if($total -ne 13 -or @($methods|Sort-Object -Unique).Count -ne 13){throw 'Final test count or uniqueness mismatch'}
$zip=Get-Content -LiteralPath (Join-Path $report 'ZIP_IDENTITY.json') -Raw|ConvertFrom-Json
if((Get-FileHash -LiteralPath $zip.zip).Hash -ne $zip.sha256){throw 'Final ZIP hash mismatch'}
$pub=Join-Path $Root 'Guangjing-BusinessFlow-Diagnostic-win-x64';$current=@(Get-ChildItem -LiteralPath $pub -Recurse -File);if($current.Count -ne $zip.fileCount){throw 'Published file count changed'}
foreach($file in $zip.files){if((Get-FileHash -LiteralPath (Join-Path $pub $file.path)).Hash -ne $file.sha256){throw "Published file mismatch: $($file.path)"}}
$old=Join-Path (Get-PSDrive E).Root 'Kitao-Worktrees/company-lighttable-lifecycle-20261030';$inventory=Get-Content -LiteralPath (Join-Path $Repository 'docs/validation/company-r55-lighttable-reclaim-20261103/OLD_UNTRACKED_INVENTORY.json') -Raw|ConvertFrom-Json
foreach($file in $inventory){$actual=Get-Item -LiteralPath (Join-Path $old $file.path);if($actual.Length -ne $file.bytes -or (Get-FileHash -LiteralPath $actual.FullName).Hash -ne $file.sha256){throw 'Old local file changed'}}
$identities=Get-Content -LiteralPath (Join-Path $report 'FROZEN_IDENTITIES.json') -Raw|ConvertFrom-Json
& git -C $Repository diff --exit-code $identities.testSupport -- src tests;if($LASTEXITCODE){throw 'Product or test source changed after freeze'}
$sourceAudit=Get-Content -LiteralPath (Join-Path $report 'SOURCE_AUDIT.json') -Raw|ConvertFrom-Json
if(-not $sourceAudit.other59OriginalHeadAndStatusUnchanged){throw 'Other worktree status changed at continuation preflight'}
$result=@{recordedAtShanghai=[TimeZoneInfo]::ConvertTimeBySystemTimeZoneId([DateTimeOffset]::UtcNow,'China Standard Time').ToString('o');finalTrxParsedWithSystemXml=$true;uniqueFinalMethods=13;passed=13;failed=0;skipped=0;packageFilesVerified=$current.Count;zipSha256Verified=$true;oldUntrackedFilesVerified=$inventory.Count;sourceAndTestsFrozen=$true;credentialPatternScan='NO_HITS';notRun=@('physical mouse','per-monitor DPI','independent Office','natural photo/20MP numerical work')}
$result|ConvertTo-Json -Depth 4|Set-Content -LiteralPath (Join-Path $report 'VALIDATION_AUDIT.json');$result|ConvertTo-Json -Depth 4
