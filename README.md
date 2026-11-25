# Area11 — pharmacy POS + inventory for Windows

## ⏳ موجودہ 5.1 — Built-in fields: پرانی حالت،risk اور Save نتیجہ — v0.134.0 Eglantine

**5.1 جاری؛1تا5 کے باقی کام اور ضروری تصدیق سے پہلے6 نہیں۔** Native Required/Optional میں loaded-baseline prepare،modal کے بعد دوبارہCAS،observed outcome اور exact saved row handoff؛Save خود پوری فہرست تازہ نہیں کرتا۔ Unknown/SAVED-callbackfailure پر invalidation،automatic timer/regrant سے نہیں کھلتا؛Explicit Refresh Fields سے موجودہ حالت دیکھ کر نیا ارادہ،نہ durable identity۔ Owner/risk/financial rules برقرار۔ ⏳21 نئے C# cases/3347 suite UNRUN؛✅125 محدود static،45 نئی lexical mutations،30 Python WAL/DELETE plans؛اصل .NET/provider/WPF ثبوت نہیں۔ [⏳ جائزہ/PT-76/باقی5.1](docs/NATIVE-FIELD-OUTCOME-REVIEW-URDU.md)؛[⏳14حصے](docs/REMAINING-14-PORTIONS-URDU.md)۔ Remaining custom/imperative writers،startup registry اور Windows تصدیق باقی؛StockTake بعد میں اسی5 میں۔ ❌6/installer شروع نہیں۔


## ⏳ موجودہ حصہ2/14 — v0.120.2 Willow

✅ مالککےاصلv0.120.1WindowsrunسےCore/TestsDebugcompileثابت؛2963total/2941PASS/22FAIL/0skip۔ ⏳ان22ناکامیوںکی14testfilesمیںfixtureاصلاح؛productioncode/حساب/permissionsنہیںبدلے،2963casesبرقرار؛نیاWindowsrerunباقی۔ [⏳ نتیجہ/حدود](docs/WINDOWS-V1201-TEST-REPORT-REVIEW.md)؛[⏳14حصے](docs/REMAINING-14-PORTIONS-URDU.md)۔ حصہ3شروعنہیںکرنا؛صرفحصہ2کیاجازتملی۔ پرانے“حصہ1تصدیقباقی/حصہ2غیرمنظور”نوٹساببعدکیاسentryسےمتروک،پرانےrunhistoricalہیں۔


## ⏳ موجودہ کام — v0.120.1 Alder،نئے منصوبے کا حصہ1/14

✅ DocumentFieldTestsمیںExpenseListکوexplicitOwnerدیا؛اصلassertionsاورproductioncodeبرقرار۔ ⏳اصلcompileابھیغیرمصدقہ(.NETSDKغائب)،2963C#casesUNRUN۔ [⏳ مکملمنصوبہ/حدود](docs/REMAINING-14-PORTIONS-URDU.md)؛اگلاپورشنصرفنئیاجازتسے،مختصرخلاصہ3–4لائن؛پرانیportion42تککیentriesتاریخیہیں۔


## ⏳ Latest — v0.120.0 Shale: portion42 / stable17–19,23–25 ledger outcomes

⏳ Customer Collect/Supplier Pay now observe existing Tx no-commit evidence;unknown COMMIT/rollback never promises Nothing changed. Customers/Recovery Receive/Suppliers Pay use existing PostingAttemptGuard:before service,ack before callbacks,only explicit no-commit retries;unknown/SAVED cleanup failure survives Refresh/regrant until verified page reopen.Consumed amount/note cleared after reload/callbacks;legitimate fresh partial payments remain allowed.47newC#source cases/2963suite ALL UNRUN. [⏳ PT-62](docs/POS-PHASE4-PART42-URDU.md),[⏳ trace](docs/POS-PHASE4-LEDGER-OUTCOME-REVIEW.md),[⏳ stable1–36](docs/NUMBERED-WORK-STATUS-URDU.md). ✅ New local probe:37 migrations/12 actual ledger-SQL rollback/retry plans+6 synthetic ack/refresh/reopen-boundary cases;31 lexical mutations rejected/restored. NOT C#/provider/WPF proof. ✅ Financial SQL/Tx callback bodies,settlement/SupplierB/current-access policy,RecoveryService read/slip/accounting and old tests retained. ⏳ No durable request key or cross-page/restart/cross-PC dedup;parent creation UI/other gallery writes/remaining callers/session/CAS/provider/parallel/powercut/restore-cloud/Windows-build-results-hardware-isolation/FullPhase4 OPEN. ❌ Installer binary NOT BUILT. ⏳ Eachportiondocs+samebranchpublication+independentactualremoteverification;36categories≠36Nexts. Earlier checkpoints historical.


## ⏳ Latest — v0.119.0 Moraine: portion41 / stable12,23–25,28 photo file faults

⏳ Photo Add and staged Product/Customer/Supplier creation now track the exclusively created bounded copy before writes; GUID names never overwrite collisions. Cleanup uses each existing transaction's observed outcome: confirmed no-commit only; unknown keeps/logs copy and returns verify-before-retry. Gallery Add has window-local unknown/ack-refresh latch, not reset by Refresh/regrant.28newC#source cases/2916suite ALL UNRUN. [⏳ PT-61](docs/POS-PHASE4-PART41-URDU.md),[⏳ trace](docs/POS-PHASE4-PHOTO-FILE-FAULT-REVIEW.md),[⏳ stable1–36](docs/NUMBERED-WORK-STATUS-URDU.md). ✅ New local reference:17 Python file cases+37 migrations/3 actual photo INSERT rollback plans+1 synthetic lost-ack retention;23lexical mutations rejected/restored. NOT C#/provider/WPF proof. ✅ Financial SQL/current access/other services and old tests retained. ⏳ Parent creation UI retries,other gallery actions,session/CAS/provider COMMIT/parallel/powercut/global identity,recovery financial paths,restore-cloud/Windows-build-results-hardware-isolation/FullPhase4 OPEN. ❌ Installer binary NOT BUILT. ⏳ Eachportiondocs+samebranchpublication+independentactualremoteverification;36categories≠36Nexts. Earlier checkpoints historical.


## ⏳ Latest — v0.118.0 Sedge: portion40 / stable5–7,17,21,23 current PR boundaries

⏳ PR Page/Get/Lines/Search/receipt require current stored active/same-parsed-role Returns reader;Create/Link/Cancel resolve stored actor inside observed Tx,decisions current Owner/Manager,stored audit name. Explicit Page verdict replaces UI empty-on-error;complete authorized receipt header+lines prevents header-only success. Action/nominal3s Loaded timer clears denied page/form/tray/lookup,stable allowed inputs retained,regrant fresh Load;detached decision/lookup refused,P39 SAVED/unknown guards retained.99newC#sourcecases/2888suite ALL UNRUN. [⏳ PT-60](docs/POS-PHASE4-PART40-URDU.md),[⏳ trace](docs/POS-PHASE4-PROVISIONAL-CURRENT-REVIEW.md),[⏳ stable1–36](docs/NUMBERED-WORK-STATUS-URDU.md). ✅ PR financial SQL/helpers and receipt Compose retained;two old tests only add explicit Owner actors;no new financial/discount policy. ✅ New local probes:35Python actor/grant denials+actual readonlySQL and27lexical mutations rejected/restored,NOT C#/WPF/security proof. ⏳ Local read gate is not external coherent snapshot/session/same-role-ID/CAS/instant prepared-output recall;remainingcallers/provider/parallel/durableidentity/recovery-files/restore-cloud/Windows2888-build-results-hardware-isolation/FullPhase4 OPEN. ❌ Installer binary NOT BUILT. ⏳ Eachportiondocs+samebranchpublication+independentactualremoteverification;36categories≠36Nexts,3–6rounds conditional notdeadline/cap. Earlier checkpoints historical.

