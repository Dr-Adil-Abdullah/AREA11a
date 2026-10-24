-- 001_core.sql
-- Area11 WPF foundation schema.
-- Mirrors the reference (Next.js) database: money is stored as INTEGER paisa,
-- timestamps as local text, and nothing is ever hard-deleted (active flags only).

CREATE TABLE IF NOT EXISTS users (
    id          INTEGER PRIMARY KEY AUTOINCREMENT,
    name        TEXT    NOT NULL UNIQUE,
    role        TEXT    NOT NULL,
    secret_hash TEXT    NOT NULL,
    active      INTEGER NOT NULL DEFAULT 1,
    created_at  TEXT    NOT NULL
);

CREATE TABLE IF NOT EXISTS settings (
    key   TEXT PRIMARY KEY,
    value TEXT NOT NULL
);

CREATE TABLE IF NOT EXISTS counters (
    kind  TEXT    PRIMARY KEY,
    value INTEGER NOT NULL DEFAULT 0
);

CREATE TABLE IF NOT EXISTS categories (
    id        INTEGER PRIMARY KEY AUTOINCREMENT,
    name      TEXT    NOT NULL,
    parent_id INTEGER REFERENCES categories(id),
    active    INTEGER NOT NULL DEFAULT 1
);

CREATE TABLE IF NOT EXISTS companies (
    id     INTEGER PRIMARY KEY AUTOINCREMENT,
    name   TEXT    NOT NULL,
    active INTEGER NOT NULL DEFAULT 1
);

CREATE TABLE IF NOT EXISTS products (
    id            INTEGER PRIMARY KEY AUTOINCREMENT,
    name          TEXT    NOT NULL,
    barcode       TEXT,
    generic       TEXT,
    category_id   INTEGER REFERENCES categories(id),
    company_id    INTEGER REFERENCES companies(id),
    box_strips    INTEGER NOT NULL DEFAULT 1,
    strip_units   INTEGER NOT NULL DEFAULT 1,
    cost_paisa    INTEGER NOT NULL DEFAULT 0,
    retail_paisa  INTEGER NOT NULL DEFAULT 0,
    vip_paisa     INTEGER NOT NULL DEFAULT 0,
    doctor_paisa  INTEGER NOT NULL DEFAULT 0,
    rack          TEXT,
    room          TEXT,
    reorder_level INTEGER NOT NULL DEFAULT 0,
    stock_base    INTEGER NOT NULL DEFAULT 0,
    photo_file    TEXT,
    active        INTEGER NOT NULL DEFAULT 1,
    created_at    TEXT    NOT NULL,
    updated_at    TEXT    NOT NULL
);

CREATE TABLE IF NOT EXISTS batches (
    id           INTEGER PRIMARY KEY AUTOINCREMENT,
    product_id   INTEGER NOT NULL REFERENCES products(id),
    batch_no     TEXT    NOT NULL,
    expiry       TEXT    NOT NULL,
    qty_base     INTEGER NOT NULL DEFAULT 0,
    cost_paisa   INTEGER NOT NULL DEFAULT 0,
    retail_paisa INTEGER NOT NULL DEFAULT 0,
    created_at   TEXT    NOT NULL
);

CREATE TABLE IF NOT EXISTS customers (
    id                 INTEGER PRIMARY KEY AUTOINCREMENT,
    name               TEXT    NOT NULL,
    phone              TEXT,
    type               TEXT    NOT NULL DEFAULT 'normal',
    credit_limit_paisa INTEGER NOT NULL DEFAULT 0,
    balance_paisa      INTEGER NOT NULL DEFAULT 0,
    notes              TEXT,
    photo_file         TEXT,
    active             INTEGER NOT NULL DEFAULT 1,
    created_at         TEXT    NOT NULL
);

CREATE TABLE IF NOT EXISTS suppliers (
    id            INTEGER PRIMARY KEY AUTOINCREMENT,
    name          TEXT    NOT NULL,
    agency        TEXT,
    phone         TEXT,
    balance_paisa INTEGER NOT NULL DEFAULT 0,
    active        INTEGER NOT NULL DEFAULT 1,
    created_at    TEXT    NOT NULL
);

CREATE TABLE IF NOT EXISTS purchases (
    id          INTEGER PRIMARY KEY AUTOINCREMENT,
    code        TEXT    NOT NULL UNIQUE,
    supplier_id INTEGER REFERENCES suppliers(id),
    total_paisa INTEGER NOT NULL DEFAULT 0,
    paid_paisa  INTEGER NOT NULL DEFAULT 0,
    note        TEXT,
    created_at  TEXT    NOT NULL,
    created_by  TEXT    NOT NULL
);

