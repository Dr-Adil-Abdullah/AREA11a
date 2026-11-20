# ⏳ P41 photo/file fault review — v0.119.0 Moraine, 2026-11-10

## ⏳ Bounded contract and trace

| Boundary | Source change | Proof still required |
|---|---|---|
| ⏳ Gallery Add | Per-call TransactionOutcome passed into existing Tx; failure exposes NoCommitConfirmed; unknown copy retained | Real provider COMMIT/rollback/dispose fault injection |
| ⏳ CopyOwned | GUID destination, CreateNew/FileShare.None, out path assigned immediately after exclusive ownership, 64KiB streaming/12MB maximum, empty rejection, Flush(true) | .NET I/O, disk-full/flush/ACL/partial source/hardware failures; hostile filesystem races not solved |
| ⏳ DiscardCopy | Static, null safe, no service/folder construction, delete only with positive no-commit observation; unknown and delete errors logged | Logs can themselves fall back to debug; no durable cleanup journal or automatic orphan deletion |
| ⏳ Parent creators | Product/CreateCustomer/CreateSupplier observe their existing Tx; all seven cleanup catch sites use outcome+owned path | Parent UI retry/acknowledgement/global identity not solved; preflight Fail results not uniformly annotated |
| ⏳ Gallery Add UI | Latch before service; only explicit no-commit releases failure; acknowledgement before refresh; completed normal refresh releases; unknown/SAVED cleanup failure retains | WPF execution/reentry, successful refresh not a durable dedup key; reopen/restart/another gallery resets local state |
| ⏳ Access/other workflows | Existing current actor, permissions, six-photo bound, cover/avatar policy preserved | Hide/Restore/Cover outcome guards, other callers/session/CAS/prepared outputs remain separate |

✅ Financial/permission SQL strings in the three changed Core services are retained; no migrations, history rewrite, pricing/refund/recovery-accounting changes. RecoveryService was inspected for scope only, unchanged and not certified. Unknown outcomes are not automatically orphans; do not delete by timestamp/name alone.

## ✅ Executed local evidence / ⏳ limitations

- ✅ `tools/check_photo_file_reference.py`:17 independent Python file cases,37 real migrations,3 fault rollback plans using actual gallery INSERT,1 synthetic lost-ack retained-reference case. Python owns the copying, plan, transactions and observation flags. This is NOT .NET CopyOwned/Database.Tx/permission/parent-service/provider execution.
- ✅ `tools/check_photo_file_source_negative.py`:23 lexical mutations rejected, exact original bytes restored. Token/order coverage only, not behavior/security proof.
- ⏳ `PhotoFileFaultTests.cs`:28 new xUnit source cases;2916 expected suite cases ALL UNRUN here.8 observed-state,4 byte/buffer/size,2 output creation refusal,3 partial/empty/growth,2 no-owned-path,1 cleanup I/O failure,4 actual-service audit rollback,4 actual-service success cases when run. Friend assembly permits internal tests without public API expansion. Synthetic outcome setters are not actual provider failure injection; directory-delete fault is not Windows ACL proof. .NET SDK absent.

## ⏳ Final sweep and preservation

✅ Final local sweep:108 static checks;433 C# grammar files;4803 calls/9854 member references with0 static issues;all15 source-negative scripts/428 lexical mutations rejected and bytes restored;all31 SQLite probes PASS. New file reference PASS as above;encrypted-backup independent reference6 round trips/24 negatives PASS;rules matrix regeneration and `git diff --check` PASS.

✅ Preservation against P40 `11ced41402daeb2dbbcbe524bd803084e9324ee5`:all799 prior tracked paths retained,141 old test paths/37 migrations/156 other Core C# files unchanged. SQL verbatim-string sequences identical in PhotoService10/ProductService34/PartnerService8 blocks;other financial services including RecoveryService unchanged.36 guide lesson anchors retained. Old tests were not relaxed.

⏳ Publication is a separate gate: same-active-branch commit/push and independent actual remote hash must succeed; report actual result in final response, not an inferred pending upstream state. Static grammar/member analysis is not Roslyn, Dapper, SQLite-provider or WPF execution. See [⏳ PT-61](POS-PHASE4-PART41-URDU.md) and [⏳ stable1–36](NUMBERED-WORK-STATUS-URDU.md).

## ⏳ Open work

⏳ FullPhase4, parent-form retries, other gallery writes, remaining recovery financial fault paths, provider/parallel/session/same-role-ID/CAS/durable identity, file power-loss/restore-cloud, Windows build/results/physical isolation remain OPEN. File flush does not make SQLite+filesystem a single atomic transaction; a crash can still leave an orphan/missing file and no durable intent ledger was added. ❌ Installer binary NOT BUILT. Only verified encrypted backup plus separate DB/photos/config is acceptable for physical faults; never synthetic live-shop posts.
