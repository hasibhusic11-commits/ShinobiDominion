using System.Collections.Generic;
using System.Linq;
using ShinobiDominion.Core;
using UnityEngine;
using UnityEngine.UI;

namespace ShinobiDominion.Game
{
    /// <summary>
    /// The Battle menu that ATTACK opens, and the modes beyond raids and dungeons (ADR-039): Village
    /// Siege, the Arena, rankings, daily quests, the daily gift and the mail of raids on the village.
    /// The server fights Siege and Arena battles in one call; this only plays the log back.
    /// </summary>
    public sealed partial class VillageController
    {
        private List<SiegeSummary> _siegeList;
        private ArenaBoard _arenaBoard;
        private QuestBoard _questBoard;
        private ModeFight _modeFight;

        private IModeServer Modes => _server as IModeServer;
        private bool ModesOnline => IsOnline && Modes != null;

        public ModeFight LastModeFight { get; private set; }

        // ---------- The Battle menu ----------

        private struct ModeTile
        {
            public string Button;
            public string Title;
            public string Blurb;
            public string Go;
            public int Art;
            public bool Open;
            public System.Action Action;
        }

        private void ShowBattleHub()
        {
            var team = Village.Buildings.Count(b => b.AssignedHeroInstanceId != null);
            var dungeonStars = RaidData.All.Sum(r => BestStarsOn(r.id));
            _panel.ShowScreen("Battle", team == 0 ? "Put a hero on your Hero Base to fight" : "Your hero leads every battle", CloseAll, panel =>
            {
                if (!string.IsNullOrEmpty(LastMessage)) panel.Line(LastMessage, 20, Ui.AccentColor);
                if (!IsOnline) panel.Line(AllowOfflinePlay ? "Practice battles only: not connected, so nothing pays." : "Battles need the server.", 20, Ui.AccentColor);
                if (IsOnline && _raidList == null)
                {
                    // Drawn again when the list arrives, so nothing is tapped on a menu about to be replaced.
                    panel.Line("Checking your battles...");
                    return;
                }
                var tiles = new[]
                {
                    new ModeTile { Button = "FindRivalButton", Title = "Raid", Blurb = "Raid a real player's village as they left it.", Go = "Find a rival", Art = 1, Open = IsOnline && team > 0, Action = FindRival },
                    new ModeTile { Button = "OpenDungeonsButton", Title = "Dungeons", Blurb = $"15 fortified camps. {dungeonStars} of {RaidData.All.Count * 3} stars.", Go = "Open the map", Art = 2, Open = IsOnline || AllowOfflinePlay, Action = OpenCampaign },
                    new ModeTile { Button = "OpenSiegeButton", Title = "Village Siege", Blurb = "Waves storm your own village. Hold them off.", Go = "Defend", Art = 3, Open = ModesOnline, Action = OpenSiege },
                    new ModeTile { Button = "OpenArenaButton", Title = "Arena", Blurb = "Hero against hero, for points and rank.", Go = "Enter", Art = 4, Open = ModesOnline && team > 0, Action = OpenArena },
                    new ModeTile { Button = "OpenTrialsButton", Title = "Trial Tower", Blurb = "Climb floor by floor past ever-stronger guardians.", Go = "Climb", Art = 5, Open = ModesOnline && team > 0, Action = OpenTrials }
                };
                var block = panel.Block(560f);
                for (var i = 0; i < tiles.Length; i++) DrawModeTile(block, tiles[i], i, tiles.Length);
                if (team == 0) panel.Line("Raids and the Arena need a hero on your Hero Base. Your village still defends itself in a Siege.", 19, Ui.MutedText);
            });
        }

