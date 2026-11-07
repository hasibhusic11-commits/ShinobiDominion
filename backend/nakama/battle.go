package main

import (
	_ "embed"
	"encoding/json"
	"errors"
	"fmt"
	"math"
	"strconv"
)

// A line-for-line port of BattleSim.cs. The shared scenarios in battle_vectors.json must produce
// the exact same log in both, so a rule changed in only one language fails a test.

const (
	battleTickLimit = 180 * 10
	cellUnits       = 100
	deployClearance = 2
	battleGridSize  = 50
	walkCost        = 10
	wallCost        = 150

	roleTank     = 0
	roleFighter  = 1
	roleAssassin = 2
	roleMage     = 3
	roleControl  = 4
	roleSupport  = 5

	eventMove         = 1
	eventHeroHit      = 2
	eventBuildingDown = 3
	eventTowerHit     = 4
	eventHeroDown     = 5
	eventAbility      = 6
	eventHeal         = 7
	eventStun         = 8

	eventDefenderMove    = 9
	eventDefenderHit     = 10
	eventDefenderDamaged = 11
	eventDefenderDown    = 12
	eventDefenderAbility = 13
	eventDefenderHeal    = 14

	// How far a defender notices attackers, and how far from its post it will chase them.
	defenderAggro = 800
	defenderLeash = 1400
)

var (
	roleRanges         = []int{120, 120, 120, 500, 400, 400}
	roleSpeeds         = []int{9, 11, 16, 10, 10, 10}
	roleIntervals      = []int{15, 12, 8, 20, 18, 18}
	roleCooldowns      = []int{100, 60, 50, 70, 80, 70}
	roleDamagePercents = []int64{70, 100, 100, 100, 80, 60}
)

func roleNumber(role string) (int, error) {
	switch role {
	case "Tank":
		return roleTank, nil
	case "Fighter":
		return roleFighter, nil
	case "Assassin":
		return roleAssassin, nil
	case "Mage":
		return roleMage, nil
	case "Control":
		return roleControl, nil
	case "Support":
		return roleSupport, nil
	}
	return 0, errors.New("unknown role " + role)
}

type battleHeroInput struct {
	ID      string `json:"id"`
	Role    string `json:"role"`
	Hp      int64  `json:"hp"`
	Attack  int64  `json:"attack"`
	Defense int64  `json:"defense"`
	X       int    `json:"x"`
	Z       int    `json:"z"`
	IsTroop bool   `json:"isTroop"`
}

// battleDefenderInput is a hero guarding the village, standing at a cell on or by its Hero Base.
type battleDefenderInput struct {
	ID      string `json:"id"`
	Role    string `json:"role"`
	Hp      int64  `json:"hp"`
	Attack  int64  `json:"attack"`
	Defense int64  `json:"defense"`
	X       int    `json:"x"`
	Z       int    `json:"z"`
}

type battleBuildingInput struct {
	ID           int    `json:"id"`
	DefinitionID string `json:"definitionId"`
	X            int    `json:"x"`
	Z            int    `json:"z"`
	Width        int    `json:"width"`
	Depth        int    `json:"depth"`
	Level        int    `json:"level"`
	Hp           int64  `json:"hp"`
	Attack       int64  `json:"attack"`
	Range        int    `json:"range"`
	IsWall       bool   `json:"isWall"`
	IsDefense    bool   `json:"isDefense"`
}

type battleResult struct {
	Stars         int       `json:"stars"`
	DamagePercent int       `json:"damagePercent"`
	DojoDestroyed bool      `json:"dojoDestroyed"`
	EndTick       int       `json:"endTick"`
	HeroesAlive   int       `json:"heroesAlive"`
	DefendersAlive int      `json:"defendersAlive"`
	Checksum      string    `json:"checksum"`
	Events        [][]int64 `json:"events"`
}

type battleUnit struct {
	index       int
	role        int
	hp          int64
	maxHp       int64
	attack      int64
	defense     int64
	px          int
	pz          int
	target      int
	blocker     int
	path        []int
	pathAt      int
	needsPlan   bool
	idle        int
	attackTimer int
	abilityTimer int
	guardTimer  int
	isTroop     bool
}

type battleDefender struct {
	index        int
	role         int
	hp           int64
	maxHp        int64
	attack       int64
	defense      int64
	px           int
	pz           int
	tx           int
	tz           int
	homeX        int
	homeZ        int
	attackTimer  int
	abilityTimer int
	guardTimer   int
}

type battleStructure struct {
	input     battleBuildingInput
	hp        int64
	alive     bool
	shotTimer int
	stunTimer int
}

type battleSim struct {
	units      []*battleUnit
	defenders  []*battleDefender
	structures []*battleStructure
	grid       []int
	cost       []int
	from       []int
	heap       []int64
	result     *battleResult
	totalHp    int64
	dealtHp    int64
	tick       int
}

