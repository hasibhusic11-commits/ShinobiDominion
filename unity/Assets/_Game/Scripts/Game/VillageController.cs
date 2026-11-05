using System;
using System.Collections.Generic;
using System.IO;
using ShinobiDominion.Core;
using UnityEngine;
using UnityEngine.UI;

namespace ShinobiDominion.Game
{
    /// <summary>
    /// Local JSON village store. PROTOTYPE ONLY: Phase 3 replaces this with server persistence,
    /// because a file on the device can be edited by the player.
    /// </summary>
    public sealed class FileVillageStore
    {
        private readonly string _path;

        public FileVillageStore(string path)
        {
            _path = path;
        }

        public string Path => _path;

        public Village LoadOrCreate(BuildingCatalog catalog, long nowUtcTicks)
        {
            if (File.Exists(_path))
            {
                try
                {
                    return Village.FromSave(catalog, VillageSave.FromJson(File.ReadAllText(_path)));
                }
                catch (Exception exception) when (exception is VillageSaveException || exception is IOException)
                {
                    // Keep the bad file for inspection rather than destroying it.
                    Debug.LogWarning($"Village save rejected, starting fresh: {exception.Message}");
                    var backup = _path + ".rejected";
                    if (File.Exists(backup)) File.Delete(backup);
                    File.Move(_path, backup);
                }
            }
            return StarterVillage.Create(catalog, nowUtcTicks);
        }

        /// <summary>Writes to a temp file then swaps it in, so a crash cannot leave a half-written save.</summary>
        public void Save(Village village)
        {
            var temp = _path + ".tmp";
            File.WriteAllText(temp, village.ToSave().ToJson());
            if (File.Exists(_path)) File.Delete(_path);
            File.Move(temp, _path);
        }

        public void Delete()
        {
            if (File.Exists(_path)) File.Delete(_path);
        }
    }

    /// <summary>
    /// Connects model, world and UI. All game rules live in Core; this class only drives the
    /// screen flow and reports errors. Public methods double as the PlayMode test surface.
    /// </summary>
    public sealed partial class VillageController : MonoBehaviour, IWorldGestureHandler
    {
        private static readonly Color GoodCell = new Color(0.25f, 0.85f, 0.40f, 0.45f);
        private static readonly Color BadCell = new Color(0.90f, 0.20f, 0.18f, 0.55f);
        private static readonly Color SelectCell = new Color(0.95f, 0.72f, 0.25f, 0.26f);
        private static readonly Color GhostTint = new Color(0.85f, 0.92f, 1f, 0.55f);

        private FileVillageStore _store;
        private IsoCamera _camera;
        private VillageView _view;
        private GridOverlay _grid;
        private CellHighlight _placementCells;
        private CellHighlight _selectionCells;
        private HudView _hud;
        private PanelView _panel;
        private RectTransform _uiRoot;
        private bool _dirty;
        private BuildingThumbnails _thumbnails;
        private float _markerTimer;

        // Placement state
        private BuildingDefinition _placingDefinition;
        private int _movingId;
        private GridCoord _ghostOrigin;
        private Rotation _ghostRotation;
        private GameObject _ghost;
        private bool _draggingGhost;
        private GridCoord _dragCellOffset;
        private PlacementToolbar _toolbar;
        private Button _confirmButton;
        private Button _undoButton;
        private BuildingCategory _catalogCategory = BuildingCategory.Structures;
        private readonly VillageHistory _history = new VillageHistory();

        // Wall tool state
        private bool _hasWallRun;
        private GridCoord _wallStart;
        private GridCoord _wallEnd;

        public Village Village { get; private set; }
        public VillageUiState UiState { get; } = new VillageUiState();
        public HudView Hud => _hud;
        public PanelView Panel => _panel;
        public GridOverlay Grid => _grid;
        public PlacementToolbar Toolbar => _toolbar;
        public VillageView View => _view;
        public string LastMessage { get; private set; }

        private static long Now => DateTime.UtcNow.Ticks;

