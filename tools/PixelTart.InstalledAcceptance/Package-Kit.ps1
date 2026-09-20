param(
    [string]$Dotnet = 'D:\AI AGENT\.dotnet\dotnet.exe',
    [string]$Poppler = 'C:\Users\Administrator\.cache\codex-runtimes\codex-primary-runtime\dependencies\native\poppler',
    [string]$Destination
)
$ErrorActionPreference = 'Stop'
$repo = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\..'))
if (-not $Destination) { $Destination = Join-Path $repo 'artifacts\installed-acceptance-kit' }
$Destination = [IO.Path]::GetFullPath($Destination)
if (-not $Destination.StartsWith($repo + '\', [StringComparison]::OrdinalIgnoreCase)) { throw 'Package destination must stay inside this repository.' }
New-Item -ItemType Directory -Path $Destination -Force | Out-Null
& $Dotnet publish (Join-Path $PSScriptRoot 'PixelTart.InstalledAcceptance.csproj') -c Release -r win-x64 --self-contained true -o $Destination
if ($LASTEXITCODE -ne 0) { throw 'Runner publish failed.' }
foreach ($file in @('planning-full.plan.json','upgrade-full.plan.json','运行安装版验收.bat','README_验收说明.md','THIRD_PARTY_NOTICES.md')) {
    Copy-Item -LiteralPath (Join-Path $PSScriptRoot $file) -Destination $Destination -Force
}
$installerDir = Join-Path $Destination 'installers'
New-Item -ItemType Directory -Path $installerDir -Force | Out-Null
foreach ($version in @('8cb95e6','8729d17')) {
    $name = "PixelTart-DeveloperPreview-2.3.0-dev.$version-x64-Setup.exe"
    Copy-Item -LiteralPath (Join-Path $repo "artifacts\planning-proposal-final\builds\2.3.0-dev.$version\installer\$name") -Destination $installerDir -Force
}
$popplerDir = Join-Path $Destination 'poppler'
New-Item -ItemType Directory -Path $popplerDir -Force | Out-Null
Get-ChildItem -LiteralPath (Join-Path $Poppler 'Library\bin') -File | Where-Object { $_.Extension -eq '.dll' -or $_.Name -in @('pdfinfo.exe','pdftoppm.exe') } | Copy-Item -Destination $popplerDir -Force
Copy-Item -LiteralPath (Join-Path $Poppler 'manifest.json') -Destination $popplerDir -Force
foreach ($plan in @('planning-full.plan.json','upgrade-full.plan.json')) {
    & $Dotnet (Join-Path $Destination 'PixelTart.InstalledAcceptance.dll') --validate (Join-Path $Destination $plan)
    if ($LASTEXITCODE -ne 0) { throw "Validation failed: $plan" }
}
& $Dotnet (Join-Path $Destination 'PixelTart.InstalledAcceptance.dll') --self-test
if ($LASTEXITCODE -ne 0) { throw 'Offline self-test failed.' }
$manifest = Get-ChildItem -LiteralPath $Destination -File -Recurse | Where-Object { $_.Name -ne 'integrity.json' -and $_.FullName -notmatch '\\acceptance-result\\' } | ForEach-Object {
    [ordered]@{ Path = [IO.Path]::GetRelativePath($Destination, $_.FullName); Bytes = $_.Length; SHA256 = (Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash }
}
# Generated build artifact, not a source edit.
$manifest | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath (Join-Path $Destination 'integrity.json') -Encoding utf8
Write-Host "AWAITING LOCAL ACCEPTANCE RUN: $Destination"
Write-Host 'No installer, application, UI automation, or screenshot API was executed.'
