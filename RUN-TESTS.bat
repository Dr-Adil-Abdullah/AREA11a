@echo off
rem Area11 tests from the main folder. Keep the existing test runner as the source of truth.
setlocal
if not exist "%~dp0tools\RUN-TESTS.cmd" (
    echo Area11 test runner was not found. Please update or extract the complete folder.
    pause
    exit /b 1
)
call "%~dp0tools\RUN-TESTS.cmd"
exit /b %errorlevel%
