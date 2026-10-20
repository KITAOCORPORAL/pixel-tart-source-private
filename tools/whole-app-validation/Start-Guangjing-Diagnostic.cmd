@echo off
setlocal
set "PIXEL_TART_ISOLATED_RUNTIME=1"
set "PIXEL_TART_ISOLATED_RUNTIME_ROOT=%~dp0UserData"
set "PIXEL_TART_ACCEPTANCE_ROOT=%~dp0UserData"
set "PIXEL_TART_HUMAN_ACCEPTANCE=0"
start "" /D "%~dp0app" "%~dp0app\KitaoPhotoSelector.exe"
endlocal
