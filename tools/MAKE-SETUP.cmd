@echo off
rem ============================================================================
rem  Area11 - make the single-file installer      (Phase 5, owner's word)
rem
rem  Run this ON THE BUILD PC - the one with the .NET SDK and the free
rem  Inno Setup 6 (once: https://jrsoftware.org/isdl.php - next, next, finish).
rem
rem  It builds a SELF-CONTAINED Area11 and packs it into ONE file:
rem
rem      dist\Area11-Setup-v0.64.0.exe
rem
rem  Send that file to the shop PC (USB stick, Drive, WhatsApp document) and
rem  double-click it there: permission question, next-next-finish, installed.
rem  The shop PC needs no .NET, no Python and no internet.
rem
rem  The shop PC's data (C:\ProgramData\Area11) is never part of this file and
rem  is never touched - not on install, not on upgrade, not on uninstall.
rem
rem  The older path (MAKE-INSTALLER.cmd: a zip plus INSTALL-AREA11.cmd) stays as
rem  the backup. Both install the same program to the same folders.
rem ============================================================================
title Area11 - make the single-file installer
setlocal EnableDelayedExpansion
cd /d "%~dp0.."

where dotnet >nul 2>nul
if errorlevel 1 (
    echo.
    echo   [X]  .NET SDK not found.
    echo.
    echo        Install the free .NET 10 SDK first:
    echo            https://dotnet.microsoft.com/download/dotnet/10.0
    echo        Then close this window and double-click this file again.
    echo.
    pause
    exit /b 1
)

set "ISCC="
where iscc.exe >nul 2>nul
if not errorlevel 1 (
    for /f "delims=" %%I in ('where iscc.exe') do set "ISCC=%%I"
)
if not defined ISCC (
    if exist "%ProgramFiles(x86)%\Inno Setup 6\ISCC.exe" set "ISCC=%ProgramFiles(x86)%\Inno Setup 6\ISCC.exe"
)
if not defined ISCC (
    if exist "%ProgramFiles%\Inno Setup 6\ISCC.exe" set "ISCC=%ProgramFiles%\Inno Setup 6\ISCC.exe"
)
if not defined ISCC (
    echo.
    echo   [X]  Inno Setup 6 not found.
    echo.
    echo        Install the free Inno Setup 6 once:
    echo            https://jrsoftware.org/isdl.php
    echo        Then close this window and double-click this file again.
    echo.
    pause
    exit /b 1
)

set "APPVER="
for /f tokens^=2^ delims^=^" %%V in ('findstr /c:"public const string Version" "dotnet\Area11.Core\Utils\BuildInfo.cs"') do set "APPVER=%%V"
if not defined APPVER (
    echo.
    echo   [X]  The version could not be read from BuildInfo.cs - nothing was built.
    echo.
    pause
    exit /b 1
)

echo.
echo   ==========================================================
echo     AREA11 - BUILDING THE SINGLE-FILE INSTALLER  v%APPVER%
echo   ==========================================================
echo.
echo   [1/4] Building a self-contained Area11 (about two minutes)...
echo.
dotnet publish "dotnet\Area11.Desktop\Area11.Desktop.csproj" -c Release -r win-x64 --self-contained true -o "dist\Area11"
if errorlevel 1 (
    echo.
    echo   ==========================================================
    echo     BUILD FAILED
    echo   ==========================================================
    echo   Copy the FIRST error line above and send it in the chat.
    echo   The line looks like:  Something.cs^(12,34^): error CS1234: ...
    echo.
    pause
    exit /b 1
)

echo.
echo   [2/4] Packing the single setup file (about a minute)...
echo.
"%ISCC%" /DMyAppVersion=%APPVER% "tools\install\Area11.iss"
if errorlevel 1 (
    echo.
    echo   ==========================================================
    echo     PACKING FAILED
    echo   ==========================================================
    echo   Copy the FIRST error line above and send it in the chat.
    echo.
    pause
    exit /b 1
)

echo.
echo   [3/4] Checking the file...
if not exist "dist\Area11-Setup-v%APPVER%.exe" (
    echo.
    echo   [X]  Inno Setup reported success but the file is missing - nothing to send.
    echo.
    pause
    exit /b 1
)

echo.
echo   [4/4] Done.
echo.
echo   ==========================================================
echo     THE INSTALLER IS READY - ONE FILE
echo   ==========================================================
echo.
echo   Send this file to the shop PC (USB stick, Drive, WhatsApp):
echo        %CD%\dist\Area11-Setup-v%APPVER%.exe
echo.
echo   On the shop PC: double-click it, say Yes to the permission
echo   question, next-next-finish - then Area11 opens from its icon.
echo.
echo   Your shop data is NOT in this file. It stays in
echo   C:\ProgramData\Area11 on each PC and is never overwritten,
echo   not even when this setup is run again for a new version.
echo.
pause
endlocal
exit /b 0
