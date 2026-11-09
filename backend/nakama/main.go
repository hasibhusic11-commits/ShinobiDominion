// Shinobi Dominion authoritative backend.
//
// Every change of value is one Postgres transaction: the player row is locked first, the
// idempotency key is checked, the rules run, and all writes commit together or not at all.
// Nakama's wallet and storage are not used for this state. See ADR-018.
package main

import (
	"context"
	"crypto/rand"
	"crypto/sha256"
	"database/sql"
	"embed"
	"encoding/hex"
	"encoding/json"
	"errors"
	"fmt"
	"io"
	"regexp"
	"sort"
	"strconv"
	"strings"
	"sync"
	"time"

	"github.com/heroiclabs/nakama-common/runtime"
)

//go:embed migrations/*.sql
var migrationFiles embed.FS

const (
	startingCrystals = 3000
	maxBaseSlots     = 1 // one hero at a time leads raids and guards (ADR-030)
	maxPayloadBytes  = 2048
	migrationLockID  = 7265001
)

// gRPC status codes, which Nakama maps to HTTP statuses for clients.
const (
	codeInvalid      = 3
	codeNotFound     = 5
	codeDenied       = 7
	codeExhausted    = 8
	codePrecondition = 9
	codeUnauth       = 16
)

func gameError(message string, code int) error { return runtime.NewError(message, code) }

var (
	errUnauthenticated = gameError("UNAUTHENTICATED", codeUnauth)
	errBadRequest      = gameError("BAD_REQUEST", codeInvalid)
	errBadKey          = gameError("BAD_IDEMPOTENCY_KEY", codeInvalid)
	errKeyReused       = gameError("KEY_REUSED", codePrecondition)
	errRateLimited     = gameError("RATE_LIMITED", codeExhausted)
	errFunds           = gameError("INSUFFICIENT_FUNDS", codePrecondition)
	errPoolVersion     = gameError("POOL_VERSION_MISMATCH", codePrecondition)
	errPoolClosed      = gameError("POOL_NOT_ACTIVE", codePrecondition)
	errHeroNotFound    = gameError("HERO_NOT_FOUND", codeNotFound)
	errDupNotFound     = gameError("DUPLICATE_NOT_FOUND", codeNotFound)
	errNotEvolvable    = gameError("NOT_EVOLVABLE", codePrecondition)
	errMaxEvolution    = gameError("AT_MAX_EVOLUTION", codePrecondition)
	errLevelTooLow     = gameError("LEVEL_TOO_LOW", codePrecondition)
	errSameInstance    = gameError("SAME_INSTANCE", codePrecondition)
	errWrongArchetype  = gameError("WRONG_ARCHETYPE", codePrecondition)
	errDupLocked       = gameError("DUPLICATE_LOCKED", codePrecondition)
	errDupAssigned     = gameError("DUPLICATE_ASSIGNED", codePrecondition)
	errNoCatalyst      = gameError("MISSING_CATALYST", codePrecondition)
	errHeroAssigned    = gameError("HERO_ALREADY_ASSIGNED", codePrecondition)
	errSlotsFull       = gameError("ALL_BASES_FULL", codePrecondition)
	errBadSlot         = gameError("BAD_SLOT", codeInvalid)
)

var (
	keyPattern  = regexp.MustCompile(`^[A-Za-z0-9_-]{8,64}$`)
	uuidPattern = regexp.MustCompile(`^[0-9a-fA-F]{8}-?[0-9a-fA-F]{4}-?[0-9a-fA-F]{4}-?[0-9a-fA-F]{4}-?[0-9a-fA-F]{12}$`)
)

// Server holds immutable config and the rate limiter.
type Server struct {
	catalog *HeroCatalog
	buildings *BuildingCatalog
	raids   *raidCatalog
	troops  *troopCatalog
	modes   *modesConfig
	pool    *SummonPool
	dev     bool
	limiter *rateLimiter
	random  io.Reader
}

func InitModule(ctx context.Context, logger runtime.Logger, db *sql.DB, nk runtime.NakamaModule, initializer runtime.Initializer) error {
	env, _ := ctx.Value(runtime.RUNTIME_CTX_ENV).(map[string]string)

	catalog, err := loadCatalog(heroesJSON)
	if err != nil {
		return fmt.Errorf("hero catalog: %w", err)
	}
	pool, err := loadPool(summonPoolJSON, catalog)
	if err != nil {
		return fmt.Errorf("summon pool: %w", err)
	}
	buildings, err := loadBuildingCatalog(buildingsJSON)
	if err != nil {
		return fmt.Errorf("building catalog: %w", err)
	}
	raids, err := loadRaidCatalog(raidsJSON, buildings)
	if err != nil {
		return fmt.Errorf("raid catalog: %w", err)
	}
	troops, err := loadTroopCatalog(troopsJSON)
	if err != nil {
		return fmt.Errorf("troop catalog: %w", err)
	}
	modes, err := loadModes(modesJSON, troops, buildings)
	if err != nil {
		return fmt.Errorf("modes: %w", err)
	}
	if err := migrate(ctx, db); err != nil {
		return fmt.Errorf("migrations: %w", err)
	}

	server := &Server{
		catalog: catalog,
		buildings: buildings,
		raids:   raids,
		troops:  troops,
		modes:   modes,
		pool:    pool,
		dev:     devModeEnabled(env),
		limiter: newRateLimiter(envFloat(env, "SD_RATE_PER_SEC", 5), envFloat(env, "SD_RATE_BURST", 20)),
		random:  rand.Reader,
	}
	return server.register(initializer, logger)
}

