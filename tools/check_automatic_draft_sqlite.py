#!/usr/bin/env python3
"""Actual draft/Hold SQL, disposable migrated memory DB. Python owns routing/Tx/payloads.
Not C#/Dapper/Database.Tx/pricing/permissions/WPF/provider-COMMIT execution.
"""
from pathlib import Path
import sqlite3
from check_counter_current_sqlite import quoted
ROOT=Path(__file__).resolve().parents[1]


def run():
    core=ROOT/'dotnet/Area11.Core'
    d=(core/'Services/DraftService.cs').read_text();h=(core/'Services/HoldCartService.cs').read_text()
    draft_insert=quoted(d,'INSERT INTO document_drafts');draft_update=quoted(d,'UPDATE document_drafts SET payload_json')
    hold_insert=quoted(h,'INSERT INTO held_carts');item_insert=quoted(h,'INSERT INTO held_cart_items')
    audit=quoted((core/'Services/AuditService.cs').read_text(),'INSERT INTO audit_logs')
    migrations=sorted((core/'Data/Migrations').glob('*.sql'))
    def seed():
        c=sqlite3.connect(':memory:');c.execute('PRAGMA foreign_keys=ON')
        for p in migrations:c.executescript(p.read_text())
        c.executescript("INSERT INTO users(id,name,role,active) VALUES(1,'Synthetic','owner',1); INSERT INTO products(id,name) VALUES(1,'Synthetic');")
        c.commit();return c
    def snap(c):return list(c.iterdump())
    args=dict(key='sale-SYN',type='sale',payload='{"Lines":[{"ProductId":1,"Quantity":2}]}',byId=1,by='Synthetic',at='2026-11-10 12:00:00',id=1,
              code='H-SYN',userId=1,customerId=None,discount=0,note='Synthetic',total=30000,count=1,basis='profit',extra=0,
              cartId=1,productId=1,name='Synthetic',unit='base',quantity=1,price=30000,position=0,mode='normal',percent=0,
              userName='Synthetic',module='pos',action='draft_saved',entity='document_drafts',entityId='1',oldValue=None,newValue=None,details=None)
    faults=0
    for kind in ('sale','purchase'):
        for update in (False,True):
            for stage in ('before','after','audit-before','audit-after'):
                c=seed();a=dict(args,type=kind,key=kind+'-SYN',module='purchases' if kind=='purchase' else 'pos')
                if update:
                    with c:c.execute(draft_insert,dict(a,payload='{"Lines":[]}'))
                a['action']='draft_updated' if update else 'draft_saved'
                timing='BEFORE' if stage.endswith('before') else 'AFTER'
                target=("INSERT ON audit_logs WHEN NEW.action='"+a['action']+"'") if stage.startswith('audit') else ('UPDATE' if update else 'INSERT')+' ON document_drafts'
                c.execute("CREATE TRIGGER synthetic_fault "+timing+" "+target+" BEGIN SELECT RAISE(ABORT,'synthetic'); END;");c.commit();before=snap(c)
                def save():
                    c.execute(draft_update if update else draft_insert,a);c.execute(audit,a)
                try:
                    with c:save()
                except sqlite3.IntegrityError:pass
                else:raise AssertionError('fault absent')
                assert snap(c)==before;c.execute('DROP TRIGGER synthetic_fault');c.commit()
                with c:save()
                assert c.execute('SELECT COUNT(*) FROM document_drafts').fetchone()[0]==1
                assert c.execute('SELECT payload_json FROM document_drafts').fetchone()[0]==a['payload']
                assert c.execute('SELECT COUNT(*) FROM audit_logs').fetchone()[0]==1
                for table in ('sales','purchases','payments','stock_movements'):assert c.execute('SELECT COUNT(*) FROM '+table).fetchone()[0]==0
                faults+=1;c.close()
    for timing in ('BEFORE','AFTER'):
        for target in ('held_carts','held_cart_items',"audit_logs WHEN NEW.action='cart_hold'"):
            c=seed();a=dict(args,action='cart_hold',entity='held_carts')
            c.execute("CREATE TRIGGER synthetic_fault "+timing+" INSERT ON "+target+" BEGIN SELECT RAISE(ABORT,'synthetic'); END;");c.commit();before=snap(c)
            def hold():
                c.execute(hold_insert,a);c.execute(item_insert,a);c.execute(audit,a)
            try:
                with c:hold()
            except sqlite3.IntegrityError:pass
            else:raise AssertionError('fault absent')
            assert snap(c)==before;c.execute('DROP TRIGGER synthetic_fault');c.commit()
            with c:hold()
            assert c.execute('SELECT COUNT(*) FROM held_carts').fetchone()[0]==1
            assert c.execute('SELECT COUNT(*) FROM held_cart_items').fetchone()[0]==1
            assert c.execute('SELECT COUNT(*) FROM audit_logs').fetchone()[0]==1
            faults+=1;c.close()
    print(f'PASS {len(migrations)} migrations; actual draft insert/update/audit + Hold header/item/audit SQL, {faults} BEFORE/AFTER fault rollbacks and single retries; snapshots/one draft or hold/no-money. NOT C#/UI/permissions/provider-COMMIT proof.')


if __name__=='__main__':run()
