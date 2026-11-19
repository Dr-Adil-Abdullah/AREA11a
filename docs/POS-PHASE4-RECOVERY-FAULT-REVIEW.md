# P36 — held Resume recovery and draft posting fault boundaries

2026-11-10 · v0.114.0 Lichen · stable15/23/24/25 · [PT-56](POS-PHASE4-PART36-URDU.md) · [stable1–36](NUMBERED-WORK-STATUS-URDU.md)

## Approved policy, not an inferred financial change

After the H-0001 consume-before-reconstruction example, the owner explicitly selected (1) atomic linked recovery Sale draft on Resume, not an unlinked duplicate, and (2) verification marker on unknown posting outcome, with restoration only for validation/positively confirmed no-commit. No message parsing. Pricing/actual-cost floor/Extra/ReturnB/SupplierB/Cost/Profit/main, Blackbox/BookCheck internals and permission policies are not redesigned.

## Source trace

| Boundary | Implementation | What it does NOT prove |
|---|---|---|
| Resume durable handoff | migration037 nullable FK + unique recovery link; HoldCartService.Resume resolves current reader, loads held snapshot, serializes bounded SaleInput, inserts shared recovery draft, consumes held row with status predicate/affected-row check, records draft_saved + cart_resume within one Database.Tx | Historical resumed rows remain NULL; no replay/backfill. Not provider/power-cut/WAL certification |
| Counter linkage | PosViewModel sets _openDraftId and reconstruction-failed latch before binding/cart callbacks; same latch protects ordinary draft loading and missing-product skips; existing Save/Complete routes use same ID | Later unsaved edits are not auto-saved. Full POS privacy/session/picker policy not addressed |
| Partial reconstruction | Save/Park/Complete common refusal; Clear asks before leaving linked work and explicitly says saved disk draft remains/unsaved edits lost; another loader cannot detach a linked empty cart | Actual WPF callback ordering/reentry/focus/exception handling UNRUN |
| Recovery duplicate copy | UI passes sourceDraftId to Hold; Core refuses terminal source or linked recovery draft before numbering/inserts | Not a universal idempotency key. Ordinary active non-recovery draft Hold policy remains separate; a caller intentionally omitting source ID is not authenticated intent tracking |
| Transaction observation | Optional per-call TransactionOutcome on generic Database.Tx: WorkStarted, CommitAttempted, RollbackConfirmed. Sale/Purchase Create failures explicitly propagate NoCommitConfirmed; default is unknown. Only no work or successful rollback BEFORE any COMMIT attempt is considered safe | Observer is not durable, not reusable across threads, not a commit receipt. Existing unrelated Tx callers/rollback handling are unchanged |
| Draft creator failure | Throw/unknown result leaves premark; confirmed no-commit calls typed restoration and checks one affected row plus actual restored row; restore failure/no-op reports uncertainty | Premark, document Create and finalization are still separate transactions; no atomic global exactly-once promise |
| Finalization failure | Marker/type/status predicate and row-count guard; thrown or rejected finalization after acknowledged Create returns success plus explicit SAVED/no-repost warning in both envelope and document warnings; drawer surfaces warnings | It cannot automatically reconcile a provider-ambiguous commit or tell whether a crash-created document exists |

Direct non-draft Sale/Purchase receive more honest failure classification, but this portion does NOT add durable request identity or a comprehensive UI retry lock to those routes. Do not advertise them as solved. Premark failure can itself be ambiguous: verify current draft state before retry. A marker may mean no document or an existing document. Existing explicit marker discard retains audit; manual verification remains required, not an automatic reconciliation tool.

## Regression sources — 29 new cases /2644 suite expected, ALL C# UNRUN

`DraftRecoveryFaultTests.cs`:24 InlineData cases +5 Facts. Creator known/unknown/throw/commit-then-throw/restore ABORT/restore IGNORE for both kinds; actual Sale/Purchase validation+correct retry; premark failure/no creator; actual finalize audit failure/saved warnings/retry denial; finalizer rejected result; four Resume statement/audit faults with exact snapshots and single retry; restart via new services/preserved payload/same-draft post; legacy resumed no replay; oversized payload no orphan; pre-COMMIT rollback and conservative post-attempt classification; nested Tx no work. Reflection supplies a private creator/finalizer seam in tests only; it is not a real injected provider COMMIT failure. Existing tests/assertions are retained.

## Local evidence

Final local pass: **103 static checks PASS;426 C# grammar files;4742 calls/9708 member references resolved with0 reported problems.** These are static heuristics, not a compiler. **27 synthetic SQLite scripts PASS** (new recovery probe applies37 migrations, four recovery stage rollbacks+retry, restore ABORT/IGNORE, type-bound finalization, unique/FK/legacy-NULL/no-money assertions). **10 sequential source-negative scripts:286 lexical mutations rejected, exact bytes restored**, including24 new P36 probes. Independent backup model:6 round trips/24 negatives PASS; rules matrix up to date; diffcheck PASS.

Preservation against published P35:771 baseline paths retained;150 other Core C# files,136 existing test files and36 previous migrations byte-identical. SaleService/PurchaseService monetary Save-through-end code byte-identical; Return/Extra/refund/Supplier implementations are among unchanged files. New migration037 only. CRLF on all three launcher/report/test scripts retained;2644 expected count matches registered29-new-case source;version/theme/icon match. Urdu guide MD/HTML rendered in parity with36 lesson anchors;stable1–36 IDs retained. Current generated checklist date uses declared release date2026-11-10 rather than sandbox clock. Historical checkpoint sections are not re-certified.

 Static/grammar/lexical/SQLite are SOURCE/SYNTHETIC evidence only. No .NET SDK, Windows, xUnit, WPF, Inno Setup, installer, screenshots, hardware or live-shop posting was run.

Commands: `.venv/bin/python tools/check_repo.py`; every `tools/check_*sqlite.py`; sequential `tools/check_*source_negative.py`; `tools/check_backup_package_reference.py`; `tools/build_rules_matrix.py --check`; `git diff --check`. Mutation scripts restore exact original bytes in finally blocks. SQLite probes use synthetic memory/temp databases only.

## Remaining gates and next order

1. Stable23–25: remaining stock/expense/recoveryPR/file stage faults, provider COMMIT/I-O/production journal/parallel, durable identity/reconciliation, ordinary Hold acknowledgement/UI crash and direct non-draft unknown-outcome UX.
2. Stable5/6/7/15/16/21: remaining callers/quick-add/full POS/session-bound identity/prepared output races. Current stored-account checks do not authenticate arbitrary IDs or eliminate check/use races.
3. Stable26–29: actual encrypted backup/restore/new-PC/cloud/file interruption evidence. Stable2/4/30–35: Windows2644-case compile/run/fix/retest, WPF, receipt, hardware, autostart/kiosk/install isolation.
4. Stable36: one authorized sendable double-click test installer, only after required build/tests/package/isolation/smoke gates. NOT BUILT. FullPhase4 OPEN.36 IDs are categories, not36Nexts;3–6 integrated rounds remain a conditional planning estimate, not a deadline/cap.

All old files/migrations/tests and unrelated monetary implementations must be retained; review the final diff against published P35. Publication uses the same active branch and an independent actual remote hash check. Earlier checkpoint claims remain historical.
