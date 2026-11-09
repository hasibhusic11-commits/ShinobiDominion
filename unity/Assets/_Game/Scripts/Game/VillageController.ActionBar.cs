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
        private const float ActionSize = 108f;
        private const float InfoSize = 108f;
        private const float ActionGap = 18f;

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
            /// <summary>The picture beside the cost: gold unless set.</summary>
            public string CostIcon;
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

            if (building.Upgrading)
            {
                // While it upgrades, the medallion finishes the job for gems instead (ADR-040).
                var gems = Village.FinishCost(building.UpgradeDoneUtcTicks - Now);
                actions.Add(new BarAction
                {
                    Name = "FinishUpgradeButton", Caption = FormatDuration(building.UpgradeDoneUtcTicks - Now), Icon = "crystals", Kind = MedalKind.Secondary,
                    OnClick = () => FinishUpgrade(building.Id), Enabled = Village.Crystals >= gems,
                    Cost = gems.ToString("N0"), CostShort = Village.Crystals < gems, CostIcon = "icon_crystals"
                });
            }
            else if (building.Level < definition.maxLevel)
            {
                var cost = definition.UpgradeCost(building.Level);
                var needsDojo = definition.At(building.Level + 1).requiredHq;
                var affordable = Village.CanAfford(cost, 0);
                actions.Add(new BarAction
                {
                    Name = "UpgradeButton", Caption = "Upgrade", Icon = "upgrade", Kind = MedalKind.Secondary,
                    OnClick = () => Upgrade(building.Id, false), Enabled = affordable && Village.HqLevel >= needsDojo && (Village.Catalog.UpgradeSeconds(definition, building.Level + 1) == 0 || Village.BusyBuilders < Village.Builders),
                    Cost = Village.HqLevel < needsDojo ? $"TH {needsDojo}" : cost.ToString("N0"), CostShort = !affordable || Village.HqLevel < needsDojo
                });
            }

            actions.Add(new BarAction { Name = "MoveButton", Caption = "Move", Icon = "move", Kind = MedalKind.Secondary, OnClick = () => BeginMove(building.Id), Enabled = true });
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
                    return new BarAction { Name = "OpenGateButton", Caption = "Summon", Icon = "summon", Kind = MedalKind.Primary, OnClick = ShowSummoningGate, Enabled = IsOnline };
                case BuildingFunction.Guild:
                    return new BarAction { Name = "GuildButton", Caption = "Guild", Icon = "guild", Kind = MedalKind.Primary, OnClick = () => OpenShortcut("friends"), Enabled = IsOnline };
                case BuildingFunction.Arena:
                    return new BarAction { Name = "ArenaButton", Caption = "Arena", Icon = "arena", Kind = MedalKind.Primary, OnClick = () => { CloseAll(); OpenAttack(); OpenArena(); }, Enabled = IsOnline };
                case BuildingFunction.Research:
                    return new BarAction { Name = "ResearchButton", Caption = "Research", Icon = "research", Kind = MedalKind.Primary, OnClick = () => ShowBuildingDetails(building.Id), Enabled = true };
                case BuildingFunction.Training:
                    return new BarAction { Name = "TrainButton", Caption = "Train", Icon = "train", Kind = MedalKind.Primary, OnClick = () => ShowBuildingDetails(building.Id), Enabled = true };
                case BuildingFunction.Collector:
                    var pending = Village.PendingCollection(building, Now);
                    return new BarAction { Name = "CollectButton", Caption = "Collect", Icon = "collect", Kind = MedalKind.Primary, OnClick = () => Collect(building.Id), Enabled = pending > 0 };
            }
            if (!string.IsNullOrEmpty(building.Definition.group))
                return new BarAction { Name = "SwitchButton", Caption = "Switch", Icon = "replay", Kind = MedalKind.Primary, OnClick = () => ShowTowerSwitch(building.Id), Enabled = !building.Upgrading };
            if (building.Definition.IsWall)
            {
                var group = Village.ConnectedWalls(building.Id);
                return new BarAction { Name = "UpgradeGroupButton", Caption = $"All {group.Count}", Icon = "walls", Kind = MedalKind.Primary, OnClick = () => Upgrade(building.Id, true), Enabled = true };
            }
            return null;
        }

        /// <summary>
        /// The Arrow, Cannon and Magic Towers are one building the player can switch at any time (owner,
        /// 2026-11-09): free and instant, keeping the level. The server decides online.
        /// </summary>
        private void ShowTowerSwitch(int id)
        {
            var building = Village.Find(id);
            if (building == null) return;
            HideActionBar();
            _panel.Show("Switch tower", CloseAll, panel =>
            {
                panel.Line($"Your level {building.Level} {building.Definition.displayName} can become another tower. It keeps its level, and switching is free.", 19);
                foreach (var option in Village.SwitchOptions(building))
                {
                    var to = option.id;
                    panel.Action("Switch_" + to, $"{option.displayName}: {option.description}", () => SwitchTower(id, to), !building.Upgrading, null, 19);
                }
            });
        }

        private void SwitchTower(int id, string to)
        {
            if (RefuseWhenNoServer()) return;
            if (IsOnline)
            {
                VillageRequest($"switch|{id}|{to}", key => _server.SwitchAsync(key, id, to), ShowContextPanel, ShowContextPanel);
                return;
            }
            RunAndRefresh(Village.TrySwitch(id, to));
        }

        private void ShowActionBar(BuildingInstance building)
        {
            HideActionBar();
            var actions = ActionsFor(building);
            var bar = Ui.Rect("ActionBar", _uiRoot);
            Ui.Stretch(bar);
            _actionBar = bar.gameObject;
            _actionBarBuilding = building.Id;

            // Owner's newest mockup (docs/ui/target_home_mockup_v2.webp): the painted medallions sit in a row at
            // the bottom right, each with its caption plate, and the building's name plate floats above the row.
            var width = 0f;
            foreach (var action in actions) width += (action.Kind == MedalKind.IconOnly ? InfoSize : ActionSize) + ActionGap;
            width -= ActionGap;
            const float right = 18f;
            const float bottom = 34f;

            var plate = Ui.Rect("NamePlate", bar);
            Ui.Anchor(plate, new Vector2(1f, 0f), new Vector2(Mathf.Max(width, 420f), 54f), new Vector2(-right - (width - Mathf.Max(width, 420f)) / 2f, bottom + ActionSize + 44f));
            var plateImage = plate.gameObject.AddComponent<Image>();
            plateImage.sprite = HudArt.Sprite("hud_building_nameplate");
            plateImage.raycastTarget = false;
            HudArt.DropShadow(plate);
            var title = HudArt.Label(plate, $"{building.Definition.displayName}  (Lv {building.Level})", HudArt.Display, 22, TextAnchor.MiddleCenter, HudArt.Ivory, HudArt.LacquerDeep);
            Ui.Stretch(title.rectTransform, 46f, 2f, 46f, 0f);
            title.resizeTextForBestFit = true;
            title.resizeTextMinSize = 14;
            title.resizeTextMaxSize = 22;

            if (!string.IsNullOrEmpty(LastMessage) && LastMessage != VillageErrorText.Describe(VillageError.None))
            {
                var note = HudArt.Label(bar, LastMessage, HudArt.Numbers, 17, TextAnchor.MiddleRight, HudArt.BrassLight, HudArt.LacquerDeep);
                Ui.Anchor(note.rectTransform, new Vector2(1f, 0f), new Vector2(640f, 26f), new Vector2(-right, bottom + ActionSize + 104f));
            }

            var x = -right - width;
            var index = 0;
            foreach (var action in actions)
            {
                var size = action.Kind == MedalKind.IconOnly ? InfoSize : ActionSize;
                var medal = Medal.Create(bar, action.Name, action.Kind, action.Icon, action.Caption, action.OnClick, action.Enabled, size);
                var rect = (RectTransform)medal.transform;
                Ui.Anchor(rect, new Vector2(1f, 0f), new Vector2(size, size), new Vector2(x + size, bottom));
                if (!string.IsNullOrEmpty(action.Cost)) CostTag(rect, action.Cost, action.CostShort, action.CostIcon ?? "icon_gold");
                UiMotion.PopIn(rect, 0.04f * index++, 0.6f, 0.22f);
                x += size + ActionGap;
            }
            UiMotion.SlideIn(plate, new Vector2(0f, -24f), 0.18f);
            UiMotion.FadeIn(_actionBar, 0.18f);
        }

        /// <summary>A small lacquer tag under a medallion with a coin and the price; red when it cannot be paid.</summary>
        private static void CostTag(RectTransform medal, string cost, bool short_, string icon)
        {
            var tag = Ui.Rect("CostTag", medal);
            Ui.Anchor(tag, new Vector2(0.5f, 1f), new Vector2(84f, 24f), new Vector2(0f, 14f));
            HudArt.Plate(tag).pixelsPerUnitMultiplier = 2.6f;
            HudArt.Picture(tag, "Coin", icon, new Vector2(0f, 0.5f), new Vector2(20f, 20f), new Vector2(5f, 0f));
            var text = HudArt.Label(tag, cost, HudArt.Numbers, 15, TextAnchor.MiddleRight, short_ ? HudArt.CrimsonLight : HudArt.Ivory, HudArt.LacquerDeep);
            Ui.Stretch(text.rectTransform, 24f, 0f, 8f, 0f);
        }
    }
}
