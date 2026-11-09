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
			// BattleEnd is the end tick of a timed battle (ADR-050); 0 for the old all-at-once kind.
			BattleEnd int `json:"battleEnd"`
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
		result, err := runTimedBattle(buildings, scenario.Team, scenario.Defenders, scenario.BattleEnd)
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
		if got := deployError(buildings, c.heroes, 0); got != c.want {
			t.Errorf("%v: got %q, want %q", c.heroes, got, c.want)
		}
	}
}

// A live battle's drops (ADR-050): ticks inside the battle and rising, and only a timed battle may use them.
func TestDeployError_ChecksDeployTicks(t *testing.T) {
	buildings := loadTestRaids(t).byID["ridge_outpost"].expanded
	at := func(id string, x, z, tick int, troop bool) battleHeroInput {
		return battleHeroInput{ID: id, Role: "Tank", Hp: 1, Attack: 1, X: x, Z: z, IsTroop: troop, DeployTick: tick}
	}
	cases := []struct {
		want    string
		endTick int
		heroes  []battleHeroInput
	}{
		{"", 300, []battleHeroInput{at("a", 2, 2, 0, false), at("b", 3, 3, 120, false), at("t", 2, 2, 5, true), at("t", 2, 2, 299, true)}},
		{"BAD_DEPLOY_TICK", 0, []battleHeroInput{at("a", 2, 2, 5, false)}},
		{"BAD_DEPLOY_TICK", 300, []battleHeroInput{at("a", 2, 2, -1, false)}},
		{"BAD_DEPLOY_TICK", 300, []battleHeroInput{at("a", 2, 2, 300, false)}},
		{"BAD_DEPLOY_TICK", battleTickLimit + 1, []battleHeroInput{at("a", 2, 2, 0, false)}},
		{"BAD_DEPLOY_TICK", -5, []battleHeroInput{at("a", 2, 2, 0, false)}},
		{"DEPLOY_TICK_ORDER", 300, []battleHeroInput{at("a", 2, 2, 50, false), at("b", 3, 3, 40, false)}},
		{"DEPLOY_TICK_ORDER", 300, []battleHeroInput{at("t", 2, 2, 50, true), at("t", 3, 3, 40, true)}},
		// A hero may land where another landed earlier, once it has walked off; never two at once.
		{"", 300, []battleHeroInput{at("a", 2, 2, 10, false), at("b", 2, 2, 11, false)}},
		{"DEPLOY_CELL_TAKEN", 300, []battleHeroInput{at("a", 2, 2, 10, false), at("b", 2, 2, 10, false)}},
	}
	for _, c := range cases {
		if got := deployError(buildings, c.heroes, c.endTick); got != c.want {
			t.Errorf("%v end %d: got %q, want %q", c.heroes, c.endTick, got, c.want)
		}
	}
}

// Units are numbered by landing: by tick, heroes before troops on a tick, else as listed.
func TestLandingOrder_SortsByTickThenHeroesFirst(t *testing.T) {
	list := []battleHeroInput{{ID: "h1", DeployTick: 30}, {ID: "h2", DeployTick: 30}, {ID: "t1", IsTroop: true, DeployTick: 0}, {ID: "t2", IsTroop: true, DeployTick: 30}}
	got := ""
	for _, h := range landingOrder(list) {
		got += h.ID + " "
	}
	if got != "t1 h1 h2 t2 " {
		t.Errorf("got %q", got)
	}
}
