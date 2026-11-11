using System.Collections.Generic;
using System.Linq;
using ShinobiDominion.Core;
using UnityEngine;
using UnityEngine.UI;
using Text = ShinobiDominion.Game.GameText;

namespace ShinobiDominion.Game
{
    /// <summary>
    /// The hero window (ADR-057): one window over the village with a banner for each side of a hero. On the left,
    /// always, the hero in its frame with its level, evolution stars, name plate, padlock and experience. On the
    /// right, the banner that is open: Hero (skills and talent, one explained at a time on parchment), Attributes,
    /// Gear or Mastery. The Back arrow returns to the Altar's shelves and the cross closes everything.
    /// </summary>
    public sealed partial class VillageController
    {
        private enum HeroTab
        {
            Hero,
            Attributes,
            Gear,
            Mastery
        }

        private static readonly Vector2 HeroWindowSize = new Vector2(1060f, 610f);
        private const float HeroLeftWidth = 300f;
        private const float HeroRightInset = 322f;

        private static readonly Color EvolutionBlue = new Color(0.42f, 0.86f, 1f);

        private HeroTab _heroTab;

        /// <summary>Opens the hero window on the banner it was last showing.</summary>
        private void OpenHeroWindow(string instanceId)
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
            var note = _masteryNote;
            var tabs = new List<WindowTab>
            {
                new WindowTab("HeroTab", "Hero", _heroTab == HeroTab.Hero, () => OpenHeroTab(instanceId, HeroTab.Hero)),
                new WindowTab("AttributesTab", "Attributes", _heroTab == HeroTab.Attributes, () => OpenHeroTab(instanceId, HeroTab.Attributes)),
                new WindowTab("GearButton", "Gear", _heroTab == HeroTab.Gear, () => OpenHeroTab(instanceId, HeroTab.Gear)),
                new WindowTab("MasteryButton", "Mastery", _heroTab == HeroTab.Mastery, () => OpenHeroTab(instanceId, HeroTab.Mastery))
            };
            _panel.ShowWindow(archetype.displayName, HeroWindowSize, CloseAll, content =>
            {
                DrawHeroColumn(content, hero, archetype);
                DrawHeroBrowse(content, hero.InstanceId);
                var area = Ui.Rect("HeroTabArea", content);
                Ui.Stretch(area, HeroRightInset, 0f, 0f, 0f);
                switch (_heroTab)
                {
                    case HeroTab.Attributes:
                        DrawAttributesTab(area, hero, archetype);
                        break;
                    case HeroTab.Gear:
                        DrawGearTab(area, hero);
                        break;
                    case HeroTab.Mastery:
                        DrawMasteryTab(area, hero, archetype, note);
                        break;
                    default:
                        DrawSkillsTab(area, hero, archetype);
                        break;
                }
            }, tabs, ShowAltarRoster);
        }

        private void OpenHeroTab(string instanceId, HeroTab tab)
        {
            _heroTab = tab;
            // What the last roll or rank-up gave is said until the player looks elsewhere.
            _masteryNote = null;
            OpenHeroWindow(instanceId);
        }

        /// <summary>Draws the window again after something about the hero changed, on the banner already open.</summary>
        private void RefreshHeroWindow(string instanceId) => OpenHeroWindow(instanceId);

        // ---------- Left: the hero ----------

