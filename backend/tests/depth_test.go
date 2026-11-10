package backendtests

import (
	"encoding/json"
	"testing"
)

// Hero depth (ADR-053): technique ranks and talents, against a running server.

type depthHero struct {
	InstanceID    string `json:"instanceId"`
	TechniqueRank int    `json:"techniqueRank"`
	TalentID      string `json:"talentId"`
	TalentLevel   int    `json:"talentLevel"`
}

type depthState struct {
	Heroes []depthHero      `json:"heroes"`
	Items  map[string]int64 `json:"items"`
}

type depthMember struct {
	Hp            int64  `json:"hp"`
	Attack        int64  `json:"attack"`
	Defense       int64  `json:"defense"`
	Power         int    `json:"power"`
	StartCharge   int    `json:"startCharge"`
	SiegePercent  int    `json:"siegePercent"`
	SpeedPercent  int    `json:"speedPercent"`
	TechniqueRank int    `json:"techniqueRank"`
	TalentID      string `json:"talentId"`
	TalentLevel   int    `json:"talentLevel"`
}

func depthOf(t *testing.T, p *player) depthState {
	t.Helper()
	body, err := p.rpc("sd_get_state", "{}")
	if err != nil {
		t.Fatal(err)
	}
	var s depthState
	if err := json.Unmarshal([]byte(body), &s); err != nil {
		t.Fatal(err)
	}
	return s
}

// depthTeam starts a raid and returns the first team member as the server will fight it.
func depthTeam(t *testing.T, p *player) depthMember {
	t.Helper()
	body, err := p.rpc("sd_raid_start", map[string]interface{}{"key": "k-" + randomHex(8), "raidId": "ridge_outpost"})
	if err != nil {
		t.Fatal(err)
	}
	var start struct {
		Team []depthMember `json:"team"`
	}
	if err := json.Unmarshal([]byte(body), &start); err != nil {
		t.Fatal(err)
	}
	if len(start.Team) == 0 {
		t.Fatal("no team")
	}
	return start.Team[0]
}

