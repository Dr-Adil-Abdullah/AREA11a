#!/usr/bin/env python3
"""Actual StockMinus SQL on synthetic memory SQLite. NOT C#/authorization/projection/WPF execution."""
from pathlib import Path
import re
import sqlite3

ROOT = Path(__file__).resolve().parents[1]


def run() -> None:
    c = sqlite3.connect(':memory:')
    c.row_factory = sqlite3.Row
    c.execute('PRAGMA foreign_keys=ON')
    migrations = sorted((ROOT / 'dotnet/Area11.Core/Data/Migrations').glob('*.sql'))
    for migration in migrations:
        c.executescript(migration.read_text())
    source = (ROOT / 'dotnet/Area11.Core/Services/StockMinusService.cs').read_text()
    blocks = re.findall(r'@"(.*?)"', source, re.S)
    assert len(blocks) == 4
    products, movements, reviews, selected = blocks
    audit_source = (ROOT / 'dotnet/Area11.Core/Services/AuditService.cs').read_text()
    audit = next(sql for sql in re.findall(r'@"(.*?)"', audit_source, re.S)
                 if sql.strip().startswith('INSERT INTO audit_logs(at, user_id'))
    c.executescript("""
INSERT INTO users(id,name,role,active) VALUES(1,'Synthetic reviewer','owner',1);
INSERT INTO products(id,name,base_unit,active) VALUES
(1,'Synthetic minus','tablet',1),(2,'Synthetic zero cost','tablet',1),
(3,'Not negative','tablet',1),(4,'Inactive minus','tablet',0);
INSERT INTO batches(id,product_id,qty_base,cost_paisa,active,batch_no) VALUES
(1,1,-3,12345,1,'SYN-1'),(2,1,1,1000,1,'SYN-2'),(3,2,-5,0,1,'SYN-3'),(4,3,0,15000,1,'SYN-4'),(5,4,-1,90000,1,'SYN-5'),
(6,1,-100,10000,0,'SYN-6');
INSERT INTO stock_movements(id,product_id,type,qty_base,created_at,ref_code) VALUES
(1,1,'import',5,'2026-10-01 10:00:00','SYN-IN'),
(2,1,'sale',-7,'2026-10-02 10:00:00','SYN-OUT'),
(3,2,'import',-5,'2026-10-03 10:00:00','SYN-OLD'),
(4,3,'import',0,'2026-10-03 10:00:00','SYN-ZERO');
INSERT INTO audit_logs(at,user_name,action,module,entity,entity_id,details) VALUES
('2026-10-02 12:00:00','Old reviewer','stock_minus_review','stock','products','1','earlier note'),
('2026-10-03 12:00:00','New reviewer','stock_minus_review','stock','products','1','Synthetic cost 36035 note'),
('2026-10-04 12:00:00','Other','other_action','stock','products','1','not a review');
""")
    before = c.total_changes
    rows = [dict(row) for row in c.execute(products)]
    assert {row['productId'] for row in rows} == {1, 2}
    minus = next(row for row in rows if row['productId'] == 1)
    assert minus['stockQty'] == -2 and minus['valuePaisa'] == -36035
    assert next(row for row in rows if row['productId'] == 2)['valuePaisa'] == 0
    history = [dict(row) for row in c.execute(movements)]
    assert [row['id'] for row in history] == [1, 2, 3]
    notes = [dict(row) for row in c.execute(reviews)]
    assert len(notes) == 2 and notes[0]['details'] == 'Synthetic cost 36035 note'
    assert c.execute(selected, {'id': 1}).fetchone()['stockQty'] == -2
    assert c.execute(selected, {'id': 99999}).fetchone() is None
    assert c.execute(selected, {'id': '1 OR 1=1'}).fetchone() is None
    assert c.total_changes == before, 'read changed stored data'
    c.commit()
    values = dict(at='2026-11-10 12:00:00', userId=1, userName='Synthetic reviewer', action='stock_minus_review',
                  module='stock', entity='products', entityId='1', oldValue=None, newValue='-2 tablet', details='Synthetic new review')
    c.execute('BEGIN IMMEDIATE')
    c.execute(audit, values)
    c.commit()
    assert c.execute('SELECT COUNT(*) FROM stock_movements').fetchone()[0] == 4
    assert c.execute('SELECT qty_base FROM batches WHERE id=1').fetchone()[0] == -3
    assert c.execute('SELECT details FROM audit_logs ORDER BY id DESC LIMIT 1').fetchone()[0] == 'Synthetic new review'
    count = c.execute('SELECT COUNT(*) FROM audit_logs').fetchone()[0]
    c.executescript("CREATE TRIGGER reject_minus_note BEFORE INSERT ON audit_logs BEGIN SELECT RAISE(ABORT,'synthetic fault'); END;")
    c.execute('BEGIN IMMEDIATE')
    try:
        c.execute(audit, values)
    except sqlite3.IntegrityError:
        c.rollback()
    else:
        raise AssertionError('expected audit fault')
    assert c.execute('SELECT COUNT(*) FROM audit_logs').fetchone()[0] == count
    assert c.execute('PRAGMA foreign_key_check').fetchall() == []
    print(f'PASS {len(migrations)} migrations/4 unchanged actual StockMinus SQL: active negative batches, raw -36035/known0, movement order, latest review, bound ID, read-only data, append-only note and audit fault/FK')
    print('NOT RUN: C# reader/Cost/main/projection/transaction wrapper, xUnit, WPF access timer, Windows or shop data')


if __name__ == '__main__':
    run()
