using System.Collections.Generic;
using System.Linq;
using ShinobiDominion.Core;
using UnityEngine;
using UnityEngine.UI;
using Text = ShinobiDominion.Game.GameText;

namespace ShinobiDominion.Game
{
    /// <summary>The dungeon map: stages in chapters, opened in order. Online, the server decides what is open.</summary>
    public sealed partial class VillageController
    {
        private static readonly Vector2[] MapSpots =
        {
            new Vector2(0.10f, 0.32f), new Vector2(0.29f, 0.70f), new Vector2(0.50f, 0.34f), new Vector2(0.71f, 0.70f), new Vector2(0.90f, 0.36f)
        };

        private const float MapHeight = 330f;

        private int _campaignChapter = 1;
        private string _selectedStage;

        private int BestStarsOn(string raidId) => _raidList?.FirstOrDefault(r => r.id == raidId)?.bestStars ?? 0;

        /// <summary>Offline practice can try any stage. Online, this only draws the map: the server enforces it.</summary>
        public bool StageOpen(RaidDefinition raid) => !IsOnline || string.IsNullOrEmpty(raid.requires) || BestStarsOn(raid.requires) > 0;

        public void OpenCampaign()
        {
            var next = RaidData.All.FirstOrDefault(r => StageOpen(r) && BestStarsOn(r.id) == 0) ?? RaidData.All.Last(StageOpen);
            _selectedStage = next.id;
            _campaignChapter = next.chapter;
            ShowCampaign();
        }

        private void ShowCampaign()
        {
            var team = Village.Buildings.Count(b => b.AssignedHeroInstanceId != null);
            var stars = RaidData.All.Sum(r => BestStarsOn(r.id));
            var selected = RaidData.Find(_selectedStage);
            var chapter = RaidData.All.Where(r => r.chapter == _campaignChapter).OrderBy(r => r.stage).ToList();
            var chapters = new List<WindowTab>();
            foreach (var number in RaidData.All.Select(r => r.chapter).Distinct())
            {
                var name = RaidData.All.First(r => r.chapter == number).chapterName;
                var chosen = number;
                chapters.Add(new WindowTab("ChapterTab" + number, name, number == _campaignChapter, () =>
                {
                    _campaignChapter = chosen;
                    _selectedStage = RaidData.All.First(r => r.chapter == chosen).id;
                    ShowCampaign();
                }, $"Chapter {number}"));
            }
            _panel.ShowStage("Dungeons", $"{stars} / {RaidData.All.Count * 3} Stars", "icon_star", null, false, 0f, ShowRaidList, panel =>
            {
                ScreenKit.Notice(panel, LastMessage);
                DrawMap(panel, chapter, selected);
                DrawStageCard(panel, selected, team);
            }, null, chapters);
        }

