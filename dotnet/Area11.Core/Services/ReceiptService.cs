using System.Globalization;
using Area11.Core.Data;
using Area11.Core.Models;
using Area11.Core.Utils;

namespace Area11.Core.Services;

/// <summary>
/// Turns a saved bill into the lines of a thermal receipt (Spec 14).
///
/// Why a separate service: the receipt must be readable on a 58 mm roll (32 characters) and on an
/// 80 mm roll (48 characters), and the shape of the paper must be testable without a printer. This
/// service produces the text and the paper behaviour only; <see cref="EscPosBuilder"/> adds the
/// printer commands and <see cref="PrinterService"/> sends the bytes to Windows.
///
/// Every word on the paper comes from Settings (owner rules D-07, E-03): shop name, address,
/// phone, footer, the discount line, and cut/copies/width.
/// </summary>
public sealed class ReceiptService
{
    private readonly Database _db;
    private readonly SettingsService _settings;
    private readonly LogService _log;

    public ReceiptService(Database db, SettingsService settings, LogService log)
    {
        _db = db;
        _settings = settings;
        _log = log;
    }

    /// <summary>Paper width in characters: 32 for 58 mm, 48 for 80 mm.</summary>
    public int PaperWidth => ColumnsForMillimetres(_settings.GetInt("printer.width", 58));

    /// <summary>
    /// The one place that turns millimetres into characters. The receipt and the hardware test slip
    /// must agree, or the slip would prove a width the bills do not use (F-46).
    /// </summary>
    public static int ColumnsForMillimetres(int millimetres) => millimetres >= 80 ? 48 : 32;

