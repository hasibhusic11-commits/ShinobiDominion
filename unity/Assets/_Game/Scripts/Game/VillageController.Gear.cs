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
        private static readonly string[] GearIcon = { "icon_attack_stat", "icon_health", "icon_defense_stat" };

        public void ShowGear(string instanceId)
        {
            var hero = Roster.Find(instanceId);
            if (hero == null) return;
            var gear = Roster.Catalog.Gear;
            var cumulative = HeroMath.Cumulative(hero.EvolutionIndex, hero.Level);
            _panel.ShowWindow("Gear", new Vector2(880f, 500f), () => ShowHeroDetail(instanceId), content =>
            {
                var y = 2f;
                var who = HudArt.Label(content, Roster.ArchetypeOf(hero).displayName, HudArt.Display, 24, TextAnchor.MiddleLeft, HudArt.BrassLight, HudArt.LacquerDeep);
                who.name = "GearHero";
                who.rectTransform.anchorMin = new Vector2(0f, 1f);
                who.rectTransform.anchorMax = new Vector2(1f, 1f);
                who.rectTransform.pivot = new Vector2(0.5f, 1f);
                who.rectTransform.sizeDelta = new Vector2(-12f, 32f);
                who.rectTransform.anchoredPosition = new Vector2(0f, y);
                y -= 34f;
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
                        row.Button("Gear_" + slot, "Upgrade", () => UpgradeGear(instanceId, chosen), can, Ui.AccentColor, 150f);
                        row.Reward("icon_chakra", gear.Mana(next).ToString("N0"));
                        row.Reward("icon_gold", gear.Gold(next).ToString("N0"));
                    }
                    y -= row.Root.sizeDelta.y + 10f;
                }
                WindowLine(content, "GearRule", $"Each level adds {gear.percentPerLevel}% to its stat. Higher gear needs a stronger hero. Gear stays with this hero.", y - 2f, 17, HudArt.IvoryMuted);
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
