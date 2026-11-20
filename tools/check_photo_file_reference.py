#!/usr/bin/env python3
"""P41 independent Python file/SQLite reference model. Does NOT execute C# helpers,
Database.Tx, permissions, parent creators, .NET flush/ACL, COMMIT fault or WPF.
Uses actual gallery INSERT SQL on all real migrations, with Python-owned plans/outcomes.
"""
from io import BytesIO
from pathlib import Path
from tempfile import TemporaryDirectory
import itertools
import os
import sqlite3
from check_counter_current_sqlite import quoted
ROOT = Path(__file__).resolve().parents[1]
LIMIT = 12 * 1024 * 1024


def copy_reference(source, path, owned, limit=LIMIT):
    # owned is assigned only after exclusive creation; models ownership, not C# out semantics.
    with open(path, 'xb') as output:
        owned.append(path)
        total = 0
        while chunk := source.read(65536):
            if len(chunk) > limit - total:
                raise ValueError('growth')
            output.write(chunk)
            total += len(chunk)
        if not total:
            raise ValueError('empty')
        output.flush()
        os.fsync(output.fileno())


def cleanup_reference(owned, started, attempted, rolled_back):
    no_commit = not started or (not attempted and rolled_back)
    if owned and no_commit:
        owned[0].unlink(missing_ok=True)
    return no_commit


class PartialFault(BytesIO):
    def read(self, count=-1):
        if self.tell():
            raise OSError('synthetic partial source read')
        return super().read(count)


def run():
    files = 0
    with TemporaryDirectory(prefix='area11-p41-reference-') as tmp:
        root = Path(tmp); path = root / 'copy.jpg'; kept = root / 'kept.jpg'; kept.write_bytes(b'keep')
        for length in (1, 65536, 65537, LIMIT):
            data = b'x' * length; owned = []
            copy_reference(BytesIO(data), path, owned)
            assert path.read_bytes() == data and owned == [path]
            path.unlink(); files += 1
        for stream, error in ((BytesIO(), ValueError), (BytesIO(b'x' * 65537), ValueError), (PartialFault(b'abc'), OSError)):
            owned = []
            try:
                copy_reference(stream, path, owned, limit=65536)
                raise AssertionError('fault not raised')
            except error:
                pass
            assert owned == [path] and path.stat().st_size <= 65536
            assert cleanup_reference(owned, True, False, True)
            assert not path.exists(); files += 1
        path.write_bytes(b'not ours'); owned = []
        try:
            copy_reference(BytesIO(b'new'), path, owned)
            raise AssertionError('collision not refused')
        except FileExistsError:
            pass
        assert owned == [] and path.read_bytes() == b'not ours'; path.unlink(); files += 1
        owned = []
        try:
            copy_reference(BytesIO(b'new'), root / 'missing' / 'copy.jpg', owned)
            raise AssertionError('missing directory not refused')
        except FileNotFoundError:
            pass
        assert not owned and not (root / 'missing').exists(); files += 1
        for started, attempted, rolled_back in itertools.product((False, True), repeat=3):
            path.write_bytes(b'owned')
            deleted = cleanup_reference([path], started, attempted, rolled_back)
            assert path.exists() != deleted
            if path.exists(): path.unlink()
            assert kept.read_bytes() == b'keep'; files += 1

        db = sqlite3.connect(':memory:'); db.execute('PRAGMA foreign_keys=ON')
        migrations = sorted((ROOT / 'dotnet/Area11.Core/Data/Migrations').glob('*.sql'))
        for migration in migrations: db.executescript(migration.read_text())
        db.executescript("INSERT INTO products(id,name,cost_paisa,retail_paisa) VALUES(1,'Synthetic photo product',1000,1500);")
        photo = (ROOT / 'dotnet/Area11.Core/Services/PhotoService.cs').read_text()
        insert = quoted(photo.split('internal ServiceResult<PhotoItem> AddInTransaction(', 1)[1], 'INSERT INTO photos')
        # Python stand-in audit event, NOT the C# audit service or parent creator execution.
        stages = 0
        for stage in ('after-copy', 'after-photo-row', 'after-audit-row'):
            before = tuple(db.iterdump()); owned = []
            db.execute('BEGIN')
            try:
                copy_reference(BytesIO(b'synthetic'), path, owned)
                if stage == 'after-copy': raise OSError('synthetic file/plan fault')
                db.execute(insert, dict(entity='product', entityId=1, fileName=path.name, sortOrder=1, at='2026-11-10'))
                if stage == 'after-photo-row': raise OSError('synthetic post-insert fault')
                db.execute("INSERT INTO audit_logs(module,action,entity,entity_id,at,user_name) VALUES('Products','photo_add','photos','1','2026-11-10','Synthetic')")
                raise OSError('synthetic post-audit fault')
            except OSError:
                db.rollback()
                assert cleanup_reference(owned, True, False, True)
            assert tuple(db.iterdump()) == before and not path.exists() and kept.read_bytes() == b'keep'
            stages += 1
        # Commit succeeds but acknowledgement is notionally lost: retain referenced file.
        owned = []; db.execute('BEGIN'); copy_reference(BytesIO(b'committed'), path, owned)
        db.execute(insert, dict(entity='product', entityId=1, fileName=path.name, sortOrder=1, at='2026-11-10')); db.commit()
        assert not cleanup_reference(owned, True, True, True)
        assert path.read_bytes() == b'committed' and db.execute('SELECT file_name FROM photos').fetchone()[0] == path.name
        # This is a synthetic acknowledgement model, not an actual COMMIT failure.
        db.close()
    print(f'PASS {files} Python file reference cases;{len(migrations)} migrations;{stages} actual photo INSERT plan fault rollbacks;1 synthetic lost-ack retention. NOT C#/.NET/provider/WPF proof.')


if __name__ == '__main__':
    run()
