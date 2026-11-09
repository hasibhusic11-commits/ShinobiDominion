package backendtests

import (
	"encoding/json"
	"sync"
	"testing"
)

// The Arena, Village Siege, daily quests and the daily gift (ADR-039).

type modeFight struct {
	Mode           string `json:"mode"`
	Won            bool   `json:"won"`
	Stars          int    `json:"stars"`
	BestStars      int    `json:"bestStars"`
	GoldEarned     int64  `json:"goldEarned"`
	ManaEarned     int64  `json:"manaEarned"`
	CrystalsEarned int64  `json:"crystalsEarned"`
	PointsDelta    int    `json:"pointsDelta"`
	Points         int    `json:"points"`
	Result         struct {
		Checksum string `json:"checksum"`
	} `json:"result"`
}

type questBoard struct {
	Quests []struct {
		ID       string `json:"id"`
		Progress int    `json:"progress"`
		Target   int    `json:"target"`
		Crystals int64  `json:"crystals"`
		Claimed  bool   `json:"claimed"`
	} `json:"quests"`
	GiftReady    bool  `json:"giftReady"`
	GiftCrystals int64 `json:"giftCrystals"`
	GiftGold     int64 `json:"giftGold"`
	GiftMana     int64 `json:"giftMana"`
}

func fightSiege(p *player, key string, level int) (modeFight, string, error) {
	var fight modeFight
	body, err := p.rpc("sd_siege_fight", map[string]interface{}{"key": key, "level": level})
	if err != nil {
		return fight, body, err
	}
	return fight, body, json.Unmarshal([]byte(body), &fight)
}

func board(t *testing.T, p *player) questBoard {
	t.Helper()
	body, err := p.rpc("sd_quest_board", "{}")
	if err != nil {
		t.Fatal(err)
	}
	var b questBoard
	json.Unmarshal([]byte(body), &b)
	return b
}

// ledgersAgree checks every balance still equals the sum of its ledger.
func ledgersAgree(t *testing.T, p *player) {
	t.Helper()
	body, err := p.rpc("sd_dev_audit", "{}")
	if err != nil {
		t.Fatal(err)
	}
	var audit struct {
		Crystals      int64 `json:"crystals"`
		LedgerSum     int64 `json:"ledgerSum"`
		Gold          int64 `json:"gold"`
		GoldLedgerSum int64 `json:"goldLedgerSum"`
		Mana          int64 `json:"mana"`
		ManaLedgerSum int64 `json:"manaLedgerSum"`
	}
	json.Unmarshal([]byte(body), &audit)
	if audit.Crystals != audit.LedgerSum || audit.Gold != audit.GoldLedgerSum || audit.Mana != audit.ManaLedgerSum {
		t.Fatalf("ledgers disagree: %+v", audit)
	}
}

func TestGift_ConcurrentOpens_PayExactlyOnce(t *testing.T) {
	p := newPlayer(t)
	before := mustState(t, p)
	gift := board(t, p)
	if !gift.GiftReady {
		t.Fatal("a new player's gift should be ready")
	}
	var wg sync.WaitGroup
	for i := 0; i < 10; i++ {
		wg.Add(1)
		go func() {
			defer wg.Done()
			p.rpc("sd_gift_open", map[string]interface{}{"key": "g-" + randomHex(8)})
		}()
	}
	wg.Wait()
	after := mustState(t, p)
	if after.Crystals != before.Crystals+gift.GiftCrystals {
		t.Fatalf("gems %d -> %d, the gift is %d", before.Crystals, after.Crystals, gift.GiftCrystals)
	}
	if after.Village.Mana != before.Village.Mana+gift.GiftMana {
		t.Fatalf("mana %d -> %d, the gift is %d", before.Village.Mana, after.Village.Mana, gift.GiftMana)
	}
	if board(t, p).GiftReady {
		t.Fatal("the gift should be opened")
	}
	_, err := p.rpc("sd_gift_open", map[string]interface{}{"key": "g-" + randomHex(8)})
	expectError(t, err, "GIFT_ALREADY_OPENED")
	ledgersAgree(t, p)
}

