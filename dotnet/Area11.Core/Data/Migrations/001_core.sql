-- ======================= SETTINGS (RULE E) =======================
CREATE TABLE IF NOT EXISTS settings (
  key        TEXT PRIMARY KEY,
  value      TEXT NOT NULL,
  updated_at TEXT NOT NULL DEFAULT (datetime('now','localtime'))
);

-- ======================= USERS =======================
CREATE TABLE IF NOT EXISTS users (
  id             INTEGER PRIMARY KEY AUTOINCREMENT,
  name           TEXT NOT NULL,
  role           TEXT NOT NULL DEFAULT 'cashier',   -- owner | manager | cashier
  pin_hash       TEXT,
  password_hash  TEXT,
  phone          TEXT,
  active         INTEGER NOT NULL DEFAULT 1,
  last_login_at  TEXT,
  created_at     TEXT NOT NULL DEFAULT (datetime('now','localtime'))
);
CREATE INDEX IF NOT EXISTS idx_users_role ON users(role);

-- ======================= CATEGORIES / COMPANIES =======================
CREATE TABLE IF NOT EXISTS categories (
  id         INTEGER PRIMARY KEY AUTOINCREMENT,
  name       TEXT NOT NULL UNIQUE,
  parent_id  INTEGER,
  sort_order INTEGER NOT NULL DEFAULT 0,
  active     INTEGER NOT NULL DEFAULT 1,
  created_at TEXT NOT NULL DEFAULT (datetime('now','localtime'))
);

CREATE TABLE IF NOT EXISTS companies (
  id     INTEGER PRIMARY KEY AUTOINCREMENT,
  name   TEXT NOT NULL UNIQUE,
  phone  TEXT,
  notes  TEXT,
  active INTEGER NOT NULL DEFAULT 1
);

-- ======================= SUPPLIERS =======================
CREATE TABLE IF NOT EXISTS suppliers (
  id                    INTEGER PRIMARY KEY AUTOINCREMENT,
  name                  TEXT NOT NULL,
  agency                TEXT,
  phone                 TEXT,
  address               TEXT,
  notes                 TEXT,
  active                INTEGER NOT NULL DEFAULT 1,
  opening_balance_paisa INTEGER NOT NULL DEFAULT 0,
  balance_paisa         INTEGER NOT NULL DEFAULT 0,   -- + = hum ne dena hai
  created_at            TEXT NOT NULL DEFAULT (datetime('now','localtime'))
);
CREATE INDEX IF NOT EXISTS idx_suppliers_name ON suppliers(name);

-- ======================= CUSTOMERS =======================
CREATE TABLE IF NOT EXISTS customers (
  id                 INTEGER PRIMARY KEY AUTOINCREMENT,
  name               TEXT NOT NULL,
  phone              TEXT,
  category           TEXT NOT NULL DEFAULT 'normal',  -- normal | vip | doctor
  credit_limit_paisa INTEGER NOT NULL DEFAULT 0,      -- 0 = no limit
  balance_paisa      INTEGER NOT NULL DEFAULT 0,      -- + = customer ne dena hai
  loyalty_points     INTEGER NOT NULL DEFAULT 0,
  stars              INTEGER NOT NULL DEFAULT 1,
  notes              TEXT,
  active             INTEGER NOT NULL DEFAULT 1,
  created_at         TEXT NOT NULL DEFAULT (datetime('now','localtime'))
);
CREATE INDEX IF NOT EXISTS idx_customers_phone ON customers(phone);
CREATE INDEX IF NOT EXISTS idx_customers_name  ON customers(name);

-- ======================= PRODUCTS =======================
CREATE TABLE IF NOT EXISTS products (
  id              INTEGER PRIMARY KEY AUTOINCREMENT,
  name            TEXT NOT NULL,
  generic         TEXT,
  brand           TEXT,
  barcode         TEXT UNIQUE,
  company_id      INTEGER REFERENCES companies(id),
  category_id     INTEGER REFERENCES categories(id),
  rack_no         TEXT,
  pack_size_label TEXT,
  base_unit       TEXT NOT NULL DEFAULT 'tablet',  -- tablet | capsule | ml | piece
  box_strips      INTEGER NOT NULL DEFAULT 0,      -- 1 Box = ? strips
  strip_tablets   INTEGER NOT NULL DEFAULT 0,      -- 1 Strip = ? base units
  cost_paisa      INTEGER NOT NULL DEFAULT 0,
  retail_paisa    INTEGER NOT NULL DEFAULT 0,
  vip_paisa       INTEGER NOT NULL DEFAULT 0,
  doctor_paisa    INTEGER NOT NULL DEFAULT 0,
  reorder_level   REAL NOT NULL DEFAULT 0,
  track_expiry    INTEGER NOT NULL DEFAULT 1,
  active          INTEGER NOT NULL DEFAULT 1,
  created_at      TEXT NOT NULL DEFAULT (datetime('now','localtime')),
  updated_at      TEXT NOT NULL DEFAULT (datetime('now','localtime'))
);
CREATE INDEX IF NOT EXISTS idx_products_name    ON products(name);
CREATE INDEX IF NOT EXISTS idx_products_generic ON products(generic);
CREATE INDEX IF NOT EXISTS idx_products_barcode ON products(barcode);

