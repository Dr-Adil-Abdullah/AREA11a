# Phase4 portion24 — current photo access and stale gallery cleanup

> **⏳ checkpoint کی حد:** اس فائل کے نتائج/اعداد اسی درج شدہ checkpoint کے ہیں؛پرانے pending یا not-started جملوں کو موجودہ حالت نہ سمجھیں۔ [تازہ مجموعی رپورٹ](COMPLETE-STATUS-AND-DECISIONS-URDU.md) اور[فیصلوں کا index](DECISION-REGISTER-INDEX-URDU.md) پہلے دیکھیں؛نیا Windows PASS فرض نہیں۔

**v0.102.0 Whimbrel · release label 2026-11-10 · SOURCE checkpoint; C#/Windows OPEN.**
Baseline: `da7dd61ed642f3a1dfa7bd02b953e49accfa7f13` (portion23). [PT-44 isolated Windows steps](POS-PHASE4-PART24-URDU.md).

## Scope / everyday example

After Customers is revoked, an old open customer gallery must not keep its cached preview visible indefinitely or allow Hide/Restore/Add/Open through a stale supplied role. Conversely, a current POS-only cashier should still see decorative product covers in the Counter without receiving the Products photo gallery. These are separate boundaries.

Initial Native/custom field review found existing OwnerAccess/SnapshotReadAccess checks in several readers/writers. That does **not** complete their record/stale-UI/session review. This portion changes **PhotoService only among Core service/data**, plus PhotoGalleryViewModel and its window lifecycle. Native/custom fields, creation without staged photos, all-record authorization and actual session identity remain OPEN. No accounting, Blackbox/BookCheck, audit-writer, backup-format or migration changes.

## Findings / implementation trace

| Path | New bounded boundary | Preserved semantics / limit |
|---|---|---|
| List / CheckAccess | Current active stored same-role own-module reader, known entity, positive existing parent | Existing list ordering/hidden flag/missing-file representation; missing parent is failure, not certified empty |
| Add / AddInTransaction | Current gallery writer on supplied Tx connection **before copy**; active parent check still separate | Existing size/extensions/six-active cap, stored file name, copied-file cleanup and caller-owned creation Tx retained |
| Hide / Restore | Current writer before idempotent return/update/audit; existing parent required | No deletion, same row/file; inactive-parent historical visibility management retained |
| SetProductCover | Current Products writer after selected row lookup, before file/order reads/writes/no-op | Existing order SQL/no-op/hidden-photo rule; stored current writer audit |
| PathOf / private Read | Resolve stored row, current module/parent, safe path before returning path | Includes hidden photos as before for authorized module reader; cannot revoke a path already handed out |
| Non-product CoversFor | Current stored same-role own-module reader before cover SELECT | Existing dictionary API/empty decoration fallback; no error-result redesign |
| Product CoversFor | Current active stored same-role actor only | Cross-workflow decoration retained, **no new Products requirement**; not workflow/session authentication, per-ID parent states are not re-filtered here |
| OwnAvatar | Current active same-role actor before existing own-ID active-user SELECT | No Staff grant needed; ID supplied by trusted app, not a proof of authenticated identity |
| Add/cover/visibility audit | Stored current writer/name inside Tx | Existing audit actions/entity/file/order values; historical rows not rewritten |
| Gallery Load failure | Clear selected row, full preview, thumbnails, title/count/hidden captions; CanEdit false | Failure says **Photos not loaded**, not successful “no photos” |
| Gallery selection/Open/actions | Fresh CheckAccess before commands; full preview resolves current PathOf by photo ID instead of opening cached FullPath | Decoded thumbnails come from an authorized List, but check/read/decode/use races remain |
| Gallery window | Existing Load plus nominal3s access timer, stop on Closed; fresh Load after regrant | No full file/decode reload every allowed tick; exact scheduling needs Windows evidence |
| Post-write Load failure | Explicit **SAVED—do not repeat; refresh** status | SQLite commit not undone by failure to repaint/open the saved photo |

