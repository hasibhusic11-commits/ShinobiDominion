using System.Linq;
using ShinobiDominion.Core;
using UnityEngine;

namespace ShinobiDominion.Game
{
    /// <summary>
    /// Hero gear (ADR-044), from the hero page: Weapon, Armor and Charm, each upgraded with gold and Mana.
    /// Online only, because gear changes battle stats and the server decides those.
    /// </summary>
    public sealed partial class VillageController
    {
        private static readonly string[] GearStat = { "attack", "HP", "defense" };

        public void ShowGear(string instanceId)
        {
            var hero = Roster.Find(instanceId);
            if (hero == null) return;
            var gear = Roster.Catalog.Gear;
            var cumulative = HeroMath.Cumulative(hero.EvolutionIndex, hero.Level);
            _panel.Show($"{Roster.ArchetypeOf(hero).displayName}: gear", () => ShowHeroDetail(instanceId), panel =>
            {
                if (!string.IsNullOrEmpty(LastMessage)) panel.Line(LastMessage, 20, Ui.AccentColor);
                panel.Line($"Each level adds {gear.percentPerLevel}% to its stat. Higher gear needs a stronger hero. Gear stays with this hero.", 19);
                if (!ModesOnline) panel.Line("Upgrading gear needs the server.", 19, Ui.AccentColor);
                for (var slot = 0; slot < 3; slot++)
                {
                    var level = hero.Gear[slot];
                    var bonus = gear.percentPerLevel * level;
                    var chosen = slot;
                    var name = $"Gear_{slot}";
                    var label = $"{gear.slots[slot]} level {level} (+{bonus}% {GearStat[slot]})";
                    if (level >= gear.maxLevel)
                    {
                        panel.Action(name, label + "   At its best", null, false, null, 19);
                        continue;
                    }
                    var next = level + 1;
                    var needs = gear.HeroLevel(next);
                    var affordable = Village.Gold >= gear.Gold(next) && Village.Mana >= gear.Mana(next);
                    var why = cumulative < needs ? $"   needs hero level {needs}" : "";
                    panel.Action(name, $"{label}   Upgrade: {gear.Gold(next):N0} gold, {gear.Mana(next):N0} Mana{why}",
                        () => UpgradeGear(instanceId, chosen), ModesOnline && affordable && cumulative >= needs, Ui.AccentColor, 18);
                }
            });
        }

        public void UpgradeGear(string instanceId, int slot)
        {
            if (!ModesOnline) return;
            var level = Roster.Find(instanceId)?.Gear[slot] ?? 0;
            VillageRequest($"gear|{instanceId}|{slot}|{level}", key => Modes.GearUpgradeAsync(key, instanceId, slot), () =>
            {
                GameSounds.Play(GameSound.Star);
                ShowGear(instanceId);
            }, () => ShowGear(instanceId));
        }
    }
}
