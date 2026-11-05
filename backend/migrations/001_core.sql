-- Authoritative player economy, in one database so that every change can share one transaction.
-- CHECK constraints are a last line of defence: even buggy code cannot store these bad states.

CREATE TABLE IF NOT EXISTS sd_player (
    user_id    UUID PRIMARY KEY,
    crystals   BIGINT NOT NULL CHECK (crystals >= 0),
    created_at TIMESTAMPTZ NOT NULL DEFAULT now()
);

CREATE TABLE IF NOT EXISTS sd_item (
    user_id UUID NOT NULL REFERENCES sd_player (user_id),
    item_id TEXT NOT NULL,
    count   BIGINT NOT NULL CHECK (count >= 0),
    PRIMARY KEY (user_id, item_id)
);

CREATE TABLE IF NOT EXISTS sd_hero (
    instance_id  UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    user_id      UUID NOT NULL REFERENCES sd_player (user_id),
    archetype_id TEXT NOT NULL,
    evolution    INT NOT NULL DEFAULT 0 CHECK (evolution BETWEEN 0 AND 3),
    level        INT NOT NULL DEFAULT 1 CHECK (level BETWEEN 1 AND 200),
    experience   BIGINT NOT NULL DEFAULT 0 CHECK (experience >= 0),
    locked       BOOLEAN NOT NULL DEFAULT FALSE,
    base_slot    INT,
    created_at   TIMESTAMPTZ NOT NULL DEFAULT now()
);
CREATE INDEX IF NOT EXISTS sd_hero_user ON sd_hero (user_id);
-- A base holds one hero. A hero holds one slot by being a single column on a single row.
CREATE UNIQUE INDEX IF NOT EXISTS sd_hero_slot ON sd_hero (user_id, base_slot) WHERE base_slot IS NOT NULL;

-- A retried request returns its stored response instead of running again.
CREATE TABLE IF NOT EXISTS sd_idempotency (
    user_id      UUID NOT NULL,
    key          TEXT NOT NULL,
    kind         TEXT NOT NULL,
    request_hash TEXT NOT NULL,
    response     TEXT NOT NULL,
    created_at   TIMESTAMPTZ NOT NULL DEFAULT now(),
    PRIMARY KEY (user_id, key)
);

CREATE TABLE IF NOT EXISTS sd_currency_ledger (
    id            BIGSERIAL PRIMARY KEY,
    user_id       UUID NOT NULL,
    delta         BIGINT NOT NULL,
    balance_after BIGINT NOT NULL CHECK (balance_after >= 0),
    reason        TEXT NOT NULL,
    ref           TEXT NOT NULL,
    created_at    TIMESTAMPTZ NOT NULL DEFAULT now()
);
CREATE INDEX IF NOT EXISTS sd_currency_ledger_user ON sd_currency_ledger (user_id);

CREATE TABLE IF NOT EXISTS sd_item_ledger (
    id         BIGSERIAL PRIMARY KEY,
    user_id    UUID NOT NULL,
    item_id    TEXT NOT NULL,
    delta      BIGINT NOT NULL,
    reason     TEXT NOT NULL,
    ref        TEXT NOT NULL,
    created_at TIMESTAMPTZ NOT NULL DEFAULT now()
);

-- One row per roll, with the table version it used.
CREATE TABLE IF NOT EXISTS sd_summon_roll (
    id           BIGSERIAL PRIMARY KEY,
    user_id      UUID NOT NULL,
    request_key  TEXT NOT NULL,
    roll_index   INT NOT NULL,
    pool_version INT NOT NULL,
    archetype_id TEXT NOT NULL,
    instance_id  UUID NOT NULL,
    created_at   TIMESTAMPTZ NOT NULL DEFAULT now(),
    UNIQUE (user_id, request_key, roll_index)
);
CREATE INDEX IF NOT EXISTS sd_summon_roll_user ON sd_summon_roll (user_id);

CREATE TABLE IF NOT EXISTS sd_evolution_log (
    id                 BIGSERIAL PRIMARY KEY,
    user_id            UUID NOT NULL,
    request_key        TEXT NOT NULL,
    main_instance_id   UUID NOT NULL,
    consumed_instance  UUID NOT NULL UNIQUE,
    consumed_archetype TEXT NOT NULL,
    catalyst_id        TEXT NOT NULL,
    from_evolution     INT NOT NULL,
    to_evolution       INT NOT NULL,
    created_at         TIMESTAMPTZ NOT NULL DEFAULT now()
);
