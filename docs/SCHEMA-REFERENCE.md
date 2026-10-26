# SCHEMA REFERENCE — exact columns of the rebuilt database

> Generated from the reference `src/lib/schema.ts` (migrations 001–013, verbatim) plus
> Area11 migrations 014–026 (permissions, photos, own fields, barcodes, provisional money, reconciliation and catalog ordering).
> Verified by running all 26 files against SQLite in order. This is the authority for every SQL
> statement in `Area11.Core`.

**35 tables** · 26 migrations · plus the `provisional_report_lines` view

## `_migrations`  (2 columns)

| Column | Type | Not null | Default |
|---|---|---|---|
| `id` | TEXT |  |  |
| `applied_at` | TEXT | yes | `datetime('now','localtime')` |

## `alert_dismissals`  (8 columns)

| Column | Type | Not null | Default |
|---|---|---|---|
| `id` | INTEGER |  |  |
| `alert_key` | TEXT | yes |  |
| `alert_type` | TEXT | yes |  |
| `entity_id` | INTEGER |  |  |
| `reason` | TEXT | yes |  |
| `user_id` | INTEGER |  |  |
| `user_name` | TEXT |  |  |
| `created_at` | TEXT | yes | `datetime('now','localtime')` |

Indexes: `idx_alert_dismiss_type`

## `audit_logs`  (12 columns)

| Column | Type | Not null | Default |
|---|---|---|---|
| `id` | INTEGER |  |  |
| `at` | TEXT | yes | `datetime('now','localtime')` |
| `user_id` | INTEGER |  |  |
| `user_name` | TEXT |  |  |
| `action` | TEXT | yes |  |
| `entity` | TEXT |  |  |
| `entity_id` | TEXT |  |  |
| `details` | TEXT |  |  |
| `ip` | TEXT |  |  |
| `module` | TEXT |  |  |
| `old_value` | TEXT |  |  |
| `new_value` | TEXT |  |  |

Indexes: `idx_audit_at`, `idx_audit_action`, `idx_audit_entity`, `idx_audit_module`

## `batches`  (15 columns)

| Column | Type | Not null | Default |
|---|---|---|---|
| `id` | INTEGER |  |  |
| `product_id` | INTEGER | yes |  |
| `batch_no` | TEXT | yes |  |
| `expiry_date` | TEXT |  |  |
| `expiry_ym` | TEXT |  |  |
| `qty_base` | REAL | yes | `0` |
| `cost_paisa` | INTEGER | yes | `0` |
| `retail_paisa` | INTEGER | yes | `0` |
| `vip_paisa` | INTEGER | yes | `0` |
| `doctor_paisa` | INTEGER | yes | `0` |
| `is_sample` | INTEGER | yes | `0` |
| `supplier_id` | INTEGER |  |  |
| `purchase_id` | INTEGER |  |  |
| `active` | INTEGER | yes | `1` |
| `created_at` | TEXT | yes | `datetime('now','localtime')` |

FK: `supplier_id` → `suppliers.id` · `product_id` → `products.id`

Indexes: `idx_batches_product`, `idx_batches_expiry`

### D-93 quantity invariant (v0.35.9 — no schema change)

`qty_base` remains **REAL**, including fractional quantities; report projections must not cast it to
`INTEGER`. Existing databases may contain negative batch quantities from the retired U-25/U-30 rules.
Those rows are historical evidence, so v0.35.9 adds **no migration**, no silent zeroing and no
retroactive `CHECK (qty_base >= 0)` that would make an old database fail to open.

Every new ordinary decrement goes through `Area11.Core/Services/StockPolicy.cs`. In the caller's
immediate transaction it reads the active batch and product total, then executes a conditional
`UPDATE` requiring both `batches.qty_base >= requested` and the active product `SUM(qty_base) >=
requested`. The batch, product total, stock/financial/audit rows and document number therefore commit
or roll back together. Sale planning separately reserves repeated products and shared FEFO batches
before numbering so the user gets the shortage before a number is taken.

