-- Phase 2: central field governance. Append-only metadata; no custom answer or business row is rewritten.
-- A stable key survives a label rename. Legacy rows receive a deterministic key from their existing id.
ALTER TABLE custom_fields ADD COLUMN field_key TEXT;
ALTER TABLE custom_fields ADD COLUMN help_text TEXT NOT NULL DEFAULT '';

UPDATE custom_fields
SET field_key = LOWER(TRIM(entity)) || '.custom.' || id
WHERE field_key IS NULL OR TRIM(field_key) = '';

-- NULL is allowed only for a legacy/manual row until the service repairs it; normal service writes
-- always assign the key in the same transaction. Existing answers remain linked by field_id.
CREATE UNIQUE INDEX IF NOT EXISTS ux_custom_fields_field_key
  ON custom_fields(field_key);
CREATE INDEX IF NOT EXISTS idx_custom_fields_entity_key
  ON custom_fields(entity, field_key);
