# Phase 4 — source evidence register, portion 1 (v0.79.0 Tern)

> **Latest: v0.80.0 portion2.** This initial portion1 register is retained with current table corrections. Settings0 reachability and Fields7 clamp are now fixed in SOURCE (Windows pending); native-field active-owner and two explicit-profit projection gaps fixed. Method-level progress / remaining high-priority actorless ledger/catalog/cost/session gaps: [portion2 access register](POS-PHASE4-ACCESS-REVIEW.md).1337 written cases,68 static checks; no C#/WPF execution. The chronological portion1 validation below stays historical.

**Date:** 2026-11-10. **Status: IN PROGRESS, not a full audit PASS.** No .NET SDK / Windows runtime on this machine. F = focused source review + bounded fix + written, UNEXECUTED C# regressions. T = route/call/data/gate trace only; not all methods or all 15 dimensions reviewed. Prior phase evidence is explicitly inherited, not rerun C# evidence.

## Navigation inventory — 26 routes (25 AddPage + wordmark Dashboard)

Source: `dotnet/Area11.Desktop/ViewModels/ShellViewModel.cs` `BuildNavigation`, `AppServices.cs` wiring, corresponding Views/ViewModels; Core paths below are `dotnet/Area11.Core/Services/*Service.cs`. View stems are `<stem>View.xaml`, VM stems `<stem>ViewModel.cs`. Partners delegates Customers/Recovery/Suppliers; Settings/Backup/Books use factories. Route visibility is not service authorization. Table data column names the affected data families, not an exhaustive SQL inventory.

