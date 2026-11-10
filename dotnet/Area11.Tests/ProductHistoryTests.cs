using Area11.Core.Models;
using Area11.Core.Services;
using Area11.Core.Utils;
using Xunit;

namespace Area11.Tests;

/// <summary>
/// Portion 13: the life story of one medicine. Every purchase names its supplier and rate, every
/// sale names its bill and buyer, and the profit on a line is exactly line-total minus quantity
/// times the cost snapshot - the approved definition, no expense allocation. Putting a product
/// away never touches its story, and the story never shows a margin to refused eyes.
/// </summary>
public sealed class ProductHistoryTests
{
    // ============================== the story ==============================

    [Fact]
    public void APurchaseNamesItsSupplierAndRate()
    {
        using var test = new TestDatabase();
        var productId = TestData.AddProduct(test, name: "Panadol 500mg");
        Buy(test, productId);

        var history = Histories(test).Load(TestData.Owner, productId).Value!;

        Assert.True(history.HasPurchases);
        var row = Assert.Single(history.Purchases);
        Assert.Equal("GSK Depot", row.SupplierName);
        Assert.Equal(10000, row.RatePaisa);
        Assert.Equal(10, row.QtyBase);
    }

    [Fact]
    public void ASaleNamesItsBillAndBuyer()
    {
        using var test = new TestDatabase();
        var productId = TestData.AddProduct(test, name: "Panadol 500mg");
        var customer = TestData.AddCustomer(test, "Ali Raza");
        var sale = Sell(test, productId, customer);

        var history = Histories(test).Load(TestData.Owner, productId).Value!;

        Assert.True(history.HasSales);
        var row = Assert.Single(history.Sales);
        Assert.Equal(sale.Code, row.BillCode);
        Assert.Equal("Ali Raza", row.BuyerName);
    }

    [Fact]
    public void AWalkInSaleHasNoBuyer()
    {
        using var test = new TestDatabase();
        var productId = TestData.AddProduct(test, name: "Panadol 500mg");
        Sell(test, productId, null);

        var history = Histories(test).Load(TestData.Owner, productId).Value!;

        Assert.Equal("walk-in", Assert.Single(history.Sales).BuyerLabel);
    }

    [Fact]
    public void TheStoryRunsOldestFirst()
    {
        using var test = new TestDatabase();
        var productId = TestData.AddProduct(test, name: "Panadol 500mg");
        Buy(test, productId);
        Buy(test, productId);

        var history = Histories(test).Load(TestData.Owner, productId).Value!;

        Assert.Equal(2, history.Purchases.Count);
        Assert.True(string.Compare(history.Purchases[0].Date, history.Purchases[1].Date, StringComparison.Ordinal) <= 0);
    }

    [Fact]
    public void TheTotalsAddUp()
    {
        using var test = new TestDatabase();
        // batchQty 0: the bought batch is the only stock, so the sale costs at 10000 (the seed batch
        // costs 20000 and FIFO would price the sale below its 15000 rate - rightly refused).
        var productId = TestData.AddProduct(test, batchQty: 0, name: "Panadol 500mg");
        Buy(test, productId);
        Sell(test, productId, null);

        var history = Histories(test).Load(TestData.Owner, productId).Value!;

        Assert.Equal(10, history.BoughtQty);
        Assert.Equal(1, history.SoldQty);
        Assert.Equal(history.Sales[0].ProfitPaisa, history.ProfitPaisa);
    }

    [Fact]
    public void ProfitIsLineTotalMinusCostAtSale()
    {
        using var test = new TestDatabase();
        // batchQty 0: the bought batch is the only stock, so the sale costs at 10000 (the seed batch
        // costs 20000 and FIFO would price the sale below its 15000 rate - rightly refused).
        var productId = TestData.AddProduct(test, batchQty: 0, name: "Panadol 500mg");
        Buy(test, productId);
        Sell(test, productId, null);

        var history = Histories(test).Load(TestData.Owner, productId).Value!;

        // Bought at 10000/base, sold one base at the cart's unit price: the line knows both.
        var row = Assert.Single(history.Sales);
        Assert.Equal(row.LineTotalPaisa - row.QtyBase * 10000, row.ProfitPaisa);
    }

    [Fact]
    public void APutAwayProductKeepsItsStory()
    {
        using var test = new TestDatabase();
        var productId = TestData.AddProduct(test, name: "Panadol 500mg");
        Buy(test, productId);
        var putAway = new ProductService(test.Database, test.Audit, test.Log).Deactivate(TestData.Owner, productId);
        Assert.True(putAway.Ok, putAway.Error);

        var history = Histories(test).Load(TestData.Owner, productId);

        Assert.True(history.Ok, history.Error);
        Assert.True(history.Value!.HasPurchases);
    }

