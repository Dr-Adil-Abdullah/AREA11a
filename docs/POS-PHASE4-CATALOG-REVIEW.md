# Phase4 portion4 — catalog read/cost projection evidence

> **Subsequent policy update D-110, v0.85.0:** The former management-only cost rule and assertions that the global profit switch leaves cost visible are superseded. View Profit / View Purchase Cost are separate grants under one main switch, plus page access. Named routes migrated in [portion7](POS-PHASE4-FINANCIAL-VISIBILITY-REVIEW.md); global invoice/ledger/batch/stock/expense/export rollout remains OPEN. The evidence/counts below are historical for this portion, not latest policy.

**v0.82.0 Godwit · #565F46 · 2026-11-10. Phase4 remains IN PROGRESS.** This is a focused source review, not full security/Windows certification. [All routes / Settings / 15 dimensions](POS-PHASE4-EVIDENCE.md); [previous ledger review](POS-PHASE4-LEDGER-REVIEW.md); [Urdu PT-24](POS-PHASE4-PART4-URDU.md).

## Finding and bounded correction

ProductService Search/SearchSmart/Get previously accepted no reader and returned catalog CostPaisa. Products bound CostText/MarginLabel without a service projection; ReportTotals exposed raw sale and restock cost even when CanSeeProfit was false. UI visibility alone did not remove those returned numbers.

All three product readers now require User first (no actorless compatibility overload). ProductReadAccess resolves the current active database account, rejects role-kind mismatch, and checks the existing module for the requested workflow. Search projects every exact/tokenized result; smart exact and fuzzy fallback both use Search with the same purpose; Get applies the same projection. SQL filters/stock/pack calculations remain unchanged. Optional PermissionService/SettingsService dependencies are supplied by AppServices; the legacy constructor creates permission/settings readers rather than skipping checks and reads the current margin setting each request.

This is **not arbitrary in-process caller authentication**: a supplied real owner ID is not a session credential. A purpose enum is a workflow selector, not a secret capability. Each purpose still requires its existing module. No permission grant or default changed. Existing PermissionService DB-error fallback remains; no immediate in-flight/session/preview/file revocation guarantee.

## View → caller → workflow matrix

Paths: `dotnet/Area11.Desktop/Views/<stem>View.xaml`, `ViewModels/<stem>ViewModel.cs`, Core `Services/ProductService.cs` → products/batches/catalog links, with `ProductReadAccess.cs`, `Models/ProductReadPurpose.cs`, `Models/Product.cs`.

| Stem / guide lesson | Reader call / purpose | Existing module | Returned catalog cost | Margin |
|---|---|---|---|---|
| Products / 04–07 | SearchSmart + Get / Catalog | Products | owner/manager only | management + global switch + Profit |
| Pos / 09–11 | SearchSmart / Counter | Pos | masked, including owner | masked |
| Purchases / 08 | Search / Purchase | Purchases | retained for authorized receiving input | management + global + Profit |
| Suppliers / 17 | Search / SupplierReturn | Suppliers | retained for authorized SR input | management + global + Profit |
| WriteOff / 20 | Search / WriteOff | Stock | retained for authorized stock input | management + global + Profit |
| Expenses / 22 | Search / Expense | Cash | retained for authorized goods/drawings input | management + global + Profit |
| Provisional / 14 | Search / ProvisionalReturn | Returns | masked, including owner | masked |
| Reports / 23 | Build → ReadTotals | existing report/profit policy | denied CostPaisa/RestockCostPaisa now zero | existing CanSeeProfit |

All Desktop product-reader calls pass actual `_user`. Current Shell grants match the purpose modules (including Returns for provisional, Cash for Expenses). Existing operational cost usage was traced: Purchases loads input CostText; SR/WriteOff/Expenses use selected batch cost with catalog fallback. These costs must not blindly become zero, nor should a module-authorized existing workflow silently become owner-only. General catalog access does not imply those operational module grants.

