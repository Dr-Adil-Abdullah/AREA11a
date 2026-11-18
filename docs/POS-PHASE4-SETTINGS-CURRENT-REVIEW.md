# Phase4 portion23 — current Settings writers and secret-safe settings audit

**v0.101.0 Dunlin · release label 2026-11-10 · SOURCE checkpoint; C#/Windows OPEN.**
Baseline: `0c27e0a9016f632093cff66c4434e8e217a96695` (portion22). [PT-43 isolated Windows steps](POS-PHASE4-PART23-URDU.md).

## Ordinary examples / bounded scope

A cashier delegated Settings may change the shop footer, but that grant alone must not change cloud credentials or PIN policy. Revoking Settings must prevent the old open page saving. A cashier with POS may still toggle the Counter's auto-print switch without receiving the whole Settings page. A Backup delegate may save the backup schedule without getting arbitrary settings access. These are existing module boundaries, not a new rank-based financial policy.

The bounded source patch touches SettingsService, its existing Startup and workbook-settings callers, SettingsViewModel and the Settings content visibility. Native/custom fields, photos, complete sessions, Auth login/recovery/credential rules and record authorization are **not** declared finished. Blackbox/BookCheck internals/navigation, backup format/password A, accounting services/formulas/SQL, migrations and pricing allocation are unchanged. New audit writes for the cloud credential are redacted; no historical audit rows are rewritten/deleted.

## Findings and implementation

1. **Generic SetMany had no general current-writer gate.** It checked selected labels/protected settings but ordinary direct callers could write other settings without a current account/module check. It now resolves the active stored same-role writer inside its existing Tx, before settings reads/writes/audits. It audits the stored current identity/name. Missing permission dependency, failed identity/grant read, stale role, missing/inactive account or denied module prevents the entire dictionary. Existing validation and post-commit cache publication remain.
2. **SetUiOnly was a general actorless configuration bypass.** Known-key checking plus a small denylist left cloud keys/security/other configuration writable without an actor or audit. It now allows exactly17 named runtime bookkeeping keys, not a prefix/all-known-key allowance. This is not a security boundary against arbitrary trusted in-process code/raw SQL; it narrows the production service API.
3. **Cloud credential copied into audit old/new.** Future SetMany changes to `sync.key` now log only `secret:not disclosed` / `secret:changed`, not either old or new value. The actual setting remains stored as before for cloud use. This does not encrypt that setting, sanitize historical audit/backups, or alter stored records. Other existing secret/DPAPI/output paths remain separate work.
4. **Startup had intentional actorless callers.** Installer auto-start and Shift-at-startup manual recovery now use an internal fixed-key/fixed-auto-or-manual system method, not generic null-actor Set or SetUiOnly. Installer retains its settings audit; emergency recovery retains its separate protection-reset event. Failed manual-mode persistence is now returned rather than claiming success. Owner SetMode preflights current Settings identity before touching either registry store, then the setting write rechecks current Owner inside Tx. OS/CLI/emergency trust is not redesigned; registry rollback is still best effort and not one SQLite+Windows atomic transaction.
5. **Workbook settings bypass SetMany by design** to keep the whole workbook inside one transaction. Its Settings preview/apply prechecks now use current CheckAccess, and its existing transaction calls RequireBulkWriter before any plan updates. This shares the owner-key classification with SetMany, so a delegated bulk-data user cannot mix an Owner-only setting with ordinary rows and get a partial save. File structure/locked-row format/SQL and independent bulk/financial gates are unchanged; ordinary delegation remains possible.
6. **Open Settings page relied on supplied role/module checks.** Its existing nominal3s active-view timer now checks current Settings access as well as staff access. Failure hides the eight-tab content, disables Save, clears cloud fields/typed key/key labels, staff selection/permissions/pricing/PIN/recovery inputs and label rows. Cloud-only loss clears its fields without discarding unrelated allowed edits. Regrant loads fresh relevant values. Current Owner state drives app identity/startup affordances. No saved key fragments are shown by Settings; the page displays only whether a key is saved. Non-cloud users do not fail ordinary Save because the now-hidden automatic-minutes input is empty. General settings already committed but subsequent startup/focus save failed is explicitly labelled **Settings SAVED**, not an apparent rollback.

## Writer/key trace

