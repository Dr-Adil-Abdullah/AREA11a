#!/usr/bin/env python3
"""Actual PR reader/actor/grant SELECTs; Python owns authority policy and routing.
Not C#/Database.Read/Tx/receipt Compose/WPF/session/CAS execution or certification.
"""
from pathlib import Path
import sqlite3
from check_counter_current_sqlite import quoted
ROOT=Path(__file__).resolve().parents[1]
S=ROOT/'dotnet/Area11.Core/Services'


def run():
    src=(S/'ProvisionalReturnService.cs').read_text()
    actor=quoted((S/'SnapshotReadAccess.cs').read_text(),'SELECT id, name, role, active FROM users')
    grant=quoted((S/'PermissionService.cs').read_text().split('public bool Can(',1)[1].split('public static bool IsFinancialVisibility(',1)[0],'SELECT allowed FROM user_permissions')
    base=quoted(src,'SELECT p.id AS id')
    order=quoted(src,'ORDER BY p.id DESC;')
    lines=quoted(src,'SELECT i.qty_entered AS qtyEntered')
    search=quoted(src,'SELECT s.id AS id, s.code AS code')
    migrations=sorted((ROOT/'dotnet/Area11.Core/Data/Migrations').glob('*.sql'))
    c=sqlite3.connect(':memory:');c.row_factory=sqlite3.Row;c.execute('PRAGMA foreign_keys=ON')
    for p in migrations:c.executescript(p.read_text())
    c.executescript("""INSERT INTO users(id,name,role,active) VALUES(1,'Stored owner','owner',1),(2,'Stored manager','manager',1),(3,'Stored cashier','cashier',1);
INSERT INTO products(id,name) VALUES(1,'Synthetic');
INSERT INTO sales(id,code,status,total_paisa,user_id) VALUES(1,'INV-SYN','paid',90000,1);
INSERT INTO sale_items(id,sale_id,product_id,name_snapshot,qty_base,line_total_paisa) VALUES(1,1,1,'Synthetic',3,90000);
INSERT INTO provisional_returns(id,code,refund_paisa,status,user_id) VALUES(1,'PR-SYN',30000,'pending',1);
INSERT INTO provisional_items(id,provisional_id,product_id,qty_base,qty_entered,unit_entered,refund_paisa) VALUES(1,1,1,1,1,'base',30000);
""");c.commit()
    snap=lambda:list(c.iterdump())
    def allowed(a):
        if a is None or not a.get('active',True) or a['id']<=0:return None
        current=c.execute(actor,a).fetchone()
        if current is None or not current['active'] or current['role']!=a['role']:return None
        g=c.execute(grant,dict(userId=current['id'],module='returns')).fetchone()
        if current['role']=='owner' or g is None or g[0]:return current
        return None
    def read(route,a):
        if allowed(a) is None:return None
        if route=='page':return list(c.execute(base+order))
        if route=='header':return list(c.execute(base+'WHERE upper(p.code) = upper(@code)\n'+order,dict(code='PR-SYN')))
        if route=='lines':return list(c.execute(lines,dict(id=1)))
        if route=='search':return list(c.execute(search,dict(like='%SYN%',limit=40)))
        if route=='receipt':
            row=c.execute(base+'WHERE upper(p.code) = upper(@code)\n'+order,dict(code='PR-SYN')).fetchone()
            content=list(c.execute(lines,dict(id=row['id'])))
            assert len(content)==row['itemCount'] and len(content)>0
            return content
        raise AssertionError(route)
    denials=0
    for route in ('page','header','lines','search','receipt'):
        for fault in ('null','missing','inactive-input','inactive-stored','role-changed','revoked','owner-inactive'):
            a=dict(id=2,role='manager',active=True)
            if fault=='null':a=None
            elif fault=='missing':a['id']=99999
            elif fault=='inactive-input':a['active']=False
            elif fault=='inactive-stored':c.execute('UPDATE users SET active=0 WHERE id=2')
            elif fault=='role-changed':c.execute("UPDATE users SET role='cashier' WHERE id=2")
            elif fault=='revoked':c.execute("INSERT INTO user_permissions(user_id,module_key,allowed) VALUES(2,'returns',0)")
            else:c.execute('UPDATE users SET active=0 WHERE id=1');a=dict(id=1,role='owner')
            c.commit();before=snap();assert read(route,a) is None;assert snap()==before;denials+=1
            c.execute("UPDATE users SET active=1,role='manager' WHERE id=2");c.execute('UPDATE users SET active=1 WHERE id=1');c.execute('DELETE FROM user_permissions');c.commit()
    before=snap()
    for user,role in ((1,'owner'),(2,'manager'),(3,'cashier')):
        a=dict(id=user,role=role)
        for route in ('page','header','lines','search','receipt'):assert len(read(route,a))==1
        assert allowed(a)['name']=='Stored '+role
    assert snap()==before
    assert not list(c.execute(base+'WHERE upper(p.code) = upper(@code)\n'+order,dict(code="PR-SYN' OR 1=1 --")))
    assert not list(c.execute(lines,dict(id=99999)))
    c.execute('ALTER TABLE provisional_items RENAME COLUMN unit_entered TO synthetic_unreadable_unit');c.commit()
    assert c.execute(base+order).fetchone()['itemCount']==1
    try:read('receipt',dict(id=2,role='manager'))
    except sqlite3.OperationalError:pass
    else:raise AssertionError('partial slip would have passed')
    assert not list(c.execute('PRAGMA foreign_key_check'))
    print(f'PASS {len(migrations)} migrations;{denials} Python current-account/grant denials+unchanged dumps,regrant,3 readers,actual PR header/page/lines/search SQL,stored names,bound IDs,line-fault prevents full read.NOT C#/permissions service/Compose/WPF/session/CAS proof.')


if __name__=='__main__':run()
