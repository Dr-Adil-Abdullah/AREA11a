namespace Area11.Core.Utils;

/// <summary>
/// The one place where a fractional result becomes a whole paisa amount.
///
/// Why this exists: <c>Math.Round(value, MidpointRounding.AwayFromZero)</c> has two overloads -
/// one for <c>double</c> and one for <c>decimal</c> - so calling it with an integral argument
/// (<c>int</c> or <c>long</c>) is a compile error, CS0121 "the call is ambiguous". That error
/// reached the shop computer once (SaleService, line 337) because a quantity multiplier was an
/// <c>int</c>. Every money round in the application now goes through this method, which takes a
/// <c>double</c> and therefore can never be ambiguous, and the build checker forbids raw
/// <c>Math.Round</c> elsewhere.
///
/// The rounding direction is fixed: <b>away from zero</b>, matching the reference application.
/// Where the direction matters for money (bills, round-off) <see cref="Money.RoundDownWithCostGuard"/>
/// is used instead.
/// </summary>
public static class Calc
{
    /// <summary>Rounds half away from zero and returns whole paisa / whole units.</summary>
    public static long Round(double value) =>
        (long)Math.Round(value, MidpointRounding.AwayFromZero);

    /// <summary>
    /// The same rounding for a <c>decimal</c> amount (percentages and money are decimal).
    /// It has its own name on purpose: if this were an overload of <see cref="Round"/> then a call
    /// with an <c>int</c> argument would be ambiguous again - the very error this file removes.
    /// </summary>
    public static long RoundMoney(decimal value) =>
        (long)Math.Round(value, MidpointRounding.AwayFromZero);

    /// <summary>
    /// Converts a quantity in a chosen unit (box / strip) into base units.
    /// The multiplier is a <c>double</c> on purpose - see the class comment.
    /// </summary>
    public static double ToBase(double quantity, double unitsPerSelected) =>
        quantity * unitsPerSelected;
}
