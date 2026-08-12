@echo off
setlocal EnableExtensions DisableDelayedExpansion
title CrewChief RU Setup Diagnostics

set "ROOT=%~dp0"
set "SETUP=%ROOT%output\CrewChiefRU_Setup.exe"
set "DIAG=%ROOT%output\diagnostics"
set "ETL=%DIAG%\sxstrace.etl"
set "SXS=%DIAG%\sxstrace.txt"
set "REPORT=%DIAG%\startup-diagnostics.txt"

if exist "%SETUP%" goto setup_found
echo Setup file was not found:
echo %SETUP%
echo.
echo Run MAKE_SETUP.cmd first.
pause
exit /b 2

:setup_found
if not exist "%DIAG%" mkdir "%DIAG%"
del /q "%ETL%" "%SXS%" "%REPORT%" 2>nul

>"%REPORT%" echo CrewChief RU Setup startup diagnostics
>>"%REPORT%" echo Date: %DATE% %TIME%
>>"%REPORT%" echo Setup: %SETUP%
>>"%REPORT%" echo.
>>"%REPORT%" echo ==== SYSTEMINFO ====
>>"%REPORT%" ver
>>"%REPORT%" echo PROCESSOR_ARCHITECTURE=%PROCESSOR_ARCHITECTURE%
>>"%REPORT%" echo PROCESSOR_ARCHITEW6432=%PROCESSOR_ARCHITEW6432%
>>"%REPORT%" echo TEMP=%TEMP%
>>"%REPORT%" echo.
>>"%REPORT%" echo ==== SETUP FILE ====
>>"%REPORT%" dir "%SETUP%"
>>"%REPORT%" echo.
>>"%REPORT%" echo ==== AUTHENTICODE ====

powershell.exe -NoProfile -Command "$s=Get-AuthenticodeSignature -LiteralPath '%SETUP%'; $s | Format-List Status,StatusMessage,Path,SignerCertificate" >>"%REPORT%" 2>&1

echo Starting SideBySide trace...
start "" /b sxstrace.exe Trace -logfile:"%ETL%"
timeout /t 2 /nobreak >nul

echo Starting the installer. Close it after testing.
start "" /wait "%SETUP%"
set "SETUP_EXIT=%ERRORLEVEL%"

echo Stopping trace...
sxstrace.exe StopTrace >>"%REPORT%" 2>&1
sxstrace.exe Parse -logfile:"%ETL%" -outfile:"%SXS%" >>"%REPORT%" 2>&1

>>"%REPORT%" echo.
>>"%REPORT%" echo ==== SETUP EXIT CODE ====
>>"%REPORT%" echo %SETUP_EXIT%
>>"%REPORT%" echo.
>>"%REPORT%" echo ==== RECENT SIDEBYSIDE EVENTS ====
wevtutil.exe qe Application /q:"*[System[Provider[@Name='SideBySide']]]" /c:15 /rd:true /f:text >>"%REPORT%" 2>&1

if not exist "%SXS%" goto report_ready
>>"%REPORT%" echo.
>>"%REPORT%" echo ==== SXSTRACE PARSED ====
type "%SXS%" >>"%REPORT%"

:report_ready
echo.
echo Diagnostics saved to:
echo %REPORT%
start "" notepad.exe "%REPORT%"
pause
exit /b %SETUP_EXIT%
