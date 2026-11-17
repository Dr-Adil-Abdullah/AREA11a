# Phase4 portion8 — Staff financial permission inspector, v0.86.0 Reedling

**Date:**2026-11-10. **Status:** requested UI/owner-read implementation checkpoint, NOT global financial rollout completion or Windows certification. The owner's new custom response requested a dedicated sensitive-permission section, hover summaries, double-click deep dive with live enabled/disabled assignment list and secure persistent backend enforcement. Earlier D-110 independent Profit/Cost decision is retained; the combined-label example is not treated as approval to merge them.

## What changed / operational trace

| Entry | View → VM → Core / database | Implemented boundary |
|---|---|---|
| Staff financial group | SettingsView → FinancialPermissions in SettingsViewModel | separate View Profit, View Purchase Cost, Supplier ledger access (Suppliers), Customer ledger access (Customers). Existing keys `profit`, `purchasecost`, `suppliers`, `customers`; no new blanket grant or role-default changes |
| Other module checkboxes | SettingsView → PagePermissions | every existing module appears once across both groups; shared hover/help behavior. Main switch remains in Sales rules, not a duplicate unsaved switch |
| Hover | PermissionToggle.Summary → PermissionDocumentation.For | short module-specific explanation for every existing key; no staff query on hover; disabled boxes show tooltip too |
| Details / double-click | SettingsView pointer handler or keyboard-accessible Details button → SettingsViewModel.OpenPermissionDetails → PermissionDetailsDoor / Window / VM | scope, prerequisites, explicit current rollout limits; read-only staff assignment list. Native Space continues toggling focused enabled checkbox |
| Live staff list | PermissionDetailsViewModel.Refresh → PermissionInspectionService.Inspect → users/user_permissions/settings | only active supplied owner AND active current stored owner; unknown keys rejected; narrow columns with no secrets; parameterized module; one local deferred read transaction, all Dapper queries explicitly use it |
| Configuration write | existing checkbox Allowed → OnPermissionChanged → PermissionService.Set | existing audited current-owner write transaction and immediate per-checkbox persistence; failed save restores checkbox. Modal itself never writes assignments |
| Auto-refresh | PermissionDetailsWindow DispatcherTimer → VM.Refresh every3s nominal + Refresh now | new DB snapshot each time. Before read, clear names/counts/main state; failed/revoked owner does not retain successful staff list. Stop/detach timer on close |

The inspector distinguishes **Assigned grant** from **Access conditions**. A saved enabled grant can be blocked by inactive account or main OFF. Main ON/financial grant is only eligibility on separately granted pages, NOT proof that every page is granted. Source column distinguishes explicit override, role default and protected owner automatic modules. Owner's explicit financial denials are respected; own denial does not prevent inspecting/administering permissions. Inactive staff remain listed with their actual saved/default grant and a blocked account state.

The saved main-switch value is read directly from DB in the inspector, so unsaved Settings controls are not displayed as if persisted. This does not change the existing process-local SettingsService cache elsewhere or introduce cross-process instant authorization invalidation. The read snapshot is coherent, not instantaneous revocation: an in-flight read may reflect its start snapshot and subsequent refresh sees changes. This is current-account consistency, NOT arbitrary-ID/session authentication.

## Double-click safety

Simply binding MouseDoubleClick to a normal immediately-writing CheckBox would let the first click save a grant before the second opens help. Instead pointer-down is handled without the native CheckBox click; Windows `GetDoubleClickTime` determines a deferred single-click interval after mouse release. A second down cancels the pending toggle and opens Details without changing it. Mouse release outside cancels. A pending old-selection object cannot save after person selection/refresh because VM verifies it is still in the current Permissions collection. Unload/DataContext change cancels pending pointer work. Keyboard Space uses the existing native two-way checkbox path. Disabled owner nonfinancial boxes have an independent Details button for access to help.

These are source contracts, not observed Windows gesture results. Double-click timing, capture, drag-out, keyboard alternation, closing/reopening and DPI must be tested on Windows. The one new P/Invoke reads the OS click interval; it does not intercept global keys.

## Security and scope NOT completed

