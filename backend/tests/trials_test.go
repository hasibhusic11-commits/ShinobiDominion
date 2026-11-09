package backendtests

import (
	"encoding/json"
	"testing"
)

// The Trial Tower (ADR-045).

func TestTrials_LockInOrder_AndPayOnlyTheFirstClear(t *testing.T) {
	p := raidReady(t, "stone_titan", 1, 1)
	_, err := p.rpc("sd_trial_fight", map[string]interface{}{"key": "t-" + randomHex(8), "floor": 3})
	expectError(t, err, "TRIAL_LOCKED")
	_, err = p.rpc("sd_trial_fight", map[string]interface{}{"key": "t-" + randomHex(8), "floor": 999})
	expectError(t, err, "TRIAL_NOT_FOUND")

	fight := func() modeFight {
		body, err := p.rpc("sd_trial_fight", map[string]interface{}{"key": "t-" + randomHex(8), "floor": 1})
		if err != nil {
			t.Fatal(err)
		}
		var f modeFight
		json.Unmarshal([]byte(body), &f)
		return f
	}
	first := fight()
	if !first.Won || first.CrystalsEarned == 0 || first.ManaEarned == 0 {
		t.Fatalf("a level 200 Legendary should clear floor 1 and be paid: %+v", first)
	}
	again := fight()
	if !again.Won || again.CrystalsEarned != 0 || again.ManaEarned != 0 || again.Result.Checksum != first.Result.Checksum {
		t.Fatalf("a replay is the same duel and pays nothing: %+v", again)
	}
	if _, err := p.rpc("sd_trial_fight", map[string]interface{}{"key": "t-" + randomHex(8), "floor": 2}); err != nil {
		t.Fatalf("floor 2 should open after floor 1: %v", err)
	}
	ledgersAgree(t, p)
}
