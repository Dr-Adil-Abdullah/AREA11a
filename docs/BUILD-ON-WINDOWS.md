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
- Expected: everything builds, **68 tests pass**, and the sign-in window opens.

> **Seeing the old blue window that says "app is running"?** That is `prototypes/WPFSample` — the very
> first smoke test from day one, not Area11. It sits in `prototypes\` now and is only kept as history.
> The real app is the `Area11.Desktop` project (assembly name `Area11`).

## 3. First sign-in

| Field | Value |
|---|---|
| User name | *leave empty* (this means "the owner") |
| Password | `area11` |

Change the password on day one (Products → Settings when the settings screen lands; for now it is in
the database and the next phase adds the screen).

The database is created automatically at:

```
C:\ProgramData\Area11\area11.db
```

If that folder is not writable the app falls back to `%LOCALAPPDATA%\Area11`.
Logs: `C:\ProgramData\Area11\logs\app-YYYY-MM-DD.log`.

## 4. When the build fails

Copy the **first** error (the top one, not the last) and send it in the chat.

```bat
dotnet --info
dotnet build -v minimal
```

| Symptom | Likely cause | Fix |
|---|---|---|
| `error CS0103: The name 'Path' / 'Directory' / 'File' does not exist` | almost always a project that failed to load, so the editor cannot resolve any type | fix the project-load error first (row below); explicit `using` lines are now in every file as a safeguard |
| The window that opens is the blue "app is running" one | the prototype was started instead of Area11 | run `dotnet run --project dotnet\Area11.Desktop`, or just double-click `START-AREA11.bat` |
| `The project file could not be loaded ... Area11.WPF\Area11.WPF.csproj` | an old `Area11.WPF.sln` is still in the folder, so Visual Studio opens the prototype | delete `Area11.WPF.sln` and the `.vs` folder, then open the root `Area11.sln` (`START-AREA11.bat` does this automatically) |
| `error CS0121: The call is ambiguous between Math.Round(double)` and `Math.Round(decimal)` | an `int`/`long` was passed to `Math.Round` | fixed — all money rounding goes through `Calc.Round` / `Calc.RoundMoney`; the checker now forbids raw `Math.Round` |
| `MSB1003` / no project found | the command was run in the wrong folder | run it in the repository root (the folder that contains `Area11.sln`) |
| `Area11.sln` is not in the folder at all | the download was interrupted, or an old copy was merged into a newer one, so old prototype files and new files sit side by side | take a fresh copy: [download the ZIP](https://github.com/Dr-Adil-Abdullah/AREA11a/archive/refs/heads/arena/a8a3b2e0-area11a.zip), extract to a **new** folder and run `START-AREA11.bat` there |
| `NETSDK1045` / SDK not found | .NET 10 SDK missing | install it, close and reopen the terminal |
| `NU1102` package not found | wrong package version or offline NuGet cache | send the first error line; check the internet once |
| `Migrations folder was not found` | content files were not copied | `dotnet build` again, then check `dotnet\Area11.Desktop\bin\Debug\net10.0-windows\Migrations` |
| `SQLite Error 14: unable to open database` | `C:\ProgramData\Area11` not writable | run once as administrator; otherwise the app falls back automatically |
| Window opens but the list is empty | first-run seed failed | read `C:\ProgramData\Area11\logs\app-*.log` |

Anything not in this table: send the first error line and the file name — that is enough.

### The project-load error (MSB4025) and how to prove where it comes from

`MSB4025: The project file could not be loaded ... Area11.WPF\Area11.WPF.csproj` means MSBuild is
reading an **old solution**. The prototype moved to `prototypes\Area11.WPF`, so the real solution,
`Area11.sln` in the repository root, must be the only one opened.

Double-click **`tools\CHECK-AREA11.cmd`** (diagnose and repair) or **`tools\diagnose.bat`**
(diagnose only, writes `tools\diagnosis.txt`). Both start in the right folder by themselves, so
nothing has to be typed - typing a copied prompt such as `PS D:\areaa>` is executed as a command and
produces a screen full of red errors.

The doctor lists every `.sln`, checks that each project it names really exists, reports a stale
`.vs` folder or an old `Area11.WPF` folder, prints the git branch and commit, and (with `-Fix`)
removes only the obsolete solution (keeping a `.bak`) and renames the old prototype folder.
It never deletes project code and never touches `C:\ProgramData\Area11`.

Manual equivalent, step by step, is in [`HOW-TO-RUN-URDU.md`](HOW-TO-RUN-URDU.md).

## 5. What already works in this build

- **Sign-in** — owner password, staff PIN, five tries then a two-minute lock, every attempt audited.
- **Counter (POS)** — search, box/strip/loose, live total computed by the same code that saves,
  FEFO batch picking, role discount limit, round-down with cost guard, cash tendered → change,
  credit to the customer's account, negative stock allowed with a warning only.
- **Purchases (PINV)** — supplier (or add one inline), batch number, expiry (month or date), cost and
  retail, sample line at zero cost, supplier payable, part payment, full audit.
- **Products** — list and search, create, edit, soft delete, categories and companies with "＋ New"
  inside the form, VIP/doctor rates, rack and room.
- **Dashboard** — today's bills and sales, profit, credit, payables, low stock, negative stock,
  expired batches.
- **Black box** — every login, product change and price change with old → new values.
- **Database** — the exact 15-migration schema, including the owner's `photos` table.

## 5b. Printing (D-05, D-23, D-26)

- On the counter screen, tick **"Print the receipt automatically"** to print at the moment a bill is
  saved; leave it empty for manual printing with **"Print last bill again"**. The choice is stored in
  the setting `printer.autoPrint`, so it survives a restart.
- Paper width comes from `printer.width`: **58** = 32 characters per line, **80** = 48. Default 58.
- The printer name comes from `printer.name`; empty means "the Windows default printer".
- A missing or switched-off printer never loses a bill: the sale is saved first, then the print is
  attempted, and a red message says what happened.

## 6. What comes next (still Phase 1)

The printed receipt with **instant ESC/POS printing** (58 / 80 mm), the hold cart, split payment, and
the Settings screen with per-person permission ticks. Then Phase 2: returns, quarantine and alerts.

Full list with IDs: [`PROGRESS.md`](PROGRESS.md).
