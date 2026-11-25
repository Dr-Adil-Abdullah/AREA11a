# ⏳ Phase4 portion39 — provisional return posting faults and local retry

> **⏳ checkpoint کی حد:** اس فائل کے نتائج/اعداد اسی درج شدہ checkpoint کے ہیں؛پرانے pending یا not-started جملوں کو موجودہ حالت نہ سمجھیں۔ [تازہ مجموعی رپورٹ](COMPLETE-STATUS-AND-DECISIONS-URDU.md) اور[فیصلوں کا index](DECISION-REGISTER-INDEX-URDU.md) پہلے دیکھیں؛نیا Windows PASS فرض نہیں۔

**⏳ v0.117.0 Bracken · 2026-11-10 · stable17,19,23–25 · PT-59 · runtime/full scope OPEN.**

## ⏳ Bounded source changes

| Point | Change | Explicit boundary |
|---|---|---|
|17/23 ⏳ PR Create/Link/Cancel | Each observes a per-call TransactionOutcome;preflight failures are known no-commit;caught validation and generic errors use observed evidence;unknown is not a blanket rollback claim | Database.Tx/TransactionOutcome and financial business bodies unchanged;actual provider COMMIT/rollback errors not injected |
|17/25 ⏳ PR page | Reuses P38 PostingAttemptGuard for all three writers;shared in-progress flag covers callbacks and printing;target ID/caption captured before Link/Cancel | Local synchronous state,not a durable token or authenticated session |
|17/25 ⏳ acknowledged cleanup | Acknowledge before result-null/selection/field/list callbacks;clear both decision inputs/lookup;invalidate selection before Load and again after its automatic pending-row selection | Page reads can still swallow errors as empty;this is not a new read-verdict/access review |
|25 ⏳ retry | Unknown or incomplete acknowledged reset blocks all page writers and Add/Remove tray;explicit known failure permits correction;clean consumed form permits genuinely new work | Reopen/restart/cross-PC/direct service callers not globally deduplicated;manual editing of text fields is not disabled |
|19/24 ⏳ money reconciliation source | PR payment remains one negative cash event;Link has no new money/stock;Cancel removes goods but never invents refund cash-in | Existing policy preserved,not a changed refund/cancellation design |

Generic errors retain the original exception in route/PR-ID logs,not raw technical detail in a UI failure message. Success with a missing result is still acknowledged and blocked,not reported as an unsaved return. A final status/print callback exception after fully consuming the form keeps WasAcknowledged and says SAVED;the page must not refund again. Source tests do not instantiate WPF ViewModels.

## ✅ Preservation boundary

- ✅ Against P38 `0b37f158df1b07f4c73b1e8e81365e7a806e608a`:all PR executable SQL literal sequences,private Save/reconciliation/allocation/helper tail,and Link/Cancel transaction callback bodies retained exactly.
- ✅ No migration,old test rewrite,financial calculation/numbering/stock/payment policy change;Sale/Purchase/Return/StockPolicy/Database/TransactionOutcome/P38 guard untouched.
- ✅ Existing PR same-bill legacy reconciliation,terminal Link/Cancel rules,integer refund remainder,OPEN batch fallback and zero-refund behavior retained. No PR auto-draft,permission redesign,backup/cloud or Blackbox/BookCheck changes.

## ⏳ C# regression inventory —45 new cases /2789 expected suite,ALL UNRUN

| Group | Cases | Intended assertions |
|---|---:|---|
|24 ⏳ Create faults |18| header/batch/movement/item/refund distribution/payment/audit/new OPEN batch/later stock line BEFORE/AFTER;whole snapshot rollback then one retry |
|24 ⏳ Link faults |8| allocation/header/audit/later allocation BEFORE/AFTER;no duplicate stock/payment;sale rows unchanged |
|24 ⏳ Cancel faults |10| batch/movement/header/audit/later line BEFORE/AFTER;prior stock line rolls back;cash payment never reversed/invented |
|23 ⏳ refusals |6| invalid or explicit supplied-role/grant denial per route;unchanged snapshot and NoCommitConfirmed;local guard accepts correction |
|17 ⏳ legacy link |2| correct same-bill allocation with/without wrong-bill attempt first;no extra payment or stock |
|19 ⏳ zero refund |1| goods added,no payment/drawer outflow |