    /// <summary>Builds the receipt of one sale. A missing sale is an error, never an empty paper.</summary>
    public ServiceResult<ReceiptDocument> Build(int saleId)
    {
        try
        {
            using var connection = _db.Open();

            var sale = _db.QuerySingle<SaleRow>(connection, @"
SELECT id, code, date, customer_id, user_id, subtotal_paisa, discount_paisa, tax_paisa,
       round_off_paisa, total_paisa, paid_paisa, due_paisa, change_paisa, payment_method, status
FROM sales WHERE id = @id;", new { id = saleId });

            if (sale is null)
            {
                return ServiceResult<ReceiptDocument>.Fail("That bill was not found.");
            }

            var items = _db.Query<ItemRow>(connection, @"
SELECT name_snapshot, qty_entered, unit_sold, unit_price_paisa, discount_paisa, line_total_paisa
FROM sale_items WHERE sale_id = @id ORDER BY id;", new { id = saleId });

            var cashier = sale.UserId is null
                ? string.Empty
                : _db.QuerySingle<string>(connection,
                    "SELECT name FROM users WHERE id = @id;", new { id = sale.UserId }) ?? string.Empty;

            var customer = sale.CustomerId is null
                ? "Walk-in"
                : _db.QuerySingle<string>(connection,
                    "SELECT name FROM customers WHERE id = @id;", new { id = sale.CustomerId }) ?? "Walk-in";

            // One row per method that actually received money (split payment, Spec 13.3). Credit is
            // deliberately absent: it is not money in the drawer, it is the khata.
            var payments = _db.Query<PaymentRow>(connection, @"
SELECT method, amount_paisa FROM payments
WHERE sale_id = @id AND method <> 'credit' ORDER BY id;", new { id = saleId });

            return ServiceResult<ReceiptDocument>.Success(Compose(sale, items, payments, cashier, customer));
        }
        catch (Exception exception)
        {
            _log.Error($"The receipt for sale {saleId} could not be built.", exception);
            return ServiceResult<ReceiptDocument>.Fail("The receipt could not be prepared.");
        }
    }

    /// <summary>
    /// Pure layout - no database - so the tests can pin the shape of the paper. `payments` holds one
    /// row per method that received money; it is empty for a credit bill, which prints "On credit".
    /// </summary>
    internal ReceiptDocument Compose(
        SaleRow sale,
        List<ItemRow> items,
        List<PaymentRow> payments,
        string cashier,
        string customer)
    {
        var width = PaperWidth;
        var document = new ReceiptDocument
        {
            Title = sale.Code,
            Width = width,
            AutoCut = _settings.GetBool("printer.autoCut", true),
            Copies = Math.Clamp(_settings.GetInt("printer.copies", 1), 1, 3)
        };

        void Add(string text, ReceiptAlign align = ReceiptAlign.Left, bool bold = false, bool big = false) =>
            document.Lines.Add(new ReceiptLine(Fit(text, width), align, bold, big));

        void Pair(string left, string right, bool bold = false) =>
            document.Lines.Add(new ReceiptLine(TwoColumns(left, right, width), ReceiptAlign.Left, bold));

        var shopName = FirstNonEmpty(
            _settings.GetString("store.name"),
            _settings.GetString("brand.appName"),
            "Area11");

        Add(shopName, ReceiptAlign.Center, bold: true);
        AddIfPresent(_settings.GetString("store.address"), ReceiptAlign.Center);
        AddIfPresent(_settings.GetString("store.phone"), ReceiptAlign.Center);
        document.Lines.Add(ReceiptLine.Sep(width));

        var dateText = FormatDate(sale.Date);
        var dateOnTop = !string.Equals(_settings.GetString("receipt.datePosition"), "bottom",
            StringComparison.OrdinalIgnoreCase);

        if (dateOnTop)
        {
            Pair($"Bill: {sale.Code}", dateText);
            Pair($"Customer: {customer}", string.IsNullOrWhiteSpace(cashier) ? string.Empty : $"By: {cashier}");
            document.Lines.Add(ReceiptLine.Sep(width));
        }
        else
        {
            Pair($"Bill: {sale.Code}", string.Empty);
            Pair($"Customer: {customer}", string.Empty);
            document.Lines.Add(ReceiptLine.Sep(width));
        }

        foreach (var item in items)
        {
            var quantity = Qty.Format(item.QtyEntered);
            var unit = string.IsNullOrWhiteSpace(item.UnitSold) ? "unit" : item.UnitSold;
            var amount = Money.Fixed(item.LineTotalPaisa);

            Add(item.NameSnapshot);
            Pair($"  {quantity} {unit} x {Money.Fixed(item.UnitPricePaisa)}", amount);
        }

        document.Lines.Add(ReceiptLine.Sep(width));

        Pair("Subtotal", Money.Fixed(sale.SubtotalPaisa));
        if (_settings.GetBool("receipt.showDiscount", true) && sale.DiscountPaisa != 0)
        {
            Pair("Discount", "-" + Money.Fixed(sale.DiscountPaisa));
        }

        if (sale.TaxPaisa != 0)
        {
            var label = FirstNonEmpty(_settings.GetString("tax.label"), "Tax");
            Pair(label, Money.Fixed(sale.TaxPaisa));
        }

        if (sale.RoundOffPaisa != 0)
        {
            Pair("Round off", Money.Fixed(sale.RoundOffPaisa));
        }

        Pair("TOTAL", Money.Fixed(sale.TotalPaisa), bold: true);

        if (payments.Count > 0)
        {
            foreach (var part in payments)
            {
                // Points spent are stored as a negative row (Spec 13.6.1). On paper the customer reads
                // "Points used 5.00", never a minus sign he would think is a refund.
                if (string.Equals(part.Method, "loyalty", StringComparison.OrdinalIgnoreCase))
                {
                    Pair("Points used", Money.Fixed(Math.Abs(part.AmountPaisa)));
                    continue;
                }

                Pair(MethodLabel(part.Method), Money.Fixed(part.AmountPaisa));
            }
        }
        else if (string.Equals(sale.PaymentMethod, "credit", StringComparison.OrdinalIgnoreCase))
        {
            Pair("On credit", Money.Fixed(sale.DuePaisa));
        }

        // The change belongs to the SALE, not to the payment rows: an older bill (or one written by
        // the reference application) carries change_paisa with no `payments` row, and the customer
        // must still read what he got back.
        if (sale.ChangePaisa > 0)
        {
            Pair("Change", Money.Fixed(sale.ChangePaisa), bold: true);
        }

        if (sale.DuePaisa > 0)
        {
            Pair("Still due", Money.Fixed(sale.DuePaisa));
        }

        if (!dateOnTop)
        {
            document.Lines.Add(ReceiptLine.Sep(width));
            Add(dateText, ReceiptAlign.Center);
        }

        document.Lines.Add(ReceiptLine.Sep(width));
        AddIfPresent(_settings.GetString("store.footerNote"), ReceiptAlign.Center);
        AddIfPresent(_settings.GetString("store.terms"), ReceiptAlign.Center);

        return document;

        void AddIfPresent(string? text, ReceiptAlign align)
        {
            if (!string.IsNullOrWhiteSpace(text))
            {
                Add(text!.Trim(), align);
            }
        }
    }

    /// <summary>Puts the right-hand value against the right edge; never wider than the paper.</summary>
    internal static string TwoColumns(string left, string right, int width)
    {
        var cleanLeft = (left ?? string.Empty).TrimEnd();
        var cleanRight = (right ?? string.Empty).TrimEnd();

        if (cleanRight.Length == 0)
        {
            return Fit(cleanLeft, width);
        }

        if (cleanLeft.Length + 1 + cleanRight.Length > width)
        {
            cleanLeft = Fit(cleanLeft, Math.Max(0, width - 1 - cleanRight.Length)).TrimEnd();
        }

        var gap = width - cleanLeft.Length - cleanRight.Length;
        gap = Math.Max(1, gap);

        return Fit(cleanLeft + new string(' ', gap) + cleanRight, width);
    }

    /// <summary>Hard cut to the paper width. Thermal printers have no wrapping we can control.</summary>
    internal static string Fit(string text, int width)
    {
        var clean = (text ?? string.Empty).Replace("\r", " ").Replace("\n", " ");
        return clean.Length <= width ? clean : clean[..width];
    }

    private static string FormatDate(string? raw)
    {
        if (DateTime.TryParse(raw, CultureInfo.InvariantCulture, DateTimeStyles.None, out var stamp))
        {
            return stamp.ToString("dd-MM-yyyy HH:mm", CultureInfo.InvariantCulture);
        }

        return raw ?? string.Empty;
    }

    private static string FirstNonEmpty(params string?[] values) =>
        values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value))?.Trim() ?? string.Empty;

    /// <summary>"cash" -> "Cash": the paper says a word a customer understands.</summary>
    private static string MethodLabel(string? method) => (method ?? string.Empty).Trim().ToLowerInvariant() switch
    {
        "cash" => "Cash",
        "online" => "Online",
        "card" => "Card",
        "credit" => "On credit",
        "loyalty" => "Points",
        _ => "Paid"
    };

    internal sealed class PaymentRow
    {
        public string Method { get; set; } = "cash";

        public long AmountPaisa { get; set; }
    }

    internal sealed class SaleRow
    {
        public int Id { get; set; }

        public string Code { get; set; } = string.Empty;

        public string? Date { get; set; }

        public int? CustomerId { get; set; }

        public int? UserId { get; set; }

        public long SubtotalPaisa { get; set; }

        public long DiscountPaisa { get; set; }

        public long TaxPaisa { get; set; }

        public long RoundOffPaisa { get; set; }

        public long TotalPaisa { get; set; }

        public long PaidPaisa { get; set; }

        public long DuePaisa { get; set; }

        public long ChangePaisa { get; set; }

        public string? PaymentMethod { get; set; }

        public string? Status { get; set; }
    }

    internal sealed class ItemRow
    {
        public string NameSnapshot { get; set; } = string.Empty;

        public double QtyEntered { get; set; }

        public string? UnitSold { get; set; }

        public long UnitPricePaisa { get; set; }

        public long DiscountPaisa { get; set; }

        public long LineTotalPaisa { get; set; }
    }
}
