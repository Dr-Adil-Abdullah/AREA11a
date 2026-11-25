using Area11.Core.Models;
using Area11.Core.Services;
using Area11.Core.Utils;
using Xunit;

namespace Area11.Tests;

/// <summary>Real-schema tests when run under .NET. Guard scenarios are synthetic observations,
/// NOT actual Desktop/WPF execution or provider COMMIT/rollback failure injection.</summary>
public sealed class RecordSaveOutcomeTests
{
    private sealed class RecordSaveFixture : IDisposable
    {
        public TestDatabase T { get; } = new();
        public ProductService Products { get; }
        public PartnerService Partners { get; }
        public int Product { get; }
        public int Customer { get; }
        public int Category { get; }
        public int Company { get; }
        public int Formula { get; }
        public int Brand { get; }
        public int Unit { get; }
        public RecordSaveFixture()
        {
            Products = new ProductService(T.Database, T.Audit, T.Log, null, TestData.Permissions(T), T.Settings);
            Partners = TestData.Partners(T);
            Product = TestData.AddProduct(T); Customer = TestData.AddCustomer(T);
            Category = Id(Products.AddCategory(TestData.Owner, "Synthetic save category"));
            Company = Id(Products.AddCompany(TestData.Owner, "Synthetic save company"));
            Formula = Id(Products.AddFormula(TestData.Owner, "Synthetic save formula"));
            Brand = Id(Products.AddBrand(TestData.Owner, "Synthetic save brand"));
            Unit = Id(Products.AddUnit(TestData.Owner, "syntheticsaveunit"));
        }
        private static int Id(ServiceResult<int> saved) { Assert.True(saved.Ok, saved.Error); Assert.True(saved.Value > 0); return saved.Value; }
        public void Execute(string sql) => T.Database.Tx(c => { T.Database.Execute(c, sql); });
        public long Number(string sql) => T.Database.Read(c => Convert.ToInt64(T.Database.Scalar(c, sql)));
        public string Snapshot() => T.Database.Read(c => string.Join("\n", new[]
        {
            "customers", "suppliers", "products", "categories", "companies", "generic_formulas", "brands", "units",
            "product_category_tags", "batches", "stock_movements", "sales", "purchases", "payments", "audit_logs", "custom_values", "photos", "sqlite_sequence"
        }.Select(table =>
        {
            var columns = T.Database.Query<string>(c, "SELECT name FROM pragma_table_info(@table);", new { table });
            var projection = string.Join("||':'||", columns.Select(column => "quote(\"" + column + "\")"));
            return table + "=" + string.Join(";", T.Database.Query<string>(c, $"SELECT {projection} FROM {table} ORDER BY 1;"));
        })));
        public ServiceResult Save(string operation, bool invalid = false)
        {
            var name = invalid ? "" : "Synthetic changed record";
            return operation switch
            {
                "product-create" => Products.Create(TestData.Owner, new ProductPatch { Name = name, BaseUnit = "tablet", CostPaisa = 20000, RetailPaisa = 30000 }),
                "customer-create" => Partners.CreateCustomer(TestData.Owner, name, null, "normal"),
                "supplier-create" => Partners.CreateSupplier(TestData.Owner, name, null, null),
                "product-edit" => Products.Update(TestData.Owner, Product, new ProductPatch { Name = name }),
                "customer-edit" => Partners.UpdateCustomer(TestData.Owner, new CustomerEditInput { Id = Customer, Name = name, Category = "normal" }),
                "category-add" => Products.AddCategory(TestData.Owner, name),
                "category-edit" => Products.UpdateCategory(TestData.Owner, Category, name, null, 10),
                "company-add" => Products.AddCompany(TestData.Owner, name),
                "company-edit" => Products.UpdateCompany(TestData.Owner, Company, name, 10),
                "formula-add" => Products.AddFormula(TestData.Owner, name),
                "formula-edit" => Products.UpdateFormula(TestData.Owner, Formula, name, 10),
                "brand-add" => Products.AddBrand(TestData.Owner, name),
                "brand-edit" => Products.UpdateBrand(TestData.Owner, Brand, name, 10),
                "unit-add" => Products.AddUnit(TestData.Owner, invalid ? "" : "syntheticchangedunit"),
                "unit-edit" => Products.UpdateUnit(TestData.Owner, Unit, invalid ? "" : "syntheticchangedunit", 10),
                "brand-link" => Products.SetBrandCompany(TestData.Owner, Brand, invalid ? int.MaxValue : Company),
                _ => throw new ArgumentOutOfRangeException(nameof(operation))
            };
        }
        public void Dispose() => T.Dispose();
    }

