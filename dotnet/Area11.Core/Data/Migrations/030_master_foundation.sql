-- Master checklist, Phase 1: clean contracts for the next, separately approved UI phases.
-- Append-only and non-destructive: existing products, quantities, money, documents and legacy
-- generic/brand text remain intact. No screen or posting rule changes in this migration.

-- Native (built-in) form fields are different from owner-created custom_fields. A stable key,
-- data type and input method describe the control; safe_required is the shipped recommendation.
-- A critical safe-required field cannot become optional without an explicit owner acknowledgement.
CREATE TABLE IF NOT EXISTS form_field_definitions (
  id                         INTEGER PRIMARY KEY AUTOINCREMENT,
  field_key                  TEXT NOT NULL COLLATE NOCASE UNIQUE,
  section_key                TEXT NOT NULL,
  label                      TEXT NOT NULL,
  data_type                  TEXT NOT NULL,
  input_method               TEXT NOT NULL,
  help_text                  TEXT NOT NULL DEFAULT '',
  safe_required              INTEGER NOT NULL DEFAULT 0,
  required                   INTEGER NOT NULL DEFAULT 0,
  critical                   INTEGER NOT NULL DEFAULT 0,
  override_acknowledged_by   TEXT,
  override_acknowledged_at   TEXT,
  sort_order                 INTEGER NOT NULL DEFAULT 0,
  created_at                 TEXT NOT NULL DEFAULT (datetime('now','localtime')),
  updated_at                 TEXT NOT NULL DEFAULT (datetime('now','localtime')),
  CHECK (length(trim(field_key)) > 0),
  CHECK (length(trim(section_key)) > 0),
  CHECK (length(trim(label)) > 0),
  CHECK (data_type IN ('text', 'number', 'quantity', 'amount', 'date', 'datetime', 'select', 'check', 'photo')),
  CHECK (input_method IN ('text', 'numeric', 'calendar', 'month_calendar', 'dropdown', 'checkbox', 'photo', 'readonly')),
  CHECK (safe_required IN (0, 1) AND required IN (0, 1) AND critical IN (0, 1)),
  CHECK (
    (override_acknowledged_by IS NULL AND override_acknowledged_at IS NULL)
    OR
    (override_acknowledged_by IS NOT NULL AND length(trim(override_acknowledged_by)) > 0
     AND override_acknowledged_at IS NOT NULL AND length(trim(override_acknowledged_at)) > 0)
  ),
  CHECK (
    critical = 0 OR safe_required = 0 OR required = 1
    OR (override_acknowledged_by IS NOT NULL AND override_acknowledged_at IS NOT NULL)
  )
);
CREATE INDEX IF NOT EXISTS idx_form_field_definitions_section
  ON form_field_definitions(section_key, sort_order, id);

-- Q1=A: an incomplete record may be parked as a draft, never posted with a missing critical value.
-- Drafts are LOCAL ONLY. They have no invoice number, stock movement, payment, balance or report row.
-- The later form service owns validation/posting; this table only preserves the draft state.
CREATE TABLE IF NOT EXISTS document_drafts (
  id                   INTEGER PRIMARY KEY AUTOINCREMENT,
  draft_key            TEXT NOT NULL COLLATE NOCASE UNIQUE,
  draft_type           TEXT NOT NULL,
  payload_json         TEXT NOT NULL,
  status               TEXT NOT NULL DEFAULT 'draft',
  created_by_id        INTEGER REFERENCES users(id),
  created_by_name      TEXT NOT NULL,
  created_at           TEXT NOT NULL DEFAULT (datetime('now','localtime')),
  updated_at           TEXT NOT NULL DEFAULT (datetime('now','localtime')),
  posted_document_id   INTEGER,
  posted_at            TEXT,
  discard_reason       TEXT,
  CHECK (length(trim(draft_key)) > 0),
  CHECK (length(trim(draft_type)) > 0),
  CHECK (length(trim(created_by_name)) > 0),
  CHECK (json_valid(payload_json) AND json_type(payload_json) = 'object'),
  CHECK (status IN ('draft', 'posted', 'discarded')),
  CHECK (status <> 'draft' OR (posted_document_id IS NULL AND posted_at IS NULL)),
  CHECK (status <> 'posted' OR (posted_document_id IS NOT NULL AND posted_at IS NOT NULL))
);
CREATE INDEX IF NOT EXISTS idx_document_drafts_status
  ON document_drafts(status, draft_type, updated_at, id);

