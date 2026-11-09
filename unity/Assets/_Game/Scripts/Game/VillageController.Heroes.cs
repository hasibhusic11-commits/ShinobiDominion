using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ShinobiDominion.Core;
using UnityEngine;
using UnityEngine.UI;

namespace ShinobiDominion.Game
{
    /// <summary>
    /// Local roster store. PROTOTYPE ONLY, like FileVillageStore: Phase 3 moves heroes and items
    /// to the server. Release builds start empty, since summoning is the only real hero source.
    /// </summary>
    public sealed class FileRosterStore
    {
        private readonly string _path;

        public FileRosterStore(string path)
        {
            _path = path;
        }

        public string Path => _path;

        public HeroRoster LoadOrCreate(HeroCatalog catalog)
        {
            if (File.Exists(_path))
            {
                try
                {
                    return HeroRoster.FromJson(catalog, File.ReadAllText(_path));
                }
                catch (Exception exception) when (exception is VillageSaveException || exception is IOException)
                {
                    Debug.LogWarning($"Hero roster rejected, starting fresh: {exception.Message}");
                    var backup = _path + ".rejected";
                    if (File.Exists(backup)) File.Delete(backup);
                    File.Move(_path, backup);
                }
            }
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            return DevInventory.Create(catalog);
#else
            return new HeroRoster(catalog);
#endif
        }

        public void Save(HeroRoster roster)
        {
            var temp = _path + ".tmp";
            File.WriteAllText(temp, roster.ToJson());
            if (File.Exists(_path)) File.Delete(_path);
            File.Move(temp, _path);
        }
    }

    public sealed partial class VillageController
    {
        private static readonly Vector2 HeroCardSize = new Vector2(176f, 244f);

        private FileRosterStore _rosterStore;
        private bool _rosterDirty;
        private HeroRarity? _rarityFilter;
        private HeroRole? _roleFilter;
        private HeroAffinity? _affinityFilter;
        private string _evolveKey;

        public HeroRoster Roster { get; private set; }

        private void OnRosterChanged()
        {
            // Online, the server holds the truth, and a local copy would only invite tampering.
            _rosterDirty = !IsOnline;
            _view.Sync();
        }

        private void FlushRoster()
        {
            if (!_rosterDirty || _rosterStore == null) return;
            _rosterDirty = false;
            try
            {
                _rosterStore.Save(Roster);
            }
            catch (IOException exception)
            {
                Debug.LogError($"Could not save heroes: {exception.Message}");
            }
        }

        private Color HeroColor(HeroArchetype archetype, int evolution)
        {
            var form = archetype.CanEvolve && evolution < archetype.forms.Count ? archetype.forms[evolution] : null;
            ColorUtility.TryParseHtmlString(form != null ? form.primaryColor : archetype.primaryColor, out var color);
            return color;
        }

        private HeroLook LookOf(HeroArchetype archetype, int evolution) =>
            new HeroLook { DesignId = archetype.id, Color = HeroColor(archetype, evolution), Evolution = evolution, Role = archetype.role, Affinity = archetype.affinity };

        /// <summary>Shows the hero's actual model, standing with its feet at the given height.</summary>
        /// <summary>Above this size a picture is the large hero view and shows the whole painting.</summary>
        private const float FullPictureSize = 300f;

        private void HeroPicture(RectTransform parent, HeroArchetype archetype, int evolution, float size, float feet)
        {
            // A hero with a concept painting always shows it. Evolved forms have no paintings of their own yet, and the
            // small built-in figure looked far worse (owner, 2026-11-09), so they show their base painting until they do.
            if (HeroConcepts.Has(archetype.id))
            {
                ConceptPicture(parent, archetype.id, size, feet);
                return;
            }
            _portraits = _portraits ?? new HeroPortraits(_worldRoot);
            var picture = Ui.Rect("HeroPicture", parent);
            picture.anchorMin = picture.anchorMax = new Vector2(0.5f, 0f);
            picture.pivot = new Vector2(0.5f, 0f);
            picture.sizeDelta = new Vector2(size, size);
            picture.anchoredPosition = new Vector2(0f, feet);
            var image = picture.gameObject.AddComponent<UnityEngine.UI.RawImage>();
            image.texture = _portraits.For(LookOf(archetype, evolution));
            image.raycastTarget = false;
        }

