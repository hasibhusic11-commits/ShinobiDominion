package backendtests

import (
	"encoding/json"
	"strings"
	"sync"
	"testing"
)

type raidBuilding struct {
	ID        int    `json:"id"`
	X         int    `json:"x"`
	Z         int    `json:"z"`
	Width     int    `json:"width"`
	Depth     int    `json:"depth"`
	IsWall    bool   `json:"isWall"`
}

type raidStart struct {
	AttackID string `json:"attackId"`
	Raid     struct {
		ID          string `json:"id"`
		GoldPerStar int64  `json:"goldPerStar"`
		BestStars   int    `json:"bestStars"`
		AvailableGold int64 `json:"availableGold"`
		AvailableMana int64 `json:"availableMana"`
	} `json:"raid"`
	Buildings []raidBuilding `json:"buildings"`
	Defenders []raidGuard    `json:"defenders"`
	Team      []struct {
		InstanceID string `json:"instanceId"`
		Role       string `json:"role"`
		Hp         int64  `json:"hp"`
		Attack     int64  `json:"attack"`
	} `json:"team"`
}

type raidGuard struct {
	ID          string `json:"id"`
	ArchetypeID string `json:"archetypeId"`
	Hp          int64  `json:"hp"`
	Attack      int64  `json:"attack"`
	X           int    `json:"x"`
	Z           int    `json:"z"`
}

type raidDeploy struct {
	AttackID string `json:"attackId"`
	Result   struct {
		Stars         int       `json:"stars"`
		DamagePercent int       `json:"damagePercent"`
		EndTick       int       `json:"endTick"`
		DefendersAlive int      `json:"defendersAlive"`
		Checksum      string    `json:"checksum"`
		Events        [][]int64 `json:"events"`
	} `json:"result"`
	GoldEarned int64 `json:"goldEarned"`
	ManaEarned int64 `json:"manaEarned"`
	BestStars  int   `json:"bestStars"`
	State      state `json:"state"`
}

// raidReady gives a player real Hero Bases with heroes on them.
func raidReady(t *testing.T, archetype string, copies, maxLevelFirst int) *player {
	t.Helper()
	p := blank(t, 0)
	bases := heroBases(t, p)
	s := grant(t, p, map[string]interface{}{"archetypeId": archetype, "copies": copies})
	for i := 0; i < copies && i < len(bases); i++ {
		if _, err := p.rpc("sd_assign_hero", map[string]interface{}{"slot": bases[i], "instanceId": s.Heroes[i].InstanceID}); err != nil {
			t.Fatal(err)
		}
	}
	for i := 0; i < maxLevelFirst; i++ {
		grant(t, p, map[string]interface{}{"maxLevelFor": s.Heroes[i].InstanceID})
	}
	return p
}

func startRaid(t *testing.T, p *player) raidStart {
	t.Helper()
	body, err := p.rpc("sd_raid_start", map[string]interface{}{"key": "k-" + randomHex(8), "raidId": "ridge_outpost"})
	if err != nil {
		t.Fatal(err)
	}
	var start raidStart
	if err := json.Unmarshal([]byte(body), &start); err != nil {
		t.Fatal(err)
	}
	return start
}

func placementsFor(start raidStart) []map[string]interface{} {
	placements := []map[string]interface{}{}
	for i, member := range start.Team {
		placements = append(placements, map[string]interface{}{"instanceId": member.InstanceID, "x": 2 + i, "z": 2})
	}
	return placements
}

func deploy(p *player, key string, attackID string, placements []map[string]interface{}) (raidDeploy, error) {
	var result raidDeploy
	body, err := p.rpc("sd_raid_deploy", map[string]interface{}{"key": key, "attackId": attackID, "placements": placements})
	if err != nil {
		return result, err
	}
	return result, json.Unmarshal([]byte(body), &result)
}

func TestRaid_NoHeroesOnBases_IsRefused(t *testing.T) {
	p := blank(t, 0)
	_, err := p.rpc("sd_raid_start", map[string]interface{}{"key": "k-" + randomHex(8), "raidId": "ridge_outpost"})
	expectError(t, err, "NO_TEAM")
}

func TestRaid_UnknownRaid_IsRefused(t *testing.T) {
	p := raidReady(t, "river_scout", 1, 0)
	_, err := p.rpc("sd_raid_start", map[string]interface{}{"key": "k-" + randomHex(8), "raidId": "free_gold"})
	expectError(t, err, "RAID_NOT_FOUND")
}

