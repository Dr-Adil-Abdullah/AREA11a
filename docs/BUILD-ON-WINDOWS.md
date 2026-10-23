# Area11 — building on Windows

The whole application is written but has **never been compiled**: the machine where it is produced is
Linux with no .NET SDK, and WPF only builds on Windows. This page is the exact procedure, plus what to
send back if something fails.

## 1. What to install (once)

| Thing | Where | Why |
|---|---|---|
| **.NET 10 SDK** | https://dotnet.microsoft.com/download/dotnet/10.0 | to build and run |
| **Visual Studio 2022** (optional) | https://visualstudio.microsoft.com | click-and-run, easier debugging |

The shop PC needs **nothing** installed later: the published app is self-contained.

## 2. Build and run — the easy way

Double-click **`START-AREA11.bat`** in the repository folder. It pulls the code, builds it and starts
the app. Step-by-step Urdu version: [`HOW-TO-RUN-URDU.md`](HOW-TO-RUN-URDU.md).

Manually, from the repository root:

```bat
git pull
dotnet build
dotnet test
dotnet run --project dotnet\Area11.Desktop
```

- There is exactly **one solution**, `Area11.sln`, in the root of the repository. Open that.
- `dotnet build` with no arguments finds it automatically.
- Expected: everything builds, **659 tests pass**, and the sign-in window opens.

### 2b. The demo shop - **removed** (D-77)

Until v0.30.1 the repository had a `DEMO-AREA11.cmd` that opened an invented two-and-a-half-month
pharmacy in its own folder. The owner asked for it to be removed once the real app was ready
(**D-77**, 23 Oct 2026), and it is gone: the script, the page, the service and its tests.

**What this means for you:** there is one program and one database. `START-AREA11.bat` always opens
the shop's own books, and no flag, button or file can point it at invented rows.

**If a copy of the demo folder is still on the PC** (`C:\ProgramData\Area11\demo`), it is not read by
anything any more. Delete that folder when convenient; the shop's own data is in
`C:\ProgramData\Area11\` and is not touched by deleting it.

### 2c. If the build fails - send one file, not twenty screenshots (v0.30.3, D-78)

`START-AREA11.bat` writes the whole build to `build.log` and puts **only the error lines** on the
screen. Read the **first** one; it is the one that matters. If you would rather send everything,
double-click **`tools\SEND-REPORT.cmd`**: it writes one text file (`report-<date>.txt`) holding the
build, `dotnet --info`, the branch, the project files, the shop folder (names, sizes and dates - **no
row of your books is read**), the whole newest crash report, the end of the log and the last build
and test output.

`tools\RUN-TESTS.cmd` works the same way: the run goes to `test.log`, and the screen shows the
result line, the names of the failed tests and any compile errors - never the middle of a failure.

> `tools\diagnose.bat` is the small folder-only check, and it runs **without PowerShell**; use it if
> script execution is blocked on that PC.
