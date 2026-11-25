# Phase4 portion6 — custom definitions and bill-time correction boundaries

> **⏳ checkpoint کی حد:** اس فائل کے نتائج/اعداد اسی درج شدہ checkpoint کے ہیں؛پرانے pending یا not-started جملوں کو موجودہ حالت نہ سمجھیں۔ [تازہ مجموعی رپورٹ](COMPLETE-STATUS-AND-DECISIONS-URDU.md) اور[فیصلوں کا index](DECISION-REGISTER-INDEX-URDU.md) پہلے دیکھیں؛نیا Windows PASS فرض نہیں۔

> **Subsequent policy update D-110, v0.85.0:** The former management-only cost rule and assertions that the global profit switch leaves cost visible are superseded. View Profit / View Purchase Cost are separate grants under one main switch, plus page access. Named routes migrated in [portion7](POS-PHASE4-FINANCIAL-VISIBILITY-REVIEW.md); global invoice/ledger/batch/stock/expense/export rollout remains OPEN. The evidence/counts below are historical for this portion, not latest policy.

**v0.84.0 Lapwing · #4D6072 · 2026-11-10. IN PROGRESS, not full audit/security/Windows PASS.** [Overall26-route/8-Settings/36-lesson register](POS-PHASE4-EVIDENCE.md); [previous snapshot review](POS-PHASE4-SNAPSHOT-REVIEW.md); [Urdu PT-26](POS-PHASE4-PART6-URDU.md).

## Findings and bounded fixes

The existing business rule was already owner-only custom-field definition administration and owner-only bill time correction. CustomFieldService Add/Update/SetActive/Move relied on cached IsOwner; TimeCorrection.CanFix did too. An inactive/demoted stored account with a stale owner snapshot could reach these methods. TimeCorrection.ApplyCorrection additionally read and planned selected bills before starting its write transaction, leaving a detached read-plan window. No new business policy was needed to enforce the existing rule.

Custom definitions now call OwnerAccess.Require **inside each write transaction**, before duplicate/target/no-op checks. The resolved stored owner supplies the audit identity, not the old display name. Expected denials use ValidationException→failed result, not unexpected-error logging. Field validation, labels, field_key assignment, type/required policy, order renumbering, soft-disable/answer preservation and SQL are unchanged.

CustomFieldService public List/Values/DescribeValues resolve current active account/role-kind/module with SnapshotReadAccess. List management/includeInactive additionally verifies current owner. Public SaveValues resolves current reader/module **inside its transaction**, passes that reader to the existing answer-validation/write helper and therefore audits the current stored name. Ordinary permitted customer answers remain allowed for a cashier; this is not owner-only answers. Unknown entity checks and existing module mapping are unchanged.

TimeCorrection.CanFix now resolves the active stored owner and fails false on validation/database failure. ListBills rechecks owner on its read connection. ApplyCorrection checks eligibility for the usual user-facing refusal and then revalidates owner **inside one Database.Tx**. Selected IDs are read, every bill planned, and sales/payment updates/audits performed on that same connection/transaction, including the already-correct no-op path. Database.Tx already uses `BeginTransaction(deferred:false)`; this implementation relies on that immediate lock and does not modify Database.cs. No detached read connection or nested transaction remains in Apply.

Offset difference, minimum one-minute shift, legacy unknown-offset treatment, selected-ID cleanup, list range/500-row behavior, shifting linked payments and old/new audit values are unchanged. sales.created_at and all money/quantity/discount/cost fields remain untouched by correction. A malformed payment/audit failure throws and rolls back earlier bills/payments/audits; missing/unreadable selected bills fail before any write. Repeated same-offset apply remains a zero-fixed/one-skipped operation, but still needs the active owner. This closes the bounded planning gap; it is not universal duplicate/concurrency/session certification.

## View → ViewModel → service → data / permission trace

Paths are relative to `dotnet/Area11.Desktop`, Core services under `dotnet/Area11.Core/Services`.

| Entry / lesson | View / VM / Core path | Data and authorization | Status / limit |
|---|---|---|---|
| Settings Fields / Own fields,32 | SettingsView + CustomFieldsView → SettingsViewModel.ExtraFields / CustomFieldsViewModel → List(manage), Add/Update/SetActive/Move | custom_fields/custom_values/audit_logs; live owner for administration | focused fixes + written regressions; WPF pending |
| Product/customer/supplier/document extra-field editors,05/08/09/15/17/22/32 | CustomFieldsEditorViewModel → public List/Values/SaveValues | CustomFieldEntity module map, current reader; public save in transaction | focused public boundary; not complete parent-service authorization audit |
| Existing answer rendering,12/35 | SaleHistoryViewModel → DescribeValues; other public callers | custom_values/custom_fields; same entity module map | current account/module, existing historical/disabled-answer display retained |
| Bills → Fix wrong time,12 | SaleHistoryViewModel → TimeCorrectionDoor → TimeCorrectionWindow / TimeCorrectionViewModel → ListBills/ApplyCorrection | sales/payments/audit_logs; current owner | one immediate read-plan-write transaction; Windows confirmation/DPI pending |
| Parent atomic create/edit pipelines | Product/Partner/Sale/Purchase/Expense services → SaveNewRecordValuesInTransaction / SaveValuesInTransaction | caller owns parent authorization/transaction; required-answer failures must roll back parent | deliberately retained delegation, review OPEN |

