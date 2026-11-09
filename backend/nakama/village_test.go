package main

import (
	_ "embed"
	"encoding/json"
	"fmt"
	"sort"
	"strconv"
	"testing"
)

//go:embed testdata/rules_vectors.json
var vectorsJSON []byte

type vectorOp struct {
	Op       string   `json:"op"`
	Def      string   `json:"def"`
	X        int      `json:"x"`
	Z        int      `json:"z"`
	X2       int      `json:"x2"`
	Z2       int      `json:"z2"`
	Rotation int      `json:"rotation"`
	Expect   string   `json:"expect"`
	Label    string   `json:"label"`
	Target   string   `json:"target"`
	ID       int      `json:"id"`
	AtX      *int     `json:"atX"`
	AtZ      *int     `json:"atZ"`
	Gold     *int64   `json:"gold"`
	Crystals *int64   `json:"crystals"`
	Mana     *int64   `json:"mana"`
	HeroLevel int     `json:"heroLevel"`
	HeroXp   int64    `json:"heroXp"`
	ToLevel  *int     `json:"toLevel"`
	ToXp     *int64   `json:"toXp"`
	Count    *int     `json:"count"`
	CountDef string   `json:"countDef"`
	Level    *int     `json:"level"`
	Hours    int64    `json:"hours"`
	Troop    string   `json:"troop"`
}

type vectorFile struct {
	Starter []struct {
		Def  string `json:"def"`
		X    int    `json:"x"`
		Z    int    `json:"z"`
	} `json:"starter"`
	Scenarios []struct {
		Name          string     `json:"name"`
		StartGold     int64      `json:"startGold"`
		StartCrystals int64      `json:"startCrystals"`
		StartMana     int64      `json:"startMana"`
		Ops           []vectorOp `json:"ops"`
	} `json:"scenarios"`
}

func loadVectors(t *testing.T) (vectorFile, *BuildingCatalog) {
	t.Helper()
	var vectors vectorFile
	if err := json.Unmarshal(vectorsJSON, &vectors); err != nil {
		t.Fatal(err)
	}
	catalog, err := loadBuildingCatalog(buildingsJSON)
	if err != nil {
		t.Fatal(err)
	}
	return vectors, catalog
}

func TestStarterLayout_MatchesSharedFile_AndIsValid(t *testing.T) {
	vectors, catalog := loadVectors(t)
	expected := []string{}
	for _, item := range vectors.Starter {
		expected = append(expected, fmt.Sprintf("%s@%d,%d", item.Def, item.X, item.Z))
	}
	actual := []string{}
	v := newVillage(catalog, 0, 0, 1)
	for _, item := range starterLayout {
		actual = append(actual, fmt.Sprintf("%s@%d,%d", item.Def, item.X, item.Z))
		if e := v.checkFootprint(catalog.byID[item.Def], item.X, item.Z, 0, 0); e != vOK {
			t.Fatalf("%s: %s", item.Def, e)
		}
		v.add(catalog.byID[item.Def], item.X, item.Z, 0, 0)
	}
	sort.Strings(expected)
	sort.Strings(actual)
	if fmt.Sprint(expected) != fmt.Sprint(actual) {
		t.Fatalf("\nshared %v\nserver %v", expected, actual)
	}
}