## ⏳ Latest — v0.117.0 Bracken: portion39 / stable17,19,23–25 PR posting fault/retry

⏳ PR Create/Link/Cancel now observe explicit per-call no-commit evidence;unknown COMMIT/rollback/throw is not a blanket Nothing changed. Page reuses P38 local guard across all three writes and tray edits;acknowledges success before callbacks,clears both decision fields/lookup,forgets old target before refresh AND the automatically selected next pending row after Load.45newC#source cases/2789suite ALL UNRUN. [⏳ PT-59](docs/POS-PHASE4-PART39-URDU.md),[⏳ trace](docs/POS-PHASE4-PROVISIONAL-POSTING-REVIEW.md),[⏳ stable1–36](docs/NUMBERED-WORK-STATUS-URDU.md). ✅ Financial SQL/private helper tail/Link+Cancel Tx bodies preserved;Link has no duplicate money/stock,Cancel does not invent cash-in;no PR draft/new financial policy. ✅ New local probes:36SQL statement faults+retry and29lexical mutations rejected/restored,NOT C#/WPF/providerCOMMIT/allocation-algorithm proof. ⏳ PR current actor/reader/search/receipt/read-verdict review remains5–7/17/21;localflag is not durable identity/restart/cross-PC exactly-once. ⏳ Remainingprovider/parallel/session/CAS/recovery-files/restore-cloud/currentWindows-build-results-hardware-isolation/FullPhase4 OPEN. ❌ Installer binary NOT BUILT. ⏳ Eachportiondocs+samebranchcommit/push+independentactualremoteverification;36categories≠36Nexts,3–6rounds conditional notdeadline/cap. Earlier checkpoints historical.

## ⏳ Latest — v0.116.0 Tarn: portion38 / stable20,23–25 expense and stock fault/retry

⏳ Expense/cash+goods drawing/write-off/stock-take Apply now observe explicit transaction outcome: only definite preflight/no-COMMIT rollback permits local retry. Unknown/throw blocks reuse; acknowledged success is latched before callbacks and says SAVED if cleanup fails. Old Apply session consumed before refresh; five stock-take edits blocked while Apply uncertain.59newC#source cases/2744suite ALL UNRUN. [⏳ PT-58](docs/POS-PHASE4-PART38-URDU.md),[⏳ trace](docs/POS-PHASE4-EXPENSE-STOCK-FAULT-REVIEW.md),[⏳ stable1–36](docs/NUMBERED-WORK-STATUS-URDU.md). ✅ Financial SQL/private helper tails and once-only Apply preserved;no expense auto-draft/new policy. ✅ New local probes:34SQL statement fault rollbacks+retry and33lexical mutations rejected/restored,NOT C#/WPF/providerCOMMIT proof. ⏳ Local guard is not durable identity/restart/cross-PC exactly-once;provider/parallel/session/CAS/remainingrecoveryPR-files/restore-cloud/currentWindows-build-results-hardware-isolation/fullPhase4 OPEN. ❌ Installer binary NOT BUILT. ⏳ Eachportiondocs+samebranchcommit/push+independentactualremoteverification;36categories≠36Nexts,3–6rounds conditional notdeadline/cap. Earlier checkpoints historical.

## ⏳ Latest — v0.115.0 Cairn: portion37 / stable15,16,23,24,25 automatic shared draft handoff

✅ Owner selected ordinary Counter/Purchase Save through existing shared Drafts and refusal of all draft-to-Hold copies;three reporting symbols recorded. ⏳ UI now saves/links before posting the SAME draft,unknown initial-save latch prevents blind new copies,Hold outcome/ack-cleanup and Purchase commit-reset/reconstruction guards added.41newC#source cases/2685suite UNRUN. [⏳ PT-57](docs/POS-PHASE4-PART37-URDU.md),[⏳ trace](docs/POS-PHASE4-AUTOMATIC-DRAFT-REVIEW.md),[⏳ stable1–36](docs/NUMBERED-WORK-STATUS-URDU.md). Initial draft-save unknown means no document creator ran;possible open draft must be checked,not a guaranteed posting marker. Current draft payload/current-reader rules retained. ⏳ FullPhase4/providerCOMMIT/parallel/globalidentity/directCoreCreatecallers/session/CAS/remainingstock-expense-recoveryPR-filefaults/restore-cloud/Windows2685-build-hardware-isolation OPEN. ❌ Installer binary NOT BUILT. ✅ Old source-board ticks no longer masquerade as100%certification in generated checklist:57started features remain ⏳ pending current evidence;source board preserved. ⏳ Eachportiondocs+samebranchcommit/push+actualremoteverification;36categories NOT36Nexts,3–6rounds conditional notdeadline/cap. Earlier checkpoints historical.

## Latest — v0.114.0 Lichen: portion36 / stable15,23,24,25 recovery and draft faults; Windows OPEN

Owner approved atomic linked Sale recovery draft on held Resume and verification marker on unknown draft-post outcome. Resume creates draft+held link/consume+two audits in one Tx; Counter links before callbacks, blocks partial reconstruction/missing-product shortened saves, confirms Clear leaves saved work; linked recovery cannot repark a duplicate. Explicit per-call no-commit observation: only no work or confirmed pre-COMMIT rollback restores; COMMIT-attempt/throw/unknown stays marked. Restore failures/no-ops honestly reported; finalize failures return SAVED+no-repost warning in document/envelope/UI.29newC#source cases/2644suite ALL UNRUN. [Trace](docs/POS-PHASE4-RECOVERY-FAULT-REVIEW.md),[PT-56](docs/POS-PHASE4-PART36-URDU.md),[stable1–36](docs/NUMBERED-WORK-STATUS-URDU.md).103static and synthetic probes are not C#/provider/WPF proof. Separate draft transactions, legacy resumed rows no backfill, ordinary active-draft Hold policy, direct non-draft unknown retries/global identity/session/CAS/actualCOMMIT/powercut/parallel remain OPEN. Next23–25 otherstock/expense/recoveryPR/filefaults+durableidentity,5/6/7quickadd/fullPOS/session/preparedoutputs,26–29restore/cloud,2/4/30–35Windows2644/build/results/hardware/isolation,36authorizedinstallerNOTBUILT. Pricing/refunds/SupplierB/Blackbox/BookCheck unchanged. FullPhase4OPEN;eachportiondocs+samebranchcommit/push+actualremoteverification. No fake live-shop posts.36categories NOT36Nexts;3–6rounds conditional,notdeadline/cap. Earlier checkpoints historical.

## Latest — v0.84.0 Lapwing: phase4 portion6 — definitions/time correction, audit IN PROGRESS