func TestRaid_ServerDecidesTeamStats_FromBasesOnly(t *testing.T) {
	p := raidReady(t, "river_scout", 2, 0)
	start := startRaid(t, p)
	if len(start.Team) != 1 {
		t.Fatalf("team of %d, want the one hero on its base", len(start.Team))
	}
	if start.Team[0].Hp != 420 || start.Team[0].Attack != 60 || start.Team[0].Role != "Assassin" {
		t.Fatalf("stats %+v", start.Team[0])
	}
}

func TestRaid_FullBattle_PaysOnlyForNewStars(t *testing.T) {
	p := raidReady(t, "solar_beast_sage", 6, 6)
	grant(t, p, map[string]interface{}{"setGold": 0})

	first := startRaid(t, p)
	won, err := deploy(p, "k-"+randomHex(8), first.AttackID, placementsFor(first))
	if err != nil {
		t.Fatal(err)
	}
	if won.Result.Stars != 3 || won.Result.DamagePercent != 100 || len(won.Result.Events) == 0 {
		t.Fatalf("stars %d damage %d events %d", won.Result.Stars, won.Result.DamagePercent, len(won.Result.Events))
	}
	if won.GoldEarned != 3*first.Raid.GoldPerStar || won.State.Village.Gold != won.GoldEarned {
		t.Fatalf("earned %d, gold %d", won.GoldEarned, won.State.Village.Gold)
	}

	// Beating it again earns nothing more, so it can't be farmed, and raids are never blocked.
	second := startRaid(t, p)
	if second.Raid.BestStars != 3 {
		t.Fatalf("best %d", second.Raid.BestStars)
	}
	again, err := deploy(p, "k-"+randomHex(8), second.AttackID, placementsFor(second))
	if err != nil {
		t.Fatal(err)
	}
	if again.GoldEarned != 0 || again.State.Village.Gold != won.GoldEarned {
		t.Fatalf("second win paid %d, gold %d", again.GoldEarned, again.State.Village.Gold)
	}
}

func TestRaid_SameInput_GivesTheSameBattle(t *testing.T) {
	// Level 200 heroes earn no XP, so the second battle starts from the same stats.
	p := raidReady(t, "solar_beast_sage", 3, 3)
	one := startRaid(t, p)
	first, err := deploy(p, "k-"+randomHex(8), one.AttackID, placementsFor(one))
	if err != nil {
		t.Fatal(err)
	}
	two := startRaid(t, p)
	second, err := deploy(p, "k-"+randomHex(8), two.AttackID, placementsFor(two))
	if err != nil {
		t.Fatal(err)
	}
	// Instance ids are the same heroes in the same places, so the log must match.
	if first.Result.Checksum != second.Result.Checksum || first.Result.EndTick != second.Result.EndTick {
		t.Fatalf("%s/%d vs %s/%d", first.Result.Checksum, first.Result.EndTick, second.Result.Checksum, second.Result.EndTick)
	}
}

func TestRaid_ForgedResults_AreRefused(t *testing.T) {
	p := raidReady(t, "river_scout", 1, 0)
	start := startRaid(t, p)
	for _, forged := range []map[string]interface{}{
		{"key": "k-" + randomHex(8), "attackId": start.AttackID, "placements": placementsFor(start), "stars": 3},
		{"key": "k-" + randomHex(8), "attackId": start.AttackID, "placements": placementsFor(start), "goldEarned": 999999},
		{"key": "k-" + randomHex(8), "attackId": start.AttackID, "placements": placementsFor(start), "result": map[string]int{"stars": 3}},
	} {
		if _, err := p.rpc("sd_raid_deploy", forged); err == nil {
			t.Fatalf("forged request accepted: %v", forged)
		}
	}
	if s := mustState(t, p); s.Village.Gold != 10000-6*400 && s.Village.Gold < 0 {
		t.Fatal(s.Village.Gold)
	}
}

func TestRaid_IllegalPlacements_AreRefused(t *testing.T) {
	p := raidReady(t, "river_scout", 2, 0)
	stranger := raidReady(t, "river_scout", 1, 0)
	start := startRaid(t, p)
	foreign := startRaid(t, stranger)
	a := start.Team[0].InstanceID
	place := func(id string, x, z int) map[string]interface{} {
		return map[string]interface{}{"instanceId": id, "x": x, "z": z}
	}
	cases := []struct {
		want       string
		placements []map[string]interface{}
	}{
		{"NO_TEAM", []map[string]interface{}{}},
		{"BAD_DEPLOY_CELL", []map[string]interface{}{place(a, -1, 2)}},
		{"BAD_DEPLOY_CELL", []map[string]interface{}{place(a, 9999, 2)}},
		{"DEPLOY_TOO_CLOSE", []map[string]interface{}{place(a, 25, 25)}},
		{"DUPLICATE_HERO", []map[string]interface{}{place(a, 2, 2), place(a, 3, 3)}},
		{"HERO_NOT_IN_TEAM", []map[string]interface{}{place(foreign.Team[0].InstanceID, 2, 2)}},
		{"HERO_NOT_IN_TEAM", []map[string]interface{}{place("00000000-0000-0000-0000-000000000000", 2, 2)}},
	}
	for _, c := range cases {
		_, err := deploy(p, "k-"+randomHex(8), start.AttackID, c.placements)
		expectError(t, err, c.want)
	}
	// None of those used up the raid.
	if _, err := deploy(p, "k-"+randomHex(8), start.AttackID, []map[string]interface{}{place(a, 2, 2)}); err != nil {
		t.Fatal(err)
	}
}