        private static void DrawModeTile(RectTransform parent, ModeTile tile, int index, int count)
        {
            var rect = Ui.Rect("ModeTile" + index, parent);
            var width = 1f / count;
            rect.anchorMin = new Vector2(index * width, 0f);
            rect.anchorMax = new Vector2((index + 1) * width, 1f);
            rect.offsetMin = new Vector2(8f, 0f);
            rect.offsetMax = new Vector2(-8f, 0f);
            Ui.Background(rect, new Color(0.08f, 0.07f, 0.12f, 1f), false);
            var artRect = Ui.Rect("Art", rect);
            Ui.Stretch(artRect, 14f, 14f, 14f, 14f);
            var art = artRect.gameObject.AddComponent<Image>();
            art.sprite = CampaignArt.Map(tile.Art);
            art.type = Image.Type.Simple;
            art.preserveAspect = false;
            art.raycastTarget = false;
            // The three stony maps get their own colour, so each mode reads as its own place.
            var tint = tile.Art == 3 ? new Color(1f, 0.7f, 0.62f) : tile.Art == 4 ? new Color(1f, 0.86f, 0.55f) : tile.Art == 5 ? new Color(0.7f, 0.82f, 1f) : Color.white;
            art.color = tile.Open ? tint : tint * new Color(0.45f, 0.45f, 0.48f);
            HudArt.DropShadow(rect, 6f, 10f, 0.5f);
            var frame = Ui.Rect("Frame", rect);
            Ui.Stretch(frame);
            var border = Ui.Framed(frame, HudArt.Sprite("frame_s80"));
            border.raycastTarget = false;
            // Only the border: the painted ground shows through the middle.
            border.fillCenter = false;
            // The frame sprite has a wide dark inner band; drawn smaller, it leaves room for the art.
            border.pixelsPerUnitMultiplier = 3f;
            var shade = Ui.Rect("Shade", rect);
            Ui.Stretch(shade);
            Ui.Background(shade, new Color(0.05f, 0.04f, 0.08f, 0.35f), false);
            shade.SetSiblingIndex(artRect.GetSiblingIndex() + 1);

            var title = HudArt.Label(rect, tile.Title, HudArt.Display, 30, TextAnchor.UpperCenter, HudArt.BrassLight, HudArt.LacquerDeep);
            Ui.Anchor(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(280f, 52f), new Vector2(0f, -16f));
            title.resizeTextForBestFit = true;
            title.resizeTextMinSize = 18;
            title.resizeTextMaxSize = 30;
            var blurb = HudArt.Label(rect, tile.Blurb, HudArt.Numbers, 18, TextAnchor.UpperCenter, HudArt.Ivory, HudArt.LacquerDeep);
            Ui.Anchor(blurb.rectTransform, new Vector2(0.5f, 1f), new Vector2(220f, 160f), new Vector2(0f, -86f));
            blurb.horizontalOverflow = HorizontalWrapMode.Wrap;

            var go = Ui.Button(rect, tile.Button, tile.Go, tile.Action, tile.Open, tile.Open ? Ui.AttackColor : (Color?)null, 21);
            Ui.Anchor((RectTransform)go.transform, new Vector2(0.5f, 0f), new Vector2(180f, 56f), new Vector2(0f, 18f));
            if (tile.Open) UiMotion.PopIn(rect, 0.04f * index, 0.85f, 0.26f);
        }

        // ---------- Village Siege ----------

        public void OpenSiege()
        {
            if (!ModesOnline || _raid != null) return;
            LastMessage = null;
            _siegeList = null;
            ShowSiege();
            Fetch(async () =>
            {
                _siegeList = await Modes.SiegeListAsync();
                if (UiState.Screen == VillageScreen.AttackFlow && _raid == null) ShowSiege();
            }, ShowBattleHub);
        }

        private void ShowSiege()
        {
            var stars = _siegeList?.Sum(l => l.bestStars) ?? 0;
            var total = (_siegeList?.Count ?? 0) * 3;
            _panel.ShowScreen("Village Siege", _siegeList == null ? "" : $"{stars} of {total} stars", ShowBattleHub, panel =>
            {
                if (!string.IsNullOrEmpty(LastMessage)) panel.Line(LastMessage, 20, Ui.AccentColor);
                panel.Line("Rogue ninja bands storm your own village. Your towers, walls and your heroes on their bases hold them off. Nothing in your village is lost, win or lose.", 19);
                panel.Line("Three stars: under 10% damage. Two: under 30%. One: they take no star. New stars pay once.", 19, Ui.AccentColor);
                if (_siegeList == null)
                {
                    panel.Line("Scouting the hills...");
                    return;
                }
                foreach (var level in _siegeList)
                {
                    var chosen = level.level;
                    var reward = level.bestStars >= 3 ? "All stars won." : $"{level.goldPerStar:N0} gold, {level.manaPerStar:N0} Mana per new star" + (level.bestStars == 0 && level.firstClearCrystals > 0 ? $", {level.firstClearCrystals:N0} gems first time" : "");
                    var caption = level.open
                        ? $"{level.level}. {level.name}   {Stars(level.bestStars)}   {level.attackers} attackers. {reward}"
                        : $"{level.level}. {level.name}   Locked: hold level {level.level - 1} first";
                    panel.Action("SiegeLevel" + level.level, caption, () => FightSiege(chosen), level.open, level.open && level.bestStars == 0 ? Ui.AttackColor : (Color?)null, 18);
                }
            });
        }

