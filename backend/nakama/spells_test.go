package main

import (
	"encoding/json"
	"reflect"
	"strconv"
	"strings"
	"testing"
)

// Battle spells (ADR-052): the data file, who may cast what, the cast checks, and what each spell does in the sim.
// These are the same cases as the client's SpellTests, with the same numbers; the shared vectors in battle_test.go
// check whole logs against the client's.

func loadTestSpells(t *testing.T) *spellCatalog {
	t.Helper()
	spells, err := loadSpellCatalog(spellsJSON)
	if err != nil {
		t.Fatal(err)
	}
	return spells
}

func spellLevels(list []raidSpell) string {
	parts := []string{}
	for _, spell := range list {
		parts = append(parts, spell.ID+":"+strconv.Itoa(spell.Level))
	}
	return strings.Join(parts, " ")
}

func eventLine(event []int64) string {
	parts := []string{}
	for _, value := range event {
		parts = append(parts, strconv.FormatInt(value, 10))
	}
	return strings.Join(parts, ",")
}

// eventLines lists the log's entries, all of them or only the given kinds.
func eventLines(result *battleResult, kinds ...int64) []string {
	lines := []string{}
	for _, event := range result.Events {
		wanted := len(kinds) == 0
		for _, kind := range kinds {
			wanted = wanted || event[1] == kind
		}
		if wanted {
			lines = append(lines, eventLine(event))
		}
	}
	return lines
}

// spellLines lists the spell entries and the falls they cause.
func spellLines(result *battleResult) []string {
	return eventLines(result, eventSpellCast, eventSpellPulse, eventSpellHit, eventSpellHitDefender, eventSpellHeal, eventSpellFreeze, eventSpellBoost, eventBuildingDown, eventDefenderDown)
}

func testBuilding(id int, definitionID string, x, z, size int, hp int64) battleBuildingInput {
	return battleBuildingInput{ID: id, DefinitionID: definitionID, X: x, Z: z, Width: size, Depth: size, Level: 1, Hp: hp}
}

func testWall(id, x, z int, hp int64) battleBuildingInput {
	return battleBuildingInput{ID: id, DefinitionID: "wall", X: x, Z: z, Width: 1, Depth: 1, Level: 1, Hp: hp, IsWall: true}
}

func testTower(id, x, z int, hp, attack int64) battleBuildingInput {
	return battleBuildingInput{ID: id, DefinitionID: "defense_tower", X: x, Z: z, Width: 3, Depth: 3, Level: 1, Hp: hp, Attack: attack, Range: 13, IsDefense: true}
}

func testTroop(role string, hp, attack int64, x, z, tick int) battleHeroInput {
	return battleHeroInput{ID: "t", Role: role, Hp: hp, Attack: attack, Defense: 10, X: x, Z: z, IsTroop: true, DeployTick: tick}
}

func testGuard(id, role string, hp, attack, defense int64, x, z int) battleDefenderInput {
	return battleDefenderInput{ID: id, Role: role, Hp: hp, Attack: attack, Defense: defense, X: x, Z: z}
}

func mustRun(t *testing.T, buildings []battleBuildingInput, heroes []battleHeroInput, defenders []battleDefenderInput, endTick int, casts ...battleSpellInput) *battleResult {
	t.Helper()
	result, err := runTimedBattle(buildings, heroes, defenders, endTick, casts)
	if err != nil {
		t.Fatal(err)
	}
	return result
}

func expectLines(t *testing.T, what string, got, want []string) {
	t.Helper()
	if !reflect.DeepEqual(got, want) {
		t.Errorf("%s:\n got %v\nwant %v", what, got, want)
	}
}

// ---------- The data file and who may cast ----------

