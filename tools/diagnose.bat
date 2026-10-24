@echo off
rem ============================================================================
rem  Area11 - quick diagnosis (double-click this file)
rem  Plain text, no colours, no PowerShell scripts. It answers three questions:
rem    1. which folder did I open
rem    2. which solution files exist
rem    3. where does each solution point, and does that project exist
rem ============================================================================
title Area11 - diagnosis
cd /d "%~dp0.."
set REPORT=%~dp0diagnosis.txt

echo Area11 diagnosis > "%REPORT%"
echo Folder: %CD% >> "%REPORT%"
echo. >> "%REPORT%"

echo ==========================================================
echo   AREA11 - quick diagnosis
echo   Folder: %CD%
echo ==========================================================
echo.
echo [1] Content of this folder:
dir /b /a
echo.
echo [1] Content of this folder: >> "%REPORT%"
dir /b /a >> "%REPORT%"
echo. >> "%REPORT%"

echo [2] Solution files in this folder:
echo.
echo [2] Solution files (.sln):
if exist "*.sln" (
    dir /b *.sln
    dir /b *.sln >> "%REPORT%"
) else (
    echo     NONE FOUND  - this is the problem
    echo     NONE FOUND >> "%REPORT%"
)

echo.
echo [3] Projects each solution asks for:
for %%S in (*.sln) do (
    echo.
    echo     --- %%S
    echo. >> "%REPORT%"
    echo     --- %%S >> "%REPORT%"
    findstr /i "csproj" "%%S"
    findstr /i "csproj" "%%S" >> "%REPORT%"
)

echo.
echo [4] Project files actually present:
echo.
echo [4] Project files (.csproj) that exist:
dir /s /b *.csproj
dir /s /b *.csproj >> "%REPORT%"

echo.
echo [5] The old prototype in this folder:
echo.
if exist "Area11.WPF" echo     Area11.WPF folder EXISTS here - run CHECK-AREA11.cmd
if not exist "Area11.WPF" echo     no old Area11.WPF folder
if exist "Area11.WPF" echo     Area11.WPF folder EXISTS here >> "%REPORT%"
if not exist "Area11.WPF" echo     no old Area11.WPF folder >> "%REPORT%"
if exist "Area11.WPF.sln" echo     Area11.WPF.sln EXISTS here - this is the villain
if not exist "Area11.WPF.sln" echo     no old Area11.WPF.sln
if exist "Area11.WPF.sln" echo     Area11.WPF.sln EXISTS here >> "%REPORT%"
if not exist "Area11.WPF.sln" echo     no old Area11.WPF.sln >> "%REPORT%"

echo.
echo [6] How to build the right one:
echo.
echo     dotnet build "%~dp0..\Area11.sln"
echo.

echo ==========================================================
echo   Written to: %REPORT%
echo   Send that file (or a screenshot of this window).
echo ==========================================================
pause
