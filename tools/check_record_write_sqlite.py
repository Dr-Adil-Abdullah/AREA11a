#!/usr/bin/env python3
"""Portion27 SQL-only checks on migrated synthetic SQLite. Not C# guards/transactions/WPF.
Executes the actual customer SELECT/UPDATE, product hide, actor and main-key queries;
Python rollback verifies SQLite trigger semantics, not Database.Tx execution.
"""
from pathlib import Path
import re
import sqlite3

ROOT = Path(__file__).resolve().parents[1]


def run():
    db = sqlite3.connect(':memory:')
    db.row_factory = sqlite3.Row
    db.execute('PRAGMA foreign_keys=ON;')
    migrations = sorted((ROOT / 'dotnet/Area11.Core/Data/Migrations').glob('*.sql'))
    for migration in migrations:
        db.executescript(migration.read_text())
    partners = (ROOT / 'dotnet/Area11.Core/Services/PartnerService.cs').read_text()
    customer = partners.split('private ServiceResult<CustomerEditResult> SaveCustomer(', 1)[1].split('public ServiceResult<List<Supplier>>', 1)[0]
    select = re.search(r'@"(\s*SELECT id AS id,.*?WHERE id = @id AND active = 1;)"', customer, re.S)[1]
    update = re.search(r'@"(\s*UPDATE customers.*?WHERE id = @id;)"', customer, re.S)[1]
    products = (ROOT / 'dotnet/Area11.Core/Services/ProductService.cs').read_text()
    hide = products.split('public ServiceResult Deactivate(', 1)[1].split('public ServiceResult CheckEditorAccess(', 1)[0]
    state_query = re.search(r'"(SELECT active FROM products WHERE id = @id;)"', hide)[1]
    hide_query = re.search(r'"(UPDATE products SET active = 0, updated_at = @at WHERE id = @id;)"', hide)[1]
    main_query = re.search(r'"(SELECT value FROM settings WHERE key = @key;)"', products)[1]
    access = (ROOT / 'dotnet/Area11.Core/Services/SnapshotReadAccess.cs').read_text()
    actor_query = re.search(r'"(SELECT id, name, role, active FROM users WHERE id = @id;)"', access)[1]
    db.executescript("""
INSERT INTO users(id,name,role,active) VALUES(1,'Synthetic stored owner','owner',1),(2,'Synthetic cashier','cashier',1);
INSERT INTO customers(id,name,phone,category,credit_limit_paisa,balance_paisa,active,created_at)
VALUES(1,'Synthetic customer','0300000000','normal',10000,5000,1,'2026-11-10');
INSERT INTO products(id,name,base_unit,cost_paisa,retail_paisa,active,created_at,updated_at)
VALUES(1,'Synthetic product','tablet',20000,30000,1,'2026-11-10','2026-11-10');
INSERT OR REPLACE INTO settings(key,value,updated_at) VALUES('profit.showMargins','true','2026-11-10');
""")
    row = db.execute(select, {'id': 1}).fetchone()
    assert row['balancePaisa'] == 5000 and row['creditLimitPaisa'] == 10000
    db.execute(update, {'id': 1, 'name': 'Synthetic correction', 'phone': '0300000001', 'category': 'normal', 'limit': 10000})
    assert db.execute(select, {'id': 1}).fetchone()['balancePaisa'] == 5000
    assert db.execute(select, {'id': '1 OR 1=1'}).fetchone() is None
    db.execute('UPDATE customers SET active=0 WHERE id=1')
    assert db.execute(select, {'id': 1}).fetchone() is None
    db.execute('UPDATE customers SET active=1 WHERE id=1')
    actor = db.execute(actor_query, {'id': 1}).fetchone()
    assert set(actor.keys()) == {'id', 'name', 'role', 'active'} and actor['name'] == 'Synthetic stored owner'
    db.execute("UPDATE users SET active=0,role='manager' WHERE id=1")
    actor = db.execute(actor_query, {'id': 1}).fetchone()
    assert actor['active'] == 0 and actor['role'] == 'manager'
    assert db.execute(actor_query, {'id': 99}).fetchone() is None
    for value in ('false', '0', 'not-json', 'true', '1'):
        db.execute("UPDATE settings SET value=? WHERE key='profit.showMargins'", (value,))
        assert db.execute(main_query, {'key': 'profit.showMargins'}).fetchone()['value'] == value
    assert db.execute(main_query, {'key': "profit.showMargins' OR 1=1 --"}).fetchone() is None
    assert db.execute(state_query, {'id': 1}).fetchone()['active'] == 1
    assert db.execute(state_query, {'id': 99}).fetchone() is None
    db.commit()
    before = list(db.iterdump())
    db.executescript("CREATE TRIGGER synthetic_hide_fault BEFORE UPDATE OF active ON products BEGIN SELECT RAISE(ABORT,'synthetic fault'); END;")
    try:
        with db:
            db.execute(update, {'id': 1, 'name': 'Must roll back', 'phone': '', 'category': 'normal', 'limit': 1})
            db.execute(hide_query, {'id': 1, 'at': '2026-11-10'})
        raise AssertionError('Expected SQL trigger error')
    except sqlite3.IntegrityError:
        pass
    db.execute('DROP TRIGGER synthetic_hide_fault;')
    assert list(db.iterdump()) == before
    db.execute(hide_query, {'id': 1, 'at': '2026-11-10'})
    assert db.execute(state_query, {'id': 1}).fetchone()['active'] == 0
    assert not db.execute('PRAGMA foreign_key_check').fetchall()
    print(f'PASS {len(migrations)} migrations; actual bound customer/actor/parent/main/hide SQL, balance preservation, missing/inactive targets, fresh raw main values and synthetic SQLite trigger rollback; NOT C# guards/Database.Tx/WPF/session proof')


if __name__ == '__main__':
    run()