        /// <summary>The hero in its frame, with level, evolution stars, Evolve, the name plate, the padlock and experience.</summary>
        private void DrawHeroColumn(RectTransform content, HeroInstance hero, HeroArchetype archetype)
        {
            var instanceId = hero.InstanceId;
            var column = Ui.Rect("HeroColumn", content);
            column.anchorMin = new Vector2(0f, 0f);
            column.anchorMax = new Vector2(0f, 1f);
            column.pivot = new Vector2(0f, 0.5f);
            column.sizeDelta = new Vector2(HeroLeftWidth, 0f);
            column.anchoredPosition = Vector2.zero;

            // The portrait.
            var portrait = Ui.Rect("Portrait", column);
            Ui.Anchor(portrait, new Vector2(0.5f, 1f), new Vector2(286f, 300f), Vector2.zero);
            Ui.Framed(portrait, HeroArt.CardBackdrop(archetype.rarity), false).type = Image.Type.Simple;
            var art = Ui.Rect("Art", portrait);
            Ui.Stretch(art, 8f, 8f, 8f, 8f);
            art.gameObject.AddComponent<RectMask2D>();
            if (HeroConcepts.Has(archetype.id))
            {
                // The whole painting, standing on the frame's foot.
                var sprite = HeroConcepts.Full(archetype.id);
                const float height = 284f;
                var figure = Ui.Rect("HeroConcept", art);
                Ui.Anchor(figure, new Vector2(0.5f, 0f), new Vector2(height * sprite.rect.width / sprite.rect.height, height), Vector2.zero);
                var image = figure.gameObject.AddComponent<Image>();
                image.sprite = sprite;
                image.raycastTarget = false;
                UiMotion.PopIn(figure, 0f, 0.94f, 0.26f);
            }
            else HeroPicture(art, archetype, hero.EvolutionIndex, 270f, 4f);

            var frameRect = Ui.Rect("Frame", portrait);
            Ui.Stretch(frameRect);
            var frame = Ui.Framed(frameRect, HudArt.Sprite(archetype.rarity == HeroRarity.Legendary ? "heroframe_gold_s30" : archetype.rarity == HeroRarity.Elite ? "heroframe_silver_s30" : "heroframe_bronze_s30"), false);
            frame.pixelsPerUnitMultiplier = 0.8f;
            if (archetype.rarity == HeroRarity.Legendary) UiMotion.Shine(frame, new Color(1f, 0.95f, 0.75f, 0.9f), 3.6f);

            // Level in the top right corner, over the hero's full level.
            var level = HudArt.Label(portrait, $"Lv {hero.Level}", HudArt.Display, 24, TextAnchor.UpperRight, HudArt.Ivory, HudArt.LacquerDeep);
            level.name = "HeroLevel";
            Ui.Anchor(level.rectTransform, new Vector2(1f, 1f), new Vector2(140f, 32f), new Vector2(-18f, -14f));
            var cap = HudArt.Label(portrait, HeroMath.MaxLevel.ToString(), HudArt.Numbers, 16, TextAnchor.UpperRight, HudArt.IvoryMuted, HudArt.LacquerDeep);
            Ui.Anchor(cap.rectTransform, new Vector2(1f, 1f), new Vector2(140f, 22f), new Vector2(-18f, -44f));

            // Rarity badge, then one glowing blue star per evolution; none before the first.
            var badge = HudArt.Picture(portrait, "RarityBadge", "badge_" + archetype.rarity.ToString().ToLowerInvariant(), new Vector2(0f, 1f), new Vector2(52f, 52f), new Vector2(-10f, 10f));
            badge.preserveAspect = true;
            for (var i = 0; i < hero.EvolutionIndex; i++)
            {
                var glow = Ui.Icon(portrait, HeroArt.Glow, new Vector2(0f, 1f), new Vector2(52f, 52f), new Vector2(34f + i * 30f, -6f));
                glow.color = new Color(EvolutionBlue.r, EvolutionBlue.g, EvolutionBlue.b, 0.6f);
                glow.raycastTarget = false;
                UiMotion.Breathe(glow, 0.4f, 1.8f + i * 0.2f);
                var star = Ui.Icon(portrait, HeroArt.Star, new Vector2(0f, 1f), new Vector2(28f, 28f), new Vector2(46f + i * 30f, -18f));
                star.name = "EvolutionStar";
                star.color = new Color(0.55f, 0.92f, 1f);
                star.raycastTarget = false;
                UiMotion.PopIn(star.transform, 0.08f * i, 0.4f, 0.35f);
            }

            if (archetype.CanEvolve)
            {
                var final = hero.EvolutionIndex >= HeroMath.MaxEvolution;
                var evolve = Ui.Button(portrait, "EvolveButton", final ? "Final Form" : "Evolve", () => ShowEvolution(instanceId), true, new Color(0.35f, 0.45f, 0.85f), 19);
                Ui.Anchor((RectTransform)evolve.transform, new Vector2(0.5f, 0f), new Vector2(150f, 40f), new Vector2(0f, 34f));
            }

            // Name plate across the frame's foot, with the padlock on its left end.
            var plate = HudArt.Picture(column, "NamePlate", "ribbon", new Vector2(0.5f, 1f), new Vector2(316f, 56f), new Vector2(0f, -274f));
            var name = HudArt.Label(plate.transform, archetype.displayName.ToUpperInvariant(), HudArt.Display, 21, TextAnchor.MiddleCenter, HudArt.Ivory, HudArt.CrimsonDark);
            Ui.Stretch(name.rectTransform, 52f, 5f, 30f, 2f);
            name.resizeTextForBestFit = true;
            name.resizeTextMinSize = 12;
            name.resizeTextMaxSize = 21;

            var padlock = Ui.Rect("LockButton", column);
            Ui.Anchor(padlock, new Vector2(0.5f, 1f), new Vector2(44f, 44f), new Vector2(-136f, -280f));
            var face = padlock.gameObject.AddComponent<Image>();
            face.sprite = HudArt.Sprite("medal_face_lacquer");
            var lockButton = padlock.gameObject.AddComponent<Button>();
            lockButton.targetGraphic = face;
            lockButton.onClick.AddListener(() => GameSounds.Play(GameSound.Click));
            lockButton.onClick.AddListener(() =>
            {
                if (IsOnline)
                {
                    SetLockOnline(instanceId, !hero.Locked);
                    return;
                }
                Roster.SetLocked(instanceId, !hero.Locked);
                RefreshHeroWindow(instanceId);
            });
            UiMotion.Pressable(lockButton);
            var lockIcon = HudArt.Picture(padlock, "Icon", hero.Locked ? "icon_lock" : "icon_unlock", new Vector2(0.5f, 0.5f), new Vector2(24f, 24f), Vector2.zero);
            lockIcon.preserveAspect = true;
            lockIcon.color = hero.Locked ? Color.white : new Color(1f, 1f, 1f, 0.55f);

            // What the hero is, in words, and what it is doing.
            var kind = HudArt.Label(column, $"{archetype.rarity}  ·  {archetype.role}  ·  {archetype.affinity}", HudArt.Numbers, 16, TextAnchor.MiddleCenter, HudArt.Ivory, HudArt.LacquerDeep);
            kind.name = "HeroKind";
            Ui.Anchor(kind.rectTransform, new Vector2(0.5f, 1f), new Vector2(HeroLeftWidth, 22f), new Vector2(0f, -334f));
            var guarding = Village.IsHeroAssigned(instanceId);
            var status = HudArt.Label(column, guarding ? "Guarding a Hero Base" : hero.Locked ? "Locked" : "Idle", HudArt.Numbers, 16, TextAnchor.MiddleCenter, guarding ? HudArt.BrassLight : HudArt.IvoryMuted, HudArt.LacquerDeep);
            status.name = "HeroStatus";
            Ui.Anchor(status.rectTransform, new Vector2(0.5f, 1f), new Vector2(HeroLeftWidth, 22f), new Vector2(0f, -356f));

            DrawExperience(column, hero);

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            var dev = Ui.Button(column, "DevLevelButton", "Dev: level 200", () =>
            {
                if (IsOnline)
                {
                    DevMaxLevelOnline(instanceId);
                    return;
                }
                Roster.GrantXp(instanceId, 100_000_000);
                RefreshHeroWindow(instanceId);
            }, hero.Level < HeroMath.MaxLevel, new Color(0.2f, 0.22f, 0.3f), 13);
            Ui.Anchor((RectTransform)dev.transform, new Vector2(0.5f, 0f), new Vector2(150f, 30f), new Vector2(0f, 0f));
#endif
        }

