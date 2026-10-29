# TAKEOVER — the complete brief for a new agent

> ## اردو میں، پہلے پانچ سطریں
> یہ فائل اِس لیے ہے کہ آپ (مالک) ریپو کا لنک کسی **نئے ایجنٹ** کو دیں اور وہ **یہیں سے** کام شروع کرے۔
> نئے ایجنٹ کو صرف یہی ایک فائل پڑھنی ہے؛ اِس کے بعد وہ خود `docs/PROGRESS.md` (کیا بنا، کیا باقی)،
> `docs/OWNER-DECISIONS.md` (آپ کے فیصلے)، `AGENTS.md` (کام کا طریقہ) اور `docs/RULES-MATRIX.md` (آپ کے 116 اصول) پڑھ لے گا۔
> اِس فائل میں یہ سب ہے: پروگرام کیا ہے، ابھی کہاں کھڑا ہے، **کیا باقی ہے**، کیا **کبھی نہیں** کرنا،
> اور ایک نیا ورژن کیسے نکالنا ہے۔ آپ کے لیے چھوٹی اردو فائل: [`docs/HANDOVER-FOR-OWNER-URDU.md`](docs/HANDOVER-FOR-OWNER-URDU.md)۔

**Status:** v0.37.0 · Aurora · `#5B4B8A` — **default branch `main`**; this Phase-2 field/schema release is on the current
working branch `arena/60395e95-area11a` until its pull request is merged.

*Last updated: 29 October 2026. D-97 authorizes Phase-2 central field governance/schema. The current tree has **892 written cases and 54 green static checks**; the owner verified **883/883 Windows xUnit cases on Zircon v0.36.0** with 0 failed/skipped in **1 m 10 s**. Aurora's Windows build, 892-case run and PT-09 physical evidence are pending. The feature board remains 52 done / 5 owner-action rows and the matrix has four partial rules. Real Excel, physical-restore and optional real-cloud evidence remain separate.*

---

## 1. What this is

**Area11** is a native **Windows desktop** pharmacy POS + inventory: **C# / .NET 10 / WPF / SQLite**,
fully offline, one database file, bills printed instantly on 58 mm or 80 mm thermal paper.

It is a port of the owner's original Next.js + SQLite web app (`Dr-Adil-Abdullah/Area11`). That
repository is a **behaviour reference only** — never modify it, never copy its code.

