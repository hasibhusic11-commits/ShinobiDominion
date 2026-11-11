package main

import (
	"context"
	"database/sql"
	"errors"
	"regexp"
	"strings"
	"time"
	"unicode"

	"github.com/heroiclabs/nakama-common/runtime"
)

// Player names and guilds (ADR-042). Everything goes through these calls: the client never writes a
// name, a membership or a chat line itself. Guilds are open to join, hold up to guildMaxMembers, and
// cost gems to found. Chat is plain text, kept short, rate limited and stored per guild.

const (
	guildMaxMembers   = 30
	guildFoundingCost = 300
	guildChatKeep     = 100
	guildChatMaxRunes = 200
	guildChatPerMin   = 12
)

var (
	namePattern      = regexp.MustCompile(`^[A-Za-z0-9][A-Za-z0-9 _-]{1,14}[A-Za-z0-9]$`)
	errBadName       = gameError("BAD_NAME", codeInvalid)
	errNameTaken     = gameError("NAME_TAKEN", codePrecondition)
	errInGuild       = gameError("ALREADY_IN_GUILD", codePrecondition)
	errNotInGuild    = gameError("NOT_IN_GUILD", codePrecondition)
	errGuildFull     = gameError("GUILD_FULL", codePrecondition)
	errGuildNotFound = gameError("GUILD_NOT_FOUND", codeNotFound)
	errBadChat       = gameError("BAD_MESSAGE", codeInvalid)
	errChatTooFast   = gameError("CHAT_TOO_FAST", codeExhausted)
	errBadReport     = gameError("BAD_REPORT", codeInvalid)
	errReportTarget  = gameError("REPORT_TARGET_UNAVAILABLE", codeNotFound)
	errReportTooFast = gameError("REPORT_LIMIT_REACHED", codeExhausted)
)

// validGuildReportReason restricts report categories to a short, reviewable taxonomy.
func validGuildReportReason(reason string) bool {
	switch reason {
	case "harassment", "hate", "spam", "other":
		return true
	default:
		return false
	}
}

// cleanName trims a name and checks it: 3 to 16 letters, digits, spaces, - or _, starting and ending
// with a letter or digit, with no double spaces.
func cleanName(raw string) (string, bool) {
	name := strings.TrimSpace(raw)
	if !namePattern.MatchString(name) || strings.Contains(name, "  ") {
		return "", false
	}
	return name, true
}

// cleanChat keeps printable text only, on one line, and refuses empty or long messages.
func cleanChat(raw string) (string, bool) {
	var b strings.Builder
	runes := 0
	for _, r := range raw {
		if r == '\n' || r == '\r' || r == '\t' {
			r = ' '
		}
		if !unicode.IsPrint(r) {
			continue
		}
		b.WriteRune(r)
		runes++
	}
	text := strings.Join(strings.Fields(b.String()), " ")
	if text == "" || runes > guildChatMaxRunes {
		return "", false
	}
	return text, true
}

func (s *Server) rpcSetName(ctx context.Context, logger runtime.Logger, db *sql.DB, nk runtime.NakamaModule, payload string) (string, error) {
	userID, err := userFrom(ctx)
	if err != nil {
		return "", err
	}
	var request struct {
		Name string `json:"name"`
	}
	if err := decodeStrict(payload, &request); err != nil {
		return "", err
	}
	name, ok := cleanName(request.Name)
	if !ok {
		return "", errBadName
	}
	if !s.limiter.Allow(userID, time.Now()) {
		return "", errRateLimited
	}
	var output string
	err = withTx(ctx, db, func(tx *sql.Tx) error {
		if _, err := s.lockPlayer(ctx, tx, userID); err != nil {
			return err
		}
		if _, err := tx.ExecContext(ctx, `UPDATE users SET display_name = $1, update_time = now() WHERE id = $2`, name, userID); err != nil {
			return err
		}
		output, err = marshal(map[string]string{"name": name})
		return err
	})
	return output, err
}

type guildSummary struct {
	ID          string `json:"id"`
	Name        string `json:"name"`
	Description string `json:"description"`
	Members     int    `json:"members"`
	Points      int    `json:"points"`
}

