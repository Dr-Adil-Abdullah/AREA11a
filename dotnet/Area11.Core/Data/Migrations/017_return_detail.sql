-- ============================================================================
-- 017 - RETURN DETAILS (v0.3.0, Phase 2)
--
-- Migration 012 already gave every return one permanent number (RET-0001).
-- This adds what a return receipt and the money checks need, without touching
-- any existing column:
--
--   cash_refund_paisa   - how much money actually left the drawer
--   credit_reduced_paisa- how much of it was taken off the customer's khata
--   qty_entered         - the quantity in the unit the customer bought
--   unit_sold           - box | strip | base
--   note                - the cashier's free remark
--
-- Why the split is stored: a refund is capped by what was collected (U-28).
-- Without these two columns the next return could not tell whether the first
-- one was paid in cash or written off the khata - and would refund twice.
-- ============================================================================

ALTER TABLE sale_returns ADD COLUMN cash_refund_paisa INTEGER NOT NULL DEFAULT 0;
ALTER TABLE sale_returns ADD COLUMN credit_reduced_paisa INTEGER NOT NULL DEFAULT 0;
ALTER TABLE sale_returns ADD COLUMN qty_entered REAL NOT NULL DEFAULT 0;
ALTER TABLE sale_returns ADD COLUMN unit_sold TEXT;
ALTER TABLE sale_returns ADD COLUMN note TEXT;

CREATE INDEX IF NOT EXISTS idx_returns_item ON sale_returns(sale_item_id);
