package main

import (
	"context"
	"database/sql"
	"errors"
	"time"

	"github.com/heroiclabs/nakama-common/runtime"
)

// Hero gear (ADR-044): Weapon raises attack, Armor HP and Charm defense, by a percentage per level. Gear
// is upgraded with gold and Mana, a level at a time, and a higher gear level needs a stronger hero. The
// server applies it in every battle, so the client's numbers always come from here. Mirrors HeroMath.WithGear.

const (
	gearWeapon = 0
	gearArmor  = 1
	gearCharm  = 2
)

var (
	errBadSlot2   = gameError("BAD_GEAR_SLOT", codeInvalid)
	errGearMax    = gameError("GEAR_AT_MAX", codePrecondition)
	errGearLocked = gameError("HERO_LEVEL_TOO_LOW_FOR_GEAR", codePrecondition)
)

type gearConfig struct {
	Slots                  []string `json:"slots"`
	MaxLevel               int      `json:"maxLevel"`
	PercentPerLevel        int64    `json:"percentPerLevel"`
	ManaPerLevel           int64    `json:"manaPerLevel"`
	GoldPerLevel           int64    `json:"goldPerLevel"`
	HeroLevelsPerGearLevel int      `json:"heroLevelsPerGearLevel"`
}

func (c gearConfig) valid() bool {
	return len(c.Slots) == 3 && c.MaxLevel >= 1 && c.MaxLevel <= 50 && c.PercentPerLevel >= 0 && c.ManaPerLevel >= 0 && c.GoldPerLevel >= 0 && c.HeroLevelsPerGearLevel >= 0
}

// withGear applies gear to battle stats: Weapon to attack, Armor to HP, Charm to defense.
func (c *HeroCatalog) withGear(hp, attack, defense int64, gear [3]int) (int64, int64, int64) {
	p := c.Gear.PercentPerLevel
	return hp * (100 + p*int64(gear[gearArmor])) / 100, attack * (100 + p*int64(gear[gearWeapon])) / 100, defense * (100 + p*int64(gear[gearCharm])) / 100
}

// gearCost is the gold and Mana to reach a gear level, and the cumulative hero level it needs.
func (c *HeroCatalog) gearCost(level int) (gold, mana int64, heroLevel int) {
	return c.Gear.GoldPerLevel * int64(level), c.Gear.ManaPerLevel * int64(level) * int64(level), (level-1)*c.Gear.HeroLevelsPerGearLevel + 1
}

func (s *Server) rpcGearUpgrade(ctx context.Context, logger runtime.Logger, db *sql.DB, nk runtime.NakamaModule, payload string) (string, error) {
	userID, err := userFrom(ctx)
	if err != nil {
		return "", err
	}
	var request struct {
		Key        string `json:"key"`
		InstanceID string `json:"instanceId"`
		Slot       int    `json:"slot"`
	}
	if err := decodeStrict(payload, &request); err != nil {
		return "", err
	}
	if !keyPattern.MatchString(request.Key) {
		return "", errBadKey
	}
	if request.Slot < 0 || request.Slot > 2 {
		return "", errBadSlot2
	}
	if !uuidPattern.MatchString(request.InstanceID) {
		return "", errHeroNotFound
	}
	if !s.limiter.Allow(userID, time.Now()) {
		return "", errRateLimited
	}
	columns := [3]string{"gear_weapon", "gear_armor", "gear_charm"}
	requestHash := hashRequest(request.InstanceID, itoa(request.Slot))
	var output string
	err = withTx(ctx, db, func(tx *sql.Tx) error {
		crystals, err := s.lockPlayer(ctx, tx, userID)
		if err != nil {
			return err
		}
		if stored, found, err := replayed(ctx, tx, userID, request.Key, "gear_upgrade", requestHash); err != nil {
			return err
		} else if found {
			output = stored
			return nil
		}
		var evolution, level, current int
		err = tx.QueryRowContext(ctx, `SELECT evolution, level, `+columns[request.Slot]+` FROM sd_hero WHERE instance_id = $1 AND user_id = $2 FOR UPDATE`,
			request.InstanceID, userID).Scan(&evolution, &level, &current)
		if errors.Is(err, sql.ErrNoRows) {
			return errHeroNotFound
		}
		if err != nil {
			return err
		}
		next := current + 1
		if next > s.catalog.Gear.MaxLevel {
			return errGearMax
		}
		gold, mana, heroLevel := s.catalog.gearCost(next)
		if 200*evolution+level < heroLevel {
			return errGearLocked
		}
		v, err := s.loadVillage(ctx, tx, userID, crystals)
		if err != nil {
			return err
		}
		beforeGold := v.gold
		if v.gold < gold {
			return gameError(string(vFunds), codePrecondition)
		}
		if refusal := v.spendMana(mana); refusal != vOK {
			return gameError(string(refusal), codePrecondition)
		}
		v.gold -= gold
		if _, err := tx.ExecContext(ctx, `UPDATE sd_hero SET `+columns[request.Slot]+` = $1 WHERE instance_id = $2`, next, request.InstanceID); err != nil {
			return err
		}
		if err := persistVillage(ctx, tx, userID, v, beforeGold, crystals, "gear_upgrade", request.Key); err != nil {
			return err
		}
		state, err := loadState(ctx, tx, userID, v.crystals)
		if err != nil {
			return err
		}
		if output, err = marshal(state); err != nil {
			return err
		}
		return storeResponse(ctx, tx, userID, request.Key, "gear_upgrade", requestHash, output)
	})
	if err != nil {
		return "", err
	}
	return output, nil
}
