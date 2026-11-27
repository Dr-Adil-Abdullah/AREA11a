#!/usr/bin/env python3
"""Telemetry lexical tripwire mutations; NOT C#/provider/Windows/WPF proof."""
from pathlib import Path
import check_repo as checks
ROOT = Path(__file__).resolve().parents[1]


def run():
    # Mutate each production token required by this gate, without weakening old tests/gates.
    source = (ROOT / 'tools/check_repo.py').read_text().split('def check_session_telemetry_outcomes():', 1)[1].split('def main(', 1)[0]
    import ast
    tree = ast.parse('def gate():' + source)
    paths = [ROOT / p for p in (
        'dotnet/Area11.Core/Services/FocusGuardService.cs',
        'dotnet/Area11.Core/Services/SettingsService.cs', 'dotnet/Area11.Desktop/App.xaml.cs',
        'dotnet/Area11.Desktop/ViewModels/ShellViewModel.cs',
        'dotnet/Area11.Desktop/Views/LoginWindow.xaml.cs', 'dotnet/Area11.Desktop/Views/LoginWindow.xaml',
        'dotnet/Area11.Desktop/Views/ShellWindow.xaml.cs', 'dotnet/Area11.Desktop/Views/ShellWindow.xaml')]
    originals = {p: p.read_bytes() for p in paths}
    # Use literal tokens inside need() lists; composed window tokens get explicit cases below.
    cases = []
    targets = {'telemetry': [paths[0]], 'settings': [paths[1]], 'app': [paths[2]],
               'tick': [paths[3]], 'view': [paths[4], paths[6]]}
    for node in ast.walk(tree):
        if isinstance(node, ast.Call) and isinstance(node.func, ast.Name) and node.func.id == 'need':
            selected = targets.get(getattr(node.args[0], 'id', ''), [paths[5], paths[7]])
            for element in node.args[1].elts:
                if isinstance(element, ast.Constant) and isinstance(element.value, str):
                    token = element.value
                    cases += [(p, token) for p in selected if token in originals[p].decode()]
    cases += [(paths[4], 'TelemetryNotice.Text = _viewModel.Services.Focus.TelemetryNotice;'),
              (paths[6], 'TelemetryNotice.Text = shell.Services.Focus.TelemetryNotice;'),
              (paths[3], 'if (!SessionWatch.Running || !ReferenceEquals(_services.CurrentUser, _user)) return;')]
    cases = list(dict.fromkeys(cases))
    try:
        checks.check_session_telemetry_outcomes(); assert not checks.FAILURES, checks.FAILURES
        for p, token in cases:
            p.write_text(originals[p].decode().replace(token, 'REMOVED_TELEMETRY_CONTRACT'))
            checks.FAILURES.clear(); checks.REPORT.clear()
            try: checks.check_session_telemetry_outcomes()
            except (IndexError, ValueError): pass  # A removed structural boundary must also fail closed.
            else: assert checks.FAILURES, 'Survived: ' + str(p) + ' ' + token
            p.write_bytes(originals[p]); print('REJECT', p.name, repr(token))
    finally:
        for p, raw in originals.items(): p.write_bytes(raw)
    assert all(p.read_bytes() == raw for p, raw in originals.items())
    print(f'PASS {len(cases)} telemetry lexical mutations rejected; exact bytes restored. NOT runtime proof.')


if __name__ == '__main__': run()
