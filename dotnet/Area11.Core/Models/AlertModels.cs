namespace Area11.Core.Models;

/// <summary>
/// One alert on the board. Alerts are never deleted: an owner ends an episode with a written
/// reason, and the reason stays in the history for good (U-31).
/// </summary>
public sealed class AlertItem
{
    /// <summary>Stable key: `negstock:12` or `quarantine:7`. The dismissal table uses it.</summary>
    public string Key { get; set; } = string.Empty;

    /// <summary>negstock | quarantine</summary>
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
    public bool IsCritical => Type == "negstock" || Type == "provisional";

    public bool IsAmber => Type == "provisional_linked";

    public string TypeLabel => Type switch
    {
        "negstock" => "Stock is MINUS",
        "quarantine" => "In quarantine",
        "provisional" => "No bill - refund paid",
        "provisional_linked" => "Bill attached - confirm",
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

    public int LiveCount => Live.Count;

    public bool HasLive => Live.Count > 0;

    /// <summary>
    /// The sentence in the red bar at the top of every page. Short on purpose: it must be readable
    /// from across the counter, and the detail is one click away.
    /// </summary>
    public string BarText
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

            return "⚠ " + string.Join(" · ", parts) + " · See now";
        }
    }
}
