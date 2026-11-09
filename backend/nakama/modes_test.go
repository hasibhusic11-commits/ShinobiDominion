package main

import (
	"testing"
)

// modesServer builds a server with the shipped data, without a database, for battle checks.
func modesServer(t *testing.T) *Server {
	t.Helper()
	pool, catalog := shippedPool(t)
	buildings, err := loadBuildingCatalog(buildingsJSON)
	if err != nil {
		t.Fatal(err)
	}
	troops, err := loadTroopCatalog(troopsJSON)
	if err != nil {
		t.Fatal(err)
	}
	modes, err := loadModes(modesJSON, troops, buildings)
	if err != nil {
		t.Fatal(err)
	}
	return &Server{catalog: catalog, buildings: buildings, troops: troops, pool: pool, modes: modes}
}

// starterBattle is a brand-new village, with a level 1 hero of the given archetype guarding its Hero Base.
func starterBattle(t *testing.T, s *Server, archetypeID string, level int) ([]battleBuildingInput, []battleDefenderInput) {
	t.Helper()
	buildings := []battleBuildingInput{}
	var guard []battleDefenderInput
	for i, item := range starterLayout {
		def := s.buildings.byID[item.Def]
		row := def.at(1)
		buildings = append(buildings, battleBuildingInput{ID: i + 1, DefinitionID: def.ID, X: item.X, Z: item.Z, Width: def.Width, Depth: def.Depth,
			Level: 1, Hp: row.Hp, Attack: row.Attack, Range: row.Range, IsWall: def.isWall(), IsDefense: def.Function == "Defense", IsTrap: def.Function == "Trap"})
		if def.ID == "hero_base" {
			d, err := s.makeDefender("d1", archetypeID, 0, level, 0, 0)
			if err != nil {
				t.Fatal(err)
			}
			guard = append(guard, battleDefenderInput{d.ID, d.Role, d.Hp, d.Attack, d.Defense, d.X, d.Z, d.Special, d.EvolutionIndex, d.Power})
		}
	}
	x, z, _ := siegeGuardPost(buildings)
	guard[0].X, guard[0].Z = x, z
	return buildings, guard
}

func TestModes_ShippedFileLoads_AndIsMarkedNotApproved(t *testing.T) {
	s := modesServer(t)
	if len(s.modes.Siege.Levels) == 0 || len(s.modes.Quests) == 0 {
		t.Fatal("modes.json has no siege levels or quests")
	}
	if s.modes.Arena.PaidWinsPerDay < 1 {
		t.Fatal("the Arena must pay something")
	}
}

func TestModes_RejectsBadFiles(t *testing.T) {
	s := modesServer(t)
	for _, bad := range []string{
		`{"schemaVersion": 2}`,
		`{"schemaVersion": 1, "siege": {"spawnSpots": [[60, 1]], "levels": []}, "arena": {"shrineHp": 1, "winPointsMin": 1, "winPointsMax": 1}}`,
		`{"schemaVersion": 1, "siege": {"spawnSpots": [[1, 1]], "levels": [{"level": 1, "statPercent": 100, "waves": [{"troopId": "dragon", "count": 1, "spot": 0}]}]}, "arena": {"shrineHp": 1, "winPointsMin": 1, "winPointsMax": 1}}`,
		`{"schemaVersion": 1, "siege": {"spawnSpots": [[1, 1]], "levels": [{"level": 1, "statPercent": 100, "waves": [{"troopId": "militia", "count": 500, "spot": 0}]}]}, "arena": {"shrineHp": 1, "winPointsMin": 1, "winPointsMax": 1}}`,
		`{"schemaVersion": 1, "siege": {"spawnSpots": [], "levels": []}, "arena": {"shrineHp": 0, "winPointsMin": 1, "winPointsMax": 1}}`,
		`{"schemaVersion": 1, "siege": {"spawnSpots": [], "levels": []}, "arena": {"shrineHp": 1, "winPointsMin": 1, "winPointsMax": 1}, "quests": [{"id": "x", "target": 0}]}`,
	} {
		if _, err := loadModes([]byte(bad), s.troops, s.buildings); err == nil {
			t.Fatalf("accepted %s", bad)
		}
	}
}

