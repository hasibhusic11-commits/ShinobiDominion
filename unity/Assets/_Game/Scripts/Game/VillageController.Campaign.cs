using System.Collections.Generic;
using System.Linq;
using ShinobiDominion.Core;
using UnityEngine;
using UnityEngine.UI;

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
            _panel.ShowScreen("Dungeons", $"{stars} of {RaidData.All.Count * 3} stars", ShowRaidList, panel =>
            {
                if (!string.IsNullOrEmpty(LastMessage)) panel.Line(LastMessage, 20, Ui.AccentColor);
                var tabs = panel.Row();
                foreach (var number in RaidData.All.Select(r => r.chapter).Distinct())
                {
                    var name = RaidData.All.First(r => r.chapter == number).chapterName;
                    var chosen = number;
                    Ui.Button(tabs, "ChapterTab" + number, $"{number}. {name}", () =>
                    {
                        _campaignChapter = chosen;
                        _selectedStage = RaidData.All.First(r => r.chapter == chosen).id;
                        ShowCampaign();
                    }, true, number == _campaignChapter ? Ui.AccentColor : (Color?)null, 19);
                }

                DrawMap(panel, chapter, selected);
                DrawStageCard(panel, selected, team);
            });
        }

        private void DrawMap(PanelView panel, List<RaidDefinition> stages, RaidDefinition selected)
        {
            var map = panel.Block(MapHeight);
            Ui.Background(map, Color.white, false).sprite = CampaignArt.Map(_campaignChapter);

            for (var i = 0; i + 1 < stages.Count && i + 1 < MapSpots.Length; i++)
                DrawRoad(map, MapSpots[i], MapSpots[i + 1], StageOpen(stages[i + 1]));

            for (var i = 0; i < stages.Count && i < MapSpots.Length; i++)
            {
                var stage = stages[i];
                var open = StageOpen(stage);
                var best = BestStarsOn(stage.id);
                var color = !open ? new Color(0.25f, 0.25f, 0.28f) : best > 0 ? Ui.AccentColor : UiSkin.Vermilion;
                var chosen = stage.id;
                var node = Ui.Button(map, "StageNode" + stage.stage, stage.stage.ToString(), () =>
                {
                    _selectedStage = chosen;
                    ShowCampaign();
                }, true, color, 24);
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

                var label = Ui.Strong(Ui.Label(map, open ? $"{best}/3" : "Locked", 17, TextAnchor.MiddleCenter));
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
                Ui.Background(dash, open ? new Color(0.98f, 0.86f, 0.55f, 0.95f) : new Color(0.1f, 0.1f, 0.12f, 0.55f), false);
            }
        }

        private void DrawStageCard(PanelView panel, RaidDefinition stage, int team)
        {
            var best = BestStarsOn(stage.id);
            panel.Line($"Stage {stage.stage}: {stage.displayName}", 25, new Color(1f, 0.9f, 0.66f));
            panel.Line(stage.description, 19);
            var guards = stage.defenders.Count;
            var towers = stage.buildings.Count(b => b.def == "defense_tower" || b.def == "cannon_tower" || b.def == "spirit_turret");
            var guardText = guards == 0 ? "No hero on guard" : guards == 1 ? "1 enemy hero on guard" : $"{guards} enemy heroes on guard";
            panel.Line($"Best: {best} of 3 stars.   {guardText}, {towers} tower{(towers == 1 ? "" : "s")}.", 19);
            panel.Line(RewardText(stage, best), 19, Ui.AccentColor);
            var open = StageOpen(stage);
            if (!open) panel.Line("Clear the stage before this one first.", 19, Ui.MutedText);
            else if (team == 0) panel.Line("Put a hero on your Hero Base first.", 19, Ui.MutedText);
            panel.Action("ScoutButton", "Scout", () => ScoutRaid(stage.id), open && team > 0 && (IsOnline || AllowOfflinePlay), Ui.AttackColor);
        }

        private static string RewardText(RaidDefinition stage, int best)
        {
            var parts = new List<string> { $"{stage.goldPerStar:N0} gold per new star", $"{stage.xpPerWin:N0} XP per hero on each win" };
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

        private void CreateBattleHud()
        {
            GameSounds.Play(GameSound.Horn);
            var panel = new GameObject("BattleHud", typeof(RectTransform));
            var rect = (RectTransform)panel.transform;
            rect.SetParent(_raid.StatusBar.transform.parent, false);
            Ui.Anchor(rect, new Vector2(1f, 1f), new Vector2(300f, 150f), new Vector2(-16f, -104f));
            Ui.Framed(rect, Ui.PillSprite);
            UiMotion.SlideIn(rect, new Vector2(340f, 0f), 0.3f);
            _raid.Hud = panel;

            var session = _raid.Session;
            var payable = session.raid.id == "rival" ? 3 : Mathf.Max(0, 3 - BestStarsOn(session.raid.id));
            var loot = Ui.Strong(Ui.Label(panel.transform, _modeFight != null ? (_modeFight.IsSiege ? "Their progress" : $"vs {_modeFight.OpponentName}") : $"Available: {payable * session.raid.goldPerStar:N0} gold", 20, TextAnchor.MiddleCenter, new Color(1f, 0.86f, 0.45f)));
            Ui.Anchor(loot.rectTransform, new Vector2(0.5f, 1f), new Vector2(280f, 36f), new Vector2(0f, -6f));

            _battleHud = new BattleHud { Stars = new Image[3] };
            for (var i = 0; i < 3; i++)
                _battleHud.Stars[i] = Ui.Icon(panel.transform, UiSkin.Diamond(new Color(0.35f, 0.35f, 0.38f), new Color(0.2f, 0.2f, 0.23f)), new Vector2(0.5f, 0.5f), new Vector2(46f, 46f), new Vector2((i - 1) * 62f, 4f));
            _battleHud.Damage = Ui.Strong(Ui.Label(panel.transform, "0% destroyed", 21, TextAnchor.MiddleCenter));
            Ui.Anchor(_battleHud.Damage.rectTransform, new Vector2(0.5f, 0f), new Vector2(280f, 36f), new Vector2(0f, 8f));

            var banner = Ui.Strong(Ui.Label(_raid.StatusBar.transform.parent, "BATTLE!", 72, TextAnchor.MiddleCenter, new Color(1f, 0.82f, 0.35f)));
            Ui.Anchor(banner.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(620f, 120f), new Vector2(0f, 110f));
            UiMotion.PopIn(banner.transform, 0f, 2.2f, 0.4f);
            Destroy(banner.gameObject, 1.1f);
        }

        private void UpdateBattleHud()
        {
            if (_battleHud == null || _raid == null) return;
            var stage = _raid.Stage;
            if (stage.DamagePercent != _battleHud.Shown)
            {
                // Only when it changes, so the label is not rebuilt every frame.
                _battleHud.Shown = stage.DamagePercent;
                _battleHud.Damage.text = $"{stage.DamagePercent}% destroyed";
            }
            var lit = (stage.DamagePercent >= 50 ? 1 : 0) + (stage.DojoDown ? 1 : 0) + (stage.DamagePercent >= 100 ? 1 : 0);
            if (lit <= _battleHud.Lit) return;
            for (var i = _battleHud.Lit; i < lit && i < 3; i++)
            {
                _battleHud.Stars[i].sprite = UiSkin.Diamond(new Color(1f, 0.87f, 0.4f), new Color(0.9f, 0.55f, 0.12f));
                UiMotion.Punch(_battleHud.Stars[i].transform, 0.5f, 0.45f);
                UiMotion.Shine(_battleHud.Stars[i], new Color(1f, 0.95f, 0.75f, 0.9f), 2.5f, 0.35f);
            }
            _battleHud.Lit = lit;
            GameSounds.Play(GameSound.Star);
        }

        /// <summary>Three big stars across the result panel, lit for those earned.</summary>
        private static void DrawResultStars(PanelView panel, int stars)
        {
            var row = panel.Block(76f);
            for (var i = 0; i < 3; i++)
            {
                var sprite = i < stars ? UiSkin.Diamond(new Color(1f, 0.87f, 0.4f), new Color(0.9f, 0.55f, 0.12f)) : UiSkin.Diamond(new Color(0.3f, 0.3f, 0.33f), new Color(0.17f, 0.17f, 0.2f));
                var icon = Ui.Icon(row, sprite, new Vector2(0.5f, 0.5f), new Vector2(70f, 70f), new Vector2((i - 1) * 90f, 0f));
                if (i >= stars) continue;
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