const costUnreached = int(^uint(0) >> 1)

func runBattle(buildings []battleBuildingInput, heroes []battleHeroInput) (*battleResult, error) {
	return runDefendedBattle(buildings, heroes, nil)
}

func runDefendedBattle(buildings []battleBuildingInput, heroes []battleHeroInput, defenders []battleDefenderInput) (*battleResult, error) {
	cells := battleGridSize * battleGridSize
	sim := &battleSim{
		grid:   make([]int, cells),
		cost:   make([]int, cells),
		from:   make([]int, cells),
		result: &battleResult{Events: [][]int64{}},
	}
	for i, b := range buildings {
		sim.structures = append(sim.structures, &battleStructure{input: b, hp: b.Hp, alive: true})
		if !b.IsWall {
			sim.totalHp += b.Hp
		}
		for z := b.Z; z < b.Z+b.Depth; z++ {
			for x := b.X; x < b.X+b.Width; x++ {
				sim.grid[z*battleGridSize+x] = i + 1
			}
		}
	}
	for i, h := range heroes {
		role, err := roleNumber(h.Role)
		if err != nil {
			return nil, err
		}
		sim.units = append(sim.units, &battleUnit{
			index: i, role: role, hp: h.Hp, maxHp: h.Hp, attack: h.Attack, defense: h.Defense,
			px: h.X*cellUnits + cellUnits/2, pz: h.Z*cellUnits + cellUnits/2,
			target: -1, blocker: -1, needsPlan: true, abilityTimer: roleCooldowns[role] / 2, isTroop: h.IsTroop,
		})
	}
	for i, d := range defenders {
		role, err := roleNumber(d.Role)
		if err != nil {
			return nil, err
		}
		px, pz := d.X*cellUnits+cellUnits/2, d.Z*cellUnits+cellUnits/2
		sim.defenders = append(sim.defenders, &battleDefender{
			index: i, role: role, hp: d.Hp, maxHp: d.Hp, attack: d.Attack, defense: d.Defense,
			px: px, pz: pz, tx: px, tz: pz, homeX: d.X, homeZ: d.Z, abilityTimer: roleCooldowns[role] / 2,
		})
	}
	sim.play()
	return sim.result, nil
}

func canDeployAt(buildings []battleBuildingInput, x, z int) bool {
	for _, b := range buildings {
		if x >= b.X-deployClearance && x < b.X+b.Width+deployClearance && z >= b.Z-deployClearance && z < b.Z+b.Depth+deployClearance {
			return false
		}
	}
	return true
}

// deployError is empty when the placement is legal. The client's rules use the same names.
func deployError(buildings []battleBuildingInput, heroes []battleHeroInput) string {
	if len(heroes) < 1 {
		return "NO_TEAM"
	}
	heroCount := 0
	for _, h := range heroes {
		if !h.IsTroop {
			heroCount++
		}
	}
	if heroCount > 6 {
		return "TOO_MANY_HEROES"
	}
	for i, h := range heroes {
		if h.X < 0 || h.X >= battleGridSize || h.Z < 0 || h.Z >= battleGridSize {
			return "BAD_DEPLOY_CELL"
		}
		if !canDeployAt(buildings, h.X, h.Z) {
			return "DEPLOY_TOO_CLOSE"
		}
		if h.IsTroop {
			continue
		}
		for j := 0; j < i; j++ {
			if heroes[j].IsTroop {
				continue
			}
			if heroes[j].ID == h.ID {
				return "DUPLICATE_HERO"
			}
			if heroes[j].X == h.X && heroes[j].Z == h.Z {
				return "DEPLOY_CELL_TAKEN"
			}
		}
	}
	return ""
}

func (s *battleSim) play() {
	for s.tick = 1; s.tick <= battleTickLimit; s.tick++ {
		for _, unit := range s.units {
			if unit.hp > 0 {
				s.stepUnit(unit)
			}
		}
		for _, defender := range s.defenders {
			if defender.hp > 0 {
				s.stepDefender(defender)
			}
		}
		for _, structure := range s.structures {
			if structure.alive && structure.input.IsDefense {
				s.stepTower(structure)
			}
		}
		if s.finished() {
			break
		}
	}
	endTick := s.tick
	if endTick > battleTickLimit {
		endTick = battleTickLimit
	}

	r := s.result
	r.EndTick = endTick
	if s.totalHp == 0 {
		r.DamagePercent = 100
	} else {
		r.DamagePercent = int(s.dealtHp * 100 / s.totalHp)
	}
	for _, unit := range s.units {
		if unit.hp > 0 {
			r.HeroesAlive++
		}
	}
	for _, defender := range s.defenders {
		if defender.hp > 0 {
			r.DefendersAlive++
		}
	}
	for _, structure := range s.structures {
		if structure.input.DefinitionID == "dojo_hq" && !structure.alive {
			r.DojoDestroyed = true
		}
	}
	if r.DamagePercent >= 50 {
		r.Stars++
	}
	if r.DojoDestroyed {
		r.Stars++
	}
	if r.DamagePercent >= 100 {
		r.Stars++
	}
	r.Checksum = battleChecksum(r.Events)
}

