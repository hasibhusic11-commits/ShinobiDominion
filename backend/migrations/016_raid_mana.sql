-- Raids pay Mana as well as gold (owner, 2026-11-09: available loot is both). Each attack records the Mana it could
-- pay per star when it started, and the Mana it did pay.
ALTER TABLE sd_raid ADD COLUMN IF NOT EXISTS mana_per_star BIGINT NOT NULL DEFAULT 0 CHECK (mana_per_star >= 0);
ALTER TABLE sd_raid ADD COLUMN IF NOT EXISTS mana_earned BIGINT NOT NULL DEFAULT 0 CHECK (mana_earned >= 0);
