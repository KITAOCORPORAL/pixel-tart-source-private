[CmdletBinding()]
param(
 [Parameter(Mandatory=$true)][string]$SourceRoot,
 [Parameter(Mandatory=$true)][string]$Dotnet,
 [Parameter(Mandatory=$true)][string]$TestAssembly,
 [Parameter(Mandatory=$true)][string]$PublishedApp,
 [Parameter(Mandatory=$true)][string]$SameDiskTempParent,
 [Parameter(Mandatory=$true)][string]$OutputParent
)
$ErrorActionPreference='Stop'
$expected='0e5c3ea7e25c0c3d1773480cdc351ca9dc94b80f'
foreach($value in @($SourceRoot,$Dotnet,$TestAssembly,$PublishedApp,$SameDiskTempParent,$OutputParent)){
 if(-not [IO.Path]::IsPathFullyQualified($value)){throw 'Every input path must be absolute.'}
 if(-not (Test-Path -LiteralPath $value)){throw "Path not found: $value"}
}
$source=[IO.Path]::GetFullPath($SourceRoot)
$head=(& git -C $source rev-parse HEAD).Trim();if($LASTEXITCODE -ne 0){throw 'Invalid Git source root.'}
& git -C $source merge-base --is-ancestor $expected $head
if($LASTEXITCODE -ne 0){throw 'Expected product source is not an ancestor; inspect source before testing.'}
$sourceDiff=@(& git -C $source diff --name-status "$expected..$head" -- src tests)
$dirty=@(& git -C $source status --porcelain -- src tests)
if($sourceDiff.Count -gt 0 -or $dirty.Count -gt 0){throw 'Product/test source is not the exact reviewed baseline; do not mix results.'}
$publishedCore=Join-Path $PublishedApp 'RAWSelectionAssistant.Core.dll'
$testCore=Join-Path ([IO.Path]::GetDirectoryName([IO.Path]::GetFullPath($TestAssembly))) 'RAWSelectionAssistant.Core.dll'
if((Get-FileHash -LiteralPath $publishedCore).Hash -ne (Get-FileHash -LiteralPath $testCore).Hash){throw 'Test host Core differs from published Core.'}
$runName='home-scenario11-'+[DateTime]::UtcNow.ToString('yyyyMMdd-HHmmss')+'-'+[Guid]::NewGuid().ToString('N').Substring(0,8)
$output=Join-Path ([IO.Path]::GetFullPath($OutputParent)) $runName
$temp=Join-Path ([IO.Path]::GetFullPath($SameDiskTempParent)) $runName
if((Test-Path -LiteralPath $output) -or (Test-Path -LiteralPath $temp)){throw 'New output/temp directories only.'}
[IO.Directory]::CreateDirectory($output)|Out-Null;[IO.Directory]::CreateDirectory($temp)|Out-Null
$arguments=@('vstest',[IO.Path]::GetFullPath($TestAssembly),'/TestCaseFilter:FullyQualifiedName=RAWSelectionAssistant.Tests.AssetLibraryP35AcceptanceTests.Scenario11_TenKSwitchAndHundredKMetadataPreview','/Logger:trx;LogFileName=scenario11.trx',('/ResultsDirectory:'+$output))
$start=[Diagnostics.ProcessStartInfo]::new();$start.FileName=[IO.Path]::GetFullPath($Dotnet);$start.WorkingDirectory=$source;$start.UseShellExecute=$false;$start.CreateNoWindow=$true;$start.RedirectStandardOutput=$true;$start.RedirectStandardError=$true
foreach($argument in $arguments){[void]$start.ArgumentList.Add($argument)}
$start.Environment['TEMP']=$temp;$start.Environment['TMP']=$temp;$start.Environment['DOTNET_ROOT']=[IO.Path]::GetDirectoryName($start.FileName)
$start.Environment['PIXEL_TART_ISOLATED_RUNTIME']='1';$start.Environment['PIXEL_TART_ISOLATED_RUNTIME_ROOT']=Join-Path $output 'runtime'
$start.Environment['PIXEL_TART_HUMAN_ACCEPTANCE']='0';$start.Environment['PIXEL_TART_ACCEPTANCE_ROOT']=Join-Path $output 'runtime';$start.Environment['PIXEL_TART_PRODUCT_SOURCE_SHA']=$expected
$start.Environment['PIXEL_TART_P35_ACCEPTANCE_ROOT']=Join-Path $output 'measurements';$start.Environment['KITAO_TEST_RESOURCE_ROOT']=$source
$before=[DateTime]::UtcNow;$process=[Diagnostics.Process]::Start($start)
$stdout=$process.StandardOutput.ReadToEndAsync();$stderr=$process.StandardError.ReadToEndAsync();$wrapperTimedOut=-not $process.WaitForExit(180000)
if($wrapperTimedOut){$process.Kill($true);$process.WaitForExit()}
[IO.File]::WriteAllText((Join-Path $output 'stdout.log'),$stdout.GetAwaiter().GetResult());[IO.File]::WriteAllText((Join-Path $output 'stderr.log'),$stderr.GetAwaiter().GetResult())
$identity=[ordered]@{sourceSha=$expected;head=$head;pid=$process.Id;startUtc=$before.ToString('O');endUtc=[DateTime]::UtcNow.ToString('O');exitCode=$process.ExitCode;wrapperTimedOut=$wrapperTimedOut;wrapperLimitSeconds=180;command=@($start.FileName)+$arguments;temp=$temp;tempVolume=[IO.Path]::GetPathRoot($temp);coreHash=(Get-FileHash -LiteralPath $testCore).Hash;testHash=(Get-FileHash -LiteralPath $TestAssembly).Hash;thresholdMs=30000;switches=10000;metadataOutsideTimer=$true;hostTimezone=(Get-TimeZone).Id;scope='Original exact test. One run only, no automatic retries. Retain any failure.'}
$identity|ConvertTo-Json -Depth 5|Set-Content -LiteralPath (Join-Path $output 'identity.json') -Encoding utf8
Write-Output $output
if($process.ExitCode -ne 0){throw 'Original test did not pass. Keep TRX/logs; follow HOME_RETEST.md before any new diagnostic capture.'}
