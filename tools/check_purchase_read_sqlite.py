#!/usr/bin/env python3
"""Source-extracted invoice/expired SELECTs on synthetic in-memory SQLite only.
Not C#/Dapper/permission/projection/WPF execution. Accepts no shop-data path.
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
INSERT INTO users(id,name,role,active) VALUES(1,'Synthetic owner','owner',1);
INSERT INTO suppliers(id,name,balance_paisa) VALUES(1,'Synthetic depot',27035);
INSERT INTO products(id,name,base_unit,cost_paisa,retail_paisa) VALUES(1,'Synthetic medicine','tablet',12345,30000);
INSERT INTO purchases(id,code,supplier_id,supplier_invoice_no,subtotal_paisa,total_paisa,paid_paisa,due_paisa,user_id)
VALUES(1,'SYN-PINV',1,'SYN-PAPER',37035,37035,10000,27035,1);
INSERT INTO batches(id,product_id,batch_no,expiry_date,qty_base,cost_paisa,supplier_id,purchase_id,active)
VALUES(1,1,'P9','2000-01-31',3,12345,1,1,1),(2,1,'FUTURE','2999-01-31',5,12345,1,NULL,1),
      (3,1,'INACTIVE','2000-01-31',8,12345,1,NULL,0),(4,1,'EMPTY','2000-01-31',0,12345,1,NULL,1);
INSERT INTO purchase_items(purchase_id,product_id,batch_id,qty_entered,unit,qty_base,cost_paisa,line_total_paisa,batch_no,expiry_date)
VALUES(1,1,1,3,'base',3,12345,37035,'P9','2000-01-31');
INSERT INTO payments(supplier_id,purchase_id,method,amount_paisa) VALUES(1,1,'online',10000);
""")
    purchase = (ROOT / 'dotnet/Area11.Core/Services/PurchaseService.cs').read_text()
    header_sql = re.search(r'_db.QuerySingle<PurchaseInvoiceHeader>\(connection, @"(.*?)"', purchase, re.S)[1]
    lines_sql = re.search(r'_db.Query<PurchaseInvoiceLine>\(connection, @"(.*?)"', purchase, re.S)[1]
    supplier = (ROOT / 'dotnet/Area11.Core/Services/SupplierLedgerService.cs').read_text()
    expired_sql = re.search(r'_db.Query<ExpiredBatchRow>\(connection, @"(.*?)"', supplier, re.S)[1]
    before = list(c.iterdump())
    c.row_factory = sqlite3.Row
    header = dict(c.execute(header_sql, {'id': 1}).fetchone())
    lines = [dict(row) for row in c.execute(lines_sql, {'id': 1})]
    expired = [dict(row) for row in c.execute(expired_sql, {'limit': 500})]
    assert header['totalPaisa'] == 37035 and header['paidPaisa'] == 10000 and header['duePaisa'] == 27035
    assert header['paidPaisa'] + header['duePaisa'] == header['totalPaisa']
    assert len(lines) == 1 and lines[0]['lineTotalPaisa'] == 37035 and lines[0]['qtyEntered'] == 3
    assert len(expired) == 1 and expired[0]['batchId'] == 1
    assert expired[0]['costPaisa'] == 12345 and expired[0]['qtyBase'] == 3
    assert c.execute(header_sql, {'id': "1 OR 1=1"}).fetchone() is None
    assert list(c.iterdump()) == before
    print(f'PASS {len(migrations)} migrations/actual SELECTs: invoice line/header/Paid+Due, expired active/nonzero/date filter, bound record ID and read-only data preservation')
    # Independent display model documents the mask contract; C# tests are still required.
    for allowed in (False, True):
        display = {key: value if allowed else 'hidden' for key, value in header.items()
                   if key in ('totalPaisa', 'paidPaisa', 'duePaisa')}
        projected_cost = expired[0]['costPaisa'] if allowed else 0
        projected_value = round(expired[0]['qtyBase'] * projected_cost) if allowed else 0
        if allowed:
            assert display['totalPaisa'] == 37035 and projected_value == 37035
        else:
            assert set(display.values()) == {'hidden'} and projected_cost == projected_value == 0
    assert list(c.iterdump()) == before
    assert not c.execute('PRAGMA foreign_key_check').fetchall()
    print('PASS independent mask model keeps metadata and distinguishes hidden money from known amounts; source data/FK unchanged')
    print('NOT RUN: C# permission/projection/ReceiptDocument/return tray, Windows/print/PDF or production data')
    c.close()


if __name__ == '__main__':
    run()
