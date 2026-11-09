package backendtests

import (
	"encoding/json"
	"sync"
	"testing"
)

// Mana, the third currency (ADR-038). Earned at Mana Mills, spent on troops and hero training.

func placeAt(t *testing.T, p *player, def string, x, z int) state {
	t.Helper()
	body, err := p.rpc("sd_village_place", map[string]interface{}{"key": "k-" + randomHex(8), "definitionId": def, "x": x, "z": z})
	if err != nil {
		t.Fatalf("%s: %v", def, err)
	}
	var s state
	json.Unmarshal([]byte(body), &s)
	return s
}

func trainHero(p *player, key, instanceID string) (state, error) {
	var s state
	body, err := p.rpc("sd_hero_train", map[string]interface{}{"key": key, "instanceId": instanceID})
	if err != nil {
		return s, err
	}
	return s, json.Unmarshal([]byte(body), &s)
}

func manaAudit(t *testing.T, p *player) (int64, int64) {
	t.Helper()
	body, err := p.rpc("sd_dev_audit", "{}")
	if err != nil {
		t.Fatal(err)
	}
	var audit struct {
		Mana          int64 `json:"mana"`
		ManaLedgerSum int64 `json:"manaLedgerSum"`
	}
	json.Unmarshal([]byte(body), &audit)
	return audit.Mana, audit.ManaLedgerSum
}

func TestMana_NewPlayerStartsWithIt_AndTheLedgerAgrees(t *testing.T) {
	p := newPlayer(t)
	if mana := mustState(t, p).Village.Mana; mana != 500 {
		t.Fatalf("starting mana %d, want 500", mana)
	}
	if balance, ledger := manaAudit(t, p); balance != ledger {
		t.Fatalf("mana %d but ledger sums to %d", balance, ledger)
	}
}

func TestMana_MillProducesOnServerTime_AndCollectsOnce(t *testing.T) {
	p := newPlayer(t)
	s := placeAt(t, p, "mana_mill", 40, 40)
	if s.Village.Gold != 4400 {
		t.Fatalf("mill should cost 600 gold, gold=%d", s.Village.Gold)
	}
	mill := findBuilding(s, "mana_mill")
	_, err := buildingOp(p, "sd_village_collect", "k-"+randomHex(8), mill.ID)
	expectError(t, err, "NothingToCollect")

	grant(t, p, map[string]interface{}{"backdateCollectHours": 1})
	var wg sync.WaitGroup
	for i := 0; i < 10; i++ {
		wg.Add(1)
		go func() {
			defer wg.Done()
			buildingOp(p, "sd_village_collect", "k-"+randomHex(8), mill.ID)
		}()
	}
	wg.Wait()
	after := mustState(t, p)
	// The backdate also fills the starter gold mine, but that is gold. Mana: 500 start + 200 for an hour.
	if after.Village.Mana != 700 {
		t.Fatalf("one hour should pay 200 mana exactly once, mana=%d", after.Village.Mana)
	}
	if balance, ledger := manaAudit(t, p); balance != ledger {
		t.Fatalf("mana %d but ledger sums to %d", balance, ledger)
	}
}

func TestMana_TrainingSpendsIt_AndRefusesWhenShort(t *testing.T) {
	p := armyReady(t, 1)
	grant(t, p, map[string]interface{}{"setMana": 25})
	before := mustState(t, p).Village
	_, err := trainTroops(p, "k-"+randomHex(8), "militia", 3)
	expectError(t, err, "InsufficientMana")
	if now := mustState(t, p).Village; now.Gold != before.Gold || now.Mana != 25 {
		t.Fatalf("a refused training charged something: gold %d->%d mana %d", before.Gold, now.Gold, now.Mana)
	}
	s, err := trainTroops(p, "k-"+randomHex(8), "militia", 2)
	if err != nil {
		t.Fatal(err)
	}
	if s.Village.Mana != 5 || s.Village.Gold != before.Gold-50 {
		t.Fatalf("2 militia should cost 20 mana and 50 gold: mana %d gold %d", s.Village.Mana, s.Village.Gold)
	}
	if balance, ledger := manaAudit(t, p); balance != ledger {
		t.Fatalf("mana %d but ledger sums to %d", balance, ledger)
	}
}

func TestMana_HeroTrainingBuysOneLevel_ReplaysOnce_AndRefusesWhenShort(t *testing.T) {
	p := blank(t, 0)
	s := grant(t, p, map[string]interface{}{"archetypeId": "thunder_empress", "setMana": 45})
	hero := s.Heroes[0].InstanceID

	key := "k-" + randomHex(8)
	first, err := trainHero(p, key, hero)
	if err != nil {
		t.Fatal(err)
	}
	if first.Heroes[0].Level != 2 || first.Village.Mana != 25 {
		t.Fatalf("level 1->2 should cost 20 mana: level %d mana %d", first.Heroes[0].Level, first.Village.Mana)
	}
	again, err := trainHero(p, key, hero)
	if err != nil {
		t.Fatal(err)
	}
	if again.Heroes[0].Level != 2 || mustState(t, p).Village.Mana != 25 {
		t.Fatal("a replayed request trained or charged twice")
	}
	if _, err := trainHero(p, "k-"+randomHex(8), hero); err != nil {
		t.Fatal(err)
	}
	// Level 3 -> 4 costs 30 and only 0 is left.
	_, err = trainHero(p, "k-"+randomHex(8), hero)
	expectError(t, err, "InsufficientMana")
	if now := mustState(t, p); now.Heroes[0].Level != 3 || now.Village.Mana != 0 {
		t.Fatalf("refusal changed state: level %d mana %d", now.Heroes[0].Level, now.Village.Mana)
	}

	grant(t, p, map[string]interface{}{"setMana": 100000, "maxLevelFor": hero})
	_, err = trainHero(p, "k-"+randomHex(8), hero)
	expectError(t, err, "AT_MAX_LEVEL")

	_, err = trainHero(p, "k-"+randomHex(8), "00000000-0000-0000-0000-000000000000")
	expectError(t, err, "HERO_NOT_FOUND")
	if _, err := p.rpc("sd_hero_train", map[string]interface{}{"key": "k-" + randomHex(8), "instanceId": hero, "manaCost": 0}); err == nil {
		t.Fatal("a client-set price was accepted")
	}
	if balance, ledger := manaAudit(t, p); balance != ledger {
		t.Fatalf("mana %d but ledger sums to %d", balance, ledger)
	}
}

func TestMana_AnotherPlayersHeroCannotBeTrained(t *testing.T) {
	owner := blank(t, 0)
	hero := grant(t, owner, map[string]interface{}{"archetypeId": "thunder_empress"}).Heroes[0].InstanceID
	thief := newPlayer(t)
	_, err := trainHero(thief, "k-"+randomHex(8), hero)
	expectError(t, err, "HERO_NOT_FOUND")
	if mustState(t, thief).Village.Mana != 500 {
		t.Fatal("the refused request charged the caller")
	}
}
