using System.Globalization;

namespace Area11.Core.Models;

/// <summary>
/// What the sample shop (F-53) will put into this database, in the owner's words.
///
/// Why a report and not just a button: filling a shop is the one action that writes into every
/// table at once, and the owner has to see first what he is about to get - and whether the program
/// is willing to do it at all in *this* database.
/// </summary>
public sealed class SampleShopReport
{
    public List<string> Steps { get; } = new();

    /// <summary>Why it cannot run here. Empty means the button is live.</summary>
    public List<string> Problems { get; } = new();

    public bool CanSeed => Problems.Count == 0;

    public string TitleLabel => CanSeed ? "Ready to fill this shop" : "This shop cannot take sample data";

    public string SubtitleLabel => CanSeed
        ? "Everything below is invented: names, bills, khata and stock. Nothing real is touched because this shop has no bills yet."
        : "The program refuses rather than risk one real bill. Read the reasons below.";

    public string ProblemText => Problems.Count == 0 ? string.Empty : string.Join(" ", Problems);

    public string StepsText => Steps.Count == 0 ? "Nothing to show." : string.Join("\n", Steps);

    /// <summary>"3 purchases, 12 bills, 2 returns, ..." - one line for the footer.</summary>
    public string SummaryLabel
    {
        get
        {
            if (Steps.Count == 0)
            {
                return "Nothing will be written.";
            }

            var total = 0;

            foreach (var step in Steps)
            {
                if (int.TryParse(step.Split(' ')[0], NumberStyles.Integer, CultureInfo.InvariantCulture,
                        out var count))
                {
                    total += count;
                }
            }

            return total == 0 ? "Ready." : total.ToString(CultureInfo.InvariantCulture) + " things in all.";
        }
    }
}
