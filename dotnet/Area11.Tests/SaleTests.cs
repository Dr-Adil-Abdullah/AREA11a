using Area11.Core.Models;
using Area11.Core.Services;
using Area11.Core.Utils;
using Xunit;

namespace Area11.Tests;

/// <summary>
/// The money rules the owner checks by hand (Spec 31.18) and the two defects that must never
/// come back: a bill whose total differs from its lines, and a round-off below cost.
/// </summary>
public sealed class SaleTests
{
    private static (SaleService Sales, int ProductId, User Owner) Build(
        TestDatabase test, long costPaisa, long retailPaisa, double batchQty = 100)
    {
        var productId = test.Database.Tx(connection =>
        {
            test.Database.Execute(connection, @"
INSERT INTO products(name, base_unit, box_strips, strip_tablets, cost_paisa, retail_paisa,
                     vip_paisa, doctor_paisa, reorder_level, active, created_at, updated_at)
VALUES('Panadol 500mg', 'tablet', 2, 10, @cost, @retail, 0, 0, 10, 1, @at, @at);",
                new { cost = costPaisa, retail = retailPaisa, at = Clock.Now() });

            var newId = (int)test.Database.NextId(connection);

            test.Database.Execute(connection, @"
INSERT INTO batches(product_id, batch_no, expiry_date, expiry_ym, qty_base, cost_paisa, retail_paisa,
                    is_sample, active, created_at)
VALUES(@productId, 'B-1', '2027-06-30', '2027-06', @qty, @cost, @retail, 0, 1, @at);",
                new { productId = newId, qty = batchQty, cost = costPaisa, retail = retailPaisa, at = Clock.Now() });

            return newId;
        });

        // A bill needs an open drawer (v0.35.0, D-92), so the fixture opens one - with a zero float,
        // so no cash total in this file moves.
        TestData.EnsureDrawerOpen(test);

        var permissions = new PermissionService(test.Database, test.Log, test.Audit);
        var sales = new SaleService(test.Database, test.Audit, new NumberingService(test.Settings),
            test.Settings, permissions, test.Log);

        return (sales, productId, new User { Id = 1, Name = "Owner", Role = "owner" });
    }

    private static SaleInput One(int productId, decimal discount = 0, int qty = 1) => new()
    {
        DiscountPercent = discount,
        PaymentMethod = "cash",
        TenderedPaisa = 0,
        Lines = new List<SaleLineInput>
        {
            new() { ProductId = productId, Unit = "base", Quantity = qty }
        }
    };

    [Fact]
    public void TheScreenSaysTheCashIsShortBeforeTheBillIsRefused()
    {
        using var test = new TestDatabase();
        var (sales, productId, owner) = Build(test, costPaisa: 20000, retailPaisa: 30000);

        var input = One(productId);          // one strip: Rs 300, an exact multiple of the round step
        input.TenderedPaisa = 10000;         // the customer hands over Rs 100

        // The quote keeps showing what the bill IS - blanking the total while the cashier corrects
        // the amount would be worse than the mistake.
        var quote = sales.Quote(input, owner);

        Assert.True(quote.Ok, quote.Error);
        Assert.Equal(30000L, quote.Value!.TotalPaisa);

        // ...and it says, in the words the save would use, that the cash cannot cover the bill.
        // Before v0.30.1 this sentence arrived only AFTER the button was pressed: the cashier saw a
        // total and a change of zero, pressed "Make bill", and the money was refused by the till.
        Assert.Contains("less than the cash part",
            string.Join(" ", quote.Value.Warnings));

        // The rule itself does not move: the save still refuses the same tender.
        var saved = sales.Create(input, owner);

        Assert.False(saved.Ok);
        Assert.Contains("less than the cash part", saved.Error ?? string.Empty);
    }

    [Fact]
    public void LegacyMarginSetting_DoesNotChangeTheFullRetailDiscount()
    {
        using var test = new TestDatabase();
        test.Settings.Set("discount.mode", "margin", null);
        var (sales, productId, owner) = Build(test, costPaisa: 20000, retailPaisa: 30000);

        var result = sales.Create(One(productId, discount: 10), owner);

        Assert.True(result.Ok, result.Error);
        Assert.Equal(27000L, result.Value!.TotalPaisa);
    }

    [Fact]
    public void RetailDiscount_IsTenPercentOfThePrice()
    {
        using var test = new TestDatabase();
        test.Settings.Set("discount.mode", "retail", null);

        var (sales, productId, owner) = Build(test, costPaisa: 20000, retailPaisa: 30000);

        var result = sales.Create(One(productId, discount: 10), owner);

        Assert.True(result.Ok, result.Error);
        Assert.Equal(27000L, result.Value!.TotalPaisa);
    }

