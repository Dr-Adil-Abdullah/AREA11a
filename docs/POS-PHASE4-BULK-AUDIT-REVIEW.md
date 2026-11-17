# Phase4 portion14 — explicit bulk-file grant / audit metadata, v0.92.0 Bittern

**2026-11-10. D-113, bounded source checkpoint, not global rollout/Phase4/security certification.** Owner chose password option A for encrypted backups, but **backup encryption is NOT implemented in this release**. [Approved pending plan](SECURE-BACKUP-PASSWORD-PLAN.md). BackupService and existing ZIP/DB format unchanged.

## Policy and compatibility

Reuse existing `Modules.Import` / stored `import` key, relabelled **Allow Excel/CSV Bulk Export & Import** in the existing Staff & Permissions checkbox system. No duplicate key, migration, auto-grant, new dependency or lost stored permission. Owner default true; manager default now false, as are staff defaults. Existing explicit owner-assigned import grants remain explicit grants. Only Owner can administer permissions; no new manager administration. Products page access alone is no longer a bulk-file permission.

Editable product CSV/XLSX needs current Import + independent Cost/main, mixed shop workbook (including settings/balances/cost) needs current Import + BOTH financial grants/main. Missing access refuses the whole file/preview/apply, never returns zeroed/partial editable prices. An explicitly delegated cashier/staff user may use the workflows when all prerequisites hold; old rank-only workbook exclusion is replaced. Existing protected columns, settings/security exclusions, fingerprints, transaction/audit and import duplicate/opening-stock rules remain. ApplyPrices also retains Products permission.

Customer/supplier master lists now require current Import, but ordinary account amounts retain their existing meaning; this is NOT a choice of the unanswered general Supplier paid/payable/Pay/SR policy. Other pages can still show their existing supplier workflows.

Audit permission without both Cost+Profit/main returns time/person/action/module and record id, NOT entity/entity-id/old/new/details. The database's original evidence is never edited. Metadata searches match action/person, not hidden entity/old/new/details; **both page and count SQL exclude hidden fields** to prevent text-match/count oracles. Raw audit details intentionally require BOTH financial grants because arbitrary mixed old/new/free-form payloads cannot safely be divided into cost-only/profit-only. Even nonfinancial raw audit payloads follow this conservative rule. Module/action/person strings are metadata, not scrubbed arbitrary text.

## Route-to-output trace

| Door | Current gate / output |
|---|---|
| ImportService.ExportProducts / Workbook.ExportList Products | current stored active same-role Import reader; Cost/main before query; complete original monetary cells or failure |
| ImportService customer/supplier export | current Import before query; no Products-only fallback; preserved account values |
| PreviewProducts/Customers/Suppliers/Workbook | mandatory User argument; current Import and applicable cost check before parsing/DB lookups; unauthorized preview throws ValidationException, not success-empty projection. No public actorless data-preview fallback |
| ApplyProducts/Customers/Suppliers/Prices | early gate plus recheck inside each of4 original transactions before writes. Cost check on products/prices. Existing write SQL untouched |
| Workbook.ExportAll/PreviewBulk/ApplyBulk | explicit Import + Cost+Profit/main, not CanManage rank; Apply rechecks inside transaction before original whitelist/fingerprint validation/writes. Safe export errors omit raw exception details |
| Audit.Read | current Audit reader before SELECT; detail boolean from both grants/main, SQL projections blank raw hidden payload, retain known flag; page/count search predicates synchronized |
| Audit.ReadSnapshot | current Audit and detail grants on each selection; denied detail returns hidden snapshot without raw strings; full reader gets bound-id row. Old selected DTO is not a token |
| Audit.BuildCsv | current Import then fresh Audit.Read with original bounded filter; never cached page.ToCsv in VM. Both-grant readers get raw text, others metadata CSV |
| ProductHistory.BuildCsv | new dedicated Import prerequisite plus existing current Products + independent financial projections; fresh preparation after Save dialog too |
| BookCheck.BuildCsv | current Import + both financial/main + current Reports-or-Audit, then reruns existing read-only reconciliation; never cached old findings |