func TestDepth_RankUp_CostsManuals_NeedsLevels_AndStrengthensTheSpecial(t *testing.T) {
	p := raidReady(t, "stone_titan", 1, 0)
	heroID := depthOf(t, p).Heroes[0].InstanceID
	before := depthTeam(t, p)
	if before.TechniqueRank != 1 || before.Power != 100 {
		t.Fatalf("a new Legendary is rank 1 with power 100: %+v", before)
	}
	rankUp := func(key, id string) (string, error) {
		return p.rpc("sd_technique_rank_up", map[string]interface{}{"key": key, "instanceId": id})
	}

	// Rank 2 needs hero level 16; this hero is level 1, and the refusal takes nothing.
	grant(t, p, map[string]interface{}{"items": map[string]int64{"technique_manual": 5}})
	_, err := rankUp("r-"+randomHex(8), heroID)
	expectError(t, err, "HERO_LEVEL_TOO_LOW_FOR_RANK")
	if got := depthOf(t, p).Items["technique_manual"]; got != 5 {
		t.Fatalf("a refused rank-up must not take manuals: %d left", got)
	}
	grant(t, p, map[string]interface{}{"maxLevelFor": heroID, "clearItems": true})
	before = depthTeam(t, p)

	// Rank 2 costs 2 manuals; the player has none.
	_, err = rankUp("r-"+randomHex(8), heroID)
	expectError(t, err, "NOT_ENOUGH_MANUALS")
	grant(t, p, map[string]interface{}{"items": map[string]int64{"technique_manual": 1}})
	_, err = rankUp("r-"+randomHex(8), heroID)
	expectError(t, err, "NOT_ENOUGH_MANUALS")
	if got := depthOf(t, p).Items["technique_manual"]; got != 1 {
		t.Fatalf("a refused rank-up must not take manuals: %d left", got)
	}

	grant(t, p, map[string]interface{}{"items": map[string]int64{"technique_manual": 9}})
	key := "r-" + randomHex(8)
	first, err := rankUp(key, heroID)
	if err != nil {
		t.Fatal(err)
	}
	s := depthOf(t, p)
	if s.Heroes[0].TechniqueRank != 2 || s.Items["technique_manual"] != 8 {
		t.Fatalf("rank 2 costs 2 manuals: rank %d, %d manuals left", s.Heroes[0].TechniqueRank, s.Items["technique_manual"])
	}
	// The same key again answers the same and takes nothing more.
	second, err := rankUp(key, heroID)
	if err != nil {
		t.Fatal(err)
	}
	if first != second {
		t.Fatal("a retried key must return the stored answer")
	}
	if s := depthOf(t, p); s.Heroes[0].TechniqueRank != 2 || s.Items["technique_manual"] != 8 {
		t.Fatalf("a retried key ranked up twice: %+v", s)
	}

	_, err = rankUp("r-"+randomHex(8), "00000000-0000-0000-0000-000000000000")
	expectError(t, err, "HERO_NOT_FOUND")
	// A client cannot name the rank it wants.
	_, err = p.rpc("sd_technique_rank_up", map[string]interface{}{"key": "r-" + randomHex(8), "instanceId": heroID, "rank": 10})
	if err == nil {
		t.Fatal("an unknown field must be refused")
	}

	after := depthTeam(t, p)
	if after.Power != 108 || after.TechniqueRank != 2 || after.Hp != before.Hp || after.Attack != before.Attack {
		t.Fatalf("rank 2 adds 8 power and nothing else: %+v -> %+v", before, after)
	}

	// The hero can go all the way to rank 10 and no further: 2+4+...+18 = 90 manuals in all.
	grant(t, p, map[string]interface{}{"items": map[string]int64{"technique_manual": 100}})
	for rank := 3; rank <= 10; rank++ {
		if _, err := rankUp("r-"+randomHex(8), heroID); err != nil {
			t.Fatalf("rank %d: %v", rank, err)
		}
	}
	_, err = rankUp("r-"+randomHex(8), heroID)
	expectError(t, err, "TECHNIQUE_AT_MAX")
	s = depthOf(t, p)
	if s.Heroes[0].TechniqueRank != 10 || s.Items["technique_manual"] != 8+100-88 {
		t.Fatalf("rank 10 after 88 more manuals: rank %d, %d left", s.Heroes[0].TechniqueRank, s.Items["technique_manual"])
	}
	if top := depthTeam(t, p); top.Power != 172 {
		t.Fatalf("a rank 10 Legendary has power 172, got %d", top.Power)
	}
	ledgersAgree(t, p)
}

