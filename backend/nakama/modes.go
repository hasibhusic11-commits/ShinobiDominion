package main

import (
	"context"
	"database/sql"
	_ "embed"
	"encoding/binary"
	"encoding/json"
	"errors"
	"fmt"
	"io"
	"strconv"
	"time"

	"github.com/heroiclabs/nakama-common/runtime"
)

// Game modes beyond raids and dungeons (ADR-039). Each battle here is fought by the server in one call
// and the client only plays the log back, so there is nothing to place and nothing to forge.

//go:embed config/modes.json
var modesJSON []byte

const (
	modeArena = "arena"
	modeSiege = "siege"
	// The UTC day, which daily quests, the gift and the Arena's paid wins all reset on.
	sqlToday = `(now() AT TIME ZONE 'UTC')::date`
)

var (
	errSiegeLocked   = gameError("SIEGE_LOCKED", codePrecondition)
	errSiegeNotFound = gameError("SIEGE_NOT_FOUND", codeNotFound)
	errQuestNotFound = gameError("QUEST_NOT_FOUND", codeNotFound)
	errQuestNotDone  = gameError("QUEST_NOT_DONE", codePrecondition)
	errQuestClaimed  = gameError("QUEST_ALREADY_CLAIMED", codePrecondition)
	errGiftOpened    = gameError("GIFT_ALREADY_OPENED", codePrecondition)
	errNoArenaFoe    = gameError("NO_ARENA_OPPONENT", codePrecondition)
)

type siegeWave struct {
	TroopID string `json:"troopId"`
	Count   int    `json:"count"`
	Spot    int    `json:"spot"`
}

type siegeLevel struct {
	Level              int         `json:"level"`
	Name               string      `json:"name"`
	StatPercent        int64       `json:"statPercent"`
	GoldPerStar        int64       `json:"goldPerStar"`
	ManaPerStar        int64       `json:"manaPerStar"`
	FirstClearCrystals int64       `json:"firstClearCrystals"`
	Waves              []siegeWave `json:"waves"`
}

type arenaConfig struct {
	DisplayName    string `json:"displayName"`
	Description    string `json:"description"`
	PaidWinsPerDay int    `json:"paidWinsPerDay"`
	WinPointsBase  int    `json:"winPointsBase"`
	WinPointsMin   int    `json:"winPointsMin"`
	WinPointsMax   int    `json:"winPointsMax"`
	LossPoints     int    `json:"lossPoints"`
	WinGold        int64  `json:"winGold"`
	WinMana        int64  `json:"winMana"`
	ShrineHp       int64  `json:"shrineHp"`
	ShrineX        int    `json:"shrineX"`
	ShrineZ        int    `json:"shrineZ"`
	GuardX         int    `json:"guardX"`
	GuardZ         int    `json:"guardZ"`
	HeroX          int    `json:"heroX"`
	HeroZ          int    `json:"heroZ"`
	MatchWindow    int    `json:"matchWindow"`
}

type questDef struct {
	ID       string `json:"id"`
	Title    string `json:"title"`
	Target   int    `json:"target"`
	Crystals int64  `json:"crystals"`
}

type modesConfig struct {
	SchemaVersion int `json:"schemaVersion"`
	Siege         struct {
		DisplayName string       `json:"displayName"`
		Description string       `json:"description"`
		SpawnSpots  [][2]int     `json:"spawnSpots"`
		Levels      []siegeLevel `json:"levels"`
	} `json:"siege"`
	Arena     arenaConfig `json:"arena"`
	Quests    []questDef  `json:"quests"`
	DailyGift struct {
		Crystals int64 `json:"crystals"`
		Gold     int64 `json:"gold"`
		Mana     int64 `json:"mana"`
	} `json:"dailyGift"`
	Trials        trialConfig `json:"trials"`
	TreasureChest chestConfig `json:"treasureChest"`
	Shop          []shopOffer `json:"shop"`
	questByID map[string]questDef
}

