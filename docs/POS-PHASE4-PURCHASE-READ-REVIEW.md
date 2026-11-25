# Phase4 portion9 — Purchase invoice and supplier expired-stock reads, v0.87.0 Waxwing

> **⏳ checkpoint کی حد:** اس فائل کے نتائج/اعداد اسی درج شدہ checkpoint کے ہیں؛پرانے pending یا not-started جملوں کو موجودہ حالت نہ سمجھیں۔ [تازہ مجموعی رپورٹ](COMPLETE-STATUS-AND-DECISIONS-URDU.md) اور[فیصلوں کا index](DECISION-REGISTER-INDEX-URDU.md) پہلے دیکھیں؛نیا Windows PASS فرض نہیں۔

> **Subsequent portion13:** [Purchase entry/draft/result boundary](POS-PHASE4-PURCHASE-PREPARATION-REVIEW.md) now adds current Purchases+Cost/main preparation checks before Create/open/save/post. Saved invoice masking below remains unchanged. Ordinary Supplier paid/payable/PaySR remains undecided. [PT-33](POS-PHASE4-PART13-URDU.md).

**Date:**2026-11-10. **Status:** bounded D-110 continuation; global rollout / Phase4 OPEN. No .NET/Windows execution. Separate Profit/Cost grants and common main switch retained. This portion does NOT decide the still-open ordinary supplier payable/payment policy.

## View → VM → Core → data / permission

| Entry | Trace | Data / gate / behavior |
|---|---|---|
| New purchase invoice preparation | PurchasesView → PurchasesViewModel.PrintInvoice / SendInvoiceOnWhatsApp → PurchaseService.BuildInvoiceDocument | SnapshotReadAccess resolves active current account/same role + Purchases module BEFORE record lookup; CostPolicy independently gates all direct invoice money |
| New print/PDF/text preparation | BuildInvoiceDocument returns ReceiptDocument → existing printer/WhatsApp/PDF flow | denied line amount AND Total/Paid/Due are literal hidden, not fake0; recipient/quantity/unit/batch/date/invoice identity remain; no raw header/line-money object returned in ReceiptDocument |
| Expired supplier list | SuppliersView.ValueLabel → SuppliersViewModel.LoadExpired → SupplierLedgerService.ExpiredBatches | existing LedgerReadAccess current account/Suppliers gate retained; CostPolicy sets CostKnown; denied raw CostPaisa0, derived ValuePaisa0 and hidden ValueLabel |
| One-click whole expired batch | SuppliersView → SuppliersViewModel.AddExpired → fresh ExpiredBatches(_user,500) | find selected batch in current bounded list; fail before any tray mutation if read failed/row absent/CostKnown false; known genuine0 allowed; fresh quantity/cost used, not stale displayed row |
| Permission help / live inspector scope | PermissionDocumentation → Settings hover/details | names invoice/expired paths as migrated; still warns about purchase entry/drafts/results, supplier ledger/generic batch and other open routes |

A manager or Profit-only grantee does NOT automatically see money in these two read surfaces. Cost-only plus correct page and main ON can see it. Main OFF or owner's explicit own cost denial masks owner too. A missing/null/inactive/forged-role invoice reader is refused; expected ValidationException denial is not logged as an unexpected crash. Unexpected invoice read failure no longer returns raw database exception text to the caller.

## Why Paid and Due are both masked on the purchase invoice

Synthetic example: three units at12345 paisa =37035 total; paid10000 +due27035 reconstructs37035 even when every line total is hidden. Denied invoice therefore has fixed Total/Paid/Due hidden rows; it does not branch on whether a hidden payment was positive. Authorized output retains its existing payment-method and zero-paid formatting. No purchase-rate column was added. Current line amounts were the exposed cost-bearing values and are hidden too.

This is only the **new purchase document preparation**. Purchase Create/result, entry form/draft lifecycle, supplier account totals and previously prepared/printed files are not declared private by this fix. Names, invoice references, dates and sample/free type labels remain nonfinancial metadata; arbitrary user-authored text is not automatically scrubbed for embedded numbers. No mathematical-secrecy claim.

## Expired row and preparation safety

ExpiredBatchRow now distinguishes hidden valuation from actual zero through CostKnown. Cost denial must not be interpreted as zero-valued inventory or a free return. AddExpired re-reads the existing list with its maximum500 bound and refuses before replacing/removing a tray row if permission/account changed or selected batch no longer appears. If an expired selection fell outside that bound, refresh/reselect; no arbitrary first row fallback. Existing WHERE/order/limit/filter SQL is unchanged.

