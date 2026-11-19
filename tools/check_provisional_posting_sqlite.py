#!/usr/bin/env python3
"""Actual PR write/drawer SQL on migrated disposable SQLite; Python owns plan/Tx/guards.
NOT C# service, permissions, allocation algorithm, WPF, provider-COMMIT or exactly-once proof.
"""
from pathlib import Path
import sqlite3
from check_counter_current_sqlite import quoted
ROOT=Path(__file__).resolve().parents[1]
S=ROOT/'dotnet/Area11.Core/Services'


def run():
    source=(S/'ProvisionalReturnService.cs').read_text()
    create=source.split('private ServiceResult<ProvisionalResult> Save(',1)[1].split('// -------------------------------------------------------------------- reads',1)[0]
    link=source.split('public ServiceResult Link(',1)[1].split('public ServiceResult Cancel(',1)[0]
    cancel=source.split('public ServiceResult Cancel(',1)[1].split('public ServiceResult<List<BillSearchRow>> SearchBills',1)[0]
    sql={k:quoted(text,prefix) for k,text,prefix in [
        ('header',create,'INSERT INTO provisional_returns'),('batch',create,'UPDATE batches'),('item',create,'INSERT INTO provisional_items'),
        ('movement',create,'INSERT INTO stock_movements'),('payment',create,'INSERT INTO payments'),
        ('newbatch',source.split('private BatchRow FindOrOpenBatch',1)[1],'INSERT INTO batches'),
        ('refund',source.split('private void AllocateRefund',1)[1],'UPDATE provisional_items'),
        ('allocation',link,'INSERT INTO provisional_return_allocations'),('linked',link,'UPDATE provisional_returns'),
        ('cancelled',cancel,'UPDATE provisional_returns'),('out',cancel,'INSERT INTO stock_movements'),
        ('decrease',(S/'StockPolicy.cs').read_text(),'UPDATE batches'),('audit',(S/'AuditService.cs').read_text(),'INSERT INTO audit_logs'),
        ('counter',(S/'SettingsService.cs').read_text().split('internal void WriteCounter(',1)[1].split('private static Dictionary',1)[0],'INSERT INTO settings'),
        ('drawer',(S/'ShiftService.cs').read_text().split('private void FillDrawer(',1)[1],'SELECT')]}
    migrations=sorted((ROOT/'dotnet/Area11.Core/Data/Migrations').glob('*.sql'))
    def seed(route,no_batch=False):
        c=sqlite3.connect(':memory:');c.row_factory=sqlite3.Row;c.execute('PRAGMA foreign_keys=ON')
        for p in migrations:c.executescript(p.read_text())
        c.executescript("""INSERT INTO users(id,name,role,active) VALUES(1,'Synthetic','owner',1);
INSERT INTO products(id,name,cost_paisa,retail_paisa) VALUES(1,'Synthetic A',1000,1500),(2,'Synthetic B',1000,1500);
INSERT INTO batches(id,product_id,batch_no,qty_base,cost_paisa,retail_paisa) VALUES(1,1,'SYN-A',10,1000,1500),(2,2,'SYN-B',10,1000,1500);
INSERT INTO settings(key,value) VALUES('bill.nextProvisionalNo','1');""")
        if no_batch:c.execute('DELETE FROM batches')
        if route!='create':
            c.executescript("""UPDATE batches SET qty_base=7;
INSERT INTO sales(id,code,date,subtotal_paisa,total_paisa,paid_paisa,status,user_id) VALUES(1,'INV-SYN','2026-11-10 12:00:00',9000,9000,9000,'paid',1);
INSERT INTO sale_items(id,sale_id,product_id,batch_id,qty_base,unit_sold,qty_entered,unit_price_paisa,line_total_paisa) VALUES(1,1,1,1,3,'base',3,1500,4500),(2,1,2,2,3,'base',3,1500,4500);
INSERT INTO payments(date,method,amount_paisa,sale_id,user_id) VALUES('2026-11-10 12:00:00','cash',9000,1,1);""")
        c.commit()
        if route!='create':
            with c:post(c,'create')
        return c
    def snap(c):return list(c.iterdump())
    def post(c,route):
        a=dict(code='PR-0001',date='2026-11-10 12:00:00',at='2026-11-10 12:00:00',phone=None,name='Synthetic',reason='Synthetic',
               notes=None,note='Synthetic',refund=3001,userId=1,provisionalId=1,refId=1,id=1,userName='Synthetic',module='Returns',
               action='provisional_'+route,entity='provisional_returns',entityId='PR-0001',oldValue=None,newValue=None,details=None)
        if route=='create':
            number=int(c.execute("SELECT value FROM settings WHERE key='bill.nextProvisionalNo'").fetchone()[0])
            assert number==1;c.execute(sql['counter'],dict(a,key='bill.nextProvisionalNo',value=number+1))
            a['provisionalId']=c.execute(sql['header'],a).lastrowid;a['refId']=a['provisionalId']
            for product in (1,2):
                batch=c.execute('SELECT id FROM batches WHERE product_id=?',(product,)).fetchone()
                if batch is None:batch_id=c.execute(sql['newbatch'],dict(a,productId=product,cost=1000,retail=1500)).lastrowid
                else:batch_id=batch[0]
                b=dict(a,productId=product,batchId=batch_id,qty=1,qtyBase=1,qtyEntered=1,unit='base',cost=1000,id=batch_id)
                c.execute(sql['batch'],b);c.execute(sql['movement'],b);item=c.execute(sql['item'],b).lastrowid
                c.execute(sql['refund'],dict(b,id=item,refund=1501 if product==1 else 1500))
            c.execute(sql['payment'],dict(a,amount=-3001))
        else:
            if c.execute('SELECT status FROM provisional_returns WHERE id=1').fetchone()[0]!='pending':return False
            if route=='link':
                for item in (1,2):
                    c.execute(sql['allocation'],dict(a,provisionalItemId=item,saleItemId=item,qtyBase=1,expected=1500,refund=1501 if item==1 else 1500))
                c.execute(sql['linked'],dict(a,saleId=1,code='INV-SYN',expected=3000,unallocated=0))
            else:
                for product in (1,2):
                    assert c.execute(sql['decrease'],dict(a,id=product,productId=product,quantity=1)).rowcount==1
                    c.execute(sql['out'],dict(a,productId=product,batchId=product,qty=-1))
                c.execute(sql['cancelled'],a)
        c.execute(sql['audit'],a);return True
    count=0
    for route,points in {'create':['header','batch','movement','item','refund','payment','audit','newbatch','second'],
                         'link':['allocation','header','audit','second'],'cancel':['batch','movement','header','audit','second']}.items():
        for point in points:
            for timing in ('BEFORE','AFTER'):
                c=seed(route,point=='newbatch')
                verb='UPDATE' if point in ('batch','refund') or (point=='header' and route!='create') else 'INSERT'
                table={'header':'provisional_returns','batch':'batches','newbatch':'batches','movement':'stock_movements','item':'provisional_items','refund':'provisional_items',
                       'payment':'payments','audit':"audit_logs WHEN NEW.action='provisional_"+route+"'",'allocation':'provisional_return_allocations',
                       'second':'provisional_return_allocations WHEN NEW.sale_item_id=2' if route=='link' else 'stock_movements WHEN NEW.product_id=2'}[point]
                c.execute('CREATE TRIGGER synthetic_pr '+timing+' '+verb+' ON '+table+" BEGIN SELECT RAISE(ABORT,'synthetic'); END;");c.commit();before=snap(c)
                try:
                    with c:post(c,route)
                except sqlite3.IntegrityError:pass
                else:raise AssertionError('fault absent')
                assert before==snap(c);c.execute('DROP TRIGGER synthetic_pr');c.commit()
                money=lambda:[tuple(r) for r in c.execute('SELECT * FROM payments ORDER BY id')]
                cash=money()
                with c:assert post(c,route)
                n=lambda q:c.execute(q).fetchone()[0]
                assert n('SELECT SUM(qty_base) FROM batches')==(2 if point=='newbatch' else 22 if route=='create' else 16 if route=='link' else 14)
                assert n('SELECT COUNT(*) FROM provisional_returns')==1
                assert n('SELECT SUM(refund_paisa) FROM provisional_items')==3001
                assert n('SELECT COUNT(*) FROM payments WHERE provisional_return_id IS NOT NULL')==1
                assert n('SELECT SUM(amount_paisa) FROM payments WHERE provisional_return_id IS NOT NULL')==-3001
                assert n('SELECT COUNT(*) FROM provisional_return_allocations')==(2 if route=='link' else 0)
                if route!='create':assert cash==money()
                row=c.execute(sql['drawer'],{'from':'2026-11-10 00:00:00','to':'2026-11-10 23:59:59'}).fetchone()
                assert row['cashRefundsPaisa']==3001;assert 100000+row['cashSalesPaisa']-row['cashRefundsPaisa']==(96999 if route=='create' else 105999)
                assert not list(c.execute('PRAGMA foreign_key_check'))
                if route!='create':
                    before=snap(c)
                    with c:assert not post(c,route)
                    assert before==snap(c)
                c.close();count+=1
    print(f'PASS {len(migrations)} migrations; {count} PR Create/Link/Cancel BEFORE/AFTER SQL faults+exact rollback+one retry;2lines/refund remainder/OPEN batch/allocations/terminal repeats/unchanged cash on Link+Cancel/actual drawer SELECT. Python-owned plan/Tx/guards;NOT C#/allocation algorithm/permissions/WPF/provider-COMMIT proof.')


if __name__=='__main__':run()
