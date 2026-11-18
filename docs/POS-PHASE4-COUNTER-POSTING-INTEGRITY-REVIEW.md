# Phase4 portion31 — Sale / customer Return posting integrity

**v0.109.0 Harbour**, stable15/17/19/23/24/25; [PT-51 Urdu](POS-PHASE4-PART31-URDU.md),[stable1–36](NUMBERED-WORK-STATUS-URDU.md).

## 1. Source changes

1. PosViewModel retains existing completion reentry latch/finally across dialogs/print/modal. Only after Core acknowledges success: invalidate consumed draft identity, mark local committed-cart-reset block, clear cart/tendered/split amount/Extra/discount/basis before receipt/modal work. Successful full cleanup releases block; if cleanup fails, Sale/SaveDraft/Hold refuse reuse until explicit Clear cart succeeds. This block is conservative same-viewmodel state, not persisted identity or prevention of queued later distinct bills.
2. Postcommit try/catch covers custom editor/lookups/settings/printing/confirmation. Expected print failures preserve SAVED wording even with popup OFF; print/modal exceptions no longer fall through only to generic unsaved command failure. Null acknowledged result also uses committed boundary. Lookups now expose successful products/customers/barcode reads; not-certified-empty after failed read. No new per-route session authentication claim.
3. ReturnsViewModel.TakeReturn gains own local latch/finally. After success, `_saleId=0` before binding work; ResetBill/form clear precede quarantine reload and print. Explicit pending/history verdict: failed read clears both lists and says not loaded, not certified empty. Successful return message comes BEFORE print, so expected printer failure is not overwritten. Exceptions retain SAVED/do-not-refund-again. No ReturnService financial/settlement changes.
4. **SaleService drawer defect found:** old HasOpenDrawer caught read exceptions and returned true. Now preflight denies unknown and Create rechecks open shift on its owning Tx connection before Save/numbering/stock/money. This enforces existing no-bill-with-closed-or-unknown-drawer policy, not a new optional rule. Other authenticated-session/current-grant boundaries in Sale/Return remain open; this patch is NOT a whole-service authorization audit.

All SaleService source starting at `private ServiceResult<SaleResult> Save(` is byte-identical to p30: Compute/Plan/Save/pricing/FEFO/payment/loyalty/Extra formulas not rewritten. All other167 Core service/data/model files unchanged, including ReturnService/Draft/StockPolicy/Shift/Permission/backup/Blackbox/BookCheck. SupplierB and authored cost/main/Profit rules unchanged. Generic Core failure contracts under actual ambiguous COMMIT/I/O are still OPEN.

## 2. Written C# tests —55, all compile/runtime UNRUN

`CounterPostingIntegrityTests.cs`:53theory rows+2facts; total **2231** (2176+55).

|Cases|Source regression scope|
|---|---|
|18|Sale BEFORE/AFTER sale-number/header/item/batch/movement/payment/customer balance/loyalty/audit faults; full bounded snapshot, no stock/books/points/drawer drift, fault removed then INV-0001 retry|
|26|Return restock14 and quarantine12 BEFORE/AFTER return/Q counters/rows/batch/movement/balance/points/audit faults; original bill preserved; RET-0001 retry, khata/cash split, Q list and overreturn refusal|
|5|Cash/card/online/credit/split sale then full return; stock/points/customer books/derived drawer; existing card/online cash-refund policy pinned, not changed|
|2|Closed or renamed/unreadable shifts table refuses financial writes, recovery then legitimate post; no claim this executes a deterministic between-check race|
|2|Second sale/return item trigger: rollback first item's mutations, preserve earlier committed sale on return failure|
|1|Negative loyalty payment/points redemption roll back after late audit fault; retry respects existing earned/redeemed rules|
|1|Separate bill Extra and ReturnB560 then540 preserved around late second-refund fault/retry, no third refund|

Snapshot tables:settings/customers/products/batches/sales/sale_items/sale_returns/stock_movements/payments/audit_logs/custom_values/sqlite_sequence; all columns, not all possible database tables. Shifts deliberately excluded from snapshot so renamed-table failure fixture can compare financial state. Quarantine is represented by sale_returns and Q setting, not a separate invented table. Existing test harness MEMORY journal/no fsync is not production durability evidence.