        public void Initialize(Village village, FileVillageStore store, HeroRoster roster, FileRosterStore rosterStore, IsoCamera isoCamera, Transform worldRoot, UiRoot ui)
        {
            Village = village;
            _store = store;
            Roster = roster;
            _rosterStore = rosterStore;
            _camera = isoCamera;
            _uiRoot = ui.Safe;

            _view = new VillageView(village, worldRoot, HeroLookFor);
            _thumbnails = new BuildingThumbnails(worldRoot);
            RefreshCollectMarkers();
            _grid = new GridOverlay(worldRoot);
            _placementCells = new CellHighlight(worldRoot, "PlacementCells", GoodCell, BadCell, 0.03f);
            _selectionCells = new CellHighlight(worldRoot, "SelectionCells", SelectCell, SelectCell, 0.025f);
            _hud = new HudView(ui.Safe, OpenAttack, OpenSettings);
            _panel = new PanelView(ui.Safe);

            Village.Changed += OnVillageChanged;
            Roster.Changed += OnRosterChanged;
            UiState.Changed += ApplyUiState;
            RefreshHud();
            ApplyUiState();
        }

        private void OnDestroy()
        {
            if (Village != null) Village.Changed -= OnVillageChanged;
            if (Roster != null) Roster.Changed -= OnRosterChanged;
            UiState.Changed -= ApplyUiState;
            _thumbnails?.Dispose();
        }

        private void Update()
        {
            _markerTimer -= Time.unscaledDeltaTime;
            if (_markerTimer > 0f) return;
            _markerTimer = 1f;
            RefreshCollectMarkers();
        }

        public void RefreshCollectMarkers()
        {
            var now = Now;
            _view.SetCollectMarkers(building => Village.PendingCollection(building, now) >= 1);
        }

        private void LateUpdate()
        {
            if (_dirty) Flush();
            if (_rosterDirty) FlushRoster();
        }

        private void OnApplicationPause(bool paused)
        {
            if (!paused) return;
            Flush();
            FlushRoster();
        }

        private void Flush()
        {
            if (!_dirty || _store == null) return;
            _dirty = false;
            try
            {
                _store.Save(Village);
            }
            catch (IOException exception)
            {
                Debug.LogError($"Could not save the village: {exception.Message}");
            }
        }

        private void OnVillageChanged()
        {
            _dirty = true;
            _view.Sync();
            RefreshHud();
        }

        private void RefreshHud()
        {
            _hud.SetWallet(Village.Gold, ServerCrystals ?? Village.Crystals);
            _hud.SetIdentity("Player", Village.HqLevel);
        }

        private void ApplyUiState()
        {
            _hud.SetAttackVisible(UiState.IsAttackVisible);
            _grid.Visible = UiState.IsGridOverlayVisible;
        }

        // ---------- World gestures ----------

        public bool OnWorldPress(Vector2 screen)
        {
            switch (UiState.Screen)
            {
                case VillageScreen.PaintingWalls:
                    _wallStart = CellAt(screen);
                    _wallEnd = _wallStart;
                    _hasWallRun = true;
                    RefreshWallPreview();
                    return true;
                case VillageScreen.PlacingBuilding:
                case VillageScreen.MovingBuilding:
                    var cell = CellAt(screen);
                    var footprint = Footprint.Of(_placingDefinition, _ghostOrigin, _ghostRotation);
                    if (!footprint.Contains(cell)) return false;
                    _draggingGhost = true;
                    _dragCellOffset = new GridCoord(cell.X - _ghostOrigin.X, cell.Z - _ghostOrigin.Z);
                    return true;
                default:
                    return false;
            }
        }

        public void OnWorldDrag(Vector2 screen)
        {
            if (UiState.Screen == VillageScreen.PaintingWalls)
            {
                _wallEnd = CellAt(screen);
                RefreshWallPreview();
            }
            else if (_draggingGhost)
            {
                var cell = CellAt(screen);
                SetGhostOrigin(new GridCoord(cell.X - _dragCellOffset.X, cell.Z - _dragCellOffset.Z));
            }
        }

        public void OnWorldRelease(Vector2 screen)
        {
            _draggingGhost = false;
        }