func TestDepth_TalentRoll_CostsAToken_IsTheServers_AndCountsInBattle(t *testing.T) {
	p := raidReady(t, "stone_titan", 1, 0)
	heroID := depthOf(t, p).Heroes[0].InstanceID
	before := depthTeam(t, p)
	if before.TalentID != "" || before.TalentLevel != 0 {
		t.Fatalf("a new hero has no talent: %+v", before)
	}
	roll := func(key string) (string, error) {
		return p.rpc("sd_talent_roll", map[string]interface{}{"key": key, "instanceId": heroID})
	}
	_, err := roll("t-" + randomHex(8))
	expectError(t, err, "NO_TALENT_TOKEN")

	grant(t, p, map[string]interface{}{"items": map[string]int64{"talent_token": 40}})
	key := "t-" + randomHex(8)
	first, err := roll(key)
	if err != nil {
		t.Fatal(err)
	}
	s := depthOf(t, p)
	hero := s.Heroes[0]
	if hero.TalentID == "" || hero.TalentLevel < 1 || hero.TalentLevel > 5 || s.Items["talent_token"] != 39 {
		t.Fatalf("one roll gives a talent at level 1 to 5 for one token: %+v, %d tokens", hero, s.Items["talent_token"])
	}
	// A retried key must give back the roll it made, never a second one.
	for i := 0; i < 3; i++ {
		again, err := roll(key)
		if err != nil {
			t.Fatal(err)
		}
		if again != first {
			t.Fatal("a retried key rolled again")
		}
	}
	if s := depthOf(t, p); s.Items["talent_token"] != 39 || s.Heroes[0] != hero {
		t.Fatalf("a retried key changed something: %+v", s)
	}
	// A client cannot name the talent it wants.
	_, err = p.rpc("sd_talent_roll", map[string]interface{}{"key": "t-" + randomHex(8), "instanceId": heroID, "talentId": "keen_edge"})
	if err == nil {
		t.Fatal("an unknown field must be refused")
	}
	_, err = p.rpc("sd_talent_roll", map[string]interface{}{"key": "t-" + randomHex(8), "instanceId": "00000000-0000-0000-0000-000000000000"})
	expectError(t, err, "HERO_NOT_FOUND")

	// Whatever came up is what the battle uses: the raid team carries it, worked out by the server.
	perLevel := map[string]int{"iron_body": 4, "keen_edge": 4, "stone_skin": 6, "ready_stance": 1, "siege_breaker": 8, "swift_feet": 6}
	check := func(hero depthHero) {
		t.Helper()
		got := depthTeam(t, p)
		want := before
		want.TalentID, want.TalentLevel = hero.TalentID, hero.TalentLevel
		amount := perLevel[hero.TalentID] * hero.TalentLevel
		switch hero.TalentID {
		case "iron_body":
			want.Hp = before.Hp * int64(100+amount) / 100
		case "keen_edge":
			want.Attack = before.Attack * int64(100+amount) / 100
		case "stone_skin":
			want.Defense = before.Defense * int64(100+amount) / 100
		case "ready_stance":
			want.StartCharge = amount
		case "siege_breaker":
			want.SiegePercent = amount
		case "swift_feet":
			want.SpeedPercent = amount
		default:
			t.Fatalf("an unknown talent came up: %q", hero.TalentID)
		}
		if got != want {
			t.Fatalf("%s level %d in battle: got %+v, want %+v", hero.TalentID, hero.TalentLevel, got, want)
		}
	}
	check(hero)

	// More rolls each take one token and replace the talent; over 30 of them more than one talent comes up.
	seen := map[string]bool{hero.TalentID: true}
	for i := 0; i < 30; i++ {
		if _, err := roll("t-" + randomHex(8)); err != nil {
			t.Fatal(err)
		}
	}
	s = depthOf(t, p)
	if s.Items["talent_token"] != 9 {
		t.Fatalf("31 rolls take 31 tokens: %d left", s.Items["talent_token"])
	}
	seen[s.Heroes[0].TalentID] = true
	check(s.Heroes[0])
	ledgersAgree(t, p)
}

func TestDepth_ADungeonWinPaysAManual(t *testing.T) {
	p := raidReady(t, "solar_beast_sage", 6, 6)
	start, err := startStage(t, p, "ridge_outpost")
	if err != nil {
		t.Fatal(err)
	}
	body, err := p.rpc("sd_raid_deploy", map[string]interface{}{"key": "k-" + randomHex(8), "attackId": start.AttackID, "placements": legalPlacements(start)})
	if err != nil {
		t.Fatal(err)
	}
	var result struct {
		Result struct {
			Stars int `json:"stars"`
		} `json:"result"`
		ItemsEarned map[string]int64 `json:"itemsEarned"`
	}
	if err := json.Unmarshal([]byte(body), &result); err != nil {
		t.Fatal(err)
	}
	if result.Result.Stars == 0 {
		t.Fatal("six max-level Legendaries should win the first stage")
	}
	if result.ItemsEarned["technique_manual"] != 1 || result.ItemsEarned["talent_token"] != 0 {
		t.Fatalf("a paid dungeon win pays one manual and no token: %v", result.ItemsEarned)
	}
	if got := depthOf(t, p).Items["technique_manual"]; got != 1 {
		t.Fatalf("the manual is in the inventory: %d", got)
	}
	ledgersAgree(t, p)
}
