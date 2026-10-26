-- E-04: companies can be put in the owner's order, just like categories.
-- Append-only: existing company, product and representative rows keep their ids and values.
ALTER TABLE companies ADD COLUMN sort_order INTEGER NOT NULL DEFAULT 0;

CREATE INDEX IF NOT EXISTS idx_categories_parent_order
    ON categories(parent_id, active, sort_order, name);

CREATE INDEX IF NOT EXISTS idx_companies_catalog_order
    ON companies(active, sort_order, name);
