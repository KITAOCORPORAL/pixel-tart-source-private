@echo off
setlocal
if not exist "%~dp0test-pages\FIXTURE_IDENTITY.json" exit /b 2
> "%~dp0test-pages\FIXTURE_CONTROL.runtime.json" echo {"originalAllowed":true}
if errorlevel 1 exit /b 1
echo Synthetic 403 original flag: true.
exit /b 0
