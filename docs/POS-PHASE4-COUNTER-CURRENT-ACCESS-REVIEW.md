# Phase4 portion32 — current Sale/Return account/module boundaries

> **⏳ checkpoint کی حد:** اس فائل کے نتائج/اعداد اسی درج شدہ checkpoint کے ہیں؛پرانے pending یا not-started جملوں کو موجودہ حالت نہ سمجھیں۔ [تازہ مجموعی رپورٹ](COMPLETE-STATUS-AND-DECISIONS-URDU.md) اور[فیصلوں کا index](DECISION-REGISTER-INDEX-URDU.md) پہلے دیکھیں؛نیا Windows PASS فرض نہیں۔

**v0.110.0 Sandbar**, stable5/6/7/15/17/23;[PT-52 Urdu](POS-PHASE4-PART32-URDU.md),[stable1–36](NUMBERED-WORK-STATUS-URDU.md).

## 1. Source scope and authority contract

- Sale CounterWarnings now requires actor; warning/Quote read current active stored account and matching parsed role through SnapshotReadAccess, then existing POS module permission. Quote uses returned stored user for pricing/financial projection. Create resolves current reader inside owning Tx BEFORE drawer check/Save, passes stored reader to unchanged financial tail/audit.
- Return FindSale/Search/Lines require actor, no actorless overload. Gates before bill payload queries. Quote resolves stored reader before Plan. Create resolves inside Tx before Save/audit. All in-repository callers updated, including existing lookup/PR/return/availability tests; no test assertions removed.
- Both public CheckPostingAccess methods return only verdict, no payload/token; ordinary Owner module bypass preserved only AFTER active/same-kind stored identity validation. Existing PermissionService explicit grants/role defaults remain; failed nonOwner grant reads deny. Financial Cost/Profit/main are independent, no new page financial-grant prerequisite.
- Sale draft PostSale current preflight, current POS gate inside terminal premark Tx and stored-current finalize actor. Existing separate premark/Create/finalize transactions and failure recovery policy unchanged. Failed finalization after successful Create still returns successful document with terminal protection; not whole-draft atomicity or session token. Draft Save/Open/List/Hold/Resume are not certified by this limited PostSale change.
- ReturnsViewModel action checks and nominal3s loaded timer: deny/failure invalidates bill ID, clears bill/form/searchmatches and quarantine lists, disables editing; valid stable tick preserves typed form, regrant requests fresh Find/Refresh, no automatic old selection restoration. Timer stops Unloaded. Existing p31 SAVED/localreentry boundary retained; print preflight denial after commit retains SAVED.
- POS Complete preflight, required warning actor, quote-failure cash/credit/points output cleanup and page-print access preflight. **No full POS timer/cart/picker stale-state erasure or masking design in this portion**; genuine authored cart not silently discarded. Existing receipt/history/quarantine service APIs and prepared files/OS viewers remain separate review. Output preflight is not atomic revocation/instant recall.

Current stored ID+parsed role is NOT authenticated login-session binding. A caller holding a different valid same-role ID is not authenticated by these helpers. Read checks/query are not atomic versionCAS. Other writers/session/registry/remote/commit-I/O/concurrency issues remain OPEN.

## 2. Written C# source regressions —74, compile/runtime UNRUN

`CounterCurrentAccessTests.cs`:69theory rows+5facts; suite **2305** (2231+74).

|Cases|Scope|
|---|---|
|32|8routes (warning,SaleQuote,SaleCreate,ReturnFind/Search/Lines/Quote/Create) ×inactive/rolechanged/revoked/grant-readerror. Deny, bounded financial+draft snapshot unchanged, restore and same-service fresh success|
|32|8routes ×null/zeroID/inactive supplied object/forgedOwner role using real ManagerID; no data/post/mutation|
|3|DeniedSaleDraftPost on inactive/role/revoke preserves draft/marker/payload; regrant can post|
|2|Stored Owner inactive/changedrole refuses all8routes despite stale Owner object|
|5facts|Stored audit name on successful Sale/Return; financial grants/mainOFF retain ordinary allowed workflow with hidden raw quote values; independent POS/Returns grants; denial does not consume retail-confirmation input; valid current Owner retains ordinary module bypass when grants table unreadable|

Snapshot includes settings/customers/batches/sales/sale_items/sale_returns/stock_movements/payments/audit_logs/custom_values/document_drafts/sqlite_sequence, all columns of bounded set, not every table. Actor/grant changes happen before snapshot; identity tables are intentionally not counted as financial rollback mutations. Test harness synthetic MEMORY journal/no fsync not production durability proof.

