@echo off
rem ============================================================================
rem  Area11 - run the tests (double-click this file)
rem  Expected: "Passed! - Failed: 0" - every test green.
rem  The banner below prints the version it is about to test and the number of
rem  cases that version has, both read from the code. A tree that was not pulled
rem  says so at once, instead of looking like a fault in the shop.
rem ============================================================================
title Area11 - tests
cd /d "%~dp0.."

rem How many cases this build should run. tools\check_repo.py fails if this number
rem and the suite disagree, so it cannot quietly go stale.
set "EXPECTED=860"

rem  Which build is about to run? The line below is the ONLY place this script names a version,
rem  and tools\check_repo.py fails if it disagrees with BuildInfo.cs - so it cannot go stale, and
rem  the owner is never told an old tree is the new one. Reading the number out of the source with
rem  findstr printed "vunknown" on his PC (v0.30.0): the tokens did not line up, and a banner that
rem  says nothing is worse than no banner, because it looks like a failure of the tree.
set "BUILD=v0.35.18 (Obsidian)"


echo.
echo  ==========================================================
echo    AREA11 - running the tests
echo  ==========================================================
echo.
echo    Build under test : %BUILD%
echo    Expect at the end: total: %EXPECTED%, failed: 0
echo.
echo    If the total below is SMALLER than %EXPECTED%, this folder was not
echo    pulled (an older build), and a green run proves nothing about this one.
echo.

if not exist "Area11.sln" (
    echo  [X] Area11.sln not found here: %CD%
    echo      Run this file from the folder that contains Area11.sln.
    echo.
    pause
    exit /b 1
)

where dotnet >nul 2>nul
if errorlevel 1 (
    echo  [X] .NET SDK not found. Install it from https://dotnet.microsoft.com/download/dotnet/10.0
    echo.
    pause
    exit /b 1
)

rem Only Core and the tests are built here: the window program is not needed to test the shop
rem rules, and leaving it out keeps the run short even when the Desktop project is mid-edit.
rem
rem The whole run goes to test.log and only two things reach the screen: the result line and
rem the names of the tests that failed. Why: the failure text of one theory row can be forty
rem lines, and it used to push the one line that decides the run - "failed: 0" - off the top
rem of the window. The log is what tools\SEND-REPORT.cmd sends.
set "TESTLOG=%CD%\test.log"
set "ERRLOG=%CD%\test-build-errors.txt"
set "FAILEDLOG=%CD%\test-failed-names.txt"
set "TOTALFILE=%CD%\test-total.txt"
del /q "%TESTLOG%" 2>nul
del /q "%ERRLOG%" 2>nul
del /q "%FAILEDLOG%" 2>nul
del /q "%TOTALFILE%" 2>nul

dotnet test "dotnet\Area11.Tests\Area11.Tests.csproj" -v minimal > "%TESTLOG%" 2>&1
set "TESTEXIT=%errorlevel%"

findstr /i /c:": error " /c:" error CS" /c:" error MC" /c:" error MSB" "%TESTLOG%" > "%ERRLOG%" 2>nul

echo.
echo  ==========================================================
echo   THE RESULT ^(the last lines of the run^)
echo  ==========================================================
echo.
powershell -NoProfile -Command "Get-Content -LiteralPath '%TESTLOG%' -Tail 15"

rem  A failed test is named on its own line ("Failed Area11.Tests.SaleTests...."). Listing the
rem  names is what lets the first fault be fixed without reading the whole log.
findstr /b /i /c:"Failed " "%TESTLOG%" > "%FAILEDLOG%" 2>nul
set "FAILEDSIZE=0"
if exist "%FAILEDLOG%" for %%A in ("%FAILEDLOG%") do set "FAILEDSIZE=%%~zA"
if not "%FAILEDSIZE%"=="0" (
    echo  ----------------------------------------------------------
    echo   THE TESTS THAT FAILED:
    echo.
    type "%FAILEDLOG%"
)

set "ERRSIZE=0"
if exist "%ERRLOG%" for %%A in ("%ERRLOG%") do set "ERRSIZE=%%~zA"
if not "%ERRSIZE%"=="0" (
    echo  ----------------------------------------------------------
    echo   THE TEST PROJECT DID NOT COMPILE:
    echo.
    type "%ERRLOG%"
)

