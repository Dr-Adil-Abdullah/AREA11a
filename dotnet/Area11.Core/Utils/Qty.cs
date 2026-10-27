using System.Globalization;

namespace Area11.Core.Utils;

/// <summary>
/// Quantities are stored as REAL in the reference schema, so the Core uses double and never
/// decimal (Microsoft.Data.Sqlite would store a decimal as TEXT and break SUM/comparisons).
/// </summary>
public static class Qty
{
    public static bool TryParse(string? text, out double value)
    {
        value = 0;

        var clean = (text ?? string.Empty).Trim().Replace(",", string.Empty, StringComparison.Ordinal);
        if (clean.Length == 0)
        {
            return true;
        }

        return double.TryParse(clean, NumberStyles.Float, CultureInfo.InvariantCulture, out value);
    }

    public static string Edit(double value) => value.ToString("0.###", CultureInfo.InvariantCulture);

    public static string Format(double value) => value.ToString("#,##0.###", CultureInfo.InvariantCulture);

    public static bool NearlyZero(double value) => Math.Abs(value) < 0.0005;

    /// <summary>True only for a finite whole base-unit quantity; REAL storage remains unchanged.</summary>
    public static bool IsWhole(double value) =>
        double.IsFinite(value) && Math.Abs(value - Math.Truncate(value)) < 0.0000001;
}
