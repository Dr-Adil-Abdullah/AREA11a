#!/usr/bin/env python3
"""Actual source SQL on synthetic memory-only data. Not C#/projection/permission/Apply execution."""
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
    take = (ROOT / 'dotnet/Area11.Core/Services/StockTakeService.cs').read_text()
    stock = (ROOT / 'dotnet/Area11.Core/Services/StockService.cs').read_text()
    def block(source: str, prefix: str) -> str:
        return next(sql for sql in re.findall(r'@"(.*?)"', source, re.S) if sql.strip().startswith(prefix))
    freeze = block(take, 'INSERT INTO stock_take_items(stock_take_id, product_id, batch_id, system_qty_base')
    count = block(take, 'UPDATE stock_take_items\nSET counted_qty_base')
    totals = block(take, 'UPDATE stock_takes\nSET items_count')
    sheet = block(take, 'SELECT i.id AS id, i.product_id AS productId')
    apply_read = block(take, 'SELECT i.id, i.product_id AS productId, i.batch_id AS batchId')
    history = block(stock, 'SELECT a.id AS id, a.date AS date')
    today = block(stock, 'SELECT IFNULL(SUM(CASE WHEN direction')
    c.executescript("""
INSERT INTO users(id,name,role,active) VALUES(1,'Synthetic owner','owner',1);
INSERT INTO products(id,name,base_unit,cost_paisa,retail_paisa) VALUES
(1,'Frozen shortage','tablet',12345,30000),(2,'Frozen excess','tablet',200,900),(3,'Uncounted','tablet',0,900);
INSERT INTO batches(id,product_id,batch_no,qty_base,cost_paisa,active) VALUES
(1,1,'P11-A',10,12345,1),(2,2,'P11-B',10,200,1),(3,3,'P11-C',10,0,1);
INSERT INTO stock_takes(id,code,status,user_id) VALUES(1,'SYN-ST','open',1);
""")
    c.execute(freeze, {'sessionId': 1, 'room': None, 'categoryId': None, 'at': '2026-11-10 12:00:00'})
    for product, counted, frozen in ((1, 7, 12345), (2, 12, 200)):
        line = c.execute('SELECT id FROM stock_take_items WHERE product_id=?', (product,)).fetchone()[0]
        diff = counted - 10
        c.execute(count, {'counted': counted, 'unit': 'tablet', 'entered': counted, 'diff': diff,
                          'value': diff * frozen, 'userId': 1, 'at': '2026-11-10 12:01:00', 'note': 'synthetic', 'id': line})
    c.execute(totals, {'id': 1})
    assert c.execute('SELECT short_paisa,extra_paisa,diff_count FROM stock_takes').fetchone() == (37035, 400, 2)
    c.execute('UPDATE products SET cost_paisa=99999')
    c.execute('UPDATE batches SET cost_paisa=99999')
    before = list(c.iterdump())
    c.row_factory = sqlite3.Row
    rows = [dict(row) for row in c.execute(sheet, {'sessionId': 1})]
    shortage = next(row for row in rows if row['productId'] == 1)
    assert shortage['systemQtyBase'] == 10 and shortage['countedQtyBase'] == 7
    assert shortage['diffQtyBase'] == -3 and shortage['valuePaisa'] == -37035
    assert next(row for row in rows if row['productId'] == 3)['valuePaisa'] is None
    pending = [dict(row) for row in c.execute(apply_read, {'sessionId': 1})]
    assert next(row for row in pending if row['productId'] == 1)['costPaisa'] == 12345
    assert sum(row['valuePaisa'] or 0 for row in rows) == -36635
    assert not list(c.execute(sheet, {'sessionId': '1 OR 1=1'}))
    assert list(c.iterdump()) == before
    print(f'PASS {len(migrations)} migrations/actual freeze/count/totals/sheet/Apply-read SQL: shortage37035,excess400,net-36635; live99999 cannot rewrite frozen12345; uncounted NULL and bound ID retained')
    c.execute("INSERT INTO stock_adjustments(product_id,batch_id,direction,qty_base,value_paisa,reason) VALUES(1,1,'out',2,24690,'damaged')")
    before = list(c.iterdump())
    row = c.execute(history, {'limit': 200}).fetchone()
    assert row['valuePaisa'] == 24690 and row['qtyBase'] == 2
    assert c.execute(today).fetchone()[0] == 24690
    assert list(c.iterdump()) == before and not c.execute('PRAGMA foreign_key_check').fetchall()
    print('PASS actual stock history/today SELECTs preserve24690,metadata/data/FK; all output SELECTs read-only')
    print('NOT RUN: .NET current-reader/cost/main/output masking/transaction wrappers, C# Apply/Count/WriteOff, xUnit/Windows/print or shop data')
    c.close()


if __name__ == '__main__':
    run()
