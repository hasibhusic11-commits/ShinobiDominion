using System.Linq;
using LitMotion;
using ShinobiDominion.Core;
using UnityEngine;
using UnityEngine.UI;

namespace ShinobiDominion.Game
{
    /// <summary>
    /// The hero page (owner's brief, 2026-11-08): a three-column hero profile. Left, a compact framed stat panel;
    /// centre, the hero on a ceremonial dais in a shrine hall, with the name plate, blue evolution stars and the
    /// Evolve button under them; right, all four skills in a list with the chosen one explained below.
    /// Evolution shows only as glowing blue stars, one per evolution, none before the first.
    /// </summary>
    public sealed partial class VillageController
    {
        // Columns, as shares of the screen width.
        private const float PageCentre = 0.465f;
        private const float LeftWidth = 272f;
        private const float RightWidth = 372f;

        private static readonly Color EvolutionBlue = new Color(0.42f, 0.86f, 1f);

        private void DrawHeroPage(RectTransform scene, HeroInstance hero, HeroArchetype archetype)
        {
            var aura = HeroColor(archetype, hero.EvolutionIndex);
            var backdrop = Ui.Framed(scene, HeroArt.Shrine(aura, PageCentre), false);
            backdrop.type = Image.Type.Simple;
            backdrop.name = "ShrineBackdrop";

            DrawShrineDressing(scene, aura);
            DrawHeroShowcase(scene, hero, archetype, aura);
            DrawHeroBrowse(scene, hero.InstanceId);
            DrawHeroStats(scene, hero, archetype, aura);
            DrawSkillPanel(scene, hero, archetype);
        }

        /// <summary>Banners on the pillars, stone lanterns beside the dais, and slow motes rising in the hero's colour.</summary>
        private void DrawShrineDressing(RectTransform scene, Color aura)
        {
            foreach (var side in new[] { -1f, 1f })
            {
                var x = PageCentre + side * 0.205f;
                var banner = HudArt.Picture(scene, "Banner", "banner_tomoe", new Vector2(x, 1f), new Vector2(62f, 100f), new Vector2(0f, -96f));
                banner.preserveAspect = true;
                var lantern = Resources.Load<Sprite>("Props/stone_lantern");
                if (lantern != null)
                {
                    var image = Ui.Icon(scene, lantern, new Vector2(PageCentre, 0f), new Vector2(58f, 102f), new Vector2(side * 262f, 112f));
                    image.name = "Lantern";
                    image.preserveAspect = true;
                    image.raycastTarget = false;
                }
            }

            // Motes drift up from the dais and fade, each on its own slow loop.
            var random = new System.Random(7);
            for (var i = 0; i < 14; i++)
            {
                var mote = Ui.Icon(scene, HeroArt.Glow, new Vector2(PageCentre, 0f), Vector2.one * (6f + (float)random.NextDouble() * 8f), Vector2.zero);
                mote.name = "Mote";
                mote.raycastTarget = false;
                var x = ((float)random.NextDouble() - 0.5f) * 360f;
                var rect = mote.rectTransform;
                var duration = 4f + (float)random.NextDouble() * 3f;
                var tint = Color.Lerp(aura, Color.white, 0.5f);
                LMotion.Create(0f, 1f, duration).WithLoops(-1).WithDelay((float)random.NextDouble() * duration)
                    .WithScheduler(MotionScheduler.UpdateIgnoreTimeScale)
                    .Bind(t =>
                    {
                        if (rect == null) return;
                        rect.anchoredPosition = new Vector2(x + Mathf.Sin(t * 6.28f + x) * 14f, 130f + t * 420f);
                        mote.color = new Color(tint.r, tint.g, tint.b, Mathf.Sin(t * Mathf.PI) * 0.8f);
                    }).AddTo(mote.gameObject);
            }
        }

