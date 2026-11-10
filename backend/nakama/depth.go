package main

import (
	"context"
	"database/sql"
	"errors"
	"time"

	"github.com/heroiclabs/nakama-common/runtime"
)

// Hero depth (ADR-053): technique ranks and talents.
//
// A technique rank makes the hero's special stronger. Ranks are bought one at a time with Technique Manuals, and a
// higher rank needs a stronger hero. A talent is one passive the hero holds; the server rolls it for one Talent
// Token. Every roll is an even, independent draw of one talent and one level from crypto/rand: nothing is
// guaranteed, nothing is remembered between rolls, and a roll can give back what the hero already had.
//
// The server applies both in every battle, so the client's numbers always come from here. Mirrors
// TechniqueSettings, TalentSettings and HeroMath.WithTalent.

const (
	talentHp      = "hp"
	talentAttack  = "attack"
	talentDefense = "defense"
	talentCharge  = "charge"
	talentSiege   = "siege"
	talentSpeed   = "speed"
)

var (
	errRankMax      = gameError("TECHNIQUE_AT_MAX", codePrecondition)
	errRankLocked   = gameError("HERO_LEVEL_TOO_LOW_FOR_RANK", codePrecondition)
	errNoManuals    = gameError("NOT_ENOUGH_MANUALS", codePrecondition)
	errNoTalentItem = gameError("NO_TALENT_TOKEN", codePrecondition)
)

type techniqueConfig struct {
	MaxRank           int    `json:"maxRank"`
	PowerPerRank      int    `json:"powerPerRank"`
	Item              string `json:"item"`
	ManualsPerRank    int    `json:"manualsPerRank"`
	HeroLevelsPerRank int    `json:"heroLevelsPerRank"`
	// Manuals a win pays: one dungeon win inside the daily XP limit, and each star of a raid on a rival.
	PerDungeonWin int `json:"perDungeonWin"`
	PerRaidStar   int `json:"perRaidStar"`
}

func (c techniqueConfig) valid() bool {
	return c.MaxRank >= 1 && c.MaxRank <= 20 && c.PowerPerRank >= 0 && c.Item != "" && c.ManualsPerRank >= 0 &&
		c.HeroLevelsPerRank >= 0 && c.PerDungeonWin >= 0 && c.PerRaidStar >= 0
}

type talentDefinition struct {
	ID          string `json:"id"`
	DisplayName string `json:"displayName"`
	Description string `json:"description"`
	Kind        string `json:"kind"`
	PerLevel    int    `json:"perLevel"`
}

type talentConfig struct {
	MaxLevel int    `json:"maxLevel"`
	Item     string `json:"item"`
	// RaidStars is how many stars a raid on a rival needs to pay one Talent Token; 0 pays none.
	RaidStars int                `json:"raidStars"`
	List      []talentDefinition `json:"list"`
}

func (c talentConfig) valid() bool {
	if c.MaxLevel < 1 || c.MaxLevel > 10 || c.Item == "" || len(c.List) == 0 || c.RaidStars < 0 || c.RaidStars > 3 {
		return false
	}
	seen := map[string]bool{}
	for _, t := range c.List {
		switch t.Kind {
		case talentHp, talentAttack, talentDefense, talentCharge, talentSiege, talentSpeed:
		default:
			return false
		}
		if t.ID == "" || t.PerLevel < 1 || seen[t.ID] {
			return false
		}
		seen[t.ID] = true
	}
	return true
}

func (c talentConfig) find(id string) (talentDefinition, bool) {
	for _, t := range c.List {
		if t.ID == id && id != "" {
			return t, true
		}
	}
	return talentDefinition{}, false
}

// heroDepth is a hero's technique rank and talent as stored.
type heroDepth struct {
	Rank   int
	Talent string
	Level  int
}

// battleEdge is what a talent changes in the battle itself, beyond the three stats.
type battleEdge struct {
	StartCharge  int
	SiegePercent int
	SpeedPercent int
}

// rankCost is the manuals to reach a rank from the one below, and the cumulative hero level it needs.
func (c *HeroCatalog) rankCost(rank int) (manuals int64, heroLevel int) {
	return int64(c.Technique.ManualsPerRank) * int64(rank-1), (rank-1)*c.Technique.HeroLevelsPerRank + 1
}