| Route | View / ViewModel stem | Core services | Data / side effects | Gate traced | Guide lessons | Evidence / remaining |
|---|---|---|---|---|---|---|
| dashboard | Dashboard | Dashboard | sales/payments/stock aggregates | unrestricted route; monetary fields service-filtered | 02 | T: wordmark route, not separate top tab; report projection deep review pending |
| counter | Pos | Sale/PricingAllowance/HoldCart/Draft | sales/items/payments/batches/allowances | Pos + live pricing allowances | 09–11,30 | T + prior phase2/3 evidence; no new arithmetic change; HasOpenDrawer error policy retained |
| bills | SaleHistory | SaleHistory/Receipt/TimeCorrection | stored sales/returns/audit | Sales; time correction separate role guard | 12,35 | T: historical rendering calls traced; full reprint/access matrix pending |
| returns | Returns | Return/Quarantine/ReturnReceipt | returns/items/quarantine/payments | Returns | 13 | T + prior B evidence; full quarantine concurrency pending |
| provisional | Provisional | ProvisionalReturn/ProvisionalReceipt | provisional returns/links/payments | Returns | 14 | T: already-paid no-bill excess remains a known limit; no retrospective cash rewrite |
| products | Products | Product/Photo/Barcode/CustomField | products/batches/photos/catalogs | Products; Product.Create uses actor.CanManage | 04–07 | F for shared photo paths only; product/cost projection role matrix pending |
| reorder | Reorder | Reorder/Photo | products/batches/reorder orders | Reorder | 19 | T; photo bulk cover now shares safe resolver; order duplication pending |
| purchases | Purchases | Purchase/Draft | purchases/items/batches/supplier ledger | Purchases | 08,11 | T; receiving totals/mixed-payment fault review pending |
| writeOff | WriteOff | Stock | batches/stock movements | Stock + role/policy caps | 20 | T; allocation and race tests pending |
| stockMinus | StockMinus | StockMinus | negative-stock review records/batches | Stock | 20 | T; review acknowledgement mutation pending |
| stockTake | StockTake | StockTake | count sessions/lines/movements/batches | StockTake | 21 | T; multi-window stale count apply pending |
| partners | Partners | CustomerLedger/Recovery/SupplierLedger/Partner | customers/suppliers/ledger/payments/returns | Customers and Suppliers tabs independently gated | 15–17 | T: container verified; Partner.CreateCustomer validation read, not full write authorization audit |
| doctors | Doctors | Rep | doctors/reps/sales profit | Products or Customers navigation; each block separately gated; profit visibility separate | 18 | T; direct profit query projection pending |
| shift | Shift | Shift/HandoverSlip | shifts/payments/handovers | Cash + per-operation role rules | 03 | T; transaction/race and drawer report failure review pending |
| expenses | Expenses | Expense/ExpenseCategory | expenses/drawings/categories | Cash + drawing/expense role rules | 22 | T; stock tag is label only, not stock receiving; broader money audit pending |
| reports | Reports | Report | sales/returns/expenses/ledger aggregates | Reports; Profit separate | 23 | T + prior Extra evidence; per-report role matrix pending |
| tax | Tax | Tax | monthly sale/return tax snapshots | Reports | 23 | T; compatibility retained, no sales-tax policy introduced |
| audit | Audit | AuditHistory | audit_logs | Audit | 25 | T; read-only history, no universal undo; payload/export disclosure pending |
| importExport | ImportExport | Import/Workbook | catalogs/partners/settings/allowlists | Import plus service-specific guards | 26 | T: service entrypoints traced; parser size/formula/SQL matrix pending |
| backup | Backup | Backup/Settings | database/photos/settings/staged rollback files | Backup | 27 | T: ExtractPhotos full/relative containment inspected; prior restore gate retained; recovery drill pending |
| lan | Lan | Lan | connection configuration/database path | Owner navigation; service matrix pending | 34 | T; second counter deferred, folder != isolated database |
| cloud | CloudSync | Sync/CloudTransport | document/table allowlists/cloud mirror | Sync/CanSync | 34 | T: transport headers/timeout and document flow read; remote RLS/credentials not verified |
| customFields | CustomFields | CustomField | field definitions/values | Settings | 32 | T; dynamic value mutation/attachment role matrix pending |
| hardware | Hardware | Hardware/Printer/Barcode | printer/scanner settings | Settings navigation; Hardware service gate; physical devices pending | 29 | T; no printer/scanner pass inferred |
| settings | Settings | Settings/Auth/Permission/PricingAllowance/Startup | settings/users/permissions/pricing allowances/audit | Settings + owner-only staff/pricing + Sync | 28–34 | F for owner write/recovery boundary; generic settings/session defaults not comprehensively fixed |
| books | BookCheck | BookCheck | read-only reconciliation SQL | Books.CanRun | 25 | T + SQLite proof of actual Extra reconciliation SQL; not all finance certified |

## Settings inventory — 8 top-level contents + 2 nested Fields sections

Source: `Views/SettingsView.xaml` top-level TabItems and `ShellViewModel.BuildSettingsTabs`; `SettingsViewModel.Save`/staff commands → Core methods below → settings/users/permissions/allowances/audit. Headerless SettingsView relies on shell indices.

