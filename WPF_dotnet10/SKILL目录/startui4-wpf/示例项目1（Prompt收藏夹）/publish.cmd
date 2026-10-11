@echo off
setlocal enabledelayedexpansion
cd /d "%~dp0app"

set "OUT=%~dp0publish"
set "EXE=%OUT%\PromptFavorites_self_contained.exe"

echo [1/3] publish single-file, SELF-CONTAINED (runtime bundled), win-x64
echo       target machine needs no .NET installed
echo       first run downloads the .NET WindowsDesktop runtime pack (about 150 MB) from nuget.org
echo       DebugType=embedded / GenerateDocumentationFile=false are global properties: they reach the
echo       referenced lib project too, so no stray StartUI4Controls.pdb / .xml is left next to the exe
dotnet publish PromptFavorites.csproj -c Release -r win-x64 --self-contained true ^
  -p:PublishSingleFile=true -p:ArtifactLabel=self_contained ^
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
echo   files in publish\:
dir /b "%OUT%"

echo.
if not "!ST!"=="0" (
  echo WARNING: self-test reported failures, see %APPDATA%\PromptFavorites\selftest.txt
)
goto :end

:fail
echo.
echo PUBLISH FAILED, dotnet exit code %ERRORLEVEL%

:end
echo.
pause