        private void DrawMap(PanelView panel, List<RaidDefinition> stages, RaidDefinition selected)
        {
            var map = panel.Block(MapHeight);
            // The chapter is a map drawn on parchment: the road in ink, each stage a seal.
            var sheet = map.gameObject.AddComponent<Image>();
            sheet.sprite = HudArt.Sprite("parchment_s40");
            sheet.type = Image.Type.Sliced;
            sheet.pixelsPerUnitMultiplier = 1.3f;
            sheet.color = new Color(0.96f, 0.9f, 0.78f);
            sheet.raycastTarget = false;

            for (var i = 0; i + 1 < stages.Count && i + 1 < MapSpots.Length; i++)
                DrawRoad(map, MapSpots[i], MapSpots[i + 1], StageOpen(stages[i + 1]));

            for (var i = 0; i < stages.Count && i < MapSpots.Length; i++)
            {
                var stage = stages[i];
                var open = StageOpen(stage);
                var best = BestStarsOn(stage.id);
                var color = !open ? new Color(0.25f, 0.25f, 0.28f) : best > 0 ? Ui.AccentColor : UiSkin.Vermilion;
                // A round seal: grey when locked, gold once any star is won, crimson for the stage still to beat.
                var seal = !open ? UiSkin.Disc(new Color(0.42f, 0.43f, 0.5f), new Color(0.22f, 0.23f, 0.3f), new Color(0.6f, 0.58f, 0.54f))
                    : best > 0 ? UiSkin.Disc(new Color(0.98f, 0.8f, 0.36f), new Color(0.7f, 0.48f, 0.14f), new Color(0.86f, 0.68f, 0.3f))
                    : UiSkin.Disc(new Color(0.82f, 0.24f, 0.17f), new Color(0.5f, 0.1f, 0.08f), new Color(0.86f, 0.68f, 0.3f));
                var chosen = stage.id;
                var node = Ui.Button(map, "StageNode" + stage.stage, stage.stage.ToString(), () =>
                {
                    _selectedStage = chosen;
                    ShowCampaign();
                }, true, color, 24);
                var face = node.GetComponent<Image>();
                face.sprite = seal;
                face.type = Image.Type.Simple;
                face.preserveAspect = true;
                var rect = (RectTransform)node.transform;
                var size = stage.id == selected.id ? 84f : 68f;
                rect.anchorMin = rect.anchorMax = MapSpots[i];
                rect.pivot = new Vector2(0.5f, 0.5f);
                rect.sizeDelta = new Vector2(size, size);
                rect.anchoredPosition = Vector2.zero;
                if (stage.id == selected.id)
                {
                    Object.Destroy(node.GetComponent<UiPress>());
                    UiMotion.Pulse(rect, 0.07f, 1.2f);
                }

                var label = HudArt.Label(map, open ? $"{best} / 3" : "Locked", HudArt.Numbers, 17, TextAnchor.MiddleCenter, ScreenKit.InkBrown, ScreenKit.ParchmentEdge);
                var labelRect = label.rectTransform;
                labelRect.anchorMin = labelRect.anchorMax = MapSpots[i];
                labelRect.pivot = new Vector2(0.5f, 1f);
                labelRect.sizeDelta = new Vector2(110f, 26f);
                labelRect.anchoredPosition = new Vector2(0f, -size * 0.5f - 2f);
            }
        }

        /// <summary>A short dashed road between two stops. Anchors keep it right at any screen width.</summary>
        private static void DrawRoad(RectTransform map, Vector2 from, Vector2 to, bool open)
        {
            const int dashes = 7;
            for (var i = 1; i < dashes; i++)
            {
                var at = Vector2.Lerp(from, to, i / (float)dashes);
                var dash = Ui.Rect("RoadDash", map);
                dash.anchorMin = dash.anchorMax = at;
                dash.pivot = new Vector2(0.5f, 0.5f);
                dash.sizeDelta = new Vector2(11f, 11f);
                dash.anchoredPosition = Vector2.zero;
                Ui.Background(dash, open ? new Color(0.36f, 0.22f, 0.1f, 0.9f) : new Color(0.36f, 0.22f, 0.1f, 0.28f), false).sprite = UiSkin.Disc(Color.white, Color.white, Color.white, 0f);
            }
        }

        private void DrawStageCard(PanelView panel, RaidDefinition stage, int team)
        {
            var best = BestStarsOn(stage.id);
            var open = StageOpen(stage);
            var can = open && team > 0 && (IsOnline || AllowOfflinePlay);
            // The chosen stage, as the open row of a list: its number, its name, what it pays and the way in.
            var row = ScreenKit.AddRow(panel, "StageCard", stage.stage.ToString(), stage.displayName, stage.description, true, 92f);
            if (can) row.Button("ScoutButton", "Scout", () => ScoutRaid(stage.id), true, Ui.AttackColor);
            else row.Locked("ScoutButton");
            if (best == 0 && stage.firstClearCrystals > 0) row.Reward("icon_crystals", stage.firstClearCrystals.ToString("N0"));
            if (stage.xpPerWin > 0) row.Reward("icon_experience", stage.xpPerWin.ToString("N0"));
            row.Reward("icon_chakra", stage.manaPerStar.ToString("N0"));
            row.Reward("icon_gold", stage.goldPerStar.ToString("N0"));
            row.Stars(best);
            var guards = stage.defenders.Count;
            var towers = stage.buildings.Count(b => b.def == "defense_tower" || b.def == "cannon_tower" || b.def == "spirit_turret");
            var guardText = guards == 0 ? "No hero on guard" : guards == 1 ? "1 enemy hero on guard" : $"{guards} enemy heroes on guard";
            var first = best == 0 && !string.IsNullOrEmpty(stage.firstClearItem) ? $"   First win: {ItemName(stage.firstClearItem)}." : "";
            panel.Line($"{guardText}, {towers} tower{(towers == 1 ? "" : "s")}.   Gold and Mana are paid for each new star, XP on every win.{first}", 17);
            if (!open) panel.Line("Clear the stage before this one first.", 18, Ui.AccentColor);
            else if (team == 0) panel.Line("Put a hero on your Hero Base first.", 18, Ui.AccentColor);
        }