| Index / section | ViewModel → service → data / permission | Lessons | Status / evidence |
|---|---|---|---|
| 0 App name & icon | Settings → Settings.SetMany; settings / active owner for brand.appName and appearance.iconFileName | 28 | F owner write boundary; portion2 SOURCE FIX: shell section0 added; all indices/clamp0–7 now agree, Windows reachability pending |
| 1 Shop | Settings → Settings.SetMany; shop/contact/receipt/logo/product-photo settings, Settings gate | 28 | T; app branding distinct from receipt branding |
| 2 Printing | Settings → Settings.SetMany, Printer; printer/paper/copies/receipt controls | 29,35 | T; real preview/paper/device pending |
| 3 Sales rules | Settings → Settings.SetMany; discount.billBasis live-owner check; sale rules re-read by services | 30 | F for basis write boundary; prior phase2/3 finance retained |
| 4 Cloud | Settings → Settings.SetMany, Sync.CanSync; sync address/key/table/shop id | 34 | T; masked input does not imply encrypted storage, remote RLS pending |
| 5 Staff & permissions | Settings → Auth.CreateStaff/SetSecret/SetActive/SetRecoveryWord, Permission.Set/Reset, Pricing.Save; users/permissions/allowances | 31 | F owner DB row checked inside transaction; recovery hash+nonsecret audit atomic; pricing already live-owner gated |
| 6 App words | Settings → AppLabel.Validate/Reset, Settings.SetMany; labels.* / Settings gate | 31 | T; labels permission preserved, not generalized settings security proof |
| 7 Fields: Built-in fields | NativeFieldsView/VM → NativeFieldPolicy.List/SetRequired → field policy | 32 | portion2 F: live active-owner reads/writes; current-name audit/ack, critical confirmation retained; C# pending |
| 7 Fields: Extra fields | CustomFieldsView/VM → CustomField.* → definitions/values / Settings | 32 | T; same service as navigation customFields; not obsolete removal |

## Supplemental surfaces and all 36 guide lessons

Every lesson is mapped below. Mapping means source address identified, NOT every sentence/action tested. No screenshots fabricated. All Windows/UI/physical statuses remain pending.

| Lesson | Source surface beyond the route inventory | Status |
|---|---|---|
| 01 | LoginView/LoginViewModel → Auth.Login/RecoverOwner; SeedService | F for bounded changes only; remainder T |
| 02 | ShellView/ShellViewModel → BuildNavigation | T; detailed reconciliation/Windows proof pending |
| 03 | ShiftView/VM | T; detailed reconciliation/Windows proof pending |
| 04 | ProductsView/VM search | T; detailed reconciliation/Windows proof pending |
| 05 | Product editor → ProductService | T; detailed reconciliation/Windows proof pending |
| 06 | CatalogWindow/UnitWindow → ProductService catalogs/units | T; detailed reconciliation/Windows proof pending |
| 07 | PhotoGallery/BarcodeList/ProductHistory → Photo/Barcode/ProductHistory | F for bounded changes only; remainder T |
| 08 | PurchasesView/VM | T; detailed reconciliation/Windows proof pending |
| 09 | PosView/VM | T; detailed reconciliation/Windows proof pending |
| 10 | PosView/VM → Sale.CounterWarnings | T; detailed reconciliation/Windows proof pending |
| 11 | Pos/Purchases/DraftsViewModel → HoldCart/Draft | T; detailed reconciliation/Windows proof pending |
| 12 | SaleHistory/TimeCorrection → SaleHistory/TimeCorrection | T; detailed reconciliation/Windows proof pending |
| 13 | ReturnsView/VM → Return/Quarantine | T; detailed reconciliation/Windows proof pending |
| 14 | ProvisionalView/VM | T; detailed reconciliation/Windows proof pending |
| 15 | CustomersView/VM → CustomerLedger/Partner | T; detailed reconciliation/Windows proof pending |
| 16 | RecoveryView/VM → Recovery/CustomerLedger | T; detailed reconciliation/Windows proof pending |
| 17 | SuppliersView/VM → SupplierLedger | T; detailed reconciliation/Windows proof pending |
| 18 | DoctorsView/VM → Rep | T; detailed reconciliation/Windows proof pending |
| 19 | ReorderView/VM | T; detailed reconciliation/Windows proof pending |
| 20 | WriteOff/StockMinus views/VMs | T; detailed reconciliation/Windows proof pending |
| 21 | StockTakeView/VM | T; detailed reconciliation/Windows proof pending |
| 22 | Expenses/ExpenseCategoryManager → Expense/ExpenseCategory | T; detailed reconciliation/Windows proof pending |
| 23 | Reports/Tax views/VMs | T; detailed reconciliation/Windows proof pending |
| 24 | AlertsView/VM → Alert | T; detailed reconciliation/Windows proof pending |
| 25 | Audit/BookCheck views/VMs | T; detailed reconciliation/Windows proof pending |
| 26 | ImportExportView/VM → Import/Workbook | T; detailed reconciliation/Windows proof pending |
| 27 | BackupView/VM → Backup | T; detailed reconciliation/Windows proof pending |
| 28 | Settings index0/index1 → Settings; icon picker | T; detailed reconciliation/Windows proof pending |
| 29 | Settings index2, HardwareView/VM → Printer/Hardware | T; detailed reconciliation/Windows proof pending |
| 30 | Settings index3, Pos → Settings/Sale/PricingAllowance | T; detailed reconciliation/Windows proof pending |
| 31 | Settings index5/index6 → Auth/Permission/PricingAllowance/AppLabel | F for bounded changes only; remainder T |
| 32 | NativeFields/CustomFields/CustomFieldsEditor | T; detailed reconciliation/Windows proof pending |
| 33 | Settings/Startup, FocusGuardService, login/shell window close paths | T; detailed reconciliation/Windows proof pending |
| 34 | CloudSync/Lan views/VMs → Sync/Lan | T; detailed reconciliation/Windows proof pending |
| 35 | PrintPreviewView/VM → Printer, WhatsAppService | T; detailed reconciliation/Windows proof pending |
| 36 | Safe practice instructions; AppPaths/Database/Backup + support tools | T; detailed reconciliation/Windows proof pending |

