#!/usr/bin/env python3
"""Source-extracted audit SELECTs on synthetic memory SQLite. Not C#/permission/WPF execution."""
from pathlib import Path
import re
import sqlite3

ROOT = Path(__file__).resolve().parents[1]


def run():
    c = sqlite3.connect(':memory:')
    migrations = sorted((ROOT / 'dotnet/Area11.Core/Data/Migrations').glob('*.sql'))
    c.execute('PRAGMA foreign_keys=ON')
    for path in migrations:
        c.executescript(path.read_text())
    c.execute("INSERT INTO users(id,name,role,active) VALUES(1,'Synthetic owner','owner',1)")
    c.executemany("INSERT INTO audit_logs(id,at,user_id,user_name,module,action,entity,entity_id,old_value,new_value,details) VALUES(?, ?, 1, 'Synthetic owner', 'SYN', 'changed', 'products', '14', ?, ?, ?)", [
        (1, '2026-11-10 12:00:00', 'old-secret14', 'new-secret14', 'valuation-secret14'),
        (2, '2026-11-10 13:00:00', 'other-old', 'other-new', 'other-detail')])
    source = (ROOT / 'dotnet/Area11.Core/Services/AuditHistoryService.cs').read_text()
    blocks = re.findall(r'@"((?:[^"]|"")*)"', source, re.S)
    page = next(b for b in blocks if 'SELECT id AS id,' in b and 'LIMIT @limit' in b)
    count = next(b for b in blocks if 'SELECT COUNT(1)' in b)
    c.row_factory = sqlite3.Row
    before = list(c.iterdump())
    args = dict(detailsKnown=0, search='', pattern='%%', module='SYN', action='',
                from_='', until='', limit=200)
    # `from` is a Python keyword but the original SQL uses the exact bound name.
    args['from'] = args.pop('from_')
    rows = [dict(r) for r in c.execute(page, args)]
    assert [r['id'] for r in rows] == [2, 1]
    assert all(r['oldValue'] == r['newValue'] == r['entity'] == r['entityId'] == '' for r in rows)
    assert all(r['details'] == 'hidden' and r['action'] == 'changed' for r in rows)
    assert c.execute(count, args).fetchone()[0] == 2
    for term in ('old-secret14', 'new-secret14', 'valuation-secret14', 'products'):
        args.update(search=term, pattern='%' + term + '%')
        assert not c.execute(page, args).fetchall()
        assert c.execute(count, args).fetchone()[0] == 0
    args.update(search='changed', pattern='%changed%')
    assert len(c.execute(page, args).fetchall()) == c.execute(count, args).fetchone()[0] == 2
    args.update(detailsKnown=1, search='old-secret14', pattern='%old-secret14%')
    row = dict(c.execute(page, args).fetchone())
    assert row['oldValue'] == 'old-secret14' and row['newValue'] == 'new-secret14'
    assert row['details'] == 'valuation-secret14' and row['entityId'] == '14'
    assert c.execute(count, args).fetchone()[0] == 1
    args.update(search='', pattern='%%', module="SYN' OR 1=1 --")
    assert not c.execute(page, args).fetchall()
    args.update(module='SYN', limit=1)
    assert len(c.execute(page, args).fetchall()) == 1 and c.execute(count, args).fetchone()[0] == 2
    assert list(c.iterdump()) == before and not c.execute('PRAGMA foreign_key_check').fetchall()
    print(f'PASS {len(migrations)} migrations/actual audit page+count SQL: metadata vs raw projections, hidden-field search/count exclusion, metadata search, ordering/limit, bound filter, exact read-only dump/FK')
    print('NOT RUN: C# current actor/grants/CSV/snapshot/timers, imports/workbooks, encryption/restore or Windows')
    c.close()


if __name__ == '__main__':
    run()
