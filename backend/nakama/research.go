package main

import (
	"context"
	"database/sql"
	"errors"
	"time"

	"github.com/heroiclabs/nakama-common/runtime"
)

// Troop research at the Research Hut (ADR-041). A study raises one kind of troop's HP and attack, costs
// Mana, takes time, and only one runs at a time. The level reachable is capped by the hut's level.
// Mirrors TroopResearch in C#; the shared rule scenarios check both.

const (
	vNoResearchHut vErr = "NoResearchHut"
	vHutTooLow     vErr = "ResearchHutTooLow"
	vResearchBusy  vErr = "ResearchBusy"
)

type researchConfig struct {
	MaxLevel        int     `json:"maxLevel"`
	PercentPerLevel int64   `json:"percentPerLevel"`
	ManaPerLevel    int64   `json:"manaPerLevel"`
	Seconds         []int64 `json:"seconds"`
}

type researchEntry struct {
	Level  int
	DoneMs int64
}

func (c *troopCatalog) researchCost(troop *troopDef, level int) int64 {
	return troop.ManaCost * c.research.ManaPerLevel * int64(level) * int64(level)
}

func (c *troopCatalog) researchTime(level int) int64 {
	if level < 0 || level >= len(c.research.Seconds) {
		return 0
	}
	return c.research.Seconds[level]
}

// boosted is a troop's battle stats after research: HP and attack grow, defense does not.
func (c *troopCatalog) boosted(troop *troopDef, level int) (hp, attack, defense int64) {
	percent := 100 + c.research.PercentPerLevel*int64(level)
	return troop.Hp * percent / 100, troop.Attack * percent / 100, troop.Defense
}

func (v *village) researchHutLevel() int {
	level := 0
	for _, b := range v.byID {
		if b.Def.Function == "Research" && b.Level > level {
			level = b.Level
		}
	}
	return level
}

func settleResearch(research map[string]*researchEntry, nowMs int64) {
	for _, entry := range research {
		if entry.DoneMs > 0 && entry.DoneMs <= nowMs {
			entry.Level++
			entry.DoneMs = 0
		}
	}
}

func startResearch(v *village, catalog *troopCatalog, research map[string]*researchEntry, troopID string, nowMs int64) vErr {
	settleResearch(research, nowMs)
	troop, ok := catalog.byID[troopID]
	if !ok {
		return vBadTroop
	}
	hut := v.researchHutLevel()
	if hut == 0 {
		return vNoResearchHut
	}
	for _, entry := range research {
		if entry.DoneMs > 0 {
			return vResearchBusy
		}
	}
	entry := research[troopID]
	if entry == nil {
		entry = &researchEntry{}
	}
	next := entry.Level + 1
	if next > catalog.research.MaxLevel {
		return vMaxLevel
	}
	if hut < next {
		return vHutTooLow
	}
	if refusal := v.spendMana(catalog.researchCost(troop, next)); refusal != vOK {
		return refusal
	}
	if seconds := catalog.researchTime(next); seconds > 0 {
		entry.DoneMs = nowMs + seconds*1000
	} else {
		entry.Level = next
	}
	research[troopID] = entry
	return vOK
}

func finishResearch(v *village, research map[string]*researchEntry, troopID string, nowMs int64) vErr {
	settleResearch(research, nowMs)
	entry := research[troopID]
	if entry == nil || entry.DoneMs == 0 {
		return vNotUpgrading
	}
	cost := finishCost(entry.DoneMs - nowMs)
	if v.crystals < cost {
		return vFunds
	}
	v.crystals -= cost
	entry.Level++
	entry.DoneMs = 0
	return vOK
}

func loadResearch(ctx context.Context, tx *sql.Tx, userID string) (map[string]*researchEntry, error) {
	rows, err := tx.QueryContext(ctx, `SELECT troop_id, level, done_ms FROM sd_research WHERE user_id = $1`, userID)
	if err != nil {
		return nil, err
	}
	defer rows.Close()
	research := map[string]*researchEntry{}
	for rows.Next() {
		var id string
		entry := &researchEntry{}
		if err := rows.Scan(&id, &entry.Level, &entry.DoneMs); err != nil {
			return nil, err
		}
		research[id] = entry
	}
	return research, rows.Err()
}