        private static string Stars(int count) => new string('*', count) + new string('-', 3 - Mathf.Clamp(count, 0, 3));

        public void FightSiege(int level)
        {
            if (!ModesOnline || _raid != null) return;
            var key = KeyFor($"siege-fight|{level}|{System.DateTime.UtcNow.Ticks / System.TimeSpan.TicksPerSecond}");
            Run(async () =>
            {
                var fight = await Modes.SiegeFightAsync(key, level);
                ClearKey();
                LastMessage = null;
                PlayModeFight(fight);
            }, ShowSiege);
        }

        // ---------- Arena ----------

        public void OpenArena()
        {
            if (!ModesOnline || _raid != null) return;
            LastMessage = null;
            _arenaBoard = null;
            ShowArena();
            Fetch(async () =>
            {
                _arenaBoard = await Modes.ArenaBoardAsync();
                if (UiState.Screen == VillageScreen.AttackFlow && _raid == null) ShowArena();
            }, ShowBattleHub);
        }

        private void ShowArena()
        {
            var board = _arenaBoard;
            _panel.ShowScreen("Arena", board == null ? "" : $"{board.points:N0} points   Rank #{board.rank:N0}", ShowBattleHub, panel =>
            {
                if (!string.IsNullOrEmpty(LastMessage)) panel.Line(LastMessage, 20, Ui.AccentColor);
                panel.Line("Your hero charges the shrine of another hero, who guards it. Topple the shrine to win. Defending costs nothing: only your own duels move your points.", 19);
                if (board == null)
                {
                    panel.Line("Finding the Arena...");
                    return;
                }
                panel.Line($"Wins {board.wins:N0}   Losses {board.losses:N0}", 22, new Color(1f, 0.9f, 0.66f));
                panel.Line(board.paidWinsLeft > 0
                    ? $"Each win today pays {board.winGold:N0} gold and {board.winMana:N0} Mana ({board.paidWinsLeft} paid wins left today)."
                    : "Today's paid wins are used up. Wins still earn points.", 19, Ui.AccentColor);
                panel.Action("ArenaFightButton", "Fight!", FightArena, true, Ui.AttackColor, 26);
                panel.Action("ArenaRankingsButton", "Rankings", ShowRankingsFromArena, true, null, 20);
            });
        }

        public void FightArena()
        {
            if (!ModesOnline || _raid != null) return;
            var key = KeyFor($"arena-fight|{System.DateTime.UtcNow.Ticks / System.TimeSpan.TicksPerSecond}");
            Run(async () =>
            {
                var fight = await Modes.ArenaFightAsync(key);
                ClearKey();
                LastMessage = null;
                PlayModeFight(fight);
            }, ShowArena);
        }

        // ---------- Playing a fought battle back ----------

        private void PlayModeFight(ModeFight fight)
        {
            if (_raid != null) return;
            _modeFight = fight;
            LastModeFight = fight;
            var stageObject = new GameObject("BattleStage");
            stageObject.transform.SetParent(_worldRoot, false);
            var stage = stageObject.AddComponent<BattleStage>();
            stage.Initialize(fight.Session, Village.Catalog, TroopData, _camera.Camera, HeroLookFor, DefenderLook);
            stage.Finished += ShowRaidResult;
            _raid = new RaidRun { Session = fight.Session, Stage = stage };
            _raid.Placements.AddRange(fight.Placements);
            _raid.Stacks.AddRange(fight.Troops);
            foreach (var placement in fight.Placements) stage.ShowPlacement(placement);
            _view.SetVisible(false);
            if (_life != null) _life.gameObject.SetActive(false);
            BeginPlayback(new RaidOutcome { Result = fight.Result, State = fight.State, GoldEarned = fight.GoldEarned, CrystalsEarned = fight.CrystalsEarned });
            var banner = fight.IsSiege ? "DEFEND!" : $"VS {fight.OpponentName.ToUpperInvariant()}";
            var label = Ui.Strong(Ui.Label(_raid.StatusBar.transform.parent, banner, 54, TextAnchor.MiddleCenter, new Color(1f, 0.82f, 0.35f)));
            Ui.Anchor(label.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(900f, 90f), new Vector2(0f, 20f));
            UiMotion.PopIn(label.transform, 0.5f, 0.3f, 0.4f);
            Destroy(label.gameObject, 2.2f);
        }

