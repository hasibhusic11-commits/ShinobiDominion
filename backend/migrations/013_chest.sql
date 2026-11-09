-- The free Treasure Chest (ADR-043): when it was last opened, on the server's clock. 0 means never.
ALTER TABLE sd_player ADD COLUMN IF NOT EXISTS chest_opened_ms BIGINT NOT NULL DEFAULT 0 CHECK (chest_opened_ms >= 0);
