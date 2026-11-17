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
            Modules.Suppliers => "Supplier contacts, account, payments, returns and statements. Separate cost controls are still being rolled out.",
            Modules.Customers => "Customer accounts, receipts, credit history and statements. Explicit profit needs its separate grant.",
            Modules.Pos => "Prepare and make sales. Rate changes, discounts and Extra need separate per-person pricing allowances.",
            Modules.Sales => "Read saved bills and prepare their history/reprints; time correction remains owner-only.",
            Modules.Returns => "Use customer returns and provisional returns; stock and refundable limits still apply.",
            Modules.Products => "Search the catalog and product history. Does not by itself grant purchase cost or product editing rights.",
            Modules.Purchases => "Receive deliveries and prepare purchase documents. Cost preparation needs its separate visibility gate.",
            Modules.Stock => "Stock write-off/count-correction workflows; actual batch stock rules remain mandatory.",
            Modules.StockTake => "Stock-count sessions and sheets. Applying a count has additional owner checks.",
            Modules.Reorder => "Reorder quantities, suppliers and order messages. Last purchase cost needs the separate cost grant.",
            Modules.Cash => "Cash/shift and expense workflows. Cost-bearing preparation has additional visibility rules.",
            Modules.Reports => "Read reports. Reports access alone does not grant explicit profit or migrated purchase-cost fields.",
            Modules.Audit => "Read the Black box audit history. Audit details may contain sensitive old/new values; cost rollout is not complete.",
            Modules.Import => "Import/export and workbook workflows. Files can contain shop-wide financial data; export cost review is still open.",
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
            Modules.PurchaseCost => "Migrated paths: catalog cost; Product History purchase rate AND line total AND Bought aggregate (including new CSV/document preparation); Reorder last cost; Dead Stock valuation; ReportTotals sale/restock cost and purchases aggregate; item-price quote cost; purchase invoice line/Total/Paid/Due amounts; expired supplier batch cost and valuation. Product-based receiving/SR/write-off/expense preparation refuses missing cost access instead of feeding a masked zero into a form.",
            Modules.Suppliers => "Existing Suppliers module: supplier list/account, expired-batch list, statements and bulk document preparation; payment and supplier-return operations have their own service checks. This is page/workflow access, NOT a new cost grant. Expired-batch cost and valuation now use View Purchase Cost + main ON; quantity and batch metadata stay visible, and one-click return preparation re-reads the current known-cost row. Supplier ledger and generic batch financial propagation is not yet migrated everywhere; do not treat this checkbox as proof of cost confidentiality.",
            Modules.Customers => "Customer list/account, receipt, monthly/bulk statements and history use the Customers module. Explicit customer profit also needs View Profit and the main switch. Account balances and credit limits are separate financial workflows, not a purchase-cost grant.",
            _ => summary + " Opening a page is not authorization for every action. Existing owner-only, account, transaction and workflow-specific checks still apply."
        };
        var prerequisites = PermissionService.IsFinancialVisibility(module)
            ? "Main profit/cost switch ON + this person's grant + the relevant page/module. Manager rank is not a financial grant. An explicit owner financial denial is respected."
            : "Active account + this module; action-specific permissions and owner-only restrictions still apply. Profit/cost visibility grants do not automatically grant this page.";
        var note = IsSensitive(module) || module is Modules.Import or Modules.Backup or Modules.Sync or Modules.Audit
            ? "GLOBAL ROLLOUT OPEN: purchase entry/drafts/results, supplier ledger/generic batches, stock-take/stock-minus, expense histories, audit/workbook/other exports and stale prepared objects still need review. Authorized profit, cost or balance figures can imply other numbers. Existing files/prints cannot be recalled. No all-route secrecy claim."
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
