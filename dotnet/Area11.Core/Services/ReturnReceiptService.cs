using System.Globalization;
using Area11.Core.Data;
using Area11.Core.Models;
using Area11.Core.Utils;

namespace Area11.Core.Services;

/// <summary>
/// The paper the customer gets when they hand medicine back (U-35, 14.3). It must say four things,
/// because each one is a question the owner is asked at the counter:
///   • the permanent return number (RET-0001) and the original bill number,
///   • what came back and how much money went out,
///   • for every item: "back in stock" or "in quarantine (Q-0001)" - so nobody claims later that
///     the shop kept the goods AND the money,
///   • the reason, and a line for the receiver's signature.
///
/// One return = one number for all its lines = one receipt (15.3.1, 14.3.2).
/// </summary>
public sealed class ReturnReceiptService
{
    private readonly Database _db;
    private readonly SettingsService _settings;
    private readonly LogService _log;

    public ReturnReceiptService(Database db, SettingsService settings, LogService log)
    {
        _db = db;
        _settings = settings;
        _log = log;
    }

    public int PaperWidth
    {
        get
        {
            var millimetres = _settings.GetInt("printer.width", 58);
            return millimetres >= 80 ? 48 : 32;
        }
    }

    /// <summary>Builds the receipt of one return, found by its permanent number (RET-0001).</summary>
    public ServiceResult<ReceiptDocument> Build(string? returnCode)
    {
        var code = (returnCode ?? string.Empty).Trim();
        if (code.Length == 0)
        {
            return ServiceResult<ReceiptDocument>.Fail("There is no return number to print.");
        }

        try
        {
            using var connection = _db.Open();

            var rows = _db.Query<Row>(connection, @"
SELECT r.code, r.date, r.reason, r.note, r.qty_entered AS qtyEntered, r.unit_sold AS unitSold,
       r.refund_paisa AS refundPaisa, r.cash_refund_paisa AS cashPaisa,
       r.credit_reduced_paisa AS creditPaisa, r.restock, r.qcode,
       sa.code AS saleCode, si.name_snapshot AS productName,
       c.name AS customerName, c.phone AS customerPhone,
       u.name AS cashierName
FROM sale_returns r
JOIN sales sa ON sa.id = r.sale_id
JOIN sale_items si ON si.id = r.sale_item_id
LEFT JOIN customers c ON c.id = sa.customer_id
LEFT JOIN users u ON u.id = r.user_id
WHERE upper(r.code) = upper(@code)
ORDER BY r.id;", new { code });

            if (rows.Count == 0)
            {
                return ServiceResult<ReceiptDocument>.Fail($"No return found with the number {code}.");
            }

            return ServiceResult<ReceiptDocument>.Success(Compose(rows));
        }
        catch (Exception exception)
        {
            _log.Error($"The return receipt {code} could not be built.", exception);
            return ServiceResult<ReceiptDocument>.Fail("The return receipt could not be prepared.");
        }
    }

    private ReceiptDocument Compose(List<Row> rows)
    {
        var width = PaperWidth;
        var first = rows[0];

        var document = new ReceiptDocument
        {
            Title = first.Code,
            Width = width,
            AutoCut = _settings.GetBool("printer.autoCut", true),
            Copies = Math.Clamp(_settings.GetInt("printer.copies", 1), 1, 3)
        };

        void Add(string text, ReceiptAlign align = ReceiptAlign.Left, bool bold = false, bool big = false) =>
            document.Lines.Add(new ReceiptLine(ReceiptService.Fit(text, width), align, bold, big));

        void Pair(string left, string right, bool bold = false) =>
            document.Lines.Add(new ReceiptLine(ReceiptService.TwoColumns(left, right, width), ReceiptAlign.Left, bold));

        var shopName = FirstNonEmpty(_settings.GetString("store.name"), _settings.GetString("brand.appName"), "Area11");
        Add(shopName, ReceiptAlign.Center, bold: true);
        AddIfPresent(_settings.GetString("store.address"), ReceiptAlign.Center);
        AddIfPresent(_settings.GetString("store.phone"), ReceiptAlign.Center);

        document.Lines.Add(ReceiptLine.Sep(width));
        Add("RETURN", ReceiptAlign.Center, bold: true, big: true);
        Pair(first.Code, FormatDate(first.Date));
        Pair($"Bill: {first.SaleCode}", string.IsNullOrWhiteSpace(first.CashierName) ? string.Empty : $"By: {first.CashierName}");

        var customer = string.IsNullOrWhiteSpace(first.CustomerName) ? "Walk-in" : first.CustomerName!;
        if (!string.IsNullOrWhiteSpace(first.CustomerPhone))
        {
            customer += " " + first.CustomerPhone;
        }

        Pair($"Customer: {customer}", string.Empty);
        document.Lines.Add(ReceiptLine.Sep(width));

        foreach (var row in rows)
        {
            Add(ReceiptService.Fit(row.ProductName ?? string.Empty, width));
            Pair($"  {Qty.Format(row.QtyEntered)} {row.UnitSold} x",
                Money.Fixed(row.RefundPaisa));

            var where = row.InStock
                ? "back in stock"
                : $"quarantine ({(string.IsNullOrWhiteSpace(row.QCode) ? "Q-?" : row.QCode)})";

            Add("  " + where);
        }

        document.Lines.Add(ReceiptLine.Sep(width));

        var refund = rows.Sum(row => row.RefundPaisa);
        var cash = rows.Sum(row => row.CashPaisa);
        var credit = rows.Sum(row => row.CreditPaisa);

        Pair("TOTAL REFUND", Money.Fixed(refund), bold: true);

        if (cash != 0)
        {
            Pair("Cash refunded", Money.Fixed(cash));
        }

        if (credit != 0)
        {
            Pair("Khata reduced", Money.Fixed(credit));
        }

        var reason = string.IsNullOrWhiteSpace(first.Reason) ? "—" : first.Reason;
        Pair("Reason", reason);

        if (!string.IsNullOrWhiteSpace(first.Note))
        {
            Add(FirstNonEmpty(first.Note, string.Empty));
        }

        document.Lines.Add(ReceiptLine.Sep(width));
        Add("Received by: ______________", ReceiptAlign.Left);
        Add("Signature", ReceiptAlign.Left);
        document.Lines.Add(ReceiptLine.Sep(width));
        AddIfPresent(_settings.GetString("store.footerNote"), ReceiptAlign.Center);

        return document;

        void AddIfPresent(string? text, ReceiptAlign align)
        {
            if (!string.IsNullOrWhiteSpace(text))
            {
                Add(text!, align);
            }
        }
    }

    private static string FormatDate(string? raw) =>
        DateTime.TryParse(raw, CultureInfo.InvariantCulture, DateTimeStyles.None, out var stamp)
            ? stamp.ToString("dd-MM-yyyy HH:mm", CultureInfo.InvariantCulture)
            : raw ?? string.Empty;

    private static string FirstNonEmpty(params string?[] values) =>
        values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value)) ?? string.Empty;

    private sealed class Row
    {
        public string Code { get; set; } = string.Empty;

        public string? Date { get; set; }

        public string? Reason { get; set; }

        public string? Note { get; set; }

        public double QtyEntered { get; set; }

        public string? UnitSold { get; set; }

        public long RefundPaisa { get; set; }

        public long CashPaisa { get; set; }

        public long CreditPaisa { get; set; }

        /// <summary>stored as 0/1 by SQLite, so it is read as a number and not as a C# bool.</summary>
        public int Restock { get; set; }

        public bool InStock => Restock != 0;

        public string? QCode { get; set; }

        public string? SaleCode { get; set; }

        public string? ProductName { get; set; }

        public string? CustomerName { get; set; }

        public string? CustomerPhone { get; set; }

        public string? CashierName { get; set; }
    }
}
