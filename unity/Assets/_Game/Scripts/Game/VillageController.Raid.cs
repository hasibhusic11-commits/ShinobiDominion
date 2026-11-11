using System.Collections.Generic;
using System.Linq;
using ShinobiDominion.Core;
using UnityEngine;
using UnityEngine.UI;
using Text = ShinobiDominion.Game.GameText;

namespace ShinobiDominion.Game
{
    public enum RaidPhase
    {
        Scouting,
        Prepare,
        Battle,
        Resolved
    }

    /// <summary>
    /// The attack flow: choose a target, fight it live by dropping cards (ADR-050), see the result. Online, the server
    /// fights the same drops again and its result is the one paid. Offline, in development only, the same rules run
    /// here as a practice battle that pays nothing.
    /// </summary>
    public sealed partial class VillageController
    {
        private sealed class RaidRun
        {
            public RaidPhase Phase = RaidPhase.Prepare;
            public RaidSession Session;
            public BattleStage Stage;
            public readonly List<RaidPlacement> Placements = new List<RaidPlacement>();
            public readonly List<RaidTroopStack> Stacks = new List<RaidTroopStack>();
            public string SelectedTroop;
            /// <summary>The hero card chosen to land next; null lands them in order.</summary>
            public string SelectedHero;
            /// <summary>The spells cast so far, in order (ADR-052); sent with the drops.</summary>
            public readonly List<RaidSpellCast> Casts = new List<RaidSpellCast>();
            /// <summary>The spell picked and waiting for a tap on the map.</summary>
            public string SelectedSpell;
            public GameObject DeployScreen;
            public GameObject CardRow;
            public RaidOutcome Outcome;
            public GameObject StatusBar;
            public GameObject Hud;
            public Text StatusText;
            /// <summary>The second the clock last showed; -1 before it has shown any.</summary>
            public int ClockSecond = -1;
            /// <summary>A live raid's own battle (ADR-050); null for a fight the server already fought.</summary>
            public BattleSim Live;
            /// <summary>The live battle is over: it can no longer change and is being sent or shown.</summary>
            public bool Ended;
        }

        private static readonly Vector3 MapCenter = new Vector3(25f, 0f, 25f);
        private const float RaidCameraSize = 24f;
        /// <summary>Open ground kept in view around the enemy base, in cells: room to drop an army.</summary>
        private const int RaidDropMargin = 6;
        /// <summary>The base and its drop margin fill this share of the screen when a raid opens.</summary>
        private const float RaidFill = 0.94f;
        /// <summary>
        /// How far below the middle of the screen the base sits when a raid opens, as a share of the screen height.
        /// The clear band between the clock and the cards is centred that far below the middle.
        /// </summary>
        private const float RaidLower = 0.03f;
        private const float BattleCameraSize = 11f;
        private const float HomeCameraSize = 8.5f;

        private const int TroopsPerTap = 5;

        private RaidCatalog _raidCatalog;
        private TroopCatalog _troopCatalog;

        public Army Army { get; } = new Army();
        private RaidRun _raid;
        private List<RaidSummary> _raidList;
        private List<DefenseRecord> _defenseLog;
        private CellHighlight _raidCells;
        private HeroPortraits _portraits;

        public RaidPhase? CurrentRaidPhase => _raid?.Phase;
        public RaidSession CurrentRaidSession => _raid?.Session;
        public IReadOnlyList<RaidPlacement> RaidPlacements => _raid?.Placements;
        public RaidOutcome LastRaidOutcome { get; private set; }

        private RaidCatalog RaidData
        {
            get
            {
                if (_raidCatalog != null) return _raidCatalog;
                var config = Resources.Load<TextAsset>("Config/raids");
                if (config == null) throw new System.InvalidOperationException("Resources/Config/raids.json is missing.");
                _raidCatalog = RaidCatalog.FromJson(config.text, Village.Catalog);
                return _raidCatalog;
            }
        }

        public TroopCatalog TroopData
        {
            get
            {
                if (_troopCatalog != null) return _troopCatalog;
                var config = Resources.Load<TextAsset>("Config/troops");
                if (config == null) throw new System.InvalidOperationException("Resources/Config/troops.json is missing.");
                _troopCatalog = TroopCatalog.FromJson(config.text);
                return _troopCatalog;
            }
        }

