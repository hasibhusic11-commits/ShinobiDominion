using System;
using System.Linq;
using ShinobiDominion.Core;
using UnityEngine;
using UnityEngine.UI;

namespace ShinobiDominion.Game
{
    /// <summary>
    /// Player names and guilds (ADR-042), on the home screen's friends shortcut and in the Menu. The server
    /// checks every name, membership and chat line; this only draws what it sends back.
    /// </summary>
    public sealed partial class VillageController
    {
        private GuildView _guild;
        private InputField _guildInput;
        private float _guildPoll;
        private string _playerName;
        private bool _leaveArmed;

        public GuildView CurrentGuild => _guild;
        public string PlayerName => string.IsNullOrEmpty(_playerName) ? "Player" : _playerName;

        // ---------- Text entry ----------

        /// <summary>A one-line text box. On a phone, tapping it opens the keyboard.</summary>
        private static InputField TextBox(Transform parent, string name, string placeholder, int limit)
        {
            var rect = Ui.Rect(name, parent);
            var layout = rect.gameObject.AddComponent<LayoutElement>();
            layout.minHeight = layout.preferredHeight = 56f;
            var back = Ui.Framed(rect, Ui.PillSprite);
            var field = rect.gameObject.AddComponent<InputField>();
            field.targetGraphic = back;
            field.characterLimit = limit;
            field.lineType = InputField.LineType.SingleLine;
            var text = Ui.LegacyLabel(rect, "", 20, TextAnchor.MiddleLeft, Color.white);
            Ui.Stretch(text.rectTransform, 16f, 4f, 16f, 4f);
            text.supportRichText = false;
            var hint = Ui.LegacyLabel(rect, placeholder, 20, TextAnchor.MiddleLeft, new Color(1f, 1f, 1f, 0.45f));
            Ui.Stretch(hint.rectTransform, 16f, 4f, 16f, 4f);
            field.textComponent = text;
            field.placeholder = hint;
            return field;
        }

        // ---------- Names ----------

        private void ShowNameWindow()
        {
            if (!ModesOnline)
            {
                RefuseShortcutOffline("Your name");
                return;
            }
            _panel.Show("Your name", OpenSettings, panel =>
            {
                if (!string.IsNullOrEmpty(LastMessage)) panel.Line(LastMessage, 20, Ui.AccentColor);
                panel.Line("3 to 16 letters, digits, spaces, - or _. Other players see it in the Arena, rankings and guilds.", 19);
                var box = TextBox(panel.Row(), "NameInput", PlayerName, 16);
                box.text = _playerName ?? "";
                panel.Action("SaveNameButton", "Save", () => SetPlayerName(box.text), true, Ui.AccentColor);
            });
        }

        public void SetPlayerName(string name)
        {
            if (!ModesOnline) return;
            Run(async () =>
            {
                _playerName = await Modes.SetNameAsync(name);
                LastMessage = null;
                RefreshHud();
                OpenSettings();
            }, ShowNameWindow);
        }

        // ---------- Guilds ----------

        public void OpenGuild()
        {
            if (RefuseShortcutOffline("Guild")) return;
            LastMessage = null;
            Fetch(async () =>
            {
                _guild = await Modes.GuildViewAsync();
                ShowGuild();
            }, ShowGuild);
        }

        private void GuildCall(Func<string, System.Threading.Tasks.Task<GuildView>> call, string request)
        {
            if (!ModesOnline) return;
            var key = KeyFor(request);
            Run(async () =>
            {
                var view = await call(key);
                ClearKey();
                LastMessage = null;
                _guild = view;
                ServerCrystals = view.crystals;
                RefreshHud();
                ShowGuild();
            }, ShowGuild);
        }

