using System.Collections.Generic;
using System.Linq;
using ShinobiDominion.Core;
using UnityEngine;
using UnityEngine.UI;

namespace ShinobiDominion.Game
{
    /// <summary>
    /// The attack screen, laid out like the owner's reference (2026-11-09, the genre's usual battle screen): a row of
    /// cards along the bottom, one per hero and one per kind of troop; End Battle and the time left at the top centre;
    /// the target, its loot and the stars at the top right; the damage at the bottom right. A raid is fought live
    /// (ADR-050): the clock runs from the start and a card is dropped by tapping it, then the map. A fight the server
    /// already fought (Siege, Arena, Trials) shows the cards used.
    /// </summary>
    public sealed partial class VillageController
    {
        private const float CardWidth = 116f;
        private const float CardHeight = 142f;
        private const float CardGap = 10f;

        private static readonly Color CardSelected = new Color(1f, 0.82f, 0.35f);

        /// <summary>
        /// What a live battle shows over the bar and HUD: the hint or the last refusal under the clock, the Auto button,
        /// and the card row. Rebuilt after each drop; the bar, stars and damage stay.
        /// </summary>
        private void ShowDeployScreen()
        {
            if (_raid == null) return;
            ClearDeployScreen();
            if (!LiveRunning)
            {
                ClearCardRow();
                return;
            }

            var screen = Ui.Rect("DeployScreen", _uiRoot);
            Ui.Stretch(screen);
            _raid.DeployScreen = screen.gameObject;

            var hint = string.IsNullOrEmpty(LastMessage)
                ? (AnythingDropped ? "Drop more cards any time, or End Battle." : "The clock is running: pick a card, then tap open ground. Red ground is too close.")
                : LastMessage;
            var hintLabel = HudArt.Label(screen, hint, HudArt.Numbers, 20, TextAnchor.MiddleCenter, string.IsNullOrEmpty(LastMessage) ? HudArt.Ivory : Ui.AccentColor, HudArt.LacquerDeep);
            hintLabel.raycastTarget = false;
            // Narrow enough to stay clear of the loot panel at the top right; two lines at most.
            Ui.Anchor(hintLabel.rectTransform, new Vector2(0.5f, 1f), new Vector2(600f, 54f), new Vector2(0f, -224f));
            hintLabel.horizontalOverflow = HorizontalWrapMode.Wrap;
            hintLabel.verticalOverflow = VerticalWrapMode.Truncate;
            hintLabel.resizeTextForBestFit = true;
            hintLabel.resizeTextMinSize = 15;
            hintLabel.resizeTextMaxSize = 20;

            // Above the cards, at the left: drop everything left at once.
            if (!NothingLeftToDrop)
            {
                var auto = Ui.Button(screen, "AutoPlaceButton", "Auto", AutoPlaceHeroes, true, null, 22);
                // Its middle on the same line as the damage box's at the bottom right (70 tall, 32 above the cards).
                Ui.Anchor((RectTransform)auto.transform, new Vector2(0f, 0f), new Vector2(130f, 52f), new Vector2(16f, CardHeight + 41f));
            }

            BuildCardRow(true);
        }

        private void ClearDeployScreen()
        {
            if (_raid?.DeployScreen != null) Destroy(_raid.DeployScreen);
            if (_raid != null) _raid.DeployScreen = null;
        }

        private void ClearCardRow()
        {
            if (_raid?.CardRow != null) Destroy(_raid.CardRow);
            if (_raid != null) _raid.CardRow = null;
        }

