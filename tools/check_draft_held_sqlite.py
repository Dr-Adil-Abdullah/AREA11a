#!/usr/bin/env python3
"""Actual draft/held SQL on migrated synthetic SQLite. Python owns authority/routing/transactions.
Not C#/Dapper/Database.Tx/pricing/terminal-post/reconstruction/session/WPF/COMMIT execution.
"""
from pathlib import Path
import re
import sqlite3
from check_counter_current_sqlite import quoted
ROOT=Path(__file__).resolve().parents[1]
S=ROOT/'dotnet/Area11.Core/Services'


def run():
    d=(S/'DraftService.cs').read_text();h=(S/'HoldCartService.cs').read_text()
    actor=quoted((S/'SnapshotReadAccess.cs').read_text(),'SELECT id, name, role, active FROM users')
    grant=quoted((S/'PermissionService.cs').read_text().split('public bool Can(',1)[1].split('public static bool IsFinancialVisibility(',1)[0],'SELECT allowed FROM user_permissions')
    audit=quoted((S/'AuditService.cs').read_text(),'INSERT INTO audit_logs')
    draft_insert=quoted(d,'INSERT INTO document_drafts');draft_update=quoted(d,'UPDATE document_drafts SET payload_json')
    draft_discard=quoted(d,"UPDATE document_drafts\nSET status = 'discarded'")
    type_probe=quoted(d,'SELECT draft_type FROM document_drafts')
    list_sql=quoted(d.split('// ------------------------------------------------------------------ list & open',1)[1].split('public ServiceResult<SaleInput> OpenSale',1)[0],'SELECT id, draft_key')
    row_sql=quoted(d.split('private DocumentDraft? ReadRow(',1)[1],'SELECT id, draft_key')
    hold_insert=quoted(h,'INSERT INTO held_carts');item_insert=quoted(h,'INSERT INTO held_cart_items')
    resume=quoted(h,"UPDATE held_carts SET status = 'resumed'");discard=quoted(h,"UPDATE held_carts SET status = 'discarded'")
    held_count=quoted(h,"SELECT COUNT(1) FROM held_carts WHERE status = 'held'")
    held_list=quoted(h,'SELECT h.id, h.code')
    held_row=quoted(h,'SELECT id, code, customer_id');held_lines=quoted(h,'SELECT product_id AS productId')
    migrations=sorted((ROOT/'dotnet/Area11.Core/Data/Migrations').glob('*.sql'))
    def seed():
        db=sqlite3.connect(':memory:');db.row_factory=sqlite3.Row;db.execute('PRAGMA foreign_keys=ON')
        for p in migrations:db.executescript(p.read_text())
        db.executescript("""
INSERT INTO users(id,name,role,active) VALUES(1,'Owner','owner',1),(2,'Stored author','manager',1);
INSERT INTO products(id,name) VALUES(1,'SYNTHETIC');
INSERT INTO document_drafts(id,draft_key,draft_type,payload_json,created_by_id,created_by_name) VALUES(1,'sale-SYN','sale','{}',1,'Owner'),(2,'purchase-SYN','purchase','{}',1,'Owner');
INSERT INTO held_carts(id,code,user_id,total_paisa,items_count) VALUES(1,'H-SYN',1,30000,1);
INSERT INTO held_cart_items(cart_id,product_id,name_snapshot,unit,quantity,unit_price_paisa,position) VALUES(1,1,'SYNTHETIC','base',1,30000,0);
""");db.commit();return db
    def snapshot(db):return list(db.iterdump())
    def require(db,kind,who=(2,'manager',True),main=True,cost=True):
        if not who or who[0]<=0 or not who[2]:raise PermissionError()
        row=db.execute(actor,{'id':who[0]}).fetchone()
        if not row or not row['active'] or row['role']!=who[1]:raise PermissionError()
        module='purchases' if kind.startswith('purchase-') else 'pos'
        if row['role']!='owner':
            try:allowed=db.execute(grant,{'userId':row['id'],'module':module}).fetchone()
            except sqlite3.Error:raise PermissionError() from None
            if allowed is not None and not allowed[0]:raise PermissionError()
        if kind in ('purchase-save','purchase-update','purchase-open') and not(main and cost):raise PermissionError()
        return row
    routes=['sale-save','sale-update','sale-list','sale-open','sale-discard','purchase-save','purchase-update','purchase-list','purchase-open','purchase-discard','hold','hold-list','hold-count','hold-resume','hold-discard']
    denials=0
    for route in routes:
        for fault in ('inactive','role','revoke','grant-read'):
            db=seed();require(db,route)
            if fault=='inactive':apply='UPDATE users SET active=0 WHERE id=2';undo='UPDATE users SET active=1 WHERE id=2'
            elif fault=='role':apply="UPDATE users SET role='cashier' WHERE id=2";undo="UPDATE users SET role='manager' WHERE id=2"
            elif fault=='revoke':apply="INSERT INTO user_permissions(user_id,module_key,allowed) VALUES(2,'pos',0),(2,'purchases',0)";undo='DELETE FROM user_permissions WHERE user_id=2'
            else:apply='ALTER TABLE user_permissions RENAME TO hidden_permissions';undo='ALTER TABLE hidden_permissions RENAME TO user_permissions'
            db.execute(apply);db.commit();before=snapshot(db)
            try:require(db,route)
            except PermissionError:denials+=1
            else:raise AssertionError('stale authority allowed')
            assert snapshot(db)==before;db.execute(undo);db.commit();require(db,route);db.close()
        for who in (None,(0,'owner',True),(2,'manager',False),(2,'owner',True)):
            db=seed();before=snapshot(db)
            try:require(db,route,who)
            except PermissionError:denials+=1
            else:raise AssertionError('invalid actor allowed')
            assert snapshot(db)==before;db.close()
    db=seed()
    for kind in ('purchase-list','purchase-discard'):require(db,kind,main=False,cost=False)
    for kind in ('purchase-save','purchase-update','purchase-open'):
        try:require(db,kind,main=False,cost=False)
        except PermissionError:pass
        else:raise AssertionError('cost payload allowed')
    params={'id':1,'type':'sale','marker':-1,'key':'sale-NEW','payload':'{}','byId':2,'by':'Stored author',
            'at':'2026-11-10 12:00:00','reason':'Synthetic','code':'H-NEW','userId':2,'customerId':None,'discount':0,
            'draft':1,'note':'Synthetic','total':30000,'count':1,'basis':'profit','extra':0,'cartId':1,'productId':1,'name':'SYNTHETIC',
            'unit':'base','quantity':1,'price':30000,'position':0,'mode':'normal','percent':0}
    for sql in (type_probe,list_sql,row_sql,held_count,held_list,held_row,held_lines):assert list(db.execute(sql,params)),sql
    assert list(db.execute(type_probe,params))[0][0]=='sale'
    writes=(draft_insert,draft_update,draft_discard,hold_insert,item_insert,resume,discard)
    for sql in writes:
        before=snapshot(db);db.execute('SAVEPOINT probe');db.execute(sql,params)
        db.execute('ROLLBACK TO probe');db.execute('RELEASE probe');assert snapshot(db)==before
    # Actual audit insert with stored name, then a trigger failure rolls back the preceding payload write.
    current=require(db,'sale-update');v=dict(params,userName=current['name'],module='pos',action='draft_updated',entity='document_drafts',entityId='1',oldValue=None,newValue='Synthetic',details=None)
    with db:db.execute(audit,v)
    assert db.execute("SELECT user_name FROM audit_logs").fetchone()[0]=='Stored author'
    db.execute("CREATE TRIGGER synthetic_audit BEFORE INSERT ON audit_logs BEGIN SELECT RAISE(ABORT,'synthetic'); END;");db.commit();before=snapshot(db)
    try:
        with db:db.execute(draft_update,dict(params,payload='{"Lines":[]}'));db.execute(audit,v)
    except sqlite3.IntegrityError:pass
    else:raise AssertionError('audit fault did not fire')
    assert snapshot(db)==before
    db.execute('ALTER TABLE held_carts RENAME TO hidden_carts');db.commit()
    try:db.execute(held_count).fetchone()
    except sqlite3.OperationalError:pass
    else:raise AssertionError('count error became zero')
    db.close()
    print(f'PASS {len(migrations)} migrations; {denials} Python currentactor/module denials+snapshots/regrant; actual7draft/heldreads+7writeSQLprobes/auditINSERT-triggerrollback;metadata-vs-costpayload,countfailure. NOT C#/pricing/whole-draftTx/session/WPF/COMMIT proof.')


if __name__=='__main__':run()
