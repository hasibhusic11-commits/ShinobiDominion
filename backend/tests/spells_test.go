package backendtests

import (
	"encoding/json"
	"testing"
)

// Battle spells (ADR-052): the attacker's Research Hut opens them, the raid session lists them, each may be cast
// once in a live battle, and the server fights the casts again with everything else.

type raidSpellView struct {
	ID    string `json:"id"`
	Level int    `json:"level"`
}

// spellStart is a raid session with the spells it may cast.
type spellStart struct {
	raidStart
	Spells []raidSpellView `json:"spells"`
}

// startSpellRaid starts the scripted raid, and also gives the spell list as it was sent.
func startSpellRaid(t *testing.T, p *player) (spellStart, string) {
	t.Helper()
	body, err := p.rpc("sd_raid_start", map[string]interface{}{"key": "k-" + randomHex(8), "raidId": "ridge_outpost"})
	if err != nil {
		t.Fatal(err)
	}
	var start spellStart
	if err := json.Unmarshal([]byte(body), &start); err != nil {
		t.Fatal(err)
	}
	var fields map[string]json.RawMessage
	if err := json.Unmarshal([]byte(body), &fields); err != nil {
		t.Fatal(err)
	}
	return start, string(fields["spells"])
}

// spellReady is a player with a hero on a base and a Research Hut of the given level: 0 for none, or 1 or 2, which
// are both built at once with no timer.
func spellReady(t *testing.T, hutLevel int) *player {
	t.Helper()
	p := raidReady(t, "river_scout", 1, 0)
	if hutLevel < 1 {
		return p
	}
	grant(t, p, map[string]interface{}{"setGold": 20000})
	// The hut needs a level 2 Town Hall.
	dojo := findBuilding(mustState(t, p), "dojo_hq")
	if _, err := buildingOp(p, "sd_village_upgrade", "k-"+randomHex(8), dojo.ID); err != nil {
		t.Fatal(err)
	}
	hut := findBuilding(placeAt(t, p, "research_hut", 40, 40), "research_hut")
	if hut == nil {
		t.Fatal("the hut was not built")
	}
	for level := 2; level <= hutLevel; level++ {
		s, err := buildingOp(p, "sd_village_upgrade", "k-"+randomHex(8), hut.ID)
		if err != nil {
			t.Fatal(err)
		}
		if b := buildingByID(s, hut.ID); b == nil || b.Level != level {
			t.Fatalf("the hut should be level %d now: %+v", level, b)
		}
	}
	return p
}

func spellCastAt(spell string, x, z, tick int) map[string]interface{} {
	return map[string]interface{}{"spellId": spell, "x": x, "z": z, "tick": tick}
}

// spellDeploy sends a fought battle with its casts. endTick 0 is the old all-at-once kind, which has no end tick.
func spellDeploy(p *player, key, attackID string, placements []map[string]interface{}, endTick int, casts ...map[string]interface{}) (raidDeploy, error) {
	var result raidDeploy
	payload := map[string]interface{}{"key": key, "attackId": attackID, "placements": placements}
	if endTick > 0 {
		payload["endTick"] = endTick
	}
	if len(casts) > 0 {
		payload["casts"] = casts
	}
	body, err := p.rpc("sd_raid_deploy", payload)
	if err != nil {
		return result, err
	}
	return result, json.Unmarshal([]byte(body), &result)
}

func TestSpells_NoResearchHut_NoSpells(t *testing.T) {
	p := spellReady(t, 0)
	start, sent := startSpellRaid(t, p)
	if len(start.Spells) != 0 || sent != "[]" {
		t.Fatalf("a player without a hut gets an empty spell list, never a missing one: %q", sent)
	}
	_, err := spellDeploy(p, "k-"+randomHex(8), start.AttackID, placementsFor(start.raidStart), 20, spellCastAt("ember_rain", 23, 23, 0))
	expectError(t, err, "SPELL_LOCKED")
	// The refusal changed nothing: the same battle without the cast is still taken.
	if _, err := spellDeploy(p, "k-"+randomHex(8), start.AttackID, placementsFor(start.raidStart), 20); err != nil {
		t.Fatal(err)
	}
}

