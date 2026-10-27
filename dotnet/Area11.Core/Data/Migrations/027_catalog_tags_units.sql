-- 27-Oct physical-testing decisions: primary category + filter tags, managed units and strength.
-- Append-only: existing products, batches, quantities, prices and historical documents stay intact.

CREATE TABLE IF NOT EXISTS units (
  id         INTEGER PRIMARY KEY AUTOINCREMENT,
  name       TEXT NOT NULL COLLATE NOCASE UNIQUE,
  active     INTEGER NOT NULL DEFAULT 1,
  sort_order INTEGER NOT NULL DEFAULT 0,
  created_at TEXT NOT NULL DEFAULT (datetime('now','localtime'))
);

INSERT OR IGNORE INTO units(name, active, sort_order)
VALUES
  ('tablet', 1, 10),
  ('capsule', 1, 20),
  ('ml', 1, 30),
  ('piece', 1, 40),
  ('sachet', 1, 50),
  ('injection', 1, 60),
  ('unit', 1, 70);

-- A legacy shop may already use a unit that was not in the old five-item screen list.
-- Keep that spelling as one canonical, case-insensitive managed row instead of breaking the product.
INSERT OR IGNORE INTO units(name, active, sort_order)
SELECT DISTINCT LOWER(TRIM(base_unit)), 1, 100
FROM products
WHERE TRIM(IFNULL(base_unit, '')) <> '';

CREATE TABLE IF NOT EXISTS product_category_tags (
  product_id  INTEGER NOT NULL REFERENCES products(id) ON DELETE CASCADE,
  category_id INTEGER NOT NULL REFERENCES categories(id),
  PRIMARY KEY(product_id, category_id)
);
CREATE INDEX IF NOT EXISTS idx_product_category_tags_category
  ON product_category_tags(category_id, product_id);
CREATE INDEX IF NOT EXISTS idx_product_category_tags_product
  ON product_category_tags(product_id, category_id);

-- Strength is descriptive, never a stock-count conversion. For example, 10 injections x 50 mL
-- remains 10 injection units in stock; strength_value/strength_unit only identify the variant.
ALTER TABLE products ADD COLUMN strength_value REAL NOT NULL DEFAULT 0;
ALTER TABLE products ADD COLUMN strength_unit TEXT NOT NULL DEFAULT '';
CREATE INDEX IF NOT EXISTS idx_products_strength
  ON products(strength_unit, strength_value, active);
