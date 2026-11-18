# Phase4 portion22 — transaction-owned commands and permission reads

**v0.100.0 Sanderling · release label 2026-11-10 · SOURCE checkpoint; Windows OPEN.**
Baseline: `580bb52e247c01ae764131ae2b45623e1f228cd8` (portion21). [PT-42 isolated Windows steps](POS-PHASE4-PART22-URDU.md).

## Scope and ordinary examples

A cashier can normally see Customers. If the Owner explicitly unticks Customers, a collection transaction must not accidentally regain the cashier default because a grant SELECT failed. Conversely, if the Owner delegates Cash to that cashier, entering a write transaction must not silently lose the grant. These are connection/error distinctions, not a new permission policy.

This portion changes **Database.cs and PermissionService.cs only** among Core service/data files. It does not add session authentication, revise ordinary Owner access, rewrite financial services, change accounting or backup formats, or extend Blackbox/BookCheck work. Shared infrastructure affects callers and therefore needs the full Windows regression run, not just the new tests.

## Source finding — not an observed Windows exception

Previously Database kept one instance-wide `_transaction` and passed it unconditionally to all five Dapper wrappers, even if the supplied command connection differed from the transaction's connection. PermissionService.Can opened another connection. Inside an existing Database.Tx, that pattern could attach the first connection's transaction to the second connection's command. Ordinary Can caught any exception and returned RoleDefault; financial Can already failed closed. Thus a grant-read failure could become an ordinary default allow, while a delegated or financial grant could be denied.

Simply omitting the foreign transaction would avoid one provider error but would still read committed state on a second connection, missing the current transaction's pending grants/account changes. Simply reusing a shared transaction field without a caller boundary would risk another thread borrowing the writer's connection. The bounded fix addresses all three source concerns with a **synchronous, same-instance monitor**; it is not an async ambient transaction system.

## Implementation trace

| Boundary | Implementation | Important limit |
|---|---|---|
| Execute, Query, QuerySingle, Scalar, NextId | Shared Command helper holds `_transactionGate`; TransactionFor passes `_transaction` only if its Connection is ReferenceEquals to the supplied connection | Separately opened connection remains separate and does not see pending writes; no hidden enlistment |
| Database.Read(callback), inside Tx on the same thread | Reentrant monitor; borrow the active connection, callback materializes its result; do not dispose the borrowed connection | Callback must stay synchronous; do not return/retain/close the connection, deferred queries, tasks or readers |
| Database.Read outside Tx | Hold same-instance gate, own/Open/dispose a connection | Does not begin a SQLite snapshot transaction; multi-query outside reads can still race other Database instances/processes |
| Database.Tx | Same monitor covers opening, immediate BeginTransaction, callback, commit/rollback/finally/disposal | Nested same-thread Tx still throws. Another thread waits rather than receiving the old shared-field nested-Tx error |
| Ordinary PermissionService.Can | Read existing parameterized grant SELECT through Database.Read; successful absent row uses existing RoleDefault; present row wins | Actual read error logs and returns false; ordinary Owner automatic bypass remains; this API alone is not current-account/session authentication |
| Financial Can | Same Read unit for current stored active/same-role identity and financial grant SELECTs | Independent Profit/PurchaseCost keys; existing fail-closed handling and Owner explicit financial revoke retained |
| MapFor | Materialized assignments through Read, so pending assignments are visible on the current Tx connection | Still a UI assignment/default projection with historical error/default fallback, NOT an authorization API or coherent current-session certificate |
| Other service paths | No edits to their SQL/business branches; transactional LedgerReadAccess/SnapshotReadAccess callers now reach Can on the writer connection | Full integration regression required; outside-Tx identity/check/query/output races remain |

