using System.Collections.Generic;
using System.Linq;
using ShinobiDominion.Core;
using UnityEngine;

namespace ShinobiDominion.Game
{
    /// <summary>
    /// The Trial Tower (ADR-045): floors of guardian heroes, each a duel the server fights. The first clear of a
    /// floor pays gems and Mana and opens the next one.
    /// </summary>
    public sealed partial class VillageController
    {
        private List<TrialFloor> _trialFloors;

        public void OpenTrials()
        {
            if (!ModesOnline || _raid != null) return;
            LastMessage = null;
            _trialFloors = null;
            ShowTrials();
            Fetch(async () =>
            {
                _trialFloors = await Modes.TrialListAsync();
                if (UiState.Screen == VillageScreen.AttackFlow && _raid == null) ShowTrials();
            }, ShowBattleHub);
        }

        private void ShowTrials()
        {
            var floors = _trialFloors;
            var cleared = floors?.Count(f => f.cleared) ?? 0;
            _panel.ShowStage("Trial Tower", floors == null ? "" : $"{cleared} / {floors.Count} floors", "icon_star", "scene_castle", false, 236f, ShowBattleHub, panel =>
            {
                ScreenKit.Notice(panel, LastMessage);
                panel.Line("Climb one guardian at a time. Your hero must topple each guardian's shrine. A floor pays once, the first time it is cleared.", 18, HudArt.Ivory);
                if (floors == null)
                {
                    panel.Line("Climbing the stairs...");
                    return;
                }
                // The next floor to beat first, then the two after it, then everything already cleared, newest first.
                var next = floors.FirstOrDefault(f => f.open && !f.cleared);
                var shown = floors.Where(f => !f.cleared && (next == null || f.floor <= next.floor + 2)).Concat(floors.Where(f => f.cleared).OrderByDescending(f => f.floor).Take(5));
                foreach (var floor in shown)
                {
                    var number = floor.floor;
                    var name = Roster.Catalog.TryGet(floor.archetypeId, out var archetype) ? archetype.displayName : "Guardian";
                    var evolution = floor.evolution > 0 ? $", Evo {floor.evolution}" : "";
                    var usable = floor.open || floor.cleared;
                    var row = ScreenKit.AddRow(panel, "TrialRow" + number, number.ToString(), usable ? name : "Locked",
                        usable ? $"Guardian level {floor.level}{evolution}" : $"Clear floor {number - 1} first", floor.open && !floor.cleared);
                    if (floor.open) row.Button("TrialFloor" + number, floor.cleared ? "Again" : "Climb", () => FightTrial(number), true, floor.cleared ? (Color?)null : Ui.AttackColor, 150f);
                    else row.Locked("TrialFloor" + number, 150f);
                    if (floor.cleared) row.Mark("icon_check");
                    else if (floor.open)
                    {
                        row.Reward("icon_chakra", floor.mana.ToString("N0"));
                        row.Reward("icon_crystals", floor.crystals.ToString("N0"));
                    }
                    else row.Dim();
                }
            });
        }

        public void FightTrial(int floor)
        {
            if (!ModesOnline || _raid != null) return;
            var key = KeyFor($"trial-fight|{floor}|{System.DateTime.UtcNow.Ticks / System.TimeSpan.TicksPerSecond}");
            Run(async () =>
            {
                var fight = await Modes.TrialFightAsync(key, floor);
                ClearKey();
                LastMessage = null;
                PlayModeFight(fight);
            }, ShowTrials);
        }
    }
}