        private static void ConceptPicture(RectTransform parent, string heroId, float size, float feet)
        {
            var full = size >= FullPictureSize;
            var sprite = full ? HeroConcepts.Full(heroId) : HeroConcepts.Card(heroId);
            var picture = Ui.Rect("HeroConcept", parent);
            picture.anchorMin = picture.anchorMax = new Vector2(0.5f, 0f);
            picture.pivot = new Vector2(0.5f, 0f);
            // The whole painting fits inside the hero view; a card's crop sits just above the hero's name.
            var height = full ? size * 0.78f : size - 10f;
            picture.sizeDelta = new Vector2(height * sprite.rect.width / sprite.rect.height, height);
            picture.anchoredPosition = new Vector2(0f, full ? feet : feet + 14f);
            var image = picture.gameObject.AddComponent<Image>();
            image.sprite = sprite;
            image.preserveAspect = true;
            image.raycastTarget = false;
        }

        private HeroLook HeroLookFor(string instanceId)
        {
            var hero = Roster?.Find(instanceId);
            if (hero == null) return null;
            var archetype = Roster.ArchetypeOf(hero);
            return LookOf(archetype, hero.EvolutionIndex);
        }

        private static string EvolutionLabel(int evolution) =>
            evolution == 0 ? "Base" : "Evo " + new string('I', evolution);

        private static Color RarityColor(HeroRarity rarity)
        {
            switch (rarity)
            {
                case HeroRarity.Legendary: return UiSkin.Brass;
                case HeroRarity.Elite: return new Color(0.40f, 0.62f, 0.85f);
                default: return new Color(0.58f, 0.60f, 0.62f);
            }
        }


        // ---------- Hero cards ----------

