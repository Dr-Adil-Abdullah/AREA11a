-- ======================= REPS, DOCTORS AND LIFETIME PROFIT =======================
-- Spec 8.3.3 / §10.3.2: the company's medical representative - a name, a phone and the day he
-- visits. Three nullable columns on `companies`, not a second table, because the specification
-- speaks of **the** rep of a company; a company with two reps simply writes both names in one line
-- for now (see D-61 for the trade-off).
ALTER TABLE companies ADD COLUMN rep_name  TEXT;
ALTER TABLE companies ADD COLUMN rep_phone TEXT;

-- 1 = Monday ... 7 = Sunday; NULL = no fixed day. An integer sorts and never mistranslates.
ALTER TABLE companies ADD COLUMN visit_day INTEGER;

-- Spec 17.7 / §10.4.1: on whose prescription a bill was sold. The doctor is a **customer** whose
-- category is 'doctor' (17.1), so the shop has one list of people, one phone book and one rate tier
-- - a second doctors table would let the two lists disagree about the same man.
ALTER TABLE sales ADD COLUMN doctor_id INTEGER REFERENCES customers(id);

CREATE INDEX IF NOT EXISTS idx_sales_doctor ON sales(doctor_id);
