using System.Globalization;
using Area11.Core.Data;
using Area11.Core.Models;
using Area11.Core.Utils;
using Microsoft.Data.Sqlite;

namespace Area11.Core.Services;

/// <summary>
/// The sample shop (F-53, D-69): one button fills every page of a new Area11 with a believable day's
/// work - medicines on the shelves, four purchases, twelve bills over the last three weeks, khata,
/// returns, an expense, a drawing, a closed drawer, a stock-take, a supplier return, a no-bill return,
/// two own fields, a held cart, and a handful of alerts that the shop would really see.
///
/// Why this exists: a new shop is an empty program. Every screen says "nothing here", and the owner
/// cannot tell a page that works from a page that has nothing to show. Sample data turns the whole app
/// into something he can walk through - and give to a new cashier to practise on.
///
/// Why it is so careful about *where* it runs: this is the only action that writes into every table at
/// once. It refuses a database that already has bills (a real shop must never receive demo money), and
/// it writes in ONE transaction, so a half-filled shop can never exist. Nothing here deletes a row.
/// </summary>
public sealed class SampleShopService
{
    /// <summary>The one black-box line this action leaves.</summary>
    public const string AuditAction = "sample_shop";

    /// <summary>A shop that already carries this many medicines is a real shop, not a demo.</summary>
    public const int RealShopProducts = 30;

    private readonly Database _db;
    private readonly SettingsService _settings;
    private readonly AuditService _audit;
    private readonly PermissionService _permissions;
    private readonly LogService _log;

    public SampleShopService(
        Database db,
        SettingsService settings,
        AuditService audit,
        PermissionService permissions,
        LogService log)
    {
        _db = db;
        _settings = settings;
        _audit = audit;
        _permissions = permissions;
        _log = log;
    }

    /// <summary>Owner only: demo money must never appear in a shop by a cashier's hand.</summary>
    public bool CanSeed(User? user) =>
        user is { IsOwner: true } && _permissions.Can(user, Modules.Settings);

    // ============================== what is on the shelves ==============================

    private sealed class ProductSeed
    {
        public string Name { get; init; } = string.Empty;

        public string Generic { get; init; } = string.Empty;

        public string Brand { get; init; } = string.Empty;

        public string Category { get; init; } = string.Empty;

        public string Company { get; init; } = string.Empty;

        public string Unit { get; init; } = "tablet";

        public string Pack { get; init; } = "10 tablets";

        public string Rack { get; init; } = "A-1";

        public string Barcode { get; init; } = string.Empty;

        public int BoxStrips { get; init; } = 10;

        public int StripTablets { get; init; } = 10;

        public int Reorder { get; init; } = 20;

        public long Cost { get; init; }

        public long Retail { get; init; }

        public long Vip { get; init; }

        public long Doctor { get; init; }

        /// <summary>Batch number, days from today the batch expires, and how many base units came in.</summary>
        public (string Batch, int ExpiresInDays, double Qty)[] Batches { get; init; } = Array.Empty<(string, int, double)>();
    }

