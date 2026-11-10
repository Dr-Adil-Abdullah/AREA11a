-- 025: reconcile a no-bill return with the real sale lines.
--
-- Cedar linked only the header.  The goods and money had already moved at PR creation, but no
-- durable row said which original sale quantities and refundable paisa they consumed.  A later RET
-- could therefore return the same goods/value again.  These rows are an allocation of the ORIGINAL
-- PR event, not another sale return, stock movement, payment or khata event.

CREATE TABLE provisional_return_allocations (
  id                     INTEGER PRIMARY KEY AUTOINCREMENT,
  provisional_item_id    INTEGER NOT NULL REFERENCES provisional_items(id) ON DELETE CASCADE,
  sale_item_id           INTEGER NOT NULL REFERENCES sale_items(id),
  qty_base               REAL NOT NULL CHECK (qty_base > 0),
  expected_refund_paisa  INTEGER NOT NULL CHECK (expected_refund_paisa >= 0),
  refund_paisa           INTEGER NOT NULL CHECK (refund_paisa >= 0),
  created_at             TEXT NOT NULL DEFAULT (datetime('now','localtime')),
  UNIQUE (provisional_item_id, sale_item_id)
);

CREATE INDEX idx_pr_alloc_sale_item
  ON provisional_return_allocations(sale_item_id);
CREATE INDEX idx_pr_alloc_provisional_item
  ON provisional_return_allocations(provisional_item_id);

-- NULL means that an old linked PR still needs its one-time line reconciliation.  New links write
-- both values in the same transaction as their allocations.  expected is the remaining bill value
-- of the returned quantities at link time; unallocated is actual PR money above the original line
-- capacity.  Neither column rewrites the original payment or the original bill.
ALTER TABLE provisional_returns ADD COLUMN linked_expected_paisa INTEGER;
ALTER TABLE provisional_returns ADD COLUMN linked_unallocated_paisa INTEGER;
