# Phase4 portion28 — stable5/6/7: current Catalog/Unit writers and windows

> **⏳ checkpoint کی حد:** اس فائل کے نتائج/اعداد اسی درج شدہ checkpoint کے ہیں؛پرانے pending یا not-started جملوں کو موجودہ حالت نہ سمجھیں۔ [تازہ مجموعی رپورٹ](COMPLETE-STATUS-AND-DECISIONS-URDU.md) اور[فیصلوں کا index](DECISION-REGISTER-INDEX-URDU.md) پہلے دیکھیں؛نیا Windows PASS فرض نہیں۔

**v0.106.0 Estuary · `#586C48` · 2026-11-10 · source checkpoint; Windows OPEN.**
Baseline `4c60952544ba462229b345cf63a0c3ef3af1c171` (portion27).
[PT-48 Urdu](POS-PHASE4-PART28-URDU.md) · [Stable1–36 status](NUMBERED-WORK-STATUS-URDU.md).

## 1. Finding / scope

Catalog and unit commands trusted supplied CanManage. A Manager whose stored account/Products grant changed could use a previously opened form, including no-op branches, unlinking product catalog relationships or renaming current unit labels. Windows retained lists/selection/authored inputs, CanEdit was constructor-supplied role, failures were sometimes overwritten by success/left stale lists, and generic Core errors appended provider messages.

This is the announced remaining5/6/7 Catalog/Units slice plus24/25 fault/result regressions, not a new business-policy decision. Owner/Manager writers and legitimate Products read-only readers remain; **no Cost/Profit/main requirement** for catalogs. No sales/pricing/stock-quantity formula, supplier exceptionB, quick-customer, backupA or Blackbox/BookCheck redesign.

## 2. All16 reviewed Core writers

| Family | Methods |
|---|---|
| Category | AddCategory, UpdateCategory, SetCategoryActive |
| Company | AddCompany, UpdateCompany, SetCompanyActive |
| Salt | AddFormula, UpdateFormula, SetFormulaActive |
| Brand | AddBrand, UpdateBrand, SetBrandActive, SetBrandCompany |
| Unit | AddUnit, UpdateUnit, SetUnitActive |

Each rejects supplied null/inactive/nonpositive/nonmanager; inside Tx, **before reads/no-op/mutation**, existing RequireProductWriter resolves current stored active/same-role Products Owner/Manager. Stored actor feeds all audit calls. Validation denials remain useful; generic exceptions log details locally and return fixed non-provider errors. All existing SQL/rule/unlink/history behavior remains.

`CheckCatalogAccess(User?)` returns current Products reader's edit capability. Valid read-only Products reader gets success/false, not promoted by a module grant. No cost/profit values returned. Actorless public lookup APIs remain **caller-gated helpers, not authenticated endpoints**; other consumers are not newly certified.

## 3. UI boundaries

1. CatalogWindowViewModel and UnitWindowViewModel derive CanEdit from successful current capability plus successful explicit list load; no constructor role-only enable.
2. Failed current read/list clears all cached rows, selections, catalog parent/brand-company IDs, names/orders, unit WholeOnly; CanEdit=false and successful-empty overlays suppressed. Explicit Refresh/review required after regrant; discarded inputs not restored.
3. New/Save/Toggle/ClearBrandCompany actions require fresh capability. Detached selection is rejected. Stable valid access-only ticks do not Load or overwrite authored edits.
4. Both windows check Loaded and nominal3s Tick, stop Closed. Not a hard3s/instant-recall guarantee; actual dispatcher/focus behavior UNRUN.
5. All8 catalog save/toggle paths plus partial Brand-link failure use ReloadAfterWrite; both Unit write paths use shared verdict. Commit followed by failed/throwing/missing-target reload says **SAVED/do not repeat/refresh and review**. Fresh selection must match saved ID rather than silently accepting first-row fallback.
6. Existing Brand name/ordering and company link remain **two transactions**. If link fails after name commit, partial SAVED message; no atomicity claim. ProductsViewModel rechecks access/invalidated editor on returning from either manager while retaining legitimate read-only catalog navigation.
7. Product form inline quick-add paths receive transactional Core guard, but their full postcommit refresh UX is outside this slice. Already-open other dialogs/lookup consumers/current-content conflicts are not globally solved.

## 4. Written regressions —49 cases, all UNRUN