CREATE TABLE IF NOT EXISTS purchase_items (
    id             INTEGER PRIMARY KEY AUTOINCREMENT,
    purchase_id    INTEGER NOT NULL REFERENCES purchases(id),
    product_id     INTEGER NOT NULL REFERENCES products(id),
    batch_id       INTEGER REFERENCES batches(id),
    unit           TEXT    NOT NULL,
    qty_base       INTEGER NOT NULL,
    cost_paisa     INTEGER NOT NULL DEFAULT 0,
    retail_paisa   INTEGER NOT NULL DEFAULT 0,
    line_total_paisa INTEGER NOT NULL DEFAULT 0,
    is_sample      INTEGER NOT NULL DEFAULT 0
);

CREATE TABLE IF NOT EXISTS shifts (
    id                       INTEGER PRIMARY KEY AUTOINCREMENT,
    opened_at                TEXT    NOT NULL,
    closed_at                TEXT,
    opening_float_paisa      INTEGER NOT NULL DEFAULT 0,
    expected_paisa           INTEGER NOT NULL DEFAULT 0,
    counted_paisa            INTEGER,
    difference_paisa         INTEGER,
    opened_by                TEXT    NOT NULL,
    closed_by                TEXT,
    handed_to_name           TEXT,
    opened_from_shift_id     INTEGER REFERENCES shifts(id),
    note                     TEXT
);

CREATE TABLE IF NOT EXISTS sales (
    id              INTEGER PRIMARY KEY AUTOINCREMENT,
    code            TEXT    NOT NULL UNIQUE,
    customer_id     INTEGER REFERENCES customers(id),
    shift_id        INTEGER REFERENCES shifts(id),
    subtotal_paisa  INTEGER NOT NULL DEFAULT 0,
    discount_paisa  INTEGER NOT NULL DEFAULT 0,
    tax_paisa       INTEGER NOT NULL DEFAULT 0,
    round_paisa     INTEGER NOT NULL DEFAULT 0,
    total_paisa     INTEGER NOT NULL DEFAULT 0,
    paid_paisa      INTEGER NOT NULL DEFAULT 0,
    due_paisa       INTEGER NOT NULL DEFAULT 0,
    tendered_paisa  INTEGER NOT NULL DEFAULT 0,
    change_paisa    INTEGER NOT NULL DEFAULT 0,
    payment_method  TEXT    NOT NULL DEFAULT 'cash',
    status          TEXT    NOT NULL DEFAULT 'paid',
    note            TEXT,
    created_at      TEXT    NOT NULL,
    created_by      TEXT    NOT NULL
);

CREATE TABLE IF NOT EXISTS sale_items (
    id               INTEGER PRIMARY KEY AUTOINCREMENT,
    sale_id          INTEGER NOT NULL REFERENCES sales(id),
    product_id       INTEGER NOT NULL REFERENCES products(id),
    batch_id         INTEGER REFERENCES batches(id),
    unit             TEXT    NOT NULL,
    qty_base         INTEGER NOT NULL,
    unit_price_paisa INTEGER NOT NULL DEFAULT 0,
    discount_paisa   INTEGER NOT NULL DEFAULT 0,
    line_total_paisa INTEGER NOT NULL DEFAULT 0,
    cost_paisa       INTEGER NOT NULL DEFAULT 0
);

CREATE TABLE IF NOT EXISTS payments (
    id           INTEGER PRIMARY KEY AUTOINCREMENT,
    sale_id      INTEGER REFERENCES sales(id),
    purchase_id  INTEGER REFERENCES purchases(id),
    customer_id  INTEGER REFERENCES customers(id),
    supplier_id  INTEGER REFERENCES suppliers(id),
    amount_paisa INTEGER NOT NULL,
    method       TEXT    NOT NULL DEFAULT 'cash',
    note         TEXT,
    shift_id     INTEGER REFERENCES shifts(id),
    created_at   TEXT    NOT NULL,
    created_by   TEXT    NOT NULL
);

CREATE TABLE IF NOT EXISTS expenses (
    id           INTEGER PRIMARY KEY AUTOINCREMENT,
    kind         TEXT    NOT NULL DEFAULT 'expense',
    amount_paisa INTEGER NOT NULL,
    note         TEXT,
    shift_id     INTEGER REFERENCES shifts(id),
    created_at   TEXT    NOT NULL,
    created_by   TEXT    NOT NULL
);

CREATE TABLE IF NOT EXISTS sale_returns (
    id            INTEGER PRIMARY KEY AUTOINCREMENT,
    code          TEXT    NOT NULL UNIQUE,
    sale_id       INTEGER REFERENCES sales(id),
    qcode         TEXT UNIQUE,
    restock       INTEGER NOT NULL DEFAULT 1,
    refund_paisa  INTEGER NOT NULL DEFAULT 0,
    reason        TEXT,
    disposition   TEXT    NOT NULL DEFAULT 'restocked',
    created_at    TEXT    NOT NULL,
    created_by    TEXT    NOT NULL,
    disposed_at   TEXT,
    disposed_by   TEXT,
    disposed_note TEXT
);

