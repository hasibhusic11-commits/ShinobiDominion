using System;
using System.Linq;
using ShinobiDominion.Core;
using UnityEngine;
using UnityEngine.UI;

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
            _panel.ShowStage("Shop", (ServerCrystals ?? 0).ToString("N0"), "icon_crystals", "scene_castle", false, 236f, CloseAll, panel =>
            {
                ScreenKit.Notice(panel, LastMessage);
                if (view == null) return;
                foreach (var offer in view.offers)
                {
                    var id = offer.id;
                    var gold = offer.gold > 0;
                    var enough = (ServerCrystals ?? 0) >= offer.crystals;
                    var row = ScreenKit.AddRow(panel, "ShopRow_" + id, gold ? "icon_gold" : "icon_chakra", gold ? $"{offer.gold:N0} Gold" : $"{offer.mana:N0} Mana", offer.name, enough, 78f);
                    row.Button("Shop_" + id, "Buy", () => BuyOffer(id), enough, Ui.AccentColor, 150f);
                    row.Reward("icon_crystals", offer.crystals.ToString("N0"));
                    if (!enough) row.Dim();
                }
            }, "Gems come from quests, dungeons, Village Siege and the daily gift. They can't be bought with money.");
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
            _panel.ShowWindow("Treasure Chest", new Vector2(760f, 560f), CloseAll, content =>
            {
                var ready = view != null && view.Ready;
                // The vault stands for the chest, lit from behind when there is something to take.
                var light = HudArt.Picture(content, "ChestLight", "glow", new Vector2(0.5f, 1f), new Vector2(520f, 320f), new Vector2(0f, 60f));
                light.color = ready ? new Color(1f, 0.78f, 0.36f, 0.6f) : new Color(0.5f, 0.55f, 0.7f, 0.25f);
                var art = Ui.Rect("ChestArt", content);
                Ui.Anchor(art, new Vector2(0.5f, 1f), new Vector2(250f, 216f), new Vector2(0f, 12f));
                var picture = art.gameObject.AddComponent<Image>();
                picture.sprite = Resources.Load<Sprite>("Buildings/gold_vault_10");
                picture.preserveAspect = true;
                picture.raycastTarget = false;
                if (ready) UiMotion.Pulse(art, 0.03f, 1.6f);
                else picture.color = new Color(0.55f, 0.55f, 0.6f);

                var strip = ScreenKit.Parchment(content, new Vector2(0.5f, 1f), new Vector2(660f, 126f), new Vector2(0f, -214f));
                var words = view == null ? (LastMessage ?? "The chest cannot be reached right now.")
                    : ready ? "Your chest is full"
                    : $"The chest refills in {FormatDuration((view.readyAtMs - view.nowMs) * TimeSpan.TicksPerMillisecond)}";
                var line = HudArt.Label(strip, words, HudArt.Display, 22, TextAnchor.MiddleCenter, ScreenKit.InkBrown, ScreenKit.ParchmentEdge);
                line.name = "ChestWords";
                line.fontStyle = FontStyle.BoldAndItalic;
                line.horizontalOverflow = HorizontalWrapMode.Wrap;
                if (view == null)
                {
                    Ui.Stretch(line.rectTransform, 30f, 12f, 30f, 12f);
                    return;
                }
                line.rectTransform.anchorMin = new Vector2(0f, 0.56f);
                line.rectTransform.anchorMax = Vector2.one;
                line.rectTransform.offsetMin = new Vector2(24f, 0f);
                line.rectTransform.offsetMax = new Vector2(-24f, -8f);
                GiftAmount(strip, 1f / 6f, "icon_gold", $"{view.gold:N0} Gold");
                GiftAmount(strip, 0.5f, "icon_chakra", $"{view.mana:N0} Mana");
                if (!ready) return;
                var open = Ui.Button(content, "OpenChestButton", "Open", OpenChestNow, true, Ui.AccentColor, 28);
                Ui.Anchor((RectTransform)open.transform, new Vector2(0.5f, 0f), new Vector2(290f, 66f), new Vector2(0f, 2f));
                UiMotion.Pulse(open.transform, 0.05f, 1.1f);
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
