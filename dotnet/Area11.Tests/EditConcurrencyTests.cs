using Area11.Core.Data;
using Area11.Core.Models;
using Area11.Core.Services;
using Area11.Core.Utils;
using Microsoft.Data.Sqlite;
using Xunit;

namespace Area11.Tests;

/// <summary>Written real-schema CAS/snapshot tests. Never use a live shop DB. C#/provider/Windows
/// execution is still required;these are not durable posting-identity tests.</summary>
public sealed class EditConcurrencyTests
{
    private static readonly User Owner = TestData.Owner;
    private static ProductService Products(TestDatabase t, Database? db = null)
    {
        db ??= t.Database; var audit = new AuditService(db, t.Log); var permissions = new PermissionService(db, t.Log, audit);
        var settings = new SettingsService(db, audit, t.Log, permissions); settings.Reload();
        return new ProductService(db, audit, t.Log, permissions: permissions, settings: settings);
    }
    private static CustomFieldService Fields(TestDatabase t) => new(t.Database, t.Audit, TestData.Permissions(t), t.Log);
    private static NativeFieldPolicyService Native(TestDatabase t) => new(t.Database, t.Audit, t.Log);
    private static T Value<T>(ServiceResult<T> result)
    { Assert.True(result.Ok, result.Error); return result.Value!; }
    private static long Audits(TestDatabase t) => t.Database.Read(c => Convert.ToInt64(t.Database.Scalar(c, "SELECT COUNT(*) FROM audit_logs;")));
    private static CustomerEditInput Edit(Customer c, string phone) => new()
    { Id = c.Id, Name = c.Name, Phone = phone, Category = c.Category ?? "normal", CreditLimitPaisa = c.CreditLimitPaisa };
    private static int AddField(TestDatabase t) => Value(Fields(t).Add(Owner, CustomFieldEntity.Product, "Synthetic note", CustomFieldType.Text, null, false)).Id;

    [Fact]
    public void ProductSecondOpenFormCannotOverwriteFirstSave()
    {
        using var t = new TestDatabase(); var id = TestData.AddProduct(t); var products = Products(t);
        var first = Value(products.LoadEditForm(Owner, id)); var second = Value(products.LoadEditForm(Owner, id));
        Assert.True(products.Update(Owner, id, new ProductPatch { RackNo = "first" }, expected: first.Version).Ok);
        var audit = Audits(t); var refused = products.Update(Owner, id, new ProductPatch { RackNo = "stale" }, expected: second.Version);
        Assert.False(refused.Ok); Assert.True(refused.NoCommitConfirmed); Assert.Contains("changed", refused.Error!);
        Assert.Equal(audit, Audits(t)); Assert.Equal("first", Value(products.Get(Owner, id)).RackNo);
    }

    [Fact]
    public void FreshProductReopenCanApplyAfterAConflict()
    {
        using var t = new TestDatabase(); var id = TestData.AddProduct(t); var p = Products(t);
        var old = Value(p.LoadEditForm(Owner, id)); Assert.True(p.Update(Owner, id, new ProductPatch { RackNo = "new" }, expected: old.Version).Ok);
        var fresh = Value(p.LoadEditForm(Owner, id));
        Assert.True(p.Update(Owner, id, new ProductPatch { RackNo = "reviewed" }, expected: fresh.Version).Ok);
        Assert.Equal("reviewed", Value(p.Get(Owner, id)).RackNo);
    }

    [Fact]
    public void TwoDatabaseHandlesCannotSilentlyOverwriteTheSameProduct()
    {
        using var t = new TestDatabase(); var id = TestData.AddProduct(t);
        var one = new Database(t.Database.FilePath, pooling: false); var two = new Database(t.Database.FilePath, pooling: false);
        var p1 = Products(t, one); var p2 = Products(t, two);
        var a = Value(p1.LoadEditForm(Owner, id)); var b = Value(p2.LoadEditForm(Owner, id));
        Assert.True(p2.Update(Owner, id, new ProductPatch { RackNo = "other connection" }, expected: b.Version).Ok);
        Assert.False(p1.Update(Owner, id, new ProductPatch { RackNo = "stale connection" }, expected: a.Version).Ok);
        Assert.Equal("other connection", Value(p1.Get(Owner, id)).RackNo);
    }