The only absolute batch assignment is the owner-only physical **stock-take**: a finite, non-negative
counted quantity is written with an audited adjustment. This is the reconciliation path for a
historical minus. `StockMinusService` and its page remain read-only reports/review trails. The legacy
`stock.allowNegative` settings key may remain in an old `settings` row, but production no longer reads
it; it is not a bypass and v0.35.9 does not delete that historical setting.

## `categories`  (6 columns)

| Column | Type | Not null | Default |
|---|---|---|---|
| `id` | INTEGER |  |  |
| `name` | TEXT | yes |  |
| `parent_id` | INTEGER |  |  |
| `sort_order` | INTEGER | yes | `0` |
| `active` | INTEGER | yes | `1` |
| `created_at` | TEXT | yes | `datetime('now','localtime')` |

## `companies`  (9 columns)

| Column | Type | Not null | Default |
|---|---|---|---|
| `id` | INTEGER |  |  |
| `name` | TEXT | yes |  |
| `phone` | TEXT |  |  |
| `notes` | TEXT |  |  |
| `active` | INTEGER | yes | `1` |
| `rep_name` | TEXT |  |  |
| `rep_phone` | TEXT |  |  |
| `visit_day` | INTEGER |  |  |
| `sort_order` | INTEGER | yes | `0` |

`visit_day` is 1 = Monday ... 7 = Sunday; NULL = no fixed day (migration 023, F-44).
`sort_order` is added by append-only migration 026; existing company rows keep their ids, values and
product links. Catalog reads order by `sort_order`, then case-insensitive name. Indexes are
`idx_categories_parent_order` and `idx_companies_catalog_order`.

## `custom_fields`  (9 columns)

| Column | Type | Not null | Default |
|---|---|---|---|
| `id` | INTEGER |  |  |
| `entity` | TEXT | yes |  |
| `label` | TEXT | yes |  |
| `type` | TEXT | yes | `'text'` |
| `options_json` | TEXT |  |  |
| `required` | INTEGER | yes | `0` |
| `active` | INTEGER | yes | `1` |
| `sort` | INTEGER | yes | `0` |
| `created_at` | TEXT | yes | `datetime('now','localtime')` |

Indexes: `idx_cf_entity`

## `custom_values`  (6 columns)

| Column | Type | Not null | Default |
|---|---|---|---|
| `id` | INTEGER |  |  |
| `field_id` | INTEGER | yes |  |
| `entity` | TEXT | yes |  |
| `entity_id` | INTEGER | yes |  |
| `value` | TEXT |  |  |
| `updated_at` | TEXT | yes | `datetime('now','localtime')` |

FK: `field_id` → `custom_fields.id`

Indexes: `idx_cv_lookup`

## `customers`  (12 columns)

| Column | Type | Not null | Default |
|---|---|---|---|
| `id` | INTEGER |  |  |
| `name` | TEXT | yes |  |
| `phone` | TEXT |  |  |
| `category` | TEXT | yes | `'normal'` |
| `credit_limit_paisa` | INTEGER | yes | `0` |
| `balance_paisa` | INTEGER | yes | `0` |
| `loyalty_points` | INTEGER | yes | `0` |
| `stars` | INTEGER | yes | `1` |
| `notes` | TEXT |  |  |
| `active` | INTEGER | yes | `1` |
| `created_at` | TEXT | yes | `datetime('now','localtime')` |
| `photo` | TEXT |  |  |

Indexes: `idx_customers_phone`, `idx_customers_name`

## `expenses`  (8 columns)

| Column | Type | Not null | Default |
|---|---|---|---|
| `id` | INTEGER |  |  |
| `date` | TEXT | yes | `datetime('now','localtime')` |
| `category` | TEXT | yes | `'other'` |
| `title` | TEXT | yes |  |
| `amount_paisa` | INTEGER | yes | `0` |
| `note` | TEXT |  |  |
| `user_id` | INTEGER |  |  |
| `created_at` | TEXT | yes | `datetime('now','localtime')` |

Indexes: `idx_expenses_date`

## `owner_drawings`  (7 columns)

| Column | Type | Not null | Default |
|---|---|---|---|
| `id` | INTEGER |  |  |
| `date` | TEXT | yes | `datetime('now','localtime')` |
| `type` | TEXT | yes | `'cash'` |
| `amount_paisa` | INTEGER | yes | `0` |
| `note` | TEXT |  |  |
| `user_id` | INTEGER |  |  |
| `created_at` | TEXT | yes | `datetime('now','localtime')` |