RequireGallery validates the entity before ModuleFor, then uses the existing current reader helper on the supplied connection and a table name selected from a fixed four-table switch with parameterized ID. Read/Hide/Restore/Cover of **inactive but existing parents remain possible** under the existing module policy; only adding requires active parent, as before. This is not a new archival-access rule. Orphan rows/unknown stored entities are denied for gallery/PathOf/writes; bulk decorative covers retain their existing per-ID query semantics.

Validation failures are bounded user messages; infrastructure List/Add/Read/Change failures no longer append raw provider errors. The internal ValidateSource path still follows its existing extension/size/file checks. A staged creation failure must **throw out of the parent's existing Tx**: the new current-writer check does so before copying. Existing creator catch/DiscardCopy behavior remains unchanged.

## Regression source written — 30 facts, all UNRUN here

`PhotoCurrentAccessTests` uses the real migrated/copy TestDatabase, a throw-away photos folder **inside that fixture's temp root**, and synthetic non-image bytes (no WPF decode claim):

- Anonymous/inactive supplied/missing stored/forged/stored inactive/changed-role/current staff account; revoke/regrant and role change to another role with default grant; permission query failure denies rather than role fallback. Denials assert no photo/audit/new-file changes.
- Customer photos remain independent of Cost/Profit/main; stored current writer name in audit; invalid entity/nonpositive/missing parent; historical inactive-parent reads/Hide/Restore retained but Add denied; orphan/unknown stored photo entity rejected.
- Product covers for current POS-only user while Products gallery stays denied; anonymous/stale cover callers denied; current Products required for cover change. Staff gallery delegation retained, own-avatar without Staff retained but stale role/inactive supplied account denied. Supplier grant/revoke controls its gallery/covers.
- Generic failed photo read, Add audit-trigger rollback/only-new-file cleanup, Hide/Restore audit rollback preserving file, access/read operations write no rows. An inactive stored Owner attempting customer creation with a staged photo leaves **no parent/photo/audit/file addition** through the existing creator Tx.

Expected suite total **1945** (1915 +30). The existing PhotoTests still cover extension/size/cap/hide/restore/cover/path/avatars. Written tests are not execution: no .NET SDK here, so Core/Tests compilation, xUnit, Desktop/WPF, camera/Windows viewer/timer/registry/device and genuine screenshots are **UNRUN**.

## Fifteen review dimensions

| Dimension | Bounded result / remaining limit |
|---|---|
|1. Scope/navigation | Photo gallery only; no main menu/Blackbox or unrelated page changes |
|2. Current actor | Stored active same-role in reviewed photo paths; no actual session token |
|3. Module delegation | Existing per-entity gates and Staff delegation; own-avatar/product-decoration exceptions retained |
|4. Record identity | Known entity/positive existing parent for galleries/writes; no new archival-state policy; bulk covers not per-ID filtered |
|5. Owner/financial policy | No new Owner-only gallery or financial/main requirement |
|6. Input/files | Existing path lexical checks/extensions/12MB/six-active limit retained; no new codec validation |
|7. Accounting | No financial/stock/settlement/Extra/discount formulas/SQL changed |
|8. Atomicity | Photo DB writes+audit share Tx; staged parent failure throws; files still external side effects |
|9. Fault/retry | Copied-file cleanup retained/new regressions; postcommit SAVED status; duplicates/power-loss OPEN |
|10. Concurrency | Portion22 synchronous connection behavior reused; check/use, multi-instance, file replacement/collision races OPEN |
|11. UI/stale memory | Gallery drops its selection/preview/thumbnail rows/captions; old external references cannot be recalled |
|12. Outputs | Fresh PathOf before preview/viewer; cannot recall Windows viewer/paper/PDF/clipboard/old returned path |
|13. Backup/cloud | Existing file-name format/backup/source paths unchanged; restore/new-PC integration OPEN |
|14. Keyboard/hardware | Existing image OnLoad/freeze/keyboard/layout retained; actual camera/codec/viewer/timers Windows OPEN |
|15. Evidence/delivery | Source/Python evidence and same-branch publication; native/custom/session/fullPhase4/installer still OPEN |