type guildMember struct {
	Name        string `json:"name"`
	Role        string `json:"role"`
	Points      int    `json:"points"`
	ArchetypeID string `json:"archetypeId"`
	Level       int    `json:"level"`
	IsMe        bool   `json:"isMe"`
}

type guildChatLine struct {
	MessageID int64 `json:"messageId"`
	Name string `json:"name"`
	Text string `json:"text"`
	AtMs int64  `json:"atMs"`
	IsMe bool   `json:"isMe"`
}

type guildView struct {
	Name     string         `json:"name"`
	MyName   string         `json:"myName"`
	Guild    *guildSummary  `json:"guild"`
	Members  []guildMember  `json:"members"`
	Chat     []guildChatLine `json:"chat"`
	Browse   []guildSummary `json:"browse"`
	Crystals int64          `json:"crystals"`
	Cost     int64          `json:"foundingCost"`
}

const sqlGuildSummary = `SELECT g.id::text, g.name, g.description,
	(SELECT count(*) FROM sd_guild_member m WHERE m.guild_id = g.id),
	COALESCE((SELECT sum(a.points) FROM sd_guild_member m JOIN sd_arena a ON a.user_id = m.user_id WHERE m.guild_id = g.id), 0)
	FROM sd_guild g`

func scanGuilds(rows *sql.Rows) ([]guildSummary, error) {
	defer rows.Close()
	list := []guildSummary{}
	for rows.Next() {
		var g guildSummary
		if err := rows.Scan(&g.ID, &g.Name, &g.Description, &g.Members, &g.Points); err != nil {
			return nil, err
		}
		list = append(list, g)
	}
	return list, rows.Err()
}

func myGuildID(ctx context.Context, tx *sql.Tx, userID string) (string, error) {
	var id string
	err := tx.QueryRowContext(ctx, `SELECT guild_id::text FROM sd_guild_member WHERE user_id = $1`, userID).Scan(&id)
	if errors.Is(err, sql.ErrNoRows) {
		return "", nil
	}
	return id, err
}

// loadGuildView is everything the guild screen shows: the player's guild with members and chat, or a
// list of guilds to join.
func (s *Server) loadGuildView(ctx context.Context, tx *sql.Tx, userID string, crystals int64) (*guildView, error) {
	view := &guildView{Name: "", MyName: playerName(ctx, tx, userID), Members: []guildMember{}, Chat: []guildChatLine{}, Browse: []guildSummary{}, Crystals: crystals, Cost: guildFoundingCost}
	guildID, err := myGuildID(ctx, tx, userID)
	if err != nil {
		return nil, err
	}
	if guildID == "" {
		rows, err := tx.QueryContext(ctx, sqlGuildSummary+` ORDER BY 5 DESC, 4 DESC, g.created_at LIMIT 30`)
		if err != nil {
			return nil, err
		}
		view.Browse, err = scanGuilds(rows)
		return view, err
	}
	rows, err := tx.QueryContext(ctx, sqlGuildSummary+` WHERE g.id = $1`, guildID)
	if err != nil {
		return nil, err
	}
	found, err := scanGuilds(rows)
	if err != nil {
		return nil, err
	}
	if len(found) == 1 {
		view.Guild = &found[0]
		view.Name = found[0].Name
	}
	memberRows, err := tx.QueryContext(ctx, `SELECT m.user_id::text, COALESCE(`+sqlPlayerName+`, 'Shinobi'), m.role, COALESCE(a.points, 0),
			COALESCE(h.archetype_id, ''), COALESCE(h.level, 0)
		FROM sd_guild_member m
		LEFT JOIN users u ON u.id = m.user_id
		LEFT JOIN sd_arena a ON a.user_id = m.user_id
		LEFT JOIN LATERAL (SELECT archetype_id, level FROM sd_hero WHERE user_id = m.user_id AND base_slot IS NOT NULL ORDER BY base_slot LIMIT 1) h ON TRUE
		WHERE m.guild_id = $1 ORDER BY m.role = 'leader' DESC, 4 DESC, m.joined_at`, guildID)
	if err != nil {
		return nil, err
	}
	defer memberRows.Close()
	for memberRows.Next() {
		var member guildMember
		var id string
		if err := memberRows.Scan(&id, &member.Name, &member.Role, &member.Points, &member.ArchetypeID, &member.Level); err != nil {
			return nil, err
		}
		member.IsMe = id == userID
		view.Members = append(view.Members, member)
	}
	if err := memberRows.Err(); err != nil {
		return nil, err
	}
	memberRows.Close()
	chatRows, err := tx.QueryContext(ctx, `SELECT * FROM (SELECT c.user_id::text, COALESCE(`+sqlPlayerName+`, 'Shinobi'), c.text, (EXTRACT(EPOCH FROM c.created_at) * 1000)::bigint, c.id
		FROM sd_guild_chat c LEFT JOIN users u ON u.id = c.user_id
		WHERE c.guild_id = $1 ORDER BY c.id DESC LIMIT 40) recent ORDER BY 5`, guildID)
	if err != nil {
		return nil, err
	}
	defer chatRows.Close()
	for chatRows.Next() {
		var line guildChatLine
		var id string
		var serial int64
		if err := chatRows.Scan(&id, &line.Name, &line.Text, &line.AtMs, &serial); err != nil {
			return nil, err
		}
		line.IsMe = id == userID
		line.MessageID = serial
		view.Chat = append(view.Chat, line)
	}
	return view, chatRows.Err()
}

