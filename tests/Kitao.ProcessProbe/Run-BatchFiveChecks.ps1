param([Parameter(Mandatory=$true)][string]$OutputRoot)
$ErrorActionPreference='Stop'
$probe=Join-Path $PSScriptRoot 'bin/Release/net10.0/Kitao.ProcessProbe.dll'
$results=@()
foreach($stage in @('Encoding','Encoded','Pending','Moved','Receipt'))
{
    $root=Join-Path $OutputRoot ('export-'+$stage+'-'+[Guid]::NewGuid().ToString('N'))
    & dotnet $probe export-crash $root $stage;if($LASTEXITCODE -ne 73){throw "Expected injected process exit at $stage"}
    $expected=if($stage -eq 'Encoding'){'Missing'}else{'Completed'}
    & dotnet $probe export-recover $root $expected;if($LASTEXITCODE){throw "Recovery failed at $stage"}
    & dotnet $probe export-recover $root $expected;if($LASTEXITCODE){throw "Repeated recovery failed at $stage"}
    $results+=[ordered]@{stage=$stage;exitCode=73;recovered=$expected;repeat='PASS'}
}
$root=Join-Path $OutputRoot ('cross-process-'+[Guid]::NewGuid().ToString('N'))
foreach($mode in @('undo-prepare','undo-other','undo-stale','version-prepare','version-update','version-stale')){& dotnet $probe $mode $root;if($LASTEXITCODE){throw $mode}}
$results|ConvertTo-Json -Depth 4
