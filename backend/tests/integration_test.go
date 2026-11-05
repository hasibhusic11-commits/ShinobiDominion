// Black-box tests against a running server, over the same HTTP API a game client uses.
package backendtests

import (
	"bytes"
	"crypto/rand"
	"encoding/base64"
	"encoding/hex"
	"encoding/json"
	"fmt"
	"io"
	"math"
	"net/http"
	"net/url"
	"os"
	"strings"
	"sync"
	"sync/atomic"
	"testing"
	"time"
)

var (
	baseURL   = envOr("NAKAMA_URL", "http://127.0.0.1:7350")
	serverKey = os.Getenv("SD_SERVER_KEY")
	httpKey   = os.Getenv("SD_HTTP_KEY")
	client    = &http.Client{Timeout: 60 * time.Second}
)

func envOr(name, fallback string) string {
	if value := os.Getenv(name); value != "" {
		return value
	}
	return fallback
}

func randomHex(n int) string {
	buffer := make([]byte, n)
	rand.Read(buffer)
	return hex.EncodeToString(buffer)
}

type player struct {
	token string
}

func newPlayer(t testing.TB) *player {
	t.Helper()
	body, _ := json.Marshal(map[string]string{"id": "test-" + randomHex(16)})
	request, _ := http.NewRequest("POST", baseURL+"/v2/account/authenticate/device?create=true", bytes.NewReader(body))
	request.Header.Set("Authorization", "Basic "+base64.StdEncoding.EncodeToString([]byte(serverKey+":")))
	request.Header.Set("Content-Type", "application/json")
	response, err := client.Do(request)
	if err != nil {
		t.Fatal(err)
	}
	defer response.Body.Close()
	var session struct {
		Token string `json:"token"`
	}
	data, _ := io.ReadAll(response.Body)
	if err := json.Unmarshal(data, &session); err != nil || session.Token == "" {
		t.Fatalf("auth failed: %d %s", response.StatusCode, data)
	}
	return &player{token: session.Token}
}

type rpcError struct {
	Status  int
	Message string
}

func (e *rpcError) Error() string { return fmt.Sprintf("%d %s", e.Status, e.Message) }

func (p *player) rpc(id string, payload interface{}) (string, error) {
	var encoded []byte
	switch value := payload.(type) {
	case string:
		encoded = []byte(value)
	default:
		encoded, _ = json.Marshal(value)
	}
	wrapped, _ := json.Marshal(string(encoded))
	request, _ := http.NewRequest("POST", baseURL+"/v2/rpc/"+id, bytes.NewReader(wrapped))
	if p != nil {
		request.Header.Set("Authorization", "Bearer "+p.token)
	}
	request.Header.Set("Content-Type", "application/json")
	response, err := client.Do(request)
	if err != nil {
		return "", err
	}
	defer response.Body.Close()
	data, _ := io.ReadAll(response.Body)
	if response.StatusCode != 200 {
		var failure struct {
			Message string `json:"message"`
		}
		json.Unmarshal(data, &failure)
		return "", &rpcError{response.StatusCode, failure.Message}
	}
	var result struct {
		Payload string `json:"payload"`
	}
	if err := json.Unmarshal(data, &result); err != nil {
		return "", err
	}
	return result.Payload, nil
}

type heroView struct {
	InstanceID     string `json:"instanceId"`
	ArchetypeID    string `json:"archetypeId"`
	EvolutionIndex int    `json:"evolutionIndex"`
	Level          int    `json:"level"`
	Locked         bool   `json:"locked"`
	BaseSlot       *int   `json:"baseSlot"`
}

type state struct {
	Crystals int64            `json:"crystals"`
	Heroes   []heroView       `json:"heroes"`
	Items    map[string]int64 `json:"items"`
}

type roll struct {
	RollIndex   int    `json:"rollIndex"`
	InstanceID  string `json:"instanceId"`
	ArchetypeID string `json:"archetypeId"`
	Rarity      string `json:"rarity"`
}

type summonResult struct {
	PoolVersion int    `json:"poolVersion"`
	Rolls       []roll `json:"rolls"`
	State       state  `json:"state"`
}

type odds struct {
	PoolVersion int    `json:"poolVersion"`
	Status      string `json:"status"`
	Price       int64  `json:"priceCrystalsPerRoll"`
	TotalWeight int64  `json:"totalWeight"`
	Items       []struct {
		ArchetypeID string `json:"archetypeId"`
		Rarity      string `json:"rarity"`
		Weight      int64  `json:"weight"`
		Chance      string `json:"chance"`
	} `json:"items"`
	RarityChance map[string]string `json:"rarityChance"`
}

func mustState(t testing.TB, p *player) state {
	t.Helper()
	body, err := p.rpc("sd_get_state", "{}")
	if err != nil {
		t.Fatal(err)
	}
	var s state
	if err := json.Unmarshal([]byte(body), &s); err != nil {
		t.Fatal(err)
	}
	return s
}

