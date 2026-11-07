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
        private bool _connecting;
        private float _reconnectTimer = 10f;
        private string _pendingKey;
        private string _pendingRequest;

        /// <summary>True once the server has answered. All hero and crystal changes then go through it.</summary>
        public bool IsOnline { get; private set; }
        public long? ServerCrystals { get; private set; }
        public Task ConnectTask { get; private set; } = Task.CompletedTask;

        /// <summary>Set by the bootstrap. True only in the editor and development builds.</summary>
        public bool AllowOfflinePlay { get; set; } = true;

        public void ConnectToServer(IGameServer server)
        {
            _server = server;
            if (_server != null) ConnectTask = ConnectAsync();
        }

        private async Task ConnectAsync()
        {
            _connecting = true;
            try
            {
                var state = await _server.ConnectAsync();
                if (this == null) return;
                IsOnline = true;
                ApplyServerState(state);
                Debug.Log($"Connected to the server: {Village.BuildingCount} buildings, {Roster.HeroCount} heroes.");
            }
            catch (GameServerException exception)
            {
                Debug.LogWarning($"Playing offline, server unavailable: {exception.Code}");
            }
            catch (FormatException exception)
            {
                Debug.LogError($"Server sent bad data: {exception.Message}");
            }
            catch (VillageSaveException exception)
            {
                Debug.LogError($"Server sent a village that failed validation: {exception.Message}");
            }
            finally
            {
                _connecting = false;
            }
        }

        /// <summary>
        /// The server's answer replaces local heroes, items, crystals, gold and the whole village.
        /// The client still validates what arrives, and a bad snapshot changes nothing.
        /// </summary>
        private void ApplyServerState(ServerState state)
        {
            Roster.ReplaceWithServerState(state.RosterJson);
            ServerCrystals = state.Crystals;
            Army.Replace(state.Troops);
            if (state.Village != null) Village.Restore(state.Village);
            RefreshHud();
            if (_selectionCells != null && UiState.Screen == VillageScreen.BuildingPanel)
            {
                var selected = Village.Find(UiState.SelectedBuildingId);
                if (selected != null) ShowSelectionCells(selected);
            }
        }

        /// <summary>
        /// False only when this build must not change anything without a server. Release builds
        /// are like that, so a tampered local file can never become a real village.
        /// </summary>
        public bool CanChangeVillage => IsOnline || (AllowOfflinePlay && !_connecting);

        private bool RefuseWhenNoServer()
        {
            if (CanChangeVillage) return false;
            LastMessage = _connecting ? "Connecting to the server..." : "Cannot reach the server. Your village is safe, and changes need a connection.";
            if (_toolbar != null) _toolbar.SetStatus(LastMessage);
            else
                _panel.Show("Not connected", CloseAll, panel =>
                {
                    panel.Line(LastMessage, 22, Ui.TextColor);
                    panel.Action("RetryConnectButton", "Try again", () =>
                    {
                        CloseAll();
                        if (_server != null && !_connecting) ConnectTask = ConnectAsync();
                    }, true, Ui.AccentColor);
                });
            return true;
        }

        /// <summary>Sends one village change, then shows whatever the server decided.</summary>
        private void VillageRequest(string request, Func<string, Task<ServerState>> call, Action onSuccess, Action onFailure)
        {
            var key = KeyFor(request);
            Run(async () =>
            {
                var state = await call(key);
                ClearKey();
                ApplyServerState(state);
                LastMessage = null;
                onSuccess?.Invoke();
            }, onFailure);
        }

        private void ShowFailureOnToolbar()
        {
            if (_toolbar != null) _toolbar.SetStatus(LastMessage);
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
            catch (VillageSaveException exception)
            {
                LastMessage = "The server sent something unexpected.";
                Debug.LogError(exception.Message);
                if (this != null) onFailure?.Invoke();
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
                var shown = 0;
                foreach (var roll in result.Rolls)
                {
                    var hero = Roster.Find(roll.instanceId);
                    if (hero == null) continue;
                    var tag = roll.copiesOwned <= 1 ? "New hero" : $"You own {roll.copiesOwned}";
                    var card = HeroCard(grid, "Result_" + roll.rollIndex, hero, tag, null, true);
                    // Cards turn up one after another, like a reveal.
                    UiMotion.PopIn(card.transform, 0.12f * shown++, 0.2f, 0.36f);
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
