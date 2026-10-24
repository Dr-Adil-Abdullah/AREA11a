@echo off
rem ============================================================================
rem  Area11 - remove the program from this PC     (F-47, D-19)
rem
rem  Double-click this file. Windows asks for permission - press Yes.
rem  Type YES when it asks, and the program, the three icons and the line in
rem  "Add or remove programs" are removed.
rem
rem  THE SHOP DATA IS NOT REMOVED.  C:\ProgramData\Area11 holds every bill, every
rem  khata and every photo; it stays exactly where it is. Installing Area11 again
rem  finds it and carries on.
rem
rem  This same file works in two places, which is why the path is searched:
rem     * tools\UNINSTALL-AREA11.cmd        - from the repository
rem     * C:\Program Files\Area11\...       - after an install (also in the
rem                                           Start menu and in "Add or remove
rem                                           programs")
rem ============================================================================
title Area11 - uninstall
setlocal

net session >nul 2>&1
if not errorlevel 1 goto :elevated

echo.
echo   The uninstaller needs Administrator rights.
echo   Windows will ask you now - press Yes.
echo.
powershell -NoProfile -ExecutionPolicy Bypass -Command "Start-Process -FilePath '%~f0' -Verb RunAs"
exit /b

:elevated
set "UNINSTALLER=%~dp0uninstall.ps1"
if not exist "%UNINSTALLER%" set "UNINSTALLER=%~dp0install\uninstall.ps1"

if not exist "%UNINSTALLER%" (
    echo.
    echo   uninstall.ps1 was not found next to this file.
    echo.
    echo   If Area11 is installed, this is still easy:
    echo     Start - Settings - Apps - "Add or remove programs" - Area11 - Remove
    echo.
    pause
    exit /b 1
)

powershell -NoProfile -ExecutionPolicy Bypass -File "%UNINSTALLER%" %*
if errorlevel 1 goto :failed

echo.
echo   Your shop data in C:\ProgramData\Area11 was not touched.
echo.
pause
exit /b 0

:failed
echo.
echo   The uninstaller stopped. The lines above say why.
echo   Your shop data in C:\ProgramData\Area11 was not touched.
echo.
pause
exit /b 1
