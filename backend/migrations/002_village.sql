-- The village moves to the server. Gold joins crystals on the player row, so one lock covers both.
ALTER TABLE sd_player ADD COLUMN IF NOT EXISTS gold BIGINT NOT NULL DEFAULT 0 CHECK (gold >= 0);
ALTER TABLE sd_player ADD COLUMN IF NOT EXISTS next_building_id INT NOT NULL DEFAULT 1 CHECK (next_building_id >= 1);
ALTER TABLE sd_player ADD COLUMN IF NOT EXISTS village_ready BOOLEAN NOT NULL DEFAULT FALSE;

ALTER TABLE sd_currency_ledger ADD COLUMN IF NOT EXISTS currency TEXT NOT NULL DEFAULT 'crystals';

CREATE TABLE IF NOT EXISTS sd_building (
    user_id           UUID NOT NULL REFERENCES sd_player (user_id),
    id                INT NOT NULL CHECK (id >= 1),
    definition_id     TEXT NOT NULL,
    x                 INT NOT NULL CHECK (x BETWEEN 0 AND 49),
    z                 INT NOT NULL CHECK (z BETWEEN 0 AND 49),
    rotation          INT NOT NULL DEFAULT 0 CHECK (rotation BETWEEN 0 AND 3),
    level             INT NOT NULL DEFAULT 1 CHECK (level >= 1),
    last_collected_ms BIGINT NOT NULL,
    PRIMARY KEY (user_id, id)
);