## `payments`  (12 columns)

| Column | Type | Not null | Default |
|---|---|---|---|
| `id` | INTEGER |  |  |
| `date` | TEXT | yes | `datetime('now','localtime')` |
| `method` | TEXT | yes | `'cash'` |
| `amount_paisa` | INTEGER | yes | `0` |
| `ref` | TEXT |  |  |
| `sale_id` | INTEGER |  |  |
| `purchase_id` | INTEGER |  |  |
| `customer_id` | INTEGER |  |  |
| `supplier_id` | INTEGER |  |  |
| `user_id` | INTEGER |  |  |
| `note` | TEXT |  |  |
| `provisional_return_id` | INTEGER |  |  |

FK: `supplier_id` → `suppliers.id` · `customer_id` → `customers.id` · `purchase_id` → `purchases.id` · `sale_id` → `sales.id` · `provisional_return_id` → `provisional_returns.id`

Indexes: `idx_payments_date`, `idx_payments_provisional`

## `photos`  (7 columns)

| Column | Type | Not null | Default |
|---|---|---|---|
| `id` | INTEGER |  |  |
| `entity` | TEXT | yes |  |
| `entity_id` | INTEGER | yes |  |
| `file_name` | TEXT | yes |  |
| `sort_order` | INTEGER | yes | `0` |
| `created_at` | TEXT | yes | `datetime('now','localtime')` |
| `active` | INTEGER | yes | `1` |

Only `file_name` lives here (rule U-19); the picture itself sits in `%ProgramData%\Area11\photos`.
`active = 0` is a **put-away** photo (D-00b) - the row and the file stay.

Indexes: `idx_photos_entity`

## `product_barcodes`  (7 columns)

| Column | Type | Not null | Default |
|---|---|---|---|
| `id` | INTEGER |  |  |
| `product_id` | INTEGER | yes |  |
| `barcode` | TEXT | yes |  |
| `pack` | TEXT | yes | `'base'` |
| `note` | TEXT |  |  |
| `active` | INTEGER | yes | `1` |
| `created_at` | TEXT | yes | `datetime('now','localtime')` |

`pack` is one of `base` / `strip` / `box` - the same three words the cart uses. Migration 022
copies the old single `products.barcode` into this table (`INSERT OR IGNORE`); that column is kept
and is still read as one more code.

Indexes: `idx_pb_code` (UNIQUE, **partial**: `WHERE active = 1`), `idx_pb_product`

## `products`  (23 columns)

| Column | Type | Not null | Default |
|---|---|---|---|
| `id` | INTEGER |  |  |
| `name` | TEXT | yes |  |
| `generic` | TEXT |  |  |
| `brand` | TEXT |  |  |
| `barcode` | TEXT |  |  |
| `company_id` | INTEGER |  |  |
| `category_id` | INTEGER |  |  |
| `rack_no` | TEXT |  |  |
| `pack_size_label` | TEXT |  |  |
| `base_unit` | TEXT | yes | `'tablet'` |
| `box_strips` | INTEGER | yes | `0` |
| `strip_tablets` | INTEGER | yes | `0` |
| `cost_paisa` | INTEGER | yes | `0` |
| `retail_paisa` | INTEGER | yes | `0` |
| `vip_paisa` | INTEGER | yes | `0` |
| `doctor_paisa` | INTEGER | yes | `0` |
| `reorder_level` | REAL | yes | `0` |
| `track_expiry` | INTEGER | yes | `1` |
| `active` | INTEGER | yes | `1` |
| `created_at` | TEXT | yes | `datetime('now','localtime')` |
| `updated_at` | TEXT | yes | `datetime('now','localtime')` |
| `photo` | TEXT |  |  |
| `room` | TEXT |  |  |

FK: `category_id` → `categories.id` · `company_id` → `companies.id`

Indexes: `idx_products_name`, `idx_products_generic`, `idx_products_barcode`

## `provisional_items`  (10 columns)