func TestSpellCatalog_LoadsTheShippedSpells_AndOpensThemByHutLevel(t *testing.T) {
	spells := loadTestSpells(t)
	ids, effects := []string{}, []string{}
	for _, spell := range spells.all {
		ids = append(ids, spell.ID)
		effects = append(effects, spell.Effect)
	}
	if got := strings.Join(ids, " "); got != "ember_rain mending_mist frost_seal war_drum" {
		t.Errorf("ids: %s", got)
	}
	if got := strings.Join(effects, " "); got != "damage heal freeze boost" {
		t.Errorf("effects: %s", got)
	}
	cases := []struct {
		hut  int
		want string
	}{
		{0, ""},
		{1, "ember_rain:1"},
		{2, "ember_rain:2 mending_mist:1"},
		{4, "ember_rain:4 mending_mist:3 frost_seal:2 war_drum:1"},
		{15, "ember_rain:15 mending_mist:14 frost_seal:13 war_drum:12"},
		// A spell stops at the end of its table.
		{99, "ember_rain:15 mending_mist:14 frost_seal:13 war_drum:12"},
	}
	for _, c := range cases {
		if got := spellLevels(spells.available(c.hut)); got != c.want {
			t.Errorf("hut %d: got %q, want %q", c.hut, got, c.want)
		}
	}
	// No Research Hut, no spells; the client still gets a list, not null.
	if text, _ := json.Marshal(raidStartResponse{Spells: spells.available(0)}); !strings.Contains(string(text), `"spells":[]`) {
		t.Errorf("an empty list should be sent as []: %s", text)
	}

	// Every spell has a level for every hut level from its unlock to the hut's last, and each level is stronger.
	buildings, err := loadBuildingCatalog(buildingsJSON)
	if err != nil {
		t.Fatal(err)
	}
	hut := buildings.byID["research_hut"]
	if hut == nil || hut.Function != "Research" {
		t.Fatal("no research hut in buildings.json")
	}
	for _, spell := range spells.all {
		if spell.UnlockHutLevel+len(spell.Power)-1 != hut.maxLevel() {
			t.Errorf("%s: unlock %d with %d levels does not end at hut level %d", spell.ID, spell.UnlockHutLevel, len(spell.Power), hut.maxLevel())
		}
		for level := 2; level <= len(spell.Power); level++ {
			if spell.powerAt(level) <= spell.powerAt(level-1) {
				t.Errorf("%s: level %d is not stronger than the one before", spell.ID, level)
			}
		}
	}
}

func oneSpell(schemaVersion int, change func(spell map[string]any)) []byte {
	spell := map[string]any{"id": "spark", "effect": "damage", "unlockHutLevel": 1, "radius": 300, "durationTicks": 30, "pulses": 3, "power": []int{10, 20}}
	if change != nil {
		change(spell)
	}
	text, _ := json.Marshal(map[string]any{"schemaVersion": schemaVersion, "spells": []any{spell}})
	return text
}

const sparkJSON = `{"id":"spark","effect":"damage","unlockHutLevel":1,"radius":300,"durationTicks":30,"pulses":3,"power":[10]}`

func TestSpellCatalog_RefusesBadFiles(t *testing.T) {
	if _, err := loadSpellCatalog([]byte(`{"schemaVersion":1,"spells":[` + sparkJSON + `]}`)); err != nil {
		t.Fatalf("one spark should load: %v", err)
	}
	good, err := loadSpellCatalog(oneSpell(1, nil))
	if err != nil || len(good.byID["spark"].Power) != 2 {
		t.Fatalf("the good file should load: %v", err)
	}
	bad := map[string][]byte{
		"a newer file":           oneSpell(2, nil),
		"no id":                  oneSpell(1, func(s map[string]any) { s["id"] = "" }),
		"unknown effect":         oneSpell(1, func(s map[string]any) { s["effect"] = "storm" }),
		"unlock 0":               oneSpell(1, func(s map[string]any) { s["unlockHutLevel"] = 0 }),
		"radius 0":               oneSpell(1, func(s map[string]any) { s["radius"] = 0 }),
		"duration 0":             oneSpell(1, func(s map[string]any) { s["durationTicks"] = 0 }),
		"pulses 0":               oneSpell(1, func(s map[string]any) { s["pulses"] = 0 }),
		"more pulses than ticks": oneSpell(1, func(s map[string]any) { s["pulses"] = 31 }),
		"no power":               oneSpell(1, func(s map[string]any) { s["power"] = []int{} }),
		"power 0":                oneSpell(1, func(s map[string]any) { s["power"] = []int{10, 0} }),
		"a freeze applies once":  oneSpell(1, func(s map[string]any) { s["effect"] = "freeze" }),
		"a boost applies once":   oneSpell(1, func(s map[string]any) { s["effect"] = "boost" }),
		"a freeze of no time":    oneSpell(1, func(s map[string]any) { s["effect"], s["pulses"], s["durationTicks"] = "freeze", 1, 5 }),
		"the same id twice":      []byte(`{"schemaVersion":1,"spells":[` + sparkJSON + `,` + sparkJSON + `]}`),
		"a null spell":           []byte(`{"schemaVersion":1,"spells":[null]}`),
		"not json":               []byte(`{`),
	}
	for name, data := range bad {
		if _, err := loadSpellCatalog(data); err == nil {
			t.Errorf("%s: should be refused", name)
		}
	}
	if freeze, err := loadSpellCatalog(oneSpell(1, func(s map[string]any) { s["effect"], s["pulses"], s["power"] = "freeze", 1, []int{100} })); err != nil || len(freeze.all) != 1 {
		t.Errorf("a freeze with one pulse should load: %v", err)
	}
}

