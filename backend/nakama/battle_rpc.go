package main

import (
	"context"
	"database/sql"
	"encoding/json"
	"errors"
	"time"

	"github.com/heroiclabs/nakama-common/runtime"
)

// A raid has to be fought soon after it starts, from the team it started with.
const raidPrepareLimit = 15 * time.Minute

var (
	errRaidNotFound   = gameError("RAID_NOT_FOUND", codeNotFound)
	errAttackNotFound = gameError("ATTACK_NOT_FOUND", codeNotFound)
	errAttackOver     = gameError("ATTACK_ALREADY_RESOLVED", codePrecondition)
	errAttackExpired  = gameError("ATTACK_EXPIRED", codePrecondition)
	errNoTeam         = gameError("NO_TEAM", codePrecondition)
	errHeroNotInTeam  = gameError("HERO_NOT_IN_TEAM", codePrecondition)
)

type raidTeamMember struct {
	InstanceID     string `json:"instanceId"`
	ArchetypeID    string `json:"archetypeId"`
	EvolutionIndex int    `json:"evolutionIndex"`
	Level          int    `json:"level"`
	Role           string `json:"role"`
	Hp             int64  `json:"hp"`
	Attack         int64  `json:"attack"`
	Defense        int64  `json:"defense"`
	Speed          int    `json:"speed"`
}

type raidSummary struct {
	ID          string `json:"id"`
	DisplayName string `json:"displayName"`
	Description string `json:"description"`
	GoldPerStar int64  `json:"goldPerStar"`
	BestStars   int    `json:"bestStars"`
}

type raidStartResponse struct {
	AttackID  string                `json:"attackId"`
	Raid      raidSummary           `json:"raid"`
	Buildings []battleBuildingInput `json:"buildings"`
	Team      []raidTeamMember      `json:"team"`
}

type deployPlacement struct {
	InstanceID string `json:"instanceId"`
	X          int    `json:"x"`
	Z          int    `json:"z"`
}

type raidDeployResponse struct {
	AttackID   string        `json:"attackId"`
	Result     *battleResult `json:"result"`
	GoldEarned int64         `json:"goldEarned"`
	BestStars  int           `json:"bestStars"`
	State      *playerState  `json:"state"`
}

func bestStarsFor(ctx context.Context, tx *sql.Tx, userID, raidID string) (int, error) {
	var stars int
	err := tx.QueryRowContext(ctx, `SELECT best_stars FROM sd_raid_progress WHERE user_id = $1 AND raid_id = $2`, userID, raidID).Scan(&stars)
	if errors.Is(err, sql.ErrNoRows) {
		return 0, nil
	}
	return stars, err
}

func (s *Server) rpcRaidList(ctx context.Context, logger runtime.Logger, db *sql.DB, nk runtime.NakamaModule, payload string) (string, error) {
	userID, err := userFrom(ctx)
	if err != nil {
		return "", err
	}
	var output string
	err = withTx(ctx, db, func(tx *sql.Tx) error {
		if _, err := s.lockPlayer(ctx, tx, userID); err != nil {
			return err
		}
		list := []raidSummary{}
		for _, raid := range s.raids.all {
			best, err := bestStarsFor(ctx, tx, userID, raid.ID)
			if err != nil {
				return err
			}
			list = append(list, raidSummary{raid.ID, raid.DisplayName, raid.Description, raid.GoldPerStar, best})
		}
		output, err = marshal(map[string]interface{}{"raids": list})
		return err
	})
	return output, err
}

