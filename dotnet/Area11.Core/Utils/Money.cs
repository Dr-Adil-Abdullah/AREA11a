using System.Globalization;

namespace Area11.Core.Utils;

/// <summary>
/// Money is always integer paisa (Spec 5.1.1). Rounds are never applied here silently:
/// <see cref="RoundDownWithCostGuard"/> implements the owner's rule Z-03 / Z-11 exactly.
/// </summary>
public static class Money
{
    public static long ToPaisa(decimal rupees) =>
        checked((long)Math.Round(rupees * 100m, MidpointRounding.AwayFromZero));

    public static decimal ToRupees(long paisa) => paisa / 100m;

    /// <summary>"Rs 1,234.50" — for screens and receipts.</summary>
    public static string Format(long paisa) =>
        "Rs " + ToRupees(paisa).ToString("#,##0.##", CultureInfo.InvariantCulture);

    /// <summary>"1234.50" — for text boxes.</summary>
    public static string Edit(long paisa) =>
        ToRupees(paisa).ToString("0.##", CultureInfo.InvariantCulture);

    /// <summary>Accepts "545", "545.50", "1,545", "Rs 545" and an empty string as zero.</summary>
    public static bool TryParse(string? text, out decimal rupees)
    {
        rupees = 0m;
        var value = (text ?? string.Empty).Trim();
        if (value.Length == 0)
        {
            return true;
        }

        if (value.StartsWith("Rs", StringComparison.OrdinalIgnoreCase))
        {
            value = value[2..].Trim();
        }

        value = value.Replace(",", string.Empty, StringComparison.Ordinal);

        return decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out rupees);
    }

    public static long ParseOrZero(string? text) => TryParse(text, out var rupees) ? ToPaisa(rupees) : 0L;

    /// <summary>
    /// Round the bill DOWN to a multiple of <paramref name="stepRupees"/> (always in the
    /// customer's favour), but never below the purchase cost of the items.
    /// 545 with cost 543 stays 545; 545 with cost 500 becomes 540 (Spec 11.3.2).
    /// </summary>
    public static long RoundDownWithCostGuard(long totalPaisa, long costPaisa, int stepRupees)
    {
        if (stepRupees <= 0 || totalPaisa <= 0)
        {
            return totalPaisa;
        }

        var step = stepRupees * 100L;
        var rounded = totalPaisa / step * step;

        return rounded < costPaisa ? totalPaisa : rounded;
    }
}
