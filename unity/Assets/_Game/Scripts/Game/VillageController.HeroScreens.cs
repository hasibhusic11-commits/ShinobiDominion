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
            var color = HeroColor(archetype, 0);
            _panel.ShowScene("Heroes", $"{archetype.rarity}  ·  {archetype.role}  ·  {archetype.affinity}", ShowAltarRoster, scene =>
            {
                var backdrop = Ui.Framed(scene, HeroArt.Stage(color), false);
                backdrop.type = Image.Type.Simple;

                DrawHeroFigure(scene, hero, archetype, color);
                DrawHeroBrowse(scene, instanceId);
                DrawHeroStats(scene, hero, archetype);
                DrawSkillPanel(scene, hero, archetype);
            });
        }

        private void DrawHeroFigure(RectTransform scene, HeroInstance hero, HeroArchetype archetype, Color color)
        {
            const float centre = 0.41f;
            var glow = Ui.Icon(scene, HeroArt.Glow, new Vector2(centre, 0f), new Vector2(420f, 120f), new Vector2(0f, 96f));
            glow.color = Color.Lerp(color, Color.white, 0.4f) * new Color(1f, 1f, 1f, 0.75f);

            if (hero.EvolutionIndex == 0 && HeroConcepts.Has(archetype.id))
            {
                var sprite = HeroConcepts.Full(archetype.id);
                var figure = Ui.Rect("HeroConcept", scene);
                const float height = 520f;
                Ui.Anchor(figure, new Vector2(centre, 0f), new Vector2(height * sprite.rect.width / sprite.rect.height, height), new Vector2(0f, 108f));
                var image = Ui.Framed(figure, sprite, false);
                image.type = Image.Type.Simple;
                UiMotion.PopIn(figure, 0f, 0.92f, 0.3f);
            }
            else
            {
                var holder = Ui.Rect("HeroFigure", scene);
                Ui.Anchor(holder, new Vector2(centre, 0f), new Vector2(460f, 460f), new Vector2(0f, 100f));
                HeroPicture(holder, archetype, hero.EvolutionIndex, 460f, 0f);
            }

            var ribbon = HudArt.Picture(scene, "Ribbon", "ribbon", new Vector2(centre, 0f), new Vector2(430f, 72f), new Vector2(0f, 74f));
            var name = HudArt.Label(ribbon.transform, FormName(hero).ToUpperInvariant(), HudArt.Display, 25, TextAnchor.MiddleCenter, HudArt.Ivory, HudArt.CrimsonDark);
            Ui.Stretch(name.rectTransform, 40f, 6f, 40f, 2f);

            if (archetype.CanEvolve)
            {
                var evolve = Ui.Button(scene, "EvolveButton", hero.EvolutionIndex >= HeroMath.MaxEvolution ? "Final Form" : "Evolve", () => ShowEvolution(hero.InstanceId), true, new Color(0.95f, 0.5f, 0.16f), 26);
                Ui.Anchor((RectTransform)evolve.transform, new Vector2(centre, 0f), new Vector2(250f, 62f), new Vector2(0f, 10f));
            }
        }

        /// <summary>Arrows either side of the hero step through the heroes the Altar is showing.</summary>
        private void DrawHeroBrowse(RectTransform scene, string instanceId)
        {
            var list = SortedHeroes(Roster.Heroes.Where(MatchesFilters)).ToList();
            var index = list.FindIndex(h => h.InstanceId == instanceId);
            if (list.Count < 2 || index < 0) return;
            var previous = list[(index - 1 + list.Count) % list.Count].InstanceId;
            var next = list[(index + 1) % list.Count].InstanceId;
            var left = Medal.Create(scene, "PreviousHeroButton", MedalKind.Primary, "back", null, () => ShowHeroDetail(previous), true, 76f);
            Ui.Anchor((RectTransform)left.transform, new Vector2(0.23f, 0.5f), new Vector2(76f, 76f), new Vector2(0f, 20f));
            var right = Medal.Create(scene, "NextHeroButton", MedalKind.Primary, "next", null, () => ShowHeroDetail(next), true, 76f);
            Ui.Anchor((RectTransform)right.transform, new Vector2(0.59f, 0.5f), new Vector2(76f, 76f), new Vector2(0f, 20f));
        }

        private void DrawHeroStats(RectTransform scene, HeroInstance hero, HeroArchetype archetype)
        {
            var box = Ui.Rect("HeroStats", scene);
            // The box hugs its contents, top left under the title.
            Ui.Anchor(box, new Vector2(0f, 1f), new Vector2(250f, 0f), new Vector2(16f, -84f));
            box.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            HudArt.Plate(box).pixelsPerUnitMultiplier = 1.1f;
            var layout = box.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(24, 24, 22, 22);
            layout.spacing = 8f;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;

            HudArt.Label(box, $"{EvolutionLabel(hero.EvolutionIndex)}   Lv {hero.Level}/{HeroMath.MaxLevel}", HudArt.Display, 21, TextAnchor.MiddleLeft, HudArt.BrassLight, HudArt.LacquerDeep);
            var bar = Ui.Rect("LevelBar", box);
            bar.gameObject.AddComponent<LayoutElement>().preferredHeight = 14f;
            var track = bar.gameObject.AddComponent<Image>();
            track.sprite = HudArt.Sprite("track_s8");
            track.type = Image.Type.Sliced;
            track.raycastTarget = false;
            var fill = Ui.Rect("Fill", bar);
            fill.anchorMin = Vector2.zero;
            fill.anchorMax = new Vector2((float)hero.Level / HeroMath.MaxLevel, 1f);
            fill.offsetMin = new Vector2(1f, 1f);
            fill.offsetMax = new Vector2(-1f, -1f);
            var fillImage = fill.gameObject.AddComponent<Image>();
            fillImage.sprite = HudArt.Sprite("fill_s8");
            fillImage.type = Image.Type.Sliced;
            fillImage.color = new Color(0.45f, 0.9f, 0.35f);
            fillImage.raycastTarget = false;

            var stats = Roster.StatsOf(hero);
            HudArt.Label(box, $"HP   {stats.Hp:N0}", HudArt.Numbers, 21, TextAnchor.MiddleLeft, new Color(0.6f, 1f, 0.6f), HudArt.LacquerDeep);
            HudArt.Label(box, $"Attack   {stats.Attack:N0}", HudArt.Numbers, 21, TextAnchor.MiddleLeft, new Color(1f, 0.7f, 0.55f), HudArt.LacquerDeep);
            HudArt.Label(box, $"Defense   {stats.Defense:N0}", HudArt.Numbers, 21, TextAnchor.MiddleLeft, new Color(0.65f, 0.85f, 1f), HudArt.LacquerDeep);
            HudArt.Label(box, $"{archetype.rarity} {archetype.role}, {archetype.affinity}", HudArt.Numbers, 16, TextAnchor.MiddleLeft, HudArt.IvoryMuted, HudArt.LacquerDeep);
            if (Village.IsHeroAssigned(hero.InstanceId)) HudArt.Label(box, "On your Hero Base", HudArt.Numbers, 17, TextAnchor.MiddleLeft, HudArt.BrassLight, HudArt.LacquerDeep);

            var instanceId = hero.InstanceId;
            Ui.Button(box, "LockButton", hero.Locked ? "Unlock" : "Lock", () =>
            {
                if (IsOnline)
                {
                    SetLockOnline(instanceId, !hero.Locked);
                    return;
                }
                Roster.SetLocked(instanceId, !hero.Locked);
                ShowHeroDetail(instanceId);
            }, true, null, 19);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Ui.Button(box, "DevLevelButton", "Dev: level 200", () =>
            {
                if (IsOnline)
                {
                    DevMaxLevelOnline(instanceId);
                    return;
                }
                Roster.GrantXp(instanceId, 100_000_000);
                ShowHeroDetail(instanceId);
            }, hero.Level < HeroMath.MaxLevel, new Color(0.2f, 0.22f, 0.3f), 16);
#endif
        }

        /// <summary>A paper scroll with the chosen skill, and a row of skill icons to choose from.</summary>
        private void DrawSkillPanel(RectTransform scene, HeroInstance hero, HeroArchetype archetype)
        {
            var paper = Ui.Rect("SkillPanel", scene);
            paper.anchorMin = new Vector2(1f, 0f);
            paper.anchorMax = new Vector2(1f, 1f);
            paper.pivot = new Vector2(1f, 1f);
            paper.sizeDelta = new Vector2(440f, -214f);
            paper.anchoredPosition = new Vector2(-16f, -84f);
            Ui.Framed(paper, HudArt.Sprite("parchment_s40"), false).pixelsPerUnitMultiplier = 1.1f;
            HudArt.DropShadow(paper);

            var skills = archetype.skills;
            if (skills.Count == 0)
            {
                Ui.Label(paper, "No skills yet.", 20, TextAnchor.MiddleCenter, PaperInk);
                return;
            }
            _selectedSkill = Mathf.Clamp(_selectedSkill, 0, skills.Count - 1);
            var skill = skills[_selectedSkill];
            var unlocked = skill.unlockEvolution <= hero.EvolutionIndex;

            var title = Ui.Label(paper, skill.displayName, 27, TextAnchor.MiddleCenter, PaperTitle);
            title.font = HudArt.Display;
            title.fontStyle = FontStyle.Bold;
            Ui.Anchor(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(380f, 48f), new Vector2(0f, -18f));
            var rule = Ui.Rect("Rule", paper);
            Ui.Anchor(rule, new Vector2(0.5f, 1f), new Vector2(360f, 3f), new Vector2(0f, -68f));
            Ui.Background(rule, new Color(0.55f, 0.36f, 0.18f, 0.6f), false);

            var state = Ui.Label(paper, unlocked ? "Unlocked" : $"Unlocks at {EvolutionLabel(skill.unlockEvolution)}", 18, TextAnchor.MiddleCenter, unlocked ? new Color(0.2f, 0.45f, 0.15f) : new Color(0.6f, 0.2f, 0.1f));
            state.fontStyle = FontStyle.Bold;
            Ui.Anchor(state.rectTransform, new Vector2(0.5f, 1f), new Vector2(380f, 28f), new Vector2(0f, -76f));

            var body = Ui.Label(paper, skill.description, 21, TextAnchor.UpperLeft, PaperInk);
            body.rectTransform.anchorMin = new Vector2(0f, 0f);
            body.rectTransform.anchorMax = new Vector2(1f, 1f);
            body.rectTransform.offsetMin = new Vector2(32f, 28f);
            body.rectTransform.offsetMax = new Vector2(-32f, -112f);

            // The icons sit on a wooden shelf under the scroll.
            var shelf = Ui.Rect("SkillShelf", scene);
            Ui.Anchor(shelf, new Vector2(1f, 0f), new Vector2(440f, 112f), new Vector2(-16f, 12f));
            HudArt.Plate(shelf).pixelsPerUnitMultiplier = 1.1f;
            HudArt.DropShadow(shelf);
            var spacing = 440f / skills.Count;
            for (var i = 0; i < skills.Count; i++)
            {
                var index = i;
                var open = skills[i].unlockEvolution <= hero.EvolutionIndex;
                var icon = Medal.Create(shelf, "Skill" + i, open ? MedalKind.Primary : MedalKind.Secondary, null, null, () =>
                {
                    _selectedSkill = index;
                    ShowHeroDetail(hero.InstanceId);
                }, true, 72f, i == _selectedSkill);
                var iconRect = (RectTransform)icon.transform;
                Ui.Anchor(iconRect, new Vector2(0f, 0.5f), new Vector2(72f, 72f), new Vector2(spacing * (i + 0.5f) - 36f, 8f));
                var letter = HudArt.Label(iconRect, skills[i].displayName.Substring(0, 1), HudArt.Display, 28, TextAnchor.MiddleCenter, open ? new Color32(0x2A, 0x1A, 0x08, 0xFF) : HudArt.Ivory, open ? new Color(1f, 0.92f, 0.7f, 0.5f) : HudArt.LacquerDeep);
                Ui.Stretch(letter.rectTransform);
                var caption = HudArt.Label(shelf, open ? "Ready" : EvolutionLabel(skills[i].unlockEvolution), HudArt.Numbers, 13, TextAnchor.MiddleCenter, HudArt.Ivory, HudArt.LacquerDeep);
                Ui.Anchor(caption.rectTransform, new Vector2(0f, 0f), new Vector2(90f, 20f), new Vector2(spacing * (i + 0.5f) - 45f, 2f));

            }
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
