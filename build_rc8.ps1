param()
$ErrorActionPreference = 'Stop'
$dotnet = 'D:\AI AGENT\.dotnet\dotnet.exe'
$publishDirectory = Join-Path $PSScriptRoot 'artifacts\releases\2.3.0\publish\rc8-win-x64'
if (Test-Path -LiteralPath $publishDirectory) { Remove-Item -LiteralPath $publishDirectory -Recurse -Force }
& $dotnet publish "$PSScriptRoot\src\RAWSelectionAssistant\RAWSelectionAssistant.csproj" -c Release -r win-x64 --self-contained true -p:Platform=x64 -p:PublishSingleFile=false -p:DebugType=None -p:DebugSymbols=false -o $publishDirectory --no-restore
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
$isccCandidates = @((Join-Path $env:LOCALAPPDATA 'Programs\Inno Setup 7\ISCC.exe'),(Join-Path $env:LOCALAPPDATA 'Programs\Inno Setup 6\ISCC.exe'),'C:\Program Files (x86)\Inno Setup 6\ISCC.exe','C:\Program Files\Inno Setup 6\ISCC.exe')
$iscc = $isccCandidates | Where-Object { Test-Path -LiteralPath $_ } | Select-Object -First 1
if (-not $iscc) { throw 'Inno Setup not found.' }
& $iscc /DCandidateBuild /DCandidateRc8 "$PSScriptRoot\installer\RAWSelectionAssistant.iss"
exit $LASTEXITCODE
