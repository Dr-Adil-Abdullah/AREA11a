@echo off
rem ============================================================================
rem  Area11 - install it on this PC          (F-47, D-19)
rem
rem  Double-click this file. Windows asks for permission - press Yes.
rem
rem  It puts the program in  C:\Program Files\Area11 , an icon on the desktop and
rem  in the Start menu, and a line in "Add or remove programs" with an uninstaller.
rem
rem  The shop data stays in  C:\ProgramData\Area11  and is NEVER deleted, not by
rem  an update and not by the uninstaller.
rem
rem  Run it again to update: the program files are replaced, the data is not
rem  touched, and it takes a few seconds once a finished copy exists.
rem
rem  It needs one of these:
rem     * dist\Area11  - made on the build PC by  tools\MAKE-INSTALLER.cmd
rem     * the .NET SDK on this PC - then it builds a self-contained copy itself
rem
rem  Read docs\INSTALL.md before the first install on the shop PC.
rem ============================================================================
title Area11 - installer
setlocal

rem ---- ask Windows for Administrator rights --------------------------------
net session >nul 2>&1
if not errorlevel 1 goto :elevated

echo.
echo   The installer needs Administrator rights.
echo   Windows will ask you now - press Yes.
echo.
powershell -NoProfile -ExecutionPolicy Bypass -Command "Start-Process -FilePath '%~f0' -Verb RunAs"
exit /b

:elevated
cd /d "%~dp0"

if not exist "install\install.ps1" (
    echo.
    echo   install\install.ps1 is missing next to this file.
    echo.
    echo   Copy the WHOLE folder again - the installer is a folder, not one file.
    echo   If you only have the zip: extract it first, then double-click
    echo   INSTALL-AREA11.cmd inside the folder that appears.
    echo.
    pause
    exit /b 1
)

powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0install\install.ps1" -Start %*
if errorlevel 1 goto :failed

echo.
pause
exit /b 0

:failed
echo.
echo   The installer stopped. The lines above say why.
echo.
echo   Nothing was deleted: the shop data in C:\ProgramData\Area11 is untouched.
echo   Copy everything above this line and send it in the chat.
echo.
pause
exit /b 1
