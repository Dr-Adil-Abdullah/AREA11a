using Area11.Core.Models;
using Area11.Core.Services;
using Xunit;

namespace Area11.Tests;

/// <summary>Phase 2: stable field identity, central definition rules and safe value normalization.</summary>
public sealed class FieldGovernanceTests
{
    [Fact]
    public void AddAssignsAStableKeyAndStoresGuidance()
    {
        using var test = new TestDatabase();
        var fields = Fields(test);

        var added = fields.Add(TestData.Owner, CustomFieldEntity.Product, "Shelf", CustomFieldType.Text,
            null, false, "Use the rack printed on the shelf label.");

        Assert.True(added.Ok, added.Error);
        Assert.Equal("product.custom." + added.Value!.Id, added.Value.Key);
        Assert.Equal("Use the rack printed on the shelf label.", added.Value.HelpText);

        var loaded = Assert.Single(fields.List(TestData.Owner, CustomFieldEntity.Product).Value!);
        Assert.Equal(added.Value.Key, loaded.Key);
        Assert.Equal(added.Value.HelpText, loaded.HelpText);
    }

    [Fact]
    public void RenameKeepsTheKeyAndAnswer_AndAnOmittedHelpEditPreservesGuidance()
    {
        using var test = new TestDatabase();
        var fields = Fields(test);
        var product = TestData.AddProduct(test);
        var added = fields.Add(TestData.Owner, CustomFieldEntity.Product, "Shelf", CustomFieldType.Text,
            null, false, "Original guidance");
        var id = added.Value!.Id;
        var key = added.Value.Key;
        Assert.True(fields.SaveValues(TestData.Owner, CustomFieldEntity.Product, product,
            new Dictionary<int, string?> { [id] = "A-12" }).Ok);

        var updated = fields.Update(TestData.Owner, id, "Rack / shelf", CustomFieldType.Text,
            null, false); // no help argument = leave the existing guidance alone

        Assert.True(updated.Ok, updated.Error);
        var loaded = Assert.Single(fields.List(TestData.Owner, CustomFieldEntity.Product).Value!);
        Assert.Equal(key, loaded.Key);
        Assert.Equal("Rack / shelf", loaded.Label);
        Assert.Equal("Original guidance", loaded.HelpText);
        Assert.Equal("A-12", fields.Values(TestData.Owner, CustomFieldEntity.Product, product).Value![id]);
    }

    [Fact]
    public void ChangingGuidanceIsAuditedWithoutChangingTheAnswer()
    {
        using var test = new TestDatabase();
        var fields = Fields(test);
        var product = TestData.AddProduct(test);
        var added = fields.Add(TestData.Owner, CustomFieldEntity.Product, "Shelf", CustomFieldType.Text,
            null, false, "Old help");
        var id = added.Value!.Id;
        Assert.True(fields.SaveValues(TestData.Owner, CustomFieldEntity.Product, product,
            new Dictionary<int, string?> { [id] = "B-4" }).Ok);

        var updated = fields.Update(TestData.Owner, id, "Shelf", CustomFieldType.Text,
            null, false, "New help");

        Assert.True(updated.Ok, updated.Error);
        Assert.Equal("B-4", fields.Values(TestData.Owner, CustomFieldEntity.Product, product).Value![id]);
        Assert.Equal(1L, Rows(test,
            "SELECT COUNT(1) FROM audit_logs WHERE action = 'custom_field_update' AND new_value LIKE '%New help%';"));
    }

    [Fact]
    public void InvalidDefinitionsAreRefusedBeforeAFieldRowIsWritten()
    {
        using var test = new TestDatabase();
        var fields = Fields(test);

        Assert.False(fields.Add(TestData.Owner, CustomFieldEntity.Product, "Bad\nname",
            CustomFieldType.Text, null, false).Ok);
        Assert.False(fields.Add(TestData.Owner, CustomFieldEntity.Product, "Help",
            CustomFieldType.Text, null, false, new string('x', FieldGovernance.MaxHelpTextLength + 1)).Ok);
        Assert.False(fields.Add(TestData.Owner, CustomFieldEntity.Product, "Choices",
            CustomFieldType.Select, "only one", false).Ok);

        Assert.Equal(0L, Rows(test, "SELECT COUNT(1) FROM custom_fields;"));
    }

    [Fact]
    public void CentralNormalizationRejectsNonFiniteNumbers()
    {
        var field = new CustomField { Label = "Dose", Type = CustomFieldType.Number };

        Assert.False(FieldGovernance.Normalize(field, "NaN", 0, out _, out _));
        Assert.False(FieldGovernance.Normalize(field, "Infinity", 0, out _, out _));
        Assert.True(FieldGovernance.Normalize(field, "2.5", 0, out var stored, out var error));
        Assert.Null(error);
        Assert.Equal("2.5", stored);
    }

    [Fact]
    public void TheExistingServiceFacadeAndTheCentralPolicyAgree()
    {
        var field = new CustomField { Label = "Charge", Type = CustomFieldType.Amount };

        var central = FieldGovernance.Normalize(field, "1,200.50", 0, out var expected, out var centralError);
        var facade = CustomFieldService.Normalize(field, "1,200.50", 0, out var actual, out var facadeError);

        Assert.True(central);
        Assert.True(facade);
        Assert.Equal(centralError, facadeError);
        Assert.Equal(expected, actual);
        Assert.Equal("120050", actual);
    }

    [Fact]
    public void AllSevenTypesUseTheSameDefinitionPolicy()
    {
        foreach (var type in CustomFieldType.All)
        {
            var options = type == CustomFieldType.Select ? "A, B" : null;
            Assert.Null(FieldGovernance.DefinitionError(
                CustomFieldEntity.Product, "Field " + type, type, options, "One line of help"));
        }

        Assert.NotNull(FieldGovernance.DefinitionError(
            CustomFieldEntity.Product, "Bad list", CustomFieldType.Select, "A", null));
    }

    [Fact]
    public void AManuallyInsertedLegacyRowGetsAFallbackKeyAndIsRepairedOnEdit()
    {
        using var test = new TestDatabase();
        var fields = Fields(test);
        var id = test.Database.Tx(connection =>
        {
            test.Database.Execute(connection, @"
INSERT INTO custom_fields(entity, field_key, label, type, options_json, help_text, required, active, sort, created_at)
VALUES('product', NULL, 'Legacy', 'text', NULL, '', 0, 1, 1, '2026-01-01');");
            return (int)test.Database.NextId(connection);
        });

        var legacy = Assert.Single(fields.List(TestData.Owner, CustomFieldEntity.Product).Value!);
        Assert.Equal("product.custom." + id, legacy.Key);

        var updated = fields.Update(TestData.Owner, id, "Legacy renamed", CustomFieldType.Text, null, false);
        Assert.True(updated.Ok, updated.Error);
        using var connection = test.Database.Open();
        Assert.Equal("product.custom." + id,
            test.Database.QuerySingle<string>(connection, "SELECT field_key FROM custom_fields WHERE id = @id;", new { id }));
    }

    private static CustomFieldService Fields(TestDatabase test) =>
        new(test.Database, test.Audit, TestData.Permissions(test), test.Log);

    private static long Rows(TestDatabase test, string sql)
    {
        using var connection = test.Database.Open();
        return Convert.ToInt64(test.Database.Scalar(connection, sql) ?? 0);
    }
}