func (s *battleSim) finished() bool {
	anyHero := false
	for _, unit := range s.units {
		if unit.hp > 0 {
			anyHero = true
		}
	}
	if !anyHero {
		return true
	}
	for _, structure := range s.structures {
		if structure.alive && !structure.input.IsWall {
			return false
		}
	}
	return true
}

func battleChecksum(events [][]int64) string {
	hash := uint64(14695981039346656037)
	for _, entry := range events {
		for _, value := range entry {
			hash ^= uint64(value)
			hash *= 1099511628211
		}
	}
	return fmt.Sprintf("%016x", hash)
}

func (s *battleSim) log(kind int, a, b, c, d int64) {
	s.result.Events = append(s.result.Events, []int64{int64(s.tick), int64(kind), a, b, c, d})
}

// ---------- Heroes ----------

func (s *battleSim) stepUnit(unit *battleUnit) {
	if unit.attackTimer > 0 {
		unit.attackTimer--
	}
	if unit.abilityTimer > 0 {
		unit.abilityTimer--
	}
	if unit.guardTimer > 0 {
		unit.guardTimer--
	}
	if unit.idle > 0 {
		unit.idle--
	}
	s.selfAbility(unit)

	// A defender in reach comes first. The attacker stands and fights, then carries on.
	if foe := s.closestDefender(unit); foe != nil {
		if unit.attackTimer == 0 {
			s.strikeDefender(unit, foe)
			unit.attackTimer = roleIntervals[unit.role]
		}
		return
	}

	if unit.needsPlan || unit.target < 0 || !s.structures[unit.target].alive {
		if unit.idle > 0 {
			return
		}
		s.plan(unit)
	}
	if unit.target < 0 {
		return
	}

	focus := unit.target
	if unit.blocker >= 0 && s.structures[unit.blocker].alive {
		focus = unit.blocker
	}
	if inBattleRange(unit.px, unit.pz, s.structures[focus].input, roleRanges[unit.role]) {
		if unit.attackTimer == 0 {
			s.strike(unit, focus)
			unit.attackTimer = roleIntervals[unit.role]
		}
		return
	}

	if unit.pathAt >= len(unit.path) {
		unit.needsPlan = true
		return
	}
	next := unit.path[unit.pathAt]
	tx := (next%battleGridSize)*cellUnits + cellUnits/2
	tz := (next/battleGridSize)*cellUnits + cellUnits/2
	speed := roleSpeeds[unit.role]
	if unit.px != tx {
		step := minInt(absInt(tx-unit.px), speed)
		if tx > unit.px {
			unit.px += step
		} else {
			unit.px -= step
		}
	} else {
		step := minInt(absInt(tz-unit.pz), speed)
		if tz > unit.pz {
			unit.pz += step
		} else {
			unit.pz -= step
		}
	}
	if unit.px == tx && unit.pz == tz {
		unit.pathAt++
		if unit.pathAt < len(unit.path) {
			s.logMove(unit)
		}
	}
}

// ---------- Defenders ----------

func squaredPoints(ax, az, bx, bz int) int64 {
	dx, dz := int64(ax-bx), int64(az-bz)
	return dx*dx + dz*dz
}

func (s *battleSim) closestDefender(unit *battleUnit) *battleDefender {
	var best *battleDefender
	bestDistance := int64(math.MaxInt64)
	reach := int64(roleRanges[unit.role]) * int64(roleRanges[unit.role])
	for _, defender := range s.defenders {
		if defender.hp <= 0 {
			continue
		}
		distance := squaredPoints(unit.px, unit.pz, defender.px, defender.pz)
		if distance <= reach && distance < bestDistance {
			best = defender
			bestDistance = distance
		}
	}
	return best
}

func burstPercent(role int) int64 {
	switch role {
	case roleFighter:
		return 150
	case roleAssassin:
		return 300
	case roleMage:
		return 200
	}
	return 100
}

func clampHit(amount, hp int64) int64 {
	if amount < 1 {
		amount = 1
	}
	if amount > hp {
		amount = hp
	}
	return amount
}

