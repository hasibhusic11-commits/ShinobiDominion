# Security model — current prototype assessment

Last reconciled: 2026-11-11. This document is a design/code review, **not a penetration test or certification**.

## Server-authoritative operations

Nakama's Go backend authenticates requests using the session user ID. Hero ownership,
summon odds, resource balances, items, catalysts, evolution, assignments, placement,
upgrade timers, resource collection, research, troop training, and rewards are decided
by the server. Campaign raids, asynchronous rival raids, Siege, Arena and Trial Tower
battles are adjudicated server-side; the client renders a simulation/playback but
cannot submit a forged result or invented payout.

Critical value-changing RPCs use a Postgres transaction, player-row lock,
request idempotency key, and request hash. Summoning uses cryptographic random
draws with no pity, first-time guarantee or client-supplied random seed. There are
C#/Go shared rules and battle-vector tests. See `backend/nakama/main.go`,
`battle_rpc.go`, `modes.go` and `backend/tests/`.

## Request hardening

- `decodeStrict` rejects unknown fields, oversized payloads, and **trailing
  JSON data/values** (fixed by the audit branch, with unit test cases).
- Guild chat text is bounded, sanitized for control characters and rate-limited;
  the UI disables Unity rich-text interpretation for user-generated chat.
- Guild chat now supports member-initiated **reports**. The server checks that
  the reported message belongs to the member's current guild, forbids
  self-reporting, limits new reports to 10 per 24 hours, and persists a private
  snapshot even after chat history is trimmed. It never exposes reports to
  players. Schema: `019_guild_chat_reports.sql`.
- Local Docker startup no longer enables shell `-x` tracing of expanded
  credentials. Local development credentials should still never be reused in
  production.

## Production blockers (not solved by the code audit)

1. **Account recovery:** device-ID-only sign-in (locally stored random ID in
   PlayerPrefs) does not survive loss or reinstall. Add verified account linking
   and recovery (platform identity, email verification, or equivalent).
2. **TLS and deployment:** the checked-in Compose file is **localhost-only** and
   uses HTTP. Production needs HTTPS/WSS, correctly scoped keys/secrets, DB
   backups, regional hosting, migration/rollback strategy and rotation.
3. **Moderation operations:** reporting alone is not moderation. Before public
   guild chat, staff need a protected review console, queue monitoring,
   moderation actions, anti-harassment/abuse rules, escalation and retention
   policies. Do not ship an unmonitored queue.
4. **Rate limiting:** the in-memory token bucket is per process; it is not a
   coordinated multi-node limiter and does not enforce per-IP abuse controls.
5. **Abuse telemetry:** no comprehensive alerting, bot detection, login security
   monitoring or independent external security review.
6. **Paid features:** no money is charged today. Monetizing random rewards
   requires platform/region/age/legal review; don't assume a compliant design.

## Release-safety defaults

- `SD_DEV_MODE` must be **false** in any nonlocal deployment. Development RPC
  grants are registered only when exactly `true`.
- `server.local.json`, `.env`, secrets and private keys are Git-ignored.
- Release Unity builds do not load or mutate offline saved currencies/heroes.
- No pull request is automatically deployed from this branch. Unity PlayMode,
  backend integration tests and real-device testing remain required.
