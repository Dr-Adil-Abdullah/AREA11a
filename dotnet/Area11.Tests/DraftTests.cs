using Area11.Core.Models;
using Area11.Core.Services;
using Xunit;

namespace Area11.Tests;

/// <summary>
/// Safe local sale/purchase drafts (master Portion 5, Q1=A, D-100 shared). These tests pin the five
/// promises: a draft changes nothing, the final post runs the same guarded Create, drafts are shared
/// by page permission, a post can never happen twice, and a failed post keeps the work as a draft.
/// </summary>
public sealed class DraftTests
{
    private static SaleInput TwoLines(int panadol, int brufen, int? customerId = null) => new()
    {
        CustomerId = customerId,
        PaymentMethod = "cash",
        DiscountPercent = 5,
        TenderedPaisa = 100000,
        Lines = new List<SaleLineInput>
        {
            new() { ProductId = panadol, Unit = "strip", Quantity = 2 },
            new() { ProductId = brufen, Unit = "base", Quantity = 7 }
        }
    };

    private static PurchaseInput Delivery(int productId, int supplierId) => new()
    {
        SupplierId = supplierId,
        PaidPaisa = 5000,
        Lines = new List<PurchaseLineInput>
        {
            new()
            {
                ProductId = productId,
                Unit = "base",
                Quantity = 2,
                CostPaisa = 10000,
                RetailPaisa = 15000,
                BatchNo = "B-7",
                Expiry = "2027-06"
            }
        }
    };

    private static long Count(TestDatabase test, string table)
    {
        using var connection = test.Database.Open();
        return Convert.ToInt64(test.Database.Scalar(connection, $"SELECT COUNT(1) FROM {table};") ?? 0L);
    }

    private static string DraftStatus(TestDatabase test, int draftId)
    {
        using var connection = test.Database.Open();
        return test.Database.Scalar(connection,
            "SELECT status FROM document_drafts WHERE id = @id;", new { id = draftId })?.ToString()
            ?? string.Empty;
    }

    [Fact]
    public void ASaleDraft_ChangesNothing()
    {
        using var test = new TestDatabase();
        var product = TestData.AddProduct(test);
        var customer = TestData.AddCustomer(test);

        var saved = TestData.Drafts(test).SaveSaleDraft(TestData.Owner, TwoLines(product, product, customer));
        Assert.True(saved.Ok, saved.Error);

        // Not a sale, not a payment, not a stock movement - only a draft row.
        Assert.Equal(0, Count(test, "sales"));
        Assert.Equal(0, Count(test, "payments"));
        Assert.Equal(0, Count(test, "stock_movements"));
        Assert.Equal(1, Count(test, "document_drafts"));
        Assert.Equal("draft", DraftStatus(test, saved.Value!.Id));

        // The customer's money is untouched too.
        using var connection = test.Database.Open();
        var balance = Convert.ToInt64(test.Database.Scalar(connection,
            "SELECT balance_paisa FROM customers WHERE id = @id;", new { id = customer }) ?? 0L);
        Assert.Equal(0, balance);
    }

    [Fact]
    public void ASaleDraft_ComesBackWhole()
    {
        using var test = new TestDatabase();
        var panadol = TestData.AddProduct(test);
        var brufen = TestData.AddProduct(test, name: "Brufen 400mg", retailPaisa: 25000);
        var customer = TestData.AddCustomer(test);
        var drafts = TestData.Drafts(test);

        var saved = drafts.SaveSaleDraft(TestData.Owner, TwoLines(panadol, brufen, customer));
        Assert.True(saved.Ok, saved.Error);

        var opened = drafts.OpenSale(TestData.Owner, saved.Value!.Id);
        Assert.True(opened.Ok, opened.Error);

        var input = opened.Value!;
        Assert.Equal(customer, input.CustomerId);
        Assert.Equal(5, input.DiscountPercent);
        Assert.Equal(100000, input.TenderedPaisa);
        Assert.Equal(2, input.Lines.Count);
        Assert.Equal(panadol, input.Lines[0].ProductId);
        Assert.Equal("strip", input.Lines[0].Unit);
        Assert.Equal(2, input.Lines[0].Quantity);
        Assert.Equal(brufen, input.Lines[1].ProductId);
        Assert.Equal(7, input.Lines[1].Quantity);
    }

