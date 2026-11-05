package main

import (
	_ "embed"
	"encoding/json"
	"errors"
	"fmt"
)

//go:embed config/buildings.json
var buildingsJSON []byte

const (
	gridSize       = 50
	hqDefinitionID = "dojo_hq"
	wallID         = "wall"
)

// vErr is a rule refusal. The values match the C# VillageError names, because the shared
// scenario file and the client both rely on them. "" means allowed.
type vErr string

const (
	vOK             vErr = ""
	vUnknown        vErr = "UnknownBuilding"
	vOutOfBounds    vErr = "OutOfBounds"
	vOverlap        vErr = "Overlap"
	vLimit          vErr = "LimitReached"
	vFunds          vErr = "InsufficientFunds"
	vNotFound       vErr = "NotFound"
	vNotRotatable   vErr = "NotRotatable"
	vMaxLevel       vErr = "MaxLevel"
	vNotRemovable   vErr = "NotRemovable"
	vNotACollector  vErr = "NotACollector"
	vNothingCollect vErr = "NothingToCollect"
	vStorageFull    vErr = "StorageFull"
	vNotAWall       vErr = "NotAWall"
)

// BuildingDef is read from the same buildings.json the game ships.
type BuildingDef struct {
	ID                 string `json:"id"`
	Function           string `json:"function"`
	Width              int    `json:"width"`
	Depth              int    `json:"depth"`
	Rotatable          bool   `json:"rotatable"`
	Removable          *bool  `json:"removable"`
	MaxLevel           int    `json:"maxLevel"`
	CostGold           int64  `json:"costGold"`
	CostCrystals       int64  `json:"costCrystals"`
	UpgradeCostPer     int64  `json:"upgradeCostGoldPerLevel"`
	MaxCountByHQ       []int  `json:"maxCountByHqLevel"`
	Produces           string `json:"produces"`
	ProductionPerHour  int64  `json:"productionPerHour"`
	StoredProductionCap int64 `json:"storedProductionCap"`
	GoldCapacityBonus  int64  `json:"goldCapacityBonus"`
}

func (d *BuildingDef) isWall() bool    { return d.Function == "Wall" }
func (d *BuildingDef) removable() bool { return d.Removable == nil || *d.Removable }

func (d *BuildingDef) maxCountAt(hqLevel int) int {
	if len(d.MaxCountByHQ) == 0 {
		return 0
	}
	index := hqLevel - 1
	if index < 0 {
		index = 0
	}
	if index >= len(d.MaxCountByHQ) {
		index = len(d.MaxCountByHQ) - 1
	}
	return d.MaxCountByHQ[index]
}

type BuildingCatalog struct {
	SchemaVersion    int           `json:"schemaVersion"`
	StartingGold     int64         `json:"startingGold"`
	BaseGoldCapacity int64         `json:"baseGoldCapacity"`
	Buildings        []*BuildingDef `json:"buildings"`
	byID             map[string]*BuildingDef
}

func loadBuildingCatalog(data []byte) (*BuildingCatalog, error) {
	var catalog BuildingCatalog
	if err := json.Unmarshal(data, &catalog); err != nil {
		return nil, err
	}
	if catalog.SchemaVersion != 1 {
		return nil, errors.New("unsupported building catalog schema")
	}
	catalog.byID = map[string]*BuildingDef{}
	for _, def := range catalog.Buildings {
		// The same defaults the C# fields declare.
		if def.Width == 0 {
			def.Width = 1
		}
		if def.Depth == 0 {
			def.Depth = 1
		}
		if def.MaxLevel == 0 {
			def.MaxLevel = 1
		}
		if def.Width < 0 || def.Depth < 0 || def.CostGold < 0 || def.CostCrystals < 0 || def.UpgradeCostPer < 0 {
			return nil, fmt.Errorf("%s has invalid values", def.ID)
		}
		if _, dup := catalog.byID[def.ID]; dup {
			return nil, fmt.Errorf("duplicate building %s", def.ID)
		}
		catalog.byID[def.ID] = def
	}
	if catalog.byID[wallID] == nil || catalog.byID[hqDefinitionID] == nil {
		return nil, errors.New("catalog needs a wall and a headquarters")
	}
	return &catalog, nil
}

