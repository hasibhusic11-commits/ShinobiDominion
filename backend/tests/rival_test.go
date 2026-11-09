package backendtests

import (
	"encoding/json"
	"net/http"
	"os"
	"strings"
	"testing"
)

type defenseLog struct {
	Defenses []struct {
		AttackID       string `json:"attackId"`
		AttackerName   string `json:"attackerName"`
		Stars          int    `json:"stars"`
		DamagePercent  int    `json:"damagePercent"`
		DefendersAlive int    `json:"defendersAlive"`
		DefendersTotal int    `json:"defendersTotal"`
		GoldLost       int64  `json:"goldLost"`
		ManaLost       int64  `json:"manaLost"`
		CanRevenge     bool   `json:"canRevenge"`
		AtMs           int64  `json:"atMs"`
	} `json:"defenses"`
}

func userIDOf(t *testing.T, p *player) string {
	t.Helper()
	request, _ := http.NewRequest("GET", baseURL+"/v2/account", nil)
	request.Header.Set("Authorization", "Bearer "+p.token)
	response, err := client.Do(request)
	if err != nil {
		t.Fatal(err)
	}
	defer response.Body.Close()
	var account struct {
		User struct {
			ID string `json:"id"`
		} `json:"user"`
	}
	if err := json.NewDecoder(response.Body).Decode(&account); err != nil {
		t.Fatal(err)
	}
	if account.User.ID == "" {
		t.Fatal("no user id")
	}
	return account.User.ID
}

func raidRival(t *testing.T, p *player, rivalID string) raidStart {
	t.Helper()
	body, err := p.rpc("sd_rival_start", map[string]interface{}{"key": "k-" + randomHex(8), "rivalId": rivalID})
	if err != nil {
		t.Fatal(err)
	}
	var start raidStart
	if err := json.Unmarshal([]byte(body), &start); err != nil {
		t.Fatal(err)
	}
	return start
}

func stateBody(t *testing.T, p *player) string {
	t.Helper()
	body, err := p.rpc("sd_get_state", nil)
	if err != nil {
		t.Fatal(err)
	}
	return body
}

func TestRival_CopiesVillageAndGuards_WithoutExposingHeroIds(t *testing.T) {
	rival := raidReady(t, "river_scout", 2, 0)
	attacker := raidReady(t, "solar_beast_sage", 1, 1)
	rivalBody := stateBody(t, rival)

	start := raidRival(t, attacker, userIDOf(t, rival))
	if start.Raid.ID != "rival" {
		t.Fatalf("raid id %q", start.Raid.ID)
	}
	if len(start.Defenders) != 1 {
		t.Fatalf("want the 1 guard, got %d", len(start.Defenders))
	}
	if len(start.Buildings) == 0 {
		t.Fatal("no buildings")
	}
	for _, guard := range start.Defenders {
		if guard.Hp <= 0 || guard.Attack <= 0 || guard.ArchetypeID != "river_scout" {
			t.Fatalf("bad guard %+v", guard)
		}
		if strings.Contains(rivalBody, guard.ID) && len(guard.ID) > 4 {
			t.Fatalf("guard id %q exposes the rival's hero id", guard.ID)
		}
	}
}

