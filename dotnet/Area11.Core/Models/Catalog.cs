namespace Area11.Core.Models;

public sealed class Category
{
    public int Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public int? ParentId { get; set; }

    public int SortOrder { get; set; }

    public bool Active { get; set; } = true;

    /// <summary>The parent's current name for management/read views; never a historical snapshot.</summary>
    public string? ParentName { get; set; }

    /// <summary>Current products linked to this row. Putting the row away never deletes them.</summary>
    public int ProductCount { get; set; }

    public string ParentLabel => ParentName ?? "Top level";

    public string CatalogLabel => Active ? Name : Name + " (put away)";

    public string ActiveLabel => Active ? "Active" : "Put away";

    public override string ToString() => Name;
}

/// <summary>A managed base/strength unit. Names are canonical lower-case, case-insensitively unique.</summary>
public sealed class Unit
{
    public int Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public bool Active { get; set; } = true;

    /// <summary>Whole stock quantities are required for this unit; the flag survives a rename.</summary>
    public bool WholeOnly { get; set; }

    public int SortOrder { get; set; }

    /// <summary>Current products using this as their stock-count base unit.</summary>
    public int ProductCount { get; set; }

    /// <summary>Current products using this only as a descriptive strength unit.</summary>
    public int StrengthCount { get; set; }

    public int UsageCount => ProductCount + StrengthCount;

    public string ActiveLabel => Active ? "Active" : "Put away";

    public string QuantityLabel => WholeOnly ? "Whole units" : "Fractions allowed";

    public string CatalogLabel => Active ? Name : Name + " (put away)";

    public override string ToString() => Name;
}

public sealed class Company
{
    public int Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public string? Phone { get; set; }

    public string? Notes { get; set; }

    /// <summary>The company's medical rep (F-44, Spec 8.3.3) - the man who comes on the visit day.</summary>
    public string? RepName { get; set; }

    public string? RepPhone { get; set; }

    /// <summary>1 = Monday ... 7 = Sunday; null = no fixed day.</summary>
    public int? VisitDay { get; set; }

    public bool Active { get; set; } = true;

    /// <summary>Owner-defined catalogue order; migration 026 adds this to existing company rows.</summary>
    public int SortOrder { get; set; }

    /// <summary>Current products linked to this row; historical documents are not counted or changed.</summary>
    public int ProductCount { get; set; }

    public string ActiveLabel => Active ? "Active" : "Put away";

    public override string ToString() => Name;
}

public sealed class Customer
{
    public int Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public string? Phone { get; set; }

    public string? Category { get; set; }

    public long CreditLimitPaisa { get; set; }

    public long BalancePaisa { get; set; }

    public int LoyaltyPoints { get; set; }

    public string? Photo { get; set; }

    public bool Active { get; set; } = true;

    public override string ToString() => Name;
}

public sealed class Supplier
{
    public int Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public string? Agency { get; set; }

    public string? Phone { get; set; }

    public long BalancePaisa { get; set; }

    public string? Notes { get; set; }

    public bool Active { get; set; } = true;

    public override string ToString() => Name;
}
