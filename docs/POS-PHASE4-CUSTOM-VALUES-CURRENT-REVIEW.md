# Phase4 portion25 — current custom-value actor / parent reads and stale editors

**v0.103.0 Turnstone · `#705D4E` · release label 2026-11-10 · SOURCE checkpoint, Windows OPEN.**
Baseline `cd4df53acf2077e46ea177c3006f6c0455fd51df` (portion24). [PT-45 isolated Windows steps](POS-PHASE4-PART25-URDU.md).

## Everyday example / scope decision

A customer form opened by an account that was subsequently deactivated must not save extra answers with an old account/name. A missing customer must not be presented as “successfully loaded, no answers”. A separate custom-answer save that commits, then fails to reload, must say **SAVED**, not encourage a duplicate save. Native metadata refresh failure must not leave the old selected requirement available for a stale warning prompt. Successful periodic checks must not churn the read-only grid/selection.

Initial review confirmed existing current Owner checks for Native policy read/write and custom-definition management, and SnapshotReadAccess on public List/Values/DescribeValues/SaveValues. The real gap was the **shared internal value writer** called by parent transactions: it trusted supplied actor and used it in custom-value audit. Another gap was parent existence on the two public answer readers, plus stale UI/failure reporting.

**Explicitly preserved caller policy:** internal value writes now resolve current active same-role account but do **not** introduce a new module grant. POS quick customer creation retains its existing caller-owned policy even if Customers is OFF; public custom-answer reads/save/editor still require Customers. Do not mistake this preservation for certification of all parent commands. Supplier create, product create/update, sale/purchase/expense callers retain their existing checks. No new financial/Owner gate on ordinary answers.

## Implementation trace

| Path | Bounded change | Preserved / not claimed |
|---|---|---|
| Values / DescribeValues | After existing current module check, require positive existing parent from fixed six-table switch before answer SELECT | Inactive master history still readable; hidden definitions/labels/history/order retained; document existence, not new status rules |
| SaveValuesInTransaction | Known entity + positive ID, canonical key, current actor re-read on supplied connection **before parent/definition reads**, stored writer in `custom_value_set` audit | Caller owns transaction/workflow authorization; active masters still required for writes; documents still existence-only; normalization and prevalidation before writes unchanged |
| SaveNewRecordValuesInTransaction | Inherits shared internal check, even when answer map is null/empty | Existing parent callers convert failed validation results to exceptions; a thrown current-account failure rolls back caller writes; no parent service code changed |
| Public SaveValues | Existing current module Tx check retained, then shared writer | No success/partial write on validation failure; provider errors generic for answer read/save |
| CheckValueAccess | Current own-module editor preflight; ID0 means a new form, positive means existing parent, negative/unknown form denied | Supplied ID is not session authentication; read history of inactive masters remains allowed, save still refused |
| Custom editor Load | Start disabled/not loaded; enable only after successful fields and values reads | Successful empty form is distinct from failed read; no allowed periodic reload that would overwrite unsaved text |
| Custom editor access loss | Drop Rows, staged photo path/name/flag, edit/loaded state and refresh captions; show failure status even after Rows empty | Does not clear every parent form field/draft outside this panel or recall external old objects |
| Editor outputs/actions | Current checks before AnswerValues, SetAnswers, staged photo path return, picker entry/return and separate Save | Denial returns empty answers/null staged path, NOT a parent command veto; existing parent workflow checks remain responsible |
| Separate Save then failed Load | Explicit **SAVED; do not repeat; refresh** | Commit is not undone by repaint/read failure |
| Custom editor view | Loaded access check and nominal3s timer; stop on Unloaded; status panel stays visible after denial | Regrant requires deliberate parent reload to restore discarded answers, rather than silently replacing unsaved work |
| Native policy Core | Both public owner read/write additionally reject supplied inactive/nonpositive ID; existing stored OwnerAccess remains authoritative | EnsureDefaults startup provisioning, catalog, critical confirmation, stored owner audit, final-post policy unchanged |
| Native metadata VM/view | Failed read clears Selected/Fields; reload current definition before risk prompt, retain selected ID; report SAVED on postcommit reload failure; loaded3s access check/Unloaded stop; successful ticks keep grid/selection stable, failed checks clear and regrant reloads | IsOwner label still reflects supplied role, not actual session; authoritative Core check remains. Risk confirmation remains per requested change |

