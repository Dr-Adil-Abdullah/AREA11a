using Area11.Core.Models;
using Area11.Core.Services;
using Xunit;

namespace Area11.Tests;

/// <summary>
/// The owner's own fields (F-42, Spec 2.2, 8.4, E-05, S-04).
///
/// The promises this file pins down:
///   1. a field the owner adds **appears on the form by itself** - and only on that form;
///   2. **nothing is deleted** (D-00b): switching a field off hides the question, keeps every answer,
///      and switching it on again brings the same answers back;
///   3. **money stays money** (D-00a): an amount answer is stored in paisa, never as text;
///   4. a required field left empty stops the **whole** save - a record is never half answered;
///   5. the rule is one rule: the screen and the tests call the same <c>Normalize</c>.
/// </summary>
public sealed class CustomFieldTests
{
    // ============================== the form ==============================

    [Fact]
    public void AFieldAppearsOnItsOwnFormOnly()
    {
        using var test = new TestDatabase();
        var fields = Fields(test);

        var added = fields.Add(TestData.Owner, CustomFieldEntity.Product, "CNIC", CustomFieldType.Text,
            optionsText: null, required: false);

        Assert.True(added.Ok, added.Error);

        var products = fields.List(TestData.Owner, CustomFieldEntity.Product);
        Assert.True(products.Ok, products.Error);
        Assert.Single(products.Value!);
        Assert.Equal("CNIC", products.Value![0].Label);
        Assert.Equal("Text", products.Value[0].TypeLabel);

        Assert.Empty(fields.List(TestData.Owner, CustomFieldEntity.Customer).Value!);
        Assert.Equal(1L, Rows(test, "SELECT COUNT(1) FROM audit_logs WHERE action = 'custom_field_add';"));
    }

    [Fact]
    public void ANameMustBeRealAndUnique()
    {
        using var test = new TestDatabase();
        var fields = Fields(test);

        Assert.False(fields.Add(TestData.Owner, CustomFieldEntity.Product, "C", CustomFieldType.Text, null, false).Ok);

        Assert.True(fields.Add(TestData.Owner, CustomFieldEntity.Product, "Shelf", CustomFieldType.Text, null, false).Ok);

        var twice = fields.Add(TestData.Owner, CustomFieldEntity.Product, "shelf", CustomFieldType.Text, null, false);
        Assert.False(twice.Ok);
        Assert.Contains("already", twice.Error);

        // The same name on another form is a different question and is allowed.
        Assert.True(fields.Add(TestData.Owner, CustomFieldEntity.Customer, "Shelf", CustomFieldType.Text, null, false).Ok);
    }

    [Fact]
    public void AListNeedsTwoChoicesAndRefusesAnythingElse()
    {
        using var test = new TestDatabase();
        var fields = Fields(test);
        var product = TestData.AddProduct(test);

        var one = fields.Add(TestData.Owner, CustomFieldEntity.Product, "Group", CustomFieldType.Select, "A", false);
        Assert.False(one.Ok);
        Assert.Contains("two choices", one.Error);

        var added = fields.Add(TestData.Owner, CustomFieldEntity.Product, "Group", CustomFieldType.Select,
            "None, A , B, a", required: false);

        Assert.True(added.Ok, added.Error);
        Assert.Equal(new[] { "None", "A", "B" }, added.Value!.Options.ToArray());

        var wrong = fields.SaveValues(TestData.Owner, CustomFieldEntity.Product, product,
            new Dictionary<int, string?> { [added.Value.Id] = "Z" });

        Assert.False(wrong.Ok);
        Assert.Contains("must be one of", wrong.Error);

        Assert.True(fields.SaveValues(TestData.Owner, CustomFieldEntity.Product, product,
            new Dictionary<int, string?> { [added.Value.Id] = "b" }).Ok);

        var values = fields.Values(TestData.Owner, CustomFieldEntity.Product, product);
        Assert.Equal("B", values.Value![added.Value.Id]);
    }

