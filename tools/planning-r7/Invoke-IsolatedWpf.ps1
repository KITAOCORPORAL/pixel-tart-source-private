param([string]$BinaryDirectory,[string]$SourceDirectory,[string]$OutputRoot,[int]$TimeoutSeconds=180,[string]$ClassPattern='*')
$ErrorActionPreference='Stop'
if($ClassPattern -ne '*'){throw 'Substring/source-pattern selection removed. Use verified_runner.py --classes with exact fully qualified classes.'}
$repo=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
& 'D:/AI AGENT/.dotnet/dotnet.exe' build (Join-Path $repo 'tools/VerifiedTestRunner') -c Release
if($LASTEXITCODE -ne 0){exit $LASTEXITCODE}
& python (Join-Path $PSScriptRoot 'verified_runner.py') --assembly (Join-Path $BinaryDirectory 'RAWSelectionAssistant.WpfTests.dll') --output $OutputRoot --timeout $TimeoutSeconds
exit $LASTEXITCODE
