#!/usr/bin/env python3
"""Actual expense/drawing/write-off/Apply SQL, migrated disposable SQLite.
Python owns plans, values, transaction and authority. NOT C#/Database.Tx/Dapper/WPF/COMMIT proof.
"""
from pathlib import Path
import sqlite3
from check_counter_current_sqlite import quoted
ROOT=Path(__file__).resolve().parents[1]
S=ROOT/'dotnet/Area11.Core/Services'


def run():
    e=(S/'ExpenseService.cs').read_text();w=(S/'StockService.cs').read_text();t=(S/'StockTakeService.cs').read_text()
    cash=e.split('private ServiceResult<DrawingResult> AddCashDrawing',1)[1].split('private ServiceResult<DrawingResult> AddGoodsDrawing',1)[0]
    goods=e.split('private ServiceResult<DrawingResult> SaveGoods',1)[1].split('// ------------------------------------------------------------------',1)[0]
    take=t.split('private ServiceResult<StockTakeResult> ApplyInside',1)[1].split('public ServiceResult Cancel(',1)[0]
    sql={
        'expense':quoted(e,'INSERT INTO expenses'), 'cash':quoted(cash,'INSERT INTO owner_drawings'),
        'goods':quoted(goods,'INSERT INTO owner_drawings'), 'goods_move':quoted(goods,'INSERT INTO stock_movements'),
        'decrease':quoted((S/'StockPolicy.cs').read_text(),'UPDATE batches'),
        'writeoff':quoted(w,'INSERT INTO stock_adjustments'), 'writeoff_move':quoted(w,'INSERT INTO stock_movements'),
        'take_new':quoted(take,'INSERT INTO batches'), 'take_qty':quoted(take,'UPDATE batches SET qty_base = @qty'),
        'take_move':quoted(take,'INSERT INTO stock_movements'), 'take_session':quoted(take,'UPDATE stock_takes'),
        'audit':quoted((S/'AuditService.cs').read_text(),'INSERT INTO audit_logs')}
    migrations=sorted((ROOT/'dotnet/Area11.Core/Data/Migrations').glob('*.sql'))
    stamp='2026-11-10 12:00:00'
    def seed():
        c=sqlite3.connect(':memory:');c.execute('PRAGMA foreign_keys=ON')
        for p in migrations:c.executescript(p.read_text())
        c.executescript("""INSERT INTO users(id,name,role,active) VALUES(1,'Synthetic','owner',1);
INSERT INTO products(id,name,cost_paisa,retail_paisa) VALUES(1,'Synthetic',1000,1500),(2,'Synthetic found',1000,1500);
INSERT INTO batches(id,product_id,batch_no,qty_base,cost_paisa,retail_paisa) VALUES(1,1,'SYN',10,1000,1500);
INSERT INTO stock_takes(id,code,status,date,user_id) VALUES(1,'ST-SYN','open','2026-11-10',1);
""");c.commit();return c
    def snap(c):return list(c.iterdump())
    actions={'expense':'expense','cash':'owner_drawing','goods':'owner_drawing_goods','writeoff':'writeoff','take':'stock_take_apply','found':'stock_take_apply'}
    def post(c,route):
        a=dict(at=stamp,category='rent',title='Synthetic',amount=3000,note='Synthetic',userId=1,productId=1,
               batchId=1,quantity=2,id=1,qty=-2,value=2000,type='writeoff',date=stamp,direction='out',qtyBase=2,unit='base',
               qtyEntered=2,reason='expired',movementId=1,diff=-2,sessionId=1,code='ST-SYN',diffCount=1,shortPaisa=2000,extraPaisa=0,
               userName='Synthetic',module='Stock',action=actions[route],entity='synthetic',entityId='1',oldValue=None,newValue=None,details=None)
        if route=='expense':c.execute(sql['expense'],a)
        elif route=='cash':c.execute(sql['cash'],dict(a,amount=4000))
        elif route in ('goods','writeoff'):
            assert c.execute(sql['decrease'],a).rowcount==1
            move=c.execute(sql[route+'_move'],a).lastrowid
            c.execute(sql[route],dict(a,movementId=move))
        else:
            # Python terminal gate is deliberately NOT represented as C# once-only proof.
            if c.execute('SELECT status FROM stock_takes WHERE id=1').fetchone()[0]!='open':return False
            if route=='found':
                a.update(productId=2,batchNo='GINTI-ST-SYN',shortPaisa=0,extraPaisa=2000,diff=2)
                a['batchId']=c.execute(sql['take_new'],a).lastrowid
            c.execute(sql['take_qty'],dict(a,id=a['batchId'],qty=2 if route=='found' else 8))
            c.execute(sql['take_move'],a);c.execute(sql['take_session'],a)
        c.execute(sql['audit'],a);return True
    def reconcile(c,route):
        n=lambda query:c.execute(query).fetchone()[0]
        assert n('SELECT SUM(qty_base) FROM batches')==(12 if route=='found' else 8 if route in ('goods','writeoff','take') else 10)
        assert n('SELECT COUNT(*) FROM expenses')==(route=='expense')
        assert n('SELECT COUNT(*) FROM owner_drawings')==(route in ('cash','goods'))
        assert n('SELECT COUNT(*) FROM stock_adjustments')==(route=='writeoff')
        assert n('SELECT COUNT(*) FROM stock_movements')==(route in ('goods','writeoff','take','found'))
        assert n('SELECT COUNT(*) FROM audit_logs')==1
        for table in ('sales','purchases','payments'):assert n('SELECT COUNT(*) FROM '+table)==0
        # Only a synthetic outflow identity: NOT execution of ShiftService or its expected drawer.
        spent=n('SELECT COALESCE(SUM(amount_paisa),0) FROM expenses')
        drawn=n("SELECT COALESCE(SUM(amount_paisa),0) FROM owner_drawings WHERE type='cash'")
        assert 100000-spent-drawn==(97000 if route=='expense' else 96000 if route=='cash' else 100000)
        if route in ('take','found'):
            assert n("SELECT COUNT(*) FROM stock_takes WHERE status='applied'")==1
            before=snap(c)
            with c:assert not post(c,route)
            assert snap(c)==before
    stages={'expense':['row','audit'],'cash':['row','audit'],'goods':['batch','movement','row','audit'],
            'writeoff':['batch','movement','row','audit'],'take':['batch','movement','session','audit'],'found':['newbatch']}
    count=0
    for route,points in stages.items():
        for stage in points:
            for timing in ('BEFORE','AFTER'):
                c=seed();verb='UPDATE' if stage in ('batch','session') else 'INSERT'
                table={'batch':'batches','newbatch':'batches','movement':'stock_movements','session':'stock_takes',
                       'audit':"audit_logs WHEN NEW.action='"+actions[route]+"'",'row':'expenses' if route=='expense' else 'stock_adjustments' if route=='writeoff' else 'owner_drawings'}[stage]
                c.execute('CREATE TRIGGER synthetic_fault '+timing+' '+verb+' ON '+table+" BEGIN SELECT RAISE(ABORT,'synthetic'); END;");c.commit();before=snap(c)
                try:
                    with c:assert post(c,route)
                except sqlite3.IntegrityError:pass
                else:raise AssertionError('fault absent')
                assert snap(c)==before;c.execute('DROP TRIGGER synthetic_fault');c.commit()
                with c:assert post(c,route)
                reconcile(c,route);c.close();count+=1
    print(f'PASS {len(migrations)} migrations; {count} BEFORE/AFTER actual expense/drawing/write-off/Apply SQL fault rollbacks + one retry; whole dump, stock/rows/audit/outflow identities. Python-owned Tx/plans/authority/terminal gate; NOT C#/provider/WPF/ShiftService/COMMIT proof.')


if __name__=='__main__':run()
