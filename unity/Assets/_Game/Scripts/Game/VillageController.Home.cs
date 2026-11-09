using System;
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
                case "crystal_chest": return "Gem Shop";
                default: return shortcut;
            }
        }

        /// <summary>Mail, rankings, quests and the gift open their screens (ADR-039). Anything else says plainly that it is coming later.</summary>
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
                case "chest": OpenChest(); return;
                case "crystal_chest": OpenShop("crystals"); return;
            }
            _panel.ShowWindow(ShortcutTitle(shortcut), new Vector2(640f, 300f), CloseAll, content =>
            {
                var text = HudArt.Label(content, $"{ShortcutTitle(shortcut)} is coming in a later update.", HudArt.Numbers, 24, TextAnchor.MiddleCenter, HudArt.Ivory, HudArt.LacquerDeep);
                Ui.Stretch(text.rectTransform);
            });
        }

        /// <summary>The "+" buttons and the gem chest open the shop: fixed swaps of gems for gold or Mana (ADR-043). No real money.</summary>
        public void OpenShop(string currency)
        {
            CloseAll();
            if (RefuseShortcutOffline("Shop")) return;
            Fetch(async () =>
            {
                _chest = await Modes.ChestViewAsync();
                ShowShop();
            }, ShowShop);
        }

        private ChestView _chest;

        public ChestView CurrentChest => _chest;

        private void ShowShop()
        {
            var view = _chest;
            _panel.ShowScreen("Shop", $"Gems {ServerCrystals ?? 0:N0}", CloseAll, panel =>
            {
                if (!string.IsNullOrEmpty(LastMessage)) panel.Line(LastMessage, 20, Ui.AccentColor);
                panel.Line("Swap gems for gold or Mana. Gems come from quests, dungeons, Village Siege and the daily gift. Gems can't be bought with money.", 19);
                if (view == null) return;
                foreach (var offer in view.offers)
                {
                    var id = offer.id;
                    var what = offer.gold > 0 ? $"{offer.gold:N0} gold" : $"{offer.mana:N0} Mana";
                    var enough = (ServerCrystals ?? 0) >= offer.crystals;
                    panel.Action("Shop_" + id, $"{offer.name}: {what} for {offer.crystals:N0} gems", () => BuyOffer(id), enough, offer.gold > 0 ? new Color(0.95f, 0.75f, 0.25f) : new Color(0.6f, 0.4f, 0.95f), 20);
                }
            });
        }

        private void BuyOffer(string offerId)
        {
            if (!ModesOnline) return;
            var key = KeyFor($"shop|{offerId}|{ServerCrystals}");
            Run(async () =>
            {
                var view = await Modes.ShopBuyAsync(key, offerId);
                ClearKey();
                LastMessage = null;
                _chest = view;
                if (view.State != null) ApplyServerState(view.State);
                GameSounds.Play(GameSound.Coin);
                ShowShop();
            }, ShowShop);
        }

        /// <summary>The free Treasure Chest: gold and Mana every few hours, more for a higher Dojo.</summary>
        public void OpenChest()
        {
            if (RefuseShortcutOffline("Treasure Chest")) return;
            Fetch(async () =>
            {
                _chest = await Modes.ChestViewAsync();
                ShowChest();
            }, ShowChest);
        }

        private void ShowChest()
        {
            var view = _chest;
            _panel.ShowWindow("Treasure Chest", new Vector2(640f, 360f), CloseAll, content =>
            {
                var text = view == null ? (LastMessage ?? "The chest cannot be reached right now.")
                    : view.Ready ? $"Your chest is full:\n{view.gold:N0} gold and {view.mana:N0} Mana."
                    : $"The chest refills in {FormatDuration((view.readyAtMs - view.nowMs) * TimeSpan.TicksPerMillisecond)}.\nNext: {view.gold:N0} gold and {view.mana:N0} Mana.";
                var label = HudArt.Label(content, text, HudArt.Numbers, 24, TextAnchor.MiddleCenter, HudArt.Ivory, HudArt.LacquerDeep);
                Ui.Anchor(label.rectTransform, new Vector2(0.5f, 1f), new Vector2(520f, 150f), new Vector2(0f, -10f));
                if (view == null || !view.Ready) return;
                var open = Ui.Button(content, "OpenChestButton", "Open", OpenChestNow, true, Ui.AccentColor, 26);
                Ui.Anchor((RectTransform)open.transform, new Vector2(0.5f, 0f), new Vector2(220f, 64f), new Vector2(0f, 6f));
                UiMotion.Pulse(open.transform, 0.06f, 1.1f);
            });
        }

        public void OpenChestNow()
        {
            if (!ModesOnline) return;
            var key = KeyFor($"chest|{_chest?.readyAtMs}");
            Run(async () =>
            {
                var view = await Modes.ChestOpenAsync(key);
                ClearKey();
                LastMessage = null;
                _chest = view;
                // The gold pops up over the Dojo and flies to the counter, like collecting.
                var dojo = Village.Buildings.FirstOrDefault(b => b.Definition.id == Village.HeadquartersId);
                if (dojo != null) _lastCollectAt = GridSpace.FootprintCenter(dojo.Footprint) + Vector3.up * 2.6f;
                if (view.State != null) ApplyServerState(view.State);
                GameSounds.Play(GameSound.Victory);
                ShowChest();
            }, ShowChest);
        }
    }
}
