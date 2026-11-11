-- Retain evidence for review independently from the rolling 100-line chat cache.
-- No foreign key to chat or guild: those rows can be removed when the guild disbands.
-- Access must be restricted to server-side moderation personnel, not a player-facing RPC.
CREATE TABLE IF NOT EXISTS sd_guild_chat_report (
    id BIGSERIAL PRIMARY KEY,
    guild_id UUID NOT NULL,
    reporter_id UUID NOT NULL REFERENCES sd_player(user_id),
    author_id UUID NOT NULL REFERENCES sd_player(user_id),
    message_id BIGINT NOT NULL,
    message_text TEXT NOT NULL CHECK (char_length(message_text) BETWEEN 1 AND 200),
    reason TEXT NOT NULL CHECK (reason IN ('harassment', 'hate', 'spam', 'other')),
    status TEXT NOT NULL DEFAULT 'open' CHECK (status IN ('open', 'reviewing', 'resolved', 'dismissed')),
    created_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    UNIQUE (reporter_id, message_id)
);
CREATE INDEX IF NOT EXISTS sd_guild_report_queue ON sd_guild_chat_report(status, created_at);
CREATE INDEX IF NOT EXISTS sd_guild_report_rate ON sd_guild_chat_report(reporter_id, created_at);
