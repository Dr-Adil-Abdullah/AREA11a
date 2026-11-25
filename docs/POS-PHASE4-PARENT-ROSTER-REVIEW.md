# Phase4 portion20 — parent tabs and administrative vs operational roster reads

> **⏳ checkpoint کی حد:** اس فائل کے نتائج/اعداد اسی درج شدہ checkpoint کے ہیں؛پرانے pending یا not-started جملوں کو موجودہ حالت نہ سمجھیں۔ [تازہ مجموعی رپورٹ](COMPLETE-STATUS-AND-DECISIONS-URDU.md) اور[فیصلوں کا index](DECISION-REGISTER-INDEX-URDU.md) پہلے دیکھیں؛نیا Windows PASS فرض نہیں۔

**v0.98.0 Godwit · release label 2026-11-10 · SOURCE checkpoint, Windows OPEN.**
Baseline: `071f5461a681e2542c05fa5d3947cb79fdb0768c` (portion19). [PT-40 isolated Windows steps](POS-PHASE4-PART20-URDU.md).

## Bounded scope and policy

Continue the authorized Phase4 current-access review: Customers/Suppliers parent tabs, Settings staff roster/selection/permission/pricing display lifecycle, and the existing handover name-picker which formerly shared the unrestricted staff directory. Not a complete Settings, shell, galla, identity/session or admin audit.

- Customers grant controls Customers + Recovery tabs. Suppliers grant controls Suppliers. Either can open its own chapter without the other, Cost/Profit or main ON. Supplier B accounting/inference acceptance and independently gated direct item costs remain unchanged.
- Full staff administration roster is **current active Owner only**, matching the existing Owner-only panel/edit policy; a Staff/Settings grant or Manager title does not make someone Owner.
- Cash handover keeps an **operational** name picker for a current Cash reader, independent of Staff/Cost/Profit/main. It exposes the same four nonsecret identity fields as before, but only active other accounts. A listed name is **not** proof of recipient Cash permission; existing Handover posting still checks its recipient. No new handover eligibility policy.
- No tax, financial formula, below-cost/overpayment, Extra, stock, backup password A, auth secret-write, permission-default, Black box/BookCheck or D-115 change. No new external app use or shop data.

## Method and UI trace

| Path | Current boundary / outcome | Limits / preserved meaning |
|---|---|---|
| `PartnerAccessService.Read(user)` | Same-role current active reader through existing SnapshotReadAccess; Customers OR Suppliers, then separate current-reader module projections | Navigation-only booleans, no money/records; not a capability; helper/permission queries not serialized |
| `PartnerAccess.SelectTab(index)` | Keep allowed 0/1 Customers or 2 Suppliers; fallback first allowed, -1 if none | Physical TabItem indexes preserved, no duplicate navigation or inner header |
| `PartnersViewModel.RefreshAccess` | Failed reader -> no branches; revoke calls existing child access refresh, unsubscribes watchers, drops denied child refs; regrant constructs fresh children | Allowed children retained, ordinary ticks do not replace forms; denied branch's unsaved form is discarded. Detached objects outside this parent are not erased/recalled |
| Parent selection and hints | Cached access clamps selection; denied branches cannot provide EmptyHint | Setter itself is not another DB authorization check; child actions remain authoritative |
| `Auth.ListUsers(actor)` | Actor required, current active Owner checked on opened connection **before** roster SELECT | Old roster SELECT/order retained; includes inactive accounts for administration; no hash columns |
| `Auth.CheckStaffAccess(actor)` | Same current Owner reader; no roster data | UI check only; no role/grant write; no reusable authorization token |
| `Auth.ListHandoverPeople(actor)` | Current Cash same-role active reader, then bound active-other-person SELECT | Four fields only (id/name/role/active), no secret/permission/pricing data; recipient eligibility still posted by unchanged ShiftService |
| `Settings.LoadUsers` | Current owner check, actorful roster; failed roster clears prior admin state | Manager's ordinary Settings form is not rebuilt or granted Owner status |
| `Settings.LoadPermissions/LoadPricing` | Owner refreshed before administrative target reads; selected objects must belong to current Users list; prior pricing display cleared before replacement | PermissionService MapFor error/default policy and PricingAllowance reader semantics unchanged; not one coherent owner/target/grant snapshot |
| Staff actions / pointer gestures | Fresh owner check before add/activate/reset secret/recovery/permissions/photos/pricing and permission action; old removed toggle cannot save | Existing transactional Core writers remain unchanged and authoritative; keyboard native behavior/delayed pointer arbitration retained |
| `Settings.ClearStaffData` | Direct selected ref null, Users/permission collections/pricing values cleared, authored staff-name/PIN/reset/recovery fields cleared, caption/status refreshed | Other ordinary Settings fields are not reset; already-open photo/details windows have their own checks/lifecycle, not force-closed here |
| `Shift.LoadPeople/RefreshPeople` | Calls operational Cash picker, replaces list and rebinds selected ID; failure empties names/selection | Other shift financial reads/history/ByPerson/posting/slip/WhatsApp are explicitly **not** certified by this portion |
| Partners/Settings/Shift view lifecycle | Loaded immediate check and nominal3s DispatcherTimer; Unloaded stops; Settings click timer/cancel path retained | Parent/access/roster refresh only, no new global shell rebuild; dispatcher delay/races/old copies remain |

`Auth.ListUsers()` no-actor overload is removed, not retained as a bypass. Actual callers were Settings and Shift; both updated by purpose. Login/VerifySecret do not use this directory and are unchanged.

