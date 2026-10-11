param([string]$OutputRoot)
$ErrorActionPreference='Stop'
if(!$OutputRoot){throw 'Provide an isolated synthetic output directory.'}
$probe=Join-Path $PSScriptRoot 'bin/Release/net10.0/Kitao.ProcessProbe.dll'
$fixture=Join-Path $OutputRoot ([Guid]::NewGuid().ToString('N'))
& dotnet $probe setup $fixture
function Start-Probe([string[]]$Arguments){$info=[Diagnostics.ProcessStartInfo]::new('dotnet');$info.UseShellExecute=$false;$info.CreateNoWindow=$true;$info.ArgumentList.Add($probe);foreach($a in $Arguments){$info.ArgumentList.Add($a)};[Diagnostics.Process]::Start($info)}
$a=Start-Probe @('write',$fixture,'a','2');$b=Start-Probe @('write',$fixture,'b','4')
$deadline=[DateTime]::UtcNow.AddSeconds(20)
while(!(Test-Path (Join-Path $fixture 'a.ready')) -or !(Test-Path (Join-Path $fixture 'b.ready'))){if([DateTime]::UtcNow -gt $deadline){throw 'workers timed out'};Start-Sleep -Milliseconds 50}
Set-Content (Join-Path $fixture 'go') 'go';$a.WaitForExit();$b.WaitForExit()
$results=@(Get-Content (Join-Path $fixture 'a.result') -Raw|ConvertFrom-Json;Get-Content (Join-Path $fixture 'b.result') -Raw|ConvertFrom-Json)
if(@($results|Where-Object Applied).Count -ne 1){throw 'Expected one CAS winner'}
& dotnet $probe aba $fixture;if($LASTEXITCODE){throw 'ABA failed'}
$locked=Start-Probe @('lock',$fixture);$deadline=[DateTime]::UtcNow.AddSeconds(20)
while(!(Test-Path (Join-Path $fixture 'locked'))){if([DateTime]::UtcNow -gt $deadline){throw 'lock timeout'};Start-Sleep -Milliseconds 50}
$locked.Kill();$locked.WaitForExit();& dotnet $probe after-crash $fixture;if($LASTEXITCODE){throw 'crash release failed'}
foreach($mode in @('setup-tether','fail-tether','retry-tether','stale-tether')){& dotnet $probe $mode $fixture;if($LASTEXITCODE){throw $mode}}
[ordered]@{race='PASS';aba='PASS';crashRelease='PASS';restartRetry='PASS';staleProcess='PASS';pids=@($results.pid)}|ConvertTo-Json
