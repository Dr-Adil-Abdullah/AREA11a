@echo off
rem ============================================================================
rem  Area11 - ONE file that says everything (double-click this file)
rem
rem  What it does: writes a plain text file into this folder with the build, the
rem  .NET version, the branch, the shop folder, the newest crash report, the end
rem  of the log and the last build/test output. Then it prints the path so the
rem  whole thing can be sent in one message.
rem
rem  What it does NOT do: nothing is built, nothing is deleted, and the shop
rem  database is never opened - only its name, size and date are read.
rem ============================================================================
setlocal
title Area11 - report

rem  Start from the folder that holds Area11.sln, whatever folder this file was
rem  copied to. tools\SEND-REPORT.cmd sits one level down from the root.
cd /d "%~dp0.."

rem  The build this report is about. The line below is the ONLY place this script
rem  names a version, and tools\check_repo.py fails if it disagrees with
rem  BuildInfo.cs - the same keeper the other two double-click scripts have.
set "BUILD=v0.30.6 (Damson)"

echo.
echo  ==========================================================
echo    AREA11 - one file that says everything
echo  ==========================================================
echo.
echo    Build on this PC : %BUILD%
echo    Folder           : %CD%
echo.
echo    This only READS. Nothing is built and nothing is deleted.
echo.

if not exist "%~dp0support-report.ps1" (
    echo  [X] support-report.ps1 is missing from the tools folder.
    echo      This copy is incomplete - pull again from GitHub Desktop.
    echo.
    pause
    exit /b 1
)

powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0support-report.ps1" -Build "%BUILD%"

if errorlevel 1 (
    echo.
    echo  [X] The report could not be written. Send the text above.
    echo.
    pause
    exit /b 1
)

echo.
echo  ----------------------------------------------------------
echo   Send the report file (it opens in Notepad if you
echo   double-click it). It is the only thing needed to name
echo   the fault - no screenshots, no copying from the console.
echo  ----------------------------------------------------------
pause