        /// <summary>The Siege and Arena result, in place of the raid one.</summary>
        private void ShowModeResult()
        {
            var fight = _modeFight;
            var result = fight.Result;
            if (fight.State != null) ApplyServerState(fight.State);
            GameSounds.Play(fight.Won ? GameSound.Victory : GameSound.Defeat);
            var title = fight.IsSiege ? (fight.Won ? "Village held!" : "The village fell") : fight.IsTrial ? (fight.Won ? $"Floor {fight.Level} cleared!" : "The guardian stands") : (fight.Won ? "Victory" : "Defeat");
            _panel.Show(title, FinishModeFight, panel =>
            {
                DrawResultStars(panel, fight.Stars);
                if (fight.IsSiege)
                {
                    panel.Line($"Siege {fight.Level}: {fight.Title}", 24, new Color(1f, 0.9f, 0.66f));
                    panel.Line($"{result.damagePercent}% of your village was damaged. " + (result.dojoDestroyed ? "The Town Hall fell." : "The Town Hall stands."));
                    var landed = fight.Troops.Sum(s => s.count);
                    panel.Line($"Attackers beaten: {landed - result.heroesAlive} of {landed}");
                }
                else if (fight.IsTrial)
                {
                    panel.Line($"{fight.Title}: {fight.OpponentName}", 24, new Color(1f, 0.9f, 0.66f));
                    panel.Line(result.dojoDestroyed ? "The guardian's shrine fell." : "The guardian's shrine still stands. Strengthen your hero and try again.");
                }
                else
                {
                    panel.Line($"Against {fight.OpponentName} ({fight.OpponentPoints:N0} points)", 24, new Color(1f, 0.9f, 0.66f));
                    panel.Line(result.dojoDestroyed ? "Their shrine fell." : "Their shrine still stands.");
                    var sign = fight.PointsDelta >= 0 ? "+" : "";
                    var points = panel.Line($"{sign}{fight.PointsDelta} points.   Now {fight.Points:N0}, rank #{fight.Rank:N0}", 24, fight.PointsDelta >= 0 ? Ui.AccentColor : new Color(1f, 0.55f, 0.45f));
                    UiMotion.Punch(points.transform, 0.25f, 0.45f);
                }
                var delay = 1.0f;
                void Reveal(Text line)
                {
                    UiMotion.FadeIn(line.gameObject, 0.3f, delay);
                    delay += 0.25f;
                }
                if (fight.GoldEarned > 0) Reveal(panel.Line($"Earned {fight.GoldEarned:N0} gold", 21, Ui.AccentColor));
                if (fight.ManaEarned > 0) Reveal(panel.Line($"Earned {fight.ManaEarned:N0} Mana", 21, Ui.AccentColor));
                if (fight.CrystalsEarned > 0) Reveal(panel.Line($"Earned {fight.CrystalsEarned:N0} gems", 21, Ui.AccentColor));
                if (fight.GoldEarned + fight.ManaEarned + fight.CrystalsEarned == 0)
                    panel.Line(fight.IsSiege || fight.IsTrial ? (fight.Won ? "No reward: only the first clear pays." : "No reward this time.") : (fight.Won ? "No reward: today's paid wins are used up, or your vault is full." : "No reward this time."), 19);
                if (fight.IsTrial)
                    panel.Action("TrialAgainButton", fight.Won ? "Back to the tower" : "Try again", () => { EndRaid(); OpenTrials(); }, true, Ui.AccentColor);
                else                 if (fight.IsSiege)
                    panel.Action("SiegeAgainButton", fight.Won ? "Back to the Siege" : "Try again", () => { EndRaid(); OpenSiege(); }, true, Ui.AccentColor);
                else
                    panel.Action("ArenaAgainButton", "Fight again", () => { EndRaid(); OpenArena(); }, true, Ui.AccentColor);
                panel.Action("ReturnHomeButton", "Return to village", FinishModeFight);
            });
        }

        public void FinishModeFight()
        {
            EndRaid();
            CloseAll();
        }

        // ---------- Rankings, quests, the gift and mail: the home screen's round shortcuts ----------

        private bool RefuseShortcutOffline(string title)
        {
            if (ModesOnline) return false;
            _panel.ShowWindow(title, new Vector2(640f, 300f), CloseAll, content =>
            {
                var text = HudArt.Label(content, $"{title} needs the server.", HudArt.Numbers, 24, TextAnchor.MiddleCenter, HudArt.Ivory, HudArt.LacquerDeep);
                Ui.Stretch(text.rectTransform);
            });
            return true;
        }