        // ---------- Training ----------

        private void TrainingActions(PanelView panel)
        {
            var hall = Army.HallLevel(Village);
            panel.Line($"Army: {Army.UsedSpace(TroopData)} of {Army.Capacity(Village)} camp space used.");
            if (Army.Capacity(Village) == 0) panel.Line("Build a Troop Camp to house them.", 20, Ui.AccentColor);
            foreach (var troop in TroopData.All)
            {
                var name = $"Train{troop.id}Button";
                if (hall < troop.requiredHall)
                {
                    panel.Line($"{troop.displayName}: needs a level {troop.requiredHall} Training Hall", 19, Ui.MutedText);
                    continue;
                }
                var error = Army.CheckTrain(Village, TroopData, troop.id, 1);
                panel.Action(name, $"{troop.displayName} ({Army.CountOf(troop.id)})   {troop.goldCost:N0} gold, {troop.manaCost:N0} Mana, {troop.space} space", () => TrainTroop(troop.id, 1), error == VillageError.None, null, 18);
            }
        }

        public void TrainTroop(string troopId, int count)
        {
            if (RefuseWhenNoServer()) return;
            if (IsOnline)
            {
                VillageRequest($"train|{troopId}|{count}|{Army.CountOf(troopId)}", key => _server.TrainTroopsAsync(key, troopId, count), ShowContextPanel, ShowContextPanel);
                return;
            }
            RunAndRefresh(Army.TryTrain(Village, TroopData, troopId, count));
        }

        // ---------- Choosing a target ----------

        public void OpenAttack()
        {
            if (!UiState.TryOpenAttack()) return;
            _selectionCells.Clear();
            _raidList = null;
            LastMessage = null;
            ShowRaidList();
            // Fetched as soon as no other request is running, so a tap still in flight can't swallow it.
            _raidListWanted = IsOnline;
            FetchRaidListWhenFree();
        }

        private void ShowRaidList() => ShowBattleHub();

        private bool _raidListWanted;

        /// <summary>Called on opening ATTACK and every frame: loads the raid list once nothing else is in flight.</summary>
        private void FetchRaidListWhenFree()
        {
            if (!_raidListWanted || _busy) return;
            _raidListWanted = false;
            if (!IsOnline || UiState.Screen != VillageScreen.AttackFlow || _raid != null) return;
            Fetch(async () =>
            {
                var list = await _server.RaidListAsync();
                _defenseLog = await _server.DefenseLogAsync();
                _raidList = list;
                if (UiState.Screen == VillageScreen.AttackFlow && _raid == null) ShowRaidList();
            }, () =>
            {
                _raidList = new List<RaidSummary>();
                if (UiState.Screen == VillageScreen.AttackFlow && _raid == null) ShowRaidList();
            });
        }

        public void ScoutRaid(string raidId)
        {
            if (_raid != null || RefuseWhenNoServer()) return;
            if (IsOnline)
            {
                var key = KeyFor($"raid-start|{raidId}");
                Run(async () =>
                {
                    var session = await _server.RaidStartAsync(key, raidId);
                    ClearKey();
                    LastMessage = null;
                    EnterPrepare(session);
                }, ShowRaidList);
                return;
            }
            EnterPrepare(LocalSession(raidId));
        }

        /// <summary>Finds a real player's village. The server chooses it and copies it for this raid.</summary>
        public void FindRival()
        {
            if (_raid != null || !IsOnline) return;
            var key = KeyFor($"rival-start|{System.DateTime.UtcNow.Ticks}");
            Run(async () =>
            {
                var session = await _server.RivalStartAsync(key);
                ClearKey();
                LastMessage = null;
                EnterPrepare(session);
            }, ShowRaidList);
        }