// ADR-049: a raid takes real loot. The rival loses exactly what the attacker gains, nothing else of theirs changes,
// and a hard enough hit shields them.
func TestRival_RaidTakesLoot_AndShieldsTheRival(t *testing.T) {
	rival := raidReady(t, "river_scout", 2, 0)
	attacker := raidReady(t, "solar_beast_sage", 6, 6)
	before := mustState(t, rival)

	start := raidRival(t, attacker, userIDOf(t, rival))
	if start.Raid.AvailableGold != before.Village.Gold*20/100 || start.Raid.AvailableMana != before.Village.Mana*20/100 {
		t.Fatalf("loot on offer %d gold %d Mana, want 20%% of %d and %d", start.Raid.AvailableGold, start.Raid.AvailableMana, before.Village.Gold, before.Village.Mana)
	}
	result, err := deploy(attacker, "k-"+randomHex(8), start.AttackID, legalPlacements(start))
	if err != nil {
		t.Fatal(err)
	}
	if result.GoldEarned <= 0 || result.GoldEarned > start.Raid.AvailableGold*int64(result.Result.DamagePercent)/100 {
		t.Fatalf("gold earned %d of %d at %d%% damage", result.GoldEarned, start.Raid.AvailableGold, result.Result.DamagePercent)
	}
	after := mustState(t, rival)
	if before.Village.Gold-after.Village.Gold != result.GoldEarned || before.Village.Mana-after.Village.Mana != result.ManaEarned {
		t.Fatalf("rival lost %d gold %d Mana, attacker gained %d and %d", before.Village.Gold-after.Village.Gold, before.Village.Mana-after.Village.Mana, result.GoldEarned, result.ManaEarned)
	}
	if len(after.Village.Buildings) != len(before.Village.Buildings) || len(after.Heroes) != len(before.Heroes) || after.Crystals != before.Crystals {
		t.Fatal("a raid takes only gold and Mana")
	}
	if result.Result.DamagePercent >= 30 && after.ShieldUntilMs == 0 {
		t.Fatal("a rival hit for 30% or more should be shielded")
	}
}

func TestRival_GuardsFight_AndTheSnapshotOutlivesLaterChanges(t *testing.T) {
	rival := raidReady(t, "river_scout", 2, 0)
	attacker := raidReady(t, "solar_beast_sage", 6, 6)
	start := raidRival(t, attacker, userIDOf(t, rival))

	// The rival pulls its hero off guard after the raid has started.
	for _, slot := range baseIDs(mustState(t, rival)) {
		if _, err := rival.rpc("sd_assign_hero", map[string]interface{}{"slot": slot, "instanceId": nil}); err != nil {
			t.Fatal(err)
		}
	}
	result, err := deploy(attacker, "k-"+randomHex(8), start.AttackID, legalPlacements(start))
	if err != nil {
		t.Fatal(err)
	}
	downs := 0
	for _, event := range result.Result.Events {
		if event[1] == 12 {
			downs++
		}
	}
	// Guards removed after the start still count: the raid fights the copy taken at the start.
	if downs != 1 || result.Result.DefendersAlive != 0 {
		t.Fatalf("downs %d alive %d, want the one guard down", downs, result.Result.DefendersAlive)
	}
}

// A second raid offers a share of what the rival has left, so one village cannot be drained.
func TestRival_SecondRaid_OffersAShareOfWhatIsLeft(t *testing.T) {
	rival := raidReady(t, "river_scout", 1, 0)
	attacker := raidReady(t, "solar_beast_sage", 6, 6)
	rivalID := userIDOf(t, rival)

	start := raidRival(t, attacker, rivalID)
	if _, err := deploy(attacker, "k-"+randomHex(8), start.AttackID, legalPlacements(start)); err != nil {
		t.Fatal(err)
	}
	left := mustState(t, rival)
	again := raidRival(t, attacker, rivalID)
	if again.Raid.AvailableGold != left.Village.Gold*20/100 || again.Raid.AvailableMana != left.Village.Mana*20/100 {
		t.Fatalf("second raid offers %d gold, want 20%% of the %d left", again.Raid.AvailableGold, left.Village.Gold)
	}
}

