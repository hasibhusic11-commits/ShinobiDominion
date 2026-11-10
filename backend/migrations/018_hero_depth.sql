-- Hero depth (ADR-053): a technique rank and one talent on each hero. Both belong to the hero, so a hero spent as
-- an evolution duplicate takes them with it, and the hero that evolves keeps its own.
ALTER TABLE sd_hero ADD COLUMN IF NOT EXISTS technique_rank INT NOT NULL DEFAULT 1 CHECK (technique_rank BETWEEN 1 AND 20);
ALTER TABLE sd_hero ADD COLUMN IF NOT EXISTS talent_id TEXT NOT NULL DEFAULT '';
ALTER TABLE sd_hero ADD COLUMN IF NOT EXISTS talent_level INT NOT NULL DEFAULT 0 CHECK (talent_level BETWEEN 0 AND 10);
