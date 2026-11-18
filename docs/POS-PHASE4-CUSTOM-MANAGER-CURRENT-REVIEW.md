# Phase4 portion26 — numbered work item8: current custom-definition manager

**v0.104.0 Knot · `#5A627E` · release label2026-11-10 · source checkpoint; Windows OPEN.**
Baseline `a2eeb91f3cfbc6080eb90ed426f931e8a92939f8` (portion25).
[PT-46 Urdu steps](POS-PHASE4-PART26-URDU.md) · [Stable36-item status](NUMBERED-WORK-STATUS-URDU.md).

## 1. Finding / everyday example

An Owner opened “CNIC”, typed a new label and left the manager open. The stored account was deactivated, or another window changed that definition to a different type/active state. The old manager still enabled controls from `_user.IsOwner`; failed List cleared only Fields, leaving editor/selected ID; Save/Move/Toggle overwrote failed reload errors with success. The “No fields” overlay was unconditional.

Prior Core writes already required the stored Owner **inside Tx**, before target/no-op and audit. This patch does not replace that or claim every direct Core call has optimistic concurrency. It closes the bounded manager UI gap, plus supplied-inactive/nonpositive owner preflight and generic infrastructure messages. No new business policy or module grant.

## 2. Implementation trace — what is done in source

| Path | New bounded boundary | Preserved / limit |
|---|---|---|
| Core Add/Update/SetActive/Move | Reject supplied inactive/nonpositive Owner before existing stored-owner transaction checks | Owner-only definitions; ordinary staff can still answer own-module fields; shared OwnerAccess unchanged |
| Core List manage/includeInactive | Supplied active-positive preflight plus existing SnapshotReadAccess/current Owner | Own-module ordinary List policy unchanged; same six forms/inactive visibility/order SQL |
| Core errors | Generic infrastructure messages for List/Add/Update/SetActive/Move | ValidationException messages remain actionable; details retained in existing internal logs |
| Definition snapshot helper | Pure `CustomFieldDefinitionState.Matches`: ID,entity,key,label,type,options,help,required,active,sort,creation stamp; null never matches | Exact metadata comparison only; not authorization/session/version token/atomic CAS |
| Manager CanManage | Initially false, enabled only after current management List succeeds | No stale supplied-role UI authority |
| Failed List/access | Disable manager, mark load failed, clear rows/selected ID/edit mode/label/options/help/required/type; “Fields not loaded” | Failed read not certified empty; actual successful zero count still shows empty hint/overlay |
| Selection | Reject wrong-tab or detached row; fresh access before edit; prevent selection reentry while replacing rows | IDs are still supplied by trusted desktop code; no session proof |
| Timer/pre-command | Current management List; compare selected persisted definition before commands | Unchanged tick does not replace rows/selection/unsaved text; nonselected real list changes update list while preserving editor text |
| Selected changed/missing | Reload list, clear selection/editor; explicit discarded-unsaved-edits/review warning; abort current action | Never silently auto-apply old text to fresh definition; requires reselect/review; not a merge UI |
| New/Cancel/Save/Move/Toggle/Suggestion | Current check before action; suggestion kind must exist on current form | Document photo prohibition/stable keys/hidden history/required/options/type warnings retained |
| Postcommit refresh | All Add/Update,Move,Toggle use FinishWrite; failed load/missing saved target says **SAVED,do not repeat,refresh** | SQLite commit not undone by UI failure; warnings shown after successful reload |
| View lifecycle/refresh | Loaded current check + nominal3second timer, stop Unloaded; public Refresh for existing shell refresh mechanism | No new inner Refresh button or shell/navigation changes; no forced replacement on unchanged ticks |

Tab change resets editor and reads that form; public Refresh deliberately reloads and may reset edits. Move/Toggle deliberately reload their saved definition, retaining their existing post-action reload behavior. Timer-detected target changes explicitly discard edits to avoid using a stale definition; owner must re-enter/review them. Other record editors' definition-version sync remains separate.

## 3. Regression source — 32 new facts / 2009 expected cases, all UNRUN here

`CustomDefinitionCurrentAccessTests`:14 facts execute the pure snapshot contract when xUnit runs (equal,missing expected/current,each of11 persisted components changed).18 real-schema TestDatabase facts cover anonymous/inactive supplied/missing/forged/stored inactive/changed-role actors; owner reactivation; Manager+Settings still not Owner; all four audit actions use stored current owner name; Add/Update/Hide/Move audit-trigger rollback; hide/restore/rename preserve key/answer; generic failed management List; document photo refusals; hidden own-form listing; no-op/unchanged update/no extra audit. Fixture DB/data are synthetic and isolated; no production filesystem/photo or remote service is used.

These are **written source regressions, not xUnit passes**. No .NET SDK here. Actual Core/Tests compilation,xUnit,Desktop/WPF/timers/bindings/focus/selection and Windows screens are UNRUN.