An already prepared tray or printed document cannot be recalled. Supplier Return service still derives actual costs from the database, not the UI tray's displayed cost. Its monetary result/warnings and the unresolved supplier payable/payment policy are **unchanged and still open**; this is not a new cost gate on all supplier posting APIs. Generic StockService.Batches, manual SR tray routes and other stock/expense preparation still need their remaining review. No silent global supplier balance exception was chosen.

## Source preservation / evidence actually executed

- **76 static checks PASS**;387 real C# grammar files;3993 calls/7539 member references resolved,0 reported problems. This is not compiler/xUnit/WPF/security certification.
- **10 negative source mutations rejected/restored:** invoice current reader, cost incorrectly tied to Profit, line total mask, Total mask, Paid mask, Due mask, expired raw mask, expired hidden label, stale selected-row reuse and known-cost preparation gate. Originals restored; guard clean. These do not execute Windows UI.
- PurchaseService **8** and SupplierLedgerService **16** verbatim SQL blocks unchanged from portion-start copies. Purchase Create prefix and entire Save/helper tail byte-for-byte unchanged. All SupplierLedgerService code outside ExpiredBatches unchanged, including Page/List/Statement/Pay/Return and their formulas.
- **44** data/migration/other financial-service baseline hashes unchanged; all643 portion-start files retained. No schema/migration/posting/history rewrite.
- New `tools/check_purchase_read_sqlite.py`:36 actual migrations; source-extracted invoice header/lines and expired SELECTs on synthetic in-memory DB. Expected line/header/Paid+Due arithmetic, active/expiry/nonzero filter, bound ID and unchanged dump/FK PASS. Independent Python display model illustrates the masking contract, **not C# projection/ReceiptDocument execution**.
- Existing BookCheck snapshot, permission-inspector SQL snapshot, Extra history/BookCheck/B560540 and time immediate-lock/rollback scripts PASS on isolated synthetic SQLite. None is .NET/runtime proof.
- Main guide MD/HTML body parity/36 lessons, review/PT links and generated rule/checklist outputs checked; `git diff --check` clean. No live-shop synthetic posts, installer, commit or push.

## Written C# tests — UNEXECUTED

**24 new facts**, PurchaseReadVisibilityTests; **1493 total written cases**. Covers null/forged/missing/inactive/role-changed invoice readers, module vs cost, manager default-deny, profit-only, cost-only, owner/global off-on, same-service revocation, missing targets, genuine0 invoice/expired cost, hidden Paid+Due even unpaid, expired metadata/filter preservation, module denial, owner explicit cost denial, financial-grant DB failure, and unchanged money/stock/payment/audit rows after masked reads. Original allowed-owner BulkPrint/SupplierLedger financial expectations not relaxed. Actual Windows v0.80.0 four failures were identified and source-fixed earlier; **no updated Windows green run exists here**.

## Remaining / 15-dimension status

Money/rounding, discount ordering, allocated batch cost, history/returns: no posting formula changes; read projections focused, financial execution pending. Stock atomicity/concurrency/duplicate saves: unchanged, no new certification. Permissions/IDs: two named readers and one current selection focused; full session/arbitrary-ID/delegated write review OPEN. Inputs/files: bound existing reads and safe expected failure, not a full export/archive/HTML proof. Backup/cloud and other finance: unchanged. Draft/Park and already prepared objects: OPEN. UI/native keys/hardware: call signatures/labels traced, Windows print/PDF/row-selection behavior UNRUN. Guide/obsolete: lessons08/17/31/35 updated, full36-lesson action audit OPEN, no removal.

**Next independent priority:** generic batch/stock/expense read/preparation and workbook/audit export paths. Supplier whole-account money/Pay/SR policy still needs an explicit answer; do not re-ask the settled separate-checkbox decision. All-route consistency, authenticated sessions, finance fault matrix, files/backup/cloud and Windows/fullPhase4 remain OPEN.

[Safe PT-29](POS-PHASE4-PART9-URDU.md). [Overall financial rollout](POS-PHASE4-FINANCIAL-VISIBILITY-REVIEW.md). [Permission UI](POS-PHASE4-PERMISSION-INSPECTOR-REVIEW.md).
