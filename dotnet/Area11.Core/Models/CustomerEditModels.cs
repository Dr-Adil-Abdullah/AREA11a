using Area11.Core.Utils;

namespace Area11.Core.Models;

/// <summary>
/// What the khata page sends when the owner corrects a customer (F-11, Spec 17.3).
/// </summary>
public sealed class CustomerEditInput
{
    public int Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public string? Phone { get; set; }

    /// <summary>normal | vip | doctor - the same three words the bill uses for rates.</summary>
    public string Category { get; set; } = "normal";

    /// <summary>
    /// null means **leave the limit alone**: a cashier may fix a phone number but must not touch
    /// money. 0 means "no limit" (the same rule the bill uses in EnforceCreditLimit).
    /// </summary>
    public long? CreditLimitPaisa { get; set; }
}

/// <summary>What was stored, and anything the owner should read before he walks away.</summary>
public sealed class CustomerEditResult
{
    public int Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public long CreditLimitPaisa { get; set; }

    public bool LimitChanged { get; set; }

    public long OldLimitPaisa { get; set; }

    /// <summary>Things that are allowed but must be said out loud (a shared phone, a limit under the udhaar).</summary>
    public List<string> Warnings { get; } = new();

    public bool HasWarning => Warnings.Count > 0;

    public string WarningText => string.Join(" ", Warnings);

    public string LimitLabel => CreditLimitPaisa <= 0 ? "no limit" : Money.Format(CreditLimitPaisa);
}
