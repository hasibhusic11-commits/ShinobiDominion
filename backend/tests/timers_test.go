package backendtests

import (
	"sync"
	"testing"
)

// Upgrade timers and builders (ADR-040).

func buildingByID(s state, id int) *buildingView {
	for i := range s.Village.Buildings {
		if s.Village.Buildings[i].ID == id {
			return &s.Village.Buildings[i]
		}
	}
	return nil
}

func TestUpgrades_TakeTime_NeedBuilders_AndFinishForGemsOnce(t *testing.T) {
	p := newPlayer(t)
	grant(t, p, map[string]interface{}{"setGold": 20000})
	first := findBuilding(mustState(t, p), "gold_mine")
	s := placeAt(t, p, "gold_mine", 40, 40)
	var second *buildingView
	for i := range s.Village.Buildings {
		if s.Village.Buildings[i].DefinitionID == "gold_mine" && s.Village.Buildings[i].ID != first.ID {
			second = &s.Village.Buildings[i]
		}
	}
	tower := findBuilding(placeAt(t, p, "defense_tower", 40, 30), "defense_tower")

	// Level 2 is instant and needs no builder.
	for _, id := range []int{first.ID, second.ID, tower.ID} {
		s, err := buildingOp(p, "sd_village_upgrade", "k-"+randomHex(8), id)
		if err != nil {
			t.Fatal(err)
		}
		if b := buildingByID(s, id); b.Level != 2 || b.UpgradeDone != 0 {
			t.Fatalf("level 2 should be instant: %+v", b)
		}
	}
	// Level 3 takes a builder and time.
	s, err := buildingOp(p, "sd_village_upgrade", "k-"+randomHex(8), first.ID)
	if err != nil {
		t.Fatal(err)
	}
	if b := buildingByID(s, first.ID); b.Level != 2 || b.UpgradeDone == 0 {
		t.Fatalf("level 3 should be running, not done: %+v", b)
	}
	_, err = buildingOp(p, "sd_village_upgrade", "k-"+randomHex(8), first.ID)
	expectError(t, err, "AlreadyUpgrading")
	if _, err := buildingOp(p, "sd_village_upgrade", "k-"+randomHex(8), second.ID); err != nil {
		t.Fatal(err)
	}
	_, err = buildingOp(p, "sd_village_upgrade", "k-"+randomHex(8), tower.ID)
	expectError(t, err, "NoFreeBuilder")
	_, err = buildingOp(p, "sd_village_finish", "k-"+randomHex(8), tower.ID)
	expectError(t, err, "NotUpgrading")

	// Ten finishes at once with different keys: one pays one gem, the rest are refused.
	before := mustState(t, p).Crystals
	var wg sync.WaitGroup
	for i := 0; i < 10; i++ {
		wg.Add(1)
		go func() {
			defer wg.Done()
			buildingOp(p, "sd_village_finish", "k-"+randomHex(8), first.ID)
		}()
	}
	wg.Wait()
	after := mustState(t, p)
	if after.Crystals != before-1 {
		t.Fatalf("finishing a one-minute upgrade costs 1 gem once: %d -> %d", before, after.Crystals)
	}
	if b := buildingByID(after, first.ID); b.Level != 3 || b.UpgradeDone != 0 {
		t.Fatalf("finished upgrade: %+v", b)
	}
	// A builder is free again.
	if _, err := buildingOp(p, "sd_village_upgrade", "k-"+randomHex(8), tower.ID); err != nil {
		t.Fatal(err)
	}
	ledgersAgree(t, p)
}
