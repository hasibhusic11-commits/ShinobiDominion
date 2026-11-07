-- One hero at a time (ADR-030). Players keep the hero on their earliest base; the others go back to the roster.
UPDATE sd_hero h
SET base_slot = NULL
WHERE base_slot IS NOT NULL
  AND base_slot <> (SELECT min(o.base_slot) FROM sd_hero o WHERE o.user_id = h.user_id AND o.base_slot IS NOT NULL);
