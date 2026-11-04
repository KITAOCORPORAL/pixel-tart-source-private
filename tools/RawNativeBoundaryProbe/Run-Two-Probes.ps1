param([Parameter(Mandatory)][string]$Sdk,[Parameter(Mandatory)][string]$Manifest,[Parameter(Mandatory)][string]$ProbeDll,[Parameter(Mandatory)][string]$ResultRoot)
$ErrorActionPreference='Stop'
# Never set or clear OMP/KMP variables, even to repair an inherited environment mismatch.
if(Test-Path -LiteralPath $ResultRoot){throw 'Preserve previous results; use a new root'}
New-Item -ItemType Directory $ResultRoot | Out-Null
$m=Get-Content -LiteralPath $Manifest -Raw|ConvertFrom-Json
$changes=@();$badFiles=@();$expected=$m.StartupEnvironment.PSObject.Properties
foreach($e in $expected){$actual=[Environment]::GetEnvironmentVariable($e.Name,'Process');if($actual -cne $e.Value){$changes+=@{name=$e.Name;expected=$e.Value;actual=$actual}}}
if($m.Environment.OMP_NUM_THREADS -cne '6'){throw 'Manifest changed original OMP environment'}
foreach($f in $m.Files){if(!(Test-Path -LiteralPath $f.Path) -or (Get-FileHash -LiteralPath $f.Path -Algorithm SHA256).Hash -cne $f.Sha256){$badFiles+=$f.Path}}
if((Get-FileHash -LiteralPath $m.Input.Path -Algorithm SHA256).Hash -cne $m.Input.Sha256){$badFiles+='AUTHORIZED_INPUT'}
$base=Split-Path $Manifest
foreach($s in @('first','second')){if(Test-Path -LiteralPath (Join-Path $base "$s.consumed")){throw 'A slot has already been consumed; no automatic retry'}}
@{status=$(if($changes.Count -or $badFiles.Count){'BLOCKED_PREFLIGHT_NO_RAW'}else{'IDENTITY_PREFLIGHT_READY_NOT_EXECUTED'});rawDecodeCalls=0;environmentChanges=$changes;invalidFiles=$badFiles;automaticRetry=$false;observerSetsOrClearsThreadEnvironment=$false;effectiveEnvironmentCheck='After unmodified binding initialization; OMP default occurs inside existing binding';timeAsiaShanghai=[TimeZoneInfo]::ConvertTimeBySystemTimeZoneId([DateTimeOffset]::UtcNow,'China Standard Time').ToString('o')}|ConvertTo-Json -Depth 8|Set-Content (Join-Path $ResultRoot 'PREFLIGHT.json')
if($changes.Count -or $badFiles.Count){Write-Output 'BLOCKED before any decode process: inherited environment or identity mismatch; do not override OMP variables';exit 3}
# The original audited runner enforces two serial independent processes, once per slot,
# 50ms memory sampling, 2GiB/600s limits, CreateNoWindow, and immediate stop on failure.
& (Join-Path $PSScriptRoot '../RawReopenStageProbe/Run-Two-Probes.ps1') -Sdk $Sdk -Manifest $Manifest -ProbeDll $ProbeDll -ResultRoot $ResultRoot
