# Phase4 portion17 — Supplier accounting exception B / D-114

**v0.95.0 Avocet · 2026-11-10 · PT-37 · SOURCE IMPLEMENTED; Windows/full Phase4 OPEN.**

## Settled owner decision

The owner selected **B / `accounting_exception`** after the concrete example purchase Rs2000, paid Rs500, payable Rs1500. A current **Suppliers module grant** allows account purchases/paid/payable/refunded/credited amounts, movements, statements, bulk statements, profile sharing, Pay and SR settlement money **even when View Purchase Cost or the common main switch is OFF**. The owner accepts purchase-cost inference, including an SR total divided by quantity. Do not ask this choice again or silently restore a blanket cost prerequisite.

This is NOT permission to display direct product/batch cost fields or cost-bearing tray previews. Those still need **independent Cost + main ON**; Profit/manager rank does not substitute. Profit/main rules elsewhere, editable bulk-file gates and password-backup A remain unchanged. Supplier statements are accounting output, not editable bulk-import workbooks. Earlier portion9/10 known-cost-only tray instructions and references to an unanswered Supplier policy are historical and superseded here.

## Implemented boundaries

| Path | Current authority / output |
|---|---|
| Existing List/Page/ExpiredBatches/Statement/BuildBulkStatements | Existing current active/same-role account + Suppliers module retained. Ledger money intentionally unmasked. Expired direct cost remains independently projected. Bulk failure no longer concatenates internal exception text. |
| `Pay` | Current reader resolved **inside existing immediate Database.Tx**, before supplier lookup/payment/balance/Audit. Current stored ID/name passed to payment/Audit. Payment cap and calculation unchanged. Positive amount/method syntax can reject before account lookup without reading money. |
| `Return` | Same transactional current-account gate, then unchanged private `SaveReturn` with resolved actor. Accounting result/warnings intentionally monetary under B. |
| `CheckReadAccess` | Small current-account/module/cost-visibility snapshot; not a bearer token or full ledger fetch. Failure denies UI access. |
| New `PrepareReturnLine` | Current Supplier account first; active product and explicitly bound active batch; finite positive stock; fresh quantity/pack/name/IDs. Optional direct cost uses existing batch-positive-else-product fallback only when Cost/main allows. Otherwise `CostKnown=false`, raw cost0; caller cannot supply a posting price. |
| Product Search/Get, purpose SupplierReturn | Only this purpose skips the raw-cost preparation prerequisite; current purpose/module checks and independent Cost/Profit projection still run. Other preparation purposes remain cost-gated. SearchSmart delegates to the same checked search. |
| Existing `Stock.PrepareBatchCost` | **Unchanged**, including raw-cost gate for SupplierReturn. Quantity-only callers use the separate bounded DTO, not a weakened raw-cost endpoint. |
| Manual / expired SR tray | Both use fresh `PrepareReturnLine`, copy CostKnown, and mutate tray only after successful preparation. Expired path also rechecks membership in the current500 selection. Unknown row value and mixed aggregate say hidden/quantity-only, not known0 or a misleading partial total. Save posts IDs/quantity/unit, never tray cost. |
| Supplier account / sharing UI | Account labels filled only after successful fresh Page, not cached selected summary. Failed list/page clears sensitive state. Profile sharing rebuilds Page/current summary; statements already rebuild. |
| Active view | Loaded/Unloaded nominal3s timer calls current access snapshot. Module/account denial clears list/account/movements/expired/search/selection/tray/bulk selection and status; CanWork notified. Cost-state change clears cost-bearing pickers and masks/rebinds retained tray rows while preserving IDs/quantities. Cost restoration does not magically mark old previews known; re-add to obtain a fresh preview. |

## Preserved accounting / scope evidence

- All **684 baseline tracked files remain**. **112 other Core service/data file hashes exact** against start-of-portion snapshot; only SupplierLedgerService, ProductService and permission-help text changed in that directory set.
- Private Supplier **`SaveReturn` through end-of-file is byte-for-byte text exact**, including ReadMovements, SummarySql, valuation/fallback, stock decrement, warnings, numbering and Audit details. All **16 pre-existing verbatim Supplier SQL blocks preserved**; two added preparation SELECTs reuse the existing bound product/batch read shapes.
- StockService, StockPolicy, LedgerReadAccess, ProductReadAccess, PermissionService, Audit writer, migrations, backup/restore and private purchase posting unchanged. Pay SQL/cap preserved; actor resolution is new.
- No fake shop posting, schema/data migration, installer artifact, screenshots or production approval.