No .NET SDK/Windows/Inno here: Core/Tests/Desktop compilation, all2231 C# cases, provider concurrency/busy, WPF collection/binding/modal/callback/printing/focus/keyboard/hardware **UNRUN**. Local block/ID invalidation must still be exercised with actual WPF fault fixtures; Core-only tests do not instantiate these viewmodels.

## 3. Executed evidence and limits

- New actual-SQL Python probe:36migrations,44BEFORE/AFTER Sale/Return statement faults+rollback+retry,stock/points/books/drawer,5methods,2closed/unreadable drawer denials PASS. Extracts actual Sale header/item/payment/movement/balance, Return rows/restock/movement/balance, Settings counter, StockPolicy decrement, Loyalty update, Audit and Shift SELECT SQL. Python owns input plans, guards, refund shares, points arithmetic, transaction boundaries and independent book identity: NOT C# Compute/Plan/permissions/Database.Tx/HasOpenDrawer/Draft/provider/WPF execution. No actual COMMIT-I/O injection.
- New source-negative runner:29lexical mutations rejected/exact bytes restored around committed input, reusable-cart block, drawer gate/failclosed, read verdicts and source-test registration. Not executable C#/WPF mutation testing; run alone without concurrent edits/builds.
- Full `.venv/bin/python tools/check_repo.py`: **98 checks PASS;420 C# grammar files;4666 calls /9490 member references resolved,0 issues**. Grammar/static analysis, NOT compilation. All **22** `tools/check_*_sqlite.py` probes PASS. New29 + stock22 + ledger23 + catalog49 + record30 lexical negatives rejected/exact restoration. Backup Python reference: pinned vector, **6 chunk-boundary round trips /24 negatives PASS**, not C# codec/DPAPI/new-PC restore.
- Rules matrix `--check`, exact guide Markdown/HTML parity,36 lesson anchors,stable1–36tracker,all tracked Windows-script CRLF and `git diff --check` PASS. Checklist regenerated:51done/6in-progress, not Phase4 safety percentage.
- Preservation vs published p30: **all746 baseline paths retained;167 other Core service/data/model files byte-identical;SaleService financial Save/Compute/Plan tail exact**. No shop data accessed or synthetic live shop post; no secret requested. Initial Python seed lacked required batch_no; corrected synthetic fixture then reran all22 probes successfully. Initial source check caught multi-statement fixture literal and expectedcount/version alignment; corrected them, final98PASS. No C# runtime conclusion follows.

### Workspace history recovery

At start the same active branch's local HEAD/index pointed at original base805626e, while GitHub branch was published p30 1f0875c. Read-only comparison found all746 published paths, no extra paths, exact text;20 Windows script byte differences were exclusively CRLF working-tree vs normalized LF Git blobs. No staged changes. Fetched same branch then mixed-reset its HEAD/index to existing published p30 without rewriting working files; status clean. This is not an application patch or user-edit overwrite.

## 4. Next same-number gates

- **15/17/19/23–25:** Execute55+whole2231, broaden partial-recovery/PR/return/bill/loyalty/split/closing integration; otherstock/expense/files/draft premark/finalize/restore/realCOMMIT faults. Production journal/busy/parallel/cross-process/PC and durable request identity/restart/timeout still OPEN.
- **5/6/7:** remaining current-authenticated-session/module callers, versionCAS/cache/stale UI. In particular existing Sale/Return permission checks are not certified current-session binding by this drawer repair.
- **26–29:** files/encrypted backup/restore/new-PC/cloud isolated integration. Draft terminal premark and finalization separate committed transactions: not whole-draft atomic rollback, no DraftService rewrite.
- **2/4/30–35:** real Windows Releasebuild/tests/logs/results/fixes/screens/hardware/startup/isolation. Verified encrypted backup AND truly separate DB/photos/config; separate exe folder is insufficient.
- **36:** authorized single sendable double-click installer NOT BUILT. Successful Windows/build/test/install/upgrade/uninstall/data-preservation gates precede readiness. FullPhase4 OPEN;36categories are not36Nexts,3–6integratedround estimate conditional/notdeadline/notupperbound.

Every portion docs+same-active-branchcommit/push+actual remote verification. Publish hash reported only after push and real remote comparison.