| Column | Type | Not null | Default |
|---|---|---|---|
| `id` | INTEGER |  |  |
| `provisional_id` | INTEGER | yes |  |
| `product_id` | INTEGER | yes |  |
| `qty_base` | REAL | yes | `0` |
| `created_at` | TEXT | yes | `datetime('now','localtime')` |
| `batch_id` | INTEGER |  |  |
| `qty_entered` | REAL | yes | `0` |
| `unit_entered` | TEXT |  |  |
| `refund_paisa` | INTEGER | yes | `0` |
| `cost_paisa_at_return` | INTEGER | yes | `0` |

FK: `product_id` → `products.id` · `provisional_id` → `provisional_returns.id` · `batch_id` → `batches.id`

Indexes: `idx_prov_items`, `idx_prov_items_product`

## `provisional_return_allocations`  (7 columns)

One row allocates part of the ORIGINAL PR event to one original sale line. It is not another return,
stock movement or payment. `expected_refund_paisa` is the remaining bill value of that quantity at
link time; `refund_paisa` is the actual PR money allocated to the line.

| Column | Type | Not null | Default |
|---|---|---|---|
| `id` | INTEGER |  |  |
| `provisional_item_id` | INTEGER | yes |  |
| `sale_item_id` | INTEGER | yes |  |
| `qty_base` | REAL | yes | CHECK > 0 |
| `expected_refund_paisa` | INTEGER | yes | CHECK >= 0 |
| `refund_paisa` | INTEGER | yes | CHECK >= 0 |
| `created_at` | TEXT | yes | `datetime('now','localtime')` |

FK: `provisional_item_id` → `provisional_items.id` (cascade) · `sale_item_id` → `sale_items.id`

Indexes: `idx_pr_alloc_sale_item`, `idx_pr_alloc_provisional_item`; unique `(provisional_item_id, sale_item_id)`

## `provisional_returns`  (20 columns)

| Column | Type | Not null | Default |
|---|---|---|---|
| `id` | INTEGER |  |  |
| `code` | TEXT | yes |  |
| `date` | TEXT | yes | `datetime('now','localtime')` |
| `phone` | TEXT |  |  |
| `reason` | TEXT |  |  |
| `notes` | TEXT |  |  |
| `refund_paisa` | INTEGER | yes | `0` |
| `status` | TEXT | yes | `'pending'` |
| `linked_sale_id` | INTEGER |  |  |
| `linked_at` | TEXT |  |  |
| `linked_by` | INTEGER |  |  |
| `user_id` | INTEGER |  |  |
| `created_at` | TEXT | yes | `datetime('now','localtime')` |
| `customer_name` | TEXT |  |  |
| `linked_code` | TEXT |  |  |
| `cancel_reason` | TEXT |  |  |
| `cancelled_at` | TEXT |  |  |
| `cancelled_by` | INTEGER |  |  |
| `linked_expected_paisa` | INTEGER |  | NULL = old link needs reconciliation |
| `linked_unallocated_paisa` | INTEGER |  | actual PR money above matched line capacity |

FK: `linked_sale_id` → `sales.id`

Indexes: `idx_prov_status`, `idx_prov_date`, unique `idx_prov_code`

## `purchase_items`  (12 columns)

| Column | Type | Not null | Default |
|---|---|---|---|
| `id` | INTEGER |  |  |
| `purchase_id` | INTEGER | yes |  |
| `product_id` | INTEGER | yes |  |
| `batch_id` | INTEGER |  |  |
| `qty_entered` | REAL | yes | `0` |
| `unit` | TEXT | yes | `'box'` |
| `qty_base` | REAL | yes | `0` |
| `cost_paisa` | INTEGER | yes | `0` |
| `retail_paisa` | INTEGER | yes | `0` |
| `line_total_paisa` | INTEGER | yes | `0` |
| `batch_no` | TEXT |  |  |
| `expiry_date` | TEXT |  |  |

FK: `batch_id` → `batches.id` · `product_id` → `products.id` · `purchase_id` → `purchases.id`

Indexes: `idx_pitems_purchase`

## `purchases`  (13 columns)