// guildOp is the path every guild change takes: lock the player, check the request id, change, answer
// with the fresh guild screen.
func (s *Server) guildOp(ctx context.Context, db *sql.DB, payload, kind string, change func(tx *sql.Tx, userID string, crystals int64, request map[string]string) (int64, error)) (string, error) {
	userID, err := userFrom(ctx)
	if err != nil {
		return "", err
	}
	var request struct {
		Key         string `json:"key"`
		Name        string `json:"name"`
		Description string `json:"description"`
		GuildID     string `json:"guildId"`
		Text        string `json:"text"`
	}
	if err := decodeStrict(payload, &request); err != nil {
		return "", err
	}
	if !keyPattern.MatchString(request.Key) {
		return "", errBadKey
	}
	if !s.limiter.Allow(userID, time.Now()) {
		return "", errRateLimited
	}
	requestHash := hashRequest(request.Name, request.Description, request.GuildID, request.Text)
	var output string
	err = withTx(ctx, db, func(tx *sql.Tx) error {
		crystals, err := s.lockPlayer(ctx, tx, userID)
		if err != nil {
			return err
		}
		if stored, found, err := replayed(ctx, tx, userID, request.Key, kind, requestHash); err != nil {
			return err
		} else if found {
			output = stored
			return nil
		}
		crystals, err = change(tx, userID, crystals, map[string]string{"name": request.Name, "description": request.Description, "guildId": request.GuildID, "text": request.Text})
		if err != nil {
			return err
		}
		view, err := s.loadGuildView(ctx, tx, userID, crystals)
		if err != nil {
			return err
		}
		if output, err = marshal(view); err != nil {
			return err
		}
		return storeResponse(ctx, tx, userID, request.Key, kind, requestHash, output)
	})
	if err != nil {
		return "", err
	}
	return output, nil
}

func (s *Server) rpcGuildView(ctx context.Context, logger runtime.Logger, db *sql.DB, nk runtime.NakamaModule, payload string) (string, error) {
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
		view, err := s.loadGuildView(ctx, tx, userID, crystals)
		if err != nil {
			return err
		}
		output, err = marshal(view)
		return err
	})
	return output, err
}

func (s *Server) rpcGuildCreate(ctx context.Context, logger runtime.Logger, db *sql.DB, nk runtime.NakamaModule, payload string) (string, error) {
	return s.guildOp(ctx, db, payload, "guild_create", func(tx *sql.Tx, userID string, crystals int64, request map[string]string) (int64, error) {
		name, ok := cleanName(request["name"])
		if !ok {
			return 0, errBadName
		}
		description, ok := cleanChat(request["description"])
		if !ok {
			description = ""
		}
		if current, err := myGuildID(ctx, tx, userID); err != nil {
			return 0, err
		} else if current != "" {
			return 0, errInGuild
		}
		if crystals < guildFoundingCost {
			return 0, errFunds
		}
		var taken bool
		if err := tx.QueryRowContext(ctx, `SELECT EXISTS (SELECT 1 FROM sd_guild WHERE lower(name) = lower($1))`, name).Scan(&taken); err != nil {
			return 0, err
		}
		if taken {
			return 0, errNameTaken
		}
		var guildID string
		if err := tx.QueryRowContext(ctx, `INSERT INTO sd_guild (name, description, leader_id) VALUES ($1, $2, $3) RETURNING id::text`, name, description, userID).Scan(&guildID); err != nil {
			return 0, err
		}
		if _, err := tx.ExecContext(ctx, `INSERT INTO sd_guild_member (user_id, guild_id, role) VALUES ($1, $2, 'leader')`, userID, guildID); err != nil {
			return 0, err
		}
		balance := crystals - guildFoundingCost
		if _, err := tx.ExecContext(ctx, `UPDATE sd_player SET crystals = $1 WHERE user_id = $2`, balance, userID); err != nil {
			return 0, err
		}
		return balance, addLedger(ctx, tx, userID, -guildFoundingCost, balance, "guild_found", guildID)
	})
}

