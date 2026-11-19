# ⏳ Phase4 portion40 — current PR actor,reads,receipt and page verdict

**⏳ v0.118.0 Sedge · 2026-11-10 · stable5–7,17,21,23 · PT-60 · current runtime/fullPhase4 OPEN.**

## ⏳ Bounded contracts

| Point | Source change | Explicit limit |
|---|---|---|
|5/6/17 ⏳ PR writers | Create resolves current stored Returns reader inside observed Tx;Link/Cancel resolve current Returns Owner/Manager before target;stored user reaches existing audit/financial helper | Supplied ID is not authenticated session;parsed-role equality,not same-role-ID proof/CAS |
|5/6/17 ⏳ PR reads | Page/Get/Lines/Search/current complete receipt all require current actor;actorless optional compatibility calls fail closed | Legacy List/null wrappers cannot distinguish denial from empty;UI does not use them for page/receipt decisions |
|17 ⏳ honest page | Explicit Page ServiceResult;failure never successful empty;UI clears rows/selection/decisions and suppresses empty hint until successful load | Page data failure does not erase the independent unsaved tray if current access still valid;decisions/reprint require loaded rows |
|21 ⏳ receipt | Build requires actor;ReadReceipt authorizes header+lines,validates nonempty/full count;no header-only success;Compose unchanged | Already prepared document/printed paper/export not recalled;read unit is local gate,not an external multi-query snapshot |
|7 ⏳ UI | Action/nominal3s Loaded timer checks current access;denial clears lists/selection/lookup/product/tray/refund/name/phone/note;stable grant preserves typing;regrant explicit refresh | Not instant revocation;busy synchronous operation defers timer;Core writers/readers recheck. Actual WPF lifecycle/notifications UNRUN |
|17/25 ⏳ stale action | Decisions require fresh capability and successful list;detached PR/lookup refused;P39 successful write+failedreload stays SAVED/blocked | Local unknown/committed guard retained across access changes,not durable global identity/restart protection |

Existing Returns readers retain PR history/detail/search/receipt access;only current Returns Owner/Manager attaches/cancels. No new Sales/Cost/Profit dependency. Core search remains a Returns-reader path,while the existing UI decision lookup stays Owner/Manager. No financial/refund policy was reopened.

## ✅ Preservation

- ✅ PR financial SQL/private Save,reconciliation and refund-allocation helpers retained;Link/Cancel inner financial work only gains stored-actor resolution before target,not a different stock/payment/terminal policy.
- ✅ Receipt Compose tail unchanged;Sale/Purchase/Return/StockPolicy/Database/TransactionOutcome/P38 local guard untouched;no migrations/historical rewrite/backup-cloud/Blackbox-BookCheck internals changed.
- ✅ Two old PR test files only add explicit existing Owner arguments to protected reads/receipt/search;money setup and assertions remain intact. P39 lexical guard updated to the observed current-writer wrapper,not removed;its29negative controls rerun.

## ⏳ Regression sources —99 new cases /2888 expected suite,ALL C# UNRUN

| Group | Cases | Intended coverage |
|---|---:|---|
|5/6 ⏳ denied reads |49| seven routes×null/missing/inactive supplied/inactive stored/rolechanged/revoked/inactive Owner;unchanged whole snapshot |
|5/23 ⏳ denied writes |21| three routes×same seven actor faults;no mutation and explicit no-commit |
|17/21 ⏳ valid reads |21| seven routes×Owner/Manager/Cashier Returns readers;no read mutation |
|23 ⏳ audit actor |3| legitimate manager writes audit with stored current name,not forged display name |
|17 ⏳ read vs decide |2| ordinary Returns reader cannot Link/Cancel |
|17/21 ⏳ verdict/receipt/compatibility |3| revoked/read-failed Page not empty,regrant fresh read,line failure cannot produce header-only receipt,actorless/missing targets fail closed |

TestDatabase migrates and seeds real synthetic fixture;actual Sale and PR setup. No ViewModel/Dispatcher/WPF is instantiated by these cases. All overload/schema/fixture/trigger assumptions still require actual .NET execution.

## ✅ Local executed evidence / ⏳ boundaries

- ✅ New `check_provisional_current_sqlite.py`:37 migrations,35 Python current-account/grant denials with unchanged dumps/regrant;three valid readers;actual actor/grant and PR header/page/lines/search SQL,stored names,bound IDs,line-read error PASS. Python owns authority/routing;not execution of C# permissions/read unit/receipt Compose.
- ✅ New `check_provisional_current_source_negative.py`:27 lexical mutations rejected;exact bytes restored. Text/order contract checks are not C# or WPF semantics.
- ✅ Full current static run:**107 checks**,**431 C# grammar parses**,**4783 calls/9828 member references**,zero reported static issues. This is not Roslyn compilation.
- ✅ All **14 source-negative scripts PASS,405 mutations** rejected and exact bytes restored;all **31 SQLite scripts PASS**. New and historical probes are included in these totals,not additional xUnit execution.
- ✅ Independent Python encrypted-package reference:6 chunk-boundary round trips/24 negatives PASS;rules matrix current;`git diff --check` PASS. No C# codec/DPAPI/actual restore execution.
- ✅ Preservation against P39 `5c3bcd62c716b3a4b2fe74169e95ff58bc879f9e`:794 previous tracked paths retained,156 other existing Core C# files exact,138 other old test paths exact;two old PR test files mechanically verified as explicit Owner read-argument-only changes;37 migrations exact.
- ✅ PR executable SQL literal sequence and private financial helper sections exact;Link/Cancel financial Tx callback bodies exact after excluding only new current-actor resolution. Receipt Compose tail exact.
- ✅ Guide MD/HTML main exact,36 lesson anchors and stable1–36 retained,all57 source-board rows exact;checklist0 fully certified/57 ongoing/0 never started. Launcher/test/report checkout CRLF retained. New C# snapshot identifiers exist in migrated SQLite;this does not execute the fixture.
- ⏳ .NET SDK/Windows unavailable here:2888 C# cases ALL UNRUN. No TRX,test binary,actual WPF screenshots or installer is claimed.

## ⏳ Remaining gates

- ⏳2888currentC#compile/tests/build/TRX/fix/retest,realWPFtimer/denial/regrant/binding/selection and manual/auto print checks. No current Windows screenshot/runtime PASS or installer claimed.
- ⏳Authenticated session/same-role-ID,CAS,remaining caller/output review;multi-query coherent snapshots across external connections and already-prepared-output recall are not supplied by this change.
- ⏳Actual provider COMMIT/rollback errors,parallel/restart/durable identity;remaining recovery/file faults,backup/restore/newPC/cloud,hardware/isolation/install proof.
- ❌Installer binary NOT BUILT;⏳FullPhase4 OPEN. No synthetic live shop posting.36categories≠36Nexts;3–6rounds conditional,not a cap/deadline.

## ⏳ Delivery record

- ⏳ Publication evidence is the final response:commit/push only on `arena/b4b7bf7b-area11a`,then independent actual `git ls-remote` exact hash match. This source document cannot contain its own final commit hash or certify publication itself.
