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
                _panel.ShowScreen("Guilds", view == null ? "" : $"You: {view.myName}", CloseAll, panel =>
                {
                    if (!string.IsNullOrEmpty(LastMessage)) panel.Line(LastMessage, 20, Ui.AccentColor);
                    if (view == null) return;
                    panel.Line("Join a guild to chat with other commanders and climb the rankings together.", 19);
                    if (view.browse.Count == 0) panel.Line("No guilds yet. Found the first one!", 21);
                    for (var i = 0; i < view.browse.Count; i++)
                    {
                        var guild = view.browse[i];
                        var full = guild.members >= GuildView.MaxMembers;
                        var id = guild.id;
                        panel.Action("JoinGuild" + i, $"{guild.name}   {guild.members}/{GuildView.MaxMembers} members   {guild.points:N0} Arena points" + (string.IsNullOrEmpty(guild.description) ? "" : $"   \"{guild.description}\""),
                            () => GuildCall(key => Modes.GuildJoinAsync(key, id), "guild-join|" + id), !full, null, 18);
                    }
                    panel.Line($"Found your own guild for {view.foundingCost:N0} gems:", 21, new Color(1f, 0.9f, 0.66f));
                    var name = TextBox(panel.Row(), "GuildNameInput", "Guild name", 16);
                    var motto = TextBox(panel.Row(), "GuildMottoInput", "Motto (optional)", 60);
                    panel.Action("FoundGuildButton", "Found guild", () => GuildCall(key => Modes.GuildCreateAsync(key, name.text, motto.text), $"guild-create|{name.text}"), view.crystals >= view.foundingCost, Ui.AccentColor);
                });
                return;
            }

            _panel.ShowScreen(view.guild.name, $"{view.guild.members}/{GuildView.MaxMembers} members   {view.guild.points:N0} Arena points", CloseAll, panel =>
            {
                if (!string.IsNullOrEmpty(LastMessage)) panel.Line(LastMessage, 20, Ui.AccentColor);
                if (!string.IsNullOrEmpty(view.guild.description)) panel.Line($"\"{view.guild.description}\"", 19);
                panel.Columns(520f, left =>
                {
                    var title = Ui.Strong(Ui.Label(left, "Members", 22, TextAnchor.UpperLeft, new Color(1f, 0.9f, 0.66f)));
                    title.rectTransform.anchorMin = new Vector2(0f, 1f);
                    title.rectTransform.anchorMax = new Vector2(1f, 1f);
                    title.rectTransform.pivot = new Vector2(0f, 1f);
                    title.rectTransform.sizeDelta = new Vector2(0f, 30f);
                    var y = -34f;
                    foreach (var member in view.members.Take(14))
                    {
                        var hero = !string.IsNullOrEmpty(member.archetypeId) && Roster.Catalog.TryGet(member.archetypeId, out var archetype) ? $", {archetype.displayName} {member.level}" : "";
                        var line = Ui.Label(left, $"{(member.role == "leader" ? "Leader  " : "")}{member.name}   {member.points:N0} pts{hero}", 18, TextAnchor.UpperLeft, member.isMe ? Ui.AccentColor : (Color?)null);
                        line.rectTransform.anchorMin = new Vector2(0f, 1f);
                        line.rectTransform.anchorMax = new Vector2(1f, 1f);
                        line.rectTransform.pivot = new Vector2(0f, 1f);
                        line.rectTransform.sizeDelta = new Vector2(0f, 26f);
                        line.rectTransform.anchoredPosition = new Vector2(0f, y);
                        y -= 27f;
                    }
                    if (view.members.Count > 14) Ui.Label(left, $"and {view.members.Count - 14} more", 17, TextAnchor.UpperLeft).rectTransform.anchoredPosition = new Vector2(0f, y);
                }, () =>
                {
                    panel.Line("Guild chat", 22, new Color(1f, 0.9f, 0.66f));
                    if (view.chat.Count == 0) panel.Line("No messages yet. Say hello!", 19);
                    foreach (var line in view.chat.Skip(Math.Max(0, view.chat.Count - 9)))
                        panel.Line($"{line.name}: {line.text}", 18, line.isMe ? Ui.AccentColor : (Color?)null).supportRichText = false;
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
