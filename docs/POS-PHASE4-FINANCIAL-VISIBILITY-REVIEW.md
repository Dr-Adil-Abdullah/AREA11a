# Phase4 portion7 — independent financial visibility (D-110), v0.85.0 Plover

> **Subsequent portion12:** [v0.90 expense/goods outputs](POS-PHASE4-EXPENSE-VISIBILITY-REVIEW.md) migrates mixed history/Today/goods result/new voucher valuation while ordinary Cash-module expense/cash-drawing amounts stay readable and stock/cash posting SQL unchanged. Purchase/supplier/StockMinus/audit/generic export/session/Windows still open. [PT-32](POS-PHASE4-PART12-URDU.md).

> **Subsequent portion11:** [v0.89 stock/frozen outputs](POS-PHASE4-STOCK-VALUATION-REVIEW.md) migrates Stock history/today/result and StockTake Current/History/Sheet/found/document/Open/Apply outputs while private raw frozen accounting stays intact. Generic exports/StockMinus/expense/purchase/supplier/session/Windows still open;no standalone stock-take CSV builder exists. [PT-31](POS-PHASE4-PART11-URDU.md).

> **Subsequent backend portion10:** [v0.88 batch/fresh preparation](POS-PHASE4-BATCH-READ-REVIEW.md) migrates generic Stock/Cash/Suppliers batch-list cost and valuation plus fresh previews/manualSR tray. Stock history/today/result and frozen stock-take and expense results/vouchers remain open; original posting/formulas unchanged. [PT-30](POS-PHASE4-PART10-URDU.md).

> **Subsequent backend portion9:** [v0.87.0 purchase invoice / expired reads](POS-PHASE4-PURCHASE-READ-REVIEW.md) migrates new invoice amounts incl Paid+Due and expired-list raw/valuation fields; refreshed known-cost gate before one-click tray preparation. This narrows, does not close, remaining routes below; purchase entry/drafts/results,ordinary supplier ledger money/Pay/SR,generic batches and stale prepared objects still OPEN. [PT-29](POS-PHASE4-PART9-URDU.md).

> **Subsequent UI portion8/D-111:** owner-requested financial group, hover/scope and live assignment inspector added in [v0.86.0](POS-PHASE4-PERMISSION-INSPECTOR-REVIEW.md). This improves configuration/visibility explanations, does NOT close the global backend rollout below or settle the supplier financial-action policy.

**Status: bounded implementation checkpoint; global rollout and Phase4 remain IN PROGRESS.** Date: 2026-11-10. No Windows/.NET build or xUnit execution. Do not use this checkpoint as proof that denied cost is inaccessible through every application route.

## Settled owner decision — supersedes D-109

The owner explicitly answered **“keep both seprat checkbox”**: Staff & permissions has independent **View Profit** (`profit`) and **View Purchase Cost** (`purchasecost`) checkboxes. The existing main setting `profit.showMargins` gates BOTH. Each person also needs the relevant page/workflow module. Manager rank and Reports access alone do not grant cost visibility. Only the owner administers grants, including their own financial checkboxes; owner access to administrative modules cannot be switched off by these financial checkboxes. Missing cost grants for existing non-owner accounts default to denied, not automatic manager access. Existing explicit Profit grants remain independent.

D-109's management-only History/Reorder rule is **superseded**, not an additional condition. A cashier with Products + View Purchase Cost + main ON may read the named catalog/history cost fields, while a manager without View Purchase Cost may not. Cost denial must hide rate AND line total/aggregate; displaying quantity10 and total2000 defeats hiding a rate200. Nonfinancial quantity/date/name/supplier information stays where its module permits.

| Main switch | View Profit | View Purchase Cost | Explicit profit/margin | Named raw cost/rate/valuation/related total |
|---|---|---|---|---|
| OFF | either | either | denied | denied |
| ON | no | no | denied | denied |
| ON | yes | no | allowed under page grant | denied |
| ON | no | yes | denied | allowed under page grant |
| ON | yes | yes | allowed under page grant | allowed under page grant |

