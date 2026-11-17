# Phase4 portion3 — coordinated ledger read boundary

**v0.81.0 Curlew · 2026-11-10 · IN PROGRESS, not a full audit/security PASS.**

Extends `POS-PHASE4-EVIDENCE.md` and `POS-PHASE4-ACCESS-REVIEW.md`. This portion addresses two specific legacy actorless read surfaces, not the whole application. No .NET SDK/WPF/runtime or real shop data used.

## What changed and why

The Customers/Suppliers screens checked module visibility, but the corresponding Core List/Page/Statement APIs did not accept a caller. A direct Core caller could request a record/statement without presenting the reader context, and CustomerSummary included raw profit even when the UI hid it.

- Require `User? user` as the FIRST argument of CustomerLedger List, Page, both Statement overloads, BuildBulkStatements and SupplierLedger List, Page, ExpiredBatches, Statement, BuildBulkStatements. No actorless public compatibility overload remains. This is an internal source API migration, not removal of a shop feature.
- New internal `LedgerReadAccess.Require` reads the current DB account on the reading connection, requires active caller/current row, rejects role mismatch with the supplied snapshot, then checks Customers or Suppliers using the **current DB User**. Missing account, failed account lookup and permission denial refuse instead of falling back to caller-supplied owner text.
- Customer PaymentReceipt already took a user; it now also uses the current-reader check before reading an existing PAY receipt.
- Customer List and Page mask explicit raw profit under the shared ProfitPolicy (global switch plus profit permission). Returned ProfitKnown=false/ProfitPaisa0/ProfitText=hidden on denial; balances, lifetime sales, loyalty and movements remain unchanged. Both list and selected account are covered, not just a WPF visibility binding.
- Single/monthly statements propagate user to Page. Bulk jobs propagate user to every Page and Statement; failure produces no returned partial job. Existing customer statements contain no profit for ANY user, including owner.
- All Customers/Suppliers ViewModel callers pass `_user`: list/refresh, selected page, expired batches, print, bulk selection, PDF/WhatsApp statement preparation and monthly statement. Existing financial tests now explicitly supply their previous owner context; their finance expectations are not weakened.

## Exact source paths / reviewed methods

| Surface | Source → gate/data | Evidence/status |
|---|---|---|
| Current reader | `Services/LedgerReadAccess.cs` → users id/name/role/active → PermissionService.Can(current,module) | Focused source review; tests written, not executed |
| Customer List/Page | `Services/CustomerLedgerService.cs` → current-reader gate → existing customer/sale/return/payment aggregates → masked CustomerSummary | Anonymous/denied/stale/forged-role/granted reader and profit switch tests written |
| Customer Summary | `Models/CustomerLedgerModels.cs` → ProfitKnown flag / masked explicit profit formatter | No persisted schema or source financial numbers changed |
| Customer Statement overloads / BuildBulkStatements | Same service → Page(user,...) and Statement(user,...) → receipt/bulk job | Monthly/full/bulk and missing-record propagation tests written |
| Customer PaymentReceipt | Same service → current-reader gate before payment SELECT; existing PAY-only filters retained | Existing receipt success then inactive-account denial test written |
| Supplier List/Page/ExpiredBatches | `Services/SupplierLedgerService.cs` → current-reader Suppliers gate → existing supplier/ledger/batch projections | Default cashier denial and owner/manager/explicit grantee success tests written |
| Supplier Statement/BuildBulkStatements | Same service → Page(user,...) / Statement(user,...) → existing document pipeline | Revocation/missing-record/no-partial-job tests written |
| UI preparation callers | `ViewModels/CustomersViewModel.cs`, `SuppliersViewModel.cs` | All actual ledger read calls now carry `_user`; source guard verifies this |
| Financial posting / SQL arithmetic | Customer Collect, supplier Pay/Return and the posting services; original read SQL aggregates retained | NOT changed by this portion. Permission/session hardening of writes remains separate |

Core paths above are under `dotnet/Area11.Core`; ViewModels under `dotnet/Area11.Desktop`.

## Deliberately preserved policy and honest limits

