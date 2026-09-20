param([Parameter(Mandatory)][string]$Destination,[Parameter(Mandatory)][string]$Installer,[Parameter(Mandatory)][string]$OldInstaller,[Parameter(Mandatory)][string]$PopplerDirectory,[string]$Dotnet='D:\AI AGENT\.dotnet\dotnet.exe')
$ErrorActionPreference='Stop'
$repo=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\..'))
$Destination=[IO.Path]::GetFullPath($Destination)
if(!$Destination.StartsWith((Join-Path $repo 'artifacts')+'\',[StringComparison]::OrdinalIgnoreCase) -or (Test-Path -LiteralPath $Destination)){throw 'Fresh internal artifact directory required'}
New-Item -ItemType Directory -Path $Destination|Out-Null
& $Dotnet publish (Join-Path $PSScriptRoot 'PixelTart.InstalledAcceptance.csproj') -c Release -r win-x64 --self-contained true -o $Destination
if($LASTEXITCODE -ne 0){throw 'Runner publish failed'}
foreach($file in @('planning-full.plan.json','upgrade-full.plan.json','selectors.json','运行安装版验收.bat','运行最终候选验收.bat','Launch-Acceptance.ps1','README_验收说明.md','README_最终候选验收.md','KIT_VERSION.txt')){Copy-Item -LiteralPath (Join-Path $PSScriptRoot $file) -Destination $Destination}
$candidateBat=Join-Path $Destination '运行最终候选验收.bat';[IO.File]::WriteAllText($candidateBat,([IO.File]::ReadAllText($candidateBat)-replace '\r?\n',"`r`n"),[Text.UTF8Encoding]::new($false))
$bat=Join-Path $Destination '运行安装版验收.bat';[IO.File]::WriteAllText($bat,([IO.File]::ReadAllText($bat)-replace '\r?\n',"`r`n"),[Text.UTF8Encoding]::new($false))
$launcher=Join-Path $Destination 'Launch-Acceptance.ps1';[IO.File]::WriteAllText($launcher,([IO.File]::ReadAllText($launcher)-replace '\r?\n',"`r`n"),[Text.UTF8Encoding]::new($true))
New-Item -ItemType Directory -Path (Join-Path $Destination 'installer')|Out-Null
foreach($path in @($Installer,$OldInstaller)){Copy-Item -LiteralPath $path -Destination (Join-Path $Destination 'installer')}
Copy-Item -LiteralPath $PopplerDirectory -Destination (Join-Path $Destination 'poppler') -Recurse
$runner=Join-Path $Destination 'PixelTart.InstalledAcceptance.exe'
foreach($flag in @('--self-test','--selector-tests','--navigation-tests')){& $runner $flag;if($LASTEXITCODE -ne 0){throw "$flag failed"}}
$manifest=Get-ChildItem -LiteralPath $Destination -File -Recurse|ForEach-Object{[ordered]@{RelativePath=[IO.Path]::GetRelativePath($Destination,$_.FullName).Replace('\','/');Size=$_.Length;SHA256=(Get-FileHash -LiteralPath $_.FullName).Hash}}
$manifest|ConvertTo-Json -Depth 5|Set-Content -LiteralPath (Join-Path $Destination 'KIT_MANIFEST.json') -Encoding utf8
& $runner --kit $Destination --preflight-only
if($LASTEXITCODE -ne 0){throw 'Internal preflight failed'}
# Deliberately no ZIP and no live invocation: this is internal build input, not delivery.
Write-Host 'INTERNAL STAGE ONLY. Candidate Gate has not been satisfied.'
