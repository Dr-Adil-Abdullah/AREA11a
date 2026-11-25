# ⏳ Phase4 portion38 — expense/drawing/write-off/Apply fault and retry review

> **⏳ checkpoint کی حد:** اس فائل کے نتائج/اعداد اسی درج شدہ checkpoint کے ہیں؛پرانے pending یا not-started جملوں کو موجودہ حالت نہ سمجھیں۔ [تازہ مجموعی رپورٹ](COMPLETE-STATUS-AND-DECISIONS-URDU.md) اور[فیصلوں کا index](DECISION-REGISTER-INDEX-URDU.md) پہلے دیکھیں؛نیا Windows PASS فرض نہیں۔

**⏳ v0.116.0 Tarn · 2026-11-10 · stable20,23–25 · PT-58 · full feature/runtime OPEN.**

## ⏳ Scope and contracts

| Point | Source change | Evidence boundary / remaining |
|---|---|---|
|20 ⏳ expenses/cash+goods drawing | Per-call observed Tx result; local shared expense-form guard arms before validation/service; acknowledged success before even result-null handling or UI callbacks; amount/qty/note/editor consumed before release | Real service/shift/permissions and WPF execution required. No expense draft/new workflow. |
|20 ⏳ write-off | Same outcome guard; product name pinned before service; result acknowledged before input clearing/history/batches | Original StockPolicy/cost fallback/rounding/SQL/private Save untouched; batch refresh may return empty/hidden on read error, not a posting verdict. |
|20 ⏳ stock-take Apply | Same observation; old session and line invalidated before callback/refresh; pending Apply blocks five editing actions | Core ApplyInside terminal session gate unchanged. Open/Count/Clear/Cancel/AddFound own failure retry redesign not done. |
|23 ⏳ no-commit classification | Each of five wrappers passes a new TransactionOutcome to Database.Tx; preflight refusals explicitly true; caught validation/generic errors use observed evidence, unknown message otherwise | Database.Tx/TransactionOutcome unchanged. CommitAttempted dominates even if later rollback returns; provider/rollback-error injection UNRUN. Pre-service throws are conservatively unknown in local UI. |
|24 ⏳ regression sources | Real migrated service fixture, whole-table snapshots, downstream faults then retry, reconciliation and local-state tests | 59 cases source only; fixture, overloads, SQL triggers and expectations uncompiled/unexecuted here. |
|25 ⏳ local retry | Ready→InFlight→Ready only explicit safe refusal; unknown→NeedsVerification; success→CommittedNeedsReset→Ready only after cleanup; WasAcknowledged survives final callback error | Not a durable idempotency key, authenticated session, process/thread-wide lock or cross-PC duplicate guarantee. Closing/reopening loses latch. |

## ✅ Explicit source preservation

- ✅ Financial SQL literals of ExpenseService/StockService/StockTakeService retained; private SaveGoods, Stock.Save and ApplyInside onward helper tails unchanged against P37.
- ✅ Existing migrations, old tests, other Core services, Sale/Purchase/Return/Database/TransactionOutcome and pricing/permission rules retained. No backup/cloud/Blackbox/BookCheck internals changed.
- ✅ Stock-category expense remains a label, not receiving. Cash drawing affects drawer; goods drawing does not subtract cash. Stock-take frozen-cost, uncounted-line warning, found batch and once-only session rules retained, not reimplemented.

## ⏳ Regression inventory — 59 new cases / 2744 expected suite, ALL C# UNRUN

| Group | Cases | Intended assertions |
|---|---:|---|
|24 ⏳ statement faults |34| expense/cash row+audit;goods/write-off batch+movement+row+audit;Apply batch+movement+session+audit;found new-batch, BEFORE/AFTER each;whole snapshot exact then single retry reconciliation |
|23 ⏳ refusals |10| five routes×invalid/inactive; explicit no-commit and no row mutation |
|24 ⏳ later line |2| Apply later found-line movement fault rolls back preceding stock line; successful retry terminal |
|20 ⏳ cost/uncounted |1| frozen valuation after current costs change; uncounted stock retained; found-batch warnings |
|25 ⏳ local state |12| same failure wording/different evidence, reentry, unknown/committed locks, invalid reset, acknowledged exception before/after reset, safe validation release |

The fixture uses migrations+Seed through TestDatabase, a synthetic drawer and direct fault triggers; snapshots include settings/parties/products/batches/business tables/stock sessions and lines/movements/payments/custom answers/audit/sequence. Actual UI ViewModels are not instantiated by these tests; local guard tests plus lexical UI checks are not WPF proof.

## ✅ Executed local evidence (not runtime certification)

- ✅ New `tools/check_expense_stock_sqlite.py`:37 real migrations,34 BEFORE/AFTER actual SQL fault rollbacks then one retry,whole dump/stock/business/audit/outflow checks PASS. Python owns plan/parameters/transaction/authority/terminal gate. No ShiftService, C# creator, Dapper or provider COMMIT execution.
- ✅ New `tools/check_expense_stock_source_negative.py`:33 lexical mutations rejected,original bytes restored. Checks source observation/order/guard/test anchors,not semantics or runtime branches.
- ✅ Full current static run: **105 checks**, **429 C# grammar parses**, **4761 calls / 9765 member references**, zero reported static issues. C# grammar/member scans are not Roslyn compilation.
- ✅ All **12 source-negative scripts PASS,349 mutations** rejected and exact files restored;all **29 SQLite scripts PASS**. These totals include the new probes;they are not additional xUnit cases.
- ✅ Independent Python encrypted-package reference:6 chunk-boundary round trips,24 negatives PASS;rules matrix current;`git diff --check` PASS. No C# codec/DPAPI/real restore execution.
- ✅ Preservation against P37 `5dc0a6521c9282428c2f5bbed5508410172d2e53`:783 previous tracked paths retained,155 other existing Core C# files exact,138 old test paths exact,37 migrations exact;three private financial helper tails and SQL literal sequences exact.
- ✅ Guide MD/HTML main content exact,all36 lesson anchors retained,stable1–36 retained,all57 source-board feature rows exact. Generated checklist0 fully certified /57 ongoing /0 never started;no feature was silently removed. Launcher/test/report scripts retain CRLF.
- ⏳ SDK/Windows/Inno unavailable here:2744 C# suite ALL UNRUN;no test binary,TRX,WPF screenshots or installer is claimed.

## ⏳ Explicit remaining gates

- ⏳2744 current C# compile/tests and WPF callback/reentry/focus/binding checks; old four Windows failures need fresh retest, not historical clearance.
- ⏳ Actual provider COMMIT/rollback errors, immediate/file/WAL production behavior,powercut,restart,reopen,parallel callers,durable identity,reconciliation and session/CAS.
- ⏳ Remaining recovery/PR/files/other callers and report integration; no new expense draft policy was assumed. Remaining5–7/26–29/30–35 work in stable tracker.
- ❌ Installer binary NOT BUILT. ⏳ FullPhase4 OPEN;source/synthetic PASS≠production/security certification. No synthetic shop posting.

## ⏳ Delivery record

- ⏳ Publication evidence belongs to the delivery response:commit/push on `arena/b4b7bf7b-area11a`,then independent `git ls-remote` exact hash comparison. This source document is not proof of publication and cannot contain its own final commit hash. No commit on another branch is authorized.
- ✅ Session-supplied release date2026-11-10 used; sandbox clock differs. Checklist `--date` supports explicit reproducible as-of without changing the machine clock or monkeypatching datetime.
