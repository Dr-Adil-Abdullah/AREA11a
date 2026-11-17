# Phase4 portion10 — batch read and fresh preparation, v0.88.0 Skylark

**Date:**2026-11-10. **Status:** bounded D-110 continuation, global rollout/Windows/Phase4 OPEN. No posting policy or supplier paid/payable decision changed. The whole Stock/Expenses/Suppliers screen is NOT yet a cost-confidential surface: histories, totals and posting results on those screens remain separately open.

## Trace / contract

| Entry | View → VM → Core | Implemented boundary |
|---|---|---|
| Write-off batch picker | WriteOffView → WriteOffViewModel.LoadBatches → StockService.Batches(user,productId,WriteOff) | active current same-role account + Stock module before lookup; independent CostPolicy/main |
| Expense/goods picker | ExpensesView → ExpensesViewModel.LoadBatches → Batches(user,productId,Expense) | Cash module, not Products/Stock as substitute; same independent cost mask |
| Manual SR picker | SuppliersView → SuppliersViewModel.LoadBatches → Batches(user,productId,SupplierReturn) | Suppliers module; does not define ordinary ledger/Pay/SR financial policy |
| Picker projections | Batches → BatchRow.CostKnown/CostPaisa/ValuePaisa/ValueLabel/PickLabel | denied raw CostPaisa0 AND derived ValuePaisa0; visible labels hidden, not genuine0. Quantity/batch/date/supplier and expiry band/tone unchanged |
| Write-off / goods valuation preview | respective RefreshPreview → Stock.PrepareBatchCost → current product + selected active product/batch | current reader/module and cost/main before money lookup; re-read cost, quantity and pack; denial shows hidden/error instead of stale/masked0 arithmetic |
| Manual SR Add line | SuppliersViewModel.AddLine → PrepareBatchCost(SupplierReturn) → tray only after success | refuses failed, missing, inactive or cross-product batch, revoked page/cost/main before removing existing line; fresh cost/pack/name used |

Batches supports exactly WriteOff/Expense/SupplierReturn purposes. Catalog/Counter/Purchase/PR/unknown enum cannot borrow a batch route merely by passing a convenient purpose. Existing ProductReadAccess supplies the workflow→module map and current active/same-role resolution; this is NOT authenticated-session identity. Relevant module can authorize batch metadata without cost; it never substitutes for cost. Manager/Profit-only is not a cost grant; owner explicit cost denial/main OFF are respected.

## Fresh preparation / retained accounting

`PrepareBatchCost` returns a successful BatchCostPreparation only when authorized. Denial/failure returns no quote; no fabricated0 result. A supplied batch must still be active and belong to the selected active product. Null selection with any live batch is refused, not silently changed to a first batch. SupplierReturn always requires a batch. WriteOff/Expense with genuinely no active batch may preview product-cost fallback, retaining the old preview behavior; this is not permission to post an otherwise invalid no-batch removal.

The effective preview cost retains the existing rule: **positive selected batch cost, otherwise product cost**. Raw batch0 remains known0 when authorized; if product cost is456 and selected batch cost0, the existing effective preview fallback is456, not a new zero-cost accounting policy. This portion does not change actual stored valuation formulas. Pack/quantity are current read inputs, not the originally displayed object. Quote is read-only, not an authorization token or stock reservation for posting.

Refresh is interaction/next-read based: previously returned picker rows, quotes and existing trays are not recalled. Multiple reads/permission checks are not a single authenticated transactional snapshot; concurrent main/account/stock changes and stale session behavior need the remaining audit. Existing role fallback on nonfinancial permission DB failure is unchanged; financial grant failure masks/refuses. No claim of instantaneous external-settings propagation.

## Preservation actually checked

- Entire StockService **WriteOff/History/LossToday/Save/helper tail byte-for-byte unchanged** from portion-start snapshot. All7 old verbatim SQL blocks retained, including batch query. Two additional SELECT blocks implement preparation; no mutation added.
- **110** other Core service/data hashes unchanged; **648** portion-start files retained. StockTakeService, ExpenseService, SupplierLedgerService, SaleService and ReturnService unchanged. No migration, history rewrite, transaction/formula/payment policy change.
- All three Desktop batch callers now pass actor and workflow. Existing ExpiryTests updated to explicit owner context; expiry-band expectations untouched. ProvisionalReturnService has a distinct private BatchRow, not changed by the public picker model flag.

## Evidence actually run vs not run

- **77 static checks PASS**,388 C# grammar files,4034 calls/7636 member references resolved,0 reported problems. No C# compiler/runtime certification.
- **10 negative source mutations rejected/restored**: actor gate, Profit replacing Cost, rawcost mask, valuation label, preparation cost gate, product/batch constraint, null-selection fallback, stale write-off cost, fake0 expense denial and bypassed manual-SR denial. Guard clean after restoration; not Windows execution.
- `tools/check_batch_read_sqlite.py`:36 actual migrations and extracted batch/product/preparation SELECTs on synthetic in-memory SQLite; active/order/zero/no-date/supplier metadata, same-product constraints/bound parameters, no-row states, read-only dump/FK and next SELECT seeing changed cost/stock/pack PASS. Old object remains stale intentionally. Does NOT execute C#/permissions/projection/preparation refusal or VM.
- Five existing isolated SQL probes PASS; guide MD/HTML exact body/36 lessons and local review links checked; generated rules/checklist and diff whitespace clean.
- **28 new C# facts /1521 total cases written, UNRUN**. Covers actors/roles/account, supported purposes, three module gates, Products not substitute, manager/Profit-only/Cost-only, owner/global denial, revocation, no fake0 preparation, current cost/pack/quantity, product-batch constraints/inactive/missing records, no silent first-batch fallback, known0/effective fallback, grant DB failure and unchanged data after reads.
- No .NET/Windows SDK/Inno execution here, no installer artifact. Prior v0.80 four actual failures identified/source-fixed earlier; patched runtime rerun still pending.

## Remaining / 15-dimension boundary

Cost reads/preparation focused; money rounding/discount order/FEFO floor/Extra/B560540 and posting/history unchanged. Frozen stock-take Current/History/Sheet/Print/CSV, stock History/LossToday/WriteOff result, expense histories/results/vouchers, supplier ledger/Pay/SR warnings/results, purchase entry/drafts/results and audit/workbook/other exports remain open. Account/ID checks here do not close sessions/delegated writes/arbitrary-ID authentication. Full finance/rollback/duplicate/concurrency, file bounds/backup/cloud and actual Windows keys/DPI/printer/scanner/install drills remain open; complete36-lesson action audit not certified. No obsolete feature removed.

**Next:** focused stock history/result and frozen stock-take output boundaries while preserving frozen costs and apply formulas; expense result/voucher paths alongside as size permits. Do not choose the unanswered ordinary supplier inference/payment policy. Continue docs + current-branch commit/push every portion per D-112.

[Safe PT-30](POS-PHASE4-PART10-URDU.md) — [Remaining plan](REMAINING-WORK-AND-TEST-INSTALLER-URDU.md) — [Financial rollout](POS-PHASE4-FINANCIAL-VISIBILITY-REVIEW.md).
