using System.Linq;
using ShinobiDominion.Core;
using UnityEngine;

namespace ShinobiDominion.Game
{
    /// <summary>
    /// The home screen pieces added from the owner's mockup on 2026-11-07 (ADR-032): the portrait, the
    /// shortcut row and the "+" shop buttons.
    /// </summary>
    public sealed partial class VillageController
    {
        /// <summary>
        /// Mana, the third currency (owner, 2026-11-08: Gold, Gems, Mana; ADR-038). Online this is the
        /// server's balance, carried in the village snapshot; it is never changed here.
        /// </summary>
        public long Mana => Village.Mana;

        private Sprite GuardPortrait()
        {
            var id = Village.Buildings.FirstOrDefault(b => b.AssignedHeroInstanceId != null)?.AssignedHeroInstanceId;
            var hero = id == null ? null : Roster?.Find(id);
            if (hero == null || hero.EvolutionIndex != 0 || !HeroConcepts.Has(hero.ArchetypeId)) return null;
            return HeroConcepts.Card(hero.ArchetypeId);
        }

        private static string ShortcutTitle(string shortcut)
        {
            switch (shortcut)
            {
                case "mail": return "Mail";
                case "trophy": return "Rankings";
                case "friends": return "Guild";
                case "quests": return "Quests";
                case "gift": return "Daily Gift";
                case "chest": return "Treasure Chest";
                case "crystal_chest": return "Gem Chest";
                default: return shortcut;
            }
        }

        /// <summary>Mail, rankings, quests and the gift open their screens (ADR-039). The chests do not exist yet, and say so plainly.</summary>
        public void OpenShortcut(string shortcut)
        {
            CloseAll();
            switch (shortcut)
            {
                case "mail": OpenMail(); return;
                case "trophy": OpenRankings(); return;
                case "quests": OpenQuests(); return;
                case "gift": OpenGift(); return;
                case "friends": OpenGuild(); return;
            }
            _panel.ShowWindow(ShortcutTitle(shortcut), new Vector2(640f, 300f), CloseAll, content =>
            {
                var text = HudArt.Label(content, $"{ShortcutTitle(shortcut)} is coming in a later update.", HudArt.Numbers, 24, TextAnchor.MiddleCenter, HudArt.Ivory, HudArt.LacquerDeep);
                Ui.Stretch(text.rectTransform);
            });
        }

        /// <summary>The "+" buttons. There is no shop yet: no real-money purchases and no exchange on the server.</summary>
        public void OpenShop(string currency)
        {
            CloseAll();
            _panel.ShowWindow("Shop", new Vector2(640f, 300f), CloseAll, content =>
            {
                var text = HudArt.Label(content, "The shop is coming in a later update.", HudArt.Numbers, 24, TextAnchor.MiddleCenter, HudArt.Ivory, HudArt.LacquerDeep);
                Ui.Stretch(text.rectTransform);
            });
        }
    }
}
