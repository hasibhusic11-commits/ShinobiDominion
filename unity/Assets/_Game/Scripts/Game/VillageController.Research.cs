using System;
using System.Linq;
using ShinobiDominion.Core;
using UnityEngine;

namespace ShinobiDominion.Game
{
    /// <summary>
    /// The Research Hut (ADR-041): studies that make each kind of troop stronger, paid in Mana, one at a
    /// time. Online, the server decides; offline, in development only, the same rules run here.
    /// </summary>
    public sealed partial class VillageController
    {
        public TroopResearch Research { get; } = new TroopResearch();

        private void ResearchActions(PanelView panel)
        {
            var hut = TroopResearch.HutLevel(Village);
            var running = Research.RunningTroop;
            if (running != null)
            {
                var entry = Research.Entries[running];
                var left = entry.DoneUtcTicks - Now;
                var gems = Village.FinishCost(left);
                panel.Line($"Studying {TroopData.Find(running)?.displayName}, level {entry.Level + 1}: {FormatDuration(left)} left", 21, Ui.AccentColor);
                panel.Action("FinishResearchButton", $"Finish now ({gems:N0} gems)", () => FinishResearch(running), Village.Crystals >= gems, Ui.AccentColor);
            }
            else panel.Line($"Each study makes one kind of troop {TroopData.Research.percentPerLevel}% stronger in HP and attack. Hut level {hut} allows studies up to level {Math.Min(hut, TroopData.Research.maxLevel)}.", 19);

            foreach (var troop in TroopData.All)
            {
                var level = Research.LevelOf(troop.id);
                var boost = TroopData.Research.percentPerLevel * level;
                var name = $"Research_{troop.id}";
                if (level >= TroopData.Research.maxLevel)
                {
                    panel.Action(name, $"{troop.displayName}  level {level} (+{boost}%)   Fully studied", null, false, null, 18);
                    continue;
                }
                var next = level + 1;
                var cost = TroopResearch.Cost(TroopData, troop, next);
                var time = FormatDuration(TroopResearch.Seconds(TroopData, next) * TimeSpan.TicksPerSecond);
                var error = Research.Check(Village, TroopData, troop.id);
                var why = error == VillageError.ResearchHutTooLow ? $"   needs hut level {next}" : "";
                panel.Action(name, $"{troop.displayName}  level {level} (+{boost}%)   Study level {next}: {cost:N0} Mana, {time}{why}",
                    () => StartResearch(troop.id), error == VillageError.None, null, 18);
            }
        }

        public void StartResearch(string troopId)
        {
            if (RefuseWhenNoServer()) return;
            if (IsOnline)
            {
                if (Modes == null) return;
                VillageRequest($"research|{troopId}|{Research.LevelOf(troopId)}", key => Modes.ResearchStartAsync(key, troopId), ShowContextPanel, ShowContextPanel);
                return;
            }
            RunAndRefresh(Research.TryStart(Village, TroopData, troopId, Now));
        }

        public void FinishResearch(string troopId)
        {
            if (RefuseWhenNoServer()) return;
            if (IsOnline)
            {
                if (Modes == null) return;
                VillageRequest($"research-finish|{troopId}|{Research.LevelOf(troopId)}", key => Modes.ResearchFinishAsync(key, troopId), ShowContextPanel, ShowContextPanel);
                return;
            }
            RunAndRefresh(Research.TryFinish(Village, troopId, Now));
        }
    }
}