func loadModes(data []byte, troops *troopCatalog, buildings *BuildingCatalog) (*modesConfig, error) {
	var modes modesConfig
	if err := json.Unmarshal(data, &modes); err != nil {
		return nil, err
	}
	if modes.SchemaVersion != 1 {
		return nil, errors.New("unsupported modes file")
	}
	for _, spot := range modes.Siege.SpawnSpots {
		if !inBounds(spot[0], spot[1]) {
			return nil, errors.New("siege spawn spot out of bounds")
		}
	}
	for i, level := range modes.Siege.Levels {
		if level.Level != i+1 || level.StatPercent < 1 || len(level.Waves) == 0 {
			return nil, fmt.Errorf("siege level %d is invalid", i+1)
		}
		total := 0
		for _, wave := range level.Waves {
			if _, ok := troops.byID[wave.TroopID]; !ok {
				return nil, fmt.Errorf("siege level %d uses unknown troop %s", level.Level, wave.TroopID)
			}
			if wave.Count < 1 || wave.Spot < 0 || wave.Spot >= len(modes.Siege.SpawnSpots) {
				return nil, fmt.Errorf("siege level %d has a bad wave", level.Level)
			}
			total += wave.Count
		}
		if total > maxRaidTroops {
			return nil, fmt.Errorf("siege level %d lands too many troops", level.Level)
		}
	}
	a := modes.Arena
	if a.ShrineHp < 1 || a.WinPointsMin < 1 || a.WinPointsMax < a.WinPointsMin || a.LossPoints < 0 || a.PaidWinsPerDay < 0 {
		return nil, errors.New("arena values are invalid")
	}
	if _, ok := buildings.byID["dojo_hq"]; !ok {
		return nil, errors.New("arena needs the dojo_hq building for its shrine")
	}
	if err := modes.checkShop(); err != nil {
		return nil, err
	}
	if err := modes.Trials.check(); err != nil {
		return nil, err
	}
	modes.questByID = map[string]questDef{}
	for _, quest := range modes.Quests {
		if quest.Target < 1 || quest.Crystals < 0 {
			return nil, errors.New("quest " + quest.ID + " is invalid")
		}
		modes.questByID[quest.ID] = quest
	}
	return &modes, nil
}

// bumpQuest adds to today's progress on a daily quest, never past its target. Unknown ids do nothing.
func (s *Server) bumpQuest(ctx context.Context, tx *sql.Tx, userID, questID string, amount int) error {
	quest, ok := s.modes.questByID[questID]
	if !ok || amount <= 0 {
		return nil
	}
	_, err := tx.ExecContext(ctx, `INSERT INTO sd_quest_day (user_id, day, quest_id, progress) VALUES ($1, `+sqlToday+`, $2, LEAST($3::int, $4::int))
		ON CONFLICT (user_id, day, quest_id) DO UPDATE SET progress = LEAST(sd_quest_day.progress + $3::int, $4::int)`, userID, questID, amount, quest.Target)
	return err
}

// ---------- The shared answer for a battle fought in one call ----------

type modeFightResponse struct {
	Mode           string                `json:"mode"`
	Level          int                   `json:"level"`
	Title          string                `json:"title"`
	Description    string                `json:"description"`
	Buildings      []battleBuildingInput `json:"buildings"`
	Team           []raidTeamMember      `json:"team"`
	Placements     []deployPlacement     `json:"placements"`
	Troops         []troopStack          `json:"troops"`
	TroopPercent   int64                 `json:"troopPercent"`
	Defenders      []raidDefender        `json:"defenders"`
	Result         *battleResult         `json:"result"`
	Won            bool                  `json:"won"`
	Stars          int                   `json:"stars"`
	BestStars      int                   `json:"bestStars"`
	GoldEarned     int64                 `json:"goldEarned"`
	ManaEarned     int64                 `json:"manaEarned"`
	CrystalsEarned int64                 `json:"crystalsEarned"`
	PointsDelta    int                   `json:"pointsDelta"`
	Points         int                   `json:"points"`
	Rank           int                   `json:"rank"`
	OpponentName   string                `json:"opponentName"`
	OpponentPoints int                   `json:"opponentPoints"`
	State          *playerState          `json:"state"`
}

func payGold(v *village, amount int64) int64 {
	if room := v.goldCapacity() - v.gold; amount > room {
		amount = room
	}
	if amount < 0 {
		amount = 0
	}
	v.gold += amount
	return amount
}

func logModeFight(ctx context.Context, tx *sql.Tx, userID, mode string, level int, opponent *string, won bool, stars, points int, gold, mana, crystals int64, checksum string) error {
	_, err := tx.ExecContext(ctx, `INSERT INTO sd_mode_fight (user_id, mode, level, opponent_id, won, stars, points_delta, gold, mana, crystals, checksum)
		VALUES ($1, $2, $3, $4, $5, $6, $7, $8, $9, $10, $11)`, userID, mode, level, opponent, won, stars, points, gold, mana, crystals, checksum)
	return err
}

// ---------- Village Siege: waves storm the player's own village ----------

