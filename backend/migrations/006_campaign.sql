-- What each raid paid besides gold, so daily limits can be counted and any payout audited.
ALTER TABLE sd_raid ADD COLUMN IF NOT EXISTS xp_earned BIGINT NOT NULL DEFAULT 0 CHECK (xp_earned >= 0);
ALTER TABLE sd_raid ADD COLUMN IF NOT EXISTS crystals_earned BIGINT NOT NULL DEFAULT 0 CHECK (crystals_earned >= 0);
ALTER TABLE sd_raid ADD COLUMN IF NOT EXISTS item_earned TEXT;

CREATE INDEX IF NOT EXISTS sd_raid_xp_wins ON sd_raid (user_id, resolved_at DESC) WHERE xp_earned > 0;
