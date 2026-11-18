using Area11.Core.Models;

namespace Area11.Core.Services;

/// <summary>One description for hover and deep-dive. Existing database module keys are unchanged.</summary>
public static class PermissionDocumentation
{
    public static bool IsSensitive(string module) => module is Modules.Profit or Modules.PurchaseCost
        or Modules.Suppliers or Modules.Customers;

    public static PermissionDefinition For(string module)
    {
        var summary = module switch
        {
            Modules.Profit => "View explicit profit and margin on permitted pages. Does not grant raw purchase cost.",
            Modules.PurchaseCost => "View purchase cost, related totals and stock valuation on migrated permitted pages. Does not grant explicit profit.",
            Modules.Suppliers => "D-114: Supplier account amounts, payments, returns and statements even with Cost/main OFF. Direct item/batch cost needs Cost/main.",
            Modules.Customers => "Customer accounts, receipts, credit history and statements. Explicit profit needs its separate grant.",
            Modules.Pos => "Prepare and make sales. Rate changes, discounts and Extra need separate per-person pricing allowances.",
            Modules.Sales => "Read saved bills and prepare their history/reprints; time correction remains owner-only.",
            Modules.Returns => "Use customer returns and provisional returns; stock and refundable limits still apply.",
            Modules.Products => "Search the catalog and product history. Does not by itself grant purchase cost or product editing rights.",
            Modules.Purchases => "Receive deliveries and prepare purchase documents. Create and purchase draft open/save/post require current Purchases plus View Purchase Cost and the main switch; summary list/discard need current Purchases only.",
            Modules.Stock => "Stock write-off/count-correction workflows; actual batch stock rules remain mandatory.",
            Modules.StockTake => "Stock-count sessions and sheets; frozen monetary outputs need independent cost/main visibility. Applying a count has additional owner checks.",
            Modules.Reorder => "Reorder quantities, suppliers and order messages. Last purchase cost needs the separate cost grant.",
            Modules.Cash => "Cash/shift and expense workflows. Goods-cost preparation/history/totals/results/vouchers have independent cost/main visibility; ordinary cash amounts retain Cash access.",
            Modules.Reports => "Read reports. Reports access alone does not grant explicit profit or migrated purchase-cost fields.",
            Modules.Audit => "Read Black box metadata. Raw old/new/details and entity references require BOTH financial grants and main ON; CSV also requires the dedicated bulk-file permission.",
            Modules.Import => "Allow Excel/CSV Bulk Export & Import: owner by default; managers/staff require an explicit owner grant. Product cost files need Cost/main; mixed shop workbook needs both financial grants/main. No masked-zero editable file.",
            Modules.Backup => "Backup/restore workflows involving the whole shop dataset, including financial history and photos.",
            Modules.Settings => "Edit ordinary settings. Owner-only identity, financial main switch and secret controls keep their extra guards.",
            Modules.Staff => "Staff administration remains owner-only. Giving this module to a manager does not make them an owner.",
            Modules.Sync => "Cloud synchronization can transfer the whole shop dataset; it is not a filtered financial report.",
            Modules.CloseApp => "May close the protected application through its authorized close flow; does not grant other modules.",
            _ => throw new ValidationException("Choose an existing permission.")
        };
        var scope = module switch
        {
            Modules.Profit => "Explicit Dashboard, report, doctor/customer and Product History profit follows the common main switch and this grant. Catalog margin is calculated before raw cost is masked. Item-price quotes return a separate ProfitKnown projection. This is not a pricing/discount allowance and does not grant purchase-cost fields.",
            Modules.PurchaseCost => "Migrated paths: catalog cost; Product History purchase rate AND line total AND Bought aggregate (including new CSV/document preparation); Reorder last cost; Dead Stock valuation; ReportTotals sale/restock cost and purchases aggregate; item-price quote cost; purchase invoice line/Total/Paid/Due amounts; expired supplier batch cost and valuation; generic batch read cost/valuation for Stock, Cash/expense and Suppliers. Raw batch-cost previews require Cost/main. D-114 SupplierReturn uses fresh quantity/pack preparation with an explicit unknown-cost flag when direct cost is hidden. Stock write-off history/today/result and stock-take current/history/frozen sheet/found-product/document/Open/Apply outputs now project cost separately; private frozen valuation stays unchanged. Expense goods-drawing list/today/result/voucher amounts now use the same independent cost/main gate, without hiding ordinary module-authorized cash expense or cash drawing amounts. Purchase Create and draft open/save/post now recheck current Purchases + Cost/main before raw payload/write; summary list/discard remain current module-only. Unsaved form money is masked and saves blocked on next check, not erased or replaced by editable zero. Editable product CSV/XLSX is now all-or-refuse with the bulk grant plus Cost/main; mixed shop workbook and raw audit detail require both financial grants/main. Receiving/write-off/expense raw-cost preparation refuses missing cost access; SupplierReturn allows quantity-only preparation, never a masked zero treated as known. D-114 Supplier accounting totals/settlements remain visible without this grant or main ON, so purchase cost can be inferred.",
            Modules.Suppliers => "D-114 ACCOUNTING EXCEPTION B: current Suppliers module access allows purchases/paid/payable, movements, statements/bulk statements, profile sharing, Pay and SR settlement amounts even when Cost/main is OFF. For example paid500 + payable1500 can reveal purchase2000; the owner explicitly accepts this inference, including SR total/quantity. Direct product/batch cost and tray previews still require independent View Purchase Cost + main ON. Manual/expired SR preparation re-reads current IDs/quantity/pack; hidden costs have CostKnown=false, not a known zero. Pay/Return recheck current active same-role account and Supplier module inside the transaction; server-side costs, stock rules and accounting remain authoritative.",
            Modules.Customers => "Customer list/account, receipt, monthly/bulk statements and history use the Customers module. Explicit customer profit also needs View Profit and the main switch. Account balances and credit limits are separate financial workflows, not a purchase-cost grant.",
            _ => summary + " Opening a page is not authorization for every action. Existing owner-only, account, transaction and workflow-specific checks still apply."
        };
        var prerequisites = PermissionService.IsFinancialVisibility(module)
            ? "Main profit/cost switch ON + this person's grant + the relevant page/module. Manager rank is not a financial grant. An explicit owner financial denial is respected."
            : "Active account + this module; action-specific permissions and owner-only restrictions still apply. Profit/cost visibility grants do not automatically grant this page.";
        var note = IsSensitive(module) || module is Modules.Import or Modules.Backup or Modules.Sync or Modules.Audit
            ? "GLOBAL ROLLOUT OPEN: D-114 Supplier accounting exception is deliberate; other outputs, sessions and stale prepared objects still need review. Authorized profit, cost or balance figures can imply other numbers. Existing files/prints cannot be recalled. No all-route secrecy claim."
            : "Read the action's own rules as well. Windows/runtime and full Phase4 route audit remain pending; this explanation is not a security certification.";
        var title = module switch
        {
            Modules.Suppliers => "Supplier ledger access (Suppliers)",
            Modules.Customers => "Customer ledger access (Customers)",
            _ => Modules.Label(module)
        };
        return new PermissionDefinition(module, title, summary, scope, prerequisites, note);
    }
}
