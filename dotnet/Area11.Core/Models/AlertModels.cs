namespace Area11.Core.Models;

/// <summary>
/// One alert on the board. Alerts are never deleted: an owner ends an episode with a written
/// reason, and the reason stays in the history for good (U-31).
/// </summary>
public sealed class AlertItem
{
    /// <summary>Stable key: `negstock:12` or `quarantine:7`. The dismissal table uses it.</summary>
    public string Key { get; set; } = string.Empty;

    /// <summary>negstock | quarantine | provisional | provisional_linked | supplier_cutoff</summary>
    public string Type { get; set; } = "negstock";

    public int EntityId { get; set; }

    public string Title { get; set; } = string.Empty;

    public string Detail { get; set; } = string.Empty;

    /// <summary>True = the owner ended this episode with a reason; it is listed but not "live".</summary>
    public bool Ended { get; set; }

    public string? Reason { get; set; }

    public string? EndedBy { get; set; }

    public string? EndedAt { get; set; }

    /// <summary>How many are in that state, when several rows were folded into one alert.</summary>
    public int Count { get; set; } = 1;

    /// <summary>Red = money or stock is wrong right now. Amber = waiting for the owner's word.</summary>
    public bool IsCritical => Type is "negstock" or "provisional" or "expired";

    /// <summary>Amber = nothing is wrong yet, but a decision has a deadline (a bill to confirm, a
    /// supplier who will stop accepting the goods, a shelf that is running out).</summary>
    public bool IsAmber => Type is "provisional_linked" or "supplier_cutoff" or "quarantine"
        or "lowstock" or "shift_open";

    /// <summary>
    /// The two kinds of alert the owner asked for (D-50).
    ///
    /// **A reminder** is small and safe to put away: the drawer was left open overnight, or a bill is
    /// attached and only needs a confirming word. Any signed-in person may close it - and closing it
    /// only hides the reminder; the shift or the bill itself does not change.
    ///
    /// **A problem** is money or stock that is wrong now (minus stock, money refunded with no bill,
    /// expired goods, a shelf below its reorder level). Only the owner ends it, and only with a
    /// written reason (U-31). The service refuses the light door for these keys.
    /// </summary>
    public bool IsLight => IsReminderType(Type);

    public bool IsSerious => !IsLight;

    /// <summary>One list, used by the model AND by AlertService, so the two can never disagree.</summary>
    public static bool IsReminderType(string? type) =>
        type is "shift_open" or "provisional_linked";

    public string TypeLabel => Type switch
    {
        "negstock" => "Stock is MINUS",
        "quarantine" => "In quarantine",
        "provisional" => "No bill - refund paid",
        "provisional_linked" => "Bill attached - confirm",
        "supplier_cutoff" => "Return to supplier",
        "lowstock" => "Running out",
        "expired" => "Expired on the shelf",
        "shift_open" => "Drawer left open",
        _ => Type
    };
}

/// <summary>The whole board: what is live now, and what the owner already ended.</summary>
public sealed class AlertReport
{
    public List<AlertItem> Live { get; init; } = new();

    public List<AlertItem> Ended { get; init; } = new();

    public int NegativeCount => Live.Count(alert => alert.Type == "negstock");

    public int QuarantineCount => Live.Count(alert => alert.Type == "quarantine");

    public int ProvisionalCount => Live.Count(alert => alert.Type == "provisional");

    public int LinkedCount => Live.Count(alert => alert.Type == "provisional_linked");

    public int CutoffCount => Live.Count(alert => alert.Type == "supplier_cutoff");

    public int LiveCount => Live.Count;

    public bool HasLive => Live.Count > 0;

    /// <summary>The two lists the bell shows: what needs the owner, and what anyone may put away.</summary>
    public List<AlertItem> Serious => Live.Where(alert => alert.IsSerious).ToList();

    public List<AlertItem> Light => Live.Where(alert => alert.IsLight).ToList();

    public int SeriousCount => Serious.Count;

    public int LightCount => Light.Count;

    public int LowStockCount => Live.Count(alert => alert.Type == "lowstock");

    public int ExpiredCount => Live.Count(alert => alert.Type == "expired");

    public int ShiftOpenCount => Live.Count(alert => alert.Type == "shift_open");

    /// <summary>The bell's tooltip sentence: "3 need the owner · 1 reminder".</summary>
    public string BellText
    {
        get
        {
            if (Live.Count == 0)
            {
                return "Nothing needs attention.";
            }

            var parts = new List<string>();

            if (SeriousCount > 0)
            {
                parts.Add(SeriousCount + (SeriousCount == 1 ? " needs the owner" : " need the owner"));
            }

            if (LightCount > 0)
            {
                parts.Add(LightCount + (LightCount == 1 ? " reminder" : " reminders"));
            }

            return string.Join(" · ", parts);
        }
    }

    /// <summary>
    /// The board's headline: one sentence that names every live problem, in the order the owner
    /// cares about (money wrong now, then deadlines). It used to be the red bar across every page;
    /// the bar is gone (D-50) and the sentence now sits at the top of the bell panel and the board,
    /// so it must read on its own - no "click here", no arrows.
    /// </summary>
    public string SummaryText
    {
        get
        {
            if (Live.Count == 0)
            {
                return string.Empty;
            }

            var parts = new List<string>();

            if (NegativeCount > 0)
            {
                parts.Add($"Stock is MINUS - {NegativeCount} medicine(s)");
            }

            if (QuarantineCount > 0)
            {
                parts.Add($"{QuarantineCount} item(s) in quarantine");
            }

            if (ProvisionalCount > 0)
            {
                parts.Add($"{ProvisionalCount} no-bill refund(s) missing the bill");
            }

            if (LinkedCount > 0)
            {
                parts.Add($"{LinkedCount} bill(s) attached - your confirmation pending");
            }

            if (ExpiredCount > 0)
            {
                parts.Add($"{ExpiredCount} batch(es) already expired on the shelf");
            }

            if (LowStockCount > 0)
            {
                parts.Add($"{LowStockCount} shelf(es) below the reorder level");
            }

            // Last, because it is a deadline and not a fault: the goods are fine today, but after
            // the cutoff the supplier may refuse them.
            if (CutoffCount > 0)
            {
                parts.Add($"{CutoffCount} batch(es) near the supplier return cutoff");
            }

            return string.Join(" · ", parts);
        }
    }
}
