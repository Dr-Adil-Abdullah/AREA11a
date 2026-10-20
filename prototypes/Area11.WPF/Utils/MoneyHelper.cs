using System.Globalization;

namespace Area11.WPF.Utils;

/// <summary>All money lives in integer paisa so rounding can never lose a rupee.</summary>
public static class MoneyHelper
{
    public static long ToPaisa(decimal rupees) => (long)Math.Round(rupees * 100m, MidpointRounding.AwayFromZero);

    public static decimal ToRupees(long paisa) => paisa / 100m;

    public static bool TryParseRupees(string? text, out decimal rupees)
    {
        rupees = 0m;
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        return decimal.TryParse(text, NumberStyles.Number, CultureInfo.CurrentCulture, out rupees)
               || decimal.TryParse(text, NumberStyles.Number, CultureInfo.InvariantCulture, out rupees);
    }

    public static long ParseRupeesOrZero(string? text) => TryParseRupees(text, out var rupees) ? ToPaisa(rupees) : 0L;

    /// <summary>
    /// Floors a bill to the nearest step (the owner's rule: always in the customer's favour),
    /// but never below the purchase cost — protecting margin is more important than a tidy number.
    /// </summary>
    public static long ApplyRoundDownWithCostGuard(long totalPaisa, long costPaisa, int roundToRupees)
    {
        var step = roundToRupees * 100L;
        if (step <= 0 || totalPaisa <= 0)
        {
            return totalPaisa;
        }

        var rounded = totalPaisa / step * step;
        return rounded < costPaisa ? totalPaisa : rounded;
    }
}