## Limits / remaining work

CoverPath remains a public **filesystem convenience**, not authorization; it retains lexical path restrictions, not a symlink/reparse-point or OS ACL certificate. Files can change after validation or after a returned path; extension/size checks are not image-content validation, existing timestamp/+b naming is not a global collision-proof scheme, cleanup is best effort, and files are not rolled back atomically by SQLite. Do not infer crash/power-loss safety from a copied-file test.

Gallery timer/checks are not immediate screen recall. Other pages' cached covers, shell avatar refresh, all native/custom/record editors, external viewer processes, old paper/files/objects and real authenticated sessions remain separate work. Parent creation **without** a staged photo is not newly authorized by this patch. Existing module defaults and Owner bypass apply only after the bounded current-account checks; supplied IDs are still not authentication.

Next: remaining native/custom field values/record callers and stale editor outputs, actual session identity; then cross-service fault/rollback/retry/concurrency/cache, files/cloud/encrypted restore/new-PC/DPAPI, Windows1945+Desktop/WPF/keyboard/printer/scanner/camera/screenshots and returned fixes; then the authorized single sendable double-click test installer. **No installer produced.** Rough3–6 further integrated review/result rounds is conditional, not an upper bound and can increase.

## Evidence / preservation

Run the repo source checker, all16 existing synthetic SQLite probes, Python backup reference, rules-matrix check, targeted source-negative mutations with exact-byte restore, baseline byte/SQL comparisons and diff/guide/CRLF checks. Exact outcomes are recorded below. No Python authorization surrogate is used as proof of C# behavior.

| Evidence actually run | Result | Does not establish |
|---|---|---|
| Repo checker | **91static PASS;411C#grammar files,4522calls,9036member references,0structural issues** | Compiler/provider/Windows behavior |
| All16 existing synthetic SQLite probes | PASS | New photo C# identity/file/Tx execution |
|21 targeted source-negative mutations | All rejected after strengthening the per-action SAVED contract;exact bytes restored | Executable C# mutation testing |
| Python backup format reference | Pinned vector,6boundary round trips,24negatives PASS | Actual C# backup/DPAPI/new-PC restore/security certification |
| Rules matrix `--check` | Up to date | Actual application behavior |
| Baseline review |713paths retained;115otherCoreServiceData exact;oldPhotoSQL/CoverPath/ValidateSource/RecordExists+cleanup tail and Decode exact | Whole-application integration |
| Guide/diff/scripts |36lesson anchors/local links/MD-HTML synchronized;three build-labelled Windows scripts retain CRLF;diff whitespace PASS | Real WPF rendering/shortcuts |
| .NET/Windows | **SDK unavailable;30newfacts/1945C#casesUNRUN** | No runtime/Windows/installer certification |

Source-negative probes weaken supplied/stored identity,known entity,current product/people covers,List/Add/cover/visibility/PathOf/avatar gates,failed-copy cleanup;restore cached-path preview,remove selection/title cleanup,turn failed gallery into empty/no-photos overlay,remove command preflight,SAVED warning,timer stop or staged-parent regression. The first duplicate-warning probe exposed an existential-token checker weakness; the guard now requires all four write actions' SAVED warning and the final21 probes reject/restore exactly. These are source checks only, not execution of the C# regressions.

Baseline has713 tracked paths; all retained.115 other Core service/data files (79C# +36migrations) remain byte-identical, including NativeFieldPolicyService, CustomFieldService, all creators, Settings/Startup/Workbook, Database/PermissionService, accounting/Shift/backup/Blackbox/BookCheck/Audit writer. Existing Photo SQL contents, CoverPath, ValidateSource and active RecordExists/file-cleanup tail are preserved; only current identity/parent reads are added. Image Decode/OnLoad/freeze remains unchanged. Theme/icon/release labels identify this source build only.

No fake live-shop posts: require verified backup and truly separate DB/photos/config paths, not just another exe folder. Never send secrets or shop DB in chat. Every portion requires docs plus same-active-branch commit/push and actual remote verification; static checks alone are not publication or production certification.
