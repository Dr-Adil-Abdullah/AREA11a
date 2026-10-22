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
| F-03 | Settings service, 77 keys, every change audited, saved in one transaction | ✅ | `Area11.Core/Services/SettingsService.cs` | `SettingsTests` 🧪 | bad values are refused before they reach the database |
| F-04 | Audit black box (old -> new values, never deleted) | 🟡 | `Services/AuditService.cs`, `Utils/AuditDiff.cs` | `AuditDiffTests` 🧪 | every module writes (U-27) |
| F-05 | Sign-in: owner password, staff PIN, 5 tries -> 2 minute lock | 🟡 | `Services/AuthService.cs`, `Utils/PasswordHasher.cs` | `PasswordHasherTests` 🧪 | owner signs in on Windows |
| F-06 | Per-person permissions stored in the database | ✅ | `Migr. 014`, `Services/PermissionService.cs`, `Views/SettingsView.xaml` | `PermissionTests` 🧪 | the owner ticks/unticks in Settings; every change is audited |
| F-07 | Permanent numbering `INV` / `PINV` / `RET` / `Q` / `SR` / `PR` / `ST` | ✅ | `Services/NumberingService.cs` | `MigrationTests` 🧪 | no duplicate numbers under one transaction |
| F-08 | Products: search, create, edit, box/strip/unit packing, prices, rack | 🟡 | `Services/ProductService.cs`, `Views/ProductsView.xaml` | - | owner adds a medicine with a batch |
| F-09 | Purchases `PINV`: supplier, batch, expiry, sample lines, payable | 🟡 | `Services/PurchaseService.cs`, `Views/PurchasesView.xaml` | - | one real delivery enters stock |
| F-10 | Counter `INV`: FEFO, box/strip/unit, discount guard, round-off, cash change, credit | 🟡 | `Services/SaleService.cs`, `Views/PosView.xaml` | `SaleTests` 🧪 | one real bill prints |
| F-11 | Customers: quick create at the counter, credit limit | ✅ | `Services/PartnerService.cs` + Edit panel on the khata page | v0.3.11 | a credit bill lands on the customer's account; the owner corrects a name, phone, category and limit without touching the udhaar |
| F-12 | Dashboard: today's sales, low stock, expiry, stock value | 🟡 | `Services/DashboardService.cs`, `Views/DashboardView.xaml` | - | numbers match a manual count |
| F-13 | **Receipt render + instant ESC/POS print, 58 / 80 mm, auto or manual (D-26)** | 🟡 | `Services/ReceiptService.cs`, `Services/EscPosBuilder.cs`, `Services/PrinterService.cs`, `Views/PosView.xaml` | `ReceiptTests` 🧪 | needs one real print on the shop printer |
| F-14 | Hold cart / bring back a held cart (`H-0001`), stored in the database | ✅ | `Migr. 016`, `Services/HoldCartService.cs`, `Views/PosView.xaml` | `HoldCartTests` 🧪 | a parked cart survives a restart and comes back line for line |
| F-15 | Split payment (cash + online / card / khata in one bill) | ✅ | `Services/SaleService.cs` (Plan), `Views/PosView.xaml` | `SplitPaymentTests` 🧪 | the credit part reaches the khata and never the drawer |
| F-16 | **First real Windows build and test run** | 🟡 | `docs/BUILD-ON-WINDOWS.md` | - | 20 Oct 2026 v0.1.1 `Build succeeded`; **21 Oct 2026 Phase-2 builds: CS0509 `ReturnService.cs(504)` (v0.3.5), CS0136 `SaleService.cs(609)` (v0.3.6), CS0037/CS1061 `ReturnsViewModel.cs(152-167)` (v0.3.7)** - all three now prevented by checker rules (sealed bases, shadowing, value-type results). Core + Tests compile; Desktop is the last unknown; `dotnet test` never run |
| F-17 | Settings screen: shop info, printing, discount rules, and per-person permission ticks | ✅ | v0.2.4 | `SettingsView` + 270 tests | owner edits everything without a developer |
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
| F-28 | Supplier ledger + supplier return `SR-nnnn` (credit note or cash) | ✅ | `Services/SupplierLedgerService.cs`, `Views/SuppliersView.xaml` | `SupplierLedgerTests` 🧪 | payable always equals opening + purchases - payments - credit notes ("books match") |
| F-29 | Expiry levels read from settings (E-07) | ✅ | `Utils/Expiry.cs` + `Models/ExpiryLevels.cs` + 3 boxes on the Settings tab | v0.3.10 | change the threshold, the colour changes |
| F-30 | Return receipt (permanent number, original bill, reason) | ✅ | `Services/ReturnReceiptService.cs` | `ReturnTests` 🧪 | the paper says stock or quarantine for every line |

