using Area11.Core.Models;
using Area11.Core.Services;
using Xunit;

namespace Area11.Tests;

/// <summary>
/// Correcting a customer and his credit limit (F-11, Spec 17.3). The promises that must hold:
///   • the name, phone and category may be corrected, and the black box keeps old → new;
///   • **the udhaar never moves** - it is history, not a field, so the khata page still matches;
///   • the **limit is money**: a cashier may fix a phone number but a changed limit needs the owner
///     or a manager, and a refusal changes nothing at all (not even the name in the same form);
///   • a limit below the udhaar, or a phone two customers share, is **saved and said out loud** -
///     the shop decides, the app only warns (U-30 spirit);
///   • a negative limit or an empty name is refused before anything is written.
/// </summary>
public sealed class CustomerEditTests
{
    private static PartnerService Partners(TestDatabase test) =>
        new(test.Database, test.Audit, test.Log);

    private static readonly User Cashier = new() { Id = 2, Name = "Cashier", Role = "cashier" };

    private static readonly User Manager = new() { Id = 3, Name = "Manager", Role = "manager" };

    private static Customer Read(TestDatabase test, int id)
    {
        using var connection = test.Database.Open();
        return test.Database.QuerySingle<Customer>(connection, @"
SELECT id AS id, name AS name, phone AS phone, category AS category,
       credit_limit_paisa AS creditLimitPaisa, balance_paisa AS balancePaisa, loyalty_points AS loyaltyPoints
FROM customers WHERE id = @id;", new { id })!;
    }

    /// <summary>A credit bill for qty tablets at Rs 300 each, so the customer really owes money.</summary>
    private static void CreditBill(TestDatabase test, int productId, int customerId, int qty)
    {
        var sale = TestData.Sales(test).Create(
            TestData.Cart(productId, qty: qty, method: "credit", customerId: customerId), TestData.Owner);

        Assert.True(sale.Ok, sale.Error);
    }

    private static CustomerEditInput Form(int id, string name, string? phone = null, string category = "normal",
        long? limit = null) => new()
    {
        Id = id,
        Name = name,
        Phone = phone,
        Category = category,
        CreditLimitPaisa = limit
    };

    [Fact]
    public void TheForm_IsSaved_AndTheBlackBoxKeepsOldAndNew()
    {
        using var test = new TestDatabase();
        var customer = TestData.AddCustomer(test, name: "Ali Raza");

        var result = Partners(test).UpdateCustomer(TestData.Owner,
            Form(customer, "Ali Raza Khan", "0321-9988776", "vip"));

        Assert.True(result.Ok, result.Error);
        Assert.False(result.Value!.LimitChanged);

        var saved = Read(test, customer);
        Assert.Equal("Ali Raza Khan", saved.Name);
        Assert.Equal("0321-9988776", saved.Phone);
        Assert.Equal("vip", saved.Category);

        using var connection = test.Database.Open();
        var audit = test.Database.QuerySingle<AuditSnapshot>(connection, @"
SELECT IFNULL(old_value, '') AS oldValue, IFNULL(new_value, '') AS newValue
FROM audit_logs
WHERE module = 'Customers' AND action = 'update' AND entity_id = @id
ORDER BY id DESC LIMIT 1;", new { id = customer.ToString() });

        Assert.NotNull(audit);
        Assert.Contains("Ali Raza", audit!.OldValue);
        Assert.Contains("Ali Raza Khan", audit.NewValue);
        Assert.Contains("vip", audit.NewValue);
    }

    [Fact]
    public void TheUdhaar_IsNeverTouchedByAnEdit_AndTheBooksStillMatch()
    {
        using var test = new TestDatabase();
        var product = TestData.AddProduct(test, retailPaisa: 30000, batchQty: 100);
        var customer = TestData.AddCustomer(test);
        CreditBill(test, product, customer, 2);

        var before = Read(test, customer);
        Assert.Equal(60000L, before.BalancePaisa);

        var result = Partners(test).UpdateCustomer(TestData.Owner,
            Form(customer, "Ali Raza", "0300-1112223", "doctor", limit: 250000));

        Assert.True(result.Ok, result.Error);
        Assert.True(result.Value!.LimitChanged);
        Assert.Equal(0L, result.Value.OldLimitPaisa);
        Assert.Equal(250000L, result.Value.CreditLimitPaisa);

        var after = Read(test, customer);
        Assert.Equal(60000L, after.BalancePaisa);          // money owed is history, not an editable field
        Assert.Equal(250000L, after.CreditLimitPaisa);
        Assert.Equal("doctor", after.Category);

        var page = new CustomerLedgerService(test.Database, test.Audit, test.Settings,
            new PermissionService(test.Database, test.Log, test.Audit), test.Log).Page(customer);

        Assert.True(page.Ok, page.Error);
        Assert.True(page.Value!.BooksMatch);                // an edit cannot break the khata
    }

    [Fact]
    public void ACashierMayFixThePhone_ButNotTheLimit()
    {
        using var test = new TestDatabase();
        var product = TestData.AddProduct(test, retailPaisa: 30000, batchQty: 100);
        var customer = TestData.AddCustomer(test);
        CreditBill(test, product, customer, 1);

        // The phone number: allowed, because the counter cannot work with a wrong number.
        var phoneOnly = Partners(test).UpdateCustomer(Cashier,
            Form(customer, "Ali Raza", "0333-7654321"));

        Assert.True(phoneOnly.Ok, phoneOnly.Error);
        Assert.Equal("0333-7654321", Read(test, customer).Phone);

        // The limit: refused, and the name sent with it must not be saved either.
        var limit = Partners(test).UpdateCustomer(Cashier,
            Form(customer, "Someone Else", limit: 900000));

        Assert.False(limit.Ok);
        Assert.Contains("owner or a manager", limit.Error!, StringComparison.OrdinalIgnoreCase);

        var unchanged = Read(test, customer);
        Assert.Equal("Ali Raza", unchanged.Name);
        Assert.Equal(0L, unchanged.CreditLimitPaisa);
    }

    [Fact]
    public void AManagerMaySetTheLimit_ButNotForNegativeMoney()
    {
        using var test = new TestDatabase();
        var customer = TestData.AddCustomer(test);

        var saved = Partners(test).UpdateCustomer(Manager, Form(customer, "Ali Raza", limit: 150000));
        Assert.True(saved.Ok, saved.Error);
        Assert.Equal(150000L, Read(test, customer).CreditLimitPaisa);

        var negative = Partners(test).UpdateCustomer(TestData.Owner, Form(customer, "Ali Raza", limit: -100));
        Assert.False(negative.Ok);
        Assert.Equal(150000L, Read(test, customer).CreditLimitPaisa);
    }

    [Fact]
    public void ALimitBelowTheUdhaar_IsSavedWithAWarning()
    {
        using var test = new TestDatabase();
        var product = TestData.AddProduct(test, retailPaisa: 30000, batchQty: 100);
        var customer = TestData.AddCustomer(test);
        CreditBill(test, product, customer, 3);            // 90,000 owed

        var result = Partners(test).UpdateCustomer(TestData.Owner,
            Form(customer, "Ali Raza", limit: 50000));

        Assert.True(result.Ok, result.Error);
        Assert.True(result.Value!.HasWarning);
        Assert.Contains("above the new limit", result.Value.WarningText, StringComparison.OrdinalIgnoreCase);

        // Saved anyway: lowering a limit is how the shop stops giving more credit.
        Assert.Equal(50000L, Read(test, customer).CreditLimitPaisa);
        Assert.Equal(90000L, Read(test, customer).BalancePaisa);
    }

    [Fact]
    public void ALimitOfZero_MeansNoLimit()
    {
        using var test = new TestDatabase();
        var customer = TestData.AddCustomer(test);

        Partners(test).UpdateCustomer(TestData.Owner, Form(customer, "Ali Raza", limit: 300000));

        var cleared = Partners(test).UpdateCustomer(TestData.Owner, Form(customer, "Ali Raza", limit: 0));

        Assert.True(cleared.Ok, cleared.Error);
        Assert.True(cleared.Value!.LimitChanged);
        Assert.Equal("no limit", cleared.Value.LimitLabel);
        Assert.Equal(0L, Read(test, customer).CreditLimitPaisa);
    }

    [Fact]
    public void ASharedPhone_IsSavedAndSaidOutLoud()
    {
        using var test = new TestDatabase();
        var first = TestData.AddCustomer(test, name: "Ali Raza", phone: "0300-1234567");
        var second = TestData.AddCustomer(test, name: "Sana Malik", phone: "0300-9999999");

        var result = Partners(test).UpdateCustomer(TestData.Owner, Form(second, "Sana Malik", "0300-1234567"));

        Assert.True(result.Ok, result.Error);
        Assert.True(result.Value!.HasWarning);
        Assert.Contains("Ali Raza", result.Value.WarningText);

        // Saved: two people in one house may share a number; the warning is there so nobody is surprised.
        Assert.Equal("0300-1234567", Read(test, second).Phone);
        Assert.Equal("0300-1234567", Read(test, first).Phone);
    }

    [Fact]
    public void AnEmptyName_OrAMissingCustomer_IsRefused()
    {
        using var test = new TestDatabase();
        var customer = TestData.AddCustomer(test);

        var empty = Partners(test).UpdateCustomer(TestData.Owner, Form(customer, "  "));
        Assert.False(empty.Ok);

        var missing = Partners(test).UpdateCustomer(TestData.Owner, Form(9999, "Nobody"));
        Assert.False(missing.Ok);

        Assert.Equal("Ali Raza", Read(test, customer).Name);
    }

    /// <summary>Only the two audit columns the test reads; keeps the SQL aliases honest.</summary>
    public sealed class AuditSnapshot
    {
        public string OldValue { get; set; } = string.Empty;

        public string NewValue { get; set; } = string.Empty;
    }
}