func TestRaid_OtherPlayersAttack_IsNotFound(t *testing.T) {
	owner := raidReady(t, "river_scout", 1, 0)
	thief := raidReady(t, "river_scout", 1, 0)
	start := startRaid(t, owner)
	_, err := deploy(thief, "k-"+randomHex(8), start.AttackID, placementsFor(start))
	expectError(t, err, "ATTACK_NOT_FOUND")
}

func TestRaid_Replay_SameKeyReturnsSameResult_NewKeyIsRefused(t *testing.T) {
	p := raidReady(t, "solar_beast_sage", 6, 6)
	grant(t, p, map[string]interface{}{"setGold": 0})
	start := startRaid(t, p)
	key := "k-" + randomHex(8)
	first, err := deploy(p, key, start.AttackID, placementsFor(start))
	if err != nil {
		t.Fatal(err)
	}
	replay, err := deploy(p, key, start.AttackID, placementsFor(start))
	if err != nil {
		t.Fatal(err)
	}
	if replay.Result.Checksum != first.Result.Checksum || replay.GoldEarned != first.GoldEarned || replay.State.Village.Gold != first.State.Village.Gold {
		t.Fatal("replay differs")
	}
	_, err = deploy(p, "k-"+randomHex(8), start.AttackID, placementsFor(start))
	expectError(t, err, "ATTACK_ALREADY_RESOLVED")
	if s := mustState(t, p); s.Village.Gold != first.GoldEarned {
		t.Fatalf("paid twice: %d", s.Village.Gold)
	}
}

func TestRaid_ConcurrentDeploys_PayOnce(t *testing.T) {
	p := raidReady(t, "solar_beast_sage", 6, 6)
	grant(t, p, map[string]interface{}{"setGold": 0})
	start := startRaid(t, p)
	var wait sync.WaitGroup
	var lock sync.Mutex
	succeeded := 0
	for i := 0; i < 8; i++ {
		wait.Add(1)
		go func() {
			defer wait.Done()
			if _, err := deploy(p, "k-"+randomHex(8), start.AttackID, placementsFor(start)); err == nil {
				lock.Lock()
				succeeded++
				lock.Unlock()
			} else if !strings.Contains(err.Error(), "ATTACK_ALREADY_RESOLVED") && !strings.Contains(err.Error(), "429") && !strings.Contains(strings.ToUpper(err.Error()), "RATE") {
				t.Errorf("unexpected: %v", err)
			}
		}()
	}
	wait.Wait()
	if succeeded != 1 {
		t.Fatalf("%d deploys succeeded", succeeded)
	}
	if s := mustState(t, p); s.Village.Gold != 3*start.Raid.GoldPerStar {
		t.Fatalf("gold %d", s.Village.Gold)
	}
}

func TestRaid_NewRaid_AbandonsTheOldOne(t *testing.T) {
	p := raidReady(t, "river_scout", 1, 0)
	old := startRaid(t, p)
	startRaid(t, p)
	_, err := deploy(p, "k-"+randomHex(8), old.AttackID, placementsFor(old))
	expectError(t, err, "ATTACK_ALREADY_RESOLVED")
}

func TestRaid_ChangingHeroesAfterStart_DoesNotChangeTheRaid(t *testing.T) {
	p := raidReady(t, "solar_beast_sage", 1, 0)
	start := startRaid(t, p)
	hero := start.Team[0].InstanceID
	before := start.Team[0].Attack
	// Levelling up or taking the hero off its base after the raid began changes nothing.
	grant(t, p, map[string]interface{}{"maxLevelFor": hero})
	result, err := deploy(p, "k-"+randomHex(8), start.AttackID, placementsFor(start))
	if err != nil {
		t.Fatal(err)
	}
	var damage int64
	for _, event := range result.Result.Events {
		if event[1] == 2 {
			damage = event[4]
			break
		}
	}
	if damage > before*3/2 {
		t.Fatalf("first hit %d is beyond what the starting stats (%d) allow, even with an ability", damage, before)
	}
}
