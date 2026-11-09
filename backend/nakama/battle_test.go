package main

import (
	_ "embed"
	"encoding/json"
	"testing"
)

//go:embed testdata/battle_vectors.json
var battleVectorsJSON []byte

func loadTestRaids(t *testing.T) *raidCatalog {
	t.Helper()
	buildings, err := loadBuildingCatalog(buildingsJSON)
	if err != nil {
		t.Fatal(err)
	}
	raids, err := loadRaidCatalog(raidsJSON, buildings)
	if err != nil {
		t.Fatal(err)
	}
	return raids
}

// The same scenarios run through the client's BattleSim, so the two must agree on every event.
func TestBattle_MatchesTheClientsLogExactly(t *testing.T) {
	raids := loadTestRaids(t)
	var vectors struct {
		Scenarios []struct {
			Name          string            `json:"name"`
			Checksum      string            `json:"checksum"`
			Stars         int               `json:"stars"`
			DamagePercent int               `json:"damagePercent"`
			EndTick       int               `json:"endTick"`
			HeroesAlive   int               `json:"heroesAlive"`
			Team          []battleHeroInput `json:"team"`
			Defenders     []battleDefenderInput `json:"defenders"`
			DefendersAlive int              `json:"defendersAlive"`
			ExtraBuildings []battleBuildingInput `json:"extraBuildings"`
		} `json:"scenarios"`
	}
	if err := json.Unmarshal(battleVectorsJSON, &vectors); err != nil {
		t.Fatal(err)
	}
	if len(vectors.Scenarios) == 0 {
		t.Fatal("no scenarios")
	}
	for _, scenario := range vectors.Scenarios {
		buildings := append(append([]battleBuildingInput{}, raids.byID["ridge_outpost"].expanded...), scenario.ExtraBuildings...)
		result, err := runDefendedBattle(buildings, scenario.Team, scenario.Defenders)
		if err != nil {
			t.Fatal(err)
		}
		if result.Checksum != scenario.Checksum || result.Stars != scenario.Stars || result.DamagePercent != scenario.DamagePercent ||
			result.EndTick != scenario.EndTick || result.HeroesAlive != scenario.HeroesAlive || result.DefendersAlive != scenario.DefendersAlive {
			t.Errorf("%s: got stars=%d damage=%d end=%d alive=%d checksum=%s", scenario.Name, result.Stars, result.DamagePercent, result.EndTick, result.HeroesAlive, result.Checksum)
		}
	}
}

func TestDeployError_RefusesIllegalPlacements(t *testing.T) {
	buildings := loadTestRaids(t).byID["ridge_outpost"].expanded
	hero := func(id string, x, z int) battleHeroInput { return battleHeroInput{ID: id, Role: "Tank", Hp: 1, Attack: 1, X: x, Z: z} }
	cases := []struct {
		want   string
		heroes []battleHeroInput
	}{
		{"", []battleHeroInput{hero("a", 2, 2)}},
		{"NO_TEAM", nil},
		{"BAD_DEPLOY_CELL", []battleHeroInput{hero("a", -1, 2)}},
		{"BAD_DEPLOY_CELL", []battleHeroInput{hero("a", 50, 2)}},
		{"BAD_DEPLOY_CELL", []battleHeroInput{hero("a", int(^uint(0)>>1), 2)}},
		{"DEPLOY_TOO_CLOSE", []battleHeroInput{hero("a", 25, 25)}},
		{"DEPLOY_TOO_CLOSE", []battleHeroInput{hero("a", 15, 20)}},
		{"DUPLICATE_HERO", []battleHeroInput{hero("a", 2, 2), hero("a", 3, 3)}},
		{"DEPLOY_CELL_TAKEN", []battleHeroInput{hero("a", 2, 2), hero("b", 2, 2)}},
	}
	for _, c := range cases {
		if got := deployError(buildings, c.heroes); got != c.want {
			t.Errorf("%v: got %q, want %q", c.heroes, got, c.want)
		}
	}
}
