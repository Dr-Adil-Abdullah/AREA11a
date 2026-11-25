#!/usr/bin/env python3
"""Three lexical regressions for owner's actual v0.134.0 CS0411;NOT compiler/runtime proof."""
from pathlib import Path
import check_repo as checks
ROOT = Path(__file__).resolve().parents[1]


def run():
    path = ROOT / 'dotnet/Area11.Core/Services/ProductService.cs'
    original = path.read_bytes()
    start = original.decode().index('    public ServiceResult Update(User actor,')
    end = original.decode().index('    /// <summary>Soft delete only', start)
    body = original.decode()[start:end]
    cases = [('_db.Tx<object?>(connection =>', '_db.Tx(connection =>'),
             ('                    return null;', '                    return;'),
             ('                return null;\n            }, outcome);', '            }, outcome);')]
    try:
        checks.check_product_update_tx_build_contract(); assert not checks.FAILURES, checks.FAILURES
        for old, new in cases:
            assert body.count(old) == 1, old
            path.write_text(original.decode()[:start] + body.replace(old, new, 1) + original.decode()[end:])
            checks.FAILURES.clear(); checks.REPORT.clear(); checks.check_product_update_tx_build_contract()
            assert checks.FAILURES, 'Survived: ' + old
            path.write_bytes(original); print('REJECT', old)
    finally: path.write_bytes(original)
    assert path.read_bytes() == original
    print('PASS 3 lexical regressions rejected;exact bytes restored.NOT C# build/provider/WPF proof.')


if __name__ == '__main__': run()