    [Fact]
    public void SameTimestampButDifferentPersistedValueIsAConflict()
    {
        using var t = new TestDatabase(); var id = TestData.AddProduct(t); var p = Products(t);
        var old = Value(p.LoadEditForm(Owner, id));
        t.Database.Tx(c => t.Database.Execute(c, "UPDATE products SET rack_no='changed without timestamp' WHERE id=@id;", new { id }));
        Assert.False(p.Update(Owner, id, new ProductPatch { RackNo = "old form" }, expected: old.Version).Ok);
    }

    [Fact]
    public void ProductTagChangeConflictsBeforeAnyParentWrite()
    {
        using var t = new TestDatabase(); var id = TestData.AddProduct(t); var p = Products(t); var old = Value(p.LoadEditForm(Owner, id));
        var category = Value(p.AddCategory(Owner, "Synthetic additional category"));
        t.Database.Tx(c => t.Database.Execute(c, "INSERT INTO product_category_tags(product_id,category_id) VALUES(@id,@category);", new { id, category }));
        var before = Audits(t);
        Assert.False(p.Update(Owner, id, new ProductPatch { Name = "must not save" }, expected: old.Version).Ok);
        Assert.Equal(before, Audits(t)); Assert.NotEqual("must not save", Value(p.Get(Owner, id)).Name);
    }

    [Fact]
    public void ParentAndExtraAnswersDoNotOverwriteAConcurrentAnswerSave()
    {
        using var t = new TestDatabase(); var id = TestData.AddProduct(t); var field = AddField(t); var p = Products(t);
        var old = Value(p.LoadEditForm(Owner, id)); var extra = Value(Fields(t).EditorSnapshot(Owner, CustomFieldEntity.Product, id));
        Assert.True(Fields(t).SaveValues(Owner, CustomFieldEntity.Product, id, new Dictionary<int, string?> { [field] = "new answer" }, extra.Version).Ok);
        var audit = Audits(t);
        Assert.False(p.Update(Owner, id, new ProductPatch { RackNo = "stale" }, new Dictionary<int, string?> { [field] = "old answer" }, old.Version).Ok);
        Assert.Equal(audit, Audits(t)); Assert.Equal("new answer", Value(Fields(t).Values(Owner, CustomFieldEntity.Product, id))[field]);
    }

    [Fact]
    public void DefinitionChangeInvalidatesAnExistingProductForm()
    {
        using var t = new TestDatabase(); var id = TestData.AddProduct(t); var p = Products(t); var old = Value(p.LoadEditForm(Owner, id));
        AddField(t);
        Assert.False(p.Update(Owner, id, new ProductPatch { RackNo = "old definition" }, expected: old.Version).Ok);
    }

    [Fact]
    public void DifferentRecordOrDifferentScopeCannotReuseAVersion()
    {
        using var t = new TestDatabase(); var a = TestData.AddProduct(t); var b = TestData.AddProduct(t, name: "Other item"); var p = Products(t);
        var old = Value(p.LoadEditForm(Owner, a));
        Assert.False(p.Update(Owner, b, new ProductPatch { RackNo = "wrong target" }, expected: old.Version).Ok);
        var category = Value(p.GetCategories()).First();
        Assert.False(p.Update(Owner, a, new ProductPatch { RackNo = "wrong scope" }, expected: category.EditVersion).Ok);
    }