// ---------- The cast checks ----------

// The refused lists in the shared vectors: the client's SpellCatalog.CastError gives the same answers.
func TestCastError_MatchesTheClientsRefusals(t *testing.T) {
	var vectors struct {
		RefusedCasts []struct {
			Name      string      `json:"name"`
			Spells    []raidSpell `json:"spells"`
			Casts     []spellCast `json:"casts"`
			BattleEnd int         `json:"battleEnd"`
			Error     string      `json:"error"`
		} `json:"refusedCasts"`
	}
	if err := json.Unmarshal(battleVectorsJSON, &vectors); err != nil {
		t.Fatal(err)
	}
	seen := map[string]bool{}
	for _, refused := range vectors.RefusedCasts {
		seen[refused.Error] = true
		if got := castError(refused.Spells, refused.Casts, refused.BattleEnd); got != refused.Error {
			t.Errorf("%s: got %q, want %q", refused.Name, got, refused.Error)
		}
	}
	for _, name := range []string{"", "SPELL_LOCKED", "SPELL_USED", "BAD_SPELL_CELL", "BAD_DEPLOY_TICK", "DEPLOY_TICK_ORDER"} {
		if !seen[name] {
			t.Errorf("the vectors have no list with the answer %q", name)
		}
	}
}

func TestCastError_NamesEachRefusal(t *testing.T) {
	available := loadTestSpells(t).available(2)
	at := func(id string, x, z, tick int) spellCast { return spellCast{SpellID: id, X: x, Z: z, Tick: tick} }
	cases := []struct {
		want  string
		end   int
		casts []spellCast
	}{
		{"", 300, nil},
		// No casts is always fine, in the old kind of battle too.
		{"", 0, nil},
		{"", 0, []spellCast{}},
		{"", 300, []spellCast{at("ember_rain", 0, 0, 0), at("mending_mist", 49, 49, 299)}},
		// Any cell, buildings included, and two on one tick.
		{"", 300, []spellCast{at("mending_mist", 22, 22, 7), at("ember_rain", 22, 22, 7)}},
		// The hut is too low.
		{"SPELL_LOCKED", 300, []spellCast{at("frost_seal", 5, 5, 0)}},
		{"SPELL_LOCKED", 300, []spellCast{at("meteor", 5, 5, 0)}},
		{"SPELL_LOCKED", 300, []spellCast{at("", 5, 5, 0)}},
		{"SPELL_USED", 300, []spellCast{at("ember_rain", 5, 5, 0), at("ember_rain", 6, 6, 50)}},
		{"BAD_SPELL_CELL", 300, []spellCast{at("ember_rain", -1, 5, 0)}},
		{"BAD_SPELL_CELL", 300, []spellCast{at("ember_rain", 5, -1, 0)}},
		{"BAD_SPELL_CELL", 300, []spellCast{at("ember_rain", battleGridSize, 5, 0)}},
		{"BAD_SPELL_CELL", 300, []spellCast{at("ember_rain", 5, battleGridSize, 0)}},
		{"BAD_SPELL_CELL", 300, []spellCast{at("ember_rain", int(^uint(0)>>1), 5, 0)}},
		{"BAD_DEPLOY_TICK", 300, []spellCast{at("ember_rain", 5, 5, -1)}},
		// Nothing lands once the battle is over.
		{"BAD_DEPLOY_TICK", 300, []spellCast{at("ember_rain", 5, 5, 300)}},
		// Only a timed battle has spells.
		{"BAD_DEPLOY_TICK", 0, []spellCast{at("ember_rain", 5, 5, 0)}},
		{"BAD_DEPLOY_TICK", -3, []spellCast{at("ember_rain", 5, 5, 0)}},
		{"BAD_DEPLOY_TICK", battleTickLimit + 1, []spellCast{at("ember_rain", 5, 5, 0)}},
		{"DEPLOY_TICK_ORDER", 300, []spellCast{at("ember_rain", 5, 5, 50), at("mending_mist", 5, 5, 49)}},
		// The first problem in the list is the one named, and for one cast: locked, used, cell, tick, order.
		{"SPELL_LOCKED", 300, []spellCast{at("war_drum", -1, 99, 999)}},
		{"SPELL_USED", 300, []spellCast{at("ember_rain", 5, 5, 10), at("ember_rain", -1, 5, 5)}},
		{"BAD_SPELL_CELL", 300, []spellCast{at("ember_rain", 5, 5, 10), at("mending_mist", 50, 5, 5)}},
		{"BAD_DEPLOY_TICK", 300, []spellCast{at("ember_rain", 5, 5, 10), at("mending_mist", 5, 5, -1)}},
	}
	for _, c := range cases {
		if got := castError(available, c.casts, c.end); got != c.want {
			t.Errorf("%v end %d: got %q, want %q", c.casts, c.end, got, c.want)
		}
	}
	if got := castError(nil, []spellCast{at("ember_rain", 5, 5, 0)}, 300); got != "SPELL_LOCKED" {
		t.Errorf("a raid with no spells: got %q", got)
	}
}

