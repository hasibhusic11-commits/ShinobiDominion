package main

import (
	"context"
	"database/sql"
	"errors"
	"time"

	"github.com/heroiclabs/nakama-common/runtime"
)

var errAtMaxLevel = gameError("AT_MAX_LEVEL", codePrecondition)

const heroMaxLevel = 200

// vAtMaxLevel matches HeroError.AtMaxLevel, so the shared scenarios use one name.
const vAtMaxLevel vErr = "AtMaxLevel"

// manaToLevelUp is the Mana price of going from this level to the next. It mirrors
// HeroRoster.ManaToLevelUp. Test values from heroes.json, not approved (ADR-038).
func (c *HeroCatalog) manaToLevelUp(level int) int64 {
	return c.ManaBase + c.ManaStep*int64(level-1)
}

// trainLevel buys exactly one level with Mana. Experience already earned toward the next level
// is kept, so paying never wipes progress. Pure, so the shared scenarios can test it.
func (c *HeroCatalog) trainLevel(v *village, level int, experience int64) (int, int64, vErr) {
	if level >= heroMaxLevel {
		return level, experience, vAtMaxLevel
	}
	if refusal := v.spendMana(c.manaToLevelUp(level)); refusal != vOK {
		return level, experience, refusal
	}
	level, experience, _ = c.applyXP(level+1, experience, 0)
	return level, experience, vOK
}

type heroTrainRequest struct {
	Key        string `json:"key"`
	InstanceID string `json:"instanceId"`
}

// rpcHeroTrain spends Mana to raise one hero by one level, at the Hero Altar. The price comes
// from the server's catalogue; the client only names the hero.
func (s *Server) rpcHeroTrain(ctx context.Context, logger runtime.Logger, db *sql.DB, nk runtime.NakamaModule, payload string) (string, error) {
	userID, err := userFrom(ctx)
	if err != nil {
		return "", err
	}
	var request heroTrainRequest
	if err := decodeStrict(payload, &request); err != nil {
		return "", err
	}
	if !keyPattern.MatchString(request.Key) {
		return "", errBadKey
	}
	if !uuidPattern.MatchString(request.InstanceID) {
		return "", errBadRequest
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
		if stored, found, err := replayed(ctx, tx, userID, request.Key, "hero_train", requestHash); err != nil {
			return err
		} else if found {
			output = stored
			return nil
		}
		var level int
		var experience int64
		err = tx.QueryRowContext(ctx, `SELECT level, experience FROM sd_hero WHERE instance_id = $1 AND user_id = $2 FOR UPDATE`, request.InstanceID, userID).Scan(&level, &experience)
		if errors.Is(err, sql.ErrNoRows) {
			return errHeroNotFound
		}
		if err != nil {
			return err
		}
		v, err := s.loadVillage(ctx, tx, userID, crystals)
		if err != nil {
			return err
		}
		level, experience, refusal := s.catalog.trainLevel(v, level, experience)
		if refusal == vAtMaxLevel {
			return errAtMaxLevel
		}
		if refusal != vOK {
			return gameError(string(refusal), codePrecondition)
		}
		if _, err := tx.ExecContext(ctx, `UPDATE sd_hero SET level = $1, experience = $2 WHERE instance_id = $3 AND user_id = $4`, level, experience, request.InstanceID, userID); err != nil {
			return err
		}
		if err := persistVillage(ctx, tx, userID, v, v.gold, crystals, "hero_train", request.Key); err != nil {
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
		return storeResponse(ctx, tx, userID, request.Key, "hero_train", requestHash, output)
	})
	if err != nil {
		return "", err
	}
	return output, nil
}