        /// <summary>
        /// The bottom row: a card per hero (picture, number, level) and per kind of troop the player has (count left).
        /// In a live battle a card is chosen by tapping it; in a fought one the cards are only shown.
        /// </summary>
        private void BuildCardRow(bool landing)
        {
            ClearCardRow();
            var team = _raid.Session.team;
            var troops = TroopData.All.Where(t => Army.CountOf(t.id) > 0 || _raid.Stacks.Any(s => s.troopId == t.id)).ToList();
            var count = team.Count + troops.Count;
            if (count == 0) return;

            var row = Ui.Rect("CardRow", _uiRoot);
            var width = count * CardWidth + (count - 1) * CardGap + 24f;
            Ui.Anchor(row, new Vector2(0.5f, 0f), new Vector2(width, CardHeight + 20f), new Vector2(0f, 6f));
            Ui.Framed(row, HudArt.Sprite("card_s34"), true, new Color(0.55f, 0.55f, 0.6f, 0.9f));
            _raid.CardRow = row.gameObject;

            var x = 12f;
            for (var i = 0; i < team.Count; i++)
            {
                var member = team[i];
                var placed = _raid.Placements.Any(p => p.instanceId == member.instanceId);
                var selected = landing && _raid.SelectedTroop == null && !placed && member.instanceId == (_raid.SelectedHero ?? NextHeroToLand()?.instanceId);
                var card = Card(row, "HeroCard" + (i + 1), x, selected, landing && !placed, () => SelectHero(member.instanceId));
                if (Roster.Catalog.TryGet(member.archetypeId, out var archetype))
                    HeroPicture(card, archetype, member.evolutionIndex, CardWidth - 8f, 26f);
                CardBadge(card, (i + 1).ToString());
                CardFooter(card, placed && landing ? "Landed" : $"Lv {member.level}");
                if (placed || !landing) Dim(card);
                x += CardWidth + CardGap;
            }
            foreach (var troop in troops)
            {
                var left = TroopsLeft(troop.id);
                var selected = landing && _raid.SelectedTroop == troop.id;
                var card = Card(row, "TroopCard_" + troop.id, x, selected, landing && left > 0, () => SelectTroop(troop.id));
                var icon = HudArt.Picture(card, "Icon", "icon_troop_card", new Vector2(0.5f, 1f), new Vector2(64f, 64f), new Vector2(0f, -14f));
                icon.preserveAspect = true;
                var name = HudArt.Label(card, troop.displayName, HudArt.Numbers, 15, TextAnchor.MiddleCenter, HudArt.Ivory, HudArt.LacquerDeep);
                Ui.Anchor(name.rectTransform, new Vector2(0.5f, 0f), new Vector2(CardWidth - 8f, 22f), new Vector2(0f, 34f));
                CardFooter(card, $"{(landing ? left : _raid.Stacks.Where(s => s.troopId == troop.id).Sum(s => s.count))}x");
                if (left <= 0 || !landing) Dim(card);
                x += CardWidth + CardGap;
            }
        }

        private RectTransform Card(RectTransform row, string name, float x, bool selected, bool interactable, System.Action onTap)
        {
            var card = Ui.Rect(name, row);
            card.anchorMin = card.anchorMax = new Vector2(0f, 0.5f);
            card.pivot = new Vector2(0f, 0.5f);
            card.sizeDelta = new Vector2(CardWidth, CardHeight);
            card.anchoredPosition = new Vector2(x, 0f);
            var face = Ui.Framed(card, HudArt.Sprite("card_s34"), true, selected ? CardSelected : Color.white);
            if (selected) UiMotion.Breathe(face, 0.9f, 1.4f);
            var button = card.gameObject.AddComponent<Button>();
            button.targetGraphic = face;
            button.interactable = interactable;
            var colors = button.colors;
            colors.disabledColor = Color.white;
            button.colors = colors;
            button.onClick.AddListener(() => GameSounds.Play(GameSound.Click));
            button.onClick.AddListener(() => onTap());
            UiMotion.Pressable(button);
            return card;
        }

        private static void CardBadge(RectTransform card, string number)
        {
            var badge = HudArt.Picture(card, "Number", "hud_notification_badge", new Vector2(1f, 1f), new Vector2(30f, 30f), new Vector2(4f, 4f));
            badge.preserveAspect = true;
            var label = HudArt.Label(badge.transform, number, HudArt.Numbers, 16, TextAnchor.MiddleCenter, HudArt.Ivory, HudArt.LacquerDeep);
            Ui.Stretch(label.rectTransform);
        }

        private static void CardFooter(RectTransform card, string text)
        {
            var plate = Ui.Rect("Footer", card);
            Ui.Anchor(plate, new Vector2(0.5f, 0f), new Vector2(CardWidth - 10f, 28f), new Vector2(0f, 4f));
            Ui.Background(plate, new Color(0.05f, 0.07f, 0.12f, 0.85f), false);
            var label = HudArt.Label(plate, text, HudArt.Numbers, 18, TextAnchor.MiddleCenter, HudArt.Ivory, HudArt.LacquerDeep);
            Ui.Stretch(label.rectTransform);
        }

