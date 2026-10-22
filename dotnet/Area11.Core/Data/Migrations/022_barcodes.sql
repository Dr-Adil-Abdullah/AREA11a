-- ======================= SEVERAL BARCODES PER MEDICINE =======================
-- Spec 8.1.5 / 35.6: a medicine may carry a different barcode for each pack size - one on the
-- tablet strip, another on the box, sometimes a third from the company's carton.
--
-- Migration 001 gave `products` a single `barcode` column (the original web app's design). It is
-- kept untouched, because an existing area11.db must open unchanged; the old values are copied
-- into the new table here (INSERT OR IGNORE, because `products.barcode` is UNIQUE but may be
-- duplicated by hand in an older file).
--
-- Rule D-00b: a barcode that is no longer printed is **put away** (`active = 0`), never deleted -
-- an old box still on the shelf must still be explainable in the black box.
CREATE TABLE IF NOT EXISTS product_barcodes (
  id         INTEGER PRIMARY KEY AUTOINCREMENT,
  product_id INTEGER NOT NULL REFERENCES products(id),
  barcode    TEXT    NOT NULL,
  pack       TEXT    NOT NULL DEFAULT 'base',   -- base | strip | box
  note       TEXT,
  active     INTEGER NOT NULL DEFAULT 1,
  created_at TEXT    NOT NULL DEFAULT (datetime('now','localtime'))
);

-- One live barcode belongs to one medicine. The index is partial so a put-away code can be given
-- to a new box later without a second row fighting the first.
CREATE UNIQUE INDEX IF NOT EXISTS idx_pb_code ON product_barcodes(barcode) WHERE active = 1;
CREATE INDEX IF NOT EXISTS idx_pb_product ON product_barcodes(product_id, active, pack);

INSERT OR IGNORE INTO product_barcodes(product_id, barcode, pack, note, active, created_at)
SELECT id, TRIM(barcode), 'base', 'copied from the old single barcode', 1,
       datetime('now', 'localtime')
FROM products
WHERE barcode IS NOT NULL AND TRIM(barcode) <> '';
