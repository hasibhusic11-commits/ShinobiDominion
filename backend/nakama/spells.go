package main

import (
	_ "embed"
	"encoding/json"
	"errors"
)

// Battle spells (ADR-052): the attacker casts each spell its Research Hut has opened once in a live raid, on any
// cell. This file is the server's copy of the rules in Spells.cs; the sim's part is in battle.go.

//go:embed config/spells.json
var spellsJSON []byte

// spellDef is one spell from spells.json. It opens when the Research Hut reaches UnlockHutLevel, which is spell
// level 1; each hut level after that is one spell level more. All values are unapproved test values.
type spellDef struct {
	ID string `json:"id"`
	// Effect is "damage", "heal", "freeze" or "boost".
	Effect         string `json:"effect"`
	UnlockHutLevel int    `json:"unlockHutLevel"`
	// Radius is the reach from the middle of the cast cell, in battle units (cellUnits to a cell).
	Radius int `json:"radius"`
	// DurationTicks is the battle ticks the spell is spread over, or lasts.
	DurationTicks int `json:"durationTicks"`
	// Pulses is how many times it applies over DurationTicks. Freeze and boost apply once.
	Pulses int `json:"pulses"`
	// Power, by spell level: damage and heal, HP per pulse; freeze, percent of DurationTicks the targets stay
	// frozen; boost, percent more damage for DurationTicks.
	Power []int64 `json:"power"`
}

type spellCatalog struct {
	all  []*spellDef
	byID map[string]*spellDef
}

// raidSpell is a spell a raid may cast once, and at what spell level.
type raidSpell struct {
	ID    string `json:"id"`
	Level int    `json:"level"`
}

// spellCast is one cast in a fought battle, as the client sends it next to placements and troop stacks.
type spellCast struct {
	SpellID string `json:"spellId"`
	// X, Z: the cell the spell is centred on, any cell of the grid, buildings included.
	X int `json:"x"`
	Z int `json:"z"`
	// Tick is the battle tick it was cast on; it lands after this tick, like a dropped card.
	Tick int `json:"tick"`
}

// loadSpellCatalog mirrors SpellCatalog.FromJson: the same file passes or fails on both sides.
func loadSpellCatalog(data []byte) (*spellCatalog, error) {
	var document struct {
		SchemaVersion int         `json:"schemaVersion"`
		Spells        []*spellDef `json:"spells"`
	}
	if err := json.Unmarshal(data, &document); err != nil {
		return nil, err
	}
	if document.SchemaVersion != 1 {
		return nil, errors.New("unsupported spell file")
	}
	catalog := &spellCatalog{all: document.Spells, byID: map[string]*spellDef{}}
	for _, spell := range document.Spells {
		if spell == nil || spell.ID == "" || catalog.byID[spell.ID] != nil {
			return nil, errors.New("a spell has no id, or its id is used twice")
		}
		kind, err := effectNumber(spell.Effect)
		if err != nil {
			return nil, err
		}
		once := kind == effectFreeze || kind == effectBoost
		valid := spell.UnlockHutLevel >= 1 && spell.Radius >= 1 && spell.DurationTicks >= 1 && spell.Pulses >= 1 &&
			spell.Pulses <= spell.DurationTicks && (!once || spell.Pulses == 1) && len(spell.Power) >= 1
		for _, power := range spell.Power {
			if power < 1 || (kind == effectFreeze && int64(spell.DurationTicks)*power/100 < 1) {
				valid = false
			}
		}
		if !valid {
			return nil, errors.New("spell " + spell.ID + " has invalid values")
		}
		catalog.byID[spell.ID] = spell
	}
	return catalog, nil
}

// levelAt is the spell's level at a Research Hut level: 0 while locked, then 1 at UnlockHutLevel and up from there.
func (d *spellDef) levelAt(hutLevel int) int {
	if hutLevel < d.UnlockHutLevel {
		return 0
	}
	return minInt(hutLevel-d.UnlockHutLevel+1, len(d.Power))
}