| API / keys | Current gate | Preserved / limit |
|---|---|---|
| Set / SetMany ordinary or mixed settings | Current active stored same-role Settings; actual Tx gate | Any denied member denies whole dictionary; no new Cost/Profit/main gate |
| Exactly `printer.autoPrint` | Current POS **or** Settings | Counter toggle retained; adding another key requires Settings |
| Only `backup.autoEnabled/everyHours/keep` | Current Backup **or** Settings | Existing delegated schedule save; other backup keys do not inherit this exception |
| Only cloud configuration (seven named keys) | Current Sync (Settings alone insufficient) | Delegated Sync retained; no network call introduced |
| Cloud mixed with ordinary Settings | Current Settings **and** Sync | Cannot smuggle credentials under an ordinary save |
| Owner-protected settings | Above module gate plus current Owner in Tx | Existing app name/icon/focus/main/bill basis; security prefix/startup/legacy discount caps also Owner-only |
| Recovery hash | Generic and quiet setters refuse | Existing Auth.SaveRecoveryHash Owner Tx/non-secret event unchanged |
| Workbook settings | Current Settings precheck + RequireBulkWriter/current Owner where applicable inside workbook Tx | Bulk grant and independent financial/main still required; no partial-file acceptance |
| SetUiOnly runtime keys | Explicit17-key allowlist; no actor/audit by design | Session6, backup bookkeeping3, sync bookkeeping3, hardware evidence5. Not a certificate of telemetry/physical evidence authenticity |
| System startup | Internal fixed `startup.mode` auto/manual method | Installer/emergency path, not an in-app arbitrary actorless setter or proof of OS authorization |
| CheckAccess / CheckCloudAccess | Read-only current identity/module result | Not a signed-in session token or coherent cross-instance snapshot |

The seventeen quiet keys are `session.lastUser/lastStart/lastSeen/cleanExit/handoverAt/handoverBy`, `backup.lastRunAt/lastFile/lastRestoreSummary`, `sync.lastSyncAt/status/lastError`, and `hardware.testedAt/testedBy/note/testedWidth/passed`. No cloud URL/key/enabled/interval, security policy, startup mode, labels, pricing or shop fields are admitted by this door.

SetMany still validates using the settings cache before Tx; a successful commit updates cache afterwards. This portion does **not** solve cross-instance cache freshness, cache-publish ordering, external-file/registry atomicity, duplicate/retry behavior or a caller mutating input collections concurrently. Outside-Tx CheckAccess identity/grant reads and UI checks have check/query/use races. The actual write rechecks current account/modules on its transaction connection using portion22's synchronous Database.Read behavior. Returned Fail still commits in Database.Tx; new write denials throw before writes.

## Regressions written — 36 new facts, all UNRUN here

- 32 `SettingsCurrentAccessTests`: null/inactive/missing/forged/changed-role/current stored account; revoke/regrant; stored audit name; missing permission dependency/query failure; POS-only precise exception/revoked/mixed dictionary; Backup-only schedule/mixed refusal; cloud-only delegation/Settings-only refusal/both grants/revocation/failure; all other known quiet keys denied and all17 runtime keys retained without audit noise; Owner security/startup/legacy cap policy; future cloud-key old/new redaction; audit-trigger rollback with DB/cache unchanged; protected/invalid dictionary all-or-nothing; read-only access checks; recovery hash refusal; ordinary Settings independent of financial/main.
- 2 `StartupCloseTests`: inactive stored Owner and forged Owner cannot touch either fake registry store (mutation counters). Existing installer/recovery/startup tests remain written regression coverage, not a measured Windows result.
- 2 `WorkbookTests`: bulk delegate with all non-owner grants cannot apply an Owner-only setting mixed with an ordinary setting; Settings revoked after preview denies whole apply.

Sync fixtures that previously used SetUiOnly for configuration now use an explicitly named **test-only direct seed** (`TestData.SeedRawSetting`) and reload. Deliberately invalid configuration remains testable without retaining a production bypass. It verifies one existing setting row was updated and never writes audit rows; no business/test data was posted to the shop.

Expected suite total **1915** (1879 +36). No .NET SDK available: Core/Tests build, xUnit execution, Desktop/WPF, real registry/installer/DPAPI/hardware and genuine screenshots are **UNRUN**. Static/Python evidence is not C#/security/Windows certification.

## Fifteen review dimensions

