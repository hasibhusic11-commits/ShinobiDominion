-- Safety: retain reports even after the guild's 100-message chat history is pruned.
-- A report snapshots the exact message for later human moderation. This table is
-- never accessible from a public RPC (only inserts scoped to a signed-in reporter).
CREATE TABLE IF NOT EXISTS sd_guild_chat_report (
    id           BIGSERIAL PRIMARY KEY,
    guild_id     UUID NOT NULL, -- retain evidence after a guild is dissolved
    message_id   BIGINT NOT NULL,
    sender_id    UUID NOT NULL REFERENCES sd_player (user_id),
    reporter_id  UUID NOT NULL REFERENCES sd_player (user_id),
    text_snapshot TEXT NOT NULL,
    status       TEXT NOT NULL DEFAULT 'pending'
                 CHECK (status IN ('pending', 'reviewed', 'dismissed')),
    reported_at  TIMESTAMPTZ NOT NULL DEFAULT now(),
    UNIQUE (reporter_id, message_id),
    CHECK (sender_id <> reporter_id)
);
CREATE INDEX IF NOT EXISTS sd_guild_reports_queue
    ON sd_guild_chat_report (status, reported_at);
CREATE INDEX IF NOT EXISTS sd_guild_reports_rate
    ON sd_guild_chat_report (reporter_id, reported_at DESC);
