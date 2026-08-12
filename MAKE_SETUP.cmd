@echo off
setlocal EnableExtensions DisableDelayedExpansion
title CrewChief RU Builder 0.9.4

set "ROOT=%~dp0"
set "SCRIPT=%ROOT%files\scripts\build-release.ps1"
set "LOG=%ROOT%build.log"
set "BOOTLOG=%ROOT%builder-startup.log"

>"%BOOTLOG%" echo CrewChief RU Builder 0.9.4 startup
>>"%BOOTLOG%" echo ROOT=%ROOT%
>>"%BOOTLOG%" echo SCRIPT=%SCRIPT%
>>"%BOOTLOG%" echo DATE=%DATE% TIME=%TIME%

if exist "%SCRIPT%" goto run_builder

echo.
echo ================================================================
echo BUILD SCRIPT NOT FOUND
echo ================================================================
echo.
echo Expected file:
echo %SCRIPT%
echo.
echo Extract the complete ZIP archive to a normal folder first.
echo Then run MAKE_SETUP.cmd from the extracted folder.
echo.
echo Startup log:
echo %BOOTLOG%
echo.
start "" notepad.exe "%BOOTLOG%" >nul 2>&1
pause
exit /b 2

:run_builder
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%SCRIPT%"
set "EXIT_CODE=%ERRORLEVEL%"

echo.
if "%EXIT_CODE%"=="0" goto build_success

echo Build failed.
echo Full log:
echo %LOG%
echo.
if exist "%LOG%" start "" notepad.exe "%LOG%"
echo Press any key to close this window.
pause >nul
exit /b %EXIT_CODE%

:build_success
echo Build completed successfully.
echo Installer:
echo %ROOT%output\CrewChiefRU_Setup.exe
echo.
echo Build log:
echo %LOG%
echo.
echo Press any key to close this window.
pause >nul
exit /b 0
