param([string]$Repository,[string]$EvidenceRoot)
$ErrorActionPreference='Stop'
if(Test-Path -LiteralPath (Join-Path $EvidenceRoot 'REUSE_VERIFICATION.json')){throw 'Preserve the existing receipt; choose a new evidence directory'}
$run='E:/Guangjing-RawColorChain-20261103/pass4'
$pub='E:/Guangjing-BusinessFlow-20261103/Guangjing-BusinessFlow-Diagnostic-win-x64'
$manifest=Get-Content (Join-Path $Repository 'docs/validation/company-r55-business-flow-20261103/ZIP_IDENTITY.json') -Raw|ConvertFrom-Json
function Hash([string]$p){(Get-FileHash -LiteralPath $p -Algorithm SHA256).Hash}
$binding=Get-Content "$run/PUBLISHED_BINDING.json" -Raw|ConvertFrom-Json
$verified=@();foreach($b in $binding){$a=Hash "$pub/$($b.file)";$h=Hash "$run/published-testhost/$($b.file)";if($a -ne $b.releaseSha256 -or $h -ne $a){throw "Binding changed: $($b.file)"};$verified+=@{file=$b.file;sha256=$a}}
$zipHash=Hash $manifest.zip;if($zipHash -ne $manifest.sha256){throw 'ZIP identity mismatch'}
$zip=[IO.Compression.ZipFile]::OpenRead($manifest.zip)
try {
 $entries=@($zip.Entries|Where-Object {$_.Name})
 if($entries.Count -ne $manifest.fileCount){throw 'ZIP entry count mismatch'}
 foreach($f in $manifest.files){$p=Join-Path $pub $f.path;if((Hash $p) -ne $f.sha256){throw "Directory identity mismatch: $($f.path)"};$entry=$zip.GetEntry('Guangjing-BusinessFlow-Diagnostic-win-x64/'+$f.path.Replace('\','/'));if(!$entry){throw "ZIP missing $($f.path)"};$s=$entry.Open();try{$h=[Convert]::ToHexString([Security.Cryptography.SHA256]::HashData($s))}finally{$s.Dispose()};if($h -ne $f.sha256){throw "ZIP entry mismatch: $($f.path)"}}
}finally{$zip.Dispose()}
$authorization=Get-Content (Join-Path $Repository 'docs/evidence/company-validation-20261015/AUTHORIZED_RAW_INPUTS.json') -Raw|ConvertFrom-Json
$inputs=Get-Content "$run/AUTHORIZED_SELECTED.json" -Raw|ConvertFrom-Json
$inputChecks=@();foreach($i in $inputs){$auth=@($authorization.files|Where-Object {$_.id -eq $i.id -and $_.sha256 -eq $i.sha256});if($auth.Count -ne 1){throw 'Authorization mismatch'};$h=Hash $i.path;if($h -ne $i.sha256){throw 'Input changed'};$inputChecks+=@{id=$i.id;name=$i.name;sha256=$h;authorizationMatch=$true}}
$outputChecks=@();foreach($id in @('company-02','company-05')){foreach($phase in @('default','display-exposure-plus05','independent-reopen')){$o=Get-Content "$run/$id/$phase-output.json" -Raw|ConvertFrom-Json;$h=Hash $o.path;if($h -ne $o.sha256){throw "Output changed $id $phase"};$outputChecks+=@{id=$id;phase=$phase;path=$o.path;sha256=$h;maximum8BitDifference=$o.parity.maximum8BitDifference;processingVersion=$o.ProcessingVersion}}}
$testSource=Join-Path $Repository 'tests/RAWSelectionAssistant.WpfTests/RawColorChainReviewTests.cs'
$testDll="$run/published-testhost/RAWSelectionAssistant.WpfTests.dll"
$testExpected=Get-Content "$run/RUN_IDENTITY.json" -Raw|ConvertFrom-Json
if((Hash $testDll) -ne $testExpected.testHostSha256){throw 'Test assembly mismatch'}
$buildOutput=Join-Path $Repository 'tests/RAWSelectionAssistant.WpfTests/bin/Release/net10.0-windows10.0.19041.0/RAWSelectionAssistant.WpfTests.dll'
if((Hash $buildOutput) -ne (Hash $testDll)){throw 'Current test build output differs'}
$result=@{recordedAtClientLocal=[TimeZoneInfo]::ConvertTimeBySystemTimeZoneId([DateTimeOffset]::UtcNow,'W. Europe Standard Time').ToString('o');mode='READ_ONLY_REUSE_NO_RAW_DECODE';bindingCount=$verified.Count;bindings=$verified;zip=$manifest.zip;zipSha256=$zipHash;zipFilesVerified=$entries.Count;productVersion=[Diagnostics.FileVersionInfo]::GetVersionInfo("$pub/KitaoPhotoSelector.dll").ProductVersion;productSource=$manifest.productSource;WholeAppDiagnosticBuild=$manifest.WholeAppDiagnosticBuild;PhotographerTrialBuild=$manifest.PhotographerTrialBuild;testSourceSha256=(Hash $testSource);testAssemblySha256=(Hash $testDll);testAssemblyCurrentBuildMatch=$true;inputs=$inputChecks;outputs=$outputChecks;newProductBuild=$false;newRawDecodes=0}
$result|ConvertTo-Json -Depth 8|Set-Content (Join-Path $EvidenceRoot 'REUSE_VERIFICATION.json')
$result|Select-Object mode,bindingCount,zipFilesVerified,productSource,testAssemblyCurrentBuildMatch,newRawDecodes|ConvertTo-Json
