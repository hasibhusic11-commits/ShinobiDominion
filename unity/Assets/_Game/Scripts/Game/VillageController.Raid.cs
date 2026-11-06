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
            public RaidOutcome Outcome;
            public GameObject StatusBar;
            public Text StatusText;
        }

        private static readonly Vector3 MapCenter = new Vector3(25f, 0f, 25f);
        private const float RaidCameraSize = 24f;
        private const float BattleCameraSize = 15f;
        private const float HomeCameraSize = 10f;

        private RaidCatalog _raidCatalog;
        private RaidRun _raid;
        private List<RaidSummary> _raidList;
        private CellHighlight _raidCells;

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
                panel.Line(team == 0 ? "No heroes are on your Hero Bases. Assign at least one to raid." : $"Your team: {team} of 6 Hero Bases filled.");
                if (!IsOnline) panel.Line(AllowOfflinePlay ? "Practice battle: not connected, so it pays nothing." : "Raids need the server.", 20, Ui.AccentColor);
                if (IsOnline && _raidList == null)
                {
                    panel.Line("Checking your raids...");
                    return;
                }
                foreach (var raid in RaidData.All)
                {
                    var best = _raidList?.FirstOrDefault(r => r.id == raid.id)?.bestStars ?? 0;
                    panel.Line(raid.displayName, 24);
                    panel.Line(raid.description, 19);
                    panel.Line($"Best: {best} of 3 stars.   {raid.goldPerStar:N0} gold for each new star.", 19);
                    panel.Action("ScoutButton", "Scout", () => ScoutRaid(raid.id), team > 0 && (IsOnline || AllowOfflinePlay), Ui.AccentColor);
                }
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

        /// <summary>A practice raid built from this device's own data, for offline development.</summary>
        private RaidSession LocalSession(string raidId)
        {
            var raid = RaidData.Find(raidId);
            var session = new RaidSession
            {
                attackId = "practice",
                raid = new RaidSummary { id = raid.id, displayName = raid.displayName, description = raid.description, goldPerStar = 0 },
                buildings = raid.Expand(Village.Catalog)
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
            stage.Initialize(session, Village.Catalog, _camera.Camera, HeroLookFor);
            stage.Finished += ShowRaidResult;
            _raid = new RaidRun { Session = session, Stage = stage };
            _view.SetVisible(false);
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

        private void ShowPreparePanel()
        {
            if (_raid == null) return;
            _panel.Show(_raid.Session.raid.displayName, RetreatRaid, panel =>
            {
                if (!string.IsNullOrEmpty(LastMessage)) panel.Line(LastMessage, 20, Ui.AccentColor);
                panel.Line("Tap the map to place your heroes. Marked cells are too close to the enemy.");
                foreach (var member in _raid.Session.team)
                {
                    var placed = _raid.Placements.Any(p => p.instanceId == member.instanceId);
                    var name = Roster.Catalog.Get(member.archetypeId).displayName;
                    panel.Line($"{(placed ? "Placed" : "Waiting")}: {name}, {member.role}", 19);
                }
                panel.Action("AutoPlaceButton", "Place the rest for me", AutoPlaceHeroes, _raid.Placements.Count < _raid.Session.team.Count);
                panel.Action("StartBattleButton", "Start battle", StartBattle, _raid.Placements.Count > 0, Ui.AttackColor);
                panel.Action("RetreatButton", "Retreat", RetreatRaid);
            });
        }

        /// <summary>Tapping a placed hero picks it back up. Tapping open ground places the next one.</summary>
        public void TapDeployCell(GridCoord cell)
        {
            if (_raid == null || _raid.Phase != RaidPhase.Prepare) return;
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
            if (_raid == null || _raid.Phase != RaidPhase.Prepare || _raid.Placements.Count == 0) return;
            if (IsOnline)
            {
                var raid = _raid;
                var placements = raid.Placements.ToList();
                var signature = string.Join(",", placements.Select(p => $"{p.instanceId}:{p.x}:{p.z}"));
                var key = KeyFor($"raid-deploy|{raid.Session.attackId}|{signature}");
                Run(async () =>
                {
                    var outcome = await _server.RaidDeployAsync(key, raid.Session.attackId, placements);
                    ClearKey();
                    LastMessage = null;
                    if (_raid == raid) BeginPlayback(outcome);
                }, ShowPreparePanel);
                return;
            }

            var heroes = _raid.Session.ToHeroes(_raid.Placements);
            var refusal = BattleSim.DeployError(_raid.Session.buildings, heroes);
            if (refusal.Length > 0)
            {
                LastMessage = ServerErrorText.Describe(refusal);
                ShowPreparePanel();
                return;
            }
            BeginPlayback(new RaidOutcome { Result = BattleSim.Run(_raid.Session.buildings, heroes) });
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
            _raid.Stage.Play(outcome.Result, _raid.Placements);
            CreateBattleBar();
        }

        private void CreateBattleBar()
        {
            var bar = new GameObject("BattleBar", typeof(RectTransform));
            var rect = (RectTransform)bar.transform;
            rect.SetParent(_uiRoot, false);
            Ui.Anchor(rect, new Vector2(0.5f, 1f), new Vector2(430f, 64f), new Vector2(0f, -104f));
            bar.AddComponent<Image>().color = new Color(0.08f, 0.09f, 0.13f, 0.86f);
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
            _raid.StatusText.text = $"Damage {_raid.Stage.DamagePercent}%    {seconds / 60}:{seconds % 60:00}";
        }

        // ---------- The result ----------

        private void ShowRaidResult()
        {
            if (_raid == null) return;
            _raid.Phase = RaidPhase.Resolved;
            if (_raid.StatusBar != null) Destroy(_raid.StatusBar);
            var outcome = _raid.Outcome;
            var result = outcome.Result;
            if (outcome.State != null) ApplyServerState(outcome.State);
            var title = result.stars > 0 ? "Victory" : "Defeat";
            _panel.Show(title, FinishRaid, panel =>
            {
                panel.Line($"{result.stars} of 3 stars", 30, Ui.AccentColor);
                panel.Line($"Damage dealt: {result.damagePercent}%");
                panel.Line(result.dojoDestroyed ? "The enemy Dojo fell." : "The enemy Dojo still stands.");
                panel.Line($"Heroes still standing: {result.heroesAlive} of {_raid.Placements.Count}");
                panel.Line($"Time: {result.endTick / BattleSim.TicksPerSecond} seconds");
                if (_raid.Session.attackId == "practice") panel.Line("Practice battle: no reward.", 20, Ui.AccentColor);
                else panel.Line(outcome.GoldEarned > 0 ? $"Earned {outcome.GoldEarned:N0} gold" : "No gold: only new stars pay, or your vault is full.");
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
            _raid.Stage.Finished -= ShowRaidResult;
            Destroy(_raid.Stage.gameObject);
            _raid = null;
            _raidCells?.Clear();
            _view.SetVisible(true);
            _camera.StopMotion();
            _camera.Initialize(MapCenter, HomeCameraSize);
        }
    }
}