// The defense log names the attacker by their game name (never their account id), shows what was lost, and offers
// revenge, which works once.
func TestRival_DefenseLog_NamesTheAttacker_AndRevengeWorksOnce(t *testing.T) {
	rival := raidReady(t, "river_scout", 1, 0)
	attacker := raidReady(t, "solar_beast_sage", 6, 6)
	start := raidRival(t, attacker, userIDOf(t, rival))
	result, err := deploy(attacker, "k-"+randomHex(8), start.AttackID, legalPlacements(start))
	if err != nil {
		t.Fatal(err)
	}
	body, err := rival.rpc("sd_defense_log", nil)
	if err != nil {
		t.Fatal(err)
	}
	if strings.Contains(body, userIDOf(t, attacker)) {
		t.Fatalf("the log exposes the attacker's account id: %s", body)
	}
	var log defenseLog
	if err := json.Unmarshal([]byte(body), &log); err != nil {
		t.Fatal(err)
	}
	if len(log.Defenses) != 1 || log.Defenses[0].DefendersTotal != 1 || log.Defenses[0].AtMs == 0 || log.Defenses[0].AttackerName == "" ||
		log.Defenses[0].GoldLost != result.GoldEarned || log.Defenses[0].ManaLost != result.ManaEarned || !log.Defenses[0].CanRevenge {
		t.Fatalf("log %+v", log)
	}

	// Only the defender may take revenge, and only once.
	if _, err := attacker.rpc("sd_raid_revenge", map[string]interface{}{"key": "k-" + randomHex(8), "attackId": start.AttackID}); err == nil {
		t.Fatal("the attacker should not be able to take revenge on their own raid")
	}
	revengeBody, err := rival.rpc("sd_raid_revenge", map[string]interface{}{"key": "k-" + randomHex(8), "attackId": start.AttackID})
	if err != nil {
		t.Fatal(err)
	}
	var revenge raidStart
	if err := json.Unmarshal([]byte(revengeBody), &revenge); err != nil {
		t.Fatal(err)
	}
	if revenge.Raid.ID != "rival" || revenge.AttackID == "" || len(revenge.Defenders) == 0 {
		t.Fatalf("revenge raid %+v", revenge)
	}
	if _, err := rival.rpc("sd_raid_revenge", map[string]interface{}{"key": "k-" + randomHex(8), "attackId": start.AttackID}); err == nil || !strings.Contains(err.Error(), "REVENGE_USED") {
		t.Fatalf("a second revenge should be refused, got %v", err)
	}

	mine, err := attacker.rpc("sd_defense_log", nil)
	if err != nil {
		t.Fatal(err)
	}
	var empty defenseLog
	if err := json.Unmarshal([]byte(mine), &empty); err != nil {
		t.Fatal(err)
	}
	if len(empty.Defenses) != 0 {
		t.Fatalf("the attacker's own log should be empty: %+v", empty)
	}
}

func TestRival_YourselfOrNobody_IsRefused(t *testing.T) {
	attacker := raidReady(t, "solar_beast_sage", 1, 0)
	if _, err := attacker.rpc("sd_rival_start", map[string]interface{}{"key": "k-" + randomHex(8), "rivalId": userIDOf(t, attacker)}); err == nil {
		t.Fatal("raiding yourself should be refused")
	}
	if _, err := attacker.rpc("sd_rival_start", map[string]interface{}{"key": "k-" + randomHex(8), "rivalId": "00000000-0000-4000-8000-000000000000"}); err == nil {
		t.Fatal("raiding nobody should be refused")
	}
	if _, err := attacker.rpc("sd_rival_start", map[string]interface{}{"key": "k-" + randomHex(8), "rivalId": "not-a-uuid"}); err == nil {
		t.Fatal("a malformed rival id should be refused")
	}
}

func TestRival_NoTeam_IsRefused(t *testing.T) {
	rival := raidReady(t, "river_scout", 1, 0)
	empty := blank(t, 0)
	_, err := empty.rpc("sd_rival_start", map[string]interface{}{"key": "k-" + randomHex(8), "rivalId": userIDOf(t, rival)})
	if err == nil || !strings.Contains(err.Error(), "NO_TEAM") {
		t.Fatalf("got %v", err)
	}
}

