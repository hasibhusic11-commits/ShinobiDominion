package main

import (
	"context"
	"database/sql"
	"encoding/json"
	"errors"
	"fmt"
	"time"

	"github.com/heroiclabs/nakama-common/runtime"
)

// Raids on real players take real loot (ADR-049, the owner's choice on 2026-11-09, matching the genre).
// When a raid begins, the loot on offer is a share of the gold and Mana the rival has; the attacker wins that share
// times the damage done, and the rival loses exactly what the attacker gains. A rival hit hard enough is shielded
// for a while, and may take revenge on the attacker once, even through the attacker's shield.
const (
	raidLootPercent  = 20
	raidShieldDamage = 30
	raidShieldHours  = 12
)

var errRevengeUsed = gameError("REVENGE_USED", codePrecondition)

// beginRivalRaid snapshots the rival's village, guards and loot, ends the attacker's own shield, and records the raid.
func (s *Server) beginRivalRaid(ctx context.Context, tx *sql.Tx, userID, rivalID string, team []raidTeamMember) (raidStartResponse, error) {
	buildings, defenders, dojoLevel, err := s.rivalSnapshot(ctx, tx, rivalID)
	if err != nil {
		return raidStartResponse{}, err
	}
	rival, err := s.loadVillage(ctx, tx, rivalID, 0)
	if err != nil {
		return raidStartResponse{}, err
	}
	lootGold := rival.gold * raidLootPercent / 100
	lootMana := rival.mana * raidLootPercent / 100
	// Attacking another player ends your own shield, as in the genre.
	if _, err := tx.ExecContext(ctx, `UPDATE sd_player SET shield_until = NULL WHERE user_id = $1`, userID); err != nil {
		return raidStartResponse{}, err
	}
	attackID, err := insertRaid(ctx, tx, userID, rivalRaidID, 1, 0, 0, &rivalID, buildings, team, defenders)
	if err != nil {
		return raidStartResponse{}, err
	}
	if _, err := tx.ExecContext(ctx, `UPDATE sd_raid SET loot_gold = $1, loot_mana = $2 WHERE attack_id = $3`, lootGold, lootMana, attackID); err != nil {
		return raidStartResponse{}, err
	}
	summary := raidSummary{
		ID:          rivalRaidID,
		DisplayName: playerName(ctx, tx, rivalID),
		Description: fmt.Sprintf("A real player's village, with a level %d Town Hall and %d heroes on guard.", dojoLevel, len(defenders)),
		AvailableGold: lootGold,
		AvailableMana: lootMana,
	}
	return raidStartResponse{AttackID: attackID, Raid: summary, Buildings: buildings, Team: team, Defenders: defenders}, nil
}

// takeLoot pays the attacker its share of the loot on offer and takes the same from the rival, then shields the rival
// when the damage was high enough. The share is limited by what the rival still has and by the attacker's storage.
func (s *Server) takeLoot(ctx context.Context, tx *sql.Tx, attacker *village, rivalID, attackID string, damage int) (int64, int64, error) {
	var lootGold, lootMana int64
	if err := tx.QueryRowContext(ctx, `SELECT loot_gold, loot_mana FROM sd_raid WHERE attack_id = $1`, attackID).Scan(&lootGold, &lootMana); err != nil {
		return 0, 0, err
	}
	var rivalCrystals int64
	if err := tx.QueryRowContext(ctx, `SELECT crystals FROM sd_player WHERE user_id = $1 FOR UPDATE`, rivalID).Scan(&rivalCrystals); err != nil {
		return 0, 0, err
	}
	rival, err := s.loadVillage(ctx, tx, rivalID, rivalCrystals)
	if err != nil {
		return 0, 0, err
	}
	if damage < 0 {
		damage = 0
	}
	if damage > 100 {
		damage = 100
	}
	beforeGold := rival.gold
	gold := minInt64(lootGold*int64(damage)/100, rival.gold)
	gold = payGold(attacker, gold)
	rival.gold -= gold
	mana := minInt64(lootMana*int64(damage)/100, rival.mana)
	mana = payMana(attacker, mana)
	rival.mana -= mana
	if err := persistVillage(ctx, tx, rivalID, rival, beforeGold, rivalCrystals, "raided", attackID); err != nil {
		return 0, 0, err
	}
	if damage >= raidShieldDamage {
		if _, err := tx.ExecContext(ctx, `UPDATE sd_player SET shield_until = GREATEST(COALESCE(shield_until, now()), now() + make_interval(hours => $2))
			WHERE user_id = $1`, rivalID, raidShieldHours); err != nil {
			return 0, 0, err
		}
	}
	return gold, mana, nil
}

func minInt64(a, b int64) int64 {
	if a < b {
		return a
	}
	return b
}