// siegeStacks lands each wave at the deployable cell nearest its spawn spot, searched in a fixed order,
// so the same village always meets the same attack.
func (s *Server) siegeStacks(buildings []battleBuildingInput, level siegeLevel) ([]troopStack, []battleHeroInput) {
	stacks := []troopStack{}
	units := []battleHeroInput{}
	for _, wave := range level.Waves {
		spot := s.modes.Siege.SpawnSpots[wave.Spot]
		x, z, ok := nearestDeployable(buildings, spot[0], spot[1])
		if !ok {
			continue
		}
		stacks = append(stacks, troopStack{TroopID: wave.TroopID, X: x, Z: z, Count: wave.Count})
	}
	for _, stack := range stacks {
		troop := s.troops.byID[stack.TroopID]
		for i := 0; i < stack.Count; i++ {
			units = append(units, battleHeroInput{troop.ID, troop.Role, scaled(troop.Hp, level.StatPercent), scaled(troop.Attack, level.StatPercent), scaled(troop.Defense, level.StatPercent), stack.X, stack.Z, true, "", 0, 0})
		}
	}
	return stacks, units
}

func scaled(value, percent int64) int64 {
	result := value * percent / 100
	if result < 1 && value > 0 {
		return 1
	}
	return result
}

func nearestDeployable(buildings []battleBuildingInput, x, z int) (int, int, bool) {
	for radius := 0; radius < battleGridSize; radius++ {
		for dz := -radius; dz <= radius; dz++ {
			for dx := -radius; dx <= radius; dx++ {
				if abs(dx) != radius && abs(dz) != radius {
					continue
				}
				cx, cz := x+dx, z+dz
				if inBounds(cx, cz) && canDeployAt(buildings, cx, cz) {
					return cx, cz, true
				}
			}
		}
	}
	return 0, 0, false
}

// siegeGuardPost is the open cell nearest the Dojo's centre, searched in a fixed order.
func siegeGuardPost(buildings []battleBuildingInput) (int, int, bool) {
	for _, b := range buildings {
		if b.DefinitionID != "dojo_hq" {
			continue
		}
		cx, cz := b.X+b.Width/2, b.Z+b.Depth/2
		for radius := 0; radius < battleGridSize; radius++ {
			for dz := -radius; dz <= radius; dz++ {
				for dx := -radius; dx <= radius; dx++ {
					if abs(dx) != radius && abs(dz) != radius {
						continue
					}
					if x, z := cx+dx, cz+dz; inBounds(x, z) && !insideAny(buildings, x, z) {
						return x, z, true
					}
				}
			}
		}
	}
	return 0, 0, false
}

func insideAny(buildings []battleBuildingInput, x, z int) bool {
	for _, b := range buildings {
		if x >= b.X && x < b.X+b.Width && z >= b.Z && z < b.Z+b.Depth {
			return true
		}
	}
	return false
}

// defenseStars turns how the attackers did into stars for the defender: none if they took a star.
func defenseStars(result *battleResult) int {
	if result.Stars > 0 {
		return 0
	}
	stars := 1
	if result.DamagePercent < 30 {
		stars++
	}
	if result.DamagePercent < 10 {
		stars++
	}
	return stars
}

type siegeSummary struct {
	Level              int    `json:"level"`
	Name               string `json:"name"`
	Attackers          int    `json:"attackers"`
	GoldPerStar        int64  `json:"goldPerStar"`
	ManaPerStar        int64  `json:"manaPerStar"`
	FirstClearCrystals int64  `json:"firstClearCrystals"`
	BestStars          int    `json:"bestStars"`
	Open               bool   `json:"open"`
}

func siegeRaidID(level int) string { return "siege_" + strconv.Itoa(level) }

func (s *Server) rpcSiegeList(ctx context.Context, logger runtime.Logger, db *sql.DB, nk runtime.NakamaModule, payload string) (string, error) {
	userID, err := userFrom(ctx)
	if err != nil {
		return "", err
	}
	var output string
	err = withTx(ctx, db, func(tx *sql.Tx) error {
		if _, err := s.lockPlayer(ctx, tx, userID); err != nil {
			return err
		}
		list := []siegeSummary{}
		previous := 1
		for _, level := range s.modes.Siege.Levels {
			best, err := bestStarsFor(ctx, tx, userID, siegeRaidID(level.Level))
			if err != nil {
				return err
			}
			attackers := 0
			for _, wave := range level.Waves {
				attackers += wave.Count
			}
			list = append(list, siegeSummary{level.Level, level.Name, attackers, level.GoldPerStar, level.ManaPerStar, level.FirstClearCrystals, best, level.Level == 1 || previous > 0})
			previous = best
		}
		output, err = marshal(map[string]interface{}{"levels": list})
		return err
	})
	return output, err
}