func mustOdds(t testing.TB, p *player) odds {
	t.Helper()
	body, err := p.rpc("sd_summon_odds", "{}")
	if err != nil {
		t.Fatal(err)
	}
	var o odds
	if err := json.Unmarshal([]byte(body), &o); err != nil {
		t.Fatal(err)
	}
	return o
}

func grant(t testing.TB, p *player, payload map[string]interface{}) state {
	t.Helper()
	body, err := p.rpc("sd_dev_grant", payload)
	if err != nil {
		t.Fatal(err)
	}
	var s state
	json.Unmarshal([]byte(body), &s)
	return s
}

// blank gives a player nothing but what the test grants.
func blank(t testing.TB, crystals int64) *player {
	t.Helper()
	p := newPlayer(t)
	grant(t, p, map[string]interface{}{"clearHeroes": true, "clearItems": true, "setCrystals": crystals})
	return p
}

func summon(p *player, poolVersion, count int, key string) (summonResult, error) {
	var result summonResult
	body, err := p.rpc("sd_summon", map[string]interface{}{"poolVersion": poolVersion, "count": count, "key": key})
	if err != nil {
		return result, err
	}
	return result, json.Unmarshal([]byte(body), &result)
}

func expectError(t *testing.T, err error, message string) {
	t.Helper()
	if err == nil {
		t.Fatalf("expected %s, got success", message)
	}
	if !strings.Contains(err.Error(), message) {
		t.Fatalf("expected %s, got %v", message, err)
	}
}

// ---------- Authentication ----------

func TestAuth_NoTokenIsRefused(t *testing.T) {
	for _, id := range []string{"sd_get_state", "sd_summon", "sd_evolve", "sd_summon_odds"} {
		var anonymous *player
		_, err := anonymous.rpc(id, "{}")
		if err == nil || err.(*rpcError).Status != 401 {
			t.Errorf("%s without a token must return 401, got %v", id, err)
		}
	}
}

func TestAuth_ServerKeyWithNoPlayerIsRefused(t *testing.T) {
	wrapped, _ := json.Marshal(`{"poolVersion":1,"count":1,"key":"abcdefgh"}`)
	address := baseURL + "/v2/rpc/sd_summon?http_key=" + url.QueryEscape(httpKey)
	response, err := client.Post(address, "application/json", bytes.NewReader(wrapped))
	if err != nil {
		t.Fatal(err)
	}
	defer response.Body.Close()
	data, _ := io.ReadAll(response.Body)
	if response.StatusCode == 200 || !strings.Contains(string(data), "UNAUTHENTICATED") {
		t.Fatalf("expected UNAUTHENTICATED, got %d %s", response.StatusCode, data)
	}
}

func TestDevSeed_NeverContainsIncompleteHeroes(t *testing.T) {
	for _, hero := range mustState(t, newPlayer(t)).Heroes {
		switch hero.ArchetypeID {
		case "crimson_eye_prodigy", "void_sovereign", "ashen_flame_lord", "storm_crow_shinobi":
			t.Fatalf("incomplete hero handed out: %s", hero.ArchetypeID)
		}
	}
}

// ---------- Odds ----------

func TestOdds_ExactAndHonest(t *testing.T) {
	p := newPlayer(t)
	o := mustOdds(t, p)
	if !strings.Contains(o.Status, "NOT APPROVED") {
		t.Fatalf("test rates must say so: %q", o.Status)
	}
	var sum int64
	for _, item := range o.Items {
		sum += item.Weight
		expected := fmt.Sprintf("%.3f%%", float64(item.Weight)*100/float64(o.TotalWeight))
		if item.Chance != expected {
			t.Errorf("%s displays %s, weight implies %s", item.ArchetypeID, item.Chance, expected)
		}
	}
	if sum != o.TotalWeight {
		t.Fatalf("weights %d != total %d", sum, o.TotalWeight)
	}
	raw, _ := p.rpc("sd_summon_odds", "{}")
	lower := strings.ToLower(raw)
	for _, banned := range []string{"pity", "guarantee", "bonus_roll", "featured"} {
		if strings.Contains(lower, banned) {
			t.Errorf("odds mention %q", banned)
		}
	}
}

// ---------- Summoning ----------

