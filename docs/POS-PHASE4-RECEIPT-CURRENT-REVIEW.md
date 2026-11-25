# Phase4 portion33 — current receipt and sales-history boundaries

> **⏳ checkpoint کی حد:** اس فائل کے نتائج/اعداد اسی درج شدہ checkpoint کے ہیں؛پرانے pending یا not-started جملوں کو موجودہ حالت نہ سمجھیں۔ [تازہ مجموعی رپورٹ](COMPLETE-STATUS-AND-DECISIONS-URDU.md) اور[فیصلوں کا index](DECISION-REGISTER-INDEX-URDU.md) پہلے دیکھیں؛نیا Windows PASS فرض نہیں۔

**v0.111.0 Inlet · 2026-11-10 · stable5/6/7/15/17/21/23/24. Source checkpoint, not Windows certification.**

[PT-53 Urdu steps](POS-PHASE4-PART33-URDU.md) · [stable1–36](NUMBERED-WORK-STATUS-URDU.md) · [guide](USER-GUIDE-URDU.md)

## Bounded change and authority

| Entry | Required current module | Boundary |
|---|---|---|
| ReceiptService.Build default Counter | POS | Required supplied actor; stored active account / same parsed role / current permission before payload SELECT |
| ReturnReceiptService.Build default Counter | Returns | Same reader check before return rows |
| Either builder with explicit History purpose | Sales | No additional POS/Returns requirement; invalid enum values deny |
| History Search/Open/receipt/return/bulk | Sales | CanLook uses fail-closed current access; direct reads gate on payload connection; wrappers propagate History purpose |
| History reprint audit | Sales | Resolve stored actor inside Database.Tx before target read and audit insert; stored name, missing return refusal, write failure surfaces |
| Bills UI | Sales | Next action and loaded nominal3second access-only timer; denial/failed read clears cached payload, no automatic regrant repopulation |

The purpose is an explicit workflow selector, NOT a caller-authenticating capability. Passing History still requires Sales. No permissive union of POS/Returns/Sales grants. Ordinary valid-current Owner bypass and existing nonOwner defaults retained; permission-read errors deny through unchanged PermissionService. No financial visibility or price/refund policy changed.

## Implementation and preservation

- Receipt constructors require PermissionService. Both builders require User? actor; all production/test callers propagated, no actorless overload or optional null actor fallback. Unknown purpose throws validation failure before payload.
- Receipt/return Compose/layout tails remain byte-identical to P32. Paper widths, corrected-time rendering, payments/Extra/return settlement amounts unchanged.
- History Search/Open fail safely without raw provider details. Bulk builds every document or returns no job, verifies summary rows exist, and rechecks authority before returning the completed job. It is not one atomic snapshot across all receipt/summary connections; concurrent edits and check/use races remain.
- RecordReprint/RecordReturnReprint use `_audit.Record(connection, reader, ...)`, not the standalone helper that catches insert exceptions. Existing action/detail shapes and corrected timestamps preserved. The return audit now verifies a real code with the same case-insensitive comparison as receipt lookup. No rewriting historical audit data or unrelated Blackbox/BookCheck changes.
- A physical print and subsequent DB audit are not atomic. Existing UI reports successful print plus an unrecorded-audit warning; no automatic reprint/retry. Actual spooler ambiguity remains.
- History clears rows/selected record/ticked set/detail/payment/return/custom text/preview/totals and disables print flags on denial/failed reads. Access-only allowed ticks preserve selection. Regrant requires a fresh Refresh. Reentrant cleanup checks fail closed; Unloaded stops timer. Actual WPF lifecycle/reentrancy is UNRUN.
- Monetary Sale/Return/Purchase/Supplier posting services, schema, Database/PermissionService/SnapshotReadAccess/AuditService implementations, backups, Blackbox/BookCheck, pricing/Extra/ReturnB/SupplierB and Cost/Profit/main policies unchanged.

## Regression inventory — C# compilation/execution UNRUN

