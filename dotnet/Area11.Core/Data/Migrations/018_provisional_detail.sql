-- ============================================================================
-- 018 - NO-BILL (RUSH) RETURN DETAILS  (v0.3.2, Phase 2, F-25)
--
-- Migration 004 created provisional_returns / provisional_items but left three
-- things out that the rush-return flow cannot work without:
--
--   customer_name  - who brought the medicine back. The table only had a phone,
--                    and at the counter the name is what people say first.
--   batch_id       - which batch the goods were put into. Without it a cancelled
--                    entry could not take the same stock back out again, and the
--                    shelf would quietly gain medicine that never came back.
--   cancel_*       - a provisional return is cancelled only with a written
--                    reason (15.5.5); the reason, the person and the time must
--                    survive, exactly like the return itself.
--
-- linked_code is the bill number as typed (INV-0001) next to the foreign key,
-- so the receipt and the alert can print it without another join.
-- ============================================================================

ALTER TABLE provisional_returns ADD COLUMN customer_name TEXT;
ALTER TABLE provisional_returns ADD COLUMN linked_code TEXT;
ALTER TABLE provisional_returns ADD COLUMN cancel_reason TEXT;
ALTER TABLE provisional_returns ADD COLUMN cancelled_at TEXT;
ALTER TABLE provisional_returns ADD COLUMN cancelled_by INTEGER;

ALTER TABLE provisional_items ADD COLUMN batch_id INTEGER REFERENCES batches(id);
ALTER TABLE provisional_items ADD COLUMN qty_entered REAL NOT NULL DEFAULT 0;
ALTER TABLE provisional_items ADD COLUMN unit_entered TEXT;

-- One permanent number per entry, like every other document (5.1.12).
CREATE UNIQUE INDEX IF NOT EXISTS idx_prov_code ON provisional_returns(code);
CREATE INDEX IF NOT EXISTS idx_prov_items_product ON provisional_items(product_id);