| | |
|---|---|
| Owner | **Dr. Adil Abdullah** — a pharmacist, **not a programmer**. He tests every build himself, on the shop PC. |
| Product name | Area11 (assembly `Area11`, folder `dotnet/Area11.Desktop`) |
| Solution | `Area11.sln` at the root, three projects — **three only** (his D-17) |
| Shop data | `C:\ProgramData\Area11` — `area11.db`, `photos\`, `logs\`, `backups\`. **Never deleted by anyone.** |
| Install | `C:\Program Files\Area11`, installed by `tools\INSTALL-AREA11.cmd` |
| Language | Talk to the owner in **Urdu or English matching his message — never Roman Urdu**. Code, database and UI are **English**. |

---

## 2. Where the truth lives — do not duplicate a number, link to its owner

| Question | File that owns the answer |
|---|---|
| Physical test results, owner feedback and requested changes | **`PHYSICAL-TESTING.md`** — append-only physical-testing log |
| What is done, what is next, per feature (ID, files, test, "done when") | **`docs/PROGRESS.md`** — the board |
| What the owner decided, in his own language (D-00 … D-97) | **`docs/OWNER-DECISIONS.md`** (Urdu, authoritative) |
| His 116 numbered rules and where each is implemented | `docs/RULES-MATRIX.md` |
| The blueprint (rules, data, pricing, billing, printing, rebuild protocol) | `docs/AREA11-MASTER-SPEC.md` |
| Every table and column | `docs/SCHEMA-REFERENCE.md` |
| Which versions exist, their colour and why each changed | `docs/VERSIONS.md` |
| How to work here: hard rules, verification, definition of done | `AGENTS.md` |
| The long manual: orientation, walk-throughs, traps, decisions in English | `HANDOFF.md` |
| Build, run, install, "what to send when it fails" | `docs/BUILD-ON-WINDOWS.md`, `docs/INSTALL.md` |
| The printable page the owner ticks with a pen | `docs/CHECKLIST-URDU.md` / `.html` (generated) |
| How to hand the project to another agent | **this file**, plus `docs/HANDOVER-FOR-OWNER-URDU.md` |

The rule that keeps this honest: **a fact is written once.** The board owns feature state; the
decision file owns the owner's word; `VERSIONS.md` owns the release history. Everything else links.

---

## 3. Where it stands right now (read this twice)

**Current source: v0.37.0 "Aurora" contains D-97 Phase-2 central field governance/schema changes and 892 written cases in 69 files. `FieldGovernance` owns custom-field definition and answer validation; migration 029 adds stable rename-safe keys and optional guidance without rewriting existing answers. The Own fields admin exposes key/help metadata, while all seven types, required-field atomicity, permissions and photo behavior remain. All 54 static checks pass locally. Zircon v0.36.0 has owner-verified **883/883 Windows xUnit cases in 1 m 10 s**; Aurora's Windows build, 892-case run and PT-09 physical walk-through are required. Phase 3 packaging/search and later phases are not started. T-10, Z-05, E-12 and S-05 remain partial, the five owner-action feature rows remain open, and PT02-POS-01 still needs exact reproduction.**

| | |
|---|---|
| Test cases written | **892** in 69 files (`tools\\RUN-TESTS.cmd` compares the run against `EXPECTED=892`) |
| Static checks | **54**, all green: `OK - 54 checks, no problems` |
| Owner's numbered rules | **116 - 92 ✅ done, 4 🟡 partial, 0 ⬜, 20 standing** - see §4 |
| Feature board | **57 features — 52 ✅ done, 5 🟡 one shop action each, 0 ⬜ not started** (91.2 % / 8.8 %) |
| Code size | 289 C# files + 40 XAML files; 53 services, 38 view models |
| Database | 29 migrations → **37 business tables + ledger/provisional report view**, append-only, existing `area11.db` opens unchanged |

### What the owner's own PC has already proved

* On **v0.33.0** his launcher printed `Build succeeded. 0 Warning(s), 0 Error(s)` and the suite ran
  **730 of 730 in 53 s — ALL GREEN**; the window opened and a person signed in (`Signed in: Owner`).
* On **v0.35.1** his launcher printed **`BUILD OK`** again, and the program started, opened its
  pages and signed in — so `Area11.Desktop` **compiles**. That closed a worry that had lasted for
  several versions (nothing in `dotnet/` can be compiled in the agent's Linux sandbox).
* On **v0.35.4** the corrected branch passed the launcher guard and the real suite reported
  **769 passed, 3 failed, 772 total in 55 s**. Those three cases are the subject of v0.35.5.
* On **v0.35.5** his next run compiled `Area11.Core` and `Area11.Tests` and reported
  **Failed: 0, Passed: 772, Total: 772 in 56 s — ALL GREEN**. This proves the numbering, session and
  Shift repairs together; it is not a claim that the Desktop installer or every page was exercised.

* On **v0.36.0 Zircon** his Windows build reported **Failed 0, Passed 883, Skipped 0,
  Total 883 in 1 m 10 s — ALL GREEN**. This is the latest full xUnit proof; it does not by itself
  prove a fresh WPF-window walk-through, real Excel or real Supabase. Spruce `94723e5` (883/883 in
  1 m 19 s) and Obsidian (860/860) remain earlier historical evidence.

### What v0.37.0 changes — D-97 Phase-2 field governance/schema

* `FieldGovernance.cs` is the single custom-field definition and answer-normalization policy. The older
  `CustomFieldService.Normalize` facade delegates to it, so existing callers and new UI use the same rules.
* Migration `029_field_governance.sql` adds `field_key` and `help_text`, backfills legacy keys as
  `entity.custom.id`, and leaves `custom_values` untouched. A label rename cannot change the key.
* Own fields shows the stable key and optional one-line help; record forms display that guidance. All seven
  field types, required-field atomic saves, put-away/restore, permissions and photo-gallery behavior remain.
* **Evidence:** 54/54 static checks and 892 written cases in 69 files, including 9 focused regressions. No
  local .NET/WPF runtime exists; the owner must build Aurora, run **`ALL GREEN - 892 of 892`** and complete
  PT-09. Zircon's 883/883 result is previous-release evidence, not Aurora proof.

### What v0.36.0 changes — D-96 Phase-1 global shell

* `ShellWindow.xaml` now uses an expanded dark header rather than a 176 px left sidebar. The fixed Area11
  wordmark, permission-filtered module buttons, active module/page states and a top-right 44×44 alert bell
  are in the shell; the signed-in person's settings/sign-out/version controls remain in a popup.
* `ShellViewModel.cs` keeps the existing stable label keys, lazy `PageGuard` route and drill-downs while
  maintaining separate module, page, Settings-section and Products-category state. Settings keeps its six
  existing contents and non-reloading tab indices; System's ordinary pages remain reachable beside the
  Settings sections. `HeaderMenuItem.cs` contains the small shell-only choice models.
* `SettingsView.xaml` is headerless but still uses `SelectedTabIndex`; its in-content tab strip is not a
  second navigation system. `ProductsViewModel.CategoryFilterId` is read-only and separate from the product
  editor's `SelectedCategory`; a put-away filtered category clears rather than becoming invisible.
* The old application-name/menu-fold controls are retired from Settings. The product title is fixed Area11,
  while shop/receipt branding remains store data. Shared header spacing, typography and button styles live
  in `Theme.xaml`; no financial, stock, schema, printing, WhatsApp or sync rule changed.
* **Evidence:** 53/53 static checks, including real C# grammar and 1,452 XAML bindings, plus the owner's
  Zircon Windows result **`ALL GREEN - 883 of 883`** in 1 m 10 s. PT-08 WPF remains separate; this does not
  prove Aurora or its 892-case suite. The previous Spruce `94723e5` result is older historical evidence.

### What v0.35.19 changes — physical-testing repairs and explicit counter pricing

* Backup/recovery and shift/alert changes remain as recorded in `PHYSICAL-TESTING.md`; none creates a payment, drawer inflow or reversal row. The reported expiry-reminder test compile error was fixed in `aedb65d` by binding `productId = product`.
* Explicit discount now uses full retail/selling price even when an old database contains `discount.mode = margin`. With an explicit discount, special rates do not stack. Otherwise an applicable VIP/Doctor rate requires Yes; No keeps ordinary retail.
* The cart shows the selected-unit rate. Owner/Manager custom rates use the existing integer-paisa override and survive hold/resume; Core refuses cashier custom-rate bypasses and lines already below batch cost when the lock is on. Quote and Create still share `SaleService.Compute`, and bill total remains the sum of line totals.
* A successful commit can show the optional Done/invoice/change card. It performs no financial action; Done or scanning the next item merely continues. Settings controls it independently of auto-print.
* The suite is **883 written cases in 68 files**, and the owner ran it green on Windows: `ALL GREEN - 883 of 883` on `94723e5`. Next perform the genuine-sale, isolated-restore and new catalog/unit/navigation/photo checks. The CAT-01/NAV-01/UNIT-01/PHOTO-01 source batch is present; do not guess PT02-POS-01.

### What v0.35.18 changes — the real Eclipse result and two workbook fixtures

Eclipse was still red, but its **858 passed / 2 failed / 860 total** was eleven better than Glaucous.
That is direct owner evidence for the exact PR allocation binding; it is not a claim that every PR or
workbook behavior is already proven. The only two remaining test identities were supplied:

* `Reader_UnderstandsSharedStringsAndRealExcelDateCells` — its method-level stack line was pasted, but
  its actual exception/assertion text was not. The fixture nevertheless had a concrete defect:
  `new MemoryStream(bytes)` is non-expandable, while `ZipArchiveMode.Update` then adds a new
  `xl/sharedStrings.xml` part. Obsidian uses `new MemoryStream(bytes.Length + 4096)`, writes the bytes
  and rewinds before opening the ZIP. The production reader is unchanged; the rerun must confirm the
  outcome rather than assuming an unseen exception message.
* `BulkUpdate_RefusesStockAndAccountBalanceChanges` — Eclipse reported **1 record / 1 field ready,
  7 unchanged, 89 skipped, 2 errors**. The fixture inserted `PACK-999` for `otherId` and then edited
  that same product's base barcode to `PACK-999`. The production guard rejects an active pack owned
  by **another** medicine, so that edit was legitimately Ready; stock and Udhaar supplied the other
  two errors. Obsidian assigns the pack to `productId` and edits `otherId`, making the collision
  genuinely cross-product. It also asserts `ChangeCount == 0`. Stock/account protection and the
  barcode service's same-product migration behavior are unchanged.

No case was removed or renamed: **860 remains the accepted total**. All **52** static checks pass,
and injecting the fixed-size stream and same-product pack separately makes the workbook guard fail
**2/2** times before byte-for-byte restoration. The owner's subsequent Obsidian run then passed
**860/860 with zero failed/skipped in 1 m 17 s**, so both fixture repairs are now xUnit-verified.
The separate real Excel workflow still remains; no real Supabase mutation is authorized.

### What v0.35.17 changes — the real Glaucous result and exact PR parameter binding

Glaucous was not green: the owner's correct 860-case run reported **847 passed, 13 failed, 0 skipped,
in 1 m 4 s**. The supplied tail identifies only
`ExcessRefundOnOneMedicine_AlsoCapsTheOtherLinesOfTheBill` and
`OldStatusOnlyLink_BlocksAnotherReturn_UntilTheSameBillIsReconciled`. Both failed at PR Link with
`Must add values for ... @provisionalItemId, @saleItemId, @qtyBase, @expected, @refund, @at`.
The complete `test.log` is still needed if any failure remains; eleven identities are not known.

The allocation SQL used camel-case names while anonymous-member shorthand exposed Pascal-case names.
Eclipse now writes `provisionalItemId = line.ProvisionalItemId` and
`saleItemId = line.SaleItemId`; the other four bindings and every financial rule are unchanged. The
existing PR facts already exercise successful links, split lines, repeated PRs, over/under refunds,
old-link repair and the promise of no second stock/payment event, so no test was renamed, removed or
added merely to change the total. **860 cases remain.** A 52nd static check protects the exact names
and rejected the old shorthand in **1/1** injection before byte-for-byte restoration. At release this
was static evidence, not an all-thirteen-fixed claim; the owner's subsequent 858/860 run is recorded above.

### What v0.35.16 changes — cloud dependency integrity

The actual cloud code had a current-schema failure that its 27 fake-wire tests did not cover. A payment
is selected by its own date, but it can belong to a sale or purchase older than 90 days. Migration 024
also lets it reference a provisional return. The old document omitted those parents; applying the exact
shape to another database with foreign keys on reproduced `FOREIGN KEY constraint failed`. Clearing the
link or skipping the financial row would hide money, so neither is allowed.

`SyncService` now uses one dependency-complete document reader for Status, Preview and Push. The six master lists remain whole;
recent sales, purchases, provisional returns, payments and expenses, plus their recent lines, are read
in bulk. Only referenced older parents are added. Their complete lines come too, and a PR allocation can
pull its old sale line and bill into the same finite closure. Unrelated old bills remain excluded and all
rows are sorted by id. The common path does not issue one query per recent bill. Format **2** has **15
tables** (the original 12 plus PR headers, items and allocations). Format 1 remains readable; an older
Area11 refuses format 2 rather than applying links it does not understand.

Apply is guarded in Core, not only by the button: switched-off or incomplete configuration and an
unsupported future format are refused before backup. A null id is skipped rather than allowing SQLite
to auto-number a new row. Refresh or a successful Push clears the previously applicable cloud preview.
The original permission, shop-id, confirmation, backup, one-transaction, unknown-column and no-delete
rules remain. No migration, quantity type, stock rule, payment rule or PR cancellation behavior changed.

Seven new facts cover old-sale and old-purchase round-trips, the full PR chain, switch/format/null-id
boundaries and format-1 compatibility: **860 written cases in 68 files**. The 51st static check protects
the dependency edges, bulk reads, write order and guards; it rejected **5/5** injected faults before
restoration. Three real-SQLite round-trips passed foreign-key checks, including recent bulk lines with
only one old-parent query and an unrelated old bill excluded. The owner subsequently ran Glaucous and
reported **847/860 with 13 failures**; that evidence is preserved above, not rewritten as a pass. For
the separate real-cloud procedure, use `docs/CLOUD-SYNC.md` §7 with a separate test Shop id/table and
test database copy. Cloud is a partial mirror, not the full-machine zip backup; never share the API key.

### What v0.35.15 changes — Paprika verification repair

Paprika's correct Windows run is evidence, not something to hide: .NET SDK 10.0.402 built it with
**0 errors / 9 warnings**, the WPF window opened and navigated, and xUnit reported **837 passed /
14 failed / 851 total**. Only four failed-test identities were included in the pasted tail; the full
146-line `test.log` remains necessary before claiming every failure was addressed.

The live Products/Reorder errors came from a mixed projection: a product with no batch makes
`IFNULL(SUM(...), 0)` integer zero, while a later stocked product returns `Double`. Both projections
now `CAST(... AS REAL)` and two facts put an empty product before one with 3.5 units. This does not
cast `qty_base` or any quantity to `INTEGER`; the schema and models remain `REAL`/`double`.

Settings serialization now uses the repository's existing `Json.WriterOptions`, so Urdu remains
readable in stored/audited JSON and the unchanged AppLabel assertion tests the real write path.
PR reconciliation and RET double-return reads explicitly project quantities as `REAL`; linking still
creates no stock movement or payment and the reconciliation tests remain in force. An unexpected PR
link error now includes its inner technical detail so the next complete log is actionable.

The nine Paprika warnings were four nullable values passed to `KeyText`, four guarded nullable ids
passed to `ReferenceExists`, and one `PrepareBulk` dictionary-nullability mismatch. Three signature/
inference edits resolve those without changing workbook behavior. **853 cases in 68 files and 50
static checks** are the local evidence, not compilation or xUnit. Quasar's own 853-case target was not
run; the current **860-case** suite includes it, and the real Excel walk-through remains separate.

### What v0.35.14 changes — T-10 native Excel and safe all-data bulk update

`Models/XlsxWorkbook.cs` is a dependency-free Open XML reader/writer, not a CSV file with a new
extension. It accepts ordinary Excel shared strings, inline strings, numbers, booleans and date cells;
its own writer uses formula-safe inline text and bounded ZIP/XML input. No package, Excel installation
or migration is required. The four-sheet blank template feeds the existing new-record previews, and
ordinary list downloads are real `.xlsx`. CSV remains for new-record imports and report/black-box exports.

**Download your data** produces Products, Customers, Suppliers, Categories, Companies, Batches,
Settings and Instructions without photos, secrets or numbering counters. On the Bulk update tab an
edited workbook produces a complete before → after preview. Empty cells mean no change; `__CLEAR__`
clears only allowed nullable values. Stock, balances and protected record links are read-only.
Settings require a separate tick and keep string/number/boolean/array/object types through the same
Settings guard. Catalog names/parents and product barcodes are checked across the whole file.

Every changed row carries a snapshot. Apply verifies all old values and cross-record rules before the
first update, then writes all rows and black-box entries in one transaction; a stale later row rolls
back an earlier valid row. An already-applied file previews **0 changes**. Fourteen facts cover these
rules, real Excel cell encodings, permissions and rollback. The written inventory is **851 cases in
68 files**; **49 static checks** and the T-10 guard's **5/5** injection/restoration outcomes pass here.

There was no .NET SDK or Excel in this sandbox when Paprika was written. The owner's subsequent
correct run did compile and open it, but xUnit was **not green: 837/851 with 14 failures**. T-10 stays
🟡; Quasar became the 853-case repair target and Glaucous now carries those cases in the 860-case
run. The real open/edit/preview/apply/re-import walk-through is still separate. Z-05 and E-12/S-05
evidence remain separate; the cloud stage began only after the owner's later `next`.

### What v0.35.13 changes — T-05 was already built; the stale status is corrected

Actual-code review found the Spec 21.3 per-day bar chart already in `ReportsView.xaml`, backed by
`ReportDayRow.BarShare` and `ReportService.ReadDays`. It includes quiet days, scales against the
busiest day without altering integer-paisa values, and zeroes per-day profit for a manager. The old
Recharts/TanStack names describe the reference web app; native WPF bars and DataGrids are the desktop
implementation. No second chart engine, package, report query, total, model or layout was added.

Three new facts cover Rs 160/320/480 proportional bars, a seven-day all-quiet zero-scale and manager
profit hiding. The new static guard protects the same service/view/test wiring and caught scaling,
binding and permission injections before passing restored source (**4/4 outcomes**). The written
inventory is **837 cases in 66 files** and all **48 static checks** pass. T-05 is ✅ on the existing
Windows-compiled chart and earlier owner-green per-day facts; the three new cases still require
Cinnamon's `ALL GREEN - 837 of 837`. Z-05, E-12/S-05 and Merlot live checks remain open; T-10 native
`.xlsx` and cloud work were not started.

### What v0.35.12 changes — Z-05 earlier-state recovery without financial rollback

The Black box now has a selected-row snapshot. It shows complete earlier and recorded-new values,
context and details in read-only copyable fields. Raw strings and CSV stay untouched; valid JSON is
indented only for display, Unicode and long values remain readable, and plain or malformed JSON is
shown verbatim. Creation, deletion and details-only rows explain missing sides. A refresh clears the
old selection, and the panel explicitly says the recorded-new value is not a live database query.

This is deliberately **not universal Undo**. The viewer has no write path or Undo/Restore/Rollback
command. It directs staff to existing audited correction screens instead of reversing completed
sales, payments, returns, stock entries or PRs. PR guidance repeats the active rule: cancellation
removes stock from the same batch only, never creates a payment and does not reverse the original
financial effect. Settings and master-data corrections require a new audited save.

Seven facts bring the written inventory to **834 cases in 66 files**. All **47 static checks** pass;
the new Z-05 guard caught a removed snapshot, an injected Undo command and a missing earlier-value
binding, then passed the restored source (**4/4 outcomes**). There is no .NET SDK here, so these are
not xUnit or WPF results. The owner must see `ALL GREEN - 834 of 834` and exercise selection, copying,
filter clearing and guidance. Z-05 stays 🟡 until then. E-12/S-05 label proof and Merlot Dashboard/PR
checks remain outstanding; charts, native `.xlsx` and cloud work were not started.

### What v0.35.11 changes — E-12/S-05 custom application labels are written

Settings → **App words** edits 32 supported application-section names. They are ordinary rows in the
existing `settings` table, so an old database gets only missing defaults; there is no migration and
no destructive rewrite. `AppLabelService` owns the catalog, trimming, Unicode and 60-character/
single-line/non-empty validation, supported-key checks, Settings permission, atomic old/new audit
and reset. Direct `SettingsService` label writes have the same permission and validation guards,
and the quiet UI-only writer refuses labels. A damaged blank stored value falls back to its default.

Stable `LabelKey` values, not editable titles, drive navigation, permissions, Shift/Settings
shortcuts and page resolution. Saving applies WPF dynamic resources and refreshes existing NavItems,
so the menu, all 26 real page headings, the Dashboard tooltip, the Cloud sync Settings tab
and the Own fields record-panel heading use the saved words without a shell rebuild. Partners remains a titleless three-tab container by design. The two-click reset
restores only these 32 labels. Receipts, arbitrary sentences, historical audit text and document
numbers are not rewrite targets.

Seven public-service facts bring the written inventory to **827 cases in 66 files**. All 46 static
checks and 6/6 SQLite storage rehearsal groups pass; the gate was also proved with theme,
missing-heading, wrong-notification, title-as-key and untrimmed-direct-write injections. **There is no .NET SDK here**, so Topaz still needs `ALL GREEN - 827
of 827` and the owner's live rename/restart/reset walk-through. E-12/S-05 stay 🟡 until then.
Merlot's fixed 820-case rerun and Dashboard/PR walk-through remain outstanding. Generic undo,
charts, native `.xlsx` and cloud work were not pulled in.

The owner's pasted **Merlot 808/820** tail was also reviewed against the current source. It exposed
one still-relevant D-93 guidance defect: a positive batch of 5 beside a legacy batch of -3 gives a
positive book total of 2, so `SaleService` omitted the physical-stock-take repair path. The refusal
now checks for an actual active minus batch; ordinary shortages still say reduce/receive, and no
stock, money or audit rule changed. Existing Quote and Create assertions were strengthened without
changing the 827-case inventory. The real-schema rehearsal passed **3/3**, and the static gate now
prepares **768 SQL statements**. This is a source repair, not a claimed Topaz xUnit result.

The owner's first Topaz build then stopped at `AppLabelTests.cs(42)` with **CS1674**: `Database`
does not implement `IDisposable`. The second-database test now uses a plain variable, matching the
existing NumberingTests lifetime pattern. The existing C# static check rejects `using ... new Database`
and caught the fault when it was injected back at line 42. The 827-case inventory and product rules
were unchanged by that repair; Topaz's own rerun was never supplied. Azure, Cinnamon and Paprika followed; Paprika's actual 851-case run failed 14, so Quasar's 853-case run became the next target. The current suite includes those cases in its 860-case target, without marking either old result green.

### What v0.35.10 changes — E-04 catalog management is complete

Products → **Manage categories and companies…** opens one window for create, rename, order,
soft put-away and restore. Categories have unlimited rows, an optional parent and a two-level
maximum enforced in Core; choosing a parent in `ProductService.Search` includes its children.
Append-only migration 026 adds `companies.sort_order`, leaving every existing company, product and
representative value in place. Rename/order/put-away/restore write old/new audit entries in the same
immediate transaction as the change; cashier writes and case-only duplicate names are refused.

Put-away is deliberately **not destructive delete**: only current product references are unlinked.
Products, batches, bills, payments, stock movements and the catalog row itself remain. A parent can
be put away without putting away its children. Restore never recreates old product links. Company
phone, notes, rep name/phone and visit day are not rewritten by name/order management. Quick-add
and the window use the same public ProductService methods, and new product links to inactive rows
are refused. Six focused facts bring the written suite to **820 cases in 65 files**. All 45 static
checks and an 8/8-group SQLite rehearsal (fresh 026 plus a real 025→026 upgrade) pass here; **Windows compilation/xUnit are still for
the owner**, who must see `ALL GREEN - 820 of 820`. D-93 stock behavior and Lapis financial rules
were not changed. Cloud, native `.xlsx`, custom labels, generic undo and charts were not pulled in.

### What v0.35.9 changes — D-93 retires negative stock for new work

`StockPolicy` is the single internal door for ordinary decrements. Inside the caller's immediate
transaction it checks the selected batch and the active product total, then uses a conditional
atomic `UPDATE`; sale, write-off/count-short, owner goods drawing, supplier return and PR cancellation
all call it. The legacy `stock.allowNegative` value remains in old settings rows for compatibility,
but no production code reads it, the checkbox is gone, and there is no owner bypass. A new static
gate rejects a future raw decrement outside this policy (the audited absolute physical count is the
only exception).

Sale planning now reserves repeated products and shared FEFO batches cumulatively before invoice
numbering. A shortage reports available versus requested quantities; the cart stays intact and no
sale/payment/stock/audit row or number survives a failed operation. Exact zero is allowed, including
fractional REAL quantities. Supplier-return and PR-cancellation failures roll back earlier lines too.
PR cancellation remains stock-only: no payment, drawer inflow, negative refund/cost row or repayment
is invented, and cancelled PRs stay excluded from active reports.

No migration rewrites a historical negative to zero. Catalog reads, Stock minus, old sales/returns
and sales reports remain readable; a historical return can still increase stock. Reconciliation is
a read-only review followed by an owner-only, finite, non-negative physical count and audited
adjustment. Lapis PR-link allocations, chronological settlement, integer-paisa immutable financial
history and cumulative return caps are unchanged.

Sixteen cases were added (**814 total, 64 files**) and old overselling fixtures were replaced with
refusal or explicit legacy-negative fixtures. All 45 static checks and a 7/7 SQLite policy rehearsal
with 500 guarded attempts pass here. **There is no .NET SDK here, so Onyx is not compiled or
xUnit-verified yet.** The owner must run `tools\RUN-TESTS.cmd` and see `ALL GREEN - 814 of 814`. Later stages still require another `next`; cloud remains postponed.

### What v0.35.7 changes — bill returns and recovery agree about the money

`CustomerSettlementLedger` is the single chronological replay used by bill-linked returns, recovery
ageing and recovery slips. Collections pay the oldest bill that existed at that moment; a return's
khata reduction stays on its own bill. Later collections shift only the remaining refundable value
from khata to cash, so earlier refunds cannot be claimed again. Recovery reprints stop at their own
payment and exclude later bills; carried-in khata is handled without an invented bill date.

The Returns page no longer calls every remaining line value "cash". `RefundablePaisa` means total
cash-plus-khata value, and a final packed-unit remainder stores its own remaining entered quantity.
Seven regressions were added (**789 total**). No schema or historical sale/payment/return row changed.
Static checks and five SQLite chronology rehearsals passed here. The owner's subsequent Windows
run compiled Core and Tests and reported **Failed 0, Passed 789, Skipped 0, Total 789 in 1 m 6 s —
ALL GREEN**. This is prior verified evidence, not a claim that Onyx has run on the owner's PC.

### What v0.35.6 changes — provisional money evidence

Migration 024 adds the provisional payment link, line refund shares and cost-at-return snapshots.
The write path records cash out once; multi-line headers use equal integer-paisa shares with the
remainder in line order (there is no reliable no-bill line price to invent). Cancellation removes the
returned stock but never creates a cash-in payment. Only pending/linked PRs feed the sales/profit read view, so a cancelled document
does not remain as a bogus refund or become a negative cash/profit transaction. The dashboard,
reports, tax list and books check use the same rules. Ten regressions were added (782 total).
Cedar was subsequently compiled and run on the owner's Windows PC: **Failed 0, Passed 782,
Skipped 0, Total 782 in 1 m 3 s — ALL GREEN**. That is prior evidence, not Cypress's result.

### What v0.35.5 changes — the three failures from the real 772-case run

The owner's v0.35.4 run was on the correct tree and ended **769 passed, 3 failed, 772 total in 55 s**.
`SessionPolicy` no longer lets the idle choice supply the countdown while the sign-in timestamp is
ahead of the PC clock; the safe hard-cap answer remains until the timeline catches up. Two Shift tests
had made a sale through a helper that already opens a drawer and then tried to open it again. They now
open the drawer before the sale. The production rule refusing a second open shift was **not** weakened,
and no schema or test count changed.

### What v0.35.4 changes — the launcher accepts this working branch

`START-AREA11.bat` accepted only `main` and a retired Arena branch, so it refused the correct
`arena/60395e95-area11a` copy before building. The active `WORK_BRANCH` is now named once and accepted
beside `main`; other branches remain refused. **No business rule, schema or test-count change.**

### What v0.35.3 changes — document numbering without a stale cache

1. `NumberingService` reads each counter on the caller's live connection inside the immediate
   transaction. A second settings instance no longer issues the number its startup cache remembered.
2. `WriteCounter` does not mutate that cache, because SQLite rollback cannot roll back a memory write.
   A failed document therefore no longer leaves the next in-memory number ahead of the database.
3. Three public-service regression tests cover independent instances, rollback and repeated
   quarantine numbers in one transaction. **772 cases, no schema change; not compiled here.**

### What v0.35.1 got wrong — both were real, both are fixed in v0.35.2

1. **4 red tests out of 769** (`Failed: 4, Passed: 765`), all four in `RunTrailTests`.
   `RunTrail.Step` wrote each step with `File.AppendAllText`, which adds **no line terminator**, so
   every step glued onto the previous one: his `%TEMP%\Area11\last-run.txt` held one enormous line
   (`database ready2026-10-23 14:56:05  login window shown…`). The **code** was wrong, the tests were
   right. It now calls `File.AppendAllLines` — one step, one line.
2. **The installer stopped** after `Print spooler: running.` with
   *"Parameter set cannot be resolved using the specified named parameters."* — a PowerShell binding
   message that named neither the line nor the command. The icon block and the *Add or remove
   programs* block now use plain .NET (`Directory.EnumerateFiles`, `Registry.LocalMachine.CreateSubKey`,
   `SetValue`), each inside its own `try/catch` that prints a sentence and carries on, and the
   top-level catch prints **`Where : line N — <the command>`** so the next stop names itself.

### If anything changes after this handover — say it in the repository, not only in the chat

The owner's habit is to close one chat and open another, so **the repository is the only memory**. Every
time you find a fault, finish a task, change a decision or learn something that cost time:

1. fix it (code first, never by weakening a test);
2. write it down here and in the file that owns that fact — board `docs/PROGRESS.md`, decision
   `docs/OWNER-DECISIONS.md`, release `docs/VERSIONS.md`, traps §11 of this file;
3. push it, and merge it to `main` (open a pull request from your branch if you worked on one) - a
   commit that exists only on a side branch is invisible to the next agent;
4. answer the owner in one message, in his language, saying what he must run and what he should see.

**The v0.35.5 suite is green on his PC: 772 of 772 in 56 s.** Keep that evidence; do not ask him to
re-prove a pulled tree unless the code changes. The earlier `AlertService.Report()` mapping report was
investigated against this exact source: `qty_base` is SQLite `REAL`, `ProblemRow.Qty` is C# `double`,
and the green suite exercises all seven alert sources, including low-stock REAL quantities 1.0 and 2.0.
No current mismatch was found, so no production cast was invented. A recurrence on v0.35.5 or later
needs a fresh report from that version. The Desktop installer still needs its own run:

> After the next code repair, ask him to pull and run `tools\RUN-TESTS.cmd` again (needs `Failed: 0`,
> with the new expected total). For installation, run `tools\INSTALL-AREA11.cmd` (needs the desktop
> icon **and** Area11 listed in *Add or remove programs* with its version).

---

## 4. What is left — exactly

**Immediate authorized task:** build Aurora on Windows, require **`ALL GREEN - 892 of 892`** and complete
**PT-09** for Phase-2 field governance/schema. Do not start Phase 3 or infer a WPF pass from the 54 static
checks. Zircon's 883/883 is previous-release evidence. The five existing 🟡 feature rows below remain
separate owner-action evidence; their counts are not changed by this redesign.

**No feature is ⬜.** The five 🟡 rows are code-complete and await **one action in the real shop**:

| ID | Feature | The single action that closes it |
|---|---|---|
| **F-08** | Products: search, create, edit, packing | owner adds **one medicine with a batch** on his PC |
| **F-09** | Purchases `PINV` | **one real delivery** enters stock |
| **F-10** | Counter `INV` | **one real bill** prints on the shop printer |
| **F-04** | Audit black box | **one real action** is seen in the black box |
| **F-46** | Printer & scanner test | the slip half is confirmed; **scan one box** and see the record in the black box |

Everything else on the board is ✅. "What is left" is therefore mostly **proof, polish and the
walk-throughs in `HANDOFF.md` §4** — not missing modules. The unproven ones, in the owner's terms:

1. every page under v0.34.0 / v0.35.x (empty-page cards, the fault card, the heartbeat, the
   failed-button line, the drawer card, the session warning);
2. backup & restore, Excel/CSV import, photos, own fields, several barcodes, doctors & reps,
   second counter over the LAN, tax report, cloud sync, bills + reprint, misspelt search, printer
   test, installer;
3. the three rules of v0.35.0 (D-92): **one time for the shop** (online first, PC clock only as a
   fallback), **the session ends by itself** (Settings choice, 12-hour ceiling), **no bill while the
   galla is closed** (the sell screen offers *Open a galla now*).

**Four of the owner's 116 numbered rules are not fully finished** — this is the one place the rule
matrix and the feature board differ, and both are telling the truth: every *feature* exists, but these
*rules* have a piece missing. `docs/RULES-MATRIX.md` is generated from
`tools/build_rules_matrix.py`; change the `STATUS` map there and regenerate, never edit the matrix.

| Rule | What is missing |
|---|---|
| **E-12** 🟡 | the 32-label Settings → App words editor, Core guards and audited reset are written; Windows and the live owner walk-through are still missing |
| **S-05** 🟡 | stable-key menu/page refresh is written for all supported headings; immediate propagation, restart persistence and reset still need the same owner proof |
| **Z-05** 🟡 | the selected-row earlier-state reader and safe correction guidance are written; Windows/xUnit and the live selection/filter-clearing walk-through are still missing. Universal financial undo is deliberately not built |
| **T-10** 🟡 | native `.xlsx`, four-sheet templates, eight-sheet download and safe bulk update are written; Windows/xUnit and real Excel open/edit/apply/re-import proof is still missing |

**T-05 is ✅ as of Cinnamon:** the spec's per-day native WPF bar chart already existed; actual-code review corrected the stale no-chart entry and added regression guards, not another chart package.

Known, deliberate limitations (not bugs): SQLite on a share is supported for **two** counters only
(a third needs the small `Area11.Server` named in the spec §34.9 — a separate future project);
WPF cannot decode `.webp` photos; cloud sync is optional and never deletes a row.

---

## 5. Your first hour

```bash
git clone https://github.com/Dr-Adil-Abdullah/AREA11a.git
cd AREA11a                               # main is the default branch - the product is already here
pip install --break-system-packages tree_sitter tree_sitter_c_sharp   # gives the real C# grammar
python3 tools/check_repo.py              # must end: OK - 54 checks, no problems
python3 tools/build_rules_matrix.py --check
```

Then read, in this order: this file → `AGENTS.md` → `docs/PROGRESS.md` (the board) → `HANDOFF.md` §4
(the walk-throughs) → `docs/OWNER-DECISIONS.md` (at least D-77, D-88 … D-97) → the code you are about
to touch.

**How work reaches `main` from now on.** `main` is protected only by habit, not by a rule: work on a
branch of your own (`git switch -c fix/<the-fault>`), commit there, push it and open a pull request -
the owner merges it with one click, or ask him and merge it yourself once his console shows it green.
**Never force-push and never rewrite `main`'s history.** The old branch
`arena/a8a3b2e0-area11a` is kept as a pointer to the same commit; do not work on it.

---

## 6. The owner — how to work with him

* **Answer in the language of his message.** Pure Urdu or pure English; **Roman Urdu is forbidden**,
  even when he writes in it.
* **He is not a programmer.** Explain the idea in plain words before the mechanism. One screen, one
  action at a time; tell him exactly what to double-click and what he should see.
* **Finish first, then answer once.** He dislikes a stream of half-finished reports: do the work,
  self-check it, then send **one** message. Give him an ETA when he asks for one.
* **Say the near-done first, then what is left, with percentages.** He asks for this constantly.
  Percentages come from the board: 57 features, 52 done = **91.2 %**; 5 in progress = **8.8 %**.
* **Questions only when a decision blocks progress — and never twice about the same thing.** Use the
  exact form:
  `سوال / Question:` then `ممکنہ حل / Possible Options:` **A / B / C / D**, every option a workable
  solution (never "whatever you prefer"). Put all questions in one message.
* **Warn about money risk explicitly**: «اس فیچر میں یہ نقصان ہے» — and name the accounting or
  business risk, not a technical one.
* **Record every answer** in `docs/OWNER-DECISIONS.md` as the next `D-xx`, then follow it — even when
  it contradicts your preference.
* **Honesty is the currency.** Never say "it builds", "it works" or "done" unless it was proved on a
  machine that can compile. Say what is proven by what: his `BUILD OK`, his green suite, the static
  checker, or a test case that has only been *written*.
* **He pastes console output.** Fix the **first** error, not the cascade. Never edit a test to make
  it pass; if the expectation itself was wrong, say why in `docs/VERSIONS.md`.
* **He is in a hurry — and correctness beats speed.** No error may remain: the shop's money depends
  on it.

### His machine

Windows 10 (19045), Visual Studio 2026, .NET SDK **10.0.402**, runtimes 10.0.13 + 8.0.32. His clone:
`C:\Users\Dr.Iqbal\Documents\area11a\area11a1\AREA11a` — **he must `git pull` before every test**.
Diagnostics live in `%TEMP%\Area11\`: `last-run.txt` (how far the run got; capped at 140 lines),
`latest-crash.txt`, `binding-errors.txt` (its **absence is the healthy answer** — and the trail is what
makes that absence mean something). `tools\SEND-REPORT.cmd` collects everything into one text file.

---

## 7. Non-negotiables (breaking one is a bug, not a style choice)

1. **Money is `long` paisa; quantities are `double`.** Never `decimal` — Microsoft.Data.Sqlite turns it
   into TEXT and `SUM()` breaks.
2. **A bill's total is the sum of its line totals.** Round once (`Money.RoundDownWithCostGuard`), then
   spread the difference; never re-round afterwards.
3. **Round down only, never below purchase cost** (his Z-11).
4. **D-93: no new decrement may take stock below zero.** Sales, write-offs, goods drawings, supplier
   returns and PR cancellation use `StockPolicy`; there is no setting or owner bypass. Historical
   negatives remain readable and are corrected only by an audited physical stock-take, never silent zeroing.
5. **Permissions are enforced in `Area11.Core`**, keyed on the signed-in `User`. Hiding a button is
   not a permission.
6. **Nothing is ever deleted.** Business rows use `active = 0`; the audit trail and sales are permanent.
7. **Migrations are append-only**, run inside `BEGIN IMMEDIATE`, and re-check `_migrations` after the
   lock. Add `029_*.sql` for the next change — never edit an existing file.
8. **Views contain no SQL and no business logic.** Services own every rule and transaction; a service
   method takes `User` when the rule depends on permissions.
9. **Everything editable comes from `settings`** — shop name, wording, limits, printer width, colour
   (his E-01).
10. **Never modify the original Next.js app**; it is a reference only.
11. **No demo data. Ever.** The sample shop was deleted on his word (D-77). No `--demo`, no
    `DEMO-AREA11.cmd`, no invented rows, and **`C:\ProgramData\Area11` is never deleted by an agent.**
12. **Three projects only** (`Core`, `Desktop`, `Tests`). No fourth.
13. **Every code change changes the app colour** (his D-24) — see §9.
14. **Every change reaches GitHub**, code and documents together.

---

## 8. How a feature goes in

```
Area11.Core/Services/XxxService.cs   ← rules + SQL + transactions, returns ServiceResult/ServiceResult<T>
Area11.Core/Models/XxxModels.cs      ← plain records; one file per feature
Desktop/AppServices.cs               ← construct it (the composition root; views never see a DbConnection)
Desktop/ViewModels/XxxViewModel.cs   ← state + commands; no business rules
Desktop/Views/XxxView.xaml(.cs)      ← drawing only
ShellViewModel.NavItems              ← one entry, guarded by Permissions.Can(user, Modules.Xxx)
ShellWindow.xaml                     ← one DataTemplate mapping the view-model to the view
Area11.Tests/XxxTests.cs             ← through the public services and a real bill in TestDatabase
docs/PROGRESS.md                     ← the board row: state, files, test, "done when"
```

`Area11.Tests` may reference **only `Area11.Core`** — tests must use public code. `TestDatabase` is a
temporary SQLite database with all 29 migrations and seed data; `TestData` opens a drawer so that every
bill in the suite goes through the real galla door.

---

## 9. Releasing a version — follow this literally

The owner keeps several copies of the folder and identifies a build **by its colour**. Nothing ships
without both a new number and a **new colour with a codename that has never been used**.

**Colour rules** (all were enforced by measurement, not taste): at least **4.5:1 on white**; at least
**20 apart** (Euclidean RGB distance) from every hex already in `Theme.xaml` and `docs/VERSIONS.md`;
brand↔dark at least **40** apart; the soft tint must keep **brand text ≥ 4.5:1** on it; avoid neon and
avoid `#047857` (SuccessBrush) and `#B45309` (WarningBrush) — a colour may not mean two things.
**Never reuse a codename** — v0.35.1 repeated *Ultramarine* from v0.5.0 and that mistake is recorded as
a mistake.