        /// <summary>The hero on a ceremonial dais, with an aura behind them, the name plate, evolution stars and Evolve.</summary>
        private void DrawHeroShowcase(RectTransform scene, HeroInstance hero, HeroArchetype archetype, Color aura)
        {
            // The dais: a shadow, a stone disc with a gold rim, and a glowing affinity ring on top.
            Ui.Icon(scene, UiSkin.Disc(new Color(0f, 0f, 0f, 0.55f), new Color(0f, 0f, 0f, 0.55f), new Color(0f, 0f, 0f, 0f), 0f), new Vector2(PageCentre, 0f), new Vector2(470f, 110f), new Vector2(0f, 98f)).raycastTarget = false;
            Ui.Icon(scene, UiSkin.Disc(new Color(0.42f, 0.38f, 0.40f), new Color(0.2f, 0.18f, 0.22f), new Color(0.93f, 0.73f, 0.33f), 0.07f), new Vector2(PageCentre, 0f), new Vector2(430f, 96f), new Vector2(0f, 110f)).raycastTarget = false;
            var ring = Ui.Icon(scene, UiSkin.Disc(new Color(0f, 0f, 0f, 0f), new Color(0f, 0f, 0f, 0f), Color.Lerp(aura, Color.white, 0.35f), 0.1f), new Vector2(PageCentre, 0f), new Vector2(330f, 70f), new Vector2(0f, 124f));
            ring.raycastTarget = false;
            UiMotion.Breathe(ring, 0.9f, 2.6f);

            var halo = HudArt.Picture(scene, "Aura", "glow", new Vector2(PageCentre, 0f), new Vector2(520f, 520f), new Vector2(0f, 150f));
            halo.color = new Color(aura.r, aura.g, aura.b, 0.45f);
            UiMotion.Breathe(halo, 0.55f, 3.2f);

            const float height = 470f;
            if (HeroConcepts.Has(archetype.id))
            {
                var sprite = HeroConcepts.Full(archetype.id);
                var figure = Ui.Rect("HeroConcept", scene);
                Ui.Anchor(figure, new Vector2(PageCentre, 0f), new Vector2(height * sprite.rect.width / sprite.rect.height, height), new Vector2(0f, 132f));
                var image = Ui.Framed(figure, sprite, false);
                image.type = Image.Type.Simple;
                image.name = "HeroConcept";
                UiMotion.PopIn(figure, 0f, 0.92f, 0.3f);
            }
            else
            {
                var holder = Ui.Rect("HeroFigure", scene);
                Ui.Anchor(holder, new Vector2(PageCentre, 0f), new Vector2(420f, 420f), new Vector2(0f, 132f));
                CentreOnPage(holder);
                HeroPicture(holder, archetype, hero.EvolutionIndex, 420f, 0f);
            }

            // Name plate: the hero's real name, on the crimson ribbon.
            var plate = HudArt.Picture(scene, "NamePlate", "ribbon", new Vector2(PageCentre, 0f), new Vector2(440f, 74f), new Vector2(0f, 70f));
            CentreOnPage(plate.rectTransform);
            var name = HudArt.Label(plate.transform, archetype.displayName.ToUpperInvariant(), HudArt.Display, 26, TextAnchor.MiddleCenter, HudArt.Ivory, HudArt.CrimsonDark);
            Ui.Stretch(name.rectTransform, 44f, 6f, 44f, 2f);
            name.resizeTextForBestFit = true;
            name.resizeTextMinSize = 16;
            name.resizeTextMaxSize = 26;

            DrawEvolutionStars(scene, hero.EvolutionIndex);

            if (archetype.CanEvolve)
            {
                var final = hero.EvolutionIndex >= HeroMath.MaxEvolution;
                var evolve = Ui.Button(scene, "EvolveButton", final ? "Final Form" : "Evolve", () => ShowEvolution(hero.InstanceId), true, Ui.AccentColor, 24);
                Ui.Anchor((RectTransform)evolve.transform, new Vector2(PageCentre, 0f), new Vector2(230f, 58f), new Vector2(0f, 10f));
                CentreOnPage((RectTransform)evolve.transform);
            }
        }

        /// <summary>
        /// Ui.Anchor puts the pivot where the anchor is, so at PageCentre each element would shift by its own width.
        /// Pivoting on its middle puts the figure, stars, ribbon and Evolve button on one centre line.
        /// </summary>
        private static void CentreOnPage(RectTransform rect) => rect.pivot = new Vector2(0.5f, rect.pivot.y);