`ReceiptCurrentAccessTests.cs`: **95 new cases** = 88 InlineData + 7 facts. Suite **2400 expected**, all unexecuted here.

- 11 routes × 4 current-account faults (inactive/role/revoke/grant-read) =44, deny without financial/audit mutation, restore and reuse same service.
- 11 routes × 4 invalid actors (null/zero/inactivecaller/forgedOwner) =44, no payload/defaultValue and no mutation.
- 7 facts: independent module grants; both audit stored names; audit trigger failure surfaced/rollback; missing return audit denied; invalid receipt purpose; Owner current active/same-role validation; missing bulk bill yields no partial job.
- Existing receipt/history/payment/Extra/return test assertions retained, only new dependency/actor arguments added. New fixture name is unique to avoid the lexical constructor check confusing unrelated nested fixture classes.

## Executed local evidence

- `tools/check_repo.py`: **100 checks PASS**; **422 C# grammar files**, **4706 static calls / 9585 references**, zero issues. Source resolution is not compilation.
- All **24** `tools/check_*_sqlite.py` scripts PASS. New receipt probe: **36 migrations / 14 actual receipt-history SELECTs / 24 current-actor-module denials**, unchanged synthetic snapshots/regrant, independent modules, actual Audit INSERT/stored name and trigger rollback. Python owns policy and transaction behavior.
- All **7** source-negative scripts PASS with exact-byte restoration: **29 receipt-current**,49 catalog,24 current-counter,29 counter-posting,23 ledger-posting,30 record-write,22 stock-posting. These are lexical guard-sensitivity probes, not executed C# mutations.
- Independent backup Python model: pinned vector, **6 chunk-boundary round trips / 24 negative cases PASS**. Rules matrix `--check` PASS. No C# codec/DPAPI/new-PC certification.
- Preservation against P32 `91d2577f1e963ba3c74d92d61c2eb71a58f79bd0`: **all756 baseline paths retained;165 other Core service/data/model files byte-identical;both receipt Compose tails exact;8 edited existing test/helper assertion counts retained**. Guide MD→HTML exact,36 lesson anchors,stable1–36 tracker,Windows scripts CRLF and `git diff --check` PASS.
- C# build/all2400 tests, Dapper/provider/Database.Tx execution, WPF timer/bindings/modal reentry, genuine printer/PDF/WhatsApp and PT-53 Windows steps: **UNRUN**. No .NET SDK/Windows/Inno available here. No installer built.

The single-bill print action captures its selected row before calling the printer, so timer cleanup during a modal printer cannot make the post-print audit dereference a cleared bound selection. Reentrant cleanup suppresses selection repopulation. These are source/lexical-reviewed safeguards, not actual WPF execution proof. Source/lexical/SQLite/Python checks are not .NET compilation, C# authorization/provider/transaction execution, WPF, session or printer proof.

## Explicit limits / next work

Current stored ID/active/same-role validation is not authenticated login-session binding and does not defeat arbitrary same-role ID impersonation. No session token, atomic CAS or cross-process authority guarantee introduced. A read can race account/grant changes; builders and summary use multiple reads. Already-open print preview, prepared PDF/file, WhatsApp message or printed paper is not recalled. Revocation between build and actual print/send is still possible. Full POS cached cart/picker masking policy is not silently changed.

Remaining current-account/caller work: quarantine, draft Save/Open, held carts, quick-add and other prepared outputs/dialogs; session/CAS integration. Other monetary/file/draft/actual-COMMIT fault matrices, provider/busy/productionjournal/parallel/cross-process and durable request identity remain OPEN. Backup/restore/newPC/cloud and Windows build/2400tests/WPF/hardware/startup/isolation still require actual logs. **FullPhase4 OPEN; authorized single-doubleclick test installer NOT BUILT.** Every portion requires docs, same-active-branch commit/push and separate actual-remote verification.36categories ≠36Nexts;3–6integrated-round estimate conditional, not a deadline/upper bound. No synthetic posts to a live shop; verified encrypted backup and truly separate DB/photos/config before Windows tests.