        /// <summary>
        /// The experience bar, and beside it the plus that trains the hero one level for Mana (ADR-038), with its
        /// price underneath. The server sets the price and decides.
        /// </summary>
        private void DrawExperience(RectTransform column, HeroInstance hero)
        {
            var instanceId = hero.InstanceId;
            var top = hero.Level >= HeroMath.MaxLevel;
            var need = top ? 1 : Roster.XpToNextLevel(hero.Level);

            var word = HudArt.Label(column, "Exp", HudArt.Numbers, 15, TextAnchor.MiddleLeft, HudArt.BrassLight, HudArt.LacquerDeep);
            Ui.Anchor(word.rectTransform, new Vector2(0f, 1f), new Vector2(40f, 24f), new Vector2(10f, -388f));
            var bar = Ui.Rect("LevelBar", column);
            Ui.Anchor(bar, new Vector2(0f, 1f), new Vector2(top ? 240f : 196f, 22f), new Vector2(48f, -389f));
            var track = bar.gameObject.AddComponent<Image>();
            track.sprite = HudArt.Sprite("track_s8");
            track.type = Image.Type.Sliced;
            track.color = new Color(0.1f, 0.11f, 0.18f, 1f);
            track.raycastTarget = false;
            var share = top ? 1f : Mathf.Clamp01((float)hero.Experience / need);
            if (share > 0f)
            {
                var fill = Ui.Rect("Fill", bar);
                fill.anchorMin = Vector2.zero;
                fill.anchorMax = new Vector2(Mathf.Max(share, 0.04f), 1f);
                fill.offsetMin = new Vector2(2f, 2f);
                fill.offsetMax = new Vector2(-2f, -2f);
                var image = fill.gameObject.AddComponent<Image>();
                image.sprite = HudArt.Sprite("fill_s8");
                image.type = Image.Type.Sliced;
                image.color = new Color(0.95f, 0.78f, 0.25f);
                image.raycastTarget = false;
            }
            var count = HudArt.Label(bar, top ? "Highest level" : $"{hero.Experience:N0} / {need:N0}", HudArt.Numbers, 14, TextAnchor.MiddleCenter, HudArt.Ivory, HudArt.LacquerDeep);
            count.name = "ExperienceCount";
            Ui.Stretch(count.rectTransform);
            count.resizeTextForBestFit = true;
            count.resizeTextMinSize = 10;
            count.resizeTextMaxSize = 14;
            if (top) return;

            var manaCost = Roster.ManaToLevelUp(hero.Level);
            var plus = Ui.Rect("TrainHeroButton", column);
            Ui.Anchor(plus, new Vector2(0f, 1f), new Vector2(40f, 40f), new Vector2(250f, -380f));
            var plusImage = plus.gameObject.AddComponent<Image>();
            plusImage.sprite = HudArt.Sprite("hud_plus_button");
            plusImage.preserveAspect = true;
            var can = Village.Mana >= manaCost;
            plusImage.color = can ? Color.white : new Color(0.55f, 0.55f, 0.6f, 1f);
            var train = plus.gameObject.AddComponent<Button>();
            train.targetGraphic = plusImage;
            train.interactable = can;
            var colors = train.colors;
            colors.disabledColor = Color.white;
            train.colors = colors;
            train.onClick.AddListener(() => GameSounds.Play(GameSound.Click));
            train.onClick.AddListener(() => TrainHero(instanceId));
            UiMotion.Pressable(train);
            plus.gameObject.AddComponent<DeniedShake>();

            var price = Ui.Rect("TrainCost", column);
            Ui.Anchor(price, new Vector2(0f, 1f), new Vector2(170f, 30f), new Vector2(48f, -418f));
            ScreenKit.Chip(price, "icon_chakra", $"+1 level  {manaCost:N0}");
        }

