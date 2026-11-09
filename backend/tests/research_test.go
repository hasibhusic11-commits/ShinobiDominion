package backendtests

import (
	"encoding/json"
	"sync"
	"testing"
)

// Troop research at the Research Hut (ADR-041).

type researchState struct {
	Crystals int64 `json:"crystals"`
	Village  struct {
		Mana int64 `json:"mana"`
	} `json:"village"`
	Research map[string]struct {
		Level int   `json:"level"`
		Done  int64 `json:"doneUtcTicks"`
	} `json:"research"`
}

func research(p *player, rpc, troopID string) (researchState, error) {
	var s researchState
	body, err := p.rpc(rpc, map[string]interface{}{"key": "r-" + randomHex(8), "troopId": troopID})
	if err != nil {
		return s, err
	}
	return s, json.Unmarshal([]byte(body), &s)
}

func TestResearch_NeedsAHut_RunsOneAtATime_AndFinishesForGemsOnce(t *testing.T) {
	p := newPlayer(t)
	grant(t, p, map[string]interface{}{"setGold": 20000})
	_, err := research(p, "sd_research_start", "militia")
	expectError(t, err, "NoResearchHut")

	dojo := findBuilding(mustState(t, p), "dojo_hq")
	if _, err := buildingOp(p, "sd_village_upgrade", "k-"+randomHex(8), dojo.ID); err != nil {
		t.Fatal(err)
	}
	placeAt(t, p, "research_hut", 40, 40)
	_, err = research(p, "sd_research_start", "dragon")
	expectError(t, err, "BadTroop")
	_, err = p.rpc("sd_research_start", map[string]interface{}{"key": "r-" + randomHex(8), "troopId": "militia", "level": 10})
	expectError(t, err, "BAD_REQUEST")

	before := mustState(t, p).Village.Mana
	s, err := research(p, "sd_research_start", "militia")
	if err != nil {
		t.Fatal(err)
	}
	if s.Village.Mana != before-200 || s.Research["militia"].Level != 0 || s.Research["militia"].Done == 0 {
		t.Fatalf("the first militia study costs 200 Mana and takes time: %+v", s)
	}
	_, err = research(p, "sd_research_start", "kunai_thrower")
	expectError(t, err, "ResearchBusy")

	gems := mustState(t, p).Crystals
	var wg sync.WaitGroup
	for i := 0; i < 10; i++ {
		wg.Add(1)
		go func() {
			defer wg.Done()
			research(p, "sd_research_finish", "militia")
		}()
	}
	wg.Wait()
	var after researchState
	body, _ := p.rpc("sd_get_state", "{}")
	json.Unmarshal([]byte(body), &after)
	if after.Crystals != gems-3 || after.Research["militia"].Level != 1 || after.Research["militia"].Done != 0 {
		t.Fatalf("a five-minute study finishes once for 3 gems: gems %d -> %d, %+v", gems, after.Crystals, after.Research)
	}
	// The next level needs a level 2 hut.
	_, err = research(p, "sd_research_start", "militia")
	expectError(t, err, "ResearchHutTooLow")
	ledgersAgree(t, p)
}