func TestSummon_SingleAndTen_ChargeExactly_AndMintExactly(t *testing.T) {
	p := blank(t, 5000)
	o := mustOdds(t, p)

	one, err := summon(p, o.PoolVersion, 1, "k-"+randomHex(8))
	if err != nil {
		t.Fatal(err)
	}
	if len(one.Rolls) != 1 || one.State.Crystals != 5000-o.Price || len(one.State.Heroes) != 1 {
		t.Fatalf("single: %+v", one)
	}

	ten, err := summon(p, o.PoolVersion, 10, "k-"+randomHex(8))
	if err != nil {
		t.Fatal(err)
	}
	if len(ten.Rolls) != 10 || ten.State.Crystals != 5000-11*o.Price || len(ten.State.Heroes) != 11 {
		t.Fatalf("ten: rolls=%d crystals=%d heroes=%d", len(ten.Rolls), ten.State.Crystals, len(ten.State.Heroes))
	}
	seen := map[string]bool{}
	for i, r := range ten.Rolls {
		if r.RollIndex != i || seen[r.InstanceID] {
			t.Fatalf("rolls must be distinct and ordered: %+v", ten.Rolls)
		}
		seen[r.InstanceID] = true
	}
}

func TestSummon_NoFundsChangesNothing(t *testing.T) {
	p := blank(t, 150)
	o := mustOdds(t, p)
	_, err := summon(p, o.PoolVersion, 10, "k-"+randomHex(8))
	expectError(t, err, "INSUFFICIENT_FUNDS")
	s := mustState(t, p)
	if s.Crystals != 150 || len(s.Heroes) != 0 {
		t.Fatalf("failed summon changed state: %+v", s)
	}
}

func TestSummon_ForgedRequestsAreRefused(t *testing.T) {
	p := blank(t, 5000)
	o := mustOdds(t, p)
	cases := []string{
		fmt.Sprintf(`{"poolVersion":%d,"count":1,"key":"forged-key-1","results":["solar_beast_sage"]}`, o.PoolVersion),
		fmt.Sprintf(`{"poolVersion":%d,"count":1,"key":"forged-key-2","seed":12345}`, o.PoolVersion),
		fmt.Sprintf(`{"poolVersion":%d,"count":1,"key":"forged-key-3","price":0}`, o.PoolVersion),
		fmt.Sprintf(`{"poolVersion":%d,"count":5,"key":"forged-key-4"}`, o.PoolVersion),
		fmt.Sprintf(`{"poolVersion":%d,"count":0,"key":"forged-key-5"}`, o.PoolVersion),
		fmt.Sprintf(`{"poolVersion":%d,"count":-10,"key":"forged-key-6"}`, o.PoolVersion),
		fmt.Sprintf(`{"poolVersion":%d,"count":11,"key":"forged-key-7"}`, o.PoolVersion),
		fmt.Sprintf(`{"poolVersion":%d,"count":1,"key":"x"}`, o.PoolVersion),
		fmt.Sprintf(`{"poolVersion":%d,"count":1}`, o.PoolVersion),
		fmt.Sprintf(`{"poolVersion":%d,"count":1,"key":"bad key; drop table"}`, o.PoolVersion),
		`not json at all`,
	}
	for _, payload := range cases {
		if _, err := p.rpc("sd_summon", payload); err == nil {
			t.Errorf("must be refused: %s", payload)
		}
	}
	_, err := summon(p, o.PoolVersion+1, 1, "k-"+randomHex(8))
	expectError(t, err, "POOL_VERSION_MISMATCH")

	s := mustState(t, p)
	if s.Crystals != 5000 || len(s.Heroes) != 0 {
		t.Fatalf("a refused request changed state: %+v", s)
	}
}

func TestSummon_ReplaySameKey_ReturnsSameResult_WithoutRecharging(t *testing.T) {
	p := blank(t, 5000)
	o := mustOdds(t, p)
	key := "k-" + randomHex(8)
	first, err := summon(p, o.PoolVersion, 10, key)
	if err != nil {
		t.Fatal(err)
	}
	for i := 0; i < 5; i++ {
		again, err := summon(p, o.PoolVersion, 10, key)
		if err != nil {
			t.Fatal(err)
		}
		for j := range first.Rolls {
			if again.Rolls[j] != first.Rolls[j] {
				t.Fatalf("replay changed roll %d", j)
			}
		}
	}
	s := mustState(t, p)
	if s.Crystals != 5000-10*o.Price || len(s.Heroes) != 10 {
		t.Fatalf("replay double-spent: crystals=%d heroes=%d", s.Crystals, len(s.Heroes))
	}
}

func TestSummon_SameKeyDifferentRequest_IsRefused(t *testing.T) {
	p := blank(t, 5000)
	o := mustOdds(t, p)
	key := "k-" + randomHex(8)
	if _, err := summon(p, o.PoolVersion, 1, key); err != nil {
		t.Fatal(err)
	}
	_, err := summon(p, o.PoolVersion, 10, key)
	expectError(t, err, "KEY_REUSED")
}

func TestSummon_KeysAreScopedPerPlayer(t *testing.T) {
	a, b := blank(t, 1000), blank(t, 1000)
	o := mustOdds(t, a)
	key := "shared-" + randomHex(8)
	if _, err := summon(a, o.PoolVersion, 1, key); err != nil {
		t.Fatal(err)
	}
	result, err := summon(b, o.PoolVersion, 1, key)
	if err != nil {
		t.Fatal(err)
	}
	if len(result.State.Heroes) != 1 || result.State.Heroes[0].InstanceID != result.Rolls[0].InstanceID {
		t.Fatalf("player b must get their own hero: %+v", result)
	}
}

