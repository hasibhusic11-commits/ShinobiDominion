-- The owner's building list (ADR-046) changed footprints and removed the Crystal Mine. Each village is fitted to it
-- once, the next time the player is loaded; layout_version records that it has been.
ALTER TABLE sd_player ADD COLUMN IF NOT EXISTS layout_version INT NOT NULL DEFAULT 1;
