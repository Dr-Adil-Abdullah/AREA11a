using System.Globalization;
using Area11.Core.Utils;

namespace Area11.Core.Models;

/// <summary>
/// How old a rupee of udhaar is. Every label and every boundary lives here so the screen, the slip
/// and the tests can never disagree about what "60 days" means.
///
/// The four bands are the ones a shopkeeper actually uses when he telephones a customer: a month,
/// two months, three months, and "old".
/// </summary>
public static class Ageing
{
    public const int Bucket1Days = 30;
    public const int Bucket2Days = 60;
    public const int Bucket3Days = 90;

    public static readonly string[] Labels = { "0-30 days", "31-60 days", "61-90 days", "90+ days" };

    /// <summary>The shorter words used as column headings.</summary>
    public static readonly string[] ShortLabels = { "0-30", "31-60", "61-90", "90+" };

    /// <summary>0, 1, 2 or 3. A debt of exactly 30 days is still in the first band.</summary>
    public static int Bucket(int days)
    {
        if (days <= Bucket1Days)
        {
            return 0;
        }

        return days <= Bucket2Days ? 1 : days <= Bucket3Days ? 2 : 3;
    }

    /// <summary>
    /// Whole days between a stored timestamp and today. A timestamp of "2026-10-01 18:20:00" and
    /// "2026-10-01" both count from that day, so a bill taken this morning is 0 days old - not 1.
    /// </summary>
    public static int AgeInDays(string? stamp, DateTime today)
    {
        if (string.IsNullOrWhiteSpace(stamp))
        {
            return 0;
        }

        if (!DateTime.TryParse(stamp, CultureInfo.InvariantCulture, DateTimeStyles.None, out var when))
        {
            return 0;
        }

        var days = (today.Date - when.Date).Days;

        return days < 0 ? 0 : days;
    }

    /// <summary>"today", "1 day", "47 days" - the words next to the oldest bill.</summary>
    public static string DaysLabel(int days) => days <= 0 ? "today" : days == 1 ? "1 day" : days + " days";
}

/// <summary>
/// One customer who still owes money, with his debt split by age.
///
/// The split is real, not an estimate: every payment and every khata-reducing return is applied to
/// the **oldest** unpaid bill first (the way a shopkeeper with a notebook does it). What is left on
/// a bill keeps that bill's date, so "Rs 4,000 in the 90+ column" means four thousand rupees that
/// have been owed for more than three months.
/// </summary>
public sealed class RecoveryRow
{
    public int CustomerId { get; set; }

    public string Name { get; set; } = string.Empty;

    public string? Phone { get; set; }

    public string? Category { get; set; }

    public long BalancePaisa { get; set; }

    public int OldestDays { get; set; }

    public string? OldestBillCode { get; set; }

    public string? LastPaymentDate { get; set; }

    /// <summary>Rupees still owed from bills that are 0-30 / 31-60 / 61-90 / 90+ days old.</summary>
    public long Bucket0Paisa { get; set; }

    public long Bucket1Paisa { get; set; }

    public long Bucket2Paisa { get; set; }

    public long Bucket3Paisa { get; set; }

    /// <summary>Everything older than two months - the list the owner works through first.</summary>
    public long OverduePaisa => Bucket2Paisa + Bucket3Paisa;

    public long BucketsTotalPaisa => Bucket0Paisa + Bucket1Paisa + Bucket2Paisa + Bucket3Paisa;

    /// <summary>
    /// Udhaar the customer already owed when the shop moved to Area11 (F-37, migration 020). It is
    /// part of what he owes, but it has no bill date - so it is **not** aged into a column. Pretending
    /// it is 90 days old would be a guess; pretending it is fresh would hide it.
    /// </summary>
    public long OpeningPaisa { get; set; }

    public bool HasOpening => OpeningPaisa > 0;

    /// <summary>
    /// True when the aged rupees **plus the carried-in udhaar** add up to the stored khata. A false
    /// value is shown in red: it means the customer's balance and his history disagree, and somebody
    /// must look before money is taken.
    /// </summary>
    public bool BooksMatch => BucketsTotalPaisa + OpeningPaisa == BalancePaisa;

    public string BalanceText => Money.Format(BalancePaisa);

    public string OverdueText => Money.Format(OverduePaisa);

    public string Bucket0Text => Money.Format(Bucket0Paisa);

    public string Bucket1Text => Money.Format(Bucket1Paisa);

    public string Bucket2Text => Money.Format(Bucket2Paisa);

    public string Bucket3Text => Money.Format(Bucket3Paisa);

    public string PhoneLabel => string.IsNullOrWhiteSpace(Phone) ? "no phone" : Phone!;

    public string AgeLabel => Ageing.DaysLabel(OldestDays);

    public string OldestLabel => string.IsNullOrWhiteSpace(OldestBillCode)
        ? AgeLabel
        : OldestBillCode + " - " + AgeLabel;

