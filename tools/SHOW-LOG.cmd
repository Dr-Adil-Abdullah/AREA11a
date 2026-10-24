@echo off
rem ============================================================================
rem  Area11 - show the newest crash report and the end of today's log
rem  (double-click this file; it only READS, it changes nothing)
rem ============================================================================
setlocal
title Area11 - logs
set "LOGDIR=%ProgramData%\Area11\logs"

echo.
echo   Log folder: %LOGDIR%
echo.

if not exist "%LOGDIR%" (
    echo   That folder does not exist, so Area11 has never started on this PC.
    echo   Run START-AREA11.bat first.
    echo.
    pause
    exit /b 1
)

set "CRASHFILE="
for /f "delims=" %%f in ('dir /b /o-d "%LOGDIR%\crash-*.txt" 2^>nul') do if not defined CRASHFILE set "CRASHFILE=%LOGDIR%\%%f"

echo   ===============================================================
echo    NEWEST CRASH REPORT
echo   ===============================================================
if defined CRASHFILE (
    echo   %CRASHFILE%
    echo.
    type "%CRASHFILE%"
) else (
    echo   None. No crash has been recorded since this version was installed.
)

echo.
echo   ===============================================================
echo    END OF THE NEWEST LOG
echo   ===============================================================
set "LOGFILE="
for /f "delims=" %%f in ('dir /b /o-d "%LOGDIR%\*.log" 2^>nul') do if not defined LOGFILE set "LOGFILE=%LOGDIR%\%%f"
if not defined LOGFILE (
    echo   No log file yet.
) else (
    echo   %LOGFILE%
    echo.
    powershell -NoProfile -Command "Get-Content -LiteralPath '%LOGFILE%' -Tail 60"
)

echo.
echo   ===============================================================
echo    Copy everything above and send it in the chat.
echo    Your shop data was only read - nothing here changes it.
echo   ===============================================================
echo.
pause
