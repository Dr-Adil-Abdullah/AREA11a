-- Extra Discount is strictly a sale-level allowance. No historical money or item rows change.
ALTER TABLE user_pricing_allowances ADD COLUMN extra_discount INTEGER NOT NULL DEFAULT 0 CHECK(extra_discount IN (0,1));
ALTER TABLE user_pricing_allowances ADD COLUMN extra_limit_paisa INTEGER NOT NULL DEFAULT 0 CHECK(extra_limit_paisa >= 0);
ALTER TABLE sales ADD COLUMN pre_extra_total_paisa INTEGER NULL;
ALTER TABLE sales ADD COLUMN extra_discount_paisa INTEGER NOT NULL DEFAULT 0 CHECK(extra_discount_paisa >= 0);
ALTER TABLE held_carts ADD COLUMN extra_discount_paisa INTEGER NOT NULL DEFAULT 0 CHECK(extra_discount_paisa >= 0);
-- NULL pre-extra means not recorded by old versions, NOT a fabricated historical snapshot.