| Column | Type | Not null | Default |
|---|---|---|---|
| `id` | INTEGER |  |  |
| `code` | TEXT | yes |  |
| `supplier_id` | INTEGER |  |  |
| `supplier_invoice_no` | TEXT |  |  |
| `date` | TEXT | yes | `datetime('now','localtime')` |
| `subtotal_paisa` | INTEGER | yes | `0` |
| `discount_paisa` | INTEGER | yes | `0` |
| `total_paisa` | INTEGER | yes | `0` |
| `paid_paisa` | INTEGER | yes | `0` |
| `due_paisa` | INTEGER | yes | `0` |
| `notes` | TEXT |  |  |
| `user_id` | INTEGER |  |  |
| `created_at` | TEXT | yes | `datetime('now','localtime')` |

FK: `supplier_id` → `suppliers.id`

Indexes: `idx_purchases_date`

## `sale_items`  (12 columns)

| Column | Type | Not null | Default |
|---|---|---|---|
| `id` | INTEGER |  |  |
| `sale_id` | INTEGER | yes |  |
| `product_id` | INTEGER | yes |  |
| `batch_id` | INTEGER |  |  |
| `name_snapshot` | TEXT |  |  |
| `qty_base` | REAL | yes | `0` |
| `unit_sold` | TEXT | yes | `'base'` |
| `qty_entered` | REAL | yes | `0` |
| `unit_price_paisa` | INTEGER | yes | `0` |
| `discount_paisa` | INTEGER | yes | `0` |
| `line_total_paisa` | INTEGER | yes | `0` |
| `cost_paisa_at_sale` | INTEGER | yes | `0` |

FK: `batch_id` → `batches.id` · `product_id` → `products.id` · `sale_id` → `sales.id`

Indexes: `idx_sitems_sale`, `idx_sitems_product`

## `sale_returns`  (18 columns)

| Column | Type | Not null | Default |
|---|---|---|---|
| `id` | INTEGER |  |  |
| `date` | TEXT | yes | `datetime('now','localtime')` |
| `sale_id` | INTEGER | yes |  |
| `sale_item_id` | INTEGER | yes |  |
| `product_id` | INTEGER | yes |  |
| `batch_id` | INTEGER |  |  |
| `qty_base` | REAL | yes | `0` |
| `refund_paisa` | INTEGER | yes | `0` |
| `restock` | INTEGER | yes | `0` |
| `reason` | TEXT |  |  |
| `user_id` | INTEGER |  |  |
| `created_at` | TEXT | yes | `datetime('now','localtime')` |
| `code` | TEXT |  |  |
| `qcode` | TEXT |  |  |
| `disposition` | TEXT | yes | `'quarantine'` |
| `disposed_at` | TEXT |  |  |
| `disposed_by` | INTEGER |  |  |
| `disposed_note` | TEXT |  |  |

FK: `batch_id` → `batches.id` · `product_id` → `products.id` · `sale_item_id` → `sale_items.id` · `sale_id` → `sales.id`

Indexes: `idx_returns_sale`, `idx_returns_date`, `idx_returns_code`, `idx_returns_qcode`, `idx_returns_disposition`

## `sales`  (18 columns)

| Column | Type | Not null | Default |
|---|---|---|---|
| `id` | INTEGER |  |  |
| `code` | TEXT | yes |  |
| `date` | TEXT | yes | `datetime('now','localtime')` |
| `customer_id` | INTEGER |  |  |
| `user_id` | INTEGER |  |  |
| `subtotal_paisa` | INTEGER | yes | `0` |
| `discount_paisa` | INTEGER | yes | `0` |
| `tax_paisa` | INTEGER | yes | `0` |
| `round_off_paisa` | INTEGER | yes | `0` |
| `total_paisa` | INTEGER | yes | `0` |
| `paid_paisa` | INTEGER | yes | `0` |
| `due_paisa` | INTEGER | yes | `0` |
| `status` | TEXT | yes | `'paid'` |
| `payment_method` | TEXT | yes | `'cash'` |
| `notes` | TEXT |  |  |
| `created_at` | TEXT | yes | `datetime('now','localtime')` |
| `change_paisa` | INTEGER | yes | `0` |
| `doctor_id` | INTEGER |  |  |

