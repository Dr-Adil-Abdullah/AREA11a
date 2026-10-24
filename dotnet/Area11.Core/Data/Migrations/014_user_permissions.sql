-- ======================= PER-USER PERMISSIONS (owner's order, 19 Oct 2026) =======================
-- The owner decides, per person, which parts of the program that person may see
-- (sales, audit log, profit, reports, purchase, settings, ...).
--
-- Design:
--   * one row per (user, module) that the owner has explicitly changed;
--   * NO row for a module = the person's ROLE DEFAULT applies (owner / manager / cashier);
--   * "Reset to role default" simply deletes that user's rows;
--   * the Owner account is always allowed everything in code and can never be locked out.
--
-- module_key examples: pos, sales, returns, customers, products, purchases, suppliers,
--                      stock, stocktake, cash, reports, profit, audit, import, backup, settings, staff, sync
CREATE TABLE IF NOT EXISTS user_permissions (
  user_id    INTEGER NOT NULL REFERENCES users(id),
  module_key TEXT    NOT NULL,
  allowed    INTEGER NOT NULL DEFAULT 1,
  updated_at TEXT    NOT NULL DEFAULT (datetime('now','localtime')),
  updated_by TEXT,
  PRIMARY KEY (user_id, module_key)
);
CREATE INDEX IF NOT EXISTS idx_user_permissions_user ON user_permissions(user_id);
