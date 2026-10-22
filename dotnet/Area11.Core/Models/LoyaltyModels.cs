using Area11.Core.Utils;

namespace Area11.Core.Models;

/// <summary>
/// Loyalty, in one place (F-40, Spec 13.6 &amp; 17.6): rupees spent → points → stars → a title the
/// cashier can say out loud.
///
/// **Off by default** (`loyalty.enabled`, decision Q-06). When the owner switches it on:
///   • the customer **earns** one point per `loyalty.rupeesPerPoint` rupees of the money the bill
///     actually costs him (after a points rebound), never on the part that went on the khata;
///   • he can **spend** points at the counter: **1 point = Rs 1**, capped by what he has, by the bill,
///     and - when the shop refuses to sell below cost - by the cost of the goods;
///   • stars are 1-5 and the fifth star makes a Normal customer VIP at `loyalty.vipThreshold`.
///
/// The maths lives here so the counter, the khata page, the settings screen and the tests all read
/// the same rules; a second copy of "what is a point worth" would drift within a month.
/// </summary>
public static class Loyalty
{
    /// <summary>One point is one rupee - the only rate the shop ever quotes to a customer.</summary>
    public const int PaisaPerPoint = 100;

    public const int MaxStars = 5;

    /// <summary>Stars 1-5: the fifth step is reached exactly at `vipThreshold` points.</summary>
    public static int StarsFor(int points, int vipThreshold = 500)
    {
        var threshold = vipThreshold <= 0 ? 500 : vipThreshold;
        var step = Math.Max(1, threshold / MaxStars);
        var stars = 1 + Math.Max(0, points) / step;

        return Math.Clamp(stars, 1, MaxStars);
    }

    /// <summary>The one-word title behind the stars - what the cashier says instead of "four stars".</summary>
    public static string Title(int stars) => Math.Clamp(stars, 1, MaxStars) switch
    {
        1 => "New",
        2 => "Regular",
        3 => "Good",
        4 => "Very good",
        _ => "VIP"
    };

    /// <summary>
    /// How much of a redemption is really allowed. A request is never refused at the till (the
    /// customer is standing there): it is **capped** by what he has, by the bill in whole rupees, and
    /// - only when the owner has switched the below-cost lock on - by the cost of the goods.
    /// </summary>
    public static (int Points, long Paisa) Redeem(
        int requested,
        int available,
        long totalPaisa,
        long costPaisa,
        bool blockBelowCost)
    {
        if (requested <= 0 || available <= 0 || totalPaisa <= 0)
        {
            return (0, 0);
        }

        var points = Math.Min(Math.Max(0, requested), available);

        // Whole rupees only: a bill of Rs 45.50 can spend at most 45 points.
        var byBill = (int)Math.Min(int.MaxValue, totalPaisa / PaisaPerPoint);
        points = Math.Min(points, byBill);

        if (blockBelowCost)
        {
            // The shop may give points away, but it may not turn them into goods sold below cost.
            var byCost = (int)Math.Min(int.MaxValue, Math.Max(0, (totalPaisa - costPaisa) / PaisaPerPoint));
            points = Math.Min(points, byCost);
        }

        points = Math.Max(0, points);

        return (points, points * (long)PaisaPerPoint);
    }

    /// <summary>"Rs 320 to spend" - the value of the points, for the khata page and the counter.</summary>
    public static string ValueText(int points) => Money.Format(Math.Max(0, points) * (long)PaisaPerPoint);
}
