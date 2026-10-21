using Area11.Core.Models;
using Area11.Core.Services;
using Area11.Core.Utils;
using Xunit;

namespace Area11.Tests;

/// <summary>
/// The receipt is the only thing the customer takes home, so its shape is pinned here: nothing may
/// be wider than the paper, the total must be on it, and the printer must receive a clean ESC/POS
/// stream that ends with a cut.
///
/// The tests go through the public doorway - a real bill written to a real (in-memory) database and
/// read back by <see cref="ReceiptService.Build"/> - and not through the layout helper directly.
/// That also proves the column mapping: a renamed column would fail here before it reaches the shop.
/// </summary>
public sealed class ReceiptTests
{
    private const int ProductId = 1;

    private static ReceiptService Service(TestDatabase test) => new(test.Database, test.Settings, test.Log);

    /// <summary>Writes one product, one bill and its lines - the minimum the receipt needs.</summary>
    private static int SeedSale(
        TestDatabase test,
        long subtotal = 30000,
        long discount = 0,
        long total = 30000,
        long paid = 50000,
        long change = 20000,
        long due = 0,
        string method = "cash",
        string itemName = "Panadol 500mg")
    {
        return test.Database.Tx(connection =>
        {
            test.Database.Execute(connection, @"
INSERT INTO products(id, name, base_unit, box_strips, strip_tablets, cost_paisa, retail_paisa,
                     vip_paisa, doctor_paisa, reorder_level, track_expiry, active, created_at, updated_at)
VALUES(@id, 'Panadol 500mg', 'tablet', 2, 10, 20000, 30000, 0, 0, 10, 1, 1, @at, @at);",
                new { id = ProductId, at = Clock.Now() });

            test.Database.Execute(connection, @"
INSERT INTO sales(code, date, customer_id, user_id, subtotal_paisa, discount_paisa, tax_paisa,
                  round_off_paisa, total_paisa, paid_paisa, due_paisa, status, payment_method,
                  notes, created_at, change_paisa)
VALUES('INV-0001', '2026-10-20 14:32:00', NULL, 1, @subtotal, @discount, 0, 0, @total, @paid, @due,
       @status, @method, NULL, '2026-10-20 14:32:00', @change);",
                new
                {
                    subtotal,
                    discount,
                    total,
                    paid,
                    due,
                    status = due > 0 ? "credit" : "paid",
                    method,
                    change
                });

            var newSaleId = (int)test.Database.NextId(connection);

            test.Database.Execute(connection, @"
INSERT INTO sale_items(sale_id, product_id, batch_id, name_snapshot, qty_base, unit_sold, qty_entered,
                       unit_price_paisa, discount_paisa, line_total_paisa, cost_paisa_at_sale)
VALUES(@saleId, @productId, NULL, @name, @qty, 'strip', @qty, @price, 0, @lineTotal, 20000);",
                new
                {
                    saleId = newSaleId,
                    productId = ProductId,
                    name = itemName,
                    qty = 1,
                    price = total,
                    lineTotal = total
                });

            return newSaleId;
        });
    }

    private static ReceiptDocument Receipt(TestDatabase test, int saleId)
    {
        var result = Service(test).Build(saleId);
        Assert.True(result.Ok, result.Error);
        Assert.NotNull(result.Value);
        return result.Value!;
    }

    [Fact]
    public void FiftyEightMillimetrePaper_IsThirtyTwoCharactersWide()
    {
        using var test = new TestDatabase();
        test.Settings.Set("printer.width", 58, null);

        Assert.Equal(32, Service(test).PaperWidth);
    }

    [Fact]
    public void EightyMillimetrePaper_IsFortyEightCharactersWide()
    {
        using var test = new TestDatabase();
        test.Settings.Set("printer.width", 80, null);

        Assert.Equal(48, Service(test).PaperWidth);
    }

