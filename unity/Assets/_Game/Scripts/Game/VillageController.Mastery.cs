using ShinobiDominion.Core;
using UnityEngine;
using UnityEngine.UI;
using Text = ShinobiDominion.Game.GameText;

namespace ShinobiDominion.Game
{
    /// <summary>
    /// Hero mastery (ADR-053), the hero window's Mastery banner: the technique rank, bought with Technique Manuals, and the hero's
    /// one talent, rolled by the server for a Talent Token. Online only: both change what the hero does in battle,
    /// and the server decides that. The device never rolls anything.
    /// </summary>
    public sealed partial class VillageController
    {
        private static readonly Color MasteryGold = new Color(1f, 0.87f, 0.4f);

        /// <summary>What the last roll or rank-up gave, shown once at the top of the mastery window.</summary>
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

        /// <summary>True for a message that says why a tap was refused; the idle status word is not one.</summary>
        private static bool IsRefusal(string message) => !string.IsNullOrEmpty(message) && message != "Ready";

        /// <summary>One line of words across a window, a given way down from its top.</summary>
        private static Text WindowLine(RectTransform content, string name, string words, float y, int size, Color color, float height = 26f)
        {
            var label = Ui.Label(content, words, size, TextAnchor.MiddleLeft, color);
            label.name = name;
            label.rectTransform.anchorMin = new Vector2(0f, 1f);
            label.rectTransform.anchorMax = new Vector2(1f, 1f);
            label.rectTransform.pivot = new Vector2(0.5f, 1f);
            label.rectTransform.sizeDelta = new Vector2(-12f, height);
            label.rectTransform.anchoredPosition = new Vector2(0f, y);
            label.verticalOverflow = VerticalWrapMode.Truncate;
            label.resizeTextForBestFit = true;
            label.resizeTextMinSize = 12;
            label.resizeTextMaxSize = size;
            return label;
        }

        public void ShowMastery(string instanceId)
        {
            if (Roster.Find(instanceId) == null) return;
            _heroTab = HeroTab.Mastery;
            OpenHeroWindow(instanceId);
        }

