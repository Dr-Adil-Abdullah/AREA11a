using Area11.Core.Utils;
using System.Globalization;

namespace Area11.Core.Models;

/// <summary>
/// One day of the tax report (F-49, Spec 6.5 / 33.4.1): what the counter sold, what tax it added.
///
/// The numbers are the files' own: `subtotal - discount` is the amount the tax was charged on,
/// `tax_paisa` is what was added, and `total_paisa` is what the customer paid. Nothing is
/// recalculated here, so a rate changed in the middle of the month still reports what each bill
/// actually took.
/// </summary>
public sealed class TaxDayRow
{
    /// <summary>The day as stored, `yyyy-MM-dd`.</summary>
    public string? Day { get; set; }

    public int Bills { get; set; }

    /// <summary>Sales before tax: subtotal minus the bill's discount.</summary>
    public long NetPaisa { get; set; }

    public long TaxPaisa { get; set; }

    public long GrossPaisa { get; set; }

    /// <summary>What the screen and the copied text show: the full day, `2026-10-05`.</summary>
    public string DayLabel => string.IsNullOrWhiteSpace(Day) || Day!.Length < 10
        ? (Day ?? "-")
        : Day.Substring(0, 10);

    public string BillsLabel => Bills.ToString(CultureInfo.InvariantCulture);

    public string NetLabel => Money.Format(NetPaisa);

    public string TaxLabel => Money.Format(TaxPaisa);

    public string GrossLabel => Money.Format(GrossPaisa);
}

/// <summary>
/// The month's tax: the totals, the days, and the settings the shop was using when the report was
/// opened. The report never guesses a rate - it shows the bills' own tax and says which rate the
/// counter is set to now, so an accountant can see a mid-month change.
/// </summary>
public sealed class TaxReport
{
    public int Year { get; set; }

    public int Month { get; set; }

    /// <summary>October 2026 - the heading.</summary>
    public string MonthLabel { get; set; } = string.Empty;

    /// <summary>What Settings calls the tax, for example "Sales Tax".</summary>
    public string TaxLabel { get; set; } = "Sales Tax";

    public bool TaxEnabled { get; set; }

    public string PercentLabel { get; set; } = "0%";

    public List<TaxDayRow> Rows { get; set; } = new();

    public int Bills { get; set; }

    public long NetPaisa { get; set; }

    public long TaxPaisa { get; set; }

    public long GrossPaisa { get; set; }

    /// <summary>Refunds paid back in this month, all reasons. Shown, never subtracted from the tax.</summary>
    public long RefundPaisa { get; set; }

    public string BillsLabel => Bills.ToString(CultureInfo.InvariantCulture);

    public string NetLabel => Money.Format(NetPaisa);

    public string TaxTotalLabel => Money.Format(TaxPaisa);

    public string GrossLabel => Money.Format(GrossPaisa);

    public string RefundLabel => Money.Format(RefundPaisa);

    /// <summary>The one sentence the owner reads before showing the page to an accountant.</summary>
    public string SettingLabel => TaxEnabled
        ? TaxLabel + " " + PercentLabel + " - switched on in Settings"
        : "Sales tax is switched off in Settings (0%). Only bills that were charged tax appear above.";

    public bool HasRows => Rows.Count > 0;
}
