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
    public void MarginDiscount_IsTenPercentOfProfit()
    {
        using var test = new TestDatabase();
        var (sales, productId, owner) = Build(test, costPaisa: 20000, retailPaisa: 30000);

        var result = sales.Create(One(productId, discount: 10), owner);

        Assert.True(result.Ok, result.Error);
        Assert.Equal(29000L, result.Value!.TotalPaisa);
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
    public void NegativeStock_DoesNotBlockTheSale_AndWarns()
    {
        using var test = new TestDatabase();
        var (sales, productId, owner) = Build(test, costPaisa: 20000, retailPaisa: 30000, batchQty: 5);

        var result = sales.Create(One(productId, qty: 12), owner);

        Assert.True(result.Ok, result.Error);
        Assert.NotEmpty(result.Warnings);

        using var connection = test.Database.Open();
        var stock = Convert.ToDouble(test.Database.Scalar(connection,
            "SELECT IFNULL(SUM(qty_base), 0) FROM batches WHERE product_id = @id;",
            new { id = productId }) ?? 0d);

        Assert.Equal(-7d, stock, 3);
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
