using Area11.Core.Models;
using Area11.Core.Services;
using Area11.Core.Utils;
using Xunit;

namespace Area11.Tests;

/// <summary>
/// The receipt is the only thing the customer takes home, so its shape is pinned here:
/// nothing may be wider than the paper, the total must be on it, and the printer must receive
/// a clean ESC/POS stream that ends with a cut.
/// </summary>
public sealed class ReceiptTests
{
    private static ReceiptService Build(TestDatabase test) =>
        new(test.Database, test.Settings, test.Log);

    private static ReceiptService.SaleRow Sale(long total = 30000, long discount = 0, long paid = 50000,
        long change = 20000, long due = 0, string method = "cash") => new()
    {
        Id = 1,
        Code = "INV-0001",
        Date = "2026-10-20 14:32:00",
        SubtotalPaisa = 30000,
        DiscountPaisa = discount,
        TaxPaisa = 0,
        RoundOffPaisa = 0,
        TotalPaisa = total,
        PaidPaisa = paid,
        DuePaisa = due,
        ChangePaisa = change,
        PaymentMethod = method,
        Status = "paid"
    };

    private static List<ReceiptService.ItemRow> Items() => new()
    {
        new()
        {
            NameSnapshot = "Panadol 500mg",
            QtyEntered = 1,
            UnitSold = "strip",
            UnitPricePaisa = 30000,
            LineTotalPaisa = 30000
        }
    };

    private static ReceiptDocument Compose(TestDatabase test, ReceiptService.SaleRow sale,
        List<ReceiptService.ItemRow>? items = null) =>
        Build(test).Compose(sale, items ?? Items(), "Owner", "Walk-in");

    [Fact]
    public void FiftyEightMillimetrePaper_IsThirtyTwoCharactersWide()
    {
        using var test = new TestDatabase();
        test.Settings.Set("printer.width", 58, null);

        Assert.Equal(32, Build(test).PaperWidth);
    }

    [Fact]
    public void EightyMillimetrePaper_IsFortyEightCharactersWide()
    {
        using var test = new TestDatabase();
        test.Settings.Set("printer.width", 80, null);

        Assert.Equal(48, Build(test).PaperWidth);
    }

    [Theory]
    [InlineData("58")]
    [InlineData("80")]
    public void NoLine_IsEverWiderThanThePaper(string width)
    {
        using var test = new TestDatabase();
        test.Settings.Set("printer.width", int.Parse(width), null);

        var document = Compose(test, Sale());

        Assert.All(document.Lines, line => Assert.True(line.Text.Length <= document.Width,
            $"line is {line.Text.Length} characters: '{line.Text}'"));
    }

    [Fact]
    public void Receipt_CarriesShopNameBillNumberTotalAndChange()
    {
        using var test = new TestDatabase();
        test.Settings.Set("store.name", "Al-Shifa Pharmacy", null);

        var text = Compose(test, Sale()).PlainText;

        Assert.Contains("Al-Shifa Pharmacy", text);
        Assert.Contains("INV-0001", text);
        Assert.Contains("300.00", text);
        Assert.Contains("200.00", text);          // change
        Assert.Contains("TOTAL", text);
        Assert.Contains("Panadol 500mg", text);
    }

    [Fact]
    public void LongProductName_IsCutInsteadOfWrapping()
    {
        using var test = new TestDatabase();
        var items = new List<ReceiptService.ItemRow>
        {
            new()
            {
                NameSnapshot = new string('X', 80),
                QtyEntered = 2,
                UnitSold = "tablet",
                UnitPricePaisa = 500,
                LineTotalPaisa = 1000
            }
        };

        var document = Compose(test, Sale(1000, 0, 1000, 0), items);

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
        var text = Compose(test, Sale(paid: 0, change: 0, due: 30000, method: "credit")).PlainText;

        Assert.Contains("On credit", text);
        Assert.DoesNotContain("Change", text);
    }

    [Fact]
    public void DisabledDiscountSetting_HidesTheDiscountLine()
    {
        using var test = new TestDatabase();
        test.Settings.Set("receipt.showDiscount", false, null);

        var text = Compose(test, Sale(discount: 5000, total: 25000, paid: 25000, change: 0)).PlainText;

        Assert.DoesNotContain("Discount", text);
    }

    [Fact]
    public void EscPos_StartsCleanAndEndsWithACut()
    {
        using var test = new TestDatabase();
        var document = Compose(test, Sale());
        document.AutoCut = true;

        var bytes = EscPosBuilder.Build(document);

        Assert.Equal(0x1B, bytes[0]);
        Assert.Equal((byte)'@', bytes[1]);

        var tail = bytes[^4..];
        Assert.Equal(new byte[] { 0x1D, (byte)'V', 66, 0 }, tail);
    }

    [Fact]
    public void EscPos_PrintsEveryRequestedCopy()
    {
        using var test = new TestDatabase();
        var document = Compose(test, Sale());
        document.Copies = 2;
        document.AutoCut = false;

        var bytes = EscPosBuilder.Build(document);

        // each copy starts with ESC @
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

        var result = Build(test).Build(9999);

        Assert.False(result.Ok);
        Assert.NotNull(result.Error);
    }
}
