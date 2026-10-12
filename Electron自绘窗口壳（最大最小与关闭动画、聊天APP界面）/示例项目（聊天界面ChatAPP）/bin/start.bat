@echo off
rem Start ChatAPP dev server + Electron window. ASCII only.
setlocal
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0start.ps1"
endlocal & exit /b %ERRORLEVEL%