        private static string RewardText(RaidDefinition stage, int best)
        {
            var parts = new List<string> { $"{stage.goldPerStar:N0} gold and {stage.manaPerStar:N0} Mana per new star", $"{stage.xpPerWin:N0} XP per hero on each win" };
            if (best == 0)
            {
                if (stage.firstClearCrystals > 0) parts.Add($"{stage.firstClearCrystals:N0} gems first time");
                if (!string.IsNullOrEmpty(stage.firstClearItem)) parts.Add($"{ItemName(stage.firstClearItem)} first time");
            }
            return string.Join(", ", parts) + ".";
        }


        // ---------- The battle screen ----------

        private sealed class BattleHud
        {
            public Image[] Stars;
            public Text Damage;
            public int Lit;
            public int Shown = -1;
        }

        private BattleHud _battleHud;

        /// <summary>A star not earned yet: the painted star, darkened.</summary>
        private static readonly Color UnlitStar = new Color(0.2f, 0.21f, 0.27f, 0.95f);

        /// <summary>The clock in the last half minute.</summary>
        private static readonly Color ClockWarning = new Color(1f, 0.46f, 0.38f);

        private void UpdateBattleHud()
        {
            if (_battleHud == null || _raid == null) return;
            var stage = _raid.Stage;
            if (stage.DamagePercent != _battleHud.Shown)
            {
                // Only when it changes, so the label is not rebuilt every frame. A rise gives the number a bump.
                if (_battleHud.Shown >= 0 && stage.DamagePercent > _battleHud.Shown) UiMotion.Punch(_battleHud.Damage.transform, 0.12f, 0.2f);
                _battleHud.Shown = stage.DamagePercent;
                _battleHud.Damage.text = $"{stage.DamagePercent}%";
            }
            var lit = (stage.DamagePercent >= 50 ? 1 : 0) + (stage.DojoDown ? 1 : 0) + (stage.DamagePercent >= 100 ? 1 : 0);
            if (lit <= _battleHud.Lit) return;
            for (var i = _battleHud.Lit; i < lit && i < 3; i++)
            {
                _battleHud.Stars[i].color = Color.white;
                UiMotion.Punch(_battleHud.Stars[i].transform, 0.5f, 0.45f);
                UiMotion.Shine(_battleHud.Stars[i], new Color(1f, 0.95f, 0.75f, 0.9f), 2.5f, 0.35f);
            }
            _battleHud.Lit = lit;
            GameSounds.Play(GameSound.Star);
        }

        /// <summary>
        /// The clock at the top of a battle. It is rewritten once a second, turns red for the last half minute, and
        /// ticks and bumps through the last ten seconds.
        /// </summary>
        private void UpdateBattleClock()
        {
            var seconds = _raid.Stage.SecondsLeft;
            if (seconds == _raid.ClockSecond) return;
            var first = _raid.ClockSecond < 0;
            _raid.ClockSecond = seconds;
            _raid.StatusText.text = $"Time left  {seconds / 60}:{seconds % 60:00}";
            _raid.StatusText.color = seconds <= 30 ? ClockWarning : HudArt.Ivory;
            if (first || seconds > 10 || seconds <= 0) return;
            UiMotion.Punch(_raid.StatusText.transform, 0.14f, 0.25f);
            GameSounds.Play(GameSound.Tick, 0.5f);
        }

        /// <summary>Three big stars across the result panel, lit for those earned.</summary>
        private static void DrawResultStars(PanelView panel, int stars)
        {
            var row = panel.Block(76f);
            for (var i = 0; i < 3; i++)
            {
                // The middle star stands a little taller, so the three read as one crest.
                var size = i == 1 ? 82f : 68f;
                var icon = Ui.Icon(row, HudArt.Sprite("icon_star"), new Vector2(0.5f, 0.5f), new Vector2(size, size), new Vector2((i - 1) * 88f, i == 1 ? 2f : -4f));
                icon.name = "ResultStar" + (i + 1);
                icon.preserveAspect = true;
                if (i >= stars)
                {
                    icon.color = UnlitStar;
                    continue;
                }
                // Earned stars land one at a time, then keep a slow shine.
                UiMotion.PopIn(icon.transform, 0.2f + i * 0.28f, 0.1f, 0.42f);
                UiMotion.Shine(icon, new Color(1f, 0.95f, 0.75f, 0.9f), 2.6f, 0.35f);
            }
        }