        /// <summary>Arrows outside the window's sides step through the heroes the Altar is showing.</summary>
        private void DrawHeroBrowse(RectTransform content, string instanceId)
        {
            var list = SortedHeroes(Roster.Heroes.Where(MatchesFilters)).ToList();
            var index = list.FindIndex(h => h.InstanceId == instanceId);
            if (list.Count < 2 || index < 0) return;
            var previous = list[(index - 1 + list.Count) % list.Count].InstanceId;
            var next = list[(index + 1) % list.Count].InstanceId;
            var left = Medal.Create(content, "PreviousHeroButton", MedalKind.Primary, "back", null, () => OpenHeroWindow(previous), true, 62f);
            Ui.Anchor((RectTransform)left.transform, new Vector2(0f, 0.5f), new Vector2(62f, 62f), new Vector2(-114f, 0f));
            var right = Medal.Create(content, "NextHeroButton", MedalKind.Primary, "next", null, () => OpenHeroWindow(next), true, 62f);
            Ui.Anchor((RectTransform)right.transform, new Vector2(1f, 0.5f), new Vector2(62f, 62f), new Vector2(114f, 0f));
        }

        // ---------- Right: Hero (skills and talent) ----------

        // Each skill slot's kind and picture: the first is the basic attack, the last the ultimate.
        private static readonly string[] MiddleIcons = { "target", "speed", "skill" };