func TestSummon_ConcurrentSameKey_ChargesOnce(t *testing.T) {
	p := blank(t, 5000)
	o := mustOdds(t, p)
	key := "k-" + randomHex(8)
	const attempts = 40
	results := make([]summonResult, attempts)
	failures := make([]error, attempts)
	var wg sync.WaitGroup
	for i := 0; i < attempts; i++ {
		wg.Add(1)
		go func(i int) {
			defer wg.Done()
			results[i], failures[i] = summon(p, o.PoolVersion, 10, key)
		}(i)
	}
	wg.Wait()
	for i := range results {
		if failures[i] != nil {
			t.Fatalf("attempt %d: %v", i, failures[i])
		}
		for j := range results[0].Rolls {
			if results[i].Rolls[j] != results[0].Rolls[j] {
				t.Fatal("simultaneous requests saw different results")
			}
		}
	}
	s := mustState(t, p)
	if s.Crystals != 5000-10*o.Price || len(s.Heroes) != 10 {
		t.Fatalf("crystals=%d heroes=%d", s.Crystals, len(s.Heroes))
	}
}

func TestSummon_ConcurrentDifferentKeys_CannotOverspend(t *testing.T) {
	o := mustOdds(t, newPlayer(t))
	const affordable = 7
	p := blank(t, affordable*o.Price+o.Price/2)
	const attempts = 40
	var success, refused int64
	var wg sync.WaitGroup
	for i := 0; i < attempts; i++ {
		wg.Add(1)
		go func() {
			defer wg.Done()
			_, err := summon(p, o.PoolVersion, 1, "k-"+randomHex(8))
			if err == nil {
				atomic.AddInt64(&success, 1)
			} else if strings.Contains(err.Error(), "INSUFFICIENT_FUNDS") {
				atomic.AddInt64(&refused, 1)
			} else {
				t.Errorf("unexpected: %v", err)
			}
		}()
	}
	wg.Wait()
	s := mustState(t, p)
	if success != affordable || refused != attempts-affordable || len(s.Heroes) != affordable || s.Crystals != o.Price/2 {
		t.Fatalf("success=%d refused=%d heroes=%d crystals=%d", success, refused, len(s.Heroes), s.Crystals)
	}
}

func TestSummon_DistributionMatchesOdds_AndNoHiddenGuarantee(t *testing.T) {
	p := blank(t, 0)
	o := mustOdds(t, p)
	const tens = 400
	grant(t, p, map[string]interface{}{"setCrystals": o.Price * 10 * tens})

	rarityWeight := map[string]int64{}
	for _, item := range o.Items {
		rarityWeight[item.Rarity] += item.Weight
	}
	counts := map[string]int{}
	tensWithNothingAboveCommon := 0
	tensWithNoLegendary := 0
	legendaryAtRoll := make([]int, 10)
	for i := 0; i < tens; i++ {
		result, err := summon(p, o.PoolVersion, 10, "k-"+randomHex(8))
		if err != nil {
			t.Fatal(err)
		}
		aboveCommon, legendary := 0, 0
		for j, r := range result.Rolls {
			counts[r.Rarity]++
			if r.Rarity != "Common" {
				aboveCommon++
			}
			if r.Rarity == "Legendary" {
				legendary++
				legendaryAtRoll[j]++
			}
		}
		if aboveCommon == 0 {
			tensWithNothingAboveCommon++
		}
		if legendary == 0 {
			tensWithNoLegendary++
		}
	}

	total := float64(tens * 10)
	for rarity, weight := range rarityWeight {
		probability := float64(weight) / float64(o.TotalWeight)
		expected := probability * total
		limit := 6 * math.Sqrt(total*probability*(1-probability))
		if math.Abs(float64(counts[rarity])-expected) > limit {
			t.Errorf("%s: got %d, expected about %.0f", rarity, counts[rarity], expected)
		}
	}
	// With rates like these, some tens must come up empty. If none ever do, something is propping them up.
	if tensWithNothingAboveCommon == 0 {
		t.Error("every ten-pull contained an above-Common hero, which suggests a hidden guarantee")
	}
	if tensWithNoLegendary == 0 {
		t.Error("every ten-pull contained a Legendary, which suggests a hidden guarantee")
	}
	// The last roll of a ten must be no better than the others: a classic disguised guarantee.
	p10 := float64(rarityWeight["Legendary"]) / float64(o.TotalWeight)
	expected := p10 * tens
	if limit := 6 * math.Sqrt(tens*p10*(1-p10)); math.Abs(float64(legendaryAtRoll[9])-expected) > limit {
		t.Errorf("tenth roll is special: %d Legendaries, expected about %.0f", legendaryAtRoll[9], expected)
	}
}

