using System.Globalization;
using Area11.Core.Models;
using Area11.Core.Services;
using Area11.Core.Utils;
using Xunit;

namespace Area11.Tests;

/// <summary>
/// "Which bill was it?" (F-20, Spec 9.1): the customer comes back without the paper, and the shop
/// finds the bill by the one thing the person remembers.
///
/// Why the search earns its own tests: a return is only as good as the bill behind it. Open the wrong
/// bill and either the shop refunds goods it never sold or refuses a customer who is right - so the
/// rule here is a LIST, never a single guessed match, and nothing is written while searching.
/// </summary>
public sealed class SaleLookupTests
{
    // ============================== by phone ==============================

    [Fact]
    public void APhoneNumberFindsTheBillWithOrWithoutDashes()
    {
        using var test = new TestDatabase();
        var customer = TestData.AddCustomer(test, "Ali Raza", phone: "0300-1234567");
        Sale(test, "INV-0001", "2026-10-01 10:00:00", customer, items: "Panadol 500mg");

        Assert.Single(Find(test, "0300-1234567"));
        Assert.Single(Find(test, "0300 1234567"));   // the counter often types it with a space
        Assert.Single(Find(test, "03001234567"));    // or with no dash at all
        Assert.Single(Find(test, "123456"));         // or only the last digits
    }

    [Fact]
    public void APhoneThatBelongsToNobodyFindsNothing()
    {
        using var test = new TestDatabase();
        var customer = TestData.AddCustomer(test, "Ali Raza", phone: "0300-1234567");
        Sale(test, "INV-0001", "2026-10-01 10:00:00", customer, items: "Panadol 500mg");

        Assert.Empty(Find(test, "0321-9999999"));
    }

    // ============================== by name and medicine ==============================

    [Fact]
    public void PartOfANameFindsTheCustomerBills()
    {
        using var test = new TestDatabase();
        var ali = TestData.AddCustomer(test, "Ali Raza", phone: "0300-1234567");
        var fatima = TestData.AddCustomer(test, "Fatima Bibi", phone: "0321-9876543");
        Sale(test, "INV-0001", "2026-10-01 10:00:00", ali, items: "Panadol 500mg");
        Sale(test, "INV-0002", "2026-10-02 10:00:00", fatima, items: "Augmentin 625mg");

        var found = Find(test, "fatima");

        Assert.Single(found);
        Assert.Equal("INV-0002", found[0].Code);
    }

    [Fact]
    public void AMedicineNameFindsEveryBillThatSoldIt()
    {
        using var test = new TestDatabase();
        var ali = TestData.AddCustomer(test, "Ali Raza", phone: "0300-1234567");
        Sale(test, "INV-0001", "2026-10-01 10:00:00", ali, items: "Panadol 500mg");
        Sale(test, "INV-0002", "2026-10-02 10:00:00", ali, items: "Augmentin 625mg");
        Sale(test, "INV-0003", "2026-10-03 10:00:00", ali, items: new[] { "Panadol 500mg", "Risek 20mg" });

        var found = Find(test, "panadol");

        Assert.Equal(2, found.Count);
        Assert.Equal("INV-0003", found[0].Code);   // newest first
        Assert.Equal("INV-0001", found[1].Code);
    }

    // ============================== by number and date ==============================

    [Fact]
    public void PartOfABillNumberFindsIt()
    {
        using var test = new TestDatabase();
        Sale(test, "INV-0007", "2026-10-01 10:00:00", null, items: "Panadol 500mg");
        Sale(test, "INV-0008", "2026-10-02 10:00:00", null, items: "Panadol 500mg");

        var found = Find(test, "0008");

        Assert.Single(found);
        Assert.Equal("INV-0008", found[0].Code);
    }

    [Fact]
    public void ADateRangeKeepsOnlyTheBillsInsideIt()
    {
        using var test = new TestDatabase();
        Sale(test, "INV-0001", "2026-10-01 10:00:00", null, items: "Panadol 500mg");
        Sale(test, "INV-0002", "2026-10-05 10:00:00", null, items: "Panadol 500mg");
        Sale(test, "INV-0003", "2026-10-20 10:00:00", null, items: "Panadol 500mg");

        var middle = Find(test, string.Empty, from: "2026-10-04", to: "2026-10-06");

        Assert.Single(middle);
        Assert.Equal("INV-0002", middle[0].Code);

        // The same day at both ends is included - "from the 5th to the 5th" means that whole day.
        var oneDay = Find(test, string.Empty, from: "2026-10-05", to: "2026-10-05");
        Assert.Single(oneDay);
        Assert.Equal("INV-0002", oneDay[0].Code);
    }

    [Fact]
    public void ADateTypedTheShopsOtherWayStillWorks()
    {
        using var test = new TestDatabase();
        Sale(test, "INV-0001", "2026-10-01 10:00:00", null, items: "Panadol 500mg");

        var found = Find(test, string.Empty, from: "01/10/2026", to: string.Empty);

        Assert.Single(found);
    }

    // ============================== what is refused ==============================