        /// <summary>
        /// A roster card: the hero painted edge to edge over a rarity sunburst, inside a metal frame.
        /// Level at the top, name and rarity (written out, never colour alone) at the bottom.
        /// </summary>
        private Button HeroCard(Transform parent, string name, HeroInstance hero, string tag, Action onClick, bool interactable)
        {
            var archetype = Roster.ArchetypeOf(hero);
            var rect = Ui.Rect(name, parent);
            var back = Ui.Framed(rect, HeroArt.CardBackdrop(archetype.rarity));
            back.type = Image.Type.Simple;
            var button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = back;
            button.interactable = interactable;
            var colors = button.colors;
            colors.disabledColor = new Color(0.5f, 0.5f, 0.5f, 0.8f);
            button.colors = colors;
            if (onClick != null) button.onClick.AddListener(() => onClick());
            UiMotion.Pressable(button);

            // The painting fills the card and is clipped by it, like a trading card.
            var art = Ui.Rect("Art", rect);
            Ui.Stretch(art, 6f, 6f, 6f, 6f);
            art.gameObject.AddComponent<RectMask2D>();
            if (hero.EvolutionIndex == 0 && HeroConcepts.Has(archetype.id))
            {
                var painting = Ui.Rect("HeroConcept", art);
                Ui.Stretch(painting);
                var image = Ui.Framed(painting, HeroConcepts.Card(archetype.id), false);
                image.type = Image.Type.Simple;
            }
            else HeroPicture(art, archetype, hero.EvolutionIndex, 170f, 46f);

            var shade = Ui.Rect("Shade", rect);
            shade.anchorMin = new Vector2(0f, 0f);
            shade.anchorMax = new Vector2(1f, 0f);
            shade.pivot = new Vector2(0.5f, 0f);
            shade.sizeDelta = new Vector2(-12f, 92f);
            shade.anchoredPosition = new Vector2(0f, 6f);
            Ui.Framed(shade, HeroArt.Shade, false).type = Image.Type.Simple;

            var frameRect = Ui.Rect("Frame", rect);
            Ui.Stretch(frameRect);
            var frame = Ui.Framed(frameRect, HudArt.Sprite(archetype.rarity == HeroRarity.Legendary ? "heroframe_gold_s30" : archetype.rarity == HeroRarity.Elite ? "heroframe_silver_s30" : "heroframe_bronze_s30"), false);
            frame.pixelsPerUnitMultiplier = 1.15f;
            // Legendary heroes catch the light, and wear a gem on the frame.
            if (archetype.rarity == HeroRarity.Legendary)
            {
                UiMotion.Shine(frame, new Color(1f, 0.95f, 0.75f, 0.9f), 3.2f);
                Ui.Icon(rect, UiSkin.Diamond(new Color(1f, 0.55f, 0.35f), new Color(0.7f, 0.12f, 0.08f)), new Vector2(0.5f, 1f), new Vector2(26f, 26f), new Vector2(0f, 10f));
            }

            var level = HudArt.Label(rect, $"Lv {hero.Level}", HudArt.Numbers, 21, TextAnchor.UpperRight, HudArt.Ivory, HudArt.LacquerDeep);
            Ui.Anchor(level.rectTransform, new Vector2(1f, 1f), new Vector2(110f, 28f), new Vector2(-14f, -12f));
            var role = Ui.Icon(rect, HudArt.Sprite("medal_face_lacquer"), new Vector2(0f, 1f), new Vector2(34f, 34f), new Vector2(10f, -10f));
            HudArt.Picture(role.transform, "Ring", "medal_ring_thin", new Vector2(0.5f, 0.5f), role.rectTransform.sizeDelta, Vector2.zero);
            var roleLetter = HudArt.Label(role.transform, archetype.role.ToString().Substring(0, 1), HudArt.Display, 16, TextAnchor.MiddleCenter, HudArt.Ivory, HudArt.LacquerDeep);
            Ui.Stretch(roleLetter.rectTransform);

            var title = HudArt.Label(rect, archetype.displayName, HudArt.Display, 15, TextAnchor.MiddleCenter, HudArt.Ivory, HudArt.LacquerDeep);
            Ui.Anchor(title.rectTransform, new Vector2(0.5f, 0f), new Vector2(166f, 26f), new Vector2(0f, 36f));
            var rarity = Ui.Strong(Ui.Label(rect, string.IsNullOrEmpty(tag) ? archetype.rarity.ToString() : tag, 14, TextAnchor.MiddleCenter, RarityColor(archetype.rarity)));
            Ui.Anchor(rarity.rectTransform, new Vector2(0.5f, 0f), new Vector2(166f, 22f), new Vector2(0f, 14f));

            // Evolved heroes wear their stage on a badge at the bottom edge.
            if (hero.EvolutionIndex > 0)
            {
                var badge = Ui.Icon(rect, UiSkin.Diamond(new Color(1f, 0.87f, 0.4f), new Color(0.9f, 0.55f, 0.12f)), new Vector2(0.5f, 0f), new Vector2(36f, 36f), new Vector2(0f, -14f));
                var stage = HudArt.Label(badge.transform, new string('I', hero.EvolutionIndex), HudArt.Display, 14, TextAnchor.MiddleCenter, HudArt.Ivory, HudArt.CrimsonDark);
                Ui.Stretch(stage.rectTransform);
            }
            return button;
        }

        // ---------- Hero Base ----------

        private void HeroBaseActions(PanelView panel, BuildingInstance building)
        {
            var hero = Roster.Find(building.AssignedHeroInstanceId);
            if (hero == null)
            {
                panel.Line("Assigned hero: none");
                panel.Action("ChangeHeroButton", "Assign Hero", () => ShowHeroPicker(building.Id), Roster.HeroCount > 0, Ui.AccentColor);
                if (Roster.HeroCount == 0) panel.Line("You own no heroes yet. Summon at the Summoning Gate.", 20);
                return;
            }

            var archetype = Roster.ArchetypeOf(hero);
            panel.Line($"{archetype.displayName}  ·  {archetype.rarity}", 22, Ui.TextColor);
            panel.Line($"{EvolutionLabel(hero.EvolutionIndex)}  Lv {hero.Level}  ·  {archetype.role}  ·  {archetype.affinity}");
            panel.Action("ChangeHeroButton", "Change Hero", () => ShowHeroPicker(building.Id), true, Ui.AccentColor);
            panel.Action("UnassignButton", "Unassign", () =>
            {
                if (IsOnline) AssignOnline(building.Id, null);
                else RunAndRefresh(Village.TryUnassignHero(building.Id));
            });
        }

