using Area11.Core.Utils;

namespace Area11.Core.Models;

/// <summary>
/// The words the shop uses for a drawer's difference, in one place (F-31, F-56).
///
/// "exact", "over by Rs 50", "short by Rs 120" is a sentence the owner reads on the shift page,
/// on the handover slip and in the per-person report. Writing it three times would let the three
/// drift, and the one that drifted would be the one he trusted.
/// </summary>
public static class ShiftWords
{
    public static string Difference(long? paisa) => paisa switch
    {
        null => "-",
        0L => "exact",
        > 0 => "over by " + Money.Format(paisa.Value),
        _ => "short by " + Money.Format(-paisa.Value)
    };

    /// <summary>"3 short, 1 over" - how often it happened, when it did at all.</summary>
    public static string Counts(int shortCount, int overCount, int exactCount)
    {
        var parts = new List<string>();

        // "1 short" reads correctly in English, so the number is never special-cased into "short".
        if (shortCount > 0)
        {
            parts.Add(shortCount + " short");
        }

        if (overCount > 0)
        {
            parts.Add(overCount + " over");
        }

        if (parts.Count > 0)
        {
            return string.Join(", ", parts);
        }

        return exactCount == 0 ? "-" : exactCount + " exact";
    }
}

/// <summary>
/// One shift (galla) - from the moment the drawer is opened with a float until it is counted and
/// closed (F-31, Spec 14). The six money lines are read from the history, never typed by hand:
///
///   in  : the float the drawer started with, cash bills, khata money received, cash a supplier
///         handed back for goods;
///   out : cash refunds to customers, money paid to suppliers (including the amount paid at the
///         moment of a purchase), **shop expenses** and **cash the owner took** (F-32).
///
/// Cards and online money are **not** here: they never entered the drawer.
/// </summary>
public sealed class ShiftMoney
{
    public int Id { get; set; }

    /// <summary>Who opened the drawer - only that person (or a manager) may close it.</summary>
    public int UserId { get; set; }

    public string OpenedAt { get; set; } = string.Empty;

    public string OpenedBy { get; set; } = string.Empty;

    public long OpeningFloatPaisa { get; set; }

    public long CashSalesPaisa { get; set; }

    public long CashCollectionsPaisa { get; set; }

    public long SupplierCashInPaisa { get; set; }

    public long SupplierCashOutPaisa { get; set; }

    public long CashRefundsPaisa { get; set; }

    /// <summary>Rent, electricity, tea, a repair - money that left the till (F-32).</summary>
    public long ExpensesPaisa { get; set; }

    /// <summary>Cash the owner took for himself. A goods drawing is NOT here: no money moved.</summary>
    public long DrawingsPaisa { get; set; }

    /// <summary>What the drawer should hold: float + everything in - everything out.</summary>
    public long ExpectedPaisa { get; set; }

    /// <summary>What the owner counted. Null while the shift is still open.</summary>
    public long? ActualPaisa { get; set; }

    /// <summary>actual - expected. Positive = extra money, negative = short.</summary>
    public long? DifferencePaisa { get; set; }

    public string Status { get; set; } = "open";

    public string? Note { get; set; }

    /// <summary>The person this drawer was handed to (migration 009). Null when it was simply closed.</summary>
    public string? HandedToName { get; set; }

    /// <summary>The shift this one was opened from - the other half of a handover (migration 009).</summary>
    public int? OpenedFromShiftId { get; set; }

    /// <summary>True when this shift ended by changing hands rather than by closing the shop.</summary>
    public bool WasHandedOver => !string.IsNullOrWhiteSpace(HandedToName);

    /// <summary>"Handed to Bilal" / "Came from drawer 12" - the two halves of the chain, in words.</summary>
    public string HandoverLabel
    {
        get
        {
            var parts = new List<string>();

            if (WasHandedOver)
            {
                parts.Add("handed to " + HandedToName!.Trim());
            }

            if (OpenedFromShiftId is int from)
            {
                parts.Add("came from drawer " + from);
            }

            return parts.Count == 0 ? "-" : string.Join(", ", parts);
        }
    }

    public bool IsOpen => Status == "open";

    public string StatusLabel => IsOpen ? "Open" : "Closed";

    public long MoneyInPaisa =>
        OpeningFloatPaisa + CashSalesPaisa + CashCollectionsPaisa + SupplierCashInPaisa;

    public long MoneyOutPaisa => CashRefundsPaisa + SupplierCashOutPaisa + ExpensesPaisa + DrawingsPaisa;

    public string OpenedLabel => OpenedAt.Length >= 16 ? OpenedAt[..16] : OpenedAt;

    public string FloatLabel => Money.Format(OpeningFloatPaisa);

    public string CashSalesLabel => Money.Format(CashSalesPaisa);

    public string CollectionsLabel => Money.Format(CashCollectionsPaisa);

    public string SupplierInLabel => Money.Format(SupplierCashInPaisa);

    public string SupplierOutLabel => Money.Format(SupplierCashOutPaisa);

    public string RefundsLabel => Money.Format(CashRefundsPaisa);

    public string ExpensesLabel => Money.Format(ExpensesPaisa);

    public string DrawingsLabel => Money.Format(DrawingsPaisa);

    public string ExpectedLabel => Money.Format(ExpectedPaisa);

    public string ActualLabel => ActualPaisa is long actual ? Money.Format(actual) : "-";

    /// <summary>The sentence the owner reads before he signs the day off: exact, over, or short.</summary>
    public string DifferenceLabel => ShiftWords.Difference(DifferencePaisa);

