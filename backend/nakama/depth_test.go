package main

import (
	"crypto/rand"
	"testing"
)

// The same numbers are checked in C# by HeroDepthTests, so the client's displayed numbers match the server's.
func TestDepth_RankRaisesThePowerOfTheSpecial(t *testing.T) {
	s := modesServer(t)
	if got := s.catalog.rankPower("Legendary", 1); got != 100 {
		t.Fatalf("rank 1 Legendary power %d", got)
	}
	if got := s.catalog.rankPower("Legendary", 10); got != 172 {
		t.Fatalf("rank 10 Legendary power %d", got)
	}
	if got := s.catalog.rankPower("Common", 4); got != 74 {
		t.Fatalf("rank 4 Common power %d", got)
	}
	if got := s.catalog.rankPower("Elite", 0); got != 75 {
		t.Fatalf("a rank below 1 counts as 1, got %d", got)
	}
	manuals, heroLevel := s.catalog.rankCost(4)
	if manuals != 6 || heroLevel != 46 {
		t.Fatalf("rank 4 costs %d manuals and needs hero level %d", manuals, heroLevel)
	}
}

func TestDepth_EachTalentChangesOnlyItsOwnThing(t *testing.T) {
	s := modesServer(t)
	cases := []struct {
		talent              string
		level               int
		hp, attack, defense int64
		edge                battleEdge
	}{
		{"", 0, 1000, 200, 50, battleEdge{}},
		{"iron_body", 5, 1200, 200, 50, battleEdge{}},
		{"keen_edge", 3, 1000, 224, 50, battleEdge{}},
		{"stone_skin", 2, 1000, 200, 56, battleEdge{}},
		{"ready_stance", 3, 1000, 200, 50, battleEdge{StartCharge: 3}},
		{"ready_stance", 5, 1000, 200, 50, battleEdge{StartCharge: 5}},
		{"siege_breaker", 5, 1000, 200, 50, battleEdge{SiegePercent: 40}},
		{"swift_feet", 5, 1000, 200, 50, battleEdge{SpeedPercent: 30}},
		// An id the catalog does not know, or a level of 0, changes nothing.
		{"no_such_talent", 5, 1000, 200, 50, battleEdge{}},
		{"keen_edge", 0, 1000, 200, 50, battleEdge{}},
	}
	for _, c := range cases {
		hp, attack, defense, power, edge := s.catalog.withDepth("Legendary", 1000, 200, 50, heroDepth{Rank: 1, Talent: c.talent, Level: c.level})
		if hp != c.hp || attack != c.attack || defense != c.defense || edge != c.edge || power != 100 {
			t.Errorf("%s %d: got %d %d %d power %d %+v", c.talent, c.level, hp, attack, defense, power, edge)
		}
	}
}

func TestDepth_ARollIsAnEvenDrawOfTalentAndLevel(t *testing.T) {
	s := modesServer(t)
	s.random = rand.Reader
	talents := map[string]int{}
	levels := map[int]int{}
	const rolls = 6000
	for i := 0; i < rolls; i++ {
		talent, level, err := s.rollTalent()
		if err != nil {
			t.Fatal(err)
		}
		if _, ok := s.catalog.Talents.find(talent); !ok {
			t.Fatalf("rolled an unknown talent %q", talent)
		}
		if level < 1 || level > s.catalog.Talents.MaxLevel {
			t.Fatalf("rolled level %d", level)
		}
		talents[talent]++
		levels[level]++
	}
	// Six talents and five levels: each should come up about a sixth and a fifth of the time. The bounds are wide
	// enough that an honest even draw fails them less than once in a million runs.
	if len(talents) != len(s.catalog.Talents.List) || len(levels) != s.catalog.Talents.MaxLevel {
		t.Fatalf("not every talent or level came up: %v %v", talents, levels)
	}
	for talent, count := range talents {
		if count < rolls/6-200 || count > rolls/6+200 {
			t.Errorf("%s came up %d times of %d", talent, count, rolls)
		}
	}
	for level, count := range levels {
		if count < rolls/5-220 || count > rolls/5+220 {
			t.Errorf("level %d came up %d times of %d", level, count, rolls)
		}
	}
}

func TestDepth_WinsPayManualsAndTokens(t *testing.T) {
	s := modesServer(t)
	manual, token := s.catalog.Technique.Item, s.catalog.Talents.Item
	cases := []struct {
		rival           bool
		stars           int
		paid            bool
		manuals, tokens int64
	}{
		{true, 0, false, 0, 0},
		{true, 1, false, 1, 0},
		{true, 2, false, 2, 0},
		{true, 3, false, 3, 1},
		{false, 3, true, 1, 0},
		{false, 3, false, 0, 0},
		{false, 0, true, 0, 0},
	}
	for _, c := range cases {
		got := s.catalog.depthRewards(c.rival, c.stars, c.paid)
		if got[manual] != c.manuals || got[token] != c.tokens {
			t.Errorf("rival=%v stars=%d paid=%v: got %v", c.rival, c.stars, c.paid, got)
		}
	}
}

func TestDepth_TheCatalogRefusesBadSettings(t *testing.T) {
	good := talentConfig{MaxLevel: 5, Item: "talent_token", List: []talentDefinition{{ID: "a", Kind: talentHp, PerLevel: 1}}}
	if !good.valid() {
		t.Fatal("a good talent config was refused")
	}
	bad := []talentConfig{
		{MaxLevel: 0, Item: "x", List: good.List},
		{MaxLevel: 5, Item: "", List: good.List},
		{MaxLevel: 5, Item: "x"},
		{MaxLevel: 5, Item: "x", List: []talentDefinition{{ID: "a", Kind: "luck", PerLevel: 1}}},
		{MaxLevel: 5, Item: "x", List: []talentDefinition{{ID: "a", Kind: talentHp, PerLevel: 0}}},
		{MaxLevel: 5, Item: "x", List: []talentDefinition{{ID: "a", Kind: talentHp, PerLevel: 1}, {ID: "a", Kind: talentSpeed, PerLevel: 1}}},
		{MaxLevel: 5, Item: "x", RaidStars: 4, List: good.List},
	}
	for i, c := range bad {
		if c.valid() {
			t.Errorf("bad talent config %d was accepted", i)
		}
	}
	if (techniqueConfig{MaxRank: 10, Item: ""}).valid() || (techniqueConfig{MaxRank: 0, Item: "x"}).valid() {
		t.Error("a bad technique config was accepted")
	}
}
