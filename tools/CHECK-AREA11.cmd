@echo off
rem ============================================================================
rem  Area11 - check the project (double-click this file)
rem
rem  It opens its own window, runs the doctor, and waits for Enter so the
rem  result can be read and copied. Nothing is deleted: an obsolete solution is
rem  copied to *.bak, an old prototype folder is only renamed.
rem ============================================================================
title Area11 - project check
cd /d "%~dp0.."

echo.
echo  Checking the Area11 folder: %CD%
echo.

powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0doctor.ps1" -Fix

echo.
echo  ------------------------------------------------------------------
echo   Send the text above to the agent (screenshot or copy-paste).
echo  ------------------------------------------------------------------
pause
