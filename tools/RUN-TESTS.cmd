@echo off
rem ============================================================================
rem  Area11 - run the tests (double-click this file)
rem  Expected: "Passed! - Failed: 0, Passed: 56"
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
    echo      Run START-AREA11.bat from the folder that contains Area11.sln.
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

dotnet test Area11.sln -v minimal

echo.
echo  ----------------------------------------------------------
echo   Look for the line:  Passed!  - Failed: 0, Passed: 56
echo   If any test failed, send the screen in the chat.
echo  ----------------------------------------------------------
pause