```python
def rgb(h): h = h.lstrip('#'); return tuple(int(h[i:i+2], 16) for i in (0, 2, 4))
def lum(h):
    f = lambda c: (c/255)/12.92 if c/255 <= 0.03928 else (((c/255)+0.055)/1.055)**2.4
    r, g, b = rgb(h); return 0.2126*f(r) + 0.7152*f(g) + 0.0722*f(b)
def contrast(a, b):
    la, lb = sorted((lum(a), lum(b)), reverse=True); return (la+0.05)/(lb+0.05)
def distance(a, b):
    return sum((x-y)**2 for x, y in zip(rgb(a), rgb(b)))**0.5
```

**The bump list — eight places, all of them:**

| # | File | What changes |
|---|---|---|
| 1 | `dotnet/Area11.Core/Utils/BuildInfo.cs` | `Version`, `CodeName`, `BrandHex`, `BrandDarkHex`, `BrandSoftHex`, `ReleasedOn` |
| 2 | `dotnet/Directory.Build.props` | `<Version>` |
| 3 | `dotnet/Area11.Desktop/Resources/Theme.xaml` | `BrandBrush`, `BrandDarkBrush`, `BrandSoftBrush` |
| 4 | `START-AREA11.bat` | `set "BUILD=v0.xx.y (Colour)"` |
| 5 | `tools/RUN-TESTS.cmd` | `BUILD=` — and `EXPECTED=` if the number of test cases changed |
| 6 | `tools/SEND-REPORT.cmd` | `BUILD=` |
| 7 | `python3 tools/make_icon.py` | regenerates `Desktop/Resources/area11.ico` in the new colour |
| 8 | `docs/VERSIONS.md` | one new row at the **end of the table** (rows ascend; the newest is **last**) |