func TestSummon_OddsDoNotShiftAfterManyFailures(t *testing.T) {
	// A pity system would raise Legendary rates after a long run without one, so compare
	// each roll that followed a dry streak against the base rate.
	p := blank(t, 0)
	o := mustOdds(t, p)
	const tens = 400
	grant(t, p, map[string]interface{}{"setCrystals": o.Price * 10 * tens})
	var legendaryRate float64
	for _, item := range o.Items {
		if item.Rarity == "Legendary" {
			legendaryRate += float64(item.Weight) / float64(o.TotalWeight)
		}
	}
	streak, afterStreak, legendaryAfterStreak := 0, 0, 0
	for i := 0; i < tens; i++ {
		result, err := summon(p, o.PoolVersion, 10, "k-"+randomHex(8))
		if err != nil {
			t.Fatal(err)
		}
		for _, r := range result.Rolls {
			if streak >= 49 {
				afterStreak++
				if r.Rarity == "Legendary" {
					legendaryAfterStreak++
				}
			}
			if r.Rarity == "Legendary" {
				streak = 0
			} else {
				streak++
			}
		}
	}
	if afterStreak >= 100 {
		expected := legendaryRate * float64(afterStreak)
		limit := 6 * math.Sqrt(float64(afterStreak)*legendaryRate*(1-legendaryRate))
		if float64(legendaryAfterStreak) > expected+limit {
			t.Fatalf("after 49 dry rolls, Legendaries jumped: %d of %d", legendaryAfterStreak, afterStreak)
		}
	}
}

// ---------- Evolution ----------

const legend = "solar_beast_sage"

func evolve(p *player, key, main, duplicate string) (state, error) {
	var s state
	body, err := p.rpc("sd_evolve", map[string]string{"key": key, "mainInstanceId": main, "duplicateInstanceId": duplicate})
	if err != nil {
		return s, err
	}
	return s, json.Unmarshal([]byte(body), &s)
}

func evolutionSetup(t testing.TB) (*player, string, string) {
	t.Helper()
	p := blank(t, 0)
	grant(t, p, map[string]interface{}{"archetypeId": legend, "level": 200})
	s := grant(t, p, map[string]interface{}{"archetypeId": legend, "level": 1, "items": map[string]int64{"awakening_scroll": 1}})
	var main, duplicate string
	for _, hero := range s.Heroes {
		if hero.Level == 200 {
			main = hero.InstanceID
		} else {
			duplicate = hero.InstanceID
		}
	}
	return p, main, duplicate
}

func TestEvolve_ConsumesExactlyOneOfEach_KeepsMain(t *testing.T) {
	p, main, duplicate := evolutionSetup(t)
	s, err := evolve(p, "k-"+randomHex(8), main, duplicate)
	if err != nil {
		t.Fatal(err)
	}
	if len(s.Heroes) != 1 || s.Heroes[0].InstanceID != main {
		t.Fatalf("main must survive alone: %+v", s.Heroes)
	}
	if s.Heroes[0].EvolutionIndex != 1 || s.Heroes[0].Level != 1 {
		t.Fatalf("expected Evo I level 1: %+v", s.Heroes[0])
	}
	if s.Items["awakening_scroll"] != 0 {
		t.Fatalf("scroll not spent: %+v", s.Items)
	}
}

