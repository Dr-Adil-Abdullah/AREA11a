-- ======================= STOCK-TAKE (ginti) =======================
-- Dukan band kar ke ginti karte waqt:
--   1. Ek "session" kholo (kitab ke hisaab ka stock freeze ho jata hai)
--   2. Har dawa gin kar likho
--   3. Farq (kam / zyada) dekho
--   4. "Apply" karte hi asal stock durust ho jata hai + movement + nuqsan ki value
CREATE TABLE IF NOT EXISTS stock_takes (
  id           INTEGER PRIMARY KEY AUTOINCREMENT,
  code         TEXT NOT NULL,                -- ST-0001
  date         TEXT NOT NULL DEFAULT (datetime('now','localtime')),
  status       TEXT NOT NULL DEFAULT 'open', -- open | applied | cancelled
  room         TEXT,                         -- sirf is kamre ki ginti (optional)
  category_id  INTEGER,                      -- sirf is category ki ginti (optional)
  note         TEXT,
  items_count  INTEGER NOT NULL DEFAULT 0,   -- kitni dawayen ginti me thin
  diff_count   INTEGER NOT NULL DEFAULT 0,   -- kitni me farq nikla
  short_paisa  INTEGER NOT NULL DEFAULT 0,   -- kami ki qeemat (cost par)
  extra_paisa  INTEGER NOT NULL DEFAULT 0,   -- zyada ki qeemat (cost par)
  user_id      INTEGER,
  applied_at   TEXT,
  applied_by   INTEGER,
  created_at   TEXT NOT NULL DEFAULT (datetime('now','localtime'))
);
CREATE INDEX IF NOT EXISTS idx_stake_date ON stock_takes(date);

CREATE TABLE IF NOT EXISTS stock_take_items (
  id             INTEGER PRIMARY KEY AUTOINCREMENT,
  stock_take_id  INTEGER NOT NULL REFERENCES stock_takes(id) ON DELETE CASCADE,
  product_id     INTEGER NOT NULL REFERENCES products(id),
  batch_id       INTEGER REFERENCES batches(id),
  system_qty_base  REAL NOT NULL DEFAULT 0,  -- session kholte waqt kitab me kitna tha
  counted_qty_base REAL,                     -- ginti me kitna mila (null = abhi nahi gina)
  unit_entered   TEXT,                       -- box | strip | base
  qty_entered    REAL,                       -- owner ne jis unit me likha
  diff_qty_base  REAL,                       -- counted - system (kami manfi)
  value_paisa    INTEGER,                    -- farq ki qeemat (cost par)
  created_at     TEXT NOT NULL DEFAULT (datetime('now','localtime'))
);
CREATE INDEX IF NOT EXISTS idx_stake_items ON stock_take_items(stock_take_id);
CREATE INDEX IF NOT EXISTS idx_stake_items_p ON stock_take_items(product_id);
