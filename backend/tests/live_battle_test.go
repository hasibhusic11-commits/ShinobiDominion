package backendtests

import (
	"encoding/json"
	"testing"
	"time"
)

func timedDeploy(p *player, attackID string, placements []map[string]interface{}, troops []map[string]interface{}, endTick int) (raidDeploy, error) {
	var result raidDeploy
	payload := map[string]interface{}{"key": "k-" + randomHex(8), "attackId": attackID, "placements": placements, "endTick": endTick}
	if troops != nil {
		payload["troops"] = troops
	}
	body, err := p.rpc("sd_raid_deploy", payload)
	if err != nil {
		return result, err
	}
	return result, json.Unmarshal([]byte(body), &result)
}

// A live battle (ADR-050): cards dropped at their ticks, ended by the player. The server fights it again and its
// log shows each unit landing when it was dropped, and the battle stopping at the end tick.
func TestLiveBattle_TimedDrops_AreFoughtAgainByTheServer(t *testing.T) {
	p := raidReady(t, "river_scout", 1, 0)
	start := startRaid(t, p)
	// The battle claims 2.5 seconds; let that much time pass, as it would on the device.
	time.Sleep(2500 * time.Millisecond)
	placements := placementsFor(start)
	placements[0]["tick"] = 12
	result, err := timedDeploy(p, start.AttackID, placements, nil, 25)
	if err != nil {
		t.Fatal(err)
	}
	if result.Result.EndTick != 25 {
		t.Fatalf("end tick %d, want the 25 the player ended at", result.Result.EndTick)
	}
	landed := false
	for _, e := range result.Result.Events {
		if e[1] == 19 {
			if e[0] != 13 || e[2] != 0 || e[3] != 2 || e[4] != 2 {
				t.Fatalf("landing %v, want unit 0 at cell 2,2 on tick 13", e)
			}
			landed = true
		}
		if e[0] < 13 {
			t.Fatalf("something happened before the first card fell: %v", e)
		}
	}
	if !landed {
		t.Fatal("the log should show the hero landing")
	}
}

// Nobody can fight a battle, see how it goes and send a better list: the battle can't have lasted longer than the
// time since the raid began.
func TestLiveBattle_ClockAheadOfRealTime_IsRefused(t *testing.T) {
	p := raidReady(t, "river_scout", 1, 0)
	start := startRaid(t, p)
	placements := placementsFor(start)
	placements[0]["tick"] = 500
	_, err := timedDeploy(p, start.AttackID, placements, nil, 900)
	expectError(t, err, "BATTLE_CLOCK_AHEAD")
	// The refusal changed nothing: an honest list is still taken.
	placements[0]["tick"] = 0
	if _, err := timedDeploy(p, start.AttackID, placements, nil, 10); err != nil {
		t.Fatal(err)
	}
}

func TestLiveBattle_BadTicks_AreRefused(t *testing.T) {
	p := raidReady(t, "river_scout", 1, 0)
	start := startRaid(t, p)
	placements := placementsFor(start)

	placements[0]["tick"] = 5
	_, err := deploy(p, "k-"+randomHex(8), start.AttackID, placements)
	expectError(t, err, "BAD_DEPLOY_TICK")

	placements[0]["tick"] = 10
	_, err = timedDeploy(p, start.AttackID, placements, nil, 10)
	expectError(t, err, "BAD_DEPLOY_TICK")

	placements[0]["tick"] = -1
	_, err = timedDeploy(p, start.AttackID, placements, nil, 10)
	expectError(t, err, "BAD_DEPLOY_TICK")
}