// starterLayout is what every new player begins with, free of charge.
var starterLayout = []struct {
	Def  string
	X, Z int
}{
	{"dojo_hq", 22, 22},
	{"builder_hall", 16, 23},
	{"hero_altar", 29, 22},
	{"summoning_gate", 23, 30},
	{"gold_mine", 18, 16},
	{"hero_base", 19, 29},
	{"hero_base", 29, 29},
}

type building struct {
	ID              int
	Def             *BuildingDef
	X, Z            int
	Rotation        int
	Level           int
	LastCollectedMs int64
}

func (b *building) size() (int, int) {
	if b.Rotation == 1 || b.Rotation == 3 {
		return b.Def.Depth, b.Def.Width
	}
	return b.Def.Width, b.Def.Depth
}

// village holds the rules with no database in them, so they can be tested directly.
// Every operation checks first and changes only on success.
type village struct {
	catalog  *BuildingCatalog
	gold     int64
	crystals int64
	nextID   int
	byID     map[int]*building
	grid     [gridSize * gridSize]int

	inserted []*building
	updated  map[int]*building
	deleted  []int
}

func newVillage(catalog *BuildingCatalog, gold, crystals int64, nextID int) *village {
	return &village{catalog: catalog, gold: gold, crystals: crystals, nextID: nextID, byID: map[int]*building{}, updated: map[int]*building{}}
}

func inBounds(x, z int) bool { return x >= 0 && x < gridSize && z >= 0 && z < gridSize }

func (v *village) occupantAt(x, z int) int {
	if !inBounds(x, z) {
		return 0
	}
	return v.grid[z*gridSize+x]
}

func (v *village) fill(b *building, id int) {
	width, depth := b.size()
	for z := b.Z; z < b.Z+depth; z++ {
		for x := b.X; x < b.X+width; x++ {
			v.grid[z*gridSize+x] = id
		}
	}
}

// addExisting loads a stored building. Stored data is trusted only after this check.
func (v *village) addExisting(b *building) error {
	if e := v.checkFootprint(b.Def, b.X, b.Z, b.Rotation, 0); e != vOK {
		return fmt.Errorf("stored building %d is invalid: %s", b.ID, e)
	}
	v.byID[b.ID] = b
	v.fill(b, b.ID)
	return nil
}

func (v *village) hqLevel() int {
	for _, b := range v.byID {
		if b.Def.ID == hqDefinitionID {
			return b.Level
		}
	}
	return 1
}

func (v *village) countOf(id string) int {
	count := 0
	for _, b := range v.byID {
		if b.Def.ID == id {
			count++
		}
	}
	return count
}

func (v *village) goldCapacity() int64 {
	capacity := v.catalog.BaseGoldCapacity
	for _, b := range v.byID {
		capacity += b.Def.GoldCapacityBonus * int64(b.Level)
	}
	return capacity
}

func (v *village) checkFootprint(def *BuildingDef, x, z, rotation, ignoreID int) vErr {
	width, depth := def.Width, def.Depth
	if rotation == 1 || rotation == 3 {
		width, depth = depth, width
	}
	// Bounds first, so absurd coordinates never reach the grid or an overflowing sum.
	if !inBounds(x, z) || !inBounds(x+width-1, z+depth-1) {
		return vOutOfBounds
	}
	for cz := z; cz < z+depth; cz++ {
		for cx := x; cx < x+width; cx++ {
			if occupant := v.occupantAt(cx, cz); occupant != 0 && occupant != ignoreID {
				return vOverlap
			}
		}
	}
	return vOK
}

