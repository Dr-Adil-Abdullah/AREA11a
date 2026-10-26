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
echo  [1/6] Folder looks complete.
goto :dotnetcheck

:incomplete
echo  [X]  This folder is an OLD or INCOMPLETE copy of Area11.
echo.
echo       Area11.sln was not found in: %CD%
echo.
echo       Fix it in three steps:
echo.
echo         1. Open this link in the browser - it downloads a fresh copy:
echo            https://github.com/Dr-Adil-Abdullah/AREA11a/archive/refs/heads/main.zip
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
echo  [2/6] .NET SDK found.

rem ---- 2. remove leftovers that point at the old prototype -------------------
rem  An old "Area11.WPF.sln" makes Visual Studio open the wrong project and it
rem  then reports MSB4025 about Area11.WPF.csproj.
if exist "Area11.WPF.sln" (
    del /q "Area11.WPF.sln"
    echo  [3/6] Removed the old Area11.WPF.sln ^(it opened the prototype by mistake^).
) else (
    echo  [3/6] No stale solution file.
)

rem  Visual Studio remembers the last solution in the .vs folder; a stale one
rem  keeps the MSB4025 error alive even after the file itself is gone.
if exist ".vs" (
    rmdir /s /q ".vs"
    echo  [3/6] Cleared the Visual Studio memory of the old solution ^(.vs^).
)

rem  The prototype now lives in prototypes\Area11.WPF. An old copy in the root is
rem  moved there, never deleted, so nothing of the owner work can be lost.
if exist "Area11.WPF" (
    if not exist "prototypes\Area11.WPF" (
        if not exist "prototypes" mkdir "prototypes"
        move "Area11.WPF" "prototypes\Area11.WPF" >nul
        echo  [3/6] Moved the old prototype folder into prototypes\.
    ) else (
        echo  [3/6] NOTE: an extra Area11.WPF folder sits here - run tools\CHECK-AREA11.cmd
    )
)
if exist "WPFSample" (
    if not exist "prototypes\WPFSample" (
        if not exist "prototypes" mkdir "prototypes"
        move "WPFSample" "prototypes\WPFSample" >nul
        echo  [3/6] Moved the old WPFSample folder into prototypes\.
    )
)

rem ---- 3. update from GitHub ------------------------------------------------
if not exist ".git" (
    echo  [4/6] This copy was downloaded as a file, not cloned - skipping the update.
    goto :build
)

where git >nul 2>nul
if errorlevel 1 (
    echo  [4/6] The git command is not available - using the code already here.
    echo        To update: GitHub Desktop -^> "Fetch origin" then "Pull origin", then run this file again.
    goto :build
)

rem  The product normally lives on "main". An Arena repair is delivered on the named working
rem  branch until its pull request is merged, so that branch must be accepted explicitly too.
rem  Anything else is refused: a stray branch can otherwise make an old copy look like an update.
set "WORK_BRANCH=arena/60395e95-area11a"
set "BRANCH="
for /f "delims=" %%b in ('git rev-parse --abbrev-ref HEAD 2^>nul') do set "BRANCH=%%b"

if not "%BRANCH%"=="main" if not "%BRANCH%"=="%WORK_BRANCH%" (
    echo.
    echo  [X]  This folder is on "%BRANCH%", which is not an approved code branch.
    echo       Use "main" after the repair is merged, or "%WORK_BRANCH%" for this round.
    echo.
    echo       Fix it in one step:
    echo         GitHub Desktop - click "Current Branch" at the top, pick the branch
    echo                          you were given, then "Fetch origin" and "Pull origin".
    echo         Visual Studio  - the branch list is in the bottom right corner.
    echo         VS Code        - click the branch name in the bottom left corner.
    echo.
    echo       Your shop data is safe: C:\ProgramData\Area11 is never touched.
    echo.
    pause
    exit /b 1
)

echo  [4/6] Updating the code from GitHub ^(branch %BRANCH%^)...
git pull --ff-only
if errorlevel 1 (
    echo.
    echo        Could not update automatically. Continuing with the code already here.
)

