# Phase4 portion19 — Recovery and stored monthly report outputs

> **⏳ checkpoint کی حد:** اس فائل کے نتائج/اعداد اسی درج شدہ checkpoint کے ہیں؛پرانے pending یا not-started جملوں کو موجودہ حالت نہ سمجھیں۔ [تازہ مجموعی رپورٹ](COMPLETE-STATUS-AND-DECISIONS-URDU.md) اور[فیصلوں کا index](DECISION-REGISTER-INDEX-URDU.md) پہلے دیکھیں؛نیا Windows PASS فرض نہیں۔

**v0.97.0 Lapwing · release label 2026-11-10 · SOURCE REVIEW, Windows OPEN.**
Previous published checkpoint: `81154f0408026688d1eb5472f619a3cf9e387064` (v0.96.0). [PT-39](POS-PHASE4-PART19-URDU.md).

## Scope and policy

Bounded continuation of the authorized Phase4 review, not a new tax/accounting feature. Recovery needs current **Customers**; the existing monthly `Tax report` needs current **Reports**. Neither contains purchase-cost/profit projections, so neither gains a Cost/Profit/main prerequisite. Reports is not a bulk-file permission: the existing Copy action remains report-only tab-separated clipboard text, not an editable inventory import/export. It still permits financial sales/tax totals by Reports grant. No promise of absolute inference secrecy.

Independent Profit/Cost controls, financial main switch, below-cost rejection, Extra forgiveness, overpayment rejection, Supplier exception B, backup password A and D-115 remain settled. No Black box, BookCheck, Audit writer, shell/navigation, schema, posting or tax-setting change. Do not turn tax on for this review.

## Method/output trace

| Path | New boundary / failure behavior | Preserved meaning |
|---|---|---|
| `Recovery.CheckAccess(user)` | Current active stored same-role Customers reader; no report data; safe generic infrastructure failure | Advisory UI check, not a capability/token |
| `Recovery.Report(user,today)` | `LedgerReadAccess.Require` on the report's opened connection before `Build` | Owing/active customers, bill-date age, oldest-first collections, source-bill returns, carried-in opening outside age columns, mismatch flag |
| `Recovery.Slip(paymentId,user)` | Current Customers reader before payment lookup; safe validation message or generic logged DB error | Bound collection-only payment ID; excludes sale/purchase/provisional payments; settlement stops at this collection |
| `Recovery.SlipDocument` | Delegates to newly gated Slip; no alternate ungated reader | **Collector's** stored current name joined through `payments.user_id`, NOT printing reader; historical before/after balances; PAY number, width/layout/footer unchanged |
| `Tax.CheckAccess(user)` | Current active stored same-role Reports reader; no report data | Advisory check only |
| `Tax.Report(user,year,month)` | `SnapshotReadAccess.Require` on opened connection before money SELECTs; fixed safe DB error instead of raw exception text | Same month/status filters, subtotal minus discount, stored tax/gross, refunds displayed separately, stored round-off; settings describe current state and do not recalculate bills |
| `TaxViewModel.Load/Copy` | Both fresh service reads; failures clear rows, amounts, setting label, refund/round-off state and old status | Copy still uses existing `ToText`; clipboard failure now generic; earlier clipboard cannot be recalled |
| `RecoveryViewModel` | Fresh access before filter/receive/reprint; failed report clears rows/cache/selection/amount/phone/age/totals/opening/books flag/status/last-payment ID; permission-loss timer does the same | Ordinary payment still delegates to already transactional `Ledger.Collect`; unsaved Note/search/method retained, selected customer's amount cleared on deselection |
| Recovery print adapter | Only successful non-null service document goes to `Printer.Print`; no invented empty document on service failure | Saved collection is never undone because printing/refresh fails |
| Post-save refresh | A failed list rebuild stops subsequent stale row refill/autoprint/old-balance status; explicit **SAVED—do not receive again; refresh** message | Collection already committed; no duplicate write/retry/clamp added |
| Both active views | Nominal 3-second DispatcherTimer, initial Loaded check, Unloaded stop; regained access reloads | No continuous full report reload while access stays allowed; permission checks are not a live data subscription |

Expected `ValidationException` text from the existing current-reader helpers is returned without logging it as infrastructure failure. This retains existing tests/UI expectations containing “permission”. Unexpected DB exceptions remain logged locally and are not concatenated into returned messages.

## Preservation evidence against v0.96.0

