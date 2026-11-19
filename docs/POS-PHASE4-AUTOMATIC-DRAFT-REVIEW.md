# ⏳ P37 — automatic shared draft handoff and no draft-to-Hold copy

2026-11-10 · v0.115.0 Cairn · stable15/16/23/24/25 · [⏳ PT-57](POS-PHASE4-PART37-URDU.md) · [⏳ stable1–36](NUMBERED-WORK-STATUS-URDU.md)

## ✅ Owner decisions recorded

- ✅ After examples of a normal Save committing without a reply, and D-001 remaining postable beside a new H-001, the owner explicitly selected automatic shared drafts for ordinary Counter/Purchase Save and refusal of Park for an open draft. Shared visibility to permitted staff was explained and accepted. No separate verification module or new permission union.
- ✅ Owner requires ✅ for100% completed work, ⏳ for ongoing/partial, ❌ for not started. New reports use this distinction. The printable57-feature checklist maps historical source-only ticks to ongoing, not current end-to-end PASS; the underlying source board is preserved. No claim that prior code was removed or every feature has never been exercised.

## ⏳ Source trace — execution evidence still required

| Status/boundary | Source change | Limit |
|---|---|---|
| ⏳ Ordinary Counter/Purchase Save | UI persists the current input through existing SaveSaleDraft/SavePurchaseDraft, links _openDraftId before callbacks, then calls PostSale/PostPurchase with that same ID. No direct Sales.Create/Purchases.Create remains in these two view-models | Existing draft payload size/current writer/name and purchase Cost/main rules now also govern automatic saving. No money creator when initial saving is unacknowledged |
| ⏳ Unknown initial draft save | Both manual and automatic helpers arm before calling Save; confirmed no-commit releases, unknown/throw blocks blind Save/Post/Park. Result/link handled before notification | Before any document creator, a potentially committed row is an open draft, not necessarily a posting marker. Operator must inspect Drafts, including after restart. The latch is local; not a durable request identity |
| ⏳ Posting/retry | Existing linked draft is reused. Post receives current input, retains retail fresh-confirm behavior, and uses P36 known-rollback/unknown-marker/finalize warning rules | Premark/Create/finalize remain separate transactions. Do not equate a marker with an existing or absent document |
| ⏳ Draft saving outcome | Save insert AND update observe TransactionOutcome. Explicit preflight/unchanged-target refusal is safe; exceptions propagate conservative NoCommitConfirmed. Collision retry now additionally requires confirmed no-commit | COMMIT attempt means unknown even if later rollback returns. Existing unique-error classification is not used as commit evidence; no provider COMMIT fault executed |
| ⏳ Draft-to-Hold | UI refuses any linked open draft. Core rejects any positive sourceDraftId inside the current-writer Tx before numbering/held insert; recovery-specific rule generalized | No atomic transfer, extra-copy conversion or new payload schema. Callers omitting their intent ID are not authenticated intent tracking |
| ⏳ Ordinary Hold | Explicit no-commit observation; acknowledged-success reset guard before callbacks, PARKED on cleanup failure, conservative unknown-outcome block | Parked history/current list requires manual verification; ordinary Hold has no global durable intent identity |
| ⏳ Purchase callbacks | Acknowledged document reset latch before ID invalidation/Lines.Clear; release only after successful page/editor refresh. Draft loading links/arms before callbacks; partial/missing-product reconstruction cannot save | Reopen page after verifying durable record. No claim to preserve later unsaved typing on navigation; actual WPF callbacks/reentry UNRUN |
| ⏳ Reporting | Tracker and new reports use three requested symbols. Checklist generator and matching checker now distinguish source board from full certification, all57 started feature rows ongoing | No source-feature deletion/reclassification; future full completion needs explicit current evidence, not a verbal tick or old Windows result |

## ⏳ C# source regressions:41 new cases /2685 total, ALL UNRUN

- ⏳16 draft-save insert/update/audit BEFORE/AFTER fault cases, both sale/purchase; exact no-mutation snapshot, explicit known-no-commit, retry one draft then one post.
- ⏳6 Hold header/item/audit BEFORE/AFTER faults plus one second-line failure: number/header/items/audit rollback then one parked cart.
- ⏳6 supplied draft intents refused:open/posted/marker/discarded/missing/purchase; no numbering/rows/audit.
- ⏳6 cash/online/card ordinary save-link-restart-read-post sequences for sale/purchase:shared metadata visibility, no money before Post, one terminal draft/document, same-ID retry refused.
- ⏳6 empty/oversize/inactive preflight cases for both types:explicit safe refusal, no rows.
- ⏳ These exercise public services in the same sequence; they do NOT execute WPF helper routing. Source/negative checks inspect that routing. Prior P36 regressions cover throwing/unknown/commit-then-throw creators and finalization warnings. No production fault hook or new migration was added.

## ✅ Local source/synthetic checks

✅ Final local checks completed:104 static checks PASS;427 grammar files;4751 calls/9734 member references resolved with0 reported problems (heuristics, NOT compilation).28 synthetic SQLite scripts PASS, including the new22-fault probe.11 sequential lexical-negative scripts rejected316 mutations (30 new), restoring exact bytes. Backup independent model:6 round trips/24 negatives PASS;rules matrix up to date;diffcheck PASS.

✅ Preservation against published P36:778 baseline paths retained,156 other Core C# files,137 existing test files and37 migrations byte-identical. Sale/Purchase/Return financial implementations,Database.Tx/TransactionOutcome unchanged. All57 feature-board rows byte-equivalent as text;only generated completion reporting changed. Launcher/report/test CRLF retained;2685 expected source cases;version/theme/icon aligned. Urdu guide MD/HTML exact rendered parity/36 lesson anchors;stable1–36 IDs retained. Generated checklist current release date2026-11-10 (not sandbox clock).

⏳ Extra shared-draft rows/transactions are the approved durability tradeoff; no new module/table/package was added. Actual disk latency/load/size/backup performance remains Windows evidence, not a claim of measured speed.

⏳ Python owns synthetic transaction/routing/authority in its probes; no C#/Dapper/Database.Tx runtime claim. New automatic-draft SQL probe applies37 actual migrations, uses unchanged extracted draft/Hold/audit SQL with22 BEFORE/AFTER faults and single retries, full snapshots and no-money assertions. New lexical mutations must all be rejected and exact original bytes restored. Existing migrations/tests/monetary implementations must remain unchanged.

## ❌ Not executed / ⏳ remaining scope

- ❌ No .NET SDK/Windows/xUnit/WPF/Inno run, real COMMIT-I/O fault/power cut/parallel execution, screenshots or hardware smoke here. No live-shop synthetic entries.
- ⏳ UI now uses drafts for ordinary posting; direct service Create callers remain outside a global idempotency scheme. Durable identity/reconciliation, authenticated session binding/CAS, other stock/expense/recoveryPR/file faults and ordinary Hold restart intent remain open.
- ⏳26–29 actual encrypted backup/restore/new-PC/cloud/files;2/4/30–35 Windows2685/build/results/fix/retest/WPF/printing/hardware/isolation.
- ❌36 authorized sendable double-click installer binary not built. ⏳ FullPhase4 OPEN.36 IDs are categories, not36Nexts;3–6 integrated rounds are conditional planning, not deadline/cap.
- ⏳ Docs and publication must accompany this portion on the same active branch, followed by independent actual remote hash verification. Earlier checkpoints are historical.
