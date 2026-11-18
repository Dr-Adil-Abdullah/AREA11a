#!/usr/bin/env python3
"""Real roster SELECTs on synthetic in-memory SQLite. Not C# access/OwnerAccess/WPF execution."""
from pathlib import Path
import re
import sqlite3

ROOT = Path(__file__).resolve().parents[1]


def run():
    c = sqlite3.connect(':memory:')
    c.row_factory = sqlite3.Row
    c.execute('PRAGMA foreign_keys=ON')
    migrations = sorted((ROOT / 'dotnet/Area11.Core/Data/Migrations').glob('*.sql'))
    for path in migrations:
        c.executescript(path.read_text())
    auth = (ROOT / 'dotnet/Area11.Core/Services/AuthService.cs').read_text()
    roster_part = auth.split('public ServiceResult<List<User>> ListUsers(User? actor)', 1)[1].split('public ServiceResult<List<User>> ListHandoverPeople', 1)[0]
    picker_part = auth.split('public ServiceResult<List<User>> ListHandoverPeople(User? actor)', 1)[1].split('public ServiceResult CheckStaffAccess', 1)[0]
    roster = re.search(r'"(SELECT id, name, role, active FROM users.*?)"', roster_part)[1]
    picker = re.search(r'"(SELECT id, name, role, active FROM users.*?)"', picker_part)[1]
    owner = re.search(r'"(SELECT id, name, role, active FROM users.*?)"',
                      (ROOT / 'dotnet/Area11.Core/Services/OwnerAccess.cs').read_text())[1]
    c.executescript("""
INSERT INTO users(id,name,role,active,password_hash,pin_hash) VALUES
(1,'Synthetic owner','owner',1,'synthetic-secret-hash','synthetic-pin-hash'),
(2,'Synthetic cashier','cashier',1,'other-secret','other-pin'),
(3,'Synthetic off','manager',0,NULL,'off-pin'),
(4,'Synthetic manager','manager',1,NULL,'manager-pin');
""")
    before = list(c.iterdump())
    rows = list(c.execute(roster))
    assert [r['id'] for r in rows] == [1, 4, 3, 2]
    assert all(set(r.keys()) == {'id', 'name', 'role', 'active'} for r in rows)
    assert any(r['active'] == 0 for r in rows), 'Owner admin sees inactive accounts too'
    for current, expected in [(1, {2, 4}), (2, {1, 4}), (4, {1, 2})]:
        picked = list(c.execute(picker, {'id': current}))
        assert {r['id'] for r in picked} == expected
        assert all(r['active'] == 1 for r in picked)
        assert all(set(r.keys()) == {'id', 'name', 'role', 'active'} for r in picked)
    assert c.execute(owner, {'id': '1 OR 1=1'}).fetchone() is None
    assert c.execute(owner, {'id': 1}).fetchone()['role'] == 'owner'
    assert list(c.iterdump()) == before
    c.execute('UPDATE users SET active=0 WHERE id=4')
    after_change = list(c.iterdump())
    assert {r['id'] for r in c.execute(picker, {'id': 2})} == {1}, 'next picker sees deactivation'
    assert len(list(c.execute(roster))) == 4
    assert list(c.iterdump()) == after_change
    assert not c.execute('PRAGMA foreign_key_check').fetchall()
    print(f'PASS {len(migrations)} migrations/actual administrative roster + Cash picker SELECTs: role/name ordering, inactive admin vs active-other picker, bound current-ID lookup, four nonsecret fields, next-read deactivation, read-only/FK preservation')
    print('NOT executed: C# Owner/Cash/parent-tab checks or permission defaults, WPF/timers/staff commands/handover posting/xUnit/Windows. Synthetic memory only.')


if __name__ == '__main__':
    run()