    // ============================== the answers ==============================

    [Fact]
    public void AnAmountIsStoredInPaisaAndComesBackInRupees()
    {
        using var test = new TestDatabase();
        var fields = Fields(test);
        var supplier = TestData.AddSupplier(test);

        var added = fields.Add(TestData.Owner, CustomFieldEntity.Supplier, "Delivery charge",
            CustomFieldType.Amount, null, false);

        Assert.True(added.Ok, added.Error);

        var saved = fields.SaveValues(TestData.Owner, CustomFieldEntity.Supplier, supplier,
            new Dictionary<int, string?> { [added.Value!.Id] = "1,200.50" });

        Assert.True(saved.Ok, saved.Error);

        var values = fields.Values(TestData.Owner, CustomFieldEntity.Supplier, supplier);
        Assert.Equal("120050", values.Value![added.Value.Id]);
        Assert.Equal("1200.5", CustomFieldType.PrettyAmount(values.Value[added.Value.Id]));
    }

    [Fact]
    public void ANumberAndADateAreCheckedBeforeTheyAreSaved()
    {
        using var test = new TestDatabase();
        var fields = Fields(test);
        var customer = TestData.AddCustomer(test);

        var number = fields.Add(TestData.Owner, CustomFieldEntity.Customer, "Age", CustomFieldType.Number, null, false);
        var date = fields.Add(TestData.Owner, CustomFieldEntity.Customer, "Date of birth", CustomFieldType.Date, null, false);

        Assert.True(number.Ok && date.Ok);

        var badNumber = fields.SaveValues(TestData.Owner, CustomFieldEntity.Customer, customer,
            new Dictionary<int, string?> { [number.Value!.Id] = "twelve" });

        Assert.False(badNumber.Ok);
        Assert.Contains("must be a number", badNumber.Error);

        var badDate = fields.SaveValues(TestData.Owner, CustomFieldEntity.Customer, customer,
            new Dictionary<int, string?> { [date.Value!.Id] = "31/02/2026" });

        Assert.False(badDate.Ok);
        Assert.Contains("must be a date", badDate.Error);

        var good = fields.SaveValues(TestData.Owner, CustomFieldEntity.Customer, customer,
            new Dictionary<int, string?>
            {
                [number.Value.Id] = "42",
                [date.Value.Id] = "22/10/2026"
            });

        Assert.True(good.Ok, good.Error);

        var values = fields.Values(TestData.Owner, CustomFieldEntity.Customer, customer).Value!;
        Assert.Equal("42", values[number.Value.Id]);
        Assert.Equal("2026-10-22", values[date.Value.Id]);
    }

    [Fact]
    public void ARequiredFieldLeftEmptyStopsTheWholeSave()
    {
        using var test = new TestDatabase();
        var fields = Fields(test);
        var product = TestData.AddProduct(test);

        var required = fields.Add(TestData.Owner, CustomFieldEntity.Product, "CNIC", CustomFieldType.Text, null, true);
        var optional = fields.Add(TestData.Owner, CustomFieldEntity.Product, "Note", CustomFieldType.Text, null, false);

        var refused = fields.SaveValues(TestData.Owner, CustomFieldEntity.Product, product,
            new Dictionary<int, string?>
            {
                [required.Value!.Id] = "  ",
                [optional.Value!.Id] = "on the shelf"
            });

        Assert.False(refused.Ok);
        Assert.Contains("CNIC", refused.Error);

        // Nothing at all was written - not even the field that was filled.
        Assert.Equal(0L, Rows(test, "SELECT COUNT(1) FROM custom_values;"));
    }