    [Fact]
    public void AnEmptySearchIsRefusedRatherThanDumpingTheWholeShop()
    {
        using var test = new TestDatabase();
        Sale(test, "INV-0001", "2026-10-01 10:00:00", null, items: "Panadol 500mg");

        var refused = TestData.Returns(test).Search(new SaleLookupInput());

        Assert.False(refused.Ok);
        Assert.Contains("every bill", refused.Error!);
    }

    [Fact]
    public void ABackwardsDateRangeIsRefused()
    {
        using var test = new TestDatabase();

        var refused = TestData.Returns(test).Search(
            new SaleLookupInput { FromDate = "2026-10-20", ToDate = "2026-10-01" });

        Assert.False(refused.Ok);
        Assert.Contains("after the 'To' date", refused.Error!);
    }

    [Fact]
    public void ANonsenseDateIsNamedInTheRefusal()
    {
        using var test = new TestDatabase();

        var refused = TestData.Returns(test).Search(
            new SaleLookupInput { Text = "panadol", FromDate = "31/31/2026" });

        Assert.False(refused.Ok);
        Assert.Contains("31/31/2026", refused.Error!);
        Assert.Contains("2026-10-20", refused.Error!);   // the message shows the shape it wants
    }

    // ============================== what the list shows ==============================

    [Fact]
    public void ABillWithoutACustomerSaysWalkingCustomer()
    {
        using var test = new TestDatabase();
        Sale(test, "INV-0001", "2026-10-01 10:00:00", null, items: "Panadol 500mg");

        var row = Find(test, "INV-0001")[0];

        Assert.Equal("Walking customer", row.CustomerLabel);
        Assert.Equal(1, row.LineCount);
        Assert.Equal("1 item", row.ItemsLabel);
        Assert.Equal(Money.Format(20000), row.TotalText);
    }

    [Fact]
    public void AnUnpaidBillShowsWhatIsStillDue()
    {
        using var test = new TestDatabase();
        var customer = TestData.AddCustomer(test, "Ali Raza", phone: "0300-1234567");
        Sale(test, "INV-0001", "2026-10-01 10:00:00", customer, due: 15000, items: "Panadol 500mg");

        var row = Find(test, "INV-0001")[0];

        Assert.True(row.HasDue);
        Assert.Equal("due " + Money.Format(15000), row.DueLabel);
        Assert.Equal("part paid", row.StatusLabel);
    }

    [Fact]
    public void NothingFoundIsAnEmptyListAndNotAnError()
    {
        using var test = new TestDatabase();
        Sale(test, "INV-0001", "2026-10-01 10:00:00", null, items: "Panadol 500mg");

        var found = TestData.Returns(test).Search(new SaleLookupInput { Text = "xyzzy" });

        Assert.True(found.Ok);
        Assert.Empty(found.Value!);
    }

    // ============================== helpers ==============================

    private static List<SaleLookupRow> Find(
        TestDatabase test, string text, string from = "", string to = "")
    {
        var found = TestData.Returns(test).Search(
            new SaleLookupInput { Text = text, FromDate = from, ToDate = to });

        Assert.True(found.Ok, found.Error);
        return found.Value!;
    }

    /// <summary>
    /// One bill with a chosen date and one line per medicine. Inserted directly because a real sale
    /// always stamps "now", and these tests are about finding a bill from October.
    ///
    /// The medicine row is needed for the foreign key, so the first call in each test creates one.
    /// </summary>
    private static void Sale(
        TestDatabase test,
        string code,
        string date,
        int? customerId,
        long total = 20000,
        long due = 0,
        params string[] items)
    {
        if (items.Length == 0)
        {
            items = new[] { "Panadol 500mg" };
        }

        if (Count(test, "products") == 0)
        {
            TestData.AddProduct(test);
        }

        var productId = Convert.ToInt32(Scalar(test, "SELECT id FROM products ORDER BY id LIMIT 1;"), CultureInfo.InvariantCulture);

        test.Database.Tx(connection =>
        {
            test.Database.Execute(connection, @"
INSERT INTO sales(code, date, customer_id, subtotal_paisa, total_paisa, paid_paisa, due_paisa,
                  status, payment_method, created_at)
VALUES(@code, @date, @customer, @total, @total, @paid, @due, @status, 'cash', @date);",
                new
                {
                    code,
                    date,
                    customer = customerId,
                    total,
                    paid = total - due,
                    due,
                    status = due == 0 ? "paid" : due == total ? "credit" : "partial"
                });

            var saleId = (int)test.Database.NextId(connection);

            foreach (var name in items)
            {
                test.Database.Execute(connection, @"
INSERT INTO sale_items(sale_id, product_id, name_snapshot, qty_base, unit_sold, qty_entered,
                       unit_price_paisa, discount_paisa, line_total_paisa, cost_paisa_at_sale)
VALUES(@saleId, @productId, @name, 10, 'base', 10, 2000, 0, 20000, 15000);",
                    new { saleId, productId, name });
            }
        });
    }

    private static long Count(TestDatabase test, string fromWhere) =>
        test.Database.Tx(connection => Convert.ToInt64(
            test.Database.Scalar(connection, "SELECT COUNT(1) FROM " + fromWhere + ";") ?? 0L));

    private static object? Scalar(TestDatabase test, string sql) =>
        test.Database.Tx(connection => test.Database.Scalar(connection, sql));
}
