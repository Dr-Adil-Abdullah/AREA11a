-- ======================= SALE RETURNS (Phase 2) =======================
-- Har return alag row. Asal sale_items kabhi nahi badalti (RULE S-06/Z).
CREATE TABLE IF NOT EXISTS sale_returns (
  id           INTEGER PRIMARY KEY AUTOINCREMENT,
  date         TEXT NOT NULL DEFAULT (datetime('now','localtime')),
  sale_id      INTEGER NOT NULL REFERENCES sales(id),
  sale_item_id INTEGER NOT NULL REFERENCES sale_items(id),
  product_id   INTEGER NOT NULL REFERENCES products(id),
  batch_id     INTEGER REFERENCES batches(id),
  qty_base     REAL NOT NULL DEFAULT 0,
  refund_paisa INTEGER NOT NULL DEFAULT 0,
  restock      INTEGER NOT NULL DEFAULT 0,   -- 1 = wapas shelf stock, 0 = quarantine (stock me nahi)
  reason       TEXT,
  user_id      INTEGER,
  created_at   TEXT NOT NULL DEFAULT (datetime('now','localtime'))
);
CREATE INDEX IF NOT EXISTS idx_returns_sale ON sale_returns(sale_id);
CREATE INDEX IF NOT EXISTS idx_returns_date ON sale_returns(date);