TransactionFor is used only under the monitor. Other threads calling these wrappers/Read/Tx on this Database instance wait until the holder completes. Open itself and direct/raw Sqlite/Dapper calls are **not** a general connection lock. Do not share a SqliteConnection across threads. The monitor is per Database, not cross-instance/process/distributed locking. A callback that starts another thread and waits for that thread to call this Database can deadlock; callbacks must not do that. Source review found no Task.Run/Wait/Result/async-lambda pattern in the current Core Services, but that is not an exhaustive lock-order/concurrency proof.

Tx still commits a returned value, including `ServiceResult.Fail`; throw to roll back after writes. Existing rollback-exception behavior is retained. External files/output/cache effects are not undone by SQLite rollback. SettingsService's independent connection reads no longer receive a foreign transaction, but its cache, reload/default behavior and pending-setting visibility were **not** redesigned. No general power-loss, retry/idempotency or duplicate-action guarantee is claimed.

## Regression source written — 27 facts, all UNRUN here

`dotnet/Area11.Tests/TransactionPermissionTests.cs` uses TestDatabase's real migrated/copied schema and no live shop data:

- Five independent foreign-connection wrapper regressions, including NextId.
- Read borrows exact connection/sees pending inserts, leaves it open; outside Read disposes; caught read failure leaves outer Tx usable.
- All wrappers on same connection still see pending state and roll back together; next Tx works; nested Tx rejection preserves the outer action.
- Two controlled other-thread regressions for Read/Tx waiting and distinct subsequent connections, with bounded waits and worker completion after releasing the transaction. These are written tests, **not measured concurrency results**.
- Committed delegated Cash and revoked Customers inside Tx; pending grant/revoke visibility; rollback does not leak a grant or retain a revoke; successful missing row still means role default.
- Ordinary grant-query faults outside/inside Tx deny instead of default allow; independent financial pending changes, current pending account deactivation, financial query failure for Owner, and ordinary Owner bypass vs explicit financial revoke.
- Pending assignment-map projection, one successful Customer collection with exact payment/balance delta, revoked collection with no payment/balance/audit changes.

Expected suite total: **1879 cases** (1852 baseline +27 facts). No .NET SDK was available here: Core/Tests compilation, xUnit execution and Desktop/WPF build are **UNRUN**. Tree-sitter grammar and structural checks cannot validate provider semantics, overload resolution, timing or deadlocks.

## Evidence and preservation

Evidence commands/results are recorded below and in the current handoff. The static checker adds a named source contract for ownership, all five wrappers, Read/Tx monitor and cleanup, both Can readers' fail-closed branches, assignment map and 27 test names. Source-negative checks intentionally weaken these contracts and verify rejection, then restore exact bytes; they are **not executable C# mutation tests**.

Preservation is checked against the baseline: all707 tracked paths retained; **114 other Core service/data files byte-identical**. PermissionService SQL literal contents and its Set/Reset/default-policy tail remain identical; Database Open/connection configuration prefix is unchanged except the new gate field. All migrations, accounting service source/formulas/SQL, Shift, Blackbox/BookCheck, audit writer, backup codec/service, UI viewmodels/shell and output renderers remain unchanged. Theme/version labels change only to identify this source build. Existing independent financial/main checks, Supplier B, bulk Owner-default rule, settlement/overpayment/Extra/below-cost rules and backup password A are not changed.

### Checks actually run in this workspace

