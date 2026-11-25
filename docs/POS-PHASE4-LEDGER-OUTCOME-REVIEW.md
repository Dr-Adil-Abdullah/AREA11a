# ⏳ P42 ledger outcome/retry review — v0.120.0 Shale, 2026-11-10

> **⏳ checkpoint کی حد:** اس فائل کے نتائج/اعداد اسی درج شدہ checkpoint کے ہیں؛پرانے pending یا not-started جملوں کو موجودہ حالت نہ سمجھیں۔ [تازہ مجموعی رپورٹ](COMPLETE-STATUS-AND-DECISIONS-URDU.md) اور[فیصلوں کا index](DECISION-REGISTER-INDEX-URDU.md) پہلے دیکھیں؛نیا Windows PASS فرض نہیں۔

## ⏳ Bounded finding and contracts

| Boundary | Source repair | Remaining proof/limits |
|---|---|---|
| ⏳ Customer Collect / Supplier Pay | Observe existing Database.Tx with separate TransactionOutcome;two definite preflight failures tagged;both catch paths expose observed NoCommitConfirmed;unknown never promises Nothing changed | Actual provider COMMIT-ack/rollback/dispose failures,not synthetic flags |
| ⏳ Customers Collect / Suppliers Pay | Existing reentry bool retained;shared PostingAttemptGuard before service;ObserveFailure before access recheck;ack before value/UI access;only completed cleanup calls CompleteReset | Actual WPF;no request key/cross-page/restart/global identity |
| ⏳ Recovery Receive | Same guard before service/IsBusy notification;ack before slip-ID/UI callbacks;catch says unknown vs SAVED;failed report leaves committed guard latched | RecoveryService report/slip/ageing unchanged;no full Recovery certification |
| ⏳ Input consumption | Clear amount/note immediately after success and again after reload/callback/receipt;Recovery consumes post-selection remaining-balance autofill | Initial customer selection still fills amount as before;normal fresh partial payments remain allowed |
| ⏳ Refresh/regrant | No refresh/access/selection path resets any of the three guards | UI fields/status may refresh,guard persists;page reopen is manual verification instruction,not automatic proof |

✅ Both Core transaction callback bodies and all SQL literal sequences remain unchanged. Current stored actor,permissions,overpayment/settlement,financial audit and SupplierB policy retained. No new cost/main gate on allowed whole-account Supplier Pay;no amount-based dedup,automatic draft,new financial policy or migration. P41 photo changes,other financial services,RecoveryService and old tests unchanged.

## ✅ Executed local evidence / ⏳ UNRUN

- ✅ `tools/check_ledger_outcome_sqlite.py`:37 real migrations;12 actual ledger-SQL BEFORE/AFTER payment,balance,audit fault rollback/retry plans;6 synthetic acknowledgement/refresh/normal/reopen-boundary scenarios. Python owns plan/Tx/guards/observation flags;not C# services,Database.Tx,permissions,actual COMMIT fault or WPF execution. Reopen test explicitly shows a valid partial amount can still create a second payment with a fresh guard.
- ✅ `tools/check_ledger_outcome_source_negative.py`:31 lexical mutations rejected;exact original bytes restored. Tokens/order only;not behavior mutation/security certification.
- ⏳ `LedgerOutcomeFaultTests.cs`:47 new xUnit source cases;2963 expected suite cases ALL UNRUN here.12 real-schema audit/payment/balance stage faults with no-commit/guard/retry,14 definite refusals,6 legitimate partial-repeat/method/book/drawer cases,15 synthetic guard observations.Three page labels document consumers,not actual execution of three ViewModels. No .NET SDK or Windows in this workspace.

## ⏳ Final sweep / publication

✅ Final local sweep:109 static checks;434 C# grammar files;4810 calls/9870 member references with0 static issues. All16 source-negative scripts/459 lexical mutations rejected and original bytes restored;all32 SQLite probes PASS. P41 independent photo-file reference17 file cases/3 actual photo-INSERT rollback plans/1 synthetic lost-ack PASS;backup reference6 round trips/24 negatives PASS;rules-matrix regeneration and diff check PASS.

✅ Preservation against P41 `229ce188383e278cbc9aa90a80a7dae18a7b0a15`:all805 prior paths retained;142 old test paths,37 migrations and158 other Core C# files unchanged. Both changed Core service transaction callback bodies identical;all32 SQL literal sequences retained(CustomerLedger14,SupplierLedger18). RecoveryService,posting guard implementation,P41 photo/parent services and other financial services unchanged. All36 Urdu guide anchors retained.

⏳ These counts do not execute .NET or Windows. Commit/push and independent actual remote hash are a separate gate;final response must report actual result,not assume publication. Static grammar/member checks are not Roslyn,Dapper,provider or WPF execution.

## ⏳ Open gates

⏳ Same-page local guard is not durable/cross-page identity:Customers and Recovery both call Collect but hold separate guards;fresh valid partial payments remain allowed. No claim of global exactly-once. Parent creation UI,other gallery actions,remaining direct callers/session/same-role-ID/CAS/provider/parallel/power-loss/restore-cloud/Windows build-results-hardware-isolation remain OPEN. ❌ Installer binary NOT BUILT;⏳ FullPhase4 OPEN. Only verified encrypted backup AND separate DB/photos/config for physical faults;never synthetic live-shop payments.

[⏳ PT-62](POS-PHASE4-PART42-URDU.md) · [⏳ stable1–36](NUMBERED-WORK-STATUS-URDU.md)
