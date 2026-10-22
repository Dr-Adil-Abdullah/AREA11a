-- ======================= PHOTOS: HIDE, NEVER DELETE =======================
-- Migration 015 gave the shop several photos per product, customer and supplier, with the file on
-- disk and only its name in the database (rule U-19). It had no way to take a wrong picture out of
-- the list.
--
-- Rule D-00b of the owner's file: **nothing is deleted**. A photo that is no longer wanted is
-- marked inactive, so the list stops showing it, the file stays in data/photos, and the black box
-- keeps the name of whoever hid it. %ProgramData%\Area11 is never cleaned by the program.
ALTER TABLE photos ADD COLUMN active INTEGER NOT NULL DEFAULT 1;

CREATE INDEX IF NOT EXISTS idx_photos_active ON photos(entity, entity_id, active);
