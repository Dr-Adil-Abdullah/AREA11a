-- ======================= SUPPLIER RETURNS (maal wapas supplier ko) =======================
--Expiry / damaged / galat maal supplier ko lota dete hain:
--   * batch se stock nikal jata hai (movement: supplier_return)
--   * supplier ka balance GHAT jata hai (hum ne dena kam ho gaya)
CREATE TABLE IF NOT EXISTS supplier_returns (
  id           INTEGER PRIMARY KEY AUTOINCREMENT,
  code         TEXT NOT NULL UNIQUE,        -- SR-0001
  date         TEXT NOT NULL DEFAULT (datetime('now','localtime')),
  supplier_id  INTEGER NOT NULL REFERENCES suppliers(id),
  purchase_id  INTEGER REFERENCES purchases(id),
  reason       TEXT,                        -- expired | damaged | wrong_item | overstock | other
  note         TEXT,
  total_paisa  INTEGER NOT NULL DEFAULT 0,  -- kitni qeemat ka maal gaya
  settled      INTEGER NOT NULL DEFAULT 0,  -- 0 = credit note (balance kam) | 1 = naqad wapas mila
  user_id      INTEGER,
  created_at   TEXT NOT NULL DEFAULT (datetime('now','localtime'))
);
CREATE INDEX IF NOT EXISTS idx_sret_supplier ON supplier_returns(supplier_id, date);

CREATE TABLE IF NOT EXISTS supplier_return_items (
  id           INTEGER PRIMARY KEY AUTOINCREMENT,
  return_id    INTEGER NOT NULL REFERENCES supplier_returns(id) ON DELETE CASCADE,
  product_id   INTEGER NOT NULL REFERENCES products(id),
  batch_id     INTEGER REFERENCES batches(id),
  qty_base     REAL NOT NULL DEFAULT 0,
  cost_paisa   INTEGER NOT NULL DEFAULT 0,
  line_total_paisa INTEGER NOT NULL DEFAULT 0
);
CREATE INDEX IF NOT EXISTS idx_sret_items ON supplier_return_items(return_id);
