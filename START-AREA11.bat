@echo off
rem ============================================================================
rem  Area11 - start the application
rem  Double-click this file. It checks the folder, updates the code, builds it
rem  and runs it. It never touches the shop database in C:\ProgramData\Area11.
rem ============================================================================
setlocal
title Area11
cd /d "%~dp0"

echo.
echo  ==========================================================
echo    AREA11 - pharmacy POS
echo  ==========================================================
echo.

rem ---- 0. is this a complete copy? -------------------------------------------
rem  A folder that holds only some of the files (an old download, or a half
rem  finished update) cannot be built. Say so in plain words instead of showing
rem  a compiler error.
if not exist "Area11.sln" goto :incomplete
if not exist "dotnet\Area11.Desktop\Area11.Desktop.csproj" goto :incomplete
echo  [1/5] Folder looks complete.
goto :dotnetcheck

:incomplete
echo  [X]  This folder is an OLD or INCOMPLETE copy of Area11.
echo.
echo       Area11.sln was not found in: %CD%
echo.
echo       Fix it in three steps:
echo.
echo         1. Open this link in the browser - it downloads a fresh copy:
echo            https://github.com/Dr-Adil-Abdullah/AREA11a/archive/refs/heads/arena/a8a3b2e0-area11a.zip
echo.
echo         2. Right-click the downloaded file - "Extract All" - choose  D:\
echo.
echo         3. Open the new folder and double-click START-AREA11.bat there.
echo.
echo       Your shop data is safe: it lives in C:\ProgramData\Area11 and no step here touches it.
echo.
pause
exit /b 1

rem ---- 1. is the .NET SDK present? -------------------------------------------
:dotnetcheck
where dotnet >nul 2>nul
if errorlevel 1 (
    echo.
    echo  [X]  .NET SDK not found.
    echo.
    echo       Install the free .NET 10 SDK first:
    echo           https://dotnet.microsoft.com/download/dotnet/10.0
    echo.
    echo       Then close this window and double-click this file again.
    pause
    exit /b 1
)
echo  [2/5] .NET SDK found.

rem ---- 2. remove leftovers that point at the old prototype -------------------
rem  An old "Area11.WPF.sln" makes Visual Studio open the wrong project and it
rem  then reports MSB4025 about Area11.WPF.csproj.
if exist "Area11.WPF.sln" (
    del /q "Area11.WPF.sln"
    echo  [3/5] Removed the old Area11.WPF.sln ^(it opened the prototype by mistake^).
) else (
    echo  [3/5] No stale solution file.
)

rem  Visual Studio remembers the last solution in the .vs folder; a stale one
rem  keeps the MSB4025 error alive even after the file itself is gone.
if exist ".vs" (
    rmdir /s /q ".vs"
    echo  [3/5] Cleared the Visual Studio memory of the old solution ^(.vs^).
)

rem  The prototype now lives in prototypes\Area11.WPF. An old copy in the root is
rem  moved there, never deleted, so nothing of the owner work can be lost.
if exist "Area11.WPF" (
    if not exist "prototypes\Area11.WPF" (
        if not exist "prototypes" mkdir "prototypes"
        move "Area11.WPF" "prototypes\Area11.WPF" >nul
        echo  [3/5] Moved the old prototype folder into prototypes\.
    ) else (
        echo  [3/5] NOTE: an extra Area11.WPF folder sits here - run tools\CHECK-AREA11.cmd
    )
)
if exist "WPFSample" (
    if not exist "prototypes\WPFSample" (
        if not exist "prototypes" mkdir "prototypes"
        move "WPFSample" "prototypes\WPFSample" >nul
        echo  [3/5] Moved the old WPFSample folder into prototypes\.
    )
)

rem ---- 3. update from GitHub ------------------------------------------------
if not exist ".git" (
    echo  [4/5] This copy was downloaded as a file, not cloned - skipping the update.
    goto :build
)

where git >nul 2>nul
if errorlevel 1 (
    echo  [4/5] The git command is not available - using the code already here.
    echo        To update: GitHub Desktop -^> "Fetch origin" then "Pull origin", then run this file again.
    goto :build
)

rem  All the work lives on ONE branch. A clone made with GitHub Desktop or Visual Studio
rem  starts on "main", where the new code is not - so say it plainly instead of quietly
rem  building an old version.
set "BRANCH="
for /f "delims=" %%b in ('git rev-parse --abbrev-ref HEAD 2^>nul') do set "BRANCH=%%b"

if not "%BRANCH%"=="arena/a8a3b2e0-area11a" (
    echo.
    echo  [X]  This folder is on the branch "%BRANCH%", but the new code is on
    echo       "arena/a8a3b2e0-area11a".
    echo.
    echo       Fix it in one step:
    echo         GitHub Desktop - click "Current Branch" at the top, pick
    echo                          "arena/a8a3b2e0-area11a", then "Fetch origin"
    echo                          and "Pull origin".
    echo         Visual Studio  - the branch list is in the bottom right corner.
    echo         VS Code        - click the branch name in the bottom left corner.
    echo.
    echo       Your shop data is safe: C:\ProgramData\Area11 is never touched.
    echo.
    pause
    exit /b 1
)

echo  [4/5] Updating the code from GitHub ^(branch %BRANCH%^)...
git pull --ff-only
if errorlevel 1 (
    echo.
    echo        Could not update automatically. Continuing with the code already here.
)

rem ---- 4. build -------------------------------------------------------------
:build
echo  [5/5] Building...  ^(this takes a minute the first time^)
dotnet build Area11.sln -v minimal
if errorlevel 1 (
    echo.
    echo  ==========================================================
    echo   BUILD FAILED
    echo  ==========================================================
    echo   Copy the FIRST error line above and send it in the chat.
    echo   The line looks like:  Something.cs^(12,34^): error CS1234: ...
    echo.
    echo   If the error mentions a project file that could not be loaded,
    echo   run tools\CHECK-AREA11.cmd once and try again.
    echo.
    pause
    exit /b 1
)

rem ---- 5. run ---------------------------------------------------------------
echo.
echo  Starting Area11...
echo.
echo   Sign in with the user name left EMPTY and the password: area11
echo.
dotnet run --project "dotnet\Area11.Desktop" --no-build

if errorlevel 1 goto 

   :crashed
set "LOGDIR=%ProgramData%\Area11\logs"
set "CRASHFILE="
for /f "delims=" %%f in ('dir /b /o-d "%LOGDIR%\crash-*.txt" 2^>nul') do if not defined CRASHFILE set "CRASHFILE=%LOGDIR%\%%f"

echo.
echo  ===========================================================
echo   AREA11 STOPPED WITH AN ERROR
echo  ===========================================================
if defined CRASHFILE goto :showcrash
goto :showlog

:showcrash
echo   Report: %CRASHFILE%
echo.
type "%CRASHFILE%"
goto :done

:showlog
echo   No crash report was written, so the program closed without an
echo   exception of its own. The end of the newest log is shown instead:
echo.
set "LOGFILE="
for /f "delims=" %%f in ('dir /b /o-d "%LOGDIR%\*.log" 2^>nul') do if not defined LOGFILE set "LOGFILE=%LOGDIR%\%%f"
if not defined LOGFILE goto :nofiles
echo   %LOGFILE%
echo.
powershell -NoProfile -Command "Get-Content -LiteralPath '%LOGFILE%' -Tail 40"
goto :done

:nofiles
echo   The folder %LOGDIR% has no files at all.

:done
echo.
echo   Copy EVERYTHING above this line and send it in the chat.
echo   Your shop data is untouched: C:\ProgramData\Area11 was only read.
pause
exit /b 1