    [Fact]
    public void FullRetailDiscount_StillCannotGoBelowCost()
    {
        using var test = new TestDatabase();
        var (sales, productId, owner) = Build(test, costPaisa: 20000, retailPaisa: 30000);

        var result = sales.Create(One(productId, discount: 50), owner);

        Assert.True(result.Ok, result.Error);
        Assert.Equal(20000L, result.Value!.TotalPaisa);
        Assert.Equal(10000L, result.Value.DiscountPaisa);
        Assert.Contains(result.Value.Warnings, warning => warning.Contains("below cost", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("vip", 25000L)]
    [InlineData("doctor", 24000L)]
    public void SpecialRate_IsAnExplicitChoice_AndNeverCombinesWithDiscount(string category, long specialRate)
    {
        using var test = new TestDatabase();
        var (sales, productId, owner) = Build(test, costPaisa: 20000, retailPaisa: 30000);
        var customerId = TestData.AddCustomer(test);

        test.Database.Tx(connection =>
        {
            test.Database.Execute(connection,
                "UPDATE products SET vip_paisa = 25000, doctor_paisa = 24000 WHERE id = @productId;",
                new { productId });
            test.Database.Execute(connection,
                "UPDATE customers SET category = @category WHERE id = @customerId;",
                new { category, customerId });
        });

        var normal = One(productId);
        normal.CustomerId = customerId;
        var normalQuote = sales.Quote(normal, owner);
        Assert.True(normalQuote.Ok, normalQuote.Error);
        Assert.Equal(30000L, normalQuote.Value!.TotalPaisa); // No = ordinary retail

        var special = One(productId);
        special.CustomerId = customerId;
        special.UseSpecialRate = true;
        var specialQuote = sales.Quote(special, owner);
        Assert.True(specialQuote.Ok, specialQuote.Error);
        Assert.Equal(specialRate, specialQuote.Value!.TotalPaisa); // Yes = explicit opt-in

        var discounted = One(productId, discount: 10);
        discounted.CustomerId = customerId;
        discounted.UseSpecialRate = true;
        var discountedQuote = sales.Quote(discounted, owner);
        Assert.True(discountedQuote.Ok, discountedQuote.Error);
        Assert.Equal(27000L, discountedQuote.Value!.TotalPaisa); // 10% of retail, not special

        var custom = One(productId);
        custom.CustomerId = customerId;
        custom.UseSpecialRate = true;
        custom.Lines[0].UnitPricePaisa = 28000;
        var customQuote = sales.Quote(custom, owner);
        Assert.True(customQuote.Ok, customQuote.Error);
        Assert.Equal(28000L, customQuote.Value!.TotalPaisa); // adjustable actual rate survives

        var saved = sales.Create(special, owner);
        Assert.True(saved.Ok, saved.Error);
        Assert.Equal(specialRate, saved.Value!.TotalPaisa);
    }

    [Fact]
    public void CustomRate_CannotBypassTheCashiersRoleLimit()
    {
        using var test = new TestDatabase();
        var (sales, productId, _) = Build(test, costPaisa: 20000, retailPaisa: 30000);
        var cashier = TestData.Cashier(test);
        var input = One(productId);
        input.Lines[0].UnitPricePaisa = 25000;

        var result = sales.Create(input, cashier);

        Assert.False(result.Ok);
        Assert.Contains("Only the owner or a manager", result.Error ?? string.Empty, StringComparison.Ordinal);
    }

    [Fact]
    public void CustomRate_BelowBatchCostIsRefusedWhenTheLockIsOn()
    {
        using var test = new TestDatabase();
        var (sales, productId, owner) = Build(test, costPaisa: 20000, retailPaisa: 30000);
        var input = One(productId);
        input.Lines[0].UnitPricePaisa = 15000;

        var quote = sales.Quote(input, owner);
        var result = sales.Create(input, owner);

        Assert.False(quote.Ok);
        Assert.False(result.Ok);
        Assert.Contains("below purchase cost", result.Error ?? string.Empty, StringComparison.Ordinal);
    }

    [Fact]
    public void RoundDown_HappensWhenItStaysAboveCost()
    {
        using var test = new TestDatabase();
        var (sales, productId, owner) = Build(test, costPaisa: 50000, retailPaisa: 54500);

        var result = sales.Create(One(productId), owner);

        Assert.True(result.Ok, result.Error);
        Assert.Equal(54000L, result.Value!.TotalPaisa);
    }

    [Fact]
    public void RoundDown_IsSkippedWhenItWouldFallBelowCost()
    {
        using var test = new TestDatabase();
        var (sales, productId, owner) = Build(test, costPaisa: 54300, retailPaisa: 54500);

        var result = sales.Create(One(productId), owner);

        Assert.True(result.Ok, result.Error);
        Assert.Equal(54500L, result.Value!.TotalPaisa);
    }

    [Fact]
    public void BillTotal_AlwaysEqualsTheSumOfItsLines()
    {
        using var test = new TestDatabase();
        var (sales, productId, owner) = Build(test, costPaisa: 33333, retailPaisa: 45678);

        var result = sales.Create(One(productId, discount: 5, qty: 3), owner);
        Assert.True(result.Ok, result.Error);

        using var connection = test.Database.Open();
        var lineSum = Convert.ToInt64(test.Database.Scalar(connection,
            "SELECT IFNULL(SUM(line_total_paisa), 0) FROM sale_items WHERE sale_id = @id;",
            new { id = result.Value!.SaleId }) ?? 0L);

        var billTotal = Convert.ToInt64(test.Database.Scalar(connection,
            "SELECT total_paisa FROM sales WHERE id = @id;",
            new { id = result.Value.SaleId }) ?? 0L);

        Assert.Equal(billTotal, lineSum);
    }

    [Fact]
    public void NegativeStock_IsRefused_EvenWhenTheLegacySwitchIsOn()
    {
        using var test = new TestDatabase();
        var (sales, productId, owner) = Build(test, costPaisa: 20000, retailPaisa: 30000, batchQty: 5);
        Assert.True(test.Settings.Set("stock.allowNegative", true, owner).Ok);

        var quote = sales.Quote(One(productId, qty: 12), owner);
        Assert.False(quote.Ok);
        Assert.Contains("below zero", quote.Error ?? string.Empty);

        var result = sales.Create(One(productId, qty: 12), owner);

        Assert.False(result.Ok);
        Assert.Contains("below zero", result.Error ?? string.Empty);

        using var connection = test.Database.Open();
        var stock = Convert.ToDouble(test.Database.Scalar(connection,
            "SELECT IFNULL(SUM(qty_base), 0) FROM batches WHERE product_id = @id;",
            new { id = productId }) ?? 0d);

        Assert.Equal(5d, stock, 3);
        Assert.Equal(0L, Convert.ToInt64(test.Database.Scalar(connection,
            "SELECT COUNT(1) FROM sales;") ?? 0L));
        Assert.Equal(0L, Convert.ToInt64(test.Database.Scalar(connection,
            "SELECT COUNT(1) FROM audit_logs WHERE action = 'negative_sale';") ?? 0L));
    }

    [Fact]
    public void CashTendered_ComputesTheChange()
    {
        using var test = new TestDatabase();
        var (sales, productId, owner) = Build(test, costPaisa: 20000, retailPaisa: 30000);

        var input = One(productId);
        input.TenderedPaisa = 100000;

        var result = sales.Create(input, owner);

        Assert.True(result.Ok, result.Error);
        Assert.Equal(70000L, result.Value!.ChangePaisa);
    }

    [Fact]
    public void DiscountAboveTheRoleLimit_IsRefused()
    {
        using var test = new TestDatabase();
        var (sales, productId, _) = Build(test, costPaisa: 20000, retailPaisa: 30000);
        var cashier = new User { Id = 2, Name = "Cashier", Role = "cashier" };

        var result = sales.Create(One(productId, discount: 10), cashier);

        Assert.False(result.Ok);
        Assert.NotNull(result.Error);
    }

    [Fact]
    public void Fefo_UsesTheNearestExpiryBatchFirst()
    {
        using var test = new TestDatabase();
        var (sales, productId, owner) = Build(test, costPaisa: 10000, retailPaisa: 15000, batchQty: 5);

        test.Database.Tx(connection =>
        {
            test.Database.Execute(connection, @"
INSERT INTO batches(product_id, batch_no, expiry_date, expiry_ym, qty_base, cost_paisa, retail_paisa,
                    is_sample, active, created_at)
VALUES(@productId, 'B-2', '2028-01-31', '2028-01', 50, 10000, 15000, 0, 1, @at);",
                new { productId, at = Clock.Now() });
        });

        var result = sales.Create(One(productId, qty: 4), owner);
        Assert.True(result.Ok, result.Error);

        using var connection = test.Database.Open();
        var used = test.Database.QuerySingle<string>(connection,
            "SELECT b.batch_no FROM sale_items i JOIN batches b ON b.id = i.batch_id WHERE i.sale_id = @id LIMIT 1;",
            new { id = result.Value!.SaleId });

        Assert.Equal("B-1", used);
    }
}