        private void ShowRankingsFromArena() => OpenRankings(ShowArena);

        public void OpenRankings() => OpenRankings(CloseAll);

        private void OpenRankings(System.Action back)
        {
            if (RefuseShortcutOffline("Rankings")) return;
            Fetch(async () =>
            {
                _arenaBoard = await Modes.ArenaBoardAsync();
                ShowRankings(back);
            }, () => ShowRankings(back));
        }

        private void ShowRankings(System.Action back)
        {
            var board = _arenaBoard;
            _panel.ShowScreen("Rankings", board == null ? "" : $"You: rank #{board.rank:N0}, {board.points:N0} points", back, panel =>
            {
                if (!string.IsNullOrEmpty(LastMessage)) panel.Line(LastMessage, 20, Ui.AccentColor);
                panel.Line("The Arena's best. Points rise with each duel won.", 19);
                if (board == null) return;
                if (board.top.Count == 0) panel.Line("No one has fought in the Arena yet. Be the first.", 21);
                foreach (var entry in board.top)
                {
                    var hero = !string.IsNullOrEmpty(entry.archetypeId) && Roster.Catalog.TryGet(entry.archetypeId, out var archetype) ? $"{archetype.displayName}, level {entry.level}" : "no hero on guard";
                    var line = panel.Line($"#{entry.rank}   {entry.name}   {entry.points:N0} points   ({hero})", entry.isMe ? 23 : 20, entry.isMe ? Ui.AccentColor : (entry.rank <= 3 ? new Color(1f, 0.87f, 0.45f) : (Color?)null));
                    if (entry.isMe) UiMotion.Shine(line, Color.white, 2.4f, 0.3f);
                }
            });
        }

        public void OpenQuests()
        {
            if (RefuseShortcutOffline("Quests")) return;
            Fetch(async () =>
            {
                _questBoard = await Modes.QuestBoardAsync();
                ShowQuests();
            }, ShowQuests);
        }

        private void ShowQuests()
        {
            var board = _questBoard;
            var done = board?.Quests.Count(q => q.claimed) ?? 0;
            _panel.ShowScreen("Daily Quests", board == null ? "" : $"{done} of {board.Quests.Count} claimed today", CloseAll, panel =>
            {
                if (!string.IsNullOrEmpty(LastMessage)) panel.Line(LastMessage, 20, Ui.AccentColor);
                panel.Line("New quests every day at midnight UTC. Each pays gems.", 19);
                if (board == null) return;
                foreach (var quest in board.Quests)
                {
                    var id = quest.id;
                    var progress = $"{Mathf.Min(quest.progress, quest.target)}/{quest.target}";
                    if (quest.claimed)
                        panel.Action("Quest_" + id, $"{quest.title}   {progress}   Claimed", null, false, null, 19);
                    else if (quest.Done)
                    {
                        var button = panel.Action("Quest_" + id, $"{quest.title}   {progress}   Claim {quest.crystals:N0} gems", () => ClaimQuest(id), true, Ui.AccentColor, 19);
                        UiMotion.Pulse(button.transform, 0.04f, 1.2f);
                    }
                    else
                        panel.Action("Quest_" + id, $"{quest.title}   {progress}   {quest.crystals:N0} gems", null, false, null, 19);
                }
            });
        }

        public void ClaimQuest(string questId)
        {
            if (!ModesOnline) return;
            var key = KeyFor($"quest-claim|{questId}|{System.DateTime.UtcNow:yyyyMMdd}");
            Run(async () =>
            {
                var board = await Modes.QuestClaimAsync(key, questId);
                ClearKey();
                LastMessage = null;
                _questBoard = board;
                if (board.State != null) ApplyServerState(board.State);
                GameSounds.Play(GameSound.Star);
                ShowQuests();
            }, ShowQuests);
        }

        public void OpenGift()
        {
            if (RefuseShortcutOffline("Daily Gift")) return;
            Fetch(async () =>
            {
                _questBoard = await Modes.QuestBoardAsync();
                ShowGift();
            }, ShowGift);
        }

