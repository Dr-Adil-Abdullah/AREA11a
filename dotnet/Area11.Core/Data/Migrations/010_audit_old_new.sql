-- ======================= BLACKBOX: PURANA vs NAYA (Spec 2) =======================
-- Har log me ab ye bhi mehfooz hoga:
--   module     = kaunse hissa (module) se kaam hua (POS / Inventory / Settings ...)
--   old_value  = tabdeeli se PEHLE ki value (sirf badli hui felds)
--   new_value  = tabdeeli ke BAAD ki value
-- Is tarah "kis ne kya badla" ka poora saboot rehta hai.
ALTER TABLE audit_logs ADD COLUMN module TEXT;
ALTER TABLE audit_logs ADD COLUMN old_value TEXT;
ALTER TABLE audit_logs ADD COLUMN new_value TEXT;
CREATE INDEX IF NOT EXISTS idx_audit_entity ON audit_logs(entity, entity_id);
CREATE INDEX IF NOT EXISTS idx_audit_module ON audit_logs(module);
