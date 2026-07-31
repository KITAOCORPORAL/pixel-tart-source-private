param()

# 仅供开发调试。普通用户应从桌面快捷方式直接启动已安装的 EXE。
$ErrorActionPreference = 'Stop'
$exe = Join-Path $PSScriptRoot 'src\RAWSelectionAssistant\bin\Debug\net10.0-windows10.0.19041.0\win-x64\KitaoPhotoSelector.exe'
if (-not (Test-Path $exe)) {
    & "$PSScriptRoot\build_debug.ps1"
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
}
Start-Process -FilePath $exe
