using Area11.Core.Utils;

namespace Area11.Core.Models;

/// <summary>
/// The day a medical rep visits (Spec 8.3.3). Stored as 1 = Monday ... 7 = Sunday so the list sorts
/// by the working week, and named here so the screen and the database can never disagree.
/// </summary>
public static class VisitDays
{
    public const int None = 0;

    public static readonly int[] All = { 1, 2, 3, 4, 5, 6, 7 };

    public static bool IsKnown(int? day) => day is null or None || (day >= 1 && day <= 7);

    /// <summary>"Monday" / "no fixed day" - what the owner reads in the table and the drop-down.</summary>
    public static string Label(int? day) => day switch
    {
        1 => "Monday",
        2 => "Tuesday",
        3 => "Wednesday",
        4 => "Thursday",
        5 => "Friday",
        6 => "Saturday",
        7 => "Sunday",
        _ => "no fixed day"
    };

    /// <summary>The same list the drop-down shows: a nullable id and the word.</summary>
    public static List<VisitDayOption> Options()
    {
        var options = new List<VisitDayOption> { new(None, Label(None)) };

        foreach (var day in All)
        {
            options.Add(new VisitDayOption(day, Label(day)));
        }

        return options;
    }
}

/// <summary>One row of the visit-day drop-down.</summary>
public sealed class VisitDayOption
{
    public VisitDayOption(int day, string label)
    {
        Day = day;
        Label = label;
    }

    /// <summary>0 = no fixed day.</summary>
    public int Day { get; }

    public string Label { get; }
}

/// <summary>One company with its rep, as the Doctors &amp; reps page shows it.</summary>
public sealed class RepCompanyRow
{
    public int Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public string? RepName { get; set; }

    public string? RepPhone { get; set; }

    /// <summary>1 = Monday ... 7 = Sunday; null when the company has no fixed visit day.</summary>
    public int? VisitDay { get; set; }

    public bool HasRep => !string.IsNullOrWhiteSpace(RepName);

    public string RepLabel => HasRep ? RepName!.Trim() : "no rep yet";

    public string PhoneLabel => string.IsNullOrWhiteSpace(RepPhone) ? "-" : RepPhone!.Trim();

    public string VisitLabel => VisitDays.Label(VisitDay);

    /// <summary>"GSK · Imran · Monday" - one line for the counter's tooltip and the black box.</summary>
    public string FullLabel => Name + " · " + RepLabel + " · " + VisitLabel;
}

/// <summary>
/// One doctor (a customer with category 'doctor') and what his prescriptions earned (Spec 17.7).
/// Profit is masked when the person may not see money: <see cref="ProfitKnown"/> turns the number
/// into a dash instead of a lie.
/// </summary>
public sealed class DoctorRow
{
    public int DoctorId { get; set; }

    public string Name { get; set; } = string.Empty;

    public string? Phone { get; set; }

    public int Bills { get; set; }

    public long SalesPaisa { get; set; }

    public long ProfitPaisa { get; set; }

    public string? LastBill { get; set; }

    /// <summary>False for a manager without the profit permission.</summary>
    public bool ProfitKnown { get; set; } = true;

    public string SalesText => Money.Format(SalesPaisa);

    public string ProfitText => ProfitKnown ? Money.Format(ProfitPaisa) : "—";

    public string BillsText => Bills == 1 ? "1 bill" : Bills + " bills";

    public string PhoneLabel => string.IsNullOrWhiteSpace(Phone) ? "-" : Phone!.Trim();

    public string LastBillLabel => string.IsNullOrWhiteSpace(LastBill)
        ? "never"
        : LastBill![..Math.Min(10, LastBill!.Length)];
}

/// <summary>One customer and the profit his bills have earned over all time (Spec 17.5).</summary>
public sealed class CustomerProfitRow
{
    public int CustomerId { get; set; }

    public string Name { get; set; } = string.Empty;

    public string? Phone { get; set; }

    public string? Category { get; set; }

    public int Bills { get; set; }

    public long SalesPaisa { get; set; }

    public long ProfitPaisa { get; set; }

    public string? LastBill { get; set; }

    public bool ProfitKnown { get; set; } = true;

    public string SalesText => Money.Format(SalesPaisa);

    public string ProfitText => ProfitKnown ? Money.Format(ProfitPaisa) : "—";

    public string CategoryLabel => string.IsNullOrWhiteSpace(Category) ? "normal" : Category!.Trim();

    public string PhoneLabel => string.IsNullOrWhiteSpace(Phone) ? "-" : Phone!.Trim();

    public string BillsText => Bills == 1 ? "1 bill" : Bills + " bills";
}
