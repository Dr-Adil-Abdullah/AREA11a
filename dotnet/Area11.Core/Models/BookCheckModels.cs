using Area11.Core.Utils;

namespace Area11.Core.Models;

/// <summary>
/// One row of the "do the books add up?" answer (F-54).
///
/// Why this is its own small class and not a whole bill: the check never needs to know the rest of
/// the row. It compares one number the row *claims* with one number the shop can *prove* from the
/// rows underneath it, and shows both. A reader who sees "INV-0042: says Rs 540, rows say Rs 535"
/// can act; a screen full of columns cannot.
/// </summary>
public sealed class BookCheckFinding
{
    /// <summary>The document or row this finding is about (`INV-0007`, `PINV-0003`, `PAY-0012`).</summary>
    public string Code { get; init; } = string.Empty;

    /// <summary>What the row says, in paisa. 0 when the rule is not about money.</summary>
    public long ExpectedPaisa { get; init; }

    /// <summary>What the rows underneath add up to, in paisa.</summary>
    public long FoundPaisa { get; init; }

    /// <summary>Expected − found: the rupees that are missing (negative = too much was written).</summary>
    public long DifferencePaisa => ExpectedPaisa - FoundPaisa;

    /// <summary>"Rs 5" - the gap, with its direction spelled out for a shopkeeper.</summary>
    public string GapText => DifferencePaisa == 0
        ? "same"
        : Money.Format(Math.Abs(DifferencePaisa)) + (DifferencePaisa > 0 ? " missing" : " too much");
}

/// <summary>
/// One rule of the check, with the sentence that says what it looked at. The sentence is the
/// point: a rule with no sentence is a number the owner cannot argue with, and the first thing he
/// will ask is "what does 0 mean here?".
/// </summary>
public sealed class BookCheckRule
{
    /// <summary>Short title, e.g. "A bill's lines add up to the bill".</summary>
    public string Title { get; init; } = string.Empty;

    /// <summary>Why the shop should care, in one sentence.</summary>
    public string Why { get; init; } = string.Empty;

    /// <summary>How many rows this rule looked at - the size of the answer, not of the shop.</summary>
    public int Checked { get; init; }

    /// <summary>The rows that did not add up.</summary>
    public List<BookCheckFinding> Findings { get; init; } = new();

    /// <summary>
    /// True when the rule found more than the page shows. The page says "and more" instead of a
    /// number it cannot back up - the count of a broken shop is not the point, the first row is.
    /// </summary>
    public bool Truncated { get; init; }

    public bool Clean => Findings.Count == 0;

    /// <summary>"1,240 bills - all add up" or "1,240 bills - 2 do not".</summary>
    public string Summary => Checked == 0
        ? "nothing to check yet"
        : Clean
            ? Checked.ToString("#,##0") + " checked - all add up"
            : Checked.ToString("#,##0") + " checked - " + Findings.Count + " do not add up"
              + (Truncated ? " (the first " + Findings.Count + " are shown)" : string.Empty);
}

/// <summary>
/// The answer of one run (F-54). Read-only: no command here, and the service has no write.
///
/// The owner's number is <see cref="Problems"/>: **zero means the books add up**. Everything else on
/// the page is the evidence for that one number, in the order of the rules, so a single glance says
/// whether the shop's money is consistent with the rows it was built from.
/// </summary>
public sealed class BookCheckReport
{
    public DateTime RanAt { get; init; }

    /// <summary>Where the database that was read lives - a check is only meaningful about one file.</summary>
    public string DatabaseFile { get; init; } = string.Empty;

    public List<BookCheckRule> Rules { get; init; } = new();

    public int Problems => Rules.Sum(rule => rule.Findings.Count);

    public int RulesChecked => Rules.Count;

    public bool Clean => Problems == 0;

    /// <summary>The line the page prints in its banner.</summary>
    public string Headline => Clean
        ? "Everything adds up: " + RulesChecked + " rule(s), no fault."
        : Problems + " fault(s) found in " + RulesChecked + " rule(s).";

    /// <summary>The sentence that says what to do next - always the same, because the answer is read-only.</summary>
    public string Advice => Clean
        ? "Nothing was changed. Run this again after a busy day, or after an import."
        : "Nothing was changed. Send this page with tools\\SEND-REPORT.cmd; a fault here means a "
          + "number was written by something other than the program, or a bill was edited by hand.";

    /// <summary>
    /// Each DOCUMENT once, with its biggest disagreement.
    ///
    /// Why not simply add every finding: one broken bill is seen by three rules - its lines, its
    /// header and "paid or owed" all disagree with it - so adding them would report a Rs 15 fault
    /// for a Rs 5 gap, and the owner would go looking for money that was never missing. A document
    /// is one amount of money that fails to add up; the rules are three ways of noticing it.
    /// </summary>
    public IReadOnlyList<BookCheckFinding> WorstPerDocument => Rules
        .SelectMany(rule => rule.Findings)
        .GroupBy(finding => finding.Code)
        .Select(group => group.OrderByDescending(finding => Math.Abs(finding.DifferencePaisa)).First())
        .ToList();

    /// <summary>
    /// Every rupee the documents claim and their own rows do not confirm. Two totals, never one:
    /// "missing" and "too much" cancel each other out, and a shop that lost 500 and gained 500 is
    /// not a shop with no problem.
    /// </summary>
    public long MissingPaisa => WorstPerDocument
        .Where(finding => finding.DifferencePaisa > 0)
        .Sum(finding => finding.DifferencePaisa);

    public long TooMuchPaisa => WorstPerDocument
        .Where(finding => finding.DifferencePaisa < 0)
        .Sum(finding => -finding.DifferencePaisa);

    /// <summary>How many documents disagree with their own rows - the number behind the money.</summary>
    public int DocumentsWithFaults => WorstPerDocument.Count;

    /// <summary>"Rs 500 missing, Rs 0 too much" - the line under the headline.</summary>
    public string MoneyText => Clean
        ? "No rupee is unexplained."
        : Money.Format(MissingPaisa) + " missing, " + Money.Format(TooMuchPaisa) + " too much";

    /// <summary>
    /// The file the Save button writes: one line per finding, plus an honest line when there is
    /// nothing to report. Money is written as a plain number so Excel can add the column up -
    /// "Rs 1,234.50" is a picture of money, not money.
    ///
    /// Why a file at all: a fault is not fixed at the counter. It goes to whoever keeps the books -
    /// the accountant, a second person, the supplier - and a list that can be sent is worth more
    /// than a screen that has to be believed.
    /// </summary>
    public string ToCsv()
    {
        var rows = Rules.SelectMany(rule => rule.Findings.Select(finding => new[]
        {
            rule.Title,
            finding.Code,
            Money.Edit(finding.ExpectedPaisa),
            Money.Edit(finding.FoundPaisa),
            Money.Edit(finding.DifferencePaisa),
            finding.GapText
        })).ToList();

        if (rows.Count == 0)
        {
            rows.Add(new[]
            {
                "No fault", "-", "0", "0", "0",
                "Checked " + RulesChecked + " rule(s) at " + RanAt.ToString("yyyy-MM-dd HH:mm") + " - all add up"
            });
        }

        return CsvWriter.Document(
            new[] { "Rule", "Document", "Says", "Rows say", "Gap", "Gap in words" },
            rows);
    }
}
