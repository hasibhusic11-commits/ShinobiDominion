package main

import (
	"context"
	"database/sql"
	"errors"
	"sort"
	"strconv"
	"time"

	"github.com/heroiclabs/nakama-common/runtime"
)

// The Trial Tower (ADR-045): floors of guardian heroes, fought as an Arena-style duel. Each floor has a
// fixed guardian, so a floor is the same every time. The first clear of a floor pays gems and Mana and
// opens the next floor. Replays are free and pay nothing.

var (
	errTrialLocked   = gameError("TRIAL_LOCKED", codePrecondition)
	errTrialNotFound = gameError("TRIAL_NOT_FOUND", codeNotFound)
)

type trialConfig struct {
	DisplayName      string `json:"displayName"`
	Floors           int    `json:"floors"`
	LevelPerFloor    int    `json:"levelPerFloor"`
	PowerPercentBase int64  `json:"powerPercentBase"`
	PowerPercentPerFloor int64 `json:"powerPercentPerFloor"`
	ShrineHpBase     int64  `json:"shrineHpBase"`
	ShrineHpPerFloor int64  `json:"shrineHpPerFloor"`
	CrystalsBase     int64  `json:"crystalsBase"`
	CrystalsPerFloor int64  `json:"crystalsPerFloor"`
	ManaPerFloor     int64  `json:"manaPerFloor"`
}

func (t trialConfig) check() error {
	if t.Floors < 1 || t.LevelPerFloor < 1 || t.PowerPercentBase < 1 || t.PowerPercentPerFloor < 0 || t.ShrineHpBase < 1 || t.ShrineHpPerFloor < 0 || t.CrystalsBase < 0 || t.CrystalsPerFloor < 0 || t.ManaPerFloor < 0 {
		return errors.New("trial tower values are invalid")
	}
	return nil
}

type trialFloor struct {
	Floor       int    `json:"floor"`
	ArchetypeID string `json:"archetypeId"`
	Evolution   int    `json:"evolution"`
	Level       int    `json:"level"`
	ShrineHp    int64  `json:"shrineHp"`
	Crystals    int64  `json:"crystals"`
	Mana        int64  `json:"mana"`
	Cleared     bool   `json:"cleared"`
	Open        bool   `json:"open"`
}

// guardian picks the hero for a floor from the live roster in a fixed order: Commons on the low floors,
// then Elites, then Legendaries, who evolve on the highest floors.
func (s *Server) guardian(floor int) trialFloor {
	t := s.modes.Trials
	ranks := map[string]int{"Common": 0, "Elite": 1, "Legendary": 2}
	heroes := []Archetype{}
	for _, a := range s.catalog.byID {
		if a.ContentComplete && !a.Retired {
			heroes = append(heroes, a)
		}
	}
	sort.Slice(heroes, func(i, j int) bool {
		if ranks[heroes[i].Rarity] != ranks[heroes[j].Rarity] {
			return ranks[heroes[i].Rarity] < ranks[heroes[j].Rarity]
		}
		return heroes[i].ID < heroes[j].ID
	})
	index := (floor - 1) * len(heroes) / t.Floors
	if index >= len(heroes) {
		index = len(heroes) - 1
	}
	pick := heroes[index]
	cumulative := (floor-1)*t.LevelPerFloor + 1
	evolution := 0
	if pick.Rarity == "Legendary" {
		evolution = (cumulative - 1) / 200
		if evolution > 3 {
			evolution = 3
		}
	}
	level := cumulative - 200*evolution
	if level > 200 {
		level = 200
	}
	if level < 1 {
		level = 1
	}
	return trialFloor{Floor: floor, ArchetypeID: pick.ID, Evolution: evolution, Level: level,
		ShrineHp: t.ShrineHpBase + t.ShrineHpPerFloor*int64(floor), Crystals: t.CrystalsBase + t.CrystalsPerFloor*int64(floor), Mana: t.ManaPerFloor * int64(floor)}
}

func trialRaidID(floor int) string { return "trial_" + strconv.Itoa(floor) }

func (s *Server) rpcTrialList(ctx context.Context, logger runtime.Logger, db *sql.DB, nk runtime.NakamaModule, payload string) (string, error) {
	userID, err := userFrom(ctx)
	if err != nil {
		return "", err
	}
	var output string
	err = withTx(ctx, db, func(tx *sql.Tx) error {
		if _, err := s.lockPlayer(ctx, tx, userID); err != nil {
			return err
		}
		floors := []trialFloor{}
		previous := true
		for floor := 1; floor <= s.modes.Trials.Floors; floor++ {
			entry := s.guardian(floor)
			best, err := bestStarsFor(ctx, tx, userID, trialRaidID(floor))
			if err != nil {
				return err
			}
			entry.Cleared = best > 0
			entry.Open = previous
			previous = entry.Cleared
			floors = append(floors, entry)
		}
		output, err = marshal(map[string]interface{}{"floors": floors})
		return err
	})
	return output, err
}

