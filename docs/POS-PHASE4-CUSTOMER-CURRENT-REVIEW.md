# Phase4 portion18 — current Customer collection / doctor-profit / rep boundaries

**v0.96.0 Grebe · 2026-11-10 · PT-38 · SOURCE IMPLEMENTED, Windows/fullPhase4 OPEN.**

## Scope and existing rules

No new business-policy choice. Apply the approved current-account/module and independent financial-grant rules to remaining customer-related paths. Customer balance/collections/statements remain available under **Customers**, without a Cost/Profit prerequisite; explicit profit requires **Customers + Profit + main ON**, not rank or Cost. Company rep contacts/edits retain **Products** delegation. D-114 Supplier accounting exception and D-115 Black box menu remain unchanged.

**Black box/Audit services, BookCheck, Shell navigation, Tax, backup, schema, pricing, stock, returns, loyalty calculations and permission defaults were not changed.** This portion is not permission/session hardening of every unrelated service or a new feature removal.

## Method-to-output trace

| Boundary | Source change |
|---|---|
| Customer `Collect` | Current stored active/same-role Customers reader resolved **inside the existing immediate Database.Tx**, before customer lookup/payment/balance/Audit. Payment and Audit use current actor. Positive amount/method syntax can reject before lookup without reading customer data. Overpayment/negative balance prevention, methods and subtraction unchanged. |
| Customer `CheckAccess` | New lightweight current Customers access snapshot; successful bool means independent Profit visibility, not whether the account is allowed at all. Failure is distinct from successful false. Not an authorization token. |
| Customer List/Page/Statement/Bulk | Existing current ledger reads retained. Generic list/page/bulk failures no longer return exception type/message. Original profit masking/reconciliation/month handling unchanged. |
| Collection receipt | Existing current-account gate retained, redundant cached-role gate removed, typed bound payment ID unchanged: no sale/purchase/provisional-return payment passed off as khata collection. Reader name resolves from stored account. **The existing “Received by” label still names the current printing reader, not a newly implemented historical original-receiver lookup.** No receipt-layout redesign. Safe generic DB error. |
| Reps List/Save | Products current reader before query; Save resolves actor inside existing Tx before bound active-company read/update/Audit. Validation and delegated Products policy retained, no new Owner-only rule. Safe error, current stored audit name. |
| Doctors list | Customers current reader before DoctorSql; independent Profit/main mask uses that resolved actor. Sales/bill metadata retained when profit hidden, raw denied profit0 with ProfitKnownfalse/dash. |
| Lifetime customer profit | Current Customers reader **and** Profit/main before CustomerProfitSql. Profit grant cannot bypass missing Customers; delegated non-Owner with both allowed. Cost not required. Old Owner-only explanatory text corrected to D-110 policy. |
| Reps `CheckAccess` | Small current account snapshot of Products/Customers/Customers+Profit flags; no company, doctor or money table query. At least one own module required. |
| Customer display/profile | Account labels populate from successful fresh Page rather than cached Selected summary. Failed reads clear account/list/movements/selection/bulk IDs/status/receipt state. Profile sharing rebuilds Page/current summary. Changing customer clears old LastPaymentId/Receipt eligibility, preventing the previous person's collection button from following selection. |
| Active Customer view | Nominal3s Loaded/Unloaded timer and guarded money/share/edit interactions. Module/account denial clears display/receipt and closes editing panels; copied edit-limit display cleared, no auto-save. Profit-only change clears cached raw profit/known flag and displayed profit; restoration re-reads displayed profit without resetting authored edit fields. |
| Active Doctor/rep view | Small flags snapshot on nominal3s timer; reload only affected sections on module/profit changes. Denied state clears rows/selected company/copied rep form/doctor and customer monetary rows. Profit-only change does not reset the rep form. Save also refreshes flags and service rechecks current account. |

## Preservation evidence

- **689 baseline tracked files retained. 112 other Core service/data hashes exact**, excluding CustomerLedgerService, RepService and permission-help text.
- All **8 CustomerLedger** and **5 Rep** verbatim SQL blocks **exact/in original order**. Customer `BalanceAt`/month/loyalty/movements/types tail exact; Rep DoctorSql/CustomerProfitSql/helpers unchanged.
- Collection method/cap/balance SQL and rep validation/update SQL unchanged. Core authorization/actor and returned error text changed, not financial arithmetic.
- Audit writer/history, BookCheck and Shell exact against start of portion. No real shop posts, data migration, installer or fabricated screenshots.

