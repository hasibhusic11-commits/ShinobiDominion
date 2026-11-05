# Backend

Nakama 3.42.0, Go runtime, Postgres 16. Tables are defined in `backend/migrations/`. Rules live in `backend/nakama/village.go` with no database code, and `unity/Assets/_Game/Tests/EditMode/rules_vectors.json` runs through both the Go and C# rules. When changing a rule, change both and add a scenario.

## Running it
Docker Desktop must be running. Secrets live in `backend/.env`, which is gitignored; `.env.example` shows the format. From `backend/`:

```bash
docker compose up -d --build --wait nakama
```

```bash
docker compose run --rm tests
```

```bash
powershell -ExecutionPolicy Bypass -File tests/run_recovery.ps1
```

The last one kills the server under load and briefly runs it in production mode. `write_client_config.ps1` points the game at the server. Ports bind to this PC only: 7350 for the game, 7351 for the admin console.

## Calls (all need a signed-in player)
| Call | Purpose |
|---|---|
| `sd_get_state` | Crystals, heroes, items. Creates the player on first use. |
| `sd_summon_odds` | The exact drop table and prices |
| `sd_summon` | `poolVersion`, `count` (1 or 10), `key` |
| `sd_evolve` | `key`, `mainInstanceId`, `duplicateInstanceId` |
| `sd_set_lock` | `instanceId`, `locked` |
| `sd_assign_hero` | `slot`, `instanceId` (null clears the slot) |
| `sd_village_place` | `key`, `definitionId`, `x`, `z`, `rotation` |
| `sd_village_walls` | `key`, `x1`, `z1`, `x2`, `z2` |
| `sd_village_move` | `key`, `id`, `x`, `z`, `rotation` |
| `sd_village_remove` | `key`, `id`. A hero on that base becomes unassigned. |
| `sd_village_upgrade` | `key`, `id`, `group` (upgrades all connected walls) |
| `sd_village_collect` | `key`, `id`. The amount comes from the server's clock. |
| `sd_dev_grant`, `sd_dev_audit` | Exist only when `SD_DEV_MODE` is exactly `true` |

## Tables
| Table | Holds |
|---|---|
| `sd_player` | Crystals and gold, one row per player, the lock every change takes |
| `sd_building` | Every building and wall segment in every village |
| `sd_hero` | Hero instances, with UUIDs separate from the hero they are |
| `sd_item` | Catalysts and other items |
| `sd_idempotency` | Completed request IDs and their stored answers |
| `sd_currency_ledger`, `sd_item_ledger` | Every change, with its reason |
| `sd_summon_roll` | Every roll, with the table version it used |
| `sd_evolution_log` | Every evolution and the hero it consumed |

Development mode gives each new player free heroes and catalysts for testing. It is not a summon, and production mode gives none.
