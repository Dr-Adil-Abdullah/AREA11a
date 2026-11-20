#!/usr/bin/env python3
"""P41 lexical mutation exercise only; originals restored even after failure."""
from pathlib import Path
import check_repo as checks
ROOT = Path(__file__).resolve().parents[1]


def run():
    files = {k: ROOT / p for k, p in {
        'photo': 'dotnet/Area11.Core/Services/PhotoService.cs',
        'product': 'dotnet/Area11.Core/Services/ProductService.cs',
        'partner': 'dotnet/Area11.Core/Services/PartnerService.cs',
        'vm': 'dotnet/Area11.Desktop/ViewModels/PhotoGalleryViewModel.cs',
        'tests': 'dotnet/Area11.Tests/PhotoFileFaultTests.cs'}.items()}
    cases = [
        ('photo', 'FileMode.CreateNew', 'FileMode.Create'),
        ('photo', 'ownedPath = destination;', ''),
        ('photo', 'ownedPath = null;', ''),
        ('photo', 'read > limit - total', 'false'),
        ('photo', 'total == 0', 'false'),
        ('photo', 'output.Flush(flushToDisk: true)', 'output.Flush()'),
        ('photo', 'if (!outcome.NoCommitConfirmed)', 'if (false)'),
        ('photo', 'Guid.NewGuid().ToString("N")', '"fixed"'),
        ('photo', 'out copiedPath), outcome);', 'out copiedPath));'),
        ('photo', 'DiscardCopy(copiedPath, outcome, _log);', ''),
        ('photo', 'NoCommitConfirmed = outcome.NoCommitConfirmed', 'NoCommitConfirmed = true'),
        ('product', 'PhotoService.DiscardCopy(stagedCopy, outcome, _log);', ''),
        ('partner', 'PhotoService.DiscardCopy(stagedCopy, outcome, _log);', ''),
        ('product', '}, outcome);', '});'),
        ('partner', '}, outcome);', '});'),
        ('vm', 'if (!CanBeginAdd() || !RefreshAccess()) return;', 'if (!RefreshAccess()) return;'),
        ('vm', '_addNeedsVerification = true;', ''),
        ('vm', 'if (!added.Ok && added.NoCommitConfirmed) _addNeedsVerification = false;', '_addNeedsVerification = false;'),
        ('vm', 'acknowledged = true;', ''),
        ('vm', 'if (!CanEdit || Selected?.Item.Id != added.Value.Id || !HasPreview)', 'if (!CanEdit)'),
        ('vm', 'catch (Exception exception)', 'catch (IOException exception)'),
        ('tests', 'Assert.Equal(before, Counts(t));', 'Assert.NotNull(before);'),
        ('tests', '[InlineData(true, true, true, false)]', '')]
    originals = {p: p.read_bytes() for p in files.values()}
    try:
        checks.check_photo_file_fault_boundaries(); assert not checks.FAILURES, checks.FAILURES
        for key, old, new in cases:
            p = files[key]; text = originals[p].decode(); assert old in text, (key, old)
            p.write_text(text.replace(old, new)); checks.FAILURES.clear(); checks.REPORT.clear()
            checks.check_photo_file_fault_boundaries(); assert checks.FAILURES, 'Survived: ' + key + ':' + old
            p.write_bytes(originals[p]); print('REJECT', key, old)
    finally:
        for p, raw in originals.items(): p.write_bytes(raw)
    assert all(p.read_bytes() == raw for p, raw in originals.items())
    print(f'PASS {len(cases)} lexical mutations rejected; exact bytes restored. NOT C#/WPF/provider proof.')


if __name__ == '__main__':
    run()