    /// <summary>Twenty medicines and items across all seven categories - the shelf a pharmacy really has.</summary>
    private static readonly ProductSeed[] Products =
    {
        new()
        {
            Name = "Panadol 500mg", Generic = "Paracetamol", Brand = "GSK", Category = "Tablets",
            Company = "GSK Pakistan", Unit = "tablet", Pack = "10 tablets", Rack = "A-1",
            Barcode = "8964000111012", Cost = 1800, Retail = 2500, Vip = 2400, Doctor = 2300, Reorder = 200,
            Batches = new[] { ("B-PAN-01", 420, 800.0), ("B-PAN-02", 200, 400.0) }
        },
        new()
        {
            Name = "Panadol Extra", Generic = "Paracetamol + Caffeine", Brand = "GSK", Category = "Tablets",
            Company = "GSK Pakistan", Unit = "tablet", Pack = "10 tablets", Rack = "A-1",
            Barcode = "8964000111029", Cost = 2200, Retail = 3000, Vip = 2900, Doctor = 2800, Reorder = 100,
            Batches = new[] { ("B-PEX-01", 380, 500.0) }
        },
        new()
        {
            Name = "Augmentin 625mg", Generic = "Amoxicillin + Clavulanate", Brand = "GSK", Category = "Tablets",
            Company = "GSK Pakistan", Unit = "tablet", Pack = "6 tablets", Rack = "A-2",
            Barcode = "8964000111036", Cost = 4200, Retail = 4800, Vip = 4600, Doctor = 4500, Reorder = 30,
            Batches = new[] { ("B-AUG-01", 150, 60.0) }
        },
        new()
        {
            Name = "Risek 20mg", Generic = "Omeprazole", Brand = "Getz", Category = "Tablets",
            Company = "Getz Pharma", Unit = "capsule", Pack = "14 capsules", Rack = "A-3",
            Barcode = "8964000111043", Cost = 1500, Retail = 2000, Vip = 1900, Doctor = 1850, Reorder = 60,
            Batches = new[] { ("B-RIS-01", 300, 200.0) }
        },
        new()
        {
            Name = "Motilium 10mg", Generic = "Domperidone", Brand = "Hilton", Category = "Tablets",
            Company = "Hilton Pharma", Unit = "tablet", Pack = "10 tablets", Rack = "A-3",
            Barcode = "8964000111050", Cost = 1200, Retail = 1700, Vip = 1600, Doctor = 1550, Reorder = 50,
            Batches = new[] { ("B-MOT-01", 260, 150.0) }
        },
        new()
        {
            Name = "Surbex Z", Generic = "Multivitamin + Zinc", Brand = "Abbott", Category = "Tablets",
            Company = "Abbott Pakistan", Unit = "tablet", Pack = "30 tablets", Rack = "A-4",
            Barcode = "8964000111067", Cost = 1600, Retail = 2200, Vip = 2100, Doctor = 2000, Reorder = 100,
            Batches = new[] { ("B-SUR-01", 500, 300.0) }
        },
        new()
        {
            Name = "Disprin", Generic = "Aspirin", Brand = "Searle", Category = "Tablets",
            Company = "Searle Pakistan", Unit = "tablet", Pack = "20 tablets", Rack = "A-4",
            Barcode = "8964000111074", Cost = 900, Retail = 1300, Vip = 1250, Doctor = 1200, Reorder = 40,
            Batches = new[] { ("B-DIS-01", 340, 40.0) }
        },
        new()
        {
            Name = "Calpol 120ml", Generic = "Paracetamol syrup", Brand = "GSK", Category = "Syrups",
            Company = "GSK Pakistan", Unit = "bottle", Pack = "120 ml", Rack = "B-1",
            Barcode = "8964000111081", Cost = 11000, Retail = 14000, Vip = 13500, Doctor = 13000, BoxStrips = 1, StripTablets = 1, Reorder = 12,
            Batches = new[] { ("B-CAL-01", 240, 40.0), ("B-CAL-02", 60, 8.0) }
        },
        new()
        {
            Name = "Brufen 100ml", Generic = "Ibuprofen syrup", Brand = "Abbott", Category = "Syrups",
            Company = "Abbott Pakistan", Unit = "bottle", Pack = "100 ml", Rack = "B-1",
            Barcode = "8964000111098", Cost = 9500, Retail = 12500, Vip = 12000, Doctor = 11500, BoxStrips = 1, StripTablets = 1, Reorder = 10,
            Batches = new[] { ("B-BRU-01", 210, 25.0) }
        },
        new()
        {
            Name = "Hydryllin Syrup", Generic = "Diphenhydramine", Brand = "Searle", Category = "Syrups",
            Company = "Searle Pakistan", Unit = "bottle", Pack = "120 ml", Rack = "B-2",
            Barcode = "8964000111104", Cost = 8500, Retail = 11000, Vip = 10500, Doctor = 10000, BoxStrips = 1, StripTablets = 1, Reorder = 8,
            Batches = new[] { ("B-HYD-01", -20, 6.0), ("B-HYD-02", 45, 10.0) }
        },
        new()
        {
            Name = "Ventolin Inhaler", Generic = "Salbutamol", Brand = "GSK", Category = "Syrups",
            Company = "GSK Pakistan", Unit = "piece", Pack = "1 inhaler", Rack = "B-3",
            Barcode = "8964000111111", Cost = 48000, Retail = 56000, Vip = 54000, Doctor = 53000, BoxStrips = 1, StripTablets = 1, Reorder = 4,
            Batches = new[] { ("B-VEN-01", 400, 12.0) }
        },
        new()
        {
            Name = "Optalgin Injection", Generic = "Metamizole", Brand = "Getz", Category = "Injections",
            Company = "Getz Pharma", Unit = "ampoule", Pack = "5 ampoules", Rack = "C-1",
            Barcode = "8964000111128", Cost = 6000, Retail = 8000, Vip = 7700, Doctor = 7500, BoxStrips = 5, StripTablets = 1, Reorder = 10,
            Batches = new[] { ("B-OPT-01", 300, 30.0) }
        },
        new()
        {
            Name = "Rocephin 1g Injection", Generic = "Ceftriaxone", Brand = "Sami", Category = "Injections",
            Company = "Sami Pharma", Unit = "vial", Pack = "1 vial", Rack = "C-1",
            Barcode = "8964000111135", Cost = 38000, Retail = 45000, Vip = 43500, Doctor = 42500, BoxStrips = 1, StripTablets = 1, Reorder = 6,
            Batches = new[] { ("B-ROC-01", 330, 14.0) }
        },
        new()
        {
            Name = "ORS Sachet", Generic = "Oral rehydration salts", Brand = "Sami", Category = "Tablets",
            Company = "Sami Pharma", Unit = "sachet", Pack = "1 sachet", Rack = "A-5",
            Barcode = "8964000111142", Cost = 1500, Retail = 2000, Vip = 1900, Doctor = 1800, BoxStrips = 1, StripTablets = 1, Reorder = 50,
            Batches = new[] { ("B-ORS-01", 540, 200.0) }
        },
        new()
        {
            Name = "Dettol 250ml", Generic = "Antiseptic liquid", Brand = "Reckitt", Category = "Skin care",
            Company = "Reckitt Pakistan", Unit = "bottle", Pack = "250 ml", Rack = "D-1",
            Barcode = "8964000111159", Cost = 28000, Retail = 34000, Vip = 33000, Doctor = 32000, BoxStrips = 1, StripTablets = 1, Reorder = 6,
            Batches = new[] { ("B-DET-01", 600, 20.0) }
        },
        new()
        {
            Name = "Johnson's Baby Powder 200g", Generic = "Talcum powder", Brand = "Johnson's",
            Category = "Baby care", Company = "Johnson & Johnson", Unit = "piece", Pack = "200 g", Rack = "D-2",
            Barcode = "8964000111166", Cost = 32000, Retail = 39000, Vip = 37500, Doctor = 36500, BoxStrips = 1, StripTablets = 1, Reorder = 5,
            Batches = new[] { ("B-JBP-01", 700, 15.0) }
        },
        new()
        {
            Name = "Pampers Small 20s", Generic = "Baby diapers", Brand = "Pampers", Category = "Baby care",
            Company = "Procter & Gamble", Unit = "pack", Pack = "20 diapers", Rack = "D-2",
            Barcode = "8964000111173", Cost = 65000, Retail = 78000, Vip = 75000, Doctor = 74000, BoxStrips = 1, StripTablets = 1, Reorder = 4,
            Batches = new[] { ("B-PAM-01", 800, 10.0) }
        },
        new()
        {
            Name = "Bandage Roll 4\"", Generic = "Cotton bandage", Brand = "Hartmann", Category = "Surgical",
            Company = "Hartmann Pakistan", Unit = "roll", Pack = "1 roll", Rack = "E-1",
            Barcode = "8964000111180", Cost = 6000, Retail = 8000, Vip = 7500, Doctor = 7000, BoxStrips = 1, StripTablets = 1, Reorder = 10,
            Batches = new[] { ("B-BAN-01", 900, 40.0) }
        },
        new()
        {
            Name = "Surgical Gloves (pair)", Generic = "Latex gloves", Brand = "Safe Care", Category = "Surgical",
            Company = "Safe Care", Unit = "pair", Pack = "1 pair", Rack = "E-1",
            Barcode = "8964000111197", Cost = 2500, Retail = 3500, Vip = 3300, Doctor = 3200, BoxStrips = 1, StripTablets = 1, Reorder = 20,
            Batches = new[] { ("B-GLV-01", 900, 60.0) }
        },
        new()
        {
            Name = "Syringe 5ml", Generic = "Disposable syringe", Brand = "Becton", Category = "Surgical",
            Company = "Becton Dickinson", Unit = "piece", Pack = "1 syringe", Rack = "E-2",
            Barcode = "8964000111203", Cost = 1200, Retail = 1800, Vip = 1700, Doctor = 1600, BoxStrips = 1, StripTablets = 1, Reorder = 50,
            // Bought twice and sold out: the counter shows a real "out of stock" medicine, and the
            // reorder pad has something to ask the supplier for.
            Batches = new[] { ("B-SYR-01", 700, 30.0), ("B-SYR-02", 620, 20.0) }
        }
    };

    private static readonly string[] CategoryParents = { "Medicines" };

    private static readonly (string Name, string Parent)[] CategoryChildren =
    {
        ("Tablets", "Medicines"),
        ("Syrups", "Medicines"),
        ("Injections", "Medicines"),
        ("Baby care", string.Empty),
        ("Skin care", string.Empty),
        ("Surgical", string.Empty)
    };