    [Fact]
    public void TwoRecordsKeepTheirOwnAnswers()
    {
        using var test = new TestDatabase();
        var fields = Fields(test);

        var first = TestData.AddProduct(test, name: "Panadol 500mg");
        var second = TestData.AddProduct(test, name: "Disprin 300mg");

        var field = fields.Add(TestData.Owner, CustomFieldEntity.Product, "Shelf", CustomFieldType.Text, null, false);
        var fieldId = field.Value!.Id;

        Assert.True(fields.SaveValues(TestData.Owner, CustomFieldEntity.Product, first,
            new Dictionary<int, string?> { [fieldId] = "A-1" }).Ok);
        Assert.True(fields.SaveValues(TestData.Owner, CustomFieldEntity.Product, second,
            new Dictionary<int, string?> { [fieldId] = "B-7" }).Ok);

        Assert.Equal("A-1", fields.Values(TestData.Owner, CustomFieldEntity.Product, first).Value![fieldId]);
        Assert.Equal("B-7", fields.Values(TestData.Owner, CustomFieldEntity.Product, second).Value![fieldId]);
    }

    [Fact]
    public void APhotoFieldUsesTheGalleryAndStoresNoText()
    {
        using var test = new TestDatabase();
        var fields = Fields(test);
        var product = TestData.AddProduct(test);

        var added = fields.Add(TestData.Owner, CustomFieldEntity.Product, "Prescription photo",
            CustomFieldType.Photo, null, required: true);

        Assert.True(added.Ok, added.Error);

        var noPhoto = fields.SaveValues(TestData.Owner, CustomFieldEntity.Product, product,
            new Dictionary<int, string?> { [added.Value!.Id] = "whatever.jpg" });

        Assert.False(noPhoto.Ok);
        Assert.Contains("at least one photo", noPhoto.Error);

        test.Database.Tx(connection => test.Database.Execute(connection, @"
INSERT INTO photos(entity, entity_id, file_name, sort_order, active, created_at)
VALUES('product', @id, 'product-1-20261022-090000-000.jpg', 1, 1, '2026-10-22 09:00:00');",
            new { id = product }));

        Assert.True(fields.SaveValues(TestData.Owner, CustomFieldEntity.Product, product,
            new Dictionary<int, string?> { [added.Value.Id] = null }).Ok);

        // The gallery is the answer: a photo field never writes a text value of its own.
        Assert.Equal(0L, Rows(test, "SELECT COUNT(1) FROM custom_values;"));
    }

    // ============================== nothing is deleted ==============================

    [Fact]
    public void SwitchingAFieldOffKeepsEveryAnswer()
    {
        using var test = new TestDatabase();
        var fields = Fields(test);
        var customer = TestData.AddCustomer(test);

        var field = fields.Add(TestData.Owner, CustomFieldEntity.Customer, "CNIC", CustomFieldType.Text, null, true);
        var fieldId = field.Value!.Id;

        Assert.True(fields.SaveValues(TestData.Owner, CustomFieldEntity.Customer, customer,
            new Dictionary<int, string?> { [fieldId] = "35202-1234567-1" }).Ok);

        Assert.True(fields.SetActive(TestData.Owner, fieldId, false).Ok);

        // The form stops asking, the manager page still shows it, the answer is still there.
        Assert.Empty(fields.List(TestData.Owner, CustomFieldEntity.Customer).Value!);
        Assert.Single(fields.List(TestData.Owner, CustomFieldEntity.Customer, includeInactive: true).Value!);
        Assert.Equal("35202-1234567-1",
            fields.Values(TestData.Owner, CustomFieldEntity.Customer, customer).Value![fieldId]);

        Assert.True(fields.SetActive(TestData.Owner, fieldId, true).Ok);

        Assert.Single(fields.List(TestData.Owner, CustomFieldEntity.Customer).Value!);
        Assert.Equal(1L, Rows(test, "SELECT COUNT(1) FROM custom_values;"));
        Assert.Equal(1L, Rows(test, "SELECT COUNT(1) FROM audit_logs WHERE action = 'custom_field_off';"));
    }

