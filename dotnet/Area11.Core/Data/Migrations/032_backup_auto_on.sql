-- 032: the automatic backup is on unless the shop said otherwise (owner's word, Nov 2026:
-- losing the data is the biggest fear). The key shipped default-off and was never presented as
-- a described choice, so a stored false is an untouched shop, not a decision - it becomes true.
-- The Backup page keeps its one-tick switch, so any shop that wants it off can still say so.
UPDATE settings SET value = 'true', updated_at = datetime('now','localtime')
WHERE key = 'backup.autoEnabled' AND value = 'false';
