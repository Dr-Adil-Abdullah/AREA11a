#!/usr/bin/env python3
"""Extract Add SQL on isolated WAL/DELETE SQLite. Python owns actor/Tx/outcome assumptions.
NOT execution of C#, Microsoft.Data.Sqlite, WPF, interactive sessions or a durable retry protocol.
"""
from pathlib import Path
import sqlite3
import tempfile
from check_ledger_posting_sqlite import quoted

ROOT = Path(__file__).resolve().parents[1]


def run():
    core = ROOT / 'dotnet/Area11.Core'
    source = (core / 'Services/CustomFieldService.cs').read_text()
    body = source.split('    public ServiceResult<CustomField> Add(', 1)[1].split('    /// <summary>\n    /// Changes the wording', 1)[0]
    insert = quoted(body, 'INSERT INTO custom_fields(')
    stable_key = quoted(body, 'UPDATE custom_fields SET field_key')
    sort = quoted(body, 'SELECT IFNULL(MAX(sort), 0) + 1')
    duplicate = quoted(source.split('    private bool LabelExists(', 1)[1], 'SELECT COUNT(1) FROM custom_fields')
    receipt_body = source.split('    private CustomDefinitionEditForm CaptureDefinitionEdit(', 1)[1].split('    /// <summary>Switches a field', 1)[0]
    receipt = quoted(receipt_body, 'SELECT id AS id')
    audit = quoted((core / 'Services/AuditService.cs').read_text(), 'INSERT INTO audit_logs')
    migrations = sorted((core / 'Data/Migrations').glob('*.sql'))
    plans = 0
    with tempfile.TemporaryDirectory(prefix='area11-custom-add-') as folder:
        for mode in ('WAL', 'DELETE'):
            path = str(Path(folder) / (mode + '.db'))
            one = sqlite3.connect(path, timeout=.1)
            two = sqlite3.connect(path, timeout=.1)
            for migration in migrations:
                one.executescript(migration.read_text())
            one.executescript("""
INSERT INTO users(id,name,role) VALUES(9001,'Synthetic current owner','owner');
INSERT INTO customers(id,name) VALUES(9001,'Synthetic customer');
INSERT INTO custom_fields(id,entity,field_key,label,type,sort) VALUES
 (9001,'customer','customer.custom.9001','Synthetic peer','text',1),
 (9002,'supplier','supplier.custom.9002','Synthetic other entity','text',1);
INSERT INTO custom_values(field_id,entity,entity_id,value) VALUES(9001,'customer',9001,'Synthetic old answer');
""")
            assert one.execute('PRAGMA journal_mode=' + mode).fetchone()[0].upper() == mode
            one.execute('PRAGMA foreign_keys=ON')
            two.execute('PRAGMA foreign_keys=ON')

            def state():
                return tuple(one.iterdump())

            def add(label, entity='customer'):
                commit_attempted = False
                one.execute('BEGIN IMMEDIATE')
                try:
                    # Synthetic Python owner resolver, NOT invocation of OwnerAccess/session validation.
                    actor = one.execute('SELECT name,role,active FROM users WHERE id=9001').fetchone()
                    assert actor is not None and actor[1:] == ('owner', 1)
                    assert not one.execute(duplicate, dict(entity=entity, label=label, id=0)).fetchone()[0]
                    next_sort = one.execute(sort, dict(entity=entity)).fetchone()[0]
                    values = dict(entity=entity, label=label, type='text', optionsJson=None,
                                  helpText='Synthetic help', required=0, sort=next_sort, at='2026-11-10T12:00:00+00:00')
                    cursor = one.execute(insert, values)
                    ident = cursor.lastrowid
                    one.execute(stable_key, dict(fieldKey=f'{entity}.custom.{ident}', id=ident))
                    one.execute(audit, dict(at=values['at'], userId=9001, userName=actor[0], module='Settings',
                                           action='custom_field_add', entity='custom_fields', entityId=str(ident),
                                           oldValue=None, newValue='Synthetic description', details='Synthetic entity'))
                    saved = one.execute(receipt, dict(entity=entity)).fetchall()
                    created = [row for row in saved if row[0] == ident]
                    assert len(created) == 1
                    commit_attempted = True
                    one.commit()
                    return True, created[0], saved, False
                except (sqlite3.DatabaseError, AssertionError):
                    one.rollback()
                    return False, None, None, not commit_attempted

            # All six entities still use the same append/key/audit/receipt SQL.
            for entity in ('product', 'customer', 'supplier', 'sale', 'purchase', 'expense'):
                ok, created, saved, no_commit = add('Synthetic ' + entity, entity)
                assert ok and not no_commit and created in saved
                assert saved == one.execute(receipt, dict(entity=entity)).fetchall()
                assert created[2] == f'{entity}.custom.{created[0]}'
                plans += 1
            for hidden in (False, True):
                if hidden:
                    one.execute("UPDATE custom_fields SET active=0 WHERE label='Synthetic customer'")
                    one.commit()
                before = state()
                ok, created, saved, known = add('Synthetic customer')
                assert not ok and known and created is None and saved is None and state() == before
                plans += 1
            stages = [
                "BEFORE INSERT ON custom_fields BEGIN SELECT RAISE(ABORT,'synthetic'); END;",
                "AFTER INSERT ON custom_fields BEGIN SELECT RAISE(ABORT,'synthetic'); END;",
                "BEFORE UPDATE OF field_key ON custom_fields BEGIN SELECT RAISE(ABORT,'synthetic'); END;",
                "AFTER UPDATE OF field_key ON custom_fields BEGIN SELECT RAISE(ABORT,'synthetic'); END;",
                "BEFORE INSERT ON audit_logs WHEN NEW.action='custom_field_add' BEGIN SELECT RAISE(ABORT,'synthetic'); END;",
                "AFTER INSERT ON audit_logs WHEN NEW.action='custom_field_add' BEGIN SELECT RAISE(ABORT,'synthetic'); END;",
                "AFTER INSERT ON audit_logs WHEN NEW.action='custom_field_add' BEGIN DELETE FROM custom_fields WHERE id=CAST(NEW.entity_id AS INTEGER); END;",
            ]
            for index, stage in enumerate(stages):
                one.execute('CREATE TRIGGER synthetic_fault ' + stage)
                before = state()
                ok, created, saved, known = add('Synthetic retry ' + str(index))
                assert not ok and known and created is None and saved is None and state() == before
                one.execute('DROP TRIGGER synthetic_fault')
                assert add('Synthetic retry ' + str(index))[0]
                plans += 1
            one.executescript("""
CREATE TABLE synthetic_deferred(parent_id INTEGER REFERENCES users(id) DEFERRABLE INITIALLY DEFERRED);
CREATE TRIGGER synthetic_commit_fault AFTER INSERT ON audit_logs WHEN NEW.action='custom_field_add'
BEGIN INSERT INTO synthetic_deferred VALUES(-99999); END;
""")
            before = state()
            ok, created, saved, known = add('Synthetic deferred')
            assert not ok and not known and created is None and saved is None and state() == before
            one.execute('DROP TRIGGER synthetic_commit_fault')
            plans += 1
            for change in ('active=0', "role='manager'"):
                one.execute('UPDATE users SET ' + change + ' WHERE id=9001')
                one.commit()
                before = state()
                assert add('Synthetic denied') == (False, None, None, True) and state() == before
                one.execute("UPDATE users SET active=1,role='owner' WHERE id=9001")
                one.commit()
                plans += 1
            # A second writer's legitimate peer edit is retained, not overwritten by this append.
            two.execute("UPDATE custom_fields SET sort=900,label='Synthetic external peer' WHERE id=9001")
            two.commit()
            ok, created, saved, known = add('Synthetic after peer')
            assert ok and created[9] == 901 and any(row[3] == 'Synthetic external peer' for row in saved)
            snapshot = list(saved)
            two.execute("UPDATE custom_fields SET label='Synthetic later peer' WHERE id=9001")
            two.commit()
            assert saved == snapshot and saved != one.execute(receipt, dict(entity='customer')).fetchall()
            plans += 1
            one.execute("CREATE TRIGGER synthetic_timestamp AFTER INSERT ON custom_fields BEGIN UPDATE custom_fields SET created_at='2001-01-02' WHERE id=NEW.id; END;")
            ok, created, saved, known = add('Synthetic stored timestamp')
            assert ok and created[10] == '2001-01-02'
            plans += 1
            assert one.execute('SELECT value FROM custom_values WHERE field_id=9001').fetchone()[0] == 'Synthetic old answer'
            assert one.execute('SELECT label FROM custom_fields WHERE id=9002').fetchone()[0] == 'Synthetic other entity'
            two.close()
            one.close()
    print(f'PASS {len(migrations)} migrations/{plans} isolated Add extracted-SQL plans, WAL+DELETE; NOT C#/provider/WPF proof')


if __name__ == '__main__':
    run()
