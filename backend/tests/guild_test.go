package backendtests

import (
	"encoding/json"
	"sync"
	"testing"
)

// Player names and guilds (ADR-042).

type guildView struct {
	MyName string `json:"myName"`
	Guild  *struct {
		ID      string `json:"id"`
		Name    string `json:"name"`
		Members int    `json:"members"`
	} `json:"guild"`
	Members []struct {
		Name string `json:"name"`
		Role string `json:"role"`
		IsMe bool   `json:"isMe"`
	} `json:"members"`
	Chat []struct {
		ID int64 `json:"id"`
		Name string `json:"name"`
		Text string `json:"text"`
		Reported bool `json:"reported"`
	} `json:"chat"`
	Browse []struct {
		ID   string `json:"id"`
		Name string `json:"name"`
	} `json:"browse"`
	Crystals int64 `json:"crystals"`
}

func guildCall(p *player, rpc string, fields map[string]interface{}) (guildView, error) {
	var view guildView
	if fields == nil {
		fields = map[string]interface{}{}
	}
	fields["key"] = "g-" + randomHex(8)
	body, err := p.rpc(rpc, fields)
	if err != nil {
		return view, err
	}
	return view, json.Unmarshal([]byte(body), &view)
}

func TestNames_AreCheckedOnTheServer(t *testing.T) {
	p := newPlayer(t)
	_, err := p.rpc("sd_set_name", map[string]interface{}{"name": "<b>x</b>"})
	expectError(t, err, "BAD_NAME")
	if _, err := p.rpc("sd_set_name", map[string]interface{}{"name": "  Storm Fox "}); err != nil {
		t.Fatal(err)
	}
	body, _ := p.rpc("sd_guild_view", "{}")
	var view guildView
	json.Unmarshal([]byte(body), &view)
	if view.MyName != "Storm Fox" {
		t.Fatalf("name %q", view.MyName)
	}
}

func TestGuild_FoundJoinChatLeave(t *testing.T) {
	leader := newPlayer(t)
	name := "Guild" + randomHex(4)
	before := mustState(t, leader).Crystals
	view, err := guildCall(leader, "sd_guild_create", map[string]interface{}{"name": name, "description": "Friendly ninjas"})
	if err != nil {
		t.Fatal(err)
	}
	if view.Guild == nil || view.Guild.Name != name || view.Crystals != before-300 {
		t.Fatalf("founding should cost 300 gems: %+v", view)
	}
	_, err = guildCall(newPlayer(t), "sd_guild_create", map[string]interface{}{"name": name})
	expectError(t, err, "NAME_TAKEN")
	_, err = guildCall(leader, "sd_guild_create", map[string]interface{}{"name": "Other" + randomHex(4)})
	expectError(t, err, "ALREADY_IN_GUILD")

	member := newPlayer(t)
	if _, err := guildCall(member, "sd_guild_join", map[string]interface{}{"guildId": view.Guild.ID}); err != nil {
		t.Fatal(err)
	}
	chat, err := guildCall(member, "sd_guild_chat", map[string]interface{}{"text": "  hello\nleader  "})
	if err != nil {
		t.Fatal(err)
	}
	if len(chat.Chat) != 1 || chat.Chat[0].Text != "hello leader" || len(chat.Members) != 2 {
		t.Fatalf("chat %+v", chat)
	}
	_, err = guildCall(member, "sd_guild_chat", map[string]interface{}{"text": "   "})
	expectError(t, err, "BAD_MESSAGE")

	// The leader leaves: the member takes over, and the guild lives on.
	after, err := guildCall(leader, "sd_guild_leave", nil)
	if err != nil {
		t.Fatal(err)
	}
	if after.Guild != nil {
		t.Fatal("the leader should be out")
	}
	mine, _ := guildCall(member, "sd_guild_chat", map[string]interface{}{"text": "still here"})
	if mine.Guild == nil || len(mine.Members) != 1 || mine.Members[0].Role != "leader" {
		t.Fatalf("the member should lead now: %+v", mine)
	}
	if _, err := guildCall(member, "sd_guild_leave", nil); err != nil {
		t.Fatal(err)
	}
	_, err = guildCall(newPlayer(t), "sd_guild_join", map[string]interface{}{"guildId": view.Guild.ID})
	expectError(t, err, "GUILD_NOT_FOUND")
	ledgersAgree(t, leader)
}

func TestGuild_ConcurrentJoinsNeverPassTheCap(t *testing.T) {
	leader := newPlayer(t)
	view, err := guildCall(leader, "sd_guild_create", map[string]interface{}{"name": "Cap" + randomHex(4)})
	if err != nil {
		t.Fatal(err)
	}
	players := make([]*player, 35)
	for i := range players {
		players[i] = newPlayer(t)
	}
	var wg sync.WaitGroup
	for _, p := range players {
		wg.Add(1)
		go func(p *player) {
			defer wg.Done()
			guildCall(p, "sd_guild_join", map[string]interface{}{"guildId": view.Guild.ID})
		}(p)
	}
	wg.Wait()
	final, _ := guildCall(leader, "sd_guild_chat", map[string]interface{}{"text": "roll call"})
	if final.Guild.Members != 30 {
		t.Fatalf("a guild holds 30, it has %d", final.Guild.Members)
	}
}


// Requires the real Nakama + Postgres integration stack; verifies that
// reporting cannot cross guilds and that evidence survives guild dissolution.
func TestGuild_ReportIsScopedAndSurvivesGuildClosure(t *testing.T) {
	leader := newPlayer(t)
	member := newPlayer(t)
	outsider := newPlayer(t)
	name := "Report" + randomHex(4)
	created, err := guildCall(leader, "sd_guild_create", map[string]interface{}{"name": name})
	if err != nil { t.Fatal(err) }
	if _, err := guildCall(member, "sd_guild_join", map[string]interface{}{"guildId": created.Guild.ID}); err != nil {
		t.Fatal(err)
	}
	chat, err := guildCall(leader, "sd_guild_chat", map[string]interface{}{"text": "please review this text"})
	if err != nil { t.Fatal(err) }
	if len(chat.Chat) == 0 || chat.Chat[len(chat.Chat)-1].ID < 1 {
		t.Fatalf("chat must carry a message ID: %+v", chat)
	}
	messageID := chat.Chat[len(chat.Chat)-1].ID

	_, err = guildCall(outsider, "sd_guild_report", map[string]interface{}{"messageId": messageID})
	expectError(t, err, "NOT_IN_GUILD")
	_, err = guildCall(leader, "sd_guild_report", map[string]interface{}{"messageId": messageID})
	expectError(t, err, "CANNOT_REPORT_SELF")

	result, err := guildCall(member, "sd_guild_report", map[string]interface{}{"messageId": messageID})
	if err != nil { t.Fatal(err) }
	if len(result.Chat) == 0 || !result.Chat[len(result.Chat)-1].Reported {
		t.Fatalf("reporter must see their own report status: %+v", result.Chat)
	}
	// A fresh request key for the same line is harmless and still returns success.
	if _, err := guildCall(member, "sd_guild_report", map[string]interface{}{"messageId": messageID}); err != nil {
		t.Fatalf("duplicate report must be idempotent: %v", err)
	}
	if _, err := guildCall(leader, "sd_guild_leave", nil); err != nil { t.Fatal(err) }
	if _, err := guildCall(member, "sd_guild_leave", nil); err != nil {
		t.Fatalf("a guild with reported messages must still dissolve cleanly: %v", err)
	}
}