// A level 1 hut opens Ember Rain at level 1. The server fights the cast: its log shows the spell landing on the tick
// after it was cast and each of its three pulses hurting the buildings in the ring.
func TestSpells_EmberRain_IsFoughtAgainByTheServer(t *testing.T) {
	p := spellReady(t, 1)
	start, _ := startSpellRaid(t, p)
	if len(start.Spells) != 1 || start.Spells[0] != (raidSpellView{"ember_rain", 1}) {
		t.Fatalf("a level 1 hut opens ember rain at level 1: %+v", start.Spells)
	}
	// The outpost's Town Hall stands on cells 22 to 25. The cast is on one of them: a spell may land on a building.
	hall := -1
	for _, b := range start.Buildings {
		if b.X == 22 && b.Z == 22 && b.Width == 4 {
			hall = b.ID
		}
	}
	if hall < 0 {
		t.Fatal("the outpost has no hall at 22,22")
	}
	key := "k-" + randomHex(8)
	result, err := spellDeploy(p, key, start.AttackID, placementsFor(start.raidStart), 30, spellCastAt("ember_rain", 23, 23, 2))
	if err != nil {
		t.Fatal(err)
	}
	if result.Result.EndTick != 30 {
		t.Fatalf("end tick %d, want the 30 the player ended at", result.Result.EndTick)
	}
	casts := 0
	pulses, hallLeft := []int64{}, []int64{}
	for _, e := range result.Result.Events {
		switch e[1] {
		case 20:
			if e[0] != 3 || e[2] != 0 || e[3] != 23 || e[4] != 23 || e[5] != 0 {
				t.Fatalf("cast %v, want cast 0 landing on tick 3 at cell 23,23 as damage", e)
			}
			casts++
		case 21:
			if e[2] != 0 || e[3] != int64(len(pulses)+1) || e[4] != 3 || e[5] != 300 {
				t.Fatalf("pulse %v, want pulse %d of 3 from cast 0 with a reach of 300", e, len(pulses)+1)
			}
			pulses = append(pulses, e[0])
		case 22:
			if e[2] != 0 || e[4] < 1 {
				t.Fatalf("hit %v, want damage from cast 0", e)
			}
			if e[3] == int64(hall) {
				if e[4] != 120 {
					t.Fatalf("hit %v, want the 120 of ember rain at level 1", e)
				}
				hallLeft = append(hallLeft, e[5])
			}
		}
	}
	if casts != 1 || len(pulses) != 3 || pulses[0] != 3 || pulses[1] != 13 || pulses[2] != 23 {
		t.Fatalf("want one cast with pulses on ticks 3, 13 and 23, got %d casts and pulses on %v", casts, pulses)
	}
	if len(hallLeft) != 3 || hallLeft[1] != hallLeft[0]-120 || hallLeft[2] != hallLeft[1]-120 {
		t.Fatalf("each pulse takes 120 from the hall, got HP left %v", hallLeft)
	}

	// The same request again gives the same battle.
	replay, err := spellDeploy(p, key, start.AttackID, placementsFor(start.raidStart), 30, spellCastAt("ember_rain", 23, 23, 2))
	if err != nil || replay.Result.Checksum != result.Result.Checksum {
		t.Fatalf("replay differs: %v", err)
	}
	// The key covers the casts: the same key with the cast on another cell is another request.
	_, err = spellDeploy(p, key, start.AttackID, placementsFor(start.raidStart), 30, spellCastAt("ember_rain", 24, 23, 2))
	expectError(t, err, "KEY_REUSED")
	_, err = spellDeploy(p, "k-"+randomHex(8), start.AttackID, placementsFor(start.raidStart), 30, spellCastAt("ember_rain", 24, 23, 2))
	expectError(t, err, "ATTACK_ALREADY_RESOLVED")
}

