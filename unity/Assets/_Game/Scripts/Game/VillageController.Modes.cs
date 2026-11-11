using System.Collections.Generic;
using System.Linq;
using ShinobiDominion.Core;
using UnityEngine;
using UnityEngine.UI;
using Text = ShinobiDominion.Game.GameText;

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
            /// <summary>The building picture that stands for the mode, and the colour of the light behind it.</summary>
            public string Picture;
            public Color Light;
            public bool Open;
            public System.Action Action;
        }

        private void ShowBattleHub()
        {
            var team = Village.Buildings.Count(b => b.AssignedHeroInstanceId != null);
            var dungeonStars = RaidData.All.Sum(r => BestStarsOn(r.id));
            _panel.ShowStage("Battle", null, null, null, false, 0f, CloseAll, panel =>
            {
                ScreenKit.Notice(panel, LastMessage);
                if (!IsOnline) panel.Line(AllowOfflinePlay ? "Practice battles only: not connected, so nothing pays." : "Battles need the server.", 20, Ui.AccentColor);
                if (IsOnline && _raidList == null)
                {
                    // Drawn again when the list arrives, so nothing is tapped on a menu about to be replaced.
                    panel.Line("Checking your battles...");
                    return;
                }
                var tiles = new[]
                {
                    new ModeTile { Button = "FindRivalButton", Title = "Raid", Blurb = "Loot a rival's village", Go = "Find a rival", Picture = "gold_vault_10", Light = new Color(1f, 0.74f, 0.3f), Open = IsOnline && team > 0, Action = FindRival },
                    new ModeTile { Button = "OpenDungeonsButton", Title = "Dungeons", Blurb = $"{dungeonStars} of {RaidData.All.Count * 3} stars", Go = "Open the map", Picture = "cannon_tower_10", Light = new Color(0.45f, 0.9f, 0.6f), Open = IsOnline || AllowOfflinePlay, Action = OpenCampaign },
                    new ModeTile { Button = "OpenSiegeButton", Title = "Village Siege", Blurb = "Defend your own village", Go = "Defend", Picture = "defense_tower_10", Light = new Color(1f, 0.5f, 0.4f), Open = ModesOnline, Action = OpenSiege },
                    new ModeTile { Button = "OpenArenaButton", Title = "Arena", Blurb = "Hero against hero", Go = "Enter", Picture = "arena_10", Light = new Color(1f, 0.85f, 0.45f), Open = ModesOnline && team > 0, Action = OpenArena },
                    new ModeTile { Button = "OpenTrialsButton", Title = "Trial Tower", Blurb = "Climb past the guardians", Go = "Climb", Picture = "spirit_turret_10", Light = new Color(0.75f, 0.55f, 1f), Open = ModesOnline && team > 0, Action = OpenTrials }
                };
                var block = panel.Block(536f);
                for (var i = 0; i < tiles.Length; i++) DrawModeTile(block, tiles[i], i, tiles.Length);
            }, team == 0 ? "Raids, the Arena and the Trial Tower need a hero on your Hero Base." : "Your hero leads every battle.");
        }

        /// <summary>
        /// One way to fight, as a framed card: the mode's building lit from behind, its name, a few words and one
        /// button. A mode that cannot be entered yet is drawn back and carries a lock.
        /// </summary>
        private static void DrawModeTile(RectTransform parent, ModeTile tile, int index, int count)
        {
            var rect = Ui.Rect("ModeTile" + index, parent);
            var width = 1f / count;
            rect.anchorMin = new Vector2(index * width, 0f);
            rect.anchorMax = new Vector2((index + 1) * width, 1f);
            rect.offsetMin = new Vector2(7f, 0f);
            rect.offsetMax = new Vector2(-7f, 0f);
            HudArt.DropShadow(rect, 6f, 10f, 0.5f);
            var card = rect.gameObject.AddComponent<Image>();
            card.sprite = HudArt.Sprite("card_s34");
            card.type = Image.Type.Sliced;
            card.pixelsPerUnitMultiplier = 1.2f;
            card.raycastTarget = false;
            if (!tile.Open) card.color = new Color(0.62f, 0.62f, 0.68f);

            // The picture stands in a dark well, with a soft light of the mode's own colour behind it.
            var well = Ui.Rect("Well", rect);
            well.anchorMin = new Vector2(0f, 0.3f);
            well.anchorMax = new Vector2(1f, 1f);
            well.offsetMin = new Vector2(18f, 0f);
            well.offsetMax = new Vector2(-18f, -66f);
            var wellImage = well.gameObject.AddComponent<Image>();
            wellImage.sprite = HudArt.Sprite("well_s18");
            wellImage.type = Image.Type.Sliced;
            wellImage.raycastTarget = false;
            var light = HudArt.Picture(well, "Light", "glow", new Vector2(0.5f, 0.5f), new Vector2(330f, 330f), Vector2.zero);
            light.color = tile.Open ? new Color(tile.Light.r, tile.Light.g, tile.Light.b, 1f) : new Color(0.5f, 0.5f, 0.55f, 0.3f);
            var art = Ui.Rect("Art", well);
            Ui.Stretch(art, 10f, 10f, 10f, 10f);
            var picture = art.gameObject.AddComponent<Image>();
            picture.sprite = Resources.Load<Sprite>("Buildings/" + tile.Picture);
            picture.preserveAspect = true;
            picture.raycastTarget = false;
            picture.color = tile.Open ? Color.white : new Color(0.42f, 0.42f, 0.47f);
            if (!tile.Open)
            {
                var locked = HudArt.Picture(well, "Lock", "icon_lock", new Vector2(0.5f, 0.5f), new Vector2(64f, 64f), Vector2.zero);
                locked.preserveAspect = true;
            }

            var title = HudArt.Label(rect, tile.Title, HudArt.Display, 27, TextAnchor.MiddleCenter, tile.Open ? HudArt.BrassLight : HudArt.IvoryMuted, HudArt.LacquerDeep);
            title.rectTransform.anchorMin = new Vector2(0f, 1f);
            title.rectTransform.anchorMax = new Vector2(1f, 1f);
            title.rectTransform.pivot = new Vector2(0.5f, 1f);
            title.rectTransform.sizeDelta = new Vector2(-36f, 44f);
            title.rectTransform.anchoredPosition = new Vector2(0f, -16f);
            title.horizontalOverflow = HorizontalWrapMode.Overflow;
            title.verticalOverflow = VerticalWrapMode.Truncate;
            title.resizeTextForBestFit = true;
            title.resizeTextMinSize = 16;
            title.resizeTextMaxSize = 27;

            var blurb = HudArt.Label(rect, tile.Blurb, HudArt.Numbers, 17, TextAnchor.MiddleCenter, tile.Open ? HudArt.Ivory : HudArt.IvoryMuted, HudArt.LacquerDeep);
            blurb.name = "Blurb";
            blurb.rectTransform.anchorMin = new Vector2(0f, 0.3f);
            blurb.rectTransform.anchorMax = new Vector2(1f, 0.3f);
            blurb.rectTransform.pivot = new Vector2(0.5f, 1f);
            blurb.rectTransform.sizeDelta = new Vector2(-36f, 56f);
            blurb.rectTransform.anchoredPosition = new Vector2(0f, -8f);
            blurb.horizontalOverflow = HorizontalWrapMode.Wrap;
            blurb.verticalOverflow = VerticalWrapMode.Truncate;
            blurb.resizeTextForBestFit = true;
            blurb.resizeTextMinSize = 13;
            blurb.resizeTextMaxSize = 17;

            var go = Ui.Button(rect, tile.Button, tile.Go, tile.Action, tile.Open, tile.Open ? Ui.AttackColor : (Color?)null, 20);
            Ui.Anchor((RectTransform)go.transform, new Vector2(0.5f, 0f), new Vector2(176f, 56f), new Vector2(0f, 22f));
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
            _panel.ShowStage("Village Siege", _siegeList == null ? "" : $"{stars} / {total} Stars", "icon_star", "scene_castle", false, 236f, ShowBattleHub, panel =>
            {
                ScreenKit.Notice(panel, LastMessage);
                panel.Line("Rogue bands storm your own village. Your towers, walls and the heroes on their bases hold them off. Nothing in your village is lost, win or lose.", 18, HudArt.Ivory);
                panel.Line("Three stars: under 10% damage      Two stars: under 30% damage      New stars pay once", 17, Ui.AccentColor);
                if (_siegeList == null)
                {
                    panel.Line("Scouting the hills...");
                    return;
                }
                foreach (var level in _siegeList)
                {
                    var chosen = level.level;
                    var name = "SiegeLevel" + level.level;
                    ScreenKit.Row row;
                    if (level.open)
                    {
                        row = ScreenKit.AddRow(panel, "SiegeRow" + level.level, level.level.ToString(), level.name, $"{level.attackers} attackers", true);
                        row.Button(name, "Fight", () => FightSiege(chosen), true, Ui.AttackColor);
                    }
                    else
                    {
                        row = ScreenKit.AddRow(panel, "SiegeRow" + level.level, level.level.ToString(), level.name, $"Locked: hold level {level.level - 1} first");
                        row.Locked(name);
                    }
                    // What each new star pays, and what the first win adds.
                    if (level.bestStars >= 3) row.Note("All stars won");
                    else
                    {
                        if (level.bestStars == 0 && level.firstClearCrystals > 0) row.Reward("icon_crystals", level.firstClearCrystals.ToString("N0"));
                        row.Reward("icon_chakra", level.manaPerStar.ToString("N0"));
                        row.Reward("icon_gold", level.goldPerStar.ToString("N0"));
                    }
                    row.Stars(level.bestStars);
                    if (!level.open) row.Dim();
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
            _panel.ShowStage("Arena", board == null ? "" : $"{board.points:N0} points    Rank #{board.rank:N0}", "icon_rank", "scene_arena", true, 560f, ShowBattleHub, panel =>
            {
                ScreenKit.Notice(panel, LastMessage);
                if (board == null)
                {
                    panel.Line("Finding the Arena...");
                    return;
                }
                panel.Line("Your hero charges the shrine of another hero, who guards it. Topple the shrine to win. Defending costs nothing: only your own duels move your points.", 18, HudArt.Ivory);
                var paid = board.paidWinsLeft > 0;
                var record = ScreenKit.AddRow(panel, "ArenaRecordRow", "icon_victory", $"Wins {board.wins:N0}      Losses {board.losses:N0}",
                    paid ? $"A win today pays this. {board.paidWinsLeft} paid wins left." : "Today's paid wins are used up. Wins still earn points.", true, 94f);
                if (paid)
                {
                    record.Reward("icon_chakra", board.winMana.ToString("N0"));
                    record.Reward("icon_gold", board.winGold.ToString("N0"));
                }
                // Who fights for the player here: the hero on the first Hero Base.
                var guardId = Village.Buildings.Where(b => b.AssignedHeroInstanceId != null).OrderBy(b => b.Id).Select(b => b.AssignedHeroInstanceId).FirstOrDefault();
                var guard = guardId == null ? null : Roster.Find(guardId);
                if (guard != null)
                    ScreenKit.AddRow(panel, "ArenaHeroRow", "icon_hero", Roster.ArchetypeOf(guard).displayName, $"Your hero in the Arena, level {guard.Level}");
                panel.Block(14f);
                ScreenKit.Feature(panel, "ArenaFightButton", "FIGHT", FightArena, true, Ui.AttackColor, 520f, 92f, "icon_attack");
                panel.Block(4f);
                ScreenKit.Feature(panel, "ArenaRankingsButton", "RANKINGS", ShowRankingsFromArena, true, null, 520f, 74f, "icon_trophy");
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
            var plaque = fight.IsSiege ? "Village Siege" : fight.IsTrial ? "Trial Tower" : "Arena Result";
            var outcome = fight.IsSiege ? (fight.Won ? "Village Held" : "Village Fell") : fight.IsTrial ? (fight.Won ? "Floor Cleared" : "Defeat") : (fight.Won ? "Victory" : "Defeat");
            _panel.ShowWindow(plaque, new Vector2(820f, 540f), FinishModeFight, content =>
            {
                var word = HudArt.Label(content, outcome.ToUpperInvariant(), HudArt.Display, 58, TextAnchor.MiddleCenter,
                    fight.Won ? new Color(1f, 0.86f, 0.42f) : HudArt.CrimsonLight, HudArt.LacquerDeep);
                word.name = "Outcome";
                word.fontStyle = FontStyle.BoldAndItalic;
                Ui.Anchor(word.rectTransform, new Vector2(0.5f, 1f), new Vector2(700f, 74f), new Vector2(0f, 8f));
                UiMotion.PopIn(word.transform, 0.05f, 0.4f, 0.4f);

                DrawWindowStars(content, fight.Stars, -104f);

                var subject = fight.IsSiege ? $"Siege {fight.Level}: {fight.Title}" : fight.IsTrial ? $"{fight.Title}: {fight.OpponentName}" : $"Against {fight.OpponentName}";
                var against = HudArt.Label(content, subject, HudArt.Display, 24, TextAnchor.MiddleCenter, HudArt.BrassLight, HudArt.LacquerDeep);
                against.name = "Subject";
                // A rival's name is something a player typed: shown as written, never obeyed.
                against.supportRichText = false;
                Ui.Anchor(against.rectTransform, new Vector2(0.5f, 1f), new Vector2(690f, 34f), new Vector2(0f, -150f));

                var strip = ScreenKit.Parchment(content, new Vector2(0.5f, 1f), new Vector2(690f, 74f), new Vector2(0f, -192f));
                if (fight.IsSiege)
                {
                    var landed = fight.Troops.Sum(s => s.count);
                    ScreenKit.Fact(strip, 0f, 0.5f, "icon_shield", "Village damaged", $"{result.damagePercent}%");
                    ScreenKit.Fact(strip, 0.5f, 1f, "icon_troops", "Attackers beaten", $"{landed - result.heroesAlive} of {landed}");
                }
                else if (fight.IsTrial)
                    ScreenKit.Fact(strip, 0f, 1f, "icon_gate", "The guardian's shrine", result.dojoDestroyed ? "Fell" : "Still stands");
                else
                {
                    var sign = fight.PointsDelta >= 0 ? "+" : "";
                    ScreenKit.Fact(strip, 0f, 0.5f, "icon_trophy", "Arena points", $"{sign}{fight.PointsDelta}", fight.PointsDelta >= 0 ? ScreenKit.InkBrown : new Color(0.62f, 0.12f, 0.08f));
                    ScreenKit.Fact(strip, 0.5f, 1f, "icon_stats", "Current rank", $"#{fight.Rank:N0}");
                }

                // What the fight paid, as chips in a row, or one line saying why it paid nothing.
                var earned = new List<(string icon, long amount)>();
                if (fight.GoldEarned > 0) earned.Add(("icon_gold", fight.GoldEarned));
                if (fight.ManaEarned > 0) earned.Add(("icon_chakra", fight.ManaEarned));
                if (fight.CrystalsEarned > 0) earned.Add(("icon_crystals", fight.CrystalsEarned));
                var rewards = Ui.Rect("Rewards", content);
                Ui.Anchor(rewards, new Vector2(0.5f, 1f), new Vector2(690f, 44f), new Vector2(0f, -280f));
                if (earned.Count == 0)
                {
                    var none = fight.IsSiege || fight.IsTrial ? (fight.Won ? "No reward: only the first clear pays." : "No reward this time.") : (fight.Won ? "No reward: today's paid wins are used up, or your vault is full." : "No reward this time.");
                    var note = Ui.Label(rewards, none, 18, TextAnchor.MiddleCenter, HudArt.IvoryMuted);
                    note.name = "NoReward";
                    Ui.Stretch(note.rectTransform);
                }
                else
                {
                    const float gap = 12f;
                    var widths = earned.Select(e => 62f + e.amount.ToString("N0").Length * 11f).ToList();
                    var x = -(widths.Sum() + gap * (earned.Count - 1)) * 0.5f;
                    for (var i = 0; i < earned.Count; i++)
                    {
                        var chip = Ui.Rect("Earned_" + earned[i].icon, rewards);
                        Ui.Anchor(chip, new Vector2(0.5f, 0.5f), new Vector2(widths[i], 42f), new Vector2(x + widths[i] * 0.5f, 0f));
                        ScreenKit.Chip(chip, earned[i].icon, earned[i].amount.ToString("N0"));
                        UiMotion.PopIn(chip, 0.9f + i * 0.2f, 0.3f, 0.3f);
                        x += widths[i] + gap;
                    }
                }

                Button again;
                if (fight.IsTrial)
                    again = Ui.Button(content, "TrialAgainButton", fight.Won ? "Back to the tower" : "Try again", () => { EndRaid(); OpenTrials(); }, true, Ui.AttackColor, 22);
                else if (fight.IsSiege)
                    again = Ui.Button(content, "SiegeAgainButton", fight.Won ? "Back to the Siege" : "Try again", () => { EndRaid(); OpenSiege(); }, true, Ui.AttackColor, 22);
                else
                    again = Ui.Button(content, "ArenaAgainButton", "Fight again", () => { EndRaid(); OpenArena(); }, true, Ui.AttackColor, 22);
                Ui.Anchor((RectTransform)again.transform, new Vector2(0.5f, 0f), new Vector2(310f, 62f), new Vector2(-166f, 0f));
                var home = Ui.Button(content, "ReturnHomeButton", "Return to village", FinishModeFight, true, null, 22);
                Ui.Anchor((RectTransform)home.transform, new Vector2(0.5f, 0f), new Vector2(310f, 62f), new Vector2(166f, 0f));
            });
        }

        /// <summary>The three stars of a result window: the middle one a little taller, the earned ones landing one at a time.</summary>
        private static void DrawWindowStars(RectTransform parent, int stars, float y)
        {
            for (var i = 0; i < 3; i++)
            {
                var size = i == 1 ? 66f : 54f;
                var icon = Ui.Icon(parent, HudArt.Sprite("icon_star"), new Vector2(0.5f, 1f), new Vector2(size, size), new Vector2((i - 1) * 74f, y + (i == 1 ? 6f : 0f)));
                icon.rectTransform.pivot = new Vector2(0.5f, 0.5f);
                icon.name = "ResultStar" + (i + 1);
                icon.preserveAspect = true;
                if (i >= stars)
                {
                    icon.color = UnlitStar;
                    continue;
                }
                UiMotion.PopIn(icon.transform, 0.2f + i * 0.28f, 0.1f, 0.42f);
                UiMotion.Shine(icon, new Color(1f, 0.95f, 0.75f, 0.9f), 2.6f, 0.35f);
            }
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
            _panel.ShowStage("Rankings", board == null ? "" : $"Your rank #{board.rank:N0}    {board.points:N0} points", "icon_trophy", "scene_lantern", false, 230f, back, panel =>
            {
                ScreenKit.Notice(panel, LastMessage);
                if (board == null) return;
                if (board.top.Count == 0)
                {
                    panel.Line("No one has fought in the Arena yet.", 21);
                    return;
                }
                // The four columns, at the same places in the heading and in every row.
                const float nameFrom = 0.12f, pointsFrom = 0.42f, heroFrom = 0.62f;
                var heading = panel.Block(38f);
                heading.name = "RankHeading";
                var band = heading.gameObject.AddComponent<Image>();
                band.sprite = HudArt.Sprite("btn_crimson_s24");
                band.type = Image.Type.Sliced;
                band.pixelsPerUnitMultiplier = 2.2f;
                band.raycastTarget = false;
                RankCell(heading, 0f, nameFrom, "Rank", HudArt.Display, 18, TextAnchor.MiddleCenter, HudArt.Ivory, HudArt.CrimsonDark);
                RankCell(heading, nameFrom, pointsFrom, "Player", HudArt.Display, 18, TextAnchor.MiddleLeft, HudArt.Ivory, HudArt.CrimsonDark);
                RankCell(heading, pointsFrom, heroFrom, "Points", HudArt.Display, 18, TextAnchor.MiddleLeft, HudArt.Ivory, HudArt.CrimsonDark);
                RankCell(heading, heroFrom, 1f, "Featured Hero", HudArt.Display, 18, TextAnchor.MiddleLeft, HudArt.Ivory, HudArt.CrimsonDark);

                var place = 0;
                foreach (var entry in board.top)
                {
                    place++;
                    var podium = place <= 3;
                    // The first three lie on gold, silver and bronze; the player's own row on plain parchment.
                    var light = podium || entry.isMe;
                    var row = panel.Block(podium ? 62f : 52f);
                    row.name = "RankRow" + place;
                    var plate = row.gameObject.AddComponent<Image>();
                    plate.sprite = HudArt.Sprite(light ? "parchment_s40" : "plate_s30");
                    plate.type = Image.Type.Sliced;
                    plate.pixelsPerUnitMultiplier = light ? 1.9f : 1.7f;
                    plate.raycastTarget = false;
                    if (podium) plate.color = place == 1 ? new Color(1f, 0.84f, 0.42f) : place == 2 ? new Color(0.82f, 0.86f, 0.93f) : new Color(0.93f, 0.66f, 0.45f);
                    var ink = light ? ScreenKit.InkBrown : HudArt.Ivory;
                    var edge = light ? ScreenKit.ParchmentEdge : HudArt.LacquerDeep;

                    if (podium)
                    {
                        var medal = HudArt.Picture(row, "Medal", "medal_face_gold", new Vector2(nameFrom * 0.5f, 0.5f), new Vector2(50f, 50f), Vector2.zero);
                        medal.preserveAspect = true;
                        medal.color = plate.color;
                        var number = HudArt.Label(medal.transform, entry.rank.ToString(), HudArt.Display, 26, TextAnchor.MiddleCenter, ScreenKit.InkBrown, ScreenKit.ParchmentEdge);
                        Ui.Stretch(number.rectTransform, 4f, 4f, 4f, 4f);
                    }
                    else RankCell(row, 0f, nameFrom, $"#{entry.rank}", HudArt.Display, 21, TextAnchor.MiddleCenter, ink, edge);

                    var name = RankCell(row, nameFrom, pointsFrom, entry.name, HudArt.Display, podium ? 22 : 20, TextAnchor.MiddleLeft, ink, edge);
                    // A name is something a player typed: shown as written, never obeyed.
                    name.supportRichText = false;
                    if (entry.isMe) UiMotion.Shine(name, Color.white, 2.4f, 0.3f);

                    var trophy = HudArt.Picture(row, "Trophy", "icon_trophy", new Vector2(pointsFrom, 0.5f), new Vector2(26f, 26f), Vector2.zero);
                    trophy.rectTransform.pivot = new Vector2(0f, 0.5f);
                    trophy.preserveAspect = true;
                    var points = RankCell(row, pointsFrom, heroFrom, $"{entry.points:N0} points", HudArt.Numbers, 19, TextAnchor.MiddleLeft, ink, edge);
                    points.rectTransform.offsetMin = new Vector2(34f, 0f);

                    var known = !string.IsNullOrEmpty(entry.archetypeId) && Roster.Catalog.TryGet(entry.archetypeId, out var archetype);
                    var hero = known ? Roster.Catalog.Get(entry.archetypeId).displayName : "No hero on guard";
                    if (known && HeroConcepts.Has(entry.archetypeId))
                    {
                        // The hero's painting, in a small framed window.
                        var window = Ui.Rect("Portrait", row);
                        window.anchorMin = window.anchorMax = new Vector2(heroFrom, 0.5f);
                        window.pivot = new Vector2(0f, 0.5f);
                        window.sizeDelta = new Vector2(44f, podium ? 50f : 44f);
                        window.gameObject.AddComponent<RectMask2D>();
                        var painting = Ui.Rect("Painting", window);
                        Ui.Stretch(painting, 0f, -14f, 0f, -2f);
                        var picture = painting.gameObject.AddComponent<Image>();
                        picture.sprite = HeroConcepts.Card(entry.archetypeId);
                        picture.preserveAspect = false;
                        picture.raycastTarget = false;
                    }
                    else
                    {
                        var figure = HudArt.Picture(row, "Portrait", "icon_hero", new Vector2(heroFrom, 0.5f), new Vector2(34f, 34f), new Vector2(5f, 0f));
                        figure.rectTransform.pivot = new Vector2(0f, 0.5f);
                        figure.preserveAspect = true;
                        if (light) figure.color = ScreenKit.InkBrown;
                    }
                    var who = RankCell(row, heroFrom, 1f, known ? $"{hero}   Lv. {entry.level}" : hero, HudArt.Numbers, 18, TextAnchor.MiddleLeft, ink, edge);
                    who.rectTransform.offsetMin = new Vector2(54f, 0f);
                }
            }, "The Arena's best. Points rise with each duel won.");
        }

        /// <summary>One cell of the rankings table, between two shares of the row's width.</summary>
        private static Text RankCell(RectTransform row, float from, float to, string words, Font font, int size, TextAnchor anchor, Color color, Color edge)
        {
            var label = HudArt.Label(row, words, font, size, anchor, color, edge);
            label.rectTransform.anchorMin = new Vector2(from, 0f);
            label.rectTransform.anchorMax = new Vector2(to, 1f);
            label.rectTransform.offsetMin = Vector2.zero;
            label.rectTransform.offsetMax = new Vector2(-8f, 0f);
            label.horizontalOverflow = HorizontalWrapMode.Overflow;
            label.verticalOverflow = VerticalWrapMode.Truncate;
            label.resizeTextForBestFit = true;
            label.resizeTextMinSize = 12;
            label.resizeTextMaxSize = size;
            return label;
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
            _panel.ShowStage("Daily Quests", board == null ? "" : $"Today's claims  {done} / {board.Quests.Count}", "icon_calendar", "scene_castle", false, 236f, CloseAll, panel =>
            {
                ScreenKit.Notice(panel, LastMessage);
                if (board == null) return;
                foreach (var quest in board.Quests)
                {
                    var id = quest.id;
                    var ready = quest.Done && !quest.claimed;
                    // Every quest still open lies on parchment; a claimed one is put away.
                    var row = ScreenKit.AddRow(panel, "QuestRow_" + id, QuestIcon(id), quest.title, null, !quest.claimed, 70f);
                    if (ready) row.Button("Quest_" + id, "Claim", () => ClaimQuest(id), true, Ui.AccentColor, 150f);
                    if (quest.claimed) row.Note("Claimed").Mark("icon_check").Dim();
                    else row.Reward("icon_crystals", quest.crystals.ToString("N0"));
                    row.Progress(quest.progress, quest.target);
                }
            }, "Refreshes daily at midnight UTC.");
        }

        /// <summary>The picture for a quest, by what it asks for.</summary>
        private static string QuestIcon(string id)
        {
            switch (id)
            {
                case "collect": return "icon_collect";
                case "train": return "icon_train";
                case "upgrade": return "icon_upgrade";
                case "raid_win": return "icon_battle";
                case "arena": return "icon_arena";
                case "siege": return "icon_shield";
                case "summon": return "icon_summon";
                default: return "icon_quests";
            }
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
            _panel.ShowWindow("Daily Gift", new Vector2(760f, 560f), CloseAll, content =>
            {
                var ready = board != null && board.GiftReady;
                // A warm light behind the gift, wider than the picture, so the picture has no edge of its own.
                var light = HudArt.Picture(content, "GiftLight", "glow", new Vector2(0.5f, 1f), new Vector2(560f, 330f), new Vector2(0f, 62f));
                light.color = new Color(1f, 0.78f, 0.36f, 0.5f);
                var gift = HudArt.Picture(content, "GiftArt", "art_gift", new Vector2(0.5f, 1f), new Vector2(378f, 224f), new Vector2(0f, 16f));
                gift.preserveAspect = true;
                if (ready) UiMotion.Pulse(gift.transform, 0.03f, 1.6f);
                else gift.color = new Color(0.5f, 0.5f, 0.56f);

                var strip = ScreenKit.Parchment(content, new Vector2(0.5f, 1f), new Vector2(660f, 126f), new Vector2(0f, -214f));
                var words = board == null ? (LastMessage ?? "The gift cannot be reached right now.")
                    : ready ? "A gift for logging in today" : "Today's gift is opened. A new one arrives at midnight UTC.";
                var line = HudArt.Label(strip, words, HudArt.Display, 22, TextAnchor.MiddleCenter, ScreenKit.InkBrown, ScreenKit.ParchmentEdge);
                line.name = "GiftWords";
                line.fontStyle = FontStyle.BoldAndItalic;
                line.horizontalOverflow = HorizontalWrapMode.Wrap;
                if (!ready)
                {
                    Ui.Stretch(line.rectTransform, 30f, 12f, 30f, 12f);
                    return;
                }
                line.rectTransform.anchorMin = new Vector2(0f, 0.56f);
                line.rectTransform.anchorMax = Vector2.one;
                line.rectTransform.offsetMin = new Vector2(24f, 0f);
                line.rectTransform.offsetMax = new Vector2(-24f, -8f);
                GiftAmount(strip, 0f, "icon_gold", $"{board.GiftGold:N0} Gold");
                GiftAmount(strip, 1f / 3f, "icon_chakra", $"{board.GiftMana:N0} Mana");
                GiftAmount(strip, 2f / 3f, "icon_crystals", $"{board.GiftCrystals:N0} Gems");

                var open = Ui.Button(content, "OpenGiftButton", "Open", OpenGiftNow, true, Ui.AccentColor, 28);
                Ui.Anchor((RectTransform)open.transform, new Vector2(0.5f, 0f), new Vector2(290f, 66f), new Vector2(0f, 2f));
                UiMotion.Pulse(open.transform, 0.05f, 1.1f);
            });
        }

        /// <summary>One of the gift's three amounts on its parchment: the picture, then the amount in dark ink.</summary>
        private static void GiftAmount(RectTransform strip, float from, string icon, string words)
        {
            var cell = Ui.Rect("Gift_" + icon, strip);
            cell.anchorMin = new Vector2(from, 0f);
            cell.anchorMax = new Vector2(from + 1f / 3f, 0.56f);
            cell.offsetMin = new Vector2(from <= 0f ? 26f : 8f, 12f);
            cell.offsetMax = new Vector2(-8f, 0f);
            var picture = HudArt.Picture(cell, "Icon", icon, new Vector2(0f, 0.5f), new Vector2(38f, 38f), new Vector2(6f, 0f));
            picture.preserveAspect = true;
            var label = HudArt.Label(cell, words, HudArt.Display, 22, TextAnchor.MiddleLeft, ScreenKit.InkBrown, ScreenKit.ParchmentEdge);
            label.name = "GiftAmount";
            Ui.Stretch(label.rectTransform, 52f, 0f, 0f, 0f);
            label.horizontalOverflow = HorizontalWrapMode.Overflow;
            label.resizeTextForBestFit = true;
            label.resizeTextMinSize = 13;
            label.resizeTextMaxSize = 22;
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
            _panel.ShowStage("Mail", log == null ? "" : $"{log.Count} reports", "icon_mail", "scene_lantern", false, 230f, CloseAll, panel =>
            {
                ScreenKit.Notice(panel, LastMessage);
                var shieldLeft = System.DateTimeOffset.FromUnixTimeMilliseconds(_shieldUntilMs) - System.DateTimeOffset.UtcNow;
                if (_shieldUntilMs > 0 && shieldLeft > System.TimeSpan.Zero)
                    ScreenKit.AddRow(panel, "ShieldRow", "icon_shield", $"Shield: {(int)shieldLeft.TotalHours}h {shieldLeft.Minutes}m", "No one can attack you. Attacking someone ends it.", true, 72f);
                if (log == null) return;
                if (log.Count == 0) panel.Line("No one has raided your village yet.", 21);
                foreach (var record in log)
                {
                    var when = System.DateTimeOffset.FromUnixTimeMilliseconds(record.atMs).LocalDateTime;
                    var who = string.IsNullOrEmpty(record.attackerName) ? "A raider" : record.attackerName;
                    var held = record.stars == 0;
                    var row = ScreenKit.AddRow(panel, "MailRow_" + record.attackId, held ? "icon_shield" : "icon_battle",
                        held ? $"Your village held against {who}" : $"{who} took {record.stars} of 3 stars",
                        $"{when:MMM d, HH:mm}     {record.damagePercent}% damage     {record.defendersAlive} of {record.defendersTotal} guards standing");
                    // A raider's name is something a player typed: shown as written, never obeyed.
                    row.Title.supportRichText = false;
                    if (record.canRevenge)
                    {
                        var attackId = record.attackId;
                        row.Button("Revenge_" + attackId, "Revenge", () => Revenge(attackId), true, Ui.AttackColor, 150f);
                    }
                    // What was taken, as tiles; nothing when nothing was lost.
                    if (record.manaLost > 0) row.Reward("icon_chakra", "-" + record.manaLost.ToString("N0"));
                    if (record.goldLost > 0) row.Reward("icon_gold", "-" + record.goldLost.ToString("N0"));
                    if (record.goldLost + record.manaLost == 0) row.Note("Nothing lost");
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
