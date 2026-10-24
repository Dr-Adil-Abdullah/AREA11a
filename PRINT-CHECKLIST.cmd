@echo off
rem ============================================================================
rem  Area11 - print the feature tick-list (double-click this file)
rem
rem  It only OPENS a page in your browser: nothing is built, nothing is
rem  changed. In the browser press Ctrl+P, choose A4 paper, and print it - then
rem  put a pen tick in the empty box beside every feature you have checked in
rem  the shop.
rem
rem  The page is made from docs/PROGRESS.md by tools/make_checklist.py, so it
rem  can never say a feature is finished while the board says otherwise.
rem ============================================================================
title Area11 - feature tick list
cd /d "%~dp0"

set "PAGE=%CD%\docs\CHECKLIST-URDU.html"

echo.
echo   Area11 - feature tick list
echo   ============================================================
echo.

if not exist "%PAGE%" (
    echo   The list is not built yet: docs\CHECKLIST-URDU.html is missing.
    echo.
    echo   Build it with:
    echo       python tools\make_checklist.py
    echo.
    pause
    exit /b 1
)

echo   Opening: %PAGE%
echo.
echo   In the browser: press Ctrl+P, paper A4, and print.
echo   The marks mean:  tick = finished,  half = one action left in the shop,
echo   cross = not started.  The empty box on each line is for your pen.
echo.

start "" "%PAGE%"

echo   The page should now be open in your browser.
echo.
pause
