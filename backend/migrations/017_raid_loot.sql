-- Raids on real players take real loot (owner, 2026-11-09, matching the genre): a share of the rival's gold and Mana,
-- scaled by the damage done. The rival is then shielded for a while, and may take revenge once per attack.
ALTER TABLE sd_player ADD COLUMN IF NOT EXISTS shield_until TIMESTAMPTZ;
ALTER TABLE sd_raid ADD COLUMN IF NOT EXISTS loot_gold BIGINT NOT NULL DEFAULT 0 CHECK (loot_gold >= 0);
ALTER TABLE sd_raid ADD COLUMN IF NOT EXISTS loot_mana BIGINT NOT NULL DEFAULT 0 CHECK (loot_mana >= 0);
ALTER TABLE sd_raid ADD COLUMN IF NOT EXISTS revenged BOOLEAN NOT NULL DEFAULT FALSE;