func TestEvolve_RefusesEveryBrokenRule(t *testing.T) {
	t.Run("level below 200", func(t *testing.T) {
		p := blank(t, 0)
		grant(t, p, map[string]interface{}{"archetypeId": legend, "level": 199, "copies": 2, "items": map[string]int64{"awakening_scroll": 1}})
		s := mustState(t, p)
		_, err := evolve(p, "k-"+randomHex(8), s.Heroes[0].InstanceID, s.Heroes[1].InstanceID)
		expectError(t, err, "LEVEL_TOO_LOW")
	})
	t.Run("wrong hero of same rarity", func(t *testing.T) {
		p := blank(t, 0)
		grant(t, p, map[string]interface{}{"archetypeId": legend, "level": 200, "items": map[string]int64{"awakening_scroll": 1}})
		s := grant(t, p, map[string]interface{}{"archetypeId": "thunder_empress"})
		var main, other string
		for _, h := range s.Heroes {
			if h.ArchetypeID == legend {
				main = h.InstanceID
			} else {
				other = h.InstanceID
			}
		}
		_, err := evolve(p, "k-"+randomHex(8), main, other)
		expectError(t, err, "WRONG_ARCHETYPE")
	})
	t.Run("itself", func(t *testing.T) {
		p, main, _ := evolutionSetup(t)
		_, err := evolve(p, "k-"+randomHex(8), main, main)
		expectError(t, err, "SAME_INSTANCE")
	})
	t.Run("no catalyst", func(t *testing.T) {
		p := blank(t, 0)
		grant(t, p, map[string]interface{}{"archetypeId": legend, "level": 200, "copies": 2})
		s := mustState(t, p)
		_, err := evolve(p, "k-"+randomHex(8), s.Heroes[0].InstanceID, s.Heroes[1].InstanceID)
		expectError(t, err, "MISSING_CATALYST")
	})
	t.Run("another transition's catalyst", func(t *testing.T) {
		p := blank(t, 0)
		grant(t, p, map[string]interface{}{"archetypeId": legend, "level": 200, "copies": 2, "items": map[string]int64{"ascension_stone": 1, "sovereign_scroll": 1}})
		s := mustState(t, p)
		_, err := evolve(p, "k-"+randomHex(8), s.Heroes[0].InstanceID, s.Heroes[1].InstanceID)
		expectError(t, err, "MISSING_CATALYST")
	})
	t.Run("final form", func(t *testing.T) {
		p := blank(t, 0)
		grant(t, p, map[string]interface{}{"archetypeId": legend, "evolution": 3, "level": 200, "items": map[string]int64{"sovereign_scroll": 1}})
		s := grant(t, p, map[string]interface{}{"archetypeId": legend})
		main, dup := s.Heroes[0].InstanceID, s.Heroes[1].InstanceID
		_, err := evolve(p, "k-"+randomHex(8), main, dup)
		expectError(t, err, "AT_MAX_EVOLUTION")
	})
	t.Run("non Legendary", func(t *testing.T) {
		p := blank(t, 0)
		s := grant(t, p, map[string]interface{}{"archetypeId": "river_scout", "level": 200, "copies": 2, "items": map[string]int64{"awakening_scroll": 1}})
		_, err := evolve(p, "k-"+randomHex(8), s.Heroes[0].InstanceID, s.Heroes[1].InstanceID)
		expectError(t, err, "NOT_EVOLVABLE")
	})
	t.Run("locked duplicate", func(t *testing.T) {
		p, main, duplicate := evolutionSetup(t)
		if _, err := p.rpc("sd_set_lock", map[string]interface{}{"instanceId": duplicate, "locked": true}); err != nil {
			t.Fatal(err)
		}
		_, err := evolve(p, "k-"+randomHex(8), main, duplicate)
		expectError(t, err, "DUPLICATE_LOCKED")
	})
	t.Run("duplicate on a base", func(t *testing.T) {
		p, main, duplicate := evolutionSetup(t)
		if _, err := p.rpc("sd_assign_hero", map[string]interface{}{"slot": 1, "instanceId": duplicate}); err != nil {
			t.Fatal(err)
		}
		_, err := evolve(p, "k-"+randomHex(8), main, duplicate)
		expectError(t, err, "DUPLICATE_ASSIGNED")
		if s := mustState(t, p); len(s.Heroes) != 2 || s.Items["awakening_scroll"] != 1 {
			t.Fatalf("refusal changed state: %+v", s)
		}
	})
}

func TestEvolve_LockedOrAssignedMainMayStillEvolve(t *testing.T) {
	p, main, duplicate := evolutionSetup(t)
	p.rpc("sd_set_lock", map[string]interface{}{"instanceId": main, "locked": true})
	p.rpc("sd_assign_hero", map[string]interface{}{"slot": 1, "instanceId": main})
	if _, err := evolve(p, "k-"+randomHex(8), main, duplicate); err != nil {
		t.Fatal(err)
	}
}

func TestEvolve_CannotTouchAnotherPlayersHeroes(t *testing.T) {
	victim, victimMain, victimDup := evolutionSetup(t)
	attacker, attackerMain, _ := evolutionSetup(t)

	_, err := evolve(attacker, "k-"+randomHex(8), victimMain, victimDup)
	expectError(t, err, "HERO_NOT_FOUND")
	_, err = evolve(attacker, "k-"+randomHex(8), attackerMain, victimDup)
	expectError(t, err, "DUPLICATE_NOT_FOUND")

	if _, err := attacker.rpc("sd_set_lock", map[string]interface{}{"instanceId": victimDup, "locked": true}); err == nil {
		t.Error("locked another player's hero")
	}
	if _, err := attacker.rpc("sd_assign_hero", map[string]interface{}{"slot": 1, "instanceId": victimMain}); err == nil {
		t.Error("assigned another player's hero")
	}
	if s := mustState(t, victim); len(s.Heroes) != 2 || s.Heroes[0].Locked || s.Heroes[1].Locked {
		t.Fatalf("victim changed: %+v", s.Heroes)
	}
}

