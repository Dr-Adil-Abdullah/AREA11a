# Review carry-forward — not a completed audit

## Phase4 portion1 (v0.79.0), still IN PROGRESS

Bounded owner-write/alternate-secret/atomic-recovery/photo-filename fixes and25 UNEXECUTED regressions;1325 written total. See `POS-PHASE4-EVIDENCE.md` for26 routes,8 Settings contents,36 lessons and honest15-dimension pending list. Generic permission read defaults, factory recovery, HasOpenDrawer and compatibility policies unchanged. Filename validation is not a reparse-point/symlink guarantee. Settings identity content index0 has no shell section; next portion must resolve/retest reachability. No whole-audit/security/Windows PASS or installer.


## Current approved phase2 (v0.77.0)

- Fixed: inherited read-only rate template replaced by guarded owned editor, no blanket grid editing.
- Fixed for the new pricing policy: per-person Core limits, manual reference cap, mandatory batch-cost floor, separate line exclusion, current-account read, non-serializable consumed save confirmation.
- Fixed: active pricing policy cannot be changed by SetUiOnly or workbook settings updates; default basis is owner-only.
- Fixed: SaleService.Quote now requires POS permission as well as the posting path (earlier Quote did not check it).
- Fixed: post-rounding/points line redistribution cannot leave a batch allocation below purchase cost.
- Caveat: profit-based quotes inherently permit cost/margin inference to someone given that discount permission, even if direct cost fields are hidden. No mathematical-secrecy claim.

## Phase4 queue — investigate, do not silently make new business choices

1. `SaleService.HasOpenDrawer` explicitly returns true on a database-read exception, a legacy documented fail-open policy. It was not introduced here. Reconcile with current safety/shift expectations and explain the existing behavior to owner before changing deliberate policy. No claim this whole workflow has been audited.
2. Retired `discount.mode`, legacy role-cap Settings properties/keys and legacy blockBelowCost storage remain for compatibility; active POS no longer relies on them. Review references/export compatibility and batch any full-feature removal decisions for owner, rather than deleting business/history data.
3. Whole navigation/page coverage and the rest of the 15-point review remain phase4, not implied by source-check PASS.
4. Phase3 code landed after explicit owner **b**: original item refund first, then remaining final bill/collection cap (560 then540), not proportional550/550. Sale Extra never spreads into products; report/top/product-history item figures before Extra, category has separate unallocated negative row; overall totals final. Windows1300/PT-22 still pending. Existing no-bill PR can historically have already-paid excess before invoice is known; linking does not rewrite that cash, explicitly warns, and later invoice returns remain capped. No claim that this legacy provisional workflow can prevent every unknown-bill overpayment; full workflow audit remains phase4.
5. New pricing allowance rows remain local security policy, not part of the existing business-table cloud mirror. Full database backup includes them. Sale snapshot columns travel through existing SELECT * document sync, but mixed old/new program versions can ignore unknown columns; use matching updated versions, never promise older binaries understand Extra. Cloud restore/legacy finance compatibility belongs to phase4/actual service testing.

No Windows runtime/Desktop/physical PASS here. See PT-21 and the owner-readable phase2 test document.