All **four existing user-facing CSV output doors** (list exports, Audit, Product History, BookCheck) now check dedicated Import. Audit/ProductHistory CSVs are **report snapshots, not accepted bulk-update round-trip templates**; their established redactions remain meaningful, unlike editable product/shop workbooks which are all-or-refuse. Pure actorless static templates/codecs have no shop rows; desktop template action is gated before dialog and again before write. Existing objects' public ToCsv methods are serializers, not new authenticated service doors; old raw objects cannot be recalled.

## Stale/UI boundary

Import/export and Audit views own nominal3s timers, start Loaded/stop Unloaded. Import revoke clears displayed import/bulk/price previews; cost revoke clears product/price previews, mixed financial revoke clears workbook preview. This deliberately drops stale previews (they can be reloaded from the original file); unlike purchase unsaved entry, these are read-only derived plans. Apply still rechecks in Core. Audit grant/main changes refresh detail-vs-metadata rows; module denial drops page/selection. Snapshot/CSV reread on interaction. Export CSVs reflect the current source with the same bounded filter, so new rows since screen display may change file contents; success labels no longer claim old cached counts.

Not continuous authenticated sessions or a coherent cross-call permission/settings/data snapshot. Timers can be delayed, Settings cache/lifecycle remains, background/unloaded views stop timers; refresh/current service entry checks are the boundary. Already returned data/files/clipboard/printed copies cannot be recalled. Raw DB access, other report/document routes and BookCheck's **page** policy are separate OPEN surfaces; changing its CSV gate does not certify its page or legacy reports. No all-route secrecy claim.

## Preservation and executed evidence

- **13 Import +1 Workbook +11 BookCheck verbatim SQL blocks exact** vs portion13. Audit page/count read SELECTs intentionally changed; writer AuditService exact. No migration/schema/money formula change. Backup/Purchase/Sale/Return/Expense/Stock/StockTake and writer history unchanged. **105 other baseline Core service/data hashes exact;664 baseline files retained.**
- **81 static checks PASS**,393 C# grammar files,4191 calls/8125 member references,0reportedissues. Not .NET compilation/xUnit/WPF proof.
- **12 negative source mutations rejected/restored:** manager default, current Import gate, cost gate, mixed profit gate, actorful preview, Apply transaction, audit projection, hidden search, snapshot mask, cached Audit CSV, cached Books CSV, timer unload. Source guard, NOT runtime fault injection.
- New `tools/check_audit_metadata_sqlite.py`:36 actual migrations and source-extracted page/count SELECTs in synthetic memory SQLite. Metadata/raw projection, hidden-field search+count exclusion, metadata search, order/limit/bound filter, exact read-only dump and FK PASS. Nine previous SQLite probes PASS. Does NOT run C#/Dapper/grants/preview/Apply/CSV/crypto/UI.
- **32 new facts /1641 written C# cases UNRUN**: role/module/identity/storage/current revocation matrix, actual nonzero delegated product export/import, Cost/main whole-file refusal, complete mixed-grant requirements, preview-before-decoding refusal, delegated permission administration denial; audit detail/metadata/main/grants/snapshot/search/CSV/current actor/immutability, history/books CSV and revoked workbook plan. Existing preview callers now pass explicit owner; CSV projection fixtures get explicit Import, preserving original redaction assertions.
- Guide MD/HTML, PT-34, owner decision/pending password contract, version/icon/runner, rules/checklist and standing evidence updated. No installer/live-shop fake posts. D-112 docs+same-branch commit/push required; actual remote Git result proves publication.

## Next

**Password backup setup/credential storage/streaming authenticated package/manual+automatic+safety+rollback and legacy recovery together**, per approved A. Then remaining Supplier policy, StockMinus/other outputs, authenticated sessions/record-level/fault/concurrency reconciliation, file/import/archive bounds, backup/cloud isolation and Windows/hardware/installer/guide drills. Expanded backup requirement keeps the remaining estimate conditional; do not count this release as encrypted-backup completion.

[PT-34](POS-PHASE4-PART14-URDU.md) — [Remaining plan](REMAINING-WORK-AND-TEST-INSTALLER-URDU.md).