Then the documents that must not drift: `docs/PROGRESS.md` (the row + a change-log line),
`docs/OWNER-DECISIONS.md` (its «دوبارہ نہ ہو» colour sentence), `HANDOFF.md` §4, `AGENTS.md` §8, and
`python3 tools/make_checklist.py` if the board changed.

```bash
python3 tools/check_repo.py          # OK - 49 checks, no problems
python3 tools/build_rules_matrix.py --check
git add -A && git commit -m "v0.xx.y Colour - <what a person would notice>" && git push
```

**Then verify the push, not the disk:** download the branch zip from
`https://codeload.github.com/Dr-Adil-Abdullah/AREA11a/zip/refs/heads/main`, unpack it
in `/tmp`, and run the checker inside it. Only then tell the owner.

**A red test is fixed in the code, never by weakening the test.** If the expectation itself was wrong,
`docs/VERSIONS.md` says why the old number was wrong — every changed expectation, every time.

---

## 10. What you can and cannot prove here

This sandbox is Linux with **no .NET SDK and no PowerShell**. That means:

* you **cannot** build the WPF app, run the tests, run `install.ps1`, or open a window;
* you **can** run `python3 tools/check_repo.py` — 49 checks that parse every C# file with the real
  tree-sitter grammar, resolve explicit usings, validate XAML/csproj XML, every binding against its
  view-model, every `StaticResource` key, every SQL statement against the real schema, every mapped
  column, constructor arity, migration execution on SQLite, version/colour/VERSIONS agreement, docs
  links, batch-file blocks and labels, and the test inventory;
