@echo off
setlocal
rem Read-only offline sample book; no build,git update or shop-data operation.
set "PAGE=%~dp0docs\guide-prototype\book.html"
if not exist "%PAGE%" (
    echo The Urdu guide file is missing. Please update or extract the complete folder.
    pause
    exit /b 1
)
start "" "%PAGE%"
exit /b 0