## Phase 3 - ledgers, reports, backup

| ID | Feature | State | Where | Test | Done when |
|---|---|---|---|---|---|
| F-31 | Shift / galla: open, close, variance (handover still to come with a second counter) | ✅ | `Services/ShiftService.cs`, `Views/ShiftView.xaml` | `ShiftTests` 🧪 | expected cash = float + sales - payouts; the page says exact / over / short · v0.4.0 |
| F-32 | Expenses and owner drawings | ✅ | `Services/ExpenseService.cs`, `Views/ExpensesView.xaml` | `ExpenseTests` 🧪 | they reduce the drawer, not the profit · v0.4.4 |
| F-33 | Udhaar recovery: ageing (0-30 / 31-60 / 61-90 / 90+), oldest first, `PAY-nnnn` recovery slip | ✅ | `Services/RecoveryService.cs`, `Views/RecoveryView.xaml` | `RecoveryTests` 🧪 | the four columns add up to the khata, and a payment knocks the oldest bill off first · v0.4.5 |
| F-34 | Reports: today / 7 / 30 days / this month, KPI cards, per-day chart, top 10 by quantity and by profit, category split, dead stock, best and worst day | ✅ | `Services/ReportService.cs`, `Views/ReportsView.xaml` | `ReportTests` 🧪 (12) | profit uses cost-at-sale and the return rule of Spec 15.6 · v0.4.9 |
| F-35 | Audit viewer + CSV export (owner; manager hidden by default) | ✅ | `Services/AuditHistoryService.cs`, `Views/AuditView.xaml` | `AuditViewerTests` 🧪 (10) | search + module/action filters + date window, no edit or delete, CSV is exactly what is on screen · v0.5.0 |
| UI | **Owner's screen overhaul (D-50)**: 176 px sidebar grouped in five headings, one bell with a count badge, red bar removed, two alert kinds (anyone closes a reminder; only the owner ends a problem), low-stock and expired alerts, customers + suppliers + recovery as one page, and the **new customer** form that never existed | ✅ | `Views/ShellWindow.xaml`, `ViewModels/PartnersViewModel.cs`, `Views/CustomersView.xaml` | `AlertKindTests` 🧪 (13), `CustomerCreateTests` 🧪 (8) | a reminder is one click for anybody; a money problem is owner-only; one people page · v0.6.0 |
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
| F-51 | Supplier-return cutoff window (§9.3.6) - a batch 3-6 months from expiry is a return reminder with a deadline | ✅ | `Services/AlertService.cs` + two Settings boxes | `SupplierCutoffTests` 🧪 | the board says the last day the supplier accepts the goods |

---

## Known bugs / debts

