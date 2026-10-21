using Area11.Core.Models;
using Area11.Core.Services;
using Xunit;

namespace Area11.Tests;

/// <summary>
/// Settings are the shop's rules, so a wrong number here is worse than a wrong click: three copies
/// of every bill, or a 5% cashier limit above the manager's, would quietly cost money. These tests
/// pin the guards that live in <see cref="SettingsService"/> (the screen is only one doorway) and the
/// printing defaults the owner agreed to (D-26).
/// </summary>
public sealed class SettingsTests
{
    private static readonly User Owner = new() { Id = 1, Name = "Owner", Role = "owner" };

    [Fact]
    public void BothPaperWidths_AreAccepted()
    {
        using var test = new TestDatabase();

        Assert.True(test.Settings.Set("printer.width", 58, Owner).Ok);

        var eighty = test.Settings.Set("printer.width", 80, Owner);
        Assert.True(eighty.Ok, eighty.Error);
        Assert.Equal(80, test.Settings.GetInt("printer.width"));
    }

    [Theory]
    [InlineData(57)]
    [InlineData(76)]
    [InlineData(0)]
    public void AnotherPaperWidth_IsRefused(int millimetres)
    {
        using var test = new TestDatabase();

        var result = test.Settings.Set("printer.width", millimetres, Owner);

        Assert.False(result.Ok);
        Assert.Equal(58, test.Settings.GetInt("printer.width"));   // the old value survives
    }

    [Theory]
    [InlineData(1, true)]
    [InlineData(2, true)]
    [InlineData(3, true)]
    [InlineData(0, false)]
    [InlineData(4, false)]
    public void Copies_AreOneTwoOrThree(int copies, bool accepted)
    {
        using var test = new TestDatabase();

        var result = test.Settings.Set("printer.copies", copies, Owner);

        Assert.Equal(accepted, result.Ok);
        Assert.Equal(accepted ? copies : 1, test.Settings.GetInt("printer.copies"));
    }

    [Fact]
    public void DiscountLimit_StaysBetweenZeroAndHundred()
    {
        using var test = new TestDatabase();

        Assert.False(test.Settings.Set("discount.maxPercentManager", 101, Owner).Ok);
        Assert.False(test.Settings.Set("discount.maxPercentManager", -1, Owner).Ok);
        Assert.True(test.Settings.Set("discount.maxPercentManager", 25, Owner).Ok);
    }

    [Fact]
    public void CashierLimit_CannotBeHigherThanTheManager()
    {
        using var test = new TestDatabase();

        var result = test.Settings.Set("discount.maxPercentCashier", 40, Owner);

        Assert.False(result.Ok);
        Assert.NotNull(result.Error);
        Assert.Equal(5, test.Settings.GetInt("discount.maxPercentCashier"));
    }

    [Fact]
    public void SetMany_IsAllOrNothing()
    {
        using var test = new TestDatabase();
        var before = test.Settings.GetString("store.name");

        var result = test.Settings.SetMany(new Dictionary<string, object?>
        {
            ["store.name"] = "New Name Pharmacy",
            ["store.phone"] = "0300-1234567",
            ["printer.copies"] = 9                // impossible - the whole save must be refused
        }, Owner);

        Assert.False(result.Ok);
        Assert.Equal(before, test.Settings.GetString("store.name"));
        Assert.Equal(string.Empty, test.Settings.GetString("store.phone"));
    }

    [Fact]
    public void AGoodSave_IsKeptAndWrittenToTheAuditLog()
    {
        using var test = new TestDatabase();

        var result = test.Settings.SetMany(new Dictionary<string, object?>
        {
            ["store.name"] = "Al-Shifa Pharmacy",
            ["printer.width"] = 80,
            ["printer.autoPrint"] = false
        }, Owner);

        Assert.True(result.Ok, result.Error);
        Assert.Equal("Al-Shifa Pharmacy", test.Settings.GetString("store.name"));
        Assert.Equal(80, test.Settings.GetInt("printer.width"));
        Assert.False(test.Settings.GetBool("printer.autoPrint"));

        using var connection = test.Database.Open();
        var audits = Convert.ToInt64(test.Database.Scalar(connection,
            "SELECT COUNT(1) FROM audit_logs WHERE action = 'settings_change' AND entity_id = 'printer.width';") ?? 0L);

        Assert.Equal(1L, audits);
    }

    [Fact]
    public void PrintingDefaults_MatchTheAgreedBehaviour()
    {
        using var test = new TestDatabase();

        Assert.Equal(58, test.Settings.GetInt("printer.width"));       // 58 mm unless changed
        Assert.True(test.Settings.GetBool("printer.autoPrint"));       // auto, until the owner chooses manual
        Assert.True(test.Settings.GetBool("printer.autoCut"));
        Assert.Equal(1, test.Settings.GetInt("printer.copies"));
        Assert.Equal(string.Empty, test.Settings.GetString("printer.name")); // Windows default printer
    }
}
