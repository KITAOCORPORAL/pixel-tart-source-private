@echo off
setlocal
"%~dp0native-host\Guangjing.Capture.NativeHost.exe" --prepare
set "GUANGJING_PREPARE_RESULT=%errorlevel%"
if not "%GUANGJING_PREPARE_RESULT%"=="0" echo Preparation failed. Native Host exit code: %GUANGJING_PREPARE_RESULT%
pause
exit /b %GUANGJING_PREPARE_RESULT%
