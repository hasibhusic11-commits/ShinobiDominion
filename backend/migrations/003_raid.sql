-- Raids. Each row keeps the exact defender layout and team it started with, so later changes to
-- raid data or to the player's heroes can't alter a raid already under way.
CREATE TABLE IF NOT EXISTS sd_raid (
    attack_id      UUID PRIMARY KEY,
    user_id        UUID NOT NULL REFERENCES sd_player (user_id),
    raid_id        TEXT NOT NULL,
    raid_version   INT NOT NULL,
    gold_per_star  BIGINT NOT NULL CHECK (gold_per_star >= 0),
    snapshot       TEXT NOT NULL,
    team           TEXT NOT NULL,
    status         TEXT NOT NULL CHECK (status IN ('prepare', 'resolved', 'abandoned')),
    stars          INT CHECK (stars BETWEEN 0 AND 3),
    damage_percent INT CHECK (damage_percent BETWEEN 0 AND 100),
    gold_earned    BIGINT CHECK (gold_earned >= 0),
    checksum       TEXT,
    created_at     TIMESTAMPTZ NOT NULL DEFAULT now(),
    resolved_at    TIMESTAMPTZ
);

-- At most one raid in progress per player.
CREATE UNIQUE INDEX IF NOT EXISTS sd_raid_one_open ON sd_raid (user_id) WHERE status = 'prepare';

CREATE TABLE IF NOT EXISTS sd_raid_progress (
    user_id    UUID NOT NULL REFERENCES sd_player (user_id),
    raid_id    TEXT NOT NULL,
    best_stars INT NOT NULL CHECK (best_stars BETWEEN 0 AND 3),
    PRIMARY KEY (user_id, raid_id)
);
