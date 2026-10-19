-- ======================= PROVISIONAL (RUSH-TIME) RETURNS =======================
-- Jab gahak ke paas bill na ho aur rush ho: cash wapas kar do, maal QUARANTINE me
-- rakho, aur "pending" note laga do jab tak asal bill na mil jaye (spec 9.2.2/9.2.3).
CREATE TABLE IF NOT EXISTS provisional_returns (
  id             INTEGER PRIMARY KEY AUTOINCREMENT,
  code           TEXT NOT NULL,
  date           TEXT NOT NULL DEFAULT (datetime('now','localtime')),
  phone          TEXT,
  reason         TEXT,
  notes          TEXT,
  refund_paisa   INTEGER NOT NULL DEFAULT 0,
  status         TEXT NOT NULL DEFAULT 'pending',   -- pending | linked | cancelled
  linked_sale_id INTEGER REFERENCES sales(id),
  linked_at      TEXT,
  linked_by      INTEGER,
  user_id        INTEGER,
  created_at     TEXT NOT NULL DEFAULT (datetime('now','localtime'))
);
CREATE INDEX IF NOT EXISTS idx_prov_status ON provisional_returns(status);
CREATE INDEX IF NOT EXISTS idx_prov_date   ON provisional_returns(date);

CREATE TABLE IF NOT EXISTS provisional_items (
  id             INTEGER PRIMARY KEY AUTOINCREMENT,
  provisional_id INTEGER NOT NULL REFERENCES provisional_returns(id),
  product_id     INTEGER NOT NULL REFERENCES products(id),
  qty_base       REAL NOT NULL DEFAULT 0,
  created_at     TEXT NOT NULL DEFAULT (datetime('now','localtime'))
);
CREATE INDEX IF NOT EXISTS idx_prov_items ON provisional_items(provisional_id);