func TestToInputs_WorksOutTheNumbersTheSimUses(t *testing.T) {
	spells := loadTestSpells(t)
	cases := []struct {
		spell string
		level int
		want  battleSpellInput
	}{
		{"ember_rain", 1, battleSpellInput{ID: "ember_rain", Effect: "damage", X: 7, Z: 8, Radius: 300, Pulses: 3, PulseTicks: 10, Power: 120, Ticks: 0, Tick: 12}},
		{"mending_mist", 2, battleSpellInput{ID: "mending_mist", Effect: "heal", X: 7, Z: 8, Radius: 350, Pulses: 5, PulseTicks: 10, Power: 130, Ticks: 0, Tick: 12}},
		{"frost_seal", 1, battleSpellInput{ID: "frost_seal", Effect: "freeze", X: 7, Z: 8, Radius: 350, Pulses: 1, PulseTicks: 40, Power: 0, Ticks: 40, Tick: 12}},
		// 160 percent of 40 ticks.
		{"frost_seal", 13, battleSpellInput{ID: "frost_seal", Effect: "freeze", X: 7, Z: 8, Radius: 350, Pulses: 1, PulseTicks: 40, Power: 0, Ticks: 64, Tick: 12}},
		{"war_drum", 1, battleSpellInput{ID: "war_drum", Effect: "boost", X: 7, Z: 8, Radius: 400, Pulses: 1, PulseTicks: 50, Power: 25, Ticks: 50, Tick: 12}},
		{"war_drum", 12, battleSpellInput{ID: "war_drum", Effect: "boost", X: 7, Z: 8, Radius: 400, Pulses: 1, PulseTicks: 50, Power: 80, Ticks: 50, Tick: 12}},
		// A level outside the table counts as the nearest one in it.
		{"ember_rain", 0, battleSpellInput{ID: "ember_rain", Effect: "damage", X: 7, Z: 8, Radius: 300, Pulses: 3, PulseTicks: 10, Power: 120, Ticks: 0, Tick: 12}},
		{"ember_rain", 40, battleSpellInput{ID: "ember_rain", Effect: "damage", X: 7, Z: 8, Radius: 300, Pulses: 3, PulseTicks: 10, Power: 1100, Ticks: 0, Tick: 12}},
	}
	for _, c := range cases {
		if got := spells.byID[c.spell].toInput(c.level, 7, 8, 12); got != c.want {
			t.Errorf("%s level %d: got %+v, want %+v", c.spell, c.level, got, c.want)
		}
		if err := checkSpell(c.want); err != nil {
			t.Errorf("%s level %d: the sim would refuse it: %v", c.spell, c.level, err)
		}
	}

	// The level is the one the raid has the spell at, and the order sent is kept.
	granted := spells.available(4)
	inputs, err := spells.toInputs(granted, []spellCast{{SpellID: "war_drum", X: 1, Z: 2, Tick: 9}, {SpellID: "ember_rain", X: 3, Z: 4, Tick: 5}})
	if err != nil || len(inputs) != 2 || inputs[0].ID != "war_drum" || inputs[0].Power != 25 || inputs[0].Tick != 9 || inputs[1].Power != 330 || inputs[1].X != 3 {
		t.Errorf("got %+v, %v", inputs, err)
	}
	if _, err := spells.toInputs(spells.available(1), []spellCast{{SpellID: "war_drum"}}); err == nil {
		t.Error("a spell the raid does not have should be an error")
	}
	if _, err := spells.toInputs(granted, []spellCast{{SpellID: "meteor"}}); err == nil {
		t.Error("a spell that is not in the game should be an error")
	}
	if none, err := spells.toInputs(granted, nil); err != nil || len(none) != 0 {
		t.Errorf("no casts: got %v, %v", none, err)
	}
}

