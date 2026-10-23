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
- Expected: everything builds, **695 tests pass**, and the sign-in window opens.

### 2b. The demo shop — `DEMO-AREA11.cmd` (D-76)

Double-click **`DEMO-AREA11.cmd`** in the repository root to look at Area11 filled with two and a half
months of an invented pharmacy. It starts the same program with `--demo`, which keeps **everything**
under `C:\ProgramData\Area11\demo` — database, photos, logs, backups — so the shop's own
`C:\ProgramData\Area11\area11.db` is never opened, written or migrated. The window title says
**DEMO SHOP (invented data)**, and the LAN and cloud-sync pages are hidden there.

Inside the demo: sign in (user name empty, password `area11`) → **SYSTEM → Sample shop** → tick the box →
**Fill this shop with the sample shop**.

To remove the demo later: close it, delete `C:\ProgramData\Area11\demo`, delete `DEMO-AREA11.cmd`.
No real bill can be affected, because no real bill was ever in that folder.

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

- The choice now lives on the **Settings → Printing** page: **"Print the receipt automatically after
  each sale"**. The same switch is on the counter screen. Off = manual, with **"Print last bill again"**.
  The value is stored in `printer.autoPrint`, so it survives a restart.
- Paper width comes from `printer.width`: **58** = 32 characters per line, **80** = 48. Default 58.
- The printer name comes from `printer.name`; empty means "the Windows default printer".
- A missing or switched-off printer never loses a bill: the sale is saved first, then the print is
  attempted, and a red message says what happened.

## 5c. Hold a cart, and split a payment (v0.2.5)

- **Park this cart for later** keeps an unfinished bill in the database and frees the counter. The
  parked cart gets a code (`H-0001`) and appears under **Parked carts** with its total. **Bring
  back** puts every line and quantity on the counter exactly as it was; **Throw away** discards it.
  A parked cart is not a sale: no INV number, no money, no stock movement.
- **Payment: split** takes a second amount (online / card / khata). The rest of the bill is cash, and
  the change is calculated from the cash part only. A credit part needs a customer and goes on the
  khata; it is never counted as money in the drawer (D-27).

## 5d. Returns and quarantine (v0.3.0)

1. **Returns** in the left menu → type the bill number (e.g. `INV-0001`) → **Find bill**.
2. Each line shows what was sold, what already came back, what can still come back, and the refund
   for the whole remainder. Type the quantity coming back on the lines that apply (in tablets).
3. Choose the **reason**, write a **note** if you want, and pick where the goods go:
   **back on the shelf now** (default) or **quarantine, decide later**.
4. Press **Take the return**. The app saves `RET-0001`, prints the return receipt (if automatic
   printing is on) and shows the amount that left the drawer and the amount taken off the khata.
5. **Quarantine** tab: every waiting item has three buttons - **Back in stock**, **Damaged /
   expired** (needs a note), **Sold on**. A decision is final and stays in the history.

Why the refund can never be too large: it is capped by what that bill actually collected, and the
khata is reduced before any cash leaves the drawer (U-28, 15.2.1).

## 5e. Alerts (v0.3.1)

- A **red bar** sits above every page while something is wrong: minus stock, or goods waiting in
  quarantine. **See now** opens the Alerts page; **Hide for this session** hides the bar until the
  next sign-in (the alert itself stays).
- **Alerts** page: the list of live problems. Pick one, write a **reason** (3+ characters), press
  **End this alert**. Only the owner sees that box - everybody else is told that the owner must do it.
- Ended alerts stay in the lower list for ever, with reason, name and time. **Bring this alert back**
  restores one if the reason was wrong (also recorded).
- An ended alert **re-opens by itself** when that medicine moves again, so one reason cannot silence
  a repeat problem.

## 5f. No-bill (rush) returns `PR-` (v0.3.2)

- **No bill (PR)** in the left menu. Search the medicine, set the quantity and the unit (box / strip /
  unit), press **Add this item**. Add as many lines as came back.
- Type the **money you paid back**, the customer's name and phone (the phone is how the bill is found
  later), pick a reason, press **Save and give the slip**. The goods are on the shelf immediately and
  the slip carries a permanent `PR-0001` number.
- The entry stays **red - bill MISSING** in the right-hand list, and the red bar on every page counts it.
- When the paper bill turns up: pick the entry, type `INV-0001` (or **Find bills** by phone, name,
  medicine or date) and press **Attach this bill**. **Stock is not added a second time.**
- The alert turns **amber** - *bill attached, owner's confirmation pending* - until the **owner**
  writes a reason in **Alerts** and ends it.
- Typed the entry twice? **Cancel this entry and take the stock back** - a reason is compulsory and
  the stock that was added goes back out of the same batch.

## 5g. Write-off (v0.3.3)

- **Write-off** in the left menu (owner/manager): search the medicine, pick the batch (the oldest
  expiry is chosen for you), type the quantity and the unit, choose the reason - **Expired, Broken /
  dropped, Lost, Count was less, Count was more, Other** - and press **Write this off**.
- The big number under the form is the **loss at purchase cost**, not the selling price. The line
  under it shows the batch quantity before and after.
- If you write off more than the book says, a **red box** appears - *"This will make the stock
  MINUS"* - and the entry still saves (U-30). The red bar on every page remembers it until a stock
  count fixes the number.
- **Count was more** is the opposite entry: it puts the missing stock back and warns you that it adds.
- The right-hand list is the history: batch, quantity, loss, reason and the person. **Nothing can be
  edited or deleted** - a mistake is corrected by a second entry with its own reason.
- The header shows **written off today**, already net of count-found-more entries.

## 5h. Stock-take `ST-` (v0.3.4)

- **Stock-take** in the left menu (owner/manager can count; only the owner can apply).
- **Open ST session** freezes the book as it is at that moment - one row per live batch, so two
  batches of the same medicine are two rows. Leave **Room** empty for the whole shop.
- Walk the shelves: pick a row, type **Counted quantity**, choose the unit, **Save this count**. The
  grid shows **Book / Counted / Diff / Value** and who counted it. **Only rows with a difference**
  gives the second pass.
- **Apply this stock-take** (owner only, once): every counted batch is set to the count, an `adjust`
  movement is written per line, and the shortage/excess is valued **at the cost frozen when the
  session opened**. A batch that had gone minus is corrected to the count.
- Medicine found on the shelf but never received: search it under **Found on the shelf**, add it,
  count it - at Apply it gets a `GINTI-ST-0001` batch.
- A row you never counted **keeps its book stock** and is named in the warning; it is never treated
  as zero. **Cancel this stock-take** needs a reason and changes no stock.

## 6. What comes next (still Phase 1)

Phase 1 is finished; Phase 2 has returns, quarantine, the alert board, no-bill `PR` returns,
write-off and the stock-take. Next inside Phase 2: customers, then the Phase 3 ledgers/reports/backup.

Already written and waiting for one real walk-through by the owner: the receipt with **instant
ESC/POS printing** (58 / 80 mm) and the **Settings** screen (shop, printing, sales rules, staff).

Full list with IDs: [`PROGRESS.md`](PROGRESS.md).