func (s *Server) rpcRaidStart(ctx context.Context, logger runtime.Logger, db *sql.DB, nk runtime.NakamaModule, payload string) (string, error) {
	userID, err := userFrom(ctx)
	if err != nil {
		return "", err
	}
	var request struct {
		Key    string `json:"key"`
		RaidID string `json:"raidId"`
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
	raid, ok := s.raids.byID[request.RaidID]
	if !ok {
		return "", errRaidNotFound
	}

	var output string
	err = withTx(ctx, db, func(tx *sql.Tx) error {
		if _, err := s.lockPlayer(ctx, tx, userID); err != nil {
			return err
		}
		if stored, found, err := replayed(ctx, tx, userID, request.Key, "raid_start", request.RaidID); err != nil {
			return err
		} else if found {
			output = stored
			return nil
		}

		team, err := s.loadRaidTeam(ctx, tx, userID)
		if err != nil {
			return err
		}
		if len(team) == 0 {
			return errNoTeam
		}
		best, err := bestStarsFor(ctx, tx, userID, raid.ID)
		if err != nil {
			return err
		}

		// Only one raid at a time. Starting a new one walks away from any unfinished one.
		if _, err := tx.ExecContext(ctx, `UPDATE sd_raid SET status = 'abandoned' WHERE user_id = $1 AND status = 'prepare'`, userID); err != nil {
			return err
		}
		snapshot, err := json.Marshal(raid.expanded)
		if err != nil {
			return err
		}
		teamJSON, err := json.Marshal(team)
		if err != nil {
			return err
		}
		var attackID string
		if err := tx.QueryRowContext(ctx, `INSERT INTO sd_raid (attack_id, user_id, raid_id, raid_version, gold_per_star, snapshot, team, status)
			VALUES (gen_random_uuid(), $1, $2, $3, $4, $5, $6, 'prepare') RETURNING attack_id::text`,
			userID, raid.ID, raid.Version, raid.GoldPerStar, string(snapshot), string(teamJSON)).Scan(&attackID); err != nil {
			return err
		}
		output, err = marshal(raidStartResponse{
			AttackID:  attackID,
			Raid:      raidSummary{raid.ID, raid.DisplayName, raid.Description, raid.GoldPerStar, best},
			Buildings: raid.expanded,
			Team:      team,
		})
		if err != nil {
			return err
		}
		return storeResponse(ctx, tx, userID, request.Key, "raid_start", request.RaidID, output)
	})
	if err != nil {
		return "", err
	}
	return output, nil
}

// loadRaidTeam reads who stands on the player's Hero Bases. Only the server decides their stats.
func (s *Server) loadRaidTeam(ctx context.Context, tx *sql.Tx, userID string) ([]raidTeamMember, error) {
	rows, err := tx.QueryContext(ctx, `SELECT instance_id::text, archetype_id, evolution, level FROM sd_hero WHERE user_id = $1 AND base_slot IS NOT NULL ORDER BY base_slot`, userID)
	if err != nil {
		return nil, err
	}
	defer rows.Close()
	team := []raidTeamMember{}
	for rows.Next() {
		var member raidTeamMember
		if err := rows.Scan(&member.InstanceID, &member.ArchetypeID, &member.EvolutionIndex, &member.Level); err != nil {
			return nil, err
		}
		archetype, ok := s.catalog.byID[member.ArchetypeID]
		if !ok {
			return nil, errHeroNotFound
		}
		role, err := roleNumber(archetype.Role)
		if err != nil {
			return nil, err
		}
		member.Role = archetype.Role
		member.Speed = roleSpeeds[role]
		member.Hp, member.Attack, member.Defense = heroBattleStats(archetype, member.EvolutionIndex, member.Level)
		team = append(team, member)
	}
	return team, rows.Err()
}

func (s *Server) rpcRaidDeploy(ctx context.Context, logger runtime.Logger, db *sql.DB, nk runtime.NakamaModule, payload string) (string, error) {
	userID, err := userFrom(ctx)
	if err != nil {
		return "", err
	}
	// The request can only say where heroes stand. Strict decoding refuses anything else,
	// such as a claimed result or reward.
	var request struct {
		Key        string            `json:"key"`
		AttackID   string            `json:"attackId"`
		Placements []deployPlacement `json:"placements"`
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
	requestHash := hashRequest(placementHash(request.AttackID, request.Placements))

	var output string
	err = withTx(ctx, db, func(tx *sql.Tx) error {
		crystals, err := s.lockPlayer(ctx, tx, userID)
		if err != nil {
			return err
		}
		if stored, found, err := replayed(ctx, tx, userID, request.Key, "raid_deploy", requestHash); err != nil {
			return err
		} else if found {
			output = stored
			return nil
		}

		var raidID, status, snapshotText, teamText string
		var goldPerStar int64
		var created time.Time
		err = tx.QueryRowContext(ctx, `SELECT raid_id, status, snapshot, team, gold_per_star, created_at FROM sd_raid WHERE attack_id = $1 AND user_id = $2 FOR UPDATE`,
			request.AttackID, userID).Scan(&raidID, &status, &snapshotText, &teamText, &goldPerStar, &created)
		if errors.Is(err, sql.ErrNoRows) {
			return errAttackNotFound
		}
		if err != nil {
			return err
		}
		if status != "prepare" {
			return errAttackOver
		}
		if time.Since(created) > raidPrepareLimit {
			return errAttackExpired
		}

		var buildings []battleBuildingInput
		if err := json.Unmarshal([]byte(snapshotText), &buildings); err != nil {
			return err
		}
		var team []raidTeamMember
		if err := json.Unmarshal([]byte(teamText), &team); err != nil {
			return err
		}
		heroes := make([]battleHeroInput, 0, len(request.Placements))
		for _, placement := range request.Placements {
			var member *raidTeamMember
			for i := range team {
				if team[i].InstanceID == placement.InstanceID {
					member = &team[i]
				}
			}
			if member == nil {
				return errHeroNotInTeam
			}
			heroes = append(heroes, battleHeroInput{member.InstanceID, member.Role, member.Hp, member.Attack, member.Defense, placement.X, placement.Z})
		}
		if refusal := deployError(buildings, heroes); refusal != "" {
			return gameError(refusal, codePrecondition)
		}

		result, err := runBattle(buildings, heroes)
		if err != nil {
			return err
		}

		// Gold is only paid for stars beyond the player's best, so a raid can't be farmed.
		best, err := bestStarsFor(ctx, tx, userID, raidID)
		if err != nil {
			return err
		}
		v, err := s.loadVillage(ctx, tx, userID, crystals)
		if err != nil {
			return err
		}
		beforeGold := v.gold
		var earned int64
		if result.Stars > best {
			earned = int64(result.Stars-best) * goldPerStar
			if room := v.goldCapacity() - v.gold; earned > room {
				earned = room
			}
			if earned < 0 {
				earned = 0
			}
			v.gold += earned
			best = result.Stars
			if _, err := tx.ExecContext(ctx, `INSERT INTO sd_raid_progress (user_id, raid_id, best_stars) VALUES ($1, $2, $3)
				ON CONFLICT (user_id, raid_id) DO UPDATE SET best_stars = EXCLUDED.best_stars`, userID, raidID, best); err != nil {
				return err
			}
		}
		if err := persistVillage(ctx, tx, userID, v, beforeGold, crystals, "raid_reward", request.AttackID); err != nil {
			return err
		}
		if _, err := tx.ExecContext(ctx, `UPDATE sd_raid SET status = 'resolved', stars = $1, damage_percent = $2, gold_earned = $3, checksum = $4, resolved_at = now() WHERE attack_id = $5`,
			result.Stars, result.DamagePercent, earned, result.Checksum, request.AttackID); err != nil {
			return err
		}

		state, err := loadState(ctx, tx, userID, crystals)
		if err != nil {
			return err
		}
		output, err = marshal(raidDeployResponse{request.AttackID, result, earned, best, state})
		if err != nil {
			return err
		}
		return storeResponse(ctx, tx, userID, request.Key, "raid_deploy", requestHash, output)
	})
	if err != nil {
		return "", err
	}
	return output, nil
}
