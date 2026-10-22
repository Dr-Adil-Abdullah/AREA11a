using System.Globalization;

namespace Area11.Core.Models;

/// <summary>
/// One line of the shop's hardware test (F-46, Spec 33.3.4): a claim the owner checks with his own
/// eyes on his own printer, and ticks when the paper really came out right.
///
/// Why a checklist and not a "printer works" switch: a thermal printer can print half a bill, cut
/// the paper in the wrong place, or lose its last character to a wrong paper width - and none of
/// those is visible from inside the program. The person standing at the printer is the only witness,
/// so the program asks him and keeps his answer.
/// </summary>
public sealed class HardwareCheck
{
    public HardwareCheck(string key, string text, string hint)
    {
        Key = key;
        Text = text;
        Hint = hint;
    }

    /// <summary>Stable name ("printed", "fits", ...) - the box that is ticked is stored by this, not by its position.</summary>
    public string Key { get; }

    public string Text { get; }

    /// <summary>What to do when it fails - shown under the line, in small print.</summary>
    public string Hint { get; }

    public bool Passed { get; set; }
}

/// <summary>
/// What this PC's printer and scanner were found to do, and who said so (F-46).
///
/// The report is built fresh from Settings every time (width, printer, copies, cut) and the saved
/// answer is laid over it, so a shop that changes its paper from 58 to 80 mm sees the old ticks with
/// a new width - and knows the slip has to be printed again.
/// </summary>
public sealed class HardwareReport
{
    public int WidthMm { get; set; } = 58;

    /// <summary>32 characters on 58 mm paper, 48 on 80 mm - the number the ruler fills.</summary>
    public int Columns { get; set; } = 32;

    public string PrinterName { get; set; } = string.Empty;

    public int Copies { get; set; } = 1;

    public bool AutoCut { get; set; } = true;

    public List<HardwareCheck> Checks { get; } = new();

    /// <summary>The owner's own words when something did not pass.</summary>
    public string? Note { get; set; }

    /// <summary>When the test was recorded ("2026-10-22 17:40"), null when it never was.</summary>
    public string? CheckedAt { get; set; }

    public string? CheckedBy { get; set; }

    /// <summary>The width the saved tick-boxes belong to; a different width means "print it again".</summary>
    public int CheckedWidthMm { get; set; }

    public bool HasRecord => !string.IsNullOrWhiteSpace(CheckedAt);

    public int PassedCount => Checks.Count(check => check.Passed);

    public int CheckCount => Checks.Count;

    public bool IsComplete => Checks.Count > 0 && Checks.All(check => check.Passed);

    /// <summary>"58 mm - 32 characters": what the paper is, in the words the owner uses.</summary>
    public string WidthLabel =>
        WidthMm.ToString(CultureInfo.InvariantCulture) + " mm - "
        + Columns.ToString(CultureInfo.InvariantCulture) + " characters";

    public string PrinterLabel =>
        string.IsNullOrWhiteSpace(PrinterName) ? "Windows default printer" : PrinterName.Trim();

    public string ProgressLabel =>
        PassedCount.ToString(CultureInfo.InvariantCulture) + " of "
        + CheckCount.ToString(CultureInfo.InvariantCulture) + " passed";

    public string LastRunLabel => !HasRecord
        ? "never checked on this PC"
        : "checked " + CheckedAt + " by " + (CheckedBy ?? "someone");

    /// <summary>
    /// True when the saved record was written for the paper width now in Settings. A shop that
    /// changed its roll must print the slip again - the ticks no longer describe this printer.
    /// </summary>
    public bool RecordMatchesWidth => !HasRecord || CheckedWidthMm == WidthMm;

    public string MissingLabel => string.Join("; ",
        Checks.Where(check => !check.Passed).Select(check => check.Text));

    /// <summary>One sentence for the page, the audit line and the owner's report to me.</summary>
    public string SummaryText
    {
        get
        {
            var head = "Printer and scanner test: " + ProgressLabel + " on " + WidthLabel
                       + " with " + PrinterLabel + ".";

            if (!HasRecord)
            {
                return head + " Nothing recorded yet.";
            }

            var tail = IsComplete
                ? " All done on " + CheckedAt + "."
                : " Still missing: " + MissingLabel + ".";

            return head + tail + (string.IsNullOrWhiteSpace(Note) ? string.Empty : " Note: " + Note!.Trim());
        }
    }
}