func (s *battleSim) strikeDefender(unit *battleUnit, defender *battleDefender) {
	percent := int64(100)
	if unit.abilityTimer == 0 && !unit.isTroop {
		percent = burstPercent(unit.role)
		if percent != 100 {
			unit.abilityTimer = roleCooldowns[unit.role]
			s.log(eventAbility, int64(unit.index), int64(unit.role), 0, 0)
		}
	}
	amount := unit.attack * percent / 100 * 100 / (100 + defender.defense)
	if defender.guardTimer > 0 {
		amount /= 2
	}
	amount = clampHit(amount, defender.hp)
	defender.hp -= amount
	s.log(eventDefenderDamaged, int64(unit.index), int64(defender.index), amount, defender.hp)
	if defender.hp == 0 {
		s.log(eventDefenderDown, int64(defender.index), 0, 0, 0)
	}
}

func (s *battleSim) stepDefender(defender *battleDefender) {
	if defender.attackTimer > 0 {
		defender.attackTimer--
	}
	if defender.abilityTimer > 0 {
		defender.abilityTimer--
	}
	if defender.guardTimer > 0 {
		defender.guardTimer--
	}

	target := s.closestAttacker(defender)
	if defender.abilityTimer == 0 {
		if defender.role == roleTank && target != nil {
			defender.guardTimer = 40
			defender.abilityTimer = roleCooldowns[roleTank]
			s.log(eventDefenderAbility, int64(defender.index), roleTank, 0, 0)
		} else if defender.role == roleSupport {
			s.mendDefenders(defender)
		}
	}

	if target == nil {
		s.walkDefender(defender, defender.homeX, defender.homeZ)
		return
	}
	reach := int64(roleRanges[defender.role]) * int64(roleRanges[defender.role])
	if squaredPoints(defender.px, defender.pz, target.px, target.pz) <= reach {
		if defender.attackTimer == 0 {
			s.defenderStrike(defender, target)
			defender.attackTimer = roleIntervals[defender.role]
		}
		return
	}
	s.walkDefender(defender, target.px/cellUnits, target.pz/cellUnits)
}

func (s *battleSim) closestAttacker(defender *battleDefender) *battleUnit {
	var best *battleUnit
	bestDistance := int64(math.MaxInt64)
	homeX := defender.homeX*cellUnits + cellUnits/2
	homeZ := defender.homeZ*cellUnits + cellUnits/2
	for _, unit := range s.units {
		if unit.hp <= 0 {
			continue
		}
		distance := squaredPoints(defender.px, defender.pz, unit.px, unit.pz)
		if distance > int64(defenderAggro)*int64(defenderAggro) {
			continue
		}
		if squaredPoints(homeX, homeZ, unit.px, unit.pz) > int64(defenderLeash)*int64(defenderLeash) {
			continue
		}
		if distance < bestDistance {
			best = unit
			bestDistance = distance
		}
	}
	return best
}

func (s *battleSim) mendDefenders(healer *battleDefender) {
	healed := false
	for _, other := range s.defenders {
		if other.hp <= 0 || other.hp >= other.maxHp {
			continue
		}
		if squaredPoints(other.px, other.pz, healer.px, healer.pz) > 600*600 {
			continue
		}
		amount := healer.attack * 2
		if room := other.maxHp - other.hp; amount > room {
			amount = room
		}
		other.hp += amount
		if !healed {
			s.log(eventDefenderAbility, int64(healer.index), roleSupport, 0, 0)
			healed = true
		}
		s.log(eventDefenderHeal, int64(other.index), amount, other.hp, 0)
	}
	if healed {
		healer.abilityTimer = roleCooldowns[roleSupport]
	}
}

func (s *battleSim) defenderStrike(defender *battleDefender, unit *battleUnit) {
	percent := int64(100)
	if defender.abilityTimer == 0 {
		percent = burstPercent(defender.role)
		if percent != 100 {
			defender.abilityTimer = roleCooldowns[defender.role]
			s.log(eventDefenderAbility, int64(defender.index), int64(defender.role), 0, 0)
		}
	}
	amount := defender.attack * percent / 100 * 100 / (100 + unit.defense)
	if unit.guardTimer > 0 {
		amount /= 2
	}
	amount = clampHit(amount, unit.hp)
	unit.hp -= amount
	s.log(eventDefenderHit, int64(defender.index), int64(unit.index), amount, unit.hp)
	if unit.hp == 0 {
		s.log(eventHeroDown, int64(unit.index), 0, 0, 0)
	}
}

func (s *battleSim) defenderCanEnter(x, z int) bool {
	occupant := s.grid[z*battleGridSize+x]
	return occupant == 0 || s.structures[occupant-1].input.DefinitionID == "hero_base"
}

