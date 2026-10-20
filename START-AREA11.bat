@echo off
rem ============================================================================
rem  Area11 - start the application
rem  Double-click this file. It updates the code, builds it and runs it.
rem  It never touches the shop database in C:\ProgramData\Area11.
rem ============================================================================
setlocal
title Area11

cd /d "%~dp0"

echo.
echo  ==========================================================
echo    AREA11 - pharmacy POS
echo  ==========================================================
echo.

rem ---- 1. is the .NET SDK present? -------------------------------------------
where dotnet >nul 2>nul
if errorlevel 1 (
    echo  [1/5] .NET SDK not found.
    echo.
    echo  Install the free .NET 10 SDK first:
    echo      https://dotnet.microsoft.com/download/dotnet/10.0
    echo.
    echo  Then close this window and double-click this file again.
    pause
    exit /b 1
)
echo  [1/5] .NET SDK found.

rem ---- 2. remove leftovers that point at the old prototype -------------------
rem  An old "Area11.WPF.sln" in this folder makes Visual Studio open the wrong
rem  project and shows errors about D:\...\Area11.WPF\Area11.WPF.csproj.
if exist "Area11.WPF.sln" (
    del /q "Area11.WPF.sln"
    echo  [2/5] Removed the old Area11.WPF.sln ^(it opened the prototype by mistake^).
) else (
    echo  [2/5] No stale solution file.
)

rem  Visual Studio remembers the last solution in the .vs folder; a stale one makes it
rem  report MSB4025 about Area11.WPF.csproj even after the file is gone.
if exist ".vs" (
    rmdir /s /q ".vs"
    echo  [2/5] Cleared the Visual Studio memory of the old solution ^(.vs^).
)

rem  The prototype now lives in prototypes\Area11.WPF. An old copy in the root is renamed,
rem  never deleted, so nothing of the owner work can be lost.
if exist "Area11.WPF" (
    if not exist "prototypes\Area11.WPF" (
        mkdir "prototypes" 2>nul
        move "Area11.WPF" "prototypes\Area11.WPF" >nul
        echo  [2/5] Moved the old prototype folder into prototypes\.
    ) else (
        echo  [2/5] WARNING: an extra Area11.WPF folder exists in the root - run tools\doctor.ps1 -Fix
    )
)

rem ---- 3. update from GitHub ------------------------------------------------
echo  [3/5] Updating the code from GitHub...
git pull --ff-only
if errorlevel 1 (
    echo.
    echo  Could not update automatically. Continuing with the code already here.
)

rem ---- 4. build -------------------------------------------------------------
echo  [4/5] Building...  ^(this takes a minute the first time^)
dotnet build Area11.sln -v minimal
if errorlevel 1 (
    echo.
    echo  ==========================================================
    echo   BUILD FAILED
    echo  ==========================================================
    echo   Copy the FIRST error line above and send it in the chat.
    echo   The line looks like:  Something.cs(12,34): error CS1234: ...
    echo.
    pause
    exit /b 1
)

rem ---- 5. run ---------------------------------------------------------------
echo  [5/5] Starting Area11...
echo.
echo   Sign in with the user name left EMPTY and the password: area11
echo.
dotnet run --project "dotnet\Area11.Desktop" --no-build

if errorlevel 1 (
    echo.
    echo  Area11 stopped with an error. Send the first red line above.
)

echo.
echo  Area11 has closed.
pause
