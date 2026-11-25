#!/usr/bin/env python3
"""Restorable lexical mutations only;not C#/WPF/authentication penetration tests."""
from pathlib import Path
import check_repo as checks

ROOT = Path(__file__).resolve().parents[1]


def run():
    cases = []
    def add(path, *tokens):
        for token in tokens: cases.append((ROOT / 'dotnet' / path, token))
    add('Area11.Core/Services/AuthenticatedSessionService.cs',
        'ConditionalWeakTable<User, Lease>', '_proofs.TryGetValue(actor, out var lease)',
        'actor.Id != lease.Id', 'actor.Kind != lease.Role', '(lease.CloseOnly && !closeOnlyAllowed)',
        'Stopwatch.GetElapsedTime(lease.Started)', 'TimeSpan.FromHours(12)', 'TimeSpan.FromMinutes(2)',
        'Interlocked.Exchange(ref lease.Revoked, 1)', 'UserRoles.Parse(row.Role) != lease.Role',
        '!string.Equals(lease.CredentialStamp, Stamp(row.PasswordHash, row.PinHash), StringComparison.Ordinal)',
        '!string.Equals(Stamp(row.PasswordHash, row.PinHash), Stamp(passwordHash, pinHash), StringComparison.Ordinal)',
        '_proofs.Add(current, lease)', 'current.Id != lease.Id')
    add('Area11.Core/Services/AuthService.cs', '_db.Sessions.Revoke(user);',
        '_db.Sessions.Issue(user, secrets?.PasswordHash, secrets?.PinHash, closeOnly: !touchLogin);',
        '_db.Sessions.Require(user);', '_db.Sessions.IssueRecoveredOwner(user);',
        '!string.Equals(stored, ReadRecoveryHash(), StringComparison.Ordinal)')
    for name in ('OwnerAccess', 'LedgerReadAccess', 'SnapshotReadAccess', 'ProductReadAccess'):
        add('Area11.Core/Services/' + name + '.cs', 'db.Sessions.Require(actor);', 'db.Sessions.Carry(actor, current);')
    for name in ('CustomFieldService', 'PhotoService'):
        add('Area11.Core/Services/' + name + '.cs', '_db.Sessions.Require(actor);', '_db.Sessions.Carry(actor, current);')
    add('Area11.Core/Services/PermissionService.cs',
        '_db.Sessions.Require(user, closeOnlyAllowed: module == Modules.CloseApp);')
    add('Area11.Core/Services/PartnerService.cs', '_db.Sessions.Require(actor);')
    add('Area11.Core/Services/PricingAllowanceService.cs', 'OwnerAccess.Require(_db, connection, actor);',
        '_db.Sessions.Require(user);')
    add('Area11.Desktop/AppServices.cs', 'Database.Sessions.Enable();',
        'CommandGuard.CaptureAuthorization = CaptureCommandAuthorization;',
        'ReferenceEquals(CurrentUser, origin) && CheckInteractiveSession()',
        '&& !CheckInteractiveSession()) e.Cancel();', 'var proof = Auth.CheckSession(user);')
    add('Area11.Desktop/ViewModels/RelayCommand.cs', 'if (_authorize?.Invoke() == false) return;')
    add('Area11.Desktop/ViewModels/ShellViewModel.cs',
        'if (_services.CheckInteractiveSession()) SessionWatch.Touch();',
        '_services.AuthenticationLost += OnAuthenticationLost;',
        '_services.AuthenticationLost -= OnAuthenticationLost;')
    add('Area11.Desktop/Views/ShellWindow.xaml.cs', '!counter.CheckCounterInputAccess()')
    add('Area11.Desktop/ViewModels/PosViewModel.cs', 'LastBill = string.Empty; _lastSaleId = 0;',
        'DocEditor.Load(0); NewCustomerEditor.Load(0);', 'HideCounterOutputs();',
        'if (!EnsureCounterAccess()) { RefreshBasisSelection(); return; }',
        '_services.Partners.GetCustomers(actor: _user)')
    add('Area11.Desktop/ViewModels/SettingsViewModel.cs', '_services.Pricing.ForUser(_user, _selectedUser)',
        '_services.Permissions.LoadEditForm(_user, _selectedUser.Id)')
    originals = {path: path.read_bytes() for path, _ in cases}
    try:
        checks.check_authenticated_session_boundaries()
        assert not checks.FAILURES, checks.FAILURES
        for path, token in cases:
            text = originals[path].decode()
            assert token in text, (path, token)
            path.write_text(text.replace(token, 'REMOVED_SESSION_CONTRACT'))
            checks.FAILURES.clear(); checks.REPORT.clear()
            checks.check_authenticated_session_boundaries()
            assert checks.FAILURES, 'Survived: ' + path.name + ': ' + token
            path.write_bytes(originals[path]); print('REJECT', path.name, token)
    finally:
        for path, raw in originals.items(): path.write_bytes(raw)
    assert all(path.read_bytes() == raw for path, raw in originals.items())
    print(f'PASS {len(cases)} lexical mutations rejected;exact bytes restored.NOT C#/WPF/security runtime proof.')


if __name__ == '__main__': run()