// The request hash covers the casts, so a retry with other casts is not taken for the same request; a request
// without casts hashes as it did before spells.
func TestPlacementHash_CoversCasts(t *testing.T) {
	parts := []deployPlacement{{InstanceID: "h1", X: 2, Z: 3, Tick: 4}}
	stacks := []troopStack{{TroopID: "militia", X: 5, Z: 6, Count: 7, Tick: 8}}
	plain := placementHash("raid", parts, stacks, 300, nil)
	if plain != "raid|300|militia:5:6:7:8|h1:2:3:4|" {
		t.Errorf("got %q", plain)
	}
	if placementHash("raid", parts, stacks, 300, []spellCast{}) != plain {
		t.Error("an empty cast list should hash like none")
	}
	one := placementHash("raid", parts, stacks, 300, []spellCast{{SpellID: "ember_rain", X: 9, Z: 10, Tick: 11}})
	if one != plain+"spell:ember_rain:9:10:11|" {
		t.Errorf("got %q", one)
	}
	if other := placementHash("raid", parts, stacks, 300, []spellCast{{SpellID: "ember_rain", X: 9, Z: 10, Tick: 12}}); other == one {
		t.Error("another tick should hash differently")
	}
}

// ---------- What each spell does ----------

func TestEmberRain_HitsBuildingsWallsAndGuardsInTheRing_InThreePulses(t *testing.T) {
	buildings := []battleBuildingInput{
		testBuilding(1, "dojo_hq", 30, 30, 4, 5000),
		testBuilding(2, "gold_vault", 10, 10, 3, 1000),
		testWall(3, 11, 14, 300),
		testBuilding(4, "gold_mine", 16, 10, 3, 1000),
	}
	// The first guard stands exactly three cells from the middle of the cast cell, on the edge of the ring.
	guards := []battleDefenderInput{testGuard("near", "Tank", 1000, 50, 30, 14, 11), testGuard("far", "Tank", 1000, 50, 30, 15, 11)}
	result := mustRun(t, buildings, nil, guards, 40, loadTestSpells(t).byID["ember_rain"].toInput(1, 11, 11, 0))
	expectLines(t, "log", eventLines(result), []string{
		"1,20,0,11,11,0",   // the cast lands on the tick after it was made
		"1,21,0,1,3,300",   // first of three pulses, radius 300
		"1,22,0,2,120,880", // the vault
		"1,22,0,3,120,180", // the wall
		"1,23,0,0,92,908",  // the guard: 120 * 100 / (100 + 30 defence)
		"11,21,0,2,3,300",
		"11,22,0,2,120,760",
		"11,22,0,3,120,60",
		"11,23,0,0,92,816",
		"21,21,0,3,3,300",
		"21,22,0,2,120,640",
		"21,22,0,3,60,0", // never more than the HP left
		"21,3,3,0,0,0",   // the wall falls
		"21,23,0,0,92,724",
	})
	// 360 of 7000; the wall counts for nothing.
	if result.EndTick != 40 || result.DamagePercent != 5 || result.DefendersAlive != 2 {
		t.Errorf("got end=%d damage=%d guards=%d", result.EndTick, result.DamagePercent, result.DefendersAlive)
	}
}

func TestEmberRain_CanFellGuards(t *testing.T) {
	buildings := []battleBuildingInput{testBuilding(1, "dojo_hq", 30, 30, 4, 5000)}
	guards := []battleDefenderInput{testGuard("g", "Tank", 150, 50, 0, 11, 11)}
	result := mustRun(t, buildings, nil, guards, 40, loadTestSpells(t).byID["ember_rain"].toInput(1, 10, 11, 0))
	expectLines(t, "log", eventLines(result), []string{"1,20,0,10,11,0", "1,21,0,1,3,300", "1,23,0,0,120,30", "11,21,0,2,3,300", "11,23,0,0,30,0", "11,12,0,0,0,0", "21,21,0,3,3,300"})
	if result.DefendersAlive != 0 {
		t.Errorf("guards left: %d", result.DefendersAlive)
	}
}