Existing owner-only rule enforced: CustomField Add/Update/SetActive/Move resolve live active owner INSIDE transaction before duplicate/target/no-op and audit stored owner name. Public List/Values/DescribeValues resolve current account/module; management/inactive list verifies owner. Public SaveValues revalidates in transaction and passes resolved reader to audit/helper; normal permitted cashier answers retained. Delegated internal parent answer helpers intentionally unchanged: parent authorizes/owns transaction; full parent review OPEN. TimeCorrection.CanFix/ListBills validate current DB owner; Apply revalidates owner and reads/plans/writes/audits in ONE immediate Database.Tx, removing detached pre-write planning window. Offset/minute/legacy/payment shift policy, created_at and money unchanged; no UI signatures changed.20 new facts,1421 written UNEXECUTED;72 static checks,12 negative source mutations rejected/restored;377 grammar files/0 reported member problems. No .NET SDK/Windows/xUnit/WPF/hardware execution. New `tools/check_time_correction_sqlite.py` independently executes36 migrations/actual source SQL in synthetic TEMP DB: immediate-lock second-writer refusal, audit-fault atomic rollback, created_at/money/old-new audit/FK preservation; not C# concurrency/session proof. Existing Extra SQLite history/BookCheck/B560540/FK PASS.12+4 verbatim SQL blocks unchanged,36 migrations/Sale/Return/Purchase/Database hashes unchanged;622 baseline files retained. Main36-lesson Urdu MD/HTML exact parity; PT-26 `docs/POS-PHASE4-PART6-URDU.md`; matrix/limits `docs/POS-PHASE4-DEFINITION-TIME-REVIEW.md`. Next SAME phase: delegated parent/write/session/record boundaries, Reports DeadStock valuation policy (D-109 only History/Reorder; do not silently expand), finance/stock/concurrency/fault/files/backup/cloud/full guide/Windows. Not arbitrary-ID authentication, full DB-error fail-closed, instant session/in-flight/preview/file revocation or whole audit PASS. Prior dirty work preserved; same branch, no reset/stash/checkout/commit/push, installer or fake live-shop posts.


## Latest — v0.83.0 Snipe: phase4 portion5 — historical cost/snapshot readers, audit IN PROGRESS

Owner explicitly chose management-only History purchase rate/total and Reorder last cost after everyday example (D-109); do not re-ask. Existing page module still required. ProductHistory masks denied raw purchase rate+total+aggregate, exposes known flags/hidden labels, and applies to VM/print/WhatsApp/PDF/CSV (both numeric CSV cells empty). Reorder LastCostKnown distinguishes unknown/denied from authorized actual zero; stock/minimum/suggestions/messages unchanged. SnapshotReadAccess resolves current active DB account/role-kind/module for History.Load,Reorder.Report,Dashboard.GetSnapshot,Report.Build/Days; Dashboard preserves Pos OR Reports. Actorless public Days bypass now requires reader/ServiceResult and global+Profit gate; no compatibility bypass.24 new facts,1401 written UNEXECUTED;71 static checks,12 negative source probes rejected/restored.376 grammar files,0 reported member problems; no .NET SDK/Windows/xUnit/WPF/hardware run.2/3/14/10 verbatim SQL blocks unchanged,36 migrations and Sale/Return/Purchase source hashes unchanged,618 baseline files retained; isolated SQLite history/BookCheck/B560540/FK PASS, not C# runtime. Main36-lesson Urdu MD/HTML reconciled, safe PT-25 `docs/POS-PHASE4-PART5-URDU.md`, evidence `docs/POS-PHASE4-SNAPSHOT-REVIEW.md`. Report DeadStock.ValuePaisa remains existing Reports-authorized batch-cost valuation: explicit next policy/review item, NOT closed by D-109; Dashboard supplier/customer aggregates and report purchase totals unchanged. Reorder SetLevel/RecordOrder writes, CustomFields/TimeCorrection/session/finance/concurrency/files/cloud/full guide/Windows remain OPEN. Not arbitrary-ID authentication, full error fail-closed, immediate in-flight/session/preview/file revocation or absolute cost secrecy. Operational receiving/SR/stock/expense costs and existing profit grants unchanged. Preserved prior extensive dirty work; same branch, no reset/stash/checkout/commit/push, installer or fake live-shop posts.


## Latest — v0.82.0 Godwit: phase4 portion4 — catalog reader/cost projection, audit IN PROGRESS

Same approved phase4. ProductService Search/SearchSmart/Get now require current active reader and purpose/module; exact/token/fuzzy/Get share projected results. Counter/PR hide raw catalog cost; Catalog cost requires management; existing module-authorized receiving/SR/stock/expense preparation keeps necessary cost. Margin additionally requires global+Profit permission and labels retail denominator. Desktop callers pass _user; masked zero cannot open product editor. ReportTotals masks denied raw sale/restock cost without changing monetary SQL.20 new facts,1377 total written UNEXECUTED;70 static checks;9 negative source probes rejected/restored. No .NET SDK/Windows/xUnit/WPF/hardware execution.34+14 verbatim SQL blocks,36 migrations,SaleService/ReturnService unchanged from portion start; isolated SQLite history/BookCheck/B560540/FK PASS, not C# runtime. Main36-lesson Urdu MD/HTML reconciled with exact body parity; safe PT-24 `docs/POS-PHASE4-PART4-URDU.md`; evidence `docs/POS-PHASE4-CATALOG-REVIEW.md`. NOT arbitrary-ID authentication, new capability grants, full DB-error fail-closed, immediate session/in-flight/preview/file revocation or absolute cost secrecy. Operational costs remain available under existing authorized modules, whose policies are unchanged. Next SAME phase: Reorder.LastCost/ProductHistory purchase rows/Dashboard/report/export projections, CustomField/TimeCorrection/session, then finance/stock/concurrency/fault/files/backup/cloud/full guide/Windows. All613 portion-start files retained; prior extensive dirty work preserved, no reset/stash/checkout/commit/push. No installer or fake live-shop posts. Full phase4 remains OPEN.


## Latest — v0.81.0 Curlew: phase4 portion3 — ledger readers/profit boundary, audit IN PROGRESS

Same authorized phase4. Customer/Supplier ledger List/Page/Statement/month/bulk/expired read APIs require User first; no actorless public fallback. New LedgerReadAccess resolves current active DB account, rejects snapshot-role mismatch, checks Customers/Suppliers using current User. Customer PAY receipt revalidates reader. Customer List/Page mask denied raw profit with ProfitKnown under global+permission policy. Existing UI print/PDF/WhatsApp/month/bulk callers now propagate _user; existing tests preserve explicit owner context and financial expectations. No posting/monetary SQL/schema/history/Extra B/cost-floor policy changed.20 new facts,1357 cases written UNEXECUTED;69 static checks; no .NET SDK/Windows runtime here. Narrow evidence `docs/POS-PHASE4-LEDGER-REVIEW.md`, Urdu PT-23 `docs/POS-PHASE4-PART3-URDU.md`, main36-lesson MD/HTML updated. Per-module grants/defaults and Permission error policy unchanged; an owner Suppliers grant remains supported. NOT arbitrary-caller authentication, full DB-error fail-closed, instant in-flight/session/preview/file revocation or whole-layer cost secrecy. Next SAME phase: actorless catalog safe-vs-authorized cost data/callers, raw report/reorder/product-history/Dashboard projections, custom fields/time correction/session, then finance/stock/fault/files/cloud/guide/Windows. At turn start HEAD was base805626e with prior0.80 source262 changed/untracked paths; preserved all pre-existing work, no reset/stash/checkout. New changes saved on current branch; no commit/push claim for this portion. No installer authorized.