// devModeEnabled is true only for the exact string "true", so anything unexpected stays safe.
func devModeEnabled(env map[string]string) bool { return env["SD_DEV_MODE"] == "true" }

func envFloat(env map[string]string, name string, fallback float64) float64 {
	value, err := strconv.ParseFloat(env[name], 64)
	if err != nil || value <= 0 {
		return fallback
	}
	return value
}

func (s *Server) register(initializer runtime.Initializer, logger runtime.Logger) error {
	rpcs := map[string]func(context.Context, runtime.Logger, *sql.DB, runtime.NakamaModule, string) (string, error){
		"sd_get_state":    s.rpcGetState,
		"sd_summon_odds":  s.rpcOdds,
		"sd_summon":       s.rpcSummon,
		"sd_evolve":       s.rpcEvolve,
		"sd_set_lock":     s.rpcSetLock,
		"sd_assign_hero":  s.rpcAssign,
		"sd_village_place": s.rpcPlace,
		"sd_village_walls": s.rpcWalls,
		"sd_village_move": s.rpcMove,
		"sd_village_remove": s.rpcRemove,
		"sd_village_upgrade": s.rpcUpgrade,
		"sd_village_collect": s.rpcCollect,
		"sd_village_finish": s.rpcFinish,
		"sd_research_start": s.rpcResearchStart,
		"sd_research_finish": s.rpcResearchFinish,
		"sd_troop_train":  s.rpcTroopTrain,
		"sd_hero_train":   s.rpcHeroTrain,
		"sd_raid_list":    s.rpcRaidList,
		"sd_raid_start":   s.rpcRaidStart,
		"sd_raid_deploy":  s.rpcRaidDeploy,
		"sd_rival_start":  s.rpcRivalStart,
		"sd_defense_log":  s.rpcDefenseLog,
		"sd_siege_list":   s.rpcSiegeList,
		"sd_siege_fight":  s.rpcSiegeFight,
		"sd_arena_fight":  s.rpcArenaFight,
		"sd_arena_board":  s.rpcArenaBoard,
		"sd_quest_board":  s.rpcQuestBoard,
		"sd_quest_claim":  s.rpcQuestClaim,
		"sd_gift_open":    s.rpcGiftOpen,
	}
	if s.dev {
		logger.Warn("DEVELOPMENT MODE: dev grant and audit calls are enabled. Never use this in production.")
		rpcs["sd_dev_grant"] = s.rpcDevGrant
		rpcs["sd_dev_audit"] = s.rpcDevAudit
	}
	for id, handler := range rpcs {
		if err := initializer.RegisterRpc(id, handler); err != nil {
			return err
		}
	}
	return nil
}

// migrate applies embedded SQL once each, under an advisory lock so two nodes cannot race.
func migrate(ctx context.Context, db *sql.DB) error {
	conn, err := db.Conn(ctx)
	if err != nil {
		return err
	}
	defer conn.Close()
	if _, err := conn.ExecContext(ctx, `SELECT pg_advisory_lock($1)`, migrationLockID); err != nil {
		return err
	}
	defer conn.ExecContext(context.Background(), `SELECT pg_advisory_unlock($1)`, migrationLockID)

	if _, err := conn.ExecContext(ctx, `CREATE TABLE IF NOT EXISTS sd_migration (name TEXT PRIMARY KEY, applied_at TIMESTAMPTZ NOT NULL DEFAULT now())`); err != nil {
		return err
	}
	entries, err := migrationFiles.ReadDir("migrations")
	if err != nil {
		return err
	}
	names := make([]string, 0, len(entries))
	for _, entry := range entries {
		names = append(names, entry.Name())
	}
	sort.Strings(names)
	for _, name := range names {
		var exists bool
		if err := conn.QueryRowContext(ctx, `SELECT EXISTS (SELECT 1 FROM sd_migration WHERE name = $1)`, name).Scan(&exists); err != nil {
			return err
		}
		if exists {
			continue
		}
		body, err := migrationFiles.ReadFile("migrations/" + name)
		if err != nil {
			return err
		}
		tx, err := conn.BeginTx(ctx, nil)
		if err != nil {
			return err
		}
		if _, err := tx.ExecContext(ctx, string(body)); err != nil {
			tx.Rollback()
			return fmt.Errorf("%s: %w", name, err)
		}
		if _, err := tx.ExecContext(ctx, `INSERT INTO sd_migration (name) VALUES ($1)`, name); err != nil {
			tx.Rollback()
			return err
		}
		if err := tx.Commit(); err != nil {
			return err
		}
	}
	return nil
}

// ---------- Request plumbing ----------

func userFrom(ctx context.Context) (string, error) {
	userID, ok := ctx.Value(runtime.RUNTIME_CTX_USER_ID).(string)
	if !ok || userID == "" {
		return "", errUnauthenticated
	}
	return userID, nil
}

// decodeStrict rejects unknown fields, so a client that sends "results" or a "seed" is refused
// rather than quietly ignored.
func decodeStrict(payload string, target interface{}) error {
	if len(payload) > maxPayloadBytes {
		return errBadRequest
	}
	decoder := json.NewDecoder(strings.NewReader(payload))
	decoder.DisallowUnknownFields()
	if err := decoder.Decode(target); err != nil {
		return errBadRequest
	}
	if decoder.More() {
		return errBadRequest
	}
	return nil
}

func hashRequest(parts ...string) string {
	sum := sha256.Sum256([]byte(strings.Join(parts, "\x00")))
	return hex.EncodeToString(sum[:])
}