## 15-dimension evidence / real remaining checklist

| # | Dimension | Evidence inspected this portion / inherited boundary | Still pending |
|---|---|---|---|
| 1 | Money/rounding | No arithmetic code changed; prior phase2/3 guards and SQLite proof inherited | Cross-page invalid amount/pack/split/payment review and executed tests |
| 2 | Discount order | Prior approved modes/Extra retained; basis setting owner check tightened | Every quote/post/report consumer reconciliation |
| 3 | Actual cost | Prior FEFO floor retained, owner has no bypass | Purchase/return/stock adjustment historical-cost edge matrix |
| 4 | Bills/returns | Prior B560+540, unchanged item snapshots; PR exception recorded | Full PR/link/partial-credit/payment scenario audit |
| 5 | Stock/atomicity | New owner/secret/recovery writes transaction-checked; trigger-failure regressions written | Sale/purchase/count/return concurrency and all rollback paths |
| 6 | Duplicate execution | Prior POS busy/modal/F4 code retained | Every mutation button, print-failure/retry and multi-window test |
| 7 | Permissions | OwnerAccess used for bounded administration writes; forged/stale roles tested in source | Permission.Can/MapFor read-error defaults, stale sessions and all direct service callers; local User object is NOT authenticated identity |
| 8 | Record boundaries | Target users must exist; photo reads use one filename boundary | Other entity IDs, exports, native-field mutations, cloud shop isolation/RLS |
| 9 | Secrets/session | Reset clears alternate secret; recovery hash+audit atomic and generic secret write blocked; factory recovery deliberately retained | PBKDF2 malformed hash resource bounds, login/logout/session invalidation and log/export disclosure |
| 10 | Inputs/files | Photo thumbnail/list/full-size/own avatar/cover now share resolver; Backup.ExtractPhotos relative containment read | Reparse-point/symlink filesystem boundary, oversized archives/images/workbooks, SQL/HTML/formula escaping matrix |
| 11 | Backup/cloud | Restore staged path/rollback and transport inspected; network not exercised | Real failure drill, credentials at rest, remote RLS, mixed-version/local allowance compatibility |
| 12 | Other finance | Route/ledger/report services mapped; stock expense tag remains label-only | Customer/supplier mixed delivery/loyalty/expense/report transaction reconciliation |
| 13 | Draft/Park | Existing price/basis/Extra revalidation not altered | Cross-user/stale stock/permission, duplicate draft post fault scenarios |
| 14 | UI/keyboard | Prior narrow search/cart/fixed footer retained; Settings index0 discrepancy recorded | Actual Windows small-screen/DPI/native keys/scanner/modal tests; Settings identity/Fields fixes now source-present; Windows retest |
| 15 | Obsolete/tutorial | 26 routes, eight Settings contents, nested fields and36 lessons inventoried; guide change boundaries stated | Complete sentence/action reconciliation and batch owner decision before whole-feature removal |

