using Area11.Core.Models;
using Area11.Core.Services;
using Xunit;

namespace Area11.Tests;

/// <summary>
/// The owner's own questions on bills, purchases and expenses (master Portion 18). The promises:
///   1. a document field appears on its own form only - never on a master form or a sibling document;
///   2. photo questions are refused on documents (no gallery exists there), while master-form photos
///      stay the owner's choice (D-100);
///   3. answers are validated and stored in the SAME transaction as the document: a missing required
///      answer refuses the whole post, naming the field, and leaves no half-written rows;
///   4. gates follow the page (pos / purchases / cash), never the person;
///   5. drafts carry the answers to the post; the bill history and the expense list show them back.
/// </summary>
public sealed class DocumentFieldTests
{
    private static CustomFieldService Fields(TestDatabase test) =>
        new(test.Database, test.Audit, TestData.Permissions(test), test.Log);

    private static int AddField(
        TestDatabase test, string entity, string label, string type = "text", bool required = false)
    {
        var added = Fields(test).Add(TestData.Owner, entity, label, type, null, required);
        Assert.True(added.Ok, added.Error);
        return added.Value!.Id;
    }

    private static PurchaseInput Delivery(int productId, int supplierId) => new()
    {
        SupplierId = supplierId,
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

    private static ExpenseInput Fare() => new()
    {
        Category = "transport",
        Title = "Rickshaw fare",
        AmountPaisa = 50000
    };

    private static long Count(TestDatabase test, string table)
    {
        using var connection = test.Database.Open();
        return Convert.ToInt64(test.Database.Scalar(connection, $"SELECT COUNT(1) FROM {table};") ?? 0L);
    }

    [Fact]
    public void ADocumentField_AppearsOnItsOwnFormOnly()
    {
        using var test = new TestDatabase();
        var fields = Fields(test);

        AddField(test, CustomFieldEntity.Sale, "Vehicle no");

        Assert.Single(fields.List(TestData.Owner, CustomFieldEntity.Sale).Value!);
        Assert.Empty(fields.List(TestData.Owner, CustomFieldEntity.Purchase).Value!);
        Assert.Empty(fields.List(TestData.Owner, CustomFieldEntity.Expense).Value!);
        Assert.Empty(fields.List(TestData.Owner, CustomFieldEntity.Product).Value!);
        Assert.Empty(fields.List(TestData.Owner, CustomFieldEntity.Customer).Value!);
    }

    [Fact]
    public void APhotoField_IsRefusedOnEveryDocumentForm()
    {
        using var test = new TestDatabase();
        var fields = Fields(test);

        foreach (var entity in new[] { CustomFieldEntity.Sale, CustomFieldEntity.Purchase, CustomFieldEntity.Expense })
        {
            var refused = fields.Add(TestData.Owner, entity, "Snapshot", CustomFieldType.Photo, null, false);
            Assert.False(refused.Ok);
            Assert.Contains("gallery", refused.Error);
        }

        // ...while a photo on a master form stays the owner's own choice (D-100).
        var master = fields.Add(TestData.Owner, CustomFieldEntity.Product, "Pack photo", CustomFieldType.Photo, null, false);
        Assert.True(master.Ok, master.Error);
    }

    [Fact]
    public void ChangingADocumentField_ToPhoto_IsRefused()
    {
        using var test = new TestDatabase();
        var id = AddField(test, CustomFieldEntity.Expense, "Receipt note");

        var changed = Fields(test).Update(TestData.Owner, id, "Receipt note", CustomFieldType.Photo, null, false);
        Assert.False(changed.Ok);
        Assert.Contains("gallery", changed.Error);
    }

    [Fact]
    public void AMissingRequiredAnswer_RefusesTheBill_NamingTheField()
    {
        using var test = new TestDatabase();
        var product = TestData.AddProduct(test);
        AddField(test, CustomFieldEntity.Sale, "Vehicle no", required: true);

        var billed = TestData.Sales(test).Create(TestData.Cart(product), TestData.Owner);

        Assert.False(billed.Ok);
        Assert.Contains("Vehicle no", billed.Error);
        Assert.Equal(0, Count(test, "sales"));
        Assert.Equal(0, Count(test, "custom_values"));
    }

    [Fact]
    public void AMissingRequiredAnswer_RefusesThePurchase()
    {
        using var test = new TestDatabase();
        var product = TestData.AddProduct(test);
        var supplier = TestData.AddSupplier(test);
        AddField(test, CustomFieldEntity.Purchase, "Driver name", required: true);

        var bought = TestData.Purchases(test).Create(Delivery(product, supplier), TestData.Owner);

        Assert.False(bought.Ok);
        Assert.Contains("Driver name", bought.Error);
        Assert.Equal(0, Count(test, "purchases"));
        Assert.Equal(0, Count(test, "custom_values"));
    }

    [Fact]
    public void AMissingRequiredAnswer_RefusesTheExpense_WithAReadableMessage()
    {
        using var test = new TestDatabase();
        AddField(test, CustomFieldEntity.Expense, "Approved by", required: true);

        var saved = TestData.CashOut(test).AddExpense(TestData.Owner, Fare());

        Assert.False(saved.Ok);
        Assert.Contains("Approved by", saved.Error);
        Assert.Equal(0, Count(test, "expenses"));
        Assert.Equal(0, Count(test, "custom_values"));
    }

    [Fact]
    public void FullAnswers_AreSavedWithAllThreeDocuments()
    {
        using var test = new TestDatabase();
        var product = TestData.AddProduct(test);
        var supplier = TestData.AddSupplier(test);
        var fields = Fields(test);

        var vehicle = AddField(test, CustomFieldEntity.Sale, "Vehicle no", required: true);
        var driver = AddField(test, CustomFieldEntity.Purchase, "Driver name");
        var approved = AddField(test, CustomFieldEntity.Expense, "Approved by");

        var cart = TestData.Cart(product);
        cart.CustomAnswers[vehicle] = "RIK-123";
        var billed = TestData.Sales(test).Create(cart, TestData.Owner);
        Assert.True(billed.Ok, billed.Error);

        var delivery = Delivery(product, supplier);
        delivery.CustomAnswers[driver] = "Nadeem";
        var bought = TestData.Purchases(test).Create(delivery, TestData.Owner);
        Assert.True(bought.Ok, bought.Error);

        var fare = Fare();
        fare.CustomAnswers[approved] = "Owner";
        var spent = TestData.CashOut(test).AddExpense(TestData.Owner, fare);
        Assert.True(spent.Ok, spent.Error);

        var billAnswers = fields.Values(TestData.Owner, CustomFieldEntity.Sale, billed.Value!.SaleId);
        Assert.True(billAnswers.Ok, billAnswers.Error);
        Assert.Equal("RIK-123", billAnswers.Value![vehicle]);

        var purchaseAnswers = fields.Values(TestData.Owner, CustomFieldEntity.Purchase, bought.Value!.PurchaseId);
        Assert.True(purchaseAnswers.Ok, purchaseAnswers.Error);
        Assert.Equal("Nadeem", purchaseAnswers.Value![driver]);

        var expenseAnswers = fields.Values(TestData.Owner, CustomFieldEntity.Expense, spent.Value!.Id);
        Assert.True(expenseAnswers.Ok, expenseAnswers.Error);
        Assert.Equal("Owner", expenseAnswers.Value![approved]);
    }

    [Fact]
    public void OptionalFields_MayStayBlank()
    {
        using var test = new TestDatabase();
        var product = TestData.AddProduct(test);
        AddField(test, CustomFieldEntity.Sale, "Vehicle no");

        var billed = TestData.Sales(test).Create(TestData.Cart(product), TestData.Owner);

        Assert.True(billed.Ok, billed.Error);
        Assert.Equal(0, Count(test, "custom_values"));
    }

    [Fact]
    public void PermissionGates_FollowThePage()
    {
        using var test = new TestDatabase();
        var product = TestData.AddProduct(test);
        var cashier = TestData.Cashier(test);
        var fields = Fields(test);

        AddField(test, CustomFieldEntity.Sale, "Vehicle no");
        var billed = TestData.Sales(test).Create(TestData.Cart(product), TestData.Owner);
        Assert.True(billed.Ok, billed.Error);

        // The counter permission opens the bill's answers...
        Assert.True(fields.DescribeValues(cashier, CustomFieldEntity.Sale, billed.Value!.SaleId).Ok);

        // ...but the purchase and cash pages stay shut for the cashier.
        Assert.False(fields.DescribeValues(cashier, CustomFieldEntity.Purchase, 1).Ok);
        Assert.False(fields.DescribeValues(cashier, CustomFieldEntity.Expense, 1).Ok);
        Assert.False(fields.SaveValues(cashier, CustomFieldEntity.Expense, 1,
            new Dictionary<int, string?>()).Ok);
    }

    [Fact]
    public void ADraft_CarriesTheBillAnswersToThePost()
    {
        using var test = new TestDatabase();
        var product = TestData.AddProduct(test);
        var vehicle = AddField(test, CustomFieldEntity.Sale, "Vehicle no", required: true);
        var drafts = TestData.Drafts(test);

        // Parked without answers, the post fails the bill's own guard - and the draft survives.
        var parked = drafts.SaveSaleDraft(TestData.Owner, TestData.Cart(product));
        Assert.True(parked.Ok, parked.Error);
        Assert.False(drafts.PostSale(TestData.Owner, parked.Value!.Id).Ok);
        Assert.Equal(0, Count(test, "sales"));

        // Answered on the page, the same draft posts - answers stored with the bill.
        var cart = TestData.Cart(product);
        cart.CustomAnswers[vehicle] = "RIK-123";
        Assert.True(drafts.SaveSaleDraft(TestData.Owner, cart, parked.Value.Id).Ok);
        var posted = drafts.PostSale(TestData.Owner, parked.Value.Id);
        Assert.True(posted.Ok, posted.Error);

        var answers = Fields(test).Values(TestData.Owner, CustomFieldEntity.Sale, posted.Value!.SaleId);
        Assert.True(answers.Ok, answers.Error);
        Assert.Equal("RIK-123", answers.Value![vehicle]);
    }

    [Fact]
    public void DescribeValues_ShowsLabelsInOwnerOrder_IncludingSwitchedOffFields()
    {
        using var test = new TestDatabase();
        var product = TestData.AddProduct(test);
        var fields = Fields(test);

        var first = AddField(test, CustomFieldEntity.Sale, "Vehicle no");
        var second = AddField(test, CustomFieldEntity.Sale, "Delivery boy");

        var cart = TestData.Cart(product);
        cart.CustomAnswers[first] = "RIK-123";
        cart.CustomAnswers[second] = "Bilal";
        var billed = TestData.Sales(test).Create(cart, TestData.Owner);
        Assert.True(billed.Ok, billed.Error);

        // The question is put away afterwards: the answer outlives it and still shows labelled.
        Assert.True(fields.SetActive(TestData.Owner, first, false).Ok);

        var lines = fields.DescribeValues(TestData.Owner, CustomFieldEntity.Sale, billed.Value!.SaleId);
        Assert.True(lines.Ok, lines.Error);
        Assert.Equal(2, lines.Value!.Count);
        Assert.Equal("Vehicle no: RIK-123", lines.Value[0].Display);
        Assert.Equal("Delivery boy: Bilal", lines.Value[1].Display);
    }

    [Fact]
    public void TheExpenseList_CarriesTheAnswersLine()
    {
        using var test = new TestDatabase();
        var approved = AddField(test, CustomFieldEntity.Expense, "Approved by");
        var cashOut = TestData.CashOut(test);

        var fare = Fare();
        fare.CustomAnswers[approved] = "Owner";
        Assert.True(cashOut.AddExpense(TestData.Owner, fare).Ok);

        var rows = cashOut.List();
        Assert.Single(rows);
        Assert.Contains("Approved by: Owner", rows[0].ExtraLabel);
        Assert.True(rows[0].HasExtra);
    }

    [Fact]
    public void UnknownFieldIds_AreRejectedOnDocuments()
    {
        using var test = new TestDatabase();
        var product = TestData.AddProduct(test);
        AddField(test, CustomFieldEntity.Sale, "Vehicle no");

        // The question was put away (or never existed): the bill refuses instead of guessing.
        var cart = TestData.Cart(product);
        cart.CustomAnswers[9999] = "RIK-123";
        var billed = TestData.Sales(test).Create(cart, TestData.Owner);

        Assert.False(billed.Ok);
        Assert.Contains("not on this form", billed.Error);
        Assert.Equal(0, Count(test, "sales"));
    }

    [Fact]
    public void DocumentAnswers_StayLocalWithMasterAnswers()
    {
        Assert.DoesNotContain("custom_fields", SyncTables.Order);
        Assert.DoesNotContain("custom_values", SyncTables.Order);
    }
}
