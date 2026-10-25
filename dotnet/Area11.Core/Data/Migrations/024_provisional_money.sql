-- 024: make a no-bill refund a complete financial document.
--
-- Before this migration the PR header said how much cash was handed back, but there was no
-- payment row for the drawer and no line allocation for item/category reports.  Keep the original
-- header and items immutable, add the missing snapshots, and backfill the old rows once.

ALTER TABLE payments ADD COLUMN provisional_return_id INTEGER REFERENCES provisional_returns(id);
CREATE INDEX IF NOT EXISTS idx_payments_provisional ON payments(provisional_return_id);

ALTER TABLE provisional_items ADD COLUMN refund_paisa INTEGER NOT NULL DEFAULT 0;
ALTER TABLE provisional_items ADD COLUMN cost_paisa_at_return INTEGER NOT NULL DEFAULT 0;

-- Cost is a historical snapshot.  A later purchase must not rewrite the profit of an old return.
UPDATE provisional_items
SET cost_paisa_at_return = COALESCE(
    (SELECT b.cost_paisa FROM batches b WHERE b.id = provisional_items.batch_id),
    (SELECT p.cost_paisa FROM products p WHERE p.id = provisional_items.product_id),
    0);

-- Allocate each old header refund equally by line. A no-bill entry has no reliable line prices;
-- equal integer shares are an explicit reporting allocation, not an invented price history.
-- SQLite integer division/remainder is the same rule as ProvisionalReturnService.AllocateRefund:
-- the first lines receive the leftover paisa and the shares always add up to the header exactly.
WITH numbered AS (
    SELECT i.id, pr.refund_paisa,
           ROW_NUMBER() OVER (PARTITION BY i.provisional_id ORDER BY i.id) AS line_no,
           COUNT(*) OVER (PARTITION BY i.provisional_id) AS line_count
    FROM provisional_items i
    JOIN provisional_returns pr ON pr.id = i.provisional_id
)
UPDATE provisional_items
SET refund_paisa = COALESCE((
    SELECT (n.refund_paisa / n.line_count)
         + CASE WHEN n.line_no <= n.refund_paisa % n.line_count THEN 1 ELSE 0 END
    FROM numbered n
    WHERE n.id = provisional_items.id), 0);

-- A read model, not a second financial ledger. Only a live pending/linked document belongs in
-- sales and profit reports. Cancelling voids that document; it is NOT a negative refund or a
-- negative cost event. The original payment stays in the cash ledger because cancellation does
-- not prove that the customer handed the money back.
CREATE VIEW provisional_report_lines AS
SELECT i.id AS item_id,
       pr.id AS provisional_id,
       i.product_id,
       pr.date AS event_date,
       i.qty_base,
       i.refund_paisa,
       i.qty_base * i.cost_paisa_at_return AS restock_cost_paisa
FROM provisional_items i
JOIN provisional_returns pr ON pr.id = i.provisional_id
WHERE pr.status IN ('pending', 'linked');

-- Existing entries also need their missing money evidence. Negative is cash out. Cancellation and
-- bill linking deliberately create no payment: neither event proves that cash moved again.
INSERT INTO payments(date, method, amount_paisa, provisional_return_id, user_id, note)
SELECT pr.date, 'cash', -pr.refund_paisa, pr.id, pr.user_id,
       'No-bill cash refund ' || pr.code
FROM provisional_returns pr
WHERE pr.refund_paisa > 0;
