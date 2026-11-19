#!/usr/bin/env python3
"""Actual actor/grant/Return lookup SQL, synthetic migrated SQLite. Python owns authority policy.
NOT C# services/Compute/Plan/Database.Tx/session/GUI/financial posting execution.
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
    actor_sql=quoted((S/'SnapshotReadAccess.cs').read_text(),'SELECT id, name, role, active FROM users')
    grant_sql=quoted((S/'PermissionService.cs').read_text().split('public bool Can(',1)[1].split('public static bool IsFinancialVisibility(',1)[0],'SELECT allowed FROM user_permissions')
    returns=(S/'ReturnService.cs').read_text();find=quoted(returns,'SELECT id FROM sales WHERE upper(code)')
    search=quoted(returns,'SELECT s.id AS saleId, s.code AS code')
    migrations=sorted((ROOT/'dotnet/Area11.Core/Data/Migrations').glob('*.sql'))
    def seed():
        db=sqlite3.connect(':memory:');db.row_factory=sqlite3.Row;db.execute('PRAGMA foreign_keys=ON')
        for p in migrations:db.executescript(p.read_text())
        db.executescript("""
INSERT INTO users(id,name,role,active) VALUES(1,'Stored owner','owner',1),(2,'Stored manager','manager',1);
INSERT INTO sales(code,date,user_id,total_paisa) VALUES('INV-SYN','2026-11-10 12:00:00',1,30000);
""");db.commit();return db
    def authorize(db,module,actor=(2,'manager',True)):
        # Explicit Python analogue of SnapshotReadAccess/PermissionService; no C# runtime claim.
        if actor is None or actor[0]<=0 or not actor[2]:return False
        try:
            current=db.execute(actor_sql,{'id':actor[0]}).fetchone()
            if current is None or not current['active'] or current['role']!=actor[1]:return False
            if current['role']=='owner':return True # Existing ordinary Owner bypass preserved.
            grant=db.execute(grant_sql,{'userId':actor[0],'module':module}).fetchone()
            return bool(grant[0]) if grant else current['role'] in ('manager','cashier','salesman')
        except sqlite3.DatabaseError:return False
    def read(db,module,actor=(2,'manager',True)):
        if not authorize(db,module,actor):return None
        exact=db.execute(find,{'code':'inv-syn'}).fetchone()[0]
        found=list(db.execute(search,{'term':'INV-SYN','like':'%INV-SYN%','digits':'','fromDate':'','toDate':''}))
        return exact,len(found)
    denied=0
    for module in ('pos','returns'):
        for fault in ('inactive','role','revoke','grant-read'):
            db=seed();assert read(db,module)==(1,1)
            if fault=='inactive':db.execute('UPDATE users SET active=0 WHERE id=2')
            elif fault=='role':db.execute("UPDATE users SET role='cashier' WHERE id=2")
            elif fault=='revoke':db.execute('INSERT INTO user_permissions(user_id,module_key,allowed) VALUES(2,?,0)',(module,))
            else:db.execute('ALTER TABLE user_permissions RENAME TO hidden_permissions')
            db.commit();before=list(db.iterdump());assert read(db,module) is None;assert list(db.iterdump())==before;denied+=1
            if fault=='inactive':db.execute('UPDATE users SET active=1 WHERE id=2')
            elif fault=='role':db.execute("UPDATE users SET role='manager' WHERE id=2")
            elif fault=='revoke':db.execute('UPDATE user_permissions SET allowed=1 WHERE user_id=2 AND module_key=?',(module,))
            else:db.execute('ALTER TABLE hidden_permissions RENAME TO user_permissions')
            db.commit();assert read(db,module)==(1,1);db.close()
        for actor in (None,(0,'owner',True),(2,'manager',False),(2,'owner',True)):
            db=seed();before=list(db.iterdump());assert read(db,module,actor) is None;assert list(db.iterdump())==before;db.close();denied+=1
    db=seed();db.execute("INSERT INTO user_permissions(user_id,module_key,allowed) VALUES(2,'pos',0)");db.commit()
    assert read(db,'pos') is None and read(db,'returns')==(1,1)
    db.execute('ALTER TABLE user_permissions RENAME TO hidden_permissions');db.commit();assert read(db,'pos',(1,'owner',True))==(1,1)
    db.close()
    print(f'PASS {len(migrations)} migrations; {denied} current-actor/module denials,unchanged snapshots,regrant,actual Return find/search SQL,module independence,ordinaryOwner bypass. Python owns policy; NOT C#/posting/session/WPF proof.')


if __name__=='__main__':run()