* a green checker is **not** a compile. v0.34.0 shipped an `CS1503` that the checker could not see and
  the owner's build caught in one line. The static checker resolves **names**, not **types**.

So: the owner's `BUILD OK` and green suite are the only proof of compilation. Ask for the **whole**
console, fix the first error, and never claim more than you measured.

---

## 11. Traps that already cost time — do not repeat them

* `File.AppendAllText` writes **no line end** — every step glued together (v0.35.1, four red tests).
* `Safe(` in `RunTrail` takes a `Func<string>` — every call site must pass a lambda (v0.35.1 CS1503).
* PowerShell `New-ItemProperty`/`New-Item` binding errors name nothing; plain .NET + `Where : line N`
  is now the rule (v0.35.2). There is no PowerShell in this sandbox — reason about parameter sets, do
  not guess and do not delete the block.
* `DataGridTextColumn` needs `IsReadOnly="True"`; `Run.Text` binds **two-way** by default — 179
  bindings were made `OneWay` after a crash round.
* `Area11.Tests` references only `Area11.Core`; tests may not name an `internal` type.
* `ObservableObject` has no `RaisePropertyChanged`; `ShellViewModel.SelectedNav` no-ops on the same
  value; `SaleService.Create` returns `ServiceResult<SaleResult>`.