func withTx(ctx context.Context, db *sql.DB, work func(*sql.Tx) error) error {
	tx, err := db.BeginTx(ctx, nil)
	if err != nil {
		return err
	}
	defer tx.Rollback()
	if err := work(tx); err != nil {
		return err
	}
	return tx.Commit()
}

// lockPlayer creates the player if new and locks their row. Every mutation starts here, so one
// player's value-changing requests run strictly one at a time.
func (s *Server) lockPlayer(ctx context.Context, tx *sql.Tx, userID string) (int64, error) {
	result, err := tx.ExecContext(ctx, `INSERT INTO sd_player (user_id, crystals) VALUES ($1, $2) ON CONFLICT (user_id) DO NOTHING`, userID, startingCrystals)
	if err != nil {
		return 0, err
	}
	if inserted, _ := result.RowsAffected(); inserted == 1 {
		if err := addLedger(ctx, tx, userID, startingCrystals, startingCrystals, "starting_grant", "account"); err != nil {
			return 0, err
		}
		if s.dev {
			if err := s.seedDevInventory(ctx, tx, userID); err != nil {
				return 0, err
			}
		}
	}
	var crystals int64
	var ready bool
	err = tx.QueryRowContext(ctx, `SELECT crystals, village_ready FROM sd_player WHERE user_id = $1 FOR UPDATE`, userID).Scan(&crystals, &ready)
	if err != nil {
		return 0, err
	}
	if !ready {
		if err := s.createStarterVillage(ctx, tx, userID); err != nil {
			return 0, err
		}
	}
	if err := settleUpgrades(ctx, tx, userID); err != nil {
		return 0, err
	}
	if err := settleResearchSQL(ctx, tx, userID); err != nil {
		return 0, err
	}
	return crystals, nil
}

// settleUpgrades finishes every upgrade whose time has passed, on the server's clock (ADR-040). It runs under
// the player lock before anything else, so every call sees finished buildings at their new level.
func settleUpgrades(ctx context.Context, tx *sql.Tx, userID string) error {
	_, err := tx.ExecContext(ctx, `UPDATE sd_building SET level = level + 1, upgrade_done_ms = 0 WHERE user_id = $1 AND upgrade_done_ms > 0 AND upgrade_done_ms <= $2`, userID, nowMillis())
	return err
}

func addLedger(ctx context.Context, tx *sql.Tx, userID string, delta, balance int64, reason, ref string) error {
	_, err := tx.ExecContext(ctx, `INSERT INTO sd_currency_ledger (user_id, delta, balance_after, reason, ref) VALUES ($1, $2, $3, $4, $5)`, userID, delta, balance, reason, ref)
	return err
}

func addItemLedger(ctx context.Context, tx *sql.Tx, userID, itemID string, delta int64, reason, ref string) error {
	_, err := tx.ExecContext(ctx, `INSERT INTO sd_item_ledger (user_id, item_id, delta, reason, ref) VALUES ($1, $2, $3, $4, $5)`, userID, itemID, delta, reason, ref)
	return err
}

// replayed returns a stored response if this key has already completed.
func replayed(ctx context.Context, tx *sql.Tx, userID, key, kind, requestHash string) (string, bool, error) {
	var storedKind, storedHash, response string
	err := tx.QueryRowContext(ctx, `SELECT kind, request_hash, response FROM sd_idempotency WHERE user_id = $1 AND key = $2`, userID, key).Scan(&storedKind, &storedHash, &response)
	if errors.Is(err, sql.ErrNoRows) {
		return "", false, nil
	}
	if err != nil {
		return "", false, err
	}
	if storedKind != kind || storedHash != requestHash {
		return "", false, errKeyReused
	}
	return response, true, nil
}

func storeResponse(ctx context.Context, tx *sql.Tx, userID, key, kind, requestHash, response string) error {
	_, err := tx.ExecContext(ctx, `INSERT INTO sd_idempotency (user_id, key, kind, request_hash, response) VALUES ($1, $2, $3, $4, $5)`, userID, key, kind, requestHash, response)
	return err
}

// ---------- State ----------

type heroState struct {
	InstanceID     string `json:"instanceId"`
	ArchetypeID    string `json:"archetypeId"`
	EvolutionIndex int    `json:"evolutionIndex"`
	Level          int    `json:"level"`
	Experience     int64  `json:"experience"`
	Locked         bool   `json:"locked"`
	BaseSlot       *int   `json:"baseSlot"`
}

type playerState struct {
	SchemaVersion int              `json:"schemaVersion"`
	Crystals      int64            `json:"crystals"`
	Heroes        []heroState      `json:"heroes"`
	Items         map[string]int64 `json:"items"`
	Village       *villageState    `json:"village"`
	Troops        map[string]int   `json:"troops"`
	Research      map[string]researchView `json:"research"`
}