        /// <summary>A used card goes grey, like the genre's spent cards.</summary>
        private static void Dim(RectTransform card)
        {
            foreach (var graphic in card.GetComponentsInChildren<Graphic>(true))
                if (graphic is Image || graphic is RawImage) graphic.color = new Color(graphic.color.r * 0.5f, graphic.color.g * 0.5f, graphic.color.b * 0.52f, graphic.color.a);
        }

        private RaidTeamMember NextHeroToLand() =>
            _raid.Session.team.FirstOrDefault(m => _raid.Placements.All(p => p.instanceId != m.instanceId));

        private void SelectHero(string instanceId)
        {
            if (!LiveRunning) return;
            _raid.SelectedTroop = null;
            _raid.SelectedHero = instanceId;
            ShowDeployScreen();
        }

        private void SelectTroop(string troopId)
        {
            if (!LiveRunning) return;
            _raid.SelectedTroop = troopId;
            _raid.SelectedHero = null;
            ShowDeployScreen();
        }

        /// <summary>
        /// Top right: whom you are fighting, what there is to win and, during the battle, the stars. Named BattleHud
        /// during the battle, so it goes with the battle.
        /// </summary>
        private RectTransform TargetPanel(Transform parent, bool battle)
        {
            var panel = Ui.Rect(battle ? "BattleTarget" : "RaidTarget", parent);
            var height = _modeFight == null ? (battle ? 184f : 150f) : (battle ? 122f : 70f);
            Ui.Anchor(panel, new Vector2(1f, 1f), new Vector2(300f, height), new Vector2(-16f, -104f));
            Ui.Framed(panel, Ui.PillSprite);
            var session = _raid.Session;
            var who = _modeFight != null ? (_modeFight.IsSiege ? "Defend your village" : $"vs {_modeFight.OpponentName}") : session.raid.displayName;
            var name = Ui.Strong(Ui.Label(panel, who, 22, TextAnchor.MiddleCenter, new Color(1f, 0.86f, 0.45f)));
            Ui.Anchor(name.rectTransform, new Vector2(0.5f, 1f), new Vector2(284f, 32f), new Vector2(0f, -6f));
            if (_modeFight == null)
            {
                var payable = Mathf.Max(0, 3 - BestStarsOn(session.raid.id));
                var rival = session.raid.id == "rival";
                // Available loot is gold and Mana (owner, 2026-11-09), each on its own line with its icon.
                var heading = Ui.Label(panel, "Available loot", 17, TextAnchor.MiddleCenter, HudArt.IvoryMuted);
                Ui.Anchor(heading.rectTransform, new Vector2(0.5f, 1f), new Vector2(240f, 24f), new Vector2(0f, -36f));
                // A rival offers a share of what it has (ADR-049); a dungeon pays per new star.
                LootLine(panel, "LootGold", "icon_gold", rival ? session.raid.availableGold : payable * session.raid.goldPerStar, -62f);
                LootLine(panel, "LootMana", "icon_chakra", rival ? session.raid.availableMana : payable * session.raid.manaPerStar, -92f);
            }
            if (!battle && session.defenders.Count > 0)
            {
                var guards = Ui.Label(panel, session.defenders.Count == 1 ? "1 enemy hero on guard" : $"{session.defenders.Count} enemy heroes on guard", 17, TextAnchor.MiddleCenter, Ui.AccentColor);
                Ui.Anchor(guards.rectTransform, new Vector2(0.5f, 0f), new Vector2(284f, 26f), new Vector2(0f, 4f));
            }
            return panel;
        }

        private static void LootLine(RectTransform panel, string name, string icon, long amount, float y)
        {
            var picture = HudArt.Picture(panel, name + "Icon", icon, new Vector2(0.5f, 1f), new Vector2(28f, 28f), new Vector2(-70f, y));
            picture.preserveAspect = true;
            var label = HudArt.Label(panel, amount.ToString("N0"), HudArt.Numbers, 22, TextAnchor.MiddleLeft, HudArt.Ivory, HudArt.LacquerDeep);
            label.name = name;
            Ui.Anchor(label.rectTransform, new Vector2(0.5f, 1f), new Vector2(150f, 30f), new Vector2(26f, y));
        }