## Latest — v0.80.0 Ibis: phase4 portion2 — Settings/native fields/profit projections, audit IN PROGRESS

Same approved phase4, not an installer. Fixed missing Settings App name & icon section0 and SelectedTabIndex stale clamp6 (Fields7 opened App words); all eight existing contents/indices retained, WPF proof pending. NativeFieldPolicy List/SetRequired verify live active DB owner; write check inside transaction BEFORE no-op; current stored owner name used in acknowledgement/audit (existing fixture assertions updated accordingly). Critical confirmation and rollback remain. Doctors now use ProfitPolicy global+permission, ProductHistory.Load zeroes denied raw sale profit rows/aggregate instead of only hiding UI/export; stored snapshots and all financial arithmetic unchanged. **1337 written cases (+12),68 static checks; no SDK/C#/Desktop/WPF execution here.** Method-specific matrix `docs/POS-PHASE4-ACCESS-REVIEW.md` explicitly leaves HIGH PRIORITY actorless CustomerLedger/SupplierLedger/catalog reads, raw costs, stale sessions, CustomFields/time-correction, finance/concurrency/files/cloud and full guide/Windows evidence OPEN. No mathematical-secrecy/authenticated-session/full-audit claim; no Permission read-error/HasOpenDrawer/factory-recovery/compatibility policy changes. Updated main36-lesson offline MD/HTML for bounded actions; Urdu test steps `docs/POS-PHASE4-PART2-URDU.md`. Next portion of SAME phase prioritizes actorless read contracts/callers and cost/profit projections, not a repeat of Settings repair. No installer authorized.


## Latest — v0.79.0 Tern: phase4 portion1 — owner/secret/photo boundaries, comprehensive audit IN PROGRESS

Owner's next authorized phase4, not an installer. This portion traces26 routes (25 AddPage + Dashboard wordmark),8 Settings contents including nested Fields, and all36 guide lessons in `docs/POS-PHASE4-EVIDENCE.md`, with T=trace/F=focused review and explicit15-dimension remaining work; NOT a full audit PASS. Fixed live active-owner DB checks inside Auth CreateStaff/SetSecret/SetActive, Permission Set/Reset, recovery save and protected brand/icon/focus/basis settings transactions; audit uses current owner name for Auth/Permission. Reset clears alternate password/PIN; missing targets refuse, all owner deactivation blocked. Recovery hash+nonsecret audit atomic/cache after success; generic recovery-key writes blocked, factory area11 recovery unchanged. Shared photo filename resolver now protects thumbnails/gallery/bulk covers/full-size/avatar/cover choice, no historical row/file deletion; not a symlink/reparse-point guarantee. TestDatabase seed confirms existing owner fixtures remain legitimate. **1325 written cases (+25),67 static checks; no .NET SDK or C#/Windows execution here.** SQLite proof separate and memory-only. Approved finance/Extra B560+540, actual-cost floor, legacy HasOpenDrawer/Permission read-error defaults, compatibility data unchanged. Next SAME phase: Settings index0 has no shell section (reachability discrepancy), direct-service/stale-session/cost projection matrix, finance/stock/duplicate faults, inputs/backup/cloud, full tutorial action reconciliation + Windows. No full security/runtime certification. Urdu test/remaining guide: `docs/POS-PHASE4-TEST-URDU.md`. No installer authorized.


## Latest — v0.78.0 Avocet: approved phase 3 — sale-level Extra Discount, return policy B (Windows pending)

Owner's `next` authorized phase3; after an everyday example owner explicitly chose **B**: A560+B560=1120, Extra20, paid1100; first item refunds560, last is capped540, never proportionally reprice original products. Owner alone configures each person's separate enabled flag/rupee cap (no automatic grant even owner); current active-account allowance re-read on quote/post. Extra is one absolute amount AFTER all ordinary line/bill discounts, rounding and points. It is NEVER spread into rates/quantities/sale-item discounts or line totals; those remain unchanged. Existing per-line actual-cost floor remains, and final sale-level payable must also cover total allocated purchase cost, owner included. More / split → Extra Discount → Apply/Remove; owned confirmation shows pre-extra/extra/final, repeated Apply replaces not adds; changed-but-unapplied entry blocks completion. Hold/draft preserve/revalidate; new cart after clear/post/park starts0. Migration036 additive with historical pre-extra NULL/extra0, historical money unchanged. Transaction stores pre-extra/extra/final, final payment/credit/change/points earning and dedicated audit. Receipt/reprint/Bills show separate amounts (Extra is mandatory even with ordinary discount line hidden). Overall/day/shift/cash/profit use final total; customer aggregate subtracts Extra once; category report has a separate negative unallocated allowance row. Product/top-item figures explicitly BEFORE bill Extra; no invented allocation. Books check reconciles sum of unchanged items minus Extra and the header/snapshots. Return preview/save both use original item value then remaining final bill/collected-money cap, with explicit Extra/earlier-refund warnings. Existing already-paid no-bill PR workflow retained, linked-bill warning added, later invoice refund cap preserved; no universal no-bill overpayment certification. **1300 written cases (+30), 66 static checks; no .NET SDK, no Core/xUnit/Desktop/WPF runtime here.** Independent Python SQLite proof executes all36 scripts and actual BookCheck SQL on synthetic memory-only data, verifies historical preservation and deliberate snapshot-corruption detection. PT-22 and phase3 Urdu test/disable guide added; main36-lesson guide updated. Phase4 full page/15-point audit is NEXT, NOT completed. No installer authorized.


## Latest — v0.77.0 Sandpiper: approved phase 2 — per-person pricing/item editor (Windows pending)

Owner's next authorized ONLY phase 2 of `docs/POS-NEXT-REVIEW-PLAN-URDU.md`. Dedicated owner-only per-person panel in Settings → Staff saves independent manual rate-edit/reduction, profit-discount, selling-total-discount and one-bill basis-override allowances. Existing/new non-owner accounts with no row start denied (manager role is not a grant); owner can configure self too. Core reads current active account/allowances per quote/post, caps line and bill adjustments, audits owner changes; UI-only settings and native workbook bypasses are blocked for protected pricing policy. New `discount.billBasis` defaults to profit for existing shops' future bills (035), ignores retired inert `discount.mode`; owner Settings may choose retail. Allowed Counter basis override prompts and resets to Settings default after clear/park/post; retail bill >5% asks BEFORE every post, Core requires and consumes transient confirmation, JSON drafts never retain it. Cart remains read-only deliberately; double-click/Enter opens an owned proposal editor, quoted by Core before Apply. Normal / manual final rate / profit item % / retail item % are mutually exclusive; adjusted rows never receive bill %; reset returns normal, further Add creates/merges only normal lines. Manual cap is against current reference selected-unit retail (1200,20% → max240/min960); without catalog reference, explicit rate permission still required and cost floor applies, but there is no percentage reduction to measure. Actual FEFO batch cost is mandatory even for owner/legacy lock-off; excessive percentage now REFUSES instead of silent clamp. Rounding/points redistribution also protects each allocation's cost. Authorized owner/manager sees actual allocated cost/gross profit/selling-margin in editor, direct hidden-cost quote fields masked. Profit-based totals can inherently imply cost to someone granted that discount; visibility is not mathematical secrecy. Migration035 is additive: sale/line pricing snapshots and held-cart modes/basis, old monetary rows unchanged; drafts keep metadata and revalidate, returns continue from stored net. **1270 written cases (+30), 65 static checks; no .NET SDK, no xUnit/Desktop/WPF execution here.** Actual SQLite migration scripts checked independently. Existing historical retail/clamping/optional-floor test expectations updated to approved policy, not blindly retained. Phase 3 NEXT: sale-level Extra Discount without rewriting product rates/line discounts. Phase 4 full page/15-point review is NOT done. Full guide, phase-2 test/rollback steps and PT-21 updated. No installer authorized.


