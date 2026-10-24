-- ============================================================================
-- 019 - STOCK-TAKE DETAILS  (v0.3.4, Phase 2, F-27)
--
-- Migration 008 created stock_takes / stock_take_items. Two things the real
-- count needs were missing:
--
--   system_cost_paisa - the cost of the batch AT THE MOMENT THE SESSION OPENED.
--                       Apply can happen hours later; if a purchase changed the
--                       cost in between, the shortage/excess value must not move
--                       with it. The frozen cost is what makes the sheet honest.
--
--   counted_by / counted_at - who wrote the number and when. A count sheet with
--                       no name on it is not an audit; grandfathering the name
--                       in only at Apply would blame the wrong person.
--
-- A stock-take number must be unique for ever (5.1.12); the index below adds
-- the guarantee the table should have had from the start.
-- ============================================================================

ALTER TABLE stock_take_items ADD COLUMN system_cost_paisa INTEGER NOT NULL DEFAULT 0;
ALTER TABLE stock_take_items ADD COLUMN counted_by INTEGER;
ALTER TABLE stock_take_items ADD COLUMN counted_at TEXT;
ALTER TABLE stock_take_items ADD COLUMN note TEXT;

CREATE UNIQUE INDEX IF NOT EXISTS idx_stake_code ON stock_takes(code);
CREATE INDEX IF NOT EXISTS idx_stake_status ON stock_takes(status);