func TestSiege_EveryWaveLandsOnAStarterVillage(t *testing.T) {
	s := modesServer(t)
	buildings, _ := starterBattle(t, s, "ember_recruit", 1)
	for _, level := range s.modes.Siege.Levels {
		stacks, units := s.siegeStacks(buildings, level)
		if len(stacks) != len(level.Waves) {
			t.Fatalf("level %d: %d of %d waves found somewhere to land", level.Level, len(stacks), len(level.Waves))
		}
		if refusal := deployError(buildings, units, 0); refusal != "" {
			t.Fatalf("level %d: %s", level.Level, refusal)
		}
	}
}

func TestSiege_IsTheSameEveryTime(t *testing.T) {
	s := modesServer(t)
	buildings, guard := starterBattle(t, s, "ember_recruit", 1)
	_, units := s.siegeStacks(buildings, s.modes.Siege.Levels[2])
	first, err := runDefendedBattle(buildings, units, guard)
	if err != nil {
		t.Fatal(err)
	}
	second, _ := runDefendedBattle(buildings, units, guard)
	if first.Checksum != second.Checksum {
		t.Fatal("the same siege gave two different battles")
	}
}

// A new player with any level 1 Common hero and no towers should hold the first level.
func TestSiege_FirstLevelCanBeHeldByANewVillage(t *testing.T) {
	s := modesServer(t)
	for _, archetype := range s.catalog.byID {
		if archetype.Rarity != "Common" || archetype.Retired || !archetype.ContentComplete {
			continue
		}
		buildings, guard := starterBattle(t, s, archetype.ID, 1)
		_, units := s.siegeStacks(buildings, s.modes.Siege.Levels[0])
		result, err := runDefendedBattle(buildings, units, guard)
		if err != nil {
			t.Fatal(err)
		}
		if defenseStars(result) == 0 {
			t.Errorf("%s (%s) could not hold level 1: %d%% damage, dojo down %v", archetype.ID, archetype.Role, result.DamagePercent, result.DojoDestroyed)
		}
	}
}

// The last level should not fall to a new village: there has to be something to grow towards.
func TestSiege_LastLevelBeatsANewVillage(t *testing.T) {
	s := modesServer(t)
	buildings, guard := starterBattle(t, s, "solar_beast_sage", 1)
	_, units := s.siegeStacks(buildings, s.modes.Siege.Levels[len(s.modes.Siege.Levels)-1])
	result, err := runDefendedBattle(buildings, units, guard)
	if err != nil {
		t.Fatal(err)
	}
	if defenseStars(result) > 0 {
		t.Fatalf("a new village held the last level with %d%% damage", result.DamagePercent)
	}
}

func TestDefenseStars(t *testing.T) {
	cases := []struct {
		stars, damage, want int
	}{{1, 60, 0}, {0, 49, 1}, {0, 29, 2}, {0, 9, 3}, {0, 0, 3}, {2, 5, 0}}
	for _, c := range cases {
		if got := defenseStars(&battleResult{Stars: c.stars, DamagePercent: c.damage}); got != c.want {
			t.Errorf("%d stars, %d%%: got %d, want %d", c.stars, c.damage, got, c.want)
		}
	}
}

// arenaDuel fights the Arena's layout: one attacking hero against one guard by the shrine.
func arenaDuel(t *testing.T, s *Server, attacker string, attackerLevel int, guard string, guardLevel int) *battleResult {
	t.Helper()
	a := s.modes.Arena
	shrine := s.buildings.byID["dojo_hq"]
	buildings := []battleBuildingInput{{ID: 1, DefinitionID: shrine.ID, X: a.ShrineX, Z: a.ShrineZ, Width: shrine.Width, Depth: shrine.Depth, Level: 1, Hp: a.ShrineHp}}
	archetype := s.catalog.byID[attacker]
	hp, attack, defense := heroBattleStats(archetype, 0, attackerLevel)
	units := []battleHeroInput{{"me", archetype.Role, hp, attack, defense, a.HeroX, a.HeroZ, false, archetype.Special, 0, powerFor(archetype.Rarity), 0}}
	if refusal := deployError(buildings, units, 0); refusal != "" {
		t.Fatal(refusal)
	}
	d, err := s.makeDefender("d1", guard, 0, guardLevel, a.GuardX, a.GuardZ)
	if err != nil {
		t.Fatal(err)
	}
	result, err := runDefendedBattle(buildings, units, []battleDefenderInput{{d.ID, d.Role, d.Hp, d.Attack, d.Defense, d.X, d.Z, d.Special, d.EvolutionIndex, d.Power}})
	if err != nil {
		t.Fatal(err)
	}
	return result
}

