using System;
using System.Collections.Generic;
using System.Linq;
using ShinobiDominion.Core;
using UnityEngine;
using UnityEngine.UI;

namespace ShinobiDominion.Game
{
    /// <summary>
    /// Upgrade timers and builders (ADR-040). Upgrades past level 2 take time and a free builder, the
    /// building levels up when the time is up, and gems can finish one at once. A small timer pill floats
    /// over each building being upgraded. Online, the server's clock decides; this only shows it.
    /// </summary>
    public sealed partial class VillageController
    {
        private sealed class TimerPill
        {
            public RectTransform Root;
            public Text Label;
            public RectTransform Fill;
        }

        private readonly Dictionary<int, TimerPill> _timerPills = new Dictionary<int, TimerPill>();
        private RectTransform _timerLayer;
        private bool _syncAfterSettle;

        /// <summary>Plain, short time left: "45s", "12m 30s", "3h 05m".</summary>
        public static string FormatDuration(long ticks)
        {
            var seconds = Math.Max(0L, (ticks + TimeSpan.TicksPerSecond - 1) / TimeSpan.TicksPerSecond);
            if (seconds < 60) return $"{seconds}s";
            if (seconds < 3600) return $"{seconds / 60}m {seconds % 60:00}s";
            if (seconds < 86400) return $"{seconds / 3600}h {seconds / 60 % 60:00}m";
            return $"{seconds / 86400}d {seconds / 3600 % 24:00}h";
        }

        private string BuilderLine() => $"Builders free: {Village.Builders - Village.BusyBuilders} of {Village.Builders}";

        /// <summary>Once a second: finish due upgrades on screen. Online, the server is asked for its own view.</summary>
        private void TickUpgrades()
        {
            if (Village.Settle(Now) | Research.Settle(Now))
            {
                GameSounds.Play(GameSound.Star);
                if (IsOnline) _syncAfterSettle = true;
                if (UiState.Screen == VillageScreen.BuildingPanel) ShowContextPanel();
            }
            if (_syncAfterSettle && IsOnline && !_busy && _raid == null)
            {
                _syncAfterSettle = false;
                Fetch(async () => ApplyServerState(await _server.GetStateAsync()));
            }
        }

        public void FinishUpgrade(int id)
        {
            if (RefuseWhenNoServer()) return;
            if (IsOnline)
            {
                if (Modes == null) return;
                VillageRequest($"finish|{id}", key => Modes.FinishUpgradeAsync(key, id), ShowContextPanel, ShowContextPanel);
                return;
            }
            RunAndRefresh(Village.TryFinishNow(id, Now));
        }

        /// <summary>The upgrade part of a building's details: what it costs and takes, or how long is left.</summary>
        private void TimedUpgradeLines(PanelView panel, BuildingInstance building)
        {
            if (building.Upgrading)
            {
                var left = building.UpgradeDoneUtcTicks - Now;
                var gems = Village.FinishCost(left);
                panel.Line($"Upgrading to level {building.Level + 1}: {FormatDuration(left)} left", 21, Ui.AccentColor);
                panel.Action("FinishUpgradeButton", $"Finish now ({gems:N0} gems)", () => FinishUpgrade(building.Id), Village.Crystals >= gems, Ui.AccentColor);
                return;
            }
            var seconds = Village.Catalog.UpgradeSeconds(building.Definition, building.Level + 1);
            if (seconds > 0)
                panel.Line($"Takes {FormatDuration(seconds * TimeSpan.TicksPerSecond)}.   {BuilderLine()}", 19, Village.BusyBuilders >= Village.Builders ? Ui.AccentColor : (Color?)null);
        }

        /// <summary>Keeps one timer pill over each building being upgraded, placed on screen every frame.</summary>
        private void UpdateTimerPills()
        {
            if (_uiRoot == null || _camera == null) return;
            var show = _raid == null && !_panel.IsFullScreen;
            if (_timerLayer == null)
            {
                _timerLayer = Ui.Rect("UpgradeTimers", _uiRoot);
                Ui.Stretch(_timerLayer);
                _timerLayer.SetAsFirstSibling();
            }
            _timerLayer.gameObject.SetActive(show);
            if (!show) return;

            var now = Now;
            var upgrading = Village.Buildings.Where(b => b.Upgrading).ToList();
            foreach (var id in _timerPills.Keys.Where(id => upgrading.All(b => b.Id != id)).ToList())
            {
                Destroy(_timerPills[id].Root.gameObject);
                _timerPills.Remove(id);
            }
            foreach (var building in upgrading)
            {
                if (!_timerPills.TryGetValue(building.Id, out var pill))
                {
                    pill = CreateTimerPill(building.Id);
                    _timerPills[building.Id] = pill;
                }
                var top = GridSpace.FootprintCenter(building.Footprint) + Vector3.up * 2.3f;
                var screen = _camera.Camera.WorldToScreenPoint(top);
                var canvas = _timerLayer.GetComponentInParent<Canvas>();
                var uiCamera = canvas == null || canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
                RectTransformUtility.ScreenPointToLocalPointInRectangle(_timerLayer, screen, uiCamera, out var local);
                pill.Root.anchoredPosition = local;
                pill.Root.gameObject.SetActive(screen.z > 0f);
                var total = Village.Catalog.UpgradeSeconds(building.Definition, building.Level + 1) * TimeSpan.TicksPerSecond;
                var left = building.UpgradeDoneUtcTicks - now;
                pill.Label.text = FormatDuration(left);
                var done = total <= 0 ? 1f : Mathf.Clamp01(1f - (float)left / total);
                pill.Fill.anchorMax = new Vector2(done, 1f);
            }
        }

        private TimerPill CreateTimerPill(int id)
        {
            var root = Ui.Rect("UpgradeTimer" + id, _timerLayer);
            root.anchorMin = root.anchorMax = new Vector2(0.5f, 0.5f);
            root.pivot = new Vector2(0.5f, 0f);
            root.sizeDelta = new Vector2(150f, 46f);
            var plate = Ui.Framed(root, Ui.PillSprite);
            plate.raycastTarget = false;

            var icon = Ui.Icon(root, HudArt.Sprite("icon_upgrade"), new Vector2(0f, 0.5f), new Vector2(34f, 34f), new Vector2(6f, 4f));
            icon.raycastTarget = false;
            var label = Ui.Strong(Ui.Label(root, "", 19, TextAnchor.MiddleCenter, Color.white));
            Ui.Anchor(label.rectTransform, new Vector2(0.5f, 1f), new Vector2(110f, 26f), new Vector2(16f, -4f));
            label.raycastTarget = false;

            var track = Ui.Rect("Track", root);
            Ui.Anchor(track, new Vector2(0.5f, 0f), new Vector2(104f, 8f), new Vector2(16f, 7f));
            Ui.Background(track, new Color(0f, 0f, 0f, 0.55f), false);
            var fill = Ui.Rect("Fill", track);
            fill.anchorMin = Vector2.zero;
            fill.anchorMax = new Vector2(0f, 1f);
            fill.offsetMin = fill.offsetMax = Vector2.zero;
            Ui.Background(fill, new Color(0.45f, 0.9f, 0.4f), false);
            UiMotion.PopIn(root, 0f, 0.5f, 0.25f);
            return new TimerPill { Root = root, Label = label, Fill = fill };
        }
    }
}
