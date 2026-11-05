using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ShinobiDominion.Core;
using UnityEngine;

namespace ShinobiDominion.Game
{
    /// <summary>
    /// The online path. While connected, the client never edits heroes, items or crystals itself:
    /// it sends a request and replaces its copy with the server's answer.
    /// </summary>
    public sealed partial class VillageController
    {
        private IGameServer _server;
        private ServerOdds _odds;
        private bool _busy;
        private string _pendingKey;
        private string _pendingRequest;

        /// <summary>True once the server has answered. All hero and crystal changes then go through it.</summary>
        public bool IsOnline { get; private set; }
        public long? ServerCrystals { get; private set; }
        public Task ConnectTask { get; private set; } = Task.CompletedTask;

        public void ConnectToServer(IGameServer server)
        {
            _server = server;
            if (_server != null) ConnectTask = ConnectAsync();
        }

        private async Task ConnectAsync()
        {
            try
            {
                var state = await _server.ConnectAsync();
                if (this == null) return;
                IsOnline = true;
                ApplyServerState(state);
                await ClearOrphanedSlotsAsync(state);
            }
            catch (GameServerException exception)
            {
                Debug.LogWarning($"Playing offline, server unavailable: {exception.Code}");
            }
            catch (FormatException exception)
            {
                Debug.LogError($"Server sent bad data: {exception.Message}");
            }
        }

        /// <summary>The server's answer replaces local heroes, items, crystals and base assignments.</summary>
        private void ApplyServerState(ServerState state)
        {
            Roster.ReplaceWithServerState(state.RosterJson);
            ServerCrystals = state.Crystals;

            foreach (var building in Village.Buildings.ToList())
            {
                if (building.Definition.function != BuildingFunction.HeroBase) continue;
                state.HeroBySlot.TryGetValue(building.Id, out var hero);
                if (hero == building.AssignedHeroInstanceId) continue;
                if (hero == null) Village.TryUnassignHero(building.Id);
                else
                {
                    // Clear any stale local holder first, since the server already allows this move.
                    foreach (var other in Village.Buildings.Where(b => b.AssignedHeroInstanceId == hero).ToList())
                        Village.TryUnassignHero(other.Id);
                    Village.TryAssignHero(building.Id, hero);
                }
            }
            RefreshHud();
        }

        /// <summary>The server counts these slots against the six, so release any whose base no longer exists.</summary>
        private async Task ClearOrphanedSlotsAsync(ServerState state)
        {
            foreach (var slot in state.HeroBySlot.Keys.ToList())
            {
                var building = Village.Find(slot);
                if (building != null && building.Definition.function == BuildingFunction.HeroBase) continue;
                ApplyServerState(await _server.AssignAsync(slot, null));
            }
        }

        private async void Run(Func<Task> work, Action onFailure = null)
        {
            if (_busy) return;
            _busy = true;
            try
            {
                await work();
            }
            catch (GameServerException exception)
            {
                LastMessage = ServerErrorText.Describe(exception.Code);
                if (this != null)
                {
                    _hasError = true;
                    onFailure?.Invoke();
                }
            }
            catch (FormatException exception)
            {
                LastMessage = "The server sent something unexpected.";
                Debug.LogError(exception.Message);
                if (this != null) onFailure?.Invoke();
            }
            finally
            {
                _busy = false;
            }
        }

        private bool _hasError;

        /// <summary>
        /// One key per intended action. A failed attempt keeps its key, so retrying after a lost
        /// response can never charge twice. Success or a different action starts a new key.
        /// </summary>
        private string KeyFor(string request)
        {
            if (_pendingRequest != request)
            {
                _pendingRequest = request;
                _pendingKey = Guid.NewGuid().ToString("N");
            }
            return _pendingKey;
        }

        private void ClearKey()
        {
            _pendingRequest = null;
            _pendingKey = null;
        }

        // ---------- Summoning Gate ----------

        public void ShowSummoningGate()
        {
            _selectionCells.Clear();
            Run(async () =>
            {
                _odds = await _server.GetOddsAsync();
                DrawSummoningGate(null);
            }, () => DrawOfflineGate());
        }

        private void DrawOfflineGate()
        {
            _panel.ShowScreen("Summoning Gate", null, CloseAll, panel =>
            {
                panel.Line(LastMessage ?? "The Summoning Gate cannot be reached right now.", 22, Ui.TextColor);
                panel.Action("RetryGateButton", "Try again", ShowSummoningGate, true, Ui.AccentColor);
            });
        }

