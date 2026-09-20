param(
    [string]$Dotnet = 'D:\AI AGENT\.dotnet\dotnet.exe',
    [string]$Poppler = 'C:\Users\Administrator\.cache\codex-runtimes\codex-primary-runtime\dependencies\native\poppler',
    [string]$Destination
)
$ErrorActionPreference = 'Stop'
$repo = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\..'))
if (-not $Destination) { $Destination = Join-Path $repo ('artifacts\installed-acceptance-kit-portable-' + (Get-Date -Format 'yyyyMMdd-HHmmss')) }
$Destination = [IO.Path]::GetFullPath($Destination)
if (-not $Destination.StartsWith($repo + '\', [StringComparison]::OrdinalIgnoreCase)) { throw 'Package destination must stay inside this repository.' }
New-Item -ItemType Directory -Path $Destination -Force | Out-Null
& $Dotnet publish (Join-Path $PSScriptRoot 'PixelTart.InstalledAcceptance.csproj') -c Release -r win-x64 --self-contained true -o $Destination
if ($LASTEXITCODE -ne 0) { throw 'Runner publish failed.' }
foreach ($file in @('planning-full.plan.json','upgrade-full.plan.json','运行安装版验收.bat','Launch-Acceptance.ps1','README_验收说明.md','THIRD_PARTY_NOTICES.md','KIT_VERSION.txt')) {
    Copy-Item -LiteralPath (Join-Path $PSScriptRoot $file) -Destination $Destination -Force
}
# Mechanical encoding normalization: CMD requires CRLF; Windows PowerShell 5.1
# requires BOM for Chinese source. No SDK/build script is shipped.
$batPath = Join-Path $Destination '运行安装版验收.bat'
[IO.File]::WriteAllText($batPath, ([IO.File]::ReadAllText($batPath) -replace '\r?\n', "`r`n"), [Text.UTF8Encoding]::new($false))
$launcherPath = Join-Path $Destination 'Launch-Acceptance.ps1'
[IO.File]::WriteAllText($launcherPath, ([IO.File]::ReadAllText($launcherPath) -replace '\r?\n', "`r`n"), [Text.UTF8Encoding]::new($true))
$installerDir = Join-Path $Destination 'installer'
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
    & $Dotnet (Join-Path $Destination 'PixelTart.InstalledAcceptance.dll') --lint-plan (Join-Path $Destination $plan)
    if ($LASTEXITCODE -ne 0) { throw "Selector lint failed: $plan" }
}
& $Dotnet (Join-Path $Destination 'PixelTart.InstalledAcceptance.dll') --self-test
if ($LASTEXITCODE -ne 0) { throw 'Offline self-test failed.' }
& $Dotnet (Join-Path $Destination 'PixelTart.InstalledAcceptance.dll') --selector-tests
if ($LASTEXITCODE -ne 0) { throw 'Selector tests failed.' }
& $Dotnet (Join-Path $Destination 'PixelTart.InstalledAcceptance.dll') --navigation-tests
if ($LASTEXITCODE -ne 0) { throw 'Navigation regression failed.' }
$manifest = Get-ChildItem -LiteralPath $Destination -File -Recurse | Where-Object { $_.Name -ne 'KIT_MANIFEST.json' -and $_.FullName -notmatch '\\acceptance-result\\' } | ForEach-Object {
    [ordered]@{ RelativePath = [IO.Path]::GetRelativePath($Destination, $_.FullName).Replace('\','/'); Size = $_.Length; SHA256 = (Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash }
}
# Generated build artifact, not a source edit.
$manifest | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath (Join-Path $Destination 'KIT_MANIFEST.json') -Encoding utf8
$zip = Join-Path (Split-Path $Destination -Parent) 'PixelTart-Installed-Acceptance-Kit-8cb95e6-v3.zip'
if (Test-Path -LiteralPath $zip) { throw "ZIP already exists; preserve previous delivery: $zip" }
Add-Type -AssemblyName System.IO.Compression.FileSystem
[IO.Compression.ZipFile]::CreateFromDirectory($Destination, $zip, [IO.Compression.CompressionLevel]::Optimal, $false)
Write-Host "ZIP: $zip"
Get-FileHash -LiteralPath $zip -Algorithm SHA256
Write-Host "AWAITING LOCAL ACCEPTANCE RUN: $Destination"
Write-Host 'No installer, application, UI automation, or screenshot API was executed.'
