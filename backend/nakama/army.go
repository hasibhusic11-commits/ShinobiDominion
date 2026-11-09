package main

import (
	"context"
	"database/sql"
	_ "embed"
	"encoding/json"
	"errors"
	"strconv"
	"time"

	"github.com/heroiclabs/nakama-common/runtime"
)

//go:embed config/troops.json
var troopsJSON []byte

const maxTrainAtOnce = 100

// The most troops one raid can land, which keeps the battle's cost bounded on the server.
const maxRaidTroops = 200

const (
	vBadTroop       vErr = "BadTroop"
	vNoTrainingHall vErr = "NoTrainingHall"
	vTroopLocked    vErr = "TroopLocked"
	vCampsFull      vErr = "CampsFull"
)

type troopDef struct {
	ID           string `json:"id"`
	DisplayName  string `json:"displayName"`
	Space        int    `json:"space"`
	GoldCost     int64  `json:"goldCost"`
	ManaCost     int64  `json:"manaCost"`
	RequiredHall int    `json:"requiredHall"`
	Role         string `json:"role"`
	Hp           int64  `json:"hp"`
	Attack       int64  `json:"attack"`
	Defense      int64  `json:"defense"`
}

type troopCatalog struct {
	all  []*troopDef
	byID map[string]*troopDef
}

func loadTroopCatalog(data []byte) (*troopCatalog, error) {
	var document struct {
		SchemaVersion int         `json:"schemaVersion"`
		Troops        []*troopDef `json:"troops"`
	}
	if err := json.Unmarshal(data, &document); err != nil {
		return nil, err
	}
	if document.SchemaVersion != 1 {
		return nil, errors.New("unsupported troop file")
	}
	catalog := &troopCatalog{all: document.Troops, byID: map[string]*troopDef{}}
	for _, troop := range document.Troops {
		if troop.Space < 1 || troop.GoldCost < 0 || troop.ManaCost < 0 || troop.Hp < 1 || troop.RequiredHall < 1 {
			return nil, errors.New("troop " + troop.ID + " has invalid values")
		}
		if _, err := roleNumber(troop.Role); err != nil {
			return nil, err
		}
		catalog.byID[troop.ID] = troop
	}
	return catalog, nil
}

func (v *village) troopCapacity() int64 {
	var capacity int64
	for _, b := range v.byID {
		if b.Def.Function == "TroopCamp" {
			capacity += b.Def.at(b.Level).TroopCapacity
		}
	}
	return capacity
}

func (v *village) hallLevel() int {
	level := 0
	for _, b := range v.byID {
		if b.Def.Function == "Training" && b.Level > level {
			level = b.Level
		}
	}
	return level
}

func usedSpace(catalog *troopCatalog, counts map[string]int) int64 {
	var used int64
	for id, count := range counts {
		if troop, ok := catalog.byID[id]; ok {
			used += int64(troop.Space) * int64(count)
		}
	}
	return used
}

// train mirrors Army.TryTrain. Count is checked first, so the arithmetic below can't overflow.
func train(v *village, catalog *troopCatalog, counts map[string]int, troopID string, count int) vErr {
	troop, ok := catalog.byID[troopID]
	if !ok || count < 1 || count > maxTrainAtOnce {
		return vBadTroop
	}
	hall := v.hallLevel()
	if hall == 0 {
		return vNoTrainingHall
	}
	if hall < troop.RequiredHall {
		return vTroopLocked
	}
	if usedSpace(catalog, counts)+int64(troop.Space)*int64(count) > v.troopCapacity() {
		return vCampsFull
	}
	// Training costs gold and Mana (ADR-038). Both are checked before either is taken.
	cost := troop.GoldCost * int64(count)
	if v.gold < cost {
		return vFunds
	}
	manaCost := troop.ManaCost * int64(count)
	if v.mana < manaCost {
		return vNoMana
	}
	v.gold -= cost
	v.spendMana(manaCost)
	counts[troopID] += count
	return vOK
}

func loadTroopCounts(ctx context.Context, tx *sql.Tx, userID string) (map[string]int, error) {
	rows, err := tx.QueryContext(ctx, `SELECT troop_id, count FROM sd_troop WHERE user_id = $1`, userID)
	if err != nil {
		return nil, err
	}
	defer rows.Close()
	counts := map[string]int{}
	for rows.Next() {
		var id string
		var count int
		if err := rows.Scan(&id, &count); err != nil {
			return nil, err
		}
		counts[id] = count
	}
	return counts, rows.Err()
}

func storeTroopCount(ctx context.Context, tx *sql.Tx, userID, troopID string, count int) error {
	if count <= 0 {
		_, err := tx.ExecContext(ctx, `DELETE FROM sd_troop WHERE user_id = $1 AND troop_id = $2`, userID, troopID)
		return err
	}
	_, err := tx.ExecContext(ctx, `INSERT INTO sd_troop (user_id, troop_id, count) VALUES ($1, $2, $3)
		ON CONFLICT (user_id, troop_id) DO UPDATE SET count = EXCLUDED.count`, userID, troopID, count)
	return err
}

func (s *Server) rpcTroopTrain(ctx context.Context, logger runtime.Logger, db *sql.DB, nk runtime.NakamaModule, payload string) (string, error) {
	userID, err := userFrom(ctx)
	if err != nil {
		return "", err
	}
	var request struct {
		Key     string `json:"key"`
		TroopID string `json:"troopId"`
		Count   int    `json:"count"`
	}
	if err := decodeStrict(payload, &request); err != nil {
		return "", err
	}
	if !keyPattern.MatchString(request.Key) {
		return "", errBadKey
	}
	if !s.limiter.Allow(userID, time.Now()) {
		return "", errRateLimited
	}
	requestHash := hashRequest(request.TroopID, strconv.Itoa(request.Count))

	var output string
	err = withTx(ctx, db, func(tx *sql.Tx) error {
		crystals, err := s.lockPlayer(ctx, tx, userID)
		if err != nil {
			return err
		}
		if stored, found, err := replayed(ctx, tx, userID, request.Key, "troop_train", requestHash); err != nil {
			return err
		} else if found {
			output = stored
			return nil
		}
		v, err := s.loadVillage(ctx, tx, userID, crystals)
		if err != nil {
			return err
		}
		counts, err := loadTroopCounts(ctx, tx, userID)
		if err != nil {
			return err
		}
		beforeGold := v.gold
		if refusal := train(v, s.troops, counts, request.TroopID, request.Count); refusal != vOK {
			return gameError(string(refusal), codePrecondition)
		}
		if err := storeTroopCount(ctx, tx, userID, request.TroopID, counts[request.TroopID]); err != nil {
			return err
		}
		if err := s.bumpQuest(ctx, tx, userID, "train", request.Count); err != nil {
			return err
		}
		if err := persistVillage(ctx, tx, userID, v, beforeGold, crystals, "troop_train", request.Key); err != nil {
			return err
		}
		state, err := loadState(ctx, tx, userID, crystals)
		if err != nil {
			return err
		}
		output, err = marshal(state)
		if err != nil {
			return err
		}
		return storeResponse(ctx, tx, userID, request.Key, "troop_train", requestHash, output)
	})
	if err != nil {
		return "", err
	}
	return output, nil
}
