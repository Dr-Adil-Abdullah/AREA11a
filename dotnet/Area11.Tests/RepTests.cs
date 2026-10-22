using Area11.Core.Models;
using Area11.Core.Services;
using Xunit;

namespace Area11.Tests;

/// <summary>
/// Medical reps, the doctor on the bill and lifetime profit (F-44, Spec §8.3.3, §10.3.2, §10.4.1,
/// 17.5, 17.7).
///
/// The promises this file pins down:
///   1. a company carries one rep - a name, a phone and a visit day that sorts Monday to Sunday;
///   2. a rep is cleared, never deleted: the black box keeps the old line (D-00b);
///   3. a bill may name a doctor **only** when that customer's category is Doctor;
///   4. a doctor's profit is the report's formula - sales minus cost, minus refunds, plus the cost
///      of the goods that came back to the shelf;
///   5. a held bill is not a sale and earns nobody anything;
///   6. a manager sees the doctors but never the profit.
/// </summary>
public sealed class RepTests
{
    // ============================== the reps ==============================

    [Fact]
    public void ARepIsSavedWithHisVisitDay()
    {
        using var test = new TestDatabase();
        var reps = Reps(test);
        var company = AddCompany(test, "GSK");

        var saved = reps.SaveRep(TestData.Owner, company, "Imran", "0300-1234567", 2);

        Assert.True(saved.Ok, saved.Error);

        var rows = reps.ListReps(TestData.Owner);

        Assert.True(rows.Ok, rows.Error);
        var row = rows.Value!.Single(item => item.Id == company);

        Assert.Equal("Imran", row.RepName);
        Assert.Equal("0300-1234567", row.RepPhone);
        Assert.Equal("Tuesday", row.VisitLabel);
        Assert.True(row.HasRep);
        Assert.Equal(1L, Rows(test, "SELECT COUNT(1) FROM audit_logs WHERE action = 'company_rep_update';"));
    }

    [Fact]
    public void AnEmptyNameClearsTheRepButKeepsTheOldLine()
    {
        using var test = new TestDatabase();
        var reps = Reps(test);
        var company = AddCompany(test, "GSK");

        Assert.True(reps.SaveRep(TestData.Owner, company, "Imran", "0300-1234567", 2).Ok);
        Assert.True(reps.SaveRep(TestData.Owner, company, "  ", null, null).Ok);

        var row = reps.ListReps(TestData.Owner).Value!.Single(item => item.Id == company);

        Assert.False(row.HasRep);
        Assert.Null(row.RepPhone);
        Assert.Null(row.VisitDay);
        Assert.Equal("no rep yet", row.RepLabel);

        // Two writes, two permanent lines - the old rep is still explainable.
        Assert.Equal(2L, Rows(test, "SELECT COUNT(1) FROM audit_logs WHERE action = 'company_rep_update';"));

        using var connection = test.Database.Open();
        var oldValue = test.Database.Scalar(connection,
            "SELECT old_value FROM audit_logs WHERE action = 'company_rep_update' ORDER BY id LIMIT 1;");

        Assert.Contains("Imran", oldValue?.ToString());
    }

    [Fact]
    public void TheVisitDayIsMondayToSundayOrNothing()
    {
        using var test = new TestDatabase();
        var reps = Reps(test);
        var company = AddCompany(test, "GSK");

        Assert.False(reps.SaveRep(TestData.Owner, company, "Imran", null, 8).Ok);
        Assert.False(reps.SaveRep(TestData.Owner, company, "Imran", null, -1).Ok);

        // 0 means "no fixed day" - the same word the drop-down's first row carries.
        Assert.True(reps.SaveRep(TestData.Owner, company, "Imran", null, 0).Ok);

        var row = reps.ListReps(TestData.Owner).Value!.Single(item => item.Id == company);

        Assert.Null(row.VisitDay);
        Assert.Equal("no fixed day", row.VisitLabel);
    }

    [Fact]
    public void APhoneNumberHoldsNoLetters()
    {
        using var test = new TestDatabase();
        var reps = Reps(test);
        var company = AddCompany(test, "GSK");

        var refused = reps.SaveRep(TestData.Owner, company, "Imran", "call me", 1);

        Assert.False(refused.Ok);
        Assert.Contains("digits", refused.Error);
        Assert.Equal(0L, Rows(test, "SELECT COUNT(1) FROM audit_logs WHERE action = 'company_rep_update';"));
    }

    [Fact]
    public void APhoneOrADayNeedsAName()
    {
        using var test = new TestDatabase();
        var reps = Reps(test);
        var company = AddCompany(test, "GSK");

        var refused = reps.SaveRep(TestData.Owner, company, "", "0300-1234567", 3);

        Assert.False(refused.Ok);
        Assert.Contains("name first", refused.Error);
    }

