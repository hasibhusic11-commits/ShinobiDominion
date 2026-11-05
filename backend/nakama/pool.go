package main

import (
	"crypto/rand"
	_ "embed"
	"encoding/json"
	"errors"
	"fmt"
	"io"
	"math/big"
	"time"
)

//go:embed config/summon_pool.json
var summonPoolJSON []byte

//go:embed config/heroes.json
var heroesJSON []byte

type PoolItem struct {
	ArchetypeID string `json:"archetypeId"`
	Weight      int64  `json:"weight"`
}

// SummonPool is a versioned drop table. It deliberately has no player input: Roll takes only
// a random source, so pity, streak or duplicate logic has nowhere to live.
type SummonPool struct {
	Version     int        `json:"version"`
	Status      string     `json:"status"`
	BannerName  string     `json:"bannerName"`
	PricePerRol int64      `json:"priceCrystalsPerRoll"`
	StartsAt    time.Time  `json:"startsAt"`
	EndsAt      time.Time  `json:"endsAt"`
	Items       []PoolItem `json:"items"`
	total       int64
}

type Archetype struct {
	ID              string `json:"id"`
	DisplayName     string `json:"displayName"`
	Rarity          string `json:"rarity"`
	ContentComplete bool   `json:"contentComplete"`
}

type HeroCatalog struct {
	SchemaVersion int         `json:"schemaVersion"`
	Catalysts     []string    `json:"catalystByTransition"`
	Heroes        []Archetype `json:"heroes"`
	byID          map[string]Archetype
}

func loadCatalog(data []byte) (*HeroCatalog, error) {
	var catalog HeroCatalog
	if err := json.Unmarshal(data, &catalog); err != nil {
		return nil, err
	}
	if len(catalog.Catalysts) != 3 {
		return nil, errors.New("exactly three catalysts are required")
	}
	seen := map[string]bool{}
	for _, c := range catalog.Catalysts {
		if seen[c] {
			return nil, errors.New("each transition needs its own catalyst")
		}
		seen[c] = true
	}
	catalog.byID = map[string]Archetype{}
	for _, h := range catalog.Heroes {
		catalog.byID[h.ID] = h
	}
	return &catalog, nil
}

func loadPool(data []byte, catalog *HeroCatalog) (*SummonPool, error) {
	var pool SummonPool
	if err := json.Unmarshal(data, &pool); err != nil {
		return nil, err
	}
	if pool.Version <= 0 {
		return nil, errors.New("pool version must be positive")
	}
	if pool.PricePerRol <= 0 {
		return nil, errors.New("pool price must be positive")
	}
	if len(pool.Items) == 0 {
		return nil, errors.New("pool is empty")
	}
	seen := map[string]bool{}
	for _, item := range pool.Items {
		if item.Weight <= 0 {
			return nil, fmt.Errorf("%s has a non-positive weight", item.ArchetypeID)
		}
		if seen[item.ArchetypeID] {
			return nil, fmt.Errorf("%s appears twice", item.ArchetypeID)
		}
		seen[item.ArchetypeID] = true
		hero, ok := catalog.byID[item.ArchetypeID]
		if !ok {
			return nil, fmt.Errorf("%s is not a known hero", item.ArchetypeID)
		}
		if !hero.ContentComplete {
			return nil, fmt.Errorf("%s is incomplete content and cannot be summoned", item.ArchetypeID)
		}
		pool.total += item.Weight
	}
	return &pool, nil
}

// Roll makes one independent draw. crypto/rand's Int is uniform over [0,total), and integer
// cumulative weights avoid floating-point and modulo bias.
func (p *SummonPool) Roll(source io.Reader) (string, error) {
	drawn, err := rand.Int(source, big.NewInt(p.total))
	if err != nil {
		return "", err
	}
	value := drawn.Int64()
	var cumulative int64
	for _, item := range p.Items {
		cumulative += item.Weight
		if value < cumulative {
			return item.ArchetypeID, nil
		}
	}
	return "", errors.New("roll fell outside the pool")
}

func (p *SummonPool) Active(now time.Time) bool {
	return !now.Before(p.StartsAt) && now.Before(p.EndsAt)
}

// percentString gives weight/total as an exact-rounded percent, computed in integers.
func percentString(weight, total int64) string {
	scaled := new(big.Rat).SetFrac64(weight*100, total)
	return scaled.FloatString(3) + "%"
}
