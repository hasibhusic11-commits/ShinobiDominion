# Security model

## What the server controls today
Crystals, heroes, catalysts, summon rolls, evolution, hero locks and hero base assignments. While connected, the game sends requests and replaces its copy with the answer. It writes no hero file to the device.

## What the server does NOT control yet
These still run on the device and **can be forged by editing a local file**. Rule 10 of the brief is not met until they move:

- Village layout, building limits, upgrades and placement validation
- Gold, and collecting from mines
- Crystal Mine output. It credits the device's local balance, which the HUD ignores while online, so that output is effectively lost. It is locked until Dojo level 2.
- Whether a base "slot" is a real Hero Base. The server enforces six slots and hero ownership, but cannot see the village.
- Battle results, because battles do not exist yet

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
| Removing the database locks (deliberately) | Tests failed, proving they can detect it |

## Known gaps before any public release
- Traffic is plain HTTP on localhost. Production needs TLS.
- Rate limiting is per server process, so it is not shared across several servers.
- The development server key sits in a gitignored local file. Production needs its own keys. Nakama's client key is designed to ship in clients, so it does not protect anything by itself.
- There is no anomaly telemetry yet, and no independent security review has been done.
- Device-ID sign-in means losing the device loses the account. Account linking is not built.
- Paid summoning needs platform, age and regional law review first. Nothing here charges real money.
