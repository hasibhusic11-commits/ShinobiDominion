package main

import (
	"context"
	"database/sql"
	"strconv"
	"time"

	"github.com/heroiclabs/nakama-common/runtime"
)

// dotnetTicksAtUnixEpoch converts server milliseconds into the ticks the client's save uses.
const dotnetTicksAtUnixEpoch = int64(621355968000000000)

func nowMillis() int64 { return time.Now().UnixMilli() }

func ticksFromMillis(ms int64) int64 { return ms*10000 + dotnetTicksAtUnixEpoch }

// createStarterVillage runs once per player, including players from before the village moved here.
func (s *Server) createStarterVillage(ctx context.Context, tx *sql.Tx, userID string) error {
	if _, err := tx.ExecContext(ctx, `DELETE FROM sd_building WHERE user_id = $1`, userID); err != nil {
		return err
	}
	// Any old assignment pointed at a village that no longer exists.
	if _, err := tx.ExecContext(ctx, `UPDATE sd_hero SET base_slot = NULL WHERE user_id = $1`, userID); err != nil {
		return err
	}
	now := nowMillis()
	for index, item := range starterLayout {
		if _, err := tx.ExecContext(ctx, `INSERT INTO sd_building (user_id, id, definition_id, x, z, last_collected_ms) VALUES ($1, $2, $3, $4, $5, $6)`,
			userID, index+1, item.Def, item.X, item.Z, now); err != nil {
			return err
		}
	}
	gold := s.buildings.StartingGold
	if _, err := tx.ExecContext(ctx, `UPDATE sd_player SET gold = $1, next_building_id = $2, village_ready = TRUE WHERE user_id = $3`, gold, len(starterLayout)+1, userID); err != nil {
		return err
	}
	_, err := tx.ExecContext(ctx, `INSERT INTO sd_currency_ledger (user_id, delta, balance_after, reason, ref, currency) VALUES ($1, $2, $2, 'starting_grant', 'account', 'gold')`, userID, gold)
	return err
}

// loadVillage reads the player's village. The caller must already hold the player lock.
func (s *Server) loadVillage(ctx context.Context, tx *sql.Tx, userID string, crystals int64) (*village, error) {
	var gold int64
	var nextID int
	if err := tx.QueryRowContext(ctx, `SELECT gold, next_building_id FROM sd_player WHERE user_id = $1`, userID).Scan(&gold, &nextID); err != nil {
		return nil, err
	}
	v := newVillage(s.buildings, gold, crystals, nextID)
	rows, err := tx.QueryContext(ctx, `SELECT id, definition_id, x, z, rotation, level, last_collected_ms FROM sd_building WHERE user_id = $1 ORDER BY id`, userID)
	if err != nil {
		return nil, err
	}
	defer rows.Close()
	for rows.Next() {
		b := &building{}
		var definitionID string
		if err := rows.Scan(&b.ID, &definitionID, &b.X, &b.Z, &b.Rotation, &b.Level, &b.LastCollectedMs); err != nil {
			return nil, err
		}
		def, ok := s.buildings.byID[definitionID]
		if !ok {
			return nil, gameError("UNKNOWN_STORED_BUILDING", codePrecondition)
		}
		b.Def = def
		if err := v.addExisting(b); err != nil {
			return nil, err
		}
	}
	return v, rows.Err()
}

// persistVillage writes only what changed, inside the caller's transaction.
func persistVillage(ctx context.Context, tx *sql.Tx, userID string, v *village, beforeGold, beforeCrystals int64, reason, ref string) error {
	for _, b := range v.inserted {
		if _, deleted := v.byID[b.ID]; !deleted {
			continue
		}
		if _, err := tx.ExecContext(ctx, `INSERT INTO sd_building (user_id, id, definition_id, x, z, rotation, level, last_collected_ms) VALUES ($1, $2, $3, $4, $5, $6, $7, $8)`,
			userID, b.ID, b.Def.ID, b.X, b.Z, b.Rotation, b.Level, b.LastCollectedMs); err != nil {
			return err
		}
	}
	for _, b := range v.updated {
		if _, err := tx.ExecContext(ctx, `UPDATE sd_building SET x = $1, z = $2, rotation = $3, level = $4, last_collected_ms = $5 WHERE user_id = $6 AND id = $7`,
			b.X, b.Z, b.Rotation, b.Level, b.LastCollectedMs, userID, b.ID); err != nil {
			return err
		}
	}
	for _, id := range v.deleted {
		if _, err := tx.ExecContext(ctx, `DELETE FROM sd_building WHERE user_id = $1 AND id = $2`, userID, id); err != nil {
			return err
		}
		// A hero on a removed base goes back to being unassigned, in the same transaction.
		if _, err := tx.ExecContext(ctx, `UPDATE sd_hero SET base_slot = NULL WHERE user_id = $1 AND base_slot = $2`, userID, id); err != nil {
			return err
		}
	}
	if _, err := tx.ExecContext(ctx, `UPDATE sd_player SET gold = $1, crystals = $2, next_building_id = $3 WHERE user_id = $4`, v.gold, v.crystals, v.nextID, userID); err != nil {
		return err
	}
	if v.gold != beforeGold {
		if _, err := tx.ExecContext(ctx, `INSERT INTO sd_currency_ledger (user_id, delta, balance_after, reason, ref, currency) VALUES ($1, $2, $3, $4, $5, 'gold')`, userID, v.gold-beforeGold, v.gold, reason, ref); err != nil {
			return err
		}
	}
	if v.crystals != beforeCrystals {
		if _, err := tx.ExecContext(ctx, `INSERT INTO sd_currency_ledger (user_id, delta, balance_after, reason, ref, currency) VALUES ($1, $2, $3, $4, $5, 'crystals')`, userID, v.crystals-beforeCrystals, v.crystals, reason, ref); err != nil {
			return err
		}
	}
	return nil
}