func TestSharedRuleScenarios(t *testing.T) {
	vectors, catalog := loadVectors(t)
	for _, scenario := range vectors.Scenarios {
		t.Run(scenario.Name, func(t *testing.T) {
			v := newVillage(catalog, scenario.StartGold, scenario.StartCrystals, 1)
			v.mana = scenario.StartMana
			heroes, heroErr := loadCatalog(heroesJSON)
			if heroErr != nil {
				t.Fatal(heroErr)
			}
			labels := map[string]int{}
			clock := int64(3600000 * 1000)
			troops, troopErr := loadTroopCatalog(troopsJSON)
			if troopErr != nil {
				t.Fatal(troopErr)
			}
			counts := map[string]int{}
			for index, op := range scenario.Ops {
				where := "step " + strconv.Itoa(index+1)
				target := op.ID
				if op.Target != "" {
					target = labels[op.Target]
				} else if op.AtX != nil {
					target = v.occupantAt(*op.AtX, *op.AtZ)
				}

				switch op.Op {
				case "setGold":
					v.gold = *op.Gold
					continue
				case "setLevel":
					v.byID[target].Level = *op.Level
					continue
				case "train":
					want := op.Expect
					if want == "None" {
						want = ""
					}
					if got := train(v, troops, counts, op.Troop, *op.Count); string(got) != want {
						t.Fatalf("%s train: got %q, want %q", where, got, op.Expect)
					}
					if op.Gold != nil && v.gold != *op.Gold {
						t.Fatalf("%s: gold %d, want %d", where, v.gold, *op.Gold)
					}
					if op.Mana != nil && v.mana != *op.Mana {
						t.Fatalf("%s: mana %d, want %d", where, v.mana, *op.Mana)
					}
					continue
				case "setMana":
					v.mana = *op.Mana
					continue
				case "trainHero":
					want := op.Expect
					if want == "None" {
						want = ""
					}
					level, xp, got := heroes.trainLevel(v, op.HeroLevel, op.HeroXp)
					if string(got) != want {
						t.Fatalf("%s trainHero: got %q, want %q", where, got, op.Expect)
					}
					if op.ToLevel != nil && level != *op.ToLevel {
						t.Fatalf("%s: hero level %d, want %d", where, level, *op.ToLevel)
					}
					if op.ToXp != nil && xp != *op.ToXp {
						t.Fatalf("%s: hero xp %d, want %d", where, xp, *op.ToXp)
					}
					if op.Mana != nil && v.mana != *op.Mana {
						t.Fatalf("%s: mana %d, want %d", where, v.mana, *op.Mana)
					}
					continue
				case "advanceHours":
					clock += op.Hours * 3600000
					continue
				case "occupied":
					if (op.Expect == "true") != (v.occupantAt(op.X, op.Z) != 0) {
						t.Fatalf("%s: occupied(%d,%d) should be %s", where, op.X, op.Z, op.Expect)
					}
					continue
				case "levelAt":
					want, _ := strconv.Atoi(op.Expect)
					if got := v.byID[v.occupantAt(op.X, op.Z)].Level; got != want {
						t.Fatalf("%s: level %d, want %d", where, got, want)
					}
					continue
				}

				var result vErr
				switch op.Op {
				case "place":
					var placed *building
					placed, result = v.place(op.Def, op.X, op.Z, op.Rotation, clock)
					if placed != nil && op.Label != "" {
						labels[op.Label] = placed.ID
					}
				case "walls":
					result = v.placeWalls(op.X, op.Z, op.X2, op.Z2, clock)
				case "move":
					result = v.move(target, op.X, op.Z, op.Rotation)
				case "remove":
					result = v.remove(target)
				case "upgrade":
					result = v.upgrade(target)
				case "upgradeGroup":
					result = v.upgradeWallGroup(target)
				case "collect":
					_, result = v.collect(target, clock)
				default:
					t.Fatalf("unknown op %s", op.Op)
				}

				want := op.Expect
				if want == "None" {
					want = ""
				}
				if string(result) != want {
					t.Fatalf("%s %s: got %q, want %q", where, op.Op, result, op.Expect)
				}
				if op.Gold != nil && v.gold != *op.Gold {
					t.Fatalf("%s: gold %d, want %d", where, v.gold, *op.Gold)
				}
				if op.Crystals != nil && v.crystals != *op.Crystals {
					t.Fatalf("%s: crystals %d, want %d", where, v.crystals, *op.Crystals)
				}
				if op.Mana != nil && v.mana != *op.Mana {
					t.Fatalf("%s: mana %d, want %d", where, v.mana, *op.Mana)
				}
				if op.Count != nil {
					def := op.CountDef
					if def == "" {
						def = op.Def
					}
					if got := v.countOf(def); got != *op.Count {
						t.Fatalf("%s: count %d, want %d", where, got, *op.Count)
					}
				}
				if op.Level != nil && target != 0 && v.byID[target] != nil && v.byID[target].Level != *op.Level {
					t.Fatalf("%s: level %d, want %d", where, v.byID[target].Level, *op.Level)
				}
			}
		})
	}
}

func TestForgedCoordinates_CannotCrashOrOverflow(t *testing.T) {
	_, catalog := loadVectors(t)
	v := newVillage(catalog, 1000000, 0, 1)
	const huge = int(^uint(0) >> 1)
	for _, c := range [][2]int{{huge, 0}, {0, huge}, {-huge, 0}, {huge - 1, huge - 1}} {
		if _, e := v.place("hero_base", c[0], c[1], 0, 0); e != vOutOfBounds {
			t.Errorf("place at %v: %q", c, e)
		}
	}
	if e := v.placeWalls(0, 0, huge, 0, 0); e != vOutOfBounds {
		t.Errorf("huge wall run: %q", e)
	}
	if e := v.placeWalls(0, 0, -huge, 0, 0); e != vOutOfBounds {
		t.Errorf("huge negative wall run: %q", e)
	}
	if _, e := v.place("training_grounds", 5, 5, 99, 0); e != vNotRotatable {
		t.Errorf("rotation 99: %q", e)
	}
	if _, e := v.place("training_grounds", 5, 5, -1, 0); e != vNotRotatable {
		t.Errorf("rotation -1: %q", e)
	}
}

func TestPending_ClockSetBackward_EarnsNothing(t *testing.T) {
	_, catalog := loadVectors(t)
	b := &building{Def: catalog.byID["gold_mine"], Level: 1, LastCollectedMs: 5000000}
	if got := pendingCollection(b, 1000); got != 0 {
		t.Fatalf("got %d", got)
	}
}

func TestPending_VeryLongTime_DoesNotOverflow(t *testing.T) {
	_, catalog := loadVectors(t)
	b := &building{Def: catalog.byID["gold_mine"], Level: 15, LastCollectedMs: 0}
	if got := pendingCollection(b, int64(1)<<62); got != b.Def.at(15).StoredCap {
		t.Fatalf("got %d", got)
	}
}
