package main

import (
	"context"
	"database/sql"
)

// currentLayoutVersion is the building list a village was last fitted to (ADR-046).
const currentLayoutVersion = 2

// fitLayout brings an older village in line with the current building list, once: buildings that no longer
// exist are removed, and any building that now overlaps another or runs off the map moves to the nearest
// free spot. Nothing else changes: levels, timers and the hero on a base stay as they were.
func (s *Server) fitLayout(ctx context.Context, tx *sql.Tx, userID string) error {
	var version int
	if err := tx.QueryRowContext(ctx, `SELECT layout_version FROM sd_player WHERE user_id = $1`, userID).Scan(&version); err != nil {
		return err
	}
	if version >= currentLayoutVersion {
		return nil
	}
	rows, err := tx.QueryContext(ctx, `SELECT id, definition_id, x, z, rotation FROM sd_building WHERE user_id = $1 ORDER BY id`, userID)
	if err != nil {
		return err
	}
	type placed struct {
		id, x, z, rotation int
		def                string
	}
	all := []placed{}
	for rows.Next() {
		var p placed
		if err := rows.Scan(&p.id, &p.def, &p.x, &p.z, &p.rotation); err != nil {
			rows.Close()
			return err
		}
		all = append(all, p)
	}
	rows.Close()
	if err := rows.Err(); err != nil {
		return err
	}

	var grid [gridSize * gridSize]bool
	fits := func(x, z, w, d int) bool {
		if x < 0 || z < 0 || x+w > gridSize || z+d > gridSize {
			return false
		}
		for i := x; i < x+w; i++ {
			for j := z; j < z+d; j++ {
				if grid[j*gridSize+i] {
					return false
				}
			}
		}
		return true
	}
	take := func(x, z, w, d int) {
		for i := x; i < x+w; i++ {
			for j := z; j < z+d; j++ {
				grid[j*gridSize+i] = true
			}
		}
	}
	// Walls first, so a wall line never breaks: buildings move around them.
	ordered := []placed{}
	for _, p := range all {
		if def, ok := s.buildings.byID[p.def]; ok && def.isWall() {
			ordered = append(ordered, p)
		}
	}
	for _, p := range all {
		if def, ok := s.buildings.byID[p.def]; !ok || !def.isWall() {
			ordered = append(ordered, p)
		}
	}
	for _, p := range ordered {
		def, ok := s.buildings.byID[p.def]
		if !ok {
			// A building that is no longer in the game: its hero, if any, goes back to the roster.
			if _, err := tx.ExecContext(ctx, `UPDATE sd_hero SET base_slot = NULL WHERE user_id = $1 AND base_slot = $2`, userID, p.id); err != nil {
				return err
			}
			if _, err := tx.ExecContext(ctx, `DELETE FROM sd_building WHERE user_id = $1 AND id = $2`, userID, p.id); err != nil {
				return err
			}
			continue
		}
		w, d := def.Width, def.Depth
		if p.rotation == 1 || p.rotation == 3 {
			w, d = d, w
		}
		if fits(p.x, p.z, w, d) {
			take(p.x, p.z, w, d)
			continue
		}
		moved := false
		for radius := 1; radius < gridSize && !moved; radius++ {
			for dz := -radius; dz <= radius && !moved; dz++ {
				for dx := -radius; dx <= radius && !moved; dx++ {
					if abs(dx) != radius && abs(dz) != radius {
						continue
					}
					if fits(p.x+dx, p.z+dz, w, d) {
						take(p.x+dx, p.z+dz, w, d)
						if _, err := tx.ExecContext(ctx, `UPDATE sd_building SET x = $1, z = $2 WHERE user_id = $3 AND id = $4`, p.x+dx, p.z+dz, userID, p.id); err != nil {
							return err
						}
						moved = true
					}
				}
			}
		}
		if !moved {
			// A full map: the building is put away rather than left overlapping.
			if _, err := tx.ExecContext(ctx, `UPDATE sd_hero SET base_slot = NULL WHERE user_id = $1 AND base_slot = $2`, userID, p.id); err != nil {
				return err
			}
			if _, err := tx.ExecContext(ctx, `DELETE FROM sd_building WHERE user_id = $1 AND id = $2`, userID, p.id); err != nil {
				return err
			}
		}
	}
	_, err = tx.ExecContext(ctx, `UPDATE sd_player SET layout_version = $1 WHERE user_id = $2`, currentLayoutVersion, userID)
	return err
}
