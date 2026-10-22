using Area11.Core.Models;
using Area11.Core.Services;
using Area11.Core.Utils;
using Xunit;

namespace Area11.Tests;

/// <summary>
/// Expenses and owner drawings (F-32, Spec 9.1 / 14.4). The promises that must hold:
///   • an expense is **not** a purchase - it never raises a supplier's payable and never adds stock;
///   • a **cash** drawing leaves the drawer, a **goods** drawing leaves the shelf at *cost* and does
///     **not** touch the cash;
///   • both reach the shift/galla count, which is the whole reason the feature exists;
///   • a cashier cannot take the owner's money, and a manager cannot either;
///   • the entries are never edited or deleted - a mistake is a second entry.
/// </summary>
public sealed class ExpenseTests
{
    private static ShiftService Shifts(TestDatabase test) =>
        new(test.Database, test.Audit,
            new PermissionService(test.Database, test.Log, test.Audit), test.Log);

    private static readonly User Manager = new() { Id = 3, Name = "Manager", Role = "manager" };

    private static readonly User Cashier = new() { Id = 2, Name = "Cashier", Role = "cashier" };

    private static double StockOf(TestDatabase test, int productId)
    {
        using var connection = test.Database.Open();
        return test.Database.QuerySingle<double>(connection,
            "SELECT IFNULL(SUM(qty_base), 0) FROM batches WHERE product_id = @id AND active = 1;",
            new { id = productId });
    }

    private static long ExpenseRows(TestDatabase test)
    {
        using var connection = test.Database.Open();
        return test.Database.QuerySingle<long>(connection, "SELECT COUNT(1) FROM expenses;");
    }

    private static long StoredPayable(TestDatabase test, int supplierId)
    {
        using var connection = test.Database.Open();
        return test.Database.QuerySingle<long>(connection,
            "SELECT balance_paisa FROM suppliers WHERE id = @id;", new { id = supplierId });
    }

    private static ExpenseInput Rent(long paisa = 150000, string title = "August rent") => new()
    {
        Category = "rent",
        Title = title,
        AmountPaisa = paisa
    };

    [Fact]
    public void AnExpense_IsRecorded_AndSaysWhatItWasFor()
    {
        using var test = new TestDatabase();

        var result = TestData.CashOut(test).AddExpense(TestData.Owner, Rent(150000));

        Assert.True(result.Ok, result.Error);
        Assert.Equal(150000L, result.Value!.AmountPaisa);
        Assert.True(result.Value.Id > 0);

        using var connection = test.Database.Open();
        var row = test.Database.QuerySingle<ExpenseRow>(connection, @"
SELECT category AS category, title AS title, amount_paisa AS amountPaisa FROM expenses WHERE id = @id;",
            new { id = result.Value.Id });

        Assert.Equal("rent", row!.Category);
        Assert.Equal("August rent", row.Title);
        Assert.Equal(150000L, row.AmountPaisa);
    }

    [Fact]
    public void AnExpense_IsNotAPurchase_ItNeverRaisedAPayableOrStock()
    {
        using var test = new TestDatabase();
        var supplier = TestData.AddSupplier(test, openingPaisa: 500000);
        var product = TestData.AddProduct(test, batchQty: 40);

        var result = TestData.CashOut(test).AddExpense(TestData.Owner, Rent());
        Assert.True(result.Ok, result.Error);

        Assert.Equal(500000L, StoredPayable(test, supplier));     // the supplier's account is untouched
        Assert.Equal(40d, StockOf(test, product));                // and so is the shelf

        using var connection = test.Database.Open();
        Assert.Equal(0L, test.Database.QuerySingle<long>(connection, "SELECT COUNT(1) FROM purchases;"));
    }

    [Fact]
    public void AZeroAmount_OrAnEmptyTitle_IsRefused()
    {
        using var test = new TestDatabase();

        var zero = TestData.CashOut(test).AddExpense(TestData.Owner, Rent(paisa: 0));
        Assert.False(zero.Ok);

        var noTitle = TestData.CashOut(test).AddExpense(TestData.Owner, Rent(title: "  r  "));
        Assert.False(noTitle.Ok);

        Assert.Equal(0L, ExpenseRows(test));                      // nothing half-written
    }

