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
            if (_skillHeroId != instanceId)
            {
                _skillHeroId = instanceId;
                _selectedSkill = 0;
            }

            var archetype = Roster.ArchetypeOf(hero);
            _panel.ShowScene("Heroes", $"{archetype.rarity}  ·  {archetype.role}  ·  {archetype.affinity}", ShowAltarRoster, scene => DrawHeroPage(scene, hero, archetype));
        }

        // ---------- Summoning Gate ----------

        private void DrawSummoningGate(string message)
        {
            var odds = _odds;
            var one = odds.priceCrystalsPerRoll;
            var crystals = ServerCrystals ?? 0;
            _panel.ShowScene(odds.bannerName, $"Crystals {crystals:N0}", CloseAll, scene =>
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
                Ui.Anchor(rates, new Vector2(0f, 1f), new Vector2(300f, 220f), new Vector2(16f, -84f));
                HudArt.Plate(rates).pixelsPerUnitMultiplier = 1.1f;
                HudArt.DropShadow(rates);
                var lines = $"{odds.status}\n\n";
                foreach (var rarity in new[] { "Legendary", "Elite", "Common" })
                    if (odds.rarityChance.TryGetValue(rarity, out var chance))
                        lines += $"{rarity}:  {chance}\n";
                var text = Ui.Label(rates, lines.TrimEnd(), 18, TextAnchor.UpperLeft);
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

                var oneButton = Ui.Button(scene, "SummonOneButton", $"Summon x1\n{one:N0} crystals", () => Summon(1), crystals >= one, new Color(0.35f, 0.45f, 0.85f), 21);
                Ui.Anchor((RectTransform)oneButton.transform, new Vector2(0.5f, 0f), new Vector2(260f, 76f), new Vector2(-150f, 14f));
                var tenButton = Ui.Button(scene, "SummonTenButton", $"Summon x10\n{one * 10:N0} crystals", () => Summon(10), crystals >= one * 10, new Color(0.95f, 0.5f, 0.16f), 21);
                Ui.Anchor((RectTransform)tenButton.transform, new Vector2(0.5f, 0f), new Vector2(260f, 76f), new Vector2(150f, 14f));
            });
        }

        /// <summary>Every hero's own chance, exactly as the server reports it.</summary>
        public void ShowSummonRates()
        {
            var odds = _odds;
            _panel.ShowScreen("Summon Rates", odds.bannerName, () => DrawSummoningGate(null), panel =>
            {
                panel.Line(odds.status, 19, Ui.AccentColor);
                panel.Line("Every roll is independent. Nothing is guaranteed, and past results do not change these rates.", 19, Ui.MutedText);
                panel.Line("Rates by rarity", 23, Ui.TextColor);
                foreach (var rarity in new[] { "Legendary", "Elite", "Common" })
                    if (odds.rarityChance.TryGetValue(rarity, out var chance))
                        panel.Line($"{rarity}:  {chance}", 20, Ui.TextColor);
                panel.Line("Rates by hero", 23, Ui.TextColor);
                foreach (var item in odds.items.OrderBy(i => i.weight))
                    panel.Line($"{item.displayName}  ({item.rarity}):  {item.chance}", 19);
            });
        }
    }
}