        private static string SkillKind(HeroSkill skill, int index, int count) => skill.special ? "Special" : index == 0 ? "Basic" : index == count - 1 ? "Ultimate" : "Skill";

        private static string SkillIcon(int index, int count) => index == 0 ? "attack" : index == count - 1 ? "shield" : MiddleIcons[(index - 1) % MiddleIcons.Length];

        /// <summary>A sheet of parchment filling part of the tab, with a blue plate for its heading on the top edge.</summary>
        private static RectTransform Sheet(RectTransform area, float top, float bottom, string heading)
        {
            var paper = Ui.Rect("Sheet", area);
            Ui.Stretch(paper, 0f, bottom, 0f, top);
            Ui.Framed(paper, HudArt.Sprite("parchment_s40"), false).pixelsPerUnitMultiplier = 1.4f;
            if (!string.IsNullOrEmpty(heading))
            {
                var plate = HudArt.Picture(paper, "SheetHeading", "hud_building_nameplate", new Vector2(0.5f, 1f), new Vector2(320f, 46f), new Vector2(0f, 16f));
                var words = HudArt.Label(plate.transform, heading, HudArt.Display, 21, TextAnchor.MiddleCenter, HudArt.BrassLight, HudArt.LacquerDeep);
                words.name = "SheetTitle";
                Ui.Stretch(words.rectTransform, 40f, 2f, 40f, 2f);
            }
            return paper;
        }

