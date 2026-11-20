#!/usr/bin/env python3
"""Shared draft lexical CAS mutations only. Always restore exact bytes;not runtime proof."""
from pathlib import Path
import check_repo as checks
ROOT = Path(__file__).resolve().parents[1]


def run():
    cases = []
    def add(path, *tokens):
        for token in tokens: cases.append((ROOT / 'dotnet' / path, token))
    add('Area11.Core/Services/EditConcurrency.cs', '"draft" => "document_drafts"')
    add('Area11.Core/Services/DraftService.cs',
        'EditConcurrency.Require(_db, connection, "draft", draftId, expected);',
        'row = ReadRow(connection, draftId)!;',
        'row.EditVersion = EditConcurrency.Capture(_db, connection, "draft", draftId);',
        'row.EditVersion = EditConcurrency.Capture(_db, connection, "draft", id);',
        'return _db.Snapshot(connection =>',
        'EditVersion = EditConcurrency.Capture(_db, connection, "draft", row.Id),',
        'EditVersion = EditConcurrency.Capture(_db, connection, "draft", draftId)',
        'expected ??= stored.EditVersion;', 'authorizeBeforeMark?.Invoke(connection);',
        'var markOutcome = new TransactionOutcome();', 'return true;', '}, markOutcome);',
        'NoCommitConfirmed = markOutcome.NoCommitConfirmed', 'if (!created.NoCommitConfirmed)',
        'EditVersion = restored,', 'NoCommitConfirmed = restored is not null',
        'return changed == 1 && row is { Status: DraftStatus.Draft, PostedDocumentId: null } && row.DraftType == type',
        '? EditConcurrency.Capture(_db, connection, "draft", draftId) : null;', 'return null;')
    for page, kind in [('Pos', 'Sale'), ('Purchases', 'Purchase')]:
        add('Area11.Desktop/ViewModels/' + page + 'ViewModel.cs',
            '_services.Drafts.Post' + kind + '(_user, _openDraftId, input, _openDraftVersion)',
            '_services.Drafts.Save' + kind + 'Draft(_user, input, _openDraftId, _openDraftVersion)',
            'if (result.EditVersion is not null) _openDraftVersion = result.EditVersion;',
            '_openDraftVersion = saved.Value.EditVersion;', '_openDraftVersion = opened.EditVersion;', '_openDraftVersion = null;')
    add('Area11.Core/Services/HoldCartService.cs', 'EditVersion = EditConcurrency.Capture(_db, connection, "draft", recoveryId),')
    add('Area11.Desktop/ViewModels/PosViewModel.cs', '_openDraftVersion = cart.EditVersion;')
    add('Area11.Desktop/ViewModels/DraftsViewModel.cs', 'PostPurchase(draft) : PostSale(draft)',
        '_services.Drafts.PostSale(_user, draft.Id, expected: draft.EditVersion)',
        '_services.Drafts.PostPurchase(_user, draft.Id, expected: draft.EditVersion)',
        '_services.Drafts.Discard(_user, draft.Id, expected: draft.EditVersion)')
    add('Area11.Core/Models/MasterFoundationModels.cs', '[System.Text.Json.Serialization.JsonIgnore]')
    add('Area11.Core/Models/SaleModels.cs', '[System.Text.Json.Serialization.JsonIgnore]')
    add('Area11.Tests/DraftEditConcurrencyTests.cs', 'Assert.Equal(before, f.Snapshot());')
    originals = {p: p.read_bytes() for p, _ in cases}
    try:
        checks.check_shared_draft_edit_boundaries(); assert not checks.FAILURES, checks.FAILURES
        for path, token in cases:
            text = originals[path].decode(); assert token in text, (path, token)
            path.write_text(text.replace(token, 'REMOVED_DRAFT_EDIT_CONTRACT'))
            checks.FAILURES.clear(); checks.REPORT.clear(); checks.check_shared_draft_edit_boundaries()
            assert checks.FAILURES, 'Survived: ' + path.name + ': ' + token
            path.write_bytes(originals[path]); print('REJECT', path.name, token)
    finally:
        for path, raw in originals.items(): path.write_bytes(raw)
    assert all(path.read_bytes() == raw for path, raw in originals.items())
    print(f'PASS {len(cases)} lexical mutations rejected;exact bytes restored.NOT C#/provider/WPF proof.')


if __name__ == '__main__': run()