    [Fact]
    public void AnUnknownProductIsRefused()
    {
        using var test = new TestDatabase();

        var history = Histories(test).Load(TestData.Owner, 999999);

        Assert.False(history.Ok);
    }

    // ============================== the gates ==============================

    [Fact]
    public void ACashierCannotLoadAnyHistory()
    {
        using var test = new TestDatabase();
        var productId = TestData.AddProduct(test, name: "Panadol 500mg");

        var history = Histories(test).Load(TestData.Cashier(test), productId);

        Assert.False(history.Ok);
    }

    [Fact]
    public void AManagerSeesTheStoryButNotTheProfit()
    {
        using var test = new TestDatabase();
        var productId = TestData.AddProduct(test, name: "Panadol 500mg");
        var manager = TestData.AddUser(test, "manager");

        var history = Histories(test).Load(manager, productId);

        Assert.True(history.Ok, history.Error);
        Assert.False(history.Value!.CanSeeProfit);
    }

    [Fact]
    public void TheGlobalSwitchHidesProfitEvenFromTheOwner()
    {
        using var test = new TestDatabase();
        var productId = TestData.AddProduct(test, name: "Panadol 500mg");
        var stored = test.Settings.Set(MasterFoundationSettings.ProfitShowMargins, false, TestData.Owner);
        Assert.True(stored.Ok, stored.Error);

        var history = Histories(test).Load(TestData.Owner, productId);

        Assert.True(history.Ok, history.Error);
        Assert.False(history.Value!.CanSeeProfit);
    }

    [Fact]
    public void ThePaperHidesProfitFromRefusedEyes()
    {
        using var test = new TestDatabase();
        // batchQty 0: the bought batch is the only stock, so the sale costs at 10000 (the seed batch
        // costs 20000 and FIFO would price the sale below its 15000 rate - rightly refused).
        var productId = TestData.AddProduct(test, batchQty: 0, name: "Panadol 500mg");
        Buy(test, productId);
        Sell(test, productId, null);
        var manager = TestData.AddUser(test, "manager");

        var hidden = Histories(test).BuildDocument(manager, productId);
        var shown = Histories(test).BuildDocument(TestData.Owner, productId);

        Assert.True(hidden.Ok, hidden.Error);
        Assert.True(shown.Ok, shown.Error);
        Assert.DoesNotContain("Profit", hidden.Value!.PlainText);
        Assert.Contains("Profit", shown.Value!.PlainText);
    }

    [Fact]
    public void TheCsvCarriesPlainNumbers()
    {
        using var test = new TestDatabase();
        // batchQty 0: the bought batch is the only stock, so the sale costs at 10000 (the seed batch
        // costs 20000 and FIFO would price the sale below its 15000 rate - rightly refused).
        var productId = TestData.AddProduct(test, batchQty: 0, name: "Panadol 500mg");
        Buy(test, productId);
        var sale = Sell(test, productId, null);

        var csv = Histories(test).BuildCsv(TestData.Owner, productId);

        Assert.True(csv.Ok, csv.Error);
        Assert.StartsWith("type,date,document,partner,qty,rate_paisa,total_paisa,profit_paisa", csv.Value!);
        Assert.Contains(sale.Code, csv.Value);
    }

    [Fact]
    public void TheCsvHidesProfitFromRefusedEyes()
    {
        using var test = new TestDatabase();
        // batchQty 0: the bought batch is the only stock, so the sale costs at 10000 (the seed batch
        // costs 20000 and FIFO would price the sale below its 15000 rate - rightly refused).
        var productId = TestData.AddProduct(test, batchQty: 0, name: "Panadol 500mg");
        Buy(test, productId);
        Sell(test, productId, null);
        var manager = TestData.AddUser(test, "manager");

        var csv = Histories(test).BuildCsv(manager, productId);

        Assert.True(csv.Ok, csv.Error);
        Assert.Contains(",\"\"", csv.Value);
    }

    // ============================== helpers ==============================

    private static ProductHistoryService Histories(TestDatabase test) =>
        new(test.Database, test.Settings, TestData.Permissions(test), test.Log);

    private static void Buy(TestDatabase test, int productId)
    {
        var created = TestData.Purchases(test).Create(new PurchaseInput
        {
            SupplierId = TestData.AddSupplier(test),
            Lines = new List<PurchaseLineInput>
            {
                new()
                {
                    ProductId = productId,
                    Unit = "base",
                    Quantity = 10,
                    CostPaisa = 10000,
                    RetailPaisa = 15000,
                    BatchNo = "B-1",
                    Expiry = "2027-06"
                }
            }
        }, TestData.Owner);

        Assert.True(created.Ok, created.Error);
    }

    private static SaleResult Sell(TestDatabase test, int productId, int? customerId)
    {
        var created = TestData.Sales(test).Create(
            TestData.Cart(productId, 1, "cash", customerId), TestData.Owner);

        Assert.True(created.Ok, created.Error);

        return created.Value!;
    }
}
