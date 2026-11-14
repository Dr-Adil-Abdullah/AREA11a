using Area11.Core.Models;
using Area11.Core.Services;
using Xunit;

namespace Area11.Tests;

/// <summary>Phase4 portion2: bounded direct Core access/projection regressions, not session authentication.</summary>
public sealed class PhaseFourAccessProjectionTests
{
    private static NativeFieldPolicyService Fields(TestDatabase t) => new(t.Database, t.Audit, t.Log);
    private static RepService Reps(TestDatabase t) => new(t.Database, t.Audit, t.Settings, TestData.Permissions(t), t.Log);
    private static ProductHistoryService History(TestDatabase t) => new(t.Database, t.Settings, TestData.Permissions(t), t.Log);
    private static int QuantityField(TestDatabase t)
    {
        var fields = Fields(t);
        Assert.True(fields.EnsureDefaults().Ok);
        return fields.List(TestData.Owner).Value!.Single(f => f.FieldKey == "sale.quantity").Id;
    }
    private static long Scalar(TestDatabase t, string sql)
    {
        using var c = t.Database.Open();
        return Convert.ToInt64(t.Database.Scalar(c, sql) ?? 0L);
    }
    private static User Forged(TestDatabase t)
    {
        var cashier = TestData.Cashier(t);
        return new User { Id = cashier.Id, Name = "Forged owner", Role = "owner" };
    }
    private static (int Product, int Doctor) PrescribedSale(TestDatabase t)
    {
        var product = TestData.AddProduct(t, costPaisa: 20000, retailPaisa: 30000);
        var doctor = TestData.AddCustomer(t, name: "Dr. Projection", category: "doctor");
        var sale = TestData.Sales(t).Create(TestData.Cart(product, qty: 2, doctorId: doctor), TestData.Owner);
        Assert.True(sale.Ok, sale.Error);
        return (product, doctor);
    }

    [Fact]
    public void ForgedOwnerCannotListNativeFieldMetadata()
    {
        using var t = new TestDatabase(); QuantityField(t);
        Assert.False(Fields(t).List(Forged(t)).Ok);
    }

    [Fact]
    public void ForgedOwnerCannotRelaxCriticalNativeField()
    {
        using var t = new TestDatabase(); var id = QuantityField(t);
        Assert.False(Fields(t).SetRequired(Forged(t), id, false, true).Ok);
        Assert.Equal(1L, Scalar(t, "SELECT required FROM form_field_definitions WHERE field_key='sale.quantity';"));
        Assert.Equal(0L, Scalar(t, "SELECT COUNT(*) FROM audit_logs WHERE action='native_field_required_changed';"));
    }

    [Fact]
    public void InactiveOwnerCannotReadOrWriteNativeFieldPolicy()
    {
        using var t = new TestDatabase(); var id = QuantityField(t);
        t.Database.Tx(c => { t.Database.Execute(c, "UPDATE users SET active=0 WHERE id=1;"); });
        Assert.False(Fields(t).List(TestData.Owner).Ok);
        Assert.False(Fields(t).SetRequired(TestData.Owner, id, false, true).Ok);
        Assert.Equal(1L, Scalar(t, "SELECT required FROM form_field_definitions WHERE field_key='sale.quantity';"));
    }

    [Fact]
    public void DemotedOwnerCannotUseStaleNativeFieldSnapshot()
    {
        using var t = new TestDatabase(); var id = QuantityField(t);
        t.Database.Tx(c => { t.Database.Execute(c, "UPDATE users SET role='manager' WHERE id=1;"); });
        Assert.False(Fields(t).List(TestData.Owner).Ok);
        Assert.False(Fields(t).SetRequired(TestData.Owner, id, false, true).Ok);
    }

    [Fact]
    public void MissingOwnerIdCannotChangeNativeFields()
    {
        using var t = new TestDatabase(); var id = QuantityField(t);
        var missing = new User { Id = int.MaxValue, Name = "Missing", Role = "owner" };
        Assert.False(Fields(t).List(missing).Ok);
        Assert.False(Fields(t).SetRequired(missing, id, false, true).Ok);
    }

    [Fact]
    public void NativeFieldAcknowledgementUsesCurrentStoredOwnerName()
    {
        using var t = new TestDatabase(); var id = QuantityField(t);
        t.Database.Tx(c => { t.Database.Execute(c, "UPDATE users SET name='Current Owner' WHERE id=1;"); });
        Assert.True(Fields(t).SetRequired(TestData.Owner, id, false, true).Ok);
        var field = Fields(t).List(TestData.Owner).Value!.Single(f => f.Id == id);
        Assert.Equal("Current Owner", field.OverrideAcknowledgedBy);
        using var connection = t.Database.Open();
        Assert.Equal("Current Owner", Convert.ToString(t.Database.Scalar(connection,
            "SELECT user_name FROM audit_logs WHERE action='native_field_required_changed' ORDER BY id DESC LIMIT 1;")));
    }

