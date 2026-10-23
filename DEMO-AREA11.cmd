@echo off
rem ============================================================================
rem  Area11 - the DEMO shop
rem  Double-click this file to look at Area11 filled with two and a half months
rem  of an invented pharmacy (D-74, D-76).
rem
rem  It builds the same program and opens it with --demo, which keeps EVERYTHING
rem  under C:\ProgramData\Area11\demo:
rem      demo\area11.db, demo\photos, demo\logs
rem  The shop's own database in C:\ProgramData\Area11\area11.db is never opened,
rem  never written and never migrated by this file.
rem
rem  When the app is finished, deleting the demo is: close this window, then
rem  delete the folder C:\ProgramData\Area11\demo and this file. Nothing else.
rem ============================================================================
setlocal
title Area11 - DEMO shop
cd /d "%~dp0"

echo.
echo  ==========================================================
echo    AREA11 - DEMO shop  (invented data, separate database)
echo  ==========================================================
echo.

if not exist "Area11.sln" goto :incomplete

where dotnet >nul 2>nul
if errorlevel 1 goto :nodotnet

set "APPEXE=%CD%\dotnet\Area11.Desktop\bin\Debug\net10.0-windows\Area11.exe"

rem ---- build if the program is missing or the source is newer ------------------
rem  A full build every time would make the demo slow to open; building only when
rem  the program file is missing keeps the double-click fast and still safe.
if exist "%APPEXE%" goto :run

echo  [1/2] Building the program once...
dotnet build Area11.sln -v minimal
if errorlevel 1 goto :buildfailed
goto :run

rem ---- run the demo ------------------------------------------------------------
:run
echo  [2/2] Opening the demo shop...
echo.
echo    Sign in with the user name left EMPTY and the password: area11
echo    Then open:  SYSTEM  ->  Sample shop  ->  tick the box  ->  Fill this shop
echo.
echo    Nothing you do here reaches your real shop.
echo.
"%APPEXE%" --demo

if errorlevel 1 goto :crashed

echo.
echo  The demo shop has closed. Your own shop was not touched.
pause
exit /b 0

rem ---- what went wrong ---------------------------------------------------------
:crashed
echo.
echo  ==========================================================
echo   THE DEMO STOPPED WITH AN ERROR
echo  ==========================================================
set "LOGDIR=%ProgramData%\Area11\demo\logs"
if exist "%LOGDIR%\crash-*.txt" goto :showcrash
echo   No crash report was written. Send the first error line above.
pause
exit /b 1

:showcrash
set "CRASHFILE="
for /f "delims=" %%f in ('dir /b /o-d "%LOGDIR%\crash-*.txt" 2^>nul') do if not defined CRASHFILE set "CRASHFILE=%LOGDIR%\%%f"
echo   Report: %CRASHFILE%
echo.
type "%CRASHFILE%"
pause
exit /b 1

:buildfailed
echo.
echo  ==========================================================
echo   BUILD FAILED
echo  ==========================================================
echo   Copy the FIRST error line above and send it in the chat.
echo   The line looks like:  Something.cs^(12,34^): error CS1234: ...
echo.
pause
exit /b 1

:incomplete
echo  [X]  This folder is not a complete copy of Area11 (Area11.sln is missing).
echo       Open the folder you cloned in GitHub Desktop and run this file there.
echo.
pause
exit /b 1

:nodotnet
echo  [X]  .NET SDK not found. Install the free .NET 10 SDK first:
echo           https://dotnet.microsoft.com/download/dotnet/10.0
echo.
pause
exit /b 1
