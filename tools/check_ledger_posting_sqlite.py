#!/usr/bin/env python3
"""Actual ledger posting/drawer SQL on synthetic SQLite; Python owns guards/Tx/threads.
NOT C# Database.Tx/permission/retry/UI execution or process/power-loss certification.
"""
from pathlib import Path
import re
import sqlite3
import tempfile
from concurrent.futures import ThreadPoolExecutor
from threading import Barrier

ROOT = Path(__file__).resolve().parents[1]
SERVICES = ROOT / 'dotnet/Area11.Core/Services'


def quoted(source, prefix):
    found = [a or b for a,b in re.findall(r'@"([^"]*)"|"([^"\n]*)"',source) if (a or b).strip().startswith(prefix)]
    assert len(found)==1,(prefix,len(found))
    return found[0]


def run():
    blocks = []
    for file,start,end in (
        ('CustomerLedgerService.cs','public ServiceResult<CollectionResult> Collect(','public ServiceResult<ReceiptDocument> Statement('),
        ('SupplierLedgerService.cs','public ServiceResult<SupplierPayment> Pay(','public ServiceResult<SupplierReturnResult> Return(')):
        blocks.append((SERVICES/file).read_text().split(start,1)[1].split(end,1)[0])
    inserts = [quoted(b,'INSERT INTO payments') for b in blocks]
    updates = [quoted(blocks[i],'UPDATE '+table) for i,table in enumerate(('customers','suppliers'))]
    balances = [quoted(blocks[0],'SELECT id, name, balance_paisa AS balancePaisa'),quoted(blocks[1],'SELECT id, name, balance_paisa AS payablePaisa')]
    audit = quoted((SERVICES/'AuditService.cs').read_text().split('public void Record(',1)[1].split('public void RecordChange(',1)[0],'INSERT INTO audit_logs')
    drawer = quoted((SERVICES/'ShiftService.cs').read_text().split('private void FillDrawer(',1)[1],'SELECT')
    migrations = sorted((ROOT/'dotnet/Area11.Core/Data/Migrations').glob('*.sql'))
    def seed(path=':memory:'):
        db=sqlite3.connect(path);db.row_factory=sqlite3.Row;db.execute('PRAGMA foreign_keys=ON')
        for p in migrations:db.executescript(p.read_text())
        db.executescript("""
INSERT INTO users(id,name,role,active) VALUES(1,'Synthetic posting owner','owner',1);
INSERT INTO customers(id,name,opening_balance_paisa,balance_paisa) VALUES(1,'Synthetic customer',100000,100000);
INSERT INTO suppliers(id,name,opening_balance_paisa,balance_paisa) VALUES(1,'Synthetic supplier',100000,100000);
""");db.commit();return db
    def post(db,side,amount=30000,method='cash'):
        try:
            db.execute('BEGIN IMMEDIATE')
            row=db.execute(balances[side],{'id':1}).fetchone()
            # Deliberate Python copy of settled balance guard; not execution of C# validation.
            if row is None or amount<=0 or amount>row[2]:db.rollback();return False
            values={'id':1,'customerId':1,'supplierId':1,'userId':1,'userName':'Synthetic posting owner',
                    'at':'2026-11-10 12:00:00','method':method,'amount':amount,'note':'Synthetic SQL probe',
                    'balance':row[2]-amount,'module':'Suppliers' if side else 'Customers',
                    'action':'supplier_pay' if side else 'customer_collect','entity':'suppliers' if side else 'customers',
                    'entityId':'1','oldValue':str(row[2]),'newValue':str(row[2]-amount),'details':'Synthetic SQL probe'}
            db.execute(inserts[side],values);db.execute(updates[side],values);db.execute(audit,values);db.commit();return True
        except sqlite3.DatabaseError:db.rollback();return False
    def totals(db):
        r=db.execute(drawer,{'from':'2026-11-10 00:00:00','to':'2026-11-10 23:59:59'}).fetchone()
        return r['cashCollectionsPaisa'],r['supplierCashOutPaisa']
    stages=('BEFORE INSERT ON payments','AFTER INSERT ON payments','BEFORE UPDATE OF balance_paisa ON {table}',
            'AFTER UPDATE OF balance_paisa ON {table}','BEFORE INSERT ON audit_logs','AFTER INSERT ON audit_logs')
    faults=0
    for side in (0,1):
        for stage in stages:
            db=seed();before=list(db.iterdump());event=stage.format(table='suppliers' if side else 'customers')
            db.executescript("CREATE TRIGGER posting_fault "+event+" BEGIN SELECT RAISE(ABORT,'synthetic posting fault'); END;")
            assert not post(db,side);assert totals(db)==(0,0)
            db.execute('DROP TRIGGER posting_fault');assert list(db.iterdump())==before
            assert post(db,side);assert db.execute('SELECT COUNT(*) FROM payments').fetchone()[0]==1
            assert totals(db)==((0,30000) if side else (30000,0));db.close();faults+=1
    for method in ('cash','card','online'):
        db=seed();assert post(db,0,30000,method) and post(db,1,20000,method)
        assert totals(db)==((30000,20000) if method=='cash' else (0,0));db.close()
    for side in (0,1):
        with tempfile.TemporaryDirectory(prefix='area11-synthetic-posting-') as folder:
            path=str(Path(folder)/'test.sqlite');db=seed(path);barrier=Barrier(2)
            def compete():
                c=sqlite3.connect(path,timeout=5);c.row_factory=sqlite3.Row
                try:barrier.wait(timeout=10);return post(c,side,60000)
                finally:c.close()
            with ThreadPoolExecutor(max_workers=2) as pool:
                a=pool.submit(compete);b=pool.submit(compete);assert sum((a.result(timeout=20),b.result(timeout=20)))==1
            assert db.execute('SELECT COUNT(*) FROM payments').fetchone()[0]==1
            assert db.execute(balances[side],{'id':1}).fetchone()[2]==40000
            assert not post(db,side,60000);assert post(db,side,40000);assert not post(db,side,40000)
            assert not db.execute('PRAGMA foreign_key_check').fetchall();db.close()
    db=seed();assert post(db,0,10000) and post(db,0,10000)
    assert db.execute('SELECT COUNT(*) FROM payments').fetchone()[0]==2;db.close()
    print(f'PASS {len(migrations)} migrations; {faults} stage faults + rollback/retry; 3 payment-method reconciliations; 2 file-backed two-connection over-settlement competitions + settlement retry refusal; partial repeats are NEW payments')
    print('NOT C# guards/Database.Tx/permission/UI/idempotency/process/power-loss proof; Python owns guards/transactions/threads')


if __name__=='__main__':run()