func (v *village) add(def *BuildingDef, x, z, rotation int, nowMs int64) *building {
	b := &building{ID: v.nextID, Def: def, X: x, Z: z, Rotation: rotation, Level: 1, LastCollectedMs: nowMs}
	v.nextID++
	v.byID[b.ID] = b
	v.fill(b, b.ID)
	v.inserted = append(v.inserted, b)
	return b
}

func validRotation(rotation int) bool { return rotation >= 0 && rotation <= 3 }

func (v *village) place(definitionID string, x, z, rotation int, nowMs int64) (*building, vErr) {
	def, ok := v.catalog.byID[definitionID]
	if !ok {
		return nil, vUnknown
	}
	if !validRotation(rotation) {
		return nil, vNotRotatable
	}
	if rotation != 0 && !def.Rotatable {
		return nil, vNotRotatable
	}
	if v.countOf(def.ID) >= def.maxCountAt(v.hqLevel()) {
		return nil, vLimit
	}
	if v.gold < def.CostGold || v.crystals < def.CostCrystals {
		return nil, vFunds
	}
	if e := v.checkFootprint(def, x, z, rotation, 0); e != vOK {
		return nil, e
	}
	v.gold -= def.CostGold
	v.crystals -= def.CostCrystals
	return v.add(def, x, z, rotation, nowMs), vOK
}

// wallRunCells locks a diagonal drag to the longer axis, with ties going to X, like the client.
func wallRunCells(x1, z1, x2, z2 int) [][2]int {
	dx, dz := x2-x1, z2-z1
	cells := [][2]int{}
	if abs(dx) >= abs(dz) {
		step := 1
		if dx < 0 {
			step = -1
		}
		for i := 0; i <= abs(dx); i++ {
			cells = append(cells, [2]int{x1 + i*step, z1})
		}
	} else {
		step := 1
		if dz < 0 {
			step = -1
		}
		for i := 0; i <= abs(dz); i++ {
			cells = append(cells, [2]int{x1, z1 + i*step})
		}
	}
	return cells
}

func abs(value int) int {
	if value < 0 {
		return -value
	}
	return value
}

// placeWalls is all-or-nothing: one bad cell, or too little gold, places nothing.
func (v *village) placeWalls(x1, z1, x2, z2 int, nowMs int64) vErr {
	// A run longer than the map cannot be valid, so refuse before allocating a forged huge run.
	if abs(x2-x1) > gridSize || abs(z2-z1) > gridSize {
		return vOutOfBounds
	}
	wall := v.catalog.byID[wallID]
	cells := wallRunCells(x1, z1, x2, z2)
	for _, cell := range cells {
		if e := v.checkFootprint(wall, cell[0], cell[1], 0, 0); e != vOK {
			return e
		}
	}
	if v.countOf(wallID)+len(cells) > wall.maxCountAt(v.hqLevel()) {
		return vLimit
	}
	gold := wall.CostGold * int64(len(cells))
	crystals := wall.CostCrystals * int64(len(cells))
	if v.gold < gold || v.crystals < crystals {
		return vFunds
	}
	v.gold -= gold
	v.crystals -= crystals
	for _, cell := range cells {
		v.add(wall, cell[0], cell[1], 0, nowMs)
	}
	return vOK
}

func (v *village) move(id, x, z, rotation int) vErr {
	b, ok := v.byID[id]
	if !ok {
		return vNotFound
	}
	if !validRotation(rotation) {
		return vNotRotatable
	}
	if rotation != b.Rotation && !b.Def.Rotatable {
		return vNotRotatable
	}
	if e := v.checkFootprint(b.Def, x, z, rotation, id); e != vOK {
		return e
	}
	v.fill(b, 0)
	b.X, b.Z, b.Rotation = x, z, rotation
	v.fill(b, id)
	v.updated[id] = b
	return vOK
}

