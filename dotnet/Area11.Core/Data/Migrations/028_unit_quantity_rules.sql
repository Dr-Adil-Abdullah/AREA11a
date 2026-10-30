-- UNIT-01 follow-up: quantity semantics belong to the managed unit row, not its editable name.
-- Append-only: existing unit labels, products, batches, quantities and historical documents stay intact.
ALTER TABLE units ADD COLUMN whole_only INTEGER NOT NULL DEFAULT 0;

-- The built-in injection row starts with whole stock/sale quantities. Subsequent renames keep this flag.
UPDATE units SET whole_only = 1 WHERE LOWER(TRIM(name)) = 'injection';
