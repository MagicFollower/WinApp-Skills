@echo off
rem Stop ChatAPP dev processes started from this project. ASCII only.
setlocal
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0stop.ps1"
endlocal & exit /b %ERRORLEVEL%