// walkDefender goes one cell at a time towards a goal, along x and then z. Buildings and walls stop it.
func (s *battleSim) walkDefender(defender *battleDefender, goalX, goalZ int) {
	if defender.px == defender.tx && defender.pz == defender.tz {
		x, z := defender.px/cellUnits, defender.pz/cellUnits
		if goalX != x && s.defenderCanEnter(x+sign(goalX-x), z) {
			x += sign(goalX - x)
		} else if goalZ != z && s.defenderCanEnter(x, z+sign(goalZ-z)) {
			z += sign(goalZ - z)
		} else {
			return
		}
		defender.tx = x*cellUnits + cellUnits/2
		defender.tz = z*cellUnits + cellUnits/2
		s.log(eventDefenderMove, int64(defender.index), int64(x), int64(z), 0)
	}
	speed := roleSpeeds[defender.role]
	if defender.px != defender.tx {
		step := minInt(absInt(defender.tx-defender.px), speed)
		if defender.tx > defender.px {
			defender.px += step
		} else {
			defender.px -= step
		}
	} else {
		step := minInt(absInt(defender.tz-defender.pz), speed)
		if defender.tz > defender.pz {
			defender.pz += step
		} else {
			defender.pz -= step
		}
	}
}

func (s *battleSim) logMove(unit *battleUnit) {
	next := unit.path[unit.pathAt]
	s.log(eventMove, int64(unit.index), int64(next%battleGridSize), int64(next/battleGridSize), 0)
}

func (s *battleSim) selfAbility(unit *battleUnit) {
	if unit.abilityTimer > 0 || unit.isTroop {
		return
	}
	switch unit.role {
	case roleTank:
		unit.guardTimer = 40
		unit.abilityTimer = roleCooldowns[roleTank]
		s.log(eventAbility, int64(unit.index), roleTank, 0, 0)
	case roleSupport:
		healed := false
		for _, other := range s.units {
			if other.hp <= 0 || other.hp >= other.maxHp {
				continue
			}
			dx := int64(other.px - unit.px)
			dz := int64(other.pz - unit.pz)
			if dx*dx+dz*dz > 600*600 {
				continue
			}
			amount := unit.attack * 2
			if other.maxHp-other.hp < amount {
				amount = other.maxHp - other.hp
			}
			other.hp += amount
			if !healed {
				s.log(eventAbility, int64(unit.index), roleSupport, 0, 0)
				healed = true
			}
			s.log(eventHeal, int64(other.index), amount, other.hp, 0)
		}
		if healed {
			unit.abilityTimer = roleCooldowns[roleSupport]
		}
	case roleControl:
		best := -1
		bestDistance := int64(^uint64(0) >> 1)
		for i, structure := range s.structures {
			if !structure.alive || !structure.input.IsDefense || structure.stunTimer > 0 {
				continue
			}
			distance := squaredDistance(unit.px, unit.pz, structure.input)
			reach := int64(roleRanges[roleControl]) + 200
			if distance <= reach*reach && distance < bestDistance {
				best = i
				bestDistance = distance
			}
		}
		if best >= 0 {
			s.structures[best].stunTimer = 30
			unit.abilityTimer = roleCooldowns[roleControl]
			s.log(eventAbility, int64(unit.index), roleControl, 0, 0)
			s.log(eventStun, int64(s.structures[best].input.ID), 0, 0, 0)
		}
	}
}

func (s *battleSim) strike(unit *battleUnit, focus int) {
	base := unit.attack * roleDamagePercents[unit.role] / 100
	percent := int64(100)
	splash := 0
	if unit.abilityTimer == 0 && !unit.isTroop {
		switch unit.role {
		case roleFighter:
			percent = 150
			splash = 150
		case roleAssassin:
			percent = 300
		case roleMage:
			percent = 200
			splash = 250
		}
		if percent != 100 {
			unit.abilityTimer = roleCooldowns[unit.role]
			s.log(eventAbility, int64(unit.index), int64(unit.role), 0, 0)
		}
	}

	input := s.structures[focus].input
	cx := input.X*cellUnits + input.Width*cellUnits/2
	cz := input.Z*cellUnits + input.Depth*cellUnits/2
	s.damage(unit, focus, base*percent/100)
	if splash == 0 {
		return
	}
	for i, structure := range s.structures {
		if i == focus || !structure.alive {
			continue
		}
		if squaredDistance(cx, cz, structure.input) <= int64(splash)*int64(splash) {
			s.damage(unit, i, base)
		}
	}
}

