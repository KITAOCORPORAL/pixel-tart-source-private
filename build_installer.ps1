param()

$ErrorActionPreference = 'Stop'
& "$PSScriptRoot\build_release.ps1"
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

$isccCandidates = @(
    (Join-Path $env:LOCALAPPDATA 'Programs\Inno Setup 7\ISCC.exe'),
    (Join-Path $env:LOCALAPPDATA 'Programs\Inno Setup 6\ISCC.exe'),
    'C:\Program Files (x86)\Inno Setup 6\ISCC.exe',
    'C:\Program Files\Inno Setup 6\ISCC.exe'
)
$iscc = $isccCandidates | Where-Object { Test-Path $_ } | Select-Object -First 1
if (-not $iscc) {
    throw '未找到 Inno Setup，请先安装后再构建安装包。'
}

& $iscc "$PSScriptRoot\installer\RAWSelectionAssistant.iss"
exit $LASTEXITCODE
