# Phase4 portion30 — Purchase / Supplier Return stock-posting integrity review

> **⏳ checkpoint کی حد:** اس فائل کے نتائج/اعداد اسی درج شدہ checkpoint کے ہیں؛پرانے pending یا not-started جملوں کو موجودہ حالت نہ سمجھیں۔ [تازہ مجموعی رپورٹ](COMPLETE-STATUS-AND-DECISIONS-URDU.md) اور[فیصلوں کا index](DECISION-REGISTER-INDEX-URDU.md) پہلے دیکھیں؛نیا Windows PASS فرض نہیں۔

Version **0.108.0 Headland**; stable16/18/19/23/24/25, PT-50. [Urdu steps](POS-PHASE4-PART30-URDU.md), [stable1–36](NUMBERED-WORK-STATUS-URDU.md).

## 1. Bounded source change and preservation contract

- PurchasesViewModel.Save: local `_saveInProgress` with outer finally; current access/validation remain before service. After acknowledged `result.Ok`, invalidate consumed draft ID/old last-document identity, then postcommit try clears Lines/PaidText/method, sets returned document identity, resets custom editor, reloads with explicit success verdict and rechecks access before print. Receipt/settings/printer exceptions are caught as SAVED/do-not-post-again. Expected invoice/printer failed results also retain commitment wording. An unexpected null success result is not routed to ordinary unsaved retry guidance.
- Purchase LoadLookups rejects failed/null supplier/product lists rather than certifying empty success; current supplier is rebound to fresh rows. Existing permission masking preserves genuine uncommitted purchase input: no blanket unsaved-data destruction added.
- SuppliersViewModel.PostReturn: own local latch/finally, consumed tray/note/cash flag clear inside postcommit try, supplier and expired-stock reload verdicts, callback followed by current access check, SAVED/do-not-return-again catch with stale-data cleanup. LoadExpired now returns bool; existing ordinary callers can ignore it, committed return cannot.
- Core financial services, SQL/formulas, permission gates, Database/Numbering/Settings/Draft/StockPolicy/Shift, SupplierB, sample stock, purchase paid clamp, return cash-vs-credit and negative payable credit policy unchanged. No schema, production fault-injection switch, global CommandGuard or unrelated Blackbox/BookCheck/backup redesign.

The repair applies **only after the caller receives Core success**. Local latch is same-instance synchronous reentry protection, not a durable operation token, multi-process lock, double-click queue cancellation or exactly-once certification. A deliberate later identical purchase/return can still be a new operation. Generic Core failure contracts under ambiguous actual commit/I/O remain OPEN.

## 2. Written C# regressions — 50 cases, compilation/runtime UNRUN

`dotnet/Area11.Tests/StockPostingIntegrityTests.cs`:47 theory rows +3 facts, total suite **2176** (2126+50).

| Cases | Written assertions |
|---|---|
|18|Purchase BEFORE/AFTER number setting/header/batch/item/movement/retail/balance/payment/audit. Two received batches. Snapshot before/after failure, books/drawer unchanged; remove trigger, single successful retry with PINV-0001, exact item/movement/payment/audit counts |
|26|SR BEFORE/AFTER number/header/batch quantity/item/movement/audit for credit AND cash, plus credit-only balance faults. Preserve original committed delivery and its payment; retry SR-0001 with stock30, correct payable, cash drawer, no invented return payment |
|3|Cash/card/online purchase payment followed by cash goods-back: books, stock and drawer reconcile independently |
|1|Sample line still adds stock at zero cost/zero line total; only ordinary line contributes payable |
|2|Invalid second delivery line rolls back already inserted first; second return-item trigger rolls back first batch decrease while preserving original delivery |

Snapshot includes all columns in settings/suppliers/products/batches/purchases/purchase_items/stock_movements/payments/supplier_returns/supplier_return_items/audit_logs/custom_values/sqlite_sequence. This is a bounded table set, not every possible database table or draft-state snapshot. Dedicated synthetic DB per fixture using existing test harness; not production journal/fsync proof. No SDK/Windows/Inno available: Core/Tests/Desktop build, these50, whole2176, WPF commands/binding events/callbacks/reentrancy/focus/printer/hardware all **UNRUN**.

### Draft boundary — intentionally NOT an all-state atomicity claim