func (s *battleSim) damage(unit *battleUnit, index int, amount int64) {
	structure := s.structures[index]
	if amount < 1 {
		amount = 1
	}
	if amount > structure.hp {
		amount = structure.hp
	}
	structure.hp -= amount
	if !structure.input.IsWall {
		s.dealtHp += amount
	}
	s.log(eventHeroHit, int64(unit.index), int64(structure.input.ID), amount, structure.hp)
	if structure.hp > 0 {
		return
	}

	structure.alive = false
	b := structure.input
	for z := b.Z; z < b.Z+b.Depth; z++ {
		for x := b.X; x < b.X+b.Width; x++ {
			s.grid[z*battleGridSize+x] = 0
		}
	}
	s.log(eventBuildingDown, int64(b.ID), 0, 0, 0)
	for _, other := range s.units {
		if other.target == index || other.blocker == index {
			other.needsPlan = true
		}
	}
}

// ---------- Choosing targets and paths ----------

func (s *battleSim) plan(unit *battleUnit) {
	unit.needsPlan = false
	unit.blocker = -1
	unit.path = nil
	unit.pathAt = 0
	unit.target = s.chooseTarget(unit)
	if unit.target < 0 {
		return
	}

	path, found := s.findPath(unit)
	if !found {
		unit.target = -1
		unit.idle = 20
		return
	}

	length := len(path)
	for i, cell := range path {
		occupant := s.grid[cell]
		if occupant != 0 && s.structures[occupant-1].input.IsWall {
			unit.blocker = occupant - 1
			length = i
			break
		}
	}
	unit.path = path[:length]
	if len(unit.path) > 0 {
		s.logMove(unit)
	}
}

func (s *battleSim) chooseTarget(unit *battleUnit) int {
	wantDefense := false
	if unit.role == roleAssassin {
		for _, structure := range s.structures {
			if structure.alive && structure.input.IsDefense {
				wantDefense = true
			}
		}
	}
	best := -1
	bestDistance := int64(^uint64(0) >> 1)
	for i, structure := range s.structures {
		if !structure.alive || structure.input.IsWall {
			continue
		}
		if wantDefense && !structure.input.IsDefense {
			continue
		}
		distance := squaredDistance(unit.px, unit.pz, structure.input)
		if distance < bestDistance {
			best = i
			bestDistance = distance
		}
	}
	return best
}

func (s *battleSim) findPath(unit *battleUnit) ([]int, bool) {
	target := s.structures[unit.target].input
	reach := roleRanges[unit.role]
	start := minInt(unit.pz/cellUnits, battleGridSize-1)*battleGridSize + minInt(unit.px/cellUnits, battleGridSize-1)
	for i := range s.cost {
		s.cost[i] = costUnreached
	}
	s.heap = s.heap[:0]
	s.cost[start] = 0
	s.from[start] = -1
	s.heapPush(start)

	for len(s.heap) > 0 {
		entry := s.heapPop()
		cell := int(entry % 4096)
		cost := int(entry / 4096)
		if cost != s.cost[cell] {
			continue
		}
		cx := cell % battleGridSize
		cz := cell / battleGridSize
		if inBattleRange(cx*cellUnits+cellUnits/2, cz*cellUnits+cellUnits/2, target, reach) {
			path := []int{}
			for at := cell; at != start; at = s.from[at] {
				path = append(path, at)
			}
			for i, j := 0, len(path)-1; i < j; i, j = i+1, j-1 {
				path[i], path[j] = path[j], path[i]
			}
			return path, true
		}
		if cx+1 < battleGridSize {
			s.relax(cell, cell+1, cost)
		}
		if cx > 0 {
			s.relax(cell, cell-1, cost)
		}
		if cz+1 < battleGridSize {
			s.relax(cell, cell+battleGridSize, cost)
		}
		if cz > 0 {
			s.relax(cell, cell-battleGridSize, cost)
		}
	}
	return nil, false
}

func (s *battleSim) relax(from, to, cost int) {
	occupant := s.grid[to]
	step := walkCost
	if occupant != 0 {
		if !s.structures[occupant-1].input.IsWall {
			return
		}
		step = wallCost
	}
	if cost+step >= s.cost[to] {
		return
	}
	s.cost[to] = cost + step
	s.from[to] = from
	s.heapPush(to)
}

func (s *battleSim) heapPush(cell int) {
	s.heap = append(s.heap, int64(s.cost[cell])*4096+int64(cell))
	i := len(s.heap) - 1
	for i > 0 {
		parent := (i - 1) / 2
		if s.heap[parent] <= s.heap[i] {
			break
		}
		s.heap[parent], s.heap[i] = s.heap[i], s.heap[parent]
		i = parent
	}
}

func (s *battleSim) heapPop() int64 {
	top := s.heap[0]
	last := len(s.heap) - 1
	s.heap[0] = s.heap[last]
	s.heap = s.heap[:last]
	i := 0
	for {
		left := i*2 + 1
		right := left + 1
		least := i
		if left < len(s.heap) && s.heap[left] < s.heap[least] {
			least = left
		}
		if right < len(s.heap) && s.heap[right] < s.heap[least] {
			least = right
		}
		if least == i {
			break
		}
		s.heap[least], s.heap[i] = s.heap[i], s.heap[least]
		i = least
	}
	return top
}

