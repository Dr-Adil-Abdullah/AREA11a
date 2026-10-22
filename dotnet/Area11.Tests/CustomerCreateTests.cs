using Area11.Core.Services;
using Xunit;

namespace Area11.Tests;

/// <summary>
/// Adding a customer (D-50).
///
/// The service existed since F-27 but nothing on the screen called it, so the honest answer to the
/// owner's question was "no, adding a customer was never built". These tests pin the behaviour the
/// new form relies on - and the one rule the form cannot enforce by itself: a phone number belongs to
/// exactly one khata.
/// </summary>
public sealed class CustomerCreateTests
{
    private static PartnerService Partners(TestDatabase test) => TestData.Partners(test);

    [Fact]
    public void ANewCustomer_StartsWithAnEmptyKhata()
    {
        using var test = new TestDatabase();

        var created = Partners(test).CreateCustomer(TestData.Owner, "   Bilal  ", "0321-9876543", "vip");

        Assert.True(created.Ok, created.Error);

        var row = TestData.Ledger(test).List().Value!.Single(customer => customer.Id == created.Value);

        Assert.Equal("Bilal", row.Name);
        Assert.Equal("0321-9876543", row.Phone);
        Assert.Equal("VIP", row.CategoryLabel);
        Assert.Equal(0, row.BalancePaisa);
    }

    [Fact]
    public void TheCategoryWord_IsNeverStoredAsTyped()
    {
        using var test = new TestDatabase();

        var created = Partners(test).CreateCustomer(TestData.Owner, "Sara", null, "DOCTOR");

        Assert.True(created.Ok, created.Error);
        Assert.Equal("Doctor", TestData.Ledger(test).List().Value!
            .Single(customer => customer.Id == created.Value).CategoryLabel);
    }

    [Fact]
    public void AnUnknownCategory_FallsBackToNormal()
    {
        using var test = new TestDatabase();

        var created = Partners(test).CreateCustomer(TestData.Owner, "Sara", null, "wholesale");

        Assert.True(created.Ok, created.Error);
        Assert.Equal("Normal", TestData.Ledger(test).List().Value!
            .Single(customer => customer.Id == created.Value).CategoryLabel);
    }

    [Fact]
    public void AOneLetterName_IsRefused()
    {
        using var test = new TestDatabase();

        var created = Partners(test).CreateCustomer(TestData.Owner, "A", null, "normal");

        Assert.False(created.Ok);
        Assert.Contains("name", created.Error!);
        Assert.Empty(TestData.Ledger(test).List().Value!);
    }

    [Fact]
    public void TheSamePhone_CannotOpenASecondKhata()
    {
        using var test = new TestDatabase();
        TestData.AddCustomer(test, name: "Ali Raza", phone: "0300-1234567");

        var created = Partners(test).CreateCustomer(TestData.Owner, "Ali Raza Jr", "0300-1234567", "normal");

        Assert.False(created.Ok);

        // The message must name the person who holds the number, or the cashier cannot act on it.
        Assert.Contains("Ali Raza", created.Error!);
        Assert.Single(TestData.Ledger(test).List().Value!);
    }

    [Fact]
    public void TwoCustomersMayBothLeaveThePhoneEmpty()
    {
        using var test = new TestDatabase();

        Assert.True(Partners(test).CreateCustomer(TestData.Owner, "Sara", "", "normal").Ok);
        Assert.True(Partners(test).CreateCustomer(TestData.Owner, "Bilal", "   ", "normal").Ok);

        Assert.Equal(2, TestData.Ledger(test).List().Value!.Count);
    }

    [Fact]
    public void AddingACustomer_IsWrittenInTheBlackBox()
    {
        using var test = new TestDatabase();

        var created = Partners(test).CreateCustomer(TestData.Owner, "Sara Khan", null, "normal");
        Assert.True(created.Ok, created.Error);

        using var connection = test.Database.Open();
        var action = test.Database.QuerySingle<string>(connection, @"
SELECT action FROM audit_logs
WHERE module = 'Customers' AND entity = 'customers' AND entity_id = @id
ORDER BY id DESC LIMIT 1;",
            new { id = created.Value.ToString() });

        Assert.Equal("create", action);
    }

    [Fact]
    public void TheNewCustomer_IsFoundAtTheCounterByPhone()
    {
        using var test = new TestDatabase();

        var created = Partners(test).CreateCustomer(TestData.Owner, "Sara Khan", "0321-9876543", "normal");
        Assert.True(created.Ok, created.Error);

        // The counter finds a khata by a few digits of the phone - that is the only search a cashier
        // has time for while a customer waits.
        var found = TestData.Ledger(test).List("9876543").Value!;

        Assert.Single(found);
        Assert.Equal(created.Value, found[0].Id);
    }
}