        /// <summary>A practice raid built from this device's own data, for offline development.</summary>
        private RaidSession LocalSession(string raidId)
        {
            var raid = RaidData.Find(raidId);
            var session = new RaidSession
            {
                attackId = "practice",
                raid = new RaidSummary { id = raid.id, displayName = raid.displayName, description = raid.description, goldPerStar = 0 },
                buildings = raid.Expand(Village.Catalog),
                defenders = raid.ExpandDefenders(Village.Catalog, Roster.Catalog)
            };
            foreach (var building in Village.Buildings.OrderBy(b => b.Id))
            {
                if (building.AssignedHeroInstanceId == null) continue;
                var hero = Roster.Find(building.AssignedHeroInstanceId);
                if (hero == null) continue;
                var stats = Roster.StatsOf(hero);
                var role = Roster.ArchetypeOf(hero).role.ToString();
                session.team.Add(new RaidTeamMember
                {
                    instanceId = hero.InstanceId,
                    archetypeId = hero.ArchetypeId,
                    evolutionIndex = hero.EvolutionIndex,
                    level = hero.Level,
                    role = role,
                    hp = stats.Hp,
                    attack = stats.Attack,
                    defense = stats.Defense,
                    speed = BattleSim.SpeedOf(role)
                });
            }
            // Practice has no server to say which spells the Relic Hall has opened.
            session.spells = SpellData.Available(Village);
            return session;
        }

        // ---------- The live battle (ADR-050) ----------

        /// <summary>
        /// The raid starts as a battle at once, like the genre's: the enemy base, an empty field and the clock running.
        /// Cards are dropped while it runs. This device steps its own copy so every drop shows straight away; the server
        /// fights the same drops again when it ends, and its result is the one that counts.
        /// </summary>
        /// <summary>A building further than this from the rest of the base, in cells, is left out of the opening view.</summary>
        private const int RaidOutlierGap = 4;

        /// <summary>
        /// Opens the battle on the heart of the enemy base: the Town Hall and everything that hangs together with
        /// it, plus open ground to drop on. A lone mine in a far corner is left for the player to find by
        /// panning; counting it would push the view back out to the whole field.
        /// </summary>
        private void FrameBase(RaidSession session)
        {
            var rest = session.buildings == null ? new List<BattleBuildingInput>() : session.buildings.Where(b => !b.isTrap).ToList();
            if (rest.Count == 0)
            {
                _camera.StopMotion();
                _camera.Initialize(MapCenter, RaidCameraSize);
                return;
            }
            var heart = rest.FirstOrDefault(b => b.definitionId == "dojo_hq") ?? rest.OrderByDescending(b => b.width * b.depth).First();
            rest.Remove(heart);
            int minX = heart.x, minZ = heart.z, maxX = heart.x + heart.width, maxZ = heart.z + heart.depth;
            for (var grew = true; grew;)
            {
                grew = false;
                for (var i = rest.Count - 1; i >= 0; i--)
                {
                    var b = rest[i];
                    var gap = Mathf.Max(Mathf.Max(minX - (b.x + b.width), b.x - maxX), Mathf.Max(minZ - (b.z + b.depth), b.z - maxZ));
                    if (gap > RaidOutlierGap) continue;
                    minX = Mathf.Min(minX, b.x);
                    minZ = Mathf.Min(minZ, b.z);
                    maxX = Mathf.Max(maxX, b.x + b.width);
                    maxZ = Mathf.Max(maxZ, b.z + b.depth);
                    rest.RemoveAt(i);
                    grew = true;
                }
            }
            var min = new Vector3(Mathf.Max(0, minX - RaidDropMargin), 0f, Mathf.Max(0, minZ - RaidDropMargin));
            var max = new Vector3(Mathf.Min(VillageGrid.Size, maxX + RaidDropMargin), 0f, Mathf.Min(VillageGrid.Size, maxZ + RaidDropMargin));
            _camera.Frame(min * GridSpace.CellSize, max * GridSpace.CellSize, RaidFill, BattleCameraSize * 0.75f, RaidCameraSize, RaidLower);
        }

