-- 033: the first login is admin + area11 (owner's word, Nov 2026). The seed used to name
-- the owner-role account 'Owner'; an untouched shop still carries that exact name, and it
-- becomes 'admin'. Two shops are deliberately left alone: one whose owner account was
-- already renamed to something of its own (that choice stands), and one that already has a
-- different active 'admin' (two admins would make every sign-in a guess).
UPDATE users SET name = 'admin'
WHERE role = 'owner' AND active = 1 AND name = 'Owner'
  AND NOT EXISTS (
    SELECT 1 FROM users AS other
    WHERE other.id <> users.id AND lower(other.name) = 'admin' AND other.active = 1
  );