-- Central salts/generic formulas and brands. name_key is assigned by the later service after
-- trimming/collapsing whitespace and folding case; it is not a second display name.
CREATE TABLE IF NOT EXISTS generic_formulas (
  id          INTEGER PRIMARY KEY AUTOINCREMENT,
  name        TEXT NOT NULL,
  name_key    TEXT NOT NULL COLLATE NOCASE UNIQUE,
  active      INTEGER NOT NULL DEFAULT 1,
  sort_order  INTEGER NOT NULL DEFAULT 0,
  created_at  TEXT NOT NULL DEFAULT (datetime('now','localtime')),
  updated_at  TEXT NOT NULL DEFAULT (datetime('now','localtime')),
  CHECK (length(trim(name)) > 0),
  CHECK (length(trim(name_key)) > 0),
  CHECK (active IN (0, 1))
);
CREATE INDEX IF NOT EXISTS idx_generic_formulas_catalog
  ON generic_formulas(active, sort_order, name);

CREATE TABLE IF NOT EXISTS brands (
  id          INTEGER PRIMARY KEY AUTOINCREMENT,
  company_id  INTEGER REFERENCES companies(id),
  name        TEXT NOT NULL,
  name_key    TEXT NOT NULL COLLATE NOCASE UNIQUE,
  active      INTEGER NOT NULL DEFAULT 1,
  sort_order  INTEGER NOT NULL DEFAULT 0,
  created_at  TEXT NOT NULL DEFAULT (datetime('now','localtime')),
  updated_at  TEXT NOT NULL DEFAULT (datetime('now','localtime')),
  CHECK (length(trim(name)) > 0),
  CHECK (length(trim(name_key)) > 0),
  CHECK (active IN (0, 1))
);
CREATE INDEX IF NOT EXISTS idx_brands_catalog
  ON brands(active, sort_order, name);
CREATE INDEX IF NOT EXISTS idx_brands_company
  ON brands(company_id, active, name);

-- Nullable transition links: legacy generic/brand text is not guessed or merged. Q2=C means one
-- preferred supplier plus every historical supplier discovered from genuine purchase lines.
ALTER TABLE products ADD COLUMN generic_formula_id INTEGER REFERENCES generic_formulas(id);
ALTER TABLE products ADD COLUMN brand_id INTEGER REFERENCES brands(id);
ALTER TABLE products ADD COLUMN preferred_supplier_id INTEGER REFERENCES suppliers(id);
CREATE INDEX IF NOT EXISTS idx_products_generic_formula
  ON products(generic_formula_id, active);
CREATE INDEX IF NOT EXISTS idx_products_brand
  ON products(brand_id, active);
CREATE INDEX IF NOT EXISTS idx_products_preferred_supplier
  ON products(preferred_supplier_id, active);
CREATE INDEX IF NOT EXISTS idx_pitems_product_purchase
  ON purchase_items(product_id, purchase_id);
CREATE INDEX IF NOT EXISTS idx_purchases_supplier_date
  ON purchases(supplier_id, date);

-- One read-only directory for supplier filtering. A preferred supplier with no purchase is still
-- present; a historical supplier remains present even when another supplier is preferred.
CREATE VIEW product_supplier_directory AS
SELECT product_id,
       supplier_id,
       MAX(is_preferred) AS is_preferred,
       MAX(last_purchased_at) AS last_purchased_at
FROM (
  SELECT p.id AS product_id,
         p.preferred_supplier_id AS supplier_id,
         1 AS is_preferred,
         NULL AS last_purchased_at
  FROM products p
  WHERE p.preferred_supplier_id IS NOT NULL

  UNION ALL

  SELECT pi.product_id AS product_id,
         pu.supplier_id AS supplier_id,
         0 AS is_preferred,
         MAX(pu.date) AS last_purchased_at
  FROM purchase_items pi
  JOIN purchases pu ON pu.id = pi.purchase_id
  WHERE pu.supplier_id IS NOT NULL
  GROUP BY pi.product_id, pu.supplier_id
)
GROUP BY product_id, supplier_id;

-- Opening and closing discrepancies share one auditable contract instead of hiding the opening
-- reason inside a general note. Only non-zero differences need a row; both short and over are kept.
CREATE TABLE IF NOT EXISTS shift_discrepancies (
  id                 INTEGER PRIMARY KEY AUTOINCREMENT,
  shift_id           INTEGER NOT NULL REFERENCES shifts(id),
  stage              TEXT NOT NULL,
  expected_paisa     INTEGER NOT NULL,
  counted_paisa      INTEGER NOT NULL,
  difference_paisa   INTEGER NOT NULL,
  reason             TEXT NOT NULL,
  alerted_at         TEXT,
  created_at         TEXT NOT NULL DEFAULT (datetime('now','localtime')),
  UNIQUE(shift_id, stage),
  CHECK (stage IN ('opening', 'closing')),
  CHECK (difference_paisa <> 0),
  CHECK (difference_paisa = counted_paisa - expected_paisa),
  CHECK (length(trim(reason)) >= 3)
);
CREATE INDEX IF NOT EXISTS idx_shift_discrepancies_stage
  ON shift_discrepancies(stage, created_at, shift_id);