* `Directory.Build.props` lives in `dotnet/`, not at the root.
* `docs/VERSIONS.md` rows **ascend** — append the new row after the previous newest.
* `python3 tools/make_checklist.py` must be run after editing the board, or check #43 fails.
* `MSB3027` / `MSB3021` are **file locks** ("the program is still running"), not compiler errors.
* Do not reach for `Math.Round` anywhere — money goes through `Calc` / `Money`; the checker rejects it.
* A test that applies a real ≈2-day clock offset poisons parallel tests — hence `LargestAcceptedOffset`.
* The build cannot replace a DLL a running Area11 has loaded: close the app (the launcher does it).

---

## 12. Data safety

The shop's real data is the only irreplaceable thing in this project.

* `C:\ProgramData\Area11` is **never deleted, reset, "cleaned" or pointed at by a test.**
* Tests use in-memory SQLite (`TestDatabase`); nothing in the suite touches the shop folder.
* A destructive operation (restore, import, stock-take posting) must be previewed, backed up first,
  and run in one transaction.
* Never commit a `.db`, a photo, a log, `bin/`, `obj/` or `dist/`.
* If the owner asks to "start fresh", the answer is a **backup + a new Windows account or another PC**,
  never deleting his folder.

---

## 13. Definition of done, and how to answer him