rem ---- 4a. close the copy that is still open --------------------------------
rem  Windows will not let a build replace a DLL that a running program has loaded.
rem  On 23 Oct 2026 the owner's build stopped with MSB3027 "Failed. The file is
rem  locked by: Area11 (8460)": the program from the previous run was still open
rem  behind this window. No amount of rebuilding fixes that, and it is not a fault
rem  in the code - so the script closes it first, and asks before it does.
rem
rem  The name below must match <AssemblyName> in Area11.Desktop.csproj.
rem  tools\check_repo.py fails if the two ever disagree, because a guard looking
rem  for the wrong process is the same as no guard at all.
set "APPPROCESS=Area11.exe"
set "OPENPID="
for /f "tokens=1,2 delims=," %%a in ('tasklist /fi "imagename eq %APPPROCESS%" /nh /fo csv 2^>nul') do (
    if /i "%%~a"=="%APPPROCESS%" if not defined OPENPID set "OPENPID=%%~b"
)

if not defined OPENPID (
    echo  [5/6] The program is not running - nothing to close.
    goto :build
)

echo.
echo  [5/6] Area11 is still OPEN ^(process %OPENPID%^).
echo        Windows cannot replace an open program's own files, so the build would
echo        stop with "the file is locked by Area11". Nothing is wrong with the code.
echo.
choice /c YN /n /m "       Close the running Area11 now? [Y=yes, N=no] "
if errorlevel 2 goto :stillopen

taskkill /pid %OPENPID% /f >nul 2>&1

rem  Windows releases the file handles a moment after the process is gone; without
rem  this pause the build can still catch the last handle. ~2 seconds.
ping -n 3 127.0.0.1 >nul
echo        Closed. Building the new one.
goto :build

:stillopen
echo.
echo        The old copy is still running, so its files cannot be replaced.
echo.
echo        Close the Area11 window ^(the X in the corner^), then double-click
echo        START-AREA11.bat again.
echo.
echo        Your shop data is safe: C:\ProgramData\Area11 is never touched.
echo.
pause
exit /b 1

rem ---- 4b. build -------------------------------------------------------------
:build
rem  Which build is about to run? The line below is the ONLY place this script names a version,
rem  and tools\check_repo.py fails if it disagrees with BuildInfo.cs - so it cannot go stale, and
rem  the owner is never told an old tree is the new one. Reading the number out of the source with
rem  findstr printed "vunknown" on his PC (v0.30.0): the tokens did not line up, and a banner that
rem  says nothing is worse than no banner, because it looks like a failure of the tree.
set "BUILD=v0.35.9 (Onyx)"

echo        Code on this PC: %BUILD%

echo  [6/6] Building...  ^(this takes a minute the first time^)

rem  The build writes everything it says to build.log, and only the ERROR lines reach the
rem  screen. Why: MSBuild prints hundreds of lines, the one line that matters - the one with
rem  "... error CS1234: ..." - scrolls past, and the owner was being asked to find it by eye
rem  in a wall of restore and warning text. The file is also what tools\SEND-REPORT.cmd
rem  sends, so nothing has to be copied by hand.
set "BUILDLOG=%CD%\build.log"
set "ERRLOG=%CD%\build-errors.txt"
set "PROJLOG=%CD%\build-projects.txt"
del /q "%BUILDLOG%" 2>nul
del /q "%ERRLOG%" 2>nul

dotnet build Area11.sln -v minimal > "%BUILDLOG%" 2>&1
if errorlevel 1 goto :buildfailed

echo        BUILD OK - the program was compiled.
goto :run

:buildfailed
rem  Four patterns, because MSBuild writes its own errors differently from the C# and XAML
rem  compilers ("MSBUILD : error MSB1009" has a space before the colon).
rem  Two of MSBuild's errors are not about the code at all, and printing them as a
rem  wall of text teaches the owner to look for a compiler fault that is not there.
rem  MSB3027 / MSB3021 mean "a running Area11 still holds the file" (23 Oct 2026).
findstr /i /c:"MSB3027" /c:"MSB3021" "%BUILDLOG%" >nul 2>&1
if not errorlevel 1 goto :buildlocked

findstr /i /c:": error " /c:" error CS" /c:" error MC" /c:" error MSB" "%BUILDLOG%" > "%ERRLOG%" 2>nul

echo.
echo  ==========================================================
echo   BUILD FAILED
echo  ==========================================================
set "ERRSIZE=0"
if exist "%ERRLOG%" for %%A in ("%ERRLOG%") do set "ERRSIZE=%%~zA"

