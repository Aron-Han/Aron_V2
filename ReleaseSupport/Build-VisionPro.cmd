@echo off
setlocal
title Aron - VisionPro Build
powershell.exe -NoLogo -NoProfile -ExecutionPolicy Bypass -File "%~dp0Build\Build-VisionPro.ps1" -Interactive %*
set "buildExit=%ERRORLEVEL%"
echo.
pause
exit /b %buildExit%
