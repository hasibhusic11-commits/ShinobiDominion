-- Guilds (ADR-042). Open to join, up to 30 members, one leader. Chat keeps the latest 100 lines per guild.
CREATE TABLE IF NOT EXISTS sd_guild (
    id          UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    name        TEXT NOT NULL,
    description TEXT NOT NULL DEFAULT '',
    leader_id   UUID NOT NULL REFERENCES sd_player (user_id),
    created_at  TIMESTAMPTZ NOT NULL DEFAULT now()
);
CREATE UNIQUE INDEX IF NOT EXISTS sd_guild_name ON sd_guild (lower(name));

CREATE TABLE IF NOT EXISTS sd_guild_member (
    user_id   UUID PRIMARY KEY REFERENCES sd_player (user_id),
    guild_id  UUID NOT NULL REFERENCES sd_guild (id),
    role      TEXT NOT NULL CHECK (role IN ('leader', 'member')),
    joined_at TIMESTAMPTZ NOT NULL DEFAULT now()
);
CREATE INDEX IF NOT EXISTS sd_guild_member_guild ON sd_guild_member (guild_id);

CREATE TABLE IF NOT EXISTS sd_guild_chat (
    id         BIGSERIAL PRIMARY KEY,
    guild_id   UUID NOT NULL REFERENCES sd_guild (id),
    user_id    UUID NOT NULL REFERENCES sd_player (user_id),
    text       TEXT NOT NULL CHECK (char_length(text) BETWEEN 1 AND 200),
    created_at TIMESTAMPTZ NOT NULL DEFAULT now()
);
CREATE INDEX IF NOT EXISTS sd_guild_chat_recent ON sd_guild_chat (guild_id, id DESC);
CREATE INDEX IF NOT EXISTS sd_guild_chat_rate ON sd_guild_chat (user_id, created_at DESC);
