namespace Area11.Core.Models;

/// <summary>Navigation projection only; a child service must authorize its own reads and writes.</summary>
public sealed class PartnerAccess
{
    public bool Customers { get; init; }
    public bool Suppliers { get; init; }
    public bool HasAny => Customers || Suppliers;

    /// <summary>Keep an allowed tab; otherwise choose the first allowed one, or no selection.</summary>
    public int SelectTab(int requested) => requested switch
    {
        0 or 1 when Customers => requested,
        2 when Suppliers => 2,
        _ => Customers ? 0 : Suppliers ? 2 : -1
    };
}
