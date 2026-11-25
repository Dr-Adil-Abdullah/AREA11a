# Phase4 portion16 — Stock Minus read/review boundary

> **⏳ checkpoint کی حد:** اس فائل کے نتائج/اعداد اسی درج شدہ checkpoint کے ہیں؛پرانے pending یا not-started جملوں کو موجودہ حالت نہ سمجھیں۔ [تازہ مجموعی رپورٹ](COMPLETE-STATUS-AND-DECISIONS-URDU.md) اور[فیصلوں کا index](DECISION-REGISTER-INDEX-URDU.md) پہلے دیکھیں؛نیا Windows PASS فرض نہیں۔

**v0.94.0 Cormorant · 2026-11-10 · D-110 / D-113 continuation · SOURCE IMPLEMENTED, Windows OPEN.**

This is a bounded continuation of the approved review, not full security/production approval. Supplier paid/payable/Pay/SR policy remains unanswered. No live-shop data, fabricated screenshot, installer or actual Windows test was used.

## What changes / what does not

A historical minus of3 units at12345paisa cost has raw valuation-37035. A Stock reader without Cost/main still sees the medicine, quantity, unit, episode date/cause, review date/person/state — but **raw ValuePaisa0/CostKnownfalse/ValueText hidden**, not an editable zero cost. Aggregate raw TotalValuePaisa and TotalText are masked too. Cost-only may see the exact valuation. Manager rank and Profit-only do not grant purchase cost. Owner's Cost checkbox can also be revoked; main OFF hides both financial dimensions even for Owner. Genuine authorized0 is known, not hidden.

ReviewNote is copied from `audit_logs.details`. Allowing it here without the existing Audit rule would bypass portion14: raw notes can contain cost/profit numbers. Therefore raw ReviewNote is null/ReviewNoteKnownfalse unless **both financial grants + main**; ReviewAt/ReviewBy/IsReviewed remain metadata. Stored Audit is never rewritten. This does not sanitize arbitrary product names, reference text, previously copied files or the user's own unsaved NoteText.

**No new permission/rank policy for writing a review:** the existing Stock grant remains sufficient to append a note; Cost/Profit are NOT prerequisites for a legitimate stock-authorized review. The word "owner's monthly review" in the historical feature description does not silently revoke existing explicit Stock delegation. No note changes stock, batch cost, movements, episode or money. D-93 physical stock-take remains the correction path; no new below-zero sale/stock policy or silent reconciliation.

## Trace / gates

| Path | Current behavior |
|---|---|
| StockMinus.Report(user) | Open connection → ReadAccess → current active stored same-role account + Stock before registry queries. Independent CostPolicy(main+Cost); raw audit notes additionally ProfitPolicy. Return new projected DTO rows/report, no mutated source objects. |
| ReadAccess / CheckReadAccess(user) | Reuse current-reader and financial flags. Small flags-only result for the active view, no record data. Not a session capability or authorization token; does not authorize later writes. |
| Registry row / total / summary | CostKnown on row/report; denied raw amount0 with hidden labels. Total also refuses unknown mixed rows. Default models do not label unknown money as known0. Quantity/count/oldest/review metadata retained. No new dedicated StockMinus CSV/print/file builder exists. |
| Review(user,productId,note) | Original note-length and minus-product checks retained. Current account/Stock checked **inside existing Database.Tx before bound product lookup and audit**. Audit uses current stored reader name, not stale supplied display name. Append-only note; no new financial grant prerequisite. Existing active-product/threshold/record-availability semantics retained. |
| Failure | Failed report returns Fail/null, not a successful empty registry. Report/review errors no longer echo raw DB exception text to UI. Existing central log retains diagnostic exception under its existing access model. |
| ViewModel.Load | Drop Selected before refresh; failure clears rows, detached selected details and totals/count/review summaries to unknown; disables review. Success uses projected ValueText/TotalText and fresh row selection. No retained raw money string. |
| Active view | Loaded/Unloaded nominal3-second timer calls RefreshAccess. Only small access/flag read each tick; full report reloaded on changed financial flags/return of access or retry of a failed load. Does not replay all movements every3seconds when access is stable. Unloaded stops timer. Existing user-authored unsaved note preserved, never auto-saved. |

### Deliberate limitations