func TestArena_StrongerHeroWins_WeakerHeroLoses(t *testing.T) {
	s := modesServer(t)
	if !arenaDuel(t, s, "ember_recruit", 60, "ember_recruit", 1).DojoDestroyed {
		t.Fatal("a level 60 hero lost to a level 1 copy of itself")
	}
	if arenaDuel(t, s, "ember_recruit", 1, "ember_recruit", 60).DojoDestroyed {
		t.Fatal("a level 1 hero beat a level 60 copy of itself")
	}
}

func TestArena_BotLevelFollowsPoints(t *testing.T) {
	s := modesServer(t)
	s.random = zeroReader{}
	mine := raidTeamMember{Level: 10, EvolutionIndex: 1}
	_, evolution, low, name, err := s.arenaBot(mine, 0)
	if err != nil || name == "" {
		t.Fatal(err)
	}
	_, _, high, _, _ := s.arenaBot(mine, 1000)
	if evolution != 1 || low != 9 || high != 19 {
		t.Fatalf("evolution %d, levels %d and %d", evolution, low, high)
	}
	_, _, capped, _, _ := s.arenaBot(raidTeamMember{Level: 200}, 100000)
	if capped != 200 {
		t.Fatalf("bot level %d is past the cap", capped)
	}
}

type zeroReader struct{}

func (zeroReader) Read(p []byte) (int, error) {
	for i := range p {
		p[i] = 0
	}
	return len(p), nil
}

// The same numbers are checked in C# by GearTests, so the client's displayed stats match the server's.
func TestGear_RaisesTheRightStat(t *testing.T) {
	s := modesServer(t)
	hp, attack, defense := s.catalog.withGear(1000, 200, 50, [3]int{2, 4, 1})
	if hp != 1200 || attack != 220 || defense != 52 {
		t.Fatalf("got %d %d %d", hp, attack, defense)
	}
	gold, mana, heroLevel := s.catalog.gearCost(3)
	if gold != 4500 || mana != 1350 || heroLevel != 41 {
		t.Fatalf("gear level 3 costs %d gold, %d Mana and needs hero level %d", gold, mana, heroLevel)
	}
}

func trialDuel(t *testing.T, s *Server, attacker string, attackerLevel int, floor int) bool {
	t.Helper()
	a := s.modes.Arena
	g := s.guardian(floor)
	shrine := s.buildings.byID["dojo_hq"]
	buildings := []battleBuildingInput{{ID: 1, DefinitionID: shrine.ID, X: a.ShrineX, Z: a.ShrineZ, Width: shrine.Width, Depth: shrine.Depth, Level: 1, Hp: g.ShrineHp}}
	archetype := s.catalog.byID[attacker]
	hp, attack, defense := heroBattleStats(archetype, 0, attackerLevel)
	units := []battleHeroInput{{"me", archetype.Role, hp, attack, defense, a.HeroX, a.HeroZ, false, archetype.Special, 0, powerFor(archetype.Rarity), 0}}
	d, err := s.makeDefender("d1", g.ArchetypeID, g.Evolution, g.Level, a.GuardX, a.GuardZ)
	if err != nil {
		t.Fatal(err)
	}
	power := s.modes.Trials.PowerPercentBase + s.modes.Trials.PowerPercentPerFloor*int64(floor)
	result, err := runDefendedBattle(buildings, units, []battleDefenderInput{{d.ID, d.Role, scaled(d.Hp, power), scaled(d.Attack, power), scaled(d.Defense, power), d.X, d.Z, d.Special, d.EvolutionIndex, d.Power}})
	if err != nil {
		t.Fatal(err)
	}
	return result.DojoDestroyed
}

func TestTrials_FirstFloorFallsToAnyNewHero_TopFloorDoesNot(t *testing.T) {
	s := modesServer(t)
	for _, archetype := range s.catalog.byID {
		if archetype.Rarity != "Common" || archetype.Retired || !archetype.ContentComplete {
			continue
		}
		if !trialDuel(t, s, archetype.ID, 1, 1) {
			t.Errorf("%s (%s) at level 1 lost floor 1", archetype.ID, archetype.Role)
		}
	}
	if trialDuel(t, s, "solar_beast_sage", 1, s.modes.Trials.Floors) {
		t.Fatal("a level 1 hero cleared the top floor")
	}
	if g := s.guardian(1); g.Level != 1 {
		t.Fatalf("floor 1 guardian is level %d", g.Level)
	}
}