func TestSpells_BadCasts_AreRefused_AndChangeNothing(t *testing.T) {
	p := spellReady(t, 2)
	start, _ := startSpellRaid(t, p)
	if len(start.Spells) != 2 || start.Spells[0] != (raidSpellView{"ember_rain", 2}) || start.Spells[1] != (raidSpellView{"mending_mist", 1}) {
		t.Fatalf("a level 2 hut opens ember rain at level 2 and mending mist at level 1: %+v", start.Spells)
	}
	placements := placementsFor(start.raidStart)
	refused := []struct {
		want    string
		endTick int
		casts   []map[string]interface{}
	}{
		// The hut is too low for it, or it is no spell at all.
		{"SPELL_LOCKED", 20, []map[string]interface{}{spellCastAt("frost_seal", 23, 23, 0)}},
		{"SPELL_LOCKED", 20, []map[string]interface{}{spellCastAt("meteor", 23, 23, 0)}},
		{"SPELL_USED", 20, []map[string]interface{}{spellCastAt("ember_rain", 23, 23, 0), spellCastAt("ember_rain", 30, 30, 5)}},
		{"BAD_SPELL_CELL", 20, []map[string]interface{}{spellCastAt("ember_rain", 50, 23, 0)}},
		{"BAD_SPELL_CELL", 20, []map[string]interface{}{spellCastAt("ember_rain", 23, -1, 0)}},
		// Nothing lands once the battle is over, or before it began.
		{"BAD_DEPLOY_TICK", 20, []map[string]interface{}{spellCastAt("ember_rain", 23, 23, 20)}},
		{"BAD_DEPLOY_TICK", 20, []map[string]interface{}{spellCastAt("ember_rain", 23, 23, -1)}},
		// Only a live battle has spells.
		{"BAD_DEPLOY_TICK", 0, []map[string]interface{}{spellCastAt("ember_rain", 23, 23, 0)}},
		{"DEPLOY_TICK_ORDER", 20, []map[string]interface{}{spellCastAt("ember_rain", 23, 23, 5), spellCastAt("mending_mist", 2, 2, 4)}},
	}
	for _, c := range refused {
		_, err := spellDeploy(p, "k-"+randomHex(8), start.AttackID, placements, c.endTick, c.casts...)
		expectError(t, err, c.want)
	}
	// A cast cannot say how strong it is: the level is the server's.
	forged := spellCastAt("ember_rain", 23, 23, 0)
	forged["level"] = 15
	_, err := spellDeploy(p, "k-"+randomHex(8), start.AttackID, placements, 20, forged)
	expectError(t, err, "BAD_REQUEST")

	// None of that changed anything: both spells can still be cast, on the same tick, and the battle is fought.
	result, err := spellDeploy(p, "k-"+randomHex(8), start.AttackID, placements, 30, spellCastAt("ember_rain", 23, 23, 0), spellCastAt("mending_mist", 2, 2, 0))
	if err != nil {
		t.Fatal(err)
	}
	landed := [][]int64{}
	strongest := int64(0)
	for _, e := range result.Result.Events {
		if e[1] == 20 {
			landed = append(landed, e)
		}
		if e[1] == 22 && e[4] > strongest {
			strongest = e[4]
		}
	}
	// Casts are numbered in the order sent: ember rain (damage, 0) is cast 0 and mending mist (heal, 1) is cast 1.
	if len(landed) != 2 || landed[0][0] != 1 || landed[0][2] != 0 || landed[0][5] != 0 || landed[1][0] != 1 || landed[1][2] != 1 || landed[1][3] != 2 || landed[1][4] != 2 || landed[1][5] != 1 {
		t.Fatalf("want ember rain as cast 0 and mending mist as cast 1 at cell 2,2, both landing on tick 1: %v", landed)
	}
	// Ember rain is level 2 here.
	if strongest != 190 {
		t.Fatalf("the strongest hit is %d, want the 190 of ember rain at level 2", strongest)
	}
}
