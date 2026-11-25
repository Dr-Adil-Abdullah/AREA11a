# Phase4 portion29 — stable23/24/25: ledger-pair posting integrity and committed UI outcomes

> **⏳ checkpoint کی حد:** اس فائل کے نتائج/اعداد اسی درج شدہ checkpoint کے ہیں؛پرانے pending یا not-started جملوں کو موجودہ حالت نہ سمجھیں۔ [تازہ مجموعی رپورٹ](COMPLETE-STATUS-AND-DECISIONS-URDU.md) اور[فیصلوں کا index](DECISION-REGISTER-INDEX-URDU.md) پہلے دیکھیں؛نیا Windows PASS فرض نہیں۔

**v0.107.0 Breakwater · `#535D7C` · 2026-11-10 · source checkpoint; C#/Windows OPEN.**
Baseline `79eeed472ce42df5e64761a0e50eb164e0821de4` (portion28).
[PT-49 Urdu](POS-PHASE4-PART29-URDU.md) · [Stable1–36 status](NUMBERED-WORK-STATUS-URDU.md).

## 1. Bounded integrated finding

Customer Collect and Supplier Pay already use a current-actor guarded Database.Tx containing payment insert, stored balance update and audit. This portion does **not** change those Core financial commands, Database, SQL, permissions or settlement rules. It adds integrated stage-fault/retry/books/drawer/concurrency source regressions around this **ledger pair**, not all monetary commands.

Their UI still had an acknowledged-commit gap: a successful Core result was followed by reload/callback/receipt outside a postcommit catch. A thrown callback/printing failure could reach generic CommandGuard's retry guidance. Supplier ReloadKeeping silently treated failed List as empty and Pay overwrote it with success. Neither action locally guarded reentrant execution while a nested print/callback dispatcher ran.

## 2. Source changes

1. CustomersViewModel.Collect and SuppliersViewModel.Pay acquire a local in-progress latch; outer finally releases after every path. Reentrant same-instance invocation returns without posting. This is **not** a durable operation token, cross-instance gate, restart/timeout or exactly-once guarantee.
2. Only after Core success, a postcommit try clears authored amount/note, reloads, checks explicit load-success flag, invokes callback and rechecks current access before success/receipt output. Failed reload or later exception says **SAVED/do not receive or pay again/check history** rather than repost guidance.
3. Postcommit catches clear stale page/receipt state; payment inputs also clear on access-denial cleanup. Current service-failure paths recheck access, preserving ordinary validation input unless access was lost.
4. Supplier List/ReloadKeeping tracks known load success, rejects failed/null data and clears stale state; Pay does not certify a failed reload. Shared helper also clears failed SR reloads, but **SupplierReturn's own committed-result reporting remains a separate open route**.
5. CommandGuard and Core service messages/transaction implementation remain unchanged. The repair handles an **acknowledged successful Core result followed by UI failure**; actual ambiguous COMMIT/I/O outcomes and generic Core failure wording under such failures remain OPEN.

## 3. Written C# regressions —32 cases, all UNRUN

| Cases | Scope |
|---|---|
|12 theory rows|Both Collect/Pay: BEFORE/AFTER payment INSERT, BEFORE/AFTER balance UPDATE, BEFORE/AFTER audit INSERT. Compare parent balances/payment/audit/batch snapshot and derived drawer/books; remove fault and exactly one successful retry |
|3 rows|Cash/card/online across both accounts, customer receipt, distinct payment IDs, books and drawer totals |
|4 rows|Competing60000+60000 against100000 owed, same Database or two instances of the same file, both ledger sides. Exactly one successful payment and40000 remainder; not fairness/retry guarantee |
|2 rows|Fresh service after full settlement refuses same amount, no second row; balance guard not idempotency token |
|2 rows|Read permission revoked after commit cannot undo or repost payment; regrant reconciles |
|2 rows|Failed permission-table read denies Manager, not role-default allowance |
|2 rows|Inactive parent leaves no payment/drawer movement |
|5 facts|Partial repeats are distinct new payments; stored audit names; failed second action preserves first commit; independent customer/supplier IDs; accounting independent of financial grants/main and settled SupplierB |

