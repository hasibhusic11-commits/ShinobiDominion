package main

import (
	"bytes"
	"crypto/rand"
	"errors"
	"math"
	"strings"
	"testing"
	"time"
)

func shippedPool(t *testing.T) (*SummonPool, *HeroCatalog) {
	t.Helper()
	catalog, err := loadCatalog(heroesJSON)
	if err != nil {
		t.Fatal(err)
	}
	pool, err := loadPool(summonPoolJSON, catalog)
	if err != nil {
		t.Fatal(err)
	}
	return pool, catalog
}

func TestShippedPool_IsMarkedNotApproved_AndWeightsSumExactly(t *testing.T) {
	pool, _ := shippedPool(t)
	if !strings.Contains(pool.Status, "NOT APPROVED") {
		t.Fatalf("status must flag test rates, got %q", pool.Status)
	}
	var sum int64
	for _, item := range pool.Items {
		sum += item.Weight
	}
	if sum != pool.total || sum != 10000 {
		t.Fatalf("sum=%d total=%d", sum, pool.total)
	}
}

func TestRoll_MatchesWeights(t *testing.T) {
	pool, _ := shippedPool(t)
	const rolls = 400000
	counts := map[string]int{}
	for i := 0; i < rolls; i++ {
		id, err := pool.Roll(rand.Reader)
		if err != nil {
			t.Fatal(err)
		}
		counts[id]++
	}
	for _, item := range pool.Items {
		p := float64(item.Weight) / float64(pool.total)
		expected := p * rolls
		deviation := math.Sqrt(rolls * p * (1 - p))
		if math.Abs(float64(counts[item.ArchetypeID])-expected) > 6*deviation {
			t.Fatalf("%s: got %d, expected about %.0f", item.ArchetypeID, counts[item.ArchetypeID], expected)
		}
	}
}

func TestRoll_BoundaryValuesLandOnTheRightItem(t *testing.T) {
	catalog := &HeroCatalog{byID: map[string]Archetype{"a": {ID: "a", ContentComplete: true}, "b": {ID: "b", ContentComplete: true}}}
	pool, err := loadPool([]byte(`{"version":1,"priceCrystalsPerRoll":1,"items":[{"archetypeId":"a","weight":1},{"archetypeId":"b","weight":255}]}`), catalog)
	if err != nil {
		t.Fatal(err)
	}
	first, err := pool.Roll(bytes.NewReader([]byte{0}))
	if err != nil || first != "a" {
		t.Fatalf("value 0 must be the first item, got %q %v", first, err)
	}
	second, err := pool.Roll(bytes.NewReader([]byte{1}))
	if err != nil || second != "b" {
		t.Fatalf("value 1 must be the second item, got %q %v", second, err)
	}
}

func TestPool_RejectsBadTables(t *testing.T) {
	_, catalog := shippedPool(t)
	cases := map[string]string{
		"zero weight":     `{"version":1,"priceCrystalsPerRoll":1,"items":[{"archetypeId":"river_scout","weight":0}]}`,
		"negative weight": `{"version":1,"priceCrystalsPerRoll":1,"items":[{"archetypeId":"river_scout","weight":-5}]}`,
		"unknown hero":    `{"version":1,"priceCrystalsPerRoll":1,"items":[{"archetypeId":"nope","weight":1}]}`,
		"incomplete hero": `{"version":1,"priceCrystalsPerRoll":1,"items":[{"archetypeId":"void_sovereign","weight":1}]}`,
		"duplicate":       `{"version":1,"priceCrystalsPerRoll":1,"items":[{"archetypeId":"river_scout","weight":1},{"archetypeId":"river_scout","weight":1}]}`,
		"no version":      `{"priceCrystalsPerRoll":1,"items":[{"archetypeId":"river_scout","weight":1}]}`,
		"free":            `{"version":1,"items":[{"archetypeId":"river_scout","weight":1}]}`,
		"empty":           `{"version":1,"priceCrystalsPerRoll":1,"items":[]}`,
	}
	for name, body := range cases {
		if _, err := loadPool([]byte(body), catalog); err == nil {
			t.Errorf("%s must be rejected", name)
		}
	}
}

func TestPercentString(t *testing.T) {
	if got := percentString(125, 10000); got != "1.250%" {
		t.Fatal(got)
	}
	if got := percentString(1, 3); got != "33.333%" {
		t.Fatal(got)
	}
}

func TestPool_Active(t *testing.T) {
	pool, _ := shippedPool(t)
	if pool.Active(pool.StartsAt.Add(-time.Second)) || !pool.Active(pool.StartsAt) || pool.Active(pool.EndsAt) {
		t.Fatal("window must be start-inclusive and end-exclusive")
	}
}

func TestDecodeStrict_RejectsExtraFieldsAndOversize(t *testing.T) {
	var request summonRequest
	if err := decodeStrict(`{"poolVersion":1,"count":1,"key":"abcdefgh"}`, &request); err != nil {
		t.Fatal(err)
	}
	for _, payload := range []string{
		`{"poolVersion":1,"count":1,"key":"abcdefgh","results":["stone_titan"]}`,
		`{"poolVersion":1,"count":1,"key":"abcdefgh","seed":42}`,
		`not json`,
		`{"poolVersion":1,"count":1,"key":"abcdefgh"}{"count":10}`,
		`{"poolVersion":1,"count":1,"key":"abcdefgh"} null`,
		`{"poolVersion":1,"count":1,"key":"abcdefgh"} true`,
		`{"poolVersion":1,"count":1,"key":"abcdefgh"} 12`,
		`{"poolVersion":1,"count":1,"key":"abcdefgh"} garbage`,
		`{"key":"` + strings.Repeat("a", maxPayloadBytes) + `"}`,
	} {
		if err := decodeStrict(payload, &request); !errors.Is(err, errBadRequest) {
			t.Errorf("payload must be rejected: %.60s", payload)
		}
	}
}

func TestDevMode_OnlyForExactTrue(t *testing.T) {
	for _, value := range []string{"", "false", "TRUE", "1", "yes", " true"} {
		if devModeEnabled(map[string]string{"SD_DEV_MODE": value}) {
			t.Errorf("%q must not enable dev mode", value)
		}
	}
	if devModeEnabled(nil) {
		t.Error("missing env must not enable dev mode")
	}
	if !devModeEnabled(map[string]string{"SD_DEV_MODE": "true"}) {
		t.Error("true must enable dev mode")
	}
}

func TestRateLimiter(t *testing.T) {
	limiter := newRateLimiter(1, 2)
	now := time.Unix(1000, 0)
	if !limiter.Allow("a", now) || !limiter.Allow("a", now) {
		t.Fatal("burst must be allowed")
	}
	if limiter.Allow("a", now) {
		t.Fatal("past burst must be refused")
	}
	if !limiter.Allow("b", now) {
		t.Fatal("players are independent")
	}
	if !limiter.Allow("a", now.Add(time.Second)) {
		t.Fatal("tokens must refill")
	}
}
