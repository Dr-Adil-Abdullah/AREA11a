@echo off
rem ============================================================================
rem  Area11 - make the installer folder for a shop PC      (F-47, D-19)
rem
rem  Run this ON THE BUILD PC - the one with Visual Studio / the .NET SDK.
rem  It builds a SELF-CONTAINED Area11: the .NET runtime travels inside the
rem  folder, so the shop PC needs no .NET, no Python and no internet.
rem
rem  When it is done, copy this folder on a USB stick:
rem
rem      dist\Area11-setup
rem
rem  and double-click  INSTALL-AREA11.cmd  inside it on the shop PC.
rem  The same folder is also written as one file:  dist\Area11-setup.zip
rem
rem  Run it again after every new version. The shop PC's data
rem  (C:\ProgramData\Area11) is never part of this folder and is never touched.
rem ============================================================================
title Area11 - make the installer
setlocal
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

echo.
echo   ==========================================================
echo     AREA11 - BUILDING THE INSTALLER
echo   ==========================================================
echo.
echo   [1/5] Building a self-contained Area11 (about two minutes)...
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
echo   [2/5] Copying the program into the installer folder...
robocopy "dist\Area11" "dist\Area11-setup\Area11" /MIR /NFL /NDL /NJH /NJS /NP /R:2 /W:2 >nul
if errorlevel 8 goto :copyfailed

echo   [3/5] Adding the installer, the uninstaller and the guide...
if not exist "dist\Area11-setup\install" mkdir "dist\Area11-setup\install"
if not exist "dist\Area11-setup\docs" mkdir "dist\Area11-setup\docs"
copy /y "tools\INSTALL-AREA11.cmd" "dist\Area11-setup\" >nul
copy /y "tools\UNINSTALL-AREA11.cmd" "dist\Area11-setup\" >nul
copy /y "tools\install\install.ps1" "dist\Area11-setup\install\" >nul
copy /y "tools\install\uninstall.ps1" "dist\Area11-setup\install\" >nul
copy /y "tools\SHARE-AREA11.cmd" "dist\Area11-setup\" >nul
copy /y "docs\INSTALL.md" "dist\Area11-setup\docs\" >nul

echo   [4/5] Writing the zip (one file instead of a folder)...
powershell -NoProfile -ExecutionPolicy Bypass -Command "Compress-Archive -Path 'dist\Area11-setup\*' -DestinationPath 'dist\Area11-setup.zip' -Force"
if errorlevel 1 (
    echo.
    echo        The zip could not be written. That is only the copy step:
    echo        the FOLDER dist\Area11-setup is complete and can still be copied.
)

echo   [5/5] Done.
echo.
echo   ==========================================================
echo     THE INSTALLER IS READY
echo   ==========================================================
echo.
echo   Copy this folder to the shop PC (USB stick):
echo        %CD%\dist\Area11-setup
echo.
echo   ... or copy the single file:
echo        %CD%\dist\Area11-setup.zip
echo.
echo   On the shop PC: open the folder (or extract the zip) and
echo   double-click  INSTALL-AREA11.cmd .
echo.
echo   Your shop data is NOT in this folder. It stays in
echo   C:\ProgramData\Area11 on each PC and is never overwritten.
echo.
pause
endlocal
exit /b 0

:copyfailed
echo.
echo   [X]  Copying the program into dist\Area11-setup failed (robocopy above).
echo.
echo        Close any window that has that folder open (Explorer, a zip tool,
echo        the previous installer) and run this file again.
echo.
pause
exit /b 1
