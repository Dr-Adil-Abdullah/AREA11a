-- ======================= ALERT DISMISS (malik ki wajah) =======================
-- Malik ka hukum (U-31): koi bhi alert sirf OWNER khatam kar sakta hai aur
-- usay WAJAH (reason) likhni lazmi hai. Wajah yahan mehfooz rehti hai aur
-- black-box (audit) me bhi darj hoti hai.
--   alert_key  = "negstock:12" (dawa id) ya "provisional:5" (bina-bill wapsi id)
--   Ek alert ek hi dafa khatam hota hai (UNIQUE).
CREATE TABLE IF NOT EXISTS alert_dismissals (
  id          INTEGER PRIMARY KEY AUTOINCREMENT,
  alert_key   TEXT NOT NULL UNIQUE,
  alert_type  TEXT NOT NULL,
  entity_id   INTEGER,
  reason      TEXT NOT NULL,
  user_id     INTEGER,
  user_name   TEXT,
  created_at  TEXT NOT NULL DEFAULT (datetime('now','localtime'))
);
CREATE INDEX IF NOT EXISTS idx_alert_dismiss_type ON alert_dismissals(alert_type);
