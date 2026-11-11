using System.Linq;
using ShinobiDominion.Core;
using UnityEngine;

namespace ShinobiDominion.Game
{
    /// <summary>
    /// Hero gear (ADR-044), the hero window's Gear banner: Weapon, Armor and Charm, each upgraded with gold and Mana.
    /// Online only, because gear changes battle stats and the server decides those.
    /// </summary>
    public sealed partial class VillageController
    {
        private static readonly string[] GearStat = { "attack", "HP", "defense" };
        private static readonly string[] GearIcon = { "icon_attack_stat", "icon_health", "icon_defense_stat" };

        public void ShowGear(string instanceId)
        {
            if (Roster.Find(instanceId) == null) return;
            _heroTab = HeroTab.Gear;
            OpenHeroWindow(instanceId);
        }

        private void DrawGearTab(RectTransform content, HeroInstance hero)
        {
            var instanceId = hero.InstanceId;
            var gear = Roster.Catalog.Gear;
            var cumulative = HeroMath.Cumulative(hero.EvolutionIndex, hero.Level);
            {
                var y = 0f;
                if (IsRefusal(LastMessage))
                {
                    WindowLine(content, "GearRefusal", LastMessage, y, 18, Ui.AccentColor);
                    y -= 28f;
                }
                if (!ModesOnline)
                {
                    WindowLine(content, "GearOffline", "Upgrading gear needs the server.", y, 18, Ui.AccentColor);
                    y -= 28f;
                }
                for (var slot = 0; slot < 3; slot++)
                {
                    var level = hero.Gear[slot];
                    var bonus = gear.percentPerLevel * level;
                    var chosen = slot;
                    var best = level >= gear.maxLevel;
                    var next = level + 1;
                    var needs = best ? 0 : gear.HeroLevel(next);
                    var affordable = !best && Village.Gold >= gear.Gold(next) && Village.Mana >= gear.Mana(next);
                    var can = !best && ModesOnline && affordable && cumulative >= needs;
                    var row = ScreenKit.AddRowAt(content, y, "GearRow_" + slot, GearIcon[slot], $"{gear.slots[slot]} level {level}",
                        $"+{bonus}% {GearStat[slot]}" + (!best && cumulative < needs ? $"     Needs hero level {needs}" : ""), can);
                    if (best) row.Note("At its best");
                    else
                    {
                        row.Button("Gear_" + slot, "Upgrade", () => UpgradeGear(instanceId, chosen), can, Ui.AccentColor, 124f);
                        row.Reward("icon_chakra", gear.Mana(next).ToString("N0"));
                        row.Reward("icon_gold", gear.Gold(next).ToString("N0"));
                    }
                    y -= row.Root.sizeDelta.y + 10f;
                }
                WindowLine(content, "GearRule", $"Each level adds {gear.percentPerLevel}% to its stat. Higher gear needs a stronger hero. Gear stays with this hero.", y - 2f, 17, HudArt.IvoryMuted, 44f).alignment = TextAnchor.UpperLeft;
            }
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