| Evidence | Result | Does NOT establish |
|---|---|---|
| `.venv/bin/python tools/check_repo.py` | **89 static checks PASS;409 grammar files,4433 calls,8799 member references,0 structural problems** | Compiler overload/type/provider correctness or Windows UI |
| All16 `tools/check_*_sqlite.py` | PASS, existing synthetic SQL probes against the real migration/source SQL | C# Database/Can/locking execution; no new Python surrogate was used as proof of this fix |
| `.venv/bin/python tools/check_backup_package_reference.py` | Independent Python pinned vector,6 boundary round trips,24 negative cases PASS | Actual C# crypto,DPAPI,restore,new PC or security certification |
| `.venv/bin/python tools/build_rules_matrix.py --check` | Up to date | Actual application behavior |
|19 targeted source-negative mutations | All rejected by the new contract;exact original bytes restored | Executable mutation testing |
| Baseline byte/content review |707paths retained;114 other Core service/data files exact (78C# +36SQL migrations);Permission SQL/default/write tail and Database Open prefix preserved | Runtime integration on every caller |
| Guide/scripts |36lesson anchors/local links retained,MD/HTML synchronized,three build-labelled Windows scripts retain CRLF;new icon generated from BuildInfo | Real Windows rendering or shortcuts |
| `git diff --check` | PASS | Compilation or tests |
| .NET SDK/Core/Tests/Desktop/WPF | **Unavailable / UNRUN;27new facts,1879 expected cases** | No C#/Windows PASS is claimed |

Source-negative cases: remove connection ownership,gate declaration,command lock;individually bypass each of five wrappers;remove Read gate/borrowing/disposal;remove Tx gate/cleanup/nested rejection;restore ordinary/financial error-default allow;remove successful missing-row defaults;skip current assignment-map Read;remove revoked-Customers regression. These checks mutated source only and restored exact bytes. They do not run the C# regressions.

### Fifteen review dimensions (bounded infrastructure change)

| Dimension | This portion / remaining limit |
|---|---|
|1. Scope/navigation | No page/menu/BookCheck changes |
|2. Actor/session | Financial current identity retained;ordinary Owner/default behavior retained;session work OPEN |
|3. Permission grants | Same-Tx reads and error-deny vs missing-row default distinction corrected in source |
|4. Data confidentiality | Independent financial grants/main/Supplier B unchanged;no whole-app secrecy claim |
|5. Record ownership | No new generic record authorization;remaining review OPEN |
|6. Input/validation | No business input/rate/amount policy edits |
|7. Accounting | Service formulas/SQL and settlement/overpayment/Extra unchanged |
|8. Atomicity | Own-connection commands,existing commit/rollback flow retained;returned Fail behavior remains |
|9. Error/recovery | Grant read errors deny;no automatic retry/new duplicate protection |
|10. Concurrency | Same-instance synchronous gate only;two tests written,not run;cross-instance/process/deadlock audit OPEN |
|11. Settings/cache | Cached/pending settings semantics not redesigned;MapFor remains UI assignment projection |
|12. Output/stale artifacts | Renderers/print/share/UI lifecycle unchanged;cannot recall old paper/files/clipboard |
|13. Files/cloud/backup | Format/source unchanged;real integration/new-PC/DPAPI OPEN |
|14. UI/keyboard/hardware | Only release theme/icon updated;actual WPF/device tests OPEN |
|15. Delivery/evidence | Docs and same-branch publication required;actual Windows suite/installer/globalPhase4 still OPEN |

## Remaining work / next bounded review

1. Settings/native/custom/photo/secret-field boundaries, supplied/current account and real session/record authorization. Ordinary Can's Owner bypass and role defaults do not prove a signed-in session. Assignment map errors are still not an authorization result.
2. Cross-service transaction faults/rollback/retry and post-commit errors, multi-instance/process concurrency, lock ordering, busy handling, duplicates and power-loss behavior. Same-instance source serialization is only one bounded step.
3. Files/cloud and actual encrypted backup/restore integration; verified independent DB/photos paths, interrupted restore/rollback, new-PC password A and DPAPI behavior.
4. Actual Windows **1879 cases**, Desktop/WPF build, timers/keyboard/navigation, printer/scanner and genuine screenshots; fix returned failures.
5. Then the authorized single sendable double-click test installer, with startup/shortcuts/upgrade/uninstall/new-PC evidence. **No installer created or readiness claim.**

The previous rough **3–6 further integrated review/result rounds** remains conditional, not a deadline or upper bound; findings can increase it. Do not make fake live-shop posts. Separate exe folders alone are not isolation. Keep passwords, PINs, keys and shop data out of chat/source. Every portion requires documentation plus same-active-branch commit/push and actual remote verification; source checks alone do not mean published.