    [Fact]
    public void ACashierOrAManager_CannotRecordAnExpense_WithoutTheCashPermission()
    {
        using var test = new TestDatabase();

        var cashier = TestData.Cashier(test);

        var refused = TestData.CashOut(test).AddExpense(cashier, Rent());

        Assert.False(refused.Ok);
        Assert.Contains("permission", refused.Error!, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(0L, ExpenseRows(test));

        // The owner has every permission, and a manager gets Cash by default.
        Assert.True(TestData.CashOut(test).AddExpense(TestData.Owner, Rent()).Ok);
        Assert.True(TestData.CashOut(test).AddExpense(Manager, Rent(title: "Electricity bill")).Ok);
    }

    [Fact]
    public void AnExpense_LowersTheDrawerCount()
    {
        using var test = new TestDatabase();

        Assert.True(Shifts(test).Open(TestData.Owner, 100000, null).Ok);          // float Rs 1,000
        Assert.True(TestData.CashOut(test).AddExpense(TestData.Owner, Rent(15000)).Ok);

        var shift = Shifts(test).Current()!;

        Assert.Equal(15000L, shift.ExpensesPaisa);
        Assert.Equal(85000L, shift.ExpectedPaisa);                                 // 1,000 - 150
    }

    [Fact]
    public void ACashDrawing_LowersTheDrawerCount_AndNeedsTheOwner()
    {
        using var test = new TestDatabase();

        Assert.True(Shifts(test).Open(TestData.Owner, 200000, null).Ok);

        var byCashier = TestData.CashOut(test).AddDrawing(Cashier, new DrawingInput
        {
            Type = "cash",
            AmountPaisa = 50000
        });

        Assert.False(byCashier.Ok);
        Assert.Contains("owner", byCashier.Error!, StringComparison.OrdinalIgnoreCase);

        var byManager = TestData.CashOut(test).AddDrawing(Manager, new DrawingInput
        {
            Type = "cash",
            AmountPaisa = 50000
        });

        Assert.False(byManager.Ok);

        var saved = TestData.CashOut(test).AddDrawing(TestData.Owner, new DrawingInput
        {
            Type = "cash",
            AmountPaisa = 50000,
            Note = "home"
        });

        Assert.True(saved.Ok, saved.Error);

        var shift = Shifts(test).Current()!;
        Assert.Equal(50000L, shift.DrawingsPaisa);
        Assert.Equal(150000L, shift.ExpectedPaisa);                                // 2,000 - 500
    }

    [Fact]
    public void AGoodsDrawing_TakesStockAtCost_AndLeavesTheDrawerAlone()
    {
        using var test = new TestDatabase();
        var product = TestData.AddProduct(test, costPaisa: 20000, retailPaisa: 30000, batchQty: 100);

        Assert.True(Shifts(test).Open(TestData.Owner, 100000, null).Ok);

        var saved = TestData.CashOut(test).AddDrawing(TestData.Owner, new DrawingInput
        {
            Type = "goods",
            ProductId = product,
            QtyEntered = 5,
            Unit = "unit",
            Note = "for my mother"
        });

        Assert.True(saved.Ok, saved.Error);

        // 5 tablets x Rs 200 cost = Rs 1,000 - NOT the Rs 1,500 retail price: no profit is booked.
        Assert.Equal(100000L, saved.Value!.AmountPaisa);
        Assert.Equal(95d, StockOf(test, product));

        var shift = Shifts(test).Current()!;
        Assert.Equal(0L, shift.DrawingsPaisa);                                     // no cash moved
        Assert.Equal(100000L, shift.ExpectedPaisa);                                // the float, untouched

        using var connection = test.Database.Open();
        var movement = test.Database.QuerySingle<MovementRow>(connection, @"
SELECT type AS type, qty_base AS qtyBase FROM stock_movements WHERE product_id = @id ORDER BY id DESC LIMIT 1;",
            new { id = product });

        Assert.Equal("drawing", movement!.Type);
        Assert.Equal(-5d, movement.QtyBase);
    }

    [Fact]
    public void AGoodsDrawing_BiggerThanTheShelf_IsAllowedWithAWarning()
    {
        using var test = new TestDatabase();
        var product = TestData.AddProduct(test, batchQty: 3);

        var saved = TestData.CashOut(test).AddDrawing(TestData.Owner, new DrawingInput
        {
            Type = "goods",
            ProductId = product,
            QtyEntered = 10,
            Unit = "unit"
        });

        Assert.True(saved.Ok, saved.Error);
        Assert.True(saved.Value!.HasWarning);
        Assert.Contains("MINUS", saved.Value.WarningText, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(-7d, StockOf(test, product));

        // …unless the owner switched negative stock off (U-30 is a setting, not a law).
        Assert.True(test.Settings.Set("stock.allowNegative", false, TestData.Owner).Ok);

        var refused = TestData.CashOut(test).AddDrawing(TestData.Owner, new DrawingInput
        {
            Type = "goods",
            ProductId = product,
            QtyEntered = 1,
            Unit = "unit"
        });

        Assert.False(refused.Ok);
        Assert.Equal(-7d, StockOf(test, product));
    }

    [Fact]
    public void AnEntryWithNoOpenShift_IsSavedWithAWarning()
    {
        using var test = new TestDatabase();

        var expense = TestData.CashOut(test).AddExpense(TestData.Owner, Rent());
        Assert.True(expense.Ok, expense.Error);
        Assert.True(expense.Value!.HasWarning);
        Assert.Contains("No shift", expense.Value.WarningText, StringComparison.OrdinalIgnoreCase);

        var drawing = TestData.CashOut(test).AddDrawing(TestData.Owner, new DrawingInput
        {
            Type = "cash",
            AmountPaisa = 10000
        });

        Assert.True(drawing.Ok, drawing.Error);
        Assert.True(drawing.Value!.HasWarning);
    }

    [Fact]
    public void TheMeaningOfOneEntry_IsStillReadableOnTheList()
    {
        using var test = new TestDatabase();
        var product = TestData.AddProduct(test, batchQty: 20);

        Assert.True(TestData.CashOut(test).AddExpense(TestData.Owner, Rent(120000)).Ok);
        Assert.True(TestData.CashOut(test).AddDrawing(TestData.Owner, new DrawingInput
        {
            Type = "cash",
            AmountPaisa = 30000
        }).Ok);
        Assert.True(TestData.CashOut(test).AddDrawing(TestData.Owner, new DrawingInput
        {
            Type = "goods",
            ProductId = product,
            QtyEntered = 2,
            Unit = "unit"
        }).Ok);

        var rows = TestData.CashOut(test).List();

        Assert.Equal(3, rows.Count);

        // All three were saved inside the same second, so the order is the documented one:
        // expenses first, then drawings, newest id first inside each group. The point of the test
        // is that the order is the same on every read - not which group wins.
        Assert.Equal(new[] { "expense", "drawing_goods", "drawing_cash" }, rows.Select(row => row.Kind));
        Assert.Equal(rows.Select(row => row.Kind), TestData.CashOut(test).List().Select(row => row.Kind));

        var expense = rows[0];
        Assert.Equal("Rent", expense.KindLabel);
        Assert.Equal("Rs 1,200", expense.CashLabel);
        Assert.Equal("Owner", expense.WhoLabel);

        var goods = rows[1];
        Assert.False(goods.TookCash);
        Assert.Equal("stock only", goods.CashLabel);
        Assert.Contains("Panadol", goods.DetailLabel);

        var cashDrawing = rows[2];
        Assert.True(cashDrawing.TookCash);
        Assert.Equal("Rs 300", cashDrawing.CashLabel);
        Assert.Equal("Owner took cash", cashDrawing.KindLabel);
    }

    [Fact]
    public void TodaysTotals_KeepGoodsApartFromCash()
    {
        using var test = new TestDatabase();
        var product = TestData.AddProduct(test, costPaisa: 20000, batchQty: 20);

        Assert.True(TestData.CashOut(test).AddExpense(TestData.Owner, Rent(100000)).Ok);
        Assert.True(TestData.CashOut(test).AddDrawing(TestData.Owner, new DrawingInput
        {
            Type = "cash",
            AmountPaisa = 50000
        }).Ok);
        Assert.True(TestData.CashOut(test).AddDrawing(TestData.Owner, new DrawingInput
        {
            Type = "goods",
            ProductId = product,
            QtyEntered = 3,
            Unit = "unit"
        }).Ok);

        var today = TestData.CashOut(test).Today();

        Assert.Equal(100000L, today.ExpensePaisa);
        Assert.Equal(50000L, today.DrawingCashPaisa);
        Assert.Equal(60000L, today.DrawingGoodsCostPaisa);       // 3 x Rs 200, at cost
        Assert.Equal(150000L, today.CashOutPaisa);               // goods are NOT in the cash figure
        Assert.Equal(3, today.Entries);
    }

    [Fact]
    public void BothEntries_AreWrittenIntoTheBlackBox()
    {
        using var test = new TestDatabase();

        Assert.True(TestData.CashOut(test).AddExpense(TestData.Owner, Rent()).Ok);
        Assert.True(TestData.CashOut(test).AddDrawing(TestData.Owner, new DrawingInput
        {
            Type = "cash",
            AmountPaisa = 10000
        }).Ok);

        using var connection = test.Database.Open();
        var actions = test.Database.Query<string>(connection, @"
SELECT action FROM audit_logs WHERE module = 'Cash' ORDER BY id;");

        Assert.Contains("expense", actions);
        Assert.Contains("owner_drawing", actions);
    }

    /// <summary>The columns this file reads back; the SQL aliases match the property names.</summary>
    public sealed class ExpenseRow
    {
        public string Category { get; set; } = string.Empty;

        public string Title { get; set; } = string.Empty;

        public long AmountPaisa { get; set; }
    }

    public sealed class MovementRow
    {
        public string Type { get; set; } = string.Empty;

        public double QtyBase { get; set; }
    }
}
