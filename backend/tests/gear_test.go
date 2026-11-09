package backendtests

import (
	"encoding/json"
	"testing"
)

// Hero gear (ADR-044).

func TestGear_CostsGoldAndMana_NeedsLevels_AndCountsInBattle(t *testing.T) {
	p := raidReady(t, "stone_titan", 1, 0)
	grant(t, p, map[string]interface{}{"setGold": 20000})
	heroID := mustState(t, p).Heroes[0].InstanceID
	before := startRaid(t, p).Team[0]

	gearUp := func(slot int) (state, error) {
		var s state
		body, err := p.rpc("sd_gear_upgrade", map[string]interface{}{"key": "g-" + randomHex(8), "instanceId": heroID, "slot": slot})
		if err == nil {
			json.Unmarshal([]byte(body), &s)
		}
		return s, err
	}
	s0 := mustState(t, p)
	if _, err := gearUp(0); err != nil {
		t.Fatal(err)
	}
	s1 := mustState(t, p)
	if s1.Village.Gold != s0.Village.Gold-1500 || s1.Village.Mana != s0.Village.Mana-150 {
		t.Fatalf("weapon level 1 costs 1,500 gold and 150 Mana: gold %d -> %d, mana %d -> %d", s0.Village.Gold, s1.Village.Gold, s0.Village.Mana, s1.Village.Mana)
	}
	// Level 2 needs hero level 21; this hero is level 1.
	_, err := gearUp(0)
	expectError(t, err, "HERO_LEVEL_TOO_LOW_FOR_GEAR")
	_, err = gearUp(3)
	expectError(t, err, "BAD_GEAR_SLOT")
	_, err = p.rpc("sd_gear_upgrade", map[string]interface{}{"key": "g-" + randomHex(8), "instanceId": "00000000-0000-0000-0000-000000000000", "slot": 0})
	expectError(t, err, "HERO_NOT_FOUND")

	after := startRaid(t, p).Team[0]
	if after.Attack != before.Attack*105/100 || after.Hp != before.Hp {
		t.Fatalf("a level 1 weapon adds 5%% attack and nothing else: %+v -> %+v", before, after)
	}
	ledgersAgree(t, p)
}
