-- ======================= WAPSI KA PAKKA NUMBER (U-35) =======================
-- Malik ka hukum: "har wapsi ka ek apna specific number ho jo change na ho sake;
--  jab hum history dekhein to poora saman show ho ke kiska saman kidhar gaya".
-- Is liye har wapsi (ek hi martaba ki poori wapsi) ko ek MUSTAQIL code milta hai:
--   RET-0001, RET-0002 ...
-- Ek wapsi me jitni bhi dawayen hon, sab par YAHI code hota hai -- taake
-- receipt (رسید) poori wapsi ki ek hi parchi ho, aur number kabhi na badle.
ALTER TABLE sale_returns ADD COLUMN code TEXT;
CREATE INDEX IF NOT EXISTS idx_returns_code ON sale_returns(code);
