using System;
using System.Collections.Generic;
using ShinobiDominion.Core;
using UnityEngine;
using UnityEngine.UI;

namespace ShinobiDominion.Game
{
    /// <summary>
    /// Tapping a building shows its name on a plate and a short row of medallion buttons at the bottom centre,
    /// leaving the village in view (docs/ui/HUD_SPEC.md). Info opens the full details panel.
    /// </summary>
    public sealed partial class VillageController
    {
        private const float ActionSize = 104f;
        private const float InfoSize = 104f;
        private const float ActionGap = 14f;

        private GameObject _actionBar;
        private int _actionBarBuilding = -1;

        public bool ActionBarOpen => _actionBar != null;

        private struct BarAction
        {
            public string Name;
            public string Caption;
            public string Icon;
            public MedalKind Kind;
            public Action OnClick;
            public bool Enabled;
            public string Cost;
            public bool CostShort;
        }

        private void HideActionBar()
        {
            if (_actionBar == null) return;
            Destroy(_actionBar);
            _actionBar = null;
            _actionBarBuilding = -1;
        }

        /// <summary>The bar belongs to one selected building; anything else on screen takes it away.</summary>
        private void UpdateActionBar()
        {
            if (_actionBar == null) return;
            if (_panel.IsOpen || _raid != null || UiState.Screen != VillageScreen.BuildingPanel || UiState.SelectedBuildingId != _actionBarBuilding)
                HideActionBar();
        }

        private List<BarAction> ActionsFor(BuildingInstance building)
        {
            var definition = building.Definition;
            var actions = new List<BarAction>
            {
                new BarAction { Name = "InfoButton", Caption = "Info", Icon = "info", Kind = MedalKind.IconOnly, OnClick = () => ShowBuildingDetails(building.Id), Enabled = true }
            };

            var primary = PrimaryAction(building);
            if (primary.HasValue) actions.Add(primary.Value);

            if (building.Level < definition.maxLevel)
            {
                var cost = definition.UpgradeCost(building.Level);
                var needsDojo = definition.At(building.Level + 1).requiredHq;
                var affordable = Village.CanAfford(cost, 0);
                actions.Add(new BarAction
                {
                    Name = "UpgradeButton", Caption = "Upgrade", Icon = "upgrade", Kind = MedalKind.Secondary,
                    OnClick = () => Upgrade(building.Id, false), Enabled = affordable && Village.HqLevel >= needsDojo,
                    Cost = Village.HqLevel < needsDojo ? $"Dojo {needsDojo}" : cost.ToString("N0"), CostShort = !affordable || Village.HqLevel < needsDojo
                });
            }

            actions.Add(new BarAction { Name = "MoveButton", Caption = "Move", Icon = "move", Kind = MedalKind.Secondary, OnClick = () => BeginMove(building.Id), Enabled = true });
            if (definition.removable)
                actions.Add(new BarAction { Name = "RemoveButton", Caption = "Remove", Icon = "close", Kind = MedalKind.Destructive, OnClick = () => RemoveSelected(building.Id), Enabled = true });
            return actions;
        }

        /// <summary>The building's own job, as the gold medallion. Buildings with nothing to do here have none.</summary>
        private BarAction? PrimaryAction(BuildingInstance building)
        {
            switch (building.Definition.function)
            {
                case BuildingFunction.BuilderHall:
                    return new BarAction { Name = "BuildButton", Caption = "Build", Icon = "build", Kind = MedalKind.Primary, OnClick = () => OpenCatalog(), Enabled = true };
                case BuildingFunction.HeroAltar:
                    return new BarAction { Name = "RosterButton", Caption = "Heroes", Icon = "roster", Kind = MedalKind.Primary, OnClick = ShowAltarRoster, Enabled = true };
                case BuildingFunction.HeroBase:
                    return new BarAction { Name = "ChangeHeroButton", Caption = "Hero", Icon = "hero", Kind = MedalKind.Primary, OnClick = () => ShowHeroPicker(building.Id), Enabled = Roster.HeroCount > 0 };
                case BuildingFunction.SummoningGate:
                    return new BarAction { Name = "OpenGateButton", Caption = "Summon", Icon = "gate", Kind = MedalKind.Primary, OnClick = ShowSummoningGate, Enabled = IsOnline };
                case BuildingFunction.Training:
                    return new BarAction { Name = "TrainButton", Caption = "Train", Icon = "train", Kind = MedalKind.Primary, OnClick = () => ShowBuildingDetails(building.Id), Enabled = true };
                case BuildingFunction.Collector:
                    var pending = Village.PendingCollection(building, Now);
                    return new BarAction { Name = "CollectButton", Caption = "Collect", Icon = "gold", Kind = MedalKind.Primary, OnClick = () => Collect(building.Id), Enabled = pending > 0 };
            }
            if (building.Definition.IsWall)
            {
                var group = Village.ConnectedWalls(building.Id);
                return new BarAction { Name = "UpgradeGroupButton", Caption = $"All {group.Count}", Icon = "walls", Kind = MedalKind.Primary, OnClick = () => Upgrade(building.Id, true), Enabled = true };
            }
            return null;
        }

