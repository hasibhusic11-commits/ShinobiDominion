-- Raids on real players' villages. The snapshot and guards are copied when the raid starts,
-- so nothing the rival does later can change it, and a raid never changes the rival's data.
ALTER TABLE sd_raid ADD COLUMN IF NOT EXISTS opponent_id UUID REFERENCES sd_player (user_id);
ALTER TABLE sd_raid ADD COLUMN IF NOT EXISTS defenders TEXT NOT NULL DEFAULT '[]';
ALTER TABLE sd_raid ADD COLUMN IF NOT EXISTS defenders_alive INT CHECK (defenders_alive >= 0);

CREATE INDEX IF NOT EXISTS sd_raid_defense_log ON sd_raid (opponent_id, resolved_at DESC) WHERE opponent_id IS NOT NULL;
CREATE INDEX IF NOT EXISTS sd_raid_rival_pay ON sd_raid (user_id, resolved_at DESC) WHERE opponent_id IS NOT NULL;
