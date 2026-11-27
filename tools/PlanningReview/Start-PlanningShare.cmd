@echo off
setlocal
pwsh.exe -NoLogo -NoProfile -File "%~dp0Start-ShareCandidate.ps1" -Delivery "%~dp0."
if errorlevel 1 (
    echo Could not start the isolated Planning Share candidate. See HOME_HANDOFF.md.
    pause
    exit /b 1
)
