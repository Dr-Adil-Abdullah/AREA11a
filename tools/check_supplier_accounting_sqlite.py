#!/usr/bin/env python3
"""Actual Supplier SQL on synthetic memory SQLite only; NOT C#/permission/WPF execution.
No shop-data argument. Python drives transactions, not Database.Tx/NumberingService.
"""
from pathlib import Path
import re
import sqlite3

ROOT = Path(__file__).resolve().parents[1]


def run() -> None:
    c = sqlite3.connect(':memory:')
    c.row_factory = sqlite3.Row
    c.execute('PRAGMA foreign_keys=ON')
    for migration in sorted((ROOT / 'dotnet/Area11.Core/Data/Migrations').glob('*.sql')):
        c.executescript(migration.read_text())
    source = (ROOT / 'dotnet/Area11.Core/Services/SupplierLedgerService.cs').read_text()
    blocks = re.findall(r'@"(.*?)"', source, re.S)
    summary = re.search(r'private const string SummarySql = @"(.*?)";', source, re.S)[1]
    preparation = source.split('public ServiceResult<SupplierReturnPreparation> PrepareReturnLine(', 1)[1].split('// ----', 1)[0]
    product_sql, batch_sql = re.findall(r'@"(.*?)"', preparation, re.S)
    movements = re.findall(r'@"(.*?)"', source.split('private List<SupplierMovement> ReadMovements(', 1)[1].split('private const string SummarySql', 1)[0], re.S)
    assert len(movements) == 4
    payment = next(sql for sql in blocks if sql.strip().startswith('INSERT INTO payments('))
    header = next(sql for sql in blocks if sql.strip().startswith('INSERT INTO supplier_returns('))
    item = next(sql for sql in blocks if sql.strip().startswith('INSERT INTO supplier_return_items('))
    movement = next(sql for sql in blocks if sql.strip().startswith('INSERT INTO stock_movements('))
    balance = re.search(r'"(UPDATE suppliers SET balance_paisa.*?)"', source)[1]
    policy = (ROOT / 'dotnet/Area11.Core/Services/StockPolicy.cs').read_text()
    decrease = next(sql for sql in re.findall(r'@"(.*?)"', policy, re.S) if sql.strip().startswith('UPDATE batches'))
    audit = next(sql for sql in re.findall(r'@"(.*?)"', (ROOT / 'dotnet/Area11.Core/Services/AuditService.cs').read_text(), re.S)
                 if sql.strip().startswith('INSERT INTO audit_logs(at, user_id'))
    c.executescript("""
INSERT INTO users(id,name,role,active) VALUES(1,'Synthetic supplier actor','owner',1);
INSERT INTO suppliers(id,name,balance_paisa) VALUES(1,'Synthetic depot',150000);
INSERT INTO products(id,name,base_unit,box_strips,strip_tablets,cost_paisa,active) VALUES
(1,'Synthetic tablet','tablet',2,10,20000,1),(2,'Inactive product','tablet',2,10,7000,0);
INSERT INTO purchases(id,code,supplier_id,total_paisa,subtotal_paisa,paid_paisa,due_paisa,user_id)
VALUES(1,'SYN-BUY',1,200000,200000,50000,150000,1);
INSERT INTO batches(id,product_id,batch_no,qty_base,cost_paisa,active) VALUES
(1,1,'SYN-RETURN',10,12345,1),(2,1,'SYN-ZERO',2,0,1),(3,1,'SYN-INACTIVE',3,4000,0);
INSERT INTO payments(supplier_id,purchase_id,method,amount_paisa) VALUES(1,1,'cash',50000);
""")
    before = list(c.iterdump())
    product = dict(c.execute(product_sql, {'id': 1}).fetchone())
    batch = dict(c.execute(batch_sql, {'id': 1, 'productId': 1}).fetchone())
    assert (product['boxStrips'], product['stripTablets'], batch['qtyBase'], batch['costPaisa']) == (2, 10, 10, 12345)
    assert c.execute(product_sql, {'id': 2}).fetchone() is None
    for bid, pid in [(1, 2), (3, 1), (9999, 1), ('1 OR 1=1', 1)]:
        assert c.execute(batch_sql, {'id': bid, 'productId': pid}).fetchone() is None
    zero_batch = dict(c.execute(batch_sql, {'id': 2, 'productId': 1}).fetchone())
    assert zero_batch['costPaisa'] == 0  # fallback itself remains C#'s responsibility

    def reconcile(expected: int) -> None:
        row = dict(c.execute(summary + ' WHERE s.id=@id;', {'id': 1}).fetchone())
        assert row['payablePaisa'] == expected
        assert row['openingPaisa'] + row['purchasesPaisa'] - row['paidPaisa'] - row['creditedPaisa'] == expected
        lines = [dict(line) for sql in movements for line in c.execute(sql, {'id': 1})]
        assert sum(line['debitPaisa'] - line['creditPaisa'] for line in lines) == expected

    reconcile(150000)
    assert list(c.iterdump()) == before
    stamp = '2026-11-10 12:00:00'
    c.execute(payment, dict(at=stamp, method='cash', amount=25000, supplierId=1, userId=1, note='Synthetic pay'))
    c.execute(balance, dict(balance=125000, id=1))
    c.commit()
    reconcile(125000)  # purchase-tied payment not double-counted

    def write_return(code: str, settled: int, qty: int) -> None:
        total = qty * 12345
        c.execute(header, dict(code=code, at=stamp, supplierId=1, reason='expired', note='Synthetic SR', total=total, settled=settled, userId=1))
        rid = c.execute('SELECT last_insert_rowid()').fetchone()[0]
        c.execute(item, dict(returnId=rid, productId=1, batchId=1, qty=qty, cost=12345, total=total))
        assert c.execute(decrease, dict(id=1, productId=1, quantity=qty)).rowcount == 1
        c.execute(movement, dict(productId=1, batchId=1, qty=-qty, returnId=rid, code=code, note='Synthetic SR', userId=1, at=stamp))
        if not settled:
            old = c.execute('SELECT balance_paisa FROM suppliers WHERE id=1').fetchone()[0]
            c.execute(balance, dict(balance=old-total, id=1))

    c.execute('BEGIN IMMEDIATE')
    write_return('SYN-SR-CREDIT', 0, 2)
    c.commit()
    reconcile(100310)
    c.execute('BEGIN IMMEDIATE')
    write_return('SYN-SR-CASH', 1, 1)
    c.commit()
    reconcile(100310)
    assert c.execute('SELECT qty_base FROM batches WHERE id=1').fetchone()[0] == 7
    assert c.execute(decrease, dict(id=1, productId=1, quantity=8)).rowcount == 0
    c.commit()
    c.executescript("CREATE TRIGGER synthetic_audit_fault BEFORE INSERT ON audit_logs BEGIN SELECT RAISE(ABORT, 'synthetic-fault'); END;")
    before_fault = list(c.iterdump())
    c.execute('BEGIN IMMEDIATE')
    try:
        write_return('SYN-SR-ROLLBACK', 0, 1)
        c.execute(audit, dict(at=stamp, userId=1, userName='Synthetic supplier actor', action='supplier_return', module='Suppliers',
                              entity='supplier_returns', entityId='3', oldValue='100310', newValue='87965', details='Synthetic'))
    except sqlite3.IntegrityError:
        c.rollback()
    else:
        raise AssertionError('synthetic audit fault did not fire')
    assert list(c.iterdump()) == before_fault
    assert not c.execute('PRAGMA foreign_key_check').fetchall()
    print('PASS actual Supplier preparation/summary/4 movement SELECTs and Pay/SR/StockPolicy SQL: bound IDs, active filters, purchase-paid reconciliation, credit vs cash, decrement guard, audit-fault SQL rollback, read-only/FK preservation')
    print('NOT executed: C# masking/authorization/Database.Tx/numbering, WPF, concurrency, Windows. Synthetic memory only.')


if __name__ == '__main__':
    run()