Supplied User/ID is not authenticated-session proof. Current role/activity/module revalidation rejects stale/forged snapshots, but hostile in-process callers still require the wider session boundary. Grant/settings/data reads are not one atomic authenticated snapshot; Settings has its existing instance-cache lifecycle. External DB changes/dispatcher blockage can delay masking. Timer is active-view only, not immediate revocation of all objects/files/clipboard/screens. Existing nonfinancial PermissionService defaults/read-error policy is unchanged; financial read errors fail closed through the existing Cost/Profit policy. Free-form operational text is not automatically scrubbed for inferred prices. No claim of absolute financial secrecy.

## Preservation evidence

- All **4 StockMinus verbatim SQL blocks byte-for-byte unchanged**. Product/batch active filter, negative stock sum/valuation, movement order, review source/latest ordering and bound product lookup unchanged.
- `FindEpisodes` and private row/helper tail exact. Crossing/recovery/new episode, age sort, unknown movement and old review behavior retained.
- **680 baseline files retained;114 other Core Services/Data hashes unchanged.** Audit writer,36 migrations, Database, Sale/Return/Extra/B560540, Stock/StockTake, Purchase, Supplier, Import/Workbook and encrypted backup/credential/rollback sources unchanged. Only StockMinusService changes in the existing service/data set; model and version metadata are separate intended edits.
- Existing StockMinusTests retains original assertions; reviewed actor-name expectation now uses the real seeded Owner name because the writer resolves the current DB account. Three old constructor sites in tests + production inject Settings explicitly; no actorless/ungated compatibility overload.

## Evidence actually executed

- **83 static checks PASS**:401 C# grammar files,4253 checked calls/8282 member references,0 reported problems. This is not a C# compiler or test runner.
- **12 negative source mutations rejected, exact bytes restored**, valid source accepted. Initial mutation exposed an insufficient CostKnown source guard (another initializer satisfied it); guard tightened to require BOTH row/report flags, then all12 rejected. This is proof of guard sensitivity, not runtime exploit/authorization resistance.
- New `tools/check_stock_minus_sqlite.py`:36 migrations and4 actual SQL blocks on synthetic memory SQLite. Active negative stock excludes inactive batch/product, preserves-36035 and actual0, movement order/latest review/bound-ID injection refusal/read-only state. Actual unchanged Audit insert appends note; injected audit failure rolls back without stock/movement change; foreign keys clean. **Not C#/Dapper/permission/projection/VM/transaction-wrapper execution.**
- All **11 synthetic SQLite probes PASS** (10previous+new); independent Python backup-format model still passes6 boundary roundtrips/24negative cases. Backup C# remains UNRUN.
- **24 new facts /1716 total written C# cases — UNRUN here.** Covers Stock-only/manager/profit-only/cost-only/both/main/Owner masks; known0/empty/default/mixed row; null/unknown/forged/inactive/changed-role reader; module/cost/profit revocation; financial-table failure; safe failed report; immutable stock/Audit; legitimate hidden-cost review; transaction reader/current stored name and audit failure.
- Guide MD/HTML body and36 lesson anchors/local links, generated checklist/matrix, icon/version alignment, diff hygiene and same-branch publication checked separately. No .NET SDK/Windows/Inno toolchain; no installer or runtime certification.

## Remaining / 15-dimension boundary

Page/core/read/projection/write-check/summary/stale-selection gates covered in source. Financial arithmetic, actual-cost floor, sale-only Extra/return B and snapshot accounting untouched. No new import/export/print route invented; existing generic reports/bulk gates remain independently reviewed, not certified by this DTO change. Session identity/record ownership/global stale objects, concurrent revocation, duplicate notes, audit/storage/power failures, very large registry timing, files/cloud and full Windows/hardware/guide walkthrough remain broader gates. Notes are append-only but concurrent review submissions are not made idempotent by this portion. No obsolete feature/data removed.

**Next:** genuinely isolated Windows [PT-36](POS-PHASE4-PART16-URDU.md) and outstanding [PT-35 backup/DPAPI/new-PC](POS-PHASE4-PART15-URDU.md); then remaining output/session/record/fault/cloud paths. Do not silently choose the unanswered Supplier financial-policy exception. [Current remaining plan](REMAINING-WORK-AND-TEST-INSTALLER-URDU.md).