        private void EnterPrepare(RaidSession session)
        {
            var stageObject = new GameObject("BattleStage");
            stageObject.transform.SetParent(_worldRoot, false);
            var stage = stageObject.AddComponent<BattleStage>();
            stage.Initialize(session, Village.Catalog, TroopData, _camera.Camera, HeroLookFor, DefenderLook);
            stage.Finished += LiveBattleOver;
            _raid = new RaidRun { Session = session, Stage = stage, Phase = RaidPhase.Battle, Live = BattleSim.Start(session.buildings, session.ToDefenderInputs()) };
            LastRaidOutcome = null;
            LastLiveResult = null;
            _panel.Hide();
            _view.SetVisible(false);
            if (_life != null) _life.gameObject.SetActive(false);
            FrameBase(session);

            var blocked = new List<KeyValuePair<GridCoord, bool>>();
            for (var z = 0; z < VillageGrid.Size; z++)
            for (var x = 0; x < VillageGrid.Size; x++)
            {
                if (BattleSim.CanDeployAt(session.buildings, x, z)) continue;
                var inside = session.buildings.Any(b => x >= b.x && x < b.x + b.width && z >= b.z && z < b.z + b.depth);
                if (!inside) blocked.Add(new KeyValuePair<GridCoord, bool>(new GridCoord(x, z), false));
            }
            _raidCells = _raidCells ?? new CellHighlight(_worldRoot, "RaidBlockedCells", new Color(0f, 0f, 0f, 0f), new Color(0.85f, 0.2f, 0.15f, 0.2f), 0.03f);
            _raidCells.Show(blocked, false);

            stage.BeginLive(_raid.Live);
            CreateBattleBar(true);
            CreateBattleHud();
            ShowDeployScreen();
        }

        private HeroLook DefenderLook(RaidDefender defender) =>
            Roster.Catalog.TryGet(defender.archetypeId, out var archetype) ? LookOf(archetype, defender.evolutionIndex) : null;

        private void ShowPreparePanel() => ShowDeployScreen();

        /// <summary>A live raid whose clock is still running, so cards can still drop.</summary>
        private bool LiveRunning => _raid != null && _raid.Live != null && !_raid.Ended && !_raid.Live.Done && _raid.Phase == RaidPhase.Battle;

        /// <summary>The battle as this device fought it, kept so the server's result can be checked against it.</summary>
        public BattleResult LastLiveResult { get; private set; }

        /// <summary>The live battle's clock, in ticks; -1 when no live battle is running.</summary>
        public int LiveTick => _raid?.Live?.Tick ?? -1;

        private int TroopsLeft(string troopId) =>
            Army.CountOf(troopId) - _raid.Stacks.Where(s => s.troopId == troopId).Sum(s => s.count);

        private bool AnythingDropped => _raid.Placements.Count + _raid.Stacks.Count > 0;

        private bool NothingLeftToDrop =>
            _raid.Session.team.All(m => _raid.Placements.Any(p => p.instanceId == m.instanceId)) && TroopData.All.All(t => TroopsLeft(t.id) <= 0);

        /// <summary>Steps through heroes and then each troop the player still has.</summary>
        public void CycleTroop()
        {
            if (!LiveRunning) return;
            var choices = new List<string> { null };
            choices.AddRange(TroopData.All.Where(t => TroopsLeft(t.id) > 0).Select(t => t.id));
            var at = choices.IndexOf(_raid.SelectedTroop);
            _raid.SelectedTroop = choices[(at + 1) % choices.Count];
            ShowPreparePanel();
        }

        /// <summary>Lands a group of troops now. They are the player's to lose once dropped: there is no picking back up.</summary>
        private void DropStack(string troopId, int x, int z, int count)
        {
            var stack = new RaidTroopStack { troopId = troopId, x = x, z = z, count = count, tick = _raid.Live.Tick };
            _raid.Stacks.Add(stack);
            foreach (var unit in _raid.Session.ToUnits(new RaidPlacement[0], new[] { stack }, TroopData, Research)) _raid.Live.Deploy(unit);
        }

        private void DropHero(RaidTeamMember member, int x, int z)
        {
            var placement = new RaidPlacement { instanceId = member.instanceId, x = x, z = z, tick = _raid.Live.Tick };
            _raid.Placements.Add(placement);
            _raid.Live.Deploy(_raid.Session.ToHeroes(new[] { placement })[0]);
        }

