# Phase4 portion11 — stock/frozen stock-take valuation outputs, v0.89.0 Redstart

**Date:**2026-11-10. **Status:** bounded D-110 continuation; global financial rollout, authenticated sessions, finance fault matrix and Windows/fullPhase4 remain OPEN. No installer built. Supplier paid/payable/Pay/SR policy unchanged and unanswered.

## Entry → VM → Core → data / contract

| Surface | Trace / new explicit actor | Gate / projection |
|---|---|---|
| Write-off history | WriteOffView → WriteOffViewModel.Load → Stock.History(user) | current active same-role account + Stock module; independent CostPolicy/main; raw row.ValuePaisa0/CostKnownfalse and hidden ValueLabel when denied; qty/reason/date/batch/name retained |
| Today loss | Load → Stock.LossTodayPaisa(user) | same reader/page; nullable long: denied/error=null, authorized real0=0. Cost denial returns before aggregate query; UI shows hidden, not Rs0 |
| Write-off success | Save → Stock.WriteOff → unchanged private Save inside Tx → projected result | current reader and cost policy resolved inside transaction BEFORE write; real valuation stored/audited, only returned LossPaisa0/CostKnownfalse/LossLabelhidden. No new cost prerequisite on otherwise permitted stock posting |
| Current/history | StockTakeView → StockTakeViewModel.Load/ReloadSheet → Current(user)/History(user) | current reader + StockTake module; Short/Extra raw amounts0/CostKnownfalse, hidden labels, operational status/progress/counts retained |
| Frozen sheet | ReloadSheet → Sheet(user,id,differencesOnly) → private ReadSheet → ProjectLines | raw nullable ValuePaisa=null when hidden, CostKnownfalse; true uncounted null still em-dash when authorized; quantity-based differences filter unchanged |
| Found products | SearchFound → NotOnSheet(user,id,term) | current StockTake reader; hidden raw cost0/CostKnownfalse; pack/product metadata retained; no explicit margin emitted. AddFound uses IDs and internal frozen SQL, never masked UI cost |
| New print document | PrintSheet → BuildSheetDocument(id,user,filter) | gate BEFORE LoadSession; same-connection throwing ReadSheet; line projections and hidden signed net Value footer when denied, book/count/difference counts remain. Query failure fails preparation, not successful empty document; unexpected DB details no longer returned |
| Open/Apply result | Open/Apply public transaction wrapper → unchanged OpenInside/ApplyInside → projected result | current active same-role/module checked before mutation; Apply still owner-only. Returned session/result Short+Extra masked independently; real frozen/posted figures untouched |

No public actorless fallback for these named reads. Existing legacy list/null failure shapes remain (not newly detailed ServiceResults); denial and genuine empty are not distinguished in those APIs. New document preparation returns an explicit failure. It does not call fail-empty public Sheet. UI refresh no longer falls back to a previously successful session; latest history replaces old history and clears lines/found state when session is unavailable. This is next-refresh behavior, not continuous revocation of every open object/status message.

## Frozen cost must not become hidden0 accounting

Synthetic frozen book10 at12345paisa, counted7: difference-3, shortage37035paisa. Later changing live cost to99999 must not change that frozen shortage. Public line ValuePaisa may be null and public session/result totals0 when hidden; **private LoadSession/ReadSheet/ApplyRow and stored system_cost_paisa remain raw**. Count/Apply never consume masked DTO values. Internal OpenInside, AddFound/Count/ClearCount, ApplyInside, Cancel and RefreshTotals/LoadSession/helpers are exact from portion start. Stock private Save/helper tail exact. Only public result projection and current-reader checks wrap the three named writes; no partial transaction is reclassified as success and no post-commit policy query is added.

Main OFF or owner explicit Cost denial hides output, does NOT turn off legitimate counting/apply or replace frozen costs with0. Otherwise valid Stock/StockTake workflows can operate without seeing money; this is independent visibility, not a new blanket financial action restriction. Supplied owner identity is still not an authenticated session token. Other stock-take mutation authorization/session behavior remains subject to the wider audit.

## Exports / wording correction

There is **no separate StockTakeService/StockTakeViewModel CSV builder** in this source. This portion migrates typed read DTOs and the existing new ReceiptDocument print path; it does not invent or claim completion of a CSV route. Generic workbook/CSV/table exports, audit values, cloud/backup and arbitrary user-entered notes may still disclose stored money. Existing PDFs/prints/documents/status messages are not recalled. No all-route or mathematical secrecy claim.

## Evidence actually run

- **78 static checks PASS**;389 real C# grammar files;4066 calls/7764 references resolved,0 reported issues. Not compiler/xUnit/WPF/security certification.
- **12 negative source mutations rejected/restored:** stock raw row, today hidden-null distinction, write-off result, Open result, current session, session totals, Apply result, frozen line, document net, found raw cost, public fail-empty Sheet in document and stale session fallback. Guard clean after restoration; no Windows execution.
- StockService **9**, StockTakeService **18** verbatim SQL blocks unchanged (multiset; sheet read moved to private helper). No schema/financial SQL/formula/history rewrite. **109** other Core service/data hashes unchanged; all **652** baseline files retained.
- New `tools/check_stock_valuation_sqlite.py`:36 actual migrations and source-extracted freeze/count/totals/sheet/Apply-read/history/today SQL on synthetic in-memory SQLite. Frozen12345 vs later live99999, shortage37035/excess400/net-36635, uncountedNULL/boundID, actual stock24690, unchanged read dumps/FK PASS. Python supplies count arithmetic for executing SQL; this is NOT C# Count/Apply/WriteOff/permission/projection execution.
- Six prior isolated SQL probes PASS. Main guide MD/HTML exact body parity/36lessons, local links, generated rules/checklist and diff whitespace checked.
- **30 new facts /1551 total written C# cases UNRUN**. Existing stock/stock-take/packaging/negative-stock tests now pass explicit owner context without relaxing expected money/frozen values. New tests cover actor/page/cost/main/owner/refusal, nullable genuine0, hidden WriteOff result with real stored cost/stock, session/sheet/filter/uncounted, print net, failure-not-empty, found cost, permission DB failure, masked Open/Apply preserving frozen values, live-cost change, once-only Apply, read-only data and write rollback fault.
- No .NET/Windows SDK execution here. Prior actual v0.80 four failures identified/source-fixed; no new green runtime result. No installer/live-shop synthetic posts. Current-branch docs+commit/push required by D-112; actual Git result is the publication proof.

## Remaining / 15-dimension checkpoint

Money/order/actual-cost/returns formulas unchanged; focused projections only. Rollback/concurrency/duplicate/fresh sessions and all delegated mutation/record-ID behavior remain open despite new in-transaction current reader on three wrappers. Expense history/drawing results/vouchers, purchase entry/drafts/results, ordinary supplier ledger/PaySR/warnings, StockMinus routes, audit/workbook/other exports and files/backup/cloud remain to review. UI/native keys/scanner/printer/install require real Windows. Complete26route/36lesson action audit not certified. No obsolete removal.

**Next coherent priority:** Expense/drawing monetary results/history/vouchers and purchase form/draft/result boundaries, plus generic export/audit propagation as size permits. Do not silently settle the ordinary supplier paid+payable policy.

[Safe PT-31](POS-PHASE4-PART11-URDU.md) — [Remaining plan](REMAINING-WORK-AND-TEST-INSTALLER-URDU.md).
