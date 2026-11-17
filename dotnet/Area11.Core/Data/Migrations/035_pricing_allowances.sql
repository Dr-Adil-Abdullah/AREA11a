-- Phase 2: new per-person allowances, immutable sale pricing snapshots and parked-cart state.
-- Missing non-owner allowance = no price/discount edits; no role-wide automatic grant.
CREATE TABLE user_pricing_allowances (
    user_id INTEGER PRIMARY KEY REFERENCES users(id),
    edit_rate INTEGER NOT NULL DEFAULT 0 CHECK(edit_rate IN (0,1)),
    manual_reduction_percent REAL NOT NULL DEFAULT 0 CHECK(manual_reduction_percent BETWEEN 0 AND 100),
    profit_discount INTEGER NOT NULL DEFAULT 0 CHECK(profit_discount IN (0,1)),
    profit_percent REAL NOT NULL DEFAULT 0 CHECK(profit_percent BETWEEN 0 AND 100),
    retail_discount INTEGER NOT NULL DEFAULT 0 CHECK(retail_discount IN (0,1)),
    retail_percent REAL NOT NULL DEFAULT 0 CHECK(retail_percent BETWEEN 0 AND 100),
    override_basis INTEGER NOT NULL DEFAULT 0 CHECK(override_basis IN (0,1))
);
ALTER TABLE sales ADD COLUMN discount_basis TEXT NOT NULL DEFAULT 'retail';
-- Old rows did not record an explicit pricing mode/percentage; do not invent those facts.
ALTER TABLE sale_items ADD COLUMN pricing_mode TEXT NOT NULL DEFAULT 'legacy';
ALTER TABLE sale_items ADD COLUMN discount_basis TEXT NOT NULL DEFAULT 'retail';
ALTER TABLE sale_items ADD COLUMN discount_percent REAL NULL;
ALTER TABLE held_carts ADD COLUMN discount_basis TEXT NULL;
ALTER TABLE held_cart_items ADD COLUMN pricing_mode TEXT NOT NULL DEFAULT 'normal';
ALTER TABLE held_cart_items ADD COLUMN discount_basis TEXT NOT NULL DEFAULT 'profit';
ALTER TABLE held_cart_items ADD COLUMN discount_percent REAL NOT NULL DEFAULT 0;
-- New key deliberately ignores the retired/inert discount.mode. Existing shops' future bills
-- switch once to profit; subsequent owner choices remain untouched. Old sale amounts never change.
INSERT OR IGNORE INTO settings(key, value, updated_at)
VALUES('discount.billBasis', '"profit"', datetime('now','localtime'));