        private void ShowHeroPicker(int baseId)
        {
            _selectionCells.Clear();
            _panel.ShowScreen("Choose a Hero", null, ShowContextPanel, panel =>
            {
                var grid = panel.Grid(HeroCardSize);
                foreach (var hero in SortedHeroes(Roster.Heroes))
                {
                    var here = Village.Find(baseId)?.AssignedHeroInstanceId == hero.InstanceId;
                    var elsewhere = !here && Village.IsHeroAssigned(hero.InstanceId);
                    var tag = here ? "On this base" : elsewhere ? "On another base" : null;
                    HeroCard(grid, "Pick_" + hero.InstanceId, hero, tag, () => AssignHero(baseId, hero.InstanceId), !here && !elsewhere);
                }
            });
        }

        /// <summary>Assigns only a hero the player owns. The one-base rule is enforced in Core.</summary>
        public VillageError AssignHero(int baseId, string heroInstanceId)
        {
            if (!Roster.Owns(heroInstanceId)) return VillageError.InvalidHeroId;
            if (RefuseWhenNoServer()) return VillageError.None;
            if (IsOnline)
            {
                AssignOnline(baseId, heroInstanceId);
                return VillageError.None;
            }
            var error = Village.TryAssignHero(baseId, heroInstanceId);
            LastMessage = VillageErrorText.Describe(error);
            if (UiState.Screen == VillageScreen.BuildingPanel) ShowContextPanel();
            return error;
        }

        // ---------- Hero Altar ----------

        private IEnumerable<HeroInstance> SortedHeroes(IEnumerable<HeroInstance> heroes) => heroes
            .OrderByDescending(h => Roster.ArchetypeOf(h).rarity)
            .ThenBy(h => Roster.ArchetypeOf(h).displayName)
            .ThenByDescending(h => HeroMath.Cumulative(h.EvolutionIndex, h.Level));

        public void ShowAltarRoster()
        {
            _selectionCells.Clear();
            var shown = SortedHeroes(Roster.Heroes.Where(MatchesFilters)).ToList();
            _panel.ShowScreen("Hero Altar", $"{shown.Count} of {Roster.HeroCount} heroes", CloseAll, panel =>
            {
                var filters = panel.Row();
                FilterTab(filters, "All", _rarityFilter == null, () => _rarityFilter = null);
                foreach (HeroRarity rarity in Enum.GetValues(typeof(HeroRarity)))
                {
                    var value = rarity;
                    FilterTab(filters, value.ToString(), _rarityFilter == value, () => _rarityFilter = value);
                }
                CycleTab(filters, "FilterRole", $"Role: {(_roleFilter?.ToString() ?? "All")}", () => _roleFilter = Next(_roleFilter));
                CycleTab(filters, "FilterAffinity", $"Affinity: {(_affinityFilter?.ToString() ?? "All")}", () => _affinityFilter = Next(_affinityFilter));

                if (Roster.HeroCount == 0) panel.Line("You own no heroes yet. Summon at the Summoning Gate.");

                var grid = panel.Grid(HeroCardSize);
                foreach (var hero in shown)
                {
                    var tag = Village.IsHeroAssigned(hero.InstanceId) ? "On a base" : hero.Locked ? "Locked" : null;
                    HeroCard(grid, "Hero_" + hero.InstanceId, hero, tag, () => ShowHeroDetail(hero.InstanceId), true);
                }

                panel.Action("AltarOptionsButton", "Altar building options", () =>
                {
                    ShowContextPanel();
                });
            });
        }

        private bool MatchesFilters(HeroInstance hero)
        {
            var archetype = Roster.ArchetypeOf(hero);
            return (_rarityFilter == null || archetype.rarity == _rarityFilter)
                   && (_roleFilter == null || archetype.role == _roleFilter)
                   && (_affinityFilter == null || archetype.affinity == _affinityFilter);
        }

        private static T? Next<T>(T? current) where T : struct, Enum
        {
            var values = (T[])Enum.GetValues(typeof(T));
            if (current == null) return values[0];
            var index = Array.IndexOf(values, current.Value);
            return index + 1 < values.Length ? values[index + 1] : (T?)null;
        }

        private void FilterTab(RectTransform row, string caption, bool active, Action apply)
        {
            var tab = Ui.Button(row, "Filter" + caption, caption, () =>
            {
                apply();
                ShowAltarRoster();
            }, true, active ? Ui.AccentColor : Ui.ButtonColor, 15);
            tab.GetComponentInChildren<Text>().horizontalOverflow = HorizontalWrapMode.Overflow;
        }

