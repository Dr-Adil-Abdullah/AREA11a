# Actual Windows v0.80.0 test report — bounded source response v0.85.1 Stonechat

Date recorded: 2026-11-10. This interrupts the ongoing Phase4/D-110 rollout to investigate the owner's real test failure. No new business policy or installer was requested.

## Evidence supplied by owner

### Complete log received — all four failure identities confirmed

The owner has now supplied the complete failure output: **4 failed/1333 passed/0 skipped/1337 total,1m46s**, net10.0, consistent with their v0.80.0 banner. Core and Tests compiled in that run; Desktop/WPF is not included in that evidence.

| Confirmed failing test | Actual assertion in owner log | Existing v0.85.1 source response |
|---|---|---|
| CounterAvailabilityTests.CashierCannotSupplyMissingRateEvenThroughCore | Expected False,Actual True,line87 | genuine cashier row rather than owner ID1; inactive/mismatched actor pricing read refuses; ungranted real cashier still denied |
| BookCheckTests.ABillWhoseTotalIsNotItsLinesIsNamedWithTheGap | Expected3,Actual4,line157 | new snapshot identity explicitly asserted; four findings but one document/Rs5 gap, not four monetary faults |
| BookCheckTests.MissingAndTooMuchAreCountedApart | Expected500,Actual0,line425 | snapshot finding uses final vs original-minus-Extra, no whole-bill amount overriding correct Rs5/Rs3 grouping; monetary expectations unchanged |
| BookCheckTests.TheSavedFileHasTheDocumentAndBothNumbers | Expected4,Actual5,line368 | CSV expects header+four findings; explicit snapshot row and original numeric bill gap assertions retained |

**No failure remains unidentified from this supplied run.** All four match the source defects/expectations already addressed; no further application code change or blanket test relaxation is justified by this log. This is NOT a green rerun of v0.85.1. No further old log request is needed. Earlier excerpts below are chronological and their then-unknown status is superseded by this complete log.

### Follow-up excerpt — same v0.80.0, duration1m46s

A subsequent owner-supplied tail again shows **4 failed/1333 passed/0 skipped/1337 total**, still **v0.80.0 Ibis**, not a v0.85.1 rerun. It now explicitly confirms CSV **Expected4/Actual5** at line368 and names `BookCheckTests.MissingAndTooMuchAreCountedApart` at line425; that test's assertion message is cut off. This is consistent with the finding-count and whole-bill-gap source defects already addressed, but is not runtime verification of the fixes.

Across the two excerpts **three distinct failing test names** are now observed (the above two BookCheck tests and the earlier cashier test). The fourth name remains unknown; do not assume it is the predicted BookCheck count test. Preserve the complete current `test.log` and request its contents, not another final15-line console tail. Newer workspace changes remain unpushed; do not blame the owner for not pulling changes that are not yet on the remote. No code changes or new test execution were made in response to this follow-up excerpt.

### First excerpt — duration1m48s

The console identifies **v0.80.0 Ibis**, expected1337 cases. Actual Windows/net10.0 run: **4 failed,1333 passed,0 skipped,total1337**, duration1m48s. The count matches that build; this is NOT a short-count/stale-DLL diagnosis for that run. It also does NOT execute the newer workspace code.

Only these failures are visible in the supplied tail:

1. `BookCheckTests.TheSavedFileHasTheDocumentAndBothNumbers`: stack trace at line368; assertion message above it omitted. Before this fix, the source at that line expected four CSV rows.
2. `CounterAvailabilityTests.CashierCannotSupplyMissingRateEvenThroughCore`: line87, Assert.False expected false but got true for the quote.

The other **two failing names/messages are omitted**. Source inspection suggests the existing BookCheck aggregate/count tests can fail for related reasons, but those two identities are **unconfirmed**, not inferred Windows results. Request the original complete `test.log` before treating all four as accounted for. Preserve the original log before another run overwrites it.

## Source findings and changes

### Counter fixture / pricing account boundary

The old counter tests relabelled stored owner ID1 as cashier/manager rather than creating real staff records. PricingAllowanceService correctly loaded stored per-ID allowances, but did not reject an actor snapshot whose role differed from the stored account. The fake cashier could therefore receive owner-default pricing allowances.

