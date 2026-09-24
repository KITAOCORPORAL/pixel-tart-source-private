param()

$ErrorActionPreference = 'Stop'
$repoRoot = [IO.Path]::GetFullPath($PSScriptRoot)
$dotnet = (Get-Command dotnet -ErrorAction Stop).Source

$publishRoot = [IO.Path]::GetFullPath((Join-Path $repoRoot 'artifacts\releases\2.3.0\publish'))
$publishDirectory = [IO.Path]::GetFullPath((Join-Path $publishRoot 'rc12-win-x64'))
if (-not $publishDirectory.StartsWith($publishRoot + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) {
    throw 'Refusing to clean a path outside the RC12 publish directory.'
}

$installerDirectory = [IO.Path]::GetFullPath((Join-Path $repoRoot 'artifacts\releases\2.3.0\installer'))
$installerPath = [IO.Path]::GetFullPath((Join-Path $installerDirectory '像素蛋挞_Setup_2.3.0_RC12_x64.exe'))
if (-not $installerPath.StartsWith($installerDirectory + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) {
    throw 'RC12 installer path escaped the intended release directory.'
}

$sourceHead = (& git -C $repoRoot rev-parse HEAD).Trim()
if (& git -C $repoRoot status --porcelain) { throw 'Working tree must be clean before final RC12 packaging.' }
if (Test-Path -LiteralPath $publishDirectory) { Remove-Item -LiteralPath $publishDirectory -Recurse -Force }

$startedAt = [DateTimeOffset]::Now
$timer = [Diagnostics.Stopwatch]::StartNew()
& $dotnet publish (Join-Path $repoRoot 'src\RAWSelectionAssistant\RAWSelectionAssistant.csproj') `
    -c Release -r win-x64 --self-contained true -p:Platform=x64 -p:PublishSingleFile=false `
    -p:DebugType=None -p:DebugSymbols=false "-p:SourceRevisionId=$sourceHead" -warnaserror `
    -o $publishDirectory --no-restore
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

$isccCandidates = @(
    (Join-Path $env:LOCALAPPDATA 'Programs\Inno Setup 7\ISCC.exe'),
    (Join-Path $env:LOCALAPPDATA 'Programs\Inno Setup 6\ISCC.exe'),
    'C:\Program Files (x86)\Inno Setup 6\ISCC.exe',
    'C:\Program Files\Inno Setup 6\ISCC.exe'
)
$iscc = $isccCandidates | Where-Object { Test-Path -LiteralPath $_ -PathType Leaf } | Select-Object -First 1
if (-not $iscc) { throw 'Inno Setup was not found; the final RC12 installer cannot be built.' }

& $iscc /DCandidateBuild /DCandidateRc12 (Join-Path $repoRoot 'installer\RAWSelectionAssistant.iss')
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
$timer.Stop()

if (-not (Test-Path -LiteralPath $installerPath -PathType Leaf)) { throw "Final RC12 installer was not produced: $installerPath" }
$installer = Get-Item -LiteralPath $installerPath
[pscustomobject]@{
    Product = '2.3.0-RC12'
    SourceHead = $sourceHead
    Path = $installer.FullName
    Bytes = $installer.Length
    SHA256 = (Get-FileHash -LiteralPath $installer.FullName -Algorithm SHA256).Hash
    StartedAt = $startedAt.ToString('O')
    FinishedAt = [DateTimeOffset]::Now.ToString('O')
    DurationSeconds = [Math]::Round($timer.Elapsed.TotalSeconds, 3)
    PublishFileCount = @(Get-ChildItem -LiteralPath $publishDirectory -File -Recurse).Count
} | ConvertTo-Json
