# Phase4 portion13 — purchase preparation/drafts/results, v0.91.0 Kingfisher

**2026-11-10. Bounded D-110 continuation, not global financial or Phase4 completion.** Ordinary supplier paid/payable/Pay/SR policy remains unanswered. No installer or Windows runtime result.

## Chosen boundary: refuse cost-bearing preparation, not editable zero

Three tablets at12345paisa cost37035paisa. A user lacking current Purchases + independent View Purchase Cost + main ON must not get an editable draft whose real12345 is replaced by0: that can become a genuine free delivery. Therefore Create, purchase draft open/save/update/post fail without the preparation gate; no raw payload or successful masked-zero result is returned. Profit-only/manager rank is insufficient; explicit owner Cost denial/main OFF applies too. Even genuine zero-cost/sample preparation needs the same gate.

This does NOT invent a new ordinary Supplier payment restriction. Existing saved purchase invoice remains the portion9 hidden-aware document (including Total/Paid/Due). Module-authorized draft summaries and discard remain available without Cost; summary DTO contains metadata/count/partner, not payload or money. Draft payload stays local-only, unencrypted compatibility data in the existing DB. Audit/backup/raw workbook access is not secured by this bounded change.

## Trace and ordering

| Surface | Path | New boundary |
|---|---|---|
| Form availability | PurchasesView loaded/nominal3s DispatcherTimer → VM.RefreshEntryAccess → Purchases.CheckEntryAccess | current stored active same-role reader + Purchases, then ProductReadAccess.RequirePreparationCost(Purchase), which applies Cost/main. Check returns only ServiceResult, no authorization token or monetary payload |
| Direct receive | VM.Save → PurchaseService.Create → existing transaction → RequireEntryReader → unchanged Save | fresh gate inside transaction before stock/money/audit; successful result CostKnowntrue, TotalLabel; failed/default unknown result label hidden |
| Draft insert/update | VM.SavePurchaseDraft → DraftService.SavePurchaseDraft → generic Save | early check plus separate checks inside both insert/update transactions before reading/writing draft payload; no masked editable return |
| Open draft | VM.OpenPurchaseDraft → DraftService.OpenPurchase → generic Open | current preparation gate before ReadRow/deserialization; no successful empty/zero fallback |
| Post saved/current payload | DraftService.PostPurchase → early gate → optional OpenPurchase → generic Post authorization callback → original terminal premark → Create → finalize | caller-supplied replacement input cannot bypass cost gate; callback before premark, Create independently rechecks before receiving. Existing restore/finalize/crash model retained, not converted into one atomic transaction |
| Summary/discard | DraftService.List/Discard | current stored Purchases reader; discard rechecks kind in transaction. No new Cost dependency, no returned payload. List still parses payload internally for count/partner |

## Unsaved UI work

VM keeps original Lines, cost editor and paid editor privately when access is denied. Row CostText/TotalText show hidden; cost/paid editors show blank, saves and entry controls disabled, document custom answers hidden. Last monetary status is replaced by denial on the next check. Native editable textbox bindings cannot write their masked blank back into the retained fields while denied. Remove-line remains an explicit user operation; permission masking itself never clears lines or writes0. Selection gets a fresh product read; failed selection clears its editor so a different medicine cannot inherit the previous medicine's rate. AddLine rereads current pack metadata, while existing unsaved lines retain their reviewed input amounts.

Load/search/add/save/save-draft/open check first. A view-owned timer starts on Loaded and stops on Unloaded. Permission restored: retained values become visible again with a review message; **nothing is automatically saved/posted/repriced**. Review before saving is instructional, not a mandatory confirmation gate. Stale raw objects remain in local memory; this is a UI mask, not memory erasure or session authentication. Settings uses its established lifecycle/cache; external DB changes are not guaranteed immediate. Dispatcher timing is nominal, can be delayed; already rendered pixels, clipboard, printed/exported files or previously returned objects cannot be recalled. There is no coherent account/permission/settings snapshot or reservation spanning all reads. Generic exports remain OPEN.

## Preservation verified here

- PurchaseService **8 verbatim SQL blocks exact**, entire private **Save and helper tail exact** compared to pre-portion source. DraftService **8 verbatim SQL blocks exact**. No migration, invoice schema, batch costing, supplier payable/payment math or audit-payload rewrite.
- **109 other Core service/data hashes unchanged**, including Sale/Return/Stock/StockTake/Expense/SupplierLedger and migrations. All **660 baseline tracked files retained**. PermissionDocumentation changes are descriptive only.
- Existing successful manager purchase-draft test now explicitly grants PurchaseCost; no role shortcut. Sale drafts untouched by purchase-only branches. Existing crash/three-transaction marker limitations remain; no new exactly-once/concurrency certification.

## Evidence actually run vs pending

- **80 static checks PASS**;391C# grammar files,4135calls/7986member references,0reportedissues. Static source/SQL/binding/member checks, NOT a .NET compiler or xUnit/WPF/security result.
- **12 negative source mutations rejected/restored**: Create gate, Cost/main gate, insert/update gate, Open gate, premark callback, summary reader, result label, unsaved line retention, row mask, Save interaction, button enabled binding, timer unload. Clean guard after restore; not runtime fault injection.
- New `tools/check_purchase_preparation_sqlite.py`:36 actual migrations and8 source-extracted draft SQL statements on synthetic memory DB. Bounded type/ID, unchanged monetary payload, read-only reads, premark/restore/finalize/put-away retention, no purchase/payment rows and FK PASS. **Does NOT execute C# authorization, projection, transaction wrappers, duplicate/concurrency/crash behavior or UI.** Eight prior SQLite probes PASS.
- **30 new facts /1609 written C# cases UNRUN**: actor/current-module matrix; independent grants/main/rank; same-service revocation and old opened payload; real totals/payment/stock, genuine0/sample; metadata-only summary, current discard, grant restoration; actual draft update/post; explicit-input bypass, retry/double-post/rollback; malformed payload denied before deserialization; permission/reader failure; read-only access checks.
- Guide MD/HTML, PT-33, standing handoff/evidence/plan/version/runner documentation updated. Windows build/xUnit/WPF timer/pixels/printer/PDF/install and live-shop actions **UNRUN**. Test installer permission exists, tools/environment still absent here. No synthetic posts on live shop.

## Remaining / next

Generic audit/workbook/CSV and operational financial outputs, StockMinus, ordinary supplier ledger/Pay/SR policy, authenticated sessions/record-level/other writes, fault/concurrency reconciliation, import/archive/backup/cloud/file bounds, full UI/guide/hardware/Windows installation drills remain. Actual batch floor for everyone, exclusive price modes, sale-only Extra and return B560then540 unchanged. No obsolete feature removed. D-112 docs + current-branch commit/push required; actual Git result proves publication, not this paragraph.

[Safe PT-33](POS-PHASE4-PART13-URDU.md) — [Remaining work](REMAINING-WORK-AND-TEST-INSTALLER-URDU.md).