## Latest — v0.76.0 Osprey: approved plan phase 1 — POS space/keyboard (Windows proof pending)

Owner approved the consolidated four-phase plan with “next”; ONLY phase 1 implemented. Owner selected the reserved compact search pane (B), not A. Removed Counter's duplicate title/subtitle/cashier block (shell identity/navigation unchanged); Refresh moved into fixed footer actions. Quiet 32-DIP-minimum, 9-DIP-rounded, softly tinted buttons include a visible keyboard-focus cue. Initial active/unblocked view load focuses search; successful Add gestures restore search without refocusing on every property change. Results row Enter/double-click use the same AddCommand once (repeat Enter ignored, photo buttons/headers/empty space excluded), cell focus avoids accidentally opening the photo gallery. Search textbox Enter is STILL exact-barcode-only. Shift+arrows move through ordered visible sections (search entry → matches when present → cart → optional details/parked → customer/payment/discount → cash/complete → actions); hidden sections skipped, cart edits committed first, open combos closed. Native caret/list/grid arrows and Tab remain; button arrows stay within their section; Enter toggles AutoPrint without invoking Complete. Esc in an open ComboBox belongs to that list first, and hiding focused results returns to search. **No financial ViewModel, Core service/model/migration, receipt or pricing calculation changed.** **1240 written cases unchanged, 64 static checks; Windows build/xUnit/keyboard/DPI/PT-20 pending.** A dedicated source gate covers phase 1; source gates are not WPF runtime proof. Phase 2 NEXT: per-person owner-only permissions, item editor (including cart double-click/Enter) and profit/retail/line exclusion calculation. Phase 3: sale-level Extra Discount (do NOT rewrite product rates/discounts). Phase 4: page-by-page 15-point review and guide reconciliation. All approved detail is in `docs/POS-NEXT-REVIEW-PLAN-URDU.md`; the plan does not override immutable old releases, it supersedes earlier draft questions. Current discount still uses full-selling-total legacy behavior; new financial decisions are NOT implemented by this UI release. No installer authorized.


## Latest — v0.75.0 Kestrel: POS layout trial B (final choice and Windows proof pending)

Owner explicitly requested “tryal b”. Search now has a permanently reserved narrow pane BESIDE the wider Cart, not an overlay. Its width is responsive (1* capped at 340 DIPs vs Cart 2.4*); blank search and successful Add/scan leave the pane empty with a prompt, never a whole catalog. Matches are one-column stacked cards (photo/name/strength/pack/stock/rate/salt/rack), so the narrow pane needs no nine-column horizontal table. Optional details/held carts open temporarily over the cart area, constrained to its width, not as a third permanent width-consuming pane. Compact fixed actions, customer/payment/discount, total/cash/change/F4, open-draft indicator and success modal are retained. Search/scan/permissions/financial ViewModel and Core service/model/migration code are unchanged from A. **1240 written cases unchanged, 63 static checks; Windows build/xUnit and visual PT-19 pending.** Trial A remains v0.74.0 Marlin (`cfbc68d`); no owner preference or PASS for either has been supplied. Next: owner compares A/B and chooses final (or requests adjustments). Profit/total discount setting, future-bill profit default for existing shops and total-basis >5% before-save confirmation are approved but STILL QUEUED. THIS release still uses the former full-selling-total discount; historical bills unchanged. Installer only on بنا دو.


## Latest — v0.74.0 Marlin: POS layout trial A (not final; Windows/owner proof pending)

Owner wants to TEST BOTH layouts, first transient search results now, then a small standing search panel on next, and choose the final layout later. Trial A removes the always-visible catalog column: blank query shows no products or hidden selection, typing opens a bounded in-window results overlay, and successful Add/scan clears it. Barcode Enter remains exact-only; fuzzy results are labelled and require human Add. Cart owns the main area; fixed compact actions (Park / Save draft / Open drafts / Parked carts / Reprint / More) sit with routine customer/payment/discount and total/cash/change/F4 outside scrolling content. Optional doctor/points/extra answers/new customer and held carts open mutually-exclusive side details; split selection opens its inputs. Search returning no rows does not mistake a deliberately blank query for an empty shop. Failed barcode adds retain their real refusal status. Existing stock, permissions, sale modal and printing remain; **NO discount calculation/default change in this UI trial**. Core service code is unchanged. **1240 written cases (unchanged), 63 static checks; no Windows build/xUnit or visual pass here.** Next on owner's next: trial B; then final layout choice. Discount decisions are approved but QUEUED: owner-selectable profit/total basis, switch existing shop's FUTURE bills to profit, total-basis >5% confirmation before each save; historical bills untouched. Current release still calculates the former full-selling-total discount. Installer only on بنا دو.


## Latest — v0.73.0 Laurel: reachable checkout, real F4, owned success popup (Windows pending)

Owner's Heather screenshots show the checkout controls below the 1366px-wide short screen and an almost invisible cart. Confirmed cause: the payment ScrollViewer lived in an Auto-height row and measured its whole long stack, starving the cart's star row. Fixed: narrower catalog/wider checkout, two bounded star rows for cart/options, and a separate fixed footer with total/cash/change/Complete sale/status. All other controls remain scrollable. F4 previously existed ONLY as button text; now a preview key handler commits rate edits, suppresses repeat keys and invokes the SAME guarded command. Re-entry is blocked through special-price dialogs, printing and confirmation. Owner selected an owned modal success window: tick, invoice, total, cash received, very large change, paid/due and honest print status. Done/× only dismiss; they cannot save/print twice. Existing sale.successPopup and AutoPrint choices stay independent. Core result adds committed cash-tender/assumption metadata (blank tender retains legacy exact-cash assumption; online/card/credit never pretend to be cash). Receipt, stock, discount and real product data unchanged. Replaced the small obsolete inline confirmation state, not an unrelated program. Product strength/pack/base-unit explanation with four examples added to the Urdu guide; product fields/values were NOT changed. **1240 written cases (+6), 62 static checks; no Windows build/xUnit or visual pass here.** Next: owner rebuild/reinstall, full 1240-case Windows log and PT-17 short-screen/F4/modal/print proof. Installer only on بنا دو.


## Latest — batch 2, portion 4: Urdu tutorial delivered (documentation only)

Owner's `next` authorized the final tutorial portion. [Offline, printable Urdu tutorial](docs/USER-GUIDE-URDU.html) and [editable Markdown source](docs/USER-GUIDE-URDU.md): **36 lessons**, all navigation pages and Settings groups, with the complete option summary FIRST and separate step-by-step detail SECOND. Source-reviewed against v0.72.0 views/view-models and relevant services; not a claim of observed Windows behavior or owner comprehension/acceptance. Covers cashier/owner workflows, missing price vs stock, no availability text on receipts, mixed supplier-payment recording, Fields' real scope, stock reconciliation, backup/restore risk, cloud limits and deferred LAN. HTML is self-contained RTL, mobile/print styled, has lesson links, no scripts or network assets. No application/build/color/test-count change: source remains v0.72.0 Heather, **1234 written cases**; its Windows run and physical checks remain pending. Next: owner reads/asks about lessons and provides Windows/physical evidence; setup/production Phase 6 ONLY on explicit بنا دو. Earlier “tutorial queued” notes are history, not the current task.


