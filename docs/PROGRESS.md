# PROGRESS - the feature board (what is done, what is next)

**This is the file to read first if you are taking over.** It lists every feature of the rebuild
with an ID, its state, the files that implement it, the test that proves it, and what "done" means.
[`RULES-MATRIX.md`](RULES-MATRIX.md) covers the owner's 116 numbered rules; this file covers the
features and the four phases.

- Owner's decisions (Urdu, authoritative): [`OWNER-DECISIONS.md`](OWNER-DECISIONS.md)
- Blueprint (English, authoritative): [`AREA11-MASTER-SPEC.md`](AREA11-MASTER-SPEC.md)
- How to build and run on Windows: [`BUILD-ON-WINDOWS.md`](BUILD-ON-WINDOWS.md)

## Legend

| Badge | Meaning |
|---|---|
| ✅ | working | 
| 🟡 | code written but **not yet compiled on Windows**, or only partly finished |
| ⬜ | not started |
| 🧪 | test written (tests have never been executed - see "Verification" below) |

> **Verification status, read this before trusting a ✅.**
> The build machine used so far has no .NET SDK (Linux sandbox). Everything in `dotnet/` has been
> checked statically (C# parses, XAML/bindings/resources valid, migrations run against SQLite) but
> **has never been compiled**. F-17 below is therefore the gate for the whole of Phase 1.
> After the first successful `dotnet build` on Windows, change the 🟡 rows that compile cleanly to ✅.

---

## Phase 1 - foundation and billing (`INV` / `PINV` / receipt / instant print)

| ID | Feature | State | Where | Test | Done when |
|---|---|---|---|---|---|
| F-01 | Solution with three projects (Core / Desktop / Tests), .NET 10, MVVM | 🟡 | `Area11.sln`, `dotnet/Directory.Build.props` | - | compiles on Windows |
| F-02 | Exact database schema, migrations 001-019, 33 business tables | ✅ | `dotnet/Area11.Core/Data/Migrations/` | `MigrationTests` 🧪 | an old `area11.db` opens unchanged |
| F-03 | Settings service, 75 keys, every change audited, saved in one transaction | ✅ | `Area11.Core/Services/SettingsService.cs` | `SettingsTests` 🧪 | bad values are refused before they reach the database |
| F-04 | Audit black box (old -> new values, never deleted) | 🟡 | `Services/AuditService.cs`, `Utils/AuditDiff.cs` | `AuditDiffTests` 🧪 | every module writes (U-27) |
| F-05 | Sign-in: owner password, staff PIN, 5 tries -> 2 minute lock | 🟡 | `Services/AuthService.cs`, `Utils/PasswordHasher.cs` | `PasswordHasherTests` 🧪 | owner signs in on Windows |
| F-06 | Per-person permissions stored in the database | ✅ | `Migr. 014`, `Services/PermissionService.cs`, `Views/SettingsView.xaml` | `PermissionTests` 🧪 | the owner ticks/unticks in Settings; every change is audited |
| F-07 | Permanent numbering `INV` / `PINV` / `RET` / `Q` / `SR` / `PR` / `ST` | ✅ | `Services/NumberingService.cs` | `MigrationTests` 🧪 | no duplicate numbers under one transaction |
| F-08 | Products: search, create, edit, box/strip/unit packing, prices, rack | 🟡 | `Services/ProductService.cs`, `Views/ProductsView.xaml` | - | owner adds a medicine with a batch |
| F-09 | Purchases `PINV`: supplier, batch, expiry, sample lines, payable | 🟡 | `Services/PurchaseService.cs`, `Views/PurchasesView.xaml` | - | one real delivery enters stock |
| F-10 | Counter `INV`: FEFO, box/strip/unit, discount guard, round-off, cash change, credit | 🟡 | `Services/SaleService.cs`, `Views/PosView.xaml` | `SaleTests` 🧪 | one real bill prints |
| F-11 | Customers: quick create at the counter, credit limit | 🟡 | `Services/PartnerService.cs` | - | a credit bill lands on the customer's account (done); editing a customer and his limit from the khata page still to come |
| F-12 | Dashboard: today's sales, low stock, expiry, stock value | 🟡 | `Services/DashboardService.cs`, `Views/DashboardView.xaml` | - | numbers match a manual count |
| F-13 | **Receipt render + instant ESC/POS print, 58 / 80 mm, auto or manual (D-26)** | 🟡 | `Services/ReceiptService.cs`, `Services/EscPosBuilder.cs`, `Services/PrinterService.cs`, `Views/PosView.xaml` | `ReceiptTests` 🧪 | needs one real print on the shop printer |
| F-14 | Hold cart / bring back a held cart (`H-0001`), stored in the database | ✅ | `Migr. 016`, `Services/HoldCartService.cs`, `Views/PosView.xaml` | `HoldCartTests` 🧪 | a parked cart survives a restart and comes back line for line |
| F-15 | Split payment (cash + online / card / khata in one bill) | ✅ | `Services/SaleService.cs` (Plan), `Views/PosView.xaml` | `SplitPaymentTests` 🧪 | the credit part reaches the khata and never the drawer |
| F-16 | **First real Windows build and test run** | 🟡 | `docs/BUILD-ON-WINDOWS.md` | - | 20 Oct 2026 v0.1.1 `Build succeeded`; **21 Oct 2026 Phase-2 builds: CS0509 `ReturnService.cs(504)` (v0.3.5), CS0136 `SaleService.cs(609)` (v0.3.6), CS0037/CS1061 `ReturnsViewModel.cs(152-167)` (v0.3.7)** - all three now prevented by checker rules (sealed bases, shadowing, value-type results). Core + Tests compile; Desktop is the last unknown; `dotnet test` never run |
| F-17 | Settings screen: shop info, printing, discount rules, and per-person permission ticks | ✅ | v0.2.4 | `SettingsView` + 188 tests | owner edits everything without a developer |
| F-18 | Tests for purchases, partners, numbering | ⬜ | `Area11.Tests/` | - | added with F-13 |

## Phase 2 - returns, stock and alerts

| ID | Feature | State | Where | Test | Done when |
|---|---|---|---|---|---|
| F-20 | Bill lookup for a return (by printed number) | 🟡 | `Services/ReturnService.FindSale` | `ReturnTests` 🧪 | search by phone / medicine / date (F-20 full) still to come |
| F-21 | Return `RET` against the original bill, strict limits, **printed return receipt** | ✅ | `Migr. 017`, `Services/ReturnService.cs`, `Services/ReturnReceiptService.cs`, `Views/ReturnsView.xaml` | `ReturnTests` 🧪 | cannot refund more than was sold or collected (U-28, U-35) |
| F-23 | Quarantine `Q` with permanent number + disposition by owner **or manager** (D-22) | ✅ | `Services/QuarantineService.cs`, `Views/ReturnsView.xaml` | `QuarantineTests` 🧪 | goods never restock without a decision; history kept forever |
| F-22 | Provisional (no-bill) return `PR-nnnn`: refund at once, stock on the shelf, bill attached later, cancel with reason | ✅ | `Services/ProvisionalReturnService.cs`, `Services/ProvisionalReceiptService.cs`, `Views/ProvisionalView.xaml` | `ProvisionalTests` 🧪 | a refund without a bill can never be checked - the number is permanent and the badge does not stop (U-33) |
| F-25 | Stock write-off / breakage: expired, broken, lost, count short/extra, **valued at cost**, never blocks (U-30) | ✅ | `Services/StockService.cs`, `Views/WriteOffView.xaml` | `StockTests` 🧪 | removing more than the book says must warn, never refuse (U-30) |
| F-26 | Stock-take `ST-nnnn`: frozen book, count sheet (book/counted/diff/value), apply once (owner), frozen cost, GINTI batch for found stock | ✅ | `Services/StockTakeService.cs`, `Views/StockTakeView.xaml` | `StockTakeTests` 🧪 | the count is evidence; the book must follow it, not the other way round (19.2) |
| F-27 | Customer khata: list + page, money received (`PAY-`), running balance, statement, stars | ✅ | `Services/CustomerLedgerService.cs`, `Views/CustomersView.xaml` | `CustomerLedgerTests` 🧪 | balance always equals the sum of movements ("books match"), and money received is refused above the udhaar |
| F-28 | Supplier ledger + supplier return (credit note) | ⬜ | - | - | payable always equals purchases - payments |
| F-29 | Expiry levels read from settings (E-07) | ⬜ | `Utils/Expiry.cs` | - | change the threshold, the colour changes |
| F-30 | Return receipt (permanent number, original bill, reason) | ✅ | `Services/ReturnReceiptService.cs` | `ReturnTests` 🧪 | the paper says stock or quarantine for every line |

## Phase 3 - ledgers, reports, backup

| ID | Feature | State | Where | Test | Done when |
|---|---|---|---|---|---|
| F-31 | Shift / galla: open, close, handover, variance | ⬜ | - | - | expected cash = float + sales - payouts |
| F-32 | Expenses and owner drawings | ⬜ | - | - | they reduce the drawer, not the profit |
| F-33 | `SR` recovery bills (old udhaar paid today) | ⬜ | - | - | - |
| F-34 | Reports: today / 7 / 30 days, top items, dead stock, per-day, profit | ⬜ | - | - | profit uses cost-at-sale |
| F-35 | Audit viewer + CSV export (owner; manager hidden by default) | ⬜ | - | - | - |
| F-36 | Backup: ZIP of database + photos, daily automatic, restore | ⬜ | - | - | restore on a clean PC |
| F-37 | Excel import / export / bulk price update | ⬜ | - | - | 1 000 rows in one go |
| F-38 | Reorder levels + WhatsApp order text | ⬜ | - | - | - |
| F-39 | Stock-minus registry (monthly review) | ⬜ | - | - | - |

## Phase 4 - everything else

| ID | Feature | State | Where | Test | Done when |
|---|---|---|---|---|---|
| F-40 | Loyalty: X profit -> Y points -> Z stars -> title | ⬜ | - | - | switched off by default |
| F-41 | Photos: 2-3 per product, supplier and customer, camera capture (D-21) | ⬜ | `Migr. 015` `photos` table | - | files in `data/photos`, DB holds names |
| F-42 | Custom fields (text / number / amount / photo) | ⬜ | - | - | appear in the form automatically |
| F-43 | Several barcodes per product (per pack size) | ⬜ | - | - | - |
| F-44 | Medical rep / salesman info, doctor tracking, lifetime profit per customer | ⬜ | - | - | - |
| F-45 | Second counter over the LAN | ⬜ | - | - | two PCs, one database |
| F-46 | Hardware test at the shop: 58 / 80 mm printer, USB scanner | ⬜ | - | - | owner's own printer works |
| F-47 | **Self-installing installer** (D-19): desktop icon, Start menu, uninstall | ⬜ | - | - | run it, it installs itself |
| F-48 | Supabase sync (push / pull) | ⬜ | - | - | never overwrites local data silently |
| F-49 | Tax report, payment receipt, customer monthly statement | ⬜ | - | - | - |
| F-50 | Fuzzy search (a misspelt medicine is still found) | ⬜ | - | - | - |

---

## Known bugs / debts

| # | Problem | Where | Fix |
|---|---|---|---|
| 1 | `Expiry.StatusLabel` takes level thresholds as parameters but the switch hard-codes 90 / 180 / 365 days | `Area11.Core/Utils/Expiry.cs` | read `expiry.level*` from settings (F-29) |
| 2 | `decimal` must never be used for quantities - Microsoft.Data.Sqlite stores it as TEXT and `SUM` breaks | everywhere | quantities are `double`; money is `long` paisa |
| 3 | Receipt / printing does not exist yet, so a bill cannot be handed to the customer | - | F-13 |
| 4 | First build: CS0103 in three prototype files. Root cause was the failed project load (a stale `Area11.WPF.sln`); explicit `using System.IO;` added everywhere as a safeguard | `prototypes/`, all Core files | fixed 20 Oct 2026 |
| 5 | CS0121 ambiguous `Math.Round` in `SaleService.cs` line 337 - the unit multiplier was an `int`, so `long * int` stayed `long` | `Area11.Core/Utils/Calc.cs` | fixed 20 Oct 2026 - all rounding via `Calc.Round` / `Calc.RoundMoney`; raw `Math.Round` is rejected by the checker |

## Verification

| Check | How | State |
|---|---|---|
| C# parses (95 files) | `python3 tools/check_repo.py` | ✅ |
| Every file imports the namespaces it uses (`System.IO` …) | `python3 tools/check_repo.py` | ✅ |
| Every SQL statement prepares against the real schema (102) | `python3 tools/check_repo.py` | ✅ |
| XAML / csproj / props are valid XML | `python3 tools/check_repo.py` | ✅ |
| Every XAML binding matches its view-model | `python3 tools/check_repo.py` | ✅ |
| Migrations 001-019 produce 33 business tables | `python3 tools/check_repo.py` (SQLite) | ✅ |
| Rules matrix up to date | `python3 tools/build_rules_matrix.py --check` | ✅ |
| **Compiles on Windows** | `dotnet build Area11.sln` | ✅ 20 Oct 2026 (v0.1.1); re-check after each release |
| Version, colour and `docs/VERSIONS.md` agree | `python3 tools/check_repo.py` | ✅ v0.3.4 Cyan |
| **188 tests green** | `dotnet test` | ⬜ **F-16 follow-up** (the tests have never run on the owner's PC) |

## Change log

| Date | What |
|---|---|
| 20 Oct 2026 | **First Windows build report:** CS0103 (`Path`/`Directory`/`File`) in `AppConfig.cs`, `Database.cs`, `LoggerService.cs` - fixed with explicit `using System.IO;`. The old `Area11.WPF.sln` was the solution being built by mistake, so it moved to `prototypes/`; the real solution is now `Area11.sln` in the root. The checker gained an explicit-usings check and a SQL-preparation check (37 statements). |
| 21 Oct 2026 | **v0.3.4 Cyan - stock-take (F-27).** ST session freezes the book; count sheet with book/counted/diff/value and an only-differences pass; Apply (owner, once) corrects batches and values the difference at the frozen cost; negative stock corrected; GINTI batch for found medicine; uncounted lines warned, never zeroed. 173 tests. |
| 21 Oct 2026 | **v0.3.8 Teal - customer khata (F-27).** Customer list (search, only-with-udhaar) and a page per customer: udhaar, limit, stars, bills, lifetime, profit, returns; every movement with a running balance; money received as `PAY-nnnn` (over-payment refused, no advance); statement on 58/80 mm; loyalty points wired but off by default. **Books match** is the promise: the balance is rebuilt from history and the difference shown in red. 188 tests. |
| 21 Oct 2026 | **v0.3.3 Navy - write-off (F-26).** Expired/broken/lost/count corrections valued at purchase cost, stock ledger + own history, black box; over-writing allowed with a big red MINUS warning; count-found-more adds stock back. 157 tests. |
| 21 Oct 2026 | **v0.3.2 Plum - no-bill returns (F-25).** `PR-nnnn` with stock on the shelf at once, red badge until the bill is attached, amber afterwards; attaching never adds stock twice; cancel needs a reason and reverses the stock; slip says the bill is missing; smart lookup by medicine/phone/name/date. 145 tests. |
| 21 Oct 2026 | **v0.3.1 Bronze - alert board (F-24).** Red bar on every page; Alerts page; owner-only dismissal with a 3+ character reason kept for ever; restore; an ended alert re-opens when the medicine moves again. 131 tests. |
| 21 Oct 2026 | **v0.3.0 Olive - Phase 2 begins.** Returns: find the bill by number, type what came back, refund capped by what that bill actually collected, khata reduced before cash, goods to the same batch or to quarantine with a `Q-` number, three quarantine decisions (stock / damaged / sold) and a printed `RET-` receipt. 131 tests. |
| 20 Oct 2026 | **v0.2.5 Fuchsia**: **hold cart** (F-14) and **split payment** (F-15). A parked cart lives in the database (`H-0001`), so it survives a restart, and comes back line for line. A split bill writes one `payments` row per method that received money; the credit part goes to `due_paisa` and the customer's balance, never to the drawer. Receipt prints each part. 131 tests. |
| 20 Oct 2026 | **v0.2.4 Slate**: the Settings screen (F-17). Printing is chosen there (58/80 mm, automatic or manual), the shop name/address/phone/footer are editable, discount limits and tax are editable, and staff get one checkbox per module. Settings are saved as one transaction and refused if a value would break the shop (131 tests). |
| 20 Oct 2026 | **v0.2.3 Rose**: no more ZIP-per-change. `START-AREA11.bat` prints the git branch it is building and refuses to continue on `main` (that is the trap when cloning with GitHub Desktop); the Urdu guide gained a "which tool" table and GitHub Desktop steps. |
| 20 Oct 2026 | **v0.2.2 Blue - build fix.** The receipt tests named an internal type (`ReceiptService.SaleRow`), which the compiler rejects (`CS0122`); they now write a real bill into the test database and read it back through the public `ReceiptService.Build`. New checker rule: no test may name an internal type. |
| 20 Oct 2026 | **v0.2.1 Indigo**: printing became a *setting* - automatic after a sale or manual with the button (`printer.autoPrint`, switchable on the POS screen until the Settings screen exists). Both paper widths stay selectable. |
| 20 Oct 2026 | **v0.2.0 Purple**: receipt on paper (32 / 48 characters), instant ESC/POS printing with cut and copies, reprint button, `printer.*` settings, version badge on the sign-in window and sidebar, 12 new receipt tests. The colour of every build is now recorded in `docs/VERSIONS.md` and enforced by the checker. |
| 20 Oct 2026 | **First successful Windows build.** `Area11.sln` compiled in 5.3 s (Core, Desktop, Tests). Six NU1903 security warnings for `SQLitePCLRaw.lib.e_sqlite3` 2.1.11 → pinned 2.1.12. Brand colour changed to green, then to **pink** (`#DB2777`) at the owner's request - all colour keys live in `Resources/Theme.xaml`. Added `tools/RUN-TESTS.cmd` and `docs/GIT-AND-VS-URDU.md`. |
| 20 Oct 2026 | Added `HANDOFF.md`, `AGENTS.md`, this board, the rules matrix, the spec copy and `tools/check_repo.py` (owner's U-13). |
| 19 Oct 2026 | Phase 1 code: schema, Core services, login, dashboard, products, purchases, POS, 44 tests. |
| 19 Oct 2026 | Owner decisions D-17…D-23 (three projects, Windows 10, installer, four phases, photos, quarantine, printing). |