    [Theory]
    [InlineData(58)]
    [InlineData(80)]
    public void NoLine_IsEverWiderThanThePaper(int millimetres)
    {
        using var test = new TestDatabase();
        test.Settings.Set("printer.width", millimetres, null);
        var saleId = SeedSale(test);

        var document = Receipt(test, saleId);

        Assert.All(document.Lines, line => Assert.True(line.Text.Length <= document.Width,
            $"line is {line.Text.Length} characters: '{line.Text}'"));
    }

    [Fact]
    public void Receipt_CarriesShopNameBillNumberTotalAndChange()
    {
        using var test = new TestDatabase();
        test.Settings.Set("store.name", "Al-Shifa Pharmacy", null);
        var saleId = SeedSale(test);

        var text = Receipt(test, saleId).PlainText;

        Assert.Contains("Al-Shifa Pharmacy", text);
        Assert.Contains("INV-0001", text);
        Assert.Contains("300.00", text);          // total
        Assert.Contains("200.00", text);          // change
        Assert.Contains("TOTAL", text);
        Assert.Contains("Panadol 500mg", text);
    }

    [Fact]
    public void LongProductName_IsCutInsteadOfWrapping()
    {
        using var test = new TestDatabase();
        test.Settings.Set("printer.width", 58, null);
        var saleId = SeedSale(test, subtotal: 1000, total: 1000, paid: 1000, change: 0,
            itemName: new string('X', 80));

        var document = Receipt(test, saleId);

        Assert.Contains(document.Lines, line => line.Text.Length == 32 && line.Text.All(c => c == 'X'));
    }

    [Fact]
    public void Money_IsAlwaysTwoDecimalsOnPaper()
    {
        Assert.Equal("0.50", Money.Fixed(50));
        Assert.Equal("1,234.50", Money.Fixed(123450));
        Assert.Equal("300.00", Money.Fixed(30000));
    }

    [Fact]
    public void CreditSale_ShowsTheDueInsteadOfCash()
    {
        using var test = new TestDatabase();
        var saleId = SeedSale(test, paid: 0, change: 0, due: 30000, method: "credit");

        var text = Receipt(test, saleId).PlainText;

        Assert.Contains("On credit", text);
        Assert.DoesNotContain("Change", text);
    }

    [Fact]
    public void DisabledDiscountSetting_HidesTheDiscountLine()
    {
        using var test = new TestDatabase();
        test.Settings.Set("receipt.showDiscount", false, null);
        var saleId = SeedSale(test, discount: 5000, total: 25000, paid: 25000, change: 0);

        var text = Receipt(test, saleId).PlainText;

        Assert.DoesNotContain("Discount", text);
    }

    [Fact]
    public void EscPos_StartsCleanAndEndsWithACut()
    {
        using var test = new TestDatabase();
        test.Settings.Set("printer.autoCut", true, null);
        test.Settings.Set("printer.copies", 1, null);
        var document = Receipt(test, SeedSale(test));

        var bytes = EscPosBuilder.Build(document);

        Assert.Equal(0x1B, bytes[0]);
        Assert.Equal((byte)'@', bytes[1]);
        Assert.Equal(new byte[] { 0x1D, (byte)'V', 66, 0 }, bytes[^4..]);
    }

    [Fact]
    public void EscPos_PrintsEveryRequestedCopy()
    {
        using var test = new TestDatabase();
        test.Settings.Set("printer.copies", 2, null);
        test.Settings.Set("printer.autoCut", false, null);
        var document = Receipt(test, SeedSale(test));

        var bytes = EscPosBuilder.Build(document);

        var restarts = 0;
        for (var index = 0; index + 1 < bytes.Length; index++)
        {
            if (bytes[index] == 0x1B && bytes[index + 1] == (byte)'@')
            {
                restarts++;
            }
        }

        Assert.Equal(2, restarts);
    }

    [Fact]
    public void Build_RefusesAMissingBill()
    {
        using var test = new TestDatabase();

        var result = Service(test).Build(9999);

        Assert.False(result.Ok);
        Assert.NotNull(result.Error);
    }
}