func loadState(ctx context.Context, tx *sql.Tx, userID string, crystals int64) (*playerState, error) {
	state := &playerState{SchemaVersion: 1, Crystals: crystals, Heroes: []heroState{}, Items: map[string]int64{}}
	rows, err := tx.QueryContext(ctx, `SELECT instance_id::text, archetype_id, evolution, level, experience, locked, base_slot FROM sd_hero WHERE user_id = $1 ORDER BY created_at, instance_id`, userID)
	if err != nil {
		return nil, err
	}
	defer rows.Close()
	for rows.Next() {
		var hero heroState
		var slot sql.NullInt64
		if err := rows.Scan(&hero.InstanceID, &hero.ArchetypeID, &hero.EvolutionIndex, &hero.Level, &hero.Experience, &hero.Locked, &slot); err != nil {
			return nil, err
		}
		if slot.Valid {
			value := int(slot.Int64)
			hero.BaseSlot = &value
		}
		state.Heroes = append(state.Heroes, hero)
	}
	if err := rows.Err(); err != nil {
		return nil, err
	}
	itemRows, err := tx.QueryContext(ctx, `SELECT item_id, count FROM sd_item WHERE user_id = $1`, userID)
	if err != nil {
		return nil, err
	}
	defer itemRows.Close()
	for itemRows.Next() {
		var id string
		var count int64
		if err := itemRows.Scan(&id, &count); err != nil {
			return nil, err
		}
		state.Items[id] = count
	}
	if err := itemRows.Err(); err != nil {
		return nil, err
	}
	itemRows.Close()
	rows.Close()
	state.Village, err = loadVillageState(ctx, tx, userID, crystals)
	if err != nil {
		return state, err
	}
	state.Troops, err = loadTroopCounts(ctx, tx, userID)
	if err != nil {
		return state, err
	}
	research, err := loadResearch(ctx, tx, userID)
	state.Research = researchViews(research)
	return state, err
}

func (s *Server) rpcGetState(ctx context.Context, logger runtime.Logger, db *sql.DB, nk runtime.NakamaModule, payload string) (string, error) {
	userID, err := userFrom(ctx)
	if err != nil {
		return "", err
	}
	var output string
	err = withTx(ctx, db, func(tx *sql.Tx) error {
		crystals, err := s.lockPlayer(ctx, tx, userID)
		if err != nil {
			return err
		}
		state, err := loadState(ctx, tx, userID, crystals)
		if err != nil {
			return err
		}
		output, err = marshal(state)
		return err
	})
	return output, err
}

func marshal(value interface{}) (string, error) {
	data, err := json.Marshal(value)
	return string(data), err
}

// ---------- Summoning: no pity, no guarantees, no player state in the roll ----------

type oddsItem struct {
	ArchetypeID string `json:"archetypeId"`
	DisplayName string `json:"displayName"`
	Rarity      string `json:"rarity"`
	Weight      int64  `json:"weight"`
	Chance      string `json:"chance"`
}

type oddsResponse struct {
	PoolVersion  int               `json:"poolVersion"`
	Status       string            `json:"status"`
	BannerName   string            `json:"bannerName"`
	Price        int64             `json:"priceCrystalsPerRoll"`
	TotalWeight  int64             `json:"totalWeight"`
	Items        []oddsItem        `json:"items"`
	RarityChance map[string]string `json:"rarityChance"`
}

func (s *Server) rpcOdds(ctx context.Context, logger runtime.Logger, db *sql.DB, nk runtime.NakamaModule, payload string) (string, error) {
	if _, err := userFrom(ctx); err != nil {
		return "", err
	}
	response := oddsResponse{
		PoolVersion:  s.pool.Version,
		Status:       s.pool.Status,
		BannerName:   s.pool.BannerName,
		Price:        s.pool.PricePerRol,
		TotalWeight:  s.pool.total,
		RarityChance: map[string]string{},
	}
	rarityWeights := map[string]int64{}
	for _, item := range s.pool.Items {
		hero := s.catalog.byID[item.ArchetypeID]
		response.Items = append(response.Items, oddsItem{
			ArchetypeID: item.ArchetypeID,
			DisplayName: hero.DisplayName,
			Rarity:      hero.Rarity,
			Weight:      item.Weight,
			Chance:      percentString(item.Weight, s.pool.total),
		})
		rarityWeights[hero.Rarity] += item.Weight
	}
	for rarity, weight := range rarityWeights {
		response.RarityChance[rarity] = percentString(weight, s.pool.total)
	}
	return marshal(response)
}

type summonRequest struct {
	PoolVersion int    `json:"poolVersion"`
	Count       int    `json:"count"`
	Key         string `json:"key"`
}

type summonRoll struct {
	RollIndex   int    `json:"rollIndex"`
	InstanceID  string `json:"instanceId"`
	ArchetypeID string `json:"archetypeId"`
	Rarity      string `json:"rarity"`
	CopiesOwned int    `json:"copiesOwned"`
}

type summonResponse struct {
	PoolVersion int          `json:"poolVersion"`
	Rolls       []summonRoll `json:"rolls"`
	State       *playerState `json:"state"`
}