        public void OnWorldTap(Vector2 screen)
        {
            switch (UiState.Screen)
            {
                case VillageScreen.Home:
                case VillageScreen.BuildingPanel:
                    var hit = _view.Pick(_camera.ScreenRay(screen));
                    if (hit != null) CollectOrSelect(hit.Id);
                    else CloseAll();
                    break;
                case VillageScreen.BuildCatalog:
                case VillageScreen.AttackFlow:
                    CloseAll();
                    break;
                case VillageScreen.PlacingBuilding:
                case VillageScreen.MovingBuilding:
                    var cell = CellAt(screen);
                    var footprint = Footprint.Of(_placingDefinition, _ghostOrigin, _ghostRotation);
                    SetGhostOrigin(new GridCoord(cell.X - footprint.Width / 2, cell.Z - footprint.Depth / 2));
                    break;
            }
        }

        public void OnBackPressed()
        {
            switch (UiState.Screen)
            {
                case VillageScreen.PlacingBuilding:
                case VillageScreen.PaintingWalls:
                    ExitPlacement();
                    break;
                case VillageScreen.MovingBuilding:
                    CancelMove();
                    break;
                default:
                    CloseAll();
                    break;
            }
        }

        private GridCoord CellAt(Vector2 screen) => GridSpace.WorldToCell(_camera.GroundPoint(screen));

        // ---------- Selection and panels ----------

        /// <summary>
        /// A tap on a mine showing its collect marker collects straight away, with no menu.
        /// When there is nothing to collect, or storage is full, it opens the panel as usual.
        /// </summary>
        public void CollectOrSelect(int id)
        {
            var building = Village.Find(id);
            if (building == null) return;
            if (UiState.Screen == VillageScreen.Home && Village.PendingCollection(building, Now) >= 1
                && Village.TryCollect(id, Now, out _) == VillageError.None)
            {
                RefreshCollectMarkers();
                return;
            }
            Select(id);
        }

        public void Select(int id)
        {
            var building = Village.Find(id);
            if (building == null) return;
            UiState.SelectBuilding(building);
            if (UiState.Screen != VillageScreen.BuildingPanel) return;
            ShowSelectionCells(building);
            ShowContextPanel();
        }

        public void CloseAll()
        {
            _panel.Hide();
            _selectionCells.Clear();
            _history.Clear();
            UiState.CloseToHome();
        }

        private void ShowSelectionCells(BuildingInstance building)
        {
            var cells = new List<KeyValuePair<GridCoord, bool>>();
            foreach (var cell in building.Footprint.Cells())
                cells.Add(new KeyValuePair<GridCoord, bool>(cell, true));
            _selectionCells.Show(cells);
        }

        private void ShowContextPanel()
        {
            var building = Village.Find(UiState.SelectedBuildingId);
            if (building == null)
            {
                CloseAll();
                return;
            }

            var definition = building.Definition;
            if (definition.function == BuildingFunction.HeroAltar && !_showAltarBuildingOptions)
            {
                ShowAltarRoster();
                return;
            }
            if (definition.function == BuildingFunction.SummoningGate && IsOnline && !_showAltarBuildingOptions)
            {
                ShowSummoningGate();
                return;
            }
            _showAltarBuildingOptions = false;

            _panel.Show(definition.displayName, CloseAll, panel =>
            {
                panel.Line(definition.description);
                panel.Line($"Level {building.Level} of {definition.maxLevel}     HP {building.MaxHp:N0}");
                FunctionActions(panel, building);
                UpgradeAction(panel, building);
                if (definition.IsWall)
                {
                    var group = Village.ConnectedWalls(building.Id);
                    panel.Action("UpgradeGroupButton", $"Upgrade connected walls ({group.Count})", () => RunAndRefresh(Village.TryUpgradeWallGroup(building.Id)));
                }
                panel.Action("MoveButton", "Move", () => BeginMove(building.Id));
                if (definition.removable) panel.Action("RemoveButton", "Remove", () => RemoveSelected(building.Id));
            });
        }

