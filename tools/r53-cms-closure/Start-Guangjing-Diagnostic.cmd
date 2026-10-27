@echo off
setlocal
if not "%~1"=="" (
 echo For a custom data root use Start-Guangjing-Diagnostic.ps1 from PowerShell 7.
 exit /b 1
)
if not exist "%~dp0app\KitaoPhotoSelector.exe" (
 echo Complete diagnostic application is missing.
 pause
 exit /b 1
)
set "PIXEL_TART_ISOLATED_RUNTIME=1"
set "PIXEL_TART_ISOLATED_RUNTIME_ROOT=%~dp0UserData\run-%RANDOM%-%RANDOM%"
if exist "%PIXEL_TART_ISOLATED_RUNTIME_ROOT%" (
 echo Unexpected data-root collision. Nothing was overwritten. Run again.
 exit /b 1
)
mkdir "%PIXEL_TART_ISOLATED_RUNTIME_ROOT%"
if errorlevel 1 exit /b 1
>"%PIXEL_TART_ISOLATED_RUNTIME_ROOT%\guangjing-r53-diagnostic-root.json" echo {"diagnosticOnly":true,"sourceSha":"b843670653dcb1899f77b873e31a11455d754f36"}
echo Diagnostic only - NOT FOR RELEASE.
echo Isolated data root: %PIXEL_TART_ISOLATED_RUNTIME_ROOT%
start "" /D "%~dp0app" "%~dp0app\KitaoPhotoSelector.exe"
endlocal