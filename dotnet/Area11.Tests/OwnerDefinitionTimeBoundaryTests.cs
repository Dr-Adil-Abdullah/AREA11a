using Area11.Core.Models;
using Area11.Core.Services;
using Xunit;

namespace Area11.Tests;

/// <summary>Phase4 portion6. Disposable local data only; no Windows/session certification.</summary>
[Collection("Time")]
public sealed class OwnerDefinitionTimeBoundaryTests
{
    private const string Wrong = "2026-11-01 10:00:00";
    private const string Correct = "2026-11-03 10:00:00";
    private static readonly TimeSpan Drift = TimeSpan.FromDays(2);
    private static CustomFieldService Fields(TestDatabase t) => new(t.Database, t.Audit, TestData.Permissions(t), t.Log);
    private static TimeCorrectionService Fix(TestDatabase t) => new(t.Database, t.Audit, t.Log);
    private static void Execute(TestDatabase t, string sql) => t.Database.Tx(c => { t.Database.Execute(c, sql); });
    private static string Text(TestDatabase t, string sql)
    {
        using var c = t.Database.Open(); return Convert.ToString(t.Database.Scalar(c, sql)) ?? "";
    }
    private static long Number(TestDatabase t, string sql) => Convert.ToInt64(Text(t, sql));
    private static int Field(TestDatabase t, string label = "Licence")
    {
        var added = Fields(t).Add(TestData.Owner, CustomFieldEntity.Customer, label, CustomFieldType.Text, null, false);
        Assert.True(added.Ok, added.Error); return added.Value!.Id;
    }
    private static int Bill(TestDatabase t, string code = "INV-BOUNDARY", string paymentDate = Wrong)
    {
        return t.Database.Tx(c =>
        {
            t.Database.Execute(c, @"
INSERT INTO sales(code,date,subtotal_paisa,total_paisa,paid_paisa,status,payment_method,created_at,time_offset_seconds)
VALUES(@code,@date,30000,30000,30000,'paid','cash',@date,0);", new { code, date = Wrong });
            var id = (int)t.Database.NextId(c);
            t.Database.Execute(c, @"
INSERT INTO payments(date,method,amount_paisa,sale_id,user_id)
VALUES(@date,'cash',30000,@id,1);", new { date = paymentDate, id });
            return id;
        });
    }
    private static void Denied(TestDatabase t, User? actor, int fieldId, int bill)
    {
        var fields = Fields(t);
        Assert.False(fields.List(actor, CustomFieldEntity.Customer, manage: true).Ok);
        Assert.False(fields.List(actor, CustomFieldEntity.Customer, includeInactive: true).Ok);
        Assert.False(fields.Add(actor, CustomFieldEntity.Customer, "Other fieldId", CustomFieldType.Text, null, false).Ok);
        Assert.False(fields.Update(actor, fieldId, "Changed label", CustomFieldType.Text, null, false).Ok);
        Assert.False(fields.SetActive(actor, fieldId, true).Ok); // even same-value success is protected
        Assert.False(fields.Move(actor, fieldId, -1).Ok); // even first-row edge is protected
        var fix = Fix(t); Assert.False(fix.CanFix(actor));
        Assert.False(fix.ListBills(actor, "2026-11-01", "2026-11-10", Drift).Ok);
        Assert.False(fix.ApplyCorrection(actor, new[] { bill }, Drift).Ok);
        Assert.Equal(Wrong, Text(t, "SELECT date FROM sales LIMIT 1;"));
    }

    [Fact]
    public void AnonymousDefinitionAndTimeAdministrationRefuse()
    {
        using var t = new TestDatabase(); Denied(t, null, Field(t), Bill(t));
    }
    [Fact]
    public void ForgedOwnerLabelCannotAdministerDefinitionsOrBillTimes()
    {
        using var t = new TestDatabase(); var fieldId = Field(t); var bill = Bill(t); var staff = TestData.Cashier(t);
        Denied(t, new User { Id = staff.Id, Role = "owner" }, fieldId, bill);
    }
    [Fact]
    public void MissingOwnerIdCannotAdministerDefinitionsOrBillTimes()
    {
        using var t = new TestDatabase(); Denied(t, new User { Id = int.MaxValue, Role = "owner" }, Field(t), Bill(t));
    }
    [Fact]
    public void InactiveDatabaseOwnerCannotUseNoOpOrDefinitionAndTimeWrites()
    {
        using var t = new TestDatabase(); var fieldId = Field(t); var bill = Bill(t);
        Execute(t, "UPDATE users SET active=0 WHERE id=1;"); Denied(t, TestData.Owner, fieldId, bill);
        Assert.Equal(1L, Number(t, "SELECT COUNT(*) FROM custom_fields;"));
        Assert.Equal(0L, Number(t, "SELECT COUNT(*) FROM audit_logs WHERE action='sale_time_fixed';"));
    }
    [Fact]
    public void DemotedOwnerSnapshotCannotUseDefinitionsOrTimeCorrection()
    {
        using var t = new TestDatabase(); var fieldId = Field(t); var bill = Bill(t);
        Execute(t, "UPDATE users SET role='manager' WHERE id=1;"); Denied(t, TestData.Owner, fieldId, bill);
    }
    [Fact]
    public void ManagerSettingsPermissionDoesNotGrantDefinitionOrTimeAdministration()
    {
        using var t = new TestDatabase(); Denied(t, TestData.AddUser(t, "manager"), Field(t), Bill(t));
    }
    [Fact]
    public void DefinitionAuditsUseCurrentStoredOwnerName()
    {
        using var t = new TestDatabase(); var first = Field(t); var second = Field(t, "Area code");
        Execute(t, "UPDATE users SET name='Current owner' WHERE id=1;"); var fields = Fields(t);
        Assert.True(fields.Update(TestData.Owner, first, "Licence revised", CustomFieldType.Text, null, true).Ok);
        Assert.True(fields.SetActive(TestData.Owner, first, false).Ok);
        Assert.True(fields.Move(TestData.Owner, second, -1).Ok);
        Field(t, "Reference");
        Assert.Equal(4L, Number(t, "SELECT COUNT(*) FROM audit_logs WHERE user_name='Current owner' AND action LIKE 'custom_field_%';"));
    }
    [Fact]
    public void UnchangedActiveTickRevalidatesOwnerBeforeSuccess()
    {
        using var t = new TestDatabase(); var fieldId = Field(t); var fields = Fields(t);
        Assert.True(fields.SetActive(TestData.Owner, fieldId, true).Ok);
        Execute(t, "UPDATE users SET role='cashier' WHERE id=1;");
        Assert.False(fields.SetActive(TestData.Owner, fieldId, true).Ok);
    }
    [Fact]
    public void CustomFieldReadsRejectInactiveAndStaleRoleReaders()
    {
        using var t = new TestDatabase(); var fieldId = Field(t); var customer = TestData.AddCustomer(t);
        var fields = Fields(t); Assert.True(fields.SaveValues(TestData.Owner, CustomFieldEntity.Customer, customer,
            new Dictionary<int, string?> { [fieldId] = "Saved answer" }).Ok);
        Execute(t, "UPDATE users SET role='cashier' WHERE id=1;");
        Assert.False(fields.List(TestData.Owner, CustomFieldEntity.Customer).Ok);
        Assert.False(fields.Values(TestData.Owner, CustomFieldEntity.Customer, customer).Ok);
        Assert.False(fields.DescribeValues(TestData.Owner, CustomFieldEntity.Customer, customer).Ok);
        Execute(t, "UPDATE users SET role='owner',active=0 WHERE id=1;");
        Assert.False(fields.Values(TestData.Owner, CustomFieldEntity.Customer, customer).Ok);
    }
    [Fact]
    public void CashierKeepsAuthorizedCustomerAnswerWorkflow()
    {
        using var t = new TestDatabase(); var fieldId = Field(t); var customer = TestData.AddCustomer(t); var staff = TestData.Cashier(t);
        var fields = Fields(t); Assert.True(fields.List(staff, CustomFieldEntity.Customer).Ok);
        Assert.True(fields.SaveValues(staff, CustomFieldEntity.Customer, customer, new Dictionary<int, string?> { [fieldId] = "ABC" }).Ok);
        Assert.Equal("ABC", fields.Values(staff, CustomFieldEntity.Customer, customer).Value![fieldId]);
        Assert.Equal("ABC", Assert.Single(fields.DescribeValues(staff, CustomFieldEntity.Customer, customer).Value!).Value);
    }
    [Fact]
    public void SameFieldServiceChecksNextReadAndPublicSaveAfterModuleRevocation()
    {
        using var t = new TestDatabase(); var fieldId = Field(t); var customer = TestData.AddCustomer(t); var staff = TestData.Cashier(t); var fields = Fields(t);
        Assert.True(fields.SaveValues(staff, CustomFieldEntity.Customer, customer, new Dictionary<int, string?> { [fieldId] = "Before" }).Ok);
        Assert.True(TestData.Permissions(t).Set(TestData.Owner, staff.Id, Modules.Customers, false).Ok);
        Assert.False(fields.List(staff, CustomFieldEntity.Customer).Ok);
        Assert.False(fields.Values(staff, CustomFieldEntity.Customer, customer).Ok);
        Assert.False(fields.DescribeValues(staff, CustomFieldEntity.Customer, customer).Ok);
        Assert.False(fields.SaveValues(staff, CustomFieldEntity.Customer, customer, new Dictionary<int, string?> { [fieldId] = "After" }).Ok);
        Assert.Equal("Before", fields.Values(TestData.Owner, CustomFieldEntity.Customer, customer).Value![fieldId]);
    }
    [Fact]
    public void PublicAnswerSaveUsesCurrentStoredReaderInAudit()
    {
        using var t = new TestDatabase(); var fieldId = Field(t); var customer = TestData.AddCustomer(t); var staff = TestData.Cashier(t);
        Execute(t, "UPDATE users SET name='Current staff' WHERE role='cashier';");
        Assert.True(Fields(t).SaveValues(staff, CustomFieldEntity.Customer, customer, new Dictionary<int, string?> { [fieldId] = "Answer" }).Ok);
        Assert.Equal("Current staff", Text(t, "SELECT user_name FROM audit_logs WHERE action='custom_value_set';"));
    }
    [Fact]
    public void DefinitionAuditFailureRollsBackDefinitionWrite()
    {
        using var t = new TestDatabase(); var fieldId = Field(t);
        Execute(t, @"CREATE TRIGGER fail_definition_audit BEFORE INSERT ON audit_logs
WHEN NEW.action='custom_field_update' BEGIN SELECT RAISE(ABORT,'test audit fault'); END;");
        Assert.False(Fields(t).Update(TestData.Owner, fieldId, "Changed name", CustomFieldType.Text, null, true).Ok);
        Assert.Equal("Licence", Text(t, "SELECT label FROM custom_fields;"));
        Assert.Equal(0L, Number(t, "SELECT required FROM custom_fields;"));
    }
    [Fact]
    public void PutAwayFieldRetainsAnswersAndOwnerCanListInactiveDefinitions()
    {
        using var t = new TestDatabase(); var fieldId = Field(t); var customer = TestData.AddCustomer(t); var fields = Fields(t);
        Assert.True(fields.SaveValues(TestData.Owner, CustomFieldEntity.Customer, customer, new Dictionary<int, string?> { [fieldId] = "Keep" }).Ok);
        Assert.True(fields.SetActive(TestData.Owner, fieldId, false).Ok);
        Assert.Empty(fields.List(TestData.Owner, CustomFieldEntity.Customer).Value!);
        Assert.Single(fields.List(TestData.Owner, CustomFieldEntity.Customer, includeInactive: true, manage: true).Value!);
        Assert.Equal("Keep", fields.Values(TestData.Owner, CustomFieldEntity.Customer, customer).Value![fieldId]);
    }
    [Fact]
    public void PreviewDoesNotAuthorizeApplyAfterOwnerDeactivation()
    {
        using var t = new TestDatabase(); var bill = Bill(t); var fix = Fix(t);
        Assert.True(fix.ListBills(TestData.Owner, "2026-11-01", "2026-11-10", Drift).Ok);
        Execute(t, "UPDATE users SET active=0 WHERE id=1;");
        Assert.False(fix.ApplyCorrection(TestData.Owner, new[] { bill }, Drift).Ok);
        Assert.Equal(Wrong, Text(t, "SELECT date FROM payments;"));
    }
    [Fact]
    public void AuthorizedTimeCorrectionKeepsCreatedAtAndAllMoneySnapshots()
    {
        using var t = new TestDatabase(); var bill = Bill(t);
        Execute(t, "UPDATE users SET name='Current owner' WHERE id=1;");
        var done = Fix(t).ApplyCorrection(TestData.Owner, new[] { bill }, Drift);
        Assert.True(done.Ok, done.Error); Assert.Equal(1, done.Value!.Fixed);
        Assert.Equal(Correct, Text(t, "SELECT date FROM sales;")); Assert.Equal(Correct, Text(t, "SELECT date FROM payments;"));
        Assert.Equal(Wrong, Text(t, "SELECT created_at FROM sales;"));
        Assert.Equal(30000L, Number(t, "SELECT total_paisa FROM sales;")); Assert.Equal(30000L, Number(t, "SELECT amount_paisa FROM payments;"));
        Assert.Equal("Current owner", Text(t, "SELECT user_name FROM audit_logs WHERE action='sale_time_fixed';"));
    }
    [Fact]
    public void SecondApplyOfSameOffsetIsIdempotentAndStillRequiresOwner()
    {
        using var t = new TestDatabase(); var bill = Bill(t); var fix = Fix(t);
        Assert.True(fix.ApplyCorrection(TestData.Owner, new[] { bill }, Drift).Ok);
        var repeated = fix.ApplyCorrection(TestData.Owner, new[] { bill }, Drift);
        Assert.True(repeated.Ok, repeated.Error); Assert.Equal(0, repeated.Value!.Fixed); Assert.Equal(1, repeated.Value.Skipped);
        Assert.Equal(Correct, Text(t, "SELECT date FROM sales;"));
        Assert.Equal(1L, Number(t, "SELECT COUNT(*) FROM audit_logs WHERE action='sale_time_fixed';"));
        Execute(t, "UPDATE users SET active=0 WHERE id=1;"); Assert.False(fix.ApplyCorrection(TestData.Owner, new[] { bill }, Drift).Ok);
    }
    [Fact]
    public void BadPaymentDateRollsBackEarlierBillAndItsAuditInSameTransaction()
    {
        using var t = new TestDatabase(); var first = Bill(t, "INV-GOOD"); var second = Bill(t, "INV-BAD", "not-a-date");
        var done = Fix(t).ApplyCorrection(TestData.Owner, new[] { first, second }, Drift);
        Assert.False(done.Ok); Assert.Equal(2L, Number(t, "SELECT COUNT(*) FROM sales WHERE date='2026-11-01 10:00:00';"));
        Assert.Equal(0L, Number(t, "SELECT COUNT(*) FROM audit_logs WHERE action='sale_time_fixed';"));
        Assert.Equal(Wrong, Text(t, "SELECT date FROM payments ORDER BY id LIMIT 1;"));
    }
    [Fact]
    public void TimeAuditFailureRollsBackBillPaymentAndOffset()
    {
        using var t = new TestDatabase(); var bill = Bill(t);
        Execute(t, @"CREATE TRIGGER fail_time_audit BEFORE INSERT ON audit_logs
WHEN NEW.action='sale_time_fixed' BEGIN SELECT RAISE(ABORT,'test audit fault'); END;");
        Assert.False(Fix(t).ApplyCorrection(TestData.Owner, new[] { bill }, Drift).Ok);
        Assert.Equal(Wrong, Text(t, "SELECT date FROM sales;")); Assert.Equal(Wrong, Text(t, "SELECT date FROM payments;"));
        Assert.Equal(0L, Number(t, "SELECT time_offset_seconds FROM sales;"));
    }
    [Fact]
    public void TimeApplyRereadsCurrentBillRatherThanReusingPreviewValues()
    {
        using var t = new TestDatabase(); var bill = Bill(t); var fix = Fix(t);
        var preview = fix.ListBills(TestData.Owner, "2026-11-01", "2026-11-10", Drift);
        Assert.Equal(Correct, Assert.Single(preview.Value!).ProposedDate);
        Execute(t, "UPDATE sales SET date='2026-11-02 12:00:00',time_offset_seconds=86400;");
        var done = fix.ApplyCorrection(TestData.Owner, new[] { bill }, Drift);
        Assert.True(done.Ok, done.Error); Assert.Equal("2026-11-03 12:00:00", Text(t, "SELECT date FROM sales;"));
        Assert.Equal("2026-11-02 10:00:00", Text(t, "SELECT date FROM payments;"));
    }
}