func TestEmberRain_EndsTheBattle_WhenTheLastBuildingFalls(t *testing.T) {
	buildings := []battleBuildingInput{testBuilding(1, "dojo_hq", 20, 20, 4, 200), testWall(2, 30, 30, 300)}
	result := mustRun(t, buildings, nil, nil, 300, loadTestSpells(t).byID["ember_rain"].toInput(1, 21, 21, 0))
	// The second pulse brings it down, and a standing wall does not keep the battle going.
	if result.EndTick != 11 || result.DamagePercent != 100 || result.Stars != 3 || !result.DojoDestroyed {
		t.Errorf("got end=%d damage=%d stars=%d dojo=%v", result.EndTick, result.DamagePercent, result.Stars, result.DojoDestroyed)
	}
	expectLines(t, "log", eventLines(result), []string{"1,20,0,21,21,0", "1,21,0,1,3,300", "1,22,0,1,120,80", "11,21,0,2,3,300", "11,22,0,1,80,0", "11,3,1,0,0,0"})
}

func TestMendingMist_HealsHurtUnitsInTheRing_NeverPastFull(t *testing.T) {
	// Each troop stands by its own tower, too far from the other one to be shot by it.
	buildings := []battleBuildingInput{testTower(1, 10, 10, 1000000, 200), testTower(2, 30, 30, 1000000, 200)}
	team := []battleHeroInput{testTroop("Fighter", 30000, 10, 15, 11, 0), testTroop("Fighter", 30000, 10, 35, 31, 0)}
	result := mustRun(t, buildings, team, nil, 60, loadTestSpells(t).byID["mending_mist"].toInput(1, 14, 11, 0))

	pulses, heals := []int64{}, []int64{}
	hp := []int64{30000, 30000}
	for _, event := range result.Events {
		switch event[1] {
		case eventSpellPulse:
			pulses = append(pulses, event[0])
		case eventTowerHit:
			hp[event[3]] = event[5]
		case eventSpellHeal:
			if event[3] != 0 {
				t.Errorf("only the troop in the ring is healed: %s", eventLine(event))
			}
			now := hp[0] + 80
			if now > 30000 {
				now = 30000
			}
			if event[4] != now-hp[0] || event[5] != now {
				t.Errorf("heal %s, expected %d up to %d", eventLine(event), now-hp[0], now)
			}
			hp[0] = now
			heals = append(heals, event[0])
		}
	}
	if !reflect.DeepEqual(pulses, []int64{1, 11, 21, 31, 41}) {
		t.Errorf("pulses on ticks %v", pulses)
	}
	// At full HP on the first pulse there is nothing to heal, so nothing is logged.
	if !reflect.DeepEqual(heals, []int64{11, 21, 31, 41}) {
		t.Errorf("heals on ticks %v", heals)
	}
	if hp[1] >= 30000 {
		t.Error("the far troop should have been hurt, and got nothing")
	}
}

func firstTick(result *battleResult, match func(event []int64) bool) int64 {
	for _, event := range result.Events {
		if match(event) {
			return event[0]
		}
	}
	return -1
}

func TestFrostSeal_StopsATowerForItsLength(t *testing.T) {
	field := func() []battleBuildingInput {
		return []battleBuildingInput{testTower(1, 10, 10, 1000000, 60), testBuilding(2, "gold_mine", 10, 14, 3, 1000000), testTower(3, 30, 30, 1000000, 60)}
	}
	team := func() []battleHeroInput { return []battleHeroInput{testTroop("Tank", 100000, 1, 15, 11, 0)} }
	spell := loadTestSpells(t).byID["frost_seal"]
	firstShot := func(result *battleResult) int64 {
		return firstTick(result, func(e []int64) bool { return e[1] == eventTowerHit && e[2] == 1 })
	}

	if got := firstShot(mustRun(t, field(), team(), nil, 120)); got != 1 {
		t.Errorf("without the spell the tower shoots at once, not on tick %d", got)
	}
	frozen := mustRun(t, field(), team(), nil, 120, spell.toInput(1, 11, 11, 0))
	// Only the tower in the ring: not the mine beside it, and not the tower far away.
	expectLines(t, "frozen", spellLines(frozen), []string{"1,20,0,11,11,2", "1,21,0,1,1,350", "1,25,0,0,1,40"})
	if got := firstShot(frozen); got != 41 {
		t.Errorf("frozen on ticks 1 to 40, so shooting again on tick 41, not %d", got)
	}
	longer := mustRun(t, field(), team(), nil, 120, spell.toInput(13, 11, 11, 0))
	expectLines(t, "longer", spellLines(longer), []string{"1,20,0,11,11,2", "1,21,0,1,1,350", "1,25,0,0,1,64"})
	if got := firstShot(longer); got != 65 {
		t.Errorf("the last level holds to tick 64, not %d", got-1)
	}
}