        private void FunctionActions(PanelView panel, BuildingInstance building)
        {
            switch (building.Definition.function)
            {
                case BuildingFunction.BuilderHall:
                    panel.Action("BuildButton", "Build", () => OpenCatalog(), true, Ui.AccentColor);
                    break;
                case BuildingFunction.Headquarters:
                    panel.Line("The Dojo level sets how many of each building you can own.");
                    break;
                case BuildingFunction.HeroAltar:
                    panel.Action("RosterButton", "Open roster", ShowAltarRoster, true, Ui.AccentColor);
                    break;
                case BuildingFunction.HeroBase:
                    HeroBaseActions(panel, building);
                    break;
                case BuildingFunction.SummoningGate:
                    panel.Line(IsOnline
                        ? "Summoning runs only on the server."
                        : "Cannot reach the server. Summoning only happens there, so it is unavailable right now.", 20, Ui.AccentColor);
                    if (IsOnline)
                        panel.Action("OpenGateButton", "Open the Gate", ShowSummoningGate, true, Ui.AccentColor);
                    break;
                case BuildingFunction.Training:
                case BuildingFunction.Research:
                    panel.Line("This building's features arrive in a later phase.", 20, Ui.AccentColor);
                    break;
                case BuildingFunction.Collector:
                    var pending = Village.PendingCollection(building, Now);
                    panel.Action("CollectButton", $"Collect {pending:N0} {building.Definition.produces}", () => Collect(building.Id), pending > 0, Ui.AccentColor);
                    break;
                case BuildingFunction.Storage:
                    panel.Line($"Total gold capacity: {Village.GoldCapacity:N0}");
                    break;
                case BuildingFunction.Defense:
                    panel.Line("Attack stats arrive with combat in Phase 4.", 20, Ui.AccentColor);
                    break;
            }
        }

        private void UpgradeAction(PanelView panel, BuildingInstance building)
        {
            if (building.Definition.maxLevel <= 1) return;
            if (building.Level >= building.Definition.maxLevel)
            {
                panel.Line("Maximum level reached.");
                return;
            }
            var cost = building.Definition.UpgradeCost(building.Level);
            panel.Action("UpgradeButton", $"Upgrade ({cost:N0} gold)", () => RunAndRefresh(Village.TryUpgrade(building.Id)), Village.CanAfford(cost, 0));
        }

        private void RunAndRefresh(VillageError error)
        {
            LastMessage = VillageErrorText.Describe(error);
            ShowContextPanel();
        }

        private void Collect(int id)
        {
            RunAndRefresh(Village.TryCollect(id, Now, out _));
        }

        private void RemoveSelected(int id)
        {
            var heldHero = Village.Find(id)?.AssignedHeroInstanceId != null;
            var error = Village.TryRemove(id);
            LastMessage = VillageErrorText.Describe(error);
            if (error == VillageError.None && heldHero) ReleaseSlotOnline(id);
            if (error == VillageError.None) CloseAll();
            else ShowContextPanel();
        }

        // ---------- Attack and settings ----------

        public void OpenAttack()
        {
            if (!UiState.TryOpenAttack()) return;
            _selectionCells.Clear();
            _panel.Show("Attack", CloseAll, panel =>
            {
                panel.Line("Raids arrive in Phase 4. Heroes will deploy from your assigned Hero Bases.");
            });
        }

        private void OpenSettings()
        {
            if (UiState.Screen != VillageScreen.Home && UiState.Screen != VillageScreen.BuildingPanel) return;
            CloseAll();
            _panel.Show("Menu", CloseAll, panel =>
            {
                panel.Line("Prototype build. Your village is saved on this device only.");
                panel.Line(Village.Catalog.BalanceStatus, 18);
                panel.Action("ResetButton", "Reset village (development)", ConfirmReset);
            });
        }

        private void ConfirmReset()
        {
            _panel.Show("Reset village?", CloseAll, panel =>
            {
                panel.Line("This deletes your local layout and starts over. It cannot be undone.");
                panel.Action("ConfirmResetButton", "Yes, reset", ResetVillage, true, Ui.AttackColor);
                panel.Action("CancelResetButton", "Keep my village", CloseAll);
            });
        }

        private void ResetVillage()
        {
            Village.Restore(StarterVillage.Create(Village.Catalog, Now).ToSave());
            CloseAll();
            if (IsOnline) ConnectTask = ConnectAsync();
        }

        // ---------- Build catalog: reachable only through the Builder Hall ----------

        public bool OpenCatalog()
        {
            if (!UiState.TryOpenCatalog()) return false;
            _selectionCells.Clear();
            ShowCatalog();
            return true;
        }