- Customers module still permits ordinary cashier khata reads; Suppliers default denial still applies to cashier. An explicit owner grant of Suppliers remains a supported read workflow, not a new owner/manager-only restriction. Existing permitted supplier statements/expired-batch data still contain supplier financial/cost information. A new independent cost-visibility policy would need coordinated review, not a silent blanket deny.
- PermissionService.Can/MapFor role defaults and documented permission-read error behavior are UNCHANGED. Current account lookup must itself succeed; after that, existing module policy remains authoritative. No claim that every possible database fault is now fail-closed.
- This is NOT authentication of an arbitrary in-process caller supplying somebody else's real id. No authenticated session token, remote API or multi-tenant boundary is introduced. Current account/role is checked for each read, not a guarantee of instantaneous revocation during a running read/job. Already-loaded UI, preview, copied data, exported files and printed paper are not erased/revoked by a later denial.
- Shared permissions are re-read on the next request; stale role snapshots require sign-in again. No global logout/session revocation framework or change to approved factory recovery.
- No sale/purchase/return/payment/Extra/B policy/rounding/stock arithmetic changed. No migration, no historical balance/item rewrite, no fake production transaction, no installer.
- Catalog Product.Search/SearchSmart/Get still have legacy actorless cost-bearing projections. Reorder.LastCostPaisa, Report raw costs, ProductHistory purchase costs, Dashboard aggregates and other read/export paths remain OPEN. Do NOT infer complete secrecy from the fixed CustomerSummary fields; authorized cost and profit-discount quotes can permit inference.
- RecoveryService and PartnerService are separate services, not automatically hardened by these two ledger classes. Custom fields and TimeCorrection current-account/write validation also remain OPEN.

## Tests and proof boundaries

`LedgerReadBoundaryTests.cs` adds20 facts: anonymous customer/supplier reads; customer denial; supplier default denial/grant/revocation; valid cashier customer workflow; inactive/missing/forged-role/demoted/inactive-snapshot accounts; existing payment receipt refusal after deactivation; list/page profit masks; global off/on and explicit grant; profit-free statements/month/bulk; no financial writes from reads; unknown records/mixed bulk failure. Existing owner-context ledger/month/bulk/import/customer edit/create/Extra tests migrated to required user arguments.

**1357 cases written; C# NOT RUN.** Structural gates are not compilation, xUnit/security execution, or WPF/hardware proof. Results of actually executed source/SQLite/parity checks are recorded below after execution.

## Fifteen-dimension progress / next SAME phase

Dimensions7/8: these ledger entrypoints and explicit CustomerSummary profit fields are now covered in source; broader catalog/cost/session/record-id paths remain. Dimension9: current account checked on reads, not general session authentication. Dimension12: existing khata reconciliation/month/bulk expectations retained, no new runtime finance proof. Dimension15: guide lessons15–17/31 and summary reconciled for these bounds;36-lesson full action proof still pending. Other dimensions retain the previous registers' outstanding money/stock/concurrency/double-execution/draft/input/backup/cloud/UI/physical tasks.

**Next priority:** catalog safe versus authorized cost projections and their receiving/pricing/UI/export callers; remaining report/reorder/product-history/Dashboard costs; then custom-field/time-correction/stale-session checks and finance/stock/fault/file/cloud/guide/Windows completion. No whole-feature removal or policy change without the owner's required explanation/approval.

## Workspace preservation

At turn start, Git HEAD was the base805626e while prior v0.80.0 source and262 changed/untracked paths were present. These pre-existing files were preserved; no reset/stash/checkout/deletion or branch switch was used. This portion is saved as working-tree changes on the same branch; no commit/push claim is made for it. Temporary validation environment and hash manifest are not product deliverables.

## Executed evidence — this portion

- `.venv/bin/python tools/check_repo.py`: **69 checks PASS**,371 C# files grammar-parsed,3746 calls/6721 member references resolved,0 reported problems. This is not a compiler.
- Nine negative source probes rejected: actorless customer List, omitted supplier reader gate, wrong active-account condition, omitted role mismatch check, retained list raw profit, retained page raw profit, dropped bulk caller propagation, UI null-reader substitution, actorless short Statement overload. Original files restored; restored gate passed (**9 negative +1 restored**). These are NOT executed C# security tests.
- Compared the turn-start files to prior pushed v0.80.0 commit1ec8220 (matching SHA256), then compared all verbatim SQL strings: CustomerLedger8/SupplierLedger16 identical after these source edits; all existing migration file hashes unchanged from turn start. This verifies source preservation, not runtime financial correctness.
- `tools/check_extra_discount_sqlite.py`: **PASS** all36 actual migration scripts, historical preservation, actual BookCheck SQL reconciliation and corruption detection, B560/540 arithmetic and foreign keys; synthetic in-memory data only. No .NET posting, no shop database.
- Main36-lesson guide: exact generated Markdown main equals HTML main,36 unique lesson anchors, no remote script/style/image assets.
- `tools/build_rules_matrix.py --check`: up to date. `git diff --check`: clean.
- Core/xUnit1357, Desktop build/launch, WPF/permission dialogs/session/DPI/scanner/printer/real exports **NOT RUN**; no dotnet executable in this sandbox. Previous release Windows results do not prove this code.
