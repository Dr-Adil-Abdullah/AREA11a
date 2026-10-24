namespace Area11.Core.Models;

public sealed class Category
{
    public int Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public int? ParentId { get; set; }

    public int SortOrder { get; set; }

    public bool Active { get; set; } = true;

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