        private void ShowCatalog()
        {
            _panel.ShowScreen("Build", WalletInfo, CloseAll, panel =>
            {
                var tabs = panel.Row();
                AddTab(tabs, BuildingCategory.Structures, "Structures");
                AddTab(tabs, BuildingCategory.Defense, "Defense");
                AddTab(tabs, BuildingCategory.Resources, "Resources");
                AddTab(tabs, BuildingCategory.HeroBases, "Hero Bases");
                AddTab(tabs, BuildingCategory.Walls, "Walls");

                // Unique essentials such as the Dojo always exist, so they would only be noise here.
                var grid = panel.Grid(new Vector2(226f, 330f));
                foreach (var definition in Village.Catalog.InCategory(_catalogCategory))
                    if (definition.removable)
                        CatalogCard(grid, definition);

                if (_history.Count > 0)
                    panel.Action("CatalogUndoButton", "Undo last build", UndoFromCatalog);
            });
        }

        private void AddTab(RectTransform row, BuildingCategory category, string caption)
        {
            var color = category == _catalogCategory ? Ui.AccentColor : Ui.ButtonColor;
            var tab = Ui.Button(row, "Tab" + category, caption, () =>
            {
                _catalogCategory = category;
                ShowCatalog();
            }, true, color, 17);
            tab.GetComponentInChildren<Text>().horizontalOverflow = HorizontalWrapMode.Overflow;
        }

        /// <summary>A shop-style card: name, real building preview, footprint, count and cost.</summary>
        private void CatalogCard(RectTransform grid, BuildingDefinition definition)
        {
            var count = Village.CountOf(definition.id);
            var limit = Village.LimitOf(definition);
            var cost = definition.costCrystals > 0
                ? $"{definition.costGold:N0} gold, {definition.costCrystals:N0} crystals"
                : $"{definition.costGold:N0} gold";

            string blocked = null;
            if (limit == 0) blocked = "Needs a higher Dojo level";
            else if (count >= limit) blocked = "Maximum built";
            else if (!Village.CanAfford(definition.costGold, definition.costCrystals)) blocked = "Not enough resources";

            var rect = Ui.Rect("Place_" + definition.id, grid);
            var frame = Ui.Framed(rect, UiSkin.Frame(new Color(0.32f, 0.30f, 0.36f), new Color(0.14f, 0.14f, 0.20f), UiSkin.Brass, 4));
            var button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = frame;
            button.interactable = blocked == null;
            var colors = button.colors;
            colors.disabledColor = new Color(0.55f, 0.55f, 0.55f, 0.85f);
            button.colors = colors;
            button.onClick.AddListener(() => BeginPlacement(definition.id));

            var title = Ui.Strong(Ui.Label(rect, definition.displayName, 21, TextAnchor.MiddleCenter));
            Ui.Anchor(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(214f, 40f), new Vector2(0f, -6f));

            var preview = Ui.Rect("Preview", rect);
            Ui.Anchor(preview, new Vector2(0.5f, 1f), new Vector2(190f, 190f), new Vector2(0f, -44f));
            var image = preview.gameObject.AddComponent<RawImage>();
            image.texture = _thumbnails.For(definition);
            image.raycastTarget = false;

            var info = Ui.Strong(Ui.Label(rect, $"{definition.width}x{definition.depth}     Built {count}/{limit}", 16, TextAnchor.MiddleCenter));
            Ui.Anchor(info.rectTransform, new Vector2(0.5f, 0f), new Vector2(214f, 26f), new Vector2(0f, 58f));

            var strip = Ui.Rect("Cost", rect);
            Ui.Anchor(strip, new Vector2(0.5f, 0f), new Vector2(206f, 48f), new Vector2(0f, 8f));
            Ui.Framed(strip, UiSkin.ButtonFace, false, blocked == null ? Ui.AccentColor : Ui.ButtonColor);
            var caption = blocked ?? (definition.IsWall ? $"{cost} each" : cost);
            var costLabel = Ui.Strong(Ui.Label(strip, caption, 16, TextAnchor.MiddleCenter));
            Ui.Stretch(costLabel.rectTransform, 4f, 2f, 4f, 2f);
        }

        private void UndoFromCatalog()
        {
            _history.Undo(Village);
            ShowCatalog();
        }

        // ---------- Placement and moving ----------

