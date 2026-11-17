#!/usr/bin/env python3
"""Execute extracted batch SELECTs on synthetic memory-only SQLite; never shop data.
Does NOT execute C#/permissions/Dapper/VM or posting. Accepts no database path.
"""
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
    c.executescript("""
INSERT INTO suppliers(id,name) VALUES(1,'Synthetic depot');
INSERT INTO products(id,name,base_unit,box_strips,strip_tablets,cost_paisa,retail_paisa,active)
VALUES(1,'Synthetic medicine','tablet',2,10,500,900,1),
      (2,'Other medicine','tablet',1,5,700,900,1),
      (3,'No batches','tablet',1,1,250,900,1);
INSERT INTO batches(id,product_id,batch_no,expiry_date,qty_base,cost_paisa,supplier_id,active)
VALUES(1,1,'P10','2000-01-31',3,12345,1,1),(2,1,'NO-DATE',NULL,0,0,NULL,1),
      (3,1,'INACTIVE','1990-01-31',99,999,NULL,0),(4,2,'OTHER','2027-06-30',4,555,NULL,1);
""")
    source = (ROOT / 'dotnet/Area11.Core/Services/StockService.cs').read_text()
    listing = re.search(r'_db.Query<BatchRow>\(connection, @"(.*?)"', source, re.S)[1]
    preparation = source.split('public ServiceResult<BatchCostPreparation> PrepareBatchCost(', 1)[1]
    product_sql = re.search(r'_db.QuerySingle<ProductRow>\(connection, @"(.*?)"', preparation, re.S)[1]
    batch_sql = re.search(r'_db.QuerySingle<BatchDbRow>\(connection, @"(.*?)"', preparation, re.S)[1]
    before = list(c.iterdump())
    c.row_factory = sqlite3.Row
    rows = [dict(row) for row in c.execute(listing, {'productId': 1})]
    assert [row['id'] for row in rows] == [1, 2]  # inactive excluded; zero/no-date preserved
    assert rows[0]['supplierName'] == 'Synthetic depot' and rows[0]['costPaisa'] == 12345
    assert rows[0]['qtyBase'] == 3 and rows[1]['qtyBase'] == rows[1]['costPaisa'] == 0
    assert c.execute(listing, {'productId': '1 OR 1=1'}).fetchone() is None
    selected = c.execute(batch_sql, {'productId': 1, 'batchId': 1}).fetchone()
    assert selected['id'] == 1 and selected['costPaisa'] == 12345
    for bad in (3, 4, -1, '1 OR 1=1'):
        assert c.execute(batch_sql, {'productId': 1, 'batchId': bad}).fetchone() is None
    assert c.execute(batch_sql, {'productId': 3, 'batchId': None}).fetchone() is None
    # Core refuses null selection when this diagnostic SELECT finds any active batch.
    assert c.execute(batch_sql, {'productId': 1, 'batchId': None}).fetchone()['id'] == 1
    assert c.execute(product_sql, {'id': 1}).fetchone()['boxStrips'] == 2
    assert list(c.iterdump()) == before
    print(f'PASS {len(migrations)} migrations/extracted batch SELECTs: active/order/zero/no-date/supplier metadata, constrained product-batch IDs, bound parameters and read-only data')
    c.execute('UPDATE batches SET cost_paisa=777,qty_base=8 WHERE id=1')
    c.execute('UPDATE products SET box_strips=3,strip_tablets=12 WHERE id=1')
    updated = list(c.iterdump())
    fresh = c.execute(batch_sql, {'productId': 1, 'batchId': 1}).fetchone()
    product = c.execute(product_sql, {'id': 1}).fetchone()
    assert fresh['costPaisa'] == 777 and fresh['qtyBase'] == 8
    assert product['boxStrips'] == 3 and product['stripTablets'] == 12
    assert rows[0]['costPaisa'] == 12345  # old Python object remains stale, not revoked
    assert list(c.iterdump()) == updated
    assert not c.execute('PRAGMA foreign_key_check').fetchall()
    print('PASS next SELECT sees changed cost/stock/pack without altering data; old object remains stale; FK clean')
    print('NOT RUN: C# actor/module/cost policy, projections/preparation/refusal/VM, xUnit/Windows or shop data')
    c.close()


if __name__ == '__main__':
    run()
