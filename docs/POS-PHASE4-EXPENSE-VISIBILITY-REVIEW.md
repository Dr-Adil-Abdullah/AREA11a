# Phase4 portion12 — expense/drawing outputs, v0.90.0 Dipper

> **⏳ checkpoint کی حد:** اس فائل کے نتائج/اعداد اسی درج شدہ checkpoint کے ہیں؛پرانے pending یا not-started جملوں کو موجودہ حالت نہ سمجھیں۔ [تازہ مجموعی رپورٹ](COMPLETE-STATUS-AND-DECISIONS-URDU.md) اور[فیصلوں کا index](DECISION-REGISTER-INDEX-URDU.md) پہلے دیکھیں؛نیا Windows PASS فرض نہیں۔

**Date:**2026-11-10. **Status:** bounded D-110 continuation. Global financial rollout/Phase4/Windows OPEN. Supplier paid+payable/Pay/SR policy NOT decided. No installer built. Separate financial grants and common main switch retained.

## Accounting distinction — not a new blanket cash restriction

Rent25000paisa and owner cash7000paisa mean cash-out32000paisa. Goods drawing3 tablets at12345paisa means goods cost37035paisa; it is NOT another cash withdrawal and never enters CashOutPaisa. Cost denial masks that goods valuation, not ordinary module-authorized cash expense/drawing amounts. The Stock category on an expense remains a label, not receiving or derived batch cost; changing its tag does not make its entered cash amount subject to purchase-cost masking. User-entered notes/title/custom answers can contain numbers and are not automatically sanitized for inferred purchase costs.

## Trace / public boundary

| Entry | View → VM → Core | Gate/projection |
|---|---|---|
| Mixed history | ExpensesView → ExpensesViewModel.Load → CashOut.List(user) | current active same-role account + Cash before lookup; goods rows AmountKnown=CostPolicy(main+Cost), denied raw AmountPaisa0 and hidden AmountLabel; ordinary expense/cash rows remain known; note/name/date/order/custom answers retained |
| Today | Load → CashOut.Today(user) | same reader; CashKnown distinguishes successful actual0 from denied/failed default. GoodsCostKnown independently gates raw DrawingGoodsCostPaisa and GoodsLabel. CashOutPaisa still expense+cash only, entry count unchanged |
| New voucher | PrintVoucher → BuildVoucherDocument(kind,id,user) | current Cash reader before record query; goods Amount hidden unless Cost/main; ordinary cash/expense Amount readable. Existing bound drawing type constraint prevents requesting a goods ID as cash to bypass mask. Missing/query failure fails preparation; unexpected DB message no longer returned |
| Expense save | SaveExpense → AddExpense → existing transaction | current account/module checked inside transaction before INSERT. Actual cash result, custom-field transaction, audit, no-shift warning and category behavior retained |
| Cash drawing save | SaveCashDrawing → AddDrawing → AddCashDrawing | owner-only plus current account/Cash inside transaction; AmountKnowntrue, ordinary cash unchanged. Expected validation denial handled without raw DB details |
| Goods drawing save | SaveGoodsDrawing → AddDrawing → AddGoodsDrawing → unchanged private SaveGoods | owner-only plus current account/Cash and cost visibility resolved BEFORE write inside Tx; stores real valuation/decreases stock/audits original amount, then projects returned AmountKnown/AmountPaisa inside Tx. No post-commit visibility check/failure added |

UI consumes returned AmountLabel for drawing success, not raw hidden0. Refresh drops old SelectedRow before replacing rows; voucher preparation always rereads. No public actorless List/Today fallback. Existing List empty/default-Today error shapes remain, but failed Today money labels are hidden rather than certified zero. ShiftOpen is unchanged boolean metadata, not a monetary read.

Current supplied IDs/roles are NOT authenticated session tokens. Multiple permission/settings/data reads are not one coherent authenticated snapshot. Main/account changes take effect on the next relevant read under existing Settings lifecycle, not guaranteed instantaneous external-DB revocation. Already prepared objects/documents/status messages/notes are not recalled. Audit/workbook/CSV/backup/cloud and other raw routes remain separately open.

## Preservation actually verified

- All **12 verbatim SQL blocks unchanged**. Private **SaveGoods exact**: same batch/product positive-batch-else-product fallback, quantity conversion, D-93 stock decrement, stock movement, owner_drawings record and full monetary audit. No schema/formula/history rewrite or supplier/payable changes.
- **110** other Core service/data hashes unchanged; all **656** baseline files retained. ShiftService, SaleService, StockService, StockTakeService, PurchaseService and SupplierLedgerService unchanged.
- Existing expense tests now use real stored manager/cashier fixtures instead of invented account IDs; original refusal, money, order, stock and cash-count expectations retained. Existing public read tests pass explicit owner.

## Evidence actually run

- **79 static checks PASS**,390 C# grammar files,4098 calls/7898 references resolved,0 reported issues. Not compiler/xUnit/WPF/security certification.
- **10 negative source mutations rejected/restored:** reader, goods-row grant, raw row amount, today goods amount, goods label, fake0 failed cash label, voucher mask, voucher type constraint, goods result and UI result reuse. Clean guard after restore; not runtime testing.
- New `tools/check_expense_visibility_sqlite.py`:36 actual migrations and extracted mixed-list/Today/expense+drawing voucher SELECTs on synthetic in-memory SQLite; stable kind ordering, cash32000 separate from goods37035, entry3, bound/type-confusion refusals, read-only dump, stock-category label and FK PASS. Does NOT execute C#/current reader/cost projection/VM/write wrappers.
- Seven previous isolated SQLite probes PASS. Guide MD/HTML exact body/36lessons, local links, generated rules/checklist and diff whitespace checked.
- **28 new facts /1579 written C# cases UNRUN**: null/forged/inactive/role/page readers; main/owner/Profit-only/Cost-only/manager; current-service revocation; known0 vs hidden; cash invariance; kind-confusion vouchers; safe error/default; read-only data; hidden goods result with actual stored cost/stock; no drawer move; real Cash grantee expense; current writer refusal; goods rollback; stock label not receiving; financial-grant DB failure masks goods only.
- No .NET/Windows/Inno execution or updated green Windows result. Prior actual v0.80 four failures identified/source-fixed earlier; runtime rerun pending. No live-shop fake posts. D-112 docs + current-branch commit/push required; report actual Git result.

## Remaining / 15-dimension boundary

Money/discount/FEFO/Extra/B560540 unchanged; no full transaction/concurrency/duplicate-fault certification. Current reader tightened here, full sessions/record/delegated writes still open. Purchase form/entry/drafts/results and operational leakage, ordinary supplier ledger/PaySR/warnings, StockMinus, audit/workbook/other exports, files/backup/cloud and full guide/UI/hardware/install drills remain. Generic expense exports do not inherit safety just because these DTO/voucher paths changed. No obsolete feature removed.

**Next coherent priority:** purchase entry/drafts/results and their saved/stale monetary state, plus remaining generic exports as size permits. Do not silently choose the unanswered ordinary supplier accounting exception/restriction policy.

[Safe PT-32](POS-PHASE4-PART12-URDU.md) — [Remaining plan](REMAINING-WORK-AND-TEST-INSTALLER-URDU.md).
