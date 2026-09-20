@echo off
chcp 65001 >nul
set "PIXEL_TART_KIT=%~dp0"
powershell.exe -NoProfile -Command "$p = Start-Process -FilePath (Join-Path $env:PIXEL_TART_KIT 'PixelTart.InstalledAcceptance.exe') -ArgumentList ('--kit ' + [char]34 + $env:PIXEL_TART_KIT.TrimEnd('\') + [char]34) -Verb RunAs -Wait -PassThru; exit $p.ExitCode"
echo 验收运行已结束，请把 acceptance-result 文件夹提供给 Codex。
echo 自动化失败不代表验收通过，请保留所有结果文件。
pause
