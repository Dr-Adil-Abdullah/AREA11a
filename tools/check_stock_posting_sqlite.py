#!/usr/bin/env python3
"""Actual stock-post SQL on synthetic migrated SQLite. Python owns plan/guards/Tx/audit values.
NOT C# service/provider/permissions/number-prefix cache/draft/UI/COMMIT-I/O execution.
"""
from pathlib import Path
import re
import sqlite3
ROOT=Path(__file__).resolve().parents[1]
SERVICES=ROOT/'dotnet/Area11.Core/Services'


def quoted(source, prefix):
    values=[a or b for a,b in re.findall(r'@"([^"]*)"|"([^"\n]*)"',source) if (a or b).strip().startswith(prefix)]
    assert len(values)==1,(prefix,len(values))
    return values[0]


def run():
    purchase=(SERVICES/'PurchaseService.cs').read_text().split('private ServiceResult<PurchaseResult> Save(',1)[1].split('private static string NormalizePaidMethod',1)[0]
    returns=(SERVICES/'SupplierLedgerService.cs').read_text().split('private ServiceResult<SupplierReturnResult> SaveReturn(',1)[1]
    audit=quoted((SERVICES/'AuditService.cs').read_text().split('public void Record(',1)[1].split('public void RecordChange(',1)[0],'INSERT INTO audit_logs')
    counter=quoted((SERVICES/'SettingsService.cs').read_text().split('internal void WriteCounter(',1)[1].split('private static Dictionary',1)[0],'INSERT INTO settings')
    decrease=quoted((SERVICES/'StockPolicy.cs').read_text(),'UPDATE batches')
    drawer=quoted((SERVICES/'ShiftService.cs').read_text().split('private void FillDrawer(',1)[1],'SELECT')
    migrations=sorted((ROOT/'dotnet/Area11.Core/Data/Migrations').glob('*.sql'))
    def seed():
        db=sqlite3.connect(':memory:');db.row_factory=sqlite3.Row;db.execute('PRAGMA foreign_keys=ON')
        for p in migrations:db.executescript(p.read_text())
        db.executescript("""
INSERT INTO users(id,name,role,active) VALUES(1,'Synthetic owner','owner',1);
INSERT INTO suppliers(id,name,opening_balance_paisa,balance_paisa) VALUES(1,'Synthetic supplier',0,0);
INSERT INTO products(id,name,base_unit,box_strips,strip_tablets,cost_paisa,retail_paisa) VALUES(1,'Synthetic stock','tablet',2,10,1000,1500);
INSERT INTO settings(key,value) VALUES('bill.nextPurchaseNo','1'),('bill.nextSupplierReturnNo','1');
""");db.commit();return db
    def snapshot(db):
        return [(r[0],list(map(tuple,db.execute('SELECT * FROM "'+r[0]+'" ORDER BY rowid')))) for r in db.execute("SELECT name FROM sqlite_master WHERE type='table' ORDER BY name")]
    def execute(db,source,prefix,values):return db.execute(quoted(source,prefix),values)
    def post(db,back=False,cash=False,method='cash',sample=False):
        v={'at':'2026-11-10 12:00:00','date':'2026-11-10 12:00:00','createdAt':'2026-11-10 12:00:00','supplierId':1,'userId':1,'userName':'Synthetic owner',
           'id':1,'productId':1,'invoiceNo':'SYNTHETIC','notes':None,'note':'Synthetic probe','reason':'expired',
           'module':'Suppliers' if back else 'Purchases','action':'supplier_return' if back else 'purchase_create',
           'entity':'supplier_returns' if back else 'purchases','entityId':'1','oldValue':None,'newValue':'Synthetic','details':None}
        try:
            db.execute('BEGIN IMMEDIATE');key='bill.nextSupplierReturnNo' if back else 'bill.nextPurchaseNo'
            number=int(db.execute('SELECT value FROM settings WHERE key=?',(key,)).fetchone()[0]);v['code']=('SR-' if back else 'PINV-')+f'{number:04d}'
            db.execute(counter,dict(v,key=key,value=number+1))
            if not back:
                execute(db,purchase,'INSERT INTO purchases',v);v['purchaseId']=db.execute('SELECT last_insert_rowid()').fetchone()[0]
                total=20000 if sample else 40000
                for index in range(2):
                    zero=sample and index==0
                    v.update(batchNo='SYN-'+str(index),expiryDate=None,expiryYm=None,qty=20,qtyBase=20,qtyEntered=20,unit='base',
                             cost=0 if zero else 1000,retail=1600+index*100,isSample=int(zero),type='sample' if zero else 'purchase',lineTotal=0 if zero else 20000)
                    execute(db,purchase,'INSERT INTO batches',v);v['batchId']=db.execute('SELECT last_insert_rowid()').fetchone()[0]
                    execute(db,purchase,'INSERT INTO purchase_items',v);execute(db,purchase,'INSERT INTO stock_movements',v)
                    execute(db,purchase,'UPDATE products SET retail_paisa',v)
                v.update(total=total,paid=4000,due=total-4000,method=method,amount=4000)
                execute(db,purchase,'UPDATE purchases SET subtotal',v);execute(db,purchase,'UPDATE suppliers SET balance_paisa = balance_paisa +',v)
                execute(db,purchase,'INSERT INTO payments',v);execute(db,purchase,'UPDATE suppliers SET balance_paisa = balance_paisa -',v)
            else:
                payable=db.execute('SELECT balance_paisa FROM suppliers WHERE id=1').fetchone()[0]
                v.update(total=10000,settled=int(cash));execute(db,returns,'INSERT INTO supplier_returns',v)
                v['returnId']=db.execute('SELECT last_insert_rowid()').fetchone()[0]
                for batch in list(db.execute('SELECT id FROM batches WHERE purchase_id IS NOT NULL ORDER BY id')):
                    v.update(batchId=batch[0],qty=5,cost=1000,total=5000)
                    execute(db,returns,'INSERT INTO supplier_return_items',v)
                    changed=db.execute(decrease,dict(v,id=batch[0],quantity=5)).rowcount;assert changed==1
                    execute(db,returns,'INSERT INTO stock_movements',dict(v,qty=-5))
                if not cash:execute(db,returns,'UPDATE suppliers SET balance_paisa = @balance',dict(v,balance=payable-10000,id=1))
            db.execute(audit,v);db.commit();return True
        except sqlite3.DatabaseError:db.rollback();return False
    def reconcile(db,payable,stock,outcash,incash):
        assert db.execute('SELECT balance_paisa FROM suppliers').fetchone()[0]==payable
        assert db.execute('SELECT COALESCE(SUM(qty_base),0) FROM batches').fetchone()[0]==stock
        # Independent book identity from committed rows; not C# Ledger.Page execution.
        books=db.execute('SELECT (SELECT COALESCE(SUM(total_paisa),0) FROM purchases)-(SELECT COALESCE(SUM(amount_paisa),0) FROM payments)-(SELECT COALESCE(SUM(total_paisa),0) FROM supplier_returns WHERE settled=0)').fetchone()[0]
        assert books==payable
        row=db.execute(drawer,{'from':'2026-11-10 00:00:00','to':'2026-11-10 23:59:59'}).fetchone()
        assert (row['supplierCashOutPaisa'],row['supplierCashInPaisa'])==(outcash,incash)
    stages={'number':'UPDATE OF value ON settings','header':'INSERT ON purchases','batch':'INSERT ON batches','item':'INSERT ON purchase_items',
            'movement':'INSERT ON stock_movements','retail':'UPDATE OF retail_paisa ON products','balance':'UPDATE OF balance_paisa ON suppliers',
            'payment':'INSERT ON payments','audit':'INSERT ON audit_logs'}
    faults=0
    for back,cash in ((False,False),(True,False),(True,True)):
        active=dict(stages)
        if back:
            active.update(header='INSERT ON supplier_returns',batch='UPDATE OF qty_base ON batches',item='INSERT ON supplier_return_items')
            for stage in ('retail','payment')+ (('balance',) if cash else ()):active.pop(stage)
        for stage,event in active.items():
            for timing in ('BEFORE','AFTER'):
                db=seed()
                if back:assert post(db)
                before=snapshot(db)
                when=" WHEN NEW.action='"+('supplier_return' if back else 'purchase_create')+"'" if stage=='audit' else ''
                db.executescript('CREATE TRIGGER stock_post_fault '+timing+' '+event+when+" BEGIN SELECT RAISE(ABORT,'synthetic fault'); END;")
                assert not post(db,back,cash),(back,cash,stage,timing)
                assert snapshot(db)==before;(reconcile(db,36000,40,4000,0) if back else reconcile(db,0,0,0,0))
                db.execute('DROP TRIGGER stock_post_fault');assert post(db,back,cash)
                reconcile(db,(36000 if cash else 26000) if back else 36000,30 if back else 40,4000,10000 if cash else 0)
                assert db.execute('SELECT COUNT(*) FROM payments').fetchone()[0]==1
                assert db.execute('SELECT value FROM settings WHERE key=?',('bill.nextSupplierReturnNo' if back else 'bill.nextPurchaseNo',)).fetchone()[0]=='2'
                db.close();faults+=1
    for method in ('cash','card','online'):
        db=seed();assert post(db,method=method);assert post(db,True,True)
        reconcile(db,36000,30,4000 if method=='cash' else 0,10000);db.close()
    db=seed();assert post(db,sample=True);reconcile(db,16000,40,4000,0)
    assert db.execute('SELECT SUM(cost_paisa) FROM batches WHERE is_sample=1').fetchone()[0]==0;db.close()
    print(f'PASS {len(migrations)} migrations; {faults} BEFORE/AFTER stock-post SQL faults+rollback+retry, books/drawer, 3 payment methods, sample. Python-owned Tx/plan/guards; NOT C#/WPF/draft/COMMIT-I-O proof.')


if __name__=='__main__':run()
