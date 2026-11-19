-- A resumed cart's recoverable work is a linked shared Sale draft, created atomically on Resume.
-- Old resumed rows are historical; do not invent a draft or claim an unrecorded sale was not posted.
ALTER TABLE held_carts ADD COLUMN recovery_draft_id INTEGER REFERENCES document_drafts(id);
CREATE UNIQUE INDEX idx_held_recovery_draft ON held_carts(recovery_draft_id) WHERE recovery_draft_id IS NOT NULL;