        private void ShowActionBar(BuildingInstance building)
        {
            HideActionBar();
            var actions = ActionsFor(building);
            var bar = Ui.Rect("ActionBar", _uiRoot);
            Ui.Stretch(bar);
            _actionBar = bar.gameObject;
            _actionBarBuilding = building.Id;

            // Name plate: the building on the left, its level on the right.
            var plate = Ui.Rect("NamePlate", bar);
            Ui.Anchor(plate, new Vector2(0.5f, 0f), new Vector2(430f, 52f), new Vector2(0f, 140f));
            var plateImage = plate.gameObject.AddComponent<Image>();
            plateImage.sprite = HudArt.Sprite("plaque_arrow_s40");
            plateImage.type = Image.Type.Sliced;
            plateImage.raycastTarget = false;
            HudArt.DropShadow(plate);
            var title = HudArt.Label(plate, $"{building.Definition.displayName}  (Lv {building.Level})", HudArt.Display, 22, TextAnchor.MiddleCenter, HudArt.Ivory, HudArt.LacquerDeep);
            Ui.Stretch(title.rectTransform, 46f, 2f, 46f, 0f);
            title.resizeTextForBestFit = true;
            title.resizeTextMinSize = 14;
            title.resizeTextMaxSize = 22;

            if (!string.IsNullOrEmpty(LastMessage) && LastMessage != VillageErrorText.Describe(VillageError.None))
            {
                var note = HudArt.Label(bar, LastMessage, HudArt.Numbers, 17, TextAnchor.MiddleCenter, HudArt.BrassLight, HudArt.LacquerDeep);
                Ui.Anchor(note.rectTransform, new Vector2(0.5f, 0f), new Vector2(640f, 26f), new Vector2(0f, 196f));
            }

            // The row is sized from its contents, so two actions or five always sit centred.
            var width = 0f;
            foreach (var action in actions) width += (action.Kind == MedalKind.IconOnly ? InfoSize : ActionSize) + ActionGap;
            width -= ActionGap;
            var x = -width / 2f;
            var index = 0;
            foreach (var action in actions)
            {
                var size = action.Kind == MedalKind.IconOnly ? InfoSize : ActionSize;
                var tile = Tile.Create(bar, action.Name, action.Icon, action.Caption, action.OnClick, action.Enabled, size);
                var rect = (RectTransform)tile.transform;
                Ui.Anchor(rect, new Vector2(0.5f, 0f), new Vector2(size, size), new Vector2(x + size / 2f, 18f));
                if (!string.IsNullOrEmpty(action.Cost)) CostTag(rect, action.Cost, action.CostShort);
                UiMotion.PopIn(rect, 0.04f * index++, 0.6f, 0.22f);
                x += size + ActionGap;
            }
            UiMotion.SlideIn(plate, new Vector2(0f, -24f), 0.18f);
            UiMotion.FadeIn(_actionBar, 0.18f);
        }

        /// <summary>A small lacquer tag under a medallion with a coin and the price; red when it cannot be paid.</summary>
        private static void CostTag(RectTransform medal, string cost, bool short_)
        {
            var tag = Ui.Rect("CostTag", medal);
            Ui.Anchor(tag, new Vector2(0.5f, 1f), new Vector2(84f, 24f), new Vector2(0f, 14f));
            HudArt.Plate(tag).pixelsPerUnitMultiplier = 2.6f;
            HudArt.Picture(tag, "Coin", "icon_gold", new Vector2(0f, 0.5f), new Vector2(20f, 20f), new Vector2(5f, 0f));
            var text = HudArt.Label(tag, cost, HudArt.Numbers, 15, TextAnchor.MiddleRight, short_ ? HudArt.CrimsonLight : HudArt.Ivory, HudArt.LacquerDeep);
            Ui.Stretch(text.rectTransform, 24f, 0f, 8f, 0f);
        }
    }
}