func TestFrostSeal_StopsGuardsInTheRing(t *testing.T) {
	field := func() []battleBuildingInput {
		return []battleBuildingInput{testBuilding(1, "dojo_hq", 30, 30, 4, 1000000)}
	}
	guards := func() []battleDefenderInput {
		return []battleDefenderInput{testGuard("in", "Fighter", 5000, 50, 30, 13, 13), testGuard("out", "Fighter", 5000, 50, 30, 10, 15)}
	}
	team := func() []battleHeroInput { return []battleHeroInput{testTroop("Tank", 100000, 1, 10, 10, 0)} }
	firstAct := func(result *battleResult, guard int64) int64 {
		return firstTick(result, func(e []int64) bool {
			return (e[1] == eventDefenderMove || e[1] == eventDefenderHit || e[1] == eventDefenderAbility || e[1] == eventDefenderHeal) && e[2] == guard
		})
	}

	plain := mustRun(t, field(), team(), guards(), 120)
	if firstAct(plain, 0) != 1 || firstAct(plain, 1) != 1 {
		t.Errorf("without the spell both guards act at once: %d, %d", firstAct(plain, 0), firstAct(plain, 1))
	}
	frozen := mustRun(t, field(), team(), guards(), 120, loadTestSpells(t).byID["frost_seal"].toInput(1, 13, 13, 0))
	expectLines(t, "frozen", spellLines(frozen), []string{"1,20,0,13,13,2", "1,21,0,1,1,350", "1,25,0,1,0,40"})
	if got := firstAct(frozen, 0); got != 41 {
		t.Errorf("the guard in the ring does nothing on ticks 1 to 40, but acted on %d", got)
	}
	if got := firstAct(frozen, 1); got != 1 {
		t.Errorf("the guard outside the ring is not held, but first acted on %d", got)
	}
}

func TestWarDrum_RaisesDamageForItsLength_ForUnitsInTheRingWhenItLands(t *testing.T) {
	field := func() []battleBuildingInput {
		return []battleBuildingInput{testBuilding(1, "dojo_hq", 12, 10, 4, 1000000)}
	}
	// The second troop lands after the drum, on the same cell, and gets nothing.
	team := func() []battleHeroInput {
		return []battleHeroInput{testTroop("Fighter", 1000, 100, 9, 11, 0), testTroop("Fighter", 1000, 100, 9, 11, 5)}
	}
	spell := loadTestSpells(t).byID["war_drum"]
	// hits counts a unit's hits by damage, up to a tick and after it.
	hits := func(result *battleResult, unit int64, split int64) (early, late map[int64]int) {
		early, late = map[int64]int{}, map[int64]int{}
		for _, e := range result.Events {
			if e[1] != eventHeroHit || e[2] != unit {
				continue
			}
			if e[0] <= split {
				early[e[4]]++
			} else {
				late[e[4]]++
			}
		}
		return early, late
	}
	only := func(counts map[int64]int, damage int64) bool { return len(counts) == 1 && counts[damage] > 0 }

	if early, late := hits(mustRun(t, field(), team(), nil, 120), 0, 50); !only(early, 100) || !only(late, 100) {
		t.Errorf("a fighter troop with 100 attack hits for 100: %v, %v", early, late)
	}
	drummed := mustRun(t, field(), team(), nil, 120, spell.toInput(1, 9, 11, 0))
	expectLines(t, "drummed", spellLines(drummed), []string{"1,20,0,9,11,3", "1,21,0,1,1,400", "1,26,0,0,50,25"})
	if early, late := hits(drummed, 0, 50); !only(early, 125) || !only(late, 100) {
		t.Errorf("25 percent more on ticks 1 to 50, and back to normal from tick 51: %v, %v", early, late)
	}
	if early, late := hits(drummed, 1, 50); !only(early, 100) || !only(late, 100) {
		t.Errorf("the troop that landed later gets nothing: %v, %v", early, late)
	}
	if early, _ := hits(mustRun(t, field(), team(), nil, 120, spell.toInput(12, 9, 11, 0)), 0, 50); !only(early, 180) {
		t.Errorf("80 percent more at the last level: %v", early)
	}
}

