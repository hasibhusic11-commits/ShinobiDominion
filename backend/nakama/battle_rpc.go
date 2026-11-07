package main

import (
	"context"
	"database/sql"
	"encoding/json"
	"errors"
	"fmt"
	"sort"
	"time"

	"github.com/heroiclabs/nakama-common/runtime"
)

// A raid has to be fought soon after it starts, from the team it started with.
const raidPrepareLimit = 15 * time.Minute

const (
	rivalRaidID = "rival"
	// Test values. A rival pays once a day, and only so many rival raids pay each day.
	rivalGoldPerDojoLevel = 150
	rivalPaidRaidsPerDay  = 10
	rivalPayCooldown      = "24 hours"
	// Test value. Replays are free, so XP stops after this many paying wins in 24 hours.
	dungeonXpWinsPerDay = 30
)

var (
	errRaidNotFound   = gameError("RAID_NOT_FOUND", codeNotFound)
	errAttackNotFound = gameError("ATTACK_NOT_FOUND", codeNotFound)
	errAttackOver     = gameError("ATTACK_ALREADY_RESOLVED", codePrecondition)
	errAttackExpired  = gameError("ATTACK_EXPIRED", codePrecondition)
	errNoTeam         = gameError("NO_TEAM", codePrecondition)
	errHeroNotInTeam  = gameError("HERO_NOT_IN_TEAM", codePrecondition)
	errNotEnoughTroops = gameError("NOT_ENOUGH_TROOPS", codePrecondition)
	errBadTroopCount  = gameError("BAD_TROOP_COUNT", codeInvalid)
	errTooManyTroops  = gameError("TOO_MANY_TROOPS", codeInvalid)
	errNoRivals       = gameError("NO_RIVALS", codePrecondition)
	errRivalChoice    = gameError("RIVAL_NOT_ALLOWED", codeInvalid)
	errRaidLocked     = gameError("RAID_LOCKED", codePrecondition)
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

// raidDefender is a hero guarding the target, with the stats fixed when the raid started.
type raidDefender struct {
	ID             string `json:"id"`
	ArchetypeID    string `json:"archetypeId"`
	EvolutionIndex int    `json:"evolutionIndex"`
	Level          int    `json:"level"`
	Role           string `json:"role"`
	Hp             int64  `json:"hp"`
	Attack         int64  `json:"attack"`
	Defense        int64  `json:"defense"`
	X              int    `json:"x"`
	Z              int    `json:"z"`
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
	Defenders []raidDefender        `json:"defenders"`
}

type deployPlacement struct {
	InstanceID string `json:"instanceId"`
	X          int    `json:"x"`
	Z          int    `json:"z"`
}

type troopStack struct {
	TroopID string `json:"troopId"`
	X       int    `json:"x"`
	Z       int    `json:"z"`
	Count   int    `json:"count"`
}

type raidDeployResponse struct {
	AttackID   string        `json:"attackId"`
	Result     *battleResult `json:"result"`
	GoldEarned int64         `json:"goldEarned"`
	BestStars  int           `json:"bestStars"`
	State      *playerState  `json:"state"`
	XpEarned       int64     `json:"xpEarned"`
	LevelsGained   int       `json:"levelsGained"`
	CrystalsEarned int64     `json:"crystalsEarned"`
	ItemEarned     string    `json:"itemEarned"`
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
		// Stages open in order. Only the server decides this.
		if raid.Requires != "" {
			previous, err := bestStarsFor(ctx, tx, userID, raid.Requires)
			if err != nil {
				return err
			}
			if previous < 1 {
				return errRaidLocked
			}
		}

		defenders, err := s.scriptedDefenders(raid)
		if err != nil {
			return err
		}
		attackID, err := insertRaid(ctx, tx, userID, raid.ID, raid.Version, raid.GoldPerStar, nil, raid.expanded, team, defenders)
		if err != nil {
			return err
		}
		output, err = marshal(raidStartResponse{
			AttackID:  attackID,
			Raid:      raidSummary{raid.ID, raid.DisplayName, raid.Description, raid.GoldPerStar, best},
			Buildings: raid.expanded,
			Team:      team,
			Defenders: defenders,
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

// insertRaid stores the exact defender layout, team and guards, so nothing can change once a raid starts.
func insertRaid(ctx context.Context, tx *sql.Tx, userID, raidID string, version int, goldPerStar int64, opponent *string, buildings []battleBuildingInput, team []raidTeamMember, defenders []raidDefender) (string, error) {
	// Only one raid at a time. Starting a new one walks away from any unfinished one.
	if _, err := tx.ExecContext(ctx, `UPDATE sd_raid SET status = 'abandoned' WHERE user_id = $1 AND status = 'prepare'`, userID); err != nil {
		return "", err
	}
	snapshot, err := json.Marshal(buildings)
	if err != nil {
		return "", err
	}
	teamJSON, err := json.Marshal(team)
	if err != nil {
		return "", err
	}
	defendersJSON, err := json.Marshal(defenders)
	if err != nil {
		return "", err
	}
	var attackID string
	err = tx.QueryRowContext(ctx, `INSERT INTO sd_raid (attack_id, user_id, raid_id, raid_version, gold_per_star, snapshot, team, status, opponent_id, defenders)
		VALUES (gen_random_uuid(), $1, $2, $3, $4, $5, $6, 'prepare', $7, $8) RETURNING attack_id::text`,
		userID, raidID, version, goldPerStar, string(snapshot), string(teamJSON), opponent, string(defendersJSON)).Scan(&attackID)
	return attackID, err
}

// scriptedDefenders stands each listed hero in the middle of the Hero Base at its cell.
func (s *Server) scriptedDefenders(raid *raidDefinition) ([]raidDefender, error) {
	base, ok := s.buildings.byID["hero_base"]
	if !ok {
		return nil, errors.New("no hero base definition")
	}
	result := []raidDefender{}
	for i, entry := range raid.Defenders {
		defender, err := s.makeDefender(fmt.Sprintf("d%d", i+1), entry.HeroID, entry.Evolution, entry.Level, entry.X+base.Width/2, entry.Z+base.Depth/2)
		if err != nil {
			return nil, err
		}
		result = append(result, defender)
	}
	return result, nil
}

func (s *Server) makeDefender(id, archetypeID string, evolution, level, x, z int) (raidDefender, error) {
	archetype, ok := s.catalog.byID[archetypeID]
	if !ok {
		return raidDefender{}, errHeroNotFound
	}
	hp, attack, defense := heroBattleStats(archetype, evolution, level)
	return raidDefender{id, archetypeID, evolution, level, archetype.Role, hp, attack, defense, x, z}, nil
}

// rivalSnapshot copies a rival's village and guards as they stand now, in a fixed order.
func (s *Server) rivalSnapshot(ctx context.Context, tx *sql.Tx, rivalID string) ([]battleBuildingInput, []raidDefender, int, error) {
	v, err := s.loadVillage(ctx, tx, rivalID, 0)
	if err != nil {
		return nil, nil, 0, err
	}
	ids := make([]int, 0, len(v.byID))
	for id := range v.byID {
		ids = append(ids, id)
	}
	sort.Ints(ids)
	buildings := make([]battleBuildingInput, 0, len(ids))
	dojoLevel := 1
	for _, id := range ids {
		b := v.byID[id]
		width, depth := b.size()
		row := b.Def.at(b.Level)
		buildings = append(buildings, battleBuildingInput{
			ID: b.ID, DefinitionID: b.Def.ID, X: b.X, Z: b.Z, Width: width, Depth: depth,
			Level: b.Level, Hp: row.Hp, Attack: row.Attack, Range: row.Range,
			IsWall: b.Def.isWall(), IsDefense: b.Def.Function == "Defense",
		})
		if b.Def.ID == "dojo_hq" {
			dojoLevel = b.Level
		}
	}

	rows, err := tx.QueryContext(ctx, `SELECT archetype_id, evolution, level, base_slot FROM sd_hero WHERE user_id = $1 AND base_slot IS NOT NULL ORDER BY base_slot`, rivalID)
	if err != nil {
		return nil, nil, 0, err
	}
	defer rows.Close()
	defenders := []raidDefender{}
	for rows.Next() {
		var archetypeID string
		var evolution, level, slot int
		if err := rows.Scan(&archetypeID, &evolution, &level, &slot); err != nil {
			return nil, nil, 0, err
		}
		base, ok := v.byID[slot]
		if !ok {
			continue
		}
		width, depth := base.size()
		// The rival's own hero ids stay private: these are only labels for this raid.
		defender, err := s.makeDefender(fmt.Sprintf("d%d", len(defenders)+1), archetypeID, evolution, level, base.X+width/2, base.Z+depth/2)
		if err != nil {
			return nil, nil, 0, err
		}
		defenders = append(defenders, defender)
	}
	return buildings, defenders, dojoLevel, rows.Err()
}

func (s *Server) rpcRivalStart(ctx context.Context, logger runtime.Logger, db *sql.DB, nk runtime.NakamaModule, payload string) (string, error) {
	userID, err := userFrom(ctx)
	if err != nil {
		return "", err
	}
	var request struct {
		Key     string `json:"key"`
		RivalID string `json:"rivalId"`
	}
	if err := decodeStrict(payload, &request); err != nil {
		return "", err
	}
	if !keyPattern.MatchString(request.Key) {
		return "", errBadKey
	}
	// Players never choose their rival. Only development tests may, to be repeatable.
	if request.RivalID != "" && (!s.dev || !uuidPattern.MatchString(request.RivalID) || request.RivalID == userID) {
		return "", errRivalChoice
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
		if stored, found, err := replayed(ctx, tx, userID, request.Key, "rival_start", rivalRaidID); err != nil {
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
		mine, err := s.loadVillage(ctx, tx, userID, crystals)
		if err != nil {
			return err
		}
		myDojo := 1
		for _, b := range mine.byID {
			if b.Def.ID == "dojo_hq" {
				myDojo = b.Level
			}
		}

		// The closest Dojo level, preferring villages with heroes on guard, then anyone at random.
		var rivalID string
		if request.RivalID != "" {
			err = tx.QueryRowContext(ctx, `SELECT user_id::text FROM sd_player WHERE user_id = $1 AND village_ready`, request.RivalID).Scan(&rivalID)
		} else {
			err = tx.QueryRowContext(ctx, `SELECT p.user_id::text FROM sd_player p
				JOIN sd_building b ON b.user_id = p.user_id AND b.definition_id = 'dojo_hq'
				WHERE p.user_id <> $1 AND p.village_ready
				ORDER BY ABS(b.level - $2),
					EXISTS (SELECT 1 FROM sd_hero h WHERE h.user_id = p.user_id AND h.base_slot IS NOT NULL) DESC,
					random()
				LIMIT 1`, userID, myDojo).Scan(&rivalID)
		}
		if errors.Is(err, sql.ErrNoRows) {
			return errNoRivals
		}
		if err != nil {
			return err
		}
		buildings, defenders, dojoLevel, err := s.rivalSnapshot(ctx, tx, rivalID)
		if err != nil {
			return err
		}
		goldPerStar := int64(dojoLevel) * rivalGoldPerDojoLevel
		attackID, err := insertRaid(ctx, tx, userID, rivalRaidID, 1, goldPerStar, &rivalID, buildings, team, defenders)
		if err != nil {
			return err
		}
		output, err = marshal(raidStartResponse{
			AttackID:  attackID,
			Raid:      raidSummary{rivalRaidID, "Rival village", fmt.Sprintf("A real player's village, with a level %d Dojo and %d heroes on guard.", dojoLevel, len(defenders)), goldPerStar, 0},
			Buildings: buildings,
			Team:      team,
			Defenders: defenders,
		})
		if err != nil {
			return err
		}
		return storeResponse(ctx, tx, userID, request.Key, "rival_start", rivalRaidID, output)
	})
	if err != nil {
		return "", err
	}
	return output, nil
}

// grantHeroXP adds XP to one hero, using the same curve as HeroRoster.GrantXp on the client.
// A hero that has left the roster since the raid started simply gets nothing.
func (s *Server) grantHeroXP(ctx context.Context, tx *sql.Tx, userID, instanceID string, amount int64) (int, error) {
	var level int
	var experience int64
	err := tx.QueryRowContext(ctx, `SELECT level, experience FROM sd_hero WHERE instance_id = $1 AND user_id = $2 FOR UPDATE`, instanceID, userID).Scan(&level, &experience)
	if errors.Is(err, sql.ErrNoRows) {
		return 0, nil
	}
	if err != nil {
		return 0, err
	}
	gained := 0
	level, experience, gained = s.catalog.applyXP(level, experience, amount)
	_, err = tx.ExecContext(ctx, `UPDATE sd_hero SET level = $1, experience = $2 WHERE instance_id = $3 AND user_id = $4`, level, experience, instanceID, userID)
	return gained, err
}

// applyXP is pure so the shared scenarios can test it. Level 200 holds no XP, matching the client.
func (c *HeroCatalog) applyXP(level int, experience, amount int64) (int, int64, int) {
	const maxLevel = 200
	experience += amount
	gained := 0
	for level < maxLevel {
		need := c.XpBase + c.XpStep*int64(level-1)
		if experience < need {
			break
		}
		experience -= need
		level++
		gained++
	}
	if level >= maxLevel {
		experience = 0
	}
	return level, experience, gained
}

// xpWinsToday counts the wins in the last 24 hours that paid XP.
func xpWinsToday(ctx context.Context, tx *sql.Tx, userID string) (int, error) {
	var wins int
	err := tx.QueryRowContext(ctx, `SELECT COUNT(*) FROM sd_raid WHERE user_id = $1 AND status = 'resolved' AND xp_earned > 0 AND resolved_at > now() - interval '24 hours'`, userID).Scan(&wins)
	return wins, err
}

func grantItem(ctx context.Context, tx *sql.Tx, userID, itemID, ref string) error {
	if _, err := tx.ExecContext(ctx, `INSERT INTO sd_item (user_id, item_id, count) VALUES ($1, $2, 1)
		ON CONFLICT (user_id, item_id) DO UPDATE SET count = sd_item.count + 1`, userID, itemID); err != nil {
		return err
	}
	return addItemLedger(ctx, tx, userID, itemID, 1, "dungeon_first_clear", ref)
}

// rivalPays says whether a rival raid may pay gold: once a rival a day, and a daily limit overall.
func rivalPays(ctx context.Context, tx *sql.Tx, userID, rivalID string) (bool, error) {
	var sameRival, today int
	err := tx.QueryRowContext(ctx, `SELECT
		COUNT(*) FILTER (WHERE opponent_id = $2),
		COUNT(*)
		FROM sd_raid WHERE user_id = $1 AND status = 'resolved' AND opponent_id IS NOT NULL AND gold_earned > 0
		AND resolved_at > now() - interval '`+rivalPayCooldown+`'`, userID, rivalID).Scan(&sameRival, &today)
	if err != nil {
		return false, err
	}
	return sameRival == 0 && today < rivalPaidRaidsPerDay, nil
}

type defenseRecord struct {
	Stars          int   `json:"stars"`
	DamagePercent  int   `json:"damagePercent"`
	DefendersAlive int   `json:"defendersAlive"`
	DefendersTotal int   `json:"defendersTotal"`
	AtMs           int64 `json:"atMs"`
}

// rpcDefenseLog lists raids on this player's village. It never says who attacked.
func (s *Server) rpcDefenseLog(ctx context.Context, logger runtime.Logger, db *sql.DB, nk runtime.NakamaModule, payload string) (string, error) {
	userID, err := userFrom(ctx)
	if err != nil {
		return "", err
	}
	rows, err := db.QueryContext(ctx, `SELECT stars, damage_percent, COALESCE(defenders_alive, 0), defenders, (EXTRACT(EPOCH FROM resolved_at) * 1000)::bigint
		FROM sd_raid WHERE opponent_id = $1 AND status = 'resolved' ORDER BY resolved_at DESC LIMIT 20`, userID)
	if err != nil {
		return "", err
	}
	defer rows.Close()
	records := []defenseRecord{}
	for rows.Next() {
		var record defenseRecord
		var defendersText string
		if err := rows.Scan(&record.Stars, &record.DamagePercent, &record.DefendersAlive, &defendersText, &record.AtMs); err != nil {
			return "", err
		}
		var defenders []raidDefender
		if err := json.Unmarshal([]byte(defendersText), &defenders); err != nil {
			return "", err
		}
		record.DefendersTotal = len(defenders)
		records = append(records, record)
	}
	if err := rows.Err(); err != nil {
		return "", err
	}
	return marshal(map[string]interface{}{"defenses": records})
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
		Troops     []troopStack      `json:"troops"`
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
	requestHash := hashRequest(placementHash(request.AttackID, request.Placements, request.Troops))

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

		var raidID, status, snapshotText, teamText, defendersText string
		var opponent sql.NullString
		var goldPerStar int64
		var created time.Time
		err = tx.QueryRowContext(ctx, `SELECT raid_id, status, snapshot, team, defenders, opponent_id::text, gold_per_star, created_at FROM sd_raid WHERE attack_id = $1 AND user_id = $2 FOR UPDATE`,
			request.AttackID, userID).Scan(&raidID, &status, &snapshotText, &teamText, &defendersText, &opponent, &goldPerStar, &created)
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
		var stored []raidDefender
		if err := json.Unmarshal([]byte(defendersText), &stored); err != nil {
			return err
		}
		defenders := make([]battleDefenderInput, 0, len(stored))
		for _, d := range stored {
			defenders = append(defenders, battleDefenderInput{d.ID, d.Role, d.Hp, d.Attack, d.Defense, d.X, d.Z})
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
			heroes = append(heroes, battleHeroInput{member.InstanceID, member.Role, member.Hp, member.Attack, member.Defense, placement.X, placement.Z, false})
		}
		// Troops come from the camps and are used up by landing, win or lose.
		counts, err := loadTroopCounts(ctx, tx, userID)
		if err != nil {
			return err
		}
		landed := 0
		used := map[string]int{}
		for _, stack := range request.Troops {
			if stack.Count < 1 || stack.Count > maxRaidTroops {
				return errBadTroopCount
			}
			landed += stack.Count
			if landed > maxRaidTroops {
				return errTooManyTroops
			}
			troop, ok := s.troops.byID[stack.TroopID]
			if !ok || used[stack.TroopID]+stack.Count > counts[stack.TroopID] {
				return errNotEnoughTroops
			}
			used[stack.TroopID] += stack.Count
			for i := 0; i < stack.Count; i++ {
				heroes = append(heroes, battleHeroInput{troop.ID, troop.Role, troop.Hp, troop.Attack, troop.Defense, stack.X, stack.Z, true})
			}
		}
		if refusal := deployError(buildings, heroes); refusal != "" {
			return gameError(refusal, codePrecondition)
		}

		result, err := runDefendedBattle(buildings, heroes, defenders)
		if err != nil {
			return err
		}
		for id, count := range used {
			if err := storeTroopCount(ctx, tx, userID, id, counts[id]-count); err != nil {
				return err
			}
		}

		v, err := s.loadVillage(ctx, tx, userID, crystals)
		if err != nil {
			return err
		}
		beforeGold := v.gold
		var earned, xpEarned, crystalsEarned int64
		var levelsGained int
		var itemEarned string
		best := result.Stars
		if opponent.Valid {
			// A raid on a rival never costs the rival anything. Limits stop one village being farmed.
			pays, err := rivalPays(ctx, tx, userID, opponent.String)
			if err != nil {
				return err
			}
			if pays && result.Stars > 0 {
				earned = int64(result.Stars) * goldPerStar
				if room := v.goldCapacity() - v.gold; earned > room {
					earned = room
				}
				if earned < 0 {
					earned = 0
				}
				v.gold += earned
			}
		} else if best, err = bestStarsFor(ctx, tx, userID, raidID); err != nil {
			return err
		} else {
			// One-time rewards pay on the first win, and read today's raid data rather than the player's claims.
			if definition, ok := s.raids.byID[raidID]; ok && result.Stars > 0 {
				if best == 0 {
					if definition.FirstClearCrystals > 0 {
						crystalsEarned = definition.FirstClearCrystals
						v.crystals += crystalsEarned
					}
					if definition.FirstClearItem != "" {
						if err := grantItem(ctx, tx, userID, definition.FirstClearItem, request.AttackID); err != nil {
							return err
						}
						itemEarned = definition.FirstClearItem
					}
				}
				wins, err := xpWinsToday(ctx, tx, userID)
				if err != nil {
					return err
				}
				if definition.XpPerWin > 0 && wins < dungeonXpWinsPerDay {
					xpEarned = definition.XpPerWin
					for _, placement := range request.Placements {
						gained, err := s.grantHeroXP(ctx, tx, userID, placement.InstanceID, xpEarned)
						if err != nil {
							return err
						}
						levelsGained += gained
					}
				}
			}
		}
		if !opponent.Valid && result.Stars > best {
			// Gold is only paid for stars beyond the player's best, so a raid can't be farmed.
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
		if _, err := tx.ExecContext(ctx, `UPDATE sd_raid SET status = 'resolved', stars = $1, damage_percent = $2, gold_earned = $3, checksum = $4, defenders_alive = $5, xp_earned = $6, crystals_earned = $7, item_earned = NULLIF($8, ''), resolved_at = now() WHERE attack_id = $9`,
			result.Stars, result.DamagePercent, earned, result.Checksum, result.DefendersAlive, xpEarned, crystalsEarned, itemEarned, request.AttackID); err != nil {
			return err
		}

		state, err := loadState(ctx, tx, userID, v.crystals)
		if err != nil {
			return err
		}
		output, err = marshal(raidDeployResponse{request.AttackID, result, earned, best, state, xpEarned, levelsGained, crystalsEarned, itemEarned})
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
