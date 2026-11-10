using Area11.Core.Utils;
using Xunit;

namespace Area11.Tests;

public sealed class StartPagePolicyTests
{
    [Fact]
    public void CounterIsChosenEvenWhenDashboardIsFirst()
    {
        Assert.Equal("counter", StartPagePolicy.Choose(new[] { "dashboard", "products", "counter" }));
    }

    [Fact]
    public void WithoutCounterTheFirstPermittedOrdinaryPageIsChosen()
    {
        Assert.Equal("products", StartPagePolicy.Choose(new[] { "dashboard", "products", "purchases" }));
    }

    [Fact]
    public void WithoutOrdinaryPagesUseOnlyAnAvailableDashboardOrNothing()
    {
        Assert.Equal("dashboard", StartPagePolicy.Choose(new[] { "dashboard" }));
        Assert.Null(StartPagePolicy.Choose(Array.Empty<string>()));
    }
}