    [Fact]
    public void VersionIsBoundToTheLoadingDatabaseInstance()
    {
        using var t = new TestDatabase(); var id = TestData.AddProduct(t); var version = Value(Products(t).LoadEditForm(Owner, id)).Version;
        var other = new Database(t.Database.FilePath, pooling: false, durable: false);
        Assert.False(Products(t, other).Update(Owner, id, new ProductPatch { RackNo = "wrong context" }, expected: version).Ok);
    }

    [Fact]
    public void EnabledDesktopStyleHostRejectsMissingBaseline()
    {
        using var t = new TestDatabase(); var id = TestData.AddProduct(t); t.Database.Sessions.Enable();
        var auth = new AuthService(t.Database, t.Audit, t.Settings, t.Log); var owner = Value(auth.Login("admin", "area11"));
        var p = Products(t); Assert.False(p.Update(owner, id, new ProductPatch { RackNo = "missing version" }).Ok);
        var form = Value(p.LoadEditForm(owner, id)); Assert.True(p.Update(owner, id, new ProductPatch { RackNo = "checked" }, expected: form.Version).Ok);
    }

    [Fact]
    public void CustomerEditDoesNotOverwriteAnotherPhoneCorrection()
    {
        using var t = new TestDatabase(); var id = TestData.AddCustomer(t); var p = TestData.Partners(t);
        var a = Value(p.LoadCustomerEditForm(Owner, id)); var b = Value(p.LoadCustomerEditForm(Owner, id));
        Assert.True(p.UpdateCustomer(Owner, Edit(a.Record, "03001110000"), expected: a.Version).Ok);
        Assert.False(p.UpdateCustomer(Owner, Edit(b.Record, "03002220000"), expected: b.Version).Ok);
        Assert.Equal("03001110000", Value(p.LoadCustomerEditForm(Owner, id)).Record.Phone);
    }

    [Fact]
    public void CustomerBalanceAndLoyaltyMovementDoNotDiscardAnUnrelatedPhoneEdit()
    {
        using var t = new TestDatabase(); var id = TestData.AddCustomer(t); var p = TestData.Partners(t); var form = Value(p.LoadCustomerEditForm(Owner, id));
        t.Database.Tx(c => t.Database.Execute(c, "UPDATE customers SET balance_paisa=12345,loyalty_points=7 WHERE id=@id;", new { id }));
        Assert.True(p.UpdateCustomer(Owner, Edit(form.Record, "03003330000"), expected: form.Version).Ok);
        Assert.Equal(12345L, t.Database.Read(c => Convert.ToInt64(t.Database.Scalar(c, "SELECT balance_paisa FROM customers WHERE id=@id;", new { id }))));
    }

    [Fact]
    public void CashierPhoneCorrectionStillWorksWithACheckedForm()
    {
        using var t = new TestDatabase(); var id = TestData.AddCustomer(t); var staff = TestData.Cashier(t); var p = TestData.Partners(t);
        var form = Value(p.LoadCustomerEditForm(staff, id)); var input = Edit(form.Record, "03004440000"); input.CreditLimitPaisa = null;
        Assert.True(p.UpdateCustomer(staff, input, expected: form.Version).Ok);
    }

    [Fact]
    public void StandaloneExtraEditorCannotReplaceNewerAnswers()
    {
        using var t = new TestDatabase(); var id = TestData.AddProduct(t); var field = AddField(t); var f = Fields(t);
        var a = Value(f.EditorSnapshot(Owner, CustomFieldEntity.Product, id)); var b = Value(f.EditorSnapshot(Owner, CustomFieldEntity.Product, id));
        Assert.True(f.SaveValues(Owner, CustomFieldEntity.Product, id, new Dictionary<int, string?> { [field] = "first" }, a.Version).Ok);
        Assert.False(f.SaveValues(Owner, CustomFieldEntity.Product, id, new Dictionary<int, string?> { [field] = "stale" }, b.Version).Ok);
        Assert.Equal("first", Value(f.Values(Owner, CustomFieldEntity.Product, id))[field]);
    }