        private void DrawMasteryTab(RectTransform content, HeroInstance hero, HeroArchetype archetype, string note)
        {
            var instanceId = hero.InstanceId;
            var technique = Roster.Catalog.Technique;
            var talents = Roster.Catalog.Talents;
            var cumulative = HeroMath.Cumulative(hero.EvolutionIndex, hero.Level);
            var manuals = Roster.ItemCount(technique.item);
            var tokens = Roster.ItemCount(talents.item);
            {
                // What the last tap did, or why it was refused.
                var y = 0f;
                if (!string.IsNullOrEmpty(note))
                {
                    var rolled = WindowLine(content, "MasteryNote", note, y, 20, MasteryGold);
                    UiMotion.Shine(rolled, Color.white, 2f, 0.3f);
                    y -= 28f;
                }
                if (IsRefusal(LastMessage))
                {
                    WindowLine(content, "MasteryRefusal", LastMessage, y, 18, Ui.AccentColor);
                    y -= 28f;
                }
                if (!ModesOnline)
                {
                    WindowLine(content, "MasteryOffline", "Mastery needs the server.", y, 18, Ui.AccentColor);
                    y -= 28f;
                }

                // Technique rank.
                var power = technique.Power(BattleSim.PowerFor(archetype.rarity.ToString()), hero.TechniqueRank);
                var top = hero.TechniqueRank >= technique.maxRank;
                var next = hero.TechniqueRank + 1;
                var cost = top ? 0 : technique.Manuals(next);
                var needs = top ? 0 : technique.HeroLevel(next);
                var canRank = !top && ModesOnline && manuals >= cost && cumulative >= needs;
                var rank = ScreenKit.AddRowAt(content, y, "TechniqueRow", "icon_skill", $"Technique rank {hero.TechniqueRank} of {technique.maxRank}",
                    $"Special power {power}%, +{technique.powerPerRank}% a rank" + (!top && cumulative < needs ? $"     Needs hero level {needs}" : ""), canRank);
                rank.Title.name = "RankLine";
                rank.Detail.name = "RankDetail";
                if (top) rank.Note("Highest rank");
                else
                {
                    rank.Button("RankUpButton", "Rank up", () => RankUpTechnique(instanceId), canRank, Ui.AccentColor, 124f);
                    rank.Reward("icon_scroll", cost.ToString("N0"));
                }
                y -= rank.Root.sizeDelta.y + 10f;

                // Talent.
                var talent = talents.Find(hero.TalentId);
                var canRoll = ModesOnline && tokens >= 1;
                var gift = ScreenKit.AddRowAt(content, y, "TalentRow", "icon_star", talent == null ? "No talent yet" : $"{talent.displayName}, level {hero.TalentLevel} of {talents.maxLevel}",
                    talent == null ? "A roll gives this hero one" : $"{TalentEffect(talent, hero.TalentLevel)}. {talent.description}", canRoll);
                gift.Title.name = "TalentLine";
                gift.Detail.name = "TalentDetail";
                gift.Button("TalentRollButton", "Roll", () =>
                {
                    if (hero.TalentLevel > 0) ConfirmTalentRoll(instanceId);
                    else RollTalent(instanceId);
                }, canRoll, Ui.AccentColor, 124f);
                gift.Reward("icon_reward", "1");
                y -= gift.Root.sizeDelta.y + 12f;

                WindowLine(content, "MasteryOwned", $"You have {manuals:N0} Technique Manuals and {tokens:N0} Talent Tokens.", y, 18, HudArt.Ivory);
                y -= 30f;
                var odds = WindowLine(content, "MasteryOdds", $"A roll is an even draw of one of the {talents.list.Count} talents at a level from 1 to {talents.maxLevel}. It replaces the talent the hero has, and can come up the same or lower.", y, 17, HudArt.IvoryMuted, 46f);
                odds.alignment = TextAnchor.UpperLeft;
                y -= 50f;
                var sources = WindowLine(content, "MasterySources", "Manuals come from dungeon wins and from stars on rival raids. A Talent Token comes from a 3-star raid on a rival. Rank and talent stay with this hero.", y, 17, HudArt.IvoryMuted, 46f);
                sources.alignment = TextAnchor.UpperLeft;
            }
        }

        /// <summary>A roll replaces what the hero has, so a hero that already has a talent is asked first.</summary>
        private void ConfirmTalentRoll(string instanceId)
        {
            var hero = Roster.Find(instanceId);
            if (hero == null) return;
            var talent = Roster.Catalog.Talents.Find(hero.TalentId);
            _panel.ShowWindow("Replace Talent", new Vector2(700f, 330f), () => ShowMastery(instanceId), content =>
            {
                var words = HudArt.Label(content, $"{talent?.displayName ?? "The talent"}, level {hero.TalentLevel}, will be replaced by whatever the roll gives. It can be weaker.", HudArt.Numbers, 22, TextAnchor.MiddleCenter, HudArt.Ivory, HudArt.LacquerDeep);
                words.name = "ReplaceWords";
                words.rectTransform.anchorMin = new Vector2(0f, 0.4f);
                words.rectTransform.anchorMax = Vector2.one;
                words.rectTransform.offsetMin = new Vector2(10f, 0f);
                words.rectTransform.offsetMax = new Vector2(-10f, 0f);
                var roll = Ui.Button(content, "TalentRollConfirm", "Roll", () => RollTalent(instanceId), ModesOnline, Ui.AttackColor, 22);
                Ui.Anchor((RectTransform)roll.transform, new Vector2(0.5f, 0f), new Vector2(250f, 60f), new Vector2(-134f, 0f));
                var keep = Ui.Button(content, "TalentRollKeep", "Keep it", () => ShowMastery(instanceId), true, null, 22);
                Ui.Anchor((RectTransform)keep.transform, new Vector2(0.5f, 0f), new Vector2(250f, 60f), new Vector2(134f, 0f));
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