func (v *village) remove(id int) vErr {
	b, ok := v.byID[id]
	if !ok {
		return vNotFound
	}
	if !b.Def.removable() {
		return vNotRemovable
	}
	v.fill(b, 0)
	delete(v.byID, id)
	delete(v.updated, id)
	v.deleted = append(v.deleted, id)
	return vOK
}

func (v *village) upgrade(id int) vErr {
	b, ok := v.byID[id]
	if !ok {
		return vNotFound
	}
	if b.Level >= b.Def.MaxLevel {
		return vMaxLevel
	}
	cost := b.Def.UpgradeCostPer * int64(b.Level)
	if v.gold < cost {
		return vFunds
	}
	v.gold -= cost
	b.Level++
	v.updated[id] = b
	return vOK
}

func (v *village) upgradeWallGroup(id int) vErr {
	first, ok := v.byID[id]
	if !ok {
		return vNotFound
	}
	if !first.Def.isWall() {
		return vNotAWall
	}
	visited := map[int]bool{id: true}
	frontier := []*building{first}
	group := []*building{}
	for len(frontier) > 0 {
		current := frontier[0]
		frontier = frontier[1:]
		group = append(group, current)
		for _, offset := range [][2]int{{0, 1}, {1, 0}, {0, -1}, {-1, 0}} {
			neighbourID := v.occupantAt(current.X+offset[0], current.Z+offset[1])
			neighbour := v.byID[neighbourID]
			if neighbour != nil && neighbour.Def.isWall() && !visited[neighbourID] {
				visited[neighbourID] = true
				frontier = append(frontier, neighbour)
			}
		}
	}
	var total int64
	upgradable := []*building{}
	for _, wall := range group {
		if wall.Level < wall.Def.MaxLevel {
			upgradable = append(upgradable, wall)
			total += wall.Def.UpgradeCostPer * int64(wall.Level)
		}
	}
	if len(upgradable) == 0 {
		return vMaxLevel
	}
	if v.gold < total {
		return vFunds
	}
	v.gold -= total
	for _, wall := range upgradable {
		wall.Level++
		v.updated[wall.ID] = wall
	}
	return vOK
}

func pendingCollection(b *building, nowMs int64) int64 {
	def := b.Def
	if def.Produces == "" || def.Produces == "None" || def.ProductionPerHour <= 0 {
		return 0
	}
	elapsed := nowMs - b.LastCollectedMs
	if elapsed <= 0 {
		return 0
	}
	rate := def.ProductionPerHour * int64(b.Level)
	capacity := def.StoredProductionCap * int64(b.Level)
	// Past this much time the mine is full anyway, which also rules out overflow below.
	if capacity > 0 && elapsed >= capacity*3600000/rate+3600000 {
		return capacity
	}
	produced := elapsed * rate / 3600000
	if produced > capacity {
		return capacity
	}
	return produced
}

// collect uses the server's clock, never the client's, so changing the device's date earns nothing.
func (v *village) collect(id int, nowMs int64) (int64, vErr) {
	b, ok := v.byID[id]
	if !ok {
		return 0, vNotFound
	}
	if b.Def.Produces == "" || b.Def.Produces == "None" {
		return 0, vNotACollector
	}
	pending := pendingCollection(b, nowMs)
	if pending <= 0 {
		return 0, vNothingCollect
	}
	var collected int64
	if b.Def.Produces == "Gold" {
		room := v.goldCapacity() - v.gold
		if room <= 0 {
			return 0, vStorageFull
		}
		collected = pending
		if room < collected {
			collected = room
		}
		v.gold += collected
	} else {
		collected = pending
		v.crystals += collected
	}
	// Anything that did not fit stays in the building by moving the timer back.
	leftover := pending - collected
	rate := b.Def.ProductionPerHour * int64(b.Level)
	b.LastCollectedMs = nowMs - leftover*3600000/rate
	v.updated[id] = b
	return collected, vOK
}