- **8 theory rows:** anonymous, supplied inactive, missing, forged Owner, stored inactive, changed stored role, revoked Products Manager, granted Products Cashier. Each exercises **all16** writers and compares catalog/product/tag/batch/audit snapshots.
- **16 theory rows:** each writer succeeds for current stored Manager and audits stored name even with mainOFF/no financial grants.
- **16 theory rows:** each writer's audit fault rolls back without exposing private provider marker.
- **9 facts:** read-only Products reader/mainOFF; current manager capability/regrant; invalid readers; authorized no-op/no invented audit plus inactive denial; regrant after denied unlink; linked base/strength unit-label rollback; used-unit hide refusal; permission-read failure deny; Brand name commit + denied company link remains separate.

Total suite **2094 cases** (2045+49). No .NET SDK/Windows/Inno here: **Core/Tests/Desktop compilation, all C# execution and WPF runtime UNRUN**. Tests/grammar/Python cannot certify authentication, provider timing, Windows safety or production security.

## 5. Executed evidence / preservation

- `.venv/bin/python tools/check_repo.py`: **95 checks PASS**, **417 C# grammar files**, **4616 calls /9383 member references**, **0 issues**. Source grammar/member analysis, **not compilation**.
- All **19** `tools/check_*_sqlite.py` probes PASS on migrated synthetic databases. New49 lexical negatives plus existing portion27 **30** and portion26 **32** rejected/exact restoration. Python backup reference: pinned vector, **6 chunk-boundary round trips /24 negatives PASS**, not C# codec/DPAPI/new-PC execution.
- Rules matrix `--check`, guide MD/HTML parity/36 lesson anchors, stable1–36 tracker, Windows CRLF, preservation and `git diff --check` PASS. Checklist regenerated (51done/6in-progress featureboard is not Phase4 safety percentage). Final same-branch commit/push/remote verification reported only after publication.
- `tools/check_catalog_source_negative.py`:49 lexical mutations, individual active/writer gates across16 methods plus reader/UI/cleanup/SAVED/timer/regression protections; exact original bytes restored. Run alone, not concurrently with edits/builds. Not executable C#/WPF mutation tests.
- `tools/check_catalog_current_sqlite.py`: actual catalog/tag/company/salt/brand unlink and unit base/strength/name SQL on36 migrated synthetic SQLite schema; typed text preservation, Python transaction rollback, unchanged batch quantities/prices. Does **not** execute C# permissions, Database.Tx, audit service or WPF.
- Preservation versus baseline: **731 tracked paths retained;167 other Core service/data/model files byte-identical;all34 old ProductService multiline literals exact**. Product Create-through-cost-helper block exact. Database/Permission/Auth/Partner/Custom/Native/Photo/Audit/backup/migrations and financial services untouched. No synthetic posting to real shop data.
- Existing portion27 lexical contracts were scoped to its three product-record writers/shared writer helper, because that shared helper now also serves16 catalog writers. Negative runner verified again; no relaxation of those three guards. New nested test fixture has unique CatalogWriteFixture name to avoid static simple-name collisions.

## 6. Same-number leftovers / next

1. **5:** authenticated login/session binding and remaining callers. Stored ID/role isn't session ownership.
2. **6:** other parent/workflow paths, caller-gated actorless lookups, version/CAS and cross-window content conflicts. Brand two-stage save remains partial-commit capable, explicitly reported.
3. **7:** actual WPF timers/bindings/focus/read-only/regrant tests, other dialogs/caches, inline quick-add reload UX. Preflight outside Tx isn't a coherent snapshot; concurrent edits can race; no instant recall of old objects/files/paper/clipboard.
4. **23–25:** next integrated fault/retry/duplicate/concurrency work, including actual C# tests and postcommit UI faults. SAVED wording isn't an idempotency token or power-loss/parallel certification.
5. **26–29:** encrypted backup/files/restore/new-PC/cloud isolated integration.
6. **2/4/30–35:** Windows2094+build/tests/results/fixes/UI/hardware/startup/isolation evidence.
7. **36:** authorized single sendable double-click test installer **NOT BUILT**; Windows build/test/install/upgrade/uninstall/data-preservation gates remain. FullPhase4 OPEN.

Every portion docs plus same-active-branch commit/push/actual remote verification. Final hash only reported after actual publication;36 stable IDs are not36 Next turns;conditional round estimates aren't deadlines/upper bounds.