// powerAt is the power at a spell level; a level outside the table counts as the nearest one in it.
func (d *spellDef) powerAt(level int) int64 {
	return d.Power[minInt(maxInt(level, 1), len(d.Power))-1]
}

// pulseTicks is the ticks from one application to the next.
func (d *spellDef) pulseTicks() int {
	return maxInt(1, d.DurationTicks/d.Pulses)
}

// toInput is the cast the sim takes: this spell at a level, on a cell. Mirrors SpellDefinition.ToInput, so the
// client works out the same numbers.
func (d *spellDef) toInput(level, x, z, tick int) battleSpellInput {
	input := battleSpellInput{ID: d.ID, Effect: d.Effect, X: x, Z: z, Radius: d.Radius, Pulses: d.Pulses, PulseTicks: d.pulseTicks(), Tick: tick}
	switch kind, _ := effectNumber(d.Effect); kind {
	case effectFreeze:
		input.Ticks = int(int64(d.DurationTicks) * d.powerAt(level) / 100)
	case effectBoost:
		input.Power = d.powerAt(level)
		input.Ticks = d.DurationTicks
	default:
		input.Power = d.powerAt(level)
	}
	return input
}

// available lists the spells a Research Hut of this level opens, in file order, each at its level. Never nil, so
// the client always gets a list. Mirrors SpellCatalog.Available.
func (c *spellCatalog) available(hutLevel int) []raidSpell {
	list := []raidSpell{}
	for _, spell := range c.all {
		if level := spell.levelAt(hutLevel); level > 0 {
			list = append(list, raidSpell{ID: spell.ID, Level: level})
		}
	}
	return list
}

func grantedSpell(available []raidSpell, id string) (raidSpell, bool) {
	for _, spell := range available {
		if spell.ID == id {
			return spell, true
		}
	}
	return raidSpell{}, false
}

// castError mirrors SpellCatalog.CastError: empty when the casts are legal, otherwise the reason, with the same
// names checked in the same order. Only a timed battle (one with an end tick) may have casts. Each must be a spell
// the raid has, cast once, on a cell of the grid, at a tick from 0 to before the end tick, and the ticks must not go down.
func castError(available []raidSpell, casts []spellCast, endTick int) string {
	if len(casts) == 0 {
		return ""
	}
	if endTick <= 0 || endTick > battleTickLimit {
		return "BAD_DEPLOY_TICK"
	}
	last := 0
	for i, cast := range casts {
		if _, ok := grantedSpell(available, cast.SpellID); !ok {
			return "SPELL_LOCKED"
		}
		for _, earlier := range casts[:i] {
			if earlier.SpellID == cast.SpellID {
				return "SPELL_USED"
			}
		}
		if cast.X < 0 || cast.X >= battleGridSize || cast.Z < 0 || cast.Z >= battleGridSize {
			return "BAD_SPELL_CELL"
		}
		if cast.Tick < 0 || cast.Tick >= endTick {
			return "BAD_DEPLOY_TICK"
		}
		if cast.Tick < last {
			return "DEPLOY_TICK_ORDER"
		}
		last = cast.Tick
	}
	return ""
}

// toInputs turns the casts into what the sim takes, in the order sent, each at the level the raid has the spell
// at. Check castError first. Mirrors SpellCatalog.ToInputs.
func (c *spellCatalog) toInputs(available []raidSpell, casts []spellCast) ([]battleSpellInput, error) {
	inputs := make([]battleSpellInput, 0, len(casts))
	for _, cast := range casts {
		spell := c.byID[cast.SpellID]
		granted, ok := grantedSpell(available, cast.SpellID)
		if spell == nil || !ok {
			return nil, errors.New("unknown spell " + cast.SpellID)
		}
		inputs = append(inputs, spell.toInput(granted.Level, cast.X, cast.Z, cast.Tick))
	}
	return inputs, nil
}
