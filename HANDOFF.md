# HANDOFF - read this first

> ## اردو میں مختصر
> یہ فائل اِس لیے ہے کہ آپ اِس ریپو کا **لنک کسی بھی ایجنٹ کو دیں** اور وہ سب سمجھ جائے:
> کیا بنا ہے، کیا باقی ہے، کس اصول پر کام ہونا ہے۔ اِس کے ساتھ `AGENTS.md` (کام کرنے کا طریقہ)،
> `docs/PROGRESS.md` (فیچر کی فہرست) اور `docs/RULES-MATRIX.md` (آپ کے 116 نمبر والے اصول) ہیں۔
> آپ کے فیصلے اردو میں `docs/OWNER-DECISIONS.md` میں ہیں۔

**Area11** - a native Windows desktop rebuild of the Area11 pharmacy POS + inventory web app
(`github.com/Dr-Adil-Abdullah/Area11`), in **C# / .NET 10 / WPF / SQLite**, built from
[`docs/AREA11-MASTER-SPEC.md`](docs/AREA11-MASTER-SPEC.md).

The owner (Dr. Adil Abdullah) is not a programmer. He tests each phase himself on his own Windows 10
shop computer. Talk to him in Urdu; keep code, database and UI in English.

---

## 1. The 60-second version

| Question | Answer |
|---|---|
| Where is the product? | **`dotnet/`** - `Area11.sln` with `Area11.Core`, `Area11.Desktop`, `Area11.Tests` |
| What can it do today? | sign-in, products, purchases `PINV`, counter billing `INV` (FEFO, discount guard, round-off, cash change, credit, hold, split), returns `RET` / `Q` / `PR`, write-off, stock-take `ST`, **alerts in one bell** (two kinds: reminders anyone may put away, money/stock problems for the owner only), settings, **customer khata (`PAY-`) with a new-customer form**, supplier ledger and supplier return `SR-`, expiry bands from Settings, customer edit and credit limit, supplier-return cutoff, shift / galla, expenses and owner drawings, **customers + suppliers + udhaar recovery on one page**, and **reports** (today / 7 / 30 / month, per-day, top 10, category split, dead stock, best and worst day, profit at cost-at-sale) and the **black box** (search, module/action filters, date window, CSV export - owner only). The window frame is the one the owner drew (D-51): a **46-pixel bar** with **Area11 small** on the left, the **shop name in the middle** (click -> Settings), the **bell beside it** (click -> the alerts page); the **person sits in the left bar** with their own menu (settings, sign out); and **every menu heading folds**, remembers the fold, and can be pinned open in Settings. **Backup & restore** writes one zip of database + photos, keeps a safety copy before any restore, and can run by itself once a day. **Excel / CSV** imports the old stock list and khata (preview first, one transaction), exports the lists, and changes many prices at once |
| Has it ever been compiled? | **Yes.** First `Build succeeded` on 20 Oct 2026; the v0.4.6 build of all three projects succeeded, and the owner's first test run compiled `Area11.Core` + `Area11.Tests` on .NET 10.0.13. What is **not** confirmed is the app reaching its sign-in window - if it does not, `C:\ProgramData\Area11\logs\crash-*.txt` says why |
| What is the very next task? | (1) does the **sign-in window open** at v0.9.0 (the brand colour is now **Rosewood `#E11D48`**)? (2) does **System -> Excel / CSV** import a small file and export one? (3) `tools\RUN-TESTS.cmd` must end **Failed: 0** (**360** cases). Then **F-38 reorder + WhatsApp**, then Phase 4 (installer, camera, second counter, printer test). |
| How many tests? | **360 cases in 32 files**. Run 1: 259 of 270 (305 s). Run 2: 270 of 271. Run 3: 282 of 283 in 26.7 s (a Dapper column alias). Run 4 (**v0.6.1**): **320 of 321 in 27.5 s** - the only failure was a test expecting a lower-cased module; fixed in v0.6.2. Runs five to eight (v0.6.2 321, v0.7.0 327, v0.8.0 342, v0.9.0 360) pending. |
| Did it build? | **Yes** up to v0.4.8 (the owner's own runs compiled Core + Tests, and v0.4.6 built all three projects). v0.6.0 (the UI overhaul) stopped on two errors - a missing `using` and a `Total` read off the result envelope - fixed in v0.6.1, whose run compiled Core + Tests and passed 320 of 321; v0.6.2 corrected that last expectation. **v0.6.2 to v0.9.0 have not been built yet** - the static checker is not a compiler, and `tools\RUN-TESTS.cmd` builds Core + Tests only, so the Desktop project is proved by `START-AREA11.bat`. The sign-in window has not been confirmed yet; if it does not appear, `C:\ProgramData\Area11\logs\crash-*.txt` says why |
| Database? | the **original** schema, migrations `001`-`019`, **33 business tables** (+ `_migrations` ledger), so an existing `area11.db` opens unchanged |
| Where does data live? | `%ProgramData%\Area11\` (`area11.db`, `photos\`, `logs\`, `backups\`) |
| Default owner login | user **Owner**, password **`area11`** (change it on day one) |

Status in one line: **Phase 1-3 is written and compiles; the first test run is done and its 11 failures are fixed; the shop PC has not yet shown the sign-in window.**

---

## 2. Read in this order

| # | File | Why |
|---|---|---|
| 1 | this file | orientation, next tasks, traps |
| 2 | [`AGENTS.md`](AGENTS.md) | the working protocol you must follow |
| 3 | [`docs/PROGRESS.md`](docs/PROGRESS.md) | every feature with ID, state, files, test, "done when" |
| 4 | [`docs/OWNER-DECISIONS.md`](docs/OWNER-DECISIONS.md) | the owner's decisions D-01…D-23 (Urdu, authoritative) |
| 5 | [`docs/RULES-MATRIX.md`](docs/RULES-MATRIX.md) | the owner's 116 numbered rules → where each one lives |
| 6 | [`docs/AREA11-MASTER-SPEC.md`](docs/AREA11-MASTER-SPEC.md) | the blueprint; sections 3, 5, 11, 12, 14, 15, 34 matter most |
| 7 | [`docs/SCHEMA-REFERENCE.md`](docs/SCHEMA-REFERENCE.md) | column-by-column reference of every table |
| 8 | [`docs/BUILD-ON-WINDOWS.md`](docs/BUILD-ON-WINDOWS.md) | how the owner builds and runs it |

Reference web app (behaviour source, **never modify it**): `git clone https://github.com/Dr-Adil-Abdullah/Area11`
- `src/lib/sales.ts` (FEFO, discount, round-off), `returns.ts`, `settings.ts` (70 keys), `audit.ts`, `db.ts`.
- The master spec wins wherever the two disagree.

---

## 3. What is done, what is not

Full detail in [`docs/PROGRESS.md`](docs/PROGRESS.md). The short version:

**Written**

| Area | Files |
|---|---|
| Schema 001-020 (33 business tables, WAL, migrations under `BEGIN IMMEDIATE`) | `Area11.Core/Data/` |
| Settings (75 keys), audit black box, auth + lockout, permissions, numbering, hold carts | `Area11.Core/Services/` |
| Products, partners, purchases `PINV`, sales `INV`, **returns `RET` + quarantine `Q`**, dashboard | `Area11.Core/Services/` |
| Money / quantity / expiry / password helpers | `Area11.Core/Utils/` |
| WPF shell (+ global alert bar), login, dashboard, products, POS (hold + split), purchases, settings, returns & quarantine, alerts, no-bill returns (PR), write-off, stock-take, customers / khata + statement, **suppliers / payable + `SR` tray** | `Area11.Desktop/` |
| 270 test cases | `Area11.Tests/` |

**Written but never tested on the shop PC (🟡)**

1. Receipt + instant printing (F-13) - needs one real print
2. Settings screen (F-17) - needs one walk-through by the owner
3. Customer khata (F-27) - needs one real collection at the counter and one printed statement
4. Supplier ledger + `SR` return (F-28) - needs one real payment and one real return to the company

**Not started (⬜)**

1. The rest of Phase 2: expiry levels from settings (next)
3. Phase 3: shifts/galla, expenses, `SR`, reports, audit viewer, backup, Excel, reorder/WhatsApp
6. Phase 4: loyalty, photos/camera, custom fields, second counter, hardware test, **installer**

---

## 4. Next tasks, in order

Do them one at a time; each one ends with a commit (see `AGENTS.md` §6).

| # | Task | Acceptance |
|---|---|---|
| 1 | **Confirm startup, then re-run** `tools\RUN-TESTS.cmd` | the sign-in window opens, and the run ends **`Failed: 0`** (the count no longer matters; it grows) |
| 2 | **F-34** reports: today / 7 / 30 days, top items, dead stock, per-day, profit (profit uses cost-at-sale) | the owner can read the day without a calculator |
| 3 | Phase 3: shift/galla + handover, expenses, owner's draw, reports, audit viewer, `SR`, backup ZIP, Excel | see `docs/PROGRESS.md` |
| 4 | Phase 4: §33.2 gaps, loyalty screen, camera, second counter, hardware test, **installer** | see `docs/OWNER-DECISIONS.md` §5 |

---

## 5. Non-negotiables (breaking one of these is a bug, not a style choice)

1. **Money is `long` paisa.** Never `double`, never `decimal` for money. Format only at the edge (`Money`).
2. **Quantities are `double`.** The schema stores `qty_base` as REAL. `decimal` becomes TEXT in
   Microsoft.Data.Sqlite and breaks `SUM()`. Use `Qty.TryParse` / `Qty.Format`.
3. **The bill total must equal the sum of its line totals** - `SaleService` spreads the difference into
   the last allocation (`Spread()`). Never "fix" it by rounding the total afterwards.
4. **Round down only, and never below cost** - `Money.RoundDownWithCostGuard(preRound, costTotal, step)`.
5. **Negative stock never blocks a sale**; it warns and records `negative_sale` (owner rule U-25/U-30).
   It *does* block when the owner has switched `stock.allowNegative` off.
6. **Discount is capped by role** (owner 100 %, manager 20 %, cashier 5 % by default) and the cap comes
   from settings. Below-cost discounts are clamped unless the setting allows them.
7. **Permissions are enforced in `Area11.Core`, never in XAML.** A hidden button is not a permission.
8. **Never delete a row.** Use `active = 0`; audit rows and sale rows are permanent.
9. **Migrations are append-only.** Add `016_*.sql`; never edit `001`-`015`. They run inside
   `BEGIN IMMEDIATE` and re-check the ledger after the lock (fixes the original duplicate-column crash).
10. **No SQL in view-models, no business logic in views** (spec §4.4.2). Services own the rules.
11. **One source of truth for a price**: `SaleService.Compute()` drives both the on-screen quote and
    the saved bill, so what the cashier sees is what is stored.
12. **Every action is audited**: who, when, old → new. Never store PINs or passwords in the audit log.

---

## 6. The owner's decisions, in English

| # | Decision |
|---|---|
| D-01 | Database is the original schema, verbatim, plus migrations 014 (per-person permissions) and 015 (photos) |
| D-02 | A cashier's return puts goods in **quarantine** and raises a permanent alert that stays on every screen until the **owner** dismisses it with a reason |
| D-03 | Permissions are per person, edited in Settings; roles are data, not hard-coded |
| D-04 | Features the original web app never finished are documented and scheduled into Phase 2/3 |
| D-05 / D-23 | Printing is instant: no dialog, straight ESC/POS, 58 and 80 mm, plus reprint |
| D-06 | Camera capture is not in Phase 1 |
| D-07 | Every word on the receipt comes from settings |
| D-08 | Loyalty is the very last feature |
| D-09 | `prototypes/Area11.WPF/` and `prototypes/WPFSample/` are prototypes, kept for reference only |
| D-10 | .NET 10 target; if the shop PC cannot run it, one project-file line switches to .NET 8 |
| D-11 / D-19 | At the very end the app installs itself: desktop icon, Start menu, uninstaller, prerequisites handled |
| D-12 | One counter first; the second counter comes in Phase 4 |
| D-13 | Every feature that carries a business risk is flagged to the owner in plain Urdu |
| D-14 | All information in one place: `docs/OWNER-DECISIONS.md` (Urdu) + this repo (English) |
| D-15 | Four phases, each ending in a build the owner can run and test |
| D-16 | The original web app is never modified |
| D-17 | Three projects: Core / Desktop / Tests |
| D-18 | Windows 10, self-contained (no .NET install needed on the shop PC) |
| D-20 | Phase 1 billing → Phase 2 returns/stock → Phase 3 reports/backup → Phase 4 the rest |
| D-21 | 2-3 photos per product (and supplier/customer photos); files in `data/photos`, DB stores the file name |
| D-22 | Quarantine disposition may be decided by **owner or manager**; dismissing the alert is **owner only, with a reason** |

Language and question rules: reply to the owner in Urdu (or English if he writes English) - **never
Roman Urdu**; ask questions as multiple choice with 2-4 workable options, all questions in one message.

---

## 7. Architecture

```
XAML view (layout only)
      │  state out, commands in
      ▼
ViewModel (INotifyPropertyChanged + RelayCommand)   Area11.Desktop/ViewModels
      ▼
Service (all rules, one transaction per document)   Area11.Core/Services
      ▼
Data (Dapper + Microsoft.Data.Sqlite, migrations)   Area11.Core/Data
      ▼
Models / Utils (paisa, quantity, expiry, hashing)    Area11.Core
```

Folder map:

| Path | Contents |
|---|---|
| `Area11.Core/Data/` | `Database.cs` (snake_case ↔ Pascal mapping, WAL, `busy_timeout=5000`, `Tx`), `MigrationRunner.cs`, `Migrations/*.sql` |
| `Area11.Core/Models/` | `User`, `Product`, `Catalog`, `SaleModels`, `PurchaseModels`, `SettingRow` |
| `Area11.Core/Services/` | `Auth`, `Permission`, `Settings`, `Audit`, `Numbering`, `Product`, `Partner`, `Purchase`, `Sale`, `Dashboard`, `Seed` |
| `Area11.Core/Utils/` | `Money`, `Qty`, `Expiry`, `PasswordHasher`, `ServiceResult`, `LogService`, `AuditDiff`, `AppPaths` |
| `Area11.Desktop/` | `App`, `AppServices` (composition root), `Resources/Theme.xaml`, `ViewModels/`, `Views/` |
| `Area11.Tests/` | `TestDatabase` (in-memory SQLite + migrations + seed), 145 cases in 15 files |
| `tools/` | `check_repo.py`, `build_rules_matrix.py` |
| `docs/` | spec, decisions, progress, rules matrix, schema reference, build guide, port plan |
| `prototypes/` | earlier prototypes (`Area11.WPF`, `WPFSample`) - reference only, do not extend |
| `docs/VERSIONS.md` | every released build, its colour and what changed - **read this to know which build the owner has** (latest: v0.3.4 Cyan) |

Wiring a new screen: service → `AppServices` property → `ShellViewModel.NavItems` (permission-gated)
→ `DataTemplate` in `ShellWindow.xaml` → view + view-model.

---

## 8. Verify before you commit

```bash
python3 tools/check_repo.py             # C#, usings, XAML, bindings, migrations, SQL, tests, docs
python3 tools/build_rules_matrix.py --check
```

`check_repo.py` runs 22 checks: C# syntax, **explicit usings** (the CS0103 class of error),
sealed bases (the CS0509 class of error), local shadowing (the CS0136 class of error),
value-type `ServiceResult` use (the CS0037/CS1061 class of error), **constructor arity** (the CS7036/CS1501
class of error - every `new XService(...)` and every `=> new(...)` helper), **every `StaticResource` key in every
XAML file** (the XamlParseException class of error), XML validity,
**690 XAML bindings and resources** - a row binding is checked against the real properties of
`Area11.Core/Models`, so a mistyped column name fails here instead of printing an empty column -
migrations (19 files -> 33 tables, executed on SQLite), **every SQL statement prepared against the
real schema**, **every column Dapper reads has a property on its model** (the "silently 0" class of
error - see D-48), test count, settings keys, and documentation links.

On Windows (or any machine with the SDK):

```bat
dotnet build Area11.sln
dotnet test Area11.sln
```

`tools/check_repo.py` uses `tree-sitter` when it is installed (`pip install tree-sitter tree-sitter-c-sharp`);
without it, it falls back to a brace/paren balance check and says so.

---

## 9. Traps that already cost time - do not repeat them

| Trap | What happens | Do instead |
|---|---|---|
| `decimal` for a quantity or price | Microsoft.Data.Sqlite writes TEXT; `SUM()` and comparisons break | `double` for quantities, `long` for paisa |
| `init`-only property on a draft line | cannot mutate after construction (`GrossPaisa` had to become `{ get; set; }`) | plain setters on view-model/draft DTOs |
| `git reset --hard` with uncommitted work | silently destroys it | commit first; `git stash` if unsure |
| rebuilding `Total` from rounded lines | total ≠ sum of lines | round once, then `Spread()` |
| trusting `batches.qty_base` after a sale | FEFO may split one line over 3 batches | always read the batch rows inside the transaction |
| `cost_paisa_at_sale` | it is **per base unit**, not per line | line profit = `line_total_paisa - qty_base * cost_paisa_at_sale` |
| editing `001`-`015` migrations | existing databases never receive the change | add `016_*.sql` |
| `INSERT OR REPLACE` on settings | loses `updated_at` and the audit trail | `INSERT ... ON CONFLICT(key) DO UPDATE` |
| assuming the sandbox can build WPF | it is Linux, no SDK | check statically, let the owner build |
| relying on `ImplicitUsings` for `System.IO` | CS0103 (`Path`/`Directory` did not exist) | write the `using` explicitly; `check_repo.py` enforces it |
| raw `Math.Round(value, MidpointRounding)` | CS0121 ambiguity when the argument is `int`/`long` (a unit multiplier!) | always `Calc.Round(double)` or `Calc.RoundMoney(decimal)`; the checker rejects raw `Math.Round` |
| leaving an old `.sln` around | Visual Studio reports MSB4025 "The project file could not be loaded" and then CS0103 for everything | one solution at the root (`Area11.sln`); `check_repo.py` verifies every `.sln` reference exists; `tools/doctor.ps1` finds and repairs it |
| building the wrong solution | the prototype `Area11.WPF.sln` sat in the root and produced errors in old files | one solution only: root `Area11.sln`; prototypes live in `prototypes/` |
| adding a 4th project "for clarity" | owner decided three (D-17) | keep three |
| deriving from a `sealed` model | **CS0509** - stopped the first Phase-2 build at `ReturnService.cs(504)` (`ItemRow : ReturnLineOption`) | leave the model unsealed (it is a transport type) or compose instead of inherit; `check_repo.py` now refuses any class deriving from a sealed one |
| a silent exit before the login window | a WinExe prints nothing; the console showed only "Area11 stopped with an error" and no exception | **fixed in v0.4.6**: `Program.Main` wraps the WPF startup, every crash writes `logs\crash-*.txt`, `START-AREA11.bat` prints it, `tools\SHOW-LOG.cmd` shows it on demand |
| trusting the sandbox checker for arity | it could not see CS7036 (wrong number of arguments) until v0.4.3; that is how `AlertTests` kept the old 4-argument `AlertService` and stopped the owner's build | **fixed**: `check_repo.py` now compares every `new XService(...)` and `=> new(...)` helper with its declaration; test helpers go through `TestData` factories, never a copy-pasted `new` |
| reusing a local name in a nested block | **CS0136** - `SaleService.Plan` used `method` inside the payment loop and again 40 lines later; stopped the second Phase-2 build | give nested locals a specific name (`rowMethod`); `check_repo.py` now walks every member and refuses equal or nested declarations (CS0136/CS0128) |
| GitHub unreachable (`Failed to connect ... port 443`) | the update never starts; the code and the repository are fine | run `tools\CHECK-NETWORK.cmd` (DNS / port 443 / proxy), then `docs/GITHUB-NETWORK.md`; the branch ZIP + `START-AREA11.bat` always works without git |
| an interrupted pull leaves a lock in `.git` | GitHub Desktop stops with **"update_ref failed for ref 'ORIG_HEAD'"** (or "Unable to create ...index.lock") and never fetches | close Visual Studio / VS Code / GitHub Desktop and run `tools\FIX-UPDATE.cmd` - it deletes only `*.lock` inside `.git` and shows what else is in the way |
| treating `ServiceResult<int>.Value` as nullable | **CS0037 / CS1061** - `T?` on an unconstrained parameter is an annotation only; with `T = int` the property is `int` | check `Ok` and the number (`> 0`), never `is null`; `check_repo.py` follows `var x = _services.Svc.Method(...)` per member and refuses `x.Value is null` / `x.Value.Value` for value-type results |

---

## 10. Definition of done (owner's U-14 / spec §30.4.1)

A task is finished only when **all** of these are true:

1. code is written and `python3 tools/check_repo.py` is clean;
2. a test covers the money/data rule it touches (or a note in `PROGRESS.md` says why it cannot);
3. `docs/PROGRESS.md` rows and `tools/build_rules_matrix.py` statuses are updated;
4. this file's "Next tasks" section still points at the real next task;
5. committed with a message that names the feature ID (`F-13: receipt + instant ESC/POS print`);
6. pushed to the session branch.

**Never leave the repo with documentation that describes code which does not exist, or code that no
document mentions.**