        private void DrawSummoningGate(string message)
        {
            var odds = _odds;
            var one = odds.priceCrystalsPerRoll;
            var crystals = ServerCrystals ?? 0;
            _panel.ShowScreen(odds.bannerName, $"Crystals {crystals:N0}", CloseAll, panel =>
            {
                panel.Line(odds.status, 19, Ui.AccentColor);
                panel.Line("Every roll is independent. Nothing is guaranteed, and past results do not change these rates.", 19, Ui.MutedText);
                if (!string.IsNullOrEmpty(message)) panel.Line(message, 21, Ui.AccentColor);

                var buttons = panel.Row();
                Ui.Button(buttons, "SummonOneButton", $"Summon x1   ({one:N0} crystals)", () => Summon(1), crystals >= one, Ui.AccentColor, 20);
                Ui.Button(buttons, "SummonTenButton", $"Summon x10   ({one * 10:N0} crystals)", () => Summon(10), crystals >= one * 10, Ui.AttackColor, 20);

                panel.Line("Rates by rarity", 23, Ui.TextColor);
                foreach (var rarity in new[] { "Legendary", "Elite", "Common" })
                    if (odds.rarityChance.TryGetValue(rarity, out var chance))
                        panel.Line($"{rarity}:  {chance}", 20, Ui.TextColor);

                panel.Line("Rates by hero", 23, Ui.TextColor);
                foreach (var item in odds.items.OrderBy(i => i.weight))
                    panel.Line($"{item.displayName}  ({item.rarity}):  {item.chance}", 19);
            });
        }

        private void Summon(int count)
        {
            var request = $"summon|{_odds.poolVersion}|{count}";
            var key = KeyFor(request);
            Run(async () =>
            {
                var result = await _server.SummonAsync(_odds.poolVersion, count, key);
                ClearKey();
                ApplyServerState(result.State);
                DrawSummonResults(result);
            }, () => DrawSummoningGate(LastMessage));
        }

        private void DrawSummonResults(ServerSummonResult result)
        {
            _panel.ShowScreen("Summon Results", $"Crystals {ServerCrystals ?? 0:N0}", ShowSummoningGate, panel =>
            {
                var grid = panel.Grid(HeroCardSize);
                foreach (var roll in result.Rolls)
                {
                    var hero = Roster.Find(roll.instanceId);
                    if (hero == null) continue;
                    var tag = roll.copiesOwned <= 1 ? "New hero" : $"You own {roll.copiesOwned}";
                    HeroCard(grid, "Result_" + roll.rollIndex, hero, tag, null, true);
                }
                panel.Action("SummonDoneButton", "Done", ShowSummoningGate, true, Ui.AccentColor);
            });
        }

        // ---------- Evolution, locks, assignment ----------

        private void ConfirmEvolutionOnline(string mainId, string duplicateId)
        {
            var key = KeyFor($"evolve|{mainId}|{duplicateId}");
            Run(async () =>
            {
                var state = await _server.EvolveAsync(key, mainId, duplicateId);
                ClearKey();
                ApplyServerState(state);
                LastMessage = null;
                ShowEvolution(mainId);
            }, () => ShowEvolutionConfirm(mainId, duplicateId));
        }

        private void SetLockOnline(string instanceId, bool locked)
        {
            Run(async () =>
            {
                ApplyServerState(await _server.SetLockAsync(instanceId, locked));
                ShowHeroDetail(instanceId);
            }, () => ShowHeroDetail(instanceId));
        }

        private void AssignOnline(int baseId, string instanceId)
        {
            Run(async () =>
            {
                ApplyServerState(await _server.AssignAsync(baseId, instanceId));
                if (UiState.Screen == VillageScreen.BuildingPanel) ShowContextPanel();
            }, () =>
            {
                if (UiState.Screen == VillageScreen.BuildingPanel) ShowContextPanel();
            });
        }

        /// <summary>Removing a base locally must also free its slot on the server.</summary>
        private void ReleaseSlotOnline(int baseId)
        {
            if (!IsOnline) return;
            AssignOnline(baseId, null);
        }

        private void DevMaxLevelOnline(string instanceId)
        {
            Run(async () =>
            {
                ApplyServerState(await _server.DevMaxLevelAsync(instanceId));
                ShowHeroDetail(instanceId);
            }, () => ShowHeroDetail(instanceId));
        }
    }
}
