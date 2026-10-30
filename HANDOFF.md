# HANDOFF - read this first

> **New agent: read [`TAKEOVER.md`](TAKEOVER.md) first** - it is the whole handover on one page
> (state, what is left, non-negotiables, release steps, traps). This file is the long manual behind it.
>
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
| What can it do today? | sign-in, products, purchases `PINV`, counter billing `INV` (FEFO, discount guard, round-off, cash change, credit, hold, split), returns `RET` / `Q` / `PR`, write-off, stock-take `ST`, **alerts in one bell** (two kinds: reminders anyone may put away, money/stock problems for the owner only), settings, **customer khata (`PAY-`) with a new-customer form**, supplier ledger and supplier return `SR-`, expiry bands from Settings, customer edit and credit limit, supplier-return cutoff, shift / galla, expenses and owner drawings, **customers + suppliers + udhaar recovery on one page**, and **reports** (today / 7 / 30 / month, per-day, top 10, category split, dead stock, best and worst day, profit at cost-at-sale) and the **black box** (search, module/action filters, date window, selected-row earlier-state reader, CSV export - owner only). The current Phase-1 frame (D-96) is an expanded dark header with fixed **Area11**, permission-filtered modules and contextual pages, a **44×44 alert bell** at the top-right, and the person/version in popup menus; the left sidebar and folding controls are retired. Settings sections and the Products category picker live in the contextual header, while existing routes, permissions and alert behavior remain. **Backup & restore** writes one zip of database + photos, keeps a safety copy before any restore, and can run by itself once a day. **Excel / CSV** imports the old stock list and khata (preview first, one transaction), exports the lists, and changes many prices at once. **Photos** for medicines, customers and suppliers: a picture is copied into `%ProgramData%\Area11\photos`, the database keeps only the name, a wrong one is put away and never deleted, and the camera opens the Windows Camera app (v0.13.0, D-58). **Own fields** (v0.14.0, D-59): the owner adds his own questions - text, number, amount, date, list, yes/no or photo - and they appear on the medicine, customer or supplier form at once, required or not, in his order. **Several barcodes** (v0.15.0, D-60): a medicine carries one code per pack (tablet / strip / box), the medicine card opens the list, and at the counter a scan on **Enter** adds one of the pack that code belongs to. **Doctors & reps** (v0.16.0, D-61): every company keeps the name, phone and visit day of its medical rep; the counter has an optional doctor picker that marks the bill as sold on that doctor's prescription; and the owner sees each doctor's and each customer's lifetime profit - a manager sees a dash, never the number. **Second counter** (v0.17.0, F-45, D-62): a second PC opens the **same `area11.db` over the shop's own share** (`\\MAIN-PC\Area11`) - SYSTEM -> *Second counter (LAN)*, owner only, the main PC turns WAL off when it shares, the change waits for a restart, a second PC **never** invents an empty shop. **Tax, receipts and statements** (v0.18.0, D-63): a **Tax report** page reads the month's bills day by day (sales before tax, tax, total, refunds) and copies the table for the accountant; money received prints a **`PAY-nnnn` slip** with the udhaar before and after *that* receipt; and the customer page prints a **monthly statement** with an opening and a closing balance. **The installer** (v0.19.0, F-47, D-64): `tools\MAKE-INSTALLER.cmd` builds a self-contained copy and a setup folder; one double-click on `INSTALL-AREA11.cmd` asks Windows for permission, puts the program in `C:\Program Files\Area11`, writes the desktop icon (for everyone who signs in) and the Start menu, lists it in *Add or remove programs* with a working uninstaller, starts the print spooler, adds *Print to PDF* when there is no printer, gives `Users` write access to the shop folder, and **never touches `C:\ProgramData\Area11`** - installing again is the update, and the uninstaller leaves every bill. **The printer and scanner test** (v0.20.0, F-46, D-65): SYSTEM -> *Printer & scanner* prints a slip whose ruler fills the line exactly (32 characters on 58 mm, 48 on 80 mm), proves a USB scan through the counter's own barcode lookup, and keeps the owner's six ticks with the date, the person and the paper width - a missing tick needs a line of reason, and changing the paper marks the old ticks stale. **Cloud sync** (v0.21.0, F-48, D-66): SYSTEM -> *Cloud sync* pushes the shop's own data to a Supabase table for a look from home, previews the cloud copy without writing a row, and brings it back only after a backup and one transaction - never deleting a row. **The bills page** (v0.22.0, F-52, D-67): SELL -> *Bills* finds any bill by number, customer name or phone over a date range, opens its lines, payments and returns, and **prints it again with the same builder from the stored rows** - the paper is previewed at 32 / 48 characters first, and every reprint is one line in the black box |
| Has it ever been compiled? | **Yes - and it has now opened and been signed into.** First `Build succeeded` on 20 Oct 2026; on **23 Oct 2026 (v0.33.0)** the owner's launcher printed `0 Warning(s), 0 Error(s)` and `app-2026-10-23.log` shows `13:52:46 Starting Area11 v0.33.0 (Mauveine)` -> `database ready` -> `13:53:31 Signed in: Owner`. That is the step every earlier build failed to reach, so the question "does the window stay open?" is **answered**: yes, on his PC. Anything that fails from now on leaves its own trail (`%TEMP%\Area11\last-run.txt`) and a crash report |
| What is the very next task? | **Run Malachite's 904-case Windows suite, then verify the repaired backup boundary in an isolated copy.** Require `ALL GREEN - 904 of 904`; then do PT01-BK-02/BK-03 A/B/C, B recovery, locked-file, disk-full, photo-fault and restart checks. Cobalt's 900/900 is previous-release evidence. Continue the retained PT-10 pack/search/variant physical walk-through afterward; Aurora PT-09, Zircon PT-08 WPF and later phases remain separate. |
| How many tests? | **904 written cases in 70 files; all 56 static checks pass.** Malachite has not been compiled or run here. The latest owner-executed result is the previous **Cobalt v0.38.0: 900/900**, with 0 failed/skipped in **1 m 4 s** and `ALL GREEN - 900 of 900`. Zircon 883/883, Aurora, Spruce and Obsidian results remain separate historical evidence. |
| Did it build? | **Not the current Malachite tree: the agent machine has no .NET SDK/WPF runtime.** Previous Cobalt evidence is narrower: `Area11.Core` / `Area11.Tests` compiled and passed 900/900 on the owner's Windows PC in 1 m 4 s; that log does not separately prove Desktop/WPF or launcher. Earlier releases did compile/open Desktop. Malachite's build, 904-case run, PT-01 isolated recovery/fault checks and PT-10 WPF/genuine workflows remain unproven. |
| Database? | the **original** schema plus append-only migrations `001`-`029`, **37 business tables** (+ the `_migrations` ledger and provisional report view), so an existing `area11.db` opens unchanged; migration 029 adds stable custom-field keys and guidance without changing answers, quantities or money; old linked PRs require one explicit same-bill reconciliation |
| Where does data live? | `%ProgramData%\Area11\` (`area11.db`, `photos\`, `logs\`, `backups\`) - or, on a second counter, the main PC's share named in `%ProgramData%\Area11\shop.ini` (D-62) |
| Default owner login | user **Owner**, password **`area11`** (change it on day one) |
| How does the owner report a fault? | **`tools\SEND-REPORT.cmd`** (v0.30.3, D-78) writes one text file with the build, `dotnet --info`, the git state, the project files, the shop folder (names/sizes/dates - no rows), the whole newest crash report and the last build/test output. `START-AREA11.bat` keeps `build.log` + `build-errors.txt` and prints only the error lines; `tools\RUN-TESTS.cmd` keeps `test.log` and prints only the result, the failed names and the compile errors |

Status in one line: **52 of 57 features done and 5 owner-action partial rows remain. v0.38.1 Malachite preserves live SQLite sidecars unless staged restore replacement succeeds and adds four backup/fault regressions: 904 written cases in 70 files and 56 green static checks. Malachite Windows build/xUnit and isolated PT-01 recovery/fault proof are pending; Cobalt 900/900 is previous-release evidence. PT-10 WPF/genuine physical proof is still required, POS remains unreproduced, D-93/PR financial rules are unchanged, and Phase 4+ requires separate approval.**

---

## 2. Read in this order

| # | File | Why |
|---|---|---|
| 1 | this file | orientation, next tasks, traps |
| 2 | [`AGENTS.md`](AGENTS.md) | the working protocol you must follow |
| 3 | [`docs/PROGRESS.md`](docs/PROGRESS.md) | every feature with ID, state, files, test, "done when" |
| 4 | [`docs/OWNER-DECISIONS.md`](docs/OWNER-DECISIONS.md) | the owner's decisions D-01…D-98 (Urdu, authoritative) |
| 5 | [`docs/RULES-MATRIX.md`](docs/RULES-MATRIX.md) | the owner's 116 numbered rules → where each one lives |
| 6 | [`docs/AREA11-MASTER-SPEC.md`](docs/AREA11-MASTER-SPEC.md) | the blueprint; sections 3, 5, 11, 12, 14, 15, 34 matter most |
| 7 | [`docs/SCHEMA-REFERENCE.md`](docs/SCHEMA-REFERENCE.md) | column-by-column reference of every table |
| 8 | [`docs/BUILD-ON-WINDOWS.md`](docs/BUILD-ON-WINDOWS.md) | how the owner builds and runs it |
| 9 | [`docs/CHECKLIST-URDU.md`](docs/CHECKLIST-URDU.md) (printable: [`.html`](docs/CHECKLIST-URDU.html)) | **the owner's tick-list** - every feature with its mark, generated from `PROGRESS.md` by `tools/make_checklist.py`, so it cannot drift |
| 10 | [`docs/AREA11-TESTING-TRACKER-URDU.md`](docs/AREA11-TESTING-TRACKER-URDU.md) (printable: [`.pdf`](docs/AREA11-TESTING-TRACKER-URDU.pdf) / [`.html`](docs/AREA11-TESTING-TRACKER-URDU.html)) | PT-01 تا PT-10 کا owner snapshot، automated بمقابلہ physical evidence، manual-instructions tracker اور result forms؛ running feedback پھر بھی `PHYSICAL-TESTING.md` میں |

Reference web app (behaviour source, **never modify it**): `git clone https://github.com/Dr-Adil-Abdullah/Area11`
- `src/lib/sales.ts` (FEFO, discount, round-off), `returns.ts`, `settings.ts` (70 keys), `audit.ts`, `db.ts`.
- The master spec wins wherever the two disagree.

