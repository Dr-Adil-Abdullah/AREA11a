#!/usr/bin/env python3
"""Actual inspector SELECTs and coherent read snapshot on temporary synthetic SQLite.
Not C#/Dapper, authorization service, WPF, or permission gesture execution.
Accepts no database path; never opens shop data.
"""
from pathlib import Path
import re
import sqlite3
import tempfile

ROOT = Path(__file__).resolve().parents[1]


def run() -> None:
    source = (ROOT / 'dotnet/Area11.Core/Services/PermissionInspectionService.cs').read_text()
    owner_sql = re.search(r'QueryFirstOrDefault<User>\(\s*"([^"]+)"', source)[1]
    switch_sql = re.search(r'QueryFirstOrDefault<string>\(\s*"([^"]+)"', source)[1]
    assignment_sql = re.search(r'Query<AssignmentRow>\(@"(.*?)"', source, re.S)[1]
    with tempfile.TemporaryDirectory(prefix='area11-permission-inspector-') as folder:
        file = Path(folder) / 'synthetic.db'
        reader = sqlite3.connect(file, isolation_level=None)
        reader.execute('PRAGMA foreign_keys=ON')
        migrations = sorted((ROOT / 'dotnet/Area11.Core/Data/Migrations').glob('*.sql'))
        for migration in migrations:
            reader.executescript(migration.read_text())
        reader.execute('PRAGMA journal_mode=WAL')
        reader.executescript("""
INSERT INTO users(id,name,role,active) VALUES(1,'Synthetic owner','owner',1),(2,'Synthetic cashier','cashier',1),(3,'Inactive manager','manager',0);
INSERT INTO settings(key,value) VALUES('profit.showMargins','true');
INSERT INTO user_permissions(user_id,module_key,allowed) VALUES(2,'purchasecost',1),(2,'profit',0);
""")
        writer = sqlite3.connect(file, isolation_level=None)
        before = list(reader.iterdump())
        owner = reader.execute(owner_sql, {'id': 1}).fetchone()
        assert owner == (1, 'Synthetic owner', 'owner', 1)
        rows = reader.execute(assignment_sql, {'module': 'purchasecost'}).fetchall()
        assert len(rows) == 3
        assert next(row for row in rows if row[0] == 2)[-1] == 1
        assert next(row for row in rows if row[0] == 3)[3:] == (0, None)
        profit = reader.execute(assignment_sql, {'module': 'profit'}).fetchall()
        assert next(row for row in profit if row[0] == 2)[-1] == 0
        assert list(reader.iterdump()) == before
        print(f'PASS {len(migrations)} migrations/actual inspector SQL: nullable defaults, independent grants, inactive account listing; SELECTs do not change data')

        reader.execute('BEGIN DEFERRED')
        assert reader.execute(owner_sql, {'id': 1}).fetchone()[3] == 1
        writer.execute("UPDATE users SET active=0 WHERE id=1")
        writer.execute("UPDATE user_permissions SET allowed=0 WHERE user_id=2 AND module_key='purchasecost'")
        writer.execute("UPDATE settings SET value='false' WHERE key='profit.showMargins'")
        # Existing read snapshot is consistent, NOT an instant-revocation guarantee.
        assert reader.execute(switch_sql, {'key': 'profit.showMargins'}).fetchone() == ('true',)
        assert next(row for row in reader.execute(assignment_sql, {'module': 'purchasecost'}) if row[0] == 2)[-1] == 1
        reader.execute('COMMIT')
        assert reader.execute(owner_sql, {'id': 1}).fetchone()[3] == 0
        assert reader.execute(switch_sql, {'key': 'profit.showMargins'}).fetchone() == ('false',)
        assert next(row for row in reader.execute(assignment_sql, {'module': 'purchasecost'}) if row[0] == 2)[-1] == 0
        print('PASS actual SQL: one coherent read snapshot; next transaction sees owner deactivation, revocation and main OFF, not cached defaults')
        probe = reader.execute(assignment_sql, {'module': "purchasecost' OR 1=1 --"}).fetchall()
        assert len(probe) == 3 and all(row[-1] is None for row in probe)
        assert {row[0] for row in probe} == {1, 2, 3}
        assert not reader.execute('PRAGMA foreign_key_check').fetchall()
        print('PASS bound module parameter cannot change join scope; FK clean (Core also rejects unknown module before SQL)')
        writer.close()
        reader.close()
    print('NOT RUN: PermissionInspectionService C#/Dapper, owner/session authentication, Windows live modal, double-click or keyboard; no shop data')


if __name__ == '__main__':
    run()
