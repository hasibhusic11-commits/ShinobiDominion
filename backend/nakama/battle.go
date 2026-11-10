package main

import (
	_ "embed"
	"encoding/json"
	"errors"
	"fmt"
	"math"
	"sort"
	"strconv"
)

// A line-for-line port of BattleSim.cs. The shared scenarios in battle_vectors.json must produce
// the exact same log in both, so a rule changed in only one language fails a test.

const (
	battleTicksPerSecond = 10
	battleTickLimit = 180 * battleTicksPerSecond
	cellUnits       = 100
	deployClearance = 2
	battleGridSize  = 50
	// Routes are priced in ticks. Attackers, heroes and troops alike, break a wall in the way (the
	// hits it takes) or walk around it, whichever is quicker. Only guards hop walls, in twice the
	// time of a step.
	hopSteps    = 2
	maxPathCost = 1_000_000

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
	// a = unit (c = 0) or defender (c = 1), b = ability charge now, 0 to chargeFull.
	eventCharge = 15
	// a = unit (c = 0) or defender (c = 1) who roared, b = allies (itself included) now attacking faster.
	eventRally = 16
	// a = trap building id, b = 0 for a Bomb Trap or 1 for a Hero Trap. The trap is spent.
	eventTrapFired = 17
	// a = trap building id, b = unit hit, c = damage, d = unit HP left.
	eventTrapHit = 18
	// Timed battles only (ADR-050): a = unit, b = cell x, c = cell z. The unit lands there; it acts from this tick.
	// Units are numbered in landing order.
	eventDeploy = 19
	// Spells, timed battles only (ADR-052). In each of these a = cast number: casts are numbered in the order they
	// land, which is their order in the sent list.
	// b = cell x, c = cell z, d = effect number (effectDamage and the rest). The spell lands, centred on the middle
	// of that cell. Its first eventSpellPulse follows on the same tick.
	eventSpellCast = 20
	// b = which application this is (1 for the first), c = how many it has in all, d = radius in battle units. One
	// per application, freeze and boost included; the entries for each target it changed follow.
	eventSpellPulse = 21
	// b = building id, c = damage, d = building HP left. eventBuildingDown follows if it fell.
	eventSpellHit = 22
	// b = defender, c = damage, d = defender HP left. eventDefenderDown follows if it fell.
	eventSpellHitDefender = 23
	// b = unit healed, c = amount, d = unit HP now.
	eventSpellHeal = 24
	// b = 0 for a tower or 1 for a defender, c = building id or defender, d = ticks it cannot act, this tick included.
	eventSpellFreeze = 25
	// b = unit, c = ticks the boost lasts, this tick included, d = percent more damage.
	eventSpellBoost = 26

	// What a battle spell does (ADR-052), as the number the log uses for it. Mirrors BattleSim.EffectDamage and the rest.
	effectDamage = 0
	effectHeal   = 1
	effectFreeze = 2
	effectBoost  = 3

	// Traps (ADR-046): a trap fires when an attacker it can catch comes within trapTrigger of its middle. A Bomb
	// Trap then hits every troop within its range in cells; a Hero Trap hurts that hero and holds it in place.
	trapTrigger   = 120
	heroTrapStun  = 30

	// Landed hits that fill a hero's ability bar (owner's rule, 2026-11-08). At full, the special fires and the
	// bar empties. Mirrors BattleSim.ChargeFull.
	chargeFull = 5

	// Rally (Solar Beast Sage's Radiant Roar, owner's design 2026-11-08): at full charge the hero roars at once,
	// and every ally within rallyRange, itself and troops included, attacks hastePercent faster for hasteTicks.
	// Mirrors BattleSim.
	specialRally = "rally"
	rallyRange   = 600
	hasteTicks   = 60
	// Roaring Sun (Evo II and up): the haste lasts this long instead.
	hasteTicksEvolved = 90
	hastePercent = 30

	// Special power by rarity (owner's rule, 2026-11-08): Common specials still fire but are weak, Elite a
	// little stronger, Legendary full. Mirrors BattleSim.PowerCommon and the rest.
	powerCommon    = 50
	powerElite     = 75
	powerLegendary = 100

	// How far a defender notices attackers, and how far from its post it will chase them.
	defenderAggro = 800
	defenderLeash = 1400
)