    [Fact]
    public void ASaleDraft_CanBeUpdatedBeforePosting()
    {
        using var test = new TestDatabase();
        var product = TestData.AddProduct(test);
        var drafts = TestData.Drafts(test);

        var saved = drafts.SaveSaleDraft(TestData.Owner, TestData.Cart(product, qty: 1));
        Assert.True(saved.Ok, saved.Error);

        var cart = TestData.Cart(product, qty: 3);
        var updated = drafts.SaveSaleDraft(TestData.Owner, cart, saved.Value!.Id);
        Assert.True(updated.Ok, updated.Error);
        Assert.Equal(saved.Value.Id, updated.Value!.Id);

        // Still one row, with the new quantity.
        Assert.Equal(1, Count(test, "document_drafts"));
        var opened = drafts.OpenSale(TestData.Owner, saved.Value.Id);
        Assert.True(opened.Ok, opened.Error);
        Assert.Equal(3, opened.Value!.Lines[0].Quantity);
    }

    [Fact]
    public void ASecondPerson_CanOpenAndPostASaleDraft()
    {
        using var test = new TestDatabase();
        var product = TestData.AddProduct(test);
        var cashier = TestData.Cashier(test);
        var drafts = TestData.Drafts(test);

        // The owner parks it at night; the cashier sees it in the morning (D-100: shared, not hidden).
        var saved = drafts.SaveSaleDraft(TestData.Owner, TestData.Cart(product, qty: 2));
        Assert.True(saved.Ok, saved.Error);

        var list = drafts.List(cashier, DraftType.Sale);
        Assert.True(list.Ok, list.Error);
        Assert.Single(list.Value!);
        Assert.Equal("Owner", list.Value![0].CreatedByName);
        Assert.Equal(1, list.Value![0].Lines);

        var posted = drafts.PostSale(cashier, saved.Value!.Id);
        Assert.True(posted.Ok, posted.Error);
        Assert.StartsWith("INV-", posted.Value!.Code);

        Assert.Equal(1, Count(test, "sales"));
        Assert.Equal("posted", DraftStatus(test, saved.Value.Id));
        using var connection = test.Database.Open();
        var documentId = Convert.ToInt64(test.Database.Scalar(connection,
            "SELECT posted_document_id FROM document_drafts WHERE id = @id;", new { id = saved.Value.Id }) ?? 0L);
        Assert.Equal(posted.Value.SaleId, documentId);
    }

    [Fact]
    public void PostingTwice_IsRefused()
    {
        using var test = new TestDatabase();
        var product = TestData.AddProduct(test);
        var drafts = TestData.Drafts(test);

        var saved = drafts.SaveSaleDraft(TestData.Owner, TestData.Cart(product));
        Assert.True(saved.Ok, saved.Error);

        var first = drafts.PostSale(TestData.Owner, saved.Value!.Id);
        Assert.True(first.Ok, first.Error);

        var second = drafts.PostSale(TestData.Owner, saved.Value.Id);
        Assert.False(second.Ok);
        Assert.Equal(1, Count(test, "sales"));
    }

