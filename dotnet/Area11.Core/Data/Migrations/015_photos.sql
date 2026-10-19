-- ======================= MULTIPLE PHOTOS (owner's order, 19 Oct 2026) =======================
-- The owner asked for several photos per product (2-3), and photos for suppliers and customers,
-- including pictures taken with a camera.
--
-- Rule U-19 stays: the FILE lives in data/photos and the database stores only the file name.
-- The old single `photo` column on products/customers (migration 005) is kept untouched so an
-- existing area11.db opens unchanged; new photos are recorded here, in order.
--
-- entity = product | customer | supplier | purchase | user
CREATE TABLE IF NOT EXISTS photos (
  id         INTEGER PRIMARY KEY AUTOINCREMENT,
  entity     TEXT    NOT NULL,
  entity_id  INTEGER NOT NULL,
  file_name  TEXT    NOT NULL,
  sort_order INTEGER NOT NULL DEFAULT 0,
  created_at TEXT    NOT NULL DEFAULT (datetime('now','localtime')),
  UNIQUE(entity, entity_id, file_name)
);
CREATE INDEX IF NOT EXISTS idx_photos_entity ON photos(entity, entity_id, sort_order);
