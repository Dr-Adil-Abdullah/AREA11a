#!/usr/bin/env python3
"""Source-extracted monthly/Recovery SELECTs, synthetic memory only. Not C#/access/WPF execution."""
from pathlib import Path
import re
import sqlite3

ROOT = Path(__file__).resolve().parents[1]


def run():
    c = sqlite3.connect(':memory:')
    c.row_factory = sqlite3.Row
    c.execute('PRAGMA foreign_keys=ON')
    migrations = sorted((ROOT / 'dotnet/Area11.Core/Data/Migrations').glob('*.sql'))
    for p in migrations:
        c.executescript(p.read_text())
    recovery = (ROOT / 'dotnet/Area11.Core/Services/RecoveryService.cs').read_text()
    tax = (ROOT / 'dotnet/Area11.Core/Services/TaxService.cs').read_text()
    blocks = re.findall(r'@"(.*?)"', recovery, re.S)
    customers = next(b for b in blocks if b.strip().startswith('SELECT c.id'))
    bills = next(b for b in blocks if b.strip().startswith('SELECT s.customer_id'))
    credits = next(b for b in blocks if b.strip().startswith('SELECT customerId'))
    slip = next(b for b in blocks if b.strip().startswith('SELECT p.id'))
    tax_rows, refunds = re.findall(r'@"(.*?)"', tax, re.S)
    c.executescript("""
INSERT INTO users(id,name,role) VALUES(1,'Synthetic collector','owner'),(2,'Synthetic printer','cashier');
INSERT INTO customers(id,name,opening_balance_paisa,balance_paisa) VALUES(1,'Synthetic owing',100000,75000),(2,'Synthetic clear',0,0),(3,'Synthetic inactive',100,100);
UPDATE customers SET active=0 WHERE id=3;
INSERT INTO products(id,name,base_unit,retail_paisa,cost_paisa) VALUES(1,'Synthetic product','tablet',30000,20000);
INSERT INTO sales(id,code,date,customer_id,status,subtotal_paisa,discount_paisa,tax_paisa,total_paisa,due_paisa,user_id)
VALUES(1,'SYN-NOV','2026-11-04',1,'credit',60000,5000,1234,56000,56000,1),
(2,'SYN-OCT','2026-10-01',1,'credit',30000,0,0,30000,30000,1),
(3,'SYN-HOLD','2026-11-01',1,'hold',999999,0,99999,1099998,999999,1),
(4,'SYN-VOID','2026-11-01',1,'void',888888,0,88888,977776,888888,1);
INSERT INTO sale_items(id,sale_id,product_id,qty_base,unit_sold,qty_entered,unit_price_paisa,line_total_paisa,cost_paisa_at_sale)
VALUES(1,1,1,1,'base',1,30000,30000,20000);
INSERT INTO payments(id,date,customer_id,amount_paisa,method,user_id,note)
VALUES(1,'2026-11-05',1,25000,'cash',1,'Synthetic collection');
INSERT INTO payments(id,date,customer_id,sale_id,amount_paisa,method,user_id)
VALUES(2,'2026-11-04',1,1,10000,'cash',1);
INSERT INTO sale_returns(sale_id,sale_item_id,product_id,qty_base,refund_paisa,credit_reduced_paisa,date)
VALUES(1,1,1,1,5000,2000,'2026-11-06');
INSERT INTO provisional_returns(id,code,date,refund_paisa,status,user_id) VALUES(1,'SYN-PR','2026-11-07',3000,'pending',1);
INSERT INTO provisional_items(provisional_id,product_id,qty_base,refund_paisa,cost_paisa_at_return) VALUES(1,1,1,3000,20000);
""")
    before = list(c.iterdump())
    assert [r['customerId'] for r in c.execute(customers)] == [1]
    assert {r['id'] for r in c.execute(bills)} == {1, 2}
    events = list(c.execute(credits))
    assert len(events) == 2 and {r['kind'] for r in events} == {'collection', 'return'}
    collection = next(r for r in events if r['kind'] == 'collection')
    assert collection['amountPaisa'] == 25000 and collection['saleId'] is None
    returned = next(r for r in events if r['kind'] == 'return')
    assert returned['amountPaisa'] == 2000 and returned['saleId'] == 1
    payment = c.execute(slip, {'id': 1}).fetchone()
    assert payment['amountPaisa'] == 25000 and payment['userName'] == 'Synthetic collector'
    assert c.execute(slip, {'id': 2}).fetchone() is None
    assert c.execute(slip, {'id': '1 OR 1=1'}).fetchone() is None
    rows = list(c.execute(tax_rows, {'like': '2026-11%'}))
    assert len(rows) == 1
    r = rows[0]
    assert (r['bills'], r['netPaisa'], r['taxPaisa'], r['grossPaisa']) == (1, 55000, 1234, 56000)
    assert c.execute(refunds, {'like': '2026-11%'}).fetchone()[0] == 8000
    assert list(c.execute(tax_rows, {'like': '2026-12%'})) == []
    assert list(c.iterdump()) == before
    assert not c.execute('PRAGMA foreign_key_check').fetchall()
    print(f'PASS {len(migrations)} migrations/actual Recovery-monthly SELECTs: owing/active/status/month filters, typed bound slip/collector, collection vs sale payment and return source-bill, stored tax/discount/refund sums, empty month, read-only/FK preservation')
    print('NOT executed: C# account checks/settlement replay/Database.Tx/xUnit, WPF/dispatcher/clipboard/printer/Windows. Synthetic memory only.')


if __name__ == '__main__':
    run()
