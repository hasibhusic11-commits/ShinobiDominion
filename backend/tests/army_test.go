package backendtests

import (
	"encoding/json"
	"sync"
	"testing"
)

type armyState struct {
	Troops  map[string]int `json:"troops"`
	Village villageView    `json:"village"`
}

func trainTroops(p *player, key, troop string, count int) (armyState, error) {
	var s armyState
	body, err := p.rpc("sd_troop_train", map[string]interface{}{"key": key, "troopId": troop, "count": count})
	if err != nil {
		return s, err
	}
	return s, json.Unmarshal([]byte(body), &s)
}

// armyReady builds a training hall and camps, and leaves plenty of gold.
func armyReady(t *testing.T, camps int) *player {
	t.Helper()
	p := blank(t, 0)
	grant(t, p, map[string]interface{}{"setGold": 20000, "setDojoLevel": 5})
	place := func(def string, x, z int) {
		if _, err := p.rpc("sd_village_place", map[string]interface{}{"key": "k-" + randomHex(8), "definitionId": def, "x": x, "z": z}); err != nil {
			t.Fatalf("%s: %v", def, err)
		}
	}
	place("training_grounds", 40, 40)
	for i := 0; i < camps; i++ {
		place("troop_camp", 2+i*4, 44)
	}
	return p
}

func TestTroops_NeedAHall_AndRoomInCamps(t *testing.T) {
	p := blank(t, 0)
	grant(t, p, map[string]interface{}{"setGold": 20000})
	_, err := trainTroops(p, "k-"+randomHex(8), "militia", 1)
	expectError(t, err, "NoTrainingHall")

	p = armyReady(t, 1)
	s, err := trainTroops(p, "k-"+randomHex(8), "militia", 15)
	if err != nil {
		t.Fatal(err)
	}
	if s.Troops["militia"] != 15 {
		t.Fatalf("%v", s.Troops)
	}
	_, err = trainTroops(p, "k-"+randomHex(8), "militia", 1)
	expectError(t, err, "CampsFull")
}

func TestTroops_StrongerOnesTakeMoreSpace(t *testing.T) {
	p := armyReady(t, 1)
	// A level 1 camp holds 15. Two 8-space riders don't fit, but the hall is too low for them anyway.
	_, err := trainTroops(p, "k-"+randomHex(8), "iron_ox_rider", 1)
	expectError(t, err, "TroopLocked")
	if _, err := trainTroops(p, "k-"+randomHex(8), "kunai_thrower", 7); err != nil {
		t.Fatal(err)
	}
	_, err = trainTroops(p, "k-"+randomHex(8), "kunai_thrower", 1)
	expectError(t, err, "CampsFull")
}

func TestTroops_ForgedRequests_AreRefused(t *testing.T) {
	p := armyReady(t, 1)
	const huge = int(^uint(0) >> 1)
	for _, count := range []int{0, -5, 101, huge} {
		_, err := trainTroops(p, "k-"+randomHex(8), "militia", count)
		expectError(t, err, "BadTroop")
	}
	_, err := trainTroops(p, "k-"+randomHex(8), "dragon", 1)
	expectError(t, err, "BadTroop")
	if _, err := p.rpc("sd_troop_train", map[string]interface{}{"key": "k-" + randomHex(8), "troopId": "militia", "count": 1, "goldCost": 0}); err == nil {
		t.Fatal("a client-set price was accepted")
	}
}

func TestTroops_ChargeExactly_AndReplayOnce(t *testing.T) {
	p := armyReady(t, 1)
	before := mustState(t, p).Village.Gold
	key := "k-" + randomHex(8)
	first, err := trainTroops(p, key, "militia", 4)
	if err != nil {
		t.Fatal(err)
	}
	if first.Village.Gold != before-100 {
		t.Fatalf("gold %d, want %d", first.Village.Gold, before-100)
	}
	again, err := trainTroops(p, key, "militia", 4)
	if err != nil {
		t.Fatal(err)
	}
	if again.Troops["militia"] != 4 || mustState(t, p).Village.Gold != before-100 {
		t.Fatal("replay trained or charged twice")
	}
}