        private static void DrawRewardLines(PanelView panel, RaidOutcome outcome)
        {
            var delay = 1.0f;
            void Reveal(Text line)
            {
                UiMotion.FadeIn(line.gameObject, 0.3f, delay);
                delay += 0.25f;
            }
            if (outcome.XpEarned > 0) Reveal(panel.Line($"Your hero gained {outcome.XpEarned:N0} XP" + (outcome.LevelsGained > 0 ? $" and {outcome.LevelsGained} levels" : ""), 21, Ui.AccentColor));
            if (outcome.CrystalsEarned > 0) Reveal(panel.Line($"Earned {outcome.CrystalsEarned:N0} gems", 21, Ui.AccentColor));
            if (!string.IsNullOrEmpty(outcome.ItemEarned))
            {
                var found = panel.Line($"Found: {ItemName(outcome.ItemEarned)}", 24, new Color(1f, 0.87f, 0.4f));
                Reveal(found);
                UiMotion.Shine(found, Color.white, 2f, 0.3f);
            }
            // Technique Manuals and Talent Tokens (ADR-053), in a fixed order.
            foreach (var pair in outcome.ItemsEarned.OrderBy(p => p.Key, System.StringComparer.Ordinal))
            {
                var earned = panel.Line($"Found: {pair.Value:N0} {ItemName(pair.Key)}{(pair.Value == 1 ? "" : "s")}", 21, new Color(1f, 0.87f, 0.4f));
                earned.name = "ItemsEarned_" + pair.Key;
                Reveal(earned);
            }
        }
    }

    /// <summary>Simple painted map backgrounds for each chapter, made in code.</summary>
    public static class CampaignArt
    {
        private static readonly Dictionary<int, Sprite> Maps = new Dictionary<int, Sprite>();

        public static Sprite Map(int chapter)
        {
            if (Maps.TryGetValue(chapter, out var sprite) && sprite != null) return sprite;
            sprite = Paint(chapter);
            Maps[chapter] = sprite;
            return sprite;
        }

        private static Sprite Paint(int chapter)
        {
            // Low ground to high ground: ember ridge, misty marsh, cold peak.
            Color[][] palettes =
            {
                new[] { new Color(0.26f, 0.16f, 0.12f), new Color(0.46f, 0.27f, 0.16f), new Color(0.62f, 0.40f, 0.22f) },
                new[] { new Color(0.12f, 0.25f, 0.24f), new Color(0.22f, 0.40f, 0.33f), new Color(0.46f, 0.58f, 0.46f) },
                new[] { new Color(0.22f, 0.25f, 0.33f), new Color(0.42f, 0.46f, 0.55f), new Color(0.84f, 0.87f, 0.92f) }
            };
            var palette = palettes[Mathf.Clamp(chapter - 1, 0, palettes.Length - 1)];
            const int width = 512;
            const int height = 200;
            var texture = new Texture2D(width, height, TextureFormat.RGB24, false) { wrapMode = TextureWrapMode.Clamp };
            var seed = chapter * 37.3f;
            // Filled into one array and uploaded at once; a call per pixel caused a hitch when the map first opened.
            var pixels = new Color32[width * height];
            for (var y = 0; y < height; y++)
            for (var x = 0; x < width; x++)
            {
                var u = x / 80f;
                var v = y / 80f;
                var land = Mathf.PerlinNoise(u + seed, v + seed) * 0.7f + Mathf.PerlinNoise(u * 3f + seed, v * 3f) * 0.3f;
                // Stepped bands read as painted contours rather than blurry noise.
                var band = Mathf.Floor(land * 7f) / 7f;
                var color = band < 0.45f ? Color.Lerp(palette[0], palette[1], band / 0.45f) : Color.Lerp(palette[1], palette[2], (band - 0.45f) / 0.55f);
                var edge = Mathf.Min(Mathf.Min(x, width - x), Mathf.Min(y, height - y)) / 26f;
                pixels[y * width + x] = color * Mathf.Lerp(0.55f, 1f, Mathf.Clamp01(edge));
            }
            texture.SetPixels32(pixels);
            texture.Apply();
            return Sprite.Create(texture, new Rect(0f, 0f, width, height), new Vector2(0.5f, 0.5f));
        }
    }
}