This is direct-field authorization, **not mathematical secrecy**. Retail and authorized profit/margin can imply cost; cost and selling totals can imply profit. Profit-based discount quotes also permit inference. Prior exports/screenshots/printed pages and already-open views cannot be recalled.

## Implemented View → VM → Core → data/permission trace

Paths below are stems under Desktop Views/ViewModels and Core Services/Models.

| Entry | Trace / data | Bounded behavior |
|---|---|---|
| Staff & permissions | SettingsView → SettingsViewModel.LoadPermissions/OnPermissionChanged → PermissionService.Set/MapFor; users/user_permissions/audit_logs | two separate labels/keys; only owner UI administration; financial overrides editable for selected owner as well; Core Set resolves active owner in transaction |
| Main visibility switch | SettingsView.ShowMargins → SettingsViewModel.Save → SettingsService.SetMany; settings/audit_logs | label explains both gates; owner-only control; manager normal Save omits this key; SetUiOnly denies key; SetMany resolves active owner before and inside transaction; workbook classifies key locked |
| Financial grant reads | CostPolicy / ProfitPolicy → PermissionService.CanSeeFinancial; current users + user_permissions | active current account/same role-kind; explicit grant else role default; database failures deny; financial gate precedes owner automatic-module branch; not session authentication |
| Product catalog / lesson04 | ProductsView → ProductsViewModel → ProductService.Search/SearchSmart/Get → ProductReadAccess | independent CostKnown/MarginKnown; calculate projected retail-denominator MarginPercent BEFORE masking raw cost; profit-only does not return fake100%; Counter/PR still cost-free |
| Product edit | ProductsViewModel.BeginEditProduct → ProductService.Get | denied/unknown cost refuses editor instead of loading masked0; separate useful message; underlying cost untouched |
| Product-based preparation / lessons08,17,20,22 | Purchases/Suppliers/Stock/Expenses VMs → ProductService purpose-specific Search/Get | cost-preparation purposes require CostPolicy plus existing workflow module; refuse rather than provide fake0 inputs; granting only Products or Profit does not suffice |
| Product History / lesson07 | ProductHistoryView/VM → ProductHistoryService.Load/BuildDocument/BuildCsv; purchases/purchase_items and sales snapshots | cost grant rather than management; denied rate+line total+Bought aggregate raw0/hidden, CSV both monetary cells empty; independent explicit profit rule |
| Reorder / lesson19 | ReorderView/VM → ReorderService.Report; latest purchase snapshots | LastCostKnown uses CostPolicy; denied raw last cost0/hidden, quantities/supplier/order message unchanged |
| Dead stock / lesson23 | ReportsView/VM → ReportService.Build → ReadDeadStock; batches valuation | CostKnown false + raw ValuePaisa0 + hidden label if denied; permitted genuine zero is known0; existing valuation SQL/order/top50 unchanged |
| Report totals / lesson23 | ReportService.ReadTotals → ReportTotals → ReportsViewModel/document labels | sale cost/restock cost/purchases aggregate follow cost grant independently of explicit profit; CostKnown/CostText/PurchasesText distinguish denied from zero; other finance fields not declared globally secret |
| Item price/discount / lesson09 | PosViewModel → LinePricingViewModel → SaleService.Quote → SaleLinePricing | independent fresh quote CostKnown/ProfitKnown and projections; VM never recomputes profit from masked cost; full sale computation and posting unchanged |

No migration is required: user_permissions already stores module keys. All36 migrations unchanged. `PermissionService.MapFor` is an administrative checkbox map, NOT the authoritative financial-read check. Settings has an existing process-local cache; this work does not introduce cross-process instant settings invalidation.

## Open rollout — owner requirement not yet globally complete

**Do not declare “everywhere consistent” complete.** Remaining reads must be traced and moved to the same policy without feeding masks into posting calculations:

