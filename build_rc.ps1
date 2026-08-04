param()

$ErrorActionPreference = 'Stop'
$dotnet = Join-Path $env:LOCALAPPDATA 'Microsoft\dotnet-sdk-10\dotnet.exe'
if (-not (Test-Path -LiteralPath $dotnet)) {
    $workspaceDotnet = Join-Path (Split-Path $PSScriptRoot -Parent) '.dotnet\dotnet.exe'
    $dotnet = if (Test-Path -LiteralPath $workspaceDotnet) { $workspaceDotnet } else { (Get-Command dotnet -ErrorAction Stop).Source }
}

$publishRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot 'artifacts\releases\2.2.0\rc\publish'))
$publishDirectory = [IO.Path]::GetFullPath((Join-Path $publishRoot 'win-x64'))
if (-not $publishDirectory.StartsWith($publishRoot + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) {
    throw 'Refusing to clean an RC path outside the 2.2.0 RC artifacts directory.'
}
if (Test-Path -LiteralPath $publishDirectory) {
    Remove-Item -LiteralPath $publishDirectory -Recurse -Force
}

& $dotnet publish "$PSScriptRoot\src\RAWSelectionAssistant\RAWSelectionAssistant.csproj" -c Release -r win-x64 --self-contained true -p:Platform=x64 -p:PublishSingleFile=false -o $publishDirectory --no-restore
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

$isccCandidates = @(
    (Join-Path $env:LOCALAPPDATA 'Programs\Inno Setup 7\ISCC.exe'),
    (Join-Path $env:LOCALAPPDATA 'Programs\Inno Setup 6\ISCC.exe'),
    'C:\Program Files (x86)\Inno Setup 6\ISCC.exe',
    'C:\Program Files\Inno Setup 6\ISCC.exe'
)
$iscc = $isccCandidates | Where-Object { Test-Path -LiteralPath $_ } | Select-Object -First 1
if (-not $iscc) {
    throw 'Inno Setup was not found; the isolated RC installer cannot be built.'
}

& $iscc /DCandidateBuild "$PSScriptRoot\installer\RAWSelectionAssistant.iss"
exit $LASTEXITCODE
