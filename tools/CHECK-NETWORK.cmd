@echo off
rem ============================================================================
rem  Area11 - why can GitHub not be reached?  (double-click this file)
rem
rem  GitHub Desktop says:
rem      fatal: unable to access 'https://github.com/...': Failed to connect
rem      to github.com port 443 after 21005 ms: Could not connect to server
rem
rem  That sentence is NOT about Area11. It means this computer could not open a
rem  connection to github.com at all. This file only LOOKS and reports; it
rem  changes nothing, deletes nothing and never touches the shop data.
rem ============================================================================
title Area11 - GitHub connection check
cd /d "%~dp0.."

echo.
echo   Folder: %CD%
echo.
echo   ---------------------------------------------------------------
echo   1. Does the name github.com resolve to an address?
echo   ---------------------------------------------------------------
nslookup github.com 2>nul

echo.
echo   ---------------------------------------------------------------
echo   2. THE IMPORTANT ONE - can this PC reach port 443 of github.com?
echo      This is exactly what git needs.
echo   ---------------------------------------------------------------
powershell -NoProfile -ExecutionPolicy Bypass -Command "$r = Test-NetConnection github.com -Port 443 -WarningAction SilentlyContinue; if ($r.TcpTestSucceeded) { Write-Host '   RESULT: port 443 OPEN - the network reaches GitHub' -ForegroundColor Green } else { Write-Host '   RESULT: port 443 BLOCKED - the network cannot reach GitHub' -ForegroundColor Red }"

echo.
echo   ---------------------------------------------------------------
echo   3. Web request (the same path the browser uses)
echo   ---------------------------------------------------------------
powershell -NoProfile -ExecutionPolicy Bypass -Command "try { $r = Invoke-WebRequest -Uri 'https://github.com' -UseBasicParsing -TimeoutSec 25; Write-Host ('   github.com answered ' + $r.StatusCode) } catch { Write-Host ('   FAILED: ' + $_.Exception.Message) }"

echo.
echo   ---------------------------------------------------------------
echo   4. A proxy left behind by a VPN or a company network
echo      (a proxy that no longer exists blocks every update)
echo   ---------------------------------------------------------------
netsh winhttp show proxy

echo.
echo   ---------------------------------------------------------------
echo   5. git's own proxy - an empty answer is normal
echo   ---------------------------------------------------------------
git config --global --get http.proxy
git config --global --get https.proxy

echo.
echo   ---------------------------------------------------------------
echo   6. Which address this folder updates from
echo   ---------------------------------------------------------------
git remote -v

echo.
echo   ===============================================================
echo    WHAT THE RESULT MEANS
echo   ===============================================================
echo.
echo    If step 2 says BLOCKED (or step 3 FAILED):
echo      The internet provider, a VPN or a firewall is stopping GitHub.
echo      Try these in order, one at a time:
echo        a) Turn the VPN OFF if it is on, or ON if the provider blocks
echo           GitHub. Run this file again after each change.
echo        b) Mobile hotspot: connect the phone by cable or Wi-Fi, wait
echo           until Windows shows "Connected", run this file again.
echo        c) Change the DNS: Settings - Network - your adapter -
echo           "Use the following DNS": 1.1.1.1 and 8.8.8.8.
echo           Then run:   ipconfig /flushdns
echo        d) A different network (home, office, a neighbour) proves
echo           whether it is the provider.
echo.
echo      Meanwhile the SAME code can be downloaded in the browser - see
echo      docs\GITHUB-NETWORK.md, or just open:
echo        https://github.com/Dr-Adil-Abdullah/AREA11a/archive/refs/heads/arena/a8a3b2e0-area11a.zip
echo      Extract it to D:\ and double-click START-AREA11.bat inside it.
echo.
echo    If step 2 says OPEN but GitHub Desktop still fails:
echo      A proxy is set in git. Clear it with these two lines:
echo          git config --global --unset http.proxy
echo          git config --global --unset https.proxy
echo      Then in GitHub Desktop: Fetch origin, then Pull origin.
echo.
echo    If the message instead says "update_ref failed" or "index.lock",
echo      that is a different fault - run tools\FIX-UPDATE.cmd.
echo.
pause
