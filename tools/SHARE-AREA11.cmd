@echo off
rem ============================================================================
rem  Area11 - share this PC's shop folder for the second counter  (F-45)
rem
rem  Run this ON THE MAIN COUNTER, the PC that holds area11.db.
rem  It shares  C:\ProgramData\Area11  as  \\THIS-PC\Area11  with Change
rem  permission, so the second counter can read and write the same file.
rem
rem  It does NOT touch the database, the photos or any bill. If you ever want
rem  to stop sharing, run:   net share Area11 /delete
rem
rem  Read docs\SECOND-COUNTER.md before the first run. Two counters on a WIRED
rem  network are supported; three or more, or one over Wi-Fi from another
rem  building, need the server described in Spec 34.9 - not this file.
rem ============================================================================
title Area11 - share the shop folder
setlocal

net session >nul 2>&1
if errorlevel 1 (
    echo.
    echo   This file needs Administrator rights to create a share.
    echo   Windows will now ask you - press Yes.
    echo.
    powershell -NoProfile -ExecutionPolicy Bypass -Command "Start-Process -FilePath '%~f0' -Verb RunAs"
    exit /b
)

echo.
echo   This PC:      %COMPUTERNAME%
echo   Shop folder:  %ProgramData%\Area11
echo.

if not exist "%ProgramData%\Area11" (
    echo   FAILED: the folder %ProgramData%\Area11 does not exist on this PC.
    echo   Start Area11 once on this PC first, then run this file again.
    goto :end
)

echo   ---------------------------------------------------------------
echo   1. Removing an older share with the same name (if any)
echo   ---------------------------------------------------------------
net share Area11 /delete >nul 2>&1
if errorlevel 1 (
    echo   No older share was found - fine.
) else (
    echo   An older share was removed and is being created again below.
)

echo.
echo   ---------------------------------------------------------------
echo   2. Creating the share with Change permission
echo   ---------------------------------------------------------------
net share Area11="%ProgramData%\Area11" /grant:Everyone,CHANGE /remark:"Area11 shop database"
if errorlevel 1 (
    echo.
    echo   FAILED. The message above says why. The most common reasons:
    echo     - File and Printer Sharing is switched off on this network
    echo     - this PC is on a "Public" network in Windows
    echo   Ask your helper before trying anything else.
    goto :end
)

echo.
echo   ---------------------------------------------------------------
echo   3. Done. On the second counter
echo   ---------------------------------------------------------------
echo   Open Area11  ^>  SYSTEM  ^>  Second counter ^(LAN^).
echo   In "This PC uses the main counter's database" write:
echo.
echo        \\%COMPUTERNAME%\Area11
echo.
echo   Then press "Join the main counter" twice and restart Area11 there.
echo.

:end
echo.
pause
endlocal