rem ==========================================================================
rem  THE VERDICT.
rem
rem  Until v0.30.9 this file printed the failure advice on EVERY run - including a
rem  fully green one. The owner's first 686/686 run ended with "Not OK is not a
rem  disaster", which is the tool telling a lie at the exact moment it is supposed
rem  to be trusted. A runner that cannot say "green" is not a runner, it is a log.
rem
rem  Two questions, asked in this order because they are different:
rem    1. did anything fail?      -> the exit code of `dotnet test`, and the word
rem                                  "Failed: 0" in the summary line;
rem    2. did the RIGHT suite run? -> the total, compared with EXPECTED. A green
rem                                  run of an old folder proves nothing at all.
rem ==========================================================================
echo.
if not "%TESTEXIT%"=="0" goto :testsnotok
findstr /r /c:"Failed: *0," "%TESTLOG%" >nul 2>&1
if errorlevel 1 goto :testsnotok

rem  The total, read out of the summary line with PowerShell because cmd cannot
rem  pick the Nth number out of "... Total:   686, Duration: 51 s" reliably.
rem  Written with [Console]::Out.Write and no line break on purpose: `set /p` on a file
rem  that ends in CRLF can leave a stray carriage return inside the number, and
rem  "686<CR>" makes the comparison below a syntax error rather than a comparison.
powershell -NoProfile -Command "$t=(Get-Content -LiteralPath '%TESTLOG%') -join ' '; $m=[regex]::Matches($t,'Total:\s*(\d+)'); if ($m.Count -gt 0) { [Console]::Out.Write($m[$m.Count-1].Groups[1].Value) }" > "%TOTALFILE%" 2>nul

set "TOTALN="
if exist "%TOTALFILE%" set /p TOTALN=<"%TOTALFILE%"

rem  Only a number may reach the comparison. Anything else (a Windows error, an empty
rem  file, a localised test runner) falls through to the "green but unreadable" screen
rem  instead of turning into a batch syntax error.
if defined TOTALN echo %TOTALN%| findstr /r /c:"^[0-9][0-9]*$" >nul 2>&1
if errorlevel 1 set "TOTALN="

if not defined TOTALN goto :testsgreen
if %TOTALN% LSS %EXPECTED% goto :testsstale

echo  ==========================================================
echo    ALL GREEN - %TOTALN% of %EXPECTED% cases passed, failed: 0
echo  ==========================================================
echo.
echo    Build %BUILD%.
echo    Every test passed. Nothing is wrong and nothing needs sending.
echo    Full output: %TESTLOG%
echo.
pause
exit /b 0

:testsstale
echo  ==========================================================
echo    GREEN, BUT THIS IS AN OLD FOLDER
echo  ==========================================================
echo.
echo    %TOTALN% case(s) ran, and this build should have %EXPECTED%.
echo    The run itself was clean - but it tested an older copy of the code,
echo    so it proves nothing about %BUILD%.
echo.
echo    Fix: GitHub Desktop - "Fetch origin" then "Pull origin", and run this
echo    file again. The total must reach %EXPECTED%.
echo.
echo    Full output: %TESTLOG%
echo.
pause
exit /b 1

:testsgreen
echo  ==========================================================
echo    ALL GREEN - every test passed, failed: 0
echo  ==========================================================
echo.
echo    Build %BUILD%. The number of cases could not be read from the log,
echo    so open it and check the summary line says "Total: %EXPECTED%".
echo    Full output: %TESTLOG%
echo.
pause
exit /b 0

:testsnotok
echo  ----------------------------------------------------------
echo   NOT OK - and that is fixable, not a disaster.
echo.
echo   Send the lines above ^(or the whole thing with tools\SEND-REPORT.cmd^)
echo   and the first one gets fixed. The count grows with every version, so
echo   only "failed: 0" matters - and the total must never be BELOW %EXPECTED%.
echo   Full output: %TESTLOG%
echo  ----------------------------------------------------------
pause
exit /b 1
