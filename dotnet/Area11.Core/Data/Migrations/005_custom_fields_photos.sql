-- ======================= CUSTOM FIELDS (apni marzi ke khaane) =======================
-- Owner khud nayi fields bana sakta hai (customer/product/supplier ke liye):
--   label = jo uper likha dikhega, type = text|number|date|select|check
CREATE TABLE IF NOT EXISTS custom_fields (
  id           INTEGER PRIMARY KEY AUTOINCREMENT,
  entity       TEXT NOT NULL,              -- customer | product | supplier
  label        TEXT NOT NULL,
  type         TEXT NOT NULL DEFAULT 'text',
  options_json TEXT,                       -- select ke liye: ["A","B"]
  required     INTEGER NOT NULL DEFAULT 0,
  active       INTEGER NOT NULL DEFAULT 1,
  sort         INTEGER NOT NULL DEFAULT 0,
  created_at   TEXT NOT NULL DEFAULT (datetime('now','localtime'))
);
CREATE INDEX IF NOT EXISTS idx_cf_entity ON custom_fields(entity, active, sort);

-- Values (koi row delete nahi hoti -- field band karne par value mehfooz rehti hai)
CREATE TABLE IF NOT EXISTS custom_values (
  id         INTEGER PRIMARY KEY AUTOINCREMENT,
  field_id   INTEGER NOT NULL REFERENCES custom_fields(id),
  entity     TEXT NOT NULL,
  entity_id  INTEGER NOT NULL,
  value      TEXT,
  updated_at TEXT NOT NULL DEFAULT (datetime('now','localtime')),
  UNIQUE(field_id, entity_id)
);
CREATE INDEX IF NOT EXISTS idx_cv_lookup ON custom_values(entity, entity_id);

-- Photo (chhoti kar ke data-URL me DB me -- offline bhi chalti hai)
ALTER TABLE customers ADD COLUMN photo TEXT;
ALTER TABLE products  ADD COLUMN photo TEXT;