        private void ShowGift()
        {
            var board = _questBoard;
            _panel.ShowWindow("Daily Gift", new Vector2(640f, 360f), CloseAll, content =>
            {
                var text = board == null ? (LastMessage ?? "The gift cannot be reached right now.")
                    : board.GiftReady ? $"A gift for logging in today:\n{board.GiftGold:N0} gold, {board.GiftMana:N0} Mana and {board.GiftCrystals:N0} gems."
                    : "Today's gift is opened. A new one arrives at midnight UTC.";
                var label = HudArt.Label(content, text, HudArt.Numbers, 24, TextAnchor.MiddleCenter, HudArt.Ivory, HudArt.LacquerDeep);
                Ui.Anchor(label.rectTransform, new Vector2(0.5f, 1f), new Vector2(520f, 150f), new Vector2(0f, -10f));
                if (board == null || !board.GiftReady) return;
                var open = Ui.Button(content, "OpenGiftButton", "Open", OpenGiftNow, true, Ui.AccentColor, 26);
                Ui.Anchor((RectTransform)open.transform, new Vector2(0.5f, 0f), new Vector2(220f, 64f), new Vector2(0f, 6f));
                UiMotion.Pulse(open.transform, 0.06f, 1.1f);
            });
        }

        public void OpenGiftNow()
        {
            if (!ModesOnline) return;
            var key = KeyFor($"gift-open|{System.DateTime.UtcNow:yyyyMMdd}");
            Run(async () =>
            {
                var board = await Modes.GiftOpenAsync(key);
                ClearKey();
                LastMessage = null;
                _questBoard = board;
                if (board.State != null) ApplyServerState(board.State);
                GameSounds.Play(GameSound.Victory);
                ShowGift();
            }, ShowGift);
        }

        /// <summary>When this player's raid shield ends (Unix ms), from the server; 0 when unshielded (ADR-049).</summary>
        private long _shieldUntilMs;

        /// <summary>
        /// Mail is the log of raids on the village, newest first (ADR-049): who attacked, what they took, and a
        /// Revenge button that works once per attack. The shield's time left is shown at the top.
        /// </summary>
        public void OpenMail()
        {
            if (RefuseShortcutOffline("Mail")) return;
            Fetch(async () =>
            {
                _defenseLog = await _server.DefenseLogAsync();
                ShowMail();
            }, ShowMail);
        }

        private void ShowMail()
        {
            var log = _defenseLog;
            _panel.ShowScreen("Mail", log == null ? "" : $"{log.Count} reports", CloseAll, panel =>
            {
                if (!string.IsNullOrEmpty(LastMessage)) panel.Line(LastMessage, 20, Ui.AccentColor);
                var shieldLeft = System.DateTimeOffset.FromUnixTimeMilliseconds(_shieldUntilMs) - System.DateTimeOffset.UtcNow;
                if (_shieldUntilMs > 0 && shieldLeft > System.TimeSpan.Zero)
                    panel.Line($"Shield: no one can attack you for {(int)shieldLeft.TotalHours}h {shieldLeft.Minutes}m. Attacking someone ends it.", 20, Ui.AccentColor);
                if (log == null) return;
                if (log.Count == 0) panel.Line("No one has raided your village yet.", 21);
                foreach (var record in log)
                {
                    var when = System.DateTimeOffset.FromUnixTimeMilliseconds(record.atMs).LocalDateTime;
                    var who = string.IsNullOrEmpty(record.attackerName) ? "A raider" : record.attackerName;
                    var verdict = record.stars == 0 ? "Your village held!" : $"{who} took {record.stars} of 3 stars.";
                    var lost = record.goldLost + record.manaLost > 0 ? $" You lost {record.goldLost:N0} gold and {record.manaLost:N0} Mana." : " Nothing was lost.";
                    panel.Line($"{when:MMM d, HH:mm}   {verdict}   {record.damagePercent}% damage, {record.defendersAlive} of {record.defendersTotal} guards standing.{lost}", 19, record.stars == 0 ? Ui.AccentColor : (Color?)null);
                    if (record.canRevenge)
                    {
                        var attackId = record.attackId;
                        panel.Action("Revenge_" + attackId, $"Revenge on {who}", () => Revenge(attackId), true, Ui.AttackColor, 19);
                    }
                }
            });
        }

        /// <summary>Raids whoever raided this village, once per attack, even through their shield. The server checks it.</summary>
        public void Revenge(string attackId)
        {
            if (_raid != null || !IsOnline) return;
            var key = KeyFor($"revenge|{attackId}");
            Run(async () =>
            {
                var session = await _server.RevengeAsync(key, attackId);
                ClearKey();
                LastMessage = null;
                CloseAll();
                EnterPrepare(session);
            }, ShowMail);
        }
    }
}
