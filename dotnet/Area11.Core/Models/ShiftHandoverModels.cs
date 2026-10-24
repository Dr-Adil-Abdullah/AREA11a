using Area11.Core.Utils;

namespace Area11.Core.Models;

/// <summary>
/// The drawer changing hands (Spec 14.4 and 18.2, F-55). One galla, two people: the first counts
/// the cash, the second starts the next shift with exactly that money.
///
/// This is NOT two separate actions. Closing the shift and opening the next one must happen inside
/// one transaction, because a drawer that is closed but not reopened loses the link between the two
/// people, and a drawer reopened with a typed float hides the difference. Here the counted money
/// **is** the next float, and the two shifts point at each other for ever
/// (`handed_to_user_id` / `opened_from_shift_id`, both in migration 009).
///
/// The difference belongs to the shift that was counted - not to the person who takes over. If the
/// first cashier was Rs 50 short, that shortfall is written down against him now; the next shift
/// starts clean at the counted figure and is never asked to explain somebody else's money.
/// </summary>
public sealed class ShiftHandover
{
    /// <summary>The shift that was counted and closed by this handover.</summary>
    public ShiftMoney From { get; set; } = new();

    /// <summary>The shift that starts with the counted cash.</summary>
    public ShiftMoney To { get; set; } = new();

    public string GivenBy { get; set; } = string.Empty;

    public string GivenTo { get; set; } = string.Empty;

    /// <summary>When the drawer changed hands - the moment the second shift starts.</summary>
    public string GivenAt { get; set; } = string.Empty;

    /// <summary>What the first person counted. This is also the second person's opening float.</summary>
    public long CountedPaisa => From.ActualPaisa ?? 0;

    public string CountedLabel => Money.Format(CountedPaisa);

    public string ExpectedLabel => From.ExpectedLabel;

    /// <summary>Exact / over / short - the same sentence the close screen shows.</summary>
    public string DifferenceLabel => From.DifferenceLabel;

    public bool IsShort => From.IsShort;

    public bool IsExact => From.IsExact;

    /// <summary>One line for the status bar under the buttons.</summary>
    public string Sentence =>
        CountedLabel + " handed from " + GivenBy + " to " + GivenTo + " - " + DifferenceLabel + ".";

    public string GivenAtLabel => GivenAt.Length >= 16 ? GivenAt[..16] : GivenAt;

    /// <summary>The two people, as one line: "Owner -> Bilal".</summary>
    public string Pair => GivenBy + " -> " + GivenTo;
}
