using System;
using System.Collections.Generic;
using System.IO;
using ShinobiDominion.Core;
using UnityEngine;
using UnityEngine.UI;
using Text = ShinobiDominion.Game.GameText;

namespace ShinobiDominion.Game
{
    /// <summary>
    /// Local JSON village store for offline development only. It is never read or written while
    /// online, and release builds do not use it, because a player can edit a file on their device.
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
        private Transform _worldRoot;
        private GridOverlay _grid;
        private CellHighlight _placementCells;
        private CellHighlight _selectionCells;
        private HudView _hud;
        private PanelView _panel;
        private RectTransform _uiRoot;
        private bool _dirty;
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
            _worldRoot = worldRoot;

            _view = new VillageView(village, worldRoot, HeroLookFor);
            var lifeObject = new GameObject("VillageLife");
            lifeObject.transform.SetParent(worldRoot, false);
            _life = lifeObject.AddComponent<VillageLife>();
            _life.Initialize(village, TroopData.All[0].id, VillageLifeWalkers);
            RefreshCollectMarkers();
            _grid = new GridOverlay(worldRoot);
            _placementCells = new CellHighlight(worldRoot, "PlacementCells", GoodCell, BadCell, 0.03f);
            _selectionCells = new CellHighlight(worldRoot, "SelectionCells", SelectCell, SelectCell, 0.025f);
            _hud = new HudView(ui.Safe, OpenAttack, OpenSettings, OpenShortcut, OpenShop);
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
        }

        private const int VillageLifeWalkers = 10;
        private VillageLife _life;
        private long _lastGold = -1;
        private long _lastCrystals = -1;
        private Vector3? _lastCollectAt;

        /// <summary>Gold rising after a tap shows on the building it came from, online or off.</summary>
        private void UpdateGoldPopups()
        {
            var gold = Village.Gold;
            if (_lastGold >= 0 && gold > _lastGold && _lastCollectAt.HasValue && _raid == null)
            {
                GameSounds.Play(GameSound.Coin);
                FloatingText.Create(_worldRoot, _camera.Camera, _lastCollectAt.Value, "+" + (gold - _lastGold).ToString("N0"), new Color(1f, 0.85f, 0.3f), 0.14f);
                _hud.FlyCoins(_camera.Camera.WorldToScreenPoint(_lastCollectAt.Value), false);
                _lastCollectAt = null;
            }
            _lastGold = gold;
            var crystals = Village.Crystals;
            if (_lastCrystals >= 0 && crystals > _lastCrystals && _lastCollectAt.HasValue && _raid == null)
            {
                GameSounds.Play(GameSound.Coin, 0.7f);
                FloatingText.Create(_worldRoot, _camera.Camera, _lastCollectAt.Value, "+" + (crystals - _lastCrystals).ToString("N0"), new Color(0.7f, 0.85f, 1f), 0.14f);
                _hud.FlyCoins(_camera.Camera.WorldToScreenPoint(_lastCollectAt.Value), true);
                _lastCollectAt = null;
            }
            _lastCrystals = crystals;
        }

        private void Update()
        {
            UpdateRaid();
            UpdateActionBar();
            UpdateGoldPopups();
            UpdateTimerPills();
            UpdateGuide();
            GameMusic.Intensity = _raid != null && _raid.Phase == RaidPhase.Battle ? 1f : 0f;
            PollGuild();
            FetchRaidListWhenFree();
            // Keep trying to reach the server, but never swap the village out mid-action.
            if (_server != null && !IsOnline && !_connecting && UiState.Screen == VillageScreen.Home)
            {
                _reconnectTimer -= Time.unscaledDeltaTime;
                if (_reconnectTimer <= 0f)
                {
                    _reconnectTimer = 10f;
                    ConnectTask = ConnectAsync();
                }
            }

            _markerTimer -= Time.unscaledDeltaTime;
            if (_markerTimer > 0f) return;
            _markerTimer = 1f;
            TickUpgrades();
            RefreshCollectMarkers();
        }

        public void RefreshCollectMarkers()
        {
            var now = Now;
            _view.SetCollectMarkers(building => Village.PendingCollection(building, now) >= 1);
            _view.SetUpgradeWorks();
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
            // Online, the server holds the truth, so nothing is saved on the device.
            _dirty = !IsOnline;
            _view.Sync();
            RefreshHud();
        }

        private void RefreshHud()
        {
            _hud.SetWallet(Village.Gold, ServerCrystals ?? Village.Crystals, Village.GoldCapacity, Mana);
            _hud.SetIdentity(PlayerName, Village.HqLevel);
            _hud.SetPortrait(GuardPortrait());
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
                case VillageScreen.AttackFlow:
                    if (_raid != null) TapDeployCell(CellAt(screen));
                    else CloseAll();
                    break;
                case VillageScreen.BuildCatalog:
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
            if (UiState.Screen == VillageScreen.Home && Village.PendingCollection(building, Now) >= 1)
            {
                _lastCollectAt = new Vector3(building.Origin.X + building.Definition.width * 0.5f, 2.6f, building.Origin.Z + building.Definition.depth * 0.5f);
                if (CollectFromTap(id)) return;
            }
            Select(id);
        }

        private bool CollectFromTap(int id)
        {
            _guideCollected = true;
            if (IsOnline)
            {
                // If the server says there is nothing yet, the player's tap still opens the panel.
                VillageRequest($"collect|{id}", key => _server.CollectAsync(key, id), RefreshCollectMarkers, () => Select(id));
                return true;
            }
            if (!CanChangeVillage) return false;
            if (Village.TryCollect(id, Now, out _) != VillageError.None) return false;
            RefreshCollectMarkers();
            return true;
        }

        public void Select(int id)
        {
            var building = Village.Find(id);
            if (building == null) return;
            UiState.SelectBuilding(building);
            if (UiState.Screen != VillageScreen.BuildingPanel) return;
            ShowSelectionCells(building);
            ShowContextPanel();
            // Keep the building clear of the side panel, which covers the left of the screen.
            if (_panel.IsOpen && !_panel.IsFullScreen)
                _camera.EnsureVisible(GridSpace.FootprintCenter(building.Footprint), 0.48f, 0.92f, 0.14f, 0.86f);
        }

        public void CloseAll()
        {
            CloseReveal();
            EndRaid();
            HideActionBar();
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

            // Every building, the Altar and the Gate included, shows its action row first, so each one can be moved.
            _panel.Hide();
            ShowActionBar(building);
        }

        /// <summary>Everything about a building: description, stats now and next, and every action, behind Info.</summary>
        public void ShowBuildingDetails(int buildingId)
        {
            var building = Village.Find(buildingId);
            if (building == null)
            {
                CloseAll();
                return;
            }
            var definition = building.Definition;
            _panel.Show(definition.displayName, ShowContextPanel, panel =>
            {
                panel.Line(definition.description);
                panel.Line($"Level {building.Level} of {definition.maxLevel}");
                StatLines(panel, building);
                FunctionActions(panel, building);
                UpgradeAction(panel, building);
                if (definition.IsWall)
                {
                    var group = Village.ConnectedWalls(building.Id);
                    panel.Action("UpgradeGroupButton", $"Upgrade connected walls ({group.Count})", () => Upgrade(building.Id, true));
                }
                // Buildings are moved, never removed (ADR-051).
                panel.Action("MoveButton", "Move", () => BeginMove(building.Id));
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
                    panel.Line("The Town Hall level sets how many of each building you can own.");
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
                    TrainingActions(panel);
                    break;
                case BuildingFunction.TroopCamp:
                    panel.Line($"All camps: {Army.UsedSpace(TroopData)} of {Army.Capacity(Village)} space used. Train troops at the Training Hall.");
                    break;
                case BuildingFunction.Research:
                    ResearchActions(panel);
                    break;
                case BuildingFunction.Guild:
                    panel.Line("Found or join a guild here, and talk with your guildmates.", 19);
                    panel.Action("OpenGuildButton", "Open the guild", () => OpenShortcut("friends"), IsOnline, Ui.AccentColor);
                    break;
                case BuildingFunction.Arena:
                    panel.Line("Duel other heroes for points and a place in the rankings.", 19);
                    panel.Action("OpenArenaHallButton", "Enter the Arena", () => { CloseAll(); OpenAttack(); OpenArena(); }, IsOnline, Ui.AccentColor);
                    break;
                case BuildingFunction.Trap:
                    var trap = building.Definition.At(building.Level);
                    panel.Line(building.Definition.id == "hero_trap"
                        ? $"Hidden from attackers. Holds the attacking hero for 3 seconds and deals {trap.attack:N0} damage, once."
                        : $"Hidden from attackers. Deals {trap.attack:N0} damage to every troop close by, once.", 19);
                    break;
                case BuildingFunction.Collector:
                    var pending = Village.PendingCollection(building, Now);
                    panel.Action("CollectButton", $"Collect {pending:N0} {building.Definition.produces}", () => Collect(building.Id), pending > 0, Ui.AccentColor);
                    break;
                case BuildingFunction.Storage:
                    panel.Line($"Total gold capacity: {Village.GoldCapacity:N0}");
                    break;
                case BuildingFunction.Defense:
                    panel.Line("Defends this village when raids against players arrive.", 20, Ui.AccentColor);
                    break;
            }
        }

        private void UpgradeAction(PanelView panel, BuildingInstance building)
        {
            if (building.Level >= building.Definition.maxLevel)
            {
                panel.Line("Maximum level reached.");
                return;
            }
            TimedUpgradeLines(panel, building);
            if (building.Upgrading) return;
            var cost = building.Definition.UpgradeCost(building.Level);
            var needsDojo = building.Definition.At(building.Level + 1).requiredHq;
            if (Village.HqLevel < needsDojo) panel.Line($"Next level needs Town Hall level {needsDojo}.", 20, Ui.AccentColor);
            var builderFree = Village.Catalog.UpgradeSeconds(building.Definition, building.Level + 1) == 0 || Village.BusyBuilders < Village.Builders;
            panel.Action("UpgradeButton", $"Upgrade ({cost:N0} gold)", () => Upgrade(building.Id, false), Village.CanAfford(cost, 0) && Village.HqLevel >= needsDojo && builderFree);
        }

        /// <summary>Each stat this building has, as now -> next level. Stats a building lacks stay hidden.</summary>
        private static void StatLines(PanelView panel, BuildingInstance building)
        {
            var definition = building.Definition;
            var now = definition.At(building.Level);
            var next = building.Level < definition.maxLevel ? definition.At(building.Level + 1) : null;

            void Stat(string name, long current, long upcoming, string unit = "")
            {
                if (current == 0 && upcoming == 0) return;
                panel.Line(next == null || upcoming == current
                    ? $"{name}: {current:N0}{unit}"
                    : $"{name}: {current:N0}{unit}  ->  {upcoming:N0}{unit}");
            }

            Stat("HP", now.hp, next?.hp ?? now.hp);
            Stat($"{definition.produces} per hour", now.production, next?.production ?? now.production);
            Stat("Holds", now.storedCap, next?.storedCap ?? now.storedCap);
            Stat("Gold capacity", now.goldCapacity, next?.goldCapacity ?? now.goldCapacity);
            Stat("Attack", now.attack, next?.attack ?? now.attack);
            Stat("Range", now.range, next?.range ?? now.range);
            Stat(definition.function == BuildingFunction.HeroAltar ? "Faster hero revival" : "Hero strength bonus", now.bonusPercent, next?.bonusPercent ?? now.bonusPercent, "%");
            Stat("Holds troops", now.troopCapacity, next?.troopCapacity ?? now.troopCapacity);
            Stat("Might", now.might, next?.might ?? now.might);
        }

        private void RunAndRefresh(VillageError error)
        {
            LastMessage = VillageErrorText.Describe(error);
            ShowContextPanel();
        }

        private void Upgrade(int id, bool group)
        {
            if (RefuseWhenNoServer()) return;
            if (IsOnline)
            {
                VillageRequest($"upgrade|{id}|{group}|{Village.Find(id)?.Level}", key => _server.UpgradeAsync(key, id, group), ShowContextPanel, ShowContextPanel);
                return;
            }
            RunAndRefresh(group ? Village.TryUpgradeWallGroup(id) : Village.TryUpgrade(id));
        }

        private void Collect(int id)
        {
            _guideCollected = true;
            if (RefuseWhenNoServer()) return;
            if (IsOnline)
            {
                VillageRequest($"collect|{id}", key => _server.CollectAsync(key, id), ShowContextPanel, ShowContextPanel);
                return;
            }
            RunAndRefresh(Village.TryCollect(id, Now, out _));
        }

        // ---------- Attack and settings ----------

        private void OpenSettings()
        {
            if (UiState.Screen != VillageScreen.Home && UiState.Screen != VillageScreen.BuildingPanel) return;
            CloseAll();
            _panel.Show("Menu", CloseAll, panel =>
            {
                panel.Line(IsOnline ? "Connected. Your village is saved on the server." : "Not connected to the server.");
                panel.Line(Village.Catalog.BalanceStatus, 18);
                if (IsOnline) panel.Action("NameButton", $"Name: {PlayerName} (tap to change)", ShowNameWindow);
                var high = GameQuality.Current == VisualQuality.High;
                panel.Line("Fewer trees at Low take effect after a restart.", 17);
                panel.Action("SoundButton", GameSounds.Muted ? "Sound: Off (tap for On)" : "Sound: On (tap for Off)", () =>
                {
                    GameSounds.Muted = !GameSounds.Muted;
                    OpenSettings();
                });
                panel.Action("MusicButton", GameMusic.Muted ? "Music: Off (tap for On)" : "Music: On (tap for Off)", () =>
                {
                    GameMusic.Muted = !GameMusic.Muted;
                    OpenSettings();
                });
                panel.Action("QualityButton", high ? "Graphics: High (tap for Low)" : "Graphics: Low (tap for High)", () =>
                {
                    GameQuality.Set(high ? VisualQuality.Low : VisualQuality.High);
                    OpenSettings();
                });
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                panel.Action("ResetButton", "Reset village (development)", ConfirmReset);
#endif
            });
        }

        private void ConfirmReset()
        {
            _panel.Show("Reset village?", CloseAll, panel =>
            {
                panel.Line("This deletes your layout and starts over. It cannot be undone.");
                panel.Action("ConfirmResetButton", "Yes, reset", ResetVillage, true, Ui.AttackColor);
                panel.Action("CancelResetButton", "Keep my village", CloseAll);
            });
        }

        private void ResetVillage()
        {
            if (IsOnline)
            {
                Run(async () =>
                {
                    ApplyServerState(await _server.DevResetVillageAsync());
                    CloseAll();
                }, CloseAll);
                return;
            }
            Village.Restore(StarterVillage.Create(Village.Catalog, Now).ToSave());
            CloseAll();
        }

        // ---------- Build catalog: reachable only through the Builder Hall ----------

        public bool OpenCatalog()
        {
            if (!UiState.TryOpenCatalog()) return false;
            _selectionCells.Clear();
            ShowCatalog();
            return true;
        }

        private static readonly Vector2 CatalogWindow = new Vector2(1180f, 510f);
        // Five cards (the most any tab has) fit one row of the window: 5 x 204 + 4 x 14 = 1076 of the 1092 inside.
        private static readonly Vector2 CatalogCardSize = new Vector2(204f, 318f);

        /// <summary>The build catalog as a framed window over the village (docs/ui/CATALOG_SPEC.md).</summary>
        private void ShowCatalog()
        {
            _panel.ShowWindow("Build", CatalogWindow, CloseAll, content =>
            {
                var tabs = Ui.Rect("Tabs", content);
                tabs.anchorMin = new Vector2(0f, 1f);
                tabs.anchorMax = new Vector2(1f, 1f);
                tabs.pivot = new Vector2(0.5f, 1f);
                tabs.sizeDelta = new Vector2(0f, 58f);
                tabs.anchoredPosition = Vector2.zero;
                var row = tabs.gameObject.AddComponent<HorizontalLayoutGroup>();
                row.spacing = 10f;
                row.childControlWidth = true;
                row.childControlHeight = true;
                row.childForceExpandWidth = true;
                row.childForceExpandHeight = true;
                AddTab(tabs, BuildingCategory.Structures, "Structures", "build");
                AddTab(tabs, BuildingCategory.Defense, "Defense", "train");
                AddTab(tabs, BuildingCategory.Resources, "Resources", "gold");
                AddTab(tabs, BuildingCategory.HeroBases, "Hero Base", "hero");
                AddTab(tabs, BuildingCategory.Walls, "Walls", "walls");
                AddTab(tabs, BuildingCategory.Traps, "Traps", "attack");

                // Unique essentials such as the Dojo always exist, so they would only be noise here.
                var cards = Ui.Rect("Cards", content);
                cards.anchorMin = new Vector2(0f, 0f);
                cards.anchorMax = new Vector2(1f, 1f);
                cards.offsetMin = new Vector2(0f, 0f);
                cards.offsetMax = new Vector2(0f, -74f);
                var grid = cards.gameObject.AddComponent<GridLayoutGroup>();
                grid.cellSize = CatalogCardSize;
                grid.spacing = new Vector2(14f, 14f);
                grid.childAlignment = TextAnchor.UpperCenter;
                var index = 0;
                foreach (var definition in Village.Catalog.InCategory(_catalogCategory))
                    if (definition.removable && !definition.switchOnly)
                        UiMotion.PopIn(CatalogCard(cards, definition).transform, 0.035f * index++, 0.85f, 0.2f);

                // Undo is offline-only for now: online it would be a free refund the server must not give.
                if (!IsOnline && _history.Count > 0)
                {
                    var undo = Medal.Create(content, "CatalogUndoButton", MedalKind.IconOnly, "back", "Undo", UndoFromCatalog, true, 56f);
                    Ui.Anchor((RectTransform)undo.transform, new Vector2(1f, 0f), new Vector2(56f, 56f), new Vector2(-6f, 30f));
                }
            });
        }

        private void AddTab(RectTransform row, BuildingCategory category, string caption, string icon)
        {
            var selected = category == _catalogCategory;
            var tab = Ui.Button(row, "Tab" + category, caption, () =>
            {
                _catalogCategory = category;
                ShowCatalog();
            }, true, selected ? Ui.AccentColor : Ui.ButtonColor, 18);
            var label = tab.GetComponentInChildren<Text>();
            label.horizontalOverflow = HorizontalWrapMode.Overflow;
            Ui.Stretch(label.rectTransform, 46f, 7f, 10f, 3f);
            label.alignment = TextAnchor.MiddleLeft;
            HudArt.Picture(tab.transform, "Icon", "icon_" + icon, new Vector2(0f, 0.5f), new Vector2(30f, 30f), new Vector2(12f, 2f));
        }

        /// <summary>A catalog card: name, the real building on a soft glow, footprint and count chips, and the price.</summary>
        private Button CatalogCard(RectTransform grid, BuildingDefinition definition)
        {
            var count = Village.CountLike(definition);
            var limit = Village.LimitOf(definition);
            var locked = limit == 0;
            var full = !locked && count >= limit;
            var affordable = Village.CanAfford(definition.costGold, definition.costCrystals);
            var available = !locked && !full && affordable;

            var rect = Ui.Rect("Place_" + definition.id, grid);
            var plate = Ui.Framed(rect, HudArt.Sprite("card_s34"));
            plate.pixelsPerUnitMultiplier = 1.15f;
            var button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = plate;
            button.interactable = available;
            var colors = button.colors;
            colors.pressedColor = new Color(0.8f, 0.8f, 0.8f, 1f);
            colors.disabledColor = Color.white;
            button.colors = colors;
            button.onClick.AddListener(() => GameSounds.Play(GameSound.Click));
            button.onClick.AddListener(() => BeginPlacement(definition.id));
            UiMotion.Pressable(button);
            rect.gameObject.AddComponent<DeniedShake>();

            // A recessed picture well, so the building sits in a framed window like a collectible card.
            var well = Ui.Rect("Well", rect);
            Ui.Anchor(well, new Vector2(0.5f, 1f), new Vector2(178f, 168f), new Vector2(0f, -44f));
            Ui.Framed(well, HudArt.Sprite("well_s18"), false).pixelsPerUnitMultiplier = 1.1f;
            var glow = HudArt.Picture(well, "Glow", "glow", new Vector2(0.5f, 0.5f), new Vector2(190f, 150f), Vector2.zero);
            glow.color = new Color(1f, 0.8f, 0.45f, 0.4f);
            var preview = Ui.Rect("Preview", well);
            Ui.Anchor(preview, new Vector2(0.5f, 0.5f), new Vector2(170f, 170f), new Vector2(0f, -4f));
            var picture = preview.gameObject.AddComponent<Image>();
            picture.sprite = BuildingSprites.For(definition.id, 1);
            picture.preserveAspect = true;
            picture.raycastTarget = false;

            var title = HudArt.Label(rect, definition.displayName, HudArt.Display, 18, TextAnchor.MiddleCenter, HudArt.Ivory, HudArt.LacquerDeep);
            Ui.Anchor(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(180f, 32f), new Vector2(0f, -12f));
            title.resizeTextForBestFit = true;
            title.resizeTextMinSize = 13;
            title.resizeTextMaxSize = 18;

            Chip(rect, $"{definition.width} x {definition.depth}", new Vector2(-44f, 74f));
            Chip(rect, $"{count} / {limit}", new Vector2(44f, 74f));

            // The price bar: gold when it can be bought, red numbers when it cannot be paid, a plain note otherwise.
            var price = Ui.Rect("Cost", rect);
            Ui.Anchor(price, new Vector2(0.5f, 0f), new Vector2(182f, 54f), new Vector2(0f, 14f));
            var priceImage = Ui.Framed(price, HudArt.Sprite(available ? "btn_gold_s24" : full || locked ? "btn_charcoal_s24" : "btn_lacquer_s24"), false);
            priceImage.pixelsPerUnitMultiplier = 1.4f;
            string caption;
            Color tint;
            if (full)
            {
                caption = "Maximum built";
                tint = HudArt.IvoryMuted;
            }
            else
            {
                caption = definition.costCrystals > 0 ? $"{definition.costGold:N0} + {definition.costCrystals:N0}" : definition.costGold.ToString("N0");
                if (definition.IsWall) caption += " each";
                tint = available ? new Color32(0x2A, 0x1A, 0x08, 0xFF) : affordable ? HudArt.IvoryMuted : HudArt.CrimsonLight;
            }
            if (!full) HudArt.Picture(price, "Coin", "icon_gold", new Vector2(0f, 0.5f), new Vector2(30f, 30f), new Vector2(14f, 3f));
            var cost = HudArt.Label(price, caption, HudArt.Numbers, 19, full ? TextAnchor.MiddleCenter : TextAnchor.MiddleLeft, tint, available ? new Color(1f, 0.92f, 0.7f, 0.5f) : HudArt.LacquerDeep);
            Ui.Stretch(cost.rectTransform, full ? 10f : 52f, 9f, 10f, 3f);

            if (locked)
            {
                var shade = Ui.Rect("Locked", rect);
                Ui.Stretch(shade, 14f, 14f, 14f, 14f);
                Ui.Background(shade, new Color(0.08f, 0.08f, 0.1f, 0.6f), false);
                HudArt.Picture(shade, "Lock", "icon_lock", new Vector2(0.5f, 0.5f), new Vector2(60f, 60f), new Vector2(0f, 40f));
                var needs = Village.HqLevel + 1;
                while (needs < 15 && definition.MaxCountAt(needs) == 0) needs++;
                var note = HudArt.Label(shade, $"TH {needs}", HudArt.Numbers, 18, TextAnchor.MiddleCenter, HudArt.Ivory, HudArt.LacquerDeep);
                Ui.Anchor(note.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(180f, 26f), new Vector2(0f, -6f));
            }
            return button;
        }

        private static void Chip(RectTransform card, string text, Vector2 offset)
        {
            var chip = Ui.Rect("Chip", card);
            Ui.Anchor(chip, new Vector2(0.5f, 0f), new Vector2(78f, 24f), offset);
            var image = chip.gameObject.AddComponent<Image>();
            image.sprite = HudArt.Sprite("track_s8");
            image.type = Image.Type.Sliced;
            image.raycastTarget = false;
            var label = HudArt.Label(chip, text, HudArt.Numbers, 14, TextAnchor.MiddleCenter, HudArt.Ivory, HudArt.LacquerDeep);
            Ui.Stretch(label.rectTransform);
        }

        private void UndoFromCatalog()
        {
            _history.Undo(Village);
            ShowCatalog();
        }

        // ---------- Placement and moving ----------

        public bool BeginPlacement(string definitionId)
        {
            if (RefuseWhenNoServer()) return false;
            var definition = Village.Catalog.Get(definitionId);
            if (!UiState.TryBeginPlacement(definition.IsWall)) return false;
            _camera.StopMotion();
            _panel.Hide();

            _placingDefinition = definition;
            _ghostRotation = Rotation.R0;
            _toolbar = new PlacementToolbar(_uiRoot);
            if (definition.IsWall)
            {
                _hasWallRun = false;
                _confirmButton = _toolbar.Add("ConfirmButton", "Confirm", () => ConfirmWallRun(), Ui.AccentColor);
                _toolbar.Add("ClearButton", "Clear", ClearWallRun);
                if (!IsOnline) _undoButton = _toolbar.Add("UndoButton", "Undo", UndoWall);
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
            if (RefuseWhenNoServer()) return VillageError.None;

            if (IsOnline)
            {
                var definitionId = _placingDefinition.id;
                var origin = _ghostOrigin;
                var rotation = (int)_ghostRotation;
                VillageRequest($"place|{definitionId}|{origin.X}|{origin.Z}|{rotation}",
                    key => _server.PlaceAsync(key, definitionId, origin.X, origin.Z, rotation), ExitPlacement, ShowFailureOnToolbar);
                return VillageError.None;
            }

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
            if (RefuseWhenNoServer()) return false;
            var building = Village.Find(id);
            if (building == null || !UiState.TryBeginMove()) return false;
            _camera.StopMotion();
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
            if (RefuseWhenNoServer()) return VillageError.None;
            if (IsOnline)
            {
                var id = _movingId;
                var origin = _ghostOrigin;
                var rotation = (int)_ghostRotation;
                VillageRequest($"move|{id}|{origin.X}|{origin.Z}|{rotation}",
                    key => _server.MoveAsync(key, id, origin.X, origin.Z, rotation), EndMoveFlow, ShowFailureOnToolbar);
                return VillageError.None;
            }

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
                if (_undoButton != null) _undoButton.interactable = _history.Count > 0;
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
            if (_undoButton != null) _undoButton.interactable = _history.Count > 0;
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
            if (RefuseWhenNoServer()) return VillageError.None;

            if (IsOnline)
            {
                var start = _wallStart;
                var end = _wallEnd;
                VillageRequest($"walls|{start}|{end}",
                    key => _server.PlaceWallsAsync(key, start.X, start.Z, end.X, end.Z), ClearWallRun, ShowFailureOnToolbar);
                return VillageError.None;
            }

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
