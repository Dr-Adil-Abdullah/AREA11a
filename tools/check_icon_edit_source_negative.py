#!/usr/bin/env python3
"""Icon lexical mutations;exact restoration.NOT runtime/codec/provider/durable-file proof."""
from pathlib import Path
import check_repo as checks
ROOT = Path(__file__).resolve().parents[1]


def run():
    cases = []
    def add(file, *tokens):
        for token in tokens: cases.append((ROOT / 'dotnet' / file, token))
    add('Area11.Core/Services/IconAssetService.cs', '_settings.CheckIconEdit(owner, expected)',
        'bytes.Length < 6 || bytes.Length > MaximumBytes', 'var content = (byte[])bytes.Clone();',
        'created = true;', 'output.Write(content, 0, content.Length);', '_files.Flush(output);', 'writerStarted = true;',
        '_settings.Set(Key, name, owner, expected)', 'if (result.NoCommitConfirmed) Discard(destination);',
        'if (created && !writerStarted && destination is not null) Discard(destination);',
        'else if (created) _log.Info("Unconfirmed icon save;retain copied file for verification:',
        'NoCommitConfirmed = !writerStarted', 'FileMode.CreateNew, FileAccess.Write, FileShare.None',
        'file.Flush(flushToDisk: true)', '_settings.Set(Key, string.Empty, owner, expected)')
    add('Area11.Core/Services/SettingsService.cs', 'RequireWriter(connection, actor, new[] { IconAssetService.Key });',
        'RequireEdit(connection, expected, new[] { IconAssetService.Key });', 'if (expected is null) throw new ValidationException("Refresh Settings before changing the icon.");')
    add('Area11.Desktop/IconAssets.cs', 'bytes = ReadBounded(source);', 'Decode(bytes);', '.Choose(owner, bytes, expected)',
        '.Reset(owner, expected)', 'if (!result.Ok) return result;', 'try { Apply(services); }',
        'Ok = true, SettingsEdit = result.SettingsEdit', 'MaximumBytes + 1L - buffer.Length',
        'if (buffer.Length > MaximumBytes)', 'icon = Decode(ReadBounded(path));')
    add('Area11.Desktop/ViewModels/SettingsViewModel.cs', 'IconAssetService.Key,', 'if (_changingIcon) return;',
        '_changingIcon = true;', 'IconAssets.Choose(_services, _user, source, form)',
        'IconAssets.Reset(_services, _user, form)', 'acknowledged = true;', 'finally { _changingIcon = false; }')
    add('Area11.Tests/IconAssetConcurrencyTests.cs', 'DeferredCommitFailureRetainsTheOwnedFileWithoutClaimingSafeRetry')
    originals = {p: p.read_bytes() for p, _ in cases}
    try:
        checks.check_icon_edit_boundaries(); assert not checks.FAILURES, checks.FAILURES
        for path, token in cases:
            text = originals[path].decode(); assert token in text, (path, token)
            path.write_text(text.replace(token, 'REMOVED_ICON_CONTRACT'))
            checks.FAILURES.clear(); checks.REPORT.clear(); checks.check_icon_edit_boundaries()
            assert checks.FAILURES, 'Survived: ' + path.name + ': ' + token
            path.write_bytes(originals[path]); print('REJECT', path.name, token)
    finally:
        for path, raw in originals.items(): path.write_bytes(raw)
    assert all(path.read_bytes() == raw for path, raw in originals.items())
    print(f'PASS {len(cases)} lexical mutations rejected;exact bytes restored.NOT C#/WPF/provider/codec proof.')


if __name__ == '__main__': run()
