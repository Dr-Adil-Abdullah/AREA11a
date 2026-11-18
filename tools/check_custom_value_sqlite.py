#!/usr/bin/env python3
"""Extract actual custom answer/parent SQL on synthetic memory SQLite; not C#/authorization proof."""
from pathlib import Path
import re
import sqlite3

ROOT = Path(__file__).resolve().parents[1]


def run():
    source = (ROOT / 'dotnet/Area11.Core/Services/CustomFieldService.cs').read_text()
    reader = source.split('private void RequireValueRecord(', 1)[1].split('public static bool Normalize(', 1)[0]
    tables = re.findall(r'CustomFieldEntity\.\w+ => "(\w+)"', reader)
    assert set(tables) == {'products', 'customers', 'suppliers', 'sales', 'purchases', 'expenses'}
    assert '"SELECT COUNT(1) FROM " + table + " WHERE id=@id;"' in reader
    count_prefix, count_suffix = re.search(r'"(SELECT COUNT\(1\) FROM )" \+ table \+ "( WHERE id=@id;)"', reader).groups()
    blocks = re.findall(r'@"([^"]*)"', source, re.S)
    values = next(b for b in blocks if 'FROM custom_values\nWHERE entity = @entity' in b)
    describe = next(b for b in blocks if 'FROM custom_values v' in b)
    actor = re.search(r'"(SELECT id, name, role, active FROM users WHERE id = @id;)"',
                      source.split('private User RequireValueActor(', 1)[1]).group(1)
    c = sqlite3.connect(':memory:')
    c.row_factory = sqlite3.Row
    c.execute('PRAGMA foreign_keys=ON')
    migrations = sorted((ROOT / 'dotnet/Area11.Core/Data/Migrations').glob('*.sql'))
    for path in migrations:
        c.executescript(path.read_text())
    c.executescript("""
INSERT INTO users(id,name,role) VALUES(1,'Synthetic owner','owner');
INSERT INTO customers(id,name) VALUES(1,'Synthetic customer');
INSERT INTO products(id,name) VALUES(1,'Synthetic product');
INSERT INTO suppliers(id,name) VALUES(1,'Synthetic supplier');
INSERT INTO sales(id,code) VALUES(1,'SYNTHETIC-S');
INSERT INTO purchases(id,code) VALUES(1,'SYNTHETIC-P');
INSERT INTO expenses(id,title) VALUES(1,'Synthetic expense');
INSERT INTO custom_fields(id,entity,label,type,active,sort) VALUES
 (1,'customer','Synthetic hidden question','text',0,2),
 (2,'customer','Synthetic first question','text',1,1),
 (3,'supplier','Synthetic supplier question','text',1,1);
INSERT INTO custom_values(field_id,entity,entity_id,value) VALUES
 (1,'customer',1,'old synthetic answer'),(2,'customer',1,'first synthetic answer'),
 (3,'supplier',1,'other synthetic answer');
""")
    before = list(c.iterdump())
    for table in tables:
        sql = count_prefix + table + count_suffix
        assert c.execute(sql, {'id': 1}).fetchone()[0] == 1
        for missing in (0, -1, 999, '1 OR 1=1'):
            assert c.execute(sql, {'id': missing}).fetchone()[0] == 0
    args = dict(entity='customer', entityId=1)
    assert {r['fieldId'] for r in c.execute(values, args)} == {1, 2}
    assert [r['fieldId'] for r in c.execute(describe, args)] == [2, 1], 'hidden definitions retain historical labels/order'
    for query in (values, describe):
        assert not c.execute(query, dict(entity='customer', entityId=99)).fetchall()
        assert not c.execute(query, dict(entity="customer' OR 1=1 --", entityId=1)).fetchall()
    assert set(c.execute(actor, {'id': 1}).fetchone().keys()) == {'id', 'name', 'role', 'active'}
    assert not c.execute(actor, {'id': '1 OR 1=1'}).fetchall()
    assert list(c.iterdump()) == before
    c.execute('UPDATE customers SET active=0 WHERE id=1')
    assert c.execute(count_prefix + 'customers' + count_suffix, {'id': 1}).fetchone()[0] == 1
    assert len(c.execute(describe, args).fetchall()) == 2
    c.execute('DELETE FROM customers WHERE id=1')
    assert c.execute(count_prefix + 'customers' + count_suffix, {'id': 1}).fetchone()[0] == 0
    assert len(c.execute(values, args).fetchall()) == 2, 'SQL alone still returns orphan answers; C# must enforce the preceding parent check'
    assert not c.execute('PRAGMA foreign_key_check').fetchall()
    print(f'PASS {len(migrations)} migrations/actual six-table parent count, current actor projection, custom values/history SELECTs: bound IDs/entity, hidden history/order, inactive vs missing parent, read-only dump/FK')
    print('NOT executed: C# parent check/actor/grants/creation rollback/audit/file cleanup, xUnit, WPF/editor timers, authenticated session or Windows.')
    c.close()


if __name__ == '__main__':
    run()