## Focused fixes and limits

- `OwnerAccess.Require` reads current active owner by supplied id in the caller transaction; rejects forged role text on a non-owner id, inactive/demoted snapshots. **Does not authenticate a caller who can supply the real owner id**; in-process session architecture remains a review item. Auth/Permission audit now uses current DB owner name.
- Reset writes selected credential and clears alternate credential atomically; both directions covered. Unknown user reset/activation refuses; owner deactivation was already a protected concept, now applies to every owner target.
- Recovery secret saved by internal Settings.SaveRecoveryHash with nonsecret audit in same transaction; cache updated only on success. Generic Set/SetMany/SetUiOnly recovery writes refuse. Factory area11/empty-reset behavior is deliberately UNCHANGED. Ownerless SetUiOnly also refuses FocusGuardService.GuardKey (no production caller needs that bypass); protected-settings regression asserts both normal and ownerless refusal.
- Safe photo filenames shared across CoverPath (including CoversFor), List, PathOf/Read, SetProductCover and OwnAvatar. Rooted/relative separator/drive-relative/UNC/traversal names refused; invalid gallery row remains but has empty FullPath/Exists=false. No row/file deleted. This is filename containment, not a symlink/reparse-point security guarantee.
- TestDatabase actually calls Seed.Seed(): active DB owner id1 exists before AuthSecret/AuthRecovery.CreateStaff. Those fixtures are NOT empty-owner bootstrap and need no weaker authorization exception. Schema permits NULL alternate hashes. Existing factory recovery and nonnumeric reset secret policies retained.

## Validation and continuation

1325 C# cases written (+20 OwnerBoundaryReviewTests, +5 PhotoTests), **none executed here**. Run `tools/RUN-TESTS.cmd` on Windows, expect total1325 failed0; desktop build and physical checks separately. Repository structural gate checks these fixes and inventory coverage but is not runtime/security certification. Independent `tools/check_extra_discount_sqlite.py` uses only synthetic in-memory data, never the shop DB.

**Portion1 next checklist (updated by portion2):** Settings index0/Fields7 source fixes done, Windows retest pending. Continue direct service authorization/stale session and cost projections (see portion2 method matrix); then finance/stock/duplicate execution fault matrix; imports/backup/cloud boundaries; finally full36-lesson action reconciliation + Windows evidence. No installer, no compatibility removal, no HasOpenDrawer or permission read-error policy change authorized by this portion.

### Executed evidence for this portion

- `/home/user/.venv/bin/python tools/check_repo.py`: **67 checks PASS**,368 C# files grammar-parsed,3673 calls/6542 member references resolved,0 reported problems. Not a compiler.
- New structural guard negative probes: wrong active-owner condition, retained alternate credential, unsafe gallery path each rejected; restored guard passed (**3 negative +1 restored outcomes**). These are source mutations, not C# execution.
- `tools/check_extra_discount_sqlite.py`: **PASS** all36 actual migration scripts, historical columns preserved, actual BookCheck SQL detects snapshot corruption, B560/540 arithmetic and foreign keys. Synthetic in-memory only; no .NET posting or shop data.
- Main guide: exact Markdown-rendered `<main>` equals offline HTML main;36 unique lesson anchors. This checks parity, not every instruction's correctness.
- `tools/build_rules_matrix.py --check`: up to date. `git diff --check`: clean.
- `dotnet` executable absent: build/xUnit/Desktop/WPF/physical checks **NOT RUN**.