if "%ERRSIZE%"=="0" (
    echo   No file-and-line error was printed. The last 30 lines of the build:
    echo.
    powershell -NoProfile -Command "Get-Content -LiteralPath '%BUILDLOG%' -Tail 30"
) else (
    echo   The errors ^(one line each - this is all that is needed^):
    echo.
    type "%ERRLOG%"

    rem  WHICH project broke? Every compiler error names its project at the end of
    rem  the line, in square brackets. Reading that is the difference between
    rem  "something is wrong" and "the window failed, the shop's rules compiled".
    rem  Added in v0.30.9 after the owner's first Desktop build attempt, where the
    rem  only useful fact in forty lines was the project name.
    del /q "%PROJLOG%" 2>nul
    for /f "tokens=2 delims=[]" %%p in ('findstr /c:".csproj]" "%ERRLOG%"') do @echo %%~nxp>>"%PROJLOG%"
    if exist "%PROJLOG%" (
        sort /unique "%PROJLOG%" /o "%PROJLOG%"
        echo.
        echo   The fault is in this project:
        type "%PROJLOG%"
    )
)

echo.
echo   Full output : %BUILDLOG%
echo   The FIRST line above is the one to send in the chat.
echo   Or double-click tools\SEND-REPORT.cmd: it puts these errors, the .NET
echo   version, the shop folder and the newest crash into ONE file.
echo.
pause
exit /b 1

rem  The one failure that is not a code error, said in one plain sentence.
:buildlocked
echo.
echo  ==========================================================
echo   THE PROGRAM IS STILL OPEN - THIS IS NOT A CODE ERROR
echo  ==========================================================
echo.
echo   Windows refused to replace Area11's own files because a copy of
echo   Area11 is still running somewhere ^(often behind this window^).
echo.
echo   1. Look for the Area11 window, in the taskbar or behind this one.
echo   2. Close it with the X in the corner - or end "Area11" in Task Manager.
echo   3. Double-click START-AREA11.bat again.
echo.
echo   Nothing is wrong with the code and nothing has to be repaired.
echo   Your shop data is safe: C:\ProgramData\Area11 is never touched.
echo.
echo   Full output : %BUILDLOG%
echo.
pause
exit /b 1

:run
rem ---- 5. run ---------------------------------------------------------------
rem  A timestamp written BEFORE the program starts. Anything older than this file
rem  belongs to an earlier run - and showing it here would send the repair in the
rem  wrong direction, which is exactly what happened on 23 Oct 2026: the window
rem  printed a report from the day before, written by v0.29.0, for a fault that
rem  had already been fixed.
set "RUNMARKER=%TEMP%\area11-run-start.txt"
echo started %DATE% %TIME%> "%RUNMARKER%"

echo.
echo  Starting Area11...
echo.
echo   Sign in with the user name left EMPTY and the password: area11
echo.
rem  Run the program that was just built, not `dotnet run`: one less moving part, and a batch file
rem  waits for it, so the message below always belongs to THIS run.
set "APPEXE=%CD%\dotnet\Area11.Desktop\bin\Debug\net10.0-windows\Area11.exe"

if exist "%APPEXE%" (
    "%APPEXE%"
) else (
    echo  The program file was not found, using dotnet run instead.
    dotnet run --project "dotnet\Area11.Desktop" --no-build
)

if errorlevel 1 goto :crashed

echo.
echo  Area11 has closed.
pause
exit /b 0