    [Fact]
    public void AFailedPost_KeepsTheDraft()
    {
        using var test = new TestDatabase();
        var product = TestData.AddProduct(test);
        var drafts = TestData.Drafts(test);

        // Saving is lenient on purpose: the unknown product is only caught by the guarded Create.
        var cart = TestData.Cart(product);
        cart.Lines.Add(new SaleLineInput { ProductId = 9999, Unit = "base", Quantity = 1 });
        var saved = drafts.SaveSaleDraft(TestData.Owner, cart);
        Assert.True(saved.Ok, saved.Error);

        var posted = drafts.PostSale(TestData.Owner, saved.Value!.Id);
        Assert.False(posted.Ok);
        Assert.Equal(0, Count(test, "sales"));

        // The work is back as a draft; fixing the cart and posting again works.
        Assert.Equal("draft", DraftStatus(test, saved.Value.Id));
        var mended = drafts.SaveSaleDraft(TestData.Owner, TestData.Cart(product), saved.Value.Id);
        Assert.True(mended.Ok, mended.Error);
        var retry = drafts.PostSale(TestData.Owner, saved.Value.Id);
        Assert.True(retry.Ok, retry.Error);
        Assert.Equal(1, Count(test, "sales"));
    }

    [Fact]
    public void APurchaseDraft_ChangesNothingUntilPosted()
    {
        using var test = new TestDatabase();
        var product = TestData.AddProduct(test);
        var supplier = TestData.AddSupplier(test);

        // No supplier yet - choosing him is part of finishing, not of saving.
        var saved = TestData.Drafts(test).SavePurchaseDraft(TestData.Owner, Delivery(product, 0));
        Assert.True(saved.Ok, saved.Error);

        Assert.Equal(0, Count(test, "purchases"));
        Assert.Equal(0, Count(test, "purchase_items"));
        Assert.Equal(0, Count(test, "stock_movements"));
        Assert.Equal(1, Count(test, "document_drafts"));
        Assert.Equal("draft", DraftStatus(test, saved.Value!.Id));
    }

    [Fact]
    public void APurchaseDraft_IsFinishedThenPostedThroughTheSameGuards()
    {
        using var test = new TestDatabase();
        var product = TestData.AddProduct(test);
        var supplier = TestData.AddSupplier(test);
        var manager = TestData.AddUser(test, "manager");
        var drafts = TestData.Drafts(test);

        var saved = drafts.SavePurchaseDraft(TestData.Owner, Delivery(product, 0));
        Assert.True(saved.Ok, saved.Error);

        // Posting without a supplier fails the purchase page's own guard - and keeps the draft.
        var early = drafts.PostPurchase(manager, saved.Value!.Id);
        Assert.False(early.Ok);
        Assert.Equal(0, Count(test, "purchases"));
        Assert.Equal("draft", DraftStatus(test, saved.Value.Id));

        // The manager finishes it (supplier chosen) and posts through the same guarded Create.
        var finished = drafts.SavePurchaseDraft(manager, Delivery(product, supplier), saved.Value.Id);
        Assert.True(finished.Ok, finished.Error);
        var posted = drafts.PostPurchase(manager, saved.Value.Id);
        Assert.True(posted.Ok, posted.Error);
        Assert.StartsWith("PINV-", posted.Value!.Code);

        Assert.Equal(1, Count(test, "purchases"));
        Assert.Equal("posted", DraftStatus(test, saved.Value.Id));

        // The stock and the payable moved exactly like a page-saved purchase.
        using var connection = test.Database.Open();
        var batchQty = Convert.ToDouble(test.Database.Scalar(connection,
            "SELECT qty_base FROM batches WHERE product_id = @id AND batch_no = 'B-7';",
            new { id = product }) ?? 0.0);
        Assert.Equal(2, batchQty);
        var balance = Convert.ToInt64(test.Database.Scalar(connection,
            "SELECT balance_paisa FROM suppliers WHERE id = @id;", new { id = supplier }) ?? 0L);
        Assert.True(balance > 0);
    }

