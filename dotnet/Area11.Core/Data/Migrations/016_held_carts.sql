-- ============================================================================
-- 016 - held carts (hold / park a bill)  [Spec 5.3.24, 8.5.1, 12.1.5]
--
-- The original app kept parked carts in the browser (localStorage), so they died
-- with the browser profile. The rebuild stores them in the shop database, so a
-- parked bill survives a crash, a restart and a power cut.
--
-- Nothing here touches `sales`: a held cart is NOT a sale, it has no INV number,
-- no money and no stock movement. It only becomes a bill when the cashier resumes
-- it and presses Complete.
-- ============================================================================

CREATE TABLE IF NOT EXISTS held_carts (
  id               INTEGER PRIMARY KEY AUTOINCREMENT,
  code             TEXT NOT NULL UNIQUE,             -- H-0001
  created_at       TEXT NOT NULL DEFAULT (datetime('now','localtime')),
  updated_at       TEXT,
  user_id          INTEGER,                          -- who parked it
  customer_id      INTEGER REFERENCES customers(id), -- may be empty
  discount_percent REAL NOT NULL DEFAULT 0,
  note             TEXT,
  total_paisa      INTEGER NOT NULL DEFAULT 0,       -- what it was worth when parked
  items_count      INTEGER NOT NULL DEFAULT 0,
  status           TEXT NOT NULL DEFAULT 'held'      -- held | resumed | discarded
);

CREATE INDEX IF NOT EXISTS idx_held_carts_status ON held_carts(status, created_at);

CREATE TABLE IF NOT EXISTS held_cart_items (
  id              INTEGER PRIMARY KEY AUTOINCREMENT,
  cart_id         INTEGER NOT NULL REFERENCES held_carts(id) ON DELETE CASCADE,
  product_id      INTEGER NOT NULL REFERENCES products(id),
  name_snapshot   TEXT NOT NULL,
  unit            TEXT NOT NULL DEFAULT 'base',      -- base | strip | box
  quantity        REAL NOT NULL,
  unit_price_paisa INTEGER NOT NULL DEFAULT 0,       -- 0 = use the tier price when resumed
  position        INTEGER NOT NULL DEFAULT 0
);

CREATE INDEX IF NOT EXISTS idx_held_cart_items_cart ON held_cart_items(cart_id, position);