type defenseRecord struct {
	AttackID       string `json:"attackId"`
	AttackerName   string `json:"attackerName"`
	Stars          int    `json:"stars"`
	DamagePercent  int    `json:"damagePercent"`
	DefendersAlive int    `json:"defendersAlive"`
	DefendersTotal int    `json:"defendersTotal"`
	GoldLost       int64  `json:"goldLost"`
	ManaLost       int64  `json:"manaLost"`
	CanRevenge     bool   `json:"canRevenge"`
	AtMs           int64  `json:"atMs"`
}

// rpcDefenseLog lists the latest raids on this player's village: who attacked, how it went, what was lost, and
// whether revenge is still open.
func (s *Server) rpcDefenseLog(ctx context.Context, logger runtime.Logger, db *sql.DB, nk runtime.NakamaModule, payload string) (string, error) {
	userID, err := userFrom(ctx)
	if err != nil {
		return "", err
	}
	var output string
	err = withTx(ctx, db, func(tx *sql.Tx) error {
		rows, err := tx.QueryContext(ctx, `SELECT attack_id::text, user_id::text, stars, damage_percent, COALESCE(defenders_alive, 0), defenders,
			(EXTRACT(EPOCH FROM resolved_at) * 1000)::bigint, gold_earned, mana_earned, revenged
			FROM sd_raid WHERE opponent_id = $1 AND status = 'resolved' ORDER BY resolved_at DESC LIMIT 20`, userID)
		if err != nil {
			return err
		}
		records := []defenseRecord{}
		attackers := []string{}
		for rows.Next() {
			var record defenseRecord
			var attacker, defendersText string
			var revenged bool
			if err := rows.Scan(&record.AttackID, &attacker, &record.Stars, &record.DamagePercent, &record.DefendersAlive, &defendersText, &record.AtMs, &record.GoldLost, &record.ManaLost, &revenged); err != nil {
				rows.Close()
				return err
			}
			var defenders []raidDefender
			if err := json.Unmarshal([]byte(defendersText), &defenders); err != nil {
				rows.Close()
				return err
			}
			record.DefendersTotal = len(defenders)
			record.CanRevenge = !revenged && attacker != userID
			records = append(records, record)
			attackers = append(attackers, attacker)
		}
		rows.Close()
		if err := rows.Err(); err != nil {
			return err
		}
		for i := range records {
			records[i].AttackerName = playerName(ctx, tx, attackers[i])
		}
		output, err = marshal(map[string]interface{}{"defenses": records})
		return err
	})
	return output, err
}

// rpcRevenge starts a raid on whoever attacked this player, once per attack, even through the attacker's shield.
func (s *Server) rpcRevenge(ctx context.Context, logger runtime.Logger, db *sql.DB, nk runtime.NakamaModule, payload string) (string, error) {
	userID, err := userFrom(ctx)
	if err != nil {
		return "", err
	}
	var request struct {
		Key      string `json:"key"`
		AttackID string `json:"attackId"`
	}
	if err := decodeStrict(payload, &request); err != nil {
		return "", err
	}
	if !keyPattern.MatchString(request.Key) {
		return "", errBadKey
	}
	if !uuidPattern.MatchString(request.AttackID) {
		return "", errAttackNotFound
	}
	if !s.limiter.Allow(userID, time.Now()) {
		return "", errRateLimited
	}
	requestHash := hashRequest(request.AttackID)
	var output string
	err = withTx(ctx, db, func(tx *sql.Tx) error {
		if _, err := s.lockPlayer(ctx, tx, userID); err != nil {
			return err
		}
		if stored, found, err := replayed(ctx, tx, userID, request.Key, "revenge", requestHash); err != nil {
			return err
		} else if found {
			output = stored
			return nil
		}
		var attacker, status string
		var revenged bool
		err := tx.QueryRowContext(ctx, `SELECT user_id::text, status, revenged FROM sd_raid WHERE attack_id = $1 AND opponent_id = $2 FOR UPDATE`,
			request.AttackID, userID).Scan(&attacker, &status, &revenged)
		if errors.Is(err, sql.ErrNoRows) || (err == nil && (status != "resolved" || attacker == userID)) {
			return errAttackNotFound
		}
		if err != nil {
			return err
		}
		if revenged {
			return errRevengeUsed
		}
		team, err := s.loadRaidTeam(ctx, tx, userID)
		if err != nil {
			return err
		}
		if len(team) == 0 {
			return errNoTeam
		}
		if _, err := tx.ExecContext(ctx, `UPDATE sd_raid SET revenged = TRUE WHERE attack_id = $1`, request.AttackID); err != nil {
			return err
		}
		response, err := s.beginRivalRaid(ctx, tx, userID, attacker, team)
		if err != nil {
			return err
		}
		if output, err = marshal(response); err != nil {
			return err
		}
		return storeResponse(ctx, tx, userID, request.Key, "revenge", requestHash, output)
	})
	if err != nil {
		return "", err
	}
	return output, nil
}
