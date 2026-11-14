using Area11.Core.Models;
using Area11.Core.Services;
using Xunit;

namespace Area11.Tests;

/// <summary>Metadata-only safety checks; these do not prove any WPF form obeys the policy.</summary>
public sealed class NativeFieldPolicyTests
{
    [Fact]
    public void CriticalOverrideRequiresOwnerConfirmationAndIsAuditedAtomically()
    {
        using var test = new TestDatabase();
        var id = AddField(test, critical: true);
        var service = new NativeFieldPolicyService(test.Database, test.Audit, test.Log);
        var refused = service.SetRequired(TestData.Owner, id, false, false);
        Assert.False(refused.Ok);
        using (var connection = test.Database.Open())
        {
            Assert.Equal(1L, Convert.ToInt64(test.Database.Scalar(connection,
                "SELECT required FROM form_field_definitions WHERE id = @id;", new { id })));
            Assert.Equal(0L, Convert.ToInt64(test.Database.Scalar(connection,
                "SELECT COUNT(*) FROM audit_logs WHERE action = 'native_field_required_changed';")));
        }

        Assert.True(service.SetRequired(TestData.Owner, id, false, true).Ok);
        using (var confirmedConnection = test.Database.Open())
        {
            Assert.Equal(TestData.SeedOwnerName, Convert.ToString(test.Database.Scalar(confirmedConnection,
                "SELECT override_acknowledged_by FROM form_field_definitions WHERE id = @id;", new { id })));
            Assert.NotNull(test.Database.Scalar(confirmedConnection,
                "SELECT override_acknowledged_at FROM form_field_definitions WHERE id = @id;", new { id }));
            Assert.Equal(1L, Convert.ToInt64(test.Database.Scalar(confirmedConnection,
                "SELECT COUNT(*) FROM audit_logs WHERE action = 'native_field_required_changed';")));
        }
        Assert.True(service.SetRequired(TestData.Owner, id, true, false).Ok);
        using var after = test.Database.Open();
        Assert.Null(test.Database.Scalar(after,
            "SELECT override_acknowledged_by FROM form_field_definitions WHERE id = @id;", new { id }));
        Assert.Equal(2L, Convert.ToInt64(test.Database.Scalar(after,
            "SELECT COUNT(*) FROM audit_logs WHERE action = 'native_field_required_changed';")));
    }