func TestQuests_CountOnTheServer_AndPayOnce(t *testing.T) {
	p := newPlayer(t)
	_, err := p.rpc("sd_quest_claim", map[string]interface{}{"key": "q-" + randomHex(8), "questId": "upgrade"})
	expectError(t, err, "QUEST_NOT_DONE")
	_, err = p.rpc("sd_quest_claim", map[string]interface{}{"key": "q-" + randomHex(8), "questId": "free_gems"})
	expectError(t, err, "QUEST_NOT_FOUND")
	_, err = p.rpc("sd_quest_claim", map[string]interface{}{"key": "q-" + randomHex(8), "questId": "upgrade", "progress": 99})
	expectError(t, err, "BAD_REQUEST")

	mine := findBuilding(mustState(t, p), "gold_mine")
	if _, err := buildingOp(p, "sd_village_upgrade", "k-"+randomHex(8), mine.ID); err != nil {
		t.Fatal(err)
	}
	b := board(t, p)
	var reward int64
	for _, q := range b.Quests {
		if q.ID == "upgrade" {
			if q.Progress != 1 || q.Claimed {
				t.Fatalf("upgrade quest after one upgrade: %+v", q)
			}
			reward = q.Crystals
		}
	}
	before := mustState(t, p).Crystals
	var wg sync.WaitGroup
	for i := 0; i < 10; i++ {
		wg.Add(1)
		go func() {
			defer wg.Done()
			p.rpc("sd_quest_claim", map[string]interface{}{"key": "q-" + randomHex(8), "questId": "upgrade"})
		}()
	}
	wg.Wait()
	if after := mustState(t, p).Crystals; after != before+reward {
		t.Fatalf("gems %d -> %d, the quest pays %d once", before, after, reward)
	}
	ledgersAgree(t, p)
}

func TestSiege_PaysOnlyNewStars_ReplaysChangeNothing_AndLocksInOrder(t *testing.T) {
	p := raidReady(t, "stone_titan", 1, 1)
	_, _, err := fightSiege(p, "s-"+randomHex(8), 3)
	expectError(t, err, "SIEGE_LOCKED")
	_, _, err = fightSiege(p, "s-"+randomHex(8), 0)
	expectError(t, err, "SIEGE_NOT_FOUND")

	key := "s-" + randomHex(8)
	first, body, err := fightSiege(p, key, 1)
	if err != nil {
		t.Fatal(err)
	}
	if !first.Won || first.Stars == 0 {
		t.Fatalf("a maxed Legendary should hold level 1: %+v", first)
	}
	if first.GoldEarned+first.ManaEarned == 0 || first.CrystalsEarned == 0 {
		t.Fatalf("a first hold pays: %+v", first)
	}
	afterFirst := mustState(t, p)
	_, replay, err := fightSiege(p, key, 1)
	if err != nil || replay != body {
		t.Fatal("a replayed key must return the same answer")
	}
	again, _, err := fightSiege(p, "s-"+randomHex(8), 1)
	if err != nil {
		t.Fatal(err)
	}
	if again.Result.Checksum != first.Result.Checksum {
		t.Fatal("the same village must meet the same siege")
	}
	if again.GoldEarned+again.ManaEarned+again.CrystalsEarned != 0 {
		t.Fatalf("no new stars, so no pay: %+v", again)
	}
	if after := mustState(t, p); after.Village.Gold != afterFirst.Village.Gold || after.Crystals != afterFirst.Crystals {
		t.Fatal("a repeat hold changed the wallet")
	}
	if _, _, err := fightSiege(p, "s-"+randomHex(8), 2); err != nil {
		t.Fatalf("level 2 should open after level 1: %v", err)
	}
	ledgersAgree(t, p)
}

func TestArena_DuelMovesPoints_AndRefusesClaims(t *testing.T) {
	_, err := newPlayer(t).rpc("sd_arena_fight", map[string]interface{}{"key": "a-" + randomHex(8)})
	expectError(t, err, "NO_TEAM")

	p := raidReady(t, "stone_titan", 1, 1)
	_, err = p.rpc("sd_arena_fight", map[string]interface{}{"key": "a-" + randomHex(8), "won": true})
	expectError(t, err, "BAD_REQUEST")

	body, err := p.rpc("sd_arena_fight", map[string]interface{}{"key": "a-" + randomHex(8)})
	if err != nil {
		t.Fatal(err)
	}
	var fight modeFight
	json.Unmarshal([]byte(body), &fight)
	if fight.Mode != "arena" || fight.Won != (fight.PointsDelta > 0) {
		t.Fatalf("odd duel: %+v", fight)
	}
	boardBody, err := p.rpc("sd_arena_board", "{}")
	if err != nil {
		t.Fatal(err)
	}
	var arena struct {
		Points int `json:"points"`
		Rank   int `json:"rank"`
	}
	json.Unmarshal([]byte(boardBody), &arena)
	if arena.Points != fight.Points || arena.Rank < 1 {
		t.Fatalf("board %+v after duel %+v", arena, fight)
	}
	for _, q := range board(t, p).Quests {
		if q.ID == "arena" && q.Progress != 1 {
			t.Fatalf("one duel should count once: %+v", q)
		}
	}
	ledgersAgree(t, p)
}