    [Fact]
    public void NativeFieldNoOpDoesNotBypassCurrentOwnerCheck()
    {
        using var t = new TestDatabase(); var id = QuantityField(t);
        Assert.False(Fields(t).SetRequired(Forged(t), id, true, false).Ok);
        Assert.Equal(0L, Scalar(t, "SELECT COUNT(*) FROM audit_logs WHERE action='native_field_required_changed';"));
    }

    [Fact]
    public void DoctorProjectionObeysGlobalProfitSwitchForOwner()
    {
        using var t = new TestDatabase(); var (_, doctor) = PrescribedSale(t);
        Assert.True(t.Settings.Set(MasterFoundationSettings.ProfitShowMargins, false, TestData.Owner).Ok);
        var row = Reps(t).ListDoctors(TestData.Owner).Value!.Single(r => r.DoctorId == doctor);
        Assert.False(row.ProfitKnown);
        Assert.Equal(0L, row.ProfitPaisa);
        Assert.Equal(60000L, row.SalesPaisa);
        Assert.Equal(1, row.Bills);
        Assert.Equal(20000L, Scalar(t, "SELECT SUM(line_total_paisa-qty_base*cost_paisa_at_sale) FROM sale_items;"));
    }

    [Fact]
    public void DoctorProfitGrantDoesNotOverrideGlobalHideSwitch()
    {
        using var t = new TestDatabase(); var (_, doctor) = PrescribedSale(t);
        var manager = TestData.AddUser(t, "manager");
        Assert.True(TestData.Permissions(t).Set(TestData.Owner, manager.Id, Modules.Profit, true).Ok);
        Assert.True(t.Settings.Set(MasterFoundationSettings.ProfitShowMargins, false, TestData.Owner).Ok);
        var row = Reps(t).ListDoctors(manager).Value!.Single(r => r.DoctorId == doctor);
        Assert.False(row.ProfitKnown);
        Assert.Equal(0L, row.ProfitPaisa);
    }

    [Fact]
    public void DoctorProfitCanBeShownAgainWithoutRewritingSales()
    {
        using var t = new TestDatabase(); var (_, doctor) = PrescribedSale(t);
        var service = Reps(t);
        Assert.True(t.Settings.Set(MasterFoundationSettings.ProfitShowMargins, false, TestData.Owner).Ok);
        Assert.False(service.ListDoctors(TestData.Owner).Value!.Single(r => r.DoctorId == doctor).ProfitKnown);
        Assert.True(t.Settings.Set(MasterFoundationSettings.ProfitShowMargins, true, TestData.Owner).Ok);
        var row = service.ListDoctors(TestData.Owner).Value!.Single(r => r.DoctorId == doctor);
        Assert.True(row.ProfitKnown);
        Assert.Equal(20000L, row.ProfitPaisa);
        Assert.Equal(1L, Scalar(t, "SELECT COUNT(*) FROM sales;"));
    }

    [Fact]
    public void ProductHistoryDoesNotReturnHiddenRawProfitToManager()
    {
        using var t = new TestDatabase(); var (product, _) = PrescribedSale(t);
        var manager = TestData.AddUser(t, "manager");
        var loaded = History(t).Load(manager, product);
        Assert.True(loaded.Ok, loaded.Error);
        Assert.False(loaded.Value!.CanSeeProfit);
        Assert.All(loaded.Value.Sales, row => Assert.Equal(0L, row.ProfitPaisa));
        Assert.Equal(0L, loaded.Value.ProfitPaisa);
        Assert.Equal(60000L, loaded.Value.SoldPaisa);
        Assert.Single(loaded.Value.Sales);
    }

    [Fact]
    public void ProductHistoryMasksOwnerRawProfitWhenGlobalOffAndRestoresFromSnapshots()
    {
        using var t = new TestDatabase(); var (product, _) = PrescribedSale(t);
        Assert.True(t.Settings.Set(MasterFoundationSettings.ProfitShowMargins, false, TestData.Owner).Ok);
        var hidden = History(t).Load(TestData.Owner, product).Value!;
        Assert.False(hidden.CanSeeProfit);
        Assert.Equal(0L, hidden.Sales.Single().ProfitPaisa);
        Assert.Equal(0L, hidden.ProfitPaisa);
        Assert.True(t.Settings.Set(MasterFoundationSettings.ProfitShowMargins, true, TestData.Owner).Ok);
        var visible = History(t).Load(TestData.Owner, product).Value!;
        Assert.True(visible.CanSeeProfit);
        Assert.Equal(20000L, visible.Sales.Single().ProfitPaisa);
        Assert.Equal(20000L, visible.ProfitPaisa);
        Assert.Equal(60000L, visible.SoldPaisa);
    }
}
