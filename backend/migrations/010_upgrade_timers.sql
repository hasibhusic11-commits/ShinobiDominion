-- Upgrades take time and need a free builder (ADR-040). 0 means no upgrade is running.
ALTER TABLE sd_building ADD COLUMN IF NOT EXISTS upgrade_done_ms BIGINT NOT NULL DEFAULT 0 CHECK (upgrade_done_ms >= 0);
CREATE INDEX IF NOT EXISTS sd_building_upgrading ON sd_building (user_id) WHERE upgrade_done_ms > 0;