        /// <summary>Tapping open ground drops the chosen card there, now: a hero, or up to five of the chosen troop.</summary>
        public void TapDeployCell(GridCoord cell)
        {
            if (!LiveRunning) return;
            if (_raid.SelectedSpell != null)
            {
                // A spell may land anywhere on the map, on the enemy too.
                if (CastSpellAt(_raid.SelectedSpell, cell)) _raid.SelectedSpell = null;
                ShowPreparePanel();
                return;
            }
            if (!VillageGrid.InBounds(cell.X, cell.Z) || !BattleSim.CanDeployAt(_raid.Session.buildings, cell.X, cell.Z))
            {
                LastMessage = "Too close to the enemy: drop on open ground.";
                ShowPreparePanel();
                return;
            }
            LastMessage = null;
            if (_raid.SelectedTroop != null)
            {
                var ready = TroopsLeft(_raid.SelectedTroop);
                if (ready > 0) DropStack(_raid.SelectedTroop, cell.X, cell.Z, Mathf.Min(ready, TroopsPerTap));
                if (TroopsLeft(_raid.SelectedTroop) <= 0) _raid.SelectedTroop = null;
                ShowPreparePanel();
                return;
            }
            // The chosen hero card, or the next hero in order. Two heroes never land on one cell at the same moment.
            var next = _raid.Session.team.FirstOrDefault(m => m.instanceId == _raid.SelectedHero && _raid.Placements.All(p => p.instanceId != m.instanceId)) ?? NextHeroToLand();
            if (next == null || _raid.Placements.Any(p => p.x == cell.X && p.z == cell.Z && p.tick == _raid.Live.Tick)) return;
            DropHero(next, cell.X, cell.Z);
            _raid.SelectedHero = null;
            ShowPreparePanel();
        }

        /// <summary>Auto: drops every card still in hand, now, along the near edges of the map.</summary>
        public void AutoPlaceHeroes()
        {
            if (!LiveRunning) return;
            // Heroes line up along the side nearest the camera, spreading out from the middle.
            var line = 8;
            foreach (var member in _raid.Session.team)
            {
                if (_raid.Placements.Any(p => p.instanceId == member.instanceId)) continue;
                var placed = false;
                for (var z = line; z >= 0 && !placed; z--)
                for (var offset = 0; offset < VillageGrid.Size && !placed; offset++)
                {
                    var x = 25 + (offset % 2 == 0 ? offset / 2 : -(offset + 1) / 2);
                    if (!VillageGrid.InBounds(x, z) || !BattleSim.CanDeployAt(_raid.Session.buildings, x, z)) continue;
                    if (_raid.Placements.Any(p => p.x == x && p.z == z)) continue;
                    DropHero(member, x, z);
                    placed = true;
                }
            }

            // Troops land in groups along the two sides facing the camera, well spread out.
            var spot = 0;
            foreach (var troop in TroopData.All)
            {
                while (TroopsLeft(troop.id) > 0)
                {
                    var side = spot % 2 == 0;
                    var along = 8 + (spot / 2 * 5) % 36;
                    var x = side ? along : 11;
                    var z = side ? 11 : along;
                    spot++;
                    if (!BattleSim.CanDeployAt(_raid.Session.buildings, x, z)) { x = side ? along : 4; z = side ? 4 : along; }
                    if (!BattleSim.CanDeployAt(_raid.Session.buildings, x, z)) continue;
                    DropStack(troop.id, x, z, Mathf.Min(TroopsLeft(troop.id), TroopsPerTap));
                }
            }
            _raid.SelectedTroop = null;
            ShowPreparePanel();
        }

        /// <summary>Leaves before anything is dropped, at no cost. Once a card has fallen, leaving ends the battle instead.</summary>
        public void RetreatRaid()
        {
            if (_raid != null && _raid.Live != null && !_raid.Ended && AnythingDropped)
            {
                EndLiveBattle();
                return;
            }
            EndRaid();
            CloseAll();
        }

        // ---------- The battle ----------

        /// <summary>A Siege, Arena or Trial fight, already fought by the server: its log plays back.</summary>
        private void BeginPlayback(RaidOutcome outcome)
        {
            _raid.Outcome = outcome;
            LastRaidOutcome = outcome;
            _raid.Phase = RaidPhase.Battle;
            _panel.Hide();
            _raidCells?.Clear();
            _camera.StopMotion();
            _camera.Initialize(MapCenter, BattleCameraSize);
            _raid.Stage.Play(outcome.Result, _raid.Placements, _raid.Stacks);
            CreateBattleBar(false);
            CreateBattleHud();
        }