        public bool BeginPlacement(string definitionId)
        {
            var definition = Village.Catalog.Get(definitionId);
            if (!UiState.TryBeginPlacement(definition.IsWall)) return false;
            _panel.Hide();

            _placingDefinition = definition;
            _ghostRotation = Rotation.R0;
            _toolbar = new PlacementToolbar(_uiRoot);
            if (definition.IsWall)
            {
                _hasWallRun = false;
                _confirmButton = _toolbar.Add("ConfirmButton", "Confirm", () => ConfirmWallRun(), Ui.AccentColor);
                _toolbar.Add("ClearButton", "Clear", ClearWallRun);
                _undoButton = _toolbar.Add("UndoButton", "Undo", UndoWall);
                _toolbar.Add("DoneButton", "Done", ExitPlacement);
                RefreshWallPreview();
            }
            else
            {
                _confirmButton = _toolbar.Add("ConfirmButton", "Confirm", ConfirmPlacement, Ui.AccentColor);
                if (definition.rotatable) _toolbar.Add("RotateButton", "Rotate", RotateGhost);
                _toolbar.Add("CancelButton", "Cancel", ExitPlacement);
                CreateGhost(definition);
                SetGhostOrigin(ViewCenterOrigin(definition));
            }
            return true;
        }

        private GridCoord ViewCenterOrigin(BuildingDefinition definition)
        {
            var cell = CellAt(new Vector2(Screen.width * 0.5f, Screen.height * 0.5f));
            return new GridCoord(cell.X - definition.width / 2, cell.Z - definition.depth / 2);
        }

        private void CreateGhost(BuildingDefinition definition)
        {
            DestroyGhost();
            _ghost = _view.CreateGhost(definition, GhostTint);
        }

        private void DestroyGhost()
        {
            if (_ghost != null) Destroy(_ghost);
            _ghost = null;
        }

        /// <summary>Clamps so the ghost stays fully on the map. Validity comes from Core, never from this clamp.</summary>
        public void SetGhostOrigin(GridCoord origin)
        {
            var footprint = Footprint.Of(_placingDefinition, origin, _ghostRotation);
            var x = Mathf.Clamp(origin.X, 0, VillageGrid.Size - footprint.Width);
            var z = Mathf.Clamp(origin.Z, 0, VillageGrid.Size - footprint.Depth);
            _ghostOrigin = new GridCoord(x, z);
            RefreshGhost();
        }

        private void RotateGhost()
        {
            _ghostRotation = _ghostRotation.Next();
            SetGhostOrigin(_ghostOrigin);
        }

        private VillageError GhostError()
        {
            if (UiState.Screen == VillageScreen.MovingBuilding)
                return Village.CheckFootprint(_placingDefinition, _ghostOrigin, _ghostRotation, _movingId);
            return Village.CheckNewPlacement(_placingDefinition, _ghostOrigin, _ghostRotation);
        }

        private void RefreshGhost()
        {
            var footprint = Footprint.Of(_placingDefinition, _ghostOrigin, _ghostRotation);
            if (_ghost != null)
            {
                _ghost.transform.localPosition = GridSpace.FootprintCenter(footprint);
                _ghost.transform.localRotation = GridSpace.ToQuaternion(_ghostRotation);
            }

            var cells = new List<KeyValuePair<GridCoord, bool>>();
            foreach (var cell in footprint.Cells())
            {
                var occupant = Village.Grid.OccupantAt(cell);
                var free = !Village.Grid.IsBlocked(cell) &&
                           (occupant == VillageGrid.NoOccupant || occupant == _movingId);
                cells.Add(new KeyValuePair<GridCoord, bool>(cell, free));
            }
            _placementCells.Show(cells);

            var error = GhostError();
            if (_confirmButton != null) _confirmButton.interactable = error == VillageError.None;
            if (_toolbar != null)
            {
                var name = _placingDefinition.displayName;
                _toolbar.SetStatus(error == VillageError.None
                    ? (UiState.Screen == VillageScreen.MovingBuilding
                        ? $"Move {name} here?"
                        : $"Place {name} for {_placingDefinition.costGold:N0} gold?")
                    : $"Blocked: {VillageErrorText.Describe(error)}");
            }
        }

        public VillageError ConfirmPlacementAction()
        {
            var error = GhostError();
            LastMessage = VillageErrorText.Describe(error);
            if (error != VillageError.None) return error;

            _history.Capture(Village);
            error = Village.TryPlace(_placingDefinition.id, _ghostOrigin, _ghostRotation, Now, out _);
            LastMessage = VillageErrorText.Describe(error);
            if (error == VillageError.None) ExitPlacement();
            return error;
        }

