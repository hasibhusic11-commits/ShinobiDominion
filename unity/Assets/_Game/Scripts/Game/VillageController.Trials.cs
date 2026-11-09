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
            _panel.ShowScreen("Trial Tower", floors == null ? "" : $"{cleared} of {floors.Count} floors cleared", ShowBattleHub, panel =>
            {
                if (!string.IsNullOrEmpty(LastMessage)) panel.Line(LastMessage, 20, Ui.AccentColor);
                panel.Line("Climb the tower one guardian at a time. Your hero must topple each guardian's shrine. Each floor pays once, the first time it is cleared.", 19);
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
                    var evolution = floor.evolution > 0 ? $" Evo {floor.evolution}" : "";
                    var caption = floor.cleared
                        ? $"Floor {number}: {name}, level {floor.level}{evolution}   Cleared"
                        : floor.open
                            ? $"Floor {number}: {name}, level {floor.level}{evolution}   First clear: {floor.crystals:N0} gems, {floor.mana:N0} Mana"
                            : $"Floor {number}: locked until floor {number - 1} is cleared";
                    panel.Action("TrialFloor" + number, caption, () => FightTrial(number), floor.open, floor.open && !floor.cleared ? Ui.AttackColor : (Color?)null, 18);
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