func TestEvolve_Replay_OnlyHappensOnce(t *testing.T) {
	p, main, duplicate := evolutionSetup(t)
	key := "k-" + randomHex(8)
	for i := 0; i < 4; i++ {
		s, err := evolve(p, key, main, duplicate)
		if err != nil {
			t.Fatal(err)
		}
		if s.Heroes[0].EvolutionIndex != 1 {
			t.Fatalf("attempt %d: %+v", i, s.Heroes[0])
		}
	}
	if s := mustState(t, p); s.Heroes[0].EvolutionIndex != 1 {
		t.Fatalf("evolved more than once: %+v", s.Heroes[0])
	}
}

func TestEvolve_ConcurrentSameKey_OnlyHappensOnce(t *testing.T) {
	p, main, duplicate := evolutionSetup(t)
	key := "k-" + randomHex(8)
	var wg sync.WaitGroup
	for i := 0; i < 30; i++ {
		wg.Add(1)
		go func() {
			defer wg.Done()
			if _, err := evolve(p, key, main, duplicate); err != nil {
				t.Errorf("same-key retry must succeed: %v", err)
			}
		}()
	}
	wg.Wait()
	s := mustState(t, p)
	if len(s.Heroes) != 1 || s.Heroes[0].EvolutionIndex != 1 || s.Items["awakening_scroll"] != 0 {
		t.Fatalf("%+v", s)
	}
}

func TestEvolve_ConcurrentlySacrificingOneDuplicateTwice_OnlyOneWins(t *testing.T) {
	p := blank(t, 0)
	grant(t, p, map[string]interface{}{"archetypeId": legend, "level": 200, "copies": 2, "items": map[string]int64{"awakening_scroll": 10}})
	s := grant(t, p, map[string]interface{}{"archetypeId": legend, "level": 1})
	var mains []string
	var duplicate string
	for _, h := range s.Heroes {
		if h.Level == 200 {
			mains = append(mains, h.InstanceID)
		} else {
			duplicate = h.InstanceID
		}
	}
	var wins int64
	var wg sync.WaitGroup
	for i := 0; i < 20; i++ {
		wg.Add(1)
		go func(i int) {
			defer wg.Done()
			if _, err := evolve(p, "k-"+randomHex(8), mains[i%2], duplicate); err == nil {
				atomic.AddInt64(&wins, 1)
			}
		}(i)
	}
	wg.Wait()
	final := mustState(t, p)
	if wins != 1 || final.Items["awakening_scroll"] != 9 || len(final.Heroes) != 2 {
		t.Fatalf("wins=%d items=%v heroes=%d", wins, final.Items, len(final.Heroes))
	}
}

func TestEvolve_ConcurrentAssignVersusSacrifice_NeverBoth(t *testing.T) {
	for round := 0; round < 15; round++ {
		p, main, duplicate := evolutionSetup(t)
		var wg sync.WaitGroup
		wg.Add(2)
		go func() {
			defer wg.Done()
			p.rpc("sd_assign_hero", map[string]interface{}{"slot": 1, "instanceId": duplicate})
		}()
		go func() {
			defer wg.Done()
			evolve(p, "k-"+randomHex(8), main, duplicate)
		}()
		wg.Wait()
		s := mustState(t, p)
		evolved := false
		for _, h := range s.Heroes {
			if h.InstanceID == main && h.EvolutionIndex == 1 {
				evolved = true
			}
		}
		if evolved == (len(s.Heroes) == 2) {
			t.Fatalf("round %d inconsistent: %+v", round, s)
		}
	}
}

// ---------- Base assignment ----------

func TestAssign_RulesAndSixLegendaries(t *testing.T) {
	p := blank(t, 0)
	grant(t, p, map[string]interface{}{"archetypeId": legend, "copies": 3})
	s := grant(t, p, map[string]interface{}{"archetypeId": "thunder_empress", "copies": 4})
	if len(s.Heroes) != 7 {
		t.Fatal(len(s.Heroes))
	}
	for i := 0; i < 6; i++ {
		if _, err := p.rpc("sd_assign_hero", map[string]interface{}{"slot": i + 1, "instanceId": s.Heroes[i].InstanceID}); err != nil {
			t.Fatalf("six Legendaries must be allowed, slot %d: %v", i+1, err)
		}
	}
	_, err := p.rpc("sd_assign_hero", map[string]interface{}{"slot": 7, "instanceId": s.Heroes[6].InstanceID})
	expectError(t, err, "ALL_BASES_FULL")

	_, err = p.rpc("sd_assign_hero", map[string]interface{}{"slot": 2, "instanceId": s.Heroes[0].InstanceID})
	expectError(t, err, "HERO_ALREADY_ASSIGNED")

	if _, err := p.rpc("sd_assign_hero", map[string]interface{}{"slot": 1, "instanceId": nil}); err != nil {
		t.Fatal(err)
	}
	if _, err := p.rpc("sd_assign_hero", map[string]interface{}{"slot": 7, "instanceId": s.Heroes[6].InstanceID}); err != nil {
		t.Fatal(err)
	}
	_, err = p.rpc("sd_assign_hero", map[string]interface{}{"slot": 0, "instanceId": s.Heroes[0].InstanceID})
	expectError(t, err, "BAD_SLOT")
	_, err = p.rpc("sd_assign_hero", map[string]interface{}{"slot": -1, "instanceId": s.Heroes[0].InstanceID})
	expectError(t, err, "BAD_SLOT")
}