## Latest — v0.72.0 Heather: batch 2, portion 2 (Windows/physical pending)

Final clarified owner decision supersedes the earlier negative-stock answer: zero/insufficient stock blocks EVERY role, including owner and manager (D-93 remains unchanged). Both missing price and unavailable stock trigger ONE Counter-only popup per live bill. Clearing, posting, parking or loading another cart resets it; price is not repeated in the warning. If stock exists, an owner/manager may enter the existing custom rate even when catalog retail is missing; cashiers cannot. No receipt/chit warning or layout change; discounts unchanged. Core reads live stock, excludes expired stock, and combines repeated products and pack units. Quote/Create independently retain stock and below-cost checks. Seven new Core facts, **1234 written cases; 61 static checks**. No .NET SDK here: Windows build/xUnit and physical proof pending. Next on owner's "next": batch-2 portion 4, full Urdu tutorial. Installer only on explicit بنا دو. v0.70.0's 1227/1227 is prior evidence, not proof of this version.


> **WIP handoff, 12 Nov 2026:** Read [`INCOMPLETE-WORK-HANDOFF.md`](INCOMPLETE-WORK-HANDOFF.md) first on branch [`arena/b4b7bf7b-area11a`](https://github.com/Dr-Adil-Abdullah/AREA11a/tree/arena/b4b7bf7b-area11a). Owner batch 2 portion 2/4 - Counter-only popup in v0.72.0 Heather (1234 cases, Windows/physical pending; final answer retains D-93 for every role; receipts unchanged); portion 4 Urdu tutorial next; Purchase crash repaired in v0.71.0 Heron (Windows/physical pending); v0.70.0 Claret Fields warn-once save Windows GREEN 1227/1227; v0.69.0 Mallard de-workshop (Windows/physical pending); v0.68.0 Jasper wrong-clock batch fix (Windows/physical pending); v0.67.0 Charcoal true tabs + lockdown chrome + v0.66.0 Crimson kiosk 1206/1206 green + v0.65.0 Gold cleanup + v0.64.0 Pine installer + v0.63.0 Copper failures + v0.62.0 Moss money audit + v0.61.0 Taupe dead-code/passwords + v0.60.0 Basalt lockdown + v0.59.0 Aegean fixes (Windows/physical pending); v0.66.0 Windows 1206/1206 green, v0.58.0 was 1163/1163; all master portions 4-18 code-complete; physical verification pending. Older status and approval statements below are historical. This branch is not a tested release.


**Previous source: v0.71.0 Heron `#3F5E78` — owner batch 2, portion 3/4: the Purchase page opens again.** The product picker set both DisplayMemberPath and ItemTemplate (a pair WPF refuses), so Purchase died on every open; the path is removed (the photo template already shows the label) and the old pin now follows the template; all other views scanned clean. **1227 tests written (unchanged), 60/60 static; rebuild + reinstall + physical proof pending (v0.70.0 was 1227/1227 green).** All master portions 4-18 code-complete; production plan in `docs/PRODUCTION-PLAN.md` (phases 1-5 done, 6 gated on بنا دو).


**Current source: v0.50.0 Walnut `#4E3B2C` — master Portion 9 finished: suppliers + product filtering (#11).** Brand/company links with company put-away unlinking brands; one explicit preferred supplier per product, never guessed from history; supplier-to-products filter over preferred + genuine purchase history (inactive history suppliers included); editor preferred picker + Clear + history hint; Brands tab company picker + Company column. **1055 tests written, 57/57 static; Windows build/xUnit and Portion 9 physical verification pending.** Batch order (5, 6, 18) complete; Portion 17 paused; next is 10 per roadmap unless the owner says otherwise.


**Current source: v0.49.0 Mahogany `#5C2E1F` — master Portion 8 finished: managed salts + brands catalogs (#9, #11).** Central CRUD with one canonical spelling each; explicit product links never guessed from typed text (link fills empty text, mismatched link+text refused); renames relabel linked rows only; put-away unlinks but keeps typed words; Catalogs window in 4 tabs; product editor pick-combos + inline add + Unlink + hint; `brand.company_id` stays NULL for Portion 9. **1040 tests written, 57/57 static; Windows build/xUnit and Portion 8 physical verification pending.** Batch order (5, 6, 18) complete; Portion 17 paused; next is 9 per roadmap unless the owner says otherwise.


**Current source: v0.48.0 Russet `#80461B` — master Portion 7 finished: packaging, units and variants.** One shared box/strip/unit conversion on every quantity path; 'unit' on all seven quantity boxes (receipts/history/carts read 'unit', old 'base' rows still post); whole-only refusals on customer + no-bill returns; distinct 50ml/100ml variants with a same-name creation warning; stock-take finder shows strength + pack. **1025 tests written, 57/57 static; Windows build/xUnit and Portion 7 physical verification pending.** Batch order (5, 6, 18) complete; Portion 17 paused; next is the owner's call (8 per roadmap, or 17).


**Current source: v0.43.0 Umber `#5C3A21` — master Portion 4 code complete.** Owner decision: a photo is never required — required-photo definitions are refused, missing pictures never block answers or record creation, legacy required-photo rows stay untouched and block nothing. Own-fields form locks the Required tick for photos; bulk-workbook path reviewed safe (existing rows only, never writes answers). **974 tests written, 57/57 static; Windows build/xUnit and Portion 4 physical verification pending.** Prior 973/973 WIP evidence is separate. Portion 5 (safe drafts) is approved next; Portion 17 remains paused.


**Current source: v0.42.0 Kyanite `#3B526C` — master Portion 3 code written.** The product editor reloads complete tag IDs; primary category and filter-only tags are shown separately and explicit tag sets need a primary. Category-less products without tags and untouched legacy tag-only data remain; no guessed backfill. **929 tests written, 57/57 static; Windows build/xUnit and PT-14 physical verification pending.** Workbook category changes recheck tags; CSV import creates products without tags. Category put-away can leave other tags and remains for owner review. Baltic 921/921 is prior Core/Tests evidence. Portions 4–15 require approval.


**Current source: v0.41.0 Siderite `#594D32`.** Owner-requested Dashboard fallback and Portion 2 Product workspace are written; **922 tests written, 57/57 static checks**, but Windows build/xUnit and PT-13 physical verification remain pending. Prior Baltic Core/Tests **921/921 PASS** is separate evidence. Portions 3–15 remain to be approved.


**Latest owner evidence:** Baltic v0.40.0 Windows Core/Tests **921 passed / 0 failed / 0 skipped / 921**, 1m14s, ALL GREEN. This does not prove Desktop/WPF build or PT-12. Owner selected Dashboard as fallback without Counter permission; existing Baltic code needs adjustment. For Portion 2, owner selected both an Edit Product button and row double-click; coding approval remains separate.

**Current source: v0.40.0 Baltic `#33566B` — Portion 1 code written, not yet Windows/WPF verified.** Counter-first permitted startup, wordmark-only Dashboard navigation and header-hosted existing page sections are included; 921 tests are written, 57 static checks pass. Owner's separately reported 918/918 run has no confirmed version. Run Windows Desktop build and `ALL GREEN - 921 of 921`; PT-12 remains pending. Master-checklist portions 2–15 await approval; see `docs/MASTER-CHECKLIST-ROADMAP.md`.


**Latest verification and repair — v0.39.1 Admiralty `#274A68`:** the owner ran v0.39.0 Tourmaline Core/Tests on Windows and reported **916 passed / 2 failed / 0 skipped / 918**. The two test repairs are in source; **a Windows rerun (918/918), Desktop/WPF and PT-11 remain pending**. Static checks are not compilation. See `PHYSICAL-TESTING.md` for the failure details; Part 1 header/navigation requirements still need clarification.


A native **Windows desktop** rebuild of the Area11 Smart Pharmacy & Retail POS + Inventory system,
written in **C# / .NET 10 / WPF / SQLite**. It works with no internet, keeps the shop's whole
database in one file, and prints bills instantly on a 58 mm or 80 mm thermal printer.

Ported from the original Next.js + SQLite web app (`Dr-Adil-Abdullah/Area11`) using the
[master specification](docs/AREA11-MASTER-SPEC.md) as the blueprint.

> **Handing this repository to a new agent? Send it the link and [`TAKEOVER.md`](TAKEOVER.md).**
> That one file is the complete brief - where it stands, what is left, the non-negotiables, how a
> release is made, the traps, and the first message to send the owner. The long manual is
> [`HANDOFF.md`](HANDOFF.md), the working protocol [`AGENTS.md`](AGENTS.md), the owner's own
> decisions [`docs/OWNER-DECISIONS.md`](docs/OWNER-DECISIONS.md), and
> [`docs/HANDOFF-CHECKLIST.md`](docs/HANDOFF-CHECKLIST.md) is the receipt of what was actually
> checked before the handover. In Urdu for the owner:
> [`docs/HANDOVER-FOR-OWNER-URDU.md`](docs/HANDOVER-FOR-OWNER-URDU.md).

---

## Where it stands

> **Want to see what is done, as one page you can print?** Open
> [`docs/CHECKLIST-URDU.html`](docs/CHECKLIST-URDU.html) (or double-click `PRINT-CHECKLIST.cmd`).
> It has one line per feature with a mark - finished / one action left / not started - and an empty
> box beside each line for the owner's pen. It is generated from the board below, so it cannot
> drift: `python3 tools/make_checklist.py`.
>
> **Physical testing اور manual instructions کا مالک tracker:** [printable PDF](docs/AREA11-TESTING-TRACKER-URDU.pdf) ·
> [RTL HTML](docs/AREA11-TESTING-TRACKER-URDU.html) · [editable Markdown](docs/AREA11-TESTING-TRACKER-URDU.md)۔
> اس میں PT-01 تا PT-10 کے passed/pending checks اور نتائج بھیجنے کے سانچے ہیں؛ running log `PHYSICAL-TESTING.md` ہی رہے گا۔

| Phase | Contents | State |
|---|---|---|
| **1** | Foundation, database, sign-in, products, purchases `PINV`, counter `INV`, receipt & instant print, settings screen, **hold cart, split payment** | ✅ written and compiled; needs one real walk-through by the owner |
| **2** | Returns `RET` / provisional `PR` / quarantine `Q`, alert centre, write-off, stock-take `ST`, khata and supplier ledgers | ✅ written (v0.3.0 - v0.3.12) |
| **3** | Shift & galla, expenses, udhaar recovery, reports, black box, **backup ZIP**, **Excel / CSV import+export**, **reorder + WhatsApp order**, **stock-minus registry** | ✅ written (v0.4.0 - v0.11.0) |
| **4** | **Loyalty** ✅ (v0.12.0), **photos & camera** ✅ (v0.13.0), **own fields** ✅ (v0.14.0), **several barcodes** ✅ (v0.15.0), **doctors, reps & lifetime profit** ✅ (v0.16.0), **second counter over the LAN** ✅ (v0.17.0), **tax, receipts & monthly statements** ✅ (v0.18.0), **self-installing installer** ✅ (v0.19.0), **printer & scanner test** 🟡 (v0.20.0 - waits for the shop's own printer), **Supabase cloud sync** ✅ (v0.21.0, repaired v0.26.0), **bills page with reprint** ✅ (v0.22.0), **printer fixed (code 6) and printers listed** ✅ (v0.22.1), **sample shop for a new database** ✅ (v0.23.0), **misspelt-medicine search** ✅ (v0.24.0), **purchase / partner / numbering tests** ✅ (v0.25.0), **cloud push repaired + Settings → Cloud sync** ✅ (v0.26.0, build fix v0.26.1), **find a bill without its number** ✅ (v0.27.0), **two and a half months of demo shop** ✅ (v0.28.0, **deleted in v0.30.2 - D-77**), **do the books add up? - eleven read-only rules over the shop's own rows** ✅ (v0.30.4), **the drawer changes hands with a signed slip** ✅ (v0.30.6), **who was short - the drawer per person, short and over never netted** ✅ (v0.31.0), **a home screen that matches the pages it summarises** ✅ (v0.29.0), **custom application labels** 🟡 (v0.35.11 - written, awaiting Windows/live proof), fuzzy search | 🟡 started (v0.12.0) |

The cloud mirror is set up from [`docs/CLOUD-SYNC.md`](docs/CLOUD-SYNC.md) (Urdu): one SQL block in Supabase,
four settings in Area11, then push / look / bring.

Live counts are in [`docs/PROGRESS.md`](docs/PROGRESS.md) (features) and
[`docs/RULES-MATRIX.md`](docs/RULES-MATRIX.md) (the owner's 116 numbered rules).

**Current source — v0.39.0 Tourmaline `#7A1F5C`:** the owner-approved master checklist **Phase 1**
adds migration 030 and typed contracts for native fields/acknowledged overrides, local-only drafts,
managed generic formulas/brands, preferred-plus-historical suppliers, opening/closing drawer
discrepancies, later-phase print/profit/sync settings, cloud format 3 parent ordering, and a guarded
isolated-test rebuild with an automatic technical snapshot. Existing generic/brand text, quantities,
money, documents and D-93/PR rules stay intact. **57/57 static checks pass with real C# grammar/member
resolution, 918 cases are written, and 17/17 raw SQLite rehearsal groups pass.** Tourmaline has not
been compiled or run here. No bulk-print/WhatsApp/profit UI or automatic-sync scheduler is claimed;
master Phases 2–7 require separate approval. Cobalt 900/900 remains previous-release evidence.

**Honest note:** the agent's build machine is Linux with no .NET SDK, so nothing in `dotnet/` is
compiled there. Every change is checked statically (C# parses, explicit usings, XAML, resources,
every binding against its view model, every SQL statement against the real schema, every mapped
column against its model, migrations executed on SQLite). The owner's Windows machine compiled and ran the previous
**v0.38.0 Cobalt** Core/Tests suite, verifying **900/900 xUnit cases in 1 m 4 s** on 29 October 2026.
That log does not prove Tourmaline, `Area11.Desktop`/WPF, or a launcher run. Require
**`ALL GREEN - 918 of 918`** on Windows, then complete PT-11's isolated schema/upgrade/rebuild checks;
genuine transactions, WPF, real Excel, printer and optional real-cloud proof remain separate. See
[`docs/VERSIONS.md`](docs/VERSIONS.md) for release history and
[`PHYSICAL-TESTING.md`](PHYSICAL-TESTING.md) for the running evidence log.

---

## Build and run (Windows 10/11)

**Two easy files in the main AREA11a folder:** double-click `START-AREA11.bat` to start the app, or `RUN-TESTS.bat` to run tests and copy the result. Tests do not run automatically when you open the app. The root test file calls the same `tools\RUN-TESTS.cmd`, which still saves `test.log` in the main folder.

After you start once, Windows normally creates two optional shortcuts in that same folder: `! START AREA11` and `! RUN TESTS`, both with the Area11 logo. They sort near the top by name. In File Explorer choose **View → Large icons** or **Extra large icons** to make them easier to see; icon size is a Windows folder-view choice. If Windows cannot create them, the `.bat` files still work. Shortcuts are specific to this folder's location: if you move the folder, delete those two shortcuts and start `START-AREA11.bat` once to recreate them. They are not uploaded to Git.

**Easiest (while the code is being worked on):** double-click **`START-AREA11.bat`** — it checks the
folder, updates, builds and starts the app. [Urdu steps](docs/HOW-TO-RUN-URDU.md).

**On the shop PC:** use the installer. On the build PC run **`tools\MAKE-INSTALLER.cmd`** once, copy
`dist\Area11-setup` (or the zip) to the shop PC and double-click **`INSTALL-AREA11.cmd`** there. It
installs to `C:\Program Files\Area11`, puts the icon on the desktop and in the Start menu, lists
itself in *Add or remove programs*, and **never touches the shop data in `C:\ProgramData\Area11`**.
[How it works, step by step](docs/INSTALL.md).

> **`Area11.sln` missing in your folder?** That copy is old or incomplete (the download was
> interrupted, or an older extract was merged into it). Take a fresh copy instead of repairing it:
> [download the ZIP](https://github.com/Dr-Adil-Abdullah/AREA11a/archive/refs/heads/main.zip),
> extract to `D:\`, then run `START-AREA11.bat` inside the new folder. Shop data lives in
> `C:\ProgramData\Area11` and is never touched.

Or manually:

```bat
git clone https://github.com/Dr-Adil-Abdullah/AREA11a.git
cd AREA11a
rem  the product is on the default branch "main" - nothing to switch to
dotnet build
dotnet test
dotnet run --project dotnet\Area11.Desktop
```

**If something fails:** `START-AREA11.bat` keeps `build.log` and shows **only the error lines**;
`tools\RUN-TESTS.cmd` keeps `test.log` and shows the result, the failed test names and the compile
errors. When you need help, double-click **`tools\SEND-REPORT.cmd`** — it writes **one text file**
(build, .NET version, branch, project files, shop folder, the whole newest crash report, the last
build and test output) and it only reads. [Urdu](docs/HOW-TO-RUN-URDU.md) · v0.30.3, D-78.

Sign in as **Owner** with password **`area11`**, then change it. Data lives in
`%ProgramData%\Area11\` (`area11.db`, `photos\`, `logs\`, `backups\`) and is never committed.
Step-by-step help, including what to do when the build fails: [`docs/BUILD-ON-WINDOWS.md`](docs/BUILD-ON-WINDOWS.md).

> The blue "app is running" window some earlier builds show is `prototypes/WPFSample`, the very first
> smoke test — it is **not** Area11. The real app is `dotnet/Area11.Desktop` (assembly name `Area11`).

---

## Repository map

| Path | What it is |
|---|---|
| `Area11.sln` · `dotnet/` | **The product**: the solution is in the root; `Area11.Core` (rules, SQLite) · `Area11.Desktop` (WPF) · `Area11.Tests` (xUnit) |
| `docs/` | specification, owner decisions, progress board, rules matrix, schema reference, build guide, port plan |
| `tools/` | `check_repo.py` (static checks) · `build_rules_matrix.py` (keeps the rules matrix in sync with the spec) · `ci/build.yml` |
| `TAKEOVER.md` | the handover brief for a new agent - read this first |
| `HANDOFF.md` · `AGENTS.md` | the takeover manual and the working protocol |
| `prototypes/` | earlier prototypes (`Area11.WPF`, `WPFSample`), reference only — they are not the app |

## Documentation

| File | Language | Contents |
|---|---|---|
| [`HANDOFF.md`](HANDOFF.md) | English | orientation, next tasks, non-negotiables, traps, decisions in English |
| [`AGENTS.md`](AGENTS.md) | English | read order, hard rules, verification, definition of done |
| [`docs/PROGRESS.md`](docs/PROGRESS.md) | English | every feature: ID, state, files, test, "done when" |
| [`docs/RULES-MATRIX.md`](docs/RULES-MATRIX.md) | English | 116 numbered owner rules → where each is implemented |
| [`docs/AREA11-MASTER-SPEC.md`](docs/AREA11-MASTER-SPEC.md) | English | the blueprint (sections 3, 5, 11, 12, 14, 15, 34) |
| [`docs/OWNER-DECISIONS.md`](docs/OWNER-DECISIONS.md) | **Urdu** | the owner's decisions D-01…D-93, phase plan, open questions |
| [`docs/SCHEMA-REFERENCE.md`](docs/SCHEMA-REFERENCE.md) | English | column-by-column reference of every table |
| [`docs/PORT-PLAN.md`](docs/PORT-PLAN.md) | English | the technical study: conflicts, module map, test map, port order |
| [`docs/BUILD-ON-WINDOWS.md`](docs/BUILD-ON-WINDOWS.md) | English | build, run, test, and what to send back when something fails |
| [`docs/GIT-AND-VS-URDU.md`](docs/GIT-AND-VS-URDU.md) | **Urdu** | bring the code from GitHub, open it in Visual Studio / VS Code, change the colours |

## Database

`dotnet/Area11.Core/Data/Migrations/` holds the **original 13 migrations verbatim** plus the
append-only Area11 migrations `014`-`030` (permissions, photos, stock detail, barcodes, provisional
money, reconciliation, catalog ordering, managed units, filter-only category tags, descriptive strength,
rename-safe quantity rules, custom-field governance and master-foundation contracts). They create
**42 application tables** plus the `_migrations` ledger and two views, and run inside `BEGIN IMMEDIATE`.
Migration 030 preserves existing generic/brand text and adds nullable managed links without a guessed
backfill; quantities, sales, payments and historical documents are not rewritten. D-93 never rewrites
a historical negative quantity to zero. Local drafts do not create financial rows or travel to cloud sync.

## The rules that shape the code

- Money is **integer paisa**; quantities are `double` (the schema stores REAL). Never `decimal`.
- A bill's total always equals the sum of its line totals.
- Rounding is **downward only**, and never below purchase cost.
- **D-93:** no new sale, write-off, goods drawing, supplier return or PR cancellation may take a batch or product below zero. The legacy checkbox is not a bypass. Historical negatives stay readable and are reconciled by physical count plus audited correction, never silent zeroing.
- Discounts are capped per role and can never sell below cost.
- Permissions are enforced in the business layer, not by hiding buttons.
- Nothing is hard-coded: shop name, wording, limits, printer width all come from `settings`.
- Every action is written to an audit trail with old → new values; nothing is ever deleted.

## Checking your work

```bash
python3 tools/check_repo.py             # C#, usings, XAML, bindings, migrations, SQL, tests, docs links
python3 tools/build_rules_matrix.py --check
```

Both must be clean before a commit; `dotnet build` + `dotnet test` are the Windows gates.