## Preservation against portion19

- All **697 baseline tracked paths retained**. **114 other Core service/data files byte-identical**, including ShiftService, Customer/Supplier ledgers, Settlement, pricing/permissions/defaults, Audit/BookCheck, backup and every migration.
- Auth prefix through Login/VerifySecret/Logout and **CreateStaff-to-EOF credential/activation/recovery/write tail byte-identical**. Original roster SELECT exact. One new bound active-other-person SELECT; Auth had no verbatim SQL blocks. No secret, role, PIN hashing, lockout, Forgot password or write-transaction change.
- Shift VM's people-summary-to-EOF (financial commands/handover/slip/WhatsApp) exact; its accounting Load logic unchanged except that its roster call now reaches the scoped reader. Full ShiftService unchanged.
- Partners XAML and all layout/tab/header templates unchanged; ShellViewModel, all Black box/BookCheck code and Audit writer exact. AppServices only registers the new navigation reader.
- Existing Auth write guard was scoped to the **write region** instead of total file count, because the new Owner-only read uses OwnerAccess too. Existing own-financial-checkbox structural guard now follows current `CanManageFinancialVisibility`; Owner still edits their two independent grants.

## Evidence and its limits

- **87 static repository checks PASS**. C# grammar **407 files**; structural resolution **4387 calls / 8594 references / 0 problems**. Not a C# compiler or WPF test.
- **28 new `[Fact]` tests written; 1817 total C# cases UNRUN**. Covers physical tab selection/fallback, own-module independence, current-reader revocation/restoration/deactivation/role/missing/forged IDs, main OFF, Owner-only full roster including inactive accounts, delegated Cash picker isolation, active/self exclusion, safe DB failure, and no audit/account writes. Existing tests unchanged.
- **17 source-negative regressions rejected and exact bytes restored**: owner/Cash gates, inactive filter, parent module/fallback/detach/retention/watcher/hint rules, stale roster/secret/toggle/action cleanup and all three timer stops. Source structural guard only, NOT executable C# mutation testing.
- `tools/check_parent_roster_sqlite.py`: **36 migrations**, actual old administrative/new operational SELECTs and current-ID lookup on synthetic memory; ordering, active/self/other filters, exactly four nonsecret fields, bound ID injection resistance, next-read deactivation, read-only dump and foreign keys PASS. No Python replacement of C# Owner/permission/parent logic is presented as runtime proof.
- All **15** `tools/check_*_sqlite.py` probes PASS; independent backup Python model 6 round trips/24 negatives PASS, not C#/DPAPI/crypto certification. Rules matrix consistency, links/guide anchors/CRLF and `git diff --check` PASS.
- No .NET SDK, Windows, WPF, actual screenshots, printer, installer build, live shop post, session authentication or concurrency runtime test was available. [PT-40](POS-PHASE4-PART20-URDU.md) is **UNRUN**.

## Fifteen-dimension disposition

| Dimension | Bounded review result |
|---|---|
| Purpose/ownership | Parent own-module vs Owner admin vs Cash operational directory separated |
| Identity | Current stored active/role checks; supplied valid IDs still not session authentication |
| Authorization | No Staff grant/rank bypass; financial grants don't replace parent/Cash grants |
| Inputs | Bound reader ID and tab selection fallback, stale target membership checks |
| Query/data minimization | Roster projects four nonsecret fields; picker active others only |
| Accounting | No financial/stock writes/SQL/formula changes |
| Financial visibility | Independent flags/main and Supplier B unchanged; navigation has no new financial gate |
| Attribution/secrets | No new audit/secret writes or hash projections; stale typed staff secrets cleared |
| Outputs | Parent refs/hints and roster lists purpose-scoped; other output windows not globally recalled |
| Stale UI | Detach denied branch/clear staff target and pricing; retain allowed child forms |
| Faults | Safe failures vs successful empty directory; Core writers unchanged |
| Lifecycle | Active-view timers stop on unload, pointer click cancellation retained |
| Data/backup | No migrations/package/password A changes; all baseline files retained |
| Evidence | Static/SQLite/source mutations distinct from unrun C#/Windows |
| Operations | PT-40 isolated test DB/photos required; no installer/readiness promise |

## Residual risk / next scope

PermissionService nonfinancial failure-to-role-default behavior and settings caches are unchanged. Parent and Settings checks can race later construction/target reads; this is not a locked coherent snapshot. A parent transient failure discards its denied/unknown branch and its unsaved forms. Clearing bound staff-secret strings is not secure memory erasure. Existing child objects, paper, clipboard, detached/photo/details windows and files are not recalled by dropping parent references. Dynamic header-section bindings and timer unload/load behavior require actual Windows evidence. Parent service success with both flags false during a grant race safely yields no selection.

This portion does **not** close full Shift/galla monetary reads or Open/Close/Handover atomicity/fault/concurrency/session-authentication review. The existing `Shift.Current()` null-on-failure and actorless History/Discrepancies are still a next candidate; operational directory cleanup alone is not galla privacy certification. Remaining Settings/native/custom/photo/secret-field lifecycle, shell/session/record/fault/concurrency, files/cloud/restore, Windows/DPAPI/new-PC/installer and global Phase4 stay OPEN. No new Black box or BookCheck redesign is authorized. Roughly **3–6 further integrated review/result rounds** remains conditional, not a guaranteed deadline or ceiling. Every portion requires docs plus same-current-branch commit/push and actual remote verification.