## Evidence executed here

| Evidence | Result / limit |
|---|---|
| Static source/member/grammar/XAML checks | **85 PASS**;403 grammar files,4328 calls,8434 member references,0 problems. NOT compiler/runtime. |
| New focused source guard negative test | **13 mutations rejected**, original bytes restored: collection reader/ID/Audit, receipt reader, rep read/write reader/Audit, doctor current reader, missing independent profit prerequisite, cached profit/profile, doctor flag refresh, timer unload. |
| New `tools/check_customer_current_sqlite.py` | Actual source SQL on synthetic memory SQLite: typed/bound receipt, collection balance, doctor/customer aggregate without join multiplication, refund/restock formula, collection+rep audit-fault rollback, read-only/FK preservation. **Python** orchestrates transactions; not C# authorization or Database.Tx execution. |
| All `tools/check_*_sqlite.py` | **13 probes PASS**; memory/temporary synthetic fixtures, not live shop data. |
| Independent Python backup format model |6 round trips +24 negative cases PASS; unchanged C#/Windows/DPAPI still unexecuted. |
| New regression source | **24 facts**: invalid/missing/inactive/role-mismatch actors; module vs financial grants; collection methods/caps/receipts/main OFF; fresh actor; revocation; synthetic Audit failure; doctor profit inference/delegation/masking; rep module split and rollback. Existing customer/return/rep regressions retained. |
| Windows / C# / WPF | **1765 written C# cases UNRUN**. No .NET SDK/Windows/Inno in this environment; no installer built. Static signature/source checks are not compilation. |

## Remaining boundaries / fifteen-dimension review

1. **Navigation/layout:** no new routes or inner headers; Shell/D-115 exact. CanCollect/IsNotAllowed now notified from current access result.
2. **Identity:** current local active/same-role ID resolution, NOT authenticated-session proof against a caller supplying another valid real account ID.
3. **Authorization:** own module + separate Profit/main; existing nonfinancial PermissionService defaults/error fallback retained. New flags are observations, not tokens.
4. **Record IDs:** collection active customer, rep active company, receipt typed payment all bound in existing SQL. No newly invented per-customer ownership restriction.
5. **Input/calculation:** caps/methods/phone/day/name validations retained. No new cost gate for receiving money.
6. **Stock/returns:** unchanged. Customer clamp/return/lifetime formula stays existing source; doctor refund/restock SQL probed independently.
7. **Atomicity:** existing immediate transactions and Audit failure behavior preserved; synthetic SQL and written C# tests are not runtime proof.
8. **Concurrency/duplicates:** no idempotency layer/reservation or simultaneous Windows posts tested. Do not repeat a collection blindly after uncertain response.
9. **Audit:** current stored actor, raw stored audit details unchanged. D-115 menu and D-113 detail rules untouched.
10. **Stale UI:** nominal3s/next guarded interaction only; dispatcher delay/settings cache/check-to-query races prevent instant/coherent revocation guarantee. Copied files/old DTOs not recalled. Module denial clears record-derived UI, profit-only change does not erase authored edits.
11. **Print/share:** fresh receipt/statement/profile preparation, not revocation of existing preview/clipboard/WhatsApp text. Free-form notes/record names not scrubbed.
12. **Bulk/files:** no bulk permission change, no editable-file generation change. Existing dedicated Import gate retained elsewhere.
13. **Errors:** generic database messages to caller; full exceptions retain existing local log behavior. Logs/security as a whole not certified.
14. **Performance/UI:** small access flags, data reload on affected flags/explicit Refresh; WPF bindings/selection/receipt width and gestures remain actual Windows gates.
15. **Recovery/release:** verified backup plus genuinely separate data/photos/VM mandatory; full Phase4/other outputs (including Tax), parent customer administration services, sessions/records/faults/files/cloud/Windows/installer remain open. BookCheck work remains deferred under the owner's focused D-115 request. Roughly **3–6 further integrated review/result rounds**, findings-dependent, not a completion guarantee.

[PT-38 Urdu checklist](POS-PHASE4-PART18-URDU.md) · [remaining/installer](REMAINING-WORK-AND-TEST-INSTALLER-URDU.md).