No .NET SDK/Windows/Inno: all Core/Tests/Desktop compilation,2305tests,WPFbindings/timer/focus/reentry/scanner/printer/camera/session/parallel **UNRUN**. Tests do not instantiate UI. Premark check/use race, identity token forgery, cross-process behavior and actual COMMIT faults are not simulated by these cases.

## 3. Executed evidence / preservation

- New `check_counter_current_sqlite.py`:36migrations,16synthetic current-actor/module denials,unchanged snapshots/regrant,actual Return find/search SQL,module independence and ordinaryOwner bypass PASS. Executes actual Snapshot actor SELECT, Permission grant SELECT and Return lookup queries. **Python owns authority/role/default/denial logic**; it does not execute C# wrappers/Compute/Plan/Database.Tx/draft posting/UI, nor prove application authorization or financial rollback.
- New `check_counter_current_source_negative.py`:24lexical negatives rejected/exact original bytes restored around read/writer gates, stored actor, draft premark/finalization and UI cleanup/timer/source-test registration. Not executable C#/WPF mutation testing; run without concurrent edits/builds.
- Final `.venv/bin/python tools/check_repo.py`: **99 checks PASS;421 C# grammar files;4685 calls /9538 member references,0 issues**. Static grammar/member analysis, NOT compilation. All **23** `tools/check_*_sqlite.py` probes PASS. New24 + counter29 + stock22 + ledger23 + catalog49 + record30 lexical negatives rejected/exact byte restoration. Python backup reference pinned vector, **6 chunk-boundary round trips /24 negatives PASS**, not C# codec/DPAPI/new-PC restore.
- Rules matrix `--check`, exact guide Markdown/HTML parity,36lesson anchors,stable1–36tracker,all tracked Windows-script CRLF and `git diff --check` PASS. Checklist regenerated:51done/6in-progress, not a Phase4 safety percentage.
- Preservation vs published p31 `83af97bed02403e6d253f1505822fc813800c91d`: **all751 baseline paths retained;165 other Core service/data/model files byte-identical;Sale/Return financial tails exact;Draft only PostSale changed**. Existing4testfile assertion counts preserved; required actor argument propagation only. No shop DB or synthetic live shop posting, no secrets requested. Initial source check caught a dynamic role fixture SQL fragment and version/count alignment; corrected fixture interpolation and release/docs, final99PASS. No C# execution inferred.

Financial SaleService tail from `private ServiceResult<SaleResult> Save(` and ReturnService tail from `private ServiceResult<ReturnResult> Save(` unchanged from p31, including pricing/FEFO/Extra/ReturnB/loyalty/settlement/SQL. Draft changes only PostSale block; all165 other Core service/data/model files unchanged. Database/Permission/Auth/SupplierB/Purchase/Shift/StockPolicy/backup/Blackbox/BookCheck unchanged. Existing test methods retain assertions, only required actor arguments added. No schema/data migration or stored historical audit rewrite.

## 4. Same-number remaining work

1. **5/6/23:** actual login-session identity binding, same-role-ID misuse, remaining receipt/history/quarantine/draft-save-open/held/quickadd callers and atomic versionCAS/current-session semantics. Do not call full monetary authorization review closed.
2. **7/15/17:** full POS cached cart/picker/output revocation/masking policy, remaining dialogs/output files; actual WPF Return timer/regrant/focus and p31 postcommit behaviors. Current preflight checks cannot recall a previously displayed/printed document.
3. **2/4/24/25:** execute74+whole2305; remaining stock/expense/recovery/PR/draft/files/realCOMMIT fault matrix, productionjournal/busy/parallel/cross-process and durable request identity/restart/timeout. Local UI latches not exactly-once.
4. **26–29/30–35:** files/encrypted backup/restore/new-PC/cloud, real Windows build/logs/results/fixes/screens/hardware/startup/isolation. Verified encrypted backup AND separate DB/photos/config; exe folder alone insufficient.
5. **36:** authorized single double-click test installer NOT BUILT. Windows/build/tests/install/upgrade/uninstall/data-preservation gates remain. FullPhase4 OPEN;36IDs not36Nexts;3–6rounds estimate conditional, not deadline/upperbound.

Every portion docs+samecurrentbranchcommit/push+actual remote verification. Hash reported only after real publication/verification.