## 4. Fifteen dimensions

| Dimension | Bounded result / remaining |
|---|---|
|1 Scope/navigation | Stable work item8 only; no shell/Blackbox/BookCheck changes |
|2 Actor/session | Current management List + existing Tx Owner; actual session authentication OPEN |
|3 Grants/delegation | Owner-only definitions preserved; staff answers and POS quick-create policy unchanged |
|4 Target identity | Current selected metadata comparison/own-tab membership; no atomic Core version check |
|5 Inputs | Existing FieldGovernance constraints/options/help/type rules; no free-text financial classification |
|6 UI state | Cached rows/selection/editor clear on denial; unchanged periodic check preserves text |
|7 Accounting | No price/discount/Extra/stock/payment/settlement SQL/formulas changed |
|8 Atomicity/audit | Existing Tx/audit preserved; audit fault regression sources; rollback execution UNRUN |
|9 Fault/retry | Explicit SAVED on postcommit failed reload; retry/idempotency/cross-service faults OPEN |
|10 Concurrency | Selected read/compare before action only; external write can still race before Tx |
|11 Files/backup | No app DB/photo/file format or backup change; no new file side effects |
|12 Outputs/privacy | Denied definition metadata removed from this manager; cannot recall old objects/paper/other editors |
|13 Documents/history | Six forms,document no-photo,stable keys,old values/type-change warning retained |
|14 Keyboard/performance | No new key interception; allowed unchanged tick no list churn; actual WPF/DPI/keyboard OPEN |
|15 Delivery/evidence | Docs/PT/numbered tracker/branding + same-branch publication; globalPhase4/installer OPEN |

## 5. What remains, by the owner's stable numbers

- **8:** Limited source repair done; manager runtime/selection/timer/failure UI still UNRUN. Snapshot is NOT atomic compare-and-swap; delete/recreate/ABA/check-use races and other editors' definition sync are not certified.
- **5,6,7:** Remaining authenticated session/parent record callers/stale outputs. Parent update with no custom map can bypass the shared answer writer; this patch does not fix every parent command.
- **23,24,25:** Integrated DB/file fault/rollback/retry/duplicate/concurrency checks, then **26–29** files/encrypted backup/restore/new-PC/cloud.
- **2,4,30–35:** Windows2009+tests/build/results/fixes/screens/hardware/isolation; then **36** authorized single sendable double-click test installer. **Not produced.**

No new code work was authorized on unrelated Blackbox/BookCheck internals. SupplierB,independent Cost/Profit/main,below-cost,Extra/returnB,overpayment,backupPasswordA and stored history preserved. No fake live-shop posts. Verified backup plus truly separate DB/photos/config required,not another exe folder. Further3–6integrated review/result rounds remains conditional,can increase,not an installer promise.36tracker numbers are NOT36Nexts.

## 6. Executed evidence / preservation

| Actually executed | Result | Not established |
|---|---|---|
| Repo checker | **93staticPASS;415C#grammar files,4564calls,9248member references,0structural issues** | Compiler/provider/xUnit/WPF/runtime |
| All17 existing synthetic SQLite probes | PASS | New C# actor/snapshot/manager/transaction behavior |
|32 targeted source-negative mutations | All rejected;exact bytes restored | Executable C#/WPF mutation testing |
| Python backup reference | Pinned vector,6boundary round trips,24negatives PASS | C# backup/DPAPI/Windows/new-PC restore/security |
| Rules matrix | `--check` up to date | Real application integration |
| Preservation | All721baselinepaths retained;115otherCoreServiceData and alloldCoreModels byte-identical;alloldCustomSQL and entire Values/DescribeValues/value-writer/parent-policy tail exact | Whole app/session/concurrency certification |
| Docs/scripts |36guideanchors+exactMD/HTMLrendering,stable1–36trackerIDs,links checked;three labelled Windows scripts retainCRLF;diffwhitespacePASS | Actual rendering/focus/keyboard/Windows actions |
| .NET/Windows | **SDK unavailable;32newfacts/2009C#cases/Core/Tests/Desktop/WPF UNRUN** | No runtime/security/installer certificate |

Source-negative mutations remove each of11 snapshot comparisons/null guard;weaken supplied active-owner/write/list checks;remove five command preflights;retain management/load/list/help/required state on failure;remove shared Move postcommit verdict/SAVED warning/timer stop/empty-overlay gate/wrong-tab guard;unregister current-owner regression or reintroduce raw provider error. Checks reject all32 and restore exact original bytes. They do not run the C# implementation.

The runner initially needed local Python cryptography dependency restored;after installing it in ignored .venv,the backup reference passed. No .NET toolchain was installed/run and no Windows output was fabricated. Existing accounting/parent/Native/Photo/Settings/backup/Audit/Blackbox/BookCheck service/data files remain exact. Source/static/Python evidence never means C#/Windows/security certification.
