# Security model

## What the server controls today
Crystals, gold, heroes, catalysts, summon rolls, evolution, hero locks, hero base assignments, village layout, building limits, placement, walls, moves, upgrades, removals and mine collection. While connected, the game sends requests and replaces its copy with the answer. It writes no hero or village file to the device.

The client keeps a copy of the rules only for instant green and red previews. Mine income uses the server's clock, so changing the device's date earns nothing.

Release builds refuse to change anything without a server and never read a local save. Editor and development builds can still play offline from a local file, for testing only.

## Server-owned gameplay as of the current prototype
The Go/Nakama server now checks battle outcomes for scripted dungeons, rival raids, Siege, Arena, and Trial Tower. It also owns timed building upgrades, hero progression, training, gear, talents, and research. See backend battle RPCs and tests for the implemented modes.

The client produces a visual replay of the server's authoritative battle log. It must not be trusted for currency, inventory, damage, placement, or combat results.

## How a request stays safe
Each summon and evolution is one Postgres transaction:
1. Lock the player's row, so one player's requests run strictly one at a time.
2. If the request ID was already completed, return its stored answer. A retry can never charge twice. Reusing an ID for a different request is refused.
3. Run the rules, write everything, then commit. Any failure rolls all of it back.

Requests with unknown fields are refused, so a client cannot supply results, seeds or prices. Hero lookups are scoped to the player, so another player's hero is simply "not found". Database `CHECK` constraints stop negative balances even if code is wrong.

Rolls use Go's `crypto/rand` with integer weights. The roll function takes only a random source, so there is nowhere for pity, streak or duplicate logic to read from.

## Proven by test (`backend/tests`)
| Attack | Result |
|---|---|
| No token, or server key with no player | Refused |
| Forged results, seed, price, or bad count | Refused, nothing changes |
| Replay, including 40 simultaneous same-ID requests | One charge, identical results |
| 40 simultaneous summons with money for 7 | Exactly 7 succeed, balance never negative |
| Sacrificing one duplicate twice at once | Exactly one succeeds |
| Assigning a duplicate while sacrificing it | Never both |
| Using another player's heroes | Refused, victim unchanged |
| Hard-killing the server under load | 2,400 retried rolls: totals and ledger agree exactly |
| Development mode off | Dev calls do not exist, no free heroes |
| Forged cost, gold, level or collect amount | Refused, nothing changes |
| Enormous or negative coordinates, huge wall runs | Refused without crashing |
| 25 simultaneous collects from one mine | Paid once |
| 20 simultaneous wall builds with gold for 3 | Exactly 3 succeed |
| 16 simultaneous Hero Bases | Stops at six |
| Same cell built 25 times at once | One succeeds |
| Touching another player's village | Not found, victim unchanged |
| Assigning a hero to a Dojo or an invented base | Refused |
| Forged local save, online | Ignored, server's village shown |
| Forged local save, release mode with no server | Never loaded, nothing can change |
| Client and server rules drifting apart | The same scenario file runs in C# and Go |
| Removing the database locks (deliberately) | Tests failed, proving they can detect it |

## Known gaps before any public release
- Local development runs HTTP on localhost. Release client builds now reject non-HTTPS configuration, but deployment still needs an audited TLS terminator and secure production routing.
- Rate limiting is per server process, so it is not shared across several servers.
- The development server key sits in a gitignored local file. Production needs its own keys. Nakama's client key is designed to ship in clients, so it does not protect anything by itself.
- There is no anomaly telemetry yet, and no independent security review has been done.
- Device-ID sign-in means losing the device loses the account. Account linking, verified recovery, and an account migration flow are not built; **do not launch with real-money purchases or nonrecoverable player progression until resolved**.
- Guild chat now allows authenticated per-message reports stored in `sd_guild_chat_report` (migration 019). Operator review, enforcement policy, user blocking, evidence retention/deletion and appeals are still required.
- The CI pipeline checks Go, shared C# rules and configuration hygiene. It cannot replace real Unity PlayMode, live Nakama integration, device QA or independent penetration testing.
- Paid summoning needs platform, age and regional law review first. Nothing here charges real money.

## Raids
The client sends only a raid id and where its heroes stand. The server reads the team from the bases, stores the defender and team as a snapshot, validates each placement, runs the battle itself, and pays gold once per attack inside one transaction with a ledger entry. Strict decoding refuses claimed results. Retries with the same key return the stored result, and a second key is refused. Rival-village raids and revenge now also execute through server-authoritative attack snapshots and validated deployment logs. Verify the current implementation and integration suite before any public release; this statement is not an independent penetration-test certification.
