-- Trained troops. They live in camps until a raid uses them up.
CREATE TABLE IF NOT EXISTS sd_troop (
    user_id  UUID NOT NULL REFERENCES sd_player (user_id),
    troop_id TEXT NOT NULL,
    count    INT NOT NULL CHECK (count > 0),
    PRIMARY KEY (user_id, troop_id)
);
