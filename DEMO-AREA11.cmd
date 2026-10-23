@echo off
rem ============================================================================
rem  Area11 - the DEMO shop
rem  Double-click this file to look at Area11 filled with two and a half months
rem  of an invented pharmacy (D-74, D-76).
rem
rem  It updates the code, builds it and opens it with --demo, which keeps
rem  EVERYTHING under C:\ProgramData\Area11\demo:
rem      demo\area11.db, demo\photos, demo\logs
rem  The shop's own database in C:\ProgramData\Area11\area11.db is never opened,
rem  never written and never migrated by this file.
rem
rem  Why it updates and builds first: an OLD build does not know --demo and would
rem  simply start the real shop. That is the one mistake this file must not allow,
rem  so it never runs a program it has not just built from the current code.
rem
rem  When the app is finished, deleting the demo is: close this window, then
rem  delete the folder C:\ProgramData\Area11\demo and this file. Nothing else.
rem ============================================================================
setlocal
title Area11 - DEMO shop
cd /d "%~dp0"

echo.
echo  ==========================================================
echo    AREA11 - DEMO shop  (invented data, separate database)
echo  ==========================================================
echo.

if not exist "Area11.sln" goto :incomplete

where dotnet >nul 2>nul
if errorlevel 1 goto :nodotnet

rem ---- 1. update the code ----------------------------------------------------
rem  Only a clone can update. A downloaded ZIP keeps the code it came with.
if not exist ".git" (
    echo  [1/3] This copy was downloaded as a file - using the code already here.
    goto :build
)

where git >nul 2>nul
if errorlevel 1 (
    echo  [1/3] git is not available - using the code already here.
    goto :build
)

set "BRANCH="
for /f "delims=" %%b in ('git rev-parse --abbrev-ref HEAD 2^>nul') do set "BRANCH=%%b"

if not "%BRANCH%"=="arena/a8a3b2e0-area11a" (
    echo.
    echo  [X]  This folder is on the branch "%BRANCH%".
    echo       The demo lives on "arena/a8a3b2e0-area11a".
    echo.
    echo       GitHub Desktop - click "Current Branch" at the top, pick
    echo                        "arena/a8a3b2e0-area11a", then "Pull origin".
    echo.
    pause
    exit /b 1
)

echo  [1/3] Updating the code from GitHub ^(branch %BRANCH%^)...
git pull --ff-only
if errorlevel 1 echo        Could not update - continuing with the code already here.

rem ---- 2. build -------------------------------------------------------------
:build
rem  Which build is about to run? The line below is the ONLY place this script names a version,
rem  and tools\check_repo.py fails if it disagrees with BuildInfo.cs - so it cannot go stale, and
rem  the owner is never told an old tree is the new one. Reading the number out of the source with
rem  findstr printed "vunknown" on his PC (v0.30.0): the tokens did not line up, and a banner that
rem  says nothing is worse than no banner, because it looks like a failure of the tree.
set "BUILD=v0.30.1 (Citrine)"

echo        Code on this PC: %BUILD%

echo  [2/3] Building...  ^(a few seconds when nothing changed^)
dotnet build Area11.sln -v minimal
if errorlevel 1 goto :buildfailed

rem ---- 3. run the demo -------------------------------------------------------
set "APPEXE=%CD%\dotnet\Area11.Desktop\bin\Debug\net10.0-windows\Area11.exe"

if not exist "%APPEXE%" goto :noexe

echo  [3/3] Opening the demo shop...
echo.
echo    The window title MUST say:  Area11 - DEMO SHOP (invented data)
echo    If it says only "Area11", CLOSE IT AT ONCE and tell me - it is an old build.
echo.
echo    Sign in with the user name left EMPTY and the password: area11
echo    The Sample-shop page opens by itself: tick the box, then press
echo                         Fill this shop with the sample shop
echo.
echo    Nothing you do here reaches your real shop.
echo.
"%APPEXE%" --demo

if errorlevel 1 goto :crashed

echo.
echo  The demo shop has closed. Your own shop was not touched.
pause
exit /b 0

rem ---- what went wrong ---------------------------------------------------------
:crashed
echo.
echo  ==========================================================
echo   THE DEMO STOPPED WITH AN ERROR
echo  ==========================================================
set "LOGDIR=%ProgramData%\Area11\demo\logs"
if exist "%LOGDIR%\crash-*.txt" goto :showcrash
echo   No crash report was written. Send the first error line above.
pause
exit /b 1

:showcrash
set "CRASHFILE="
for /f "delims=" %%f in ('dir /b /o-d "%LOGDIR%\crash-*.txt" 2^>nul') do if not defined CRASHFILE set "CRASHFILE=%LOGDIR%\%%f"
echo   Report: %CRASHFILE%
echo.
type "%CRASHFILE%"
pause
exit /b 1

:buildfailed
echo.
echo  ==========================================================
echo   BUILD FAILED
echo  ==========================================================
echo   Copy the FIRST error line above and send it in the chat.
echo   The line looks like:  Something.cs^(12,34^): error CS1234: ...
echo.
pause
exit /b 1

:noexe
echo.
echo  The program file was not found after the build:
echo    %APPEXE%
echo  Send that line and the build output above.
echo.
pause
exit /b 1

:incomplete
echo  [X]  This folder is not a complete copy of Area11 (Area11.sln is missing).
echo       Open the folder you cloned in GitHub Desktop and run this file there.
echo.
pause
exit /b 1

:nodotnet
echo  [X]  .NET SDK not found. Install the free .NET 10 SDK first:
echo           https://dotnet.microsoft.com/download/dotnet/10.0
echo.
pause
exit /b 1