    private static readonly (string Name, string Phone, string Rep, string RepPhone, string Visit)[] Companies =
    {
        ("GSK Pakistan", "042-111-475-725", "Asif Mehmood", "0300-1234567", "Tuesday"),
        ("Getz Pharma", "021-111-111-321", "Bilal Ahmed", "0321-4567890", "Wednesday"),
        ("Hilton Pharma", "021-111-445-866", "Naveed Iqbal", "0333-2223344", "Monday"),
        ("Abbott Pakistan", "021-111-222-666", "Kamran Shah", "0300-7778899", "Friday"),
        ("Searle Pakistan", "021-111-732-753", "Faisal Raza", "0345-1112233", "Thursday"),
        ("Sami Pharma", "021-111-726-427", "Waqas Ali", "0311-4445566", "Saturday"),
        ("Reckitt Pakistan", "021-111-732-548", "Sohail Anwar", "0301-8887766", "Monday"),
        ("Johnson & Johnson", "021-111-562-562", "Rimsha Khan", "0332-3332211", "Tuesday"),
        ("Procter & Gamble", "021-111-742-742", "Adnan Sheikh", "0300-9998877", "Wednesday"),
        ("Hartmann Pakistan", "042-351-7788", "Tariq Jameel", "0302-1116655", "Friday"),
        ("Safe Care", "042-355-1122", "Usman Ghani", "0306-5544332", "Thursday"),
        ("Becton Dickinson", "021-111-232-866", "Hina Aslam", "0344-9988776", "Saturday")
    };

    private static readonly (string Name, string Agency, string Phone, string Address)[] Suppliers =
    {
        ("Al-Noor Distributors", "GSK", "042-37234567", "Hall Road, Lahore"),
        ("Medina Traders", "Getz, Hilton", "042-37654321", "Shah Alam Market, Lahore"),
        ("Shalimar Pharma Supply", "Abbott, Searle", "042-35778899", "Bund Road, Lahore"),
        ("City Surgical House", "Surgical items", "042-35112233", "Akbari Mandi, Lahore")
    };

    private static readonly (string Name, string Phone, string Category, long LimitPaisa, long OpeningPaisa)[] Customers =
    {
        ("Ali Raza", "0300-1234567", "normal", 500000, 150000),
        ("Fatima Bibi", "0321-9876543", "normal", 200000, 0),
        ("Zubair Khan", "0333-5551234", "normal", 300000, 0),
        ("Dr. Sana Malik", "0300-5556677", "doctor", 1000000, 0),
        ("Ayesha Siddiqui", "0345-2223344", "normal", 150000, 45000),
        ("Hameed & Sons Clinic", "042-35566778", "vip", 2000000, 0),
        ("Bilal Traders", "0301-7776655", "normal", 400000, 0),
        ("Nasir Ali", "0311-3334455", "normal", 100000, 0)
    };

    private static readonly (string Label, string Type, string Entity)[] CustomFields =
    {
        ("Fridge item?", "check", "product"),
        ("CNIC", "text", "customer")
    };

    /// <summary>One bill: how long ago, how it was paid, which customer, and the lines it carries.</summary>
    private sealed class BillSeed
    {
        public int DaysAgo { get; init; }

        public string Method { get; init; } = "cash";

        public int Customer { get; init; } = -1;

        public bool PartPaid { get; init; }

        public int Hour { get; init; } = 12;

        public (int Product, int Batch, double Qty)[] Lines { get; init; } = Array.Empty<(int, int, double)>();
    }

    /// <summary>Twelve bills over three weeks: every payment method, one khata that is partly paid.</summary>
    private static readonly BillSeed[] Bills =
    {
        new() { DaysAgo = 18, Method = "cash", Hour = 11, Lines = new[] { (0, 0, 40.0), (13, 0, 4.0) } },
        new() { DaysAgo = 15, Method = "cash", Hour = 17, Lines = new[] { (3, 0, 20.0), (5, 0, 30.0) } },
        new() { DaysAgo = 12, Method = "online", Hour = 13, Lines = new[] { (7, 0, 2.0), (4, 0, 10.0) } },
        new() { DaysAgo = 10, Method = "credit", Customer = 0, Hour = 19, Lines = new[] { (2, 0, 12.0), (0, 0, 20.0) } },
        new() { DaysAgo = 8, Method = "card", Hour = 16, Lines = new[] { (10, 0, 1.0), (16, 0, 2.0) } },
        new() { DaysAgo = 7, Method = "cash", Hour = 10, Lines = new[] { (8, 0, 1.0), (6, 0, 10.0) } },
        new() { DaysAgo = 5, Method = "split", Hour = 18, Lines = new[] { (12, 0, 1.0), (17, 0, 5.0) } },
        new() { DaysAgo = 4, Method = "credit", Customer = 4, PartPaid = true, Hour = 12,
            Lines = new[] { (1, 0, 20.0), (3, 0, 14.0), (13, 0, 2.0) } },
        new() { DaysAgo = 3, Method = "cash", Hour = 9, Lines = new[] { (11, 0, 2.0), (14, 0, 1.0) } },
        new() { DaysAgo = 2, Method = "cash", Hour = 20, Lines = new[] { (19, 0, 30.0), (19, 1, 20.0) } },
        new() { DaysAgo = 1, Method = "online", Hour = 15, Lines = new[] { (9, 1, 1.0), (5, 0, 20.0) } },
        new() { DaysAgo = 0, Method = "cash", Hour = 10, Lines = new[] { (0, 0, 10.0), (13, 0, 2.0), (4, 0, 10.0) } }
    };

    // ============================== what the owner is shown ==============================

    public ServiceResult<SampleShopReport> Preview(User? user)
    {
        if (!CanSeed(user))
        {
            return ServiceResult<SampleShopReport>.Fail(
                "Only the owner may fill the shop with sample data.");
        }

        try
        {
            return ServiceResult<SampleShopReport>.Success(Describe());
        }
        catch (Exception exception)
        {
            _log.Error("The sample shop could not be described.", exception);
            return ServiceResult<SampleShopReport>.Fail(
                "The sample shop could not be prepared. " + exception.Message);
        }
    }

    /// <summary>
    /// Writes the whole sample shop in ONE transaction: every table, or none of it.
    /// </summary>
    public ServiceResult<SampleShopReport> Seed(User? user)
    {
        if (!CanSeed(user))
        {
            return ServiceResult<SampleShopReport>.Fail(
                "Only the owner may fill the shop with sample data.");
        }

        SampleShopReport report;

        try
        {
            report = Describe();
        }
        catch (Exception exception)
        {
            _log.Error("The sample shop could not be described before writing.", exception);
            return ServiceResult<SampleShopReport>.Fail(
                "The sample shop could not be prepared, so nothing was written. " + exception.Message);
        }

        if (!report.CanSeed)
        {
            return ServiceResult<SampleShopReport>.Fail(report.ProblemText);
        }

        try
        {
            _db.Tx(connection =>
            {
                Write(connection, user!);

                _audit.Record(connection, user, "System", AuditAction, "shop", null,
                    oldValue: null,
                    newValue: null,
                    details: "Filled the shop with sample data: " + Products.Length + " medicines, "
                             + Bills.Length + " bills, 2 returns, 4 purchases, khata, expenses, a closed drawer, "
                             + "a stock-take, a supplier return and a no-bill return. No real data existed.");
            });

            // The document counters live in the settings cache, and the sample moved them forward.
            _settings.Reload();

            return ServiceResult<SampleShopReport>.Success(report);
        }
        catch (Exception exception)
        {
            _log.Error("The sample shop could not be written.", exception);

            return ServiceResult<SampleShopReport>.Fail(
                "The sample shop could not be written, so nothing was added: " + exception.Message);
        }
    }

