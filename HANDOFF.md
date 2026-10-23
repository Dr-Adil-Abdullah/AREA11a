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
| What can it do today? | sign-in, products, purchases `PINV`, counter billing `INV` (FEFO, discount guard, round-off, cash change, credit, hold, split), returns `RET` / `Q` / `PR`, write-off, stock-take `ST`, **alerts in one bell** (two kinds: reminders anyone may put away, money/stock problems for the owner only), settings, **customer khata (`PAY-`) with a new-customer form**, supplier ledger and supplier return `SR-`, expiry bands from Settings, customer edit and credit limit, supplier-return cutoff, shift / galla, expenses and owner drawings, **customers + suppliers + udhaar recovery on one page**, and **reports** (today / 7 / 30 / month, per-day, top 10, category split, dead stock, best and worst day, profit at cost-at-sale) and the **black box** (search, module/action filters, date window, CSV export - owner only). The window frame is the one the owner drew (D-51): a **46-pixel bar** with **Area11 small** on the left, the **shop name in the middle** (click -> Settings), the **bell beside it** (click -> the alerts page); the **person sits in the left bar** with their own menu (settings, sign out); and **every menu heading folds**, remembers the fold, and can be pinned open in Settings. **Backup & restore** writes one zip of database + photos, keeps a safety copy before any restore, and can run by itself once a day. **Excel / CSV** imports the old stock list and khata (preview first, one transaction), exports the lists, and changes many prices at once. **Photos** for medicines, customers and suppliers: a picture is copied into `%ProgramData%\Area11\photos`, the database keeps only the name, a wrong one is put away and never deleted, and the camera opens the Windows Camera app (v0.13.0, D-58). **Own fields** (v0.14.0, D-59): the owner adds his own questions - text, number, amount, date, list, yes/no or photo - and they appear on the medicine, customer or supplier form at once, required or not, in his order. **Several barcodes** (v0.15.0, D-60): a medicine carries one code per pack (tablet / strip / box), the medicine card opens the list, and at the counter a scan on **Enter** adds one of the pack that code belongs to. **Doctors & reps** (v0.16.0, D-61): every company keeps the name, phone and visit day of its medical rep; the counter has an optional doctor picker that marks the bill as sold on that doctor's prescription; and the owner sees each doctor's and each customer's lifetime profit - a manager sees a dash, never the number. **Second counter** (v0.17.0, F-45, D-62): a second PC opens the **same `area11.db` over the shop's own share** (`\\MAIN-PC\Area11`) - SYSTEM -> *Second counter (LAN)*, owner only, the main PC turns WAL off when it shares, the change waits for a restart, a second PC **never** invents an empty shop. **Tax, receipts and statements** (v0.18.0, D-63): a **Tax report** page reads the month's bills day by day (sales before tax, tax, total, refunds) and copies the table for the accountant; money received prints a **`PAY-nnnn` slip** with the udhaar before and after *that* receipt; and the customer page prints a **monthly statement** with an opening and a closing balance. **The installer** (v0.19.0, F-47, D-64): `tools\MAKE-INSTALLER.cmd` builds a self-contained copy and a setup folder; one double-click on `INSTALL-AREA11.cmd` asks Windows for permission, puts the program in `C:\Program Files\Area11`, writes the desktop icon (for everyone who signs in) and the Start menu, lists it in *Add or remove programs* with a working uninstaller, starts the print spooler, adds *Print to PDF* when there is no printer, gives `Users` write access to the shop folder, and **never touches `C:\ProgramData\Area11`** - installing again is the update, and the uninstaller leaves every bill. **The printer and scanner test** (v0.20.0, F-46, D-65): SYSTEM -> *Printer & scanner* prints a slip whose ruler fills the line exactly (32 characters on 58 mm, 48 on 80 mm), proves a USB scan through the counter's own barcode lookup, and keeps the owner's six ticks with the date, the person and the paper width - a missing tick needs a line of reason, and changing the paper marks the old ticks stale. **Cloud sync** (v0.21.0, F-48, D-66): SYSTEM -> *Cloud sync* pushes the shop's own data to a Supabase table for a look from home, previews the cloud copy without writing a row, and brings it back only after a backup and one transaction - never deleting a row. **The bills page** (v0.22.0, F-52, D-67): SELL -> *Bills* finds any bill by number, customer name or phone over a date range, opens its lines, payments and returns, and **prints it again with the same builder from the stored rows** - the paper is previewed at 32 / 48 characters first, and every reprint is one line in the black box |
| Has it ever been compiled? | **Yes.** First `Build succeeded` on 20 Oct 2026; the v0.4.6 build of all three projects succeeded, and the owner's first test run compiled `Area11.Core` + `Area11.Tests` on .NET 10.0.13. What is **not** confirmed is the app reaching its sign-in window - if it does not, `C:\ProgramData\Area11\logs\crash-*.txt` says why |
| What is the very next task? | (0) **pull first** - the owner's last run was **v0.30.0, 696 cases, 0 failed** (the first complete green run); v0.30.3 has **659** and the first build of `Area11.Desktop` is still the gate (F-01/F-16). To see the demo without touching his own rows, double-click **`DEMO-AREA11.cmd`** (D-76): it opens a separate shop in `%ProgramData%\Area11\demo` **straight on the Sample-shop page** - tick and fill. (1) run `START-AREA11.bat` and confirm the sign-in window opens in **Citrine `#A98B18`** - v0.29.0 repairs the home screen (profit, expiry by day, profit hidden from staff, held/void bills) and adds "New customer..." at the counter; (2) `tools\RUN-TESTS.cmd` must end **Failed: 0** (**697** cases) - run 18 on the owner's PC gave **553 total, 2 failed** and both were old expectations, repaired here; (3) **the installer**: `tools\MAKE-INSTALLER.cmd`, then `dist\Area11-setup\INSTALL-AREA11.cmd` - it must install, put the icon on the desktop, open in **Citrine `#A98B18`**, and leave `C:\ProgramData\Area11` exactly as it was. Then the printer/scanner test (F-46) on the shop's own paper: SYSTEM -> *Printer & scanner*, print the slip, scan a box, tick and save - that page is new in v0.20.0. |
| How many tests? | **659 cases in 51 files** (v0.30.2 - 37 demo tests went with the demo shop). Run 1: 259 of 270 (305 s). Run 2: 270 of 271. Run 3: 282 of 283 in 26.7 s (a Dapper column alias). Run 4 (**v0.6.1**): **320 of 321 in 27.5 s** - the only failure was a test expecting a lower-cased module; fixed in v0.6.2. Runs five to sixteen (v0.6.2 321, v0.7.0 327, v0.8.0 342, v0.9.0 360, v0.10.0 375, v0.11.0 386, v0.12.0 400, v0.13.0 410, v0.14.0 422, v0.15.0 434, v0.16.0 450, v0.17.0 467) were never reported. **Run 16 (v0.18.0): 479 total, 6 failed, 473 passed in 43.2 s** - and the six were all tests written from an older rule, not program faults; every one is repaired in **v0.19.1** (505 cases, see `docs/VERSIONS.md`). Runs 17+ (v0.19.0 503, v0.19.1 505, v0.20.0 519, v0.21.0 539, v0.22.0 553, v0.22.1 559, v0.23.0 577, v0.24.0 593, v0.25.0 629, v0.26.0/0.26.1 641, v0.27.0 654, v0.28.0 666, v0.29.0 686, v0.29.1 689, v0.29.2 695, v0.29.5 696, v0.29.6 696, **v0.30.0 696**) - and **Run 22 (v0.30.0, the owner's own PC): 696 total, 0 failed, 696 passed in 74.3 s - the first complete green run of the project**; all of the above are waiting on his machine. **Run 21 (v0.29.5, the owner's own PC): 696 total, 2 failed, 694 passed in 69.7 s** - both were my own tests (a page step count, and a column that does not exist in `companies`), repaired in v0.29.6; it was also the first run in which the sample shop itself worked on real SQLite - 1,100+ bills, 1,100+ black-box lines, 73 drawers. **Run 20 (v0.29.0, the owner's own PC): 686 total, 31 failed, 655 passed in 53.0 s** - 28 were the sample shop (fixed in 0.29.1) and 3 were wrong expectations in `DashboardTests` (fixed in 0.29.2: margin discount, gross sales beside net profit, and expiry bands handed over as text). **Run 19 (v0.28.0): 666 total, 28 failed, 638 passed in 55.0 s** - 26 of the 28 were one fault in the sample shop (a UNIQUE `categories.name` clash on a shop that already had its five start-up categories), and the other two were tests that encoded one machine; all 28 are repaired in **v0.29.1** (see `docs/VERSIONS.md`), so **the sample shop had never once worked on real SQLite before that release**. **Run 18 (v0.22.0): 553 total, 2 failed, 551 passed in 57.9 s** - both failures were tests that remembered older words (see the v0.22.1 row of `docs/VERSIONS.md`), not program faults. |
| Did it build? | **The first build of v0.18.0 failed with 9 errors and they were fixed in v0.18.1** (a static member read off an instance, two photo-window members and a test helper overload - none of them a business rule); that build has not been re-run yet. **Yes** up to v0.4.8 (the owner's own runs compiled Core + Tests, and v0.4.6 built all three projects). v0.6.0 (the UI overhaul) stopped on two errors - a missing `using` and a `Total` read off the result envelope - fixed in v0.6.1, whose run compiled Core + Tests and passed 320 of 321; v0.6.2 corrected that last expectation. **v0.6.2 to v0.19.0 have not been built yet** - the static checker is not a compiler, and `tools\RUN-TESTS.cmd` builds Core + Tests only, so the Desktop project is proved by `START-AREA11.bat`. The sign-in window has not been confirmed yet; if it does not appear, `C:\ProgramData\Area11\logs\crash-*.txt` says why |
| Database? | the **original** schema, migrations `001`-`023`, **34 business tables** (+ `_migrations` ledger), so an existing `area11.db` opens unchanged |
| Where does data live? | `%ProgramData%\Area11\` (`area11.db`, `photos\`, `logs\`, `backups\`) - or, on a second counter, the main PC's share named in `%ProgramData%\Area11\shop.ini` (D-62) |
| Default owner login | user **Owner**, password **`area11`** (change it on day one) |
| How does the owner report a fault? | **`tools\SEND-REPORT.cmd`** (v0.30.3, D-78) writes one text file with the build, `dotnet --info`, the git state, the project files, the shop folder (names/sizes/dates - no rows), the whole newest crash report and the last build/test output. `START-AREA11.bat` keeps `build.log` + `build-errors.txt` and prints only the error lines; `tools\RUN-TESTS.cmd` keeps `test.log` and prints only the result, the failed names and the compile errors |

Status in one line: **all four phases are written - 44 of 52 features done and 8 that only the shop PC can prove; 669 test cases, of which the owner's machine has run 696 of 696 on the version before the demo shop was deleted (the 37 that left with it are the only difference) - but the WPF project itself has still never been compiled, so the sign-in window remains the one thing unproven, and the installer (F-47) has never been run.**

---

## 2. Read in this order

| # | File | Why |
|---|---|---|
| 1 | this file | orientation, next tasks, traps |
| 2 | [`AGENTS.md`](AGENTS.md) | the working protocol you must follow |
| 3 | [`docs/PROGRESS.md`](docs/PROGRESS.md) | every feature with ID, state, files, test, "done when" |
| 4 | [`docs/OWNER-DECISIONS.md`](docs/OWNER-DECISIONS.md) | the owner's decisions D-01…D-76 (Urdu, authoritative) |
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
| Schema 001-023 (34 business tables, WAL, migrations under `BEGIN IMMEDIATE`) | `Area11.Core/Data/` |
| Settings (75 keys), audit black box, auth + lockout, permissions, numbering, hold carts | `Area11.Core/Services/` |
| Products, partners, purchases `PINV`, sales `INV`, **returns `RET` + quarantine `Q`**, dashboard | `Area11.Core/Services/` |
| **Photos** (copy in, six per record, put away never delete, camera hint) | `Area11.Core/Services/PhotoService.cs`, `Area11.Desktop/Views/PhotoGalleryWindow.xaml` |
| **Own fields** (the owner's own questions, seven kinds of answer, required, order, never deleted) | `Area11.Core/Services/CustomFieldService.cs`, `Area11.Desktop/Views/CustomFieldsView.xaml`, `CustomFieldsEditorView.xaml` |
| Money / quantity / expiry / password helpers | `Area11.Core/Utils/` |
| WPF shell (+ global alert bar), login, dashboard, products, POS (hold + split), purchases, settings, returns & quarantine, alerts, no-bill returns (PR), write-off, stock-take, customers / khata + statement, **suppliers / payable + `SR` tray** | `Area11.Desktop/` |
| **Second counter over the LAN** (`shop.ini`, journal plan, owner-only join, audit) | `Area11.Core/Utils/ShopConfig.cs`, `Area11.Core/Data/StoragePlan.cs`, `Area11.Core/Services/LanService.cs`, `Area11.Desktop/Views/LanView.xaml` |
| **Tax report + `PAY-` slip + monthly statement** (monthly table, money received, opening/closing balance) | `Area11.Core/Services/TaxService.cs`, `Area11.Core/Services/CustomerLedgerService.cs`, `Area11.Desktop/Views/TaxView.xaml` |
| **Installer** (self-contained build, one double-click, icons, Add/remove entry, spooler + Print-to-PDF + folder permission, database never touched) | `Area11.Core/Services/InstallService.cs`, `Area11.Core/Models/InstallModels.cs`, `tools/INSTALL-AREA11.cmd`, `tools/UNINSTALL-AREA11.cmd`, `tools/MAKE-INSTALLER.cmd`, `tools/install/*.ps1`, `docs/INSTALL.md` |
| **Bills page + reprint** (find any bill, open its lines/payments/returns, print it again from the stored rows, preview at 32/48 characters, one black-box line per reprint) | `Area11.Core/Services/SaleHistoryService.cs`, `Area11.Core/Models/SaleHistoryModels.cs`, `Area11.Desktop/Views/SaleHistoryView.xaml` |
| **Cloud sync** (push, preview, bring with a backup; never deletes a row) | `Area11.Core/Services/SyncService.cs`, `Area11.Core/Services/CloudTransport.cs`, `Area11.Desktop/Views/CloudSyncView.xaml` |
| **Dashboard** (F-12): the home screen's eleven numbers, each equal to the page it summarises - profit by the report's rule, expiry by day, hidden from staff without the profit permission | `Area11.Core/Services/DashboardService.cs`, `Desktop/ViewModels/DashboardViewModel.cs`, `Area11.Tests/DashboardTests.cs` |
| **No demo, ever** (D-77): the sample shop that filled a new database with two and a half months of invented bills was **deleted on the owner's word** in v0.30.2 - `SampleShopService`, its page, `DEMO-AREA11.cmd` and the `--demo` switch are gone, and no flag or button can invent a row | `Area11.Core/Utils/AppPaths.cs` (one data root), `Area11.Core/Utils/BuildInfo.cs` (one title) |
| 668 test cases | `Area11.Tests/` |

**Written but never tested on the shop PC (🟡)**

1. Receipt + instant printing (F-13) - needs one real print
2. Settings screen (F-17) - needs one walk-through by the owner
3. Customer khata (F-27) - needs one real collection at the counter and one printed statement
4. Supplier ledger + `SR` return (F-28) - needs one real payment and one real return to the company

**Not started (⬜)**

1. The rest of Phase 2: expiry levels from settings (next)
3. Phase 3: shifts/galla, expenses, `SR`, reports, audit viewer, backup, Excel, reorder/WhatsApp
6. Phase 4: the **misspelt-medicine search** (F-50) - the second counter (v0.17.0), the installer (v0.19.0), the tax round (v0.18.0), the hardware test (v0.20.0), cloud sync (v0.21.0) and the bills page (v0.22.0) have shipped

---

## 4. Next tasks, in order

Do them one at a time; each one ends with a commit (see `AGENTS.md` §6).

| # | Task | Acceptance |
|---|---|---|
| 1 | **Confirm startup, then re-run** `tools\RUN-TESTS.cmd` | the sign-in window opens in **Burgundy** `#6B1F3A` and the run ends **`Failed: 0`** (686 cases) |
| 2 | **Own fields walk-through** (F-42) | add a required field to the customer form, fill it on a record, try to save it empty (refused, named), put the field away and bring it back (the answer is still there) |
| 3 | **Barcode walk-through** (F-43) | open a medicine, press **Barcodes of this medicine…**, add a strip code and a box code, scan the box code in the counter's search box and press Enter - the cart must show **one box**, not a strip; put a code away and scan it again (not found) |
| 4 | **Doctors & reps walk-through** (F-44) | write a rep and a visit day on a company; make a customer with category **Doctor**; bill something with that doctor chosen; the page must show the bill, its profit and the lifetime profit per customer - and a manager's login must show a dash instead of the profit |
| 5 | **Second counter walk-through** (F-45, D-62) | share the main folder, *Prepare* on the main PC, restart, *Join* from the second PC - the second PC shows the main shop's bills; switch the main PC off and start the second one: it must **refuse**, naming `\\MAIN-PC\Area11\area11.db`, and create nothing |
| 6 | **Tax, receipt and statement walk-through** (F-49, D-63) | enable tax in Settings, make two bills, open **MONEY → Tax report**: the month's tax equals the two receipts; a refund is listed separately; **Copy for the accountant** pastes a clean table. Receive money on a khata: the **PAY-** slip shows the balance before and after. Print a **Month statement** and check the opening + this month's lines = the closing balance |
| 7 | **Installer run** (F-47, D-64) | `tools\MAKE-INSTALLER.cmd` finishes; `dist\Area11-setup\INSTALL-AREA11.cmd` installs and opens Area11 in **Burgundy**; the desktop icon is there; *Add or remove programs* lists Area11 with its version; an uninstall removes the program and **leaves `C:\ProgramData\Area11` exactly as it was**. See [docs/INSTALL.md](docs/INSTALL.md) |
| 8 | **Printer & scanner test on the shop's own paper** (F-46, D-65) | **(v0.22.1: the page now lists every printer Windows knows - pick yours in the box; the code-6 refusal is fixed.)** SYSTEM -> *Printer & scanner*, **Print test slip**: the ruler line fills the paper on 58 mm (32 characters) and on 80 mm (48) - no digit missing, and a shop that changed the roll sees the old ticks marked stale. Scan a box: the page names the medicine without a click. Tick the six boxes, save, and the black box shows one `hardware_test` line with the date, the name and the paper width |
| 9 | **Cloud sync walk-through** (F-48, D-66) | make a Supabase project and run the SQL in `docs/CLOUD-SYNC.md`; fill the four settings; **Push** - Supabase's table editor shows one row for `shop-1`. Press **See what the cloud has**: the page shows the cloud rows beside the rows here and **nothing** changes (the black box must show no `sync_apply`). Tick the box and press **Bring**: one backup appears in `C:\ProgramData\Area11\backups`, one `sync_apply` line appears in the black box, and today's bill is still in the counter |
| 10 | **A fresh, empty shop** (D-77) | there is no demo any more. On a **new** machine: install, sign in as the owner, and the Counter starts with zero medicines and an empty cart - the first thing to type is a supplier and a medicine (or import a CSV from **MONEY → Data**). If you want to *train* someone without touching the shop's books, take a **backup** and restore it onto another computer, or use a second Windows account. The old `C:\ProgramData\Area11\demo` folder, if it exists, is read by nothing and may be deleted |
| 11 | **Bills page walk-through** (F-52, D-67) | SELL -> *Bills*: today's bill is at the top; search it by number, by the customer's name and by phone. Open it: the lines, the money and the khata due match the paper the customer took. Press **Print this bill again** - the same numbers, the same width. Ask a cashier to open the page: no menu entry, and no bill. Then check the black box for one `sale_reprint` line naming the bill and its total |
| 12 | **Misspelt search walk-through** (F-50, D-70) | at the counter type `panadl` - the list shows Panadol and the amber line says "Nothing is spelt \"panadl\"…"; press Enter and it must **not** add anything (Enter is a barcode); press *Add to cart* and it adds. Then type `zink` - the page must say nothing is close, not guess. On Products, `augmnetin` finds Augmentin |
| 13 | The owner's own run of the printer slip (F-46, D-65) | see `docs/PROGRESS.md` and `docs/OWNER-DECISIONS.md` §5 |
| 14 | **"Which bill was it?" walk-through** (F-20, D-73) | on the Returns page open *No bill number? Find it*: search a phone with dashes, then with spaces, then as bare digits - all three must find the same bill; search a medicine name and check two bills land in the list newest first; give a from/to date and check only that day remains. Press *Open this bill* and the bill box fills itself. Then try an empty search (refused) and an upside-down date range (refused) |

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
| D-58 | Photos are **copied** into `photos\` and only the file name is stored (U-19); 12 MB and six per record; a wrong photo is **put away**, never deleted; the camera opens the Windows Camera app |
| D-79 | **The shop checks its own books** (v0.30.4, F-54): SYSTEM → *Check the books* runs **eleven read-only rules** over the database - a bill's lines/header/paid+due/money rows/impossible money, a purchase's lines/header/paid+due/money rows, a khata below zero, and every payment belonging to someone - and names each failing document with what it says beside what its rows add up to. No repair, no recalculation, no write of any kind; the Reports/Black-box permission decides who may open it |
| D-78 | **A fault is reported as one file** (v0.30.3): `tools\SEND-REPORT.cmd` collects the build, the SDK, the branch, the project files, the shop folder (no rows), the whole newest crash report and the last build/test output into one text file; the two launchers print only the lines that decide the run. Read-only throughout - nothing is built, deleted or opened |
| D-77 | **The demo is deleted** (v0.30.2, 23 Oct 2026): the owner said *"delete whatever demo data there is - now we are trying to make an app that will be completely real."* That was the time D-76 reserved for him. Gone: `DEMO-AREA11.cmd`, `--demo`, the **SYSTEM → Sample shop** page and menu entry, `SampleShopService`, `SampleShopModels`, `DemoShop.cs`, the DEMO window title, the receipt watermark, `AppPaths.RealDataRoot`, and 37 tests. **One program, one database, one title** - and nothing in the product can write a row the shop did not sell. `C:\ProgramData\Area11` is still never to be deleted; only the now-orphaned `demo` sub-folder may go |
| D-76 | *(spent in v0.30.2)* **The demo was its own shop** (v0.29.2): `--demo` moved everything to `%ProgramData%\Area11\demo`, hid LAN and cloud sync, and printed *DEMO - NOT A REAL BILL*. That separation is exactly why deleting it was one folder and needed no cleanup of real books - the decision is kept here as history |
| D-59 | **Own fields** (text / number / amount / date / list / yes-no / photo) are made in SYSTEM → Own fields, appear on the form at once, are **put away** rather than deleted, store money in paisa, and stop the whole save when a required answer is empty; the panel has its own Save |
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

`check_repo.py` runs 31 checks (v0.29.0 made the **explicit usings** rule see `Type?` in a signature, stopped it counting nested private classes as namespace types, and gave it a word pre-filter so it runs in 6 s instead of 34; v0.29.3 added the **`{x:Static}`** rule, v0.29.4 the **service method name** rule, v0.29.6 taught the **SQL** rule to read the tests and to stop losing its place after a verbatim or interpolated literal - it now prepares **711** statements instead of 327; v0.29.7 taught the **column** rule to read the tests as well - 871 columns over 101 files; v0.29.8 widened the **constructor** rule to every project and added the **`_services.X`** rule, and made `tools\RUN-TESTS.cmd` print the version it tests; v0.29.9 made the **XAML binding** rule ask the page instead of the whole program - 81 row types resolved from ItemsSource, 1,278 bindings; v0.30.0 added the **page map** rule - 27 `{x:Type}` names, and every shell page's template must hold the view of the same name): C# syntax, **explicit usings** (the CS0103 class of error),
sealed bases (the CS0509 class of error), local shadowing (the CS0136 class of error),
value-type `ServiceResult` use (the CS0037/CS1061 class of error), **constructor arity** (the CS7036/CS1501
class of error - every `new XService(...)` and every `=> new(...)` helper), **every `StaticResource` key and every
`{x:Static}` member in every XAML file** (both XamlParseException classes of error - a misspelt one builds
cleanly and throws when the window is created), XML validity,
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