// rankPower is the special power at a rank, on top of the rarity's own.
func (c *HeroCatalog) rankPower(rarity string, rank int) int {
	if rank < 1 {
		rank = 1
	}
	return powerFor(rarity) + c.Technique.PowerPerRank*(rank-1)
}

// withDepth applies a hero's rank and talent: stat talents to the stats, the rest to the battle.
func (c *HeroCatalog) withDepth(rarity string, hp, attack, defense int64, d heroDepth) (int64, int64, int64, int, battleEdge) {
	power := c.rankPower(rarity, d.Rank)
	edge := battleEdge{}
	talent, ok := c.Talents.find(d.Talent)
	if !ok || d.Level <= 0 {
		return hp, attack, defense, power, edge
	}
	amount := talent.PerLevel * d.Level
	percent := int64(100 + amount)
	switch talent.Kind {
	case talentHp:
		hp = hp * percent / 100
	case talentAttack:
		attack = attack * percent / 100
	case talentDefense:
		defense = defense * percent / 100
	case talentCharge:
		edge.StartCharge = minInt(amount, chargeFull)
	case talentSiege:
		edge.SiegePercent = amount
	case talentSpeed:
		edge.SpeedPercent = amount
	}
	return hp, attack, defense, power, edge
}

// spendItems takes a number of one item, or refuses when the player has fewer.
func spendItems(ctx context.Context, tx *sql.Tx, userID, itemID string, count int64, refusal error, reason, ref string) error {
	if count <= 0 {
		return nil
	}
	spent, err := tx.ExecContext(ctx, `UPDATE sd_item SET count = count - $3 WHERE user_id = $1 AND item_id = $2 AND count >= $3`, userID, itemID, count)
	if err != nil {
		return err
	}
	if rows, err := spent.RowsAffected(); err != nil {
		return err
	} else if rows != 1 {
		return refusal
	}
	return addItemLedger(ctx, tx, userID, itemID, -count, reason, ref)
}

// grantItems adds a number of one item and records why.
func grantItems(ctx context.Context, tx *sql.Tx, userID, itemID string, count int64, reason, ref string) error {
	if count <= 0 {
		return nil
	}
	if _, err := tx.ExecContext(ctx, `INSERT INTO sd_item (user_id, item_id, count) VALUES ($1, $2, $3)
		ON CONFLICT (user_id, item_id) DO UPDATE SET count = sd_item.count + EXCLUDED.count`, userID, itemID, count); err != nil {
		return err
	}
	return addItemLedger(ctx, tx, userID, itemID, count, reason, ref)
}

// depthRewards is what a win pays in manuals and tokens: a rival raid by its stars, a dungeon once per paid win.
func (c *HeroCatalog) depthRewards(rival bool, stars int, paidDungeonWin bool) map[string]int64 {
	rewards := map[string]int64{}
	if stars <= 0 {
		return rewards
	}
	if rival {
		if n := int64(c.Technique.PerRaidStar) * int64(stars); n > 0 {
			rewards[c.Technique.Item] = n
		}
		if c.Talents.RaidStars > 0 && stars >= c.Talents.RaidStars {
			rewards[c.Talents.Item]++
		}
	} else if paidDungeonWin && c.Technique.PerDungeonWin > 0 {
		rewards[c.Technique.Item] = int64(c.Technique.PerDungeonWin)
	}
	return rewards
}