    private SampleShopReport Describe()
    {
        var report = new SampleShopReport();

        using var connection = _db.Open();

        var sales = Convert.ToInt64(_db.Scalar(connection, "SELECT COUNT(1) FROM sales;") ?? 0L);
        var purchases = Convert.ToInt64(_db.Scalar(connection, "SELECT COUNT(1) FROM purchases;") ?? 0L);
        var products = Convert.ToInt64(_db.Scalar(connection, "SELECT COUNT(1) FROM products;") ?? 0L);
        var samples = Convert.ToInt64(_db.Scalar(connection,
            "SELECT COUNT(1) FROM audit_logs WHERE action = @action;", new { action = AuditAction }) ?? 0L);

        if (samples > 0)
        {
            report.Problems.Add("The sample shop was already added to this database, and it is never added twice.");
        }

        if (sales > 0 || purchases > 0)
        {
            report.Problems.Add(
                "This shop already has " + sales + " bills and " + purchases + " purchases - real money. "
                + "Sample data is only for a shop with no bills at all, so that nothing real can be mixed with demo data.");
        }

        if (products >= RealShopProducts)
        {
            report.Problems.Add(
                "There are already " + products + " medicines in this shop. That is a real shop, not a new one.");
        }

        report.Steps.Add(Products.Length + " medicines across 7 categories, with one or two batches each - "
                         + "healthy, near expiry, one expired and one sold out.");
        report.Steps.Add(Companies.Length + " companies with the medical rep's name, phone and visit day.");
        report.Steps.Add(Suppliers.Length + " suppliers with an agency and a balance to pay.");
        report.Steps.Add(Customers.Length + " customers, two of them carrying khata from before Area11.");
        report.Steps.Add("4 purchases (PINV), one of them unpaid, so the supplier page owes money.");
        report.Steps.Add(Bills.Length + " bills over the last three weeks: cash, online, card, split and khata.");
        report.Steps.Add("2 returns: one back to the shelf, one to quarantine with its own Q code.");
        report.Steps.Add("1 supplier return (SR) for goods near expiry, and 1 no-bill return (PR).");
        report.Steps.Add("3 expenses, 2 owner drawings and 1 closed shift with the drawer counted.");
        report.Steps.Add("1 applied stock-take (ST) that found a shortfall and an extra strip.");
        report.Steps.Add("2 own fields with answers, 1 held cart, and a strip code and a box code on Panadol.");
        report.Steps.Add("Alerts that a real shop would have: near expiry, expired, out of stock, khata due.");

        return report;
    }

    // ============================== writing it ==============================

