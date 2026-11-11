-- 031: the shop's own expense categories (owner's word, Nov 2026).
-- Two main types: running = the cost of keeping the shop open (rent, power, salary);
-- stock = goods bought for the shelf (a label only - stock itself still arrives via Purchases).
-- Append-only: the eight historical words seed as running, so every old expense stays valid.

CREATE TABLE IF NOT EXISTS expense_categories (
  id         INTEGER PRIMARY KEY AUTOINCREMENT,
  name       TEXT NOT NULL COLLATE NOCASE UNIQUE,
  type       TEXT NOT NULL DEFAULT 'running' CHECK (type IN ('running', 'stock')),
  active     INTEGER NOT NULL DEFAULT 1,
  created_at TEXT NOT NULL DEFAULT (datetime('now','localtime'))
);

INSERT OR IGNORE INTO expense_categories(name, type, active) VALUES
  ('rent', 'running', 1),
  ('electricity', 'running', 1),
  ('salary', 'running', 1),
  ('transport', 'running', 1),
  ('tea', 'running', 1),
  ('packing', 'running', 1),
  ('repair', 'running', 1),
  ('other', 'running', 1);
