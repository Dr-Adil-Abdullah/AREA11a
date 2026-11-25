# Phase4 portion5 — historical cost, snapshot readers and daily-report/export evidence

> **⏳ checkpoint کی حد:** اس فائل کے نتائج/اعداد اسی درج شدہ checkpoint کے ہیں؛پرانے pending یا not-started جملوں کو موجودہ حالت نہ سمجھیں۔ [تازہ مجموعی رپورٹ](COMPLETE-STATUS-AND-DECISIONS-URDU.md) اور[فیصلوں کا index](DECISION-REGISTER-INDEX-URDU.md) پہلے دیکھیں؛نیا Windows PASS فرض نہیں۔

> **Subsequent policy update D-110, v0.85.0:** The former management-only cost rule and assertions that the global profit switch leaves cost visible are superseded. View Profit / View Purchase Cost are separate grants under one main switch, plus page access. Named routes migrated in [portion7](POS-PHASE4-FINANCIAL-VISIBILITY-REVIEW.md); global invoice/ledger/batch/stock/expense/export rollout remains OPEN. The evidence/counts below are historical for this portion, not latest policy.

**v0.83.0 Snipe · #5B5265 · 2026-11-10. IN PROGRESS, not full audit/security/Windows PASS.** [Overall route/Settings/15-point register](POS-PHASE4-EVIDENCE.md); [previous catalog contract](POS-PHASE4-CATALOG-REVIEW.md); [safe Urdu PT-25](POS-PHASE4-PART5-URDU.md).

## Owner decision D-109 — asked before implementation

Example presented: cost200, retail300; hiding catalog cost does not help if Product History still returns purchase rate200 or quantity10/total2000. Reorder's raw LastCostPaisa also carried cost despite no current UI/message binding. The owner explicitly selected **management**: existing History/Reorder module access AND owner/manager for these purchase costs. The alternatives (additional Purchases/Suppliers grants, or page permission alone) were not selected. Quantity/date/supplier/sales remain under existing page authorization. This is a narrow approved rule, not a new owner-only restriction on operational receiving/SR/write-off/expense preparation.

## View → ViewModel → service/data matrix

All paths below are under `dotnet/Area11.Desktop` and Core `Services`. AppServices supplies database/settings/permissions/log dependencies.

| Route / lesson | View → VM → service entry | Data / gate | Focused result |
|---|---|---|---|
| Product history / 07 | ProductsViewModel → ProductHistoryDoor → ProductHistoryWindow.xaml / ProductHistoryViewModel → ProductHistory.Load | products/purchase_items/purchases/suppliers/sale_items/sales/customers; Products | current active account, role-kind match and module; purchase cost requires current CanManage |
| History print/WhatsApp/PDF / 07,35 | same VM → BuildDocument → Load | same historical rows | masked purchase rate AND total formatters, masked Bought summary; each new preparation revalidates |
| History CSV / 07 | same VM → BuildCsv → Load | same historical rows | denied purchase rate_paisa AND total_paisa empty quoted cells, not fake zero; quantity/sales retained |
| Reorder / 19 | ReorderView.xaml / ReorderViewModel → Report | products/batches/latest supplier purchase; Reorder | current reader before even disabled-report success; LastCostKnown and management-only raw LastCostPaisa |
| Dashboard / 02 | DashboardView.xaml / DashboardViewModel → GetSnapshot | sales/returns/provisional reporting/cost aggregates/customer/supplier/shifts | current reader; existing Pos OR Reports rule retained; ProfitPolicy uses resolved reader |
| Reports / 23 | ReportsView.xaml / ReportsViewModel → Build + BuildDocument | period sales/returns/stock/category aggregates | current reader/Reports; existing global+Profit gates use resolved reader; document reloads Build |
| Daily rows / 23 | public Core Days (no repository caller found) | ReadDays same SQL | previous actorless public List method always passed canSeeProfit:true; now requires reader, returns ServiceResult and obeys same Reports/global/Profit boundary |

No actorless Days compatibility overload remains. Public static report range/period helpers carry no business data. Reorder BuildPadDocument/BuildText operate on supplied ReorderLine values, which have no cost field; their existing output is quantity-based, not a fresh database cost query. Reorder RecordOrder/SetLevel write authorization and arbitrary caller-supplied message content were not comprehensively reviewed here.

## Projection details

`SnapshotReadAccess.Require` reads current database id/name/role/active, rejects absent/inactive accounts, inactive actor snapshots, nonpositive IDs and role-kind mismatch, and checks existing module permission. Optional alternative module is only used for Dashboard, preserving Pos **OR** Reports. Each reader has a ValidationException refusal path. This is not authenticated-session verification: arbitrary in-process code that can supply a real privileged ID is outside this boundary. PermissionService defaults and DB-error fallback remain unchanged; no global immediate session/in-flight/preview/file revocation claim.

ProductHistory.CanSeeCost is current reader.CanManage, independent of global profit visibility. Each purchase row gets CostKnown. Denied RatePaisa AND LineTotalPaisa are zeroed before returning the model; BoughtPaisa therefore also zero. RateLabel/TotalLabel/BoughtTotalLabel say `hidden`, not legitimate zero. Authorized actual zero remains CostKnown=true. The VM Bought summary and document Bought summary use BoughtTotalLabel; print purchase rows use masked formatters. CSV purchase money cells are empty when denied, with the existing header/order/numeric units retained. Sale values and historical snapshots are not rewritten. Explicit sale profit remains under the existing global+Profit policy, including owner grants; D-109 does not revoke other established profit permissions or promise mathematical secrecy from authorized profit.

