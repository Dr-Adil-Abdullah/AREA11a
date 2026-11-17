namespace Area11.Core.Models;

/// <summary>Existing workflow whose module authorizes a catalog read, not a new permission grant.</summary>
public enum ProductReadPurpose
{
    Catalog,
    Counter,
    Purchase,
    SupplierReturn,
    WriteOff,
    Expense,
    ProvisionalReturn
}