func TestTroops_NoFunds_ChangesNothing(t *testing.T) {
	p := armyReady(t, 1)
	grant(t, p, map[string]interface{}{"setGold": 10})
	_, err := trainTroops(p, "k-"+randomHex(8), "militia", 1)
	expectError(t, err, "InsufficientFunds")
	if mustState(t, p).Village.Gold != 10 {
		t.Fatal("gold changed")
	}
}

func TestTroops_ConcurrentTraining_CannotOverfillCamps(t *testing.T) {
	p := armyReady(t, 1)
	var wait sync.WaitGroup
	for i := 0; i < 10; i++ {
		wait.Add(1)
		go func() {
			defer wait.Done()
			trainTroops(p, "k-"+randomHex(8), "militia", 5)
		}()
	}
	wait.Wait()
	var s armyState
	body, err := p.rpc("sd_get_state", map[string]interface{}{})
	if err != nil {
		t.Fatal(err)
	}
	json.Unmarshal([]byte(body), &s)
	if s.Troops["militia"] > 15 {
		t.Fatalf("%d troops in a camp that holds 15", s.Troops["militia"])
	}
}

func TestRaid_TroopsFight_AndAreUsedUp(t *testing.T) {
	p := armyReady(t, 2)
	if _, err := trainTroops(p, "k-"+randomHex(8), "militia", 10); err != nil {
		t.Fatal(err)
	}
	s := grant(t, p, map[string]interface{}{"archetypeId": "river_scout", "copies": 1})
	bases := heroBases(t, p)
	if _, err := p.rpc("sd_assign_hero", map[string]interface{}{"slot": bases[0], "instanceId": s.Heroes[0].InstanceID}); err != nil {
		t.Fatal(err)
	}
	start := startRaid(t, p)
	body, err := p.rpc("sd_raid_deploy", map[string]interface{}{
		"key": "k-" + randomHex(8), "attackId": start.AttackID,
		"placements": placementsFor(start),
		"troops":     []map[string]interface{}{{"troopId": "militia", "x": 5, "z": 5, "count": 6}},
	})
	if err != nil {
		t.Fatal(err)
	}
	var result struct {
		State armyState `json:"state"`
	}
	json.Unmarshal([]byte(body), &result)
	if result.State.Troops["militia"] != 4 {
		t.Fatalf("%d left, want the 4 that stayed home", result.State.Troops["militia"])
	}
}

func TestRaid_CannotLandTroopsYouDontHave(t *testing.T) {
	p := armyReady(t, 1)
	if _, err := trainTroops(p, "k-"+randomHex(8), "militia", 2); err != nil {
		t.Fatal(err)
	}
	s := grant(t, p, map[string]interface{}{"archetypeId": "river_scout", "copies": 1})
	bases := heroBases(t, p)
	p.rpc("sd_assign_hero", map[string]interface{}{"slot": bases[0], "instanceId": s.Heroes[0].InstanceID})
	start := startRaid(t, p)
	land := func(troop string, x, z, count int) error {
		_, err := p.rpc("sd_raid_deploy", map[string]interface{}{
			"key": "k-" + randomHex(8), "attackId": start.AttackID, "placements": placementsFor(start),
			"troops": []map[string]interface{}{{"troopId": troop, "x": x, "z": z, "count": count}},
		})
		return err
	}
	expectError(t, land("militia", 5, 5, 3), "NOT_ENOUGH_TROOPS")
	expectError(t, land("militia", 5, 5, 0), "BAD_TROOP_COUNT")
	expectError(t, land("militia", 5, 5, -1), "BAD_TROOP_COUNT")
	expectError(t, land("dragon", 5, 5, 1), "NOT_ENOUGH_TROOPS")
	expectError(t, land("militia", 25, 25, 1), "DEPLOY_TOO_CLOSE")
	expectError(t, land("militia", -1, 5, 1), "BAD_DEPLOY_CELL")
}