    [Theory]
    [InlineData("product-create", false)]
    [InlineData("product-create", true)]
    [InlineData("customer-create", false)]
    [InlineData("customer-create", true)]
    [InlineData("supplier-create", false)]
    [InlineData("supplier-create", true)]
    [InlineData("product-edit", false)]
    [InlineData("product-edit", true)]
    [InlineData("customer-edit", false)]
    [InlineData("customer-edit", true)]
    [InlineData("category-add", false)]
    [InlineData("category-add", true)]
    [InlineData("category-edit", false)]
    [InlineData("category-edit", true)]
    [InlineData("company-add", false)]
    [InlineData("company-add", true)]
    [InlineData("company-edit", false)]
    [InlineData("company-edit", true)]
    [InlineData("formula-add", false)]
    [InlineData("formula-add", true)]
    [InlineData("formula-edit", false)]
    [InlineData("formula-edit", true)]
    [InlineData("brand-add", false)]
    [InlineData("brand-add", true)]
    [InlineData("brand-edit", false)]
    [InlineData("brand-edit", true)]
    [InlineData("unit-add", false)]
    [InlineData("unit-add", true)]
    [InlineData("unit-edit", false)]
    [InlineData("unit-edit", true)]
    [InlineData("brand-link", false)]
    [InlineData("brand-link", true)]
    public void AuditFaultRollsBackEveryParentAndAllowsOneLocalRetry(string operation, bool after)
    {
        using var f = new RecordSaveFixture(); var before = f.Snapshot();
        var clause = after ? "AFTER INSERT ON audit_logs" : "BEFORE INSERT ON audit_logs";
        f.Execute("CREATE TRIGGER record_save_fault " + clause + " BEGIN SELECT RAISE(ABORT,'synthetic-private-record-save'); END;");
        var guard = new PostingAttemptGuard(); Assert.True(guard.TryBegin()); var failed = f.Save(operation);
        Assert.False(failed.Ok); Assert.True(failed.NoCommitConfirmed); Assert.DoesNotContain("synthetic-private", failed.Error!);
        guard.ObserveFailure(failed); Assert.True(guard.Ready); Assert.Equal(before, f.Snapshot());
        f.Execute("DROP TRIGGER record_save_fault;"); Assert.True(guard.TryBegin()); var saved = f.Save(operation); Assert.True(saved.Ok, saved.Error);
        guard.AcknowledgeCommit(); Assert.False(guard.TryBegin());
        Assert.Equal(0L, f.Number("SELECT COUNT(*) FROM sales;")); Assert.Equal(0L, f.Number("SELECT COUNT(*) FROM purchases;"));
        Assert.Equal(0L, f.Number("SELECT COUNT(*) FROM payments;")); Assert.Equal(100L, f.Number("SELECT SUM(qty_base) FROM batches;"));
        guard.CompleteReset(); Assert.True(guard.Ready);
    }

    [Theory]
    [InlineData("product-create")]
    [InlineData("customer-create")]
    [InlineData("supplier-create")]
    [InlineData("product-edit")]
    [InlineData("customer-edit")]
    [InlineData("category-add")]
    [InlineData("category-edit")]
    [InlineData("company-add")]
    [InlineData("company-edit")]
    [InlineData("formula-add")]
    [InlineData("formula-edit")]
    [InlineData("brand-add")]
    [InlineData("brand-edit")]
    [InlineData("unit-add")]
    [InlineData("unit-edit")]
    [InlineData("brand-link")]
    public void ValidationRefusalIsConfirmedAndDoesNotStrandAnOrdinaryCorrection(string operation)
    {
        using var f = new RecordSaveFixture(); var before = f.Snapshot(); var guard = new PostingAttemptGuard(); Assert.True(guard.TryBegin());
        var result = f.Save(operation, invalid: true); Assert.False(result.Ok); Assert.True(result.NoCommitConfirmed);
        guard.ObserveFailure(result); Assert.True(guard.Ready); Assert.Equal(before, f.Snapshot()); Assert.True(f.Save(operation).Ok);
    }

    [Theory]
    [InlineData("product-create")]
    [InlineData("customer-create")]
    [InlineData("supplier-create")]
    [InlineData("product-edit")]
    [InlineData("customer-edit")]
    [InlineData("category-add")]
    [InlineData("category-edit")]
    [InlineData("company-add")]
    [InlineData("company-edit")]
    [InlineData("formula-add")]
    [InlineData("formula-edit")]
    [InlineData("brand-add")]
    [InlineData("brand-edit")]
    [InlineData("unit-add")]
    [InlineData("unit-edit")]
    [InlineData("brand-link")]
    public void AcknowledgedSaveCannotBeReenteredAfterSimulatedRefreshFailure(string operation)
    {
        using var f = new RecordSaveFixture(); var guard = new PostingAttemptGuard(); var before = f.Number("SELECT COUNT(*) FROM audit_logs;");
        Assert.True(guard.TryBegin()); var result = f.Save(operation); Assert.True(result.Ok, result.Error); guard.AcknowledgeCommit();
        // The fault below simulates a callback AFTER the real service succeeds;not a WPF test.
        // Explicit Action selects xUnit's synchronous overload, not obsolete Func<Task>.
        Assert.Throws<InvalidOperationException>((Action)(() => throw new InvalidOperationException("synthetic UI refresh")));
        guard.ObserveException(); Assert.True(guard.WasAcknowledged); Assert.False(guard.TryBegin());
        Assert.Equal(before + 1, f.Number("SELECT COUNT(*) FROM audit_logs;"));
        Assert.Equal(PostingAttemptState.CommittedNeedsReset, guard.State);
    }

    [Theory]
    [InlineData("unknown")]
    [InlineData("throw")]
    [InlineData("confirmed")]
    [InlineData("ack-fault")]
    [InlineData("reset")]
    [InlineData("cancel")]
    [InlineData("reentry")]
    [InlineData("malformed-success")]
    public void SyntheticFormObservationsNeverReleaseUnverifiedWork(string scenario)
    {
        var guard = new PostingAttemptGuard(); Assert.True(guard.TryBegin());
        switch (scenario)
        {
            case "confirmed": guard.ObserveFailure(new ServiceResult { NoCommitConfirmed = true }); break;
            case "unknown": guard.ObserveFailure(ServiceResult.Fail("synthetic unknown")); break;
            case "cancel": guard.CancelBeforeWrite(); break;
            case "ack-fault": guard.AcknowledgeCommit(); guard.ObserveException(); break;
            case "reset": guard.AcknowledgeCommit(); guard.CompleteReset(); break;
            case "reentry": Assert.False(guard.TryBegin()); break;
            default: guard.ObserveException(); break;
        }
        Assert.Equal(scenario is "confirmed" or "reset" or "cancel", guard.Ready);
        if (!guard.Ready) Assert.False(guard.TryBegin());
    }
}