DraftService.PostPurchase performs current-access preflight and terminal premark in a separate committed transaction, calls Purchase.Create, then finalizes separately. Ordinary Create failure attempts RestoreAfterFailure; failure to restore/finalize can leave terminal marker requiring post check. After successful Create but failed finalization it returns the successful document, not a safe-to-repost draft. No DraftService source or policy changed. New fault cases target direct Create/Return, not draft marker/finalize/restore faults. Whole-draft rollback, crash recovery and ambiguous commit remain separate work.

## 3. Executed evidence

Final verification results are recorded below after running the checks; these describe Linux source/synthetic evidence only, never C#/Windows execution.

- New `tools/check_stock_posting_sqlite.py` extracts actual Purchase insert/update/payment/movement SQL, SupplierReturn insert/update/movement SQL, Settings counter upsert, StockPolicy conditional decrement, Audit insert and Shift drawer SELECT. **36 migrations,44 BEFORE/AFTER statement faults+rollback+retry,3 payment methods,sample PASS**. Python constructs plan/values/guards and owns BEGIN/rollback/commit; independent book identity is Python SQL, NOT execution of C# Ledger.Page/permissions/custom values/Draft/Database.Tx/number-prefix cache or real COMMIT-I/O.
- New `tools/check_stock_posting_source_negative.py`: **22 lexical negatives rejected, exact original bytes restored** around latches, acknowledgment boundary, consumed input, reload/access verdicts and regression registration. Not executable C#/WPF mutation testing; run without concurrent edits/builds.
- Final `.venv/bin/python tools/check_repo.py`: **97 checks PASS**, **419 C# grammar files**, **4643 calls /9441 member references,0 issues**. Grammar/member analysis is not compilation. All **21** `tools/check_*_sqlite.py` probes PASS; new22 + ledger23 + catalog49 + record30 lexical negatives rejected with exact byte restoration. Python backup reference: pinned vector, **6 chunk-boundary round trips /24 negatives PASS**, not C# codec/DPAPI/new-PC restore.
- Rules matrix `--check`, guide exact Markdown/HTML parity,36lesson anchors,stable1–36tracker,Windows CRLF and `git diff --check` PASS. Checklist regenerated:51done/6in-progress featureboard is not Phase4 safety percentage.
- Preservation against published p29 `d7333b719d0c938f92d1877bd9e9a3057d89e3aa`: **all741 baseline tracked paths retained;all168 Core service/data/model files byte-identical**. Only Core BuildInfo branding changed outside that set. No shop DB accessed, no synthetic live shop post, no secret requested. A probe initially lacked the extracted Purchase INSERT's `createdAt` binding; fixed the Python fixture and reran all21 probes successfully. Initial source check exposed SQL-fragment literals and version/count alignment; corrected test trigger/projection construction and release/docs, then full97PASS. No C# execution inferred from these fixes.

## 4. Same-number remaining work / next gates

1. **16/18/19:** real UI receipt/refresh/stock/books/drawer/mixed-supplier integration, all permission combinations, actual reentry/binding failure tests; no new complete route certification.
2. **23:** actual C# provider/busy/parallel and production durable journal/fsync, cross-process/PC, real commit I/O/ambiguity and recovery. No concurrency implementation changed here.
3. **24:** execute new50; then Sale/customerReturn and other stock/expense/file/commit/draft fault matrix with independent preservation checks. BEFORE/AFTER trigger aborts do not cover power loss/failed COMMIT.
4. **25:** durable request identity/restart/timeout/exactly-once design OPEN. Sale/customerReturn postcommit UI routes remain separate review. Do not deduplicate legitimate later operations by amount/time/note.
5. **5/6/7/26–29:** actual authenticated-session/CAS/remaining callers/cache, files/encrypted backup/restore/new-PC/cloud integration.
6. **2/4/30–35:** Windows2176+build/tests/logs/results/fixes/screens/hardware/startup/isolation, genuinely separate DB/photos/config after verified encrypted backup.
7. **36:** authorized single sendable double-click test installer NOT BUILT; Windows/build/test/install/upgrade/uninstall/data-preservation gates remain. FullPhase4 OPEN. Stable36 categories are not36 Next turns;3–6 integrated rounds remains conditional, not deadline or upper bound.

Every portion includes docs and same-active-branch commit/push with actual remote verification; publication hash belongs in final report only after that verification.
