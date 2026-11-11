using ShinobiDominion.Core;
using UnityEngine;

namespace ShinobiDominion.Game
{
    /// <summary>
    /// Hero mastery (ADR-053), from the hero page: the technique rank, bought with Technique Manuals, and the hero's
    /// one talent, rolled by the server for a Talent Token. Online only: both change what the hero does in battle,
    /// and the server decides that. The device never rolls anything.
    /// </summary>
    public sealed partial class VillageController
    {
        private static readonly Color MasteryGold = new Color(1f, 0.87f, 0.4f);

        /// <summary>What the last roll or rank-up gave, shown once at the top of the mastery panel.</summary>
        private string _masteryNote;

        /// <summary>What a talent does at a level, as one short line.</summary>
        public static string TalentEffect(TalentDefinition talent, int level)
        {
            if (talent == null || level <= 0) return "";
            var amount = talent.perLevel * level;
            switch (talent.kind)
            {
                case TalentSettings.Hp: return $"+{amount}% HP";
                case TalentSettings.Attack: return $"+{amount}% attack";
                case TalentSettings.Defense: return $"+{amount}% defense";
                case TalentSettings.Charge: return $"lands with {Mathf.Min(amount, BattleSim.ChargeFull)} of {BattleSim.ChargeFull} charge";
                case TalentSettings.Siege: return $"+{amount}% damage to buildings";
                case TalentSettings.Speed: return $"+{amount}% walking speed";
                default: return "";
            }
        }

        /// <summary>The hero page's button label: the rank, and the talent when there is one.</summary>
        private string MasteryLabel(HeroInstance hero)
        {
            var talent = Roster.Catalog.Talents.Find(hero.TalentId);
            return talent == null ? $"Mastery (rank {hero.TechniqueRank})" : $"Mastery (rank {hero.TechniqueRank}, {talent.displayName} {hero.TalentLevel})";
        }

        public void ShowMastery(string instanceId)
        {
            var hero = Roster.Find(instanceId);
            if (hero == null) return;
            var archetype = Roster.ArchetypeOf(hero);
            var technique = Roster.Catalog.Technique;
            var talents = Roster.Catalog.Talents;
            var cumulative = HeroMath.Cumulative(hero.EvolutionIndex, hero.Level);
            var manuals = Roster.ItemCount(technique.item);
            var tokens = Roster.ItemCount(talents.item);
            var note = _masteryNote;
            _masteryNote = null;
            _panel.Show($"{archetype.displayName}: mastery", () => ShowHeroDetail(instanceId), panel =>
            {
                if (!string.IsNullOrEmpty(LastMessage)) panel.Line(LastMessage, 20, Ui.AccentColor);
                if (!string.IsNullOrEmpty(note))
                {
                    var rolled = panel.Line(note, 24, MasteryGold);
                    rolled.name = "MasteryNote";
                    UiMotion.Shine(rolled, Color.white, 2f, 0.3f);
                }
                if (!ModesOnline) panel.Line("Mastery needs the server.", 19, Ui.AccentColor);

                // Technique rank.
                var power = technique.Power(BattleSim.PowerFor(archetype.rarity.ToString()), hero.TechniqueRank);
                panel.Line($"Technique rank {hero.TechniqueRank} of {technique.maxRank}. Special power {power}%, +{technique.powerPerRank}% a rank.", 21, HudArt.BrassLight).name = "RankLine";
                if (hero.TechniqueRank >= technique.maxRank)
                {
                    panel.Action("RankUpButton", "Technique at its highest rank", null, false, null, 19);
                }
                else
                {
                    var next = hero.TechniqueRank + 1;
                    var cost = technique.Manuals(next);
                    var needs = technique.HeroLevel(next);
                    var why = cumulative < needs ? $"   needs hero level {needs}" : "";
                    panel.Action("RankUpButton", $"Rank up: {cost:N0} manuals (have {manuals:N0}){why}",
                        () => RankUpTechnique(instanceId), ModesOnline && manuals >= cost && cumulative >= needs, Ui.AccentColor, 18);
                }

                // Talent.
                var talent = talents.Find(hero.TalentId);
                var line = talent == null
                    ? "No talent yet."
                    : $"Talent: {talent.displayName}, level {hero.TalentLevel} of {talents.maxLevel} ({TalentEffect(talent, hero.TalentLevel)}). {talent.description}";
                panel.Line(line, 21, HudArt.BrassLight).name = "TalentLine";
                panel.Action("TalentRollButton", $"Roll a talent: 1 token (have {tokens:N0})",
                    () =>
                    {
                        if (hero.TalentLevel > 0) ConfirmTalentRoll(instanceId);
                        else RollTalent(instanceId);
                    }, ModesOnline && tokens >= 1, Ui.AccentColor, 18);
                panel.Line($"A roll is an even draw of one of the {talents.list.Count} talents at a level from 1 to {talents.maxLevel}. It replaces the talent the hero has, and can come up the same or lower.", 17);
                panel.Line("Manuals come from dungeon wins and from stars on rival raids. A Talent Token comes from a 3-star raid on a rival. Rank and talent stay with this hero.", 17);
            });
        }

        /// <summary>A roll replaces what the hero has, so a hero that already has a talent is asked first.</summary>
        private void ConfirmTalentRoll(string instanceId)
        {
            var hero = Roster.Find(instanceId);
            if (hero == null) return;
            var talent = Roster.Catalog.Talents.Find(hero.TalentId);
            _panel.Show("Replace this talent?", () => ShowMastery(instanceId), panel =>
            {
                panel.Line($"{talent?.displayName ?? "The talent"}, level {hero.TalentLevel}, will be replaced by whatever the roll gives. It can be weaker.", 21);
                panel.Action("TalentRollConfirm", "Roll", () => RollTalent(instanceId), ModesOnline, Ui.AccentColor, 20);
                panel.Action("TalentRollKeep", "Keep it", () => ShowMastery(instanceId), true, null, 20);
            });
        }

        public void RankUpTechnique(string instanceId)
        {
            if (!ModesOnline) return;
            var rank = Roster.Find(instanceId)?.TechniqueRank ?? 1;
            VillageRequest($"rank|{instanceId}|{rank}", key => Modes.TechniqueRankUpAsync(key, instanceId), () =>
            {
                GameSounds.Play(GameSound.Star);
                var hero = Roster.Find(instanceId);
                if (hero != null) _masteryNote = $"Technique rank {hero.TechniqueRank}.";
                ShowMastery(instanceId);
            }, () => ShowMastery(instanceId));
        }

        public void RollTalent(string instanceId)
        {
            if (!ModesOnline) return;
            // The count of tokens changes with every roll, so each roll gets its own key and a retry reuses it.
            var tokens = Roster.ItemCount(Roster.Catalog.Talents.item);
            VillageRequest($"talent|{instanceId}|{tokens}", key => Modes.TalentRollAsync(key, instanceId), () =>
            {
                GameSounds.Play(GameSound.Ability);
                var hero = Roster.Find(instanceId);
                var talent = hero == null ? null : Roster.Catalog.Talents.Find(hero.TalentId);
                if (talent != null) _masteryNote = $"Rolled {talent.displayName}, level {hero.TalentLevel}: {TalentEffect(talent, hero.TalentLevel)}.";
                ShowMastery(instanceId);
            }, () => ShowMastery(instanceId));
        }
    }
}