        private void CycleTab(RectTransform row, string name, string caption, Action apply)
        {
            var tab = Ui.Button(row, name, caption, () =>
            {
                apply();
                ShowAltarRoster();
            }, true, Ui.ButtonColor, 15);
            tab.GetComponentInChildren<Text>().horizontalOverflow = HorizontalWrapMode.Overflow;
        }

        // ---------- Evolution ----------

        private void FormCard(Transform parent, HeroArchetype archetype, int evolution, bool current)
        {
            var rect = Ui.Rect("Form" + evolution, parent);
            Ui.Framed(rect, HudArt.Sprite("card_s34"), false).pixelsPerUnitMultiplier = 1.15f;
            var well = Ui.Rect("Well", rect);
            Ui.Anchor(well, new Vector2(0.5f, 1f), new Vector2(176f, 150f), new Vector2(0f, -40f));
            Ui.Framed(well, HudArt.Sprite("well_s18"), false);
            if (current)
            {
                var halo = HudArt.Picture(rect, "Glow", "glow", new Vector2(0.5f, 1f), new Vector2(200f, 160f), new Vector2(0f, -36f));
                halo.color = new Color(1f, 0.85f, 0.45f, 0.45f);
            }
            var art = Ui.Rect("Art", well);
            Ui.Stretch(art, 4f, 4f, 4f, 4f);
            art.gameObject.AddComponent<RectMask2D>();
            HeroPicture(art, archetype, evolution, 150f, 0f);
            var form = archetype.forms[evolution];
            var title = HudArt.Label(rect, (current ? "Now: " : string.Empty) + EvolutionLabel(evolution), HudArt.Display, 17, TextAnchor.MiddleCenter, current ? HudArt.BrassLight : HudArt.Ivory, HudArt.LacquerDeep);
            Ui.Anchor(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(190f, 28f), new Vector2(0f, -10f));
            var name = HudArt.Label(rect, form.formName, HudArt.Display, 15, TextAnchor.MiddleCenter, HudArt.BrassLight, HudArt.LacquerDeep);
            Ui.Anchor(name.rectTransform, new Vector2(0.5f, 0f), new Vector2(180f, 24f), new Vector2(0f, 62f));
            var summary = Ui.Label(rect, form.summary, 13, TextAnchor.UpperCenter, Ui.MutedText);
            Ui.Anchor(summary.rectTransform, new Vector2(0.5f, 0f), new Vector2(178f, 54f), new Vector2(0f, 10f));
        }

        public void ShowEvolution(string instanceId)
        {
            var hero = Roster.Find(instanceId);
            if (hero == null)
            {
                ShowAltarRoster();
                return;
            }

            var archetype = Roster.ArchetypeOf(hero);
            _panel.ShowScreen($"{archetype.displayName}: Evolution", null, () => ShowHeroDetail(instanceId), panel =>
            {
                var forms = panel.Grid(new Vector2(204f, 276f));
                for (var e = 0; e < archetype.forms.Count; e++)
                    FormCard(forms, archetype, e, e == hero.EvolutionIndex);

                if (hero.EvolutionIndex >= HeroMath.MaxEvolution)
                {
                    panel.Line("Final form reached.", 24, Ui.TextColor);
                    return;
                }

                var catalyst = Roster.CatalystFor(hero.EvolutionIndex);
                var owned = Roster.ItemCount(catalyst);
                var duplicates = Roster.EligibleDuplicates(instanceId, Village.IsHeroAssigned);

                Requirement(panel, hero.Level == HeroMath.MaxLevel, $"Level 200 in this form (now {hero.Level})");
                Requirement(panel, duplicates.Count > 0, $"One extra copy of {archetype.displayName} ({duplicates.Count} available)");
                Requirement(panel, owned > 0, $"One {ItemName(catalyst)} (you have {owned})");

                var mainError = Roster.CheckEvolutionMain(instanceId);
                if (mainError != HeroError.None)
                {
                    panel.Line(HeroErrorText.Describe(mainError), 21, Ui.AccentColor);
                    return;
                }
                if (duplicates.Count == 0)
                {
                    panel.Line("Locked copies and copies on a Hero Base cannot be used.", 21, Ui.AccentColor);
                    return;
                }

                panel.Line("Choose the copy to sacrifice:", 22, Ui.TextColor);
                var grid = panel.Grid(HeroCardSize);
                foreach (var duplicate in duplicates)
                    HeroCard(grid, "Sacrifice_" + duplicate.InstanceId, duplicate, "Sacrifice", () => ShowEvolutionConfirm(instanceId, duplicate.InstanceId), true);
            });
        }

