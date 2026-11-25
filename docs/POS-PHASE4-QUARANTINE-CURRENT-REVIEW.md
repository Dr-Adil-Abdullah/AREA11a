# Phase4 portion34 — current quarantine reads/count/decisions

> **⏳ checkpoint کی حد:** اس فائل کے نتائج/اعداد اسی درج شدہ checkpoint کے ہیں؛پرانے pending یا not-started جملوں کو موجودہ حالت نہ سمجھیں۔ [تازہ مجموعی رپورٹ](COMPLETE-STATUS-AND-DECISIONS-URDU.md) اور[فیصلوں کا index](DECISION-REGISTER-INDEX-URDU.md) پہلے دیکھیں؛نیا Windows PASS فرض نہیں۔

**v0.112.0 Shore · 2026-11-10 · stable5/6/7/17/20/23/24/25. Source checkpoint, not Windows certification.**

[PT-54 Urdu](POS-PHASE4-PART34-URDU.md) · [stable1–36](NUMBERED-WORK-STATUS-URDU.md) · [guide](USER-GUIDE-URDU.md)

## Bounded contract

| Entry | Authority / result |
|---|---|
| Pending(User?), History(User?) | Required actor; unchanged SnapshotReadAccess current stored active/same-parsed-role Returns reader before payload |
| PendingCount(User?) | Same reader; explicit ServiceResult<int>; read failure is not successful zero |
| Dispose(User?, returnId, outcome, note) | Supplied manager capability preflight; resolve current stored Returns Owner/Manager inside Database.Tx before target query/mutation; existing target/disposition/outcome/note rules retained |
| CheckDecisionAccess(User?) | Access-only UI preflight; writer repeats inside transaction; not a transferable capability |
| Returns quarantine UI | Current decision capability plus successful load; read-only Returns readers still see lists; detached row guard, local busy/finally, consume committed row/note before reload, SAVED on postcommit failure |

Stored reader supplies disposed_by, movement user_id and audit actor/name. No old actorless overloads or nullable optional-actor escape. Ordinary current Owner bypass and established permission defaults/read-error denial retained. Main/Cost/Profit do not replace Returns grant. Current supplied IDs are NOT authenticated login-session tokens, nor protection against arbitrary same-role-ID impersonation.

## Preservation / deliberate exclusions

- Quarantine AddBack tail and all existing Quarantine SQL literals unchanged. Restocked and Sold return stock by the existing policy; Expired does not add/remove units. Existing destroy-note validation, one-decision rule and sold-on follow-up counter bill policy retained. No new refund/price/cost/stock allocation rules.
- Monetary Sale/Return/Purchase/Supplier/ledger/expense services, receipt layouts, schema, Database/Permission/SnapshotReadAccess/Audit implementations, backup, Blackbox/BookCheck unchanged.
- Existing Return current-account/3second Loaded/Unloaded lifecycle and stale page cleanup retained. New access-only decision capability does not reload an allowed list or erase a stable typed note. After revocation/regrant, use fresh Refresh. The decision UI has no new timer; it uses the existing Return access lifecycle.
- A decision's acknowledged success consumes the row/note before reload. Failed reload returns SAVED/do-not-decide-again, not a false unsaved claim. Exceptions before acknowledged success report unknown outcome; no blind automatic retry.
- PendingCount had no production caller; existing test callers now read its explicit verdict/value. AlertService has its own independent quarantine scan SQL and does not call this service; that scanner and its broader alert policy are deliberately NOT reviewed or certified by this portion.
- Legacy missing/inactive batch and fallback lineage behavior is unchanged. Python checks exercise a valid referenced batch and an available null-reference fallback; this is not complete batch correctness or repair. Actual CAS/provider/busy/productionjournal/crossprocess/COMMIT-I-O boundaries remain OPEN.

## C# regression inventory — compilation/execution UNRUN

`QuarantineCurrentAccessTests.cs`: **79 new cases** =74InlineData+5facts; suite **2479 expected**.

- 6 routes ×4 stored changes/regrant =24; 6routes ×4 invalid actors =24. Routes:Pending/History/Count/restocked/expired/sold. Denials have unchanged financial/stock/disposition/audit/sequence snapshots.
-20BEFORE/AFTERstatementfaults:disposition/batch/movement/audit for Restocked/Sold,disposition/audit for Expired. Rollback snapshot,retry reconciliation,monetary snapshot unchanged and repeat refusal.
-3stored-name/ID cases and3overwrite/repeat cases,one for each outcome.
-5facts: legitimate readonly Returns reader;count/list read failure;main/financial independence;currentOwner validation;missing target/invalid outcome/missing destroy note.
- Existing QuarantineTests/ReturnTests assertions retained;only required reader arguments and count.Value projections changed. The new tests explicitly assert count.Ok, including unreadable count failure vs actual pending1.

## Executed local evidence

- `tools/check_repo.py`: **101 checks PASS**; **423 C# grammar files**, **4718 static calls / 9621 member references**, zero issues. This is not a compiler.
- All **25** `tools/check_*_sqlite.py` scripts PASS. New quarantine probe: **36 migrations**, actual list/history/count/target/update/batch/fallback/movement/audit SQL, **48 current-actor/module denials**, **20 BEFORE/AFTER faults with exact rollback/retry/repeat refusal**, three outcomes, two available fallback scenarios, stored name, readonly/decider distinction and count-read error. Python owns authority/plan/transactions; these are not C# services/provider results.
- All **8** lexical source-negative scripts PASS with exact-byte restoration: **29 new quarantine**,49catalog,24current-counter,29counter-posting,23ledger-posting,29receipt-current,30record-write,22stock-posting. Total235lexical negatives; not executed C# mutations.
- Independent backup Python reference: pinned vector, **6 chunk-boundary round trips / 24 negative cases PASS**. Rules matrix `--check` PASS; no C# codec/DPAPI/actual restore certification.
- Preservation against P33 `0b291d702a79f5b298b62e14335e86ff7ee81e3c`: **all761 baseline paths retained;167 other Core service/data/model files byte-identical;Quarantine AddBack tail and every existing SQL literal exact;Return TakeReturn method exact;2 edited old test files retain assertion counts**. Guide MD→HTML exact,36lesson anchors,stable1–36tracker,Windows scripts CRLF and `git diff --check` PASS.
- **C# build/all2479 tests, Dapper/provider/Database.Tx execution, WPF bindings/timer/reentry/SAVED transitions and all PT-54 Windows steps UNRUN.** No .NET SDK/Windows/Inno here; no installer built.

Source/lexical/Python SQL checks are not C#/Dapper/provider/Database.Tx/session/WPF execution or stock security certification.

## Remaining gates

Current-session binding/same-role-ID/CAS and remaining draft Save/Open/held/quick-add/fullPOS/prepared-output callers remain. Other stock/expense/recoveryPR/draft/file and actual-COMMIT fault matrices,durable request identity,restart/parallel/crossprocess remain. Backup/restore/newPC/cloud and actual Windows2479+Releasebuild/WPF/hardware/startup/isolation logs are required. **FullPhase4 OPEN; authorized single-doubleclick test installer NOT BUILT.** Verified encrypted backup plus truly separate DB/photos/config before isolated Windows tests;no synthetic live shop posts. Each portion docs+same-active-branch commit/push+separate actual-remote verification;36categories≠36Nexts,conditional3–6round estimate is not deadline/upperbound.
