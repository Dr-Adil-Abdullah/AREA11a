@echo off
rem ============================================================================
rem  Area11 - run the tests (double-click this file)
rem  Expected: "Passed! - Failed: 0" - every test green.
rem  The banner below prints the version it is about to test and the number of
rem  cases that version has, both read from the code. A tree that was not pulled
rem  says so at once, instead of looking like a fault in the shop.
rem ============================================================================
title Area11 - tests
cd /d "%~dp0.."

rem How many cases this build should run. tools\check_repo.py fails if this number
rem and the suite disagree, so it cannot quietly go stale.
set "EXPECTED=659"

rem  Which build is about to run? The line below is the ONLY place this script names a version,
rem  and tools\check_repo.py fails if it disagrees with BuildInfo.cs - so it cannot go stale, and
rem  the owner is never told an old tree is the new one. Reading the number out of the source with
rem  findstr printed "vunknown" on his PC (v0.30.0): the tokens did not line up, and a banner that
rem  says nothing is worse than no banner, because it looks like a failure of the tree.
set "BUILD=v0.30.2 (Auburn)"


echo.
echo  ==========================================================
echo    AREA11 - running the tests
echo  ==========================================================
echo.
echo    Build under test : %BUILD%
echo    Expect at the end: total: %EXPECTED%, failed: 0
echo.
echo    If the total below is SMALLER than %EXPECTED%, this folder was not
echo    pulled (an older build), and a green run proves nothing about this one.
echo.

if not exist "Area11.sln" (
    echo  [X] Area11.sln not found here: %CD%
    echo      Run this file from the folder that contains Area11.sln.
    echo.
    pause
    exit /b 1
)

where dotnet >nul 2>nul
if errorlevel 1 (
    echo  [X] .NET SDK not found. Install it from https://dotnet.microsoft.com/download/dotnet/10.0
    echo.
    pause
    exit /b 1
)

rem Only Core and the tests are built here: the window program is not needed to test the shop
rem rules, and leaving it out keeps the run short even when the Desktop project is mid-edit.
dotnet test "dotnet\Area11.Tests\Area11.Tests.csproj" -v minimal

echo.
echo  ----------------------------------------------------------
echo   Look for:  Passed!  - Failed: 0
echo   The number of tests grows with every version, so only
echo   "Failed: 0" matters. If anything failed, send the screen.
echo  ----------------------------------------------------------
pause