CREATE TABLE IF NOT EXISTS return_items (
    id           INTEGER PRIMARY KEY AUTOINCREMENT,
    return_id    INTEGER NOT NULL REFERENCES sale_returns(id),
    sale_item_id INTEGER NOT NULL REFERENCES sale_items(id),
    product_id   INTEGER NOT NULL REFERENCES products(id),
    batch_id     INTEGER REFERENCES batches(id),
    qty_base     INTEGER NOT NULL,
    refund_paisa INTEGER NOT NULL DEFAULT 0
);

CREATE TABLE IF NOT EXISTS stock_movements (
    id         INTEGER PRIMARY KEY AUTOINCREMENT,
    product_id INTEGER NOT NULL REFERENCES products(id),
    batch_id   INTEGER REFERENCES batches(id),
    type       TEXT    NOT NULL,
    qty_base   INTEGER NOT NULL,
    note       TEXT,
    created_at TEXT    NOT NULL,
    created_by TEXT    NOT NULL
);

CREATE TABLE IF NOT EXISTS stock_adjustments (
    id           INTEGER PRIMARY KEY AUTOINCREMENT,
    product_id   INTEGER NOT NULL REFERENCES products(id),
    batch_id     INTEGER REFERENCES batches(id),
    direction    TEXT    NOT NULL,
    qty_base     INTEGER NOT NULL,
    reason       TEXT,
    value_paisa  INTEGER NOT NULL DEFAULT 0,
    note         TEXT,
    created_at   TEXT    NOT NULL,
    created_by   TEXT    NOT NULL
);

CREATE TABLE IF NOT EXISTS stock_takes (
    id         INTEGER PRIMARY KEY AUTOINCREMENT,
    code       TEXT    NOT NULL UNIQUE,
    status     TEXT    NOT NULL DEFAULT 'open',
    note       TEXT,
    created_at TEXT    NOT NULL,
    applied_at TEXT,
    created_by TEXT    NOT NULL
);

CREATE TABLE IF NOT EXISTS stock_take_lines (
    id                INTEGER PRIMARY KEY AUTOINCREMENT,
    take_id           INTEGER NOT NULL REFERENCES stock_takes(id),
    product_id        INTEGER NOT NULL REFERENCES products(id),
    batch_id          INTEGER REFERENCES batches(id),
    book_qty_base     INTEGER NOT NULL DEFAULT 0,
    counted_qty_base  INTEGER NOT NULL DEFAULT 0,
    difference_base   INTEGER NOT NULL DEFAULT 0,
    cost_paisa        INTEGER NOT NULL DEFAULT 0
);

CREATE TABLE IF NOT EXISTS alert_dismissals (
    id         INTEGER PRIMARY KEY AUTOINCREMENT,
    alert_key  TEXT    NOT NULL UNIQUE,
    reason     TEXT    NOT NULL,
    created_at TEXT    NOT NULL,
    created_by TEXT    NOT NULL
);

CREATE TABLE IF NOT EXISTS audit_logs (
    id          INTEGER PRIMARY KEY AUTOINCREMENT,
    at          TEXT    NOT NULL,
    user_name   TEXT    NOT NULL,
    module      TEXT    NOT NULL,
    action      TEXT    NOT NULL,
    entity      TEXT    NOT NULL,
    entity_id   INTEGER,
    old_value   TEXT,
    new_value   TEXT,
    detail      TEXT,
    ip          TEXT
);

CREATE TABLE IF NOT EXISTS custom_fields (
    id           INTEGER PRIMARY KEY AUTOINCREMENT,
    entity       TEXT    NOT NULL,
    label        TEXT    NOT NULL,
    field_type   TEXT    NOT NULL,
    required     INTEGER NOT NULL DEFAULT 0,
    active       INTEGER NOT NULL DEFAULT 1,
    options_json TEXT
);

CREATE TABLE IF NOT EXISTS custom_values (
    id        INTEGER PRIMARY KEY AUTOINCREMENT,
    field_id  INTEGER NOT NULL REFERENCES custom_fields(id),
    entity    TEXT    NOT NULL,
    entity_id INTEGER NOT NULL,
    value     TEXT
);

CREATE INDEX IF NOT EXISTS ix_products_name      ON products(name);
CREATE INDEX IF NOT EXISTS ix_products_barcode   ON products(barcode);
CREATE INDEX IF NOT EXISTS ix_batches_product    ON batches(product_id);
CREATE INDEX IF NOT EXISTS ix_batches_expiry     ON batches(expiry);
CREATE INDEX IF NOT EXISTS ix_sales_created      ON sales(created_at);
CREATE INDEX IF NOT EXISTS ix_sales_customer     ON sales(customer_id);
CREATE INDEX IF NOT EXISTS ix_sale_items_sale    ON sale_items(sale_id);
CREATE INDEX IF NOT EXISTS ix_payments_created   ON payments(created_at);
CREATE INDEX IF NOT EXISTS ix_movements_product  ON stock_movements(product_id);
CREATE INDEX IF NOT EXISTS ix_audit_at           ON audit_logs(at);
CREATE INDEX IF NOT EXISTS ix_audit_module       ON audit_logs(module);
