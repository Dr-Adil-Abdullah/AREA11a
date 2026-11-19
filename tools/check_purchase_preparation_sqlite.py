#!/usr/bin/env python3
"""Actual draft SQL on synthetic in-memory SQLite; NOT C#/authorization/WPF execution.
No data-file argument, no shop writes. Permission refusal belongs to the C# suite.
"""
from pathlib import Path
import json
import re
import sqlite3

ROOT = Path(__file__).resolve().parents[1]


def run():
    c = sqlite3.connect(':memory:')
    c.execute('PRAGMA foreign_keys=ON')
    migrations = sorted((ROOT / 'dotnet/Area11.Core/Data/Migrations').glob('*.sql'))
    for migration in migrations:
        c.executescript(migration.read_text())
    c.execute("INSERT INTO users(id,name,role,active) VALUES(1,'Synthetic owner','owner',1)")
    c.execute("INSERT INTO suppliers(id,name) VALUES(1,'Synthetic supplier')")
    source = (ROOT / 'dotnet/Area11.Core/Services/DraftService.cs').read_text()
    blocks = re.findall(r'@"((?:[^"]|"")*)"', source, re.S)

    def sql(start):
        matches = [b.replace('""', '"').strip() for b in blocks if b.strip().startswith(start)]
        assert len(matches) == 1, (start, len(matches))
        return matches[0]

    insert = sql('INSERT INTO document_drafts(')
    update = sql('UPDATE document_drafts SET payload_json')
    listed = next(b for b in blocks if 'WHERE draft_type = @type' in b)
    read = next(b for b in blocks if 'FROM document_drafts WHERE id = @id;' in b)
    premark = sql("UPDATE document_drafts\nSET payload_json")
    restore = sql("UPDATE document_drafts\nSET status = 'draft'")
    finalize = sql('UPDATE document_drafts SET posted_document_id')
    discard = sql("UPDATE document_drafts\nSET status = 'discarded'")
    payload = json.dumps({'SupplierId': 1, 'PaidPaisa': 10000, 'Lines': [
        {'ProductId': 1, 'Quantity': 3, 'CostPaisa': 12345, 'Unit': 'base'}]})
    args = dict(key='purchase-SYN1', type='purchase', payload=payload, byId=1,
                by='Synthetic owner', at='2026-11-10 12:00:00', id=1, marker=-1)
    c.execute(insert, args)
    args['key'] = 'sale-SYN2'
    args['type'] = 'sale'
    c.execute(insert, args)
    args['type'] = 'purchase'
    assert len(c.execute(listed, args).fetchall()) == 1
    assert c.execute('SELECT payload_json FROM document_drafts WHERE id=1').fetchone()[0] == payload
    assert c.execute(read, dict(id='1 OR 1=1')).fetchone() is None
    assert c.execute('SELECT COUNT(*) FROM purchases').fetchone()[0] == 0
    before = list(c.iterdump())
    c.execute(read, args).fetchone()
    c.execute(listed, args).fetchall()
    assert list(c.iterdump()) == before
    # The caller's marker lifecycle is retained; these statements alone do NOT authorize a post.
    c.execute(premark, args)
    assert len(c.execute(listed, args).fetchall()) == 1  # interrupted marker remains visible
    c.execute(restore, args)
    c.execute(update, args)
    assert c.execute('SELECT status FROM document_drafts WHERE id=1').fetchone()[0] == 'draft'
    assert c.execute('SELECT payload_json FROM document_drafts WHERE id=1').fetchone()[0] == payload
    c.execute(premark, args)
    c.execute(finalize, dict(document=42, id=1, at=args['at'], type='purchase', marker=-1))
    assert not c.execute(listed, args).fetchall()
    c.execute(restore, args)  # cannot restore an actual finalized document as an unposted draft
    assert c.execute('SELECT posted_document_id FROM document_drafts WHERE id=1').fetchone()[0] == 42
    args['id'] = 2
    c.execute(discard, dict(id=2, reason='Synthetic put-away', at=args['at']))
    assert c.execute('SELECT status FROM document_drafts WHERE id=2').fetchone()[0] == 'discarded'
    assert c.execute('SELECT COUNT(*) FROM document_drafts').fetchone()[0] == 2
    assert c.execute('SELECT COUNT(*) FROM purchases').fetchone()[0] == 0
    assert c.execute('SELECT COUNT(*) FROM payments').fetchone()[0] == 0
    assert not c.execute('PRAGMA foreign_key_check').fetchall()
    print(f'PASS {len(migrations)} migrations/8 actual draft SQL statements: bounded type/ID, read-only reads, original money payload, marker/restore/finalize/put-away retention, no purchases/payments, FK')
    print('NOT RUN: C# current reader, Cost/main refusal, transaction wrappers, duplicate/concurrency/crash guarantees, WPF timer/masking or Windows')
    c.close()


if __name__ == '__main__':
    run()
