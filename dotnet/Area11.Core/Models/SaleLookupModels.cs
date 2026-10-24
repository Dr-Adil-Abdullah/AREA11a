using System.Globalization;
using Area11.Core.Utils;

namespace Area11.Core.Models;

/// <summary>
/// What the counter types when the customer comes back without the bill (F-20, Spec 9.1).
///
/// Why one box and not four: a person standing at the counter says "I bought these last Tuesday",
/// "it was on Ali Raza's khata" or "the Panadol" - not "please search field three". The same text is
/// matched against the bill number, the customer's name, the phone with or without dashes, and the
/// medicines on the bill.
/// </summary>
public sealed class SaleLookupInput
{
    /// <summary>Bill number, phone, customer name or medicine. Empty means "any".</summary>
    public string? Text { get; set; }

    /// <summary>yyyy-MM-dd, or empty for "no lower limit".</summary>
    public string? FromDate { get; set; }

    /// <summary>yyyy-MM-dd, or empty for "no upper limit".</summary>
    public string? ToDate { get; set; }
}

/// <summary>
/// One bill in the "which bill was it?" list: enough to recognise it at the counter, and nothing
/// about money that the return screen does not already show once the bill is opened.
/// </summary>
public sealed class SaleLookupRow
{
    public int SaleId { get; set; }

    public string Code { get; set; } = string.Empty;

    /// <summary>"2026-10-20 14:05:00" - the shop's own clock, as the database stores it.</summary>
    public string Date { get; set; } = string.Empty;

    public string CustomerName { get; set; } = string.Empty;

    public string CustomerPhone { get; set; } = string.Empty;

    public long TotalPaisa { get; set; }

    public long DuePaisa { get; set; }

    /// <summary>paid | partial | credit | hold | void (Spec 6.1.6).</summary>
    public string Status { get; set; } = string.Empty;

    public int LineCount { get; set; }

    /// <summary>Date and time without the seconds - seconds never help anyone find a bill.</summary>
    public string DateLabel => Date.Length >= 16 ? Date[..16] : Date;

    public string CustomerLabel
    {
        get
        {
            if (CustomerName.Length == 0 && CustomerPhone.Length == 0)
            {
                return "Walking customer";
            }

            if (CustomerName.Length == 0)
            {
                return CustomerPhone;
            }

            return CustomerPhone.Length == 0 ? CustomerName : CustomerName + " · " + CustomerPhone;
        }
    }

    public string TotalText => Money.Format(TotalPaisa);

    public string ItemsLabel =>
        LineCount.ToString(CultureInfo.InvariantCulture)
        + (LineCount == 1 ? " item" : " items");

    /// <summary>The words the owner uses, not the database's - "on the khata" beats "credit".</summary>
    public string StatusLabel => Status.Trim() switch
    {
        "paid" => "paid",
        "partial" => "part paid",
        "credit" => "on the khata",
        "hold" => "held",
        "void" => "void",
        _ => Status
    };

    /// <summary>True when money is still owed on this bill - shown beside the total in the list.</summary>
    public bool HasDue => DuePaisa > 0;

    public string DueLabel => DuePaisa > 0 ? "due " + Money.Format(DuePaisa) : string.Empty;
}
