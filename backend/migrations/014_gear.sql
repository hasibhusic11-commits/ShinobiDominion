-- Hero gear (ADR-044): one level per slot on each hero, 0 when not upgraded. Gear belongs to the hero, so a
-- hero spent as an evolution duplicate takes its gear with it.
ALTER TABLE sd_hero ADD COLUMN IF NOT EXISTS gear_weapon INT NOT NULL DEFAULT 0 CHECK (gear_weapon BETWEEN 0 AND 50);
ALTER TABLE sd_hero ADD COLUMN IF NOT EXISTS gear_armor INT NOT NULL DEFAULT 0 CHECK (gear_armor BETWEEN 0 AND 50);
ALTER TABLE sd_hero ADD COLUMN IF NOT EXISTS gear_charm INT NOT NULL DEFAULT 0 CHECK (gear_charm BETWEEN 0 AND 50);