Reorder reports set LastCostKnown only for an actual last supplier purchase and a manager/owner reader. LastCostPaisa is zero otherwise. No last purchase remains distinguishable from a real authorized zero-cost sample purchase. Tier/suggestion/stock/minimum/supplier choice/pack arithmetic unchanged. No new cost column or cost is added to the supplier order message.

Report Days now takes `(ReportRange range, User? user)` and returns `ServiceResult<List<ReportDayRow>>`; module/current-account checks and ProfitPolicy occur before ReadDays. Build and Days use identical existing ReadDays SQL. BuildDocument calls Build for each new preparation. Existing ReportTotals sale/restock cost mask from portion4 retained. ReadDays already zeroes denied profit, TopByQty already zeroes raw profit, TopByProfit is absent when denied, and document Profit row is gated. Dashboard never executes its profit SQL unless ProfitPolicy permits and returns zero/flag=false otherwise. No monetary formulas changed.

## Remaining explicit report-cost finding — NOT silently closed

`ReportService.ReadDeadStock` returns **ValuePaisa = SUM(batch quantity × purchase cost)**, ordered by value, under existing Reports access. This is an existing stock-valuation workflow, not covered by the owner's D-109 History/Reorder decision. It and its UI/derived inference require a separate explicit policy review; this portion does **not** declare every report field cost-free. Reports purchase totals, Dashboard supplier payable/customer credit and other aggregate finance remain under their existing policies. Operational costs, authorized profit-based quotes and other exports may reveal or imply cost. Broader CSV/formula/file/HTML safety and previously prepared document lifetime remain OPEN.

## Evidence actually executed

- `tools/check_repo.py`: **71 static checks**,376 C# files parsed with real grammar,3836 calls/7044 references resolved with zero reported problems. Real grammar/member checks are not compilation.
- **12 negative source mutations rejected**, then originals restored and the new guard passed: live role, module gate, historical management rule, purchase raw rate, purchase raw total, CSV purchase rate, print Bought summary, Reorder last cost, Dashboard OR-module propagation, Days reader signature, report resolved-reader profit, VM Bought summary. These test source-guard sensitivity, not .NET runtime.
- **2/3/14/10 verbatim SQL blocks unchanged** in ProductHistory/Reorder/Report/Dashboard versus portion-start source; all36 migration files and SaleService/ReturnService/PurchaseService SHA256 unchanged. All618 portion-start files retained. Source snapshots are scratch evidence, not product artifacts.
- `tools/check_extra_discount_sqlite.py`:36 real migrations on isolated memory-only SQLite; historical money preservation; actual BookCheck query, deliberate corruption detection; B560/540 arithmetic; foreign keys clean. No production shop data opened. This is not C# service execution.
- Main Urdu MD/HTML exact rendered-body parity,36 unique lesson anchors, no remote script/link/img assets; rules matrix/checklist regenerated; `git diff --check` clean.

## Written but NOT executed C# evidence

**24 new facts in SnapshotReadBoundaryTests;1401 total written cases.** No .NET SDK, xUnit, WPF/Desktop build, Windows print/scanner/DPI run here. Tests cover anonymous/forged/missing/inactive/stale readers; denied modules and both Dashboard alternatives; same-service next-read revocation; purchase rate/total/aggregate masks; operational/profit grants not overriding D-109; manager cost with global profit OFF; owner toggle restoring stored profit; CSV/document/summary masks and revalidation; Reorder grant/mask, owner/manager authorized cost, unknown versus genuine zero, disabled-report early path; checked daily rows under global toggle; Dashboard profit grant/toggle; report focused day/top/document masks; unchanged posted purchase/sale snapshots.

## 15-dimension status / next portion

1–4 money/discount/actual cost/history: projection and historical snapshot reads reviewed, unchanged SQL/posting source, independent SQLite proof; C# run pending. 5–6 stock/atomicity/concurrency/duplicate: no posting change; wider fault proof OPEN. 7–8 permission/IDs: focused current-reader and approved management cost contracts only. 9 login/session/secrets: no new authentication; full session/service boundary review OPEN. 10 input/files/SQL/HTML/archive: existing SQL unchanged; export masking only, general file/CSV safety OPEN. 11 backup/cloud/isolation: no behavior change, full rollback/isolation proof OPEN. 12 other finance: focused snapshot/profit paths; DeadStock valuation and other ledgers/expenses/loyalty/exports OPEN. 13 drafts/park: unchanged, lifecycle review OPEN. 14 UI/keyboard: history formatted purchase cells/summary source checked; WPF/native keys/DPI/hardware OPEN. 15 tutorial/obsolete: relevant02/07/19/23/35 lessons reconciled, broader action audit OPEN; no feature removal.

Next SAME phase priorities: explicit remaining valuation policy and report/export audit, CustomFields/TimeCorrection/read-write/session boundaries, then finance/stock/concurrency/duplicate/fault/inputs/backup/cloud and complete36-lesson action/Windows evidence. No installer, fake live-shop transactions, commit or push; previous dirty work retained on the same branch, no reset/stash/checkout.
