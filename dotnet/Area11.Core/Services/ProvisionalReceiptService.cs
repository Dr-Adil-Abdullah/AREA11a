using System.Globalization;
using Area11.Core.Data;
using Area11.Core.Models;
using Area11.Core.Utils;

namespace Area11.Core.Services;

/// <summary>
/// The slip for a no-bill return (14.3.3). It has one job the sale receipt does not have: it must
/// tell the customer, on paper, that **the bill is still missing** and that the entry can be traced
/// by its `PR-nnnn` number. That is the only protection the shop has when no bill exists.
/// </summary>
public sealed class ProvisionalReceiptService
{
    private readonly ProvisionalReturnService _provisional;
    private readonly SettingsService _settings;
    private readonly LogService _log;

    public ProvisionalReceiptService(
        ProvisionalReturnService provisional,
        SettingsService settings,
        LogService log)
    {
        _provisional = provisional;
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

    public ServiceResult<ReceiptDocument> Build(string? code)
    {
        var clean = (code ?? string.Empty).Trim();
        if (clean.Length == 0)
        {
            return ServiceResult<ReceiptDocument>.Fail("There is no no-bill return number to print.");
        }

        try
        {
            var row = _provisional.Get(clean);
            if (row is null)
            {
                return ServiceResult<ReceiptDocument>.Fail("No no-bill return found with the number " + clean + ".");
            }

            var lines = _provisional.Lines(row.Id);
            var document = Compose(row, lines);

            return ServiceResult<ReceiptDocument>.Success(document);
        }
        catch (Exception exception)
        {
            _log.Error("The no-bill return receipt " + clean + " could not be built.", exception);
            return ServiceResult<ReceiptDocument>.Fail("The no-bill return receipt could not be prepared.");
        }
    }

    private ReceiptDocument Compose(ProvisionalRow row, List<string> lines)
    {
        var width = PaperWidth;

        var document = new ReceiptDocument
        {
            Title = row.Code,
            Width = width,
            AutoCut = _settings.GetBool("printer.autoCut", true),
            Copies = Math.Clamp(_settings.GetInt("printer.copies", 1), 1, 3)
        };

        void Add(string text, ReceiptAlign align = ReceiptAlign.Left, bool bold = false, bool big = false) =>
            document.Lines.Add(new ReceiptLine(ReceiptService.Fit(text, width), align, bold, big));

        void Pair(string left, string right, bool bold = false) =>
            document.Lines.Add(new ReceiptLine(ReceiptService.TwoColumns(left, right, width), ReceiptAlign.Left, bold));

        Add(FirstNonEmpty(_settings.GetString("store.name"), _settings.GetString("brand.appName"), "Area11"),
            ReceiptAlign.Center, bold: true);

        var address = _settings.GetString("store.address");
        if (!string.IsNullOrWhiteSpace(address))
        {
            Add(address, ReceiptAlign.Center);
        }

        var phone = _settings.GetString("store.phone");
        if (!string.IsNullOrWhiteSpace(phone))
        {
            Add(phone, ReceiptAlign.Center);
        }

        document.Lines.Add(ReceiptLine.Sep(width));
        Add("NO-BILL RETURN", ReceiptAlign.Center, bold: true, big: true);
        Pair(row.Code, FormatDate(row.Date));
        Pair("Customer: " + row.CustomerLabel, string.Empty);

        if (!string.IsNullOrWhiteSpace(row.CashierName))
        {
            Pair("By: " + row.CashierName, string.Empty);
        }

        document.Lines.Add(ReceiptLine.Sep(width));

        foreach (var line in lines)
        {
            Add("- " + line);
        }

        document.Lines.Add(ReceiptLine.Sep(width));
        Pair("TOTAL REFUND", Money.Fixed(row.RefundPaisa), bold: true);

        var reason = string.IsNullOrWhiteSpace(row.Reason) ? "no bill" : row.Reason!;
        Pair("Reason", reason);

        if (!string.IsNullOrWhiteSpace(row.Notes))
        {
            Add(row.Notes!);
        }

        document.Lines.Add(ReceiptLine.Sep(width));

        // A reprint must describe the CURRENT state; saying "bill missing" above "bill attached"
        // made an old linked slip contradict itself.
        if (row.IsLinked && !string.IsNullOrWhiteSpace(row.LinkedCode))
        {
            Add("Bill attached: " + row.LinkedCode, ReceiptAlign.Center, bold: true);
            Add(row.NeedsReconciliation
                ? "Quantity reconciliation required."
                : "Stock/payment not added again.", ReceiptAlign.Center);

            if (row.HasLinkVariance)
            {
                Add("Refund differs from bill value.", ReceiptAlign.Center, bold: true);
            }
        }
        else
        {
            Add("Paper bill missing. Keep this slip.", ReceiptAlign.Center, bold: true);
            Add("The bill can be attached later; the", ReceiptAlign.Center);
            Add("number above is permanent.", ReceiptAlign.Center);
        }

        if (row.IsCancelled)
        {
            Add("CANCELLED: " + FirstNonEmpty(row.CancelReason, "no reason"), ReceiptAlign.Center, bold: true);
        }

        document.Lines.Add(ReceiptLine.Sep(width));
        Add("Received by: ______________");
        Add("Signature");
        document.Lines.Add(ReceiptLine.Sep(width));

        var footer = _settings.GetString("store.footerNote");
        if (!string.IsNullOrWhiteSpace(footer))
        {
            Add(footer, ReceiptAlign.Center);
        }

        return document;
    }

    private static string FormatDate(string? raw) =>
        DateTime.TryParse(raw, CultureInfo.InvariantCulture, DateTimeStyles.None, out var stamp)
            ? stamp.ToString("dd-MM-yyyy HH:mm", CultureInfo.InvariantCulture)
            : raw ?? string.Empty;

    private static string FirstNonEmpty(params string?[] values) =>
        values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value)) ?? string.Empty;
}