    public bool IsShort => DifferencePaisa < 0;

    public bool IsExact => DifferencePaisa == 0;

    /// <summary>Sidebar line: "Opened 21 Oct 09:00 by Owner".</summary>
    public string HeaderLabel => IsOpen
        ? "Opened " + OpenedLabel + " by " + OpenedBy
        : "Closed - opened " + OpenedLabel + " by " + OpenedBy;

    public string NoteLabel => string.IsNullOrWhiteSpace(Note) ? "-" : Note!;
}


/// <summary>
/// One person's record of the drawer over a window (F-56, Spec 7.2.7 - *"a per-user shift report"*,
/// which the original spec listed as not built).
///
/// Why this is a money feature and not a chart: the difference on a shift is written against the
/// person who counted it, and **the only way a shop notices a pattern is to add the differences of
/// one person over time**. A single Rs 20 short is noise; twelve of them is a drawer that is being
/// opened between counts. The report never blames anybody - it repeats what the shift rows already
/// say, in the order a shopkeeper asks it: who, how many shifts, how much was expected, how much
/// was counted, and how far apart those two numbers were.
/// </summary>
public sealed class ShiftPersonSummary
{
    public int UserId { get; set; }

    /// <summary>The name on the shift rows; "(removed)" when the staff row itself was deleted.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>How many closed drawers this person counted in the window.</summary>
    public int ClosedShifts { get; set; }

    public long ExpectedPaisa { get; set; }

    public long CountedPaisa { get; set; }

    /// <summary>Counted − expected over the whole window: positive means extra money, negative means short.</summary>
    public long DifferencePaisa { get; set; }

    public int ShortCount { get; set; }

    public int OverCount { get; set; }

    public int ExactCount { get; set; }

    /// <summary>The most short in one count, as a positive number (0 when never short).</summary>
    public long WorstShortPaisa { get; set; }

    /// <summary>The most over in one count (0 when never over).</summary>
    public long WorstOverPaisa { get; set; }

    public string ShiftsLabel => ClosedShifts + (ClosedShifts == 1 ? " shift" : " shifts");

    public string ExpectedLabel => Money.Format(ExpectedPaisa);

    public string CountedLabel => Money.Format(CountedPaisa);

    public string DifferenceLabel => ShiftWords.Difference(DifferencePaisa);

    /// <summary>"3 short, 1 over" - the pattern, when there is one. "-" when every count was exact.</summary>
    public string PatternLabel => ShiftWords.Counts(ShortCount, OverCount, ExactCount);

    /// <summary>"Rs 120" - the worst single shortfall, or "-" when there was none.</summary>
    public string WorstShortLabel => WorstShortPaisa > 0 ? Money.Format(WorstShortPaisa) : "-";

    /// <summary>"Rs 30" - the biggest extra ever found in that person's drawer.</summary>
    public string WorstOverLabel => WorstOverPaisa > 0 ? Money.Format(WorstOverPaisa) : "-";

    public bool IsShort => DifferencePaisa < 0;

    public bool IsClean => DifferencePaisa == 0;
}

/// <summary>
/// Everybody's drawers over one window, with the shop's own totals (F-56).
///
/// The totals are kept apart the same way the books check keeps them (F-54): **short and over are
/// two numbers, never one**. A shop that was short Rs 500 on one counter and over Rs 500 on another
/// is not a shop with no problem - it is a shop where one drawer is leaking and another is holding
/// the leak. A single net figure would hide exactly the thing the owner is looking for.
/// </summary>
public sealed class ShiftPersonReport
{
    /// <summary>One row per person, worst net difference first - the person who needs a word is at the top.</summary>
    public List<ShiftPersonSummary> People { get; set; } = new();

    public int Days { get; set; } = 30;

    public int ClosedShifts { get; set; }

    public long ExpectedPaisa { get; set; }

    public long CountedPaisa { get; set; }

    /// <summary>Counted − expected over the whole shop. Positive = more cash than the bills explain.</summary>
    public long DifferencePaisa { get; set; }

    /// <summary>Everything the shop was short, added up as a positive number.</summary>
    public long MissingPaisa { get; set; }

    /// <summary>Everything the shop was over, added up as a positive number. Never netted off.</summary>
    public long TooMuchPaisa { get; set; }

    public bool AnyShifts => ClosedShifts > 0;

    /// <summary>How many people counted a drawer in the window.</summary>
    public int PeopleCount => People.Count;

    public string WindowLabel => "Last " + Days + " days";

    public string ExpectedLabel => Money.Format(ExpectedPaisa);

    public string CountedLabel => Money.Format(CountedPaisa);

    public string DifferenceLabel => ShiftWords.Difference(DifferencePaisa);

    /// <summary>
    /// The sentence under the table. It says what was looked at and what it found, in that order,
    /// because "0 people" and "everybody exact" look identical in a table and are not the same news.
    /// </summary>
    public string Sentence
    {
        get
        {
            if (!AnyShifts)
            {
                return "No drawer has been counted in the " + Days + " days - nothing to compare yet.";
            }

            var people = PeopleCount == 1 ? "1 person" : PeopleCount + " people";
            var shifts = ClosedShifts == 1 ? "1 closed shift" : ClosedShifts + " closed shifts";

            var money = MissingPaisa == 0 && TooMuchPaisa == 0
                ? "every count was exact."
                : Money.Format(MissingPaisa) + " short, " + Money.Format(TooMuchPaisa) + " over.";

            return WindowLabel + ": " + shifts + " counted by " + people + " - " + money;
        }
    }
}