- Batch/stock-take sheet and variance/loss valuation, stock-minus/write-off, expense/drawing history; some existing read methods are actorless.
- Purchase invoice/document and supplier ledger/expired-batch reads/prints. Existing module authorization is not enough for the new cost rule. Their totals/payments can expose cost even when rate is omitted.
- Native workbook/CSV downloads, import previews, audit old/new values and other export/print paths. Locking the main setting in workbook imports **does not** make workbook cost data confidential.
- Dashboard supplier payables, finance/cash totals and derived cost inference; distinguish ordinary balance authorization from a cost field before changing semantics.
- Preparation VMs may independently read batches after the newly guarded product search. All existing prepared/stale objects and drafts need review before asserting the all-route boundary.
- Complete current-reader/module/session/ID review, per-read revocation and Windows UI behavior. A real privileged account ID supplied by arbitrary in-process code is not an authenticated credential.

Next work is implementation of these known routes under D-110, not another question about whether to have separate checkboxes. Do not silently grant managers cost to preserve an old test; update superseded tests and check caller workflows. Do not change stock quantities, invoice values, payable balances, actual batch allocation, cost floor or financial posting to conceal a displayed number.

## Actually executed evidence

- `tools/check_repo.py`: **73 static checks**,379 C# grammar files,3921 calls/7318 member references resolved,0 reported problems. Static syntax/contracts/member checks are NOT compilation or runtime/security certification.
- **12 negative source mutations rejected and restored**: shared-vs-independent cost key; main switch omission; owner financial bypass; financial DB-error fail-open; DeadStock raw mask; report cost coupled to profit; History management rule; quote cost coupled to profit; item editor combined flag; own-owner checkbox disabled; UI-only switch bypass; workbook switch bypass. Guard clean after restoration.
- Verbatim SQL blocks unchanged for Sale14, Report14, Permission1, Product34, ProductHistory2, Reorder3 versus portion-start copies. SaleService source from `Create` through posting/computation tail byte-for-byte unchanged.40 migration/posting/time/Database baseline hashes unchanged; all626 baseline files remain. These checks do not prove all existing financial formulas correct.
- `tools/check_extra_discount_sqlite.py`:36 actual migrations on temporary synthetic SQLite, historic column preservation, actual BookCheck SQL/corruption detection/B560540/FK PASS; not C# execution.
- `tools/check_time_correction_sqlite.py`:36 migrations, actual source SQL, independent immediate-lock/audit-fault rollback/time-money preservation/FK PASS; not .NET concurrency/session execution.
- `git diff --check` clean. No installer, live-shop synthetic posts, commit or push. Prior dirty work retained.

## Written but NOT executed C# evidence

**24 new facts**, FinancialVisibilityTests, **1445 total written cases**. Cases cover separate keys/defaults, neither/profit-only/cost-only/both, main switch including owner, restoration without reposting, independent revocations, manager no bypass, owner own overrides/admin retention, owner-only administration, current inactive/forged/missing account denial, financial DB failure, module gate, History CSV cells, independent quote projections, report totals, protected switch, preparation refusal, cost-floor preservation and authorized genuine-zero valuation. Superseded ProductReadBoundaryTests and SnapshotReadBoundaryTests expectations updated to D-110. No .NET SDK here: all C# cases UNEXECUTED. Windows tests must use truly separate data, never live shop entries.

## 15-dimension status

1 money/rounding,2 discount order,3 batch cost,4 history/returns: projection-only changes; posting/SQL preservation checked, C# regressions unrun.5 stock/atomicity/concurrency and6 duplicates: no posting changes; existing independent SQLite evidence only, broad runtime OPEN.7 financial permissions: named routes focused, global rollout explicitly OPEN.8 IDs and9 auth/session/secrets: current-account financial reads improved, not authenticated-session proof.10 inputs/files and11 backup/cloud: workbook main-switch protection only, export/archive/cloud data confidentiality still OPEN.12 other finance: ReportTotals masks focused, supplier/purchase/variance/payment inference OPEN.13 drafts/park: stale/prepared lifecycle OPEN.14 UI/native keys/hardware: binding checks only, actual Windows/DPI/keyboard/printer OPEN.15 guide/obsolete: current policy reconciled with main guide and D-110; full36-lesson action audit OPEN; no feature/data removal.

[Safe owner checklist PT-27](POS-PHASE4-PART7-URDU.md). Phase4 is not finished.