func (s *Server) rpcSiegeFight(ctx context.Context, logger runtime.Logger, db *sql.DB, nk runtime.NakamaModule, payload string) (string, error) {
	userID, err := userFrom(ctx)
	if err != nil {
		return "", err
	}
	var request struct {
		Key   string `json:"key"`
		Level int    `json:"level"`
	}
	if err := decodeStrict(payload, &request); err != nil {
		return "", err
	}
	if !keyPattern.MatchString(request.Key) {
		return "", errBadKey
	}
	if request.Level < 1 || request.Level > len(s.modes.Siege.Levels) {
		return "", errSiegeNotFound
	}
	if !s.limiter.Allow(userID, time.Now()) {
		return "", errRateLimited
	}
	level := s.modes.Siege.Levels[request.Level-1]
	requestHash := hashRequest(strconv.Itoa(level.Level))

	var output string
	err = withTx(ctx, db, func(tx *sql.Tx) error {
		crystals, err := s.lockPlayer(ctx, tx, userID)
		if err != nil {
			return err
		}
		if stored, found, err := replayed(ctx, tx, userID, request.Key, "siege_fight", requestHash); err != nil {
			return err
		} else if found {
			output = stored
			return nil
		}
		if level.Level > 1 {
			before, err := bestStarsFor(ctx, tx, userID, siegeRaidID(level.Level-1))
			if err != nil {
				return err
			}
			if before == 0 {
				return errSiegeLocked
			}
		}
		// The player's own village and guard, as they stand right now.
		buildings, defenders, _, err := s.rivalSnapshot(ctx, tx, userID)
		if err != nil {
			return err
		}
		// Under siege the guard leaves its base and stands by the Dojo, where every wave is headed.
		if x, z, ok := siegeGuardPost(buildings); ok {
			for i := range defenders {
				defenders[i].X, defenders[i].Z = x, z
			}
		}
		stacks, units := s.siegeStacks(buildings, level)
		inputs := make([]battleDefenderInput, 0, len(defenders))
		for _, d := range defenders {
			inputs = append(inputs, battleDefenderInput{d.ID, d.Role, d.Hp, d.Attack, d.Defense, d.X, d.Z, d.Special, d.EvolutionIndex, d.Power})
		}
		result, err := runDefendedBattle(buildings, units, inputs)
		if err != nil {
			return err
		}
		stars := defenseStars(result)

		v, err := s.loadVillage(ctx, tx, userID, crystals)
		if err != nil {
			return err
		}
		beforeGold := v.gold
		best, err := bestStarsFor(ctx, tx, userID, siegeRaidID(level.Level))
		if err != nil {
			return err
		}
		response := modeFightResponse{Mode: modeSiege, Level: level.Level, Title: level.Name, Description: s.modes.Siege.Description,
			Buildings: buildings, Team: []raidTeamMember{}, Placements: []deployPlacement{}, Troops: stacks, TroopPercent: level.StatPercent,
			Defenders: defenders, Result: result, Won: stars > 0, Stars: stars, BestStars: best}
		if stars > best {
			// Only stars beyond the best so far pay, so a level can't be farmed.
			newStars := int64(stars - best)
			response.GoldEarned = payGold(v, newStars*level.GoldPerStar)
			response.ManaEarned = newStars * level.ManaPerStar
			v.mana += response.ManaEarned
			if best == 0 && level.FirstClearCrystals > 0 {
				response.CrystalsEarned = level.FirstClearCrystals
				v.crystals += response.CrystalsEarned
			}
			response.BestStars = stars
			if _, err := tx.ExecContext(ctx, `INSERT INTO sd_raid_progress (user_id, raid_id, best_stars) VALUES ($1, $2, $3)
				ON CONFLICT (user_id, raid_id) DO UPDATE SET best_stars = EXCLUDED.best_stars`, userID, siegeRaidID(level.Level), stars); err != nil {
				return err
			}
		}
		if stars > 0 {
			if err := s.bumpQuest(ctx, tx, userID, "siege", 1); err != nil {
				return err
			}
		}
		if err := persistVillage(ctx, tx, userID, v, beforeGold, crystals, "siege_reward", request.Key); err != nil {
			return err
		}
		if err := logModeFight(ctx, tx, userID, modeSiege, level.Level, nil, stars > 0, stars, 0, response.GoldEarned, response.ManaEarned, response.CrystalsEarned, result.Checksum); err != nil {
			return err
		}
		if response.State, err = loadState(ctx, tx, userID, v.crystals); err != nil {
			return err
		}
		if output, err = marshal(response); err != nil {
			return err
		}
		return storeResponse(ctx, tx, userID, request.Key, "siege_fight", requestHash, output)
	})
	if err != nil {
		return "", err
	}
	return output, nil
}

// ---------- Arena: a hero duel for points ----------