- **No existing financial service/data SQL or posting formula changed in this portion.** Supplier payments/returns, PurchaseService, StockService and migrations remain byte-for-byte unchanged from portion start.
- `Supplier ledger access (Suppliers)` is the existing page/workflow permission, not a newly invented balance-only grant, not an automatic cost grant, and not an answer authorizing a supplier balance exception. Customer ledger is likewise its existing module. Neither module is globally disabled just because main profit/cost switch is OFF.
- **D-110 all-route backend propagation remains OPEN:** purchase invoices, supplier ledger/expired/batch paths, stock-take/stock-minus/write-off/expense histories, audit/workbook/other exports and prepared objects. These are prominently labelled in the new UI. No mathematical-secrecy or "no leaks anywhere" claim.
- The pending supplier decision remains unresolved by the new UI request: masking purchases while exposing paid+payable reconstructs purchase total; hiding all related money affects payment/SR usability. Do not silently decide a new financial-action rule on the strength of a UI request. Continue the already-settled independent checkbox policy without re-asking it.
- Per-user grant persistence uses existing SQLite/audit transactions. This is owner authorization, not new encryption-at-rest or a substitute for OS/database-file access controls.
- Public help text contains no account assignments. Assignment endpoint cannot be used by a manager merely granted Staff/Settings. No PIN/password hash/recovery/cloud key selected or returned.

## Evidence actually executed

- **75 static checks PASS**;386 C# grammar files;3962 calls/7465 member references resolved;0 reported problems. Static grammar/member/binding/contracts are NOT a .NET build, xUnit or WPF execution.
- **10 negative source mutations rejected/restored:** stored owner gate, supplied active owner gate, module-bound join, read snapshot transaction, main-OFF status, stale-name clearing, double-click branch, OS interval, timer detachment and financial-group binding. No Windows input was simulated by these static probes.
- `tools/check_permission_inspector_sqlite.py`:36 real migrations, source-extracted owner/main/assignment SELECTs, synthetic temporary SQLite only. Nullable defaults/independent grants/inactive listing, read-only dump equality, parameter binding and FK PASS. Independent two-connection read snapshot remains coherent while second writer revokes; next transaction observes deactivation/revocation/main OFF. Not C#/Dapper or authentication execution. The initial probe accidentally compared an old active flag to a newly deactivated row; only that independent-script expectation was corrected to examine parameter scope, no application behavior changed to satisfy it.
- All **110 pre-existing Core services/data file hashes unchanged**; all632 portion-start baseline files retained. No posting/migration/data changes in this portion.
- Existing actual-SQL BookCheck snapshot and Extra synthetic checks PASS; actual old v0.80.0 four failures all identified earlier, **no updated Windows green run received**.
- `git diff --check` clean; main guide MD/HTML rendered-body parity/36 lessons, generated rules/checklist maintained. No synthetic live-shop posts, installer, commit or push.

## Written C# regressions — NOT executed

**20 new facts** in PermissionInspectionTests; **1469 total written cases**. Covers every help key, separate financial keys/sensitive ledger grouping, null/inactive/manager/forged/missing/demoted owner refusal, unknown module, manager defaults, current grant/revocation/main state, direct persisted switch vs cache, owner financial override vs protected owner module, inactive users, changed stored name/role, DB failure, malformed switch, module-vs-financial status and no grant/settings/audit/financial writes. Actual modal lifetime, clipboard/screenshots, mouse/keyboard/DPI and C# transaction behavior remain UNRUN.

## 15-dimension continuation

Money/rounding, discounts, batch cost, saved history/returns: unchanged implementation, no new monetary runtime proof. Stock/atomicity/concurrency and duplicates: unchanged; read snapshot tested independently only. Permissions/record IDs: new owner-only read endpoint focused; full sessions/auth remain OPEN. Input/files/secrets: bound module/narrow projection, export/archive/file controls not completed. Backup/cloud/other finance: unchanged; no new secrecy claims. Draft/Park: pending objects guarded in this UI only; financial draft lifecycle OPEN. UI/native keys: source gesture handling and modal added, Windows evidence pending. Guide/obsolete: lesson31/current status updated; no feature/data removal or full36-lesson PASS.

[Owner-safe PT-28 checklist](POS-PHASE4-PART8-URDU.md). Remaining financial rollout: [portion7 matrix](POS-PHASE4-FINANCIAL-VISIBILITY-REVIEW.md). Old Windows failure mapping: [actual report](WINDOWS-V080-TEST-REPORT-REVIEW.md).
