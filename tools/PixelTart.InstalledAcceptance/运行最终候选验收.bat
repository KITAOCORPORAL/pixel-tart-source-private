@echo off
setlocal
call "%~dp0运行安装版验收.bat" %*
exit /b %errorlevel%
