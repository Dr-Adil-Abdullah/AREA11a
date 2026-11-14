# Review carry-forward — not a completed audit

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
4. Phase3 Extra Discount must be a SALE-level separate rupee value, never rewrite product rates/item discounts. Partial-return settlement may need an explained owner decision; do not invent the policy or ship over-refunds.

No Windows runtime/Desktop/physical PASS here. See PT-21 and the owner-readable phase2 test document.
