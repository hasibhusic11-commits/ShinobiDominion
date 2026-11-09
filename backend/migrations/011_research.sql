-- Troop research at the Research Hut (ADR-041). done_ms is when a running study finishes; 0 when none runs.
CREATE TABLE IF NOT EXISTS sd_research (
    user_id  UUID NOT NULL REFERENCES sd_player (user_id),
    troop_id TEXT NOT NULL,
    level    INT NOT NULL DEFAULT 0 CHECK (level >= 0),
    done_ms  BIGINT NOT NULL DEFAULT 0 CHECK (done_ms >= 0),
    PRIMARY KEY (user_id, troop_id)
);