// villageState is the same shape as the client's VillageSave, which validates it again on arrival.
type villageState struct {
	SchemaVersion  int             `json:"schemaVersion"`
	MapVersion     int             `json:"mapVersion"`
	NextInstanceID int             `json:"nextInstanceId"`
	Gold           int64           `json:"gold"`
	Crystals       int64           `json:"crystals"`
	Buildings      []buildingState `json:"buildings"`
}

type buildingState struct {
	ID            int     `json:"id"`
	DefinitionID  string  `json:"definitionId"`
	X             int     `json:"x"`
	Z             int     `json:"z"`
	Rotation      int     `json:"rotation"`
	Level         int     `json:"level"`
	AssignedHero  *string `json:"assignedHeroInstanceId"`
	LastCollected int64   `json:"lastCollectedUtcTicks"`
}

func loadVillageState(ctx context.Context, tx *sql.Tx, userID string, crystals int64) (*villageState, error) {
	state := &villageState{SchemaVersion: 1, MapVersion: 1, Crystals: crystals, Buildings: []buildingState{}}
	if err := tx.QueryRowContext(ctx, `SELECT gold, next_building_id FROM sd_player WHERE user_id = $1`, userID).Scan(&state.Gold, &state.NextInstanceID); err != nil {
		return nil, err
	}
	rows, err := tx.QueryContext(ctx, `SELECT b.id, b.definition_id, b.x, b.z, b.rotation, b.level, b.last_collected_ms,
		(SELECT h.instance_id::text FROM sd_hero h WHERE h.user_id = b.user_id AND h.base_slot = b.id)
		FROM sd_building b WHERE b.user_id = $1 ORDER BY b.id`, userID)
	if err != nil {
		return nil, err
	}
	defer rows.Close()
	for rows.Next() {
		var entry buildingState
		var hero sql.NullString
		var collectedMs int64
		if err := rows.Scan(&entry.ID, &entry.DefinitionID, &entry.X, &entry.Z, &entry.Rotation, &entry.Level, &collectedMs, &hero); err != nil {
			return nil, err
		}
		entry.LastCollected = ticksFromMillis(collectedMs)
		if hero.Valid {
			value := hero.String
			entry.AssignedHero = &value
		}
		state.Buildings = append(state.Buildings, entry)
	}
	return state, rows.Err()
}

// runVillageOp is the one path every village change takes: lock the player, check the request
// id, apply the rules, then write everything together or nothing at all.
func (s *Server) runVillageOp(ctx context.Context, db *sql.DB, userID, key, kind, requestHash string, apply func(*village, int64) vErr) (string, error) {
	if !keyPattern.MatchString(key) {
		return "", errBadKey
	}
	if !s.limiter.Allow(userID, time.Now()) {
		return "", errRateLimited
	}
	var output string
	err := withTx(ctx, db, func(tx *sql.Tx) error {
		crystals, err := s.lockPlayer(ctx, tx, userID)
		if err != nil {
			return err
		}
		if stored, found, err := replayed(ctx, tx, userID, key, kind, requestHash); err != nil {
			return err
		} else if found {
			output = stored
			return nil
		}
		v, err := s.loadVillage(ctx, tx, userID, crystals)
		if err != nil {
			return err
		}
		beforeGold := v.gold
		if refusal := apply(v, nowMillis()); refusal != vOK {
			return gameError(string(refusal), codePrecondition)
		}
		if err := persistVillage(ctx, tx, userID, v, beforeGold, crystals, kind, key); err != nil {
			return err
		}
		state, err := loadState(ctx, tx, userID, v.crystals)
		if err != nil {
			return err
		}
		output, err = marshal(state)
		if err != nil {
			return err
		}
		return storeResponse(ctx, tx, userID, key, kind, requestHash, output)
	})
	if err != nil {
		return "", err
	}
	return output, nil
}