Total suite **2126 cases** (2094+32). No .NET SDK/Windows/Inno here: Core/Tests/Desktop compilation, all C# tests including threaded/provider tests, and WPF callbacks/receipt/reentrancy execution **UNRUN**. No C# concurrency, production-security or Windows PASS claim.

The separate-instance tests use synthetic local databases with the existing test harness's MEMORY journal/no fsync; they are not production WAL/LAN/power-loss/process certification. Two concurrent over-settlement attempts exercise balance protection, not two identical request IDs. Partial same-amount posts can both legitimately commit: deliberately pinned as a limitation, no invented duplicate-denial policy.

## 4. Executed evidence / preservation

- `.venv/bin/python tools/check_repo.py`: **96 checks PASS**, **418 C# grammar files**, **4631 calls /9413 member references**, **0 issues**. Source grammar/member analysis, **not compilation**.
- All **20** `tools/check_*_sqlite.py` probes PASS. New23 lexical negatives, existing catalog49 and record-write30 lexical negatives rejected/exact restoration. Python backup reference pinned vector, **6 chunk-boundary round trips /24 negatives PASS**; not C# codec/DPAPI/new-PC execution.
- Rules matrix `--check`, guide MD/HTML parity/36 lesson anchors, stable1–36 tracker, Windows CRLF, source preservation and `git diff --check` PASS. Checklist regenerated:51done/6in-progress featureboard is not a Phase4 safety percentage. Publication hash only after actual same-branch push/remote verification.
- New `tools/check_ledger_posting_sqlite.py` extracts actual Collect/Pay payment/balance SQL, Audit insert and Shift drawer aggregation. All36 migrations,12 stage faults/rollback/retry,3 payment-method reconciliations,2 file-backed two-connection over-settlement competitions/settlement retries, and distinct partial repeat exercised. **Python owns balance guards, BEGIN IMMEDIATE, rollback and threads**; this does not execute C# permissions/Database.Tx/services/UI or prove global idempotency.
- New `tools/check_ledger_posting_source_negative.py`:23 lexical negative mutations around UI latches, committed cleanup/result/access checks, load verdict and source-test registration; exact original bytes restored. Not executable C#/WPF mutation tests; run alone, not alongside edits/builds.
- Preservation: **736 baseline tracked paths retained;all168 Core service/data/model files byte-identical**. Therefore Customer/Supplier posting SQL, Database/Permission/Auth/Audit/Shift/Sale/Purchase/Return/backup/Blackbox/BookCheck code unchanged. Only Core BuildInfo branding changes outside that set. No live shop synthetic posting.
- Session workspace initially had the exact736-file published v0.106 tree but Git pointed at the original base. Read-only remote fetch/content comparison found zero differing/extra files and no staged changes; same current branch/index realigned to the existing published commit without rewriting working files. This was not a new source change or overwrite of user edits.

## 5. Same-number remaining work

1. **23:** actual C# provider/busy/parallel execution; production journal/durable configuration, cross-process/PC, ambiguous commit/I/O, retries and locking. No transaction implementation rewrite here.
2. **24:** actual32 cases, then broader Sale/Purchase/Return/stock/payment/audit/file/commit fault matrix and preservation/reconciliation. Statement-trigger failures are not all possible commit failures.
3. **25:** durable request identity/restart/timeout/duplicate-submit design still open; no automatic deduplication by amount/time/note. Other commands' postcommit reporting (including SR) and actual UI reentrancy/printing tests remain.
4. **5/6/7:** real authenticated-session binding, remaining callers/parent workflows/version-CAS/other stale caches and Windows focus/regrant proof.
5. **26–29:** file/encrypted-backup/restore/new-PC/cloud isolated integration.
6. **2/4/30–35:** actual Windows2126+build/tests/results/fixes/screens/hardware/startup/isolation; no fabricated evidence.
7. **36:** authorized single sendable double-click test installer NOT BUILT; successful Windows/build/test/install/upgrade/uninstall/data-preservation gates remain. FullPhase4 OPEN.

Every portion docs plus same-current-branch commit/push/actual remote verification. Final hash reported only after publication. Stable36 IDs are not36 Next turns;conditional round estimates are not deadlines or upper bounds.