func TestAssign_ConcurrentCannotExceedSix_OrDoubleUseHero(t *testing.T) {
	p := blank(t, 0)
	s := grant(t, p, map[string]interface{}{"archetypeId": legend, "copies": 12})
	var wg sync.WaitGroup
	for i := 0; i < 12; i++ {
		wg.Add(1)
		go func(i int) {
			defer wg.Done()
			p.rpc("sd_assign_hero", map[string]interface{}{"slot": i + 1, "instanceId": s.Heroes[i].InstanceID})
		}(i)
	}
	wg.Wait()
	used := 0
	slots := map[int]bool{}
	for _, h := range mustState(t, p).Heroes {
		if h.BaseSlot != nil {
			used++
			if slots[*h.BaseSlot] {
				t.Fatal("two heroes on one base")
			}
			slots[*h.BaseSlot] = true
		}
	}
	if used != 6 {
		t.Fatalf("assigned %d heroes", used)
	}
}

// ---------- Recovery ----------

// TestCrashRecovery_LoadWithRetries keeps summoning while run_recovery.ps1 kills the server.
// Every request retries its own key until it lands, as a real client would. Afterwards each
// total must agree: no lost charge, no free hero, no double spend.
func TestCrashRecovery_LoadWithRetries(t *testing.T) {
	if os.Getenv("SD_RECOVERY_TEST") != "1" {
		t.Skip("run through run_recovery.ps1")
	}
	p := blank(t, 0)
	o := mustOdds(t, p)
	// Several workers with no pauses, so requests are mid-transaction when the server dies.
	const workers = 8
	const requests = workers * 300
	grant(t, p, map[string]interface{}{"setCrystals": o.Price * requests})

	var sawFailure int64
	var wg sync.WaitGroup
	for w := 0; w < workers; w++ {
		wg.Add(1)
		go func() {
			defer wg.Done()
			for i := 0; i < requests/workers; i++ {
				key := "k-" + randomHex(8)
				deadline := time.Now().Add(120 * time.Second)
				for {
					_, err := summon(p, o.PoolVersion, 1, key)
					if err == nil {
						break
					}
					atomic.AddInt64(&sawFailure, 1)
					if time.Now().After(deadline) {
						t.Errorf("server did not recover: %v", err)
						return
					}
					time.Sleep(200 * time.Millisecond)
				}
			}
		}()
	}
	wg.Wait()
	if t.Failed() {
		return
	}
	if sawFailure == 0 {
		t.Fatal("the server was never down during this run, so recovery was not exercised")
	}
	t.Logf("requests that hit an outage: %d", sawFailure)

	body, err := p.rpc("sd_dev_audit", "{}")
	if err != nil {
		t.Fatal(err)
	}
	var audit struct {
		Crystals      int64 `json:"crystals"`
		LedgerSum     int64 `json:"ledgerSum"`
		SummonDebits  int64 `json:"summonDebits"`
		Rolls         int64 `json:"rolls"`
		RollsWithHero int64 `json:"rollsWithHero"`
	}
	json.Unmarshal([]byte(body), &audit)
	t.Logf("audit: %+v", audit)
	if audit.Crystals != 0 || audit.Rolls != requests || audit.RollsWithHero != requests || audit.SummonDebits != requests*o.Price {
		t.Fatalf("totals disagree after crash: %+v", audit)
	}
	if audit.LedgerSum != audit.Crystals {
		t.Fatalf("ledger %d != balance %d", audit.LedgerSum, audit.Crystals)
	}
	if s := mustState(t, p); len(s.Heroes) != requests {
		t.Fatalf("heroes=%d", len(s.Heroes))
	}
}

// TestProductionMode_HasNoDevCalls runs with SD_DEV_MODE off, through run_recovery.ps1.
func TestProductionMode_HasNoDevCalls(t *testing.T) {
	if os.Getenv("SD_PRODUCTION_TEST") != "1" {
		t.Skip("run through run_recovery.ps1")
	}
	p := newPlayer(t)
	for _, id := range []string{"sd_dev_grant", "sd_dev_audit"} {
		_, err := p.rpc(id, `{"setCrystals":99999999}`)
		if err == nil || err.(*rpcError).Status != 404 {
			t.Fatalf("%s must not exist in production mode: %v", id, err)
		}
	}
	s := mustState(t, p)
	if s.Crystals != 3000 || len(s.Heroes) != 0 || len(s.Items) != 0 {
		t.Fatalf("production players must start with no free heroes or items: %+v", s)
	}
}