| # | Problem | Where | Fix |
|---|---|---|---|
| 1 | ~~`Expiry.StatusLabel` takes level thresholds as parameters but the switch hard-codes 90 / 180 / 365 days~~ | `Area11.Core/Utils/Expiry.cs` | **fixed v0.3.10** - the bands are read from `expiry.levels` (F-29) |
| 2 | `decimal` must never be used for quantities - Microsoft.Data.Sqlite stores it as TEXT and `SUM` breaks | everywhere | quantities are `double`; money is `long` paisa |
| 3 | Receipt / printing does not exist yet, so a bill cannot be handed to the customer | - | F-13 |
| 4 | First build: CS0103 in three prototype files. Root cause was the failed project load (a stale `Area11.WPF.sln`); explicit `using System.IO;` added everywhere as a safeguard | `prototypes/`, all Core files | fixed 20 Oct 2026 |
| 5 | CS0121 ambiguous `Math.Round` in `SaleService.cs` line 337 - the unit multiplier was an `int`, so `long * int` stayed `long` | `Area11.Core/Utils/Calc.cs` | fixed 20 Oct 2026 - all rounding via `Calc.Round` / `Calc.RoundMoney`; raw `Math.Round` is rejected by the checker |

## Verification

| Check | How | State |
|---|---|---|
| C# parses (143 files) | `python3 tools/check_repo.py` | ✅ |
| Every file imports the namespaces it uses (`System.IO` … **and every type this repo declares**) | `python3 tools/check_repo.py` | ✅ |
| Every SQL statement prepares against the real schema (168) | `python3 tools/check_repo.py` | ✅ |
| XAML / csproj / props are valid XML | `python3 tools/check_repo.py` | ✅ |
| Every XAML binding matches its view-model | `python3 tools/check_repo.py` | ✅ |
| Every `StaticResource` key exists, in **every** XAML file | `python3 tools/check_repo.py` | ✅ |
| Every column a query maps has a property on its model | `python3 tools/check_repo.py` | ✅ (22nd check) |
| No member of a model is read off its `ServiceResult` envelope (CS1061) | `python3 tools/check_repo.py` | ✅ new in v0.6.1 |
| Every `new XService(...)` matches its constructor (CS7036/CS1501) | `python3 tools/check_repo.py` | ✅ |
| Migrations 001-019 produce 33 business tables | `python3 tools/check_repo.py` (SQLite) | ✅ |
| Rules matrix up to date | `python3 tools/build_rules_matrix.py --check` | ✅ |
| **Compiles on Windows** | `dotnet build Area11.sln` | ✅ 20 Oct 2026 (v0.1.1); re-check after each release |
| Version, colour and `docs/VERSIONS.md` agree | `python3 tools/check_repo.py` | ✅ v0.6.2 Magenta |
| **321 tests green** | `dotnet test` (or `tools\RUN-TESTS.cmd`) | 🟡 **fourth run (v0.6.1): 320 of 321 in 27.5 s** - the only failure was a test that expected the black box to lower-case a module name; corrected in v0.6.2 - **fifth run pending** |

## Change log

