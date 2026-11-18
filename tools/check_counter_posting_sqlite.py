#!/usr/bin/env python3
"""Extracted Sale/Return posting SQL on synthetic SQLite. Python owns plans/guards/Tx/loyalty.
NOT C# Compute/Plan/permissions/provider/draft/UI/commit-I/O execution or production certification.
"""
from pathlib import Path
import re
import sqlite3
ROOT=Path(__file__).resolve().parents[1]
S=ROOT/'dotnet/Area11.Core/Services'


def quoted(source,prefix):
    values=set(a or b for a,b in re.findall(r'@"([^"]*)"|"([^"\n]*)"',source) if (a or b).strip().startswith(prefix))
    assert len(values)==1,(prefix,len(values))
    return values.pop()


def run():
    sales=(S/'SaleService.cs').read_text();sale=sales.split('private ServiceResult<SaleResult> Save(',1)[1].split('private ComputedSale',1)[0]
    returns=(S/'ReturnService.cs').read_text();back=returns.split('private ServiceResult<ReturnResult> Save(',1)[1].split('// ----------------------------------------------------------------- planning',1)[0]
    ledger=(S/'CustomerLedgerService.cs').read_text()
    award=quoted(ledger.split('internal static void AwardPoints(',1)[1].split('internal static void ReversePoints(',1)[0],'UPDATE customers SET loyalty_points')
    reverse=quoted(ledger.split('internal static void ReversePoints(',1)[1].split('private static void PromoteStars(',1)[0],'UPDATE customers SET loyalty_points')
    audit=quoted((S/'AuditService.cs').read_text().split('public void Record(',1)[1].split('public void RecordChange(',1)[0],'INSERT INTO audit_logs')
    counter=quoted((S/'SettingsService.cs').read_text().split('internal void WriteCounter(',1)[1].split('private static Dictionary',1)[0],'INSERT INTO settings')
    decrease=quoted((S/'StockPolicy.cs').read_text(),'UPDATE batches')
    drawer=quoted((S/'ShiftService.cs').read_text().split('private void FillDrawer(',1)[1],'SELECT')
    open_drawer=quoted(sales.split('private bool HasOpenDrawer(SqliteConnection connection)',1)[1].split('private ServiceResult<SaleResult> Save(',1)[0],'SELECT COUNT(1)')
    received=quoted(sale.split('foreach (var part in plan.Received)',1)[1],'INSERT INTO payments')
    migrations=sorted((ROOT/'dotnet/Area11.Core/Data/Migrations').glob('*.sql'))
    def seed():
        db=sqlite3.connect(':memory:');db.row_factory=sqlite3.Row;db.execute('PRAGMA foreign_keys=ON')
        for p in migrations:db.executescript(p.read_text())
        db.executescript("""
INSERT INTO users(id,name,role) VALUES(1,'Synthetic owner','owner');
INSERT INTO customers(id,name,loyalty_points) VALUES(1,'Synthetic counter',40);
INSERT INTO shifts(user_id,opening_float_paisa,status) VALUES(1,500000,'open');
INSERT INTO products(id,name,base_unit,cost_paisa,retail_paisa) VALUES(1,'Synthetic A','tablet',1000,2000),(2,'Synthetic B','tablet',1000,2000);
INSERT INTO batches(id,product_id,batch_no,qty_base,cost_paisa,retail_paisa) VALUES(1,1,'SYN-A',100,1000,2000),(2,2,'SYN-B',100,1000,2000);
INSERT INTO settings(key,value) VALUES('bill.nextSaleNo','1'),('bill.nextReturnNo','1'),('bill.nextQuarantineNo','1');
""");db.commit();return db
    def snapshot(db):
        return [(r[0],list(map(tuple,db.execute('SELECT * FROM "'+r[0]+'" ORDER BY rowid')))) for r in db.execute("SELECT name FROM sqlite_master WHERE type='table' ORDER BY name")]
    def post(db,returning=False,restock=True,method='split'):
        due=30000 if method=='split' else 40000 if method=='credit' else 0;paid=40000-due
        v={'at':'2026-11-10 12:00:00','date':'2026-11-10 12:00:00','createdAt':'2026-11-10 12:00:00',
           'customerId':1,'doctorId':None,'userId':1,'userName':'Synthetic owner','id':1,'notes':None,'note':'Synthetic SQL',
           'subtotal':40000,'discount':0,'tax':0,'roundOff':0,'total':40000,'preExtra':40000,'extra':0,'paid':paid,'due':due,
           'status':'partial' if due and paid else 'credit' if due else 'paid','method':method,'change':0,'timeOffset':0,'basis':'profit',
           'module':'Returns' if returning else 'POS','action':'return_create' if returning else 'sale_create',
           'entity':'sale_returns' if returning else 'sales','entityId':'1','oldValue':None,'newValue':'Synthetic','details':None}
        def take(key,prefix):
            n=int(db.execute('SELECT value FROM settings WHERE key=?',(key,)).fetchone()[0]);db.execute(counter,dict(v,key=key,value=n+1));return prefix+f'{n:04d}'
        try:
            db.execute('BEGIN IMMEDIATE')
            if not returning:
                # Actual gate SQL, Python orchestration; not C# HasOpenDrawer execution.
                if db.execute(open_drawer).fetchone()[0]<=0:db.rollback();return False
                v['code']=take('bill.nextSaleNo','INV-');db.execute(quoted(sale,'INSERT INTO sales'),v);v['saleId']=db.execute('SELECT last_insert_rowid()').fetchone()[0]
                for product in (1,2):
                    v.update(productId=product,batchId=product,name='Synthetic',qtyBase=10,unitSold='base',qtyEntered=10,unitPrice=2000,
                             lineTotal=20000,costUnit=1000,pricingMode='normal',discountBasis='profit',discountPercent=0,qty=-10)
                    db.execute(quoted(sale,'INSERT INTO sale_items'),v);assert db.execute(decrease,dict(v,id=product,quantity=10)).rowcount==1
                    db.execute(quoted(sale,'INSERT INTO stock_movements'),v)
                if paid:db.execute(received,dict(v,method='cash' if method=='split' else method,amount=paid))
                if due:db.execute(quoted(sale,'UPDATE customers SET balance_paisa'),dict(v,amount=due,id=1))
                db.execute(award,dict(v,points=4,id=1))
            else:
                v.update(saleId=1,code=take('bill.nextReturnNo','RET-'),restock=int(restock),reason='changed mind',refund=20000,
                         disposition='restocked' if restock else 'quarantine',unitSold='base',qtyEntered=10,qtyBase=10,cash=paid//2,credit=due//2)
                for item in list(db.execute('SELECT id,product_id,batch_id FROM sale_items ORDER BY id')):
                    v.update(itemId=item[0],productId=item[1],batchId=item[2],qcode=None if restock else take('bill.nextQuarantineNo','Q-'))
                    db.execute(quoted(back,'INSERT INTO sale_returns'),v)
                    if restock:
                        db.execute(quoted(back,'UPDATE batches SET qty_base'),dict(v,id=item[2],qty=10))
                        db.execute(quoted(back,'INSERT INTO stock_movements'),dict(v,qty=10,returnOf=item[0]))
                if due:
                    db.execute(quoted(back,'UPDATE customers SET balance_paisa'),dict(v,id=1,amount=due))
                    db.execute(audit,dict(v,action='khata_reduced'))
                db.execute(reverse,dict(v,id=1,points=4))
            db.execute(audit,v);db.commit();return True
        except sqlite3.DatabaseError:db.rollback();return False
    def reconcile(db,balance,stock,points,cashin,cashout):
        assert db.execute('SELECT balance_paisa FROM customers').fetchone()[0]==balance
        books=db.execute('SELECT (SELECT COALESCE(SUM(due_paisa),0) FROM sales)-(SELECT COALESCE(SUM(credit_reduced_paisa),0) FROM sale_returns)').fetchone()[0]
        assert books==balance
        assert db.execute('SELECT SUM(qty_base) FROM batches').fetchone()[0]==stock
        assert db.execute('SELECT loyalty_points FROM customers').fetchone()[0]==points
        r=db.execute(drawer,{'from':'2026-11-10 00:00:00','to':'2026-11-10 23:59:59'}).fetchone()
        assert (r['cashSalesPaisa'],r['cashRefundsPaisa'])==(cashin,cashout)
    stages={'number':'UPDATE OF value ON settings','header':'INSERT ON sales','item':'INSERT ON sale_items','batch':'UPDATE OF qty_base ON batches',
            'movement':'INSERT ON stock_movements','payment':'INSERT ON payments','balance':'UPDATE OF balance_paisa ON customers',
            'points':'UPDATE OF loyalty_points ON customers','audit':'INSERT ON audit_logs'}
    faults=0
    for returning,restock in ((False,True),(True,True),(True,False)):
        active=dict(stages)
        if returning:
            active['header']='INSERT ON sale_returns';active.pop('item');active.pop('payment')
            if not restock:active.pop('batch');active.pop('movement');active['qnumber']=active['number']
        for stage,event in active.items():
            for timing in ('BEFORE','AFTER'):
                db=seed()
                if returning:assert post(db)
                before=snapshot(db)
                when=" WHEN NEW.action='"+('return_create' if returning else 'sale_create')+"'" if stage=='audit' else ''
                if stage in ('number','qnumber'):when=" WHEN NEW.key='bill."+('nextQuarantineNo' if stage=='qnumber' else 'nextReturnNo' if returning else 'nextSaleNo')+"'"
                db.executescript('CREATE TRIGGER counter_post_fault '+timing+' '+event+when+" BEGIN SELECT RAISE(ABORT,'synthetic fault'); END;")
                assert not post(db,returning,restock),(returning,restock,stage,timing)
                assert snapshot(db)==before
                reconcile(db,30000 if returning else 0,180 if returning else 200,44 if returning else 40,10000 if returning else 0,0)
                db.execute('DROP TRIGGER counter_post_fault');assert post(db,returning,restock)
                reconcile(db,0 if returning else 30000,(200 if restock else 180) if returning else 180,40 if returning else 44,10000,10000 if returning else 0)
                assert db.execute('SELECT COUNT(*) FROM payments').fetchone()[0]==1
                db.close();faults+=1
    for method in ('cash','card','online','credit','split'):
        db=seed();assert post(db,method=method);assert post(db,True,method=method)
        due=30000 if method=='split' else 40000 if method=='credit' else 0
        reconcile(db,0,200,40,40000 if method=='cash' else 10000 if method=='split' else 0,40000-due);db.close()
    for unreadable in (False,True):
        db=seed();db.execute('ALTER TABLE shifts RENAME TO hidden_shifts' if unreadable else "UPDATE shifts SET status='closed'");db.commit()
        before=snapshot(db);assert not post(db);assert snapshot(db)==before;db.close()
    print(f'PASS {len(migrations)} migrations; {faults} counter SQL BEFORE/AFTER faults+rollback+retry,stock/points/books/drawer,5methods,2closed/unreadable drawer denials. Python owns planning/guards/Tx; NOT C#/WPF/session/draft/COMMIT proof.')


if __name__=='__main__':run()
