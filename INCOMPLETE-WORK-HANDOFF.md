# AREA11a — Unfinished work / نامکمل کام اور اگلا قدم

**Read this first. This is a work-in-progress handoff, not a release or a declaration that Portion 4 is complete.**

## Exact source to resume

- Repository: <https://github.com/Dr-Adil-Abdullah/AREA11a>
- WIP branch: [`arena/60395e95-area11a`](https://github.com/Dr-Adil-Abdullah/AREA11a/tree/arena/60395e95-area11a)
- Open pull request: <https://github.com/Dr-Adil-Abdullah/AREA11a/pull/2>
- This file on the WIP branch: <https://github.com/Dr-Adil-Abdullah/AREA11a/blob/arena/60395e95-area11a/INCOMPLETE-WORK-HANDOFF.md>

**Important:** the plain repository URL may open the default branch, not this branch. A new agent must open the branch or PR above, inspect the latest commit and working tree, and follow its own environment's branch rules. Do not merge this WIP as if it were a completed release. This handoff records status as of 24 October 2026; check actual Git history and files for newer work before acting. Older entries in README, TAKEOVER, HANDOFF and historical sections of progress documents describe their dates, not current approval or verification status.

## Owner instruction / مالک کی ہدایت

- Finish **Portion 4 first**, then start and finish **Portion 5**, then proceed to 6 and 7 sequentially. The owner has explicitly authorized this sequence; **“Next” is permission to proceed**, not a request to stop after a tiny edit and ask for approval again. Ask only questions that genuinely block safe implementation. Address the owner in Urdu and give an ordered done/remaining/current/next status. When a portion is complete, explain changes and targeted test steps.
- Portion 17 was started at the owner's request but is **paused, not abandoned**. Its WIP is preserved in this same branch. Do not mistake it for finished work or discard it. Do not prioritize it over 4–7 without a newer owner direction.
- Keep `PHYSICAL-TESTING.md` as the feedback ledger; record results against their real versions. Test results arriving later do not by themselves block continued work; urgent data, money, stock, security and backup risks do take priority.

## Ordered roadmap and status

1. Header/navigation/default Sales: code delivered. Dashboard is the fallback when Counter is not permitted; never grant permissions to force a route. Physical verification pending.
2. Product workspace: code delivered. Both the explicit Edit Product button and product-row double-click are required. Windows/WPF and physical checks pending.
3. Categories/tags: Kyanite code delivered; Core/Tests owner result below. Category put-away may leave tag-only products; owner review and physical checks remain.
4. **Central built-in and custom field policies — INCOMPLETE, highest priority.** Details and next actions below.
5. **Safe local Sale/Purchase drafts — NOT COMPLETE.** Begin immediately after finishing 4. Incomplete transactions must remain draft-only, excluded from sync, and create no sale, purchase, payment, stock movement or financial effect until a separately validated final post. Existing posting guards must not be weakened.
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

## Portion 4: exact WIP and next work

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
- On this mixed WIP, last recorded local checks: `tools/check_repo.py` **57/57**, C# grammar parsed **304 files**, touched XAML parsed as XML, and `git diff --check` clean. These are static checks, **not compilation or xUnit execution**. `tools/RUN-TESTS.cmd` expects **973 written cases**, not 973 passing cases; neither WIP Core/Tests nor Desktop/WPF was run here. No local .NET SDK was available. Run the actual Windows tests and build Desktop/WPF; report results honestly.
- Use the owner's genuine books only for genuine transactions. Fault injection, dummy sale/purchase, restore and risky financial tests must use an isolated copy. Demo test-data resets were approved, not deletion or modification of genuine books.
- Do not regress negative-stock retirement or D-93 financial/cancellation rules. `qty_base` is REAL and `ItemRow.Qty` is `double`; never cast quantities to INTEGER. A PR cancellation is not proof of customer cash repayment: never invent automatic positive payments or drawer inflows. PT02-POS-01 remains unreproduced; obtain exact evidence rather than guessing a fix.
- `RUN-TESTS.bat` and `START-AREA11.bat` are separate root launchers; launching the app must not automatically run the tests. Optional machine-specific shortcut generation remains Windows-unverified.

## First actions for the next agent

1. Confirm the right branch/PR and inspect Git status and this file, `docs/PROGRESS.md`, `docs/MASTER-CHECKLIST-ROADMAP.md`, `PHYSICAL-TESTING.md`, and the actual changed code. Preserve all WIP and any newer user edits.
2. Continue **Portion 4**, prioritizing required-photo usability and complete, transactionally safe field behavior across real forms and import paths. Add targeted regression tests and run all available checks, including Windows builds/tests when available.
3. Explain exact completion and tests in Urdu; then start **Portion 5** without another routine approval request. Keep draft/final-post financial boundaries intact.
4. Update this file and the progress/physical-test documents as the facts change; distinguish implemented, reviewed, compiled, automated-tested, physically tested and owner-accepted status.
