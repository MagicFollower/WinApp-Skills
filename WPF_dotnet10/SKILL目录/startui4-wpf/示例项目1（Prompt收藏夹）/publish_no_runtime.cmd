@echo off
setlocal enabledelayedexpansion
cd /d "%~dp0app"

set "OUT=%~dp0publish_no_runtime"
set "EXE=%OUT%\PromptFavorites_no_runtime.exe"

echo [1/3] publish single-file, FRAMEWORK-DEPENDENT (no runtime bundled), win-x64
echo       small artifact, but the target machine MUST have the .NET 10 Desktop Runtime
echo       check a machine with:  dotnet --list-runtimes   (need Microsoft.WindowsDesktop.App 10.x)
echo       WPF native dlls are taken from that shared runtime at run time, nothing is extracted
dotnet publish PromptFavorites.csproj -c Release -r win-x64 --self-contained false ^
  -p:PublishSingleFile=true -p:ArtifactLabel=no_runtime ^
  -p:DebugType=embedded -p:GenerateDocumentationFile=false ^
  -o "%OUT%"
if errorlevel 1 goto :fail

echo.
echo [2/3] self-test of the packaged exe (exit code = failed assertion count)
start "" /wait "%EXE%" --selftest
set "ST=!ERRORLEVEL!"
echo selftest exit code: !ST!

echo.
echo [3/3] artifact
for %%F in ("%EXE%") do echo   %%~fF   %%~zF bytes
echo   files in publish_no_runtime\:
dir /b "%OUT%"

echo.
if not "!ST!"=="0" (
  echo WARNING: self-test reported failures, see %APPDATA%\PromptFavorites\selftest.txt
)
goto :end

:fail
echo.
echo PUBLISH FAILED, dotnet exit code %ERRORLEVEL%
echo If the failure says the runtime pack could not be resolved, this machine lacks the win-x64
echo .NET 10 Desktop targeting/runtime pack - use publish.cmd (self-contained) instead.

:end
echo.
pause