        /// <summary>
        /// One glowing blue star per evolution, centred just above the name plate; nothing at all before the first
        /// evolution. Replaces the old Ready / Evo I / Evo II / Evo III row.
        /// </summary>
        private void DrawEvolutionStars(RectTransform scene, int evolutions)
        {
            if (evolutions <= 0) return;
            var row = Ui.Rect("EvolutionStars", scene);
            Ui.Anchor(row, new Vector2(PageCentre, 0f), new Vector2(evolutions * 54f, 54f), new Vector2(0f, 126f));
            CentreOnPage(row);
            for (var i = 0; i < evolutions; i++)
            {
                var x = (i - (evolutions - 1) * 0.5f) * 54f;
                var glow = Ui.Icon(row, HeroArt.Glow, new Vector2(0.5f, 0.5f), new Vector2(96f, 96f), new Vector2(x, 0f));
                glow.color = new Color(EvolutionBlue.r, EvolutionBlue.g, EvolutionBlue.b, 0.7f);
                glow.raycastTarget = false;
                UiMotion.Breathe(glow, 0.45f, 1.8f + i * 0.2f);
                var star = Ui.Icon(row, HeroArt.Star, new Vector2(0.5f, 0.5f), new Vector2(48f, 48f), new Vector2(x, 0f));
                star.name = "EvolutionStar";
                star.color = new Color(0.55f, 0.92f, 1f);
                star.raycastTarget = false;
                var outline = star.gameObject.AddComponent<Outline>();
                outline.effectColor = new Color(0.05f, 0.15f, 0.35f, 0.9f);
                outline.effectDistance = new Vector2(1.5f, -1.5f);
                UiMotion.PopIn(star.transform, 0.08f * i, 0.4f, 0.35f);
            }
        }

        /// <summary>Arrows either side of the hero, clear of the painting, step through the heroes the Altar is showing.</summary>
        private void DrawHeroBrowse(RectTransform scene, string instanceId)
        {
            var list = SortedHeroes(Roster.Heroes.Where(MatchesFilters)).ToList();
            var index = list.FindIndex(h => h.InstanceId == instanceId);
            if (list.Count < 2 || index < 0) return;
            var previous = list[(index - 1 + list.Count) % list.Count].InstanceId;
            var next = list[(index + 1) % list.Count].InstanceId;
            var left = Medal.Create(scene, "PreviousHeroButton", MedalKind.Primary, "back", null, () => ShowHeroDetail(previous), true, 70f);
            Ui.Anchor((RectTransform)left.transform, new Vector2(PageCentre, 0.5f), new Vector2(70f, 70f), new Vector2(-262f, 10f));
            var right = Medal.Create(scene, "NextHeroButton", MedalKind.Primary, "next", null, () => ShowHeroDetail(next), true, 70f);
            Ui.Anchor((RectTransform)right.transform, new Vector2(PageCentre, 0.5f), new Vector2(70f, 70f), new Vector2(262f, 10f));
        }

