#!/usr/bin/env python3
"""Isolated extracted definition UPDATE/duplicate/receipt/audit SQL,Python-owned actor/CAS/Tx.
NOT execution of C# validation/EditConcurrency/Database/outcome/session/provider/WPF."""
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
    update_body = source.split('    public ServiceResult Update(', 1)[1].split('    /// <summary>Desktop checked door', 1)[0]
    update = quoted(update_body, 'UPDATE custom_fields')
    duplicate = quoted(source.split('    private bool LabelExists(', 1)[1].split('    private CustomField? ReadField(', 1)[0], 'SELECT COUNT(1) FROM custom_fields')
    receipt = quoted(source.split('    private CustomDefinitionEditForm CaptureDefinitionEdit(', 1)[1].split('    /// <summary>Switches a field', 1)[0], 'SELECT id AS id')
    baseline = quoted((core / 'Services/EditConcurrency.cs').read_text(), 'SELECT * FROM custom_fields WHERE entity=(SELECT entity FROM custom_fields WHERE id=@id) ORDER BY id;')
    audit = quoted((core / 'Services/AuditService.cs').read_text(), 'INSERT INTO audit_logs')
    migrations = sorted((core / 'Data/Migrations').glob('*.sql')); plans = 0
    with tempfile.TemporaryDirectory(prefix='area11-custom-update-') as folder:
        for mode in ('WAL', 'DELETE'):
            path = str(Path(folder) / (mode + '.db'))
            one = sqlite3.connect(path, timeout=.05); two = sqlite3.connect(path, timeout=.05)
            for migration in migrations: one.executescript(migration.read_text())
            one.executescript("""
INSERT INTO users(id,name,role) VALUES(9001,'Synthetic owner','owner');
INSERT INTO customers(id,name) VALUES(9001,'Synthetic customer');
INSERT INTO custom_fields(id,entity,field_key,label,type,sort) VALUES
 (9001,'customer','customer.custom.9001','Synthetic first','text',1),
 (9002,'customer','customer.custom.9002','Synthetic second','text',2),
 (9003,'supplier','supplier.custom.9003','Synthetic other entity','text',1);
INSERT INTO custom_values(field_id,entity,entity_id,value) VALUES(9001,'customer',9001,'Synthetic old text');
""")
            assert one.execute('PRAGMA journal_mode=' + mode).fetchone()[0].upper() == mode
            one.execute('PRAGMA foreign_keys=ON'); two.execute('PRAGMA foreign_keys=ON')
            def state(): return tuple(one.iterdump())
            def token(): return hashlib.sha256(json.dumps(one.execute(baseline, dict(id=9001)).fetchall()).encode()).digest()
            def rows(): return one.execute(receipt, dict(entity='customer')).fetchall()
            def answers(): return one.execute('SELECT * FROM custom_values ORDER BY field_id,entity_id').fetchall()
            def change(expected, label='Synthetic renamed', kind='text', help_text=''):
                one.execute('BEGIN IMMEDIATE')
                try:
                    assert one.execute('SELECT role,active FROM users WHERE id=9001').fetchone() == ('owner',1)
                    assert expected == token()
                    assert one.execute(duplicate, dict(entity='customer', label=label, id=9001)).fetchone()[0] == 0
                    before = one.execute('SELECT label,type,help_text,required FROM custom_fields WHERE id=9001').fetchone(); assert before is not None
                    one.execute(update, dict(id=9001, fieldKey='customer.custom.9001', label=label, type=kind, optionsJson=None, helpText=help_text, required=0))
                    if before != (label,kind,help_text,0):
                        one.execute(audit, dict(at='2026-11-10',userId=9001,userName='Synthetic owner',module='Settings',action='custom_field_update',
                            entity='custom_fields',entityId='9001',oldValue=json.dumps(before),newValue=json.dumps((label,kind,help_text,0)),details='customer'))
                    saved = rows(); saved_token = token(); one.commit(); return saved, saved_token
                except BaseException: one.rollback(); raise
            before = state(); loaded = token(); change(loaded, 'Synthetic first'); assert state() == before; plans += 1
            saved, saved_token = change(loaded); assert saved == rows() and saved_token == token(); plans += 1
            old_answers = answers(); change(token(), kind='number'); assert answers() == old_answers; plans += 1
            for column,value in [('label','Synthetic changed'),('sort',99),('active',0),('required',1),('help_text','Synthetic help')]:
                loaded = token(); two.execute(f'UPDATE custom_fields SET {column}=? WHERE id=9002',(value,)); two.commit(); before = state()
                try: change(loaded); raise RuntimeError('stale edit accepted')
                except AssertionError: pass
                assert before == state(); plans += 1
            before = state()
            try: change(token(), 'Synthetic changed'); raise RuntimeError('duplicate accepted')
            except AssertionError: pass
            assert before == state(); plans += 1
            for stage in ('update','audit'):
                one.execute('CREATE TRIGGER synthetic_definition_fault BEFORE ' + ('UPDATE ON custom_fields' if stage=='update' else "INSERT ON audit_logs WHEN NEW.action='custom_field_update'") + " BEGIN SELECT RAISE(ABORT,'synthetic'); END;"); one.commit()
                loaded = token(); before = state()
                try: change(loaded, 'Synthetic fault edit'); raise RuntimeError('fault not reached')
                except sqlite3.DatabaseError: pass
                assert before == state(); one.execute('DROP TRIGGER synthetic_definition_fault'); one.commit(); plans += 1
            saved = rows(); loaded = token(); two.execute("UPDATE custom_fields SET label='Synthetic later writer' WHERE id=9002"); two.commit()
            assert saved != rows() and loaded != token(); plans += 1
            loaded = token(); two.execute("UPDATE custom_fields SET label='Synthetic unrelated writer' WHERE id=9003"); two.commit()
            change(loaded, 'Synthetic same-entity-only'); plans += 1
            loaded = token(); two.execute('UPDATE users SET active=0 WHERE id=9001'); two.commit(); before = state()
            try: change(loaded); raise RuntimeError('revoked owner accepted')
            except AssertionError: pass
            assert before == state(); two.execute('UPDATE users SET active=1 WHERE id=9001'); two.commit(); plans += 1
            one.executescript("CREATE TABLE synthetic_definition_commit(parent_id INTEGER REFERENCES users(id) DEFERRABLE INITIALLY DEFERRED); CREATE TRIGGER synthetic_definition_commit_audit AFTER INSERT ON audit_logs BEGIN INSERT INTO synthetic_definition_commit VALUES(-99999); END;")
            before = state()
            try: change(token(), 'Synthetic commit-fault'); raise RuntimeError('commit fault not reached')
            except sqlite3.IntegrityError: pass
            assert before == state(); plans += 1
            one.close(); two.close()
    print(f'PASS {len(migrations)} migrations/{plans} extracted SQL plans WAL+DELETE;answers retained.NOT C#/provider/WPF/CAS implementation proof.')


if __name__ == '__main__': run()