func (s *Server) rpcSummon(ctx context.Context, logger runtime.Logger, db *sql.DB, nk runtime.NakamaModule, payload string) (string, error) {
	userID, err := userFrom(ctx)
	if err != nil {
		return "", err
	}
	var request summonRequest
	if err := decodeStrict(payload, &request); err != nil {
		return "", err
	}
	if request.Count != 1 && request.Count != 10 {
		return "", errBadRequest
	}
	if !keyPattern.MatchString(request.Key) {
		return "", errBadKey
	}
	if !s.limiter.Allow(userID, time.Now()) {
		return "", errRateLimited
	}
	if request.PoolVersion != s.pool.Version {
		return "", errPoolVersion
	}
	if !s.pool.Active(time.Now()) {
		return "", errPoolClosed
	}

	requestHash := hashRequest(strconv.Itoa(request.PoolVersion), strconv.Itoa(request.Count))
	var output string
	err = withTx(ctx, db, func(tx *sql.Tx) error {
		crystals, err := s.lockPlayer(ctx, tx, userID)
		if err != nil {
			return err
		}
		if stored, found, err := replayed(ctx, tx, userID, request.Key, "summon", requestHash); err != nil {
			return err
		} else if found {
			output = stored
			return nil
		}

		cost := s.pool.PricePerRol * int64(request.Count)
		if crystals < cost {
			return errFunds
		}
		balance := crystals - cost
		if _, err := tx.ExecContext(ctx, `UPDATE sd_player SET crystals = $1 WHERE user_id = $2`, balance, userID); err != nil {
			return err
		}
		if err := addLedger(ctx, tx, userID, -cost, balance, "summon", request.Key); err != nil {
			return err
		}

		// A ten-pull is this loop run ten times. Nothing here reads previous results.
		response := summonResponse{PoolVersion: s.pool.Version, Rolls: []summonRoll{}}
		for i := 0; i < request.Count; i++ {
			archetypeID, err := s.pool.Roll(s.random)
			if err != nil {
				return err
			}
			var instanceID string
			if err := tx.QueryRowContext(ctx, `INSERT INTO sd_hero (user_id, archetype_id) VALUES ($1, $2) RETURNING instance_id::text`, userID, archetypeID).Scan(&instanceID); err != nil {
				return err
			}
			if _, err := tx.ExecContext(ctx, `INSERT INTO sd_summon_roll (user_id, request_key, roll_index, pool_version, archetype_id, instance_id) VALUES ($1, $2, $3, $4, $5, $6)`, userID, request.Key, i, s.pool.Version, archetypeID, instanceID); err != nil {
				return err
			}
			var copies int
			if err := tx.QueryRowContext(ctx, `SELECT count(*) FROM sd_hero WHERE user_id = $1 AND archetype_id = $2`, userID, archetypeID).Scan(&copies); err != nil {
				return err
			}
			response.Rolls = append(response.Rolls, summonRoll{
				RollIndex:   i,
				InstanceID:  instanceID,
				ArchetypeID: archetypeID,
				Rarity:      s.catalog.byID[archetypeID].Rarity,
				CopiesOwned: copies,
			})
		}

		if err := s.bumpQuest(ctx, tx, userID, "summon", request.Count); err != nil {
			return err
		}
		response.State, err = loadState(ctx, tx, userID, balance)
		if err != nil {
			return err
		}
		output, err = marshal(response)
		if err != nil {
			return err
		}
		return storeResponse(ctx, tx, userID, request.Key, "summon", requestHash, output)
	})
	if err != nil {
		return "", err
	}
	return output, nil
}

// ---------- Evolution: exactly one duplicate, exactly one catalyst, all or nothing ----------

type evolveRequest struct {
	Key         string `json:"key"`
	MainID      string `json:"mainInstanceId"`
	DuplicateID string `json:"duplicateInstanceId"`
}

type lockedHero struct {
	archetypeID string
	evolution   int
	level       int
	locked      bool
	slot        sql.NullInt64
}

func loadHero(ctx context.Context, tx *sql.Tx, userID, instanceID string) (*lockedHero, error) {
	var hero lockedHero
	// Scoped to user_id: another player's hero is simply not found, so nothing about it leaks.
	err := tx.QueryRowContext(ctx, `SELECT archetype_id, evolution, level, locked, base_slot FROM sd_hero WHERE instance_id = $1 AND user_id = $2 FOR UPDATE`, instanceID, userID).
		Scan(&hero.archetypeID, &hero.evolution, &hero.level, &hero.locked, &hero.slot)
	if errors.Is(err, sql.ErrNoRows) {
		return nil, nil
	}
	if err != nil {
		return nil, err
	}
	return &hero, nil
}

func (s *Server) rpcEvolve(ctx context.Context, logger runtime.Logger, db *sql.DB, nk runtime.NakamaModule, payload string) (string, error) {
	userID, err := userFrom(ctx)
	if err != nil {
		return "", err
	}
	var request evolveRequest
	if err := decodeStrict(payload, &request); err != nil {
		return "", err
	}
	if !keyPattern.MatchString(request.Key) {
		return "", errBadKey
	}
	if !uuidPattern.MatchString(request.MainID) || !uuidPattern.MatchString(request.DuplicateID) {
		return "", errBadRequest
	}
	if !s.limiter.Allow(userID, time.Now()) {
		return "", errRateLimited
	}

	requestHash := hashRequest(request.MainID, request.DuplicateID)
	var output string
	err = withTx(ctx, db, func(tx *sql.Tx) error {
		crystals, err := s.lockPlayer(ctx, tx, userID)
		if err != nil {
			return err
		}
		if stored, found, err := replayed(ctx, tx, userID, request.Key, "evolve", requestHash); err != nil {
			return err
		} else if found {
			output = stored
			return nil
		}

		main, err := loadHero(ctx, tx, userID, request.MainID)
		if err != nil {
			return err
		}
		if main == nil {
			return errHeroNotFound
		}
		if s.catalog.byID[main.archetypeID].Rarity != "Legendary" {
			return errNotEvolvable
		}
		if main.evolution >= 3 {
			return errMaxEvolution
		}
		if main.level != 200 {
			return errLevelTooLow
		}
		if strings.EqualFold(request.MainID, request.DuplicateID) {
			return errSameInstance
		}
		duplicate, err := loadHero(ctx, tx, userID, request.DuplicateID)
		if err != nil {
			return err
		}
		if duplicate == nil {
			return errDupNotFound
		}
		if duplicate.archetypeID != main.archetypeID {
			return errWrongArchetype
		}
		if duplicate.locked {
			return errDupLocked
		}
		if duplicate.slot.Valid {
			return errDupAssigned
		}

		catalyst := s.catalog.Catalysts[main.evolution]
		spent, err := tx.ExecContext(ctx, `UPDATE sd_item SET count = count - 1 WHERE user_id = $1 AND item_id = $2 AND count >= 1`, userID, catalyst)
		if err != nil {
			return err
		}
		if rows, _ := spent.RowsAffected(); rows != 1 {
			return errNoCatalyst
		}
		if err := addItemLedger(ctx, tx, userID, catalyst, -1, "evolve", request.Key); err != nil {
			return err
		}
		if _, err := tx.ExecContext(ctx, `DELETE FROM sd_hero WHERE instance_id = $1 AND user_id = $2`, request.DuplicateID, userID); err != nil {
			return err
		}
		if _, err := tx.ExecContext(ctx, `UPDATE sd_hero SET evolution = evolution + 1, level = 1, experience = 0 WHERE instance_id = $1 AND user_id = $2`, request.MainID, userID); err != nil {
			return err
		}
		if _, err := tx.ExecContext(ctx, `INSERT INTO sd_evolution_log (user_id, request_key, main_instance_id, consumed_instance, consumed_archetype, catalyst_id, from_evolution, to_evolution) VALUES ($1, $2, $3, $4, $5, $6, $7, $8)`,
			userID, request.Key, request.MainID, request.DuplicateID, duplicate.archetypeID, catalyst, main.evolution, main.evolution+1); err != nil {
			return err
		}

		state, err := loadState(ctx, tx, userID, crystals)
		if err != nil {
			return err
		}
		output, err = marshal(state)
		if err != nil {
			return err
		}
		return storeResponse(ctx, tx, userID, request.Key, "evolve", requestHash, output)
	})
	if err != nil {
		return "", err
	}
	return output, nil
}

