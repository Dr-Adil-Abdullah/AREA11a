-- ======================= BILL TIME CORRECTION (v0.68.0) =======================
-- Har bill par ye bhi mehfooz hoga:
--   time_offset_seconds = us waqt online time aur computer clock ka farq (seconds me),
--                         jab ye bill bana tha. NULL = v0.68.0 se pehle ka bill, jis ka
--                         farq maloom nahi (us waqt ye column tha hi nahi).
-- Is tarah jab time server baad me batata hai ke computer ki ghari ghalat thi, to dukandaar
-- unhi billon ko ek sath theek kar sakta hai jo ghalat ghari par bane thay - `sales.date`
-- theek waqt hota hai (report isi par hai), `sales.created_at` ghalat stamp rehta hai.
ALTER TABLE sales ADD COLUMN time_offset_seconds INTEGER NULL;
