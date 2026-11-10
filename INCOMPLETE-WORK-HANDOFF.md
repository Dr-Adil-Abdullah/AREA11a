# AREA11a — Unfinished work / نامکمل کام اور اگلا قدم

**Read this first. This is a work-in-progress handoff, not a release or a declaration that Portion 4 is complete.**

## Exact source to resume

- Repository: <https://github.com/Dr-Adil-Abdullah/AREA11a>
- WIP branch: [`arena/60395e95-area11a`](https://github.com/Dr-Adil-Abdullah/AREA11a/tree/arena/60395e95-area11a)
- Open pull request: <https://github.com/Dr-Adil-Abdullah/AREA11a/pull/2>
- This file on the WIP branch: <https://github.com/Dr-Adil-Abdullah/AREA11a/blob/arena/60395e95-area11a/INCOMPLETE-WORK-HANDOFF.md>

**Important:** the plain repository URL may open the default branch, not this branch. A new agent must open the branch or PR above, inspect the latest commit and working tree, and follow its own environment's branch rules. Do not merge this WIP as if it were a completed release. This handoff records the latest owner feedback in this session; check actual Git history and files for newer work before acting. Older entries in README, TAKEOVER, HANDOFF and historical sections of progress documents describe their dates, not current approval or verification status.

## Login error: owner reports it is gone; further UI checks pending

The owner reported an XamlParseException after entering the password; screenshot showed line 72 position 31. `9d02546` replaced the suspicious DataGrid `RowHeight="Auto"` with `NaN` in Counter and Products. The owner subsequently said in Urdu that the login screen opened and the error is gone. Record this as **owner-reported improvement**, not proof of a full end-to-end app, full Counter/Products, thumbnail sizes, or physical acceptance. The original inner exception/stack trace was not provided. See PT-16 in `PHYSICAL-TESTING.md`. Investigate immediately if it recurs; do not ask for credentials or reset genuine data. The owner supplied a Windows Core/Tests log with v0.42.0 banner, **973 passed / 0 failed / 0 skipped in 1m14s**; it does not establish exact commit or complete WPF behavior. The prior 929/929 Kyanite result remains separate.

## Owner instruction / مالک کی ہدایت

- **Portion 4 is code-complete (v0.43.0 Umber).** Start and finish **Portion 5** next, then proceed to 6 and 7 sequentially. The owner has explicitly authorized this sequence; **“Next” is permission to proceed**, not a request to stop after a tiny edit and ask for approval again. Ask only questions that genuinely block safe implementation. Address the owner in Urdu and give an ordered done/remaining/current/next status. When a portion is complete, explain changes and targeted test steps.
- Portion 17 was started at the owner's request but is **paused, not abandoned**. Its WIP is preserved in this same branch. Do not mistake it for finished work or discard it. Do not prioritize it over 4–7 without a newer owner direction.
- Keep `PHYSICAL-TESTING.md` as the feedback ledger; record results against their real versions. Test results arriving later do not by themselves block continued work; urgent data, money, stock, security and backup risks do take priority.
- Decisions: incomplete financial records are drafts only (Q1=A); supplier directory combines one preferred supplier and all historical suppliers (Q2=C); profit is sale minus cost at sale and applicable discounts/returns, without expense allocation (Q3=A); sync is configurable manual plus automatic (Q4=C). WhatsApp PDF is prepared locally and the owner attaches/sends it; opening WhatsApp is not proof of delivery.
- Owner requested a portable handoff for another agent. This file is a curated record of scope, decisions, status and next steps, **not a verbatim export of every chat message**; do not promise that all conversation text, local databases or untracked files on other PCs were uploaded. The repository and this branch contain the committed source and documentation; shop data stays on the owner's PC unless separately and safely backed up.

## Ordered roadmap and status

1. Header/navigation/default Sales: code delivered. Dashboard is the fallback when Counter is not permitted; never grant permissions to force a route. Physical verification pending.
2. Product workspace: code delivered. Both the explicit Edit Product button and product-row double-click are required. Windows/WPF and physical checks pending.
3. Categories/tags: Kyanite code delivered; Core/Tests owner result below. Category put-away may leave tag-only products; owner review and physical checks remain.
4. **Central built-in and custom field policies — CODE COMPLETE in v0.43.0 Umber; Windows/physical verification pending.** Photo-never-required decision implemented; coverage review finished; 974 written cases, 57/57 static.
5. **Safe local Sale/Purchase drafts — NEXT, highest priority.** Begin immediately. Incomplete transactions must remain draft-only, excluded from sync, and create no sale, purchase, payment, stock movement or financial effect until a separately validated final post. Existing posting guards must not be weakened.
6. Expiry calendar/picker — pending.
7. Packaging, units and distinct variants — pending beyond existing descriptive pack foundation.
8. Generic/salt and brand management — pending.
9. Supplier management and filtering — pending.
10. Keyword-plus-unit smart search — pending beyond existing search foundation.
11. Universal and configurable bulk printing — pending.
12. Native WhatsApp and prepared PDF — pending.
13. Customer/product histories, exports and global profit toggle — pending.
14. Drawer discrepancy details, reasons and alerts — pending.
15. Offline SQLite/cloud hybrid sync and safe test workflow — pending beyond existing foundations.
16. Windows startup/close modes — planned, not implemented. Owner requested manual/normal close, auto-start/normal close, and auto-start/protected close with owner password or specifically permitted staff; clarify minimize, pre-login and recovery when relevant.
17. Comprehensive photos/avatar/icon — WIP preserved and paused. Product cover, customer/doctor/staff gallery, own avatar, thumbnail size and owner-selected runtime icon have unverified code; stock screens, privacy, backup/restore, cloud and icon behavior still need review.

## Portion 4: complete in v0.43.0 — what was done

Owner decision closed the only blocker: **a photo is never required.** Required-photo definitions are refused with guidance; photo validation never fails; product/customer/supplier creation saves parent plus answers atomically; legacy required-photo rows are never rewritten and block nothing. The Own-fields form locks the Required tick for photos and photo rows show no star. Native policies (customer/supplier phone, supplier agency, product generic/brand/rack/pack) are enforced on direct create/edit, CSV preview/apply and workbook preview/apply with legacy-blank preservation; CSV refuses required extra answers at preview and apply; workbook only updates existing rows and never writes answers (reviewed safe, no bypass). Two new photo regressions: 974 written cases, 57/57 static. No local .NET/WPF run: require Windows `ALL GREEN - 974 of 974` and Portion 4 physical checks in a test copy. **Discovered gap:** no supplier-edit path exists; supplier answers after creation use the separate editor only. Historical WIP notes below are superseded.

## Portion 4: historical WIP notes (superseded)

Re-read actual source before changing it; the following is a guide, not proof of correct behavior:

- `NativeFieldPolicyService.cs` and Settings Fields UI define owner-only preferences, confirmed/named critical overrides and audit. The five Sale/Purchase critical preferences are **metadata only**, not implemented draft behavior. Final posting checks remain authoritative. Customer/supplier phone, supplier agency, and product generic/brand/rack/pack have required-policy checks in selected direct save, CSV and workbook paths; verify complete coverage and legacy-blank behavior before claiming done.
- `CustomFieldService.cs` management is owner-only; authorized users can still answer active fields. Product creation/edit and customer creation/edit, and supplier creation have WIP transaction-scoped parent/answer saves. Customer creation is wired on Customers and Counter; supplier creation is wired on Purchases. CSV product/customer/supplier imports refuse required custom answers they cannot supply and recheck during apply. The separate answer editor for existing records remains. Review all direct/API, workbook and other creation/edit paths and permissions, stale definitions, audit rollback, and whether legacy incomplete records can be safely edited. The product and customer optional API arguments are not proof that all callers enforce required fields.
- Required photo on a newly created product/customer/supplier **cannot yet be supplied before the parent record exists**. Current WIP refuses creation and explains the limitation. Implement a safe, usable staged-photo workflow with rollback/cleanup/privacy/backup behavior or another safe solution; do not claim it works today.
- `CustomFieldsEditorViewModel.cs` includes a date calendar alongside typed input; invalid legacy text must not be silently overwritten. Finish the broad field types, form controls, policy semantics, and end-to-end integration required by the owner's checklist. Custom-value saves must not create half-written parent rows; do not let import/workbook bypass required rules.
- Owner warning and explicit confirmation are required when making critical fields optional. Sale/Purchase incomplete records cannot final-post; their safe drafts are Portion 5. Do not label metadata preferences as active posting/draft controls.
- Verify the WIP, repair compile/test/UI failures, and only then identify which requirements are genuinely complete. Do not hold ordinary development hostage to late physical feedback, but do not call untested WIP verified.

Useful entry points: `dotnet/Area11.Core/Services/{NativeFieldPolicyService,CustomFieldService,PartnerService,ProductService,ImportService,WorkbookService}.cs`; desktop `ViewModels/{NativeFieldsViewModel,CustomFieldsEditorViewModel,CustomersViewModel,PosViewModel,ProductsViewModel,PurchasesViewModel}.cs`; corresponding Views; `dotnet/Area11.Tests/{NativeFieldPolicyTests,CustomFieldTests,ImportTests,WorkbookTests}.cs`.

## Verification and operating safety

- Last owner-reported prior release: **v0.42.0 Kyanite Windows Core/Tests 929 passed, 0 failed, 0 skipped, 1m15s**. This was the previous build, not this WIP, and does **not** establish Desktop/WPF build, launch, physical or hardware verification. Earlier Siderite 922/922 and Baltic 921/921 are separate versions.
- On this mixed WIP, last recorded local checks: `tools/check_repo.py` **57/57**, C# grammar parsed **304 files**, touched XAML parsed as XML, and `git diff --check` clean. These are static checks, **not compilation or xUnit execution**. `tools/RUN-TESTS.cmd` expects **973 cases**; the owner reported 973/973 passing as described above, but the exact source commit and complete Desktop/WPF behavior remain unverified. No local .NET SDK was available. Run the actual Windows tests and build Desktop/WPF; report results honestly.
- Use the owner's genuine books only for genuine transactions. Fault injection, dummy sale/purchase, restore and risky financial tests must use an isolated copy. Demo test-data resets were approved, not deletion or modification of genuine books.
- Do not regress negative-stock retirement or D-93 financial/cancellation rules. `qty_base` is REAL and `ItemRow.Qty` is `double`; never cast quantities to INTEGER. A PR cancellation is not proof of customer cash repayment: never invent automatic positive payments or drawer inflows. PT02-POS-01 remains unreproduced; obtain exact evidence rather than guessing a fix.
- `RUN-TESTS.bat` and `START-AREA11.bat` are separate root launchers; launching the app must not automatically run the tests. Optional machine-specific shortcut generation remains Windows-unverified.

## First actions for the next agent

1. Confirm the right branch/PR and inspect Git status and this file, `docs/PROGRESS.md`, `docs/MASTER-CHECKLIST-ROADMAP.md`, `PHYSICAL-TESTING.md`, and the actual changed code. Preserve all WIP and any newer user edits.
2. Portion 4 is code-complete: run Windows build plus `ALL GREEN - 974 of 974`, then Portion 4 physical checks in a test copy. Then start **Portion 5** (safe local drafts) without another routine approval request.
3. Explain exact completion and tests in Urdu; then start **Portion 5** without another routine approval request. Keep draft/final-post financial boundaries intact.
4. Update this file and the progress/physical-test documents as the facts change; distinguish implemented, reviewed, compiled, automated-tested, physically tested and owner-accepted status.