rem ---- 6. what went wrong? -------------------------------------------------
rem  A Windows program has no console of its own, so the exception text does not
rem  always reach this window. The program writes a crash file for every failure;
rem  this step prints the ONE report that belongs to THIS run, and refuses to
rem  present an older one as if it were today's.
rem
rem  Two places are searched, because the report goes wherever the program could
rem  write it: the shop folder, and %TEMP%\Area11 when the shop folder is not
rem  writable (a second counter whose share is switched off).
:crashed
set "LOGDIR=%ProgramData%\Area11\logs"
set "TEMPAPPDIR=%TEMP%\Area11"
set "CRASHFILE="
set "CRASHLIST=%TEMP%\area11-crash-this-run.txt"
del "%CRASHLIST%" 2>nul
powershell -NoProfile -Command "$m=[datetime]::MinValue; if (Test-Path -LiteralPath '%RUNMARKER%') { $m=(Get-Item -LiteralPath '%RUNMARKER%').LastWriteTimeUtc }; $all=@(); $pointer=Join-Path $env:TEMP 'Area11\latest-crash.txt'; if (Test-Path -LiteralPath $pointer) { $raw=Get-Content -LiteralPath $pointer -Raw; if ($raw) { $said=$raw.Trim(); if ($said -and (Test-Path -LiteralPath $said)) { $all += Get-Item -LiteralPath $said } } }; foreach ($d in @('%LOGDIR%','%TEMPAPPDIR%')) { if (Test-Path -LiteralPath $d) { $all += Get-ChildItem -LiteralPath $d -Filter 'crash-*.txt' -File -ErrorAction SilentlyContinue } }; $new=@($all | Where-Object { $_.LastWriteTimeUtc -gt $m } | Sort-Object LastWriteTimeUtc -Descending); if ($new.Count -gt 0) { [Console]::Out.Write($new[0].FullName) }" > "%CRASHLIST%" 2>nul
if exist "%CRASHLIST%" for /f "usebackq delims=" %%f in ("%CRASHLIST%") do set "CRASHFILE=%%f"

echo.
echo  ===========================================================
echo   AREA11 STOPPED WITH AN ERROR
echo  ===========================================================
if defined CRASHFILE goto :showcrash
goto :showlog

:showcrash
echo   Report: %CRASHFILE%
echo   This report was written by THIS run.
echo.
type "%CRASHFILE%"
goto :done

rem  No NEW report: say so plainly instead of printing yesterday's. An old report
rem  from another version is worse than no report - it names a fault that may no
rem  longer exist and hides the one that does.
:showlog
echo   This run wrote NO crash report of its own.
echo.
call :olderreport
echo.
echo   The end of the newest log is shown instead:
echo.
set "LOGFILE="
for /f "delims=" %%f in ('dir /b /o-d "%LOGDIR%\*.log" 2^>nul') do if not defined LOGFILE set "LOGFILE=%LOGDIR%\%%f"
if not defined LOGFILE goto :nofiles
echo   %LOGFILE%
echo.
powershell -NoProfile -Command "Get-Content -LiteralPath '%LOGFILE%' -Tail 40"
goto :done

:olderreport
set "OLDFILE="
set "OLDDATE="
set "OLDINFO=%TEMP%\area11-old-crash.txt"
del "%OLDINFO%" 2>nul
powershell -NoProfile -Command "$all=@(); $pointer=Join-Path $env:TEMP 'Area11\latest-crash.txt'; if (Test-Path -LiteralPath $pointer) { $raw=Get-Content -LiteralPath $pointer -Raw; if ($raw) { $said=$raw.Trim(); if ($said -and (Test-Path -LiteralPath $said)) { $all += Get-Item -LiteralPath $said } } }; foreach ($d in @('%LOGDIR%','%TEMPAPPDIR%')) { if (Test-Path -LiteralPath $d) { $all += Get-ChildItem -LiteralPath $d -Filter 'crash-*.txt' -File -ErrorAction SilentlyContinue } }; $best=@($all | Sort-Object LastWriteTimeUtc -Descending); if ($best.Count -gt 0) { [Console]::Out.Write($best[0].FullName + '|' + $best[0].LastWriteTime.ToString('yyyy-MM-dd HH:mm')) }" > "%OLDINFO%" 2>nul
if not exist "%OLDINFO%" goto :eof
for /f "usebackq delims=" %%f in ("%OLDINFO%") do set "OLDFILE=%%f"
if not defined OLDFILE goto :eof
for /f "tokens=1* delims=|" %%a in ("%OLDFILE%") do echo     The newest report on this PC is %%a  (%%b, an EARLIER run - not shown).
echo   It describes a different day, so it is named but NOT printed.
goto :eof


:nofiles
echo   The folder %LOGDIR% has no files at all.

:done
echo.
echo   Copy EVERYTHING above this line and send it in the chat.
echo   Your shop data is untouched: C:\ProgramData\Area11 was only read.
pause
exit /b 1