- All **693 baseline tracked paths retained**. The refreshed checkout was first compared with fetched remote: only 20 CRLF/LF normalizations, no content differences. Local index/HEAD realigned with `git reset --mixed` to the already-published checkpoint; working files untouched. No force push or branch switch.
- **113 other Core service/data files byte-identical**, including CustomerLedgerService, CustomerSettlementLedger, pricing/returns/stock, permission helpers/defaults, backup, Audit and BookCheck. All migrations unchanged.
- Recovery's **4** and Tax's **2** original verbatim SQL blocks exact. Recovery `Build` ageing/replay/sort and `SlipDocument`-to-EOF exact. Tax `ToText`-to-EOF exact. Recovery's single-line customer lookup is also unchanged.
- Only these two Core services, their two VMs/views, new tests/probe/structural guard, release identity/icon/scripts and documents are in scope.

## Evidence, not runtime certification

- **86 repository static checks PASS**; C# grammar **404 files**, structural resolution **4378 calls / 8531 references / 0 problems**. Not a compiler.
- **24 new `[Fact]` cases written; 1789 total C# cases UNRUN**. Includes null/inactive/missing/forged-role/stored-role/deactivated account, own-module versus financial grants, main OFF, revocation/restoration, typed slip, full settlement, later payment stability, collector versus printer, carried opening/age boundary, stored monthly tax/round-off/status/month, read-only and safe DB errors. Existing accounting tests retained, not re-executed.
- **15 source-negative mutations rejected/restored** by the structural guard: account gates, typed collection, collector attribution, stored tax, safe errors, last-slip/books state, actual document, post-save failure branch, setting/report cleanup and both timer stops. These are NOT executable C# mutation tests; they check declared source structure, not semantic correctness.
- `tools/check_recovery_report_sqlite.py`: all **36 migrations** on synthetic in-memory DB; source-extracted real Recovery/monthly SELECTs exercised for active owing/status/month filters, bound typed slip/collector, sale payment exclusion, collection vs source-bill return events, net/tax/gross/refund/empty month, read-only and foreign keys. Python does **not** execute C# access, settlement Replay, Database.Tx, receipt rendering or UI.
- All **14** `tools/check_*_sqlite.py` probes and independent Python backup reference model (6 round trips / 24 negatives) PASS. Backup reference is not platform crypto/DPAPI proof.
- Rules matrix consistency and `git diff --check` PASS. [Physical log](../PHYSICAL-TESTING.md) remains UNRUN.

## Fifteen-dimension bounded review

| Dimension | Disposition |
|---|---|
| Purpose / ownership | Customers recovery, Reports stored monthly witness; no new policy |
| Identity | Current stored active same-role account before bounded data query |
| Authorization | Own-module grant; financial grants cannot substitute |
| Inputs | Existing ID/year/month/amount validation retained |
| Query binding / types | Same parameterized SELECTs; collection-only slip |
| Accounting / ageing | Same ledger replay, oldest-first and source-bill return rules |
| Financial visibility | No cost/profit output added; explicit Reports/account totals retained |
| Attribution | Recovery joins collector, unlike Customer PaymentReceipt's printing-reader label |
| Output channels | Fresh document/copy source; failed actual document never printed |
| Stale UI / lifecycle | Denial and failed reads scrub cached report fields; nominal timer only |
| Commit / fault result | Already-saved collection clearly retained on failed refresh/print |
| Data / migrations | No schema or posting changes; preservation checks above |
| Backup / secrets | No secret/package/restore work; Password A unchanged |
| Evidence quality | Static/SQLite/source mutation separate from unrun C#/Windows |
| Operations / deployment | PT-39 isolated Windows required; no installer or shop readiness claim |

## Explicit limits and next work

A supplied valid account ID is not authenticated session proof. PermissionService's existing default/error policy is unchanged, and helper/permission/data queries are not one serialized authorization snapshot. Settings caching, dispatcher stalls, revocation between successful check and printer/clipboard handoff and old detached objects/paper/files remain possible. There is no timed lock or instant recall guarantee. Auto-print failure after a committed collection must not cause duplicate collection; verify actual status/printer behavior on Windows. No refresh timer promises up-to-date balances while grants stay unchanged.

Parent/container/admin navigation, full session identity, record/fault/concurrency review, file/cloud/restore real-world testing, all Windows/WPF/DPAPI/new-PC/installer gates and global Phase4 remain open. Black box/BookCheck are not a newly authorized next scope. Roughly **3–6 further integrated review/result rounds** remains conditional, not a finish date or upper bound. Every portion requires docs plus same-branch commit/push and actual remote verification.
