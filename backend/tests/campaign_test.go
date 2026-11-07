package backendtests

import (
	"encoding/json"
	"strings"
	"testing"
)

type campaignDeploy struct {
	raidDeploy
	XpEarned       int64  `json:"xpEarned"`
	LevelsGained   int    `json:"levelsGained"`
	CrystalsEarned int64  `json:"crystalsEarned"`
	ItemEarned     string `json:"itemEarned"`
}

func startStage(t *testing.T, p *player, id string) (raidStart, error) {
	t.Helper()
	var start raidStart
	body, err := p.rpc("sd_raid_start", map[string]interface{}{"key": "k-" + randomHex(8), "raidId": id})
	if err != nil {
		return start, err
	}
	return start, json.Unmarshal([]byte(body), &start)
}

func fightStage(t *testing.T, p *player, id string) campaignDeploy {
	t.Helper()
	start, err := startStage(t, p, id)
	if err != nil {
		t.Fatal(err)
	}
	body, err := p.rpc("sd_raid_deploy", map[string]interface{}{"key": "k-" + randomHex(8), "attackId": start.AttackID, "placements": legalPlacements(start)})
	if err != nil {
		t.Fatal(err)
	}
	var result campaignDeploy
	if err := json.Unmarshal([]byte(body), &result); err != nil {
		t.Fatal(err)
	}
	return result
}

func TestCampaign_LockedStage_IsRefused_UntilThePreviousOneIsCleared(t *testing.T) {
	p := raidReady(t, "solar_beast_sage", 6, 6)
	if _, err := startStage(t, p, "stage_03"); err == nil || !strings.Contains(err.Error(), "RAID_LOCKED") {
		t.Fatalf("got %v", err)
	}
	if won := fightStage(t, p, "cinder_garrison"); won.Result.Stars == 0 {
		t.Fatal("six level 200 heroes should clear the garrison")
	}
	if _, err := startStage(t, p, "stage_03"); err != nil {
		t.Fatalf("stage 3 should now be open: %v", err)
	}
	if _, err := startStage(t, p, "stage_04"); err == nil || !strings.Contains(err.Error(), "RAID_LOCKED") {
		t.Fatalf("stage 4 should stay closed: %v", err)
	}
}

func TestCampaign_Win_GivesEveryLandedHeroXP_FromTheServer(t *testing.T) {
	p := raidReady(t, "solar_beast_sage", 6, 5)
	before := mustState(t, p)
	won := fightStage(t, p, "ridge_outpost")
	if won.Result.Stars == 0 {
		t.Fatal("five level 200 heroes should clear the first stage")
	}
	if won.XpEarned <= 0 {
		t.Fatalf("xp %d", won.XpEarned)
	}
	after := mustState(t, p)
	checked := 0
	for i := range before.Heroes {
		if before.Heroes[i].Level >= 200 {
			continue
		}
		checked++
		if after.Heroes[i].Level == before.Heroes[i].Level && after.Heroes[i].Experience == before.Heroes[i].Experience {
			t.Fatalf("hero %d gained nothing", i)
		}
	}
	if checked == 0 {
		t.Fatal("no hero below the level cap to check")
	}
}

func TestCampaign_HeroXP_StopsAfterThe24HourCap(t *testing.T) {
	p := raidReady(t, "solar_beast_sage", 6, 5)
	for i := 0; i < 30; i++ {
		if won := fightStage(t, p, "ridge_outpost"); won.XpEarned <= 0 {
			t.Fatalf("win %d paid no XP", i+1)
		}
	}
	over := fightStage(t, p, "ridge_outpost")
	if over.Result.Stars == 0 {
		t.Fatal("this team should still win")
	}
	if over.XpEarned != 0 {
		t.Fatalf("win 31 paid %d XP", over.XpEarned)
	}
}

func TestCampaign_LostBattle_PaysNothing(t *testing.T) {
	p := raidReady(t, "river_scout", 1, 0)
	before := mustState(t, p)
	lost := fightStage(t, p, "cinder_garrison")
	if lost.Result.Stars != 0 {
		t.Skip("this team won, which is not what this test is checking")
	}
	if lost.XpEarned != 0 || lost.CrystalsEarned != 0 || lost.ItemEarned != "" || lost.GoldEarned != 0 {
		t.Fatalf("a loss paid %+v", lost)
	}
	after := mustState(t, p)
	if after.Crystals != before.Crystals || after.Heroes[0].Experience != before.Heroes[0].Experience {
		t.Fatal("a loss changed the player")
	}
}

