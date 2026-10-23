using System.Globalization;
using Area11.Core.Models;
using Area11.Core.Utils;

namespace Area11.Core.Services;

/// <summary>
/// The handover slip (Spec 14.4 and 18.2.2, F-55): the paper two people sign when the drawer
/// changes hands. It carries **the same six money lines** the shift screen shows, the expected
/// figure, the counted figure, the difference in words, and two signature lines.
///
/// It is built as a <see cref="ReceiptDocument"/> - the same object the sale receipt and the return
/// receipt use - so it prints through the one proven printer path (58 mm = 32 characters,
/// 80 mm = 48) and can be tested without a printer.
///
/// The slip is deliberately a record, not a second opinion: the numbers come from the shift rows,
/// which were themselves rebuilt from the bills and payments.
/// </summary>
public sealed class HandoverSlipService
{
    private readonly SettingsService _settings;

    public HandoverSlipService(SettingsService settings)
    {
        _settings = settings;
    }

    /// <summary>32 characters on a 58 mm roll, 48 on an 80 mm roll (Spec 14.2).</summary>
    public int PaperWidth => ReceiptService.ColumnsForMillimetres(_settings.GetInt("printer.width", 58));

    public ReceiptDocument Build(ShiftHandover handover)
    {
        ArgumentNullException.ThrowIfNull(handover);

        var width = PaperWidth;

        var document = new ReceiptDocument
        {
            Title = "Handover",
            Width = width,
            AutoCut = _settings.GetBool("printer.autoCut", true),
            Copies = 1
        };

        void Add(string text, ReceiptAlign align = ReceiptAlign.Left, bool bold = false, bool big = false) =>
            document.Lines.Add(new ReceiptLine(ReceiptService.Fit(text, width), align, bold, big));

        void Pair(string left, string right, bool bold = false) =>
            document.Lines.Add(new ReceiptLine(ReceiptService.TwoColumns(left, right, width), ReceiptAlign.Left, bold));

        void AddIfPresent(string? text, ReceiptAlign align)
        {
            if (!string.IsNullOrWhiteSpace(text))
            {
                Add(text!, align);
            }
        }

        var shopName = FirstNonEmpty(_settings.GetString("store.name"), _settings.GetString("brand.appName"), "Area11");

        Add(shopName, ReceiptAlign.Center, bold: true);
        AddIfPresent(_settings.GetString("store.address"), ReceiptAlign.Center);
        AddIfPresent(_settings.GetString("store.phone"), ReceiptAlign.Center);

        document.Lines.Add(ReceiptLine.Sep(width));
        Add("HANDOVER", ReceiptAlign.Center, bold: true, big: true);
        Add("galla ka tabadla", ReceiptAlign.Center);
        document.Lines.Add(ReceiptLine.Sep(width));

        Add("From: " + handover.GivenBy);
        Add("To:   " + handover.GivenTo);
        Add("At:   " + Stamp(handover.GivenAt));
        Add("Drawer no. " + handover.From.Id.ToString(CultureInfo.InvariantCulture)
            + " -> " + handover.To.Id.ToString(CultureInfo.InvariantCulture));
        document.Lines.Add(ReceiptLine.Sep(width));

        // The six lines that explain the drawer - the same ones the shift screen shows.
        Pair("Opening float", Money.Fixed(handover.From.OpeningFloatPaisa));
        Pair("Cash bills", Money.Fixed(handover.From.CashSalesPaisa));
        Pair("Khata received", Money.Fixed(handover.From.CashCollectionsPaisa));
        Pair("Cash from supplier", Money.Fixed(handover.From.SupplierCashInPaisa));
        Pair("Refunds", "-" + Money.Fixed(handover.From.CashRefundsPaisa));
        Pair("Paid to suppliers", "-" + Money.Fixed(handover.From.SupplierCashOutPaisa));
        Pair("Shop expenses", "-" + Money.Fixed(handover.From.ExpensesPaisa));
        Pair("Owner took (cash)", "-" + Money.Fixed(handover.From.DrawingsPaisa));

        document.Lines.Add(ReceiptLine.Sep(width));
        Pair("Expected", Money.Fixed(handover.From.ExpectedPaisa));
        Pair("Counted", Money.Fixed(handover.CountedPaisa));
        Pair("Difference", Difference(handover.From), bold: true);
        document.Lines.Add(ReceiptLine.Sep(width));

        Pair("HANDED OVER", Money.Fixed(handover.CountedPaisa), bold: true);
        Add("This money is the opening float of drawer " + handover.To.Id.ToString(CultureInfo.InvariantCulture) + ".");
        document.Lines.Add(ReceiptLine.Sep(width));

        // Spec 18.2.2: both signatures. Two short lines so a 32-character slip still fits them.
        Add("Given by: ________________");
        Add(handover.GivenBy);
        Add(string.Empty);
        Add("Taken by: ________________");
        Add(handover.GivenTo);
        Add(string.Empty);
        AddIfPresent(_settings.GetString("store.footerNote"), ReceiptAlign.Center);

        return document;
    }

    /// <summary>
    /// "exact" / "50.00 over" / "50.00 short" - the same words the shift screen uses, kept short
    /// enough for the right-hand column of a 58 mm slip.
    /// </summary>
    internal static string Difference(ShiftMoney shift) => shift.DifferencePaisa switch
    {
        null => "-",
        0L => "exact",
        > 0 => Money.Fixed(shift.DifferencePaisa.Value) + " over",
        _ => Money.Fixed(-shift.DifferencePaisa.Value) + " short"
    };

    /// <summary>"23-10-2026 18:40" - the full stamp, not the date alone (a shift can change hands twice a day).</summary>
    private static string Stamp(string? raw)
    {
        if (DateTime.TryParse(raw, CultureInfo.InvariantCulture, DateTimeStyles.None, out var when))
        {
            return when.ToString("dd-MM-yyyy HH:mm", CultureInfo.InvariantCulture);
        }

        return string.IsNullOrWhiteSpace(raw) ? "-" : raw!;
    }

    private static string FirstNonEmpty(params string?[] values)
    {
        foreach (var value in values)
        {
            if (!string.IsNullOrWhiteSpace(value))
            {
                return value!.Trim();
            }
        }

        return string.Empty;
    }

}