    [Fact]
    public void CustomDefinitionEditRejectsOldLabelsAndOrder()
    {
        using var t = new TestDatabase(); var id = AddField(t); var f = Fields(t);
        var old = Value(f.List(Owner, CustomFieldEntity.Product, includeInactive: true, manage: true)).Single(row => row.Id == id);
        Assert.True(f.Update(Owner, id, "New label", CustomFieldType.Text, null, false, expected: old.EditVersion).Ok);
        var before = Audits(t);
        Assert.False(f.Update(Owner, id, "Stale label", CustomFieldType.Text, null, false, expected: old.EditVersion).Ok);
        Assert.False(f.SetActive(Owner, id, false, old.EditVersion).Ok);
        Assert.False(f.Move(Owner, id, -1, old.EditVersion).Ok); Assert.Equal(before, Audits(t));
    }

    [Fact]
    public void DefinitionReorderBaselineIncludesTheWholeEntityOrder()
    {
        using var t = new TestDatabase(); var f = Fields(t); var id = AddField(t);
        var old = Value(f.List(Owner, CustomFieldEntity.Product, includeInactive: true, manage: true)).Single(row => row.Id == id);
        Value(f.Add(Owner, CustomFieldEntity.Product, "Another question", CustomFieldType.Text, null, false));
        Assert.False(f.Move(Owner, id, -1, old.EditVersion).Ok);
    }

    [Fact]
    public void NativeRiskConfirmationDoesNotAuthorizeAChangedDefinition()
    {
        using var t = new TestDatabase(); var n = Native(t); Assert.True(n.EnsureDefaults().Ok);
        var old = Value(n.List(Owner)).First(row => row.Critical);
        t.Database.Tx(c => t.Database.Execute(c, "UPDATE form_field_definitions SET label='changed during modal' WHERE id=@id;", new { id = old.Id }));
        Assert.False(n.SetRequired(Owner, old.Id, false, true, old.EditVersion).Ok);
        Assert.True(Value(n.List(Owner)).Single(row => row.Id == old.Id).Required);
    }

    [Fact]
    public void CatalogRenameStaleVersionCannotOverwriteNewName()
    {
        using var t = new TestDatabase(); var p = Products(t); var id = Value(p.AddCategory(Owner, "Synthetic category"));
        var old = Value(p.GetCategories()).Single(row => row.Id == id);
        Assert.True(p.UpdateCategory(Owner, id, "First name", null, 10, old.EditVersion).Ok);
        Assert.False(p.UpdateCategory(Owner, id, "Stale name", null, 20, old.EditVersion).Ok);
        Assert.Equal("First name", Value(p.GetCategories()).Single(row => row.Id == id).Name);
    }

    [Fact]
    public void UnitQuantityRuleCannotBeOverwrittenByAnOldUnitForm()
    {
        using var t = new TestDatabase(); var p = Products(t); var id = Value(p.AddUnit(Owner, "syntheticunit"));
        var old = Value(p.GetUnits()).Single(row => row.Id == id);
        Assert.True(p.UpdateUnit(Owner, id, "syntheticunit", 10, true, old.EditVersion).Ok);
        Assert.False(p.UpdateUnit(Owner, id, "staleunit", 10, false, old.EditVersion).Ok);
        Assert.True(Value(p.GetUnits()).Single(row => row.Id == id).WholeOnly);
    }

    [Fact]
    public void BrandPhaseTwoUsesTheExactPhaseOneVersionNotAFreshBlindRead()
    {
        using var t = new TestDatabase(); var p = Products(t); var created = p.AddBrand(Owner, "Synthetic brand"); Assert.True(created.Ok);
        var company = Value(p.AddCompany(Owner, "Synthetic company"));
        var current = Value(p.GetBrands()).Single(row => row.Id == created.Value);
        Assert.True(p.SetBrandCompany(Owner, created.Value, company, current.EditVersion).Ok);
        Assert.False(p.SetBrandCompany(Owner, created.Value, null, created.EditVersion).Ok);
        Assert.Equal(company, Value(p.GetBrands()).Single(row => row.Id == created.Value).CompanyId);
    }

