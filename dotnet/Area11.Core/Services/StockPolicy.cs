using Area11.Core.Data;
using Area11.Core.Utils;
using Microsoft.Data.Sqlite;

namespace Area11.Core.Services;

/// <summary>
/// D-93's single door for every NEW stock decrement. The caller must already be inside
/// <see cref="Database.Tx"/>: its immediate transaction makes the read, conditional write, ledger
/// and business rows one atomic unit. Historical negative rows are left alone; this policy only
/// refuses to make them worse. A physical stock-take is the separate, audited absolute-count path.
/// </summary>
internal static class StockPolicy
{
    /// <summary>
    /// Takes exactly <paramref name="quantity"/> from one live batch and returns its new quantity.
    /// The conditional UPDATE is defence in depth behind the transaction: even a future caller
    /// cannot turn an unchecked read into a below-zero write.
    /// </summary>
    public static double Decrease(
        Database db,
        SqliteConnection connection,
        int productId,
        int batchId,
        double quantity,
        string productName,
        string baseUnit,
        string operation)
    {
        if (!double.IsFinite(quantity) || quantity <= 0)
        {
            throw new ValidationException(
                productName + ": the " + operation + " quantity must be a real number greater than zero.");
        }
        if (UnitPolicy.RequiresWhole(db, connection, baseUnit) && !Qty.IsWhole(quantity))
        {
            throw new ValidationException(
                productName + ": " + operation + " uses whole " + baseUnit + " units; strength is not a stock division.");
        }

        var batch = ReadBatch(db, connection, productId, batchId)
            ?? throw new ValidationException(
                productName + ": the selected batch is not active any more. Refresh the form; nothing was saved.");
        var productQty = ReadProductQuantity(db, connection, productId);

        // Alerts and the owner’s reconciliation registry are per PRODUCT, not only per batch. A
        // positive batch beside a legacy minus batch must not be used to make that total worse.
        if (productQty < quantity)
        {
            throw InsufficientProduct(productQty, quantity, productName, baseUnit, operation);
        }

        if (batch.QtyBase < quantity)
        {
            throw InsufficientBatch(batch, quantity, productName, baseUnit, operation);
        }

        var changed = db.Execute(connection, @"
UPDATE batches
SET qty_base = qty_base - @quantity
WHERE id = @id AND product_id = @productId AND active = 1 AND qty_base >= @quantity
  AND (SELECT COALESCE(SUM(b.qty_base), 0) FROM batches b
       WHERE b.product_id = @productId AND b.active = 1) >= @quantity;",
            new { quantity, id = batchId, productId });

        if (changed != 1)
        {
            // Database.Tx serialises writers, so this should only be reachable after a future code
            // change or an unusual database error. Read the live number rather than guessing.
            var current = ReadBatch(db, connection, productId, batchId);
            if (current is not null)
            {
                var currentProductQty = ReadProductQuantity(db, connection, productId);
                if (currentProductQty < quantity)
                {
                    throw InsufficientProduct(currentProductQty, quantity, productName, baseUnit, operation);
                }

                throw InsufficientBatch(current, quantity, productName, baseUnit, operation);
            }

            throw new ValidationException(
                productName + ": the selected batch changed before stock could be saved. Refresh and try again; nothing was saved.");
        }

        return batch.QtyBase - quantity;
    }

    private static BatchState? ReadBatch(Database db, SqliteConnection connection, int productId, int batchId) =>
        db.QuerySingle<BatchState>(connection, @"
SELECT id, batch_no AS batchNo, qty_base AS qtyBase
FROM batches WHERE id = @id AND product_id = @productId AND active = 1;",
            new { id = batchId, productId });

    private static double ReadProductQuantity(Database db, SqliteConnection connection, int productId) =>
        Convert.ToDouble(db.Scalar(connection, @"
SELECT COALESCE(SUM(qty_base), 0) FROM batches
WHERE product_id = @productId AND active = 1;", new { productId }) ?? 0d);

    private static ValidationException InsufficientProduct(
        double available,
        double requested,
        string productName,
        string baseUnit,
        string operation)
    {
        var unit = string.IsNullOrWhiteSpace(baseUnit) ? "base unit(s)" : baseUnit.Trim();

        return new ValidationException(
            $"{productName}: cannot {operation} {Qty.Format(requested)} {unit}; only " +
            $"{Qty.Format(Math.Max(0, available))} {unit} remains in the product's total stock " +
            $"(book {Qty.Format(available)}). New work cannot take stock below zero. " +
            "Reconcile historical minus batches with a physical stock-take, reduce the quantity, or receive stock first.");
    }

    private static ValidationException InsufficientBatch(
        BatchState batch,
        double requested,
        string productName,
        string baseUnit,
        string operation)
    {
        var unit = string.IsNullOrWhiteSpace(baseUnit) ? "base unit(s)" : baseUnit.Trim();
        var batchLabel = string.IsNullOrWhiteSpace(batch.BatchNo) ? "(no batch number)" : batch.BatchNo;
        var available = Math.Max(0, batch.QtyBase);
        var historical = batch.QtyBase < 0
            ? " This is a historical minus quantity; reconcile it with a physical stock-take before taking more."
            : " Reduce the quantity or receive stock first.";

        return new ValidationException(
            $"{productName}: cannot {operation} {Qty.Format(requested)} {unit}; only " +
            $"{Qty.Format(available)} {unit} is available in batch {batchLabel} " +
            $"(book {Qty.Format(batch.QtyBase)}). New work cannot take stock below zero." + historical);
    }

    private sealed class BatchState
    {
        public int Id { get; set; }

        public string BatchNo { get; set; } = string.Empty;

        public double QtyBase { get; set; }
    }
}
