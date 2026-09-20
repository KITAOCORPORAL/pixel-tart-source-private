param([Parameter(Mandatory)][string]$Zip, [Parameter(Mandatory)][string]$TestDirectory)
$ErrorActionPreference = 'Stop'
$TestDirectory = [IO.Path]::GetFullPath($TestDirectory)
if (Test-Path -LiteralPath $TestDirectory) { throw 'Use a fresh test directory to preserve prior evidence.' }
New-Item -ItemType Directory -Path $TestDirectory | Out-Null
$zipCopy = Join-Path $TestDirectory ([IO.Path]::GetFileName($Zip))
Copy-Item -LiteralPath $Zip -Destination $zipCopy
Add-Type -AssemblyName System.IO.Compression.FileSystem
$kit = Join-Path $TestDirectory '解压 验收包'
[IO.Compression.ZipFile]::ExtractToDirectory($zipCopy, $kit)
$results = @()
function Invoke-Probe([string]$Name, [int]$Expected, [string]$Text) {
    Push-Location -LiteralPath $kit
    try {
        $output = @(& $env:ComSpec /d /c 'call "运行安装版验收.bat" --preflight-only <nul' 2>&1)
        $code = $LASTEXITCODE
        $output | Set-Content -LiteralPath (Join-Path $TestDirectory "$Name.txt") -Encoding utf8
        if ($code -ne $Expected -or ($output -join "`n") -notmatch [regex]::Escape($Text)) { throw "$Name failed: code=$code; $($output -join "`n")" }
        if (-not (Test-Path -LiteralPath (Join-Path $kit 'acceptance-result/acceptance-launch.log'))) { throw 'No launch log.' }
        if (-not (Test-Path -LiteralPath (Join-Path $kit 'acceptance-result.zip'))) { throw 'No diagnostic zip.' }
        $script:results += [ordered]@{ Case=$Name; ExitCode=$code; Expected=$Expected; Result='PASS'; LiveUI=$false }
        Write-Host "$Name PASS (exit $code)"
    } finally { Pop-Location }
}
Invoke-Probe 'portable-success' 0 'PORTABLE PREFLIGHT PASS'
foreach ($case in @(
    @('missing-runner','PixelTart.InstalledAcceptance.exe','未找到：PixelTart.InstalledAcceptance.exe'),
    @('missing-dependency','coreclr.dll','[2/7] 依赖文件 FAIL'),
    @('missing-installer','installer/PixelTart-DeveloperPreview-2.3.0-dev.8cb95e6-x64-Setup.exe','[5/7] Installer FAIL'),
    @('missing-full-plan','planning-full.plan.json','[3/7] Full Plan FAIL')
)) {
    $source = Join-Path $kit $case[1]
    $held = Join-Path $TestDirectory ($case[0] + '.held')
    # Reversible move of an exact file in this newly extracted test kit only.
    Move-Item -LiteralPath $source -Destination $held
    try { Invoke-Probe $case[0] 1 $case[2] }
    finally { Move-Item -LiteralPath $held -Destination $source }
}
$original = Join-Path $kit 'planning-full.plan.json'
$held = Join-Path $TestDirectory 'full-plan.held'
Move-Item -LiteralPath $original -Destination $held
Copy-Item -LiteralPath (Join-Path $kit 'upgrade-full.plan.json') -Destination $original
try { Invoke-Probe 'tampered-plan' 1 '[3/7] Full Plan FAIL' }
finally {
    Move-Item -LiteralPath $original -Destination (Join-Path $TestDirectory 'tampered-plan.fixture.json')
    Move-Item -LiteralPath $held -Destination $original
}
Invoke-Probe 'restored-success' 0 'PORTABLE PREFLIGHT PASS'
$results | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $TestDirectory 'portable-tests.json') -Encoding utf8
Write-Host "Portable evidence: $TestDirectory"