// ---------- Timed battles ----------

// A cast and a card dropped on the same tick land together, the card first, so the spell reaches it.
func TestCastAndCard_OnTheSameTick_LandTogether_TheCardFirst(t *testing.T) {
	buildings := loadTestRaids(t).byID["ridge_outpost"].expanded
	drum := loadTestSpells(t).byID["war_drum"].toInput(1, 10, 10, 10)
	result := mustRun(t, buildings, []battleHeroInput{testTroop("Fighter", 600, 40, 10, 10, 10)}, nil, 200, drum)
	expectLines(t, "log", eventLines(result)[:4], []string{"11,19,0,10,10,0", "11,20,0,10,10,3", "11,21,0,1,1,400", "11,26,0,0,50,25"})
}

// Casts are numbered in the order sent, also when two share a tick, and a later tick sent first is put in order.
func TestCasts_AreNumberedInTheOrderTheyLand(t *testing.T) {
	buildings := []battleBuildingInput{testBuilding(1, "dojo_hq", 30, 30, 4, 1000000)}
	spells := loadTestSpells(t)
	result := mustRun(t, buildings, nil, nil, 30,
		spells.byID["war_drum"].toInput(1, 1, 1, 7),
		spells.byID["frost_seal"].toInput(1, 2, 2, 7),
		spells.byID["mending_mist"].toInput(1, 3, 3, 2))
	expectLines(t, "casts", eventLines(result, eventSpellCast), []string{"3,20,0,3,3,1", "8,20,1,1,1,3", "8,20,2,2,2,2"})
}

func TestSpells_NeedATimedBattle_AndSaneNumbers(t *testing.T) {
	buildings := loadTestRaids(t).byID["ridge_outpost"].expanded
	team := []battleHeroInput{{ID: "a", Role: "Tank", Hp: 1500, Attack: 60, Defense: 60, X: 10, Z: 10}, {ID: "d", Role: "Mage", Hp: 800, Attack: 100, Defense: 25, X: 13, Z: 10}}
	spell := loadTestSpells(t).byID["ember_rain"].toInput(1, 10, 10, 0)

	// The all-at-once kind has no spells.
	if _, err := runTimedBattle(buildings, team, nil, 0, []battleSpellInput{spell}); err == nil {
		t.Error("a battle without an end tick should refuse casts")
	}
	old, err := runBattle(buildings, team)
	if err != nil {
		t.Fatal(err)
	}
	if mustRun(t, buildings, team, nil, 0).Checksum != old.Checksum {
		t.Error("no casts, the same log as before spells existed")
	}
	timed := mustRun(t, buildings, team, nil, 300)
	if again, err := runTimedBattle(buildings, team, nil, 300, []battleSpellInput{}); err != nil || again.Checksum != timed.Checksum {
		t.Errorf("an empty cast list should change nothing: %v", err)
	}
	if mustRun(t, buildings, team, nil, 300, spell).Checksum == timed.Checksum {
		t.Error("a cast should change the log")
	}

	bad := map[string]func(s *battleSpellInput){
		"effect":     func(s *battleSpellInput) { s.Effect = "storm" },
		"pulses":     func(s *battleSpellInput) { s.Pulses = 0 },
		"pulseTicks": func(s *battleSpellInput) { s.PulseTicks = 0 },
		"radius":     func(s *battleSpellInput) { s.Radius = -1 },
		"power":      func(s *battleSpellInput) { s.Power = -1 },
		"ticks":      func(s *battleSpellInput) { s.Ticks = -1 },
	}
	for name, change := range bad {
		input := spell
		change(&input)
		if _, err := runTimedBattle(buildings, nil, nil, 300, []battleSpellInput{input}); err == nil {
			t.Errorf("a spell with a bad %s should be refused", name)
		}
	}
	if _, err := effectNumber("storm"); err == nil {
		t.Error("storm is not an effect")
	}
	if number, err := effectNumber("boost"); err != nil || number != effectBoost {
		t.Errorf("boost: %d, %v", number, err)
	}
}
