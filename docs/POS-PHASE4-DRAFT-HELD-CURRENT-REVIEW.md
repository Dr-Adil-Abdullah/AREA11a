# Phase4 portion35 — current draft and held-cart access

**v0.113.0 Dune · 2026-11-10 · stable5/6/7/15/16/23/25. Source checkpoint, not Windows certification.**

[PT-55 Urdu](POS-PHASE4-PART35-URDU.md) · [stable1–36](NUMBERED-WORK-STATUS-URDU.md) · [guide](USER-GUIDE-URDU.md)

## Authority contract

| Route | Current boundary |
|---|---|
| Sale draft Save/Update/List/Open/Discard | Active stored account / same parsed role / POS; writes resolve actor inside Tx |
| Purchase draft List/Discard | Current Purchases module only; preserve established metadata policy |
| Purchase draft Save/Update/Open | Current Purchases + existing preparation Cost/main gate |
| Draft discard | Tx classifies only draft_type, authorizes correct current module, then reads full row; no outside pre-read of payload |
| Held Hold/Resume/Discard | Current POS writer inside Tx before number/target/mutation; stored audit actor |
| Held List/Count | Required current POS actor; Count explicit ServiceResult<int>, no successful-zero fallback on read error |

Shared normalized draft reader rejects unknown type and cleans type before comparing purchase payload purpose. The purchase financial gate is delegated to unchanged PurchaseService.RequireEntryReader. Existing drafts remain shared across allowed people, not creator-owned. Creation records stored current name/ID; updates retain historical creator but use current audit actor. Purchase finalize audit now resolves current preparation actor; its exception/terminal behavior remains unchanged.

## UI boundaries and honest exclusions

- Draft list failure clears selection/rows and does not show certified-empty text. Access-only nominal3second Loaded/Closed timer preserves allowed selections; denial clears, regrant requires Refresh.
- Open/Post/Discard check a current loaded row; Post/Discard recheck after modal Yes. Purchase payload gates are checked independently of metadata access. Local busy/finally and acknowledged-success row consumption/refresh preserve SAVED on later draft post/discard refresh faults.
- POS parked list now has an explicit loaded verdict; current action checks and nominal3second Loaded/Unloaded timer clear denied parked rows, not the authored unsaved cart. Resume/Discard callbacks require membership in the successful current list. SaveSaleDraft/OpenSaleDraft/Hold use current POS preflight as well as Core guards.
- **Not full POS cache erasure/masking**: authored cart, pickers and already-prepared outputs are not globally revoked. Real WPF/modal/selection/focus/reentry is UNRUN.
- **HeldResume still consumes DB status before UI reconstruction**. Reconstruction failure/crash/restart recovery and generic Hold postcommit UI faults remain OPEN. Holding an already-open draft also retains the pre-existing duplicate-intent semantics; no silent destructive draft change introduced.
- **Draft premark/Create/finalize remain separate Tx**. Failed-finalize marker policy, restore best effort and already-acknowledged Create result behavior retained; no whole-draft atomicity, durable identity or global exactly-once claim. Permission change after document commit may prevent finalization audit and leave marker; user must verify the document.

## Preservation

No migrations, payload formats, terminal/shared policies or financial calculation changes. Existing Sale/Purchase/Return/Supplier services, pricing/Extra/ReturnB/SupplierB/main/Cost/Profit, Database/Permission/SnapshotReadAccess/Audit, backups/Blackbox/BookCheck unchanged. Hold Quote policy and all old draft/held SQL literals retained; only type-only discard classification SQL added. Existing preparation source guard follows the shared reader rather than obsolete inline spelling and explicitly verifies Cost/main delegation. Existing DraftTests creator expectation updated from caller-authored "Owner" to actual seeded stored admin name; assertion retained. Old held-count tests receive required actor/result.Value; new tests explicitly assert verdicts.

## C# regression inventory — all compilation/execution UNRUN

`DraftHeldCurrentAccessTests.cs`: **136 new cases =129InlineData+7facts**; suite **2615 expected**.

-15routes×4storedfaults/regrant=60:Sale/Purchase save/update/list/open/discard,Hold/list/count/resume/discard. Denial snapshots include drafts/held rows/items/numbers/audit and financial tables; allowed non-posting routes preserve money/stock.
-15routes×4invalidactors=60:null/zero/inactivecaller/forgedOwner.
-9stored-writer cases:Sale/Purchase create/update/discard andHold/resume/discard;creator history retained.
-7facts:Purchase metadata-vs-cost payload including normalized type;independent POS/Purchases;failed count;wrong/unknown kind;storedOwner current check;terminal reuse/audit behavior;shared drafts across people.

No new C# statement-stage fault/whole-draft/reconstruction tests are claimed in this bounded current-access portion. Prior fault suites remain; actual execution and broader integrated faults are still required.

## Executed local evidence

- `tools/check_repo.py`: **102 checks PASS;424 C# grammar files;4728 static calls /9647 member references;zero issues**. Not compilation.
- All **26** `tools/check_*_sqlite.py` scripts PASS. New draft/held probe: **36 migrations;120 Python actor/module denials with unchanged snapshots/regrant;7 actual draft/held read SQLs +7 write SQL probes;actual audit INSERT/trigger rollback;metadata-vs-cost-payload and count-read error**. Python owns policy/routing/transactions; no C# Quote/pricing/terminal-post execution.
- All **9** lexical source-negative scripts PASS with exact-byte restoration: **27new draft-held**,49catalog,24current-counter,29counter-posting,23ledger-posting,29quarantine-current,29receipt-current,30record-write,22stock-posting. Total262 lexical probes;not executed C# mutations.
- Independent backup Python reference:pinned vector,**6 chunk-boundary round trips /24negative cases PASS**. Rules matrix `--check` PASS;no C# codec/DPAPI/actual restore certification.
- Preservation against P34 `f230ad4abacfdb774ebbe25241b2d1a8ea6b131f`:**all766 baseline paths retained;166other Core service/data/model files byte-identical;all old Draft/Hold SQL retained;generic draft Post+Restore bodies and POS CompleteSaleOnce exact;2old edited test assertion counts retained** (one creator-name expectation deliberately updated). Guide MD→HTML exact,36lesson anchors,stable1–36tracker,Windows scripts CRLF and `git diff --check` PASS.
- **C# build/all2615tests,Dapper/provider/Database.Tx,actual WPF timer/bindings/modal/reentry/SAVED and PT-55 Windows steps UNRUN.** No .NET SDK/Windows/Inno here;no installer built.

Lexical/SQL/Python checks are not C#/Dapper/provider/Database.Tx/pricing/session/WPF/COMMIT proof.

## Next / installer gates

Remaining quick-add/fullPOS/session/prepared outputs and authenticated identity/same-role-ID/CAS;draft premark/Create/finalize/restore and held reconstruction faults;other stock/expense/recoveryPR/files/actualCOMMIT-I/O/provider/busy/productionjournal/parallel/crossprocess/durableidentity;26–29backup/restore/newPC/cloud;2/4/30–35Windows2615+build/logs/fixes/hardware/startup/isolation. **FullPhase4 OPEN; authorized single-doubleclick test installer NOT BUILT.** Verified encrypted backup and truly separate DB/photos/config before tests;no synthetic live shop posts. Each portion docs+same-active-branch commit/push+separate actual-remote verification;36categories≠36Nexts,conditional3–6round estimate not deadline/upperbound.