        /// <summary>
        /// A shelf of square icons, one for each skill and one for the talent, and under it the chosen one explained
        /// on parchment. The special skill's icon carries its technique rank and the talent's icon its level.
        /// </summary>
        private void DrawSkillsTab(RectTransform area, HeroInstance hero, HeroArchetype archetype)
        {
            var instanceId = hero.InstanceId;
            var skills = archetype.skills;
            var talents = Roster.Catalog.Talents;
            var technique = Roster.Catalog.Technique;
            var talent = talents.Find(hero.TalentId);
            _selectedSkill = Mathf.Clamp(_selectedSkill, 0, skills.Count);
            var onTalent = _selectedSkill == skills.Count;

            var shelf = Ui.Rect("SkillShelf", area);
            shelf.anchorMin = new Vector2(0f, 1f);
            shelf.anchorMax = new Vector2(1f, 1f);
            shelf.pivot = new Vector2(0.5f, 1f);
            shelf.sizeDelta = new Vector2(0f, 96f);
            HudArt.Plate(shelf).pixelsPerUnitMultiplier = 1.3f;

            const float icon = 70f;
            const float gap = 14f;
            var slots = skills.Count + 1;
            var total = slots * icon + (slots - 1) * gap + 18f;
            for (var i = 0; i < slots; i++)
            {
                var index = i;
                var isTalent = i == skills.Count;
                var skill = isTalent ? null : skills[i];
                var open = isTalent ? talent != null : skill.unlockEvolution <= hero.EvolutionIndex;
                var selected = i == _selectedSkill;
                // The talent stands a little apart from the skills.
                var x = -total * 0.5f + icon * 0.5f + i * (icon + gap) + (isTalent ? 18f : 0f);

                var slot = Ui.Rect(isTalent ? "TalentIcon" : "Skill" + i, shelf);
                Ui.Anchor(slot, new Vector2(0.5f, 0.5f), new Vector2(icon, icon), new Vector2(x, 0f));
                if (selected)
                {
                    var lit = HudArt.Picture(slot, "Selected", "glow", new Vector2(0.5f, 0.5f), new Vector2(icon + 44f, icon + 44f), Vector2.zero);
                    lit.color = new Color(1f, 0.82f, 0.4f, 0.75f);
                }
                var well = Ui.Rect("Well", slot);
                Ui.Stretch(well);
                var frame = Ui.Framed(well, HudArt.Sprite(selected ? "card_s34" : "well_s18"), true);
                frame.pixelsPerUnitMultiplier = selected ? 1.7f : 1.3f;
                var button = slot.gameObject.AddComponent<Button>();
                button.targetGraphic = frame;
                button.onClick.AddListener(() => GameSounds.Play(GameSound.Click));
                button.onClick.AddListener(() =>
                {
                    _selectedSkill = index;
                    RefreshHeroWindow(instanceId);
                });
                UiMotion.Pressable(button);

                var picture = HudArt.Picture(slot, "Icon", isTalent ? "icon_star" : "icon_" + SkillIcon(i, skills.Count), new Vector2(0.5f, 0.5f), new Vector2(40f, 40f), new Vector2(0f, 6f));
                picture.preserveAspect = true;
                picture.color = open ? Color.white : new Color(0.45f, 0.45f, 0.5f, 0.9f);

                // Under the picture: the rank of the special skill, the level of the talent, or the stars a locked skill needs.
                string under = null;
                if (isTalent) under = talent == null ? "None" : $"{hero.TalentLevel}/{talents.maxLevel}";
                else if (skill.special && open) under = $"{hero.TechniqueRank}/{technique.maxRank}";
                if (under != null)
                {
                    var mark = HudArt.Label(slot, under, HudArt.Numbers, 15, TextAnchor.LowerCenter, HudArt.Ivory, HudArt.LacquerDeep);
                    mark.name = isTalent ? "TalentMark" : "SkillRank";
                    Ui.Stretch(mark.rectTransform, 2f, 3f, 2f, 0f);
                }
                if (!isTalent && !open)
                {
                    var locked = HudArt.Picture(slot, "Lock", "icon_lock", new Vector2(0.5f, 0.5f), new Vector2(24f, 24f), new Vector2(0f, 8f));
                    locked.preserveAspect = true;
                    for (var s = 0; s < skill.unlockEvolution; s++)
                    {
                        var needStar = Ui.Icon(slot, HeroArt.Star, new Vector2(0.5f, 0f), new Vector2(14f, 14f), new Vector2((s - (skill.unlockEvolution - 1) * 0.5f) * 15f, 5f));
                        needStar.name = "NeedStar";
                        needStar.color = EvolutionBlue;
                        needStar.raycastTarget = false;
                    }
                }
            }

            var paper = Sheet(area, 116f, 0f, onTalent ? "Hero Talent" : "Hero Skills");
            string title;
            string tag;
            string body;
            Color tagColor;
            if (onTalent)
            {
                title = talent == null ? "No talent yet" : $"{talent.displayName}, level {hero.TalentLevel}";
                tag = talent == null ? "A roll at the Mastery banner gives this hero one" : TalentEffect(talent, hero.TalentLevel);
                tagColor = new Color(0.18f, 0.45f, 0.14f);
                body = talent == null ? "A talent is one lasting gift: more HP, attack or defense, a faster walk, a head start on the special skill, or harder blows against buildings." : talent.description;
            }
            else
            {
                var chosen = skills[_selectedSkill];
                var unlocked = chosen.unlockEvolution <= hero.EvolutionIndex;
                title = chosen.displayName;
                var kind = SkillKind(chosen, _selectedSkill, skills.Count);
                tag = unlocked
                    ? chosen.special ? $"{kind}  ·  rank {hero.TechniqueRank} of {technique.maxRank}  ·  power {technique.Power(BattleSim.PowerFor(archetype.rarity.ToString()), hero.TechniqueRank)}%" : $"{kind}  ·  Unlocked"
                    : $"{kind}  ·  Locked: evolve {chosen.unlockEvolution} time{(chosen.unlockEvolution == 1 ? "" : "s")}";
                tagColor = unlocked ? new Color(0.18f, 0.45f, 0.14f) : new Color(0.62f, 0.2f, 0.1f);
                body = chosen.description;
            }

            var name = Ui.Label(paper, title, 27, TextAnchor.MiddleCenter, PaperTitle);
            name.name = "SkillName";
            name.font = HudArt.Display;
            name.fontStyle = FontStyle.Bold;
            name.rectTransform.anchorMin = new Vector2(0f, 1f);
            name.rectTransform.anchorMax = new Vector2(1f, 1f);
            name.rectTransform.pivot = new Vector2(0.5f, 1f);
            name.rectTransform.sizeDelta = new Vector2(-60f, 40f);
            name.rectTransform.anchoredPosition = new Vector2(0f, -40f);
            name.resizeTextForBestFit = true;
            name.resizeTextMinSize = 16;
            name.resizeTextMaxSize = 27;

            var status = Ui.Label(paper, tag, 17, TextAnchor.MiddleCenter, tagColor);
            status.name = "SkillStatus";
            status.fontStyle = FontStyle.Bold;
            status.rectTransform.anchorMin = new Vector2(0f, 1f);
            status.rectTransform.anchorMax = new Vector2(1f, 1f);
            status.rectTransform.pivot = new Vector2(0.5f, 1f);
            status.rectTransform.sizeDelta = new Vector2(-60f, 26f);
            status.rectTransform.anchoredPosition = new Vector2(0f, -82f);
            status.resizeTextForBestFit = true;
            status.resizeTextMinSize = 12;
            status.resizeTextMaxSize = 17;

            var text = Ui.Label(paper, body, 21, TextAnchor.UpperCenter, PaperInk);
            text.name = "SkillBody";
            text.rectTransform.anchorMin = Vector2.zero;
            text.rectTransform.anchorMax = Vector2.one;
            text.rectTransform.offsetMin = new Vector2(44f, 84f);
            text.rectTransform.offsetMax = new Vector2(-44f, -122f);

            // The one thing that can be done about what is shown: raise the special skill's rank, or change the talent.
            var special = !onTalent && skills[_selectedSkill].special && skills[_selectedSkill].unlockEvolution <= hero.EvolutionIndex;
            if (onTalent || special)
            {
                var act = Ui.Button(paper, onTalent ? "TalentChangeButton" : "SkillRankButton", onTalent ? (talent == null ? "Roll" : "Change") : "Rank up", () => OpenHeroTab(instanceId, HeroTab.Mastery), true, new Color(0.35f, 0.45f, 0.85f), 21);
                Ui.Anchor((RectTransform)act.transform, new Vector2(0.5f, 0f), new Vector2(210f, 50f), new Vector2(0f, 24f));
            }
        }