func TestCampaign_FirstClear_PaysOnce_AndRepeatsStillGiveXP(t *testing.T) {
	p := raidReady(t, "solar_beast_sage", 6, 0)
	grant(t, p, map[string]interface{}{"setDojoLevel": 15})
	for _, hero := range mustState(t, p).Heroes {
		grant(t, p, map[string]interface{}{"maxLevelFor": hero.InstanceID})
	}
	before := mustState(t, p)
	first := fightStage(t, p, "ridge_outpost")
	if first.CrystalsEarned <= 0 {
		t.Fatalf("first clear paid %d crystals", first.CrystalsEarned)
	}
	if after := mustState(t, p); after.Crystals != before.Crystals+first.CrystalsEarned {
		t.Fatalf("crystals %d, want %d", after.Crystals, before.Crystals+first.CrystalsEarned)
	}
	second := fightStage(t, p, "ridge_outpost")
	if second.CrystalsEarned != 0 || second.GoldEarned != 0 {
		t.Fatalf("a repeat paid %+v", second)
	}
	if second.XpEarned <= 0 {
		t.Fatal("repeats should still give XP")
	}
}

func TestCampaign_ChapterBoss_GivesItsCatalyst_OnlyOnce(t *testing.T) {
	p := raidReady(t, "solar_beast_sage", 6, 6)
	for _, id := range []string{"cinder_garrison", "stage_03", "stage_04"} {
		if won := fightStage(t, p, id); won.Result.Stars == 0 {
			t.Fatalf("%s should fall to six level 200 heroes", id)
		}
	}
	before := mustState(t, p).Items["awakening_scroll"]
	boss := fightStage(t, p, "stage_05")
	if boss.ItemEarned != "awakening_scroll" {
		t.Fatalf("item %q, stars %d", boss.ItemEarned, boss.Result.Stars)
	}
	if after := mustState(t, p).Items["awakening_scroll"]; after != before+1 {
		t.Fatalf("scrolls %d, want %d", after, before+1)
	}
	again := fightStage(t, p, "stage_05")
	if again.ItemEarned != "" {
		t.Fatalf("a repeat gave %q", again.ItemEarned)
	}
	if after := mustState(t, p).Items["awakening_scroll"]; after != before+1 {
		t.Fatalf("scrolls %d after a repeat", after)
	}
}

func TestCampaign_RetriedDeploy_PaysOnce(t *testing.T) {
	p := raidReady(t, "solar_beast_sage", 2, 0)
	start, err := startStage(t, p, "ridge_outpost")
	if err != nil {
		t.Fatal(err)
	}
	key := "k-" + randomHex(8)
	payload := map[string]interface{}{"key": key, "attackId": start.AttackID, "placements": legalPlacements(start)}
	first, err := p.rpc("sd_raid_deploy", payload)
	if err != nil {
		t.Fatal(err)
	}
	stateAfterFirst := mustState(t, p)
	second, err := p.rpc("sd_raid_deploy", payload)
	if err != nil {
		t.Fatal(err)
	}
	if first != second {
		t.Fatal("a retry should return the stored answer")
	}
	stateAfterSecond := mustState(t, p)
	if stateAfterFirst.Crystals != stateAfterSecond.Crystals || stateAfterFirst.Heroes[0].Experience != stateAfterSecond.Heroes[0].Experience || stateAfterFirst.Heroes[0].Level != stateAfterSecond.Heroes[0].Level {
		t.Fatal("a retry paid twice")
	}
}

func TestCampaign_ClaimingARewardInTheRequest_IsRefused(t *testing.T) {
	p := raidReady(t, "solar_beast_sage", 1, 0)
	start, err := startStage(t, p, "ridge_outpost")
	if err != nil {
		t.Fatal(err)
	}
	for _, extra := range []string{"xpEarned", "crystalsEarned", "itemEarned", "levelsGained"} {
		_, err := p.rpc("sd_raid_deploy", map[string]interface{}{"key": "k-" + randomHex(8), "attackId": start.AttackID, "placements": legalPlacements(start), extra: 999})
		if err == nil {
			t.Fatalf("a request claiming %s should be refused", extra)
		}
	}
}

func TestCampaign_RivalRaid_GivesNoDungeonRewards(t *testing.T) {
	rival := raidReady(t, "river_scout", 1, 0)
	attacker := raidReady(t, "solar_beast_sage", 6, 0)
	before := mustState(t, attacker)
	start := raidRival(t, attacker, userIDOf(t, rival))
	body, err := attacker.rpc("sd_raid_deploy", map[string]interface{}{"key": "k-" + randomHex(8), "attackId": start.AttackID, "placements": legalPlacements(start)})
	if err != nil {
		t.Fatal(err)
	}
	var result campaignDeploy
	if err := json.Unmarshal([]byte(body), &result); err != nil {
		t.Fatal(err)
	}
	if result.XpEarned != 0 || result.CrystalsEarned != 0 || result.ItemEarned != "" {
		t.Fatalf("a rival raid paid dungeon rewards: %+v", result)
	}
	if after := mustState(t, attacker); after.Crystals != before.Crystals {
		t.Fatal("crystals changed")
	}
}
