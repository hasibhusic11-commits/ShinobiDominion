using System.Collections.Generic;
using System.Linq;
using ShinobiDominion.Core;
using UnityEngine;
using UnityEngine.UI;

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
    /// The attack flow: choose a target, place heroes, watch the battle, see the result. Online, the
    /// server fights the battle and this only plays back its log. Offline, in development only, the
    /// same rules run here as a practice battle that pays nothing.
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
            public RaidOutcome Outcome;
            public GameObject StatusBar;
            public GameObject Hud;
            public Text StatusText;
        }

        private static readonly Vector3 MapCenter = new Vector3(25f, 0f, 25f);
        private const float RaidCameraSize = 24f;
        private const float BattleCameraSize = 11f;
        private const float HomeCameraSize = 7f;

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
                panel.Action(name, $"{troop.displayName} ({Army.CountOf(troop.id)})   {troop.goldCost:N0} gold, {troop.space} space", () => TrainTroop(troop.id, 1), error == VillageError.None, null, 18);
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
            if (!IsOnline) return;
            Run(async () =>
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

        private void ShowRaidList()
        {
            var team = Village.Buildings.Count(b => b.AssignedHeroInstanceId != null);
            _panel.Show("Attack", CloseAll, panel =>
            {
                if (!string.IsNullOrEmpty(LastMessage)) panel.Line(LastMessage, 20, Ui.AccentColor);
                panel.Line(team == 0 ? "No hero is on your Hero Base. Put one there to raid." : "Your hero leads the raid. Only one hero is used at a time.");
                if (!IsOnline) panel.Line(AllowOfflinePlay ? "Practice battle: not connected, so it pays nothing." : "Raids need the server.", 20, Ui.AccentColor);
                if (IsOnline && _raidList == null)
                {
                    panel.Line("Checking your raids...");
                    return;
                }
                if (IsOnline)
                {
                    panel.Line("Rival villages", 24);
                    panel.Line("Raid another player's village as they left it. They lose nothing, and each rival pays once a day.", 19);
                    panel.Action("FindRivalButton", "Find a rival", FindRival, team > 0, Ui.AccentColor);
                    if (_defenseLog != null && _defenseLog.Count > 0)
                    {
                        panel.Line("Raids on your village", 22);
                        foreach (var record in _defenseLog.Take(3))
                            panel.Line($"{record.stars} of 3 stars, {record.damagePercent}% damage. {record.defendersAlive} of {record.defendersTotal} guards still standing.", 19);
                    }
                }
                panel.Line("Dungeons", 24);
                panel.Line("Fight through fortified camps for gold, crystals, hero XP and evolution materials.", 19);
                panel.Action("OpenDungeonsButton", "Open the dungeon map", OpenCampaign, IsOnline || AllowOfflinePlay, Ui.AccentColor);
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
            return session;
        }

        // ---------- Placing heroes ----------

        private void EnterPrepare(RaidSession session)
        {
            var stageObject = new GameObject("BattleStage");
            stageObject.transform.SetParent(_worldRoot, false);
            var stage = stageObject.AddComponent<BattleStage>();
            stage.Initialize(session, Village.Catalog, TroopData, _camera.Camera, HeroLookFor, DefenderLook);
            stage.Finished += ShowRaidResult;
            _raid = new RaidRun { Session = session, Stage = stage };
            _view.SetVisible(false);
            if (_life != null) _life.gameObject.SetActive(false);
            _camera.StopMotion();
            _camera.Initialize(MapCenter, RaidCameraSize);

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
            ShowPreparePanel();
        }

        private HeroLook DefenderLook(RaidDefender defender) =>
            Roster.Catalog.TryGet(defender.archetypeId, out var archetype) ? LookOf(archetype, defender.evolutionIndex) : null;

        private void ShowPreparePanel()
        {
            if (_raid == null) return;
            _panel.Show(_raid.Session.raid.displayName, RetreatRaid, panel =>
            {
                if (!string.IsNullOrEmpty(LastMessage)) panel.Line(LastMessage, 20, Ui.AccentColor);
                panel.Line("Tap the map to land. Shaded cells are too close to the enemy. Troops that land are used up.", 19);
                if (_raid.Session.defenders.Count > 0) panel.Line("An enemy hero is on guard.", 19, Ui.AccentColor);
                panel.Line($"Heroes placed: {_raid.Placements.Count} of {_raid.Session.team.Count}");
                var left = TroopData.All.Where(t => TroopsLeft(t.id) > 0).ToList();
                if (Army.Counts.Count > 0)
                    panel.Line(left.Count > 0 ? "Left: " + string.Join(", ", left.Select(t => $"{t.displayName} {TroopsLeft(t.id)}")) : "All troops placed.", 19);
                if (left.Count > 0)
                {
                    var selected = TroopData.Find(_raid.SelectedTroop);
                    var caption = selected != null ? $"Landing: {selected.displayName}" : "Landing: heroes. Tap to choose troops";
                    panel.Action("NextTroopButton", caption, CycleTroop, true, null, 19);
                }
                var everything = _raid.Placements.Count == _raid.Session.team.Count && left.Count == 0;
                panel.Action("AutoPlaceButton", "Place the rest for me", AutoPlaceHeroes, !everything);
                panel.Action("StartBattleButton", "Start battle", StartBattle, _raid.Placements.Count + _raid.Stacks.Count > 0, Ui.AttackColor);
                panel.Action("RetreatButton", "Retreat", RetreatRaid);
            });
        }

        private int TroopsLeft(string troopId) =>
            Army.CountOf(troopId) - _raid.Stacks.Where(s => s.troopId == troopId).Sum(s => s.count);

        /// <summary>Steps through heroes and then each troop the player still has.</summary>
        public void CycleTroop()
        {
            if (_raid == null) return;
            var choices = new List<string> { null };
            choices.AddRange(TroopData.All.Where(t => TroopsLeft(t.id) > 0).Select(t => t.id));
            var at = choices.IndexOf(_raid.SelectedTroop);
            _raid.SelectedTroop = choices[(at + 1) % choices.Count];
            ShowPreparePanel();
        }

        private void AddStack(string troopId, int x, int z, int count)
        {
            var stack = new RaidTroopStack { troopId = troopId, x = x, z = z, count = count };
            _raid.Stacks.Add(stack);
            _raid.Stage.ShowStack(stack);
        }

        /// <summary>Tapping a placed hero or troop picks it back up. Tapping open ground lands the next one.</summary>
        public void TapDeployCell(GridCoord cell)
        {
            if (_raid == null || _raid.Phase != RaidPhase.Prepare) return;
            var stacked = _raid.Stacks.FirstOrDefault(s => s.x == cell.X && s.z == cell.Z);
            if (stacked != null)
            {
                _raid.Stacks.Remove(stacked);
                _raid.Stage.HideStack(stacked);
                ShowPreparePanel();
                return;
            }
            if (_raid.SelectedTroop != null)
            {
                var ready = TroopsLeft(_raid.SelectedTroop);
                if (ready > 0 && VillageGrid.InBounds(cell.X, cell.Z) && BattleSim.CanDeployAt(_raid.Session.buildings, cell.X, cell.Z))
                    AddStack(_raid.SelectedTroop, cell.X, cell.Z, Mathf.Min(ready, TroopsPerTap));
                if (_raid.SelectedTroop != null && TroopsLeft(_raid.SelectedTroop) <= 0) _raid.SelectedTroop = null;
                ShowPreparePanel();
                return;
            }
            var existing = _raid.Placements.FirstOrDefault(p => p.x == cell.X && p.z == cell.Z);
            if (existing != null)
            {
                _raid.Placements.Remove(existing);
                _raid.Stage.HidePlacement(existing.instanceId);
                ShowPreparePanel();
                return;
            }
            var next = _raid.Session.team.FirstOrDefault(m => _raid.Placements.All(p => p.instanceId != m.instanceId));
            if (next == null || !VillageGrid.InBounds(cell.X, cell.Z) || !BattleSim.CanDeployAt(_raid.Session.buildings, cell.X, cell.Z)) return;
            AddPlacement(next, cell.X, cell.Z);
            ShowPreparePanel();
        }

        private void AddPlacement(RaidTeamMember member, int x, int z)
        {
            var placement = new RaidPlacement { instanceId = member.instanceId, x = x, z = z };
            _raid.Placements.Add(placement);
            _raid.Stage.ShowPlacement(placement);
        }

        public void AutoPlaceHeroes()
        {
            if (_raid == null || _raid.Phase != RaidPhase.Prepare) return;
            // Line up along the side nearest the camera, spreading out from the middle.
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
                    AddPlacement(member, x, z);
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
                    AddStack(troop.id, x, z, Mathf.Min(TroopsLeft(troop.id), TroopsPerTap));
                }
            }
            _raid.SelectedTroop = null;
            ShowPreparePanel();
        }

        public void RetreatRaid()
        {
            EndRaid();
            CloseAll();
        }

        // ---------- The battle ----------

        public void StartBattle()
        {
            if (_raid == null || _raid.Phase != RaidPhase.Prepare || _raid.Placements.Count + _raid.Stacks.Count == 0) return;
            if (IsOnline)
            {
                var raid = _raid;
                var placements = raid.Placements.ToList();
                var stacks = raid.Stacks.ToList();
                var signature = string.Join(",", placements.Select(p => $"{p.instanceId}:{p.x}:{p.z}")) + "|" + string.Join(",", stacks.Select(s => $"{s.troopId}:{s.x}:{s.z}:{s.count}"));
                var key = KeyFor($"raid-deploy|{raid.Session.attackId}|{signature}");
                Run(async () =>
                {
                    var outcome = await _server.RaidDeployAsync(key, raid.Session.attackId, placements, stacks);
                    ClearKey();
                    LastMessage = null;
                    if (_raid == raid) BeginPlayback(outcome);
                }, ShowPreparePanel);
                return;
            }

            var heroes = _raid.Session.ToUnits(_raid.Placements, _raid.Stacks, TroopData);
            var refusal = BattleSim.DeployError(_raid.Session.buildings, heroes);
            if (refusal.Length > 0)
            {
                LastMessage = ServerErrorText.Describe(refusal);
                ShowPreparePanel();
                return;
            }
            foreach (var stack in _raid.Stacks) Army.Remove(stack.troopId, stack.count);
            BeginPlayback(new RaidOutcome { Result = BattleSim.Run(_raid.Session.buildings, heroes, _raid.Session.ToDefenderInputs()) });
        }

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
            CreateBattleBar();
            CreateBattleHud();
        }

        private void CreateBattleBar()
        {
            var bar = new GameObject("BattleBar", typeof(RectTransform));
            var rect = (RectTransform)bar.transform;
            rect.SetParent(_uiRoot, false);
            Ui.Anchor(rect, new Vector2(0.5f, 1f), new Vector2(430f, 64f), new Vector2(0f, -104f));
            Ui.Framed(rect, Ui.PillSprite);
            var text = Ui.Label(bar.transform, "", 24, TextAnchor.MiddleLeft);
            Ui.Anchor((RectTransform)text.transform, new Vector2(0f, 0.5f), new Vector2(270f, 56f), new Vector2(14f, 0f));
            var skip = Ui.Button(bar.transform, "SkipBattleButton", "Skip", SkipBattle, true, null, 22);
            Ui.Anchor((RectTransform)skip.transform, new Vector2(1f, 0.5f), new Vector2(150f, 52f), new Vector2(-8f, 0f));
            _raid.StatusBar = bar;
            _raid.StatusText = text;
        }

        public void SkipBattle()
        {
            if (_raid != null && _raid.Phase == RaidPhase.Battle) _raid.Stage.Skip();
        }

        private void UpdateRaid()
        {
            if (_raid?.StatusText == null || _raid.Phase != RaidPhase.Battle) return;
            var seconds = _raid.Stage.SecondsLeft;
            _raid.StatusText.text = $"Time left  {seconds / 60}:{seconds % 60:00}";
            UpdateBattleHud();
        }

        // ---------- The result ----------

        private void ShowRaidResult()
        {
            if (_raid == null) return;
            _raid.Phase = RaidPhase.Resolved;
            if (_raid.StatusBar != null) Destroy(_raid.StatusBar);
            if (_raid.Hud != null) Destroy(_raid.Hud);
            _battleHud = null;
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
                panel.Line(result.dojoDestroyed ? "The enemy Dojo fell." : "The enemy Dojo still stands.");
                var landed = _raid.Placements.Count + _raid.Stacks.Sum(s => s.count);
                panel.Line($"Still standing: {result.heroesAlive} of {landed}");
                var guards = _raid.Session.defenders.Count;
                if (guards > 0) panel.Line($"Enemy heroes beaten: {guards - result.defendersAlive} of {guards}");
                panel.Line($"Time: {result.endTick / BattleSim.TicksPerSecond} seconds");
                if (_raid.Session.attackId == "practice") panel.Line("Practice battle: no reward.", 20, Ui.AccentColor);
                else if (outcome.GoldEarned > 0) panel.Line($"Earned {outcome.GoldEarned:N0} gold");
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
            if (_raid.StatusBar != null) Destroy(_raid.StatusBar);
            if (_raid.Hud != null) Destroy(_raid.Hud);
            _battleHud = null;
            _raid.Stage.Finished -= ShowRaidResult;
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