| Date | What |
|---|---|
| 22 Oct 2026 | **v0.6.2 Magenta - 320 of 321, and the failure was the test.** The first real run of the new screen (v0.6.1) passed 320 cases in 27.5 s; the one failure, `AuditViewerTests.EveryLine_CarriesWhoWhatWhenAndTheChange`, expected the module as `settings` while the row (and the drop-downs in the same file) say `Settings`. A black box that renames a word is not a black box, and the module filter matches the stored text exactly - so the expectation was corrected, not the page. No Core or Desktop code changed. 321 tests. |
| 22 Oct 2026 | **v0.6.1 Chestnut - the first build of the new screen, and its two errors.** `AuditViewModel` used `AuditHistoryService` without `using Area11.Core.Services;` (CS0103) and `AuditViewerTests` read `page.Total` off the `ServiceResult<AuditPage>` instead of its `Value` (CS1061). Both are now **checker rules**: every type this repository declares is mapped to its namespace and a mention without the import fails with a line number; and a `var` that holds a `ServiceResult` may not be read as if it were the model inside. Proved by breaking the code on purpose. Also fixed: a supplier-only account opened the merged people page on a hidden tab (blank screen), plus two build warnings. 321 tests. |
| 22 Oct 2026 | **v0.6.0 Peacock - the screen the owner asked for (D-50).** The sidebar is 176 px and one line per page under five headings; the red strip across every page is gone. A **bell with a count badge** now sits opposite the logo: **red** when the owner must act, **amber** for housekeeping. It opens a panel split into **Needs the owner** and **Reminders**, and "Full board" opens the old page for the reason and the history. **Two kinds of alert, and the line is drawn in the service:** a reminder (open drawer from an earlier day, a bill waiting for a confirmation) can be put away by **anybody with one click**; `DismissLight` **refuses every other key**, so a cashier can never silence minus stock. Two new serious sources: **shelves at or below their reorder level** (all folded into one line, lowest first) and **batches already expired**, valued at cost. The shop name opens **Settings -> Shop**, the person's name opens **Settings -> Staff**. **Customers, suppliers and udhaar recovery are one menu entry** with three tabs. And the honest answer: adding a customer had **never been built** - the service existed and no screen called it. It is built now, with a phone number that may belong to only one khata. 28 new tests. 321 tests. |
| 20 Oct 2026 | **First Windows build report:** CS0103 (`Path`/`Directory`/`File`) in `AppConfig.cs`, `Database.cs`, `LoggerService.cs` - fixed with explicit `using System.IO;`. The old `Area11.WPF.sln` was the solution being built by mistake, so it moved to `prototypes/`; the real solution is now `Area11.sln` in the root. The checker gained an explicit-usings check and a SQL-preparation check (37 statements). |
| 21 Oct 2026 | **v0.3.4 Cyan - stock-take (F-27).** ST session freezes the book; count sheet with book/counted/diff/value and an only-differences pass; Apply (owner, once) corrects batches and values the difference at the frozen cost; negative stock corrected; GINTI batch for found medicine; uncounted lines warned, never zeroed. 173 tests. |
| 21 Oct 2026 | **v0.4.6 Graphite - the program explains its own failure.** The first clean build of all three projects started and closed before the login window: a WinExe prints nothing, so "Area11 stopped with an error" was the only evidence. Now a custom `Main` wraps the WPF startup, every crash writes `logs\crash-<stamp>.txt` with the stack trace, the batch prints that file by itself, and `tools\SHOW-LOG.cmd` shows it on demand. The migration runner searches for its SQL files and names every folder tried; the Desktop project copies `Migrations` itself. 21st checker rule: every `StaticResource` key in every XAML; the batch runs the built exe directly. 270 tests. |
| 21 Oct 2026 | **v0.4.5 Orchid - old udhaar recovery (F-33).** Every customer who owes money, sorted by the oldest debt, with a real FIFO ageing in four columns. A payment or a khata-reducing return knocks the oldest bill off first, so the "90+" column is money that has truly been owed three months. The columns must add up to the stored khata or the row turns red. Receiving money prints a **recovery slip** with the permanent `PAY-nnnn` number, built from history so a reprint never changes. The roadmap said `SR`; Spec 5.1.12 reserves `SR-` for supplier returns, so the slip uses `PAY-` (D-43). 270 tests. |
| 21 Oct 2026 | **v0.4.4 Rust - expenses and owner drawings (F-32).** New **Expenses** page: shop costs (rent, electricity, salary, transport, tea, packing, repair, other) and what the owner took. An expense is not a purchase (no stock, no supplier account); a **cash** drawing lowers the drawer, a **goods** drawing lowers the shelf at **cost** and leaves the cash alone. The shift/galla now shows both as their own lines, so an honest expense no longer looks like a short drawer. Cash permission for expenses, owner only for drawings, nothing edited or deleted. 248 tests. |
| 21 Oct 2026 | **v0.4.3 Maroon - build fix (Tests).** CS7036 in `AlertTests` (a test helper still built `AlertService` with four arguments) and CS0117 in `ShiftTests` (`QtyBase` on a supplier return line; the real property is `QtyEntered` + `Unit`). All three test helpers now go through `TestData.Alerts`, and a **20th checker rule** compares every service constructor call - including `=> new(...)` helpers - with its declaration, so the next change of this kind fails in the checker, not on the shop PC. 236 tests. |
| 21 Oct 2026 | **v0.4.2 Violet - GitHub unreachable.** The owner reported **"Failed to connect to github.com port 443"**: a network fault, not the app. `tools\CHECK-NETWORK.cmd` reports DNS, port 443, the web path, `netsh winhttp` and git proxies in plain words; `docs/GITHUB-NETWORK.md` separates it from the `.git` lock fault and gives the browser-ZIP route. 236 tests. |
| 21 Oct 2026 | **v0.4.1 Blue - repair tool for a stuck update.** `tools\FIX-UPDATE.cmd` removes the leftover `*.lock` files inside `.git` that make GitHub Desktop say **"update_ref failed for ref 'ORIG_HEAD'"**, clears the stale `ORIG_HEAD` bookmark and prints the branch, the last commit and every locally changed file. Only `.git` is touched; the shop data stays untouched. 236 tests. |
| 21 Oct 2026 | **v0.4.0 Pink - shift / galla (F-31).** Open the drawer with a float; the expected cash is rebuilt from cash bills, khata collections, supplier cash, refunds and supplier payments; closing says exact / over / short and is refused twice; a peer cannot close somebody else's shift; the `cash` permission guards the page; every closed shift stays in the history. 236 tests. |
| 21 Oct 2026 | **v0.3.12 Cocoa - supplier-return cutoff (§9.3.6).** Batches from 180 days down to the 90-day cutoff appear on the alert board as **Return to supplier** (amber, not red: nothing is wrong yet), with quantity, expiry and the **last day the supplier will accept it**; the two numbers are settings and a reminder that starts after the cutoff is refused; the owner ends an alert with a reason as before; the top bar counts them. 225 tests. |
| 21 Oct 2026 | **v0.3.11 Wine - correct a customer and his limit (F-11).** Edit panel on the khata page: name, phone, category, credit limit; the udhaar is never editable and the page still says **Books match**; a limit that really changes needs the owner or a manager and a refusal changes nothing; a limit below the udhaar or a shared phone is saved **with a warning**; 0 means no limit; old → new in the black box. 218 tests. |
| 21 Oct 2026 | **v0.3.10 Ink - expiry levels from settings (F-29).** The three bands used to be three numbers inside `Expiry.StatusLabel`; it now honours the numbers it is given, and every screen hands it the owner's `expiry.levels`. Three boxes on the Settings tab (default 365 / 180 / 90), a guard that refuses two equal bands, a repair path for broken JSON, and a dashboard window that follows Level 3. 210 tests. |
| 21 Oct 2026 | **v0.3.9 Tangerine - supplier ledger + `SR` (F-28).** Supplier list and page: payable rebuilt from opening + purchases - payments - credit notes, with every movement and a running balance; **Books match** in green or the difference in red. Pay a supplier (over-payment refused, purchase-linked money counted once). Return goods `SR-nnnn` with a credit note, or cash from the supplier; stock leaves the batch, negative allowed with a warning (U-30); expired batches go back with one click; statement on 58/80 mm. 202 tests. |
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
| 21 Oct 2026 | **v0.4.7 Aubergine - the first test run, and all 11 failures answered.** The owner ran `tools\RUN-TESTS.cmd`: **259 of 270 passed in 305 s**. Three were real faults. (a) The recovery slip could never find its own payment: the outer `SELECT` of the `UNION` left out `kind`, so `Slip` answered "That collection is not in the customer's history" - and one test died with a `NullReferenceException` instead. (b) A bill with no `payments` row printed no "Change" line, so an imported bill showed the customer paying more than the total. (c) `Database.Open` asked SQLite to switch to WAL **before** it set `busy_timeout`, so one momentary lock (antivirus, a second copy) failed the whole query; the wait is set first, the WAL switch is best-effort, and a failed customer list now names the exception instead of hiding it. The other eight were the tests' own mistakes and were corrected, not the code: a hard-coded "15 migrations" (there are 19 - the test now reads the folder), a round-down test using a Rs 10 step but expecting a Rs 1 answer, `audit_log` instead of `audit_logs`, a credit-limit test run as the **owner** (Spec 13.4.2 always allows him) with `credit.blockOverLimit` left off, a running balance of 40,000 where the arithmetic says 50,000, and a shift expecting 300,000 from a comment that described a different day (the drawer holds 200,000). **Speed:** the 19 migrations ran once per test (305 s); `TestDatabase` now migrates one schema per run, copies it per test, and deletes the copies. `tools\RUN-TESTS.cmd` prints no test count any more - only "Failed: 0" - and builds the tests alone. 271 tests. |
| 21 Oct 2026 | **v0.4.8 Denim - 270 of 271, and a fast run.** The one remaining failure (`TheMeaningOfOneEntry_IsStillReadableOnTheList`) hard-coded an order that depends on all three saves landing in the same second; on this machine each save took ~2 s, so the drawings legitimately came first. The test now asserts the query's rule and finds rows by kind, so it cannot depend on the machine again. **Why the run was still 288 s:** `Database.Open` asked for `journal_mode=WAL` on every connection, and every close deleted the WAL file - one test created and destroyed it dozens of times. WAL is now asked for once per run (the app benefits too), and test databases run with the journal in memory and no sync (`durable: false`), which keeps the schema, the SQL and the transactions identical while removing the disk waits. 271 tests. |
| 21 Oct 2026 | **v0.4.9 Iris - reports (F-34).** A new **Reports** page: today / last 7 / last 30 / this month, KPI cards (bills, items, sales, discount, average bill, cash, khata, refunds, expenses, drawings, purchases), a **per-day bar chart** with every day of the period (quiet days included), **top 10 by quantity and by profit**, the **category split**, **dead stock** (stock on the shelf with no sale for 60 days, valued at cost) and the **best and worst day ever**. Profit follows Spec 21.6 - (sales - refunds) - (cost - cost of the goods that came back to the shelf) - and the same rule is applied to the per-day sheet, the top lists and the category split, so the item profits add up to the total. A returned strip in **quarantine** stays a loss: only its refund is taken off. The profit is the **owner's** (Spec 12.1.2): a manager sees every other number, a cashier is refused. No CSV yet - that is F-35. 283 tests. |
| 21 Oct 2026 | **v0.4.10 Mulberry - the column with no home.** The third run took **26.7 s** (305 s on the first) and passed **282 of 283**. The failure was not arithmetic: the report named its profit column `profit` while the row class called it `ProfitPaisa`, and **Dapper maps by name, dropping anything it cannot map** - so the item showed zero profit with no error anywhere. Fixed, the test strengthened (both lists must agree on money and quantity, and the item profits must add up to the total), and a **22nd checker rule** now fails the build when any column of a mapped `SELECT` has no property on its model - including queries written as a `const string`, which is exactly where this one hid. 283 tests. |
| 21 Oct 2026 | **v0.5.0 Ultramarine - the black box opens (F-35).** A read-only page over `audit_logs`: search across action, person, entity and the old/new value; module and action drop-downs; today / 7 / 30 / all time; 200-10,000 lines; every row shows who, when, on what and the change as an arrow. No edit and no delete exist anywhere - not in the page, not in the service (Spec 22.1). **Save CSV** exports exactly the filtered rows in the same order, RFC 4180 quoted, UTF-8 **with BOM** so Excel reads Urdu correctly. The `audit` permission is **owner only by default** and the owner may grant it to one person. A manager sees nothing, and the service refuses him; a cashier too. 10 new tests. 293 tests. |