The fixture uses migrated+Seed TestDatabase,two medicines,a real synthetic drawer,and Sale/PR services for setup. Full snapshot covers settings/parties/products/batches/PR header+items+allocations/sales+items/returns/payments/movements/audit/sequence. All fixture,SQL,trigger,overload and expectation assumptions still require actual .NET execution.

## ✅ Executed local probes / ⏳ limits

- ✅ New `tools/check_provisional_posting_sqlite.py`:37 migrations,36 BEFORE/AFTER statement faults with exact dump rollback and one retry;two-line refund remainder,OPEN batch,allocations,terminal repeats,unchanged payment on Link/Cancel and actual drawer SELECT PASS. Python owns plans,values,Tx,authority and terminal gates;not C# allocation algorithm/Database.Tx/ShiftService execution.
- ✅ New `tools/check_provisional_posting_source_negative.py`:29 lexical mutations rejected;exact source bytes restored. This verifies selected source text/order,not runtime control-flow semantics.
- ✅ Full current static run:**106 checks**,**430 C# grammar parses**,**4765 calls/9782 member references**,zero reported static issues. Grammar/member scans are not Roslyn compilation.
- ✅ All **13 source-negative scripts PASS,378 mutations** rejected and exact bytes restored;all **30 SQLite scripts PASS**. Totals include new probes,not xUnit execution.
- ✅ Independent Python encrypted-package reference:6 chunk-boundary round trips,24 negatives PASS;rules matrix current;`git diff --check` PASS. No C# codec/DPAPI/actual restore execution.
- ✅ Preservation against P38:789 previous tracked paths retained,158 other existing Core C# files exact,139 old test paths exact,37 migrations exact. PR SQL/private financial helper tail/Link+Cancel Tx callback bodies exact.
- ✅ Guide MD/HTML main exact,36 lesson anchors and stable1–36 retained,all57 source-board feature rows exact;checklist0 fully certified/57 ongoing/0 never started. Launcher/test/report checkout retains CRLF. New C# snapshot table identifiers checked against migrated SQLite;still not execution of the fixture.
- ⏳ .NET SDK/Windows unavailable here:2789 C# cases ALL UNRUN. Source assumptions,real provider/writer and WPF behavior need the actual runner.

## ⏳ Remaining gates and safety limits

- ⏳ Actual Windows/.NET2789 suite/build/TRX/fix/retest,WPF selection/binding/cleanup/reentry/printing/provider errors. No current C# runtime PASS,Windows screenshot or test installer claimed.
- ⏳ PR current stored writer/reader/receipt/search permissions,read-failure≠empty and broader stale output review remain separate5–7/17/21 work. The existing authorization policy was not tightened in this bounded fault review;no session/security certification.
- ⏳ Real COMMIT/rollback failure,parallel/restart/reopen/durable identity,remaining recovery/file/restore/cloud faults and full installed/hardware/isolation checks.
- ❌ Installer binary NOT BUILT. ⏳ FullPhase4 OPEN;no fake live-shop posts.36categories are not36Nexts;3–6rounds conditional,not a cap/deadline.

## ⏳ Delivery provenance

- ✅ Workspace Git metadata was rehydrated at the old base while content matched the published P38 tree except20 CRLF checkout normalizations. Fetched the same active remote branch,compared all789 paths and confirmed no workspace-only nonignored files,then aligned HEAD/index to P38 with mixed reset preserving working files. No source rollback or other branch switch.
- ⏳ Commit/push on `arena/b4b7bf7b-area11a` and independent actual `git ls-remote` hash comparison belong to the final delivery response;this source document is not proof of publication and cannot contain its own final commit hash.
