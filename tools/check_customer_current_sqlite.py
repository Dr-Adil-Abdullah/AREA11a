#!/usr/bin/env python3
"""Actual Customer/Rep SQL, synthetic memory only. Not C#/permission/WPF execution."""
from pathlib import Path
import re
import sqlite3

ROOT = Path(__file__).resolve().parents[1]


def run() -> None:
    c = sqlite3.connect(':memory:')
    c.row_factory = sqlite3.Row
    c.execute('PRAGMA foreign_keys=ON')
    migrations = sorted((ROOT / 'dotnet/Area11.Core/Data/Migrations').glob('*.sql'))
    for migration in migrations:
        c.executescript(migration.read_text())
    source = (ROOT / 'dotnet/Area11.Core/Services/CustomerLedgerService.cs').read_text()
    rep_source = (ROOT / 'dotnet/Area11.Core/Services/RepService.cs').read_text()
    blocks = re.findall(r'@"(.*?)"', source, re.S)
    payment = next(sql for sql in blocks if sql.strip().startswith('INSERT INTO payments('))
    balance = re.search(r'"(UPDATE customers SET balance_paisa.*?)"', source)[1]
    receipt = re.search(r'_db.QuerySingle<PaymentRow>\(connection, @"(.*?)"', source, re.S)[1]
    summary = re.search(r'_db.QuerySingle<CustomerSummary>\(connection, @"(.*?)"', source, re.S)[1]
    doctors = re.search(r'private const string DoctorSql = @"(.*?)";', rep_source, re.S)[1]
    profits = re.search(r'private const string CustomerProfitSql = @"(.*?)";', rep_source, re.S)[1]
    rep_update = next(sql for sql in re.findall(r'@"(.*?)"', rep_source, re.S) if sql.strip().startswith('UPDATE companies'))
    audit = next(sql for sql in re.findall(r'@"(.*?)"', (ROOT / 'dotnet/Area11.Core/Services/AuditService.cs').read_text(), re.S)
                 if sql.strip().startswith('INSERT INTO audit_logs(at, user_id'))
    c.executescript("""
INSERT INTO users(id,name,role,active) VALUES(1,'Synthetic actor','owner',1);
INSERT INTO customers(id,name,opening_balance_paisa,balance_paisa,category) VALUES
(1,'Synthetic khata',100000,100000,'normal'),(2,'Synthetic doctor',0,0,'doctor');
INSERT INTO companies(id,name,active) VALUES(1,'Synthetic company',1);
INSERT INTO products(id,name,base_unit,cost_paisa,retail_paisa) VALUES(1,'Synthetic product','tablet',20000,30000);
INSERT INTO sales(id,code,customer_id,doctor_id,total_paisa,subtotal_paisa,paid_paisa,status,user_id)
VALUES(1,'SYN-SALE',2,2,60000,60000,60000,'paid',1);
INSERT INTO sale_items(id,sale_id,product_id,qty_base,unit_sold,qty_entered,unit_price_paisa,line_total_paisa,cost_paisa_at_sale)
VALUES(1,1,1,1,'base',1,30000,30000,20000),(2,1,1,1,'base',1,30000,30000,20000);
INSERT INTO payments(id,customer_id,sale_id,amount_paisa,method) VALUES(1,2,1,60000,'cash');
""")
    before = list(c.iterdump())
    assert c.execute(summary, {'id': 1}).fetchone()['balancePaisa'] == 100000
    assert c.execute(receipt, {'id': 1}).fetchone() is None, 'sale payment is not a collection receipt'
    for sql, field in [(doctors, 'doctorId'), (profits, 'customerId')]:
        row = dict(c.execute(sql).fetchone())
        assert row[field] == 2 and row['salesPaisa'] == 60000 and row['profitPaisa'] == 20000
    assert list(c.iterdump()) == before
    stamp = '2026-11-10 12:00:00'
    c.execute(payment, dict(at=stamp, method='online', amount=25000, customerId=1, userId=1, note='Synthetic collection'))
    payment_id = c.execute('SELECT last_insert_rowid()').fetchone()[0]
    c.execute(balance, dict(balance=75000, id=1))
    c.commit()
    row = dict(c.execute(receipt, {'id': payment_id}).fetchone())
    assert row['amountPaisa'] == 25000 and row['balancePaisa'] == 75000 and row['openingPaisa'] == 100000
    assert c.execute(receipt, {'id': '1 OR 1=1'}).fetchone() is None
    c.executescript("""
INSERT INTO sale_returns(sale_id,sale_item_id,product_id,qty_base,refund_paisa,restock,credit_reduced_paisa)
VALUES(1,1,1,1,30000,1,0);
""")
    for sql in (doctors, profits):
        row = dict(c.execute(sql).fetchone())
        assert row['profitPaisa'] == 10000  # 60000 -40000 -30000 +20000
    c.executescript("CREATE TRIGGER synthetic_customer_audit_fault BEFORE INSERT ON audit_logs BEGIN SELECT RAISE(ABORT,'synthetic-fault'); END;")
    for operation in ('collection', 'rep'):
        before_fault = list(c.iterdump())
        c.execute('BEGIN IMMEDIATE')
        try:
            if operation == 'collection':
                c.execute(payment, dict(at=stamp, method='cash', amount=10000, customerId=1, userId=1, note='Synthetic failed collection'))
                c.execute(balance, dict(balance=65000, id=1))
            else:
                c.execute(rep_update, dict(name='Synthetic rep', phone='0300', day=1, companyId=1))
            c.execute(audit, dict(at=stamp, userId=1, userName='Synthetic actor', action='synthetic', module='Synthetic',
                                  entity='synthetic', entityId='1', oldValue='before', newValue='after', details='Synthetic fault'))
        except sqlite3.IntegrityError:
            c.rollback()
        else:
            raise AssertionError('audit fault did not fire')
        assert list(c.iterdump()) == before_fault
    assert not c.execute('PRAGMA foreign_key_check').fetchall()
    print(f'PASS {len(migrations)} migrations/actual Customer-Rep SQL: bound typed receipt, collection balance, doctor/customer profit without join multiplication, refund/restock formula, collection+rep audit-fault SQL rollback, read-only/FK preservation')
    print('NOT executed: C# current-account/financial masking/Database.Tx/xUnit, WPF/timers/receipt layout, concurrency or Windows. Synthetic memory only.')


if __name__ == '__main__':
    run()
