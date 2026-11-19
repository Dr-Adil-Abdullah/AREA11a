#!/usr/bin/env python3
"""Actual receipt/history SELECTs and audit INSERT on synthetic migrated SQLite.
Python owns policy and transactions: NOT C#/Dapper/Database.Tx/session/WPF/spooler execution.
"""
from pathlib import Path
import re
import sqlite3
from check_counter_current_sqlite import quoted
ROOT=Path(__file__).resolve().parents[1]
S=ROOT/'dotnet/Area11.Core/Services'


def run():
    migrations=sorted((ROOT/'dotnet/Area11.Core/Data/Migrations').glob('*.sql'))
    actor_sql=quoted((S/'SnapshotReadAccess.cs').read_text(),'SELECT id, name, role, active FROM users')
    grants=(S/'PermissionService.cs').read_text().split('public bool Can(',1)[1].split('public static bool IsFinancialVisibility(',1)[0]
    grant_sql=quoted(grants,'SELECT allowed FROM user_permissions')
    audit_sql=quoted((S/'AuditService.cs').read_text(),'INSERT INTO audit_logs')
    selects=[]
    for filename in ('ReceiptService.cs','ReturnReceiptService.cs','SaleHistoryService.cs'):
        text=(S/filename).read_text()
        selects.extend((filename,a or b) for a,b in re.findall(r'@"([^"]*)"|"([^"\n]*)"',text) if (a or b).strip().startswith('SELECT '))
    assert len(selects)==14,len(selects)
    db=sqlite3.connect(':memory:');db.row_factory=sqlite3.Row;db.execute('PRAGMA foreign_keys=ON')
    for p in migrations:db.executescript(p.read_text())
    db.executescript("""
INSERT INTO users(id,name,role,active) VALUES(1,'Stored owner','owner',1),(2,'Current printer','manager',1);
INSERT INTO customers(id,name) VALUES(1,'SYNTHETIC customer');
INSERT INTO products(id,name) VALUES(1,'SYNTHETIC');
INSERT INTO sales(id,code,date,user_id,total_paisa) VALUES(1,'INV-SYN','2026-11-10 12:00:00',1,30000);
INSERT INTO sale_items(id,sale_id,product_id,name_snapshot,qty_base,qty_entered,unit_price_paisa,line_total_paisa) VALUES(1,1,1,'SYNTHETIC',2,2,15000,30000);
INSERT INTO sale_returns(sale_id,sale_item_id,product_id,qty_base,qty_entered,refund_paisa,code,user_id) VALUES(1,1,1,1,1,15000,'RET-SYN',1);
INSERT INTO payments(sale_id,method,amount_paisa) VALUES(1,'cash',30000);
""")
    db.commit()
    params={'id':1,'code':'ret-syn','from':'2026-11-01','to':'2026-11-30','method':'','status':'','query':'%INV-SYN%'}
    for filename,sql in selects:
        assert list(db.execute(sql,params)),(filename,sql)
    def require(actor,module):
        if not actor or actor[0]<=0 or not actor[2]:raise PermissionError()
        row=db.execute(actor_sql,{'id':actor[0]}).fetchone()
        if not row or not row['active'] or row['role']!=actor[1]:raise PermissionError()
        if row['role']=='owner':return row
        try:grant=db.execute(grant_sql,{'userId':row['id'],'module':module}).fetchone()
        except sqlite3.Error:raise PermissionError() from None
        if grant is not None and not grant[0]:raise PermissionError()
        return row
    def read(actor,module):
        try:require(actor,module)
        except PermissionError:return None
        return list(db.execute(selects[0][1],params))
    denied=0;actor=(2,'manager',True)
    for module in ('pos','returns','sales'):
        assert read(actor,module)
        for fault,apply,undo in (
            ('inactive','UPDATE users SET active=0 WHERE id=2','UPDATE users SET active=1 WHERE id=2'),
            ('role',"UPDATE users SET role='cashier' WHERE id=2","UPDATE users SET role='manager' WHERE id=2"),
            ('revoke',f"INSERT INTO user_permissions(user_id,module_key,allowed) VALUES(2,'{module}',0)",f"DELETE FROM user_permissions WHERE user_id=2 AND module_key='{module}'"),
            ('grant-read','ALTER TABLE user_permissions RENAME TO hidden_permissions','ALTER TABLE hidden_permissions RENAME TO user_permissions')):
            db.execute(apply);db.commit();before=list(db.iterdump());assert read(actor,module) is None;assert list(db.iterdump())==before;denied+=1
            db.execute(undo);db.commit();assert read(actor,module)
        for invalid in (None,(0,'owner',True),(2,'manager',False),(2,'owner',True)):
            before=list(db.iterdump());assert read(invalid,module) is None;assert list(db.iterdump())==before;denied+=1
    db.execute("INSERT INTO user_permissions(user_id,module_key,allowed) VALUES(2,'pos',0),(2,'returns',0),(2,'sales',1)");db.commit()
    assert read(actor,'sales') and read(actor,'pos') is None and read(actor,'returns') is None
    db.execute('UPDATE user_permissions SET allowed=1-allowed WHERE user_id=2');db.commit()
    assert read(actor,'sales') is None and read(actor,'pos') and read(actor,'returns')
    db.execute('DELETE FROM user_permissions WHERE user_id=2');db.commit()
    def audit(action):
        with db:
            row=require(actor,'sales')
            exists=db.execute(quoted((S/'SaleHistoryService.cs').read_text(),'SELECT COUNT(1) FROM sale_returns'),{'code':'ret-syn'}).fetchone()[0]
            assert exists
            db.execute(audit_sql,{'at':'2026-11-10 12:01:00','userId':row['id'],'userName':row['name'],'action':action,'module':'sales','entity':None,'entityId':None,'oldValue':None,'newValue':None,'details':'Synthetic reprint'})
    audit('sale_reprint');audit('return_reprint')
    assert [r[0] for r in db.execute("SELECT user_name FROM audit_logs")]==['Current printer','Current printer']
    db.execute("CREATE TRIGGER synthetic_fault BEFORE INSERT ON audit_logs BEGIN SELECT RAISE(ABORT,'synthetic'); END;");db.commit();before=list(db.iterdump())
    try:audit('sale_reprint')
    except sqlite3.IntegrityError:pass
    else:raise AssertionError('fault did not surface')
    assert list(db.iterdump())==before
    print(f'PASS {len(migrations)} migrations; {len(selects)} actual receipt/history SELECTs; {denied} actor/module denials+unchanged snapshots/regrant; module independence,actual audit INSERT/stored name/trigger rollback. Python policy/Tx, NOT C#/session/WPF/printer.')


if __name__=='__main__':run()