var (
	roleRanges         = []int{120, 120, 120, 500, 400, 400}
	roleSpeeds         = []int{9, 11, 16, 10, 10, 10}
	roleIntervals      = []int{15, 12, 8, 20, 18, 18}
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

// effectNumber mirrors BattleSim.EffectNumber.
func effectNumber(effect string) (int, error) {
	switch effect {
	case "damage":
		return effectDamage, nil
	case "heal":
		return effectHeal, nil
	case "freeze":
		return effectFreeze, nil
	case "boost":
		return effectBoost, nil
	}
	return 0, errors.New("unknown spell effect " + effect)
}

// battleSpellInput is one cast of a battle spell (ADR-052), already worked out from spells.json for the caster's
// spell level, so the sim needs no catalog. Mirrors BattleSpellInput in BattleSim.cs; spellCatalog.toInput builds it.
type battleSpellInput struct {
	// ID is the spell's id in spells.json. The sim does not read it.
	ID string `json:"id"`
	// Effect is "damage", "heal", "freeze" or "boost".
	Effect string `json:"effect"`
	// X, Z: the cell the spell is centred on, any cell of the grid, buildings included.
	X int `json:"x"`
	Z int `json:"z"`
	// Radius is the reach from the middle of that cell, in battle units (cellUnits to a cell).
	Radius int `json:"radius"`
	// Pulses is how many times it applies. Freeze and boost apply once.
	Pulses int `json:"pulses"`
	// PulseTicks is the ticks from one application to the next.
	PulseTicks int `json:"pulseTicks"`
	// Power is the damage or heal of each application, or the percent more damage of a boost. 0 for a freeze.
	Power int64 `json:"power"`
	// Ticks is how long a freeze or a boost lasts. 0 for damage and heal.
	Ticks int `json:"ticks"`
	// Tick is the tick of the battle the spell was cast on. It lands at the start of the next one.
	Tick int `json:"tick"`
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
	// Special is the hero's own special in place of its role's, from heroes.json: "" or "rally".
	Special string `json:"special,omitempty"`
	// Evolution is how many times the hero has evolved (0 to 3); the rally lasts longer from Evo II.
	Evolution int `json:"evolution,omitempty"`
	// Power is how strong the special is, in percent of a Legendary's: by rarity, set by the server. 0 means 100.
	Power int `json:"power,omitempty"`
	// DeployTick is when the card was dropped (ADR-050): the unit lands after this tick and acts from the next one.
	// Only a timed battle (one with an end tick) uses it; elsewhere it must be 0.
	DeployTick int `json:"deployTick,omitempty"`
	// Talents (ADR-053), set by the server; all 0 for troops. StartCharge is the ability charge the hero lands with,
	// SiegePercent its extra damage to buildings and SpeedPercent its extra walking speed.
	StartCharge  int `json:"startCharge,omitempty"`
	SiegePercent int `json:"siegePercent,omitempty"`
	SpeedPercent int `json:"speedPercent,omitempty"`
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
	Special string `json:"special,omitempty"`
	// Evolution is how many times the hero has evolved (0 to 3); the rally lasts longer from Evo II.
	Evolution int `json:"evolution,omitempty"`
	// Power is how strong the special is, in percent of a Legendary's: by rarity, set by the server. 0 means 100.
	Power int `json:"power,omitempty"`
	// Talents (ADR-053): the charge the defender starts with and its extra walking speed.
	StartCharge  int `json:"startCharge,omitempty"`
	SpeedPercent int `json:"speedPercent,omitempty"`
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
	// IsTrap marks a hidden trap: it is walked over, never targeted, and counts for nothing in the score.
	IsTrap       bool   `json:"isTrap,omitempty"`
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
	charge      int
	guardTimer  int
	hasteTimer  int
	rally       bool
	evolution   int
	power       int
	// speed is the walking speed per tick: the role's, raised by Swift Feet. siege is extra damage to buildings (ADR-053).
	speed       int
	siege       int64
	isTroop     bool
	// stunTimer holds a unit in place after a Hero Trap: it does nothing until it runs out.
	stunTimer   int
	// A boost spell (ADR-052): the unit deals boostPercent more damage on every tick before boostUntil.
	boostUntil   int
	boostPercent int64
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
	charge       int
	guardTimer   int
	hasteTimer   int
	rally        bool
	evolution    int
	power        int
	speed        int
	// stunTimer is a freeze spell (ADR-052): the defender does nothing, and its own timers stand still, until it runs out.
	stunTimer    int
}

// battleSpell is a spell that has landed (ADR-052).
type battleSpell struct {
	index      int
	input      battleSpellInput
	effect     int
	cx         int
	cz         int
	pulsesLeft int
	nextPulse  int
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
	// pending holds cards dropped but not landed yet, in landing order.
	pending []battleHeroInput
	// pendingCasts holds spells cast but not landed yet, in the order cast (ADR-052); spells holds the landed ones.
	pendingCasts []battleSpellInput
	spells       []*battleSpell
	// timed (ADR-050): the battle ends at endTick or when every building is down, never because the field is empty.
	timed   bool
	endTick int
	done    bool
	defenders  []*battleDefender
	structures []*battleStructure
	traps      []*battleStructure
	grid       []int
	cost       []int
	from       []int
	heap       []int64
	pathStep     int
	pathPerHit   int64
	pathInterval int
	result     *battleResult
	totalHp    int64
	dealtHp    int64
	tick       int
}

const costUnreached = int(^uint(0) >> 1)

func runBattle(buildings []battleBuildingInput, heroes []battleHeroInput) (*battleResult, error) {
	return runDefendedBattle(buildings, heroes, nil)
}

// runDefendedBattle fights with everyone landed before the first tick, until one side is gone or time is up.
func runDefendedBattle(buildings []battleBuildingInput, heroes []battleHeroInput, defenders []battleDefenderInput) (*battleResult, error) {
	return runTimedBattle(buildings, heroes, defenders, 0, nil)
}

// runTimedBattle mirrors BattleSim.Run with an end tick. Above 0 it is a timed battle (ADR-050): each unit lands at its
// DeployTick, in landingOrder, and the battle stops at endTick (when the attacker ended it) or when every building is down.
// Spells (ADR-052) land on the tick after their own, after the cards of that tick; only a timed battle can have them.
func runTimedBattle(buildings []battleBuildingInput, heroes []battleHeroInput, defenders []battleDefenderInput, endTick int, casts []battleSpellInput) (*battleResult, error) {
	cells := battleGridSize * battleGridSize
	sim := &battleSim{
		grid:    make([]int, cells),
		cost:    make([]int, cells),
		from:    make([]int, cells),
		result:  &battleResult{Events: [][]int64{}},
		timed:   endTick > 0,
		endTick: minInt(endTick, battleTickLimit),
	}
	for _, b := range buildings {
		if b.IsTrap {
			sim.traps = append(sim.traps, &battleStructure{input: b, hp: b.Hp, alive: true})
			continue
		}
		i := len(sim.structures)
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
	for _, h := range heroes {
		if _, err := roleNumber(h.Role); err != nil {
			return nil, err
		}
	}
	for i, d := range defenders {
		role, err := roleNumber(d.Role)
		if err != nil {
			return nil, err
		}
		px, pz := d.X*cellUnits+cellUnits/2, d.Z*cellUnits+cellUnits/2
		sim.defenders = append(sim.defenders, &battleDefender{
			index: i, role: role, hp: d.Hp, maxHp: d.Hp, attack: d.Attack, defense: d.Defense,
			px: px, pz: pz, tx: px, tz: pz, homeX: d.X, homeZ: d.Z,
			rally: d.Special == specialRally, evolution: d.Evolution, power: powerOf(d.Power),
			charge: startCharge(d.StartCharge), speed: speedWith(role, d.SpeedPercent),
		})
	}
	for _, defender := range sim.defenders {
		if defender.charge > 0 {
			sim.log(eventCharge, int64(defender.index), int64(defender.charge), 1, 0)
		}
	}
	if sim.timed {
		sim.pending = landingOrder(heroes)
	} else {
		for _, h := range heroes {
			sim.land(h)
		}
	}
	if len(casts) > 0 {
		if !sim.timed {
			return nil, errors.New("only a timed battle can have spells")
		}
		sim.pendingCasts = append([]battleSpellInput{}, casts...)
		sort.SliceStable(sim.pendingCasts, func(i, j int) bool { return sim.pendingCasts[i].Tick < sim.pendingCasts[j].Tick })
		for _, cast := range sim.pendingCasts {
			if err := checkSpell(cast); err != nil {
				return nil, err
			}
		}
	}
	for sim.step() {
	}
	sim.finish()
	return sim.result, nil
}

// landingOrder mirrors BattleSim.LandingOrder: by DeployTick, heroes before troops on the same tick, otherwise as listed.
func landingOrder(heroes []battleHeroInput) []battleHeroInput {
	ordered := append([]battleHeroInput{}, heroes...)
	sort.SliceStable(ordered, func(i, j int) bool {
		a, b := ordered[i], ordered[j]
		if a.DeployTick != b.DeployTick {
			return a.DeployTick < b.DeployTick
		}
		return !a.IsTroop && b.IsTroop
	})
	return ordered
}

// land puts a unit on the field with the next unit number. Its role was checked before the battle began.
func (s *battleSim) land(h battleHeroInput) {
	role, _ := roleNumber(h.Role)
	s.units = append(s.units, &battleUnit{
		index: len(s.units), role: role, hp: h.Hp, maxHp: h.Hp, attack: h.Attack, defense: h.Defense,
		px: h.X*cellUnits + cellUnits/2, pz: h.Z*cellUnits + cellUnits/2,
		target: -1, blocker: -1, needsPlan: true, isTroop: h.IsTroop,
		rally: !h.IsTroop && h.Special == specialRally, evolution: h.Evolution, power: powerOf(h.Power),
	})
	unit := s.units[len(s.units)-1]
	unit.speed = speedWith(role, 0)
	if !h.IsTroop {
		unit.charge = startCharge(h.StartCharge)
		unit.speed = speedWith(role, h.SpeedPercent)
		unit.siege = int64(maxInt(0, h.SiegePercent))
	}
	if unit.charge > 0 {
		s.log(eventCharge, int64(unit.index), int64(unit.charge), 0, 0)
	}
}

func canDeployAt(buildings []battleBuildingInput, x, z int) bool {
	for _, b := range buildings {
		// A trap must not give itself away, so landing beside one is allowed.
		if b.IsTrap {
			continue
		}
		if x >= b.X-deployClearance && x < b.X+b.Width+deployClearance && z >= b.Z-deployClearance && z < b.Z+b.Depth+deployClearance {
			return false
		}
	}
	return true
}

// deployError is empty when the placement is legal. The client's rules use the same names. With an end tick (a timed
// battle, ADR-050) every DeployTick must be from 0 to before the end tick, rising among heroes and among troops; two
// heroes may share a cell only on different ticks. Without one, every DeployTick is 0. Mirrors BattleSim.DeployError.
func deployError(buildings []battleBuildingInput, heroes []battleHeroInput, endTick int) string {
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
	if endTick < 0 || endTick > battleTickLimit {
		return "BAD_DEPLOY_TICK"
	}
	lastHero, lastTroop := 0, 0
	for i, h := range heroes {
		if h.X < 0 || h.X >= battleGridSize || h.Z < 0 || h.Z >= battleGridSize {
			return "BAD_DEPLOY_CELL"
		}
		if !canDeployAt(buildings, h.X, h.Z) {
			return "DEPLOY_TOO_CLOSE"
		}
		if (endTick == 0 && h.DeployTick != 0) || (endTick > 0 && (h.DeployTick < 0 || h.DeployTick >= endTick)) {
			return "BAD_DEPLOY_TICK"
		}
		last := lastHero
		if h.IsTroop {
			last = lastTroop
		}
		if h.DeployTick < last {
			return "DEPLOY_TICK_ORDER"
		}
		if h.IsTroop {
			lastTroop = h.DeployTick
			continue
		}
		lastHero = h.DeployTick
		for j := 0; j < i; j++ {
			if heroes[j].IsTroop {
				continue
			}
			if heroes[j].ID == h.ID {
				return "DUPLICATE_HERO"
			}
			if heroes[j].X == h.X && heroes[j].Z == h.Z && heroes[j].DeployTick == h.DeployTick {
				return "DEPLOY_CELL_TAKEN"
			}
		}
	}
	return ""
}

// step plays one tick, like BattleSim.Step. False once the battle is over.
func (s *battleSim) step() bool {
	if s.done {
		return false
	}
	s.tick++
	// Cards dropped after the last tick land now, before anyone moves.
	for len(s.pending) > 0 && s.pending[0].DeployTick < s.tick {
		input := s.pending[0]
		s.pending = s.pending[1:]
		s.land(input)
		s.log(eventDeploy, int64(len(s.units)-1), int64(input.X), int64(input.Z), 0)
	}
	// Spells cast after the last tick land now, after the cards, and every spell due applies before anyone moves.
	for len(s.pendingCasts) > 0 && s.pendingCasts[0].Tick < s.tick {
		input := s.pendingCasts[0]
		s.pendingCasts = s.pendingCasts[1:]
		s.landSpell(input)
	}
	for _, spell := range s.spells {
		if spell.pulsesLeft > 0 && spell.nextPulse <= s.tick {
			s.pulse(spell)
		}
	}
	for _, unit := range s.units {
		if unit.hp > 0 {
			s.stepUnit(unit)
		}
	}
	for _, trap := range s.traps {
		if trap.alive {
			s.checkTrap(trap)
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
	limit := battleTickLimit
	if s.timed {
		limit = s.endTick
	}
	if s.finished() || s.tick >= limit {
		s.done = true
	}
	return !s.done
}

// ---------- Spells (ADR-052) ----------

// checkSpell refuses a spell whose numbers could not have come from a valid spells.json, like BattleSim.CheckSpell.
func checkSpell(spell battleSpellInput) error {
	if _, err := effectNumber(spell.Effect); err != nil {
		return err
	}
	if spell.Radius < 0 || spell.Pulses < 1 || spell.PulseTicks < 1 || spell.Power < 0 || spell.Ticks < 0 {
		return errors.New("spell " + spell.ID + " has invalid values")
	}
	return nil
}

// landSpell mirrors BattleSim.LandSpell. The effect was checked before the battle began.
func (s *battleSim) landSpell(input battleSpellInput) {
	effect, _ := effectNumber(input.Effect)
	spell := &battleSpell{
		index:      len(s.spells),
		input:      input,
		effect:     effect,
		cx:         input.X*cellUnits + cellUnits/2,
		cz:         input.Z*cellUnits + cellUnits/2,
		pulsesLeft: input.Pulses,
		nextPulse:  s.tick,
	}
	s.spells = append(s.spells, spell)
	s.log(eventSpellCast, int64(spell.index), int64(input.X), int64(input.Z), int64(spell.effect))
}

// pulse applies a spell once to everything in its ring, like BattleSim.Pulse.
func (s *battleSim) pulse(spell *battleSpell) {
	input := spell.input
	spell.pulsesLeft--
	spell.nextPulse = s.tick + input.PulseTicks
	cast := int64(spell.index)
	s.log(eventSpellPulse, cast, int64(input.Pulses-spell.pulsesLeft), int64(input.Pulses), int64(input.Radius))
	reach := int64(input.Radius) * int64(input.Radius)
	switch spell.effect {
	case effectDamage:
		for i, structure := range s.structures {
			if !structure.alive || squaredDistance(spell.cx, spell.cz, structure.input) > reach {
				continue
			}
			amount := clampHit(input.Power, structure.hp)
			structure.hp -= amount
			if !structure.input.IsWall {
				s.dealtHp += amount
			}
			s.log(eventSpellHit, cast, int64(structure.input.ID), amount, structure.hp)
			if structure.hp == 0 {
				s.collapse(i)
			}
		}
		for _, defender := range s.defenders {
			if defender.hp <= 0 || squaredPoints(defender.px, defender.pz, spell.cx, spell.cz) > reach {
				continue
			}
			amount := input.Power * 100 / (100 + defender.defense)
			if defender.guardTimer > 0 {
				amount /= 2
			}
			amount = clampHit(amount, defender.hp)
			defender.hp -= amount
			s.log(eventSpellHitDefender, cast, int64(defender.index), amount, defender.hp)
			if defender.hp == 0 {
				s.log(eventDefenderDown, int64(defender.index), 0, 0, 0)
			}
		}
	case effectHeal:
		for _, unit := range s.units {
			if unit.hp <= 0 || unit.hp >= unit.maxHp || squaredPoints(unit.px, unit.pz, spell.cx, spell.cz) > reach {
				continue
			}
			amount := input.Power
			if amount > unit.maxHp-unit.hp {
				amount = unit.maxHp - unit.hp
			}
			if amount < 1 {
				continue
			}
			unit.hp += amount
			s.log(eventSpellHeal, cast, int64(unit.index), amount, unit.hp)
		}
	case effectFreeze:
		for _, structure := range s.structures {
			if !structure.alive || !structure.input.IsDefense || squaredDistance(spell.cx, spell.cz, structure.input) > reach {
				continue
			}
			structure.stunTimer = maxInt(structure.stunTimer, input.Ticks)
			s.log(eventSpellFreeze, cast, 0, int64(structure.input.ID), int64(structure.stunTimer))
		}
		for _, defender := range s.defenders {
			if defender.hp <= 0 || squaredPoints(defender.px, defender.pz, spell.cx, spell.cz) > reach {
				continue
			}
			defender.stunTimer = maxInt(defender.stunTimer, input.Ticks)
			s.log(eventSpellFreeze, cast, 1, int64(defender.index), int64(defender.stunTimer))
		}
	case effectBoost:
		for _, unit := range s.units {
			if unit.hp <= 0 || squaredPoints(unit.px, unit.pz, spell.cx, spell.cz) > reach {
				continue
			}
			unit.boostUntil = s.tick + input.Ticks
			unit.boostPercent = input.Power
			s.log(eventSpellBoost, cast, int64(unit.index), int64(input.Ticks), input.Power)
		}
	}
}

// attackOf is a unit's attack this tick: more while a boost spell is on it. Mirrors BattleSim.AttackOf.
func (s *battleSim) attackOf(unit *battleUnit) int64 {
	if s.tick < unit.boostUntil {
		return unit.attack * (100 + unit.boostPercent) / 100
	}
	return unit.attack
}

// finish scores the battle at the tick it stopped, like BattleSim.Finish.
func (s *battleSim) finish() {
	s.done = true
	r := s.result
	r.EndTick = s.tick
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
	// A timed battle goes on with an empty field: more cards may still fall until the attacker ends it.
	if !s.timed {
		anyHero := false
		for _, unit := range s.units {
			if unit.hp > 0 {
				anyHero = true
			}
		}
		if !anyHero {
			return true
		}
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

// ---------- Ability charge ----------

// gainUnit: a landed hit adds one charge, up to full. Troops have no special.
func (s *battleSim) gainUnit(unit *battleUnit) {
	if unit.isTroop || unit.charge >= chargeFull {
		return
	}
	unit.charge++
	s.log(eventCharge, int64(unit.index), int64(unit.charge), 0, 0)
}

func (s *battleSim) spendUnit(unit *battleUnit) {
	unit.charge = 0
	s.log(eventCharge, int64(unit.index), 0, 0, 0)
}

func (s *battleSim) gainDefender(defender *battleDefender) {
	if defender.charge >= chargeFull {
		return
	}
	defender.charge++
	s.log(eventCharge, int64(defender.index), int64(defender.charge), 1, 0)
}

func (s *battleSim) spendDefender(defender *battleDefender) {
	defender.charge = 0
	s.log(eventCharge, int64(defender.index), 0, 1, 0)
}

// ---------- Rally ----------

// powerFor is the special power for a rarity.
func powerFor(rarity string) int {
	switch rarity {
	case "Common":
		return powerCommon
	case "Elite":
		return powerElite
	}
	return powerLegendary
}

// startCharge keeps a starting charge inside the bar.
func startCharge(charge int) int {
	return minInt(maxInt(charge, 0), chargeFull)
}

// speedWith is a role's walking speed with a talent's percent on top, never slower than the role.
func speedWith(role, percent int) int {
	return roleSpeeds[role] * (100 + maxInt(0, percent)) / 100
}

func powerOf(power int) int {
	if power <= 0 {
		return powerLegendary
	}
	return power
}

// boost scales only the extra part of a burst percent: 300 at power 50 is 200.
func boost(percent int64, power int) int64 {
	return 100 + (percent-100)*int64(power)/100
}

// attackInterval is the ticks between attacks for a role, shortened while hasted.
func attackInterval(role, hasteTimer int) int {
	if hasteTimer > 0 {
		return roleIntervals[role] * 100 / (100 + hastePercent)
	}
	return roleIntervals[role]
}

func hasteFor(evolution int) int {
	if evolution >= 2 {
		return hasteTicksEvolved
	}
	return hasteTicks
}

func (s *battleSim) rallyUnits(roarer *battleUnit) {
	s.spendUnit(roarer)
	s.log(eventAbility, int64(roarer.index), int64(roarer.role), 0, 0)
	count := 0
	for _, other := range s.units {
		if other.hp <= 0 || squaredPoints(other.px, other.pz, roarer.px, roarer.pz) > int64(rallyRange)*rallyRange {
			continue
		}
		other.hasteTimer = hasteFor(roarer.evolution)
		count++
	}
	s.log(eventRally, int64(roarer.index), int64(count), 0, 0)
}

func (s *battleSim) rallyDefenders(roarer *battleDefender) {
	s.spendDefender(roarer)
	s.log(eventDefenderAbility, int64(roarer.index), int64(roarer.role), 0, 0)
	count := 0
	for _, other := range s.defenders {
		if other.hp <= 0 || squaredPoints(other.px, other.pz, roarer.px, roarer.pz) > int64(rallyRange)*rallyRange {
			continue
		}
		other.hasteTimer = hasteFor(roarer.evolution)
		count++
	}
	s.log(eventRally, int64(roarer.index), int64(count), 1, 0)
}

// ---------- Heroes ----------

func (s *battleSim) stepUnit(unit *battleUnit) {
	if unit.stunTimer > 0 {
		unit.stunTimer--
		return
	}
	if unit.attackTimer > 0 {
		unit.attackTimer--
	}
	if unit.guardTimer > 0 {
		unit.guardTimer--
	}
	if unit.hasteTimer > 0 {
		unit.hasteTimer--
	}
	if unit.idle > 0 {
		unit.idle--
	}
	s.selfAbility(unit)

	// A defender in reach comes first. The attacker stands and fights, then carries on.
	if foe := s.closestDefender(unit); foe != nil {
		if unit.attackTimer == 0 {
			s.strikeDefender(unit, foe)
			unit.attackTimer = attackInterval(unit.role, unit.hasteTimer)
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
			unit.attackTimer = attackInterval(unit.role, unit.hasteTimer)
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
	speed := unit.speed
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
	if unit.charge >= chargeFull && !unit.isTroop && !unit.rally {
		percent = boost(burstPercent(unit.role), unit.power)
		if percent != 100 {
			s.spendUnit(unit)
			s.log(eventAbility, int64(unit.index), int64(unit.role), 0, 0)
		}
	}
	if percent == 100 {
		s.gainUnit(unit)
	}
	amount := s.attackOf(unit) * percent / 100 * 100 / (100 + defender.defense)
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
	if defender.stunTimer > 0 {
		defender.stunTimer--
		return
	}
	if defender.attackTimer > 0 {
		defender.attackTimer--
	}
	if defender.guardTimer > 0 {
		defender.guardTimer--
	}
	if defender.hasteTimer > 0 {
		defender.hasteTimer--
	}

	target := s.closestAttacker(defender)
	if defender.charge >= chargeFull && defender.rally {
		s.rallyDefenders(defender)
	} else if defender.charge >= chargeFull {
		if defender.role == roleTank && target != nil {
			defender.guardTimer = 40 * defender.power / 100
			s.spendDefender(defender)
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
			defender.attackTimer = attackInterval(defender.role, defender.hasteTimer)
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
		amount := healer.attack * 2 * int64(healer.power) / 100
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
		s.spendDefender(healer)
	}
}

func (s *battleSim) defenderStrike(defender *battleDefender, unit *battleUnit) {
	percent := int64(100)
	if defender.charge >= chargeFull && !defender.rally {
		percent = boost(burstPercent(defender.role), defender.power)
		if percent != 100 {
			s.spendDefender(defender)
			s.log(eventDefenderAbility, int64(defender.index), int64(defender.role), 0, 0)
		}
	}
	if percent == 100 {
		s.gainDefender(defender)
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

// defenderCanEnter: guards are heroes, so they hop walls; only buildings stop them.
func (s *battleSim) defenderCanEnter(x, z int) bool {
	occupant := s.grid[z*battleGridSize+x]
	return occupant == 0 || s.structures[occupant-1].input.DefinitionID == "hero_base" || s.structures[occupant-1].input.IsWall
}

func (s *battleSim) isWallCell(cell int) bool {
	occupant := s.grid[cell]
	return occupant != 0 && s.structures[occupant-1].input.IsWall
}

// walkDefender goes one cell at a time towards a goal, along x and then z. Buildings stop it; walls slow it while it hops.
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
	speed := defender.speed
	if s.isWallCell(defender.tz/cellUnits*battleGridSize + defender.tx/cellUnits) {
		speed = maxInt(1, speed/hopSteps)
	}
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
	if unit.charge < chargeFull || unit.isTroop {
		return
	}
	if unit.rally {
		s.rallyUnits(unit)
		return
	}
	switch unit.role {
	case roleTank:
		unit.guardTimer = 40 * unit.power / 100
		s.spendUnit(unit)
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
			amount := unit.attack * 2 * int64(unit.power) / 100
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
			s.spendUnit(unit)
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
			s.structures[best].stunTimer = 30 * unit.power / 100
			s.spendUnit(unit)
			s.log(eventAbility, int64(unit.index), roleControl, 0, 0)
			s.log(eventStun, int64(s.structures[best].input.ID), 0, 0, 0)
		}
	}
}

func (s *battleSim) strike(unit *battleUnit, focus int) {
	base := s.attackOf(unit) * roleDamagePercents[unit.role] / 100 * (100 + unit.siege) / 100
	percent := int64(100)
	splash := 0
	if unit.charge >= chargeFull && !unit.isTroop && !unit.rally {
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
			percent = boost(percent, unit.power)
			s.spendUnit(unit)
			s.log(eventAbility, int64(unit.index), int64(unit.role), 0, 0)
		}
	}
	if percent == 100 {
		s.gainUnit(unit)
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
	if structure.hp == 0 {
		s.collapse(index)
	}
}

// collapse takes a building with no HP left off the field, like BattleSim.Collapse.
func (s *battleSim) collapse(index int) {
	structure := s.structures[index]
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

	// Walk up to the first wall in the way, and break it.
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
	speed := unit.speed
	s.pathStep = (cellUnits + speed - 1) / speed
	s.pathPerHit = unit.attack * roleDamagePercents[unit.role] / 100 * (100 + unit.siege) / 100
	if s.pathPerHit < 1 {
		s.pathPerHit = 1
	}
	s.pathInterval = roleIntervals[unit.role]
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

// wallBreakTicks is the ticks an attacker spends breaking a wall with this much health, then stepping through.
func (s *battleSim) wallBreakTicks(hp int64) int {
	hits := (hp + s.pathPerHit - 1) / s.pathPerHit
	ticks := hits*int64(s.pathInterval) + int64(s.pathStep)
	if ticks > maxPathCost {
		return maxPathCost
	}
	return int(ticks)
}

func (s *battleSim) relax(from, to, cost int) {
	occupant := s.grid[to]
	step := s.pathStep
	if occupant != 0 {
		structure := s.structures[occupant-1]
		if !structure.input.IsWall {
			return
		}
		step = s.wallBreakTicks(structure.hp)
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

// ---------- Traps ----------

func trapCentre(b battleBuildingInput) (int, int) {
	return b.X*cellUnits + b.Width*cellUnits/2, b.Z*cellUnits + b.Depth*cellUnits/2
}

func (s *battleSim) trapHit(trap *battleStructure, unit *battleUnit) {
	amount := trap.input.Attack * 100 / (100 + unit.defense)
	if amount < 1 {
		amount = 1
	}
	if amount > unit.hp {
		amount = unit.hp
	}
	unit.hp -= amount
	s.log(eventTrapHit, int64(trap.input.ID), int64(unit.index), amount, unit.hp)
	if unit.hp == 0 {
		s.log(eventHeroDown, int64(unit.index), 0, 0, 0)
	}
}

// checkTrap fires a trap once something it catches comes close: troops for a Bomb Trap, a hero for a Hero Trap.
func (s *battleSim) checkTrap(trap *battleStructure) {
	cx, cz := trapCentre(trap.input)
	heroTrap := trap.input.DefinitionID == "hero_trap"
	var caught *battleUnit
	for _, unit := range s.units {
		if unit.hp <= 0 || unit.isTroop == heroTrap {
			continue
		}
		if squaredPoints(unit.px, unit.pz, cx, cz) <= trapTrigger*trapTrigger {
			caught = unit
			break
		}
	}
	if caught == nil {
		return
	}
	trap.alive = false
	if heroTrap {
		s.log(eventTrapFired, int64(trap.input.ID), 1, 0, 0)
		s.trapHit(trap, caught)
		if caught.hp > 0 {
			caught.stunTimer = heroTrapStun
		}
		return
	}
	s.log(eventTrapFired, int64(trap.input.ID), 0, 0, 0)
	reach := int64(trap.input.Range) * cellUnits
	for _, unit := range s.units {
		if unit.hp <= 0 || !unit.isTroop {
			continue
		}
		if squaredPoints(unit.px, unit.pz, cx, cz) <= reach*reach {
			s.trapHit(trap, unit)
		}
	}
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
	// Each tower fires its own way, as in the client's StepTower: fast single arrows, slow heavy cannon balls
	// whose blast hits everyone close by in full, and magic bolts that splash wider for less.
	cannon := input.DefinitionID == "cannon_tower"
	splashing := cannon || input.DefinitionID == "spirit_turret"
	blast, blastPercent := int64(150), int64(40)
	if cannon {
		blast, blastPercent = 125, 100
	}
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

	switch {
	case cannon:
		tower.shotTimer = 25
	case splashing:
		tower.shotTimer = 15
	default:
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
		if dx*dx+dz*dz <= blast*blast {
			s.shoot(tower, unit, blastPercent)
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
	ManaPerStar int64       `json:"manaPerStar"`
	Buildings   []raidEntry `json:"buildings"`
	Walls       []raidWall  `json:"walls"`
	Defenders   []raidDefenderEntry `json:"defenders"`
	Requires    string `json:"requires"`
	XpPerWin    int64  `json:"xpPerWin"`
	FirstClearCrystals int64 `json:"firstClearCrystals"`
	FirstClearItem string `json:"firstClearItem"`
	// Strength scales the HP and attack of every building, wall, tower and guard in this dungeon, in percent
	// (0 or 100 means as listed), like the client's RaidDefinition.Scaled.
	Strength    int    `json:"strength"`
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
			Level: level, Hp: raid.scaled(row.Hp), Attack: raid.scaled(row.Attack), Range: row.Range,
			IsWall: def.isWall(), IsDefense: def.Function == "Defense", IsTrap: def.Function == "Trap",
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

func placementHash(attackID string, parts []deployPlacement, stacks []troopStack, endTick int, casts []spellCast) string {
	joined := attackID + "|" + strconv.Itoa(endTick) + "|"
	for _, stack := range stacks {
		joined += stack.TroopID + ":" + strconv.Itoa(stack.X) + ":" + strconv.Itoa(stack.Z) + ":" + strconv.Itoa(stack.Count) + ":" + strconv.Itoa(stack.Tick) + "|"
	}
	for _, part := range parts {
		joined += part.InstanceID + ":" + strconv.Itoa(part.X) + ":" + strconv.Itoa(part.Z) + ":" + strconv.Itoa(part.Tick) + "|"
	}
	for _, cast := range casts {
		joined += "spell:" + cast.SpellID + ":" + strconv.Itoa(cast.X) + ":" + strconv.Itoa(cast.Z) + ":" + strconv.Itoa(cast.Tick) + "|"
	}
	return joined
}

func maxInt(a, b int) int {
	if a > b {
		return a
	}
	return b
}

// scaled applies the dungeon's strength to a stat; anything above zero stays at least 1.
func (r *raidDefinition) scaled(value int64) int64 {
	if value <= 0 || r.Strength <= 0 || r.Strength == 100 {
		return value
	}
	scaled := value * int64(r.Strength) / 100
	if scaled < 1 {
		return 1
	}
	return scaled
}
