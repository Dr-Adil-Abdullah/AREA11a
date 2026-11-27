#!/usr/bin/env python3
"""Shift recovery lexical mutations only. Exact bytes restored; NOT C#/Windows/provider/WPF proof."""
from pathlib import Path
import check_repo as checks
ROOT = Path(__file__).resolve().parents[1] / 'dotnet'


def run():
    cases = []
    def missing(path, tokens):
        for token in tokens: cases.append((ROOT / path, token, 'REMOVED_RECOVERY_CONTRACT'))
    missing('Area11.Core/Services/SettingsService.cs', [
        'var receipt = _db.Tx(connection =>',
        '_audit.Record(connection, null, "Auth", "protection_reset", "settings", StartupService.ModeKey,',
        'old, json, "Startup mode reset to manual by Shift-at-startup recovery;',
        'return new SettingsEditForm(_db,', 'lock (_gate) _cache[StartupService.ModeKey] = json;',
        'return new ServiceResult { Ok = true, SettingsEdit = receipt };',
        'NoCommitConfirmed = !committed && outcome.NoCommitConfirmed',
        'Error = committed ? "SAVED. Manual startup', 'Startup recovery saving is unconfirmed.',
        'RequireEdit(connection, receipt, new[] { StartupService.ModeKey });',
        'receipt.GetRaw(StartupService.ModeKey) == JsonSerializer.Serialize(StartupService.Manual)'])
    missing('Area11.Core/Services/StartupService.cs', [
        'private readonly PostingAttemptGuard _recoveryAttempt = new();',
        'if (!_recoveryAttempt.TryBegin()) return ServiceResult.Fail(_recoveryHint);',
        'var machineBefore = CaptureRecoveryEntry(_machine);', 'var userBefore = CaptureRecoveryEntry(_userStore);',
        'var saved = _settings.ResetStartupForRecovery();', '_recoveryAttempt.ObserveFailure(saved);',
        'NoCommitConfirmed = saved.NoCommitConfirmed', '_recoveryAttempt.AcknowledgeCommit();',
        'receipt is null || receipt.GetRaw(ModeKey) != "\\\"manual\\\""',
        'CleanupRecoveryEntry(_machine, machineBefore, receipt, "machine-wide", warnings);',
        'CleanupRecoveryEntry(_userStore, userBefore, receipt, "this-account", warnings);',
        'var machineAbsent = VerifyRecoveryEntry(_machine);', 'var userAbsent = VerifyRecoveryEntry(_userStore);',
        'if (!machineAbsent || !userAbsent)', 'if (!_settings.MatchesStartupRecovery(receipt))',
        'if (warnings.Count == 0) _recoveryAttempt.CompleteReset();', 'else _recoveryHint = "SAVED.',
        'Ok = true, SettingsEdit = receipt, Warnings = warnings.ToArray()',
        '_recoveryAttempt.ObserveException();', '_recoveryAttempt.WasAcknowledged',
        '? "SAVED. Manual startup and its audit were stored;', 'checkedStore.Capture() : null;',
        'if (before is null)', 'checkedStore.Replace(before, AutoStartEntry.Missing);',
        'if (!checkedStore.Capture().Matches(AutoStartEntry.Missing))',
        'attempted through a legacy host and cannot be fully verified.', 'No restoration was attempted.',
        'return store is ICheckedAutoStartStore checkedStore && checkedStore.Capture().Matches(AutoStartEntry.Missing);'])
    missing('Area11.Desktop/App.xaml.cs', [
        'var recovery = _services.Startup.ResetToManual();', 'var recoveryNote = recovery.Ok',
        'string.Join(" ", recovery.Warnings)', ': recovery.Error ??', 'recoveryNote + " " +',
        'recovery.Ok && recovery.Warnings.Count == 0 ? MessageBoxImage.Information : MessageBoxImage.Warning'])
    for forbidden in ('RestoreQuietly(_machine, true, "synthetic.exe");', 'DisableQuietly(_machine);',
                      '_machine.Enable("synthetic.exe");', '_audit.RecordStandalone(null, "Auth", "protection_reset");'):
        cases.append((ROOT / 'Area11.Core/Services/StartupService.cs',
            '            var saved = _settings.ResetStartupForRecovery();',
            '            ' + forbidden + '\n            var saved = _settings.ResetStartupForRecovery();'))
    cases.append((ROOT / 'Area11.Core/Services/StartupService.cs',
        '            _recoveryAttempt.AcknowledgeCommit();\n            var receipt = saved.SettingsEdit;',
        '            var receipt = saved.SettingsEdit;\n            _recoveryAttempt.AcknowledgeCommit();'))
    original = {p: p.read_bytes() for p, _, _ in cases}
    try:
        checks.check_startup_recovery_outcomes(); assert not checks.FAILURES, checks.FAILURES
        for path, token, replacement in cases:
            text = original[path].decode(); assert token in text, (path, token)
            path.write_text(text.replace(token, replacement))
            checks.FAILURES.clear(); checks.REPORT.clear(); checks.check_startup_recovery_outcomes()
            assert checks.FAILURES, 'Survived: ' + token
            path.write_bytes(original[path]); print('REJECT', path.name, token)
    finally:
        for path, raw in original.items(): path.write_bytes(raw)
    assert all(path.read_bytes() == raw for path, raw in original.items())
    print(f'PASS {len(cases)} startup recovery lexical mutations rejected; exact bytes restored. NOT C#/Windows/provider/WPF proof.')


if __name__ == '__main__': run()