## Evidence actually executed here

| Check | Result / limit |
|---|---|
| `tools/check_repo.py` using local tree-sitter environment | **84 PASS**;402 C# grammar files,4293 calls,8353 member references,0 issues. Not compiler or runtime. |
| New portion17 source guard negative mutations | **13 rejected**, exact source bytes restored and clean guard accepted: preparation reader/binding/known/raw mask, both transactional writer gates, both current actors, purpose bypass, tray mask/aggregate, timer unload, cached profile. |
| `tools/check_supplier_accounting_sqlite.py` | Actual source SQL on synthetic memory DB: product/batch binding/active filters, preparation packs, four movement queries, purchase paid not double-counted, Pay, credit vs cash SR, StockPolicy conditional decrease, audit-fault SQL rollback, read-only/FK preservation. Python drives transactions, not C# service execution. |
| All `tools/check_*_sqlite.py` | **12 probes PASS**, including the new probe. No real shop DB accepted/used. |
| Independent Python backup format model | **6 round trips +24 negatives PASS**; unchanged backup C#/DPAPI NOT executed. |
| New xUnit source | **24 facts written** (accounting inference2000/500/1500, all invalid accounts/module revocation, separate raw-cost gate, masked search/preparation, known0/fallback, current stored audit names, credit/cash, stock/overpayment, audit-failure rollback). One old financial-preparation test excludes SupplierReturn product selection because D-114 deliberately changes that contract; raw Stock preparation tests retained. |
| .NET / Windows / WPF / DPAPI / installer | **1740 total written C# cases UNRUN**. No SDK/Windows/Inno here. Static tooling initially did not catch a missing required Pay argument in new tests; manual signature review fixed all6 sites and added an exact guard. This reinforces why static PASS is not compilation proof. |

## Fifteen review dimensions and remaining limits

1. **Navigation:** existing Supplier page/shell preserved; no duplicate header/navigation redesign.
2. **Identity:** current active/same-role ID resolution, not authentication against arbitrary in-process impersonation of another valid account ID.
3. **Authorization:** Suppliers for accounting, independent Cost/main for direct fields. Existing nonfinancial PermissionService defaults/error policy retained, not globally rewritten fail-closed.
4. **Input:** preparation bound IDs/active batch/finite stock; posting validations unchanged and authoritative. No caller price accepted.
5. **Calculation:** payable = opening + purchases − payments − credit notes; cash SR leaves payable alone.
6. **Stock:** existing per-batch and per-product StockPolicy D-93 guard retained; preparation is not reservation.
7. **Atomicity:** existing immediate Tx wraps gate/posting; synthetic SQL rollback and written C# fault test, not C# execution proof.
8. **Duplicates/concurrency:** no new idempotency layer or concurrency certification; preparing twice does not reserve stock. Actual concurrent Windows writes/fault timing remain open.
9. **Audit:** current stored actor used; original stored financial details preserved. Reading raw Audit details still needs both financial grants/main.
10. **Screen/stale state:** nominal3s or next guarded interaction cleanup; dispatcher delays/settings caches and check-to-query races mean no instantaneous/coherent snapshot guarantee. Cost-only changes preserve tray quantity; module denial clears the tray, no fake auto-save.
11. **Print/share:** fresh statement/profile preparation, not recall of already opened previews/files/clipboard/WhatsApp text. Free-form names/notes are not scrubbed.
12. **Bulk/backup:** D-113 explicit editable-file gate and password A unchanged; Supplier statement is deliberate accounting exception, not unrestricted editable-file export.
13. **Error handling:** safe preparation/access/bulk DB error messages; full errors stay local log per existing behavior. Settings malformed/cache behavior not globally certified.
14. **Performance/environment:** small active access snapshot; expired list refresh on cost-state change; full load on module restoration only. Actual WPF re-entrancy/gesture/performance behavior UNRUN.
15. **Recovery/release:** verified backup plus truly separate test DB/photos/VM required. Installer authorized but still unbuilt; full audit, remaining outputs/BookCheck/session/files/cloud/faults and Windows PT-35/36/37 open. Estimate **3–6 further integrated review/result rounds**, findings-dependent, not a delivery guarantee.

[Owner checklist PT-37](POS-PHASE4-PART17-URDU.md) · [decisions](OWNER-DECISIONS.md) · [remaining/installer](REMAINING-WORK-AND-TEST-INSTALLER-URDU.md).
