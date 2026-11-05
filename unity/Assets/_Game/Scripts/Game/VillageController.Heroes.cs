using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ShinobiDominion.Core;
using UnityEngine;

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
        private FileRosterStore _rosterStore;
        private bool _rosterDirty;
        private bool _showAltarBuildingOptions;
        private HeroRarity? _rarityFilter;
        private HeroRole? _roleFilter;
        private HeroAffinity? _affinityFilter;
        private string _evolveKey;

        public HeroRoster Roster { get; private set; }

        private void OnRosterChanged()
        {
            _rosterDirty = true;
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

        private HeroLook HeroLookFor(string instanceId)
        {
            var hero = Roster?.Find(instanceId);
            if (hero == null) return null;
            var archetype = Roster.ArchetypeOf(hero);
            var form = archetype.CanEvolve && hero.EvolutionIndex < archetype.forms.Count ? archetype.forms[hero.EvolutionIndex] : null;
            var html = form != null ? form.primaryColor : archetype.primaryColor;
            ColorUtility.TryParseHtmlString(html, out var color);
            return new HeroLook { Color = color, Evolution = hero.EvolutionIndex };
        }

        private string FormName(HeroInstance hero)
        {
            var archetype = Roster.ArchetypeOf(hero);
            if (!archetype.CanEvolve || hero.EvolutionIndex >= archetype.forms.Count) return archetype.displayName;
            return archetype.forms[hero.EvolutionIndex].formName;
        }

        private static string EvolutionLabel(int evolution) =>
            evolution == 0 ? "Base" : "Evo " + new string('I', evolution);

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
            panel.Action("UnassignButton", "Unassign", () => RunAndRefresh(Village.TryUnassignHero(building.Id)));
        }

        private void ShowHeroPicker(int baseId)
        {
            _panel.Show("Choose a Hero", ShowContextPanel, panel =>
            {
                foreach (var hero in SortedHeroes(Roster.Heroes))
                {
                    var archetype = Roster.ArchetypeOf(hero);
                    var here = Village.Find(baseId)?.AssignedHeroInstanceId == hero.InstanceId;
                    var elsewhere = !here && Village.IsHeroAssigned(hero.InstanceId);
                    var suffix = here ? "  (on this base)" : elsewhere ? "  (on another base)" : string.Empty;
                    panel.Action("Pick_" + hero.InstanceId,
                        $"{archetype.displayName}  {EvolutionLabel(hero.EvolutionIndex)}  Lv {hero.Level}{suffix}",
                        () => AssignHero(baseId, hero.InstanceId),
                        !here && !elsewhere, null, 18);
                }
            });
        }

        /// <summary>Assigns only a hero the player owns. The one-base rule is enforced in Core.</summary>
        public VillageError AssignHero(int baseId, string heroInstanceId)
        {
            var error = Roster.Owns(heroInstanceId) ? Village.TryAssignHero(baseId, heroInstanceId) : VillageError.InvalidHeroId;
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
            _panel.Show("Hero Altar", CloseAll, panel =>
            {
                var rarityRow = panel.Row();
                FilterTab(rarityRow, "All", _rarityFilter == null, () => _rarityFilter = null);
                foreach (HeroRarity rarity in Enum.GetValues(typeof(HeroRarity)))
                {
                    var value = rarity;
                    FilterTab(rarityRow, value.ToString(), _rarityFilter == value, () => _rarityFilter = value);
                }

                var cycleRow = panel.Row();
                CycleTab(cycleRow, "FilterRole", $"Role: {(_roleFilter?.ToString() ?? "All")}", () => _roleFilter = Next(_roleFilter));
                CycleTab(cycleRow, "FilterAffinity", $"Affinity: {(_affinityFilter?.ToString() ?? "All")}", () => _affinityFilter = Next(_affinityFilter));

                var shown = SortedHeroes(Roster.Heroes.Where(MatchesFilters)).ToList();
                panel.Line($"Showing {shown.Count} of {Roster.HeroCount} heroes", 19);
                if (Roster.HeroCount == 0) panel.Line("You own no heroes yet. Summon at the Summoning Gate.");

                foreach (var hero in shown)
                {
                    var archetype = Roster.ArchetypeOf(hero);
                    var copies = Roster.Heroes.Count(h => h.ArchetypeId == hero.ArchetypeId);
                    var tags = (Village.IsHeroAssigned(hero.InstanceId) ? "  On base" : string.Empty) + (hero.Locked ? "  Locked" : string.Empty);
                    panel.Action("Hero_" + hero.InstanceId,
                        $"{archetype.displayName}  {EvolutionLabel(hero.EvolutionIndex)}  Lv {hero.Level}  x{copies}{tags}",
                        () => ShowHeroDetail(hero.InstanceId), true, null, 18);
                }

                panel.Action("AltarOptionsButton", "Altar building options", () =>
                {
                    _showAltarBuildingOptions = true;
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
            tab.GetComponentInChildren<UnityEngine.UI.Text>().horizontalOverflow = HorizontalWrapMode.Overflow;
        }

        private void CycleTab(RectTransform row, string name, string caption, Action apply)
        {
            Ui.Button(row, name, caption, () =>
            {
                apply();
                ShowAltarRoster();
            }, true, Ui.ButtonColor, 16);
        }

        public void ShowHeroDetail(string instanceId)
        {
            var hero = Roster.Find(instanceId);
            if (hero == null)
            {
                ShowAltarRoster();
                return;
            }

            var archetype = Roster.ArchetypeOf(hero);
            _panel.Show(archetype.displayName, ShowAltarRoster, panel =>
            {
                panel.Line($"{archetype.rarity}  ·  {archetype.role}  ·  {archetype.affinity}", 21, Ui.TextColor);
                panel.Line($"{FormName(hero)}  ({EvolutionLabel(hero.EvolutionIndex)})  Lv {hero.Level} of {HeroMath.MaxLevel}");
                var stats = Roster.StatsOf(hero);
                panel.Line($"HP {stats.Hp:N0}    Attack {stats.Attack:N0}    Defense {stats.Defense:N0}");
                if (Village.IsHeroAssigned(hero.InstanceId)) panel.Line("Assigned to a Hero Base.");
                panel.Line("Art is placeholder. Equipment arrives in a later phase.", 18);

                panel.Line("Skills", 22, Ui.TextColor);
                foreach (var skill in archetype.skills)
                {
                    var unlocked = skill.unlockEvolution <= hero.EvolutionIndex;
                    panel.Line(unlocked
                        ? $"{skill.displayName}: {skill.description}"
                        : $"Locked until {EvolutionLabel(skill.unlockEvolution)}: {skill.displayName}", 19);
                }

                panel.Action("LockButton", hero.Locked ? "Unlock" : "Lock (protect from sacrifice)", () =>
                {
                    Roster.SetLocked(instanceId, !hero.Locked);
                    ShowHeroDetail(instanceId);
                });
                if (archetype.CanEvolve)
                    panel.Action("EvolveButton", "Evolution", () => ShowEvolution(instanceId), true, Ui.AccentColor);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                panel.Action("DevLevelButton", "Dev only: set to level 200", () =>
                {
                    Roster.GrantXp(instanceId, 100_000_000);
                    ShowHeroDetail(instanceId);
                }, hero.Level < HeroMath.MaxLevel);
#endif
            });
        }

        // ---------- Evolution ----------

        public void ShowEvolution(string instanceId)
        {
            var hero = Roster.Find(instanceId);
            if (hero == null)
            {
                ShowAltarRoster();
                return;
            }

            var archetype = Roster.ArchetypeOf(hero);
            _panel.Show("Evolution", () => ShowHeroDetail(instanceId), panel =>
            {
                for (var e = 0; e < archetype.forms.Count; e++)
                {
                    var marker = e == hero.EvolutionIndex ? "Current: " : string.Empty;
                    panel.Line($"{marker}{EvolutionLabel(e)}  {archetype.forms[e].formName}", 20, e == hero.EvolutionIndex ? Ui.TextColor : Ui.MutedText);
                    panel.Line(archetype.forms[e].summary, 17);
                }

                if (hero.EvolutionIndex >= HeroMath.MaxEvolution)
                {
                    panel.Line("Final form reached.", 22, Ui.TextColor);
                    return;
                }

                var catalyst = Roster.CatalystFor(hero.EvolutionIndex);
                var owned = Roster.ItemCount(catalyst);
                var duplicates = Roster.EligibleDuplicates(instanceId, Village.IsHeroAssigned);

                panel.Line("Requirements", 22, Ui.TextColor);
                panel.Line($"{Check(hero.Level == HeroMath.MaxLevel)} Level 200 in this form (now {hero.Level})");
                panel.Line($"{Check(duplicates.Count > 0)} One extra copy of {archetype.displayName} ({duplicates.Count} available)");
                panel.Line($"{Check(owned > 0)} One {ItemName(catalyst)} (you have {owned})");

                var mainError = Roster.CheckEvolutionMain(instanceId);
                if (mainError != HeroError.None)
                {
                    panel.Line(HeroErrorText.Describe(mainError), 20, Ui.AccentColor);
                    return;
                }
                if (duplicates.Count == 0)
                {
                    panel.Line("Locked copies and copies on a Hero Base cannot be used.", 20, Ui.AccentColor);
                    return;
                }

                panel.Line("Choose the copy to sacrifice:", 21, Ui.TextColor);
                foreach (var duplicate in duplicates)
                {
                    panel.Action("Sacrifice_" + duplicate.InstanceId,
                        $"{EvolutionLabel(duplicate.EvolutionIndex)}  Lv {duplicate.Level}",
                        () => ShowEvolutionConfirm(instanceId, duplicate.InstanceId));
                }
            });
        }

        private static string Check(bool met) => met ? "[Met]" : "[Not met]";

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

            _panel.Show("Confirm Evolution", () => ShowEvolution(mainId), panel =>
            {
                panel.Line($"{archetype.forms[main.EvolutionIndex].formName}  to  {archetype.forms[main.EvolutionIndex + 1].formName}", 22, Ui.TextColor);
                panel.Line($"Now: {EvolutionLabel(main.EvolutionIndex)} Lv {main.Level}   HP {before.Hp:N0}   Attack {before.Attack:N0}   Defense {before.Defense:N0}");
                panel.Line($"After: {EvolutionLabel(main.EvolutionIndex + 1)} Lv 1   HP {after.Hp:N0}   Attack {after.Attack:N0}   Defense {after.Defense:N0}");
                panel.Line("The displayed level resets to 1, but combat power does not drop.", 19);
                panel.Line($"This permanently consumes your {archetype.displayName} copy ({EvolutionLabel(duplicate.EvolutionIndex)} Lv {duplicate.Level}) and one {ItemName(Roster.CatalystFor(main.EvolutionIndex))}.", 20, Ui.AccentColor);
                panel.Action("ConfirmEvolveButton", "Evolve", () => ConfirmEvolution(key, mainId, duplicateId), true, Ui.AttackColor);
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