    [Fact]
    public void ACompanyThatIsGoneCannotTakeARep()
    {
        using var test = new TestDatabase();
        var reps = Reps(test);

        var refused = reps.SaveRep(TestData.Owner, 9999, "Imran", null, 1);

        Assert.False(refused.Ok);
        Assert.Contains("company", refused.Error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void TheCashierCannotWriteARep()
    {
        using var test = new TestDatabase();
        var reps = Reps(test);
        var company = AddCompany(test, "GSK");
        var cashier = TestData.Cashier(test);

        Assert.False(reps.SaveRep(cashier, company, "Imran", null, 1).Ok);
        Assert.False(reps.ListReps(cashier).Ok);
        Assert.True(reps.SaveRep(TestData.Owner, company, "Imran", null, 1).Ok);
    }

    // ============================== the doctor on the bill ==============================

    [Fact]
    public void ABillCanNameADoctor()
    {
        using var test = new TestDatabase();
        var sales = TestData.Sales(test);
        var product = TestData.AddProduct(test);
        var doctor = TestData.AddCustomer(test, name: "Dr. Ahmed", category: "doctor");

        var sale = sales.Create(TestData.Cart(product, qty: 2, doctorId: doctor), TestData.Owner);

        Assert.True(sale.Ok, sale.Error);
        Assert.Equal(doctor, (int)Rows(test, "SELECT doctor_id FROM sales;"));
    }

    [Fact]
    public void ANormalCustomerIsNotADoctor()
    {
        using var test = new TestDatabase();
        var sales = TestData.Sales(test);
        var product = TestData.AddProduct(test);
        var customer = TestData.AddCustomer(test, name: "Ali Raza");

        var refused = sales.Create(TestData.Cart(product, doctorId: customer), TestData.Owner);

        Assert.False(refused.Ok);
        Assert.Contains("doctor", refused.Error, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(0L, Rows(test, "SELECT COUNT(1) FROM sales;"));
    }

    [Fact]
    public void AnUnknownDoctorIsRefused()
    {
        using var test = new TestDatabase();
        var sales = TestData.Sales(test);
        var product = TestData.AddProduct(test);

        Assert.False(sales.Create(TestData.Cart(product, doctorId: 4242), TestData.Owner).Ok);
        Assert.Equal(0L, Rows(test, "SELECT COUNT(1) FROM sales;"));
    }

    // ============================== what a doctor earns ==============================

    [Fact]
    public void TheDoctorReportCountsPrescriptionsAndProfit()
    {
        using var test = new TestDatabase();
        var sales = TestData.Sales(test);
        var reps = Reps(test);
        var product = TestData.AddProduct(test, costPaisa: 20000, retailPaisa: 30000);
        var doctor = TestData.AddCustomer(test, name: "Dr. Ahmed", category: "doctor");

        Assert.True(sales.Create(TestData.Cart(product, qty: 2, doctorId: doctor), TestData.Owner).Ok);

        var row = reps.ListDoctors(TestData.Owner).Value!.Single(item => item.DoctorId == doctor);

        Assert.Equal(1, row.Bills);
        Assert.Equal(60000L, row.SalesPaisa);
        Assert.Equal(20000L, row.ProfitPaisa);      // 60000 sold - 40000 cost
        Assert.Equal(10, row.LastBillLabel.Length); // 2026-10-22
        Assert.True(row.ProfitKnown);
    }

    [Fact]
    public void AReturnedStripLowersTheDoctorsProfit()
    {
        using var test = new TestDatabase();
        var sales = TestData.Sales(test);
        var returns = TestData.Returns(test);
        var reps = Reps(test);
        var product = TestData.AddProduct(test, costPaisa: 20000, retailPaisa: 30000);
        var doctor = TestData.AddCustomer(test, name: "Dr. Ahmed", category: "doctor");

        var sale = sales.Create(TestData.Cart(product, qty: 4, doctorId: doctor), TestData.Owner);
        Assert.True(sale.Ok, sale.Error);
        Assert.True(returns.Create(TestData.Return(sale.Value!, 1), TestData.Owner).Ok);

        var row = reps.ListDoctors(TestData.Owner).Value!.Single(item => item.DoctorId == doctor);

        // 120000 sold - 80000 cost - 30000 refunded + 20000 back on the shelf.
        Assert.Equal(30000L, row.ProfitPaisa);
    }

    [Fact]
    public void AHeldBillIsNotADoctorsSale()
    {
        using var test = new TestDatabase();
        var reps = Reps(test);
        var product = TestData.AddProduct(test);
        var doctor = TestData.AddCustomer(test, name: "Dr. Ahmed", category: "doctor");

        // A parked cart (#hold) is not a sale; the doctor report must not count it.
        test.Database.Tx(connection =>
        {
            test.Database.Execute(connection, @"
INSERT INTO sales(code, date, doctor_id, total_paisa, status, created_at)
VALUES('INV-HOLD-1', '2026-10-22 11:00:00', @doctor, 50000, 'hold', '2026-10-22 11:00:00');",
                new { doctor });

            var saleId = (int)test.Database.NextId(connection);

            test.Database.Execute(connection, @"
INSERT INTO sale_items(sale_id, product_id, qty_base, unit_sold, qty_entered, unit_price_paisa,
                       line_total_paisa, cost_paisa_at_sale)
VALUES(@saleId, @product, 1, 'base', 1, 50000, 50000, 20000);",
                new { saleId, product });

            return saleId;
        });

        var row = reps.ListDoctors(TestData.Owner).Value!.Single(item => item.DoctorId == doctor);

        Assert.Equal(0, row.Bills);
        Assert.Equal(0L, row.SalesPaisa);
        Assert.Equal(0L, row.ProfitPaisa);
    }

    [Fact]
    public void TheDoctorListHoldsOnlyDoctors()
    {
        using var test = new TestDatabase();
        var reps = Reps(test);
        var doctor = TestData.AddCustomer(test, name: "Dr. Ahmed", category: "doctor");
        TestData.AddCustomer(test, name: "Ali Raza");

        var rows = reps.ListDoctors(TestData.Owner);

        Assert.True(rows.Ok, rows.Error);
        Assert.Single(rows.Value!);
        Assert.Equal(doctor, rows.Value![0].DoctorId);
        Assert.Equal("Dr. Ahmed", rows.Value[0].Name);
    }

    // ============================== lifetime profit ==============================

    [Fact]
    public void EachCustomerCarriesHisOwnLifetimeProfit()
    {
        using var test = new TestDatabase();
        var sales = TestData.Sales(test);
        var reps = Reps(test);
        var product = TestData.AddProduct(test, costPaisa: 20000, retailPaisa: 30000);
        var first = TestData.AddCustomer(test, name: "Ali Raza");
        var second = TestData.AddCustomer(test, name: "Sara Khan");

        Assert.True(sales.Create(TestData.Cart(product, qty: 1, customerId: first), TestData.Owner).Ok);
        Assert.True(sales.Create(TestData.Cart(product, qty: 1, customerId: first), TestData.Owner).Ok);
        Assert.True(sales.Create(TestData.Cart(product, qty: 3, customerId: second), TestData.Owner).Ok);

        var rows = reps.ListCustomerProfit(TestData.Owner);

        Assert.True(rows.Ok, rows.Error);

        var one = rows.Value!.Single(item => item.CustomerId == first);
        var two = rows.Value!.Single(item => item.CustomerId == second);

        Assert.Equal(2, one.Bills);
        Assert.Equal(60000L, one.SalesPaisa);
        Assert.Equal(20000L, one.ProfitPaisa);
        Assert.Equal(1, two.Bills);
        Assert.Equal(90000L, two.SalesPaisa);
        Assert.Equal(30000L, two.ProfitPaisa);
    }

    [Fact]
    public void AManagerSeesTheDoctorsButNeverTheirProfit()
    {
        using var test = new TestDatabase();
        var sales = TestData.Sales(test);
        var reps = Reps(test);
        var product = TestData.AddProduct(test);
        var doctor = TestData.AddCustomer(test, name: "Dr. Ahmed", category: "doctor");
        var manager = TestData.AddUser(test, "manager");

        Assert.True(sales.Create(TestData.Cart(product, qty: 2, doctorId: doctor), TestData.Owner).Ok);

        var doctors = reps.ListDoctors(manager);

        Assert.True(doctors.Ok, doctors.Error);
        var row = doctors.Value!.Single(item => item.DoctorId == doctor);

        Assert.Equal(1, row.Bills);
        Assert.False(row.ProfitKnown);
        Assert.Equal("—", row.ProfitText);

        // Lifetime profit is the owner's number only.
        Assert.False(reps.ListCustomerProfit(manager).Ok);
        Assert.True(reps.ListCustomerProfit(TestData.Owner).Ok);
    }

    // ============================== helpers ==============================

    private static RepService Reps(TestDatabase test) =>
        new(test.Database, test.Audit, TestData.Permissions(test), test.Log);

    private static int AddCompany(TestDatabase test, string name)
    {
        var products = new ProductService(test.Database, test.Audit, test.Log);
        var created = products.AddCompany(TestData.Owner, name);

        Assert.True(created.Ok, created.Error);
        return created.Value;
    }

    private static long Rows(TestDatabase test, string sql)
    {
        using var connection = test.Database.Open();

        return Convert.ToInt64(test.Database.Scalar(connection, sql) ?? 0);
    }
}