        /// <summary>A compact framed panel: rarity, level and experience, the main stats with icons, role and affinity, Lock.</summary>
        private void DrawHeroStats(RectTransform scene, HeroInstance hero, HeroArchetype archetype, Color aura)
        {
            var box = Ui.Rect("HeroStats", scene);
            Ui.Anchor(box, new Vector2(0f, 1f), new Vector2(LeftWidth, 0f), new Vector2(14f, -82f));
            box.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            HudArt.Plate(box).pixelsPerUnitMultiplier = 1.1f;
            HudArt.DropShadow(box);
            var layout = box.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(22, 22, 18, 20);
            layout.spacing = 6f;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;

            // Rarity badge and name.
            var top = StatRow(box, 40f);
            var badge = HudArt.Picture(top, "RarityBadge", "badge_" + archetype.rarity.ToString().ToLowerInvariant(), new Vector2(0f, 0.5f), new Vector2(38f, 38f), Vector2.zero);
            badge.preserveAspect = true;
            var rarity = HudArt.Label(top, archetype.rarity.ToString().ToUpperInvariant(), HudArt.Display, 21, TextAnchor.MiddleLeft, RarityColor(archetype.rarity), HudArt.LacquerDeep);
            Ui.Stretch(rarity.rectTransform, 46f, 0f, 0f, 0f);

            // Level and the experience bar.
            var level = HudArt.Label(box, $"Lv {hero.Level}/{HeroMath.MaxLevel}", HudArt.Display, 25, TextAnchor.MiddleLeft, HudArt.BrassLight, HudArt.LacquerDeep);
            level.gameObject.AddComponent<LayoutElement>().preferredHeight = 32f;
            var bar = Ui.Rect("LevelBar", box);
            bar.gameObject.AddComponent<LayoutElement>().preferredHeight = 12f;
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
            Divider(box);

            var stats = Roster.StatsOf(hero);
            StatLine(box, "health", "HP", stats.Hp.ToString("N0"), new Color(0.6f, 1f, 0.6f));
            StatLine(box, "attack_stat", "Attack", stats.Attack.ToString("N0"), new Color(1f, 0.72f, 0.55f));
            StatLine(box, "defense_stat", "Defense", stats.Defense.ToString("N0"), new Color(0.65f, 0.85f, 1f));
            StatLine(box, "power", "Might", (stats.Hp / 10 + stats.Attack + stats.Defense).ToString("N0"), HudArt.BrassLight);
            Divider(box);
            StatLine(box, "hero", "Role", archetype.role.ToString(), HudArt.Ivory);
            StatLine(box, "star", "Affinity", archetype.affinity.ToString(), Color.Lerp(aura, Color.white, 0.35f));
            if (Village.IsHeroAssigned(hero.InstanceId))
            {
                var guarding = HudArt.Label(box, "Guarding your Hero Base", HudArt.Numbers, 16, TextAnchor.MiddleCenter, HudArt.BrassLight, HudArt.LacquerDeep);
                guarding.gameObject.AddComponent<LayoutElement>().preferredHeight = 24f;
            }

            var instanceId = hero.InstanceId;
            // Hero training (ADR-038): Mana buys one level. The server sets the price and decides.
            if (hero.Level < HeroMath.MaxLevel)
            {
                var manaCost = Roster.ManaToLevelUp(hero.Level);
                Ui.Button(box, "TrainHeroButton", $"Train +1 level ({manaCost:N0} Mana)", () => TrainHero(instanceId), Village.Mana >= manaCost, Ui.AccentColor, 19);
            }
            var gearTotal = hero.Gear.Sum();
            Ui.Button(box, "GearButton", gearTotal == 0 ? "Gear" : $"Gear (W{hero.Gear[HeroMath.Weapon]} A{hero.Gear[HeroMath.Armor]} C{hero.Gear[HeroMath.Charm]})", () => ShowGear(instanceId), true, null, 19);
            Ui.Button(box, "MasteryButton", MasteryLabel(hero), () => ShowMastery(instanceId), true, null, 19);
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
            }, hero.Level < HeroMath.MaxLevel, new Color(0.2f, 0.22f, 0.3f), 15);
#endif
        }

        private static RectTransform StatRow(RectTransform parent, float height)
        {
            var row = Ui.Rect("Row", parent);
            row.gameObject.AddComponent<LayoutElement>().preferredHeight = height;
            return row;
        }

        /// <summary>One stat: an icon, a muted label on the left, the value large and bright on the right.</summary>
        private static void StatLine(RectTransform parent, string icon, string label, string value, Color valueColor)
        {
            var row = StatRow(parent, 30f);
            var picture = HudArt.Picture(row, "Icon", "icon_" + icon, new Vector2(0f, 0.5f), new Vector2(26f, 26f), Vector2.zero);
            picture.preserveAspect = true;
            var name = HudArt.Label(row, label, HudArt.Numbers, 17, TextAnchor.MiddleLeft, HudArt.IvoryMuted, HudArt.LacquerDeep);
            Ui.Stretch(name.rectTransform, 34f, 0f, 0f, 0f);
            var number = HudArt.Label(row, value, HudArt.Numbers, 21, TextAnchor.MiddleRight, valueColor, HudArt.LacquerDeep);
            Ui.Stretch(number.rectTransform, 90f, 0f, 0f, 0f);
        }

        private static void Divider(RectTransform parent)
        {
            var line = StatRow(parent, 6f);
            var rule = Ui.Rect("Rule", line);
            rule.anchorMin = new Vector2(0f, 0.5f);
            rule.anchorMax = new Vector2(1f, 0.5f);
            rule.sizeDelta = new Vector2(0f, 2f);
            Ui.Background(rule, new Color(0.9f, 0.7f, 0.35f, 0.35f), false);
        }

        // Each skill slot's kind and picture: the first is the basic attack, the last the ultimate.
        private static readonly string[] MiddleIcons = { "target", "speed", "skill" };

        private static string SkillKind(HeroSkill skill, int index, int count) => skill.special ? "Special" : index == 0 ? "Basic" : index == count - 1 ? "Ultimate" : "Skill";

        private static string SkillIcon(int index, int count) => index == 0 ? "attack" : index == count - 1 ? "shield" : MiddleIcons[(index - 1) % MiddleIcons.Length];

        /// <summary>
        /// All four skills in a framed list (picture, name, kind, unlocked or the evolution star it needs); the
        /// chosen one is lit and explained on parchment underneath.
        /// </summary>
        private void DrawSkillPanel(RectTransform scene, HeroInstance hero, HeroArchetype archetype)
        {
            var panel = Ui.Rect("SkillPanel", scene);
            panel.anchorMin = new Vector2(1f, 1f);
            panel.anchorMax = new Vector2(1f, 1f);
            panel.pivot = new Vector2(1f, 1f);
            panel.sizeDelta = new Vector2(RightWidth, 200f);
            panel.anchoredPosition = new Vector2(-14f, -82f);
            HudArt.Plate(panel).pixelsPerUnitMultiplier = 1.1f;
            HudArt.DropShadow(panel);

            var header = HudArt.Picture(panel, "SkillsHeader", "hud_building_nameplate", new Vector2(0.5f, 1f), new Vector2(RightWidth - 40f, 46f), new Vector2(0f, -14f));
            var heading = HudArt.Label(header.transform, hero.TechniqueRank > 1 ? $"SKILLS - RANK {hero.TechniqueRank}" : "SKILLS", HudArt.Display, 22, TextAnchor.MiddleCenter, HudArt.BrassLight, HudArt.LacquerDeep);
            Ui.Stretch(heading.rectTransform);

            var skills = archetype.skills;
            if (skills.Count == 0)
            {
                var none = HudArt.Label(panel, "No skills yet.", HudArt.Numbers, 20, TextAnchor.MiddleCenter, HudArt.Ivory, HudArt.LacquerDeep);
                Ui.Stretch(none.rectTransform);
                return;
            }
            _selectedSkill = Mathf.Clamp(_selectedSkill, 0, skills.Count - 1);

            var rowHeight = skills.Count > 4 ? 74f : 88f;
            const float listTop = 70f;
            // The panel hugs the list and a short explanation, so no empty parchment hangs under it.
            panel.sizeDelta = new Vector2(RightWidth, listTop + skills.Count * rowHeight + 190f);
            for (var i = 0; i < skills.Count; i++)
            {
                var index = i;
                var skill = skills[i];
                var open = skill.unlockEvolution <= hero.EvolutionIndex;
                var selected = i == _selectedSkill;

                var row = Ui.Rect("Skill" + i, panel);
                Ui.Anchor(row, new Vector2(0.5f, 1f), new Vector2(RightWidth - 36f, rowHeight - 6f), new Vector2(0f, -listTop - i * rowHeight));
                var frame = Ui.Framed(row, HudArt.Sprite(selected ? "card_s34" : "well_s18"), true);
                frame.pixelsPerUnitMultiplier = selected ? 1.6f : 1.2f;
                if (selected)
                {
                    var lit = HudArt.Picture(row, "Selected", "glow", new Vector2(0.5f, 0.5f), new Vector2(RightWidth, rowHeight + 20f), Vector2.zero);
                    lit.color = new Color(1f, 0.8f, 0.4f, 0.35f);
                    lit.transform.SetAsFirstSibling();
                }
                var button = row.gameObject.AddComponent<Button>();
                button.targetGraphic = frame;
                button.onClick.AddListener(() => GameSounds.Play(GameSound.Click));
                button.onClick.AddListener(() =>
                {
                    _selectedSkill = index;
                    ShowHeroDetail(hero.InstanceId);
                });
                UiMotion.Pressable(button);

                var medal = Medal.Create(row, "SkillIcon", MedalKind.IconOnly, SkillIcon(i, skills.Count), null, null, open, 64f);
                Ui.Anchor((RectTransform)medal.transform, new Vector2(0f, 0.5f), new Vector2(64f, 64f), new Vector2(10f, 0f));
                medal.GetComponent<Button>().enabled = false;

                var title = HudArt.Label(row, skill.displayName, HudArt.Display, 20, TextAnchor.LowerLeft, open ? HudArt.Ivory : HudArt.IvoryMuted, HudArt.LacquerDeep);
                // One line, kept clear of the lock on a locked row: a long name gets a smaller size to fit the space.
                var titleRoom = RightWidth - 36f - 84f - (open ? 12f : 52f);
                Ui.Stretch(title.rectTransform, 84f, rowHeight * 0.46f, open ? 12f : 52f, 6f);
                title.horizontalOverflow = HorizontalWrapMode.Overflow;
                title.fontSize = Mathf.Clamp((int)(titleRoom / (skill.displayName.Length * 0.7f)), 13, 20);
                var kind = SkillKind(skill, i, skills.Count);
                var sub = HudArt.Label(row, kind, HudArt.Numbers, 16, TextAnchor.UpperLeft, open ? HudArt.BrassLight : new Color(0.6f, 0.8f, 1f), HudArt.LacquerDeep);
                Ui.Stretch(sub.rectTransform, 84f, 6f, 8f, rowHeight * 0.56f);
                if (!open)
                {
                    // A lock, and beside it the blue stars this skill needs, matching the evolution stars under the hero.
                    for (var s = 0; s < skill.unlockEvolution; s++)
                    {
                        // On the lower line, beside the skill kind, so the name above has the width.
                        var need = Ui.Icon(row, HeroArt.Star, new Vector2(1f, 0f), new Vector2(18f, 18f), new Vector2(-52f - s * 19f, 8f));
                        need.name = "NeedStar";
                        need.color = EvolutionBlue;
                        need.raycastTarget = false;
                    }
                    var lockIcon = HudArt.Picture(row, "Lock", "icon_lock", new Vector2(1f, 0f), new Vector2(22f, 22f), new Vector2(-24f, 22f));
                    lockIcon.preserveAspect = true;
                }
            }

            // The chosen skill, explained on parchment under the list.
            var chosen = skills[_selectedSkill];
            var unlocked = chosen.unlockEvolution <= hero.EvolutionIndex;
            var paper = Ui.Rect("SkillDetail", panel);
            paper.anchorMin = new Vector2(0f, 0f);
            paper.anchorMax = new Vector2(1f, 1f);
            paper.offsetMin = new Vector2(16f, 16f);
            paper.offsetMax = new Vector2(-16f, -(listTop + skills.Count * rowHeight - 6f));
            Ui.Framed(paper, HudArt.Sprite("parchment_s40"), false).pixelsPerUnitMultiplier = 1.4f;

            var name = Ui.Label(paper, chosen.displayName, 23, TextAnchor.MiddleLeft, PaperTitle);
            name.font = HudArt.Display;
            name.fontStyle = FontStyle.Bold;
            Ui.Anchor(name.rectTransform, new Vector2(0f, 1f), new Vector2(RightWidth - 90f, 34f), new Vector2(20f, -10f));
            name.rectTransform.pivot = new Vector2(0f, 1f);
            name.rectTransform.anchoredPosition = new Vector2(20f, -10f);

            var status = Ui.Label(paper, unlocked ? "Unlocked" : $"Locked: evolve {chosen.unlockEvolution} time{(chosen.unlockEvolution == 1 ? "" : "s")}", 16, TextAnchor.MiddleLeft, unlocked ? new Color(0.18f, 0.45f, 0.14f) : new Color(0.62f, 0.2f, 0.1f));
            status.fontStyle = FontStyle.Bold;
            status.rectTransform.anchorMin = status.rectTransform.anchorMax = new Vector2(0f, 1f);
            status.rectTransform.pivot = new Vector2(0f, 1f);
            status.rectTransform.sizeDelta = new Vector2(RightWidth - 90f, 22f);
            status.rectTransform.anchoredPosition = new Vector2(20f, -44f);

            var tag = Ui.Label(paper, SkillKind(chosen, _selectedSkill, skills.Count).ToUpperInvariant(), 13, TextAnchor.MiddleRight, new Color(0.55f, 0.33f, 0.12f));
            tag.fontStyle = FontStyle.Bold;
            tag.rectTransform.anchorMin = tag.rectTransform.anchorMax = new Vector2(1f, 1f);
            tag.rectTransform.pivot = new Vector2(1f, 1f);
            tag.rectTransform.sizeDelta = new Vector2(120f, 22f);
            tag.rectTransform.anchoredPosition = new Vector2(-18f, -16f);

            var body = Ui.Label(paper, chosen.description, 21, TextAnchor.UpperLeft, PaperInk);
            body.rectTransform.anchorMin = Vector2.zero;
            body.rectTransform.anchorMax = Vector2.one;
            body.rectTransform.offsetMin = new Vector2(20f, 14f);
            body.rectTransform.offsetMax = new Vector2(-20f, -72f);
        }
    }
}