`CostKnown` / `MarginKnown` default false. Denied product cost is raw zero with CostKnown=false and CostText="hidden"; margin is "—". Authorized zero cost is distinguishable by CostKnown=true. Stock/retail/pack fields remain available. Products new/edit entry guards require CanManage; full edit load refuses masked CostKnown=false, preventing a projected zero being loaded into editable cost. Existing Core write guards remain. Margin formula is unchanged `(retail-cost)/retail`; column now says **Margin (% retail)**, e.g. cost200/retail300 →33.3%, not50% markup.

Reports ReadTotals computes net sales, actual stored sale cost minus restocked cost and profit exactly as before. Only the returned denied raw cost/restock values are zeroed under the same canSeeProfit gate already used for profit. This does not erase stored costs, hide purchase totals, rewrite history, or certify every report row/export.

## Executed evidence versus written tests

- **Executed:** `tools/check_repo.py`:70 static checks;374 C# files parsed with real grammar,3795 calls/6938 member references resolved with zero reported problems. Not a compiler.
- **Executed:** nine targeted source mutations rejected by the new catalog guard: role comparison, module check, cost projection, direct read gate, fuzzy purpose propagation, report sale cost, report restock cost, Counter caller purpose, masked-editor guard. Each original restored; guard clean afterwards. These prove guard sensitivity only.
- **Executed:** all34 ProductService and14 ReportService verbatim SQL blocks byte-equivalent as decoded source to portion-start snapshots; all36 migration files plus SaleService/ReturnService SHA256 unchanged from portion start. All613 baseline files still exist. No production database opened.
- **Executed:** `tools/check_extra_discount_sqlite.py`:36 actual migrations on isolated memory-only SQLite, historical preservation, actual BookCheck SQL and deliberate corruption detection, B560/540 arithmetic, foreign keys clean. This does not execute C# financial services.
- **Executed:** main Urdu guide Markdown/HTML exact rendered-body parity,36 lesson anchors, offline assets; rules matrix/checklist regeneration; `git diff --check` clean.
- **Written, NOT executed:**20 new facts in ProductReadBoundaryTests;1377 total C# cases. No .NET SDK, Desktop build, xUnit, WPF or hardware run here. Windows output remains required.

New facts cover null/forged-role/inactive/missing/stale-role actors, denied modules, Products+Profit grant without management, owner/manager catalog policy, global toggle with same service and legacy constructor, exact/token/fuzzy projection, authorized operational costs, next-read revoke, unsupported purpose, masked Counter object with independently posted actual batch cost, actual-cost-floor rejection, manager report masking with a real restock return, and owner global switch restoration without reposting. Existing search/catalog tests now pass their legitimate owner explicitly; their business assertions are unchanged.

## 15-dimension coverage and remaining scope

1–4 money/discount/cost/history: read projection only; unchanged SQL/posting services and separate SQLite proof, C# execution still pending. 5–6 atomicity/concurrency/duplicates: no write change, full fault/concurrency audit OPEN. 7–8 permission/IDs: focused product read gate/projection reviewed; no authenticated-session claim. 9 login/secrets: inherited earlier focused work, full session audit OPEN. 10 input/files/SQL/archive/HTML: parameterized existing query text unchanged; no general input/file certification. 11 backup/cloud/rollback: no change, full isolation/fault proof OPEN. 12 other finance/reports: only ReportTotals denied raw sale/restock fields reviewed. 13 drafts/park: unchanged, full lifecycle review OPEN. 14 UI/keyboard: masked editor and denominator label source checked, Windows/DPI/native keys/scanner OPEN. 15 obsolete/tutorial: relevant guide lessons reconciled; no feature removal, broader36-lesson action audit OPEN.

Priority next in the SAME phase: Reorder.LastCostPaisa, ProductHistory purchase-cost rows, Dashboard/report/export remaining projections, CustomField/TimeCorrection/service-session boundaries; then finance/stock/concurrency/duplicate/fault/input/backup/cloud and complete guide/Windows evidence. Do not call the full catalog service cost-safe: category/unit/barcode/photo/other methods and operational cost sources are not all covered by this three-reader contract. Profit-based quotes and module-authorized cost workflows permit inference; no absolute cost-secrecy promise.

No schema/posting policy change, installer, fake live-shop post, commit or push. Prior dirty work retained on the same branch; no reset/stash/checkout. Sale-level Extra, independent caps, actual-cost floor for everyone and return policy B remain unchanged.
