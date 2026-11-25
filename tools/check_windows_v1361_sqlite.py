#!/usr/bin/env python3
"""Two owner-failure reference reproductions on disposable SQLite WAL/DELETE.
Extracted SQL only;Python owns timestamps/fixtures,not xUnit scheduling or C# service execution.
"""
from pathlib import Path
import sqlite3
import tempfile
from check_ledger_posting_sqlite import quoted
ROOT = Path(__file__).resolve().parents[1]


def run():
    core = ROOT / 'dotnet/Area11.Core'
    update = quoted((core / 'Services/NativeFieldPolicyService.cs').read_text(), 'UPDATE form_field_definitions')
    drawer = quoted((core / 'Services/ShiftService.cs').read_text().split('private void FillDrawer(', 1)[1], 'SELECT')
    migrations = sorted((core / 'Data/Migrations').glob('*.sql'))
    cases = 0
    with tempfile.TemporaryDirectory(prefix='area11-v1361-') as folder:
        for mode in ('WAL', 'DELETE'):
            db = sqlite3.connect(str(Path(folder) / (mode + '.db')))
            db.row_factory = sqlite3.Row
            for migration in migrations: db.executescript(migration.read_text())
            assert db.execute('PRAGMA journal_mode=' + mode).fetchone()[0].upper() == mode
            db.execute('PRAGMA foreign_keys=ON')
            db.executescript("""
INSERT INTO users(id,name,role) VALUES(9001,'Synthetic owner','owner');
INSERT INTO suppliers(id,name) VALUES(9001,'Synthetic supplier');
INSERT INTO form_field_definitions(id,field_key,section_key,label,data_type,input_method,safe_required,required,critical)
VALUES(9001,'sale.synthetic','Sale','Synthetic quantity','quantity','numeric',1,1,1);
INSERT INTO purchases(id,code,supplier_id,date,total_paisa,paid_paisa,due_paisa)
VALUES(9001,'PINV-SYNTHETIC',9001,'2026-11-10 12:01:30',40000,4000,36000);
INSERT INTO payments(date,method,amount_paisa,purchase_id,supplier_id,user_id)
VALUES('2026-11-10 12:01:30','cash',4000,9001,9001,9001);
""")
            old = tuple(db.execute('SELECT * FROM form_field_definitions WHERE id=9001').fetchone())
            try:
                db.execute('UPDATE form_field_definitions SET required=0 WHERE id=9001')
            except sqlite3.IntegrityError as error:
                assert 'CHECK constraint failed' in str(error); db.rollback()
            else: raise AssertionError('Original invalid fixture must fail without acknowledgement')
            assert tuple(db.execute('SELECT * FROM form_field_definitions WHERE id=9001').fetchone()) == old
            cases += 1
            db.execute(update, dict(id=9001, required=0, by='Synthetic owner', ackAt='2026-11-10 12:00:00', at='2026-11-10 12:00:00'))
            db.commit()
            new = db.execute('SELECT * FROM form_field_definitions WHERE id=9001').fetchone()
            assert tuple(new) != old
            assert (new['required'], new['critical'], new['safe_required']) == (0, 1, 1)
            assert new['override_acknowledged_by'] == 'Synthetic owner' and new['override_acknowledged_at'] is not None
            cases += 1
            # If a parallel test resets +90s after a real posting stamp,the payment is outside @to.
            values = db.execute(drawer, {'from':'2026-11-10 12:00:00','to':'2026-11-10 12:00:01'}).fetchone()
            assert values['supplierCashOutPaisa'] == 0
            assert db.execute('SELECT SUM(amount_paisa) FROM payments').fetchone()[0] == 4000
            cases += 1
            # With a stable clock,the same unchanged drawer SQL includes the exact4000,not fake0.
            values = db.execute(drawer, {'from':'2026-11-10 12:00:00','to':'2026-11-10 12:01:31'}).fetchone()
            assert values['supplierCashOutPaisa'] == 4000
            assert 500000 - values['supplierCashOutPaisa'] == 496000
            assert not list(db.execute('PRAGMA foreign_key_check'))
            cases += 1; db.close()
    print(f'PASS {len(migrations)} migrations/{cases} WAL+DELETE reference cases:illegal fixture rejected,acknowledged update,clock-jump0 vs stable4000. NOT C#/xUnit scheduler/runtime proof.')


if __name__ == '__main__': run()
