-- Mana, the third currency (ADR-038). It sits on the player row with gold and crystals, so the one
-- player lock covers all three. Players from before the Mana Mill start with none.
ALTER TABLE sd_player ADD COLUMN IF NOT EXISTS mana BIGINT NOT NULL DEFAULT 0 CHECK (mana >= 0);