func (s *Server) rpcGuildJoin(ctx context.Context, logger runtime.Logger, db *sql.DB, nk runtime.NakamaModule, payload string) (string, error) {
	return s.guildOp(ctx, db, payload, "guild_join", func(tx *sql.Tx, userID string, crystals int64, request map[string]string) (int64, error) {
		guildID := request["guildId"]
		if !uuidPattern.MatchString(guildID) {
			return 0, errGuildNotFound
		}
		if current, err := myGuildID(ctx, tx, userID); err != nil {
			return 0, err
		} else if current != "" {
			return 0, errInGuild
		}
		// The guild row is locked so two players can't both take the last place.
		// Lock first, then count in a new statement: a count inside the locking statement would use the
		// snapshot from before the wait and miss joins that just committed.
		var locked string
		err := tx.QueryRowContext(ctx, `SELECT id::text FROM sd_guild WHERE id = $1 FOR UPDATE`, guildID).Scan(&locked)
		if errors.Is(err, sql.ErrNoRows) {
			return 0, errGuildNotFound
		}
		if err != nil {
			return 0, err
		}
		var members int
		if err := tx.QueryRowContext(ctx, `SELECT count(*) FROM sd_guild_member WHERE guild_id = $1`, guildID).Scan(&members); err != nil {
			return 0, err
		}
		if members >= guildMaxMembers {
			return 0, errGuildFull
		}
		_, err = tx.ExecContext(ctx, `INSERT INTO sd_guild_member (user_id, guild_id, role) VALUES ($1, $2, 'member')`, userID, guildID)
		return crystals, err
	})
}

func (s *Server) rpcGuildLeave(ctx context.Context, logger runtime.Logger, db *sql.DB, nk runtime.NakamaModule, payload string) (string, error) {
	return s.guildOp(ctx, db, payload, "guild_leave", func(tx *sql.Tx, userID string, crystals int64, request map[string]string) (int64, error) {
		guildID, err := myGuildID(ctx, tx, userID)
		if err != nil {
			return 0, err
		}
		if guildID == "" {
			return 0, errNotInGuild
		}
		if _, err := tx.ExecContext(ctx, `SELECT id FROM sd_guild WHERE id = $1 FOR UPDATE`, guildID); err != nil {
			return 0, err
		}
		var role string
		if err := tx.QueryRowContext(ctx, `DELETE FROM sd_guild_member WHERE user_id = $1 RETURNING role`, userID).Scan(&role); err != nil {
			return 0, err
		}
		if role != "leader" {
			return crystals, nil
		}
		// The longest-serving member leads next; a guild with nobody left closes.
		var next string
		err = tx.QueryRowContext(ctx, `SELECT user_id::text FROM sd_guild_member WHERE guild_id = $1 ORDER BY joined_at, user_id LIMIT 1`, guildID).Scan(&next)
		if errors.Is(err, sql.ErrNoRows) {
			if _, err := tx.ExecContext(ctx, `DELETE FROM sd_guild_chat WHERE guild_id = $1`, guildID); err != nil {
				return 0, err
			}
			_, err = tx.ExecContext(ctx, `DELETE FROM sd_guild WHERE id = $1`, guildID)
			return crystals, err
		}
		if err != nil {
			return 0, err
		}
		if _, err := tx.ExecContext(ctx, `UPDATE sd_guild_member SET role = 'leader' WHERE user_id = $1`, next); err != nil {
			return 0, err
		}
		_, err = tx.ExecContext(ctx, `UPDATE sd_guild SET leader_id = $1 WHERE id = $2`, next, guildID)
		return crystals, err
	})
}

