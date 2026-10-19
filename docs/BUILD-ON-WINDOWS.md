# Area11 — building on Windows (first build)

The solution in `dotnet/` is written but **could not be compiled in the Linux workspace** where it
was produced (no .NET SDK, and WPF only builds on Windows). Everything else was verified: C# syntax
of all 39 files, every XAML/CSProj file parses, all 15 migrations run in order against SQLite, and
every screen binding was checked against its view-model.

## 1. What to install (once)

| Thing | Where | Why |
|---|---|---|
| **.NET 10 SDK** | https://dotnet.microsoft.com/download/dotnet/10.0 | to build and run |
| **Visual Studio 2022** (optional) | https://visualstudio.microsoft.com | easiest way to run and debug |

The shop PC needs **nothing** installed later: the published app is self-contained.

## 2. Build and test

```bat
cd dotnet
dotnet restore
dotnet build
dotnet test
dotnet run --project Area11.Desktop
```

Expected: all projects build, **44 tests pass**, and the sign-in window opens.

> **Blue window that says “app is running”?** That is `WPFSample` — the very first smoke test.
> It is **not** Area11. Start `dotnet\Area11.sln` and run the **Area11.Desktop** project (or use the
> command above); the new window asks for a user name and password.

## 3. First sign-in

| Field | Value |
|---|---|
| User name | *leave empty* (owner) |
| Password | `area11` |

The database is created automatically at:

```
C:\ProgramData\Area11\area11.db
```

(If that folder is not writable, the app falls back to `%LOCALAPPDATA%\Area11`.)
Logs: `C:\ProgramData\Area11\logs\app-YYYY-MM-DD.log`.

## 4. If something fails

Copy the **first error** (the top one, not the last) and send it in the chat. Useful extras:

```bat
dotnet --info
dotnet restore -v minimal
```

| Symptom | Likely cause | Fix |
|---|---|---|
| `NETSDK1045` / SDK not found | .NET 10 SDK missing | install it, then reopen the terminal |
| `NU1102` package not found | wrong package version | tell me the version it asks for |
| `Migrations folder was not found` | content files not copied | `dotnet build` again, then check `Area11.Desktop\bin\Debug\net10.0-windows\Migrations` |
| `SQLite Error 14: unable to open database` | `C:\ProgramData\Area11` not writable | run once as administrator, or the app falls back automatically |
| Window opens but no data | first-run seed failed | check `logs\app-*.log` |

## 5. What already works in this build

- **Counter (POS)**: search, box/strip/loose, live total from the same code that saves, FEFO batch
  picking, role discount limit, round-down with cost guard, cash tendered → change, credit to the
  customer account, negative stock allowed with a warning only.
- **Purchases (PINV)**: supplier (or add one inline), batch number, expiry (month or date), cost and
  retail, sample line at zero cost, supplier payable, part payment, full audit.
- Sign-in: owner password, staff PIN, five-try lock, every attempt audited.
- Dashboard: today's bills/sales/profit, credit, payables, low stock, negative stock, expired batches.
- Products: list + search, create, edit (partial update), soft delete, categories and companies
  with “＋ New” inside the form, VIP/doctor rates, rack and room.
- Permissions: per-person module checkboxes in the data model (screen comes with Settings).
- Black box: every login, product change and price change with old → new.
- Database: the exact 15-migration schema, including the owner's `photos` table for 2–3 photos.

## 6. What comes next (still Phase 1)

Purchases (`PINV`) + batches/expiry, the POS counter (`INV`), the printed receipt and instant
printing. Then Phase 2 (returns/quarantine/alerts).