        // ---------- Right: Attributes ----------

        /// <summary>The four battle numbers large on one sheet, and what the hero is and carries listed on a second.</summary>
        private void DrawAttributesTab(RectTransform area, HeroInstance hero, HeroArchetype archetype)
        {
            var stats = Roster.StatsOf(hero);
            var numbers = Ui.Rect("AttributeNumbers", area);
            numbers.anchorMin = new Vector2(0f, 1f);
            numbers.anchorMax = new Vector2(1f, 1f);
            numbers.pivot = new Vector2(0.5f, 1f);
            numbers.sizeDelta = new Vector2(0f, 156f);
            Ui.Framed(numbers, HudArt.Sprite("parchment_s40"), false).pixelsPerUnitMultiplier = 1.4f;
            var upper = Ui.Rect("Upper", numbers);
            upper.anchorMin = new Vector2(0f, 0.5f);
            upper.anchorMax = Vector2.one;
            upper.offsetMin = new Vector2(8f, 0f);
            upper.offsetMax = new Vector2(-8f, -10f);
            ScreenKit.Fact(upper, 0f, 0.5f, "icon_attack_stat", "Attack", stats.Attack.ToString("N0"));
            ScreenKit.Fact(upper, 0.5f, 1f, "icon_health", "HP", stats.Hp.ToString("N0"));
            var lower = Ui.Rect("Lower", numbers);
            lower.anchorMin = Vector2.zero;
            lower.anchorMax = new Vector2(1f, 0.5f);
            lower.offsetMin = new Vector2(8f, 10f);
            lower.offsetMax = new Vector2(-8f, 0f);
            ScreenKit.Fact(lower, 0f, 0.5f, "icon_defense_stat", "Defense", stats.Defense.ToString("N0"));
            ScreenKit.Fact(lower, 0.5f, 1f, "icon_power", "Might", (stats.Hp / 10 + stats.Attack + stats.Defense).ToString("N0"));

            var paper = Sheet(area, 170f, 0f, null);
            var technique = Roster.Catalog.Technique;
            var talent = Roster.Catalog.Talents.Find(hero.TalentId);
            var gear = Roster.Catalog.Gear;
            var lines = new List<(string name, string value)>
            {
                ("Rarity", archetype.rarity.ToString()),
                ("Role", archetype.role.ToString()),
                ("Affinity", archetype.affinity.ToString()),
                ("Form", hero.EvolutionIndex == 0 ? "Base" : $"Evolved {hero.EvolutionIndex} time{(hero.EvolutionIndex == 1 ? "" : "s")}"),
                ("Technique", $"Rank {hero.TechniqueRank} of {technique.maxRank}, special power {technique.Power(BattleSim.PowerFor(archetype.rarity.ToString()), hero.TechniqueRank)}%"),
                ("Talent", talent == null ? "None" : $"{talent.displayName} {hero.TalentLevel}: {TalentEffect(talent, hero.TalentLevel)}"),
                ("Gear", string.Join(",  ", Enumerable.Range(0, 3).Select(s => $"{gear.slots[s]} {hero.Gear[s]}")))
            };
            const float lineHeight = 36f;
            for (var i = 0; i < lines.Count; i++)
            {
                var y = -22f - i * lineHeight;
                var label = Ui.Label(paper, lines[i].name, 18, TextAnchor.MiddleRight, ScreenKit.InkBrownSoft);
                label.rectTransform.anchorMin = new Vector2(0f, 1f);
                label.rectTransform.anchorMax = new Vector2(0.26f, 1f);
                label.rectTransform.pivot = new Vector2(0.5f, 1f);
                label.rectTransform.sizeDelta = new Vector2(0f, lineHeight);
                label.rectTransform.anchoredPosition = new Vector2(0f, y);
                var value = Ui.Label(paper, lines[i].value, 19, TextAnchor.MiddleLeft, ScreenKit.InkBrown);
                value.name = "Attribute_" + lines[i].name;
                value.fontStyle = FontStyle.Bold;
                value.rectTransform.anchorMin = new Vector2(0.26f, 1f);
                value.rectTransform.anchorMax = new Vector2(1f, 1f);
                value.rectTransform.pivot = new Vector2(0.5f, 1f);
                value.rectTransform.sizeDelta = new Vector2(-48f, lineHeight);
                value.rectTransform.anchoredPosition = new Vector2(-6f, y);
                value.verticalOverflow = VerticalWrapMode.Truncate;
                value.resizeTextForBestFit = true;
                value.resizeTextMinSize = 12;
                value.resizeTextMaxSize = 19;
            }
        }
    }
}
