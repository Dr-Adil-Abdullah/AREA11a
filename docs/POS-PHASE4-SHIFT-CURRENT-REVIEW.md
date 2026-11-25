# Phase4 portion21 — current Cash reads and transactional drawer changes

> **⏳ checkpoint کی حد:** اس فائل کے نتائج/اعداد اسی درج شدہ checkpoint کے ہیں؛پرانے pending یا not-started جملوں کو موجودہ حالت نہ سمجھیں۔ [تازہ مجموعی رپورٹ](COMPLETE-STATUS-AND-DECISIONS-URDU.md) اور[فیصلوں کا index](DECISION-REGISTER-INDEX-URDU.md) پہلے دیکھیں؛نیا Windows PASS فرض نہیں۔

**v0.99.0 Curlew · release label 2026-11-10 · SOURCE checkpoint, Windows OPEN.**
Baseline: `555b5d2bfcc6fdb4e508353ad33a8069b9dc3166`. [PT-41 isolated Windows steps](POS-PHASE4-PART21-URDU.md).

## Scope and policy

Review Shift/galla financial reads, Open/Close/Handover authorization and rollback grouping, stale screen/cached-slip lifecycle, and the Counter/session callers that only need to know whether a drawer is open. Not a complete session, Database concurrency, backup, device, deployment or production certification.

Cash is ordinary operating money: authorized Cash readers retain the current/history/discrepancy amounts and supplier cash movements without a new Cost/Profit/main requirement. The **ByPerson aggregate** additionally retains its Owner/Manager rule. Existing Cash history/discrepancy rows are not newly made Owner-only by this patch. POS-only gets only an open/closed boolean, not drawer totals. Independent financial grants, Supplier B, backup A, below-cost/overpayment/Extra rules and D-115 are unchanged. Black box/BookCheck internals, menu rule and Audit writer are not changed.

## Findings and implementation

1. **Open and Close previously used bare connections**, unlike Handover's transaction. A write followed by audit/discrepancy failure could therefore leave partial state. They now run through Database.Tx, including lookup, expected-cash calculation, shift write, discrepancy and audit.
2. **Current actor and recipient:** `RequireCash(connection,actor)` resolves the active stored same-role account and the saved Cash grant on the **provided connection**. Owner's ordinary module access remains automatic; a missing staff grant uses the existing role default; an actual Cash grant-query error now fails closed for this bounded workflow.
3. **Important connection hazard:** Database stores its active transaction in a shared field, and PermissionService.Can opens another connection. Reusing that generic helper from a new write transaction risks attaching the active transaction to a command on a different connection and then falling back to a role default. This portion deliberately uses same-connection Cash reads. **The shared Database/permission connection pattern in OTHER transactional services is still an urgent next review item, not fixed/certified here.** This is source reasoning, not an observed Windows exception or completed global proof.
4. Handover checks **both** current Cash users inside its existing Tx before reading/writing the drawer. Stored names replace stale supplied names in rows/audit/result. Opener-or-current-senior rules remain. Missing post-write opened/closed result reads throw, rather than returning Fail from a lambda that Database.Tx would otherwise commit.
5. Financial readers no longer offer actorless `Current/LastCounted/History/Discrepancies`. `Page(user,limit)` authorizes first and returns an explicit ServiceResult containing current/previous/history/discrepancies and the current senior flag. Failed read has no Page; successfully closed has `Page.Current == null`.
6. `DrawerState(user)` is a status-only Cash-or-POS read. Counter blocks on failed/unknown state and says so; it no longer fetches a money object just to ask whether a drawer exists. Session expiry continues signing out on any failure and conservatively marks a count/check note on open **or unknown**. Existing note storage keys are retained; wording no longer falsely certifies that an unread drawer was open/closed. No automatic closing or invented count.

## Method/output trace

