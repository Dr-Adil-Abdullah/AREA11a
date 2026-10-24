using Area11.Core.Utils;
using Xunit;

namespace Area11.Tests;

/// <summary>
/// Spec 11.3 / 31.18: round DOWN only, never up, and never below the purchase cost.
/// These are the same cases the reference proved live.
/// </summary>
public sealed class MoneyTests
{
    [Theory]
    [InlineData(54500, 54300, 54500)]
    [InlineData(54500, 50000, 54000)]
    [InlineData(55600, 50000, 55000)]
    [InlineData(54500, 0, 54000)]
    public void RoundDownWithCostGuard_MatchesTheReferenceExamples(long total, long cost, long expected)
    {
        Assert.Equal(expected, Money.RoundDownWithCostGuard(total, cost, 10));
    }

    [Fact]
    public void RoundDown_NeverRoundsUp()
    {
        // The step is TEN RUPEES (the shop's round-off), so 549.99 becomes 540.00 - not 549.00,
        // and certainly never 550.00. Getting this wrong in either direction changes the bill.
        Assert.Equal(54000, Money.RoundDownWithCostGuard(54999, 0, 10));
        Assert.Equal(54000, Money.RoundDownWithCostGuard(54000, 0, 10));

        // …and it is never bigger than the amount it was given.
        for (var paisa = 1L; paisa <= 200000L; paisa += 137)
        {
            Assert.True(Money.RoundDownWithCostGuard(paisa, 0, 10) <= paisa);
        }
    }

    [Fact]
    public void RoundDown_DisabledWhenStepIsZero()
    {
        Assert.Equal(54555, Money.RoundDownWithCostGuard(54555, 0, 0));
    }

    [Theory]
    [InlineData("545", 54500L)]
    [InlineData("545.50", 54550L)]
    [InlineData("1,545", 154500L)]
    [InlineData("Rs 1000", 100000L)]
    [InlineData("", 0L)]
    public void TryParse_AcceptsRupeeText(string text, long expectedPaisa)
    {
        Assert.True(Money.TryParse(text, out var rupees));
        Assert.Equal(expectedPaisa, Money.ToPaisa(rupees));
    }

    [Fact]
    public void TryParse_RejectsGarbage()
    {
        Assert.False(Money.TryParse("abc", out _));
    }

    [Fact]
    public void Format_UsesRupees()
    {
        Assert.Equal("Rs 545", Money.Format(54500));
    }
}