        /// <summary>One evolution requirement on its own plaque: a green check or a red cross, then what is needed.</summary>
        private static void Requirement(PanelView panel, bool met, string text)
        {
            var row = panel.Block(50f);
            var plate = Ui.Rect("Requirement", row);
            Ui.Stretch(plate, 0f, 0f, 380f, 0f);
            HudArt.Plate(plate);
            HudArt.Picture(plate, met ? "Met" : "NotMet", met ? "icon_check" : "icon_cross", new Vector2(0f, 0.5f), new Vector2(30f, 30f), new Vector2(16f, 1f));
            var label = HudArt.Label(plate, text, HudArt.Numbers, 18, TextAnchor.MiddleLeft, met ? HudArt.Ivory : HudArt.IvoryMuted, HudArt.LacquerDeep);
            Ui.Stretch(label.rectTransform, 58f, 4f, 16f, 2f);
            label.text = (met ? "Met: " : "Not met: ") + text;
        }

        private static string ItemName(string itemId) =>
            string.Join(" ", itemId.Split('_').Select(word => char.ToUpperInvariant(word[0]) + word.Substring(1)));

        private void ShowEvolutionConfirm(string mainId, string duplicateId)
        {
            var main = Roster.Find(mainId);
            var duplicate = Roster.Find(duplicateId);
            if (main == null || duplicate == null)
            {
                ShowAltarRoster();
                return;
            }

            // One key per confirmation screen, so a double tap or a retry can never evolve twice.
            _evolveKey = Guid.NewGuid().ToString("N");
            var key = _evolveKey;
            var archetype = Roster.ArchetypeOf(main);
            var before = Roster.StatsOf(main);
            var after = HeroMath.Stats(archetype, main.EvolutionIndex + 1, 1);

            _panel.ShowScreen("Confirm Evolution", null, () => ShowEvolution(mainId), panel =>
            {
                var forms = panel.Grid(new Vector2(204f, 276f));
                FormCard(forms, archetype, main.EvolutionIndex, true);
                FormCard(forms, archetype, main.EvolutionIndex + 1, false);

                panel.Line($"Now: {EvolutionLabel(main.EvolutionIndex)} Lv {main.Level}     HP {before.Hp:N0}     Attack {before.Attack:N0}     Defense {before.Defense:N0}", 21, Ui.TextColor);
                panel.Line($"After: {EvolutionLabel(main.EvolutionIndex + 1)} Lv 1     HP {after.Hp:N0}     Attack {after.Attack:N0}     Defense {after.Defense:N0}", 21, Ui.TextColor);
                panel.Line("The displayed level resets to 1, but combat power does not drop.", 19);
                panel.Line($"This permanently consumes your {archetype.displayName} copy ({EvolutionLabel(duplicate.EvolutionIndex)} Lv {duplicate.Level}) and one {ItemName(Roster.CatalystFor(main.EvolutionIndex))}.", 21, Ui.AccentColor);
                if (IsOnline && !string.IsNullOrEmpty(_pendingKey) && _hasError) panel.Line(LastMessage, 20, Ui.AccentColor);
                panel.Action("ConfirmEvolveButton", "Evolve", () =>
                {
                    if (IsOnline) ConfirmEvolutionOnline(mainId, duplicateId);
                    else ConfirmEvolution(key, mainId, duplicateId);
                }, true, Ui.AttackColor);
                panel.Action("CancelEvolveButton", "Cancel", () => ShowEvolution(mainId));
            });
        }

        public HeroError ConfirmEvolution(string key, string mainId, string duplicateId)
        {
            var error = Roster.TryEvolve(key, mainId, duplicateId, Village.IsHeroAssigned);
            LastMessage = HeroErrorText.Describe(error);
            ShowEvolution(mainId);
            return error;
        }
    }
}
