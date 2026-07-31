param()

$ErrorActionPreference = 'Stop'
$dotnet = Join-Path $env:LOCALAPPDATA 'Microsoft\dotnet-sdk-10\dotnet.exe'
if (-not (Test-Path $dotnet)) {
    $dotnet = (Get-Command dotnet -ErrorAction Stop).Source
}
$solution = Join-Path $PSScriptRoot 'RAWSelectionAssistant.sln'
$publishDirectory = Join-Path $PSScriptRoot 'artifacts\publish\win-x64'
$publishRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot 'artifacts\publish'))
$resolvedPublishDirectory = [System.IO.Path]::GetFullPath($publishDirectory)
if (-not $resolvedPublishDirectory.StartsWith($publishRoot + [System.IO.Path]::DirectorySeparatorChar, [System.StringComparison]::OrdinalIgnoreCase)) {
    throw 'Refusing to clean a publish path outside the project artifacts directory.'
}

& $dotnet clean $solution -c Release -p:Platform=x64
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
& $dotnet build $solution -c Release -p:Platform=x64 --no-restore
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
& $dotnet test $solution -c Release -p:Platform=x64 --no-build
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
if (Test-Path -LiteralPath $resolvedPublishDirectory) {
    Remove-Item -LiteralPath $resolvedPublishDirectory -Recurse -Force
}
& $dotnet publish "$PSScriptRoot\src\RAWSelectionAssistant\RAWSelectionAssistant.csproj" `
    -c Release -r win-x64 --self-contained true -p:Platform=x64 -p:PublishSingleFile=false `
    -o $publishDirectory --no-restore
exit $LASTEXITCODE