func TestRival_ForgedResults_AreRefused(t *testing.T) {
	rival := raidReady(t, "river_scout", 1, 0)
	attacker := raidReady(t, "solar_beast_sage", 1, 0)
	start := raidRival(t, attacker, userIDOf(t, rival))
	for _, extra := range []string{"goldEarned", "stars", "defendersAlive", "defenders"} {
		_, err := attacker.rpc("sd_raid_deploy", map[string]interface{}{"key": "k-" + randomHex(8), "attackId": start.AttackID, "placements": legalPlacements(start), extra: 999})
		if err == nil {
			t.Fatalf("a request claiming %s should be refused", extra)
		}
	}
}

func TestRival_AnotherPlayersAttack_IsNotFound(t *testing.T) {
	rival := raidReady(t, "river_scout", 1, 0)
	attacker := raidReady(t, "solar_beast_sage", 1, 0)
	start := raidRival(t, attacker, userIDOf(t, rival))
	if _, err := deploy(rival, "k-"+randomHex(8), start.AttackID, legalPlacements(start)); err == nil {
		t.Fatal("another player's attack should not be found")
	}
}

func TestGarrison_HasGuards_AndTheyFight(t *testing.T) {
	p := raidReady(t, "solar_beast_sage", 6, 6)
	body, err := p.rpc("sd_raid_start", map[string]interface{}{"key": "k-" + randomHex(8), "raidId": "cinder_garrison"})
	if err != nil {
		t.Fatal(err)
	}
	var start raidStart
	if err := json.Unmarshal([]byte(body), &start); err != nil {
		t.Fatal(err)
	}
	if len(start.Defenders) != 1 {
		t.Fatalf("want 1 guard, got %d", len(start.Defenders))
	}
	result, err := deploy(p, "k-"+randomHex(8), start.AttackID, legalPlacements(start))
	if err != nil {
		t.Fatal(err)
	}
	fought := false
	for _, event := range result.Result.Events {
		if event[1] == 11 {
			fought = true
		}
	}
	if !fought {
		t.Fatal("the attackers should have hit a guard")
	}
}

func TestScriptedRaid_WithNoGuards_ReturnsAnEmptyList(t *testing.T) {
	p := raidReady(t, "solar_beast_sage", 1, 0)
	start := startRaid(t, p)
	if start.Defenders == nil || len(start.Defenders) != 0 {
		t.Fatalf("defenders %+v", start.Defenders)
	}
}

// In production no player can choose their rival, not even a real one. Runs with SD_DEV_MODE off.
func TestProductionMode_RivalCannotBeChosen(t *testing.T) {
	if os.Getenv("SD_PRODUCTION_TEST") != "1" {
		t.Skip("needs the server with dev mode off")
	}
	p := newPlayer(t)
	_, err := p.rpc("sd_rival_start", map[string]interface{}{"key": "k-" + randomHex(8), "rivalId": "00000000-0000-4000-8000-000000000000"})
	if err == nil || !strings.Contains(err.Error(), "RIVAL_NOT_ALLOWED") {
		t.Fatalf("got %v", err)
	}
	_, err = p.rpc("sd_rival_start", map[string]interface{}{"key": "k-" + randomHex(8)})
	if err == nil || !strings.Contains(err.Error(), "NO_TEAM") {
		t.Fatalf("an ordinary request should get as far as the team check, got %v", err)
	}
}

// legalPlacements lands the team on open ground, since a rival's village can stand anywhere.
func legalPlacements(start raidStart) []map[string]interface{} {
	placements := []map[string]interface{}{}
	for z := 0; z < 50 && len(placements) < len(start.Team); z++ {
		for x := 0; x < 50 && len(placements) < len(start.Team); x++ {
			open := true
			for _, b := range start.Buildings {
				if x >= b.X-2 && x < b.X+b.Width+2 && z >= b.Z-2 && z < b.Z+b.Depth+2 {
					open = false
					break
				}
			}
			if open {
				placements = append(placements, map[string]interface{}{"instanceId": start.Team[len(placements)].InstanceID, "x": x, "z": z})
			}
		}
	}
	return placements
}
