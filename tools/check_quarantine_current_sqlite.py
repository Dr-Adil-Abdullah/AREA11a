#!/usr/bin/env python3
"""Actual quarantine SQL on synthetic migrated SQLite; Python owns policy/plan/transactions.
NOT C#/Dapper/Database.Tx/session/GUI/COMMIT-I/O/production concurrency certification.
"""
from pathlib import Path
import sqlite3
from check_counter_current_sqlite import quoted
ROOT=Path(__file__).resolve().parents[1]
S=ROOT/'dotnet/Area11.Core/Services'


def run():
    source=(S/'QuarantineService.cs').read_text()
    actor_sql=quoted((S/'SnapshotReadAccess.cs').read_text(),'SELECT id, name, role, active FROM users')
    grant_sql=quoted((S/'PermissionService.cs').read_text().split('public bool Can(',1)[1].split('public static bool IsFinancialVisibility(',1)[0],'SELECT allowed FROM user_permissions')
    select=quoted(source,'SELECT r.id AS returnId')
    pending=select+quoted(source,"WHERE r.disposition = 'quarantine'")
    history=select+quoted(source,"WHERE r.qcode IS NOT NULL")
    count=quoted(source,'SELECT COUNT(1) FROM sale_returns')
    row_sql=quoted(source,'SELECT id, qcode, product_id AS productId')
    update=quoted(source,'UPDATE sale_returns')
    batch=quoted(source,'UPDATE batches SET qty_base')
    fallback=quoted(source,'SELECT id FROM batches WHERE product_id')
    movement=quoted(source,'INSERT INTO stock_movements')
    audit=quoted((S/'AuditService.cs').read_text(),'INSERT INTO audit_logs')
    migrations=sorted((ROOT/'dotnet/Area11.Core/Data/Migrations').glob('*.sql'))
    def seed():
        db=sqlite3.connect(':memory:');db.row_factory=sqlite3.Row;db.execute('PRAGMA foreign_keys=ON')
        for p in migrations:db.executescript(p.read_text())
        db.executescript("""
INSERT INTO users(id,name,role,active) VALUES(1,'Owner','owner',1),(2,'Stored decider','manager',1),(3,'Reader','cashier',1);
INSERT INTO products(id,name) VALUES(1,'SYNTHETIC');
INSERT INTO batches(id,product_id,batch_no,qty_base,cost_paisa,retail_paisa) VALUES(1,1,'SYN-BATCH',97,20000,30000);
INSERT INTO sales(id,code,date,user_id,total_paisa) VALUES(1,'INV-SYN','2026-11-10 12:00:00',1,90000);
INSERT INTO sale_items(id,sale_id,product_id,batch_id,qty_base,qty_entered,line_total_paisa) VALUES(1,1,1,1,3,3,90000);
INSERT INTO sale_returns(id,sale_id,sale_item_id,product_id,batch_id,qty_base,qty_entered,refund_paisa,code,qcode,disposition,user_id) VALUES(1,1,1,1,1,3,3,90000,'RET-SYN','Q-SYN','quarantine',1);
""");db.commit();return db
    def snapshot(db):
        return [(r[0],list(map(tuple,db.execute('SELECT * FROM "'+r[0]+'" ORDER BY rowid')))) for r in db.execute("SELECT name FROM sqlite_master WHERE type='table' ORDER BY name")]
    def require(db,actor=(2,'manager',True),manage=False):
        if not actor or actor[0]<=0 or not actor[2]:raise PermissionError()
        row=db.execute(actor_sql,{'id':actor[0]}).fetchone()
        if not row or not row['active'] or row['role']!=actor[1]:raise PermissionError()
        if manage and row['role'] not in ('owner','manager'):raise PermissionError()
        if row['role']!='owner':
            try:grant=db.execute(grant_sql,{'userId':row['id'],'module':'returns'}).fetchone()
            except sqlite3.Error:raise PermissionError() from None
            if grant and not grant[0]:raise PermissionError()
        return row
    def post(db,outcome,actor=(2,'manager',True)):
        with db:
            current=require(db,actor,manage=True)
            row=db.execute(row_sql,{'id':1}).fetchone()
            if not row or row['disposition']!='quarantine':raise ValueError('already decided')
            v={'id':1,'returnId':1,'disposition':outcome,'at':'2026-11-10 12:01:00','userId':current['id'],
               'note':'Synthetic checked goods','restock':0 if outcome=='expired' else 1,'qty':row['qtyBase'],
               'productId':row['productId'],'batchId':row['batchId'],'qcode':row['qcode'],
               'type':'quarantine_sold' if outcome=='sold' else 'quarantine_restock'}
            db.execute(update,v)
            if outcome in ('restocked','sold'):
                target=row['batchId']
                if target is None:
                    found=db.execute(fallback,v).fetchone();target=found[0] if found else None
                if target is not None:db.execute(batch,dict(v,id=target))
                db.execute(movement,v)
            db.execute(audit,dict(v,userName=current['name'],action='quarantine_'+outcome,module='Quarantine',entity='sale_returns',entityId='1',oldValue='quarantine',newValue='Synthetic outcome',details=None))
    db=seed();require(db)
    assert len(list(db.execute(pending)))==1 and not list(db.execute(history)) and db.execute(count).fetchone()[0]==1
    db.close()
    denied=0
    for route in ('pending','history','count','restocked','expired','sold'):
        for fault in ('inactive','role','revoke','grant-read'):
            db=seed()
            if fault=='inactive':db.execute('UPDATE users SET active=0 WHERE id=2')
            elif fault=='role':db.execute("UPDATE users SET role='cashier' WHERE id=2")
            elif fault=='revoke':db.execute("INSERT INTO user_permissions(user_id,module_key,allowed) VALUES(2,'returns',0)")
            else:db.execute('ALTER TABLE user_permissions RENAME TO hidden_permissions')
            db.commit();before=snapshot(db)
            try:require(db,manage=route in ('restocked','expired','sold'))
            except PermissionError:denied+=1
            else:raise AssertionError('stale actor allowed')
            assert snapshot(db)==before;db.close()
        for actor in (None,(0,'owner',True),(2,'manager',False),(2,'owner',True)):
            db=seed();before=snapshot(db)
            try:require(db,actor,manage=route in ('restocked','expired','sold'))
            except PermissionError:denied+=1
            else:raise AssertionError('invalid actor allowed')
            assert snapshot(db)==before;db.close()
    faults=0
    for outcome in ('restocked','expired','sold'):
        stages={'disposition':'UPDATE OF disposition ON sale_returns','audit':'INSERT ON audit_logs'}
        if outcome!='expired':stages.update(batch='UPDATE OF qty_base ON batches',movement='INSERT ON stock_movements')
        for stage,target in stages.items():
            for timing in ('BEFORE','AFTER'):
                db=seed();before=snapshot(db)
                db.execute(f"CREATE TRIGGER q_fault {timing} {target} BEGIN SELECT RAISE(ABORT,'synthetic'); END;");db.commit()
                try:post(db,outcome)
                except sqlite3.IntegrityError:pass
                else:raise AssertionError('fault did not fire')
                assert snapshot(db)==before;faults+=1;db.execute('DROP TRIGGER q_fault');db.commit();post(db,outcome)
                assert db.execute(count).fetchone()[0]==0
                assert len(list(db.execute(history)))==1 and not list(db.execute(pending))
                assert db.execute('SELECT SUM(qty_base) FROM batches').fetchone()[0]==(97 if outcome=='expired' else 100)
                assert db.execute("SELECT COUNT(*) FROM stock_movements WHERE ref_type='quarantine'").fetchone()[0]==(0 if outcome=='expired' else 1)
                assert db.execute("SELECT user_name FROM audit_logs WHERE action LIKE 'quarantine_%'").fetchone()[0]=='Stored decider'
                committed=snapshot(db)
                try:post(db,outcome)
                except ValueError:pass
                else:raise AssertionError('repeat allowed')
                assert snapshot(db)==committed;db.close()
    for outcome in ('restocked','sold'):
        db=seed();db.execute('UPDATE sale_returns SET batch_id=NULL');db.commit();post(db,outcome)
        assert db.execute('SELECT SUM(qty_base) FROM batches').fetchone()[0]==100;db.close()
    db=seed();require(db,(3,'cashier',True))
    try:require(db,(3,'cashier',True),manage=True)
    except PermissionError:pass
    else:raise AssertionError('reader decided')
    db.execute('ALTER TABLE sale_returns RENAME TO hidden_returns');db.commit()
    try:db.execute(count).fetchone()
    except sqlite3.OperationalError:pass
    else:raise AssertionError('unreadable count certified zero')
    db.close()
    print(f'PASS {len(migrations)} migrations; actual quarantine SELECT/count/row/update/batch/fallback/movement/audit SQL; {denied} actor/module denials, {faults} BEFORE/AFTER faults+exact rollback/retry/repeat refusal;3outcomes,2fallbacks,storedname,readonlymanager distinction,count-read failure. Python owns plan/policy/Tx; NOT C#/provider/session/WPF/COMMIT proof.')


if __name__=='__main__':run()
