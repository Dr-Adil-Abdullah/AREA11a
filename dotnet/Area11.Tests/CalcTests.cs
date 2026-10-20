using Area11.Core.Utils;
using Xunit;

namespace Area11.Tests;

/// <summary>
/// Regression guard for CS0121: an integral unit multiplier (an int) made
/// `Math.Round(long)` ambiguous on the first Windows build. Calc.Round must accept the result of
/// any int/double/long expression without a cast, and keep the reference rounding direction.
/// </summary>
public sealed class CalcTests
{
    [Theory]
    [InlineData(1, 1.0)]
    [InlineData(10, 10.0)]
    [InlineData(3, 3.0)]
    public void UnitMultiplier_FromAnInt_CompilesAndRounds(int unitsPerSelected, double expected)
    {
        // 50000 paisa (Rs 500) x an int multiplier - the exact shape that failed to compile.
        const long tierPrice = 50000;
        var rounded = Calc.Round(tierPrice * (double)unitsPerSelected);

        Assert.Equal(50000L * (long)expected, rounded);
    }

    [Theory]
    [InlineData(0.4, 0)]
    [InlineData(0.5, 1)]
    [InlineData(1.5, 2)]
    [InlineData(2.5, 3)]
    [InlineData(-1.5, -2)]
    public void Round_GoesAwayFromZero(double value, long expected)
    {
        Assert.Equal(expected, Calc.Round(value));
    }

    [Theory]
    [InlineData(0.5, 1)]
    [InlineData(2.5, 3)]
    public void RoundMoney_UsesTheSameDirection(decimal value, long expected)
    {
        Assert.Equal(expected, Calc.RoundMoney(value));
    }

    [Fact]
    public void ToBase_MultipliesAQuantityByThePacking()
    {
        Assert.Equal(20d, Calc.ToBase(2, 10), 6);
        Assert.Equal(7.5d, Calc.ToBase(0.5, 15), 6);
    }
}
