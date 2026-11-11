using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ShinobiDominion.Core;
using UnityEngine;
using UnityEngine.UI;
using Text = ShinobiDominion.Game.GameText;

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
            // Use a form-specific painting if present, retaining the original base artwork as fallback.
            if (HeroConcepts.Has(archetype.id))
            {
                ConceptPicture(parent, archetype.id, evolution, size, feet);
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

        private static void ConceptPicture(RectTransform parent, string heroId, int evolution, float size, float feet)
        {
            var full = size >= FullPictureSize;
            var form = HeroConcepts.AvailableForm(heroId, evolution);
            var sprite = full ? HeroConcepts.Full(heroId, form) : HeroConcepts.Card(heroId, form);
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
            if (HeroConcepts.Has(archetype.id))
            {
                // Match Hero Detail: use the owned evolution's painting when authored,
                // otherwise retain this same hero's base artwork until the new form ships.
                var visualForm = HeroConcepts.AvailableForm(archetype.id, hero.EvolutionIndex);
                var painting = Ui.Rect("HeroConcept", art);
                Ui.Stretch(painting);
                var image = Ui.Framed(painting, HeroConcepts.Card(archetype.id, visualForm), false);
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
            title.horizontalOverflow = HorizontalWrapMode.Wrap;
            title.verticalOverflow = VerticalWrapMode.Truncate;
            title.resizeTextForBestFit = true;
            title.resizeTextMinSize = 10;
            title.resizeTextMaxSize = 15;
            var rarity = Ui.Strong(Ui.Label(rect, string.IsNullOrEmpty(tag) ? archetype.rarity.ToString() : tag, 14, TextAnchor.MiddleCenter, RarityColor(archetype.rarity)));
            Ui.Anchor(rarity.rectTransform, new Vector2(0.5f, 0f), new Vector2(166f, 22f), new Vector2(0f, 14f));

            // Three earned cyan stars (never placeholder stars or a Roman numeral badge),
            // consistent with Hero Detail and the approved v3 progression language.
            SdKit.EvolutionStars(rect, hero.EvolutionIndex, new Vector2(0.5f, 0f), new Vector2(0f, 105f), 19f);
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
                var grid = panel.Grid(BustCell);
                foreach (var hero in SortedHeroes(Roster.Heroes))
                {
                    var here = Village.Find(baseId)?.AssignedHeroInstanceId == hero.InstanceId;
                    var elsewhere = !here && Village.IsHeroAssigned(hero.InstanceId);
                    var tag = here ? "On this base" : elsewhere ? "On another base" : null;
                    ShelfBust(Ui.Rect("Slot", grid), "Pick_" + hero.InstanceId, hero, () => AssignHero(baseId, hero.InstanceId), !here && !elsewhere, tag);
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

        /// <summary>The reference shelf holds five busts, reduced automatically for narrower screens.</summary>
        private const float ShelfHeight = 214f;
        /// <summary>One bust and its lip, where busts stand in a grid and not on the Altar's shelves.</summary>
        private static readonly Vector2 BustCell = new Vector2(186f, 214f);

        private static readonly Color PlankTop = new Color(0.47f, 0.31f, 0.17f);
        private static readonly Color PlankFace = new Color(0.33f, 0.21f, 0.12f);
        private static readonly Color PlankShade = new Color(0.13f, 0.08f, 0.05f);

        /// <summary>
        /// The Heroes Altar (ADR-057): a window over the village with one banner for all heroes and one for each
        /// rarity, each carrying its count, and under them shelves of hero busts, five to a shelf. A bust shows the
        /// hero, its level on the shelf's lip, a padlock when it is locked and a shield when it guards a Hero Base.
        /// </summary>
        public void ShowAltarRoster()
        {
            _selectionCells.Clear();
            var pool = Roster.Heroes.Where(MatchesRoleAndAffinity).ToList();
            var shown = SortedHeroes(pool.Where(MatchesFilters)).ToList();
            var tabs = new List<WindowTab> { new WindowTab("FilterAll", "All", _rarityFilter == null, () => SetRarityFilter(null), pool.Count.ToString()) };
            foreach (var rarity in ((HeroRarity[])Enum.GetValues(typeof(HeroRarity))).Reverse())
            {
                var value = rarity;
                tabs.Add(new WindowTab("Filter" + value, value.ToString(), _rarityFilter == value, () => SetRarityFilter(value),
                    pool.Count(h => Roster.ArchetypeOf(h).rarity == value).ToString()));
            }

            _panel.ShowThemedWindow("Heroes Altar", new Vector2(1100f, 620f), CloseAll, content =>
            {
                SdKit.Tabs(content, tabs);
                var shelves = Ui.Rect("Shelves", content);
                Ui.Stretch(shelves, 0f, 54f, 0f, 62f);
                var list = PanelView.ScrollIn(shelves);
                var width = content.rect.width > 0f ? content.rect.width : 972f;
                var columns = SdKit.RosterColumns(width);
                var rows = Mathf.Max(2, Mathf.CeilToInt(shown.Count / (float)columns));
                list.sizeDelta = new Vector2(0f, rows * ShelfHeight + 6f);
                var cell = width / columns;

                // Every shelf is drawn, the bare ones too, so the Altar reads as a cabinet with room left in it.
                for (var r = 0; r < rows; r++)
                {
                    var plank = Ui.Rect("Shelf" + r, list);
                    plank.anchorMin = new Vector2(0f, 1f);
                    plank.anchorMax = new Vector2(1f, 1f);
                    plank.pivot = new Vector2(0.5f, 0f);
                    plank.sizeDelta = new Vector2(0f, 40f);
                    plank.anchoredPosition = new Vector2(0f, -(r + 1) * ShelfHeight + 6f);
                    Ui.Background(plank, PlankFace, false);
                    var top = Ui.Rect("Top", plank);
                    top.anchorMin = new Vector2(0f, 1f);
                    top.anchorMax = new Vector2(1f, 1f);
                    top.pivot = new Vector2(0.5f, 1f);
                    top.sizeDelta = new Vector2(0f, 7f);
                    Ui.Background(top, PlankTop, false);
                    var shade = Ui.Rect("Shade", plank);
                    shade.anchorMin = Vector2.zero;
                    shade.anchorMax = new Vector2(1f, 0f);
                    shade.pivot = new Vector2(0.5f, 1f);
                    shade.sizeDelta = new Vector2(0f, 8f);
                    Ui.Background(shade, new Color(PlankShade.r, PlankShade.g, PlankShade.b, 0.8f), false);
                }

                for (var i = 0; i < shown.Count; i++)
                {
                    var hero = shown[i];
                    var slot = Ui.Rect("Slot", list);
                    slot.anchorMin = slot.anchorMax = new Vector2(0f, 1f);
                    slot.pivot = new Vector2(0f, 1f);
                    slot.sizeDelta = new Vector2(cell, ShelfHeight);
                    slot.anchoredPosition = new Vector2((i % columns) * cell, -(i / columns) * ShelfHeight);
                    ShelfBust(slot, "Hero_" + hero.InstanceId, hero, () => ShowHeroDetail(hero.InstanceId));
                }

                if (shown.Count == 0)
                {
                    var message = Roster.HeroCount == 0
                        ? "You own no heroes yet. Summon at the Summoning Gate."
                        : "No heroes match these filters.";
                    var none = HudArt.Label(shelves, message, HudArt.Numbers, 20, TextAnchor.MiddleCenter, HudArt.Ivory, HudArt.LacquerDeep);
                    Ui.Stretch(none.rectTransform, 20f, 0f, 20f, 0f);
                }

                // Under the shelves: the two finer filters, each stepping through its values.
                var foot = Ui.Rect("AltarFoot", content);
                foot.anchorMin = Vector2.zero;
                foot.anchorMax = new Vector2(1f, 0f);
                foot.pivot = new Vector2(0.5f, 0f);
                foot.sizeDelta = new Vector2(0f, 46f);
                CycleTab(foot, "FilterRole", $"Role: {(_roleFilter?.ToString() ?? "All")}", -124f, () => _roleFilter = Next(_roleFilter));
                CycleTab(foot, "FilterAffinity", $"Affinity: {(_affinityFilter?.ToString() ?? "All")}", 124f, () => _affinityFilter = Next(_affinityFilter));
            });
        }

        private void SetRarityFilter(HeroRarity? rarity)
        {
            _rarityFilter = rarity;
            ShowAltarRoster();
        }

        /// <summary>
        /// A hero standing on a shelf: the bust in its frame, and on the shelf's lip its level, with a padlock for a
        /// locked hero and a shield for one guarding a Hero Base.
        /// </summary>
        private Button ShelfBust(RectTransform slot, string name, HeroInstance hero, Action onClick, bool interactable = true, string tag = null)
        {
            var root = Ui.Rect(name, slot);
            Ui.Stretch(root, 8f, 6f, 8f, 4f);
            var bust = HeroBust(root, hero, new Vector2(154f, 160f), new Vector2(0.5f, 0f), new Vector2(0f, 40f));
            var button = root.gameObject.AddComponent<Button>();
            button.targetGraphic = bust;
            button.interactable = interactable;
            var colors = button.colors;
            colors.disabledColor = new Color(0.5f, 0.5f, 0.5f, 0.8f);
            button.colors = colors;
            if (onClick != null)
            {
                button.onClick.AddListener(() => GameSounds.Play(GameSound.Click));
                button.onClick.AddListener(() => onClick());
            }
            UiMotion.Pressable(button);

            var lip = Ui.Rect("Lip", root);
            Ui.Anchor(lip, new Vector2(0.5f, 0f), new Vector2(154f, 34f), new Vector2(0f, 2f));
            lip.pivot = new Vector2(0.5f, 0f);
            var words = HudArt.Label(lip, string.IsNullOrEmpty(tag) ? $"Lv {hero.Level}" : tag, HudArt.Numbers, 19, TextAnchor.MiddleCenter, HudArt.Ivory, HudArt.LacquerDeep);
            words.name = "BustLevel";
            Ui.Stretch(words.rectTransform, 30f, 0f, 30f, 0f);
            words.resizeTextForBestFit = true;
            words.resizeTextMinSize = 11;
            words.resizeTextMaxSize = 19;
            if (Village.IsHeroAssigned(hero.InstanceId))
            {
                var guard = HudArt.Picture(lip, "Guarding", "icon_shield", new Vector2(0f, 0.5f), new Vector2(24f, 24f), new Vector2(4f, 0f));
                guard.preserveAspect = true;
            }
            if (hero.Locked)
            {
                var locked = HudArt.Picture(lip, "Locked", "icon_lock", new Vector2(1f, 0.5f), new Vector2(22f, 22f), new Vector2(-4f, 0f));
                locked.preserveAspect = true;
            }
            return button;
        }

        /// <summary>
        /// A hero's bust in a metal frame coloured by rarity, over that rarity's sunburst. The rarity is also said
        /// by a badge in the top left corner, never by colour alone. An evolved hero wears its stage at the foot.
        /// Returns the backdrop, so a button can use it as its graphic.
        /// </summary>
        private Image HeroBust(Transform parent, HeroInstance hero, Vector2 size, Vector2 anchor, Vector2 offset)
        {
            var archetype = Roster.ArchetypeOf(hero);
            var rect = Ui.Rect("Bust", parent);
            Ui.Anchor(rect, anchor, size, offset);
            var back = Ui.Framed(rect, HeroArt.CardBackdrop(archetype.rarity));
            back.type = Image.Type.Simple;

            var art = Ui.Rect("Art", rect);
            Ui.Stretch(art, 6f, 6f, 6f, 6f);
            art.gameObject.AddComponent<RectMask2D>();
            if (HeroConcepts.Has(archetype.id))
            {
                // The painting covers the frame: scaled to fill it, centred, and cut by its edge.
                var sprite = HeroConcepts.Card(archetype.id, HeroConcepts.AvailableForm(archetype.id, hero.EvolutionIndex));
                var box = size - new Vector2(12f, 12f);
                var scale = Mathf.Max(box.x / sprite.rect.width, box.y / sprite.rect.height);
                var painting = Ui.Rect("HeroConcept", art);
                painting.anchorMin = painting.anchorMax = new Vector2(0.5f, 1f);
                painting.pivot = new Vector2(0.5f, 1f);
                painting.sizeDelta = new Vector2(sprite.rect.width * scale, sprite.rect.height * scale);
                var image = painting.gameObject.AddComponent<Image>();
                image.sprite = sprite;
                image.raycastTarget = false;
            }
            else HeroPicture(art, archetype, hero.EvolutionIndex, size.y, 0f);

            var frameRect = Ui.Rect("Frame", rect);
            Ui.Stretch(frameRect);
            var frame = Ui.Framed(frameRect, HudArt.Sprite(archetype.rarity == HeroRarity.Legendary ? "heroframe_gold_s30" : archetype.rarity == HeroRarity.Elite ? "heroframe_silver_s30" : "heroframe_bronze_s30"), false);
            frame.pixelsPerUnitMultiplier = 1.15f;
            if (archetype.rarity == HeroRarity.Legendary) UiMotion.Shine(frame, new Color(1f, 0.95f, 0.75f, 0.9f), 3.2f);

            var badge = HudArt.Picture(rect, "RarityBadge", "badge_" + archetype.rarity.ToString().ToLowerInvariant(), new Vector2(0f, 1f), new Vector2(40f, 40f), new Vector2(-8f, 8f));
            badge.preserveAspect = true;

            SdKit.EvolutionStars(rect, hero.EvolutionIndex, new Vector2(1f, 1f), new Vector2(-52f, -18f), 18f);
            return back;
        }

        private bool MatchesRoleAndAffinity(HeroInstance hero)
        {
            var archetype = Roster.ArchetypeOf(hero);
            return (_roleFilter == null || archetype.role == _roleFilter) && (_affinityFilter == null || archetype.affinity == _affinityFilter);
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

        private void CycleTab(RectTransform foot, string name, string caption, float x, Action apply)
        {
            var tab = Ui.Button(foot, name, caption, () =>
            {
                apply();
                ShowAltarRoster();
            }, true, Ui.ButtonColor, 16);
            Ui.Anchor((RectTransform)tab.transform, new Vector2(0.5f, 0.5f), new Vector2(232f, 40f), new Vector2(x, 0f));
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

                // Centred like the requirement rows above and the cards below.
                panel.Line("Choose the copy to sacrifice:", 22, Ui.TextColor).alignment = TextAnchor.UpperCenter;
                var grid = panel.Grid(BustCell);
                foreach (var duplicate in duplicates)
                    ShelfBust(Ui.Rect("Slot", grid), "Sacrifice_" + duplicate.InstanceId, duplicate, () => ShowEvolutionConfirm(instanceId, duplicate.InstanceId), true, $"Lv {duplicate.Level}");
            });
        }

        /// <summary>One evolution requirement on its own plaque: a green check or a red cross, then what is needed.</summary>
        private static void Requirement(PanelView panel, bool met, string text)
        {
            var row = panel.Block(50f);
            var plate = Ui.Rect("Requirement", row);
            Ui.Anchor(plate, new Vector2(0.5f, 0.5f), new Vector2(760f, 50f), Vector2.zero);
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
