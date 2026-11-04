param([string]$Sdk,[string]$Manifest,[string]$ProbeDll,[string]$ResultRoot)
$ErrorActionPreference='Stop'
$env:DOTNET_ROOT=Split-Path $Sdk
$items=@();$limit=2147483648L
foreach($slot in @('first','second')){
 $dest=Join-Path $ResultRoot $slot
 if(Test-Path -LiteralPath $dest){throw 'Preserve existing result; no retry'}
 $p=[Diagnostics.Process]::new();$p.StartInfo=[Diagnostics.ProcessStartInfo]::new();$p.StartInfo.FileName=$Sdk;$p.StartInfo.UseShellExecute=$false;$p.StartInfo.CreateNoWindow=$true;$p.StartInfo.RedirectStandardOutput=$true;$p.StartInfo.RedirectStandardError=$true;$p.StartInfo.WorkingDirectory=Split-Path $ProbeDll
 foreach($a in @($ProbeDll,'--decode-once',$Manifest,$dest,$slot)){$p.StartInfo.ArgumentList.Add($a)}
 $started=[DateTimeOffset]::UtcNow;$p.Start()|Out-Null;$pidActual=$p.Id;$out=$p.StandardOutput.ReadToEndAsync();$err=$p.StandardError.ReadToEndAsync();$maxPrivate=0L;$maxWorking=0L;$osPeak=0L;$aborted=$false
 while(!$p.WaitForExit(50)){$p.Refresh();$maxPrivate=[Math]::Max($maxPrivate,$p.PrivateMemorySize64);$maxWorking=[Math]::Max($maxWorking,$p.WorkingSet64);$osPeak=[Math]::Max($osPeak,$p.PeakWorkingSet64);if($maxPrivate -gt $limit -or $maxWorking -gt $limit){$aborted=$true;$p.Kill($true);$p.WaitForExit();break};if(([DateTimeOffset]::UtcNow-$started).TotalSeconds -gt 600){$aborted=$true;$p.Kill($true);$p.WaitForExit();break}}
 $exit=$p.ExitCode;$out.Result|Set-Content (Join-Path $ResultRoot "$slot.stdout.log");$err.Result|Set-Content (Join-Path $ResultRoot "$slot.stderr.log")
 $items+=@{slot=$slot;processId=$pidActual;startedAtUtc=$started.ToString('o');finishedAtUtc=[DateTimeOffset]::UtcNow.ToString('o');exitCode=$exit;aborted=$aborted;peakPrivateBytesSampled=$maxPrivate;peakWorkingSetBytesSampled=$maxWorking;peakWorkingSetOsObserved=$osPeak;samplePeriodMs=50;hardLimitBytes=$limit;sequential=$true;nativeThreadPolicy='UNCHANGED';automaticRetry=$false}
 $items|ConvertTo-Json -Depth 5|Set-Content (Join-Path $ResultRoot 'PROCESS_RECEIPTS.json');$p.Dispose();$items[-1]|ConvertTo-Json -Compress
 if($exit -ne 0 -or $aborted){throw 'Probe did not complete; do not retry or advance to additional decode'}
}