        private void ShowGuild()
        {
            var view = _guild;
            _guildInput = null;
            if (view?.guild == null)
            {
                _panel.ShowStage("Guilds", view == null ? "" : $"You: {view.myName}", "icon_friends", null, false, 0f, CloseAll, panel =>
                {
                    ScreenKit.Notice(panel, LastMessage);
                    if (view == null) return;
                    if (view.browse.Count == 0) panel.Line("No guilds yet.", 21);
                    for (var i = 0; i < view.browse.Count; i++)
                    {
                        var guild = view.browse[i];
                        var full = guild.members >= GuildView.MaxMembers;
                        var id = guild.id;
                        var row = ScreenKit.AddRow(panel, "GuildRow" + i, "icon_guild", guild.name, string.IsNullOrEmpty(guild.description) ? null : guild.description);
                        // A guild's name and motto are things a player typed: shown as written, never obeyed.
                        row.Title.supportRichText = false;
                        if (row.Detail != null) row.Detail.supportRichText = false;
                        row.Button("JoinGuild" + i, full ? "Full" : "Join", () => GuildCall(key => Modes.GuildJoinAsync(key, id), "guild-join|" + id), !full, full ? (Color?)null : Ui.AccentColor, 130f);
                        row.Reward("icon_trophy", guild.points.ToString("N0"));
                        row.Reward("icon_friends", $"{guild.members}/{GuildView.MaxMembers}");
                        if (full) row.Dim();
                    }
                    panel.Line($"Found your own guild for {view.foundingCost:N0} gems:", 21, new Color(1f, 0.9f, 0.66f));
                    var name = TextBox(panel.Row(), "GuildNameInput", "Guild name", 16);
                    var motto = TextBox(panel.Row(), "GuildMottoInput", "Motto (optional)", 60);
                    ScreenKit.Feature(panel, "FoundGuildButton", "Found guild", () => GuildCall(key => Modes.GuildCreateAsync(key, name.text, motto.text), $"guild-create|{name.text}"), view.crystals >= view.foundingCost, Ui.AccentColor, 320f, 62f);
                });
                return;
            }

            _panel.ShowStage(view.guild.name, $"{view.guild.members}/{GuildView.MaxMembers} members    {view.guild.points:N0} points", "icon_friends", null, false, 0f, CloseAll, panel =>
            {
                ScreenKit.Notice(panel, LastMessage);
                panel.Columns(470f, left =>
                {
                    // The members, each on its own small plate: the leader first, the player's own row on parchment.
                    var y = 0f;
                    foreach (var member in view.members.Take(9))
                    {
                        var plate = Ui.Rect("Member", left);
                        plate.anchorMin = new Vector2(0f, 1f);
                        plate.anchorMax = new Vector2(1f, 1f);
                        plate.pivot = new Vector2(0.5f, 1f);
                        plate.sizeDelta = new Vector2(0f, 44f);
                        plate.anchoredPosition = new Vector2(0f, y);
                        var image = plate.gameObject.AddComponent<Image>();
                        image.sprite = HudArt.Sprite(member.isMe ? "parchment_s40" : "plate_s30");
                        image.type = Image.Type.Sliced;
                        image.pixelsPerUnitMultiplier = 2f;
                        image.raycastTarget = false;
                        var ink = member.isMe ? ScreenKit.InkBrown : HudArt.Ivory;
                        var edge = member.isMe ? ScreenKit.ParchmentEdge : HudArt.LacquerDeep;
                        var badge = HudArt.Picture(plate, "Role", member.role == "leader" ? "icon_rank" : "icon_hero", new Vector2(0f, 0.5f), new Vector2(24f, 24f), new Vector2(14f, 0f));
                        badge.preserveAspect = true;
                        if (member.isMe) badge.color = ScreenKit.InkBrown;
                        var hero = !string.IsNullOrEmpty(member.archetypeId) && Roster.Catalog.TryGet(member.archetypeId, out var archetype) ? $"{archetype.displayName} {member.level}" : "";
                        var who = HudArt.Label(plate, member.name, HudArt.Display, 18, TextAnchor.MiddleLeft, ink, edge);
                        // A member's name is something a player typed: shown as written, never obeyed.
                        who.supportRichText = false;
                        Ui.Stretch(who.rectTransform, 46f, 0f, 250f, 0f);
                        who.horizontalOverflow = HorizontalWrapMode.Overflow;
                        who.resizeTextForBestFit = true;
                        who.resizeTextMinSize = 12;
                        who.resizeTextMaxSize = 18;
                        var guard = Ui.Label(plate, hero, 15, TextAnchor.MiddleLeft, member.isMe ? ScreenKit.InkBrownSoft : HudArt.IvoryMuted);
                        guard.rectTransform.anchorMin = new Vector2(1f, 0f);
                        guard.rectTransform.anchorMax = Vector2.one;
                        guard.rectTransform.offsetMin = new Vector2(-244f, 0f);
                        guard.rectTransform.offsetMax = new Vector2(-88f, 0f);
                        guard.horizontalOverflow = HorizontalWrapMode.Overflow;
                        guard.resizeTextForBestFit = true;
                        guard.resizeTextMinSize = 10;
                        guard.resizeTextMaxSize = 15;
                        var points = HudArt.Label(plate, $"{member.points:N0} pts", HudArt.Numbers, 16, TextAnchor.MiddleRight, ink, edge);
                        points.rectTransform.anchorMin = new Vector2(1f, 0f);
                        points.rectTransform.anchorMax = Vector2.one;
                        points.rectTransform.offsetMin = new Vector2(-88f, 0f);
                        points.rectTransform.offsetMax = new Vector2(-12f, 0f);
                        y -= 48f;
                    }
                    if (view.members.Count > 9)
                    {
                        var more = Ui.Label(left, $"and {view.members.Count - 9} more", 16, TextAnchor.UpperLeft);
                        more.rectTransform.anchorMin = new Vector2(0f, 1f);
                        more.rectTransform.anchorMax = new Vector2(1f, 1f);
                        more.rectTransform.pivot = new Vector2(0f, 1f);
                        more.rectTransform.sizeDelta = new Vector2(0f, 24f);
                        more.rectTransform.anchoredPosition = new Vector2(8f, y - 2f);
                    }
                }, () =>
                {
                    if (!string.IsNullOrEmpty(view.guild.description)) panel.Line(view.guild.description, 18, HudArt.BrassLight).supportRichText = false;
                    if (view.chat.Count == 0) panel.Line("No messages yet.", 19);
                    foreach (var line in view.chat.Skip(Math.Max(0, view.chat.Count - 9)))
                    {
                        var row = panel.Row();
                        var caption = Ui.Label(row, $"{line.name}: {line.text}", 17,
                            TextAnchor.MiddleLeft, line.isMe ? Ui.AccentColor : Ui.MutedText);
                        caption.supportRichText = false;
                        caption.resizeTextForBestFit = true;
                        caption.resizeTextMinSize = 12;
                        caption.resizeTextMaxSize = 17;
                        var captionLayout = caption.gameObject.AddComponent<LayoutElement>();
                        captionLayout.flexibleWidth = 1f;
                        captionLayout.minHeight = 42f;
                        if (!line.isMe && line.id > 0)
                        {
                            var messageId = line.id;
                            var report = SdKit.Button(row, "ReportGuildMessage_" + messageId,
                                line.reported ? "Reported" : "Report", SdButtonKind.Navy,
                                () => GuildCall(key => Modes.GuildReportAsync(key, messageId),
                                    "guild-report|" + messageId), !line.reported);
                            var reportLayout = report.gameObject.AddComponent<LayoutElement>();
                            reportLayout.preferredWidth = 102f;
                            reportLayout.minWidth = 92f;
                            reportLayout.preferredHeight = 40f;
                        }
                    }
                    _guildInput = TextBox(panel.Row(), "GuildChatInput", "Write a message", 200);
                    var input = _guildInput;
                    panel.Action("SendChatButton", "Send", () => GuildCall(key => Modes.GuildChatAsync(key, input.text), $"guild-chat|{input.text}|{DateTime.UtcNow.Ticks}"), true, Ui.AccentColor, 20);
                    // Leaving takes a second tap, so one slip never throws the player out.
                    panel.Action("LeaveGuildButton", _leaveArmed ? "Tap again to leave" : "Leave guild", () =>
                    {
                        if (!_leaveArmed)
                        {
                            _leaveArmed = true;
                            ShowGuild();
                            return;
                        }
                        _leaveArmed = false;
                        GuildCall(key => Modes.GuildLeaveAsync(key), "guild-leave|" + view.guild.id);
                    }, true, _leaveArmed ? UiSkin.Vermilion : (Color?)null, 18);
                });
            });
        }

        /// <summary>While the guild screen is open, new chat arrives every few seconds, unless the player is typing.</summary>
        private void PollGuild()
        {
            if (_guildInput == null || _guild?.guild == null) return;
            if (_guildInput.isFocused || !string.IsNullOrEmpty(_guildInput.text)) return;
            _guildPoll -= Time.unscaledDeltaTime;
            if (_guildPoll > 0f || _busy) return;
            _guildPoll = 5f;
            Fetch(async () =>
            {
                var view = await Modes.GuildViewAsync();
                if (_guildInput == null) return;
                var changed = view.chat.Count != _guild.chat.Count || view.chat.LastOrDefault()?.atMs != _guild.chat.LastOrDefault()?.atMs || view.members.Count != _guild.members.Count;
                _guild = view;
                if (changed) ShowGuild();
            });
        }
    }
}
