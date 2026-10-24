-- ======================= QUARANTINE: PAKKA NUMBER + POORA PATA (U-34) =======================
-- Malik ka hukum: "har saman jo quarantine me hoga us ka ek apna SPECIFIC NUMBER hoga
--  jo change nahi ho sakta, aur jab hum history dekhein to sara saman show ho
--  ke kaun sa saman kidhar gaya."
--
-- Is liye har (na-restok kiye gaye) wapsi ke item ko ek MUSTAQIL code milta hai:
--   Q-0001, Q-0002 ...   (kabhi nahi badalta)
-- aur 'disposition' batata hai ke woh saman AAKHIR gaya kahan:
--   'quarantine' = abhi quarantine me (ALERT jata rahe ga)
--   'restocked'  = janch ke baad wapas shelf / stock me
--   'expired'    = expiry ya kharaab -- write-off (becha nahi jaye ga)
--   'sold'       = quarantine se hi bech diya gaya
ALTER TABLE sale_returns ADD COLUMN qcode TEXT;
ALTER TABLE sale_returns ADD COLUMN disposition TEXT NOT NULL DEFAULT 'quarantine';
ALTER TABLE sale_returns ADD COLUMN disposed_at TEXT;
ALTER TABLE sale_returns ADD COLUMN disposed_by INTEGER;
ALTER TABLE sale_returns ADD COLUMN disposed_note TEXT;
CREATE INDEX IF NOT EXISTS idx_returns_qcode ON sale_returns(qcode);
CREATE INDEX IF NOT EXISTS idx_returns_disposition ON sale_returns(disposition);