FK: `customer_id` → `customers.id` · `doctor_id` → `customers.id`

Indexes: `idx_sales_date`, `idx_sales_customer`, `idx_sales_status`, `idx_sales_doctor`

`doctor_id` is the customer (category `doctor`) whose prescription the bill was sold on
(migration 023, F-44). NULL on most bills.

## `settings`  (3 columns)

| Column | Type | Not null | Default |
|---|---|---|---|
| `key` | TEXT |  |  |
| `value` | TEXT | yes |  |
| `updated_at` | TEXT | yes | `datetime('now','localtime')` |

v0.35.13 retains **121 known keys**. Thirty-two are `labels.*` JSON strings added in v0.35.11 for
E-12/S-05 application section names. `SettingsService.EnsureDefaults()` inserts only missing rows
during startup, so an old
database upgrades without a migration or an overwrite. Label saves use the normal validated,
permission-checked, atomic old/new audit path; the unaudited UI-preference writer refuses them.

## `shifts`  (13 columns)

| Column | Type | Not null | Default |
|---|---|---|---|
| `id` | INTEGER |  |  |
| `user_id` | INTEGER |  |  |
| `opened_at` | TEXT | yes | `datetime('now','localtime')` |
| `closed_at` | TEXT |  |  |
| `opening_float_paisa` | INTEGER | yes | `0` |
| `expected_paisa` | INTEGER |  |  |
| `actual_paisa` | INTEGER |  |  |
| `difference_paisa` | INTEGER |  |  |
| `status` | TEXT | yes | `'open'` |
| `note` | TEXT |  |  |
| `handed_to_user_id` | INTEGER |  |  |
| `handed_to_name` | TEXT |  |  |
| `opened_from_shift_id` | INTEGER |  |  |

## `stock_adjustments`  (14 columns)

| Column | Type | Not null | Default |
|---|---|---|---|
| `id` | INTEGER |  |  |
| `date` | TEXT | yes | `datetime('now','localtime')` |
| `product_id` | INTEGER | yes |  |
| `batch_id` | INTEGER |  |  |
| `direction` | TEXT | yes |  |
| `qty_base` | REAL | yes | `0` |
| `unit_entered` | TEXT |  |  |
| `qty_entered` | REAL |  |  |
| `value_paisa` | INTEGER | yes | `0` |
| `reason` | TEXT | yes |  |
| `note` | TEXT |  |  |
| `user_id` | INTEGER |  |  |
| `movement_id` | INTEGER |  |  |
| `created_at` | TEXT | yes | `datetime('now','localtime')` |

FK: `movement_id` → `stock_movements.id` · `batch_id` → `batches.id` · `product_id` → `products.id`

Indexes: `idx_adjust_date`, `idx_adjust_product`

## `stock_movements`  (11 columns)

| Column | Type | Not null | Default |
|---|---|---|---|
| `id` | INTEGER |  |  |
| `product_id` | INTEGER | yes |  |
| `batch_id` | INTEGER |  |  |
| `type` | TEXT | yes |  |
| `qty_base` | REAL | yes | `0` |
| `ref_type` | TEXT |  |  |
| `ref_id` | INTEGER |  |  |
| `ref_code` | TEXT |  |  |
| `note` | TEXT |  |  |
| `user_id` | INTEGER |  |  |
| `created_at` | TEXT | yes | `datetime('now','localtime')` |

FK: `batch_id` → `batches.id` · `product_id` → `products.id`

Indexes: `idx_moves_product`, `idx_moves_date`

## `stock_take_items`  (11 columns)

| Column | Type | Not null | Default |
|---|---|---|---|
| `id` | INTEGER |  |  |
| `stock_take_id` | INTEGER | yes |  |
| `product_id` | INTEGER | yes |  |
| `batch_id` | INTEGER |  |  |
| `system_qty_base` | REAL | yes | `0` |
| `counted_qty_base` | REAL |  |  |
| `unit_entered` | TEXT |  |  |
| `qty_entered` | REAL |  |  |
| `diff_qty_base` | REAL |  |  |
| `value_paisa` | INTEGER |  |  |
| `created_at` | TEXT | yes | `datetime('now','localtime')` |