// ---------- Lock and base assignment ----------

type lockRequest struct {
	InstanceID string `json:"instanceId"`
	Locked     bool   `json:"locked"`
}

func (s *Server) rpcSetLock(ctx context.Context, logger runtime.Logger, db *sql.DB, nk runtime.NakamaModule, payload string) (string, error) {
	userID, err := userFrom(ctx)
	if err != nil {
		return "", err
	}
	var request lockRequest
	if err := decodeStrict(payload, &request); err != nil {
		return "", err
	}
	if !uuidPattern.MatchString(request.InstanceID) {
		return "", errBadRequest
	}
	if !s.limiter.Allow(userID, time.Now()) {
		return "", errRateLimited
	}
	var output string
	err = withTx(ctx, db, func(tx *sql.Tx) error {
		crystals, err := s.lockPlayer(ctx, tx, userID)
		if err != nil {
			return err
		}
		result, err := tx.ExecContext(ctx, `UPDATE sd_hero SET locked = $1 WHERE instance_id = $2 AND user_id = $3`, request.Locked, request.InstanceID, userID)
		if err != nil {
			return err
		}
		if rows, _ := result.RowsAffected(); rows != 1 {
			return errHeroNotFound
		}
		state, err := loadState(ctx, tx, userID, crystals)
		if err != nil {
			return err
		}
		output, err = marshal(state)
		return err
	})
	if err != nil {
		return "", err
	}
	return output, nil
}

type assignRequest struct {
	Slot       int     `json:"slot"`
	InstanceID *string `json:"instanceId"`
}

// rpcAssign puts a hero in a base slot, or clears the slot when instanceId is null. It enforces
// ownership, one hero per base, one base per hero and six bases, with any rarity mix allowed.
func (s *Server) rpcAssign(ctx context.Context, logger runtime.Logger, db *sql.DB, nk runtime.NakamaModule, payload string) (string, error) {
	userID, err := userFrom(ctx)
	if err != nil {
		return "", err
	}
	var request assignRequest
	if err := decodeStrict(payload, &request); err != nil {
		return "", err
	}
	if request.Slot <= 0 {
		return "", errBadSlot
	}
	if request.InstanceID != nil && !uuidPattern.MatchString(*request.InstanceID) {
		return "", errBadRequest
	}
	if !s.limiter.Allow(userID, time.Now()) {
		return "", errRateLimited
	}
	var output string
	err = withTx(ctx, db, func(tx *sql.Tx) error {
		crystals, err := s.lockPlayer(ctx, tx, userID)
		if err != nil {
			return err
		}
		if request.InstanceID != nil {
			var definitionID string
			err := tx.QueryRowContext(ctx, `SELECT definition_id FROM sd_building WHERE user_id = $1 AND id = $2`, userID, request.Slot).Scan(&definitionID)
			if errors.Is(err, sql.ErrNoRows) {
				return errBadSlot
			}
			if err != nil {
				return err
			}
			if def := s.buildings.byID[definitionID]; def == nil || def.Function != "HeroBase" {
				return errBadSlot
			}
			hero, err := loadHero(ctx, tx, userID, *request.InstanceID)
			if err != nil {
				return err
			}
			if hero == nil {
				return errHeroNotFound
			}
			if hero.slot.Valid && int(hero.slot.Int64) != request.Slot {
				return errHeroAssigned
			}
		}
		if _, err := tx.ExecContext(ctx, `UPDATE sd_hero SET base_slot = NULL WHERE user_id = $1 AND base_slot = $2`, userID, request.Slot); err != nil {
			return err
		}
		if request.InstanceID != nil {
			var used int
			if err := tx.QueryRowContext(ctx, `SELECT count(*) FROM sd_hero WHERE user_id = $1 AND base_slot IS NOT NULL`, userID).Scan(&used); err != nil {
				return err
			}
			if used >= maxBaseSlots {
				return errSlotsFull
			}
			if _, err := tx.ExecContext(ctx, `UPDATE sd_hero SET base_slot = $1 WHERE instance_id = $2 AND user_id = $3`, request.Slot, *request.InstanceID, userID); err != nil {
				return err
			}
		}
		state, err := loadState(ctx, tx, userID, crystals)
		if err != nil {
			return err
		}
		output, err = marshal(state)
		return err
	})
	if err != nil {
		return "", err
	}
	return output, nil
}

