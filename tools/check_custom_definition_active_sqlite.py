#!/usr/bin/env python3
"""Extracted On/Off UPDATE/audit/receipt SQL on isolated SQLite WAL+DELETE.
Python owns actor/CAS/Tx/outcome assumptions.NOT C#/provider/WPF execution."""
from pathlib import Path
import hashlib
import json
import sqlite3
import tempfile
from check_ledger_posting_sqlite import quoted
ROOT = Path(__file__).resolve().parents[1]


def run():
    core = ROOT / 'dotnet/Area11.Core'
    source = (core / 'Services/CustomFieldService.cs').read_text()
    body = source.split('    public ServiceResult SetActive(', 1)[1].split('    /// <summary>Checked Desktop visibility', 1)[0]
    update = quoted(body, 'UPDATE custom_fields SET active = @active WHERE id = @id;')
    receipt = quoted(source.split('    private CustomDefinitionEditForm CaptureDefinitionEdit(', 1)[1].split('    /// <summary>Switches a field', 1)[0], 'SELECT id AS id')
    baseline = quoted((core / 'Services/EditConcurrency.cs').read_text(), 'SELECT * FROM custom_fields WHERE entity=(SELECT entity FROM custom_fields WHERE id=@id) ORDER BY id;')
    audit = quoted((core / 'Services/AuditService.cs').read_text(), 'INSERT INTO audit_logs')
    migrations = sorted((core / 'Data/Migrations').glob('*.sql')); plans = 0
    with tempfile.TemporaryDirectory(prefix='area11-custom-active-') as folder:
        for mode in ('WAL','DELETE'):
            path = str(Path(folder)/(mode+'.db')); one = sqlite3.connect(path, timeout=.05); two = sqlite3.connect(path, timeout=.05)
            for migration in migrations: one.executescript(migration.read_text())
            one.executescript("""
INSERT INTO users(id,name,role) VALUES(9001,'Synthetic owner','owner');
INSERT INTO customers(id,name) VALUES(9001,'Synthetic customer');
INSERT INTO custom_fields(id,entity,field_key,label,type,sort) VALUES
 (9001,'customer','customer.custom.9001','Synthetic first','text',1),
 (9002,'customer','customer.custom.9002','Synthetic peer','text',2),
 (9003,'supplier','supplier.custom.9003','Synthetic other entity','text',1);
INSERT INTO custom_values(field_id,entity,entity_id,value) VALUES(9001,'customer',9001,'Synthetic historical answer');
""")
            assert one.execute('PRAGMA journal_mode='+mode).fetchone()[0].upper()==mode
            one.execute('PRAGMA foreign_keys=ON'); two.execute('PRAGMA foreign_keys=ON')
            def state(): return tuple(one.iterdump())
            def token(): return hashlib.sha256(json.dumps(one.execute(baseline,dict(id=9001)).fetchall()).encode()).digest()
            def rows(): return one.execute(receipt,dict(entity='customer')).fetchall()
            def active(): return one.execute('SELECT active FROM custom_fields WHERE id=9001').fetchone()[0]
            def change(wanted, expected):
                one.execute('BEGIN IMMEDIATE')
                try:
                    assert one.execute('SELECT role,active FROM users WHERE id=9001').fetchone()==('owner',1)
                    assert expected==token()  # Compared BEFORE the no-op decision.
                    was=active(); before=rows(); answers=one.execute('SELECT * FROM custom_values').fetchall()
                    if was!=wanted:
                        one.execute(update,dict(id=9001,active=wanted))
                        one.execute(audit,dict(at='2026-11-10',userId=9001,userName='Synthetic owner',module='Settings',
                            action='custom_field_on' if wanted else 'custom_field_off',entity='custom_fields',entityId='9001',
                            oldValue='shown' if was else 'put away',newValue='shown' if wanted else 'put away',details='Synthetic first'))
                    saved=rows(); saved_token=token()
                    # The extracted receipt projection puts active at index8. Everything else stays exact.
                    assert [r[:8]+r[9:] for r in saved]==[r[:8]+r[9:] for r in before]
                    assert answers==one.execute('SELECT * FROM custom_values').fetchall()
                    one.commit(); return saved,saved_token
                except BaseException: one.rollback(); raise
            for wanted in (1,0,0,1):
                before=state(); no_op=active()==wanted; saved,saved_token=change(wanted,token())
                assert saved==rows() and saved_token==token() and active()==wanted
                if no_op: assert before==state()
                plans+=1
            for column,value in [('label','Synthetic external'),('sort',99),('active',0),('required',1),('help_text','Synthetic help')]:
                loaded=token(); two.execute(f'UPDATE custom_fields SET {column}=? WHERE id=9002',(value,));two.commit();before=state()
                try: change(0,loaded);raise RuntimeError('stale peer edit accepted')
                except AssertionError: pass
                assert state()==before;plans+=1
            loaded=token();two.execute('UPDATE custom_fields SET active=0 WHERE id=9001');two.commit();before=state()
            try: change(0,loaded);raise RuntimeError('stale desired no-op accepted')
            except AssertionError: pass
            assert before==state();plans+=1
            for stage in ('update','audit'):
                wanted=1-active()
                one.execute('CREATE TRIGGER synthetic_visibility_fault BEFORE '+('UPDATE ON custom_fields' if stage=='update' else "INSERT ON audit_logs WHEN NEW.action IN ('custom_field_on','custom_field_off')")+" BEGIN SELECT RAISE(ABORT,'synthetic'); END;");one.commit()
                loaded=token();before=state()
                try: change(wanted,loaded);raise RuntimeError('fault not reached')
                except sqlite3.DatabaseError: pass
                assert before==state();one.execute('DROP TRIGGER synthetic_visibility_fault');one.commit()
                change(wanted,loaded);assert active()==wanted;plans+=1
            saved=rows();loaded=token();two.execute("UPDATE custom_fields SET label='Synthetic later writer' WHERE id=9002");two.commit()
            assert saved!=rows() and loaded!=token();plans+=1
            loaded=token();two.execute("UPDATE custom_fields SET label='Synthetic unrelated' WHERE id=9003");two.commit()
            change(1-active(),loaded);plans+=1
            loaded=token();two.execute('UPDATE users SET active=0 WHERE id=9001');two.commit();before=state()
            try: change(active(),loaded);raise RuntimeError('revoked no-op accepted')
            except AssertionError: pass
            assert before==state();two.execute('UPDATE users SET active=1 WHERE id=9001');two.commit();plans+=1
            one.executescript("CREATE TABLE synthetic_visibility_commit(parent_id INTEGER REFERENCES users(id) DEFERRABLE INITIALLY DEFERRED); CREATE TRIGGER synthetic_visibility_commit_audit AFTER INSERT ON audit_logs BEGIN INSERT INTO synthetic_visibility_commit VALUES(-99999); END;")
            before=state()
            try: change(1-active(),token());raise RuntimeError('commit fault not reached')
            except sqlite3.IntegrityError: pass
            assert before==state();plans+=1
            one.close();two.close()
    print(f'PASS {len(migrations)} migrations/{plans} extracted OnOff SQL reference plans WAL+DELETE;answers/keys retained.NOT C#/provider/WPF/CAS proof.')


if __name__=='__main__': run()
