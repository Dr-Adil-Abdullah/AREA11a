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
| What can it do today? | sign-in, products, purchases `PINV`, counter billing `INV` (FEFO, discount guard, round-off, cash change, credit), dashboard |
| Has it ever been compiled? | **Yes - 20 Oct 2026.** The owner's Windows build reported `Build succeeded`. Since then the code grew (receipt/printing), so the current build is unverified again |
| What is the very next task? | **build + `tools\RUN-TESTS.cmd` (68 cases)** on the owner's PC - the test project has never compiled yet; then the settings screen **F-17** |
| How many tests? | 68 cases in 8 files - written, **never executed**; run `tools\RUN-TESTS.cmd` |
| Database? | the **original** schema, migrations `001`-`015`, **31 business tables** (+ `_migrations` ledger), so an existing `area11.db` opens unchanged |
| Where does data live? | `%ProgramData%\Area11\` (`area11.db`, `photos\`, `logs\`, `backups\`) |
| Default owner login | user **Owner**, password **`area11`** (change it on day one) |

Status in one line: **Phase 1 is ~85 % written, 0 % compiled.** Everything else is planned.

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

**Written (🟡, never compiled)**

| Area | Files |
|---|---|
| Schema 001-015 (31 business tables, WAL, migrations under `BEGIN IMMEDIATE`) | `Area11.Core/Data/` |
| Settings (71 keys), audit black box, auth + lockout, permissions, numbering | `Area11.Core/Services/` |
| Products, partners (customers/suppliers), purchases `PINV`, sales `INV`, dashboard | `Area11.Core/Services/` |
| Money / quantity / expiry / password helpers | `Area11.Core/Utils/` |
| WPF shell, login, dashboard, products, POS, purchases | `Area11.Desktop/` |
| 68 test cases | `Area11.Tests/` |

**Not started (⬜)**

1. Receipt + instant printing (F-13) ← **next**
2. Hold cart, split payment (F-14, F-15)
3. Settings screen with per-person permission ticks (F-17)
4. Whole of Phase 2: returns `RET`, provisional `PR`, quarantine `Q`, alert centre, write-off, stock-take `ST`, ledgers, expiry levels from settings
5. Phase 3: shifts/galla, expenses, `SR`, reports, audit viewer, backup, Excel, reorder/WhatsApp
6. Phase 4: loyalty, photos/camera, custom fields, second counter, hardware test, **installer**

---

## 4. Next tasks, in order

Do them one at a time; each one ends with a commit (see `AGENTS.md` §6).

| # | Task | Acceptance |
|---|---|---|
| 1 | **F-16 follow-up** run `tools\RUN-TESTS.cmd` on the owner's PC | all 68 tests pass |
| 2 | **F-13** receipt renderer + ESC/POS instant print (58/80 mm) | pressing "print" prints with no dialog; reprint from a saved bill; printer absent ⇒ bill still saves |
| 3 | **F-17** Settings screen (store info, discount, tax, printer width) + per-person permission ticks | the owner changes a setting and the next bill obeys it |
| 4 | **F-14 / F-15** hold cart, split payment | two held carts survive a restart; one bill with two payment rows |
| 5 | **F-29** expiry thresholds from settings (fixes Known bug #1) | changing `expiry.level3Days` moves the colour band |
| 6 | Phase 2 (F-20…F-30), in that order | see `docs/PROGRESS.md` |

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
| `Area11.Tests/` | `TestDatabase` (in-memory SQLite + migrations + seed), 68 cases in 8 files |
| `tools/` | `check_repo.py`, `build_rules_matrix.py` |
| `docs/` | spec, decisions, progress, rules matrix, schema reference, build guide, port plan |
| `prototypes/` | earlier prototypes (`Area11.WPF`, `WPFSample`) - reference only, do not extend |
| `docs/VERSIONS.md` | every released build, its colour and what changed - **read this to know which build the owner has** (latest: v0.2.3 Rose) |

Wiring a new screen: service → `AppServices` property → `ShellViewModel.NavItems` (permission-gated)
→ `DataTemplate` in `ShellWindow.xaml` → view + view-model.

---

## 8. Verify before you commit

```bash
python3 tools/check_repo.py             # C#, usings, XAML, bindings, migrations, SQL, tests, docs
python3 tools/build_rules_matrix.py --check
```

`check_repo.py` runs 10 checks: C# syntax, **explicit usings** (the CS0103 class of error),
XML validity, 137 XAML bindings and resources, migrations (15 files -> 31 tables, executed on
SQLite), **every SQL statement prepared against the real schema**, test count, settings keys,
and documentation links.

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