func storeResearch(ctx context.Context, tx *sql.Tx, userID, troopID string, entry *researchEntry) error {
	_, err := tx.ExecContext(ctx, `INSERT INTO sd_research (user_id, troop_id, level, done_ms) VALUES ($1, $2, $3, $4)
		ON CONFLICT (user_id, troop_id) DO UPDATE SET level = EXCLUDED.level, done_ms = EXCLUDED.done_ms`, userID, troopID, entry.Level, entry.DoneMs)
	return err
}

// settleResearchSQL finishes due studies under the player lock, like settleUpgrades.
func settleResearchSQL(ctx context.Context, tx *sql.Tx, userID string) error {
	_, err := tx.ExecContext(ctx, `UPDATE sd_research SET level = level + 1, done_ms = 0 WHERE user_id = $1 AND done_ms > 0 AND done_ms <= $2`, userID, nowMillis())
	return err
}

type researchView struct {
	Level       int   `json:"level"`
	DoneUtcTicks int64 `json:"doneUtcTicks"`
}

func researchViews(research map[string]*researchEntry) map[string]researchView {
	views := map[string]researchView{}
	for id, entry := range research {
		view := researchView{Level: entry.Level}
		if entry.DoneMs > 0 {
			view.DoneUtcTicks = ticksFromMillis(entry.DoneMs)
		}
		views[id] = view
	}
	return views
}

func (s *Server) rpcResearchStart(ctx context.Context, logger runtime.Logger, db *sql.DB, nk runtime.NakamaModule, payload string) (string, error) {
	return s.researchOp(ctx, db, payload, "research_start", func(v *village, research map[string]*researchEntry, troopID string, now int64) vErr {
		return startResearch(v, s.troops, research, troopID, now)
	})
}

func (s *Server) rpcResearchFinish(ctx context.Context, logger runtime.Logger, db *sql.DB, nk runtime.NakamaModule, payload string) (string, error) {
	return s.researchOp(ctx, db, payload, "research_finish", func(v *village, research map[string]*researchEntry, troopID string, now int64) vErr {
		return finishResearch(v, research, troopID, now)
	})
}

func (s *Server) researchOp(ctx context.Context, db *sql.DB, payload, kind string, apply func(*village, map[string]*researchEntry, string, int64) vErr) (string, error) {
	userID, err := userFrom(ctx)
	if err != nil {
		return "", err
	}
	var request struct {
		Key     string `json:"key"`
		TroopID string `json:"troopId"`
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
	var output string
	err = withTx(ctx, db, func(tx *sql.Tx) error {
		crystals, err := s.lockPlayer(ctx, tx, userID)
		if err != nil {
			return err
		}
		if stored, found, err := replayed(ctx, tx, userID, request.Key, kind, request.TroopID); err != nil {
			return err
		} else if found {
			output = stored
			return nil
		}
		v, err := s.loadVillage(ctx, tx, userID, crystals)
		if err != nil {
			return err
		}
		research, err := loadResearch(ctx, tx, userID)
		if err != nil {
			return err
		}
		beforeGold := v.gold
		if refusal := apply(v, research, request.TroopID, nowMillis()); refusal != vOK {
			return gameError(string(refusal), codePrecondition)
		}
		if entry := research[request.TroopID]; entry != nil {
			if err := storeResearch(ctx, tx, userID, request.TroopID, entry); err != nil {
				return err
			}
		}
		if err := persistVillage(ctx, tx, userID, v, beforeGold, crystals, kind, request.Key); err != nil {
			return err
		}
		state, err := loadState(ctx, tx, userID, v.crystals)
		if err != nil {
			return err
		}
		if output, err = marshal(state); err != nil {
			return err
		}
		return storeResponse(ctx, tx, userID, request.Key, kind, request.TroopID, output)
	})
	if err != nil {
		return "", err
	}
	return output, nil
}

var errNoResearch = errors.New("research config missing")