func squaredDistance(px, pz int, b battleBuildingInput) int64 {
	minX := b.X * cellUnits
	maxX := (b.X + b.Width) * cellUnits
	minZ := b.Z * cellUnits
	maxZ := (b.Z + b.Depth) * cellUnits
	var dx, dz int64
	if px < minX {
		dx = int64(minX - px)
	} else if px > maxX {
		dx = int64(px - maxX)
	}
	if pz < minZ {
		dz = int64(minZ - pz)
	} else if pz > maxZ {
		dz = int64(pz - maxZ)
	}
	return dx*dx + dz*dz
}

func inBattleRange(px, pz int, b battleBuildingInput, reach int) bool {
	return squaredDistance(px, pz, b) <= int64(reach)*int64(reach)
}

// ---------- Towers ----------

func (s *battleSim) stepTower(tower *battleStructure) {
	if tower.shotTimer > 0 {
		tower.shotTimer--
	}
	if tower.stunTimer > 0 {
		tower.stunTimer--
		return
	}
	if tower.shotTimer > 0 {
		return
	}

	input := tower.input
	splashing := input.DefinitionID == "spirit_turret"
	reach := int64(input.Range) * cellUnits
	var chosen *battleUnit
	chosenDistance := int64(^uint64(0) >> 1)
	chosenGuard := false
	for _, unit := range s.units {
		if unit.hp <= 0 {
			continue
		}
		distance := squaredDistance(unit.px, unit.pz, input)
		if distance > reach*reach {
			continue
		}
		guarding := unit.guardTimer > 0
		if chosen == nil || (guarding && !chosenGuard) || (guarding == chosenGuard && distance < chosenDistance) {
			chosen = unit
			chosenDistance = distance
			chosenGuard = guarding
		}
	}
	if chosen == nil {
		return
	}

	if splashing {
		tower.shotTimer = 15
	} else {
		tower.shotTimer = 10
	}
	s.shoot(tower, chosen, 100)
	if !splashing {
		return
	}
	for _, unit := range s.units {
		if unit == chosen || unit.hp <= 0 {
			continue
		}
		dx := int64(unit.px - chosen.px)
		dz := int64(unit.pz - chosen.pz)
		if dx*dx+dz*dz <= 150*150 {
			s.shoot(tower, unit, 50)
		}
	}
}

func (s *battleSim) shoot(tower *battleStructure, unit *battleUnit, percent int64) {
	amount := tower.input.Attack * percent / 100 * 100 / (100 + unit.defense)
	if unit.guardTimer > 0 {
		amount /= 2
	}
	if amount < 1 {
		amount = 1
	}
	if amount > unit.hp {
		amount = unit.hp
	}
	unit.hp -= amount
	s.log(eventTowerHit, int64(tower.input.ID), int64(unit.index), amount, unit.hp)
	if unit.hp == 0 {
		s.log(eventHeroDown, int64(unit.index), 0, 0, 0)
	}
}

func minInt(a, b int) int {
	if a < b {
		return a
	}
	return b
}

func absInt(a int) int {
	if a < 0 {
		return -a
	}
	return a
}

// ---------- Raid data, from the same raids.json the game ships ----------

//go:embed config/raids.json
var raidsJSON []byte

type raidEntry struct {
	Def   string `json:"def"`
	Level int    `json:"level"`
	X     int    `json:"x"`
	Z     int    `json:"z"`
}

type raidDefenderEntry struct {
	HeroID    string `json:"heroId"`
	Evolution int    `json:"evolution"`
	Level     int    `json:"level"`
	X         int    `json:"x"`
	Z         int    `json:"z"`
}

type raidWall struct {
	Level int `json:"level"`
	X1    int `json:"x1"`
	Z1    int `json:"z1"`
	X2    int `json:"x2"`
	Z2    int `json:"z2"`
}

type raidDefinition struct {
	ID          string      `json:"id"`
	DisplayName string      `json:"displayName"`
	Description string      `json:"description"`
	Version     int         `json:"version"`
	GoldPerStar int64       `json:"goldPerStar"`
	Buildings   []raidEntry `json:"buildings"`
	Walls       []raidWall  `json:"walls"`
	Defenders   []raidDefenderEntry `json:"defenders"`
	Requires    string `json:"requires"`
	XpPerWin    int64  `json:"xpPerWin"`
	FirstClearCrystals int64 `json:"firstClearCrystals"`
	FirstClearItem string `json:"firstClearItem"`
	expanded    []battleBuildingInput
}