// ---------- Development only. Not registered unless SD_DEV_MODE is exactly "true". ----------

func (s *Server) seedDevInventory(ctx context.Context, tx *sql.Tx, userID string) error {
	for _, hero := range s.catalog.Heroes {
		if !hero.ContentComplete {
			continue
		}
		if _, err := tx.ExecContext(ctx, `INSERT INTO sd_hero (user_id, archetype_id) VALUES ($1, $2)`, userID, hero.ID); err != nil {
			return err
		}
	}
	if _, err := tx.ExecContext(ctx, `INSERT INTO sd_hero (user_id, archetype_id, level) VALUES ($1, 'solar_beast_sage', 200), ($1, 'thunder_empress', 1), ($1, 'stone_titan', 1)`, userID); err != nil {
		return err
	}
	for _, catalyst := range s.catalog.Catalysts {
		if _, err := tx.ExecContext(ctx, `INSERT INTO sd_item (user_id, item_id, count) VALUES ($1, $2, 1)`, userID, catalyst); err != nil {
			return err
		}
		if err := addItemLedger(ctx, tx, userID, catalyst, 1, "dev_seed", "account"); err != nil {
			return err
		}
	}
	return nil
}

type devGrantRequest struct {
	Crystals    int64            `json:"crystals"`
	SetCrystals *int64           `json:"setCrystals"`
	Items       map[string]int64 `json:"items"`
	ArchetypeID string           `json:"archetypeId"`
	Evolution   int              `json:"evolution"`
	Level       int              `json:"level"`
	Copies      int              `json:"copies"`
	ClearHeroes bool             `json:"clearHeroes"`
	ClearItems  bool             `json:"clearItems"`
	MaxLevelFor string           `json:"maxLevelFor"`
	SetGold     *int64           `json:"setGold"`
	SetMana     *int64           `json:"setMana"`
	SetDojoLevel *int `json:"setDojoLevel"`
	ResetVillage bool            `json:"resetVillage"`
	BackdateCollectHours int64   `json:"backdateCollectHours"`
}

func (s *Server) rpcDevGrant(ctx context.Context, logger runtime.Logger, db *sql.DB, nk runtime.NakamaModule, payload string) (string, error) {
	userID, err := userFrom(ctx)
	if err != nil {
		return "", err
	}
	var request devGrantRequest
	if err := decodeStrict(payload, &request); err != nil {
		return "", err
	}
	if request.Crystals < 0 || request.Copies < 0 {
		return "", errBadRequest
	}
	var output string
	err = withTx(ctx, db, func(tx *sql.Tx) error {
		crystals, err := s.lockPlayer(ctx, tx, userID)
		if err != nil {
			return err
		}
		if request.ClearHeroes {
			if _, err := tx.ExecContext(ctx, `DELETE FROM sd_hero WHERE user_id = $1`, userID); err != nil {
				return err
			}
		}
		if request.ClearItems {
			if _, err := tx.ExecContext(ctx, `DELETE FROM sd_item WHERE user_id = $1`, userID); err != nil {
				return err
			}
		}
		target := crystals + request.Crystals
		if request.SetCrystals != nil {
			target = *request.SetCrystals
		}
		if target < 0 {
			return errBadRequest
		}
		if target != crystals {
			if _, err := tx.ExecContext(ctx, `UPDATE sd_player SET crystals = $1 WHERE user_id = $2`, target, userID); err != nil {
				return err
			}
			if err := addLedger(ctx, tx, userID, target-crystals, target, "dev_grant", "dev"); err != nil {
				return err
			}
		}
		for item, amount := range request.Items {
			if amount <= 0 {
				return errBadRequest
			}
			if _, err := tx.ExecContext(ctx, `INSERT INTO sd_item (user_id, item_id, count) VALUES ($1, $2, $3) ON CONFLICT (user_id, item_id) DO UPDATE SET count = sd_item.count + EXCLUDED.count`, userID, item, amount); err != nil {
				return err
			}
			if err := addItemLedger(ctx, tx, userID, item, amount, "dev_grant", "dev"); err != nil {
				return err
			}
		}
		if request.ArchetypeID != "" {
			if _, ok := s.catalog.byID[request.ArchetypeID]; !ok {
				return errBadRequest
			}
			level := request.Level
			if level == 0 {
				level = 1
			}
			copies := request.Copies
			if copies == 0 {
				copies = 1
			}
			for i := 0; i < copies; i++ {
				if _, err := tx.ExecContext(ctx, `INSERT INTO sd_hero (user_id, archetype_id, evolution, level) VALUES ($1, $2, $3, $4)`, userID, request.ArchetypeID, request.Evolution, level); err != nil {
					return errBadRequest
				}
			}
		}
		if request.ResetVillage {
			if err := s.createStarterVillage(ctx, tx, userID); err != nil {
				return err
			}
		}
		if request.SetDojoLevel != nil {
			if *request.SetDojoLevel < 1 || *request.SetDojoLevel > levelsPerBuilding {
				return errBadRequest
			}
			if _, err := tx.ExecContext(ctx, `UPDATE sd_building SET level = $1 WHERE user_id = $2 AND definition_id = 'dojo_hq'`, *request.SetDojoLevel, userID); err != nil {
				return err
			}
		}
		if request.SetGold != nil {
			if *request.SetGold < 0 {
				return errBadRequest
			}
			var before int64
			if err := tx.QueryRowContext(ctx, `SELECT gold FROM sd_player WHERE user_id = $1`, userID).Scan(&before); err != nil {
				return err
			}
			if _, err := tx.ExecContext(ctx, `UPDATE sd_player SET gold = $1 WHERE user_id = $2`, *request.SetGold, userID); err != nil {
				return err
			}
			if _, err := tx.ExecContext(ctx, `INSERT INTO sd_currency_ledger (user_id, delta, balance_after, reason, ref, currency) VALUES ($1, $2, $3, 'dev_grant', 'dev', 'gold')`, userID, *request.SetGold-before, *request.SetGold); err != nil {
				return err
			}
		}
		if request.SetMana != nil {
			if *request.SetMana < 0 {
				return errBadRequest
			}
			var before int64
			if err := tx.QueryRowContext(ctx, `SELECT mana FROM sd_player WHERE user_id = $1`, userID).Scan(&before); err != nil {
				return err
			}
			if _, err := tx.ExecContext(ctx, `UPDATE sd_player SET mana = $1 WHERE user_id = $2`, *request.SetMana, userID); err != nil {
				return err
			}
			if _, err := tx.ExecContext(ctx, `INSERT INTO sd_currency_ledger (user_id, delta, balance_after, reason, ref, currency) VALUES ($1, $2, $3, 'dev_grant', 'dev', 'mana')`, userID, *request.SetMana-before, *request.SetMana); err != nil {
				return err
			}
		}
		if request.BackdateCollectHours != 0 {
			if _, err := tx.ExecContext(ctx, `UPDATE sd_building SET last_collected_ms = last_collected_ms - $1 WHERE user_id = $2`, request.BackdateCollectHours*3600000, userID); err != nil {
				return err
			}
		}
		if request.MaxLevelFor != "" {
			if !uuidPattern.MatchString(request.MaxLevelFor) {
				return errBadRequest
			}
			if _, err := tx.ExecContext(ctx, `UPDATE sd_hero SET level = 200, experience = 0 WHERE instance_id = $1 AND user_id = $2`, request.MaxLevelFor, userID); err != nil {
				return err
			}
		}
		state, err := loadState(ctx, tx, userID, target)
		if err != nil {
			return err
		}
		output, err = marshal(state)
		return err
	})
	if err != nil {
		return "", err
	}
	return output, nil
}

