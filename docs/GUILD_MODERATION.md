# Guild message reporting — reviewer operations

This is an **operator runbook**, not an in-game administrative dashboard. No player-facing RPC lists or edits reports. A trusted database operator must handle review in a protected environment.

## Data flow
1. Guild chat now returns a stable numeric `messageId` to the Unity client.
2. A member can report another member's currently retained message with one reason: `harassment`, `hate`, `spam`, `other`.
3. `sd_guild_report` checks the active authenticated user, guild membership, author mismatch, the original message ID and guild, and a limit of 10 new reports per 24 hours. Identical (reporter, message) submissions are stored once.
4. SQL migration `019_guild_chat_reports.sql` stores message evidence even if the original chat is rotated or guild is later disbanded. **This is sensitive user-generated content**, not public game data.

## Manual reviewer queries (admin-only, least-privilege role)

```sql
SELECT id, guild_id, reporter_id, author_id, message_id, message_text,
       reason, status, created_at
FROM sd_guild_chat_report
WHERE status = 'open'
ORDER BY created_at ASC
LIMIT 100;
```

After reviewing evidence, a trusted operator may mark a record (an actual enforcement policy must be defined separately):

```sql
UPDATE sd_guild_chat_report
SET status = 'reviewing'
WHERE id = $1 AND status = 'open';
```

Never expose database access, report payloads or reporter identity to client RPCs, normal guild members, or GitHub logs.

## Remaining safety work before public release

- Build private moderation UI/queue with staff authorization, enforcement actions, action audit history and appeals.
- Implement player block/mute, conduct guidelines and response SLA.
- Define report retention and deletion periods with legal/privacy review; migration deliberately does not implement auto-purge.
- Add integration tests for report ownership, cross-guild access, idempotency/rate limits, and disappearing messages.
- Add anti-abuse alerting and shared (not only in-process) rate limiting for scaled server deployments.

**No automatic punishment** is imposed by the report endpoint. Avoid making moderation decisions based solely on a single report.