        /// <summary>
        /// Top centre during the battle: End Battle and the time left. A fought log also gets a speed button; a live
        /// battle runs at its own pace, as the server checks it took as long as it says (ADR-050).
        /// </summary>
        private void CreateBattleBar(bool live)
        {
            ClearDeployScreen();
            var bar = new GameObject("BattleBar", typeof(RectTransform));
            var rect = (RectTransform)bar.transform;
            rect.SetParent(_uiRoot, false);
            Ui.Anchor(rect, new Vector2(0.5f, 1f), new Vector2(420f, 116f), new Vector2(0f, -100f));
            var end = Ui.Button(bar.transform, "SkipBattleButton", "End Battle", SkipBattle, true, Ui.AttackColor, 26);
            Ui.Anchor((RectTransform)end.transform, new Vector2(0.5f, 1f), new Vector2(260f, 60f), new Vector2(0f, 0f));
            if (!live)
            {
                Button speed = null;
                speed = Ui.Button(bar.transform, "BattleSpeedButton", SpeedCaption(), () =>
                {
                    _battleSpeed = _battleSpeed >= 3f ? 1f : _battleSpeed + 1f;
                    if (_raid?.Stage != null) _raid.Stage.PlaybackSpeed = _battleSpeed;
                    speed.GetComponentInChildren<Text>().text = SpeedCaption();
                }, true, null, 22);
                Ui.Anchor((RectTransform)speed.transform, new Vector2(1f, 1f), new Vector2(76f, 52f), new Vector2(0f, -4f));
                _raid.Stage.PlaybackSpeed = _battleSpeed;
            }
            var time = HudArt.Label(bar.transform, "", HudArt.Numbers, 30, TextAnchor.MiddleCenter, HudArt.Ivory, HudArt.LacquerDeep);
            Ui.Anchor(time.rectTransform, new Vector2(0.5f, 0f), new Vector2(300f, 48f), new Vector2(0f, 0f));
            _raid.StatusBar = bar;
            _raid.StatusText = time;
            BuildCardRow(live);
        }

        private void CreateBattleHud()
        {
            GameSounds.Play(GameSound.Horn);
            // One full-screen holder, named BattleHud, so everything the battle shows goes together.
            var hud = Ui.Rect("BattleHud", _raid.StatusBar.transform.parent);
            Ui.Stretch(hud);
            hud.SetAsFirstSibling();
            hud.SetSiblingIndex(_raid.StatusBar.transform.GetSiblingIndex());
            _raid.Hud = hud.gameObject;

            var target = TargetPanel(hud, true);
            UiMotion.SlideIn(target, new Vector2(340f, 0f), 0.3f);
            _battleHud = new BattleHud { Stars = new Image[3] };
            for (var i = 0; i < 3; i++)
                _battleHud.Stars[i] = Ui.Icon(target, UiSkin.Diamond(new Color(0.35f, 0.35f, 0.38f), new Color(0.2f, 0.2f, 0.23f)), new Vector2(0.5f, 0f), new Vector2(42f, 42f), new Vector2((i - 1) * 56f, 12f));

            // Bottom right: how much is destroyed, above the card row.
            var damage = Ui.Rect("Damage", hud);
            Ui.Anchor(damage, new Vector2(1f, 0f), new Vector2(170f, 70f), new Vector2(-16f, CardHeight + 32f));
            Ui.Framed(damage, Ui.PillSprite);
            _battleHud.Damage = Ui.Strong(Ui.Label(damage, "0%", 34, TextAnchor.MiddleCenter, HudArt.Ivory));
            Ui.Stretch(_battleHud.Damage.rectTransform);

            var banner = Ui.Strong(Ui.Label(_raid.StatusBar.transform.parent, "BATTLE!", 72, TextAnchor.MiddleCenter, new Color(1f, 0.82f, 0.35f)));
            Ui.Anchor(banner.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(620f, 120f), new Vector2(0f, 110f));
            UiMotion.PopIn(banner.transform, 0f, 2.2f, 0.4f);
            Destroy(banner.gameObject, 1.1f);
        }

        /// <summary>Everything the attack screen put up, gone: for the result and for leaving.</summary>
        private void ClearAttackScreen()
        {
            ClearDeployScreen();
            ClearCardRow();
            if (_raid?.StatusBar != null) Destroy(_raid.StatusBar);
            if (_raid?.Hud != null) Destroy(_raid.Hud);
            _battleHud = null;
        }
    }
}
