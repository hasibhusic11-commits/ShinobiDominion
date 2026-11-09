package backendtests

import (
	"sync"
	"testing"
)

// The free Treasure Chest and the gem shop (ADR-043).

func TestChest_OpensOncePerWait(t *testing.T) {
	p := newPlayer(t)
	before := mustState(t, p)
	var wg sync.WaitGroup
	for i := 0; i < 10; i++ {
		wg.Add(1)
		go func() {
			defer wg.Done()
			p.rpc("sd_chest_open", map[string]interface{}{"key": "c-" + randomHex(8)})
		}()
	}
	wg.Wait()
	after := mustState(t, p)
	// A new village has a level 1 Dojo: 500 gold and 150 Mana, once.
	if after.Village.Gold != before.Village.Gold+500 || after.Village.Mana != before.Village.Mana+150 {
		t.Fatalf("gold %d -> %d, mana %d -> %d", before.Village.Gold, after.Village.Gold, before.Village.Mana, after.Village.Mana)
	}
	_, err := p.rpc("sd_chest_open", map[string]interface{}{"key": "c-" + randomHex(8)})
	expectError(t, err, "CHEST_NOT_READY")
	ledgersAgree(t, p)
}

func TestShop_SwapsGemsAtFixedRates(t *testing.T) {
	p := newPlayer(t)
	before := mustState(t, p)
	if _, err := p.rpc("sd_shop_buy", map[string]interface{}{"key": "s-" + randomHex(8), "offerId": "mana_flask"}); err != nil {
		t.Fatal(err)
	}
	after := mustState(t, p)
	if after.Crystals != before.Crystals-50 || after.Village.Mana != before.Village.Mana+1200 {
		t.Fatalf("a Mana flask is 50 gems for 1,200 Mana: %+v", after.Village)
	}
	_, err := p.rpc("sd_shop_buy", map[string]interface{}{"key": "s-" + randomHex(8), "offerId": "free_stuff"})
	expectError(t, err, "OFFER_NOT_FOUND")
	_, err = p.rpc("sd_shop_buy", map[string]interface{}{"key": "s-" + randomHex(8), "offerId": "mana_flask", "crystals": 0})
	expectError(t, err, "BAD_REQUEST")
	// The starter vault holds 20,000 and the village starts with 5,000, so 22,000 more would not fit.
	_, err = p.rpc("sd_shop_buy", map[string]interface{}{"key": "s-" + randomHex(8), "offerId": "gold_chest"})
	expectError(t, err, "StorageFull")
	grant(t, p, map[string]interface{}{"setCrystals": 10})
	_, err = p.rpc("sd_shop_buy", map[string]interface{}{"key": "s-" + randomHex(8), "offerId": "gold_pouch"})
	expectError(t, err, "INSUFFICIENT_FUNDS")
	ledgersAgree(t, p)
}
