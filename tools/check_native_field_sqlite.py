#!/usr/bin/env python3
"""Extracted native UPDATE/read/audit SQL on isolated SQLite WAL/DELETE databases.
Python owns actor/CAS/Tx/outcome assumptions;NOT C# Database/provider/WPF execution."""
from pathlib import Path
import hashlib
import json
import sqlite3
import tempfile
from check_ledger_posting_sqlite import quoted
ROOT = Path(__file__).resolve().parents[1]


def run():
    core = ROOT / 'dotnet/Area11.Core'
    source = (core / 'Services/NativeFieldPolicyService.cs').read_text()
    update = quoted(source, 'UPDATE form_field_definitions')
    select = quoted(source.split('private FormFieldDefinition CaptureEdit(', 1)[1], 'SELECT * FROM form_field_definitions WHERE id')
    audit = quoted((core / 'Services/AuditService.cs').read_text(), 'INSERT INTO audit_logs')
    migrations = sorted((core / 'Data/Migrations').glob('*.sql')); plans = 0
    with tempfile.TemporaryDirectory(prefix='area11-native-field-') as folder:
        for mode in ('WAL', 'DELETE'):
            path = str(Path(folder) / (mode + '.db'))
            one = sqlite3.connect(path, timeout=.05); two = sqlite3.connect(path, timeout=.05)
            for migration in migrations: one.executescript(migration.read_text())
            one.execute("INSERT INTO users(id,name,role) VALUES(9001,'Synthetic owner','owner');")
            one.execute("INSERT INTO form_field_definitions(id,field_key,section_key,label,data_type,input_method,safe_required,required,critical) VALUES(9001,'sale.synthetic','Sale','Synthetic quantity','quantity','numeric',1,1,1);")
            one.execute("INSERT INTO form_field_definitions(id,field_key,section_key,label,data_type,input_method) VALUES(9002,'customer.synthetic','Customer','Synthetic phone','text','text');"); one.commit()
            assert one.execute('PRAGMA journal_mode=' + mode).fetchone()[0].upper() == mode
            one.execute('PRAGMA foreign_keys=ON'); two.execute('PRAGMA foreign_keys=ON')
            def row(db=one, ident=9001): return db.execute(select, dict(id=ident)).fetchone()
            def token(db=one, ident=9001): return hashlib.sha256(json.dumps(row(db, ident)).encode()).digest()
            def state(): return tuple(one.iterdump())
            def change(required, expected, confirmed=True):
                one.execute('BEGIN IMMEDIATE')
                try:
                    assert one.execute('SELECT role,active FROM users WHERE id=9001').fetchone() == ('owner', 1)
                    assert token() == expected
                    current = one.execute('SELECT required,critical,safe_required FROM form_field_definitions WHERE id=9001').fetchone()
                    assert current is not None
                    if current[0] != required:
                        assert required or not (current[1] and current[2]) or confirmed
                        one.execute(update, dict(id=9001, required=required, by='Synthetic owner' if not required else None,
                            ackAt='2026-11-25' if not required else None, at='2026-11-25'))
                        one.execute(audit, dict(at='2026-11-25', userId=9001, userName='Synthetic owner', module='Settings',
                            action='native_field_required_changed', entity='form_field_definitions', entityId='9001',
                            oldValue='required' if current[0] else 'optional', newValue='required' if required else 'optional', details='synthetic only'))
                    receipt = row(); saved = token(); one.commit(); return receipt, saved
                except BaseException:
                    one.rollback(); raise
            # No-op/prepare are read-only but still compare current owner and baseline.
            before = state(); loaded = token(); one.execute('BEGIN IMMEDIATE'); assert token() == loaded; one.commit()
            assert before == state(); plans += 1
            change(1, loaded); assert before == state(); plans += 1
            try: change(0, loaded, confirmed=False); raise RuntimeError('missing risk gate')
            except AssertionError: pass
            assert before == state(); plans += 1
            saved_row, saved_token = change(0, loaded); assert saved_row == row(); assert saved_token == token(); plans += 1
            for fault in ('update', 'audit'):
                one.execute('CREATE TRIGGER synthetic_native_fault BEFORE ' + ('UPDATE ON form_field_definitions' if fault == 'update' else "INSERT ON audit_logs WHEN NEW.action='native_field_required_changed'") + " BEGIN SELECT RAISE(ABORT,'synthetic'); END;"); one.commit()
                before = state()
                try: change(1, saved_token); raise RuntimeError('fault not reached')
                except sqlite3.DatabaseError: pass
                assert before == state()
                one.execute('DROP TRIGGER synthetic_native_fault'); one.commit(); plans += 1
            # Independent writer metadata edits,including risk descriptors and requested no-op.
            for column, value in [('label','Synthetic changed'), ('critical',0), ('safe_required',0), ('required',1), ('help_text','Synthetic help')]:
                loaded = token(); two.execute(f'UPDATE form_field_definitions SET {column}=? WHERE id=9001', (value,)); two.commit(); before = state()
                try: change(1, loaded); raise RuntimeError('stale baseline accepted')
                except AssertionError: pass
                assert before == state(); plans += 1
            # Other target is independent,not folded into the exact target receipt.
            loaded = token(); two.execute("UPDATE form_field_definitions SET required=1 WHERE id=9002"); two.commit()
            change(1, loaded); assert row(ident=9002)[0] == 9002; plans += 1
            # Old receipt never adopts a later metadata writer.
            original = row(); loaded = token(); two.execute("UPDATE form_field_definitions SET label='Synthetic later writer' WHERE id=9001"); two.commit()
            assert row() != original and token() != loaded; plans += 1
            loaded = token(); two.execute('UPDATE users SET active=0 WHERE id=9001'); two.commit(); before = state()
            try: change(0, loaded); raise RuntimeError('revoked actor accepted')
            except AssertionError: pass
            assert before == state(); two.execute('UPDATE users SET active=1 WHERE id=9001'); two.commit(); plans += 1
            # Deferred COMMIT fault is synthetic,not provider lost-response injection.
            one.executescript("CREATE TABLE synthetic_native_commit(parent_id INTEGER REFERENCES users(id) DEFERRABLE INITIALLY DEFERRED); CREATE TRIGGER synthetic_native_commit_audit AFTER INSERT ON audit_logs BEGIN INSERT INTO synthetic_native_commit VALUES(-99999); END;")
            loaded = token(); before = state()
            try: change(0, loaded); raise RuntimeError('commit fault not reached')
            except sqlite3.IntegrityError: pass
            assert before == state(); plans += 1
            one.close(); two.close()
    print(f'PASS {len(migrations)} migrations/{plans} extracted-SQL reference plans (WAL+DELETE). NOT C#/provider/WPF/CAS implementation proof.')


if __name__ == '__main__': run()
