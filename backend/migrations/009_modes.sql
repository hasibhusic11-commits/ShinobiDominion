-- Game modes beyond raids and dungeons (ADR-039): the Arena, Village Siege, daily quests and the daily gift.

-- Arena points. A row appears on the first duel. Being attacked never costs points.
CREATE TABLE IF NOT EXISTS sd_arena (
    user_id    UUID PRIMARY KEY REFERENCES sd_player (user_id),
    points     INT NOT NULL DEFAULT 0 CHECK (points >= 0),
    wins       INT NOT NULL DEFAULT 0 CHECK (wins >= 0),
    losses     INT NOT NULL DEFAULT 0 CHECK (losses >= 0),
    updated_at TIMESTAMPTZ NOT NULL DEFAULT now()
);
CREATE INDEX IF NOT EXISTS sd_arena_points ON sd_arena (points DESC, updated_at);

-- Every Arena duel and Siege defense, for daily limits and audits. Battles are fought by the server.
CREATE TABLE IF NOT EXISTS sd_mode_fight (
    id           BIGSERIAL PRIMARY KEY,
    user_id      UUID NOT NULL REFERENCES sd_player (user_id),
    mode         TEXT NOT NULL CHECK (mode IN ('arena', 'siege')),
    level        INT NOT NULL DEFAULT 0,
    opponent_id  UUID REFERENCES sd_player (user_id),
    won          BOOLEAN NOT NULL,
    stars        INT NOT NULL CHECK (stars BETWEEN 0 AND 3),
    points_delta INT NOT NULL DEFAULT 0,
    gold         BIGINT NOT NULL DEFAULT 0 CHECK (gold >= 0),
    mana         BIGINT NOT NULL DEFAULT 0 CHECK (mana >= 0),
    crystals     BIGINT NOT NULL DEFAULT 0 CHECK (crystals >= 0),
    checksum     TEXT NOT NULL,
    created_at   TIMESTAMPTZ NOT NULL DEFAULT now()
);
CREATE INDEX IF NOT EXISTS sd_mode_fight_day ON sd_mode_fight (user_id, mode, created_at DESC);

-- Daily quest progress, one row per quest per UTC day.
CREATE TABLE IF NOT EXISTS sd_quest_day (
    user_id  UUID NOT NULL REFERENCES sd_player (user_id),
    day      DATE NOT NULL,
    quest_id TEXT NOT NULL,
    progress INT NOT NULL DEFAULT 0 CHECK (progress >= 0),
    claimed  BOOLEAN NOT NULL DEFAULT FALSE,
    PRIMARY KEY (user_id, day, quest_id)
);

-- The daily gift can be opened once per UTC day.
ALTER TABLE sd_player ADD COLUMN IF NOT EXISTS last_gift_day DATE;