type auditResponse struct {
	Crystals      int64 `json:"crystals"`
	LedgerSum     int64 `json:"ledgerSum"`
	SummonDebits  int64 `json:"summonDebits"`
	Rolls         int64 `json:"rolls"`
	RollsWithHero int64 `json:"rollsWithHero"`
	Gold          int64 `json:"gold"`
	GoldLedgerSum int64 `json:"goldLedgerSum"`
	Mana          int64 `json:"mana"`
	ManaLedgerSum int64 `json:"manaLedgerSum"`
}

// rpcDevAudit reports totals that must always agree, used to prove crash recovery.
func (s *Server) rpcDevAudit(ctx context.Context, logger runtime.Logger, db *sql.DB, nk runtime.NakamaModule, payload string) (string, error) {
	userID, err := userFrom(ctx)
	if err != nil {
		return "", err
	}
	var audit auditResponse
	err = db.QueryRowContext(ctx, `SELECT
		COALESCE((SELECT crystals FROM sd_player WHERE user_id = $1), 0),
		COALESCE((SELECT sum(delta) FROM sd_currency_ledger WHERE user_id = $1 AND currency = 'crystals'), 0),
		COALESCE((SELECT -sum(delta) FROM sd_currency_ledger WHERE user_id = $1 AND reason = 'summon' AND currency = 'crystals'), 0),
		(SELECT count(*) FROM sd_summon_roll WHERE user_id = $1),
		(SELECT count(*) FROM sd_summon_roll r JOIN sd_hero h ON h.instance_id = r.instance_id WHERE r.user_id = $1),
		COALESCE((SELECT gold FROM sd_player WHERE user_id = $1), 0),
		COALESCE((SELECT sum(delta) FROM sd_currency_ledger WHERE user_id = $1 AND currency = 'gold'), 0),
		COALESCE((SELECT mana FROM sd_player WHERE user_id = $1), 0),
		COALESCE((SELECT sum(delta) FROM sd_currency_ledger WHERE user_id = $1 AND currency = 'mana'), 0)`, userID).
		Scan(&audit.Crystals, &audit.LedgerSum, &audit.SummonDebits, &audit.Rolls, &audit.RollsWithHero, &audit.Gold, &audit.GoldLedgerSum, &audit.Mana, &audit.ManaLedgerSum)
	if err != nil {
		return "", err
	}
	return marshal(audit)
}

// ---------- Rate limiting ----------

type bucket struct {
	tokens float64
	last   time.Time
}

// rateLimiter is a per-player token bucket, per server process.
type rateLimiter struct {
	mu      sync.Mutex
	rate    float64
	burst   float64
	buckets map[string]*bucket
}

func newRateLimiter(rate, burst float64) *rateLimiter {
	return &rateLimiter{rate: rate, burst: burst, buckets: map[string]*bucket{}}
}

func (l *rateLimiter) Allow(userID string, now time.Time) bool {
	l.mu.Lock()
	defer l.mu.Unlock()
	if len(l.buckets) > 100000 {
		l.buckets = map[string]*bucket{}
	}
	current, ok := l.buckets[userID]
	if !ok {
		current = &bucket{tokens: l.burst, last: now}
		l.buckets[userID] = current
	}
	current.tokens += now.Sub(current.last).Seconds() * l.rate
	if current.tokens > l.burst {
		current.tokens = l.burst
	}
	current.last = now
	if current.tokens < 1 {
		return false
	}
	current.tokens--
	return true
}
