using Area11.Core.Data;
using Area11.Core.Models;
using Area11.Core.Utils;
using Microsoft.Data.Sqlite;
using System.Globalization;
using System.Text;

namespace Area11.Core.Services;

/// <summary>
/// Portion 13: the life story of one medicine. Every purchase (when, from whom, how much, at what
/// rate) and every sale (when, on which bill, to whom, at what rate) - oldest first, rates as they
/// were on the day, never today's. Putting a product away never touches this story.
///
/// Profit follows the approved definition (sale minus cost-at-sale minus discounts/returns, no
/// expense allocation): each sale line carries `line_total - qty * cost_paisa_at_sale`, and the
/// line total already has the line discount inside it. The numbers show only behind
/// <see cref="ProfitPolicy"/> - the permission plus the global switch.
/// </summary>
public sealed class ProductHistoryService
{
    private readonly Database _db;
    private readonly SettingsService _settings;
    private readonly PermissionService _permissions;
    private readonly LogService _log;

    public ProductHistoryService(
        Database db,
        SettingsService settings,
        PermissionService permissions,
        LogService log)
    {
        _db = db;
        _settings = settings;
        _permissions = permissions;
        _log = log;
    }

    /// <summary>The full story: purchases and sales, oldest first, with honest totals.</summary>
    public ServiceResult<ProductHistory> Load(User? user, int productId)
    {
        if (!_permissions.Can(user, Modules.Products))
        {
            return ServiceResult<ProductHistory>.Fail("You do not have permission to see the products.");
        }

        try
        {
            using var connection = _db.Open();
            var name = _db.QuerySingle<string>(connection,
                "SELECT name FROM products WHERE id = @id;", new { id = productId });
            if (name is null)
            {
                return ServiceResult<ProductHistory>.Fail("That medicine no longer exists.");
            }

            var history = new ProductHistory
            {
                ProductId = productId,
                ProductName = name,
                CanSeeProfit = ProfitPolicy.CanSee(_permissions, _settings, user)
            };

            history.Purchases.AddRange(_db.Query<ProductPurchaseRow>(connection, @"
SELECT p.date AS date, s.name AS supplierName, p.code AS billCode,
       i.qty_base AS qtyBase, i.cost_paisa AS ratePaisa, i.line_total_paisa AS lineTotalPaisa
FROM purchase_items i
JOIN purchases p ON p.id = i.purchase_id
JOIN suppliers s ON s.id = p.supplier_id
WHERE i.product_id = @id
ORDER BY p.date, p.id, i.id;", new { id = productId }));

            history.Sales.AddRange(_db.Query<ProductSaleRow>(connection, @"
SELECT s.date AS date, s.code AS billCode, IFNULL(c.name, '') AS buyerName,
       i.qty_base AS qtyBase, i.unit_price_paisa AS ratePaisa,
       i.line_total_paisa AS lineTotalPaisa,
       CAST(i.line_total_paisa - i.qty_base * i.cost_paisa_at_sale AS INTEGER) AS profitPaisa
FROM sale_items i
JOIN sales s ON s.id = i.sale_id
LEFT JOIN customers c ON c.id = s.customer_id
WHERE i.product_id = @id AND s.status IN ('paid', 'partial', 'credit')
ORDER BY s.date, s.id, i.id;", new { id = productId }));

            return ServiceResult<ProductHistory>.Success(history);
        }
        catch (Exception exception)
        {
            _log.Error("The history of product " + productId + " could not be read.", exception);
            return ServiceResult<ProductHistory>.Fail("The history could not be read. " + exception.Message);
        }
    }

    /// <summary>The story on paper: bought lines, sold lines, totals - profit only for allowed eyes.</summary>
    public ServiceResult<ReceiptDocument> BuildDocument(User? user, int productId)
    {
        var loaded = Load(user, productId);
        if (!loaded.Ok || loaded.Value is null)
        {
            return ServiceResult<ReceiptDocument>.Fail(loaded.Error ?? "The history could not be prepared.");
        }

        try
        {
            var history = loaded.Value;
            var width = ReceiptService.ColumnsForMillimetres(_settings.GetInt("printer.width", 58));
            var document = new ReceiptDocument
            {
                Title = "History " + history.ProductName,
                Width = width,
                AutoCut = _settings.GetBool("printer.autoCut", true),
                Copies = 1
            };

            void Add(string text, ReceiptAlign align = ReceiptAlign.Left, bool bold = false, bool big = false) =>
                document.Lines.Add(new ReceiptLine(ReceiptService.Fit(text, width), align, bold, big));

            void Pair(string left, string right, bool bold = false) =>
                document.Lines.Add(new ReceiptLine(ReceiptService.TwoColumns(left, right, width), ReceiptAlign.Left, bold));

            var shop = _settings.GetString("store.name");
            if (!string.IsNullOrWhiteSpace(shop))
            {
                Add(shop!.Trim(), ReceiptAlign.Center, bold: true);
            }

            Add("MEDICINE HISTORY", ReceiptAlign.Center, bold: true, big: true);
            Add(history.ProductName, ReceiptAlign.Center, bold: true);
            document.Lines.Add(ReceiptLine.Sep(width));

            Add("BOUGHT (" + history.Purchases.Count + ")", ReceiptAlign.Left, bold: true);
            foreach (var row in history.Purchases)
            {
                Add(row.DateLabel + " " + row.SupplierName);
                Pair("    " + row.QtyLabel + " @ " + row.RateLabel, row.TotalLabel);
            }

            document.Lines.Add(ReceiptLine.Sep(width));
            Add("SOLD (" + history.Sales.Count + ")", ReceiptAlign.Left, bold: true);
            foreach (var row in history.Sales)
            {
                Add(row.DateLabel + " " + row.BillCode + " " + row.BuyerLabel);
                Pair("    " + row.QtyLabel + " @ " + row.RateLabel, row.TotalLabel);
            }

            document.Lines.Add(ReceiptLine.Sep(width));
            Pair("Bought", history.BoughtQtyLabel + " / " + Money.Format(history.BoughtPaisa));
            Pair("Sold", history.SoldQtyLabel + " / " + Money.Format(history.SoldPaisa), bold: true);
            if (history.CanSeeProfit)
            {
                Pair("Profit", Money.Format(history.ProfitPaisa), bold: true);
            }

            return ServiceResult<ReceiptDocument>.Success(document);
        }
        catch (Exception exception)
        {
            _log.Error("The history of product " + productId + " could not be prepared.", exception);
            return ServiceResult<ReceiptDocument>.Fail("The history could not be prepared. " + exception.Message);
        }
    }

    /// <summary>
    /// The story as CSV (RFC 4180, no BOM - the page adds it on save, the Audit precedent): one
    /// `bought` or `sold` line per movement, money as plain numbers so the accountant can add the
    /// column. The profit column stays empty for eyes the policy refuses.
    /// </summary>
    public ServiceResult<string> BuildCsv(User? user, int productId)
    {
        var loaded = Load(user, productId);
        if (!loaded.Ok || loaded.Value is null)
        {
            return ServiceResult<string>.Fail(loaded.Error ?? "The history could not be prepared.");
        }

        try
        {
            var history = loaded.Value;
            var output = new StringBuilder();
            output.AppendLine("type,date,document,partner,qty,rate_paisa,total_paisa,profit_paisa");

            static string Cell(string value) =>
                "\"" + (value ?? string.Empty).Replace("\"", "\"\"") + "\"";

            string Qty(double value) => value.ToString("0.###", CultureInfo.InvariantCulture);

            foreach (var row in history.Purchases)
            {
                output.Append("bought,").Append(Cell(row.DateLabel)).Append(',').Append(Cell(row.BillCode))
                    .Append(',').Append(Cell(row.SupplierName)).Append(',').Append(Qty(row.QtyBase))
                    .Append(',').Append(row.RatePaisa.ToString(CultureInfo.InvariantCulture))
                    .Append(',').Append(row.LineTotalPaisa.ToString(CultureInfo.InvariantCulture))
                    .AppendLine(",\"\"");
            }

            foreach (var row in history.Sales)
            {
                output.Append("sold,").Append(Cell(row.DateLabel)).Append(',').Append(Cell(row.BillCode))
                    .Append(',').Append(Cell(row.BuyerLabel)).Append(',').Append(Qty(row.QtyBase))
                    .Append(',').Append(row.RatePaisa.ToString(CultureInfo.InvariantCulture))
                    .Append(',').Append(row.LineTotalPaisa.ToString(CultureInfo.InvariantCulture))
                    .Append(',').AppendLine(history.CanSeeProfit
                        ? row.ProfitPaisa.ToString(CultureInfo.InvariantCulture)
                        : "\"\"");
            }

            return ServiceResult<string>.Success(output.ToString());
        }
        catch (Exception exception)
        {
            _log.Error("The history CSV of product " + productId + " could not be prepared.", exception);
            return ServiceResult<string>.Fail("The history CSV could not be prepared. " + exception.Message);
        }
    }
}