func (s *Server) arenaRow(ctx context.Context, tx *sql.Tx, userID string) (points, wins, losses int, err error) {
	if _, err = tx.ExecContext(ctx, `INSERT INTO sd_arena (user_id) VALUES ($1) ON CONFLICT (user_id) DO NOTHING`, userID); err != nil {
		return
	}
	err = tx.QueryRowContext(ctx, `SELECT points, wins, losses FROM sd_arena WHERE user_id = $1 FOR UPDATE`, userID).Scan(&points, &wins, &losses)
	return
}

func arenaRank(ctx context.Context, tx *sql.Tx, points int) (int, error) {
	var above int
	err := tx.QueryRowContext(ctx, `SELECT count(*) FROM sd_arena WHERE points > $1`, points).Scan(&above)
	return above + 1, err
}

func arenaPaidWinsToday(ctx context.Context, tx *sql.Tx, userID string) (int, error) {
	var count int
	err := tx.QueryRowContext(ctx, `SELECT count(*) FROM sd_mode_fight WHERE user_id = $1 AND mode = 'arena' AND won AND (gold > 0 OR mana > 0)
		AND (created_at AT TIME ZONE 'UTC')::date = `+sqlToday, userID).Scan(&count)
	return count, err
}

// playerName is the display name a player chose, or a stable "Shinobi" tag made from their id. Nakama's own
// usernames are random letters, which read as noise in the rankings.
func playerName(ctx context.Context, tx *sql.Tx, userID string) string {
	var name string
	if err := tx.QueryRowContext(ctx, `SELECT `+sqlPlayerName+` FROM users u WHERE u.id = $1`, userID).Scan(&name); err != nil {
		return "Shinobi"
	}
	return name
}

const sqlPlayerName = `COALESCE(NULLIF(u.display_name, ''), 'Shinobi ' || upper(substr(replace(u.id::text, '-', ''), 1, 4)))`

func (s *Server) randomIndex(n int) (int, error) {
	if n <= 1 {
		return 0, nil
	}
	var buffer [8]byte
	if _, err := io.ReadFull(s.random, buffer[:]); err != nil {
		return 0, err
	}
	return int(binary.BigEndian.Uint64(buffer[:]) % uint64(n)), nil
}

// arenaBot is a wandering hero for when no real player is close enough in points. Its level climbs with
// the player's points, so the ladder keeps getting harder.
func (s *Server) arenaBot(mine raidTeamMember, points int) (string, int, int, string, error) {
	choices := []string{}
	for _, item := range s.pool.Items {
		choices = append(choices, item.ArchetypeID)
	}
	if len(choices) == 0 {
		return "", 0, 0, "", errNoArenaFoe
	}
	pick, err := s.randomIndex(len(choices))
	if err != nil {
		return "", 0, 0, "", err
	}
	archetype := s.catalog.byID[choices[pick]]
	level := mine.Level + points/100 - 1
	if level < 1 {
		level = 1
	}
	if level > 200 {
		level = 200
	}
	return archetype.ID, mine.EvolutionIndex, level, "Wandering " + archetype.DisplayName, nil
}

