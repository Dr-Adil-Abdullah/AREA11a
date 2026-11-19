#!/usr/bin/env python3
"""Actual P36 SQL on synthetic migrated in-memory SQLite. Python owns transactions/payload.
NOT C#/Dapper/Database.Tx/provider COMMIT/crash/WPF/authorization/pricing execution.
"""
from pathlib import Path
import json
import sqlite3
from check_counter_current_sqlite import quoted
ROOT=Path(__file__).resolve().parents[1]


def run():
    core=ROOT/'dotnet/Area11.Core';h=(core/'Services/HoldCartService.cs').read_text();d=(core/'Services/DraftService.cs').read_text()
    insert=quoted(h,'INSERT INTO document_drafts');consume=quoted(h,"UPDATE held_carts SET status = 'resumed'")
    premark=quoted(d,'UPDATE document_drafts\nSET payload_json');restore=quoted(d,"UPDATE document_drafts\nSET status = 'draft'")
    finalize=quoted(d,'UPDATE document_drafts SET posted_document_id');audit=quoted((core/'Services/AuditService.cs').read_text(),'INSERT INTO audit_logs')
    migrations=sorted((core/'Data/Migrations').glob('*.sql'))
    def seed():
        c=sqlite3.connect(':memory:');c.execute('PRAGMA foreign_keys=ON')
        for p in migrations:c.executescript(p.read_text())
        c.executescript("INSERT INTO users(id,name,role,active) VALUES(1,'Synthetic','owner',1); INSERT INTO held_carts(id,code,user_id,total_paisa,items_count) VALUES(1,'H-SYN',1,31000,1);")
        c.commit();return c
    args=dict(key='held-1-SYN',payload=json.dumps({'Lines':[{'ProductId':1,'Quantity':1,'UnitPricePaisa':31000}]}),byId=1,by='Synthetic',at='2026-11-10 12:00:00',id=1,type='sale',marker=-1)
    def snap(c):return list(c.iterdump())
    def resume(c):
        c.execute(insert,args);draft=c.execute('SELECT last_insert_rowid()').fetchone()[0]
        assert c.execute(consume,dict(args,draft=draft)).rowcount==1
        for action,entity in [('draft_saved','document_drafts'),('cart_resume','held_carts')]:
            c.execute(audit,dict(args,userId=1,userName='Synthetic',module='pos',action=action,entity=entity,entityId='1',oldValue=None,newValue=None,details='Synthetic'))
        return draft
    for trigger in ('BEFORE INSERT ON document_drafts','BEFORE UPDATE ON held_carts',
                    "BEFORE INSERT ON audit_logs WHEN NEW.action='draft_saved'","BEFORE INSERT ON audit_logs WHEN NEW.action='cart_resume'"):
        c=seed();c.execute("CREATE TRIGGER synthetic_fault "+trigger+" BEGIN SELECT RAISE(ABORT,'synthetic'); END;");c.commit();before=snap(c)
        try:
            with c:resume(c)
        except sqlite3.IntegrityError:pass
        else:raise AssertionError('missing injected fault')
        assert snap(c)==before;c.execute('DROP TRIGGER synthetic_fault');c.commit()
        with c:draft=resume(c)
        assert c.execute('SELECT status,recovery_draft_id FROM held_carts').fetchone()==('resumed',draft)
        assert c.execute('SELECT COUNT(*) FROM document_drafts').fetchone()[0]==1
        assert c.execute('SELECT COUNT(*) FROM audit_logs').fetchone()[0]==2
        # The exact consume statement cannot consume a historical row again.
        assert c.execute(consume,dict(args,draft=draft)).rowcount==0
        c.close()
    c=seed()
    with c:resume(c)
    with c:c.execute(premark,args)
    assert c.execute('SELECT status,posted_document_id FROM document_drafts').fetchone()==('posted',-1)
    # Unknown path makes NO restoring SQL call; marker survives independent reads.
    assert c.execute('SELECT payload_json FROM document_drafts').fetchone()[0]==args['payload']
    for trigger in ("BEGIN SELECT RAISE(ABORT,'synthetic'); END",'BEGIN SELECT RAISE(IGNORE); END'):
        c.execute("CREATE TRIGGER synthetic_restore BEFORE UPDATE ON document_drafts WHEN NEW.status='draft' "+trigger);c.commit()
        try:
            with c:assert c.execute(restore,args).rowcount==0
        except sqlite3.IntegrityError:pass
        assert c.execute('SELECT status,posted_document_id FROM document_drafts').fetchone()==('posted',-1)
        c.execute('DROP TRIGGER synthetic_restore');c.commit()
    with c:assert c.execute(restore,args).rowcount==1
    assert c.execute('SELECT status,posted_document_id FROM document_drafts').fetchone()==('draft',None)
    with c:c.execute(premark,args)
    assert c.execute(restore,dict(args,type='purchase')).rowcount==0
    assert c.execute(finalize,dict(args,document=42,type='purchase')).rowcount==0
    with c:assert c.execute(finalize,dict(args,document=42)).rowcount==1
    assert c.execute(restore,args).rowcount==0
    for table in ('sales','purchases','payments','stock_movements'):assert c.execute('SELECT COUNT(*) FROM '+table).fetchone()[0]==0
    # New link is unique and constrained; existing resumed history remains NULL, not replayed.
    c.execute("INSERT INTO held_carts(code,user_id,total_paisa,items_count,status) VALUES('H-OLD',1,0,0,'resumed')")
    assert c.execute("SELECT recovery_draft_id FROM held_carts WHERE code='H-OLD'").fetchone()[0] is None
    c.commit()
    for value in (1,999):
        try:
            with c:c.execute("UPDATE held_carts SET recovery_draft_id=? WHERE code='H-OLD'",(value,))
        except sqlite3.IntegrityError:pass
        else:raise AssertionError('link constraint absent')
    print(f'PASS {len(migrations)} migrations; actual recovery INSERT/consume/2audits with4 fault rollbacks+retry; marker/restore ABORT+IGNORE/typedfinalize/unique+FK/legacyNULL; no money rows. NOT C#/provider COMMIT/crash/WPF proof.')


if __name__=='__main__':run()