FK: `batch_id` → `batches.id` · `product_id` → `products.id` · `stock_take_id` → `stock_takes.id`

Indexes: `idx_stake_items`, `idx_stake_items_p`

## `stock_takes`  (15 columns)

| Column | Type | Not null | Default |
|---|---|---|---|
| `id` | INTEGER |  |  |
| `code` | TEXT | yes |  |
| `date` | TEXT | yes | `datetime('now','localtime')` |
| `status` | TEXT | yes | `'open'` |
| `room` | TEXT |  |  |
| `category_id` | INTEGER |  |  |
| `note` | TEXT |  |  |
| `items_count` | INTEGER | yes | `0` |
| `diff_count` | INTEGER | yes | `0` |
| `short_paisa` | INTEGER | yes | `0` |
| `extra_paisa` | INTEGER | yes | `0` |
| `user_id` | INTEGER |  |  |
| `applied_at` | TEXT |  |  |
| `applied_by` | INTEGER |  |  |
| `created_at` | TEXT | yes | `datetime('now','localtime')` |

Indexes: `idx_stake_date`

## `supplier_return_items`  (7 columns)

| Column | Type | Not null | Default |
|---|---|---|---|
| `id` | INTEGER |  |  |
| `return_id` | INTEGER | yes |  |
| `product_id` | INTEGER | yes |  |
| `batch_id` | INTEGER |  |  |
| `qty_base` | REAL | yes | `0` |
| `cost_paisa` | INTEGER | yes | `0` |
| `line_total_paisa` | INTEGER | yes | `0` |

FK: `batch_id` → `batches.id` · `product_id` → `products.id` · `return_id` → `supplier_returns.id`

Indexes: `idx_sret_items`

## `supplier_returns`  (11 columns)

| Column | Type | Not null | Default |
|---|---|---|---|
| `id` | INTEGER |  |  |
| `code` | TEXT | yes |  |
| `date` | TEXT | yes | `datetime('now','localtime')` |
| `supplier_id` | INTEGER | yes |  |
| `purchase_id` | INTEGER |  |  |
| `reason` | TEXT |  |  |
| `note` | TEXT |  |  |
| `total_paisa` | INTEGER | yes | `0` |
| `settled` | INTEGER | yes | `0` |
| `user_id` | INTEGER |  |  |
| `created_at` | TEXT | yes | `datetime('now','localtime')` |

FK: `purchase_id` → `purchases.id` · `supplier_id` → `suppliers.id`

Indexes: `idx_sret_supplier`

## `suppliers`  (10 columns)

| Column | Type | Not null | Default |
|---|---|---|---|
| `id` | INTEGER |  |  |
| `name` | TEXT | yes |  |
| `agency` | TEXT |  |  |
| `phone` | TEXT |  |  |
| `address` | TEXT |  |  |
| `notes` | TEXT |  |  |
| `active` | INTEGER | yes | `1` |
| `opening_balance_paisa` | INTEGER | yes | `0` |
| `balance_paisa` | INTEGER | yes | `0` |
| `created_at` | TEXT | yes | `datetime('now','localtime')` |

Indexes: `idx_suppliers_name`

## `user_permissions`  (5 columns)

| Column | Type | Not null | Default |
|---|---|---|---|
| `user_id` | INTEGER | yes |  |
| `module_key` | TEXT | yes |  |
| `allowed` | INTEGER | yes | `1` |
| `updated_at` | TEXT | yes | `datetime('now','localtime')` |
| `updated_by` | TEXT |  |  |

FK: `user_id` → `users.id`

Indexes: `idx_user_permissions_user`

## `users`  (9 columns)

| Column | Type | Not null | Default |
|---|---|---|---|
| `id` | INTEGER |  |  |
| `name` | TEXT | yes |  |
| `role` | TEXT | yes | `'cashier'` |
| `pin_hash` | TEXT |  |  |
| `password_hash` | TEXT |  |  |
| `phone` | TEXT |  |  |
| `active` | INTEGER | yes | `1` |
| `last_login_at` | TEXT |  |  |
| `created_at` | TEXT | yes | `datetime('now','localtime')` |

Indexes: `idx_users_role`
