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
    public void WithoutCounterTheDashboardIsChosenEvenIfOtherPagesAreAvailable()
    {
        Assert.Equal("dashboard", StartPagePolicy.Choose(new[] { "products", "purchases", "dashboard" }));
    }

    [Fact]
    public void CounterIsChosenEvenWhenDashboardIsUnavailable()
    {
        Assert.Equal("counter", StartPagePolicy.Choose(new[] { "products", "counter" }));
    }

    [Fact]
    public void WithoutDashboardOrCounterUseOnlyAnAvailablePageOrNothing()
    {
        Assert.Equal("products", StartPagePolicy.Choose(new[] { "products", "purchases" }));
        Assert.Equal("dashboard", StartPagePolicy.Choose(new[] { "dashboard" }));
        Assert.Null(StartPagePolicy.Choose(Array.Empty<string>()));
    }
}