No caller signatures changed in this portion. Existing public field readers already received User and Desktop callers retain their `_user`. UI role/menu checks remain convenience, not the authoritative grant. Six supported entity values, built-in/native-field policy and document-photo restriction are unchanged. Custom fields remain labels/answers, not receiving or financial posting rules.

## Explicit remaining boundaries

- **Not arbitrary-ID authentication:** a real privileged ID supplied by arbitrary in-process code is not a session credential. OwnerAccess/SnapshotReadAccess enforce local stored account policy only. PermissionService existing defaults/error fallback are unchanged; CanFix itself refuses on errors.
- **Delegated custom answer helpers are not independently module-gated here.** Parent services already own transactions/authorization, including supported cross-workflow creation. Silently requiring a new module there could break an existing legitimate parent workflow. Review those parent boundaries separately; do not claim the whole custom-field layer has been authenticated.
- Values/DescribeValues retain historical answer behavior, including disabled field definitions. No new per-record ownership policy was invented; full record/session/parent lifecycle review remains OPEN.
- No immediate invalidation of already opened UI/preview/files, no in-flight revocation beyond transaction serialization, no general database-instance/thread safety proof. Multi-process/real Microsoft.Data.Sqlite/WPF behavior remains unexecuted here.
- Reports DeadStock.ValuePaisa still uses the existing Reports-authorized cost valuation. D-109 applies only to History/Reorder costs; no silent expansion. Its valuation policy and remaining exports are OPEN.

## Actually executed evidence

- `tools/check_repo.py`: **72 static checks**,377 C# files parsed with real grammar,3867 calls/7136 member references resolved,0 reported problems. These are not compilation/xUnit/WPF.
- **12 negative source mutations rejected/restored**: each of four definition owner gates, administrative listing owner check, public-save resolved reader, time apply owner, time transaction wrapper, time audit identity, public field-read current gate, Database immediate lock and definition audit identity. Originals restored and new guard clean.
- **12 CustomField +4 TimeCorrection verbatim SQL blocks unchanged** versus portion-start source. All36 migration files, SaleService/ReturnService/PurchaseService and Database.cs SHA256 unchanged. All622 baseline files retained.
- New reproducible `tools/check_time_correction_sqlite.py`:36 actual migrations on a **temporary synthetic database only**; actual source-extracted bill/payment/audit SQL; BEGIN IMMEDIATE blocks a second writer during read-plan window; injected audit failure rolls sale/payment/audit back; successful dates shift while created_at/money remain and old/new audit/FK checks match. Python supplies the shift independently. **Not C# service/thread/session proof.** No shop path accepted or production data opened.
- Existing `tools/check_extra_discount_sqlite.py`: isolated36 migrations/history/actual BookCheck SQL/corruption detection/B560540/FK PASS, not C# financial execution.
- Main Urdu MD/HTML exact rendered-body parity,36 unique lesson anchors, offline assets; rules matrix/checklist regenerated; `git diff --check` clean.

## Written, NOT executed C# evidence

**20 new facts** in OwnerDefinitionTimeBoundaryTests; **1421 total written cases**. No .NET SDK/Windows/xUnit/Desktop/WPF/hardware run here. Existing TimeCorrectionTests/CustomFieldTests financial/business expectations were not changed.

New cases cover null/forged/missing/inactive/demoted readers, manager Settings not granting ownership, stored-name audit, no-op tick, stale public reads, allowed cashier answers, same-service module revoke, public answer audit identity, definition audit rollback, soft-disable retaining answers, preview followed by deactivation, corrected dates with original created_at/money, same-offset idempotence, malformed-payment rollback across bills, audit-fault rollback and apply rereading current bill instead of preview data. They do not simulate all interleavings; static guard/independent SQLite lock proof and future C# runtime evidence are separate.

## 15-dimension coverage / next work

1–4 money/rounding/discount/cost/history: no price/posting formula changes; time historical-date update path focused, money/created_at assertions written. 5–6 atomicity/concurrency/duplicates: time read-plan gap removed in source; isolated SQLite lock/fault proof, .NET concurrency still OPEN. 7–8 permission/IDs: definitions and selected public field/time paths focused. 9 login/session: no replacement authentication/session system; wider session OPEN. 10 input/files/SQL/HTML/archive: existing field validation/SQL retained, no full export/file proof. 11 backup/cloud: unchanged, isolation/rollback audit OPEN. 12 other finance: date movements of linked payments retained; wider valuation/ledgers/loyalty/expenses OPEN. 13 drafts/park: no change, full lifecycle OPEN. 14 UI/keyboard: existing callers preserved; actual confirmation/native keys/DPI/Windows OPEN. 15 tutorial/obsolete: lessons12/32 and overview reconciled, full36-lesson action audit OPEN; no removal.

Next SAME phase: parent/delegated write boundaries (including custom answers), remaining report valuation decision/export safety, finance/stock/concurrency/duplicate faults, inputs/files/backup/cloud and complete guide/Windows evidence. No installer, fake live-shop posts, commit or push; prior dirty work retained on same branch, no reset/stash/checkout. Sale-level Extra, all-role actual cost floor and return B560/540 unchanged.