- Counter tests now use `TestData.Cashier` / `AddUser` real rows. The permitted manager case receives an explicit owner-managed EditRate grant, not automatic manager pricing rights.
- The ungranted cashier quote/save must refuse rate entry. The assertion now matches the existing per-person pricing error rather than retired role-only wording.
- Zero-stock tests give each real role an explicit rate grant first, so refusal exercises stock rather than accidentally passing on a prior pricing denial.
- PricingAllowanceService.Read rejects inactive caller snapshots and current-vs-supplied role-kind mismatch before loading grants/defaults. Existing forged-owner test now expects that refusal.
- Two new facts cover owner-ID relabelling across three staff roles with no sale/payment/stock changes, and inactive owner snapshot refusal.

This is a bounded current-account consistency check, **not arbitrary-ID/session authentication**. General caller/session review remains OPEN. No new manager/cashier pricing policy was introduced; existing explicit per-person rate/cap rules and actual allocated batch-cost floor remain.

### Books check: finding count AND actual money bug

New sales retain `pre_extra_total_paisa` and sale-level Extra. Deliberately increasing a new bill total by Rs5 causes **four** independent findings: lines, header, paid+due and original/Extra/final snapshot. The old test expected three findings/four CSV rows. Tests now assert four findings/five CSV rows and explicitly require the snapshot finding; the detector was not disabled to satisfy old expectations.

A separate real reporting error was found: the snapshot check returned Expected0/Found full bill total for any snapshot mismatch. `WorstPerDocument` then selected that larger whole-bill difference and could report Rs305 excess instead of the real Rs5 missing.

- Snapshot mismatch findings now compare current final total (Expected) with original-minus-Extra (Found), matching other bill-gap direction. Rs305 vs Rs300 gives Rs5 missing.
- Legacy NULL snapshot skips this identity as before; an otherwise identical legacy corruption still has three findings.
- Existing negative-money/invalid-discount checks remain. No sale/purchase/return posting, quantity, ledger balance, schema or financial history is rewritten by BookCheck; it remains read-only.
- Two new facts cover a snapshot-only Rs1 mismatch and legacy NULL behavior. Existing missing/excess tests retain their exact Rs5/Rs3 expectations.

### Test runner visibility

The runner used `findstr /b ... "Failed "`, which misses VSTest lines indented with spaces, including the owner's excerpt. It now accepts leading spaces and prints all matching test names instead of silently relying on the final15 lines. Full `test.log` remains the evidence for error details. The Windows batch command itself has not been executed here.

## Executed here vs NOT executed

- **74 static checks PASS**;379 real C# grammar files;3931 calls/7334 member references resolved,0 reported problems. Not compilation/xUnit.
- New `python3 tools/check_bookcheck_snapshot_sqlite.py`: all36 actual migrations, source-extracted five bill-check SQL queries on synthetic in-memory data. New-vs-legacy findings, Rs5/Rs3 separate summaries, Rs1 snapshot-only mismatch, valid Extra with unchanged items and FK checks PASS. The grouping calculation is an independent Python model, NOT the C# BookCheckReport/CSV implementation.
- Original whole-bill SQL mutation rejected by both actual-SQL regression and static guard; role-mismatch gate removal rejected by static guard. Originals restored.
- Regex model matches the supplied indented test line and excludes summary; not Windows findstr execution.
- Existing Extra/time synthetic SQLite scripts PASS, not C# services/runtime.
- **Four new facts;1449 total written C# cases, all UNEXECUTED here.** No dotnet SDK/Windows in this environment. Owner's1333 passing results belong only to their reported v0.80.0 run.
- `git diff --check` clean. No installer, live-shop fake posts, commit or push. Work remains on the existing Arena branch; newer workspace files are not claimed to be available through a fresh remote pull.

## Next action

The complete old log has been received; do NOT ask the owner for it again. All four reported failures have mapped source responses. Once updated source is actually transferred, Windows must run the new **v0.85.1/1449-case** suite. Newer source remains unpushed at this checkpoint, so Pull alone is not claimed to deliver it. Do not call the fixes runtime-verified until the updated result exists. No reinstall/reset/delete of shop data is indicated by this test report.

D-110 separate financial checkboxes/main switch are unchanged. Global invoice/ledger/batch/stock/expense/export rollout and comprehensive Phase4 remain OPEN: [remaining trace](POS-PHASE4-FINANCIAL-VISIBILITY-REVIEW.md).
