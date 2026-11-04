param([string]$Repository,[string]$Root,[string]$Sdk)
$ErrorActionPreference='Stop';$Repository=[IO.Path]::GetFullPath($Repository);$Root=[IO.Path]::GetFullPath($Root)
$hostDir=Join-Path $Root 'published-testhost';if(Test-Path -LiteralPath $hostDir){throw 'Preserve existing testhost, use a new folder'}
Copy-Item -LiteralPath (Join-Path $Repository 'tests/RAWSelectionAssistant.WpfTests/bin/Release/net10.0-windows10.0.19041.0') -Destination $hostDir -Recurse
$published=Join-Path (Get-PSDrive E).Root 'Guangjing-BusinessFlow-20261103/Guangjing-BusinessFlow-Diagnostic-win-x64';$bindings=@()
foreach($file in Get-ChildItem -LiteralPath $published -Filter '*.dll' -File){$target=Join-Path $hostDir $file.Name;if(Test-Path -LiteralPath $target){Copy-Item -LiteralPath $file.FullName -Destination $target -Force;$a=(Get-FileHash -LiteralPath $file.FullName).Hash;$b=(Get-FileHash -LiteralPath $target).Hash;if($a -ne $b){throw 'Release binding mismatch'};$bindings+=@{file=$file.Name;releaseSha256=$a;boundSha256=$b}}}
$bindings|ConvertTo-Json -Depth 5|Set-Content -LiteralPath (Join-Path $Root 'PUBLISHED_BINDING.json')
$env:DOTNET_ROOT=Split-Path $Sdk;$env:PIXEL_TART_ISOLATED_RUNTIME='1';$env:PIXEL_TART_ACCEPTANCE='1';$env:KITAO_RAW_CHAIN_INPUTS=Join-Path $Root 'AUTHORIZED_SELECTED.json'
$results=@()
foreach($input in Get-Content -LiteralPath $env:KITAO_RAW_CHAIN_INPUTS -Raw|ConvertFrom-Json){
 $env:KITAO_RAW_CHAIN_ID=$input.id;$env:KITAO_RAW_CHAIN_ROOT=Join-Path $Root $input.id;$env:PIXEL_TART_ISOLATED_RUNTIME_ROOT=Join-Path $Root ('UserData-'+$input.id)
 if(Test-Path -LiteralPath $env:PIXEL_TART_ISOLATED_RUNTIME_ROOT){throw 'Existing fixture root preserved; choose a new review run'}
 & $Sdk vstest (Join-Path $hostDir 'RAWSelectionAssistant.WpfTests.dll') '/TestCaseFilter:FullyQualifiedName~RawColorChainReviewTests.FullAppDefaultNamedRawPreviewExportAndSave' "/Logger:trx;LogFileName=$($input.id)-producer.trx" "/ResultsDirectory:$Root/raw" *> (Join-Path $Root "raw/$($input.id)-producer.log")
 $producer=$LASTEXITCODE;Get-Content -LiteralPath (Join-Path $Root "raw/$($input.id)-producer.log") -Tail 7
 $reopen=$null
 if($producer -eq 0 -and (Test-Path -LiteralPath (Join-Path $env:KITAO_RAW_CHAIN_ROOT 'reopen-receipt.json'))){
 & $Sdk vstest (Join-Path $hostDir 'RAWSelectionAssistant.WpfTests.dll') '/TestCaseFilter:FullyQualifiedName~RawColorChainReviewTests.IndependentAppReopenNamedRawPreservesStateAndPreview' "/Logger:trx;LogFileName=$($input.id)-reopen.trx" "/ResultsDirectory:$Root/raw" *> (Join-Path $Root "raw/$($input.id)-reopen.log")
 $reopen=$LASTEXITCODE;Get-Content -LiteralPath (Join-Path $Root "raw/$($input.id)-reopen.log") -Tail 7
 }
 $results+=@{id=$input.id;producerExit=$producer;reopenExit=$reopen}
 $results|ConvertTo-Json -Depth 4|Set-Content -LiteralPath (Join-Path $Root 'RUN_RESULTS.json')
}
@{recordedAtShanghai=[TimeZoneInfo]::ConvertTimeBySystemTimeZoneId([DateTimeOffset]::UtcNow,'China Standard Time').ToString('o');productVersion=[Diagnostics.FileVersionInfo]::GetVersionInfo((Join-Path $hostDir 'KitaoPhotoSelector.dll')).ProductVersion;testHostSha256=(Get-FileHash -LiteralPath (Join-Path $hostDir 'RAWSelectionAssistant.WpfTests.dll')).Hash;bindings=$bindings.Count;decodeBudgetUnchanged=$true}|ConvertTo-Json|Set-Content -LiteralPath (Join-Path $Root 'RUN_IDENTITY.json')