A round is done when: the checker is clean; a test covers the rule you touched (or the board says why
it cannot); `docs/PROGRESS.md` is updated; the rules matrix is regenerated; `HANDOFF.md` §4 names the
real next task; the commit message names the version and the feature; it is pushed.

The answer to the owner is **one message**, in his language: what changed (one line each), exactly
what he must do (numbered commands and what he should see), what is left with percentages, and an
honest caveat about what is not yet proven. If he asks for a printable status, regenerate
`docs/CHECKLIST-URDU.html` — it is built from the board and cannot drift.

---

## 14. If you get stuck

1. Read the checker's message — it names the file and the reason, and it was written to be read.
2. Ask for `%TEMP%\Area11\last-run.txt` and `latest-crash.txt`; they say **how far** a run got.
3. Change one thing, re-run, commit. Do not stack guesses.
4. Roll back with `git fetch origin && git reset --mixed origin/main` — **never**
   `--force`.
5. When a decision belongs to the shop, ask the owner in the MCQ form and record the answer as the
   next `D-xx`.

---

## 15. Appendix — the important files

| Path | What is in it |
|---|---|
| `dotnet/Area11.Core/Data/Database.cs`, `StoragePlan.cs`, `Migrations/001…026` | connection, WAL / share mode, the schema |
| `dotnet/Area11.Core/Services/` (51) | every rule: `SaleService`, `PurchaseService`, `ShiftService`, `SettingsService`, `AuthService`, `PermissionService`, `ReportService`, `BackupService`, `InstallService`, `SyncService`, `HardwareService` … |
| `dotnet/Area11.Core/Utils/` | `Money`, `Calc`, `AppPaths`, `BuildInfo`, `RunTrail`, `CommandGuard`, `TimeSource`, `NtpTime`, `SessionPolicy`, `SessionWatch`, `SessionHandover`, `ShopConfig`, `CrashLog` |
| `dotnet/Area11.Desktop/AppServices.cs` | the composition root — a new service is constructed **here** |
| `dotnet/Area11.Desktop/ViewModels/ShellViewModel.cs` | the frame, the menu, the session, the fault banner |
| `dotnet/Area11.Desktop/Views/` (37) | the pages; `PosView`, `ProductsView`, `PurchasesView`, `ShiftView`, `HardwareView` … |
| `dotnet/Area11.Desktop/Resources/Theme.xaml` | the three brand brushes |
| `dotnet/Area11.Tests/` (67 test files) | `TestDatabase`, `TestData`, one file per feature |
| `tools/check_repo.py` | the 52 static checks (read the docstring of a failing check) |
| `tools/make_checklist.py`, `make_icon.py`, `build_rules_matrix.py` | the printable list, the icon, the rules matrix |
| `tools/*.cmd`, `tools/install/*.ps1` | what the owner double-clicks: start, update, test, report, install, uninstall, share |
| `docs/` | the spec, the decisions, the board, the rules, the schema, the guides |