Fixed table names: products, customers, suppliers, sales, purchases, expenses. IDs are parameters, not SQL interpolation. Read existence is separate from original `RecordExists` active-master write rule. No delete of old values/definitions, no migration or backup format change. Existing no-op and required/photo/number/amount/date/select validation remain.

## Regression source written — 32 facts / 1977 total cases UNRUN

`CustomValueCurrentAccessTests`: **26 facts** using isolated real-schema TestDatabase and an explicit photos path inside that fixture's temp root (including creator failure cleanup). Anonymous/inactive supplied/missing stored/forged owner/stored inactive/changed-role/revoke/regrant; invalid/nonpositive/missing/existing-empty/inactive parent; new form preflight; internal creation rollback for stale/inactive/forged actors, including no answers; **no new Customers grant on quick create**; current stored custom-value audit name; audit-trigger rollback of public answer change and parent creation; generic read fault; document parent/POS mapping; read-only audit preservation and hidden definition history. Synthetic SQL-only sale parent is a reader fixture, not a posted-sale integration claim.

`NativeFieldCurrentAccessTests`: **6 facts** for supplied inactive/stored inactive/changed-role/forged owner; restored owner plus unchanged critical confirmation; failed policy query not successful empty. Existing FieldGovernance/CustomField/DocumentField/NativeFieldPolicy/photo tests remain.

No .NET SDK here: these sources have **not been compiled/executed**. All1977 expected C# cases, Desktop/WPF build/timers/dispatcher/bindings, camera, actual Windows screenshots and hardware remain **UNRUN**. Source grammar/member checks do not replace the compiler or xUnit.

## Fifteen review dimensions

| Dimension | Result / remaining limit |
|---|---|
|1. Scope/navigation | Custom-answer panel and Native metadata only; custom-definition admin stale UI remains separate |
|2. Current identity | Stored active same-role actor inside value Tx; actual authenticated session still OPEN |
|3. Module grants | Public current own-module gates retained; internal workflow gates deliberately caller-owned, quick-create preserved |
|4. Record identity | Positive existing parent reads; active-master writes unchanged; no row-owner/document-status policy introduced |
|5. Owner policy | Native supplied active/positive plus existing stored Owner; no broader OwnerAccess helper change |
|6. Inputs | Existing normalization/limits/required/photo validation and two-phase prevalidation unchanged |
|7. Financial rules | No cost/profit/main, SupplierB, below-cost, Extra, settlement/overpayment, tax or stock formula changes |
|8. Atomicity | Current actor/custom-value audit share caller connection; existing parent throw-on-fail retained |
|9. Fault/retry | Audit fault regression source and SAVED state; cross-service retry/duplicates/crash/power-loss OPEN |
|10. Concurrency | Existing synchronous per-Database connection behavior; multi-instance/session and check/use races OPEN |
|11. Cached state | This panel drops rows/staged file metadata on failed check; other parent input/drafts/admin/paper not recalled |
|12. Outputs | AnswerValues/SetAnswers/staged-path preflight; old returned dictionaries/references still external |
|13. Files/cloud/restore | No new file/schema/backup-format operation; staged copy and cross-service rollback integration still OPEN |
|14. Desktop/keyboard | Existing controls, selection ID retained for Native action; actual timer/render/caret/focus testing OPEN |
|15. Evidence/release | Source/Python only; docs/Urdu/branding/current-branch publication; global Phase4/installer OPEN |

## Important limits / next

