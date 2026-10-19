-- ======================= CHANGE (tendered / change) =======================
-- Grahak ne kitne diye aur kitne wapas kiye -- ye sale revenue nahi, sirf record.
ALTER TABLE sales ADD COLUMN change_paisa INTEGER NOT NULL DEFAULT 0;

-- ======================= STOCK ADJUSTMENTS =======================
-- Nuqsan/write-off aur ginti ki durusti ka apna record (stock_movements ke saath)
CREATE TABLE IF NOT EXISTS stock_adjustments (
  id           INTEGER PRIMARY KEY AUTOINCREMENT,
  date         TEXT NOT NULL DEFAULT (datetime('now','localtime')),
  product_id   INTEGER NOT NULL REFERENCES products(id),
  batch_id     INTEGER REFERENCES batches(id),
  direction    TEXT NOT NULL,              -- out (kami) | in (ziyada)
  qty_base     REAL NOT NULL DEFAULT 0,
  unit_entered TEXT,
  qty_entered  REAL,
  value_paisa  INTEGER NOT NULL DEFAULT 0, -- cost par value (report ke liye)
  reason       TEXT NOT NULL,              -- expired|damaged|lost|count_short|count_extra|other
  note         TEXT,
  user_id      INTEGER,
  movement_id  INTEGER REFERENCES stock_movements(id),
  created_at   TEXT NOT NULL DEFAULT (datetime('now','localtime'))
);
CREATE INDEX IF NOT EXISTS idx_adjust_date    ON stock_adjustments(date);
CREATE INDEX IF NOT EXISTS idx_adjust_product ON stock_adjustments(product_id);