| Path | Current boundary / failure result | Preserved semantics / limit |
|---|---|---|
| `CheckAccess(user)` | Same-connection current Cash reader, result bool is current senior flag | UI check only, not a token |
| `Page(user,limit)` | Current Cash before financial queries; limit1–500; generic infrastructure failure | Existing current/previous/history/discrepancy SELECTs, genuine closed vs unknown distinct; multi-query read is not one serialized snapshot |
| `DrawerState(user)` | Current stored Cash-or-POS reader before count-only SELECT; errors not successful false | Boolean only. Uses existing SnapshotReadAccess/default/error policy outside write Tx |
| `ByPerson(user,days)` | Current Cash plus stored Owner/Manager before aggregates | Existing range1–3650, per-person and separate shop short/over SQL exact |
| `Open` | Tx before current Cash identity/grant, then closed-state/previous-count/reason checks and writes | Zero float allowed; any opening difference needs reason; stored actor audit |
| `Close` | Tx before current Cash/own-shift-or-senior checks and monetary read/write/audit/discrepancy | Actual minus expected; no invented actual; one-rupee reason retained |
| `Handover` | Existing Tx plus current giving/receiving Cash accounts before drawer lookup | Actual counted becomes next float; source difference stays on old shift; stored recipient name; rollback on missing post-write rows |
| Shift screen Load | Explicit Page failure clears old totals/history/previous closing/discrepancies/person summary/recipient/count inputs/handover cache/status; no certified Rs0 | Legitimate closed page still shows zero current totals and real previous actual; authored Note retained |
| Active Shift timer | Existing nominal3s `RefreshPeople` now checks Cash/current senior too; loss clears, restoration reloads, ordinary allowed ticks refresh picker only | Same Loaded/Unloaded view timer retained; not a live money subscription/instant deadline |
| Open/Close/Handover save result | Failed page refresh after committed action gives **SAVED—do not repeat; refresh** | Printing/refresh failure cannot undo saved money; Handover suppresses auto-print if page/access failed |
| Reprint / WhatsApp | Fresh current Cash before building cached last successful handover snapshot; denial clears it | Builder/layout unchanged. Snapshot is **in memory**, not a fresh DB reconstruction; old paper/PDF/clipboard is not recalled |
| Session note | Open/unknown on expiry -> count/check note; known closed -> no new note | Existing SetUiOnly/Read/Clear best-effort failure semantics unchanged; note write/read failures can still lose a note, global session gate remains OPEN |

`HandoverSlipService.Build` is a pure renderer of a supplied saved result, not a database reader. It remains byte-identical. The old UI comment saying a slip was rebuilt from recorded DB rows has been corrected; the new boundary does not claim immutable artifact recall or authenticated caller identities.

## Preservation and test migration

- **703 baseline tracked paths retained**; all remote files compared before realigning refreshed local history to the published baseline with mixed reset, no content overwritten. Only20 Windows EOL differences existed before normalization.
- **115 other Core service/data files byte-identical**, including financial posting/ledger services, PermissionService/Database/helpers, Audit/BookCheck, backup and all36 migrations.
- All **13 original Shift verbatim SQL blocks exact**, including inserts/updates/ByPerson/history/discrepancies/FillDrawer. Entire `HasDifferenceReason`-to-EOF helper/formula/boundary tail exact. New current Cash identity/grant and count-only state queries are parameterized; no schema changes.
- Existing assertions in Shift/Discrepancy/Handover/Expense/Provisional tests now read through actorful Page, retaining monetary expectations and test counts. `.Value!.Current` cannot quietly turn a failed Page into a successful null-current assertion: failed Page has null Value and dereferencing fails. TestData.EnsureDrawerOpen explicitly rejects failed Page instead of opening on uncertainty. Seven existing test files changed for this API migration, no old cases removed.
- POS/Shell edits are limited to status-only state handling and conservative expiry marking; neither Audit menu nor financial posting paths were redesigned. SessionHandover changes wording only, existing keys/read/clear failure policy retained.

## Evidence — not executable runtime proof