        /// <summary>Battle playback speed, kept between battles. It only changes how fast a fought log plays.</summary>
        private static float _battleSpeed = 1f;

        private static string SpeedCaption() => $"x{_battleSpeed:0}";

        /// <summary>End Battle: a live raid stops now and is sent (or left, if nothing was dropped); a fought one skips to its result.</summary>
        public void SkipBattle()
        {
            if (_raid == null || _raid.Phase != RaidPhase.Battle) return;
            if (_raid.Live == null)
            {
                _raid.Stage.Skip();
                return;
            }
            EndLiveBattle();
        }

        private void EndLiveBattle()
        {
            if (_raid == null || _raid.Live == null || _raid.Ended) return;
            if (!AnythingDropped)
            {
                EndRaid();
                CloseAll();
                return;
            }
            var live = _raid.Live;
            // Cards dropped and spells cast this tick land before the end, so each is inside the battle.
            if (!live.Done && (live.UnitsPending > 0 || live.SpellsPending > 0 || live.Tick == 0)) live.Step();
            live.Finish();
            LiveBattleOver();
        }

        /// <summary>
        /// The live battle is over: every building down, time up, or ended by the player. Practice battles are scored
        /// here; online, the drops and the end tick go to the server, which fights them again and pays its result.
        /// </summary>
        private void LiveBattleOver()
        {
            var raid = _raid;
            if (raid == null || raid.Live == null || raid.Ended) return;
            raid.Ended = true;
            var local = raid.Live.Finish();
            LastLiveResult = local;
            raid.Stage.CatchUp();
            if (raid.Placements.Count + raid.Stacks.Count == 0)
            {
                // Time ran out with nothing dropped: nothing was fought.
                EndRaid();
                CloseAll();
                return;
            }
            _raidCells?.Clear();
            ClearDeployScreen();
            if (!IsOnline || raid.Session.attackId == "practice")
            {
                foreach (var stack in raid.Stacks) Army.Remove(stack.troopId, stack.count);
                ShowOutcome(new RaidOutcome { Result = local });
                return;
            }
            SendLiveBattle(raid);
        }

        private void SendLiveBattle(RaidRun raid)
        {
            var placements = raid.Placements.ToList();
            var stacks = raid.Stacks.ToList();
            var casts = raid.Casts.ToList();
            var endTick = raid.Live.Tick;
            var signature = string.Join(",", placements.Select(p => $"{p.instanceId}:{p.x}:{p.z}:{p.tick}")) + "|" + string.Join(",", stacks.Select(s => $"{s.troopId}:{s.x}:{s.z}:{s.count}:{s.tick}")) + "|" + endTick
                + "|" + string.Join(",", casts.Select(c => $"{c.spellId}:{c.x}:{c.z}:{c.tick}"));
            var key = KeyFor($"raid-deploy|{raid.Session.attackId}|{signature}");
            Run(async () =>
            {
                var outcome = await _server.RaidDeployAsync(key, raid.Session.attackId, placements, stacks, endTick, casts);
                ClearKey();
                LastMessage = null;
                // The server's fight is the one that counts. The two can only differ if this device was tampered with.
                if (outcome.Result.checksum != LastLiveResult?.checksum)
                    Debug.LogWarning($"The server's battle ({outcome.Result.checksum}) differs from this device's ({LastLiveResult?.checksum}); showing the server's.");
                if (_raid == raid) ShowOutcome(outcome);
            }, () =>
            {
                if (_raid == raid) ShowSendFailed(raid);
            });
        }

        /// <summary>The battle is over but the server has not confirmed it: try again with the same key, or leave.</summary>
        private void ShowSendFailed(RaidRun raid)
        {
            _panel.Show("Battle not confirmed", FinishRaid, panel =>
            {
                panel.Line(LastMessage ?? "The server could not be reached.", 22, Ui.AccentColor);
                panel.Line("Your battle is kept. Try again to get its result.");
                panel.Action("RetrySendButton", "Try again", () =>
                {
                    _panel.Hide();
                    SendLiveBattle(raid);
                }, true, Ui.AccentColor);
                panel.Action("ReturnHomeButton", "Return to village", FinishRaid, true);
            });
        }