    [Fact]
    public void AuditFailureRollsBackCriticalOverride()
    {
        using var test = new TestDatabase();
        var id = AddField(test, critical: true);
        test.Database.Tx(auditConnection => test.Database.Execute(auditConnection, @"
CREATE TRIGGER deny_field_audit BEFORE INSERT ON audit_logs
WHEN NEW.action = 'native_field_required_changed'
BEGIN SELECT RAISE(ABORT, 'audit unavailable'); END;"));
        var service = new NativeFieldPolicyService(test.Database, test.Audit, test.Log);
        Assert.False(service.SetRequired(TestData.Owner, id, false, true).Ok);
        using var connection = test.Database.Open();
        Assert.Equal(1L, Convert.ToInt64(test.Database.Scalar(connection,
            "SELECT required FROM form_field_definitions WHERE id = @id;", new { id })));
        Assert.Null(test.Database.Scalar(connection,
            "SELECT override_acknowledged_by FROM form_field_definitions WHERE id = @id;", new { id }));
        Assert.Equal(0L, Convert.ToInt64(test.Database.Scalar(connection,
            "SELECT COUNT(*) FROM audit_logs WHERE action = 'native_field_required_changed';")));
    }

    [Fact]
    public void DefaultCatalogIsIdempotentAndDoesNotOverwriteConfirmedOwnerChoice()
    {
        using var test = new TestDatabase();
        var service = new NativeFieldPolicyService(test.Database, test.Audit, test.Log);
        Assert.True(service.EnsureDefaults().Ok);
        var fields = service.List(TestData.Owner).Value!;
        Assert.Equal(13, fields.Count);
        var quantity = fields.Single(field => field.FieldKey == "sale.quantity");
        Assert.True(service.SetRequired(TestData.Owner, quantity.Id, false, true).Ok);
        Assert.True(service.EnsureDefaults().Ok);
        var after = service.List(TestData.Owner).Value!;
        Assert.Equal(13, after.Count);
        var kept = after.Single(field => field.Id == quantity.Id);
        Assert.False(kept.Required);
        Assert.Equal(TestData.SeedOwnerName, kept.OverrideAcknowledgedBy);
    }

    [Fact]
    public void CustomerPhonePolicyGovernsCreateAndClearWithoutBreakingLegacyUnrelatedEdits()
    {
        using var test = new TestDatabase();
        var service = new NativeFieldPolicyService(test.Database, test.Audit, test.Log);
        Assert.True(service.EnsureDefaults().Ok);
        var phone = service.List(TestData.Owner).Value!.Single(field => field.FieldKey == "customer.phone");
        var partners = TestData.Partners(test);
        var legacy = partners.CreateCustomer(TestData.Owner, "Legacy person", null, "normal");
        var complete = partners.CreateCustomer(TestData.Owner, "Person with phone", "03001234567", "normal");
        Assert.True(legacy.Ok);
        Assert.True(complete.Ok);
        Assert.True(service.SetRequired(TestData.Owner, phone.Id, true, false).Ok);
        var refused = partners.CreateCustomer(TestData.Owner, "Missing phone", null, "normal");
        Assert.False(refused.Ok);
        Assert.Contains("phone", refused.Error!);
        Assert.False(partners.UpdateCustomer(TestData.Owner, new CustomerEditInput
        {
            Id = complete.Value, Name = "Person with phone", Phone = null
        }).Ok);
        Assert.True(partners.UpdateCustomer(TestData.Owner, new CustomerEditInput
        {
            Id = legacy.Value, Name = "Legacy renamed", Phone = null
        }).Ok);
        Assert.True(service.SetRequired(TestData.Owner, phone.Id, false, false).Ok);
        Assert.True(partners.CreateCustomer(TestData.Owner, "Optional phone again", null, "normal").Ok);
    }

    [Fact]
    public void SupplierPhonePolicyControlsNewSupplierCreation()
    {
        using var test = new TestDatabase();
        var service = new NativeFieldPolicyService(test.Database, test.Audit, test.Log);
        Assert.True(service.EnsureDefaults().Ok);
        var field = service.List(TestData.Owner).Value!.Single(row => row.FieldKey == "supplier.phone");
        var partners = TestData.Partners(test);
        Assert.True(partners.CreateSupplier(TestData.Owner, "Old supplier", null, null).Ok);
        Assert.True(service.SetRequired(TestData.Owner, field.Id, true, false).Ok);
        var missing = partners.CreateSupplier(TestData.Owner, "New supplier", null, null);
        Assert.False(missing.Ok);
        Assert.Contains("phone", missing.Error!);
        Assert.True(partners.CreateSupplier(TestData.Owner, "New supplier", "12345", null).Ok);
        Assert.True(service.SetRequired(TestData.Owner, field.Id, false, false).Ok);
        Assert.True(partners.CreateSupplier(TestData.Owner, "Optional supplier", null, null).Ok);
    }

    [Theory]
    [InlineData("product.generic")]
    [InlineData("product.brand")]
    [InlineData("product.rack")]
    [InlineData("product.pack")]
    public void ProductTextPolicyControlsCreationAndExplicitClearButPreservesLegacy(string key)
    {
        using var test = new TestDatabase();
        var policy = new NativeFieldPolicyService(test.Database, test.Audit, test.Log);
        Assert.True(policy.EnsureDefaults().Ok);
        var field = policy.List(TestData.Owner).Value!.Single(item => item.FieldKey == key);
        var products = new ProductService(test.Database, test.Audit, test.Log);
        var legacy = TestData.AddProduct(test);
        var complete = products.Create(TestData.Owner, new ProductPatch
        {
            Name = "Complete medicine", Generic = "salt", Brand = "brand", RackNo = "shelf", PackSizeLabel = "50 ml"
        });
        Assert.True(complete.Ok, complete.Error);
        Assert.True(policy.SetRequired(TestData.Owner, field.Id, true, false).Ok);
        var incomplete = products.Create(TestData.Owner, new ProductPatch { Name = "Missing field" });
        Assert.False(incomplete.Ok);
        var clear = key switch
        {
            "product.generic" => new ProductPatch { Generic = "" },
            "product.brand" => new ProductPatch { Brand = "" },
            "product.pack" => new ProductPatch { PackSizeLabel = "" },
            _ => new ProductPatch { RackNo = "" }
        };
        Assert.False(products.Update(TestData.Owner, complete.Value, clear).Ok);
        Assert.True(products.Update(TestData.Owner, legacy, new ProductPatch { Name = "Legacy rename" }).Ok);
        Assert.True(policy.SetRequired(TestData.Owner, field.Id, false, false).Ok);
        Assert.True(products.Create(TestData.Owner, new ProductPatch { Name = "Optional medicine" }).Ok);
    }

    [Fact]
    public void SupplierAgencyPolicyControlsNewSupplierCreation()
    {
        using var test = new TestDatabase();
        var policy = new NativeFieldPolicyService(test.Database, test.Audit, test.Log);
        Assert.True(policy.EnsureDefaults().Ok);
        var field = policy.List(TestData.Owner).Value!.Single(row => row.FieldKey == "supplier.agency");
        var partners = TestData.Partners(test);
        Assert.True(policy.SetRequired(TestData.Owner, field.Id, true, false).Ok);
        Assert.False(partners.CreateSupplier(TestData.Owner, "New supplier", "123", null).Ok);
        Assert.True(partners.CreateSupplier(TestData.Owner, "New supplier", "123", "Agency").Ok);
        Assert.True(policy.SetRequired(TestData.Owner, field.Id, false, false).Ok);
        Assert.True(partners.CreateSupplier(TestData.Owner, "Optional supplier", null, null).Ok);
    }

    [Fact]
    public void ManagerCannotReadOrChangeNativePoliciesEvenWithSettingsPermission()
    {
        using var test = new TestDatabase();
        var id = AddField(test, critical: false);
        var manager = TestData.AddUser(test, "manager");
        var service = new NativeFieldPolicyService(test.Database, test.Audit, test.Log);
        Assert.False(service.List(manager).Ok);
        Assert.False(service.SetRequired(manager, id, false, true).Ok);
        Assert.True(service.List(TestData.Owner).Ok);
        using var connection = test.Database.Open();
        Assert.Equal(1L, Convert.ToInt64(test.Database.Scalar(connection,
            "SELECT required FROM form_field_definitions WHERE id = @id;", new { id })));
        Assert.Equal(0L, Convert.ToInt64(test.Database.Scalar(connection,
            "SELECT COUNT(*) FROM audit_logs WHERE action = 'native_field_required_changed';")));
    }

    [Fact]
    public void SellingPriceShipsNecessaryAndIsNamedWhenZero()
    {
        using var test = new TestDatabase();
        var policy = new NativeFieldPolicyService(test.Database, test.Audit, test.Log);
        Assert.True(policy.EnsureDefaults().Ok);
        var products = new ProductService(test.Database, test.Audit, test.Log);

        // A price of zero is the Counter's blank-total case: the save must name it.
        var missing = products.MissingRequiredLabels(new ProductPatch { Name = "Disprin", RetailPaisa = 0 });
        Assert.Contains("Selling price", missing);
    }

    [Fact]
    public void SellingPriceWarningGoesQuietWhenTheOwnerMarksItOptional()
    {
        using var test = new TestDatabase();
        var policy = new NativeFieldPolicyService(test.Database, test.Audit, test.Log);
        Assert.True(policy.EnsureDefaults().Ok);
        var retail = policy.List(TestData.Owner).Value!.Single(field => field.FieldKey == "product.retail");
        Assert.True(retail.Required);
        Assert.True(policy.SetRequired(TestData.Owner, retail.Id, false, false).Ok);
        var products = new ProductService(test.Database, test.Audit, test.Log);

        var missing = products.MissingRequiredLabels(new ProductPatch { Name = "Disprin" });
        Assert.DoesNotContain("Selling price", missing);
    }

    [Fact]
    public void RequiredTextFieldsAreNamedAndALinkedSaltCountsAsPresent()
    {
        using var test = new TestDatabase();
        var policy = new NativeFieldPolicyService(test.Database, test.Audit, test.Log);
        Assert.True(policy.EnsureDefaults().Ok);
        var generic = policy.List(TestData.Owner).Value!.Single(field => field.FieldKey == "product.generic");
        Assert.True(policy.SetRequired(TestData.Owner, generic.Id, true, false).Ok);
        var products = new ProductService(test.Database, test.Audit, test.Log);

        Assert.Contains("Generic / salt",
            products.MissingRequiredLabels(new ProductPatch { Name = "Disprin", RetailPaisa = 100 }));

        // The typed text is only one way to fill the salt: a catalog link also fills it.
        Assert.DoesNotContain("Generic / salt",
            products.MissingRequiredLabels(new ProductPatch { Name = "Disprin", RetailPaisa = 100, GenericFormulaId = 7 }));
    }

    [Fact]
    public void ProductSaveIsNeverBlockedByMissingNecessaryFields()
    {
        using var test = new TestDatabase();
        var policy = new NativeFieldPolicyService(test.Database, test.Audit, test.Log);
        Assert.True(policy.EnsureDefaults().Ok);
        var products = new ProductService(test.Database, test.Audit, test.Log);

        // Warn-once is the owner's word: the popup warns, the service always saves.
        var created = products.Create(TestData.Owner, new ProductPatch { Name = "Priceless", RetailPaisa = 0 });
        Assert.True(created.Ok);
        Assert.True(created.Value > 0);
    }

    private static int AddField(TestDatabase test, bool critical) => test.Database.Tx(connection =>
    {
        test.Database.Execute(connection, @"
INSERT INTO form_field_definitions(field_key, section_key, label, data_type, input_method,
                                   safe_required, required, critical)
VALUES('sale.test', 'sale', 'Test field', 'text', 'text', 1, 1, @critical);",
            new { critical = critical ? 1 : 0 });
        return (int)test.Database.NextId(connection);
    });
}