func (s *Server) rpcArenaFight(ctx context.Context, logger runtime.Logger, db *sql.DB, nk runtime.NakamaModule, payload string) (string, error) {
	userID, err := userFrom(ctx)
	if err != nil {
		return "", err
	}
	var request struct {
		Key string `json:"key"`
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
	a := s.modes.Arena

	var output string
	err = withTx(ctx, db, func(tx *sql.Tx) error {
		crystals, err := s.lockPlayer(ctx, tx, userID)
		if err != nil {
			return err
		}
		if stored, found, err := replayed(ctx, tx, userID, request.Key, "arena_fight", modeArena); err != nil {
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
		hero := team[0]
		points, wins, losses, err := s.arenaRow(ctx, tx, userID)
		if err != nil {
			return err
		}

		// A real player with a guard and close in points if there is one, else a wandering hero.
		var opponentID *string
		var foeArchetype, foeName string
		var foeEvolution, foeLevel int
		var foeGear [3]int
		foePoints := points
		var found string
		err = tx.QueryRowContext(ctx, `SELECT user_id::text, points FROM (
				SELECT a.user_id, a.points FROM sd_arena a
				WHERE a.user_id <> $1 AND ABS(a.points - $2) <= $3
					AND EXISTS (SELECT 1 FROM sd_hero h WHERE h.user_id = a.user_id AND h.base_slot IS NOT NULL)
				ORDER BY ABS(a.points - $2) LIMIT 5) near
			ORDER BY random() LIMIT 1`, userID, points, a.MatchWindow).Scan(&found, &foePoints)
		if err != nil && !errors.Is(err, sql.ErrNoRows) {
			return err
		}
		if err == nil {
			if err := tx.QueryRowContext(ctx, `SELECT archetype_id, evolution, level, gear_weapon, gear_armor, gear_charm FROM sd_hero WHERE user_id = $1 AND base_slot IS NOT NULL ORDER BY base_slot LIMIT 1`, found).
				Scan(&foeArchetype, &foeEvolution, &foeLevel, &foeGear[0], &foeGear[1], &foeGear[2]); err != nil {
				return err
			}
			opponentID = &found
			foeName = playerName(ctx, tx, found)
		} else {
			foePoints = points
			if foeArchetype, foeEvolution, foeLevel, foeName, err = s.arenaBot(hero, points); err != nil {
				return err
			}
		}
		defender, err := s.makeDefender("d1", foeArchetype, foeEvolution, foeLevel, a.GuardX, a.GuardZ)
		if err != nil {
			return err
		}
		defender.Hp, defender.Attack, defender.Defense = s.catalog.withGear(defender.Hp, defender.Attack, defender.Defense, foeGear)

		shrine := s.buildings.byID["dojo_hq"]
		buildings := []battleBuildingInput{{ID: 1, DefinitionID: shrine.ID, X: a.ShrineX, Z: a.ShrineZ, Width: shrine.Width, Depth: shrine.Depth, Level: 1, Hp: a.ShrineHp}}
		placement := deployPlacement{InstanceID: hero.InstanceID, X: a.HeroX, Z: a.HeroZ}
		units := []battleHeroInput{{hero.InstanceID, hero.Role, hero.Hp, hero.Attack, hero.Defense, placement.X, placement.Z, false, hero.Special, hero.EvolutionIndex, hero.Power}}
		result, err := runDefendedBattle(buildings, units, []battleDefenderInput{{defender.ID, defender.Role, defender.Hp, defender.Attack, defender.Defense, defender.X, defender.Z, defender.Special, defender.EvolutionIndex, defender.Power}})
		if err != nil {
			return err
		}
		won := result.DojoDestroyed

		v, err := s.loadVillage(ctx, tx, userID, crystals)
		if err != nil {
			return err
		}
		beforeGold := v.gold
		response := modeFightResponse{Mode: modeArena, Title: "Arena duel", Description: a.Description, Buildings: buildings, Team: team[:1],
			Placements: []deployPlacement{placement}, Troops: []troopStack{}, TroopPercent: 100, Defenders: []raidDefender{defender},
			Result: result, Won: won, OpponentName: foeName, OpponentPoints: foePoints}
		if won {
			response.Stars = 3
			delta := a.WinPointsBase + (foePoints-points)/10
			if delta < a.WinPointsMin {
				delta = a.WinPointsMin
			}
			if delta > a.WinPointsMax {
				delta = a.WinPointsMax
			}
			response.PointsDelta = delta
			wins++
			paid, err := arenaPaidWinsToday(ctx, tx, userID)
			if err != nil {
				return err
			}
			if paid < a.PaidWinsPerDay {
				response.GoldEarned = payGold(v, a.WinGold)
				response.ManaEarned = a.WinMana
				v.mana += a.WinMana
			}
		} else {
			response.PointsDelta = -a.LossPoints
			if points+response.PointsDelta < 0 {
				response.PointsDelta = -points
			}
			losses++
		}
		response.Points = points + response.PointsDelta
		if _, err := tx.ExecContext(ctx, `UPDATE sd_arena SET points = $1, wins = $2, losses = $3, updated_at = now() WHERE user_id = $4`, response.Points, wins, losses, userID); err != nil {
			return err
		}
		if response.Rank, err = arenaRank(ctx, tx, response.Points); err != nil {
			return err
		}
		if err := s.bumpQuest(ctx, tx, userID, "arena", 1); err != nil {
			return err
		}
		if err := persistVillage(ctx, tx, userID, v, beforeGold, crystals, "arena_reward", request.Key); err != nil {
			return err
		}
		if err := logModeFight(ctx, tx, userID, modeArena, 0, opponentID, won, response.Stars, response.PointsDelta, response.GoldEarned, response.ManaEarned, 0, result.Checksum); err != nil {
			return err
		}
		if response.State, err = loadState(ctx, tx, userID, v.crystals); err != nil {
			return err
		}
		if output, err = marshal(response); err != nil {
			return err
		}
		return storeResponse(ctx, tx, userID, request.Key, "arena_fight", modeArena, output)
	})
	if err != nil {
		return "", err
	}
	return output, nil
}

type arenaEntry struct {
	Rank        int    `json:"rank"`
	Name        string `json:"name"`
	Points      int    `json:"points"`
	ArchetypeID string `json:"archetypeId"`
	Level       int    `json:"level"`
	Evolution   int    `json:"evolution"`
	IsMe        bool   `json:"isMe"`
}

// rpcArenaBoard is the rankings: the top of the Arena and where the player stands. Read only.
func (s *Server) rpcArenaBoard(ctx context.Context, logger runtime.Logger, db *sql.DB, nk runtime.NakamaModule, payload string) (string, error) {
	userID, err := userFrom(ctx)
	if err != nil {
		return "", err
	}
	var output string
	err = withTx(ctx, db, func(tx *sql.Tx) error {
		if _, err := s.lockPlayer(ctx, tx, userID); err != nil {
			return err
		}
		points, wins, losses, err := s.arenaRow(ctx, tx, userID)
		if err != nil {
			return err
		}
		rank, err := arenaRank(ctx, tx, points)
		if err != nil {
			return err
		}
		paid, err := arenaPaidWinsToday(ctx, tx, userID)
		if err != nil {
			return err
		}
		rows, err := tx.QueryContext(ctx, `SELECT a.user_id::text, a.points, COALESCE(`+sqlPlayerName+`, ''),
				COALESCE(h.archetype_id, ''), COALESCE(h.level, 0), COALESCE(h.evolution, 0)
			FROM sd_arena a
			LEFT JOIN users u ON u.id = a.user_id
			LEFT JOIN LATERAL (SELECT archetype_id, level, evolution FROM sd_hero WHERE user_id = a.user_id AND base_slot IS NOT NULL ORDER BY base_slot LIMIT 1) h ON TRUE
			ORDER BY a.points DESC, a.updated_at LIMIT 30`)
		if err != nil {
			return err
		}
		defer rows.Close()
		top := []arenaEntry{}
		for rows.Next() {
			var entry arenaEntry
			var id string
			if err := rows.Scan(&id, &entry.Points, &entry.Name, &entry.ArchetypeID, &entry.Level, &entry.Evolution); err != nil {
				return err
			}
			if entry.Name == "" {
				entry.Name = "Shinobi"
			}
			entry.IsMe = id == userID
			// Equal points share a rank, the same way the player's own rank is counted.
			entry.Rank = len(top) + 1
			if len(top) > 0 && top[len(top)-1].Points == entry.Points {
				entry.Rank = top[len(top)-1].Rank
			}
			top = append(top, entry)
		}
		if err := rows.Err(); err != nil {
			return err
		}
		paidLeft := s.modes.Arena.PaidWinsPerDay - paid
		if paidLeft < 0 {
			paidLeft = 0
		}
		output, err = marshal(map[string]interface{}{
			"points": points, "wins": wins, "losses": losses, "rank": rank, "paidWinsLeft": paidLeft,
			"winGold": s.modes.Arena.WinGold, "winMana": s.modes.Arena.WinMana, "top": top,
		})
		return err
	})
	return output, err
}

// ---------- Daily quests and the daily gift ----------

type questStatus struct {
	ID       string `json:"id"`
	Title    string `json:"title"`
	Target   int    `json:"target"`
	Progress int    `json:"progress"`
	Crystals int64  `json:"crystals"`
	Claimed  bool   `json:"claimed"`
}

type questBoard struct {
	Quests       []questStatus `json:"quests"`
	GiftReady    bool          `json:"giftReady"`
	GiftCrystals int64         `json:"giftCrystals"`
	GiftGold     int64         `json:"giftGold"`
	GiftMana     int64         `json:"giftMana"`
	State        *playerState  `json:"state,omitempty"`
}

func (s *Server) loadQuestBoard(ctx context.Context, tx *sql.Tx, userID string) (*questBoard, error) {
	progress := map[string]int{}
	claimed := map[string]bool{}
	rows, err := tx.QueryContext(ctx, `SELECT quest_id, progress, claimed FROM sd_quest_day WHERE user_id = $1 AND day = `+sqlToday, userID)
	if err != nil {
		return nil, err
	}
	defer rows.Close()
	for rows.Next() {
		var id string
		var amount int
		var done bool
		if err := rows.Scan(&id, &amount, &done); err != nil {
			return nil, err
		}
		progress[id] = amount
		claimed[id] = done
	}
	if err := rows.Err(); err != nil {
		return nil, err
	}
	rows.Close()
	board := &questBoard{Quests: []questStatus{}, GiftCrystals: s.modes.DailyGift.Crystals, GiftGold: s.modes.DailyGift.Gold, GiftMana: s.modes.DailyGift.Mana}
	for _, quest := range s.modes.Quests {
		board.Quests = append(board.Quests, questStatus{quest.ID, quest.Title, quest.Target, progress[quest.ID], quest.Crystals, claimed[quest.ID]})
	}
	err = tx.QueryRowContext(ctx, `SELECT last_gift_day IS NULL OR last_gift_day < `+sqlToday+` FROM sd_player WHERE user_id = $1`, userID).Scan(&board.GiftReady)
	return board, err
}

func (s *Server) rpcQuestBoard(ctx context.Context, logger runtime.Logger, db *sql.DB, nk runtime.NakamaModule, payload string) (string, error) {
	userID, err := userFrom(ctx)
	if err != nil {
		return "", err
	}
	var output string
	err = withTx(ctx, db, func(tx *sql.Tx) error {
		if _, err := s.lockPlayer(ctx, tx, userID); err != nil {
			return err
		}
		board, err := s.loadQuestBoard(ctx, tx, userID)
		if err != nil {
			return err
		}
		output, err = marshal(board)
		return err
	})
	return output, err
}

func (s *Server) rpcQuestClaim(ctx context.Context, logger runtime.Logger, db *sql.DB, nk runtime.NakamaModule, payload string) (string, error) {
	userID, err := userFrom(ctx)
	if err != nil {
		return "", err
	}
	var request struct {
		Key     string `json:"key"`
		QuestID string `json:"questId"`
	}
	if err := decodeStrict(payload, &request); err != nil {
		return "", err
	}
	if !keyPattern.MatchString(request.Key) {
		return "", errBadKey
	}
	quest, ok := s.modes.questByID[request.QuestID]
	if !ok {
		return "", errQuestNotFound
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
		if stored, found, err := replayed(ctx, tx, userID, request.Key, "quest_claim", quest.ID); err != nil {
			return err
		} else if found {
			output = stored
			return nil
		}
		var progress int
		var claimed bool
		err = tx.QueryRowContext(ctx, `SELECT progress, claimed FROM sd_quest_day WHERE user_id = $1 AND day = `+sqlToday+` AND quest_id = $2 FOR UPDATE`, userID, quest.ID).Scan(&progress, &claimed)
		if errors.Is(err, sql.ErrNoRows) {
			return errQuestNotDone
		}
		if err != nil {
			return err
		}
		if claimed {
			return errQuestClaimed
		}
		if progress < quest.Target {
			return errQuestNotDone
		}
		if _, err := tx.ExecContext(ctx, `UPDATE sd_quest_day SET claimed = TRUE WHERE user_id = $1 AND day = `+sqlToday+` AND quest_id = $2`, userID, quest.ID); err != nil {
			return err
		}
		balance := crystals + quest.Crystals
		if _, err := tx.ExecContext(ctx, `UPDATE sd_player SET crystals = $1 WHERE user_id = $2`, balance, userID); err != nil {
			return err
		}
		if err := addLedger(ctx, tx, userID, quest.Crystals, balance, "quest", quest.ID); err != nil {
			return err
		}
		board, err := s.loadQuestBoard(ctx, tx, userID)
		if err != nil {
			return err
		}
		if board.State, err = loadState(ctx, tx, userID, balance); err != nil {
			return err
		}
		if output, err = marshal(board); err != nil {
			return err
		}
		return storeResponse(ctx, tx, userID, request.Key, "quest_claim", quest.ID, output)
	})
	if err != nil {
		return "", err
	}
	return output, nil
}

func (s *Server) rpcGiftOpen(ctx context.Context, logger runtime.Logger, db *sql.DB, nk runtime.NakamaModule, payload string) (string, error) {
	userID, err := userFrom(ctx)
	if err != nil {
		return "", err
	}
	var request struct {
		Key string `json:"key"`
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
		if stored, found, err := replayed(ctx, tx, userID, request.Key, "gift_open", "gift"); err != nil {
			return err
		} else if found {
			output = stored
			return nil
		}
		result, err := tx.ExecContext(ctx, `UPDATE sd_player SET last_gift_day = `+sqlToday+` WHERE user_id = $1 AND (last_gift_day IS NULL OR last_gift_day < `+sqlToday+`)`, userID)
		if err != nil {
			return err
		}
		if changed, _ := result.RowsAffected(); changed != 1 {
			return errGiftOpened
		}
		v, err := s.loadVillage(ctx, tx, userID, crystals)
		if err != nil {
			return err
		}
		beforeGold := v.gold
		gift := s.modes.DailyGift
		payGold(v, gift.Gold)
		v.mana += gift.Mana
		v.crystals += gift.Crystals
		if err := persistVillage(ctx, tx, userID, v, beforeGold, crystals, "daily_gift", request.Key); err != nil {
			return err
		}
		board, err := s.loadQuestBoard(ctx, tx, userID)
		if err != nil {
			return err
		}
		if board.State, err = loadState(ctx, tx, userID, v.crystals); err != nil {
			return err
		}
		if output, err = marshal(board); err != nil {
			return err
		}
		return storeResponse(ctx, tx, userID, request.Key, "gift_open", "gift", output)
	})
	if err != nil {
		return "", err
	}
	return output, nil
}