-- ======================= BATCHES =======================
CREATE TABLE IF NOT EXISTS batches (
  id           INTEGER PRIMARY KEY AUTOINCREMENT,
  product_id   INTEGER NOT NULL REFERENCES products(id),
  batch_no     TEXT NOT NULL,
  expiry_date  TEXT,             -- YYYY-MM-DD
  expiry_ym    TEXT,             -- YYYY-MM (tez filter)
  qty_base     REAL NOT NULL DEFAULT 0,
  cost_paisa   INTEGER NOT NULL DEFAULT 0,
  retail_paisa INTEGER NOT NULL DEFAULT 0,
  vip_paisa    INTEGER NOT NULL DEFAULT 0,
  doctor_paisa INTEGER NOT NULL DEFAULT 0,
  is_sample    INTEGER NOT NULL DEFAULT 0,
  supplier_id  INTEGER REFERENCES suppliers(id),
  purchase_id  INTEGER,
  active       INTEGER NOT NULL DEFAULT 1,
  created_at   TEXT NOT NULL DEFAULT (datetime('now','localtime'))
);
CREATE INDEX IF NOT EXISTS idx_batches_product ON batches(product_id);
CREATE INDEX IF NOT EXISTS idx_batches_expiry  ON batches(expiry_ym);

-- ======================= PURCHASES =======================
CREATE TABLE IF NOT EXISTS purchases (
  id                   INTEGER PRIMARY KEY AUTOINCREMENT,
  code                 TEXT NOT NULL UNIQUE,     -- PINV-0001
  supplier_id          INTEGER REFERENCES suppliers(id),
  supplier_invoice_no  TEXT,
  date                 TEXT NOT NULL DEFAULT (datetime('now','localtime')),
  subtotal_paisa       INTEGER NOT NULL DEFAULT 0,
  discount_paisa       INTEGER NOT NULL DEFAULT 0,
  total_paisa          INTEGER NOT NULL DEFAULT 0,
  paid_paisa           INTEGER NOT NULL DEFAULT 0,
  due_paisa            INTEGER NOT NULL DEFAULT 0,
  notes                TEXT,
  user_id              INTEGER,
  created_at           TEXT NOT NULL DEFAULT (datetime('now','localtime'))
);
CREATE INDEX IF NOT EXISTS idx_purchases_date ON purchases(date);

CREATE TABLE IF NOT EXISTS purchase_items (
  id              INTEGER PRIMARY KEY AUTOINCREMENT,
  purchase_id     INTEGER NOT NULL REFERENCES purchases(id) ON DELETE CASCADE,
  product_id      INTEGER NOT NULL REFERENCES products(id),
  batch_id        INTEGER REFERENCES batches(id),
  qty_entered     REAL NOT NULL DEFAULT 0,
  unit            TEXT NOT NULL DEFAULT 'box',   -- box | strip | base
  qty_base        REAL NOT NULL DEFAULT 0,
  cost_paisa      INTEGER NOT NULL DEFAULT 0,
  retail_paisa    INTEGER NOT NULL DEFAULT 0,
  line_total_paisa INTEGER NOT NULL DEFAULT 0,
  batch_no        TEXT,
  expiry_date     TEXT
);
CREATE INDEX IF NOT EXISTS idx_pitems_purchase ON purchase_items(purchase_id);

-- ======================= SALES =======================
CREATE TABLE IF NOT EXISTS sales (
  id             INTEGER PRIMARY KEY AUTOINCREMENT,
  code           TEXT NOT NULL UNIQUE,          -- INV-0001
  date           TEXT NOT NULL DEFAULT (datetime('now','localtime')),
  customer_id    INTEGER REFERENCES customers(id),
  user_id        INTEGER,
  subtotal_paisa INTEGER NOT NULL DEFAULT 0,
  discount_paisa INTEGER NOT NULL DEFAULT 0,
  tax_paisa      INTEGER NOT NULL DEFAULT 0,
  round_off_paisa INTEGER NOT NULL DEFAULT 0,
  total_paisa    INTEGER NOT NULL DEFAULT 0,
  paid_paisa     INTEGER NOT NULL DEFAULT 0,
  due_paisa      INTEGER NOT NULL DEFAULT 0,
  status         TEXT NOT NULL DEFAULT 'paid',   -- paid|partial|credit|hold|void
  payment_method TEXT NOT NULL DEFAULT 'cash',   -- cash|credit|online|card|split
  notes          TEXT,
  created_at     TEXT NOT NULL DEFAULT (datetime('now','localtime'))
);
CREATE INDEX IF NOT EXISTS idx_sales_date     ON sales(date);
CREATE INDEX IF NOT EXISTS idx_sales_customer ON sales(customer_id);
CREATE INDEX IF NOT EXISTS idx_sales_status   ON sales(status);