    /// <summary>"old" once the whole balance is past three months - the colour trigger on screen.</summary>
    public string AgeTone => OldestDays <= Ageing.Bucket1Days
        ? "fresh"
        : OldestDays <= Ageing.Bucket2Days ? "waiting" : OldestDays <= Ageing.Bucket3Days ? "late" : "old";

    public string LastPaymentLabel => string.IsNullOrWhiteSpace(LastPaymentDate)
        ? "never paid"
        : LastPaymentDate!.Length >= 10 ? LastPaymentDate![..10] : LastPaymentDate!;

    /// <summary>"Carried in" is said in words, because a rupee with no date must not look like a bill.</summary>
    public string OpeningLabel => HasOpening ? "carried in " + Money.Format(OpeningPaisa) : string.Empty;

    public string Summary => Name + " - " + BalanceText + " (" + AgeLabel + " old)";
}

/// <summary>The whole recovery screen in one object: who owes, how old, and whether the books agree.</summary>
public sealed class RecoveryReport
{
    public List<RecoveryRow> Rows { get; } = new();

    public long Bucket0Paisa { get; set; }

    public long Bucket1Paisa { get; set; }

    public long Bucket2Paisa { get; set; }

    public long Bucket3Paisa { get; set; }

    public int CustomersCount => Rows.Count;

    public long TotalPaisa => Rows.Sum(row => row.BalancePaisa);

    public long OverduePaisa => Bucket2Paisa + Bucket3Paisa;

    public int MismatchCount => Rows.Count(row => !row.BooksMatch);

    public long BucketsTotalPaisa => Bucket0Paisa + Bucket1Paisa + Bucket2Paisa + Bucket3Paisa;

    /// <summary>True when every customer's aged rupees add up to his stored khata.</summary>
    /// <summary>The carried-in udhaar of every customer, added up - shown beside the buckets.</summary>
    public long OpeningTotalPaisa => Rows.Sum(row => row.OpeningPaisa);

    public string OpeningTotalText => Money.Format(OpeningTotalPaisa);

    public bool BooksMatch => MismatchCount == 0 && BucketsTotalPaisa + OpeningTotalPaisa == TotalPaisa;

    public string TotalText => Money.Format(TotalPaisa);

    public string OverdueText => Money.Format(OverduePaisa);

    public string Bucket0Text => Money.Format(Bucket0Paisa);

    public string Bucket1Text => Money.Format(Bucket1Paisa);

    public string Bucket2Text => Money.Format(Bucket2Paisa);

    public string Bucket3Text => Money.Format(Bucket3Paisa);

    public string CountText => CustomersCount == 1 ? "1 customer owes money" : CustomersCount + " customers owe money";
}

/// <summary>
/// The slip handed over when old udhaar is paid (`PAY-nnnn`).
///
/// Why its own model rather than a receipt: the customer is paying an **old** debt, not buying
/// medicine today, so the paper must show three numbers - what he owed before, what he paid now,
/// and what is left. A bill-shaped slip would confuse him and the next cashier.
/// </summary>
public sealed class RecoverySlip
{
    public int PaymentId { get; set; }

    public string Code { get; set; } = string.Empty;

    public string? Date { get; set; }

    public string CustomerName { get; set; } = string.Empty;

    public string? Phone { get; set; }

    public long BalanceBeforePaisa { get; set; }

    public long ReceivedPaisa { get; set; }

    public long BalanceAfterPaisa { get; set; }

    public string Method { get; set; } = "cash";

    public string? Note { get; set; }

    public string? ReceivedBy { get; set; }

    // The bill the money was mainly knocked off, as each and every rupee walked through the oldest
    // debt first. Shown as a line so the customer cannot claim the money was never credited.
    public List<RecoveryHit> Hits { get; } = new();

    public string BeforeText => Money.Format(BalanceBeforePaisa);

    public string ReceivedText => Money.Format(ReceivedPaisa);

    public string AfterText => Money.Format(BalanceAfterPaisa);

    public string MethodLabel => (Method ?? "cash").Trim().ToLowerInvariant() switch
    {
        "online" => "Online",
        "card" => "Card",
        _ => "Cash"
    };

    public bool Cleared => BalanceAfterPaisa <= 0;
}

/// <summary>One bill that the received money was applied to, and how much of it was cleared.</summary>
public sealed class RecoveryHit
{
    public string BillCode { get; set; } = string.Empty;

    public string? BillDate { get; set; }

    public long PaidPaisa { get; set; }

    public long LeftPaisa { get; set; }

    public string PaidText => Money.Format(PaidPaisa);

    public string LeftText => Money.Format(LeftPaisa);

    /// <summary>"INV-0042 - 2026-06-02: Rs 1,000 paid, Rs 500 left".</summary>
    public string Label => BillCode + " - " + (string.IsNullOrWhiteSpace(BillDate) ? "no date" : BillDate![..Math.Min(10, BillDate!.Length)])
                           + ": " + PaidText + " paid, " + LeftText + " left";
}
