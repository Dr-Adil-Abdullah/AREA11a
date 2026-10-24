@echo off
rem ============================================================================
rem  Area11 - repair a failed "Update" in GitHub Desktop (double-click this file)
rem
rem  Why this exists
rem    A git command that was interrupted - or a second program holding the
rem    folder - can leave a *.lock file inside .git. The next update then stops
rem    with messages like:
rem        update_ref failed for ref 'ORIG_HEAD'
rem        cannot lock ref 'ORIG_HEAD': Unable to create ...\ORIG_HEAD.lock
rem        Unable to create ...\.git\index.lock: File exists
rem    This file removes those leftover lock files and shows what else is
rem    standing in the way.
rem
rem  What it does NOT do
rem    It never deletes a source file, never touches the database and never
rem    throws away your work. The shop data lives in C:\ProgramData\Area11,
rem    which is not inside this folder.
rem ============================================================================
title Area11 - repair the update
cd /d "%~dp0.."

echo.
echo   Folder: %CD%
echo.
echo   Close Visual Studio, VS Code and GitHub Desktop before pressing a key.
echo   Nothing outside the .git folder is touched.
echo.
pause

if not exist ".git" (
    echo   [X] This folder is not a git clone, so there is nothing to repair.
    echo       Clone it again with GitHub Desktop.
    goto :done
)

echo   Looking for leftover lock files ...
powershell -NoProfile -ExecutionPolicy Bypass -Command ^
  "$n = 0; Get-ChildItem -Path '.git' -Filter '*.lock' -Recurse -Force -File -ErrorAction SilentlyContinue | ForEach-Object { Write-Host ('   deleting ' + $_.FullName); Remove-Item -LiteralPath $_.FullName -Force -ErrorAction SilentlyContinue; $n++ }; if ($n -eq 0) { Write-Host '   no lock file found' } else { Write-Host ('   ' + $n + ' lock file(s) removed') }"

rem ORIG_HEAD is only a bookmark to the previous state; git writes it again on
rem the next merge. Removing it clears the read-only case as well.
if exist ".git\ORIG_HEAD" (
    echo   clearing the old ORIG_HEAD bookmark ...
    powershell -NoProfile -ExecutionPolicy Bypass -Command ^
      "Remove-Item -LiteralPath '.git\ORIG_HEAD' -Force -ErrorAction SilentlyContinue"
)

set "GIT="
where git >nul 2>&1 && set "GIT=git"

echo.
if not defined GIT (
    echo   git is not on the PATH - that is fine, GitHub Desktop has its own copy.
    echo   Only the branch and the file list below are missing.
) else (
    echo   Branch:
    %GIT% rev-parse --abbrev-ref HEAD 2>nul
    echo.
    echo   Last commit:
    %GIT% log -1 --format="   %%h  %%s" 2>nul
    echo.
    echo   Files with local changes ^(empty = nothing of yours is in the way^):
    %GIT% status --short 2>nul
)

echo.
echo   ------------------------------------------------------------------
echo   Now, in GitHub Desktop:
echo     1. Repository -^> Fetch origin
echo     2. Repository -^> Pull origin
echo.
echo   If the list above shows a file you never edited, then:
echo     Changes tab -^> right-click -^> Discard all changes -^> Pull origin
echo   ------------------------------------------------------------------
echo.

:done
pause
