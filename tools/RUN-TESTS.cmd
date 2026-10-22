@echo off
rem ============================================================================
rem  Area11 - run the tests (double-click this file)
rem  Expected: "Passed! - Failed: 0" - every test green, no number to remember.
rem ============================================================================
title Area11 - tests
cd /d "%~dp0.."

echo.
echo  ==========================================================
echo    AREA11 - running the tests
echo  ==========================================================
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
