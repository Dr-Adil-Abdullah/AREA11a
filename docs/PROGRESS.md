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
| F-01 | Solution with three projects (Core / Desktop / Tests), .NET 10, MVVM | 🟡 | `dotnet/Area11.sln`, `dotnet/Directory.Build.props` | - | compiles on Windows |
| F-02 | Exact database schema, migrations 001-015, 31 business tables | ✅ | `dotnet/Area11.Core/Data/Migrations/` | `MigrationTests` 🧪 | an old `area11.db` opens unchanged |
| F-03 | Settings service, 71 keys, every change audited | 🟡 | `Area11.Core/Services/SettingsService.cs` | - | Settings screen (F-18) reads/writes it |
| F-04 | Audit black box (old -> new values, never deleted) | 🟡 | `Services/AuditService.cs`, `Utils/AuditDiff.cs` | `AuditDiffTests` 🧪 | every module writes (U-27) |
| F-05 | Sign-in: owner password, staff PIN, 5 tries -> 2 minute lock | 🟡 | `Services/AuthService.cs`, `Utils/PasswordHasher.cs` | `PasswordHasherTests` 🧪 | owner signs in on Windows |
| F-06 | Per-person permissions stored in the database | 🟡 | `Migr. 014`, `Services/PermissionService.cs` | `PermissionTests` 🧪 | Settings screen can tick/untick (F-18) |
| F-07 | Permanent numbering `INV` / `PINV` / `RET` / `Q` / `SR` / `PR` / `ST` | ✅ | `Services/NumberingService.cs` | `MigrationTests` 🧪 | no duplicate numbers under one transaction |
| F-08 | Products: search, create, edit, box/strip/unit packing, prices, rack | 🟡 | `Services/ProductService.cs`, `Views/ProductsView.xaml` | - | owner adds a medicine with a batch |
| F-09 | Purchases `PINV`: supplier, batch, expiry, sample lines, payable | 🟡 | `Services/PurchaseService.cs`, `Views/PurchasesView.xaml` | - | one real delivery enters stock |
| F-10 | Counter `INV`: FEFO, box/strip/unit, discount guard, round-off, cash change, credit | 🟡 | `Services/SaleService.cs`, `Views/PosView.xaml` | `SaleTests` 🧪 | one real bill prints |
| F-11 | Customers: quick create at the counter, credit limit | 🟡 | `Services/PartnerService.cs` | - | a credit bill lands on the customer's account |
| F-12 | Dashboard: today's sales, low stock, expiry, stock value | 🟡 | `Services/DashboardService.cs`, `Views/DashboardView.xaml` | - | numbers match a manual count |
| F-13 | **Receipt render + instant ESC/POS print, 58 / 80 mm, no dialog** | ⬜ | - | - | pressing print prints (D-05, D-23) |
| F-14 | Hold cart / bring back a held cart | ⬜ | - | - | two customers can wait at once |
| F-15 | Split payment (cash + card/online in one bill) | ⬜ | - | - | - |
| F-16 | **First real Windows build and test run** | ⬜ | `docs/BUILD-ON-WINDOWS.md` | - | 0 errors, 0 warnings, 45/45 tests |
| F-17 | Settings screen: shop info, discount rules, and per-person permission ticks | ⬜ | - | - | owner edits everything without a developer |
| F-18 | Tests for purchases, partners, numbering | ⬜ | `Area11.Tests/` | - | added with F-13 |

## Phase 2 - returns, stock and alerts

| ID | Feature | State | Where | Test | Done when |
|---|---|---|---|---|---|
| F-20 | Bill search / lost-bill lookup | ⬜ | - | - | find a bill by number, phone or item |
| F-21 | Return `RET` against the original bill, strict limits | ⬜ | - | - | cannot refund more than was sold (U-28) |
| F-22 | Provisional return `PR` (customer has no bill yet) | ⬜ | - | - | links to a bill later |
| F-23 | Quarantine `Q` with permanent number + disposition by owner **or manager** (D-22) | ⬜ | - | - | goods never restock without a decision |
| F-24 | Alert centre: negative stock, rush return; dismiss **owner only, with a reason** | ⬜ | - | - | alert returns until dismissed (U-31) |
| F-25 | Stock write-off / breakage | ⬜ | - | - | audited, stock corrected |
| F-26 | Stock-take `ST` session: open, count, apply | ⬜ | - | - | differences become movements |
| F-27 | Customer ledger: payments, statement, stars | ⬜ | - | - | balance always equals the sum of movements |
| F-28 | Supplier ledger + supplier return (credit note) | ⬜ | - | - | payable always equals purchases - payments |
| F-29 | Expiry levels read from settings (E-07) | ⬜ | `Utils/Expiry.cs` | - | change the threshold, the colour changes |
| F-30 | Return receipt (permanent number, original bill, reason) | ⬜ | - | - | - |

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

## Verification

| Check | How | State |
|---|---|---|
| C# parses (51 files) | `python3 tools/check_repo.py` | ✅ |
| XAML / csproj / props are valid XML | `python3 tools/check_repo.py` | ✅ |
| Every XAML binding matches its view-model | `python3 tools/check_repo.py` | ✅ |
| Migrations 001-015 produce 31 business tables | `python3 tools/check_repo.py` (SQLite) | ✅ |
| Rules matrix up to date | `python3 tools/build_rules_matrix.py --check` | ✅ |
| **Compiles on Windows** | `dotnet build` | ⬜ **F-16** |
| **45 tests green** | `dotnet test` | ⬜ **F-16** |

## Change log

| Date | What |
|---|---|
| 20 Oct 2026 | Added `HANDOFF.md`, `AGENTS.md`, this board, the rules matrix, the spec copy and `tools/check_repo.py` (owner's U-13). |
| 19 Oct 2026 | Phase 1 code: schema, Core services, login, dashboard, products, purchases, POS, 44 tests. |
| 19 Oct 2026 | Owner decisions D-17…D-23 (three projects, Windows 10, installer, four phases, photos, quarantine, printing). |
