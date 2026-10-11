@echo off
rem Build portable exe + installer exe (plus win-unpacked dir). ASCII only.
setlocal
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0dist.ps1" -Target all
endlocal & exit /b %ERRORLEVEL%