type raidCatalog struct {
	byID map[string]*raidDefinition
	all  []*raidDefinition
}

func loadRaidCatalog(data []byte, buildings *BuildingCatalog) (*raidCatalog, error) {
	var document struct {
		SchemaVersion int               `json:"schemaVersion"`
		Raids         []*raidDefinition `json:"raids"`
	}
	if err := json.Unmarshal(data, &document); err != nil {
		return nil, err
	}
	if document.SchemaVersion != 1 {
		return nil, errors.New("unsupported raid file")
	}
	catalog := &raidCatalog{byID: map[string]*raidDefinition{}, all: document.Raids}
	known := map[string]bool{}
	for _, raid := range document.Raids {
		if raid.Requires != "" && !known[raid.Requires] {
			return nil, errors.New("raid " + raid.ID + " requires a raid listed later or not at all: " + raid.Requires)
		}
		known[raid.ID] = true
		expanded, err := expandRaid(raid, buildings)
		if err != nil {
			return nil, err
		}
		raid.expanded = expanded
		catalog.byID[raid.ID] = raid
	}
	return catalog, nil
}

func expandRaid(raid *raidDefinition, buildings *BuildingCatalog) ([]battleBuildingInput, error) {
	result := []battleBuildingInput{}
	add := func(definitionID string, level, x, z int) error {
		def, ok := buildings.byID[definitionID]
		if !ok {
			return errors.New("raid " + raid.ID + ": unknown building " + definitionID)
		}
		if level < 1 {
			level = 1
		}
		row := def.at(level)
		result = append(result, battleBuildingInput{
			ID: len(result) + 1, DefinitionID: definitionID, X: x, Z: z, Width: def.Width, Depth: def.Depth,
			Level: level, Hp: row.Hp, Attack: row.Attack, Range: row.Range,
			IsWall: def.isWall(), IsDefense: def.Function == "Defense",
		})
		return nil
	}
	for _, entry := range raid.Buildings {
		if err := add(entry.Def, entry.Level, entry.X, entry.Z); err != nil {
			return nil, err
		}
	}
	taken := map[int]bool{}
	for _, b := range result {
		for z := b.Z; z < b.Z+b.Depth; z++ {
			for x := b.X; x < b.X+b.Width; x++ {
				cell := z*battleGridSize + x
				if x < 0 || x >= battleGridSize || z < 0 || z >= battleGridSize || taken[cell] {
					return nil, errors.New("raid " + raid.ID + ": " + b.DefinitionID + " overlaps or leaves the map")
				}
				taken[cell] = true
			}
		}
	}
	wallAt := map[int]bool{}
	for _, run := range raid.Walls {
		if run.X1 != run.X2 && run.Z1 != run.Z2 {
			return nil, errors.New("raid " + raid.ID + ": diagonal wall")
		}
		dx, dz := sign(run.X2-run.X1), sign(run.Z2-run.Z1)
		length := absInt(run.X2 - run.X1)
		if absInt(run.Z2-run.Z1) > length {
			length = absInt(run.Z2 - run.Z1)
		}
		for i := 0; i <= length; i++ {
			x, z := run.X1+dx*i, run.Z1+dz*i
			if x < 0 || x >= battleGridSize || z < 0 || z >= battleGridSize {
				return nil, errors.New("raid " + raid.ID + ": wall leaves the map")
			}
			cell := z*battleGridSize + x
			if taken[cell] {
				if wallAt[cell] {
					continue
				}
				return nil, errors.New("raid " + raid.ID + ": wall overlaps a building")
			}
			taken[cell] = true
			wallAt[cell] = true
			if err := add("wall", run.Level, x, z); err != nil {
				return nil, err
			}
		}
	}
	return result, nil
}

func sign(value int) int {
	if value > 0 {
		return 1
	}
	if value < 0 {
		return -1
	}
	return 0
}

// heroBattleStats mirrors HeroMath.Stats: progress never falls when a hero evolves.
func heroBattleStats(a Archetype, evolution, level int) (hp, attack, defense int64) {
	steps := int64(200*evolution + level - 1)
	return a.BaseHp + a.GrowthHp*steps, a.BaseAttack + a.GrowthAttack*steps, a.BaseDefense + a.GrowthDefense*steps
}

func placementHash(attackID string, parts []deployPlacement, stacks []troopStack) string {
	joined := attackID + "|"
	for _, stack := range stacks {
		joined += stack.TroopID + ":" + strconv.Itoa(stack.X) + ":" + strconv.Itoa(stack.Z) + ":" + strconv.Itoa(stack.Count) + "|"
	}
	for _, part := range parts {
		joined += part.InstanceID + ":" + strconv.Itoa(part.X) + ":" + strconv.Itoa(part.Z) + "|"
	}
	return joined
}
