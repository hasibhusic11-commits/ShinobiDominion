using System.Linq;
using ShinobiDominion.Core;
using UnityEngine;
using UnityEngine.UI;

namespace ShinobiDominion.Game
{
    /// <summary>
    /// The hero page and the Summoning Gate as painted scenes: the hero stands large in the middle,
    /// details sit in panels to either side, and the main action is a big button under the hero.
    /// </summary>
    public sealed partial class VillageController
    {
        private int _selectedSkill;
        private string _skillHeroId;

        private static readonly Color PaperInk = new Color(0.36f, 0.20f, 0.10f);
        private static readonly Color PaperTitle = new Color(0.30f, 0.16f, 0.07f);

        // ---------- Hero page ----------

        public void ShowHeroDetail(string instanceId)
        {
            var hero = Roster.Find(instanceId);
            if (hero == null)
            {
                ShowAltarRoster();
                return;
            }
            // Opened afresh, the window shows the hero itself; a change made inside it keeps its banner.
            _heroTab = HeroTab.Hero;
            OpenHeroWindow(instanceId);
        }

        // ---------- Summoning Gate ----------

        private void DrawSummoningGate(string message)
        {
            var odds = _odds;
            var one = odds.priceCrystalsPerRoll;
            var crystals = ServerCrystals ?? 0;
            _panel.ShowScene(odds.bannerName, $"Gems {crystals:N0}", CloseAll, scene =>
            {
                Ui.Framed(scene, HeroArt.GateScene, false).type = Image.Type.Simple;

                // Three of the Legendary heroes this gate can give, standing in front of the gate.
                var featured = odds.items.Where(i => i.rarity == "Legendary" && HeroConcepts.Has(i.archetypeId)).Take(3).ToList();
                var spots = new[] { new Vector2(0.32f, 430f), new Vector2(0.68f, 430f), new Vector2(0.5f, 500f) };
                for (var i = 0; i < featured.Count; i++)
                {
                    var sprite = HeroConcepts.Full(featured[i].archetypeId);
                    var spot = featured.Count == 1 ? spots[2] : spots[i];
                    var figure = Ui.Rect("Featured_" + featured[i].archetypeId, scene);
                    Ui.Anchor(figure, new Vector2(spot.x, 0f), new Vector2(spot.y * sprite.rect.width / sprite.rect.height, spot.y), new Vector2(0f, 120f));
                    Ui.Framed(figure, sprite, false).type = Image.Type.Simple;
                    UiMotion.PopIn(figure, 0.1f * i, 0.85f, 0.4f);
                }

                // The odds stay on screen, as the rules require, with every hero one tap away.
                var rates = Ui.Rect("GateRates", scene);
                Ui.Anchor(rates, new Vector2(0f, 1f), new Vector2(300f, 272f), new Vector2(16f, -84f));
                HudArt.Plate(rates).pixelsPerUnitMultiplier = 1.1f;
                HudArt.DropShadow(rates);
                var lines = $"{odds.status}\n\n";
                foreach (var rarity in new[] { "Legendary", "Elite", "Common" })
                    if (odds.rarityChance.TryGetValue(rarity, out var chance))
                        lines += $"{rarity}:  {chance}\n";
                var text = Ui.Label(rates, lines.TrimEnd(), 16, TextAnchor.UpperLeft);
                Ui.Stretch(text.rectTransform, 16f, 60f, 16f, 14f);
                var ratesButton = Ui.Button(rates, "RatesButton", "All rates", ShowSummonRates, true, null, 17);
                Ui.Anchor((RectTransform)ratesButton.transform, new Vector2(0.5f, 0f), new Vector2(200f, 44f), new Vector2(0f, 10f));

                if (!string.IsNullOrEmpty(message))
                {
                    var note = Ui.Strong(Ui.Label(scene, message, 21, TextAnchor.MiddleCenter, Ui.AccentColor));
                    Ui.Anchor(note.rectTransform, new Vector2(0.5f, 0f), new Vector2(900f, 30f), new Vector2(0f, 136f));
                }
                var fair = Ui.Strong(Ui.Label(scene, "Every roll is independent. Nothing is guaranteed, and past results do not change these rates.", 16, TextAnchor.MiddleCenter, Ui.MutedText));
                Ui.Anchor(fair.rectTransform, new Vector2(0.5f, 0f), new Vector2(900f, 26f), new Vector2(0f, 100f));

                var oneButton = Ui.Button(scene, "SummonOneButton", "Summon x1", () => Summon(1), crystals >= one, new Color(0.35f, 0.45f, 0.85f), 23);
                Ui.Anchor((RectTransform)oneButton.transform, new Vector2(0.5f, 0f), new Vector2(300f, 68f), new Vector2(-164f, 18f));
                ScreenKit.AttachCost(oneButton, "icon_crystals", one.ToString("N0"));
                var tenButton = Ui.Button(scene, "SummonTenButton", "Summon x10", () => Summon(10), crystals >= one * 10, Ui.AccentColor, 23);
                Ui.Anchor((RectTransform)tenButton.transform, new Vector2(0.5f, 0f), new Vector2(300f, 68f), new Vector2(164f, 18f));
                ScreenKit.AttachCost(tenButton, "icon_crystals", (one * 10).ToString("N0"));
            });
        }

        /// <summary>Every hero's own chance, exactly as the server reports it.</summary>
        public void ShowSummonRates()
        {
            var odds = _odds;
            _panel.ShowScreen("Summon Rates", odds.bannerName, () => DrawSummoningGate(null), panel =>
            {
                panel.Line(odds.status, 17, Ui.AccentColor);
                panel.Line("Every roll is independent. Nothing is guaranteed, and past results do not change these rates.", 17, Ui.MutedText);
                ScreenKit.Heading(panel, "By rarity");
                foreach (var rarity in new[] { "Legendary", "Elite", "Common" })
                    if (odds.rarityChance.TryGetValue(rarity, out var chance))
                        ScreenKit.AddRow(panel, "RarityRate_" + rarity, "icon_rarity_" + rarity.ToLowerInvariant(), rarity, null, false, 58f).Note(chance, HudArt.Ivory);
                ScreenKit.Heading(panel, "By hero");
                foreach (var item in odds.items.OrderBy(i => i.weight))
                    ScreenKit.AddRow(panel, "HeroRate_" + item.archetypeId, "icon_rarity_" + item.rarity.ToLowerInvariant(), item.displayName, item.rarity, false, 58f).Note(item.chance, HudArt.Ivory);
            });
        }
    }
}
