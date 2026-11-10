param([Parameter(Mandatory)][string]$Sdk,[Parameter(Mandatory)][string]$BatchConfig,[Parameter(Mandatory)][string]$Manifest,[Parameter(Mandatory)][string]$HostDll,[Parameter(Mandatory)][string]$ResultRoot)
$ErrorActionPreference='Stop'
if(Test-Path -LiteralPath $ResultRoot){throw 'Preserve all earlier outputs'}
New-Item -ItemType Directory $ResultRoot|Out-Null
# Budget is the first gate: no binding/native initialization or child decode on a denied claim.
& node (Join-Path $PSScriptRoot 'CandidateBatch.mjs') --audit $BatchConfig (Join-Path $ResultRoot 'BUDGET_PREFLIGHT.json')
if($LASTEXITCODE -ne 0){throw 'Budget audit failed; no decode started'}
$b=Get-Content (Join-Path $ResultRoot 'BUDGET_PREFLIGHT.json') -Raw|ConvertFrom-Json
if(!$b.launchAllowed -or $b.remaining -le 0){@{status='NO_REMAINING_VERIFIED_BATCH_ALLOWANCE';rawCalls=0;decoderProcessesStarted=0;consumed=$b.consumed;remaining=$b.remaining;automaticRetry=$false}|ConvertTo-Json|Set-Content (Join-Path $ResultRoot 'EXECUTION_RECEIPT.json');exit 3}
$m=Get-Content -LiteralPath $Manifest -Raw|ConvertFrom-Json
if(!$m.NativeSelfTest -or !(Test-Path -LiteralPath $m.NativeSelfTest.Path)){throw 'Native self-test absent; no decode'}
# Before reservation require every frozen artifact and current startup env; host repeats all checks.
foreach($f in @($m.Files)+@($m.Native,$m.Binding,$m.Contract,$m.NativeSelfTest,$m.Input)){
 if((Get-FileHash -LiteralPath $f.Path).Hash -ine $f.Sha256){throw ('Frozen identity mismatch: '+$f.Path)}
}
foreach($p in $m.StartupEnvironment.PSObject.Properties){if([Environment]::GetEnvironmentVariable($p.Name) -cne $p.Value){throw 'Startup environment drift; never set/clear OMP/KMP'}}
$manifestHash=(Get-FileHash -LiteralPath $Manifest).Hash.ToLowerInvariant();$receipts=@();$limit=2147483648L
foreach($slot in (@('first','second')|Select-Object -Skip $b.consumed)){
 # Persist reservation before spawn. Any failed spawn/run is consumed, with no replacement.
 $ticket=Join-Path $ResultRoot "$slot.execution-ticket.json"
 & node (Join-Path $PSScriptRoot 'CandidateBatch.mjs') --reserve $BatchConfig $ticket $slot $manifestHash
 if($LASTEXITCODE -ne 0){throw 'Reservation denied; do not retry'}
 $start=[DateTimeOffset]::UtcNow;$clock=[Diagnostics.Stopwatch]::StartNew();$p=[Diagnostics.Process]::new();$si=[Diagnostics.ProcessStartInfo]::new();$si.FileName=$Sdk;$si.UseShellExecute=$false;$si.CreateNoWindow=$true;$si.RedirectStandardOutput=$true;$si.RedirectStandardError=$true;$si.WorkingDirectory=Split-Path $HostDll
 foreach($a in @($HostDll,'--decode-once',$Manifest,(Join-Path $ResultRoot $slot),$ticket)){$si.ArgumentList.Add($a)};$p.StartInfo=$si;$id=$null;$failed=$null;$exit=$null;$peak=0L;$maxPrivate=0L;$aborted=$false;$samples=[Collections.Generic.List[object]]::new()
 try{
  $p.Start()|Out-Null;$id=$p.Id;$stdout=$p.StandardOutput.ReadToEndAsync();$stderr=$p.StandardError.ReadToEndAsync()
  while(!$p.WaitForExit(50)){try{$p.Refresh();if($p.HasExited){break}}catch{if($p.HasExited){break};throw};$peak=[Math]::Max($peak,$p.PeakWorkingSet64);$maxPrivate=[Math]::Max($maxPrivate,$p.PrivateMemorySize64);$samples.Add(@{elapsedMs=$clock.Elapsed.TotalMilliseconds;workingSetBytes=$p.WorkingSet64;osPeakWorkingSetBytes=$p.PeakWorkingSet64;privateBytes=$p.PrivateMemorySize64});if($maxPrivate -gt $limit -or $peak -gt $limit -or $clock.Elapsed.TotalSeconds -gt 600){$aborted=$true;$p.Kill($true);$p.WaitForExit();break}}
  $exit=$p.ExitCode;$stdout.Result|Set-Content (Join-Path $ResultRoot "$slot.stdout.log");$stderr.Result|Set-Content (Join-Path $ResultRoot "$slot.stderr.log")
 }catch{$failed=$_.ToString()}finally{$p.Dispose();$samples|ConvertTo-Json -Depth 4|Set-Content (Join-Path $ResultRoot "$slot.resource-samples.json");$r=Get-Content $ticket -Raw|ConvertFrom-Json;$receipts+=@{reservationId=$r.reservationId;slot=$slot;pid=$id;startedAtUtc=$start.ToString('o');endedAtUtc=[DateTimeOffset]::UtcNow.ToString('o');exitCode=$exit;spawnOrRunError=$failed;aborted=$aborted;peakWorkingSetOsObserved=$peak;peakPrivateBytes50ms=$maxPrivate;sampleMs=50;elapsedGuardMs=$clock.Elapsed.TotalMilliseconds;limitBytes=$limit;automaticRetry=$false;status=$(if(!$failed -and !$aborted -and $exit -eq 0){'completed'}else{'failed'})};$receipts|ConvertTo-Json -Depth 6|Set-Content (Join-Path $ResultRoot 'PROCESS_RECEIPTS.json')}
 if($failed -or $aborted -or $exit -ne 0){$cfg=Get-Content $BatchConfig -Raw|ConvertFrom-Json;$stop=Join-Path $cfg.ledgerRoot 'STOP_AFTER_FAILURE.json';$payload=@{task=$cfg.task;status='STOPPED_AFTER_FAILURE';slot=$slot;reservationId=$r.reservationId;automaticRetry=$false;exitCode=$exit}|ConvertTo-Json;if(!(Test-Path -LiteralPath $stop)){$f=[IO.FileStream]::new($stop,[IO.FileMode]::CreateNew);try{$bytes=[Text.UTF8Encoding]::new($false).GetBytes($payload);$f.Write($bytes,0,$bytes.Length);$f.Flush($true)}finally{$f.Dispose()}};throw 'Consumed attempt failed; stop without retry or advancing'}
}
