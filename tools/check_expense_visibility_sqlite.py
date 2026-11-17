#!/usr/bin/env python3
"""Actual expense/drawing SELECTs on synthetic memory-only SQLite; not C# policy/projection/VM."""
from pathlib import Path
import re
import sqlite3

ROOT = Path(__file__).resolve().parents[1]


def run() -> None:
    c = sqlite3.connect(':memory:')
    c.execute('PRAGMA foreign_keys=ON')
    migrations = sorted((ROOT / 'dotnet/Area11.Core/Data/Migrations').glob('*.sql'))
    for migration in migrations:
        c.executescript(migration.read_text())
    source = (ROOT / 'dotnet/Area11.Core/Services/ExpenseService.cs').read_text()
    queries = re.findall(r'@"(.*?)"', source, re.S)
    listing = next(q for q in queries if q.strip().startswith("SELECT e.id AS id, 'expense'"))
    expense = next(q for q in queries if q.strip().startswith('SELECT e.title AS title'))
    drawing = next(q for q in queries if q.strip().startswith("SELECT 'Owner drawing' AS title"))
    today = next(q for q in queries if q.strip().startswith('SELECT\n  IFNULL((SELECT SUM(e.amount_paisa)'))
    c.executescript("""
INSERT INTO users(id,name,role,active) VALUES(1,'Synthetic owner','owner',1);
INSERT INTO products(id,name,base_unit,cost_paisa,retail_paisa) VALUES(1,'Drawing medicine','tablet',12345,30000);
INSERT INTO batches(id,product_id,batch_no,qty_base,cost_paisa) VALUES(1,1,'P12',7,12345);
INSERT INTO expenses(id,date,category,title,amount_paisa,user_id) VALUES(1,'2026-11-10 12:00:00','rent','Synthetic rent',25000,1);
INSERT INTO owner_drawings(id,date,type,amount_paisa,note,user_id) VALUES
(1,'2026-11-10 12:00:00','cash',7000,'Synthetic cash',1),
(2,'2026-11-10 12:00:00','goods',37035,'3 tablet Drawing medicine (P12)',1);
""")
    before = list(c.iterdump())
    c.row_factory = sqlite3.Row
    rows = [dict(row) for row in c.execute(listing, {'limit': 200})]
    assert [r['kind'] for r in rows] == ['expense', 'drawing_goods', 'drawing_cash']
    assert [r['amountPaisa'] for r in rows] == [25000, 37035, 7000]
    amounts = dict(c.execute(today, {'from': '2026-11-10', 'to': '2026-11-10 23:59:59'}).fetchone())
    assert amounts == {'expensePaisa': 25000, 'drawingCashPaisa': 7000, 'drawingGoodsCostPaisa': 37035, 'entries': 3}
    assert amounts['expensePaisa'] + amounts['drawingCashPaisa'] == 32000  # goods never in cash sum
    assert c.execute(expense, {'id': 1}).fetchone()['amountPaisa'] == 25000
    assert c.execute(drawing, {'id': 1, 'type': 'cash'}).fetchone()['amountPaisa'] == 7000
    assert c.execute(drawing, {'id': 2, 'type': 'goods'}).fetchone()['amountPaisa'] == 37035
    assert c.execute(drawing, {'id': 2, 'type': 'cash'}).fetchone() is None
    assert c.execute(drawing, {'id': 1, 'type': 'goods'}).fetchone() is None
    assert c.execute(drawing, {'id': '2 OR 1=1', 'type': 'goods'}).fetchone() is None
    assert c.execute(drawing, {'id': 2, 'type': "goods' OR 1=1 --"}).fetchone() is None
    assert list(c.iterdump()) == before
    print(f'PASS {len(migrations)} migrations/actual list/today/voucher SELECTs: stable mixed order, cash32000 separate from goods37035, entry3 and typed/bound ID prevent kind confusion; unchanged read dump')
    c.execute("UPDATE expense_categories SET type='stock' WHERE name='rent'")
    after_tag = list(c.iterdump())
    assert c.execute(expense, {'id': 1}).fetchone()['amountPaisa'] == 25000
    assert c.execute('SELECT qty_base FROM batches').fetchone()[0] == 7
    assert c.execute('SELECT COUNT(*) FROM purchases').fetchone()[0] == 0
    assert list(c.iterdump()) == after_tag and not c.execute('PRAGMA foreign_key_check').fetchall()
    print('PASS stock-category label leaves expense amount, shelf quantity and purchases unchanged; FK clean')
    print('NOT RUN: ExpenseService C#/Dapper/current-reader/cost-main/masks/write wrappers, xUnit/Windows/voucher rendering or shop data')
    c.close()


if __name__ == '__main__':
    run()