    private void Write(SqliteConnection connection, User user)
    {
        var at = Clock.Now();
        var userId = user.Id;

        var categories = WriteCategories(connection, at);
        var companies = WriteCompanies(connection, at);
        var suppliers = WriteSuppliers(connection, at);
        var customers = WriteCustomers(connection, at);
        var products = WriteProducts(connection, at, categories, companies);

        var batches = WriteBatches(connection, at, products, suppliers);

        var purchases = WritePurchases(connection, at, userId, suppliers, products, batches);
        var sales = WriteSales(connection, at, userId, customers, products, batches);
        var returns = WriteReturns(connection, at, userId, products, batches, sales);

        WriteExpenses(connection, at, userId);
        WriteShift(connection, at, userId);
        WriteStockTake(connection, at, userId, products, batches);
        WriteSupplierReturn(connection, at, userId, products, batches, purchases);
        WriteProvisional(connection, at, userId, products, batches);
        WritePackBarcodes(connection, at, products);
        WriteCustomFields(connection, at, products, customers);
        WriteHeldCart(connection, at, userId, products);

        // Stock on the shelf IS the sum of the movements: bought + returned - sold - written off.
        // Doing it in one statement at the end is what keeps the sample shop's shelves and its
        // movement history in agreement, which is the property the tests pin.
        _db.Execute(connection, @"
UPDATE batches
SET qty_base = IFNULL((SELECT SUM(m.qty_base) FROM stock_movements m WHERE m.batch_id = batches.id), 0)
WHERE id > 0;");

        // What each khata customer still owes: what he carried in, plus what the bills left on the
        // khata, minus whatever a return took off it (a return never edits the bill itself).
        _db.Execute(connection, @"
UPDATE customers
SET balance_paisa = MAX(0, opening_balance_paisa
    + IFNULL((SELECT SUM(s.due_paisa) FROM sales s WHERE s.customer_id = customers.id AND s.due_paisa > 0), 0)
    - IFNULL((SELECT SUM(r.credit_reduced_paisa) FROM sale_returns r
              JOIN sales s2 ON s2.id = r.sale_id
              WHERE s2.customer_id = customers.id), 0))
WHERE id > 0;");

        _db.Execute(connection, @"
UPDATE suppliers
SET balance_paisa = opening_balance_paisa + IFNULL((SELECT SUM(p.due_paisa) FROM purchases p WHERE p.supplier_id = suppliers.id AND p.due_paisa > 0), 0)
WHERE id > 0;");

        AdvanceCounters(connection, sales.Count, purchases.Count, returns.Count);
    }

    private static int Insert(SqliteConnection connection, Database database, string sql, object row)
    {
        database.Execute(connection, sql, row);
        return (int)database.NextId(connection);
    }

    private static string Code(SettingsService settings, string prefixKey, int number) =>
        settings.GetString(prefixKey) + number.ToString("D4", CultureInfo.InvariantCulture);

    private static string Day(int daysAgo) => Clock.DateDaysAgo(daysAgo);

    private static string Stamp(int daysAgo, int hour) =>
        Day(daysAgo) + " " + hour.ToString("D2", CultureInfo.InvariantCulture) + ":"
        + ((daysAgo * 7) % 60).ToString("D2", CultureInfo.InvariantCulture) + ":00";

    private static string Expiry(int daysFromToday) =>
        DateTime.Today.AddDays(daysFromToday).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    private static string ExpiryMonth(int daysFromToday) =>
        DateTime.Today.AddDays(daysFromToday).ToString("yyyy-MM", CultureInfo.InvariantCulture);

    private Dictionary<string, int> WriteCategories(SqliteConnection connection, string at)
    {
        var ids = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var sort = 1;

        foreach (var parent in CategoryParents)
        {
            ids[parent] = Insert(connection, _db, @"
INSERT INTO categories(name, parent_id, sort_order, active, created_at)
VALUES(@name, NULL, @sort, 1, @at);", new { name = parent, sort = sort++, at });
        }

        foreach (var child in CategoryChildren)
        {
            var parentId = child.Parent.Length == 0 ? (int?)null : ids[child.Parent];

            ids[child.Name] = Insert(connection, _db, @"
INSERT INTO categories(name, parent_id, sort_order, active, created_at)
VALUES(@name, @parent, @sort, 1, @at);", new { name = child.Name, parent = parentId, sort = sort++, at });
        }

        return ids;
    }

    private Dictionary<string, int> WriteCompanies(SqliteConnection connection, string at)
    {
        var ids = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        foreach (var company in Companies)
        {
            ids[company.Name] = Insert(connection, _db, @"
INSERT INTO companies(name, phone, notes, active, rep_name, rep_phone, visit_day)
VALUES(@name, @phone, @notes, 1, @rep, @repPhone, @visit);",
                new
                {
                    name = company.Name,
                    phone = company.Phone,
                    notes = "Sample company",
                    rep = company.Rep,
                    repPhone = company.RepPhone,
                    visit = company.Visit
                });
        }

        return ids;
    }

    private List<int> WriteSuppliers(SqliteConnection connection, string at)
    {
        var ids = new List<int>();

        foreach (var supplier in Suppliers)
        {
            ids.Add(Insert(connection, _db, @"
INSERT INTO suppliers(name, agency, phone, address, notes, active, opening_balance_paisa, balance_paisa, created_at)
VALUES(@name, @agency, @phone, @address, @notes, 1, 0, 0, @at);",
                new
                {
                    name = supplier.Name,
                    agency = supplier.Agency,
                    phone = supplier.Phone,
                    address = supplier.Address,
                    notes = "Sample supplier",
                    at
                }));
        }

        return ids;
    }

    private List<int> WriteCustomers(SqliteConnection connection, string at)
    {
        var ids = new List<int>();

        foreach (var customer in Customers)
        {
            ids.Add(Insert(connection, _db, @"
INSERT INTO customers(name, phone, category, credit_limit_paisa, balance_paisa, loyalty_points, stars,
                      notes, active, created_at, opening_balance_paisa)
VALUES(@name, @phone, @category, @limit, 0, 0, 0, @notes, 1, @at, @opening);",
                new
                {
                    name = customer.Name,
                    phone = customer.Phone,
                    category = customer.Category,
                    limit = customer.LimitPaisa,
                    notes = "Sample customer",
                    at,
                    opening = customer.OpeningPaisa
                }));
        }

        return ids;
    }

    private List<int> WriteProducts(SqliteConnection connection, string at, Dictionary<string, int> categories,
        Dictionary<string, int> companies)
    {
        var ids = new List<int>();

        foreach (var product in Products)
        {
            ids.Add(Insert(connection, _db, @"
INSERT INTO products(name, generic, brand, barcode, company_id, category_id, rack_no, pack_size_label,
                     base_unit, box_strips, strip_tablets, cost_paisa, retail_paisa, vip_paisa, doctor_paisa,
                     reorder_level, track_expiry, active, created_at, updated_at)
VALUES(@name, @generic, @brand, @barcode, @company, @category, @rack, @pack,
       @unit, @boxStrips, @stripTablets, @cost, @retail, @vip, @doctor,
       @reorder, 1, 1, @at, @at);",
                new
                {
                    name = product.Name,
                    generic = product.Generic,
                    brand = product.Brand,
                    barcode = product.Barcode,
                    company = companies[product.Company],
                    category = categories[product.Category],
                    rack = product.Rack,
                    pack = product.Pack,
                    unit = product.Unit,
                    boxStrips = product.BoxStrips,
                    stripTablets = product.StripTablets,
                    cost = product.Cost,
                    retail = product.Retail,
                    vip = product.Vip,
                    doctor = product.Doctor,
                    reorder = product.Reorder,
                    at
                }));
        }

        return ids;
    }

    /// <summary>One or two batches per medicine, written into the shop's stock with no movement yet.</summary>
    private List<List<int>> WriteBatches(SqliteConnection connection, string at, List<int> products,
        List<int> suppliers)
    {
        var batches = new List<List<int>>();

        for (var index = 0; index < Products.Length; index++)
        {
            var product = Products[index];
            var list = new List<int>();

            for (var position = 0; position < product.Batches.Length; position++)
            {
                var batch = product.Batches[position];

                list.Add(Insert(connection, _db, @"
INSERT INTO batches(product_id, batch_no, expiry_date, expiry_ym, qty_base, cost_paisa, retail_paisa,
                    vip_paisa, doctor_paisa, is_sample, supplier_id, purchase_id, active, created_at)
VALUES(@productId, @batchNo, @expiry, @expiryYm, 0, @cost, @retail, @vip, @doctor, 0, @supplierId, NULL, 1, @at);",
                    new
                    {
                        productId = products[index],
                        batchNo = batch.Batch,
                        expiry = Expiry(batch.ExpiresInDays),
                        expiryYm = ExpiryMonth(batch.ExpiresInDays),
                        cost = product.Cost,
                        retail = product.Retail,
                        vip = product.Vip,
                        doctor = product.Doctor,
                        supplierId = suppliers[index % suppliers.Count],
                        at
                    }));
            }

            batches.Add(list);
        }

        return batches;
    }

    /// <summary>Four purchase bills; the fourth is unpaid, so the supplier ledger has a real payable.</summary>
    private List<int> WritePurchases(SqliteConnection connection, string at, int userId, List<int> suppliers,
        List<int> products, List<List<int>> batches)
    {
        var purchaseIds = new List<int>();

        for (var supplierIndex = 0; supplierIndex < Suppliers.Length; supplierIndex++)
        {
            var code = Code(_settings, "bill.purchasePrefix", supplierIndex + 1);
            var lines = new List<(int Product, int Batch, double Qty)>();

            for (var index = supplierIndex; index < Products.Length; index += Suppliers.Length)
            {
                for (var position = 0; position < Products[index].Batches.Length; position++)
                {
                    lines.Add((index, position, Products[index].Batches[position].Qty));
                }
            }

            long subtotal = 0;

            foreach (var line in lines)
            {
                subtotal += Products[line.Product].Cost * (long)line.Qty;
            }

            var unpaid = supplierIndex == Suppliers.Length - 1;
            var paid = unpaid ? 0 : subtotal;
            var stamp = Stamp(20 - supplierIndex, 11);

            var purchaseId = Insert(connection, _db, @"
INSERT INTO purchases(code, supplier_id, supplier_invoice_no, date, subtotal_paisa, discount_paisa, total_paisa,
                      paid_paisa, due_paisa, notes, user_id, created_at)
VALUES(@code, @supplier, @invoice, @date, @subtotal, 0, @total, @paid, @due, @notes, @userId, @at);",
                new
                {
                    code,
                    supplier = suppliers[supplierIndex],
                    invoice = "SUP-" + (1000 + supplierIndex).ToString(CultureInfo.InvariantCulture),
                    date = stamp,
                    subtotal,
                    total = subtotal,
                    paid,
                    due = subtotal - paid,
                    notes = "Sample purchase",
                    userId,
                    at = stamp
                });

            foreach (var line in lines)
            {
                var product = Products[line.Product];
                var lineTotal = product.Cost * (long)line.Qty;

                _db.Execute(connection, @"
INSERT INTO purchase_items(purchase_id, product_id, batch_id, qty_entered, unit, qty_base, cost_paisa,
                           retail_paisa, line_total_paisa, batch_no, expiry_date)
VALUES(@purchaseId, @productId, @batchId, @qty, @unit, @qty, @cost, @retail, @lineTotal, @batchNo, @expiry);",
                    new
                    {
                        purchaseId,
                        productId = products[line.Product],
                        batchId = batches[line.Product][line.Batch],
                        qty = line.Qty,
                        unit = product.Unit,
                        cost = product.Cost,
                        retail = product.Retail,
                        lineTotal,
                        batchNo = product.Batches[line.Batch].Batch,
                        expiry = Expiry(product.Batches[line.Batch].ExpiresInDays)
                    });

                WriteMovement(connection, products[line.Product], batches[line.Product][line.Batch],
                    "purchase", line.Qty, "purchase", purchaseId, code, "Bought", userId, stamp);
            }

            if (!unpaid)
            {
                _db.Execute(connection, @"
INSERT INTO payments(date, method, amount_paisa, ref, sale_id, purchase_id, customer_id, supplier_id, user_id, note)
VALUES(@date, 'cash', @amount, @ref, NULL, @purchaseId, NULL, @supplierId, @userId, 'Paid the purchase');",
                    new
                    {
                        date = stamp,
                        amount = paid,
                        purchaseId,
                        supplierId = suppliers[supplierIndex],
                        userId,
                        @ref = code
                    });
            }

            purchaseIds.Add(purchaseId);
        }

        return purchaseIds;
    }

    private void WriteMovement(SqliteConnection connection, int productId, int batchId, string type, double qty,
        string refType, int? refId, string refCode, string note, int userId, string at)
    {
        _db.Execute(connection, @"
INSERT INTO stock_movements(product_id, batch_id, type, qty_base, ref_type, ref_id, ref_code, note, user_id, created_at)
VALUES(@productId, @batchId, @type, @qty, @refType, @refId, @refCode, @note, @userId, @at);",
            new { productId, batchId, type, qty, refType, refId, refCode, note, userId, at });
    }

    /// <summary>The bills, their lines and the money that came in. Returns the sale item ids per bill.</summary>
    private List<SaleSample> WriteSales(SqliteConnection connection, string at, int userId, List<int> customers,
        List<int> products, List<List<int>> batches)
    {
        var result = new List<SaleSample>();
        var step = _settings.GetInt("bill.roundTo", 10);

        for (var index = 0; index < Bills.Length; index++)
        {
            var bill = Bills[index];
            var code = Code(_settings, "bill.salePrefix", index + 1);
            var stamp = Stamp(bill.DaysAgo, bill.Hour);

            long subtotal = 0;
            long costTotal = 0;
            var lines = new List<SaleLineSample>();

            foreach (var line in bill.Lines)
            {
                var product = Products[line.Product];
                var price = bill.Customer >= 0 ? product.Vip : product.Retail;
                var total = price * (long)line.Qty;

                subtotal += total;
                costTotal += product.Cost * (long)line.Qty;

                lines.Add(new SaleLineSample
                {
                    ProductId = products[line.Product],
                    BatchId = batches[line.Product][line.Batch],
                    Name = product.Name,
                    Qty = line.Qty,
                    Price = price,
                    Total = total,
                    Cost = product.Cost
                });
            }

            // The shop rounds DOWN to the nearest ten rupees, never below cost - the same rule the
            // counter uses (Z-03), so the sample bills obey the owner's own settings.
            var total2 = Money.RoundDownWithCostGuard(subtotal, costTotal, step);
            var roundOff = subtotal - total2;

            var paid = bill.Method == "credit" ? (bill.PartPaid ? total2 / 2 : 0) : total2;
            var due = total2 - paid;
            var status = due == 0 ? "paid" : paid == 0 ? "credit" : "partial";

            var saleId = Insert(connection, _db, @"
INSERT INTO sales(code, date, customer_id, user_id, subtotal_paisa, discount_paisa, tax_paisa, round_off_paisa,
                  total_paisa, paid_paisa, due_paisa, status, payment_method, notes, created_at)
VALUES(@code, @date, @customer, @userId, @subtotal, 0, 0, @roundOff,
       @total, @paid, @due, @status, @method, @notes, @at);",
                new
                {
                    code,
                    date = stamp,
                    customer = bill.Customer >= 0 ? customers[bill.Customer] : (int?)null,
                    userId,
                    subtotal,
                    roundOff,
                    total = total2,
                    paid,
                    due,
                    status,
                    method = bill.Method,
                    notes = "Sample bill",
                    at = stamp
                });

            foreach (var line in lines)
            {
                line.SaleItemId = Insert(connection, _db, @"
INSERT INTO sale_items(sale_id, product_id, batch_id, name_snapshot, qty_base, unit_sold, qty_entered,
                       unit_price_paisa, discount_paisa, line_total_paisa, cost_paisa_at_sale)
VALUES(@saleId, @productId, @batchId, @name, @qty, 'base', @qty, @price, 0, @total, @cost);",
                    new
                    {
                        saleId,
                        productId = line.ProductId,
                        batchId = line.BatchId,
                        name = line.Name,
                        qty = line.Qty,
                        price = line.Price,
                        total = line.Total,
                        cost = line.Cost
                    });

                WriteMovement(connection, line.ProductId, line.BatchId, "sale", -line.Qty, "sale", saleId,
                    code, "Sold", userId, stamp);
            }

            if (paid > 0)
            {
                if (bill.Method == "split")
                {
                    var half = paid / 2;

                    _db.Execute(connection, @"
INSERT INTO payments(date, method, amount_paisa, ref, sale_id, purchase_id, customer_id, supplier_id, user_id, note)
VALUES(@date, 'cash', @amount, NULL, @saleId, NULL, NULL, NULL, @userId, 'Split - cash part');",
                        new { date = stamp, amount = half, saleId, userId });

                    _db.Execute(connection, @"
INSERT INTO payments(date, method, amount_paisa, ref, sale_id, purchase_id, customer_id, supplier_id, user_id, note)
VALUES(@date, 'card', @amount, NULL, @saleId, NULL, NULL, NULL, @userId, 'Split - card part');",
                        new { date = stamp, amount = paid - half, saleId, userId });
                }
                else
                {
                    _db.Execute(connection, @"
INSERT INTO payments(date, method, amount_paisa, ref, sale_id, purchase_id, customer_id, supplier_id, user_id, note)
VALUES(@date, @method, @amount, NULL, @saleId, NULL, NULL, NULL, @userId, 'Bill paid');",
                        new { date = stamp, method = bill.Method == "credit" ? "cash" : bill.Method, amount = paid, saleId, userId });
                }
            }

            result.Add(new SaleSample
            {
                SaleId = saleId,
                Code = code,
                CustomerIndex = bill.Customer,
                Total = total2,
                Due = due,
                Lines = lines
            });
        }

        return result;
    }

    /// <summary>Two returns: one back to the shelf, one to quarantine with its own Q number.</summary>
    private List<int> WriteReturns(SqliteConnection connection, string at, int userId, List<int> products,
        List<List<int>> batches, List<SaleSample> sales)
    {
        var returnIds = new List<int>();
        var plans = new[]
        {
            (Sale: 0, Line: 0, Qty: 4.0, Restock: true, Reason: "Customer changed their mind"),
            (Sale: 3, Line: 1, Qty: 2.0, Restock: false, Reason: "Strip was damaged in the bag")
        };

        for (var index = 0; index < plans.Length; index++)
        {
            var plan = plans[index];
            var sale = sales[plan.Sale];
            var line = sale.Lines[plan.Line];
            var code = Code(_settings, "bill.returnPrefix", index + 1);
            var qcode = plan.Restock ? null : Code(_settings, "bill.quarantinePrefix", index);
            var refund = line.Price * (long)plan.Qty;
            var stamp = Stamp(sale.CustomerIndex >= 0 ? 1 : 2, 16);
            var credit = sale.Due > 0 && plan.Sale == 3 ? refund : 0;
            var cash = refund - credit;

            var returnId = Insert(connection, _db, @"
INSERT INTO sale_returns(date, sale_id, sale_item_id, product_id, batch_id, qty_base, refund_paisa, restock,
                         reason, user_id, created_at, qcode, disposition, qty_entered, unit_sold, note,
                         cash_refund_paisa, credit_reduced_paisa, code)
VALUES(@date, @saleId, @itemId, @productId, @batchId, @qty, @refund, @restock,
       @reason, @userId, @at, @qcode, @disposition, @qty, 'base', @note,
       @cash, @credit, @code);",
                new
                {
                    date = stamp,
                    saleId = sale.SaleId,
                    itemId = line.SaleItemId,
                    productId = line.ProductId,
                    batchId = line.BatchId,
                    qty = plan.Qty,
                    refund,
                    restock = plan.Restock ? 1 : 0,
                    reason = plan.Reason,
                    userId,
                    at = stamp,
                    qcode,
                    disposition = plan.Restock ? "restocked" : "quarantine",
                    note = plan.Restock ? "Back on the shelf" : "Kept aside until the owner decides",
                    cash,
                    credit,
                    code
                });

            // Only goods that went back on the shelf move the stock. A quarantine return is set
            // aside and is NOT on the shelf - exactly how ReturnService behaves (U-32), so the
            // shelves here can never claim goods the shop cannot sell.
            if (plan.Restock)
            {
                WriteMovement(connection, line.ProductId, line.BatchId, "return_in", plan.Qty, "return",
                    returnId, code, "Returned to the shelf", userId, stamp);
            }

            returnIds.Add(returnId);
        }

        return returnIds;
    }

    private void WriteExpenses(SqliteConnection connection, string at, int userId)
    {
        var expenses = new[]
        {
            (DaysAgo: 12, Category: "Bills", Title: "Electricity bill", Amount: 1850000L),
            (DaysAgo: 9, Category: "Rent", Title: "Shop rent - this month", Amount: 5000000L),
            (DaysAgo: 2, Category: "Other", Title: "Tea and cleaning", Amount: 120000L)
        };

        foreach (var expense in expenses)
        {
            _db.Execute(connection, @"
INSERT INTO expenses(date, category, title, amount_paisa, note, user_id, created_at)
VALUES(@date, @category, @title, @amount, @note, @userId, @at);",
                new
                {
                    date = Day(expense.DaysAgo),
                    category = expense.Category,
                    title = expense.Title,
                    amount = expense.Amount,
                    note = "Sample expense",
                    userId,
                    at
                });
        }

        _db.Execute(connection, @"
INSERT INTO owner_drawings(date, type, amount_paisa, note, user_id, created_at)
VALUES(@date, 'cash', @amount, @note, @userId, @at);",
            new { date = Day(6), amount = 1000000L, note = "Sample drawing - home", userId, at });

        _db.Execute(connection, @"
INSERT INTO owner_drawings(date, type, amount_paisa, note, user_id, created_at)
VALUES(@date, 'cash', @amount, @note, @userId, @at);",
            new { date = Day(1), amount = 500000L, note = "Sample drawing - school fee", userId, at });
    }

    private void WriteShift(SqliteConnection connection, string at, int userId)
    {
        _db.Execute(connection, @"
INSERT INTO shifts(user_id, opened_at, closed_at, opening_float_paisa, expected_paisa, actual_paisa,
                   difference_paisa, status, note, handed_to_user_id, handed_to_name, opened_from_shift_id)
VALUES(@userId, @opened, @closed, @float, @expected, @actual, 0, 'closed', @note, NULL, NULL, NULL);",
            new
            {
                userId,
                opened = Day(1) + " 09:00:00",
                closed = Day(1) + " 21:00:00",
                @float = 500000L,
                expected = 4135000L,
                actual = 4135000L,
                note = "Sample shift - counted at night"
            });
    }

    private void WriteStockTake(SqliteConnection connection, string at, int userId, List<int> products,
        List<List<int>> batches)
    {
        var code = Code(_settings, "bill.stockTakePrefix", 1);
        var sessionId = Insert(connection, _db, @"
INSERT INTO stock_takes(code, date, status, room, category_id, note, items_count, diff_count, short_paisa,
                        extra_paisa, user_id, applied_at, applied_by, created_at)
VALUES(@code, @date, 'applied', '', NULL, @note, 2, 2, @short, @extra, @userId, @appliedAt, @userId, @at);",
            new
            {
                code,
                date = Day(3),
                note = "Sample stock-take - two shelves",
                @short = 1800L, // one tablet short at cost
                extra = 1500L, // one ORS sachet extra at cost
                userId,
                appliedAt = Day(3) + " 20:00:00",
                at
            });

        // One shortfall and one extra: the page shows both colours, and the stock moves by the difference.
        WriteStockTakeLine(connection, sessionId, usersId: userId, productId: products[0], batchId: batches[0][0],
            systemQty: 800, countedQty: 799, cost: Products[0].Cost, day: Day(3), code: code);

        WriteStockTakeLine(connection, sessionId, usersId: userId, productId: products[13], batchId: batches[13][0],
            systemQty: 200, countedQty: 201, cost: Products[13].Cost, day: Day(3), code: code);
    }

    private void WriteStockTakeLine(SqliteConnection connection, int sessionId, int usersId, int productId,
        int batchId, double systemQty, double countedQty, long cost, string day, string code)
    {
        var diff = countedQty - systemQty;

        _db.Execute(connection, @"
INSERT INTO stock_take_items(stock_take_id, product_id, batch_id, system_qty_base, counted_qty_base, unit_entered,
                             qty_entered, diff_qty_base, value_paisa, created_at, system_cost_paisa, counted_by,
                             counted_at, note)
VALUES(@sessionId, @productId, @batchId, @system, @counted, 'base', @counted, @diff, @value, @at, @cost, @userId,
       @at, @note);",
            new
            {
                sessionId,
                productId,
                batchId,
                system = systemQty,
                counted = countedQty,
                diff,
                value = (long)diff * cost,
                at = day + " 19:30:00",
                cost,
                userId = usersId,
                note = diff < 0 ? "Fewer on the shelf" : "More than the book"
            });

        WriteMovement(connection, productId, batchId, "adjust", diff, "stock_take", sessionId, code,
            "Stock-take difference", usersId, day + " 20:00:00");
    }

    private void WriteSupplierReturn(SqliteConnection connection, string at, int userId, List<int> products,
        List<List<int>> batches, List<int> purchases)
    {
        var code = Code(_settings, "bill.supplierReturnPrefix", 1);

        // The near-expiry syrup goes back to the company - the page the owner uses when a supplier
        // will still take short-dated goods (F-51's cutoff window is exactly about this).
        var productIndex = 9;
        var product = Products[productIndex];
        var qty = 4.0;
        var total = product.Cost * (long)qty;

        var returnId = Insert(connection, _db, @"
INSERT INTO supplier_returns(code, date, supplier_id, purchase_id, reason, note, total_paisa, settled, user_id, created_at)
VALUES(@code, @date, (SELECT supplier_id FROM batches WHERE id = @batchId), @purchaseId, @reason, @note, @total, 0, @userId, @at);",
            new
            {
                code,
                date = Day(2),
                batchId = batches[productIndex][1],
                purchaseId = purchases[2],
                reason = "Near expiry",
                note = "Sample supplier return",
                total,
                userId,
                at
            });

        _db.Execute(connection, @"
INSERT INTO supplier_return_items(return_id, product_id, batch_id, qty_base, cost_paisa, line_total_paisa)
VALUES(@returnId, @productId, @batchId, @qty, @cost, @total);",
            new
            {
                returnId,
                productId = products[productIndex],
                batchId = batches[productIndex][1],
                qty,
                cost = product.Cost,
                total
            });

        WriteMovement(connection, products[productIndex], batches[productIndex][1], "supplier_return", -qty,
            "supplier_return", returnId, code, "Returned to the company", userId, Day(2) + " 17:00:00");
    }

    private void WriteProvisional(SqliteConnection connection, string at, int userId, List<int> products,
        List<List<int>> batches)
    {
        var code = Code(_settings, "bill.provisionalPrefix", 1);
        var productIndex = 1;
        var product = Products[productIndex];
        var qty = 10.0;

        var provisionalId = Insert(connection, _db, @"
INSERT INTO provisional_returns(code, date, phone, reason, notes, refund_paisa, status, linked_sale_id, linked_at,
                                linked_by, user_id, created_at, customer_name, linked_code, cancel_reason,
                                cancelled_at, cancelled_by)
VALUES(@code, @date, @phone, @reason, @notes, @refund, 'pending', NULL, NULL, NULL, @userId, @at,
       @customerName, NULL, NULL, NULL, NULL);",
            new
            {
                code,
                date = Day(1),
                phone = "0300-0001122",
                reason = "No bill with the customer",
                notes = "Sample no-bill return",
                refund = product.Retail * (long)qty,
                userId,
                at,
                customerName = "Walk-in customer"
            });

        _db.Execute(connection, @"
INSERT INTO provisional_items(provisional_id, product_id, qty_base, created_at, batch_id, qty_entered, unit_entered)
VALUES(@provisionalId, @productId, @qty, @at, @batchId, @qty, 'base');",
            new
            {
                provisionalId,
                productId = products[productIndex],
                qty,
                at,
                batchId = batches[productIndex][0]
            });
    }

    /// <summary>
    /// One medicine with a code per pack (F-43): the counter must add a strip when the strip is
    /// scanned and a box when the box is scanned - the sample shows that rule without a manual.
    /// </summary>
    private void WritePackBarcodes(SqliteConnection connection, string at, List<int> products)
    {
        var codes = new[]
        {
            (Barcode: "8964000111504", Pack: "strip"),
            (Barcode: "8964000111511", Pack: "box")
        };

        foreach (var code in codes)
        {
            _db.Execute(connection, @"
INSERT INTO product_barcodes(product_id, barcode, pack, note, active, created_at)
VALUES(@productId, @barcode, @pack, @note, 1, @at);",
                new
                {
                    productId = products[0],
                    barcode = code.Barcode,
                    pack = code.Pack,
                    note = "Sample pack code",
                    at
                });
        }
    }

    private void WriteCustomFields(SqliteConnection connection, string at, List<int> products, List<int> customers)
    {
        var sort = 1;

        foreach (var field in CustomFields)
        {
            var fieldId = Insert(connection, _db, @"
INSERT INTO custom_fields(entity, label, type, options_json, required, active, sort, created_at)
VALUES(@entity, @label, @type, NULL, 0, 1, @sort, @at);",
                new { entity = field.Entity, label = field.Label, type = field.Type, sort = sort++, at });

            if (field.Entity == "customer")
            {
                _db.Execute(connection, @"
INSERT INTO custom_values(field_id, entity, entity_id, value, updated_at)
VALUES(@fieldId, @entity, @entityId, @value, @at);",
                    new { fieldId, entity = "customer", entityId = customers[0], value = "35202-1234567-1", at });
            }
            else
            {
                _db.Execute(connection, @"
INSERT INTO custom_values(field_id, entity, entity_id, value, updated_at)
VALUES(@fieldId, @entity, @entityId, @value, @at);",
                    new { fieldId, entity = "product", entityId = products[10], value = "no", at });
            }
        }
    }

    private void WriteHeldCart(SqliteConnection connection, string at, int userId, List<int> products)
    {
        var code = Code(_settings, "bill.holdPrefix", 1);
        var first = 0;
        var second = 4;
        var total = Products[first].Retail * 20 + Products[second].Retail * 10;

        var cartId = Insert(connection, _db, @"
INSERT INTO held_carts(code, created_at, updated_at, user_id, customer_id, discount_percent, note, total_paisa,
                       items_count, status)
VALUES(@code, @at, @at, @userId, NULL, 0, @note, @total, 2, 'held');",
            new { code, at = Day(0) + " 11:30:00", userId, note = "Sample hold - customer went for cash", total });

        _db.Execute(connection, @"
INSERT INTO held_cart_items(cart_id, product_id, name_snapshot, unit, quantity, unit_price_paisa, position)
VALUES(@cartId, @productId, @name, 'base', 20, @price, 1);",
            new { cartId, productId = products[first], name = Products[first].Name, price = Products[first].Retail });

        _db.Execute(connection, @"
INSERT INTO held_cart_items(cart_id, product_id, name_snapshot, unit, quantity, unit_price_paisa, position)
VALUES(@cartId, @productId, @name, 'base', 10, @price, 2);",
            new { cartId, productId = products[second], name = Products[second].Name, price = Products[second].Retail });
    }

    /// <summary>
    /// Moves every document counter past the sample, so the first real bill is INV-0013 and never
    /// collides with a demo one (which the unique index on `sales.code` would refuse loudly).
    /// </summary>
    private void AdvanceCounters(SqliteConnection connection, int sales, int purchases, int returns)
    {
        var counters = new Dictionary<string, long>
        {
            ["bill.nextSaleNo"] = sales + 1,
            ["bill.nextPurchaseNo"] = purchases + 1,
            ["bill.nextReturnNo"] = returns + 1,
            ["bill.nextProvisionalNo"] = 2,
            ["bill.nextSupplierReturnNo"] = 2,
            ["bill.nextStockTakeNo"] = 2,
            ["bill.nextQuarantineNo"] = 2,
            ["bill.nextHoldNo"] = 2
        };

        foreach (var counter in counters)
        {
            _settings.WriteCounter(connection, counter.Key, counter.Value);
        }
    }

    private sealed class SaleSample
    {
        public int SaleId { get; init; }

        public string Code { get; init; } = string.Empty;

        public int CustomerIndex { get; init; } = -1;

        public long Total { get; init; }

        public long Due { get; init; }

        public List<SaleLineSample> Lines { get; init; } = new();
    }

    private sealed class SaleLineSample
    {
        public int SaleItemId { get; set; }

        public int ProductId { get; init; }

        public int BatchId { get; init; }

        public string Name { get; init; } = string.Empty;

        public double Qty { get; init; }

        public long Price { get; init; }

        public long Total { get; init; }

        public long Cost { get; init; }
    }
}
