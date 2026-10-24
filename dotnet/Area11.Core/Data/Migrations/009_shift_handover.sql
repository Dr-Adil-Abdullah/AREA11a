-- ======================= SHIFT HANDOVER (galla ka tabadla) =======================
-- Jab ek cashier ki shift khatam ho aur doosra aye:
--   * ginta hua cash agle shift ka opening float ban jata hai
--   * 'kis ne kis ko diya' mehfooz rehta hai
--   * ek handover parchi (slip) chhap sakte hain
ALTER TABLE shifts ADD COLUMN handed_to_user_id INTEGER;
ALTER TABLE shifts ADD COLUMN handed_to_name TEXT;
ALTER TABLE shifts ADD COLUMN opened_from_shift_id INTEGER;