func (s *Server) rpcGuildChat(ctx context.Context, logger runtime.Logger, db *sql.DB, nk runtime.NakamaModule, payload string) (string, error) {
	return s.guildOp(ctx, db, payload, "guild_chat", func(tx *sql.Tx, userID string, crystals int64, request map[string]string) (int64, error) {
		text, ok := cleanChat(request["text"])
		if !ok {
			return 0, errBadChat
		}
		guildID, err := myGuildID(ctx, tx, userID)
		if err != nil {
			return 0, err
		}
		if guildID == "" {
			return 0, errNotInGuild
		}
		var recent int
		if err := tx.QueryRowContext(ctx, `SELECT count(*) FROM sd_guild_chat WHERE user_id = $1 AND created_at > now() - interval '1 minute'`, userID).Scan(&recent); err != nil {
			return 0, err
		}
		if recent >= guildChatPerMin {
			return 0, errChatTooFast
		}
		if _, err := tx.ExecContext(ctx, `INSERT INTO sd_guild_chat (guild_id, user_id, text) VALUES ($1, $2, $3)`, guildID, userID, text); err != nil {
			return 0, err
		}
		_, err = tx.ExecContext(ctx, `DELETE FROM sd_guild_chat WHERE guild_id = $1 AND id NOT IN (SELECT id FROM sd_guild_chat WHERE guild_id = $1 ORDER BY id DESC LIMIT $2)`, guildID, guildChatKeep)
		return crystals, err
	})
}

 // NEVER reformat or quote chat text into SQL. This RPC stores evidence even if the original
 // guild disbands or older chat messages are pruned, and does not expose other guilds' content.
 // Reports are a queue for human review, NOT automated judgments or punitive bans.
 func (s *Server) rpcGuildReport(ctx context.Context, logger runtime.Logger, db *sql.DB, nk runtime.NakamaModule, payload string) (string, error) {
 	userID, err := userFrom(ctx)
 	if err != nil { return "", err }
 	var request struct {
 		MessageID int64 `json:"messageId"`
 		Reason string `json:"reason"`
 	}
 	if err := decodeStrict(payload, &request); err != nil { return "", err }
 	if request.MessageID <= 0 || !validGuildReportReason(request.Reason) { return "", errBadReport }
 	if !s.limiter.Allow(userID, time.Now()) { return "", errRateLimited }
 	var output string
 	err = withTx(ctx, db, func(tx *sql.Tx) error {
 		if _, err := s.lockPlayer(ctx, tx, userID); err != nil { return err }
 		guildID, err := myGuildID(ctx, tx, userID)
 		if err != nil { return err }
 		if guildID == "" { return errNotInGuild }
 		var authorID, messageGuildID, messageText string
 		err = tx.QueryRowContext(ctx, `SELECT user_id::text, guild_id::text, text FROM sd_guild_chat WHERE id = $1`, request.MessageID).
 			Scan(&authorID, &messageGuildID, &messageText)
 		if errors.Is(err, sql.ErrNoRows) { return errReportTarget }
 		if err != nil { return err }
 		if guildID != messageGuildID || authorID == userID { return errReportTarget }
 		var reportsToday int
 		err = tx.QueryRowContext(ctx, `SELECT COUNT(*) FROM sd_guild_chat_report WHERE reporter_id = $1 AND created_at >= now() - interval '24 hours'`, userID).
 			Scan(&reportsToday)
 		if err != nil { return err }
 		if reportsToday >= 10 { return errReportTooFast }
 		_, err = tx.ExecContext(ctx, `INSERT INTO sd_guild_chat_report
 			(guild_id, reporter_id, author_id, message_id, message_text, reason)
 			VALUES ($1::uuid, $2::uuid, $3::uuid, $4, $5, $6)
 			ON CONFLICT (reporter_id, message_id) DO NOTHING`,
 			guildID, userID, authorID, request.MessageID, messageText, request.Reason)
 		if err != nil { return err }
 		output = `{"reported":true}`
 		return nil
 	})
 	return output, err
 }