    [Fact]
    public void TheOwnerCanPutTheQuestionsInHisOwnOrder()
    {
        using var test = new TestDatabase();
        var fields = Fields(test);

        foreach (var label in new[] { "First", "Second", "Third" })
        {
            Assert.True(fields.Add(TestData.Owner, CustomFieldEntity.Product, label,
                CustomFieldType.Text, null, false).Ok);
        }

        var third = fields.List(TestData.Owner, CustomFieldEntity.Product).Value![2];

        Assert.True(fields.Move(TestData.Owner, third.Id, -1).Ok);

        var order = fields.List(TestData.Owner, CustomFieldEntity.Product).Value!
            .Select(field => field.Label).ToArray();

        Assert.Equal(new[] { "First", "Third", "Second" }, order);

        // Again, then it is first - and one more time is refused, because nothing is above it.
        Assert.True(fields.Move(TestData.Owner, third.Id, -1).Ok);
        Assert.Equal(new[] { "Third", "First", "Second" },
            fields.List(TestData.Owner, CustomFieldEntity.Product).Value!
                .Select(field => field.Label).ToArray());

        Assert.False(fields.Move(TestData.Owner, third.Id, -1).Ok);

        Assert.True(fields.Move(TestData.Owner, third.Id, 1).Ok);
        Assert.Equal(new[] { "First", "Third", "Second" },
            fields.List(TestData.Owner, CustomFieldEntity.Product).Value!
                .Select(field => field.Label).ToArray());
    }

    // ============================== who may do what ==============================

    [Fact]
    public void ACashierMayAnswerACustomerQuestionButNotTheStocks()
    {
        using var test = new TestDatabase();
        var fields = Fields(test);
        var cashier = TestData.Cashier(test);
        var customer = TestData.AddCustomer(test);
        var product = TestData.AddProduct(test);

        var customerField = fields.Add(TestData.Owner, CustomFieldEntity.Customer, "CNIC",
            CustomFieldType.Text, null, false);
        var productField = fields.Add(TestData.Owner, CustomFieldEntity.Product, "Shelf",
            CustomFieldType.Text, null, false);

        Assert.True(fields.SaveValues(cashier, CustomFieldEntity.Customer, customer,
            new Dictionary<int, string?> { [customerField.Value!.Id] = "35202-7654321-9" }).Ok);

        Assert.False(fields.SaveValues(cashier, CustomFieldEntity.Product, product,
            new Dictionary<int, string?> { [productField.Value!.Id] = "A-1" }).Ok);

        Assert.False(fields.Add(cashier, CustomFieldEntity.Product, "Rack", CustomFieldType.Text, null, false).Ok);
        Assert.False(fields.List(cashier, CustomFieldEntity.Product, includeInactive: true, manage: true).Ok);
    }

    [Fact]
    public void AnUnknownFieldOrRecordIsRefused()
    {
        using var test = new TestDatabase();
        var fields = Fields(test);
        var product = TestData.AddProduct(test);

        var field = fields.Add(TestData.Owner, CustomFieldEntity.Product, "Shelf", CustomFieldType.Text, null, false);

        Assert.False(fields.SaveValues(TestData.Owner, CustomFieldEntity.Product, product,
            new Dictionary<int, string?> { [9999] = "A-1" }).Ok);

        Assert.False(fields.SaveValues(TestData.Owner, CustomFieldEntity.Product, 9999,
            new Dictionary<int, string?> { [field.Value!.Id] = "A-1" }).Ok);

        Assert.False(fields.Add(TestData.Owner, "spaceship", "Deck", CustomFieldType.Text, null, false).Ok);
        Assert.Equal(0L, Rows(test, "SELECT COUNT(1) FROM custom_values;"));
    }

    // ============================== helpers ==============================

    private static CustomFieldService Fields(TestDatabase test) =>
        new(test.Database, test.Audit, TestData.Permissions(test), test.Log);

    private static long Rows(TestDatabase test, string sql)
    {
        using var connection = test.Database.Open();

        return Convert.ToInt64(test.Database.Scalar(connection, sql) ?? 0);
    }
}