---

## 16. The first message to send him (copy, paste, adjust)

> میں نے ریپو پڑھ لی ہے۔ ابھی حالت یہ ہے: **v0.35.19 (Spruce)** — 883 written cases اور 52 static checks مکمل ہیں۔
> Spruce `94723e5` کا Windows xUnit مکمل **ALL GREEN - 883 of 883** ہو چکا ہے (1 منٹ 19 سیکنڈ)۔ Obsidian کا پرانا **860/860، 1 منٹ 17 سیکنڈ** نتیجہ تاریخی ثبوت ہے؛ WPF اور genuine physical workflows الگ باقی ہیں۔
> Backup/shift/alert، PRICE-01/SALE-01 اور CAT-01/NAV-01/UNIT-01/PHOTO-01 کی authorized source fixes لکھی جا چکی ہیں؛ Windows xUnit green ہے، physical verification الگ ہے۔ اب ان workflows کی genuine testing کرنی ہے؛ PT02-POS-01 کے لیے exact reproduction لازم ہے۔
> اصل books میں صرف genuine transactions کریں؛ restore/fault scenarios الگ test copy میں۔ `next` پر اگلا physical-test portion دیں۔
> Real Excel اور App words/Black-box walk-throughs بھی باقی ہیں؛ cloud الگ test Shop id/table اور test database کے بغیر نہیں۔