Public Values/DescribeValues check existence on their read connection but are not a single atomic read snapshot across grant/parent/answer SELECTs. Definition list/value load/periodic editor check similarly do not eliminate check/use races. Module revocation does not retroactively erase already returned answers. The new periodic UI check tests **access/parent existence**, not definition-version changes; owner edits a definition while another editor is open still rely on existing fresh save validation.

Parent creation always reaches the internal writer, so current actor is checked even with no custom answers. Parent **updates may skip it when customValues is null**; do not claim they are now fully authorized. Internal helper verifies identity only, not module; revoked-module quick-create remains possible by existing policy. No supplied actor ID constitutes a signed-in session. No row-level secrecy or financial-content classification is added for owner-defined free text. A timer is bounded periodic cleanup, not instant screen recall.

Next: custom-definition manager stale metadata/actions, remaining parent record create/update/read callers, real session identity and other cached outputs; cross-service fault/rollback/retry/concurrency and file cleanup; files/cloud/passwordA/DPAPI/encrypted restore/new-PC integration; Windows1977+Desktop/WPF/keyboard/printer/scanner/camera/results/fixes; then the authorized single sendable double-click test installer. **Installer not produced; fullPhase4 OPEN.** Rough3–6 further integrated review/result rounds remains conditional, can increase and is not an upper bound.

No fake live-shop records. Verified backup and truly separate DB/photos/config paths are required, not just another exe folder. Never send shop data/passwords/keys in chat. Every portion requires docs + same-active-branch commit/push + actual remote verification. Final evidence follows.

## Evidence actually executed / preservation

| Evidence | Actual result | Does not establish |
|---|---|---|
| Repo checker | **92 static checks PASS;413 C# grammar files,4542 calls,9157 member references,0 structural issues** | Compilation/provider/xUnit/Windows behavior |
| All17 synthetic memory SQLite probes | PASS, including new `tools/check_custom_value_sqlite.py` | C# actor/grant/parent guard execution or real shop posting |
| New SQL probe | All36 migrations;source-extracted six-table parent count,current actor four-field projection,values/history SELECTs;bound IDs/entity,hidden/history ordering,inactive vs missing parent,read-only dump/FK PASS | SQL alone still returns orphan answers;the preceding C# parent guard remains essential and UNRUN |
|28 targeted source-negative mutations | All rejected;exact bytes restored | Executable C# mutation testing |
| Python backup reference | Pinned vector,6boundary round trips,24negative cases PASS | C# backup service/DPAPI/new-PC restore/security certification |
| Rules matrix | `--check` up to date | Application runtime policy |
| Baseline/source preservation | All716 paths retained;114otherCoreServiceData files byte-identical;all original SQL strings in both changed services retained;Custom Normalize/validation/write RecordExists tail exact | Full app/session/concurrency/fault safety |
| Guide/scripts/diff |36lesson anchors,Markdown body exactly rendered into HTML,local links checked;three labelled Windows scripts retain CRLF;diff whitespace PASS | Actual WPF typography/keyboard/focus/timer rendering |
| .NET/Windows | **SDK unavailable;32newfacts/1977cases/Core/Tests/Desktop/WPF UNRUN** | No runtime/Windows/installer certificate |

Source-negative cases remove supplied/stored actor/current-writer/entity/canonicalization/audit gates;remove Values/DescribeValues parent checks or editor preflight;reintroduce provider text/new internal module restriction;restore stale AnswerValues/SetAnswers/staged-path/Save outputs and ambiguous postcommit status;retain rows/loaded/edit/staged state;hide failure status;remove both timer stops,Native supplied-active/selection/failure guards or quick-create policy regression registration. These are source contract checks, not execution of the proposed regressions.

Other Core service/data preservation includes all36migrations,Database/PermissionService/OwnerAccess/SnapshotReadAccess,all parent creators/updaters,PhotoService,Settings/Startup/Workbook,Sale/Purchase/Expense/Shift/settlement/accounting/backup/Blackbox/BookCheck/Audit writer. Theme/icon/version/test-runner/checklist identify this source build only. There is no production-ready/installer-ready claim.