    [Fact]
    public void PermissionGates_ArePerPage()
    {
        using var test = new TestDatabase();
        var product = TestData.AddProduct(test);
        var supplier = TestData.AddSupplier(test);
        var cashier = TestData.Cashier(test);
        var drafts = TestData.Drafts(test);

        var sale = drafts.SaveSaleDraft(TestData.Owner, TestData.Cart(product));
        Assert.True(sale.Ok, sale.Error);
        var purchase = drafts.SavePurchaseDraft(TestData.Owner, Delivery(product, supplier));
        Assert.True(purchase.Ok, purchase.Error);

        // The cashier holds the counter permission: sale drafts are shared with him.
        Assert.True(drafts.List(cashier, DraftType.Sale).Ok);
        Assert.True(drafts.OpenSale(cashier, sale.Value!.Id).Ok);

        // But not the purchase page: those drafts stay invisible and untouchable.
        Assert.False(drafts.List(cashier, DraftType.Purchase).Ok);
        Assert.False(drafts.OpenPurchase(cashier, purchase.Value!.Id).Ok);
        Assert.False(drafts.SavePurchaseDraft(cashier, Delivery(product, supplier)).Ok);
        Assert.False(drafts.PostPurchase(cashier, purchase.Value.Id).Ok);
        Assert.False(drafts.Discard(cashier, purchase.Value.Id).Ok);
    }

    [Fact]
    public void DiscardedDrafts_LeaveTheListButStayInHistory()
    {
        using var test = new TestDatabase();
        var product = TestData.AddProduct(test);
        var drafts = TestData.Drafts(test);

        var saved = drafts.SaveSaleDraft(TestData.Owner, TestData.Cart(product));
        Assert.True(saved.Ok, saved.Error);

        var discarded = drafts.Discard(TestData.Owner, saved.Value!.Id, "customer never came back");
        Assert.True(discarded.Ok, discarded.Error);

        var list = drafts.List(TestData.Owner, DraftType.Sale);
        Assert.True(list.Ok, list.Error);
        Assert.Empty(list.Value!);

        // The row stays: discarded, with the reason, and the black box saw it.
        Assert.Equal("discarded", DraftStatus(test, saved.Value.Id));
        using var connection = test.Database.Open();
        var reason = test.Database.Scalar(connection,
            "SELECT discard_reason FROM document_drafts WHERE id = @id;", new { id = saved.Value.Id })?.ToString();
        Assert.Equal("customer never came back", reason);
        var actions = test.Database.Query<string>(connection,
            "SELECT action FROM audit_logs WHERE entity = 'document_drafts' AND entity_id = @id ORDER BY id;",
            new { id = saved.Value.Id.ToString() });
        Assert.Contains("draft_saved", actions);
        Assert.Contains("draft_discarded", actions);
    }

    [Fact]
    public void AnInterruptedPost_CanNeverPostAgain()
    {
        using var test = new TestDatabase();
        var product = TestData.AddProduct(test);
        var drafts = TestData.Drafts(test);

        var saved = drafts.SaveSaleDraft(TestData.Owner, TestData.Cart(product));
        Assert.True(saved.Ok, saved.Error);

        // A crash between the document commit and the final mark leaves the marker behind.
        test.Database.Tx(connection =>
        {
            test.Database.Execute(connection,
                "UPDATE document_drafts SET status = 'posted', posted_document_id = -1, posted_at = @at WHERE id = @id;",
                new { id = saved.Value!.Id, at = "2026-01-01 10:00:00" });
        });

        var id = saved.Value!.Id;
        Assert.False(drafts.OpenSale(TestData.Owner, id).Ok);
        Assert.False(drafts.PostSale(TestData.Owner, id).Ok);

        var list = drafts.List(TestData.Owner, DraftType.Sale);
        Assert.True(list.Ok, list.Error);
        Assert.Single(list.Value!);
        Assert.True(list.Value![0].NeedsPostCheck);

        // Once the document is verified on its page, the row can be cleared - audited.
        Assert.True(drafts.Discard(TestData.Owner, id, "bill verified on the Bills page").Ok);
        Assert.Equal("discarded", DraftStatus(test, id));
        Assert.Equal(0, Count(test, "sales"));
    }

    [Fact]
    public void Drafts_StayLocalAndNeverSync()
    {
        Assert.DoesNotContain("document_drafts", SyncTables.Order);
        Assert.DoesNotContain("document_drafts", SyncTables.Recent);
    }
}
