@echo off
setlocal EnableExtensions
chcp 65001 >nul
title Pixel Tart - Installed Acceptance
set "PIXEL_TART_KIT=%~dp0"
echo ========================================
echo Pixel Tart 安装版自动验收 - AcceptanceKit v3
echo ========================================
echo 验收包目录："%PIXEL_TART_KIT%"
echo 正在检查验收环境...
if not exist "%PIXEL_TART_KIT%acceptance-result" mkdir "%PIXEL_TART_KIT%acceptance-result" 2>nul
set "PIXEL_TART_LAUNCH_LOG=%PIXEL_TART_KIT%acceptance-result\acceptance-launch.log"
echo [%date% %time%] BAT START KIT_ROOT="%PIXEL_TART_KIT%">>"%PIXEL_TART_LAUNCH_LOG%" 2>nul
ver >>"%PIXEL_TART_LAUNCH_LOG%" 2>nul
if not exist "%PIXEL_TART_KIT%PixelTart.InstalledAcceptance.exe" goto missing_runner
if not exist "%PIXEL_TART_KIT%Launch-Acceptance.ps1" goto missing_launcher
set "PIXEL_TART_PREFLIGHT="
if /i "%~1"=="--preflight-only" set "PIXEL_TART_PREFLIGHT=-PreflightOnly"
powershell.exe -NoLogo -NoProfile -ExecutionPolicy Bypass -File "%PIXEL_TART_KIT%Launch-Acceptance.ps1" %PIXEL_TART_PREFLIGHT%
if errorlevel 1 (set "PIXEL_TART_EXIT=1") else (set "PIXEL_TART_EXIT=0")
echo [%date% %time%] Launcher ExitCode=%PIXEL_TART_EXIT% >>"%PIXEL_TART_LAUNCH_LOG%" 2>nul
goto finish
:missing_runner
echo [1/7] Runner FAIL
echo [FAIL] 未找到：PixelTart.InstalledAcceptance.exe
echo 请确认已经解压完整的 PixelTart-Installed-Acceptance-Kit-8cb95e6-v3.zip。
echo 不要单独运行 BAT。
echo Runner Exists=False; Installer Exists=NotChecked; Runner ExitCode=NOT_STARTED; Error=MissingRunner>>"%PIXEL_TART_LAUNCH_LOG%" 2>nul
if exist "%PIXEL_TART_KIT%Launch-Acceptance.ps1" powershell.exe -NoLogo -NoProfile -ExecutionPolicy Bypass -File "%PIXEL_TART_KIT%Launch-Acceptance.ps1" -PreflightOnly
set "PIXEL_TART_EXIT=1"
goto finish
:missing_launcher
echo [FAIL] 未找到 Launch-Acceptance.ps1，请重新解压完整 ZIP。
echo Runner Exists=True; Installer Exists=NotChecked; Runner ExitCode=NOT_STARTED; Error=MissingLauncher>>"%PIXEL_TART_LAUNCH_LOG%" 2>nul
set "PIXEL_TART_EXIT=1"
:finish
echo.
echo 启动日志："%PIXEL_TART_LAUNCH_LOG%"
echo 如目录不可写，请把 ZIP 解压到可写目录后重试。
echo 按任意键退出。
pause
exit /b %PIXEL_TART_EXIT%