func (s *Server) rpcTrialFight(ctx context.Context, logger runtime.Logger, db *sql.DB, nk runtime.NakamaModule, payload string) (string, error) {
	userID, err := userFrom(ctx)
	if err != nil {
		return "", err
	}
	var request struct {
		Key   string `json:"key"`
		Floor int    `json:"floor"`
	}
	if err := decodeStrict(payload, &request); err != nil {
		return "", err
	}
	if !keyPattern.MatchString(request.Key) {
		return "", errBadKey
	}
	if request.Floor < 1 || request.Floor > s.modes.Trials.Floors {
		return "", errTrialNotFound
	}
	if !s.limiter.Allow(userID, time.Now()) {
		return "", errRateLimited
	}
	floor := s.guardian(request.Floor)
	requestHash := hashRequest(strconv.Itoa(request.Floor))
	a := s.modes.Arena

	var output string
	err = withTx(ctx, db, func(tx *sql.Tx) error {
		crystals, err := s.lockPlayer(ctx, tx, userID)
		if err != nil {
			return err
		}
		if stored, found, err := replayed(ctx, tx, userID, request.Key, "trial_fight", requestHash); err != nil {
			return err
		} else if found {
			output = stored
			return nil
		}
		if request.Floor > 1 {
			below, err := bestStarsFor(ctx, tx, userID, trialRaidID(request.Floor-1))
			if err != nil {
				return err
			}
			if below == 0 {
				return errTrialLocked
			}
		}
		team, err := s.loadRaidTeam(ctx, tx, userID)
		if err != nil {
			return err
		}
		if len(team) == 0 {
			return errNoTeam
		}
		hero := team[0]
		defender, err := s.makeDefender("d1", floor.ArchetypeID, floor.Evolution, floor.Level, a.GuardX, a.GuardZ)
		if err != nil {
			return err
		}
		// Low floors are gentler than the hero's level alone would make them; high floors are tougher.
		power := s.modes.Trials.PowerPercentBase + s.modes.Trials.PowerPercentPerFloor*int64(request.Floor)
		defender.Hp, defender.Attack, defender.Defense = scaled(defender.Hp, power), scaled(defender.Attack, power), scaled(defender.Defense, power)
		shrine := s.buildings.byID["dojo_hq"]
		buildings := []battleBuildingInput{{ID: 1, DefinitionID: shrine.ID, X: a.ShrineX, Z: a.ShrineZ, Width: shrine.Width, Depth: shrine.Depth, Level: 1, Hp: floor.ShrineHp}}
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
		name := "Guardian"
		if archetype, ok := s.catalog.byID[floor.ArchetypeID]; ok {
			name = archetype.DisplayName
		}
		response := modeFightResponse{Mode: "trial", Level: request.Floor, Title: s.modes.Trials.DisplayName + ", floor " + strconv.Itoa(request.Floor),
			Buildings: buildings, Team: team[:1], Placements: []deployPlacement{placement}, Troops: []troopStack{}, TroopPercent: 100,
			Defenders: []raidDefender{defender}, Result: result, Won: won, OpponentName: name}
		best, err := bestStarsFor(ctx, tx, userID, trialRaidID(request.Floor))
		if err != nil {
			return err
		}
		response.BestStars = best
		if won {
			response.Stars = 3
			response.BestStars = 3
			if best == 0 {
				// Only the first clear of a floor pays.
				response.CrystalsEarned = floor.Crystals
				response.ManaEarned = floor.Mana
				v.crystals += floor.Crystals
				v.mana += floor.Mana
				if _, err := tx.ExecContext(ctx, `INSERT INTO sd_raid_progress (user_id, raid_id, best_stars) VALUES ($1, $2, 3)
					ON CONFLICT (user_id, raid_id) DO UPDATE SET best_stars = 3`, userID, trialRaidID(request.Floor)); err != nil {
					return err
				}
			}
		}
		if err := persistVillage(ctx, tx, userID, v, beforeGold, crystals, "trial_reward", request.Key); err != nil {
			return err
		}
		if response.State, err = loadState(ctx, tx, userID, v.crystals); err != nil {
			return err
		}
		if output, err = marshal(response); err != nil {
			return err
		}
		return storeResponse(ctx, tx, userID, request.Key, "trial_fight", requestHash, output)
	})
	if err != nil {
		return "", err
	}
	return output, nil
}