| Dimension | This portion / limit |
|---|---|
|1. Scope/navigation | Same eight Settings sections; only denied content visibility, not new tabs/menu/Blackbox |
|2. Current actor | Stored active/same-role before settings writes; not real session authentication |
|3. Module delegation | Settings, precise POS/Backup exceptions, Sync-only/cloud-mixed requirements |
|4. Owner protection | Shared owner-key classification for direct and workbook write paths |
|5. Secrets | Quiet bypass closed, future cloud audit redacted, typed sensitive inputs cleared; storage/historical copies not encrypted/sanitized |
|6. Record authorization | No general native/custom/photo/record rollout claim |
|7. Validation | Existing guard/JSON serialization/defaults unchanged |
|8. Accounting | No financial formulas/stock/settlement/Extra/tax behavior changes |
|9. Atomicity | Existing Settings/workbook Tx before all writes/audit; cache after commit |
|10. Fault/retry | Audit-fault regression written; SAVED wording after later failure; duplicate/retry work OPEN |
|11. Concurrency/cache | Portion22 connection behavior reused; outside reads/cache/order/registry races OPEN |
|12. UI/stale state | Existing active-view timer, hide/clear/regrant; native child objects not comprehensively erased/reviewed here |
|13. Files/cloud/backup | No network/backup format change; restore/new-PC/DPAPI integration OPEN |
|14. Keyboard/hardware | Existing keyboard/layout/timer wiring retained; real Windows/device checks OPEN |
|15. Evidence/delivery | Source checks and docs plus same-branch publication; Windows/installer/fullPhase4 OPEN |

## Evidence / preservation

Commands: `.venv/bin/python tools/check_repo.py`, all16 `tools/check_*_sqlite.py`, Python backup reference, `tools/build_rules_matrix.py --check`, source-negative contract checks, `git diff --check`, baseline byte comparison, guide anchor/local-link and Windows-script CRLF checks. Exact outcomes are recorded below. Source-negative checks weaken source contracts then restore exact bytes; they are **not executable C# mutation testing**. No Python authorization surrogate is used to claim this C# fix works.

| Evidence actually run | Result | Does not prove |
|---|---|---|
| Repo checker | **90static PASS;410C#grammar files,4494calls,8921member references,0structural issues** | C# compilation,overloads,provider or WPF behavior |
| All16 existing synthetic SQLite probes | PASS | SettingsService/RequireWriter/Database/Can C# execution |
|23 source-negative mutations | All rejected;exact original bytes restored | Executable C# mutation testing |
| Python backup format reference | Pinned vector,6chunk-boundary round trips,24negatives PASS | Actual C# codec/DPAPI/new-PC restore/security certification |
| Rules matrix `--check` | Up to date | Actual shop behavior |
| Baseline comparison |710paths retained;113otherCoreServiceData exact;Settings validation/default/recovery body,Workbook outside3gates,Startup VerifyClose exact | Whole-application integration |
| Guide/scripts/diff |36lesson anchors,local links,MD/HTML synchronization,three build-labelled Windows scripts CRLF retained;`git diff --check` PASS | Windows rendering/timer/shortcut execution |
| .NET/Windows | **SDK unavailable;1915cases/36newfacts UNRUN** | No runtime/Windows/installer PASS claimed |

Source-negative cases remove/widen the runtime allowlist,transaction writer,owner direct/cap rule,missing-permission failure,cloud gate,POS/Backup exceptions;restore old/new credential audit payloads;remove workbook current/Tx gates,startup current preflight/fixedsystempath/manualfailure propagation;remove currentSettings UI check/cloud-key cleanup/denied visibility/non-cloud minutes guard/hidden key fragments/SAVED warning or a regression. All source bytes were restored after each check. These are structural guard probes, not executable mutation tests.

Preservation baseline contains710 tracked paths. All are retained;113 other Core service/data files (77C# +36migrations) are byte-identical. Database/PermissionService portion22, Auth credential/login/recovery, OwnerAccess, native/custom/photo services, audit writer, Blackbox/BookCheck, Shift/accounting/backup/financial helpers are unchanged. Workbook differs only in Settings access prechecks and the one existing-Tx writer gate; no SQL/file layout change. Settings PrepareBulk/Guard/defaults and recovery-hash write body remain unchanged; Startup close/unlock authorization unchanged. App theme/icon/version labels change only to identify this source build.

## Remaining / next bounded priority

Native/custom field and photo/secret/record boundaries and real session identity remain; do not call this full Settings/session completion. Next examine those remaining record readers/writers/outputs with everyday examples before unclear policy decisions. Then cross-service faults/rollback/retry/concurrency and files/cloud/real encrypted backup/restore/new-PC/DPAPI; Windows1915cases plus Desktop/WPF/timers/keyboard/printer/scanner/screenshots and result fixes; then the authorized single sendable double-click test installer with startup/shortcuts/upgrade/uninstall evidence. **No installer created.** Rough3–6 further integrated review/result rounds stays conditional, not a deadline/upper bound and can increase.

No fake live-shop posts; verified backup and truly separate DB/photos/config are necessary, a second exe folder is not isolation. Keep passwords/PINs/keys/real DB out of chat/source. Every portion needs docs, same-active-branch commit/push and actual remote verification. Earlier checkpoints describe their own time, not current global certification.