        private void ConfirmPlacement()
        {
            if (UiState.Screen == VillageScreen.MovingBuilding) ConfirmMove();
            else ConfirmPlacementAction();
        }

        public void ExitPlacement()
        {
            TearDownPlacement();
            UiState.EndPlacement();
            if (UiState.IsCatalogVisible) ShowCatalog();
        }

        private void TearDownPlacement()
        {
            DestroyGhost();
            _placementCells.Clear();
            _toolbar?.Destroy();
            _toolbar = null;
            _confirmButton = null;
            _undoButton = null;
            _hasWallRun = false;
            _draggingGhost = false;
        }

        public bool BeginMove(int id)
        {
            var building = Village.Find(id);
            if (building == null || !UiState.TryBeginMove()) return false;
            _panel.Hide();
            _selectionCells.Clear();

            _movingId = id;
            _placingDefinition = building.Definition;
            _ghostRotation = building.Rotation;
            _view.SetHidden(id);
            CreateGhost(building.Definition);

            _toolbar = new PlacementToolbar(_uiRoot);
            _confirmButton = _toolbar.Add("ConfirmButton", "Confirm", () => ConfirmMove(), Ui.AccentColor);
            if (building.Definition.rotatable) _toolbar.Add("RotateButton", "Rotate", RotateGhost);
            _toolbar.Add("CancelButton", "Cancel", CancelMove);
            SetGhostOrigin(building.Origin);
            return true;
        }

        public VillageError ConfirmMove()
        {
            var error = Village.TryMove(_movingId, _ghostOrigin, _ghostRotation);
            LastMessage = VillageErrorText.Describe(error);
            if (error == VillageError.None) EndMoveFlow();
            return error;
        }

        public void CancelMove() => EndMoveFlow();

        private void EndMoveFlow()
        {
            _movingId = 0;
            _view.SetHidden(0);
            TearDownPlacement();
            UiState.EndMove();
        }

        // ---------- Wall tool ----------

        private void RefreshWallPreview()
        {
            if (_toolbar == null) return;
            if (!_hasWallRun)
            {
                _placementCells.Clear();
                _toolbar.SetStatus("Press and drag across the grid to draw a straight wall.");
                _confirmButton.interactable = false;
                _undoButton.interactable = _history.Count > 0;
                return;
            }

            var preview = Village.PreviewWallRun(_wallStart, _wallEnd);
            var cells = new List<KeyValuePair<GridCoord, bool>>();
            foreach (var cell in preview.Cells)
                if (VillageGrid.InBounds(cell.Coord))
                    cells.Add(new KeyValuePair<GridCoord, bool>(cell.Coord, cell.IsValid));
            _placementCells.Show(cells);

            _toolbar.SetStatus(preview.CanCommit
                ? $"{preview.Cells.Count} wall segments for {preview.TotalGold:N0} gold"
                : $"Blocked: {VillageErrorText.Describe(preview.Error)}");
            _confirmButton.interactable = preview.CanCommit;
            _undoButton.interactable = _history.Count > 0;
        }

        /// <summary>Test and tool entry point: sets the current run as if dragged.</summary>
        public void SetWallRun(GridCoord start, GridCoord end)
        {
            _wallStart = start;
            _wallEnd = end;
            _hasWallRun = true;
            RefreshWallPreview();
        }

        public VillageError ConfirmWallRun()
        {
            if (!_hasWallRun) return VillageError.None;
            var error = Village.PreviewWallRun(_wallStart, _wallEnd).Error;
            LastMessage = VillageErrorText.Describe(error);
            if (error != VillageError.None) return error;

            _history.Capture(Village);
            error = Village.TryPlaceWallRun(_wallStart, _wallEnd, Now);
            LastMessage = VillageErrorText.Describe(error);
            if (error == VillageError.None) ClearWallRun();
            return error;
        }

        private void ClearWallRun()
        {
            _hasWallRun = false;
            RefreshWallPreview();
        }

        private void UndoWall()
        {
            _history.Undo(Village);
            RefreshWallPreview();
        }
    }
}