        private void ShowOutcome(RaidOutcome outcome)
        {
            _raid.Outcome = outcome;
            LastRaidOutcome = outcome;
            ShowRaidResult();
        }

        private void UpdateRaid()
        {
            if (_raid?.StatusText == null || _raid.Phase != RaidPhase.Battle) return;
            UpdateBattleClock();
            // A fight played back is followed by the camera. A live raid is not: the player is aiming drops and
            // spells at the base, so the view stays where they left it.
            var center = _raid.Live == null ? _raid.Stage.ActionCenter : null;
            if (center.HasValue) _camera.Drift(center.Value);
            UpdateBattleHud();
            UpdateSpellAim();
            // Everyone dropped has fallen, no card is left and no spell is still falling: nothing more can happen, so
            // the battle ends. A spell not cast by then is not waited for.
            if (LiveRunning && _raid.Live.Tick > 0 && AnythingDropped && _raid.Live.UnitsPending == 0 && _raid.Live.UnitsStanding == 0 && NothingLeftToDrop
                && _raid.Live.SpellsPending == 0 && _raid.Live.SpellsActive == 0)
                EndLiveBattle();
        }

        // ---------- The result ----------

        private void ShowRaidResult()
        {
            if (_raid == null) return;
            _raid.Phase = RaidPhase.Resolved;
            ClearAttackScreen();
            if (_modeFight != null)
            {
                ShowModeResult();
                return;
            }
            var outcome = _raid.Outcome;
            var result = outcome.Result;
            if (outcome.State != null) ApplyServerState(outcome.State);
            var title = result.stars > 0 ? "Victory" : "Defeat";
            GameSounds.Play(result.stars > 0 ? GameSound.Victory : GameSound.Defeat);
            _panel.Show(title, FinishRaid, panel =>
            {
                DrawResultStars(panel, result.stars);
                panel.Line($"{result.stars} of 3 stars", 30, Ui.AccentColor);
                panel.Line($"Damage dealt: {result.damagePercent}%");
                panel.Line(result.dojoDestroyed ? "The enemy Town Hall fell." : "The enemy Town Hall still stands.");
                var landed = _raid.Placements.Count + _raid.Stacks.Sum(s => s.count);
                panel.Line($"Still standing: {result.heroesAlive} of {landed}");
                var guards = _raid.Session.defenders.Count;
                if (guards > 0) panel.Line($"Enemy heroes beaten: {guards - result.defendersAlive} of {guards}");
                panel.Line($"Time: {result.endTick / BattleSim.TicksPerSecond} seconds");
                if (_raid.Casts.Count > 0) panel.Line($"Spells cast: {_raid.Casts.Count}");
                if (_raid.Session.attackId == "practice") panel.Line("Practice battle: no reward.", 20, Ui.AccentColor);
                else if (outcome.GoldEarned + outcome.ManaEarned > 0) panel.Line(outcome.ManaEarned > 0 ? $"Earned {outcome.GoldEarned:N0} gold and {outcome.ManaEarned:N0} Mana" : $"Earned {outcome.GoldEarned:N0} gold");
                else if (_raid.Session.raid.id == "rival") panel.Line(result.stars == 0 ? "No gold: earn a star to get paid." : "No gold: this rival already paid today, you reached today's limit, or your vault is full.");
                else panel.Line("No gold: only new stars pay, or your vault is full.");
                DrawRewardLines(panel, outcome);
                panel.Action("ReturnHomeButton", "Return to village", FinishRaid, true, Ui.AccentColor);
            });
        }

        public void FinishRaid()
        {
            EndRaid();
            CloseAll();
        }

        private void EndRaid()
        {
            if (_raid == null) return;
            ClearAttackScreen();
            _raid.Stage.Finished -= ShowRaidResult;
            _raid.Stage.Finished -= LiveBattleOver;
            _modeFight = null;
            Destroy(_raid.Stage.gameObject);
            _raid = null;
            _raidCells?.Clear();
            _view.SetVisible(true);
            if (_life != null) _life.gameObject.SetActive(true);
            _camera.StopMotion();
            _camera.Initialize(MapCenter, HomeCameraSize);
        }
    }
}