---

## 3. What is done, what is not

Full detail in [`docs/PROGRESS.md`](docs/PROGRESS.md). The short version:

**Written**

| Area | Files |
|---|---|
| Schema 001-029 (37 business tables + ledger/provisional report view, WAL, migrations under `BEGIN IMMEDIATE`) | `Area11.Core/Data/` |
| Settings (123 keys, including 32 application labels), audit black box, auth + lockout, permissions, numbering, hold carts | `Area11.Core/Services/` |
| Products, partners, purchases `PINV`, sales `INV`, **returns `RET` + quarantine `Q`**, dashboard | `Area11.Core/Services/` |
| **Photos** (copy in, six per record, put away never delete, camera hint) | `Area11.Core/Services/PhotoService.cs`, `Area11.Desktop/Views/PhotoGalleryWindow.xaml` |
| **Own fields** (the owner's own questions, seven kinds of answer, required, order, never deleted; central policy, stable keys and optional guidance) | `Area11.Core/Services/FieldGovernance.cs`, `Area11.Core/Services/CustomFieldService.cs`, `Area11.Desktop/Views/CustomFieldsView.xaml`, `CustomFieldsEditorView.xaml` |
| Money / quantity / expiry / password helpers | `Area11.Core/Utils/` |
| WPF shell (+ global alert bar), login, dashboard, products, POS (hold + split), purchases, settings, returns & quarantine, alerts, no-bill returns (PR), write-off, stock-take, customers / khata + statement, **suppliers / payable + `SR` tray** | `Area11.Desktop/` |
| **Second counter over the LAN** (`shop.ini`, journal plan, owner-only join, audit) | `Area11.Core/Utils/ShopConfig.cs`, `Area11.Core/Data/StoragePlan.cs`, `Area11.Core/Services/LanService.cs`, `Area11.Desktop/Views/LanView.xaml` |
| **Tax report + `PAY-` slip + monthly statement** (monthly table, money received, opening/closing balance) | `Area11.Core/Services/TaxService.cs`, `Area11.Core/Services/CustomerLedgerService.cs`, `Area11.Desktop/Views/TaxView.xaml` |
| **Installer** (self-contained build, one double-click, icons, Add/remove entry, spooler + Print-to-PDF + folder permission, database never touched) | `Area11.Core/Services/InstallService.cs`, `Area11.Core/Models/InstallModels.cs`, `tools/INSTALL-AREA11.cmd`, `tools/UNINSTALL-AREA11.cmd`, `tools/MAKE-INSTALLER.cmd`, `tools/install/*.ps1`, `docs/INSTALL.md` |
| **Bills page + reprint** (find any bill, open its lines/payments/returns, print it again from the stored rows, preview at 32/48 characters, one black-box line per reprint) | `Area11.Core/Services/SaleHistoryService.cs`, `Area11.Core/Models/SaleHistoryModels.cs`, `Area11.Desktop/Views/SaleHistoryView.xaml` |
| **Cloud sync** (push, preview, bring with a backup; never deletes a row) | `Area11.Core/Services/SyncService.cs`, `Area11.Core/Services/CloudTransport.cs`, `Area11.Desktop/Views/CloudSyncView.xaml` |
| **Dashboard** (F-12): the home screen's eleven numbers, each equal to the page it summarises - profit by the report's rule, expiry by day, hidden from staff without the profit permission | `Area11.Core/Services/DashboardService.cs`, `Desktop/ViewModels/DashboardViewModel.cs`, `Area11.Tests/DashboardTests.cs` |
| **No demo, ever** (D-77): the sample shop that filled a new database with two and a half months of invented bills was **deleted on the owner's word** in v0.30.2 - `SampleShopService`, its page, `DEMO-AREA11.cmd` and the `--demo` switch are gone, and no flag or button can invent a row | `Area11.Core/Utils/AppPaths.cs` (one data root), `Area11.Core/Utils/BuildInfo.cs` (one title) |
| **904 test cases written in 70 files** (Malachite Windows run pending; previous Cobalt Windows: 900/900 in 1 m 4 s; physical workflows remain separate) | `Area11.Tests/` |

**The five features that still need one action in the shop (🟡)** — the board is the only source of
truth for state; this list is a copy and `docs/PROGRESS.md` wins if they ever differ:

| ID | Feature | The action |
|---|---|---|
| F-04 | Audit black box | one real action is seen in it |
| F-08 | Products | the owner adds a medicine with a batch |
| F-09 | Purchases `PINV` | one real delivery enters stock |
| F-10 | Counter `INV` | one real bill prints |
| F-46 | Printer & scanner | one USB scan finds a medicine and the record lands in the black box (the slip half was confirmed on 23 Oct 2026) |

**Four numbered rules are still open** (E-12 🟡 custom labels written/awaiting owner proof,
S-05 🟡 live propagation written/awaiting the same proof, Z-05 🟡 earlier-state reader written/awaiting owner proof,
T-10 🟡 native `.xlsx` and all-data bulk update written, awaiting Windows/xUnit and real Excel proof). **T-05 is ✅**: actual-code review found the existing native per-day chart; no duplicate engine was added. The matrix is generated from
`tools/build_rules_matrix.py`, and the list is repeated in [`TAKEOVER.md`](TAKEOVER.md) §4.

**Not started (⬜): none.** All 57 features on the board are written; 52 are ✅ and the 5 above are 🟡.
Earlier versions of this file carried a list here (expiry levels, shifts, reports, the misspelt search
…) that had already shipped - they are all ✅ on the board now. If you are reading a copy of this file
that still lists them as ⬜, it is stale: trust `docs/PROGRESS.md`.

---

## 4. Next tasks, in order

Do them one at a time; each one ends with a commit (see `AGENTS.md` §6).

**Backup safety plus retained Phase-3 verification:** Malachite repairs the staged-restore failure boundary
without changing D-98's Phase-3 model. Live SQLite sidecars now survive a failed copy/locked destination;
full recovery and rollback remain. The existing `pack_size_label` is still editable and searchable in
Products, Counter and Purchase, and Purchase shows strength/pack text for separate variants. There are 904
written cases in 70 files and 56 passing static checks; the earlier targeted 7/7 SQLite search probe and
Cobalt 900/900 owner run are previous evidence, not a Malachite run. The next task is the Windows
**904/904** run and isolated PT01-BK-02/BK-03 recovery/fault procedure in
[`PHYSICAL-TESTING.md`](PHYSICAL-TESTING.md), followed by the retained PT-10 walk-through. Aurora PT-09 and
Zircon PT-08 remain separate evidence. No new variant/stock model, schema migration, cloud change,
financial rule or Phase-4 history/profit work was introduced.

| # | Task | Acceptance |
|---|---|---|
| 1 | **Malachite backup safety + retained live walk-throughs** `START-AREA11.bat` | **Current Windows suite pending:** require **`ALL GREEN - 904 of 904`**. **Next:** in an isolated copy complete PT01-BK-02/BK-03 A/B/C, B recovery, committed-WAL/locked-file, disk-full, photo-fault and restart recovery; then retain PT-10 pack editing/normalization, `50ml` discovery, equal-named variant selection, extra-barcode scanning and unchanged conversion/rates. Retain Aurora PT-09 stable key/rename/help/validation/permissions/migration checks. Zircon's 883/883 and Cobalt's 900/900 are previous-release automated evidence; their WPF checks remain separate. **Retained live checks:** the owner's Obsidian run ended **ALL GREEN - 860 of 860**, with zero failed/skipped in 1 m 17 s; these older results do not prove Zircon. **Paprika repair check:** create or retain one never-received product named `A empty` and one named `B stocked` with 3.5 base units; Products and Reorder pad must both open and show 0 and 3.5 without a `Double -> Int64` error. Save an Urdu app label and confirm its Black box new value is readable Unicode. Attach a pending PR to its INV and confirm no second stock/payment event and no double return; if it fails, the message now includes technical detail. **E-12/S-05:** open Settings → **App words**. Rename Counter (POS) to `ریکاؤنٹر` and Settings to `Choices`; Save must immediately change their menu rows and the currently open Settings heading. Open the counter and confirm its heading; rename Dashboard to `Home`, save and confirm both its menu row and page heading. Rename Cloud sync and confirm its Settings tab changes too; on a record with own fields, confirm that panel heading follows Own fields. Restart and confirm the custom words persist. Try a blank label: the whole save must be refused without changing other settings. Use **Restore original words** twice: all 32 defaults return, and the Black box shows old/new `labels.*` rows. Reprint an old bill and confirm its INV number and historical wording were not rewritten. **Z-05:** open Black box, select a settings change with old/new JSON, and confirm the complete earlier and recorded-new values, Unicode, details and guidance appear; copy text without editing it. Select a PR cancellation and confirm it says same-batch stock-only, no payment and no original-financial reversal. Search/filter again and confirm the old snapshot disappears. Confirm there is no Undo/Restore command and no business or audit row changes. **T-05 retained:** open Reports → Last 7 days with Rs 160 / Rs 320 / Rs 480 sales on different days; the bars should be about one-third, two-thirds and full, quiet days stay visible, and exact money labels are unchanged. A manager must not see profit. This confirms the existing chart; no new chart engine was added. **T-10:** on Excel / CSV → Import, save the blank template and open it in real Excel: Products, Customers, Suppliers and Instructions must be four readable sheets. Fill one medicine with opening stock and expiry, preview it, then choose it again after import and confirm the duplicate is skipped. On Bulk update, Download your data and confirm eight sheets with no photos, secrets or counters. Change a medicine name/retail, leave another editable cell empty, clear an allowed optional value with `__CLEAR__`, and confirm every before → after line. Trying to change Stock on shelf, Udhaar now or Payable now must refuse the whole workbook. Settings change only with their separate tick and keep string/number/array types. Apply once, inspect the black-box rows, then upload the same workbook again: it must say 0 changes. No stock or account balance may change from the file. Spruce's 883-case run is green on `94723e5`; these live checks remain separate evidence. **E-04:** Products → **Manage categories and companies…**: add a parent and child, rename/reorder them, and confirm a third level is refused. Put away a linked category/company: the product remains, only its current link clears, and old records remain; bring it back and confirm no old link is invented. Check company rep/visit-day fields and the black-box old/new rows. A cashier write must be refused. **D-93 retained:** try repeated cart lines whose total exceeds stock; the refusal names available/requested, the cart stays, and the next valid bill has no number gap. Sell to exact zero, then prove the next unit is refused. With a positive batch of 5 beside a legacy batch of -3, a 3-unit sale must be refused with physical stock-take guidance; an ordinary shortage must keep reduce/receive guidance. Repeat shortage checks for write-off, goods drawing and supplier return; a failed PR cancellation leaves it pending and creates no payment. Open a legacy-minus fixture in Stock minus, then reconcile it with a physical stock-take (zero and non-zero counts) and inspect the audited adjustment. **Lapis retained:** reconcile a pending PR against its INV with no new stock/payment rows, no double return, and visible over/under-refund warnings; repair a pre-025 link only against the same bill. Retain Cypress's Rs 500 cash + Rs 100 khata check and Cedar's stock-only PR cancellation/drawer checks. **Merlot report follow-up:** open Dashboard with one medicine at or below its reorder level; the alert board must load and fold the low-stock row without a cast error. Attach a pending PR to its INV; it must reconcile without another stock or payment event. **Glaucous cloud:** on a separate test Shop id/table and test database copy, follow `docs/CLOUD-SYNC.md` §7: push and preview format 2, then deliberately apply after a backup; recent payments for an old sale and old purchase must retain those parents and all their lines, and a PR payment must retain its PR/items/allocation/old-sale chain. An unrelated old bill stays out, Refresh/Push invalidates the old preview, switching cloud off refuses Apply, and no key is shared. Cloud is a partial mirror, never the full-machine zip backup |
| 2 | **Own fields walk-through** (F-42) | add a required field to the customer form, fill it on a record, try to save it empty (refused, named), put the field away and bring it back (the answer is still there) |
| 3 | **Barcode walk-through** (F-43) | open a medicine, press **Barcodes of this medicine…**, add a strip code and a box code, scan the box code in the counter's search box and press Enter - the cart must show **one box**, not a strip; put a code away and scan it again (not found) |
| 4 | **Doctors & reps walk-through** (F-44) | write a rep and a visit day on a company; make a customer with category **Doctor**; bill something with that doctor chosen; the page must show the bill, its profit and the lifetime profit per customer - and a manager's login must show a dash instead of the profit |
| 5 | **Second counter walk-through** (F-45, D-62) | share the main folder, *Prepare* on the main PC, restart, *Join* from the second PC - the second PC shows the main shop's bills; switch the main PC off and start the second one: it must **refuse**, naming `\\MAIN-PC\Area11\area11.db`, and create nothing |
| 6 | **Tax, receipt and statement walk-through** (F-49, D-63) | enable tax in Settings, make two bills, open **MONEY → Tax report**: the month's tax equals the two receipts; a refund is listed separately; **Copy for the accountant** pastes a clean table. Receive money on a khata: the **PAY-** slip shows the balance before and after. Print a **Month statement** and check the opening + this month's lines = the closing balance |
| 7 | **Installer run** (F-47, D-64) | `tools\MAKE-INSTALLER.cmd` finishes; `dist\Area11-setup\INSTALL-AREA11.cmd` installs and opens Area11 in **Malachite**; the desktop icon is there; *Add or remove programs* lists Area11 with its version; an uninstall removes the program and **leaves `C:\ProgramData\Area11` exactly as it was**. See [docs/INSTALL.md](docs/INSTALL.md) |
| 8 | **Printer & scanner test on the shop's own paper** (F-46, D-65) | **(v0.22.1: the page now lists every printer Windows knows - pick yours in the box; the code-6 refusal is fixed.)** SYSTEM -> *Printer & scanner*, **Print test slip**: the ruler line fills the paper on 58 mm (32 characters) and on 80 mm (48) - no digit missing, and a shop that changed the roll sees the old ticks marked stale. Scan a box: the page names the medicine without a click. Tick the six boxes, save, and the black box shows one `hardware_test` line with the date, the name and the paper width |
| 9 | **Cloud sync - postponed by the owner** | leave Supabase and cloud settings untouched for now. Area11 remains offline-first; resume the F-48 walk-through only when the owner asks for cloud work |
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
5. **D-93 retires negative stock for new transactions**, superseding U-25/U-30. `StockPolicy` guards
   every sale, write-off, goods drawing, supplier return and PR cancellation; there is no setting or
   owner bypass. Existing negatives stay readable and need audited physical reconciliation, never a
   silent zeroing migration. Exact zero is allowed; failed operations are atomic and preserve the form.
6. **Discount is capped by role** (owner 100 %, manager 20 %, cashier 5 % by default) and the cap comes
   from settings. The 27-Oct rule applies explicit % to full retail/selling price; legacy margin mode is inert.
   Below-cost discounts are clamped unless the setting allows them. Custom rates are Owner/Manager only and
   cannot bypass the batch-cost lock; VIP/Doctor rates require explicit Yes, while No keeps retail.
7. **Permissions are enforced in `Area11.Core`, never in XAML.** A hidden button is not a permission.
8. **Never delete a row.** Use `active = 0`; audit rows and sale rows are permanent.
9. **Migrations are append-only.** Add `030_*.sql`; never edit `001`-`029`. They run inside
   `BEGIN IMMEDIATE` and re-check the ledger after the lock (fixes the original duplicate-column crash).
10. **No SQL in view-models, no business logic in views** (spec §4.4.2). Services own the rules.
11. **One source of truth for a price**: `SaleService.Compute()` drives both the on-screen quote and
    the saved bill, so what the cashier sees is what is stored.
12. **Every action is audited**: who, when, old → new. Never store PINs or passwords in the audit log.

---

## 6. The owner's decisions, in English

| # | Decision |
|---|---|
| D-98 | Phase 3 packaging/variants/search authorized. First slice reuses the existing descriptive pack label in Products, Counter and Purchase; variants remain separate products with base-unit stock and existing conversion/barcode rules. Cobalt has 55 static checks and owner-verified Windows xUnit 900/900 in 1 m 4 s; PT-10 physical evidence and any later Phase-4 approval remain separate |
| D-97 | Zircon 883/883 Windows xUnit verified; Phase 2 central field governance/schema authorized. Aurora uses stable keys, central validation, guidance and migration 029; Phase 3+ needs separate review |
| D-96 | Phase 1 only: fixed Area11 expanded dark header, clickable modules/context pages, Settings sections and Products category picker in the shell, 44×44 bell, no left-sidebar folding. Preserve permissions/routes/alerts and all financial rules; later redesign phases require separate owner approval |
| D-01 | Database is the original schema, verbatim, plus migrations 014 (per-person permissions) and 015 (photos) |
| D-02 | A cashier's return puts goods in **quarantine** and raises a permanent alert that stays on every screen until the **owner** dismisses it with a reason |
| D-03 | Permissions are per person, edited in Settings; roles are data, not hard-coded |
| D-04 | Features the original web app never finished are documented and scheduled into Phase 2/3 |
| D-05 / D-23 | Printing is instant: no dialog, straight ESC/POS, 58 and 80 mm, plus reprint |
| D-06 | Camera capture is not in Phase 1 |
| D-58 | Photos are **copied** into `photos\` and only the file name is stored (U-19); 12 MB and six per record; a wrong photo is **put away**, never deleted; the camera opens the Windows Camera app |
| D-93 | **Negative stock is retired for new work** (v0.35.9 Onyx): one central `StockPolicy` guards selected-batch and whole-product quantities for sales, write-offs, goods drawings, supplier returns and PR cancellation. Carts reserve repeated products/shared FEFO batches before numbering; failures are atomic and preserve the form; exact zero works. The legacy setting is inert, historical negatives are never silently zeroed, and physical stock-take is the audited correction. PR cancellation remains stock-only with no repayment or report-reversal row. **814 cases, 45 static checks, 7/7 SQLite rehearsal groups plus 500 guarded attempts; Windows xUnit pending** |
| D-92 | **One time for the whole shop, a session that ends by itself, and no bill without a galla** (v0.35.0): `TimeSource` + `NtpTime` take the time from a time server when the internet answers (re-asked every 20 minutes) and from the computer when it does not, keeping one correction that `Clock` - the single door for every stamp - applies; an answer more than two days out is refused; `SessionPolicy` + `SessionWatch` hold the owner's idle choice (1/2/4/6/12 h) and a **12-hour ceiling that is not a setting**, announce the last 15 minutes in the top bar naming the clock that will fire, and end the session at the sign-in window; `SaleService.Create` refuses a bill with no open shift, the counter shows a card with **Open a galla now**, and a drawer left open by a forgotten session is remembered, never closed by the program - only a counted amount may close a drawer |
| D-91 | **The owner's printed tick-list** (v0.34.0, docs only): `docs/CHECKLIST-URDU.md` + `.html` (A4, a pen box per feature) are **generated** from `docs/PROGRESS.md` by `tools/make_checklist.py`, and `PRINT-CHECKLIST.cmd` opens the printable page. Gate **#43** compares the marks and the totals with the board, so the printed list cannot drift; proved by four injections. **43 checks** |
| D-90 | **A heartbeat, and a button that cannot take the counter down** (v0.34.0): an absent `%TEMP%\Area11\binding-errors.txt` used to prove two opposite things at once - *no silent binding fault* and *the program never started* - and on 23 Oct 2026 that ambiguity produced a false alarm. `RunTrail` now writes `%TEMP%\Area11\last-run.txt` **before the first window** and adds a line for the database, the sign-in window, the signed-in name, every page opened, any UI-thread fault and the exit code (capped at 140 lines, never throws, never in the shop folder). Both kinds of `RelayCommand` now run through `CommandGuard.Run`: a throwing button is caught inside itself, the crash report is still written, and a red banner **above** the page names the button, says the page is still open and the rest of the shop untouched, names the report file - and never claims *"nothing was saved"*. The page is not rebuilt, so a failed Save no longer empties the cart. **20 tests (750), 42 checks**, gate #42 proved by five injections plus two more for the version table |
| D-89 | **An empty page says why it is empty, and a page that fails while drawing says that too** (v0.33.0): every page the menu can open implements `IEmptyPage` and computes its hint from its own list (`EmptyPage.When(rows == 0, "Products", searched: …)`), so a blank grid now reads *"Nothing here yet. Add the first medicine with 'New medicine', or bring the shelf list in from Excel / CSV."* - and a search that finds nothing says *"Nothing matches what you typed."* with the instruction to widen it. One card in the window frame draws it for every page; two pages are exempt in writing (the fault card, and the container whose tabs carry their own sentences). **Second half:** a fault raised *after* the page was built - while WPF drew it - is handed by the dispatcher to the frame, which shows the same card saying **"stopped while its screen was being drawn"**, and logs and records it. **13 tests (730), 41 checks**, gates #40/#41 proved by nine injections |
| D-88 | **A broken page must not take the till down** (v0.32.0): every menu row, Settings and the alert bell are built through `PageGuard.Open`, so a constructor that throws (one failed read, one odd row, one missing table in an old copy of the file) becomes a **card in the place the page would have been** instead of an exception on the UI thread. The card names the page, keeps the exception's own words, says **"Nothing else is affected"**, tells the operator to press another page and try again, and names the report file (`PageFault.ReportLabel`, with a fallback to the log folder when nothing could be written). The guard **never throws** - the log and the recorder have their own unfiltered catches, and `Open` is a net around the whole body - it always logs, and it hands the exception to the crash recorder. **13 tests (717), 40 checks**, gate #40 proved by four injections. **Where it stops:** it covers a page's constructor, not a fault inside the XAML view - that one belongs to the crash reporter and the binding watch |
| D-87 | **697 of 697 green, and the evidence tool that broke** (v0.31.2): the owner's suite passed **697 of 697 in 51 s** on the build that carried the 171 binding repairs - but `tools\SEND-REPORT.cmd` first printed *"Cannot bind argument to parameter 'LiteralPath' because it is null"* and still wrote a report. The holder is computed once now, the log section cannot pass `$null`, and **the report lists its own errors**. New: a **binding watch** writes WPF's silent binding complaints (a wrong property name = an empty box and no exception) to `%TEMP%\Area11\binding-errors.txt`, timestamped, clipped, rotated, never throwing, installed before the first window, and named in the crash report. **7 tests (704), 39 checks**, gate #39 proved by breaking it three ways |
| D-86 | **The first real run of the window** (v0.31.1): the owner's launcher closed the old copy, compiled the whole solution (`BUILD OK` - the first time the WPF project built) and the window stopped. **Two faults, both fixed:** (1) **171 bindings** pointed a TwoWay-by-default target (`Run.Text`, `DataGrid...Column.Binding`) at a computed label with no setter, so WPF threw *"A TwoWay or OneWayToSource binding cannot work on the read-only property"* and closed the window - every one now says `Mode=OneWay`; (2) the failure screen printed **a report from the day before, written by v0.29.0**, because this run wrote none (its report went to `%TEMP%\Area11`) - it now prints only a report newer than a **marker** written before the program starts, reads the program's own pointer to its newest report, and **names an old file without printing it**. **2 tests (697), 2 gates (38 checks), both proved by breaking them** |
| D-85 | **Who was short** (v0.31.0, F-56, Spec 7.2.7): the Shift page now shows the drawer per person over a window - shifts counted, expected, counted, net difference, the pattern ("3 short, 1 over"), the worst short and worst over, worst first. **Short and over are summed from the shift rows, never from the people's nets** (a shop short 500 and over 200 is "500 / 200", not "300 / 0"), an open drawer is never counted, and only the owner or a manager may look - a cashier with the till permission is refused and the card is hidden. Window 1-3650 days, default 30. **9 tests; 695 cases in 54 files** |
| D-84 | **686 of 686 green - and the runner that could not say so** (v0.30.9): the owner's run passed every case (failed: 0, 686, 51 s), and `RUN-TESTS.cmd` still ended it with *"Not OK is not a disaster"*, because that sentence was in the main flow. The verdict now comes from the exit code, `Failed: 0` and the total against `EXPECTED`: **ALL GREEN - 686 of 686**, **GREEN, BUT THIS IS AN OLD FOLDER** (a clean run of a stale pull, exit 1), or a real failure. The launcher also names the project that broke. Gates #34/#35 keep both honest, both proved by breaking them |
| D-83 | **The launcher closes the old copy before it builds** (v0.30.8): the owner's build stopped with MSB3027 / *"The file is locked by: Area11 (8460)"* - the program from the previous run was still open, and Windows will not overwrite a DLL in use. Step 5 of 6 now finds that copy, names it, asks *"Close the running Area11 now? [Y/N]"*, ends it and waits ~2 s before building; **N** stops cleanly. If it happens anyway, MSB3027/MSB3021 print one plain sentence instead of the MSBuild wall. Check #34 reads `<AssemblyName>` and fails if the script would look for a different exe, stop asking (`tasklist`), stop closing (`taskkill`) or stop recognising either code |
| D-82 | **The first real run, and the six red lines** (v0.30.7): the owner ran `RUN-TESTS.cmd` - 685 total, 679 passed, 6 failed. Three were wrong tests (a purchase test built three units and expected two; a CSV test wanted `5 missing` where the screen says `Rs 5 missing`; a handover test wanted one `shift_open` row when there are two). Three were one real fault in the handover's boundary second: the new shift counted a bill whose cash was already in the counted float (Rs 300 invented), and the answer depended on the clock. The window is now `[opened, at]` for the closing shift and `(at, now]` for a shift opened by a handover - decided by `opened_from_shift_id`, never by a flag - proved by running the SQL on a database built from all 23 migrations (exact, short-by-50, a bill pinned to the boundary second, and a two-handover chain: 300 / 500 / 600). **686 cases in 53 files** |
| D-81 | **The drawer changes hands** (v0.30.6, F-55): one transaction closes the first shift, records who gave it to whom and opens the next with the **counted** money as its float, so a shortfall stays on the shift that produced it; the closing window is `[opened, at)` and the new one `[at, now]`, so a bill in the handover second is counted once. Five refusals (yourself, switched-off account, no Cash permission, a peer handing a third person somebody else's drawer, no shift open), and the slip carries both names, the eight money lines, the difference in words and two signature lines at 32 / 48 characters |
| D-80 | **The code is read by a real C# grammar, and a fault can leave the screen** (v0.30.5): the 32nd check parses all 239 C# files with tree-sitter-c-sharp (an invalid file is named with a line number, where brace counting noticed nothing); the 33rd resolves 2,021 calls and 3,325 `Type.Member` references against the declaring type, catching a misspelt name and a wrong argument count before the build. The books check now saves its findings as **CSV** and shows a **rupee total per document** (not per rule - one broken bill is three findings), with missing and too much kept apart |
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

`check_repo.py` runs 39 checks (v0.29.0 made the **explicit usings** rule see `Type?` in a signature, stopped it counting nested private classes as namespace types, and gave it a word pre-filter so it runs in 6 s instead of 34; v0.29.3 added the **`{x:Static}`** rule, v0.29.4 the **service method name** rule, v0.29.6 taught the **SQL** rule to read the tests and to stop losing its place after a verbatim or interpolated literal - it now prepares **711** statements instead of 327; v0.29.7 taught the **column** rule to read the tests as well - 871 columns over 101 files; v0.29.8 widened the **constructor** rule to every project and added the **`_services.X`** rule, and made `tools\RUN-TESTS.cmd` print the version it tests; v0.29.9 made the **XAML binding** rule ask the page instead of the whole program - 81 row types resolved from ItemsSource, 1,278 bindings; v0.30.0 added the **page map** rule - 27 `{x:Type}` names, and every shell page's template must hold the view of the same name; v0.31.0 added the **shift report** rule - `ByPerson` must ask for the Cash permission **and** a manager, the shop's short and over must come from a second query over the shift rows (the service may not call `.Sum(` at all), and the `PeopleSummary` table must sit inside the one `CanSeePeople` wrapper - proved by breaking the guard and the visibility; v0.31.1 added the **binding-mode** rule - a TwoWay-by-default target (`Run.Text`, a grid column, a TextBox, a ComboBox) may not bind a property with no public setter without `Mode=OneWay`, resolved against the row type the grid really shows, proved by removing one `Mode=OneWay` - the **crash-report** rule - the launcher carries a run marker, reads the program's pointer to its own newest report, searches both folders, and names an older report without printing it, proved by breaking the filter and by printing an old file; and v0.31.2 the **report-tool** rule - every `$variable` reaches a path parameter only after it is assigned, the report carries its own errors, and the binding watch is installed before the first window, proved three times by breaking it): C# syntax, **explicit usings** (the CS0103 class of error),
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
| adding a ZIP part through `new MemoryStream(bytes)` | that constructor creates a fixed-size stream; the shared-strings fixture can fail before the reader is tested | use an expandable `MemoryStream(capacity)`, write the bytes and rewind (Obsidian) |
| putting a pack code and an edited base code on the same product | the workbook edit is valid, so the cross-product collision test reports too few errors | put the active pack on the OTHER product and assert zero ready changes |
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