CREATE TABLE IF NOT EXISTS sale_items (
  id                 INTEGER PRIMARY KEY AUTOINCREMENT,
  sale_id            INTEGER NOT NULL REFERENCES sales(id) ON DELETE CASCADE,
  product_id         INTEGER NOT NULL REFERENCES products(id),
  batch_id           INTEGER REFERENCES batches(id),
  name_snapshot      TEXT,
  qty_base           REAL NOT NULL DEFAULT 0,
  unit_sold          TEXT NOT NULL DEFAULT 'base',
  qty_entered        REAL NOT NULL DEFAULT 0,
  unit_price_paisa   INTEGER NOT NULL DEFAULT 0,
  discount_paisa     INTEGER NOT NULL DEFAULT 0,
  line_total_paisa   INTEGER NOT NULL DEFAULT 0,
  cost_paisa_at_sale INTEGER NOT NULL DEFAULT 0
);
CREATE INDEX IF NOT EXISTS idx_sitems_sale    ON sale_items(sale_id);
CREATE INDEX IF NOT EXISTS idx_sitems_product ON sale_items(product_id);

-- ======================= PAYMENTS =======================
CREATE TABLE IF NOT EXISTS payments (
  id           INTEGER PRIMARY KEY AUTOINCREMENT,
  date         TEXT NOT NULL DEFAULT (datetime('now','localtime')),
  method       TEXT NOT NULL DEFAULT 'cash',
  amount_paisa INTEGER NOT NULL DEFAULT 0,
  ref          TEXT,
  sale_id      INTEGER REFERENCES sales(id),
  purchase_id  INTEGER REFERENCES purchases(id),
  customer_id  INTEGER REFERENCES customers(id),
  supplier_id  INTEGER REFERENCES suppliers(id),
  user_id      INTEGER,
  note         TEXT
);
CREATE INDEX IF NOT EXISTS idx_payments_date ON payments(date);

-- ======================= STOCK MOVEMENTS =======================
CREATE TABLE IF NOT EXISTS stock_movements (
  id         INTEGER PRIMARY KEY AUTOINCREMENT,
  product_id INTEGER NOT NULL REFERENCES products(id),
  batch_id   INTEGER REFERENCES batches(id),
  type       TEXT NOT NULL,     -- in|out|adjust|return_in|return_out|writeoff|sample
  qty_base   REAL NOT NULL DEFAULT 0,
  ref_type   TEXT,
  ref_id     INTEGER,
  ref_code   TEXT,
  note       TEXT,
  user_id    INTEGER,
  created_at TEXT NOT NULL DEFAULT (datetime('now','localtime'))
);
CREATE INDEX IF NOT EXISTS idx_moves_product ON stock_movements(product_id);
CREATE INDEX IF NOT EXISTS idx_moves_date    ON stock_movements(created_at);

-- ======================= CASH CONTROL =======================
CREATE TABLE IF NOT EXISTS shifts (
  id                  INTEGER PRIMARY KEY AUTOINCREMENT,
  user_id             INTEGER,
  opened_at           TEXT NOT NULL DEFAULT (datetime('now','localtime')),
  closed_at           TEXT,
  opening_float_paisa INTEGER NOT NULL DEFAULT 0,
  expected_paisa      INTEGER,
  actual_paisa        INTEGER,
  difference_paisa    INTEGER,
  status              TEXT NOT NULL DEFAULT 'open',
  note                TEXT
);

CREATE TABLE IF NOT EXISTS expenses (
  id           INTEGER PRIMARY KEY AUTOINCREMENT,
  date         TEXT NOT NULL DEFAULT (datetime('now','localtime')),
  category     TEXT NOT NULL DEFAULT 'other',
  title        TEXT NOT NULL,
  amount_paisa INTEGER NOT NULL DEFAULT 0,
  note         TEXT,
  user_id      INTEGER,
  created_at   TEXT NOT NULL DEFAULT (datetime('now','localtime'))
);
CREATE INDEX IF NOT EXISTS idx_expenses_date ON expenses(date);

CREATE TABLE IF NOT EXISTS owner_drawings (
  id           INTEGER PRIMARY KEY AUTOINCREMENT,
  date         TEXT NOT NULL DEFAULT (datetime('now','localtime')),
  type         TEXT NOT NULL DEFAULT 'cash',   -- cash | goods
  amount_paisa INTEGER NOT NULL DEFAULT 0,
  note         TEXT,
  user_id      INTEGER,
  created_at   TEXT NOT NULL DEFAULT (datetime('now','localtime'))
);

-- ======================= AUDIT LOG (blackbox) =======================
CREATE TABLE IF NOT EXISTS audit_logs (
  id        INTEGER PRIMARY KEY AUTOINCREMENT,
  at        TEXT NOT NULL DEFAULT (datetime('now','localtime')),
  user_id   INTEGER,
  user_name TEXT,
  action    TEXT NOT NULL,
  entity    TEXT,
  entity_id TEXT,
  details   TEXT,
  ip        TEXT
);
CREATE INDEX IF NOT EXISTS idx_audit_at     ON audit_logs(at);
CREATE INDEX IF NOT EXISTS idx_audit_action ON audit_logs(action);