func (s *Server) rpcTechniqueRankUp(ctx context.Context, logger runtime.Logger, db *sql.DB, nk runtime.NakamaModule, payload string) (string, error) {
	userID, err := userFrom(ctx)
	if err != nil {
		return "", err
	}
	var request struct {
		Key        string `json:"key"`
		InstanceID string `json:"instanceId"`
	}
	if err := decodeStrict(payload, &request); err != nil {
		return "", err
	}
	if !keyPattern.MatchString(request.Key) {
		return "", errBadKey
	}
	if !uuidPattern.MatchString(request.InstanceID) {
		return "", errHeroNotFound
	}
	if !s.limiter.Allow(userID, time.Now()) {
		return "", errRateLimited
	}
	requestHash := hashRequest(request.InstanceID)
	var output string
	err = withTx(ctx, db, func(tx *sql.Tx) error {
		crystals, err := s.lockPlayer(ctx, tx, userID)
		if err != nil {
			return err
		}
		if stored, found, err := replayed(ctx, tx, userID, request.Key, "technique_rank", requestHash); err != nil {
			return err
		} else if found {
			output = stored
			return nil
		}
		var evolution, level, current int
		err = tx.QueryRowContext(ctx, `SELECT evolution, level, technique_rank FROM sd_hero WHERE instance_id = $1 AND user_id = $2 FOR UPDATE`,
			request.InstanceID, userID).Scan(&evolution, &level, &current)
		if errors.Is(err, sql.ErrNoRows) {
			return errHeroNotFound
		}
		if err != nil {
			return err
		}
		next := current + 1
		if next > s.catalog.Technique.MaxRank {
			return errRankMax
		}
		manuals, heroLevel := s.catalog.rankCost(next)
		if 200*evolution+level < heroLevel {
			return errRankLocked
		}
		if err := spendItems(ctx, tx, userID, s.catalog.Technique.Item, manuals, errNoManuals, "technique_rank", request.Key); err != nil {
			return err
		}
		if _, err := tx.ExecContext(ctx, `UPDATE sd_hero SET technique_rank = $1 WHERE instance_id = $2`, next, request.InstanceID); err != nil {
			return err
		}
		state, err := loadState(ctx, tx, userID, crystals)
		if err != nil {
			return err
		}
		if output, err = marshal(state); err != nil {
			return err
		}
		return storeResponse(ctx, tx, userID, request.Key, "technique_rank", requestHash, output)
	})
	if err != nil {
		return "", err
	}
	return output, nil
}

// rollTalent makes one independent draw: an even pick of one talent, then an even pick of one level.
func (s *Server) rollTalent() (string, int, error) {
	t, err := s.randomIndex(len(s.catalog.Talents.List))
	if err != nil {
		return "", 0, err
	}
	l, err := s.randomIndex(s.catalog.Talents.MaxLevel)
	if err != nil {
		return "", 0, err
	}
	return s.catalog.Talents.List[t].ID, l + 1, nil
}

func (s *Server) rpcTalentRoll(ctx context.Context, logger runtime.Logger, db *sql.DB, nk runtime.NakamaModule, payload string) (string, error) {
	userID, err := userFrom(ctx)
	if err != nil {
		return "", err
	}
	var request struct {
		Key        string `json:"key"`
		InstanceID string `json:"instanceId"`
	}
	if err := decodeStrict(payload, &request); err != nil {
		return "", err
	}
	if !keyPattern.MatchString(request.Key) {
		return "", errBadKey
	}
	if !uuidPattern.MatchString(request.InstanceID) {
		return "", errHeroNotFound
	}
	if !s.limiter.Allow(userID, time.Now()) {
		return "", errRateLimited
	}
	requestHash := hashRequest(request.InstanceID)
	var output string
	err = withTx(ctx, db, func(tx *sql.Tx) error {
		crystals, err := s.lockPlayer(ctx, tx, userID)
		if err != nil {
			return err
		}
		// A retried key returns the roll it already made; it never rolls again.
		if stored, found, err := replayed(ctx, tx, userID, request.Key, "talent_roll", requestHash); err != nil {
			return err
		} else if found {
			output = stored
			return nil
		}
		var owned int
		err = tx.QueryRowContext(ctx, `SELECT 1 FROM sd_hero WHERE instance_id = $1 AND user_id = $2 FOR UPDATE`, request.InstanceID, userID).Scan(&owned)
		if errors.Is(err, sql.ErrNoRows) {
			return errHeroNotFound
		}
		if err != nil {
			return err
		}
		if err := spendItems(ctx, tx, userID, s.catalog.Talents.Item, 1, errNoTalentItem, "talent_roll", request.Key); err != nil {
			return err
		}
		talent, level, err := s.rollTalent()
		if err != nil {
			return err
		}
		if _, err := tx.ExecContext(ctx, `UPDATE sd_hero SET talent_id = $1, talent_level = $2 WHERE instance_id = $3`, talent, level, request.InstanceID); err != nil {
			return err
		}
		state, err := loadState(ctx, tx, userID, crystals)
		if err != nil {
			return err
		}
		if output, err = marshal(state); err != nil {
			return err
		}
		return storeResponse(ctx, tx, userID, request.Key, "talent_roll", requestHash, output)
	})
	if err != nil {
		return "", err
	}
	return output, nil
}