- **88 repository static checks PASS**; C# grammar **408 files**, structural member resolution **4402 calls / 8718 references / 0 problems**. Not a C# compiler.
- **35 new `[Fact]` tests written; 1852 total C# cases UNRUN**. Current/null/inactive/missing/forged/changed users, revoke/restore/financial independence, POS boolean-only, closed-vs-failure, current senior/peer rules, stored names, recipient revocation, Open/Close/Handover audit/discrepancy rollback, post-write read failure, reason/counted float, invalid limits, read-only, neutral session note, Cash grant-read failure and separate-Database parallel opening case. Parallel test is **written, NOT executed**.
- **19 source-negative mutations rejected/restored**: Tx/current actor/recipient/grant binding, senior projection, post-write throws, counted float/name/boundary, cached-slip/input cleanup, action checks, SAVED status, Counter unknown state and conservative session note/expiry condition. Structural source checks only, not executable mutation testing.
- `tools/check_shift_current_sqlite.py`: all **36 migrations**, all13 actual Shift SQL blocks + actual Audit insert, synthetic memory only. Checks drawer source fields, cash vs goods drawing, inclusive/exclusive same-second handover boundary, history/read-only, **six** SQL audit/discrepancy rollback groups, counted next float/one-rupee/FK. Python orchestrates transactions; it does not execute C# RequireCash, Database.Tx, commands, concurrency or receipt rendering.
- All **16** `tools/check_*_sqlite.py` probes PASS; independent Python backup model6roundtrips/24negatives PASS, not C#/DPAPI/crypto certification. Rules matrix/checks/links/guide anchors/Windows CRLF/diff whitespace PASS.
- No .NET SDK/Windows/WPF/printer/actual Windows screenshots/new-PC restore/installer execution. [PT-41](POS-PHASE4-PART21-URDU.md) remains **UNRUN**.

## Fifteen-dimension bounded review

| Dimension | Disposition |
|---|---|
| Purpose/ownership | Current Cash money; POS only status; ByPerson senior-only aggregate retained |
| Identity | Stored active same-role actor/recipient, not authenticated session proof |
| Authorization | Same-connection Cash grants, scoped fail-closed grant errors; no new financial prerequisite |
| Inputs | Existing float/actual/reason/recipient/range rules; bounded page limit |
| Query/data minimization | Exact financial SQL; boolean-only Counter/session reader |
| Accounting | Unchanged expected-cash formula, counted float, source short/over and boundary |
| Financial visibility | Ordinary Cash/history totals preserved; no Cost/Profit projections added |
| Attribution | Current stored actor/recipient names, no secret data queried |
| Outputs | Fresh access before cached snapshot print/share; renderer/layout unchanged |
| Stale UI | Unknown fields hidden, selections/history/handover snapshot cleared; Note retained |
| Atomicity/faults | All three actions Tx; audit/discrepancy/post-write-read failures throw/rollback |
| Concurrency | Immediate Tx serializes compliant separate writers in design; C# parallel test UNRUN, shared-instance concurrency not certified |
| Data/backup | No migrations/backup changes; retained baseline and exact financial SQL |
| Evidence | Static/SQL/Python distinct from unrun C#/WPF/Windows |
| Operations | Safe PT-41, explicit remaining work and installer prerequisites |

## What is left — prioritized, not a completion percentage

1. **Next source priority:** shared Database transaction/connection and PermissionService interaction in other transactional services; test delegated/revoked grants and fault fallbacks on the actual C# stack. Do not treat prior static gates as runtime proof.
2. Remaining Settings/native/custom/photo/secret-field UI and full shell/session/record authorization review. Supplied real account IDs are not session authentication. Read/check/print handoff races, caches, implicit grants/default failures and detached objects remain.
3. Cross-service write/fault/retry/concurrency/data-loss cases. Immediate transactions are not a universal unique-open invariant against rogue SQL or a guarantee for concurrent use of the same Database instance (shared `_transaction`). Commit/power-loss ambiguity and exactly-once retries still require evidence.
4. File/cloud/backup/restore integration, interrupted restore/rollback, genuinely separate DB/photos, original password/new-PC/DPAPI behavior and real artifacts. Password A implementation is not yet Windows-certified.
5. Actual Windows Core/Tests **and Desktop/WPF build**, full1852-case run, keyboard/header/timers, physical58/80mm printer/scanner and genuine screenshots. No Linux static screenshot substitute.
6. Fix returned results, then package/verify the authorized **single sendable double-click test installer**, shortcuts/startup/upgrade/uninstall/new-PC checks. No installer artifact yet; version0.99 is not production readiness.

Black box/BookCheck remain outside additional redesign under D-115. Roughly **3–6 further integrated source/result rounds** remains conditional and may increase with connection/runtime findings; not a deadline, ceiling or percentage. Each portion still requires docs + same-current-branch commit/push and actual remote verification. Global Phase4 remains OPEN.