    [Fact]
    public void SnapshotKeepsBothReadsConsistentAcrossAnotherConnectionCommit()
    {
        using var t = new TestDatabase(); var id = TestData.AddProduct(t);
        var one = new Database(t.Database.FilePath, pooling: false); var two = new Database(t.Database.FilePath, pooling: false);
        using (var warm = two.Open()) { }
        one.Snapshot(c =>
        {
            var old = one.QuerySingle<string>(c, "SELECT name FROM products WHERE id=@id;", new { id });
            two.Tx(writer => two.Execute(writer, "UPDATE products SET name='committed elsewhere' WHERE id=@id;", new { id }));
            Assert.Equal(old, one.QuerySingle<string>(c, "SELECT name FROM products WHERE id=@id;", new { id }));
            Assert.Equal(old, one.Read(borrowed => one.QuerySingle<string>(borrowed, "SELECT name FROM products WHERE id=@id;", new { id })));
            return true;
        });
        Assert.Equal("committed elsewhere", one.Read(c => one.QuerySingle<string>(c, "SELECT name FROM products WHERE id=@id;", new { id })));
    }

    [Fact]
    public void ReadSnapshotBlocksAccidentalWriteAndLeavesTheDatabaseUsable()
    {
        using var t = new TestDatabase();
        Assert.Throws<SqliteException>(() => t.Database.Snapshot(c => t.Database.Execute(c, "UPDATE customers SET name='must not write';")));
        t.Database.Tx(c => t.Database.Execute(c, "UPDATE customers SET name='fresh write';"));
        Assert.True(t.Database.Snapshot(c => Convert.ToInt64(t.Database.Scalar(c, "SELECT COUNT(*) FROM users;")) > 0));
    }

    [Fact]
    public void SnapshotInsideWriteBorrowsTheSameConnectionAndNestedTxIsRefused()
    {
        using var t = new TestDatabase();
        t.Database.Tx(c =>
        {
            Assert.True(t.Database.Snapshot(borrowed => ReferenceEquals(c, borrowed)));
            Assert.Throws<InvalidOperationException>(() => t.Database.Tx(_ => true));
        });
    }

    [Fact]
    public void FreshMainSwitchReadDoesNotUseAnotherSettingsInstancesOldOnCache()
    {
        using var t = new TestDatabase(); var stale = new SettingsService(t.Database, t.Audit, t.Log, TestData.Permissions(t)); stale.Reload();
        Assert.True(stale.GetBool(MasterFoundationSettings.ProfitShowMargins, true));
        t.Database.Tx(c => t.Database.Execute(c, "UPDATE settings SET value='false' WHERE key=@key;", new { key = MasterFoundationSettings.ProfitShowMargins }));
        Assert.True(stale.GetBool(MasterFoundationSettings.ProfitShowMargins, true));
        Assert.False(CostPolicy.CanSee(TestData.Permissions(t), stale, Owner));
        Assert.False(ProfitPolicy.CanSee(TestData.Permissions(t), stale, Owner));
        t.Database.Tx(c => t.Database.Execute(c, "UPDATE settings SET value='true' WHERE key=@key;", new { key = MasterFoundationSettings.ProfitShowMargins }));
        Assert.True(CostPolicy.CanSee(TestData.Permissions(t), stale, Owner));
    }

    [Fact]
    public void EditBaselinesNeverLeakIntoAuditOrDraftSerialization()
    {
        using var t = new TestDatabase(); var id = TestData.AddProduct(t); var form = Value(Products(t).LoadEditForm(Owner, id));
        var json = System.Text.Json.JsonSerializer.Serialize(form);
        Assert.DoesNotContain("Fingerprint", json); Assert.DoesNotContain("EditVersion", json); Assert.DoesNotContain("\"Version\"", json);
    }
}