func itoa(values ...int) string {
	joined := ""
	for _, value := range values {
		joined += strconv.Itoa(value) + "|"
	}
	return joined
}

type placeRequest struct {
	Key          string `json:"key"`
	DefinitionID string `json:"definitionId"`
	X            int    `json:"x"`
	Z            int    `json:"z"`
	Rotation     int    `json:"rotation"`
}

func (s *Server) rpcPlace(ctx context.Context, logger runtime.Logger, db *sql.DB, nk runtime.NakamaModule, payload string) (string, error) {
	userID, err := userFrom(ctx)
	if err != nil {
		return "", err
	}
	var request placeRequest
	if err := decodeStrict(payload, &request); err != nil {
		return "", err
	}
	return s.runVillageOp(ctx, db, userID, request.Key, "place", hashRequest(request.DefinitionID, itoa(request.X, request.Z, request.Rotation)), func(v *village, now int64) vErr {
		_, refusal := v.place(request.DefinitionID, request.X, request.Z, request.Rotation, now)
		return refusal
	})
}

type wallsRequest struct {
	Key string `json:"key"`
	X1  int    `json:"x1"`
	Z1  int    `json:"z1"`
	X2  int    `json:"x2"`
	Z2  int    `json:"z2"`
}

func (s *Server) rpcWalls(ctx context.Context, logger runtime.Logger, db *sql.DB, nk runtime.NakamaModule, payload string) (string, error) {
	userID, err := userFrom(ctx)
	if err != nil {
		return "", err
	}
	var request wallsRequest
	if err := decodeStrict(payload, &request); err != nil {
		return "", err
	}
	return s.runVillageOp(ctx, db, userID, request.Key, "walls", hashRequest(itoa(request.X1, request.Z1, request.X2, request.Z2)), func(v *village, now int64) vErr {
		return v.placeWalls(request.X1, request.Z1, request.X2, request.Z2, now)
	})
}

type moveRequest struct {
	Key      string `json:"key"`
	ID       int    `json:"id"`
	X        int    `json:"x"`
	Z        int    `json:"z"`
	Rotation int    `json:"rotation"`
}

func (s *Server) rpcMove(ctx context.Context, logger runtime.Logger, db *sql.DB, nk runtime.NakamaModule, payload string) (string, error) {
	userID, err := userFrom(ctx)
	if err != nil {
		return "", err
	}
	var request moveRequest
	if err := decodeStrict(payload, &request); err != nil {
		return "", err
	}
	return s.runVillageOp(ctx, db, userID, request.Key, "move", hashRequest(itoa(request.ID, request.X, request.Z, request.Rotation)), func(v *village, now int64) vErr {
		return v.move(request.ID, request.X, request.Z, request.Rotation)
	})
}

type buildingRequest struct {
	Key   string `json:"key"`
	ID    int    `json:"id"`
	Group bool   `json:"group"`
}

func (s *Server) rpcRemove(ctx context.Context, logger runtime.Logger, db *sql.DB, nk runtime.NakamaModule, payload string) (string, error) {
	userID, err := userFrom(ctx)
	if err != nil {
		return "", err
	}
	var request buildingRequest
	if err := decodeStrict(payload, &request); err != nil {
		return "", err
	}
	return s.runVillageOp(ctx, db, userID, request.Key, "remove", hashRequest(itoa(request.ID)), func(v *village, now int64) vErr {
		return v.remove(request.ID)
	})
}

func (s *Server) rpcUpgrade(ctx context.Context, logger runtime.Logger, db *sql.DB, nk runtime.NakamaModule, payload string) (string, error) {
	userID, err := userFrom(ctx)
	if err != nil {
		return "", err
	}
	var request buildingRequest
	if err := decodeStrict(payload, &request); err != nil {
		return "", err
	}
	return s.runVillageOp(ctx, db, userID, request.Key, "upgrade", hashRequest(itoa(request.ID), strconv.FormatBool(request.Group)), func(v *village, now int64) vErr {
		if request.Group {
			return v.upgradeWallGroup(request.ID)
		}
		return v.upgrade(request.ID)
	})
}

func (s *Server) rpcCollect(ctx context.Context, logger runtime.Logger, db *sql.DB, nk runtime.NakamaModule, payload string) (string, error) {
	userID, err := userFrom(ctx)
	if err != nil {
		return "", err
	}
	var request buildingRequest
	if err := decodeStrict(payload, &request); err != nil {
		return "", err
	}
	return s.runVillageOp(ctx, db, userID, request.Key, "collect", hashRequest(itoa(request.ID)), func(v *village, now int64) vErr {
		_, refusal := v.collect(request.ID, now)
		return refusal
	})
}
