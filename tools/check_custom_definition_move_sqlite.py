#!/usr/bin/env python3
"""Isolated extracted Move/receipt SQL on WAL+DELETE. Python owns CAS/actor/Tx assumptions.
NOT C#/Microsoft.Data.Sqlite/session/WPF execution or durable retry identity proof.
"""
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
    body = source.split('    public ServiceResult Move(', 1)[1].split('    /// <summary>Desktop reorder door', 1)[0]
    ordered_sql = quoted(body, 'SELECT id AS id')
    update = quoted(body, 'UPDATE custom_fields SET sort = @sort')
    receipt_body = source.split('    private CustomDefinitionEditForm CaptureDefinitionEdit(', 1)[1].split('    /// <summary>Switches a field', 1)[0]
    receipt_sql = quoted(receipt_body, 'SELECT id AS id')
    baseline = quoted((core / 'Services/EditConcurrency.cs').read_text(), 'SELECT * FROM custom_fields WHERE entity=(SELECT entity FROM custom_fields WHERE id=@id) ORDER BY id;')
    audit = quoted((core / 'Services/AuditService.cs').read_text(), 'INSERT INTO audit_logs')
    migrations = sorted((core / 'Data/Migrations').glob('*.sql'))
    plans = 0
    with tempfile.TemporaryDirectory(prefix='area11-custom-move-') as folder:
        for mode in ('WAL', 'DELETE'):
            one = sqlite3.connect(str(Path(folder) / (mode + '.db')), timeout=.1)
            two = sqlite3.connect(str(Path(folder) / (mode + '.db')), timeout=.1)
            for migration in migrations:
                one.executescript(migration.read_text())
            one.executescript("""
INSERT INTO users(id,name,role) VALUES(9001,'Synthetic current owner','owner');
INSERT INTO customers(id,name) VALUES(9001,'Synthetic customer');
INSERT INTO custom_fields(id,entity,field_key,label,type,sort,active) VALUES
 (9001,'customer','customer.custom.9001','Synthetic first','text',1,0),
 (9002,'customer','customer.custom.9002','Synthetic middle','text',2,1),
 (9003,'customer','customer.custom.9003','Synthetic last','text',3,1),
 (9004,'supplier','supplier.custom.9004','Synthetic supplier','text',1,1);
INSERT INTO custom_values(field_id,entity,entity_id,value) VALUES(9001,'customer',9001,'Synthetic old answer');
""")
            assert one.execute('PRAGMA journal_mode=' + mode).fetchone()[0].upper() == mode
            one.execute('PRAGMA foreign_keys=ON')
            two.execute('PRAGMA foreign_keys=ON')

            def state():
                return tuple(one.iterdump())

            def token(ident=9002):
                return hashlib.sha256(json.dumps(one.execute(baseline, dict(id=ident)).fetchall()).encode()).digest()

            def reset(sorts=(1, 2, 3)):
                for ident, value in zip((9001, 9002, 9003), sorts):
                    one.execute('UPDATE custom_fields SET sort=? WHERE id=?', (value, ident))
                one.commit()

            def move(expected, ident=9002, direction=1):
                commit_attempted = False
                one.execute('BEGIN IMMEDIATE')
                try:
                    # Synthetic Python checks, NOT OwnerAccess/session/EditConcurrency execution.
                    assert one.execute('SELECT role,active FROM users WHERE id=9001').fetchone() == ('owner', 1), 'owner'
                    assert direction in (-1, 1), 'direction'
                    assert expected == token(ident), 'baseline'
                    field = one.execute('SELECT entity,sort,label FROM custom_fields WHERE id=?', (ident,)).fetchone()
                    assert field is not None, 'missing'
                    before = one.execute(ordered_sql, dict(entity=field[0])).fetchall()
                    ids = [row[0] for row in before]
                    index = ids.index(ident)
                    target = index + direction
                    assert 0 <= target < len(ids), 'boundary'
                    ids[index], ids[target] = ids[target], ids[index]
                    answers = one.execute('SELECT * FROM custom_values').fetchall()
                    for position, row_id in enumerate(ids, 1):
                        one.execute(update, dict(sort=position, id=row_id))
                    one.execute(audit, dict(at='2026-11-10', userId=9001, userName='Synthetic current owner',
                                           module='Settings', action='custom_field_order', entity='custom_fields', entityId=str(ident),
                                           oldValue=str(field[1]), newValue=str(target + 1), details=field[2]))
                    saved = one.execute(receipt_sql, dict(entity=field[0])).fetchall()
                    assert [row[0] for row in saved] == ids, 'receipt'
                    assert [row[9] for row in saved] == list(range(1, len(ids) + 1)), 'ranks'
                    old_by_id = {row[0]: row for row in before}
                    for row in saved:
                        old = old_by_id[row[0]]
                        assert row[:9] + row[10:] == old[:9] + old[10:], 'non-sort definition changed'
                    assert answers == one.execute('SELECT * FROM custom_values').fetchall(), 'answers changed'
                    saved_token = token(ident)
                    commit_attempted = True
                    one.commit()
                    return True, (saved, saved_token), False, ''
                except (AssertionError, sqlite3.DatabaseError) as error:
                    one.rollback()
                    return False, None, not commit_attempted, str(error)

            for sorts in ((1, 2, 3), (90, 90, 90), (10, 30, 90)):
                for direction in (-1, 1):
                    reset(sorts)
                    result = move(token(), direction=direction)
                    assert result[0]
                    assert [row[0] for row in result[1][0]] == ([9002, 9001, 9003] if direction < 0 else [9001, 9003, 9002])
                    plans += 1
            for ident, direction in ((9001, -1), (9003, 1)):
                reset()
                before = state()
                result = move(token(ident), ident, direction)
                assert result == (False, None, True, 'boundary') and state() == before
                plans += 1
            for column, value in [('label', 'Synthetic external'), ('sort', 97), ('active', 0), ('required', 1), ('help_text', 'Synthetic external help'), ('created_at', '2001-01-02')]:
                reset()
                loaded = token()
                original = two.execute(f'SELECT {column} FROM custom_fields WHERE id=9003').fetchone()[0]
                two.execute(f'UPDATE custom_fields SET {column}=? WHERE id=9003', (value,))
                two.commit()
                before = state()
                result = move(loaded)
                assert result == (False, None, True, 'baseline') and state() == before
                two.execute(f'UPDATE custom_fields SET {column}=? WHERE id=9003', (original,))
                two.commit()
                plans += 1
            # A boundary attempt must not avoid the entity-baseline comparison.
            reset()
            loaded = token(9001)
            two.execute("UPDATE custom_fields SET label='Synthetic stale boundary peer' WHERE id=9003")
            two.commit()
            before = state()
            assert move(loaded, 9001, -1) == (False, None, True, 'baseline') and state() == before
            plans += 1
            # All positions, before and after the row update; also audit and missing receipt faults.
            stages = [f"{when} UPDATE OF sort ON custom_fields WHEN OLD.id={ident} BEGIN SELECT RAISE(ABORT,'synthetic'); END;"
                      for ident in (9001, 9003, 9002) for when in ('BEFORE', 'AFTER')]
            stages += [
                "BEFORE INSERT ON audit_logs WHEN NEW.action='custom_field_order' BEGIN SELECT RAISE(ABORT,'synthetic'); END;",
                "AFTER INSERT ON audit_logs WHEN NEW.action='custom_field_order' BEGIN SELECT RAISE(ABORT,'synthetic'); END;",
                "AFTER INSERT ON audit_logs WHEN NEW.action='custom_field_order' BEGIN DELETE FROM custom_fields WHERE id=CAST(NEW.entity_id AS INTEGER); END;",
            ]
            for stage in stages:
                reset()
                one.execute('CREATE TRIGGER synthetic_fault ' + stage)
                loaded = token()
                before = state()
                result = move(loaded)
                assert not result[0] and result[2] and state() == before
                one.execute('DROP TRIGGER synthetic_fault')
                assert move(loaded)[0]
                plans += 1
            reset()
            one.executescript("""
CREATE TABLE synthetic_deferred(parent_id INTEGER REFERENCES users(id) DEFERRABLE INITIALLY DEFERRED);
CREATE TRIGGER synthetic_commit AFTER INSERT ON audit_logs WHEN NEW.action='custom_field_order'
BEGIN INSERT INTO synthetic_deferred VALUES(-99999); END;
""")
            before = state()
            result = move(token())
            assert not result[0] and result[1] is None and not result[2] and state() == before
            one.execute('DROP TRIGGER synthetic_commit')
            plans += 1
            for change in ('active=0', "role='manager'"):
                reset()
                one.execute('UPDATE users SET ' + change + ' WHERE id=9001')
                one.commit()
                before = state()
                assert move(token(9001), 9001, -1) == (False, None, True, 'owner') and state() == before
                one.execute("UPDATE users SET active=1,role='owner' WHERE id=9001")
                one.commit()
                plans += 1
            reset()
            loaded = token()
            two.execute("UPDATE custom_fields SET label='Synthetic other entity changed' WHERE id=9004")
            two.commit()
            result = move(loaded)
            assert result[0] and one.execute('SELECT label FROM custom_fields WHERE id=9004').fetchone()[0] == 'Synthetic other entity changed'
            snapshot = list(result[1][0])
            two.execute("UPDATE custom_fields SET label='Synthetic later writer' WHERE id=9001")
            two.commit()
            assert result[1][0] == snapshot
            assert move(result[1][1], direction=-1) == (False, None, True, 'baseline')
            plans += 1
            assert one.execute('SELECT value FROM custom_values WHERE field_id=9001').fetchone()[0] == 'Synthetic old answer'
            one.close()
            two.close()
    print(f'PASS {len(migrations)} migrations/{plans} isolated Move extracted-SQL plans, WAL+DELETE; NOT C#/provider/WPF/CAS implementation proof')


if __name__ == '__main__':
    run()
