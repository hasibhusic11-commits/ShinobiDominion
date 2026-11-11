using System;
using System.Linq;
using ShinobiDominion.Core;
using UnityEngine;
using UnityEngine.UI;
using Text = ShinobiDominion.Game.GameText;

namespace ShinobiDominion.Game
{
    /// <summary>
    /// The first-time guide, as the genre does it: one short tip at a time with a bouncing arrow over what
    /// to tap, moving on by itself once the player has done it. Steps already done are passed over, it can
    /// be skipped, and it is remembered on this device. It never blocks a tap: only its Skip button is live.
    /// </summary>
    public sealed partial class VillageController
    {
        private sealed class GuideStep
        {
            public string Text;
            public string Building;
            public bool PointAtAttack;
            public Func<bool> Done;
        }

        private const string GuidePrefix = "sd_guide_step_";
        private GuideStep[] _guideSteps;
        private int _guideStep = -1;
        private bool _guideCollected;
        private RectTransform _guideRoot;
        private Text _guideText;
        private RectTransform _guideArrow;
        private float _guideDoneTimer;

        public int GuideStepIndex => _guideStep;
        public bool GuideShowing => _guideRoot != null && _guideRoot.gameObject.activeSelf;

        private string GuideKey => GuidePrefix + Mathf.Abs((VillageBootstrap.SavePathOverride ?? "main").GetHashCode());

        private void StartGuide()
        {
            _guideSteps = new[]
            {
                new GuideStep { Text = "Welcome, commander! Tap your Gold Mine and collect the gold it has made.", Building = "gold_mine", Done = () => _guideCollected },
                new GuideStep { Text = "Every army needs a leader. Open the Summoning Gate and summon your first hero.", Building = "summoning_gate", Done = () => Roster.HeroCount > 0 },
                new GuideStep { Text = "Tap your Hero Base and put your hero on it. They lead your raids and guard the village.", Building = "hero_base", Done = () => Village.Buildings.Any(b => b.AssignedHeroInstanceId != null) },
                new GuideStep { Text = "Time to fight! Press ATTACK, open the Dungeons and clear the first stage.", PointAtAttack = true, Done = () => LastRaidOutcome != null || LastModeFight != null },
                new GuideStep { Text = "Upgrade your Town Hall: higher levels unlock more buildings. Upgrades take time and a builder.", Building = "dojo_hq", Done = () => Village.HqLevel >= 2 || Village.Buildings.Any(b => b.Upgrading) }
            };
            _guideStep = PlayerPrefs.GetInt(GuideKey, 0);
        }

        public void SkipGuide()
        {
            _guideStep = _guideSteps.Length + 1;
            PlayerPrefs.SetInt(GuideKey, _guideStep);
            PlayerPrefs.Save();
            if (_guideRoot != null)
            {
                // Gone this frame, not at the end of it.
                _guideRoot.gameObject.SetActive(false);
                Destroy(_guideRoot.gameObject);
            }
            _guideRoot = null;
        }

        private void UpdateGuide()
        {
            if (_guideSteps == null) StartGuide();
            if (_guideStep > _guideSteps.Length || _uiRoot == null) return;

            // Pass over everything already done, so returning players never see a stale tip.
            var moved = false;
            while (_guideStep < _guideSteps.Length && _guideSteps[_guideStep].Done())
            {
                _guideStep++;
                moved = true;
            }
            if (moved)
            {
                PlayerPrefs.SetInt(GuideKey, _guideStep);
                PlayerPrefs.Save();
                if (_guideRoot != null) UiMotion.Punch(_guideRoot, 0.12f);
                if (_guideStep < _guideSteps.Length) GameSounds.Play(GameSound.Star);
            }

            if (_guideRoot == null) CreateGuide();
            var finished = _guideStep >= _guideSteps.Length;
            if (finished)
            {
                _guideText.text = "You're ready! ATTACK also holds the Arena and Village Siege. Daily quests are on the left.";
                _guideArrow.gameObject.SetActive(false);
                _guideDoneTimer += Time.unscaledDeltaTime;
                if (_guideDoneTimer > 6f)
                {
                    _guideStep = _guideSteps.Length + 1;
                    PlayerPrefs.SetInt(GuideKey, _guideStep);
                    Destroy(_guideRoot.gameObject);
                    _guideRoot = null;
                }
                return;
            }

            // Only on the village itself: full screens, panels and battles have their own words.
            var visible = _raid == null && !_panel.IsOpen && UiState.Screen != VillageScreen.PlacingBuilding && UiState.Screen != VillageScreen.MovingBuilding && UiState.Screen != VillageScreen.PaintingWalls;
            _guideRoot.gameObject.SetActive(visible);
            if (!visible) return;
            var step = _guideSteps[_guideStep];
            _guideText.text = step.Text;
            PointGuideArrow(step);
        }

        private void PointGuideArrow(GuideStep step)
        {
            Vector2? screen = null;
            if (step.PointAtAttack && _hud != null)
            {
                var attack = (RectTransform)_hud.AttackButton.transform;
                screen = RectTransformUtility.WorldToScreenPoint(CanvasCamera(), attack.TransformPoint(attack.rect.center + new Vector2(0f, attack.rect.height * 0.55f)));
            }
            else if (!string.IsNullOrEmpty(step.Building))
            {
                var building = Village.Buildings.FirstOrDefault(b => b.Definition.id == step.Building);
                if (building != null)
                {
                    var world = GridSpace.FootprintCenter(building.Footprint) + Vector3.up * 2.6f;
                    var point = _camera.Camera.WorldToScreenPoint(world);
                    if (point.z > 0f) screen = point;
                }
            }
            _guideArrow.gameObject.SetActive(screen.HasValue);
            if (!screen.HasValue) return;
            var layer = (RectTransform)_guideArrow.parent;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(layer, screen.Value, CanvasCamera(), out var local);
            _guideArrow.anchoredPosition = local + new Vector2(0f, 26f + Mathf.Abs(Mathf.Sin(Time.unscaledTime * 4f)) * 22f);
        }

        private Camera CanvasCamera()
        {
            var canvas = _uiRoot.GetComponentInParent<Canvas>();
            return canvas == null || canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
        }

        private void CreateGuide()
        {
            _guideRoot = Ui.Rect("Guide", _uiRoot);
            Ui.Stretch(_guideRoot);
            _guideRoot.SetSiblingIndex(Mathf.Max(0, _uiRoot.childCount - 1));

            var arrow = Ui.Rect("GuideArrow", _guideRoot);
            arrow.anchorMin = arrow.anchorMax = new Vector2(0.5f, 0.5f);
            arrow.pivot = new Vector2(0.5f, 0f);
            arrow.sizeDelta = new Vector2(40f, 50f);
            var arrowImage = arrow.gameObject.AddComponent<Image>();
            arrowImage.sprite = ArrowSprite();
            arrowImage.raycastTarget = false;
            _guideArrow = arrow;

            // Top centre, under the currency bar: the bottom of the idle screen stays ATTACK's alone.
            var bubble = Ui.Rect("GuideBubble", _guideRoot);
            Ui.Anchor(bubble, new Vector2(0.5f, 1f), new Vector2(560f, 72f), new Vector2(0f, -108f));
            var plate = Ui.Framed(bubble, Ui.PillSprite);
            plate.raycastTarget = false;
            HudArt.DropShadow(bubble, 5f, 8f, 0.45f).raycastTarget = false;
            var text = HudArt.Label(bubble, "", HudArt.Numbers, 17, TextAnchor.MiddleLeft, HudArt.Ivory, HudArt.LacquerDeep);
            Ui.Stretch(text.rectTransform, 18f, 6f, 100f, 6f);
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.raycastTarget = false;
            _guideText = text;
            var skip = Ui.Button(bubble, "SkipGuideButton", "Skip", SkipGuide, true, null, 18);
            Ui.Anchor((RectTransform)skip.transform, new Vector2(1f, 0.5f), new Vector2(84f, 44f), new Vector2(-10f, 0f));
            UiMotion.SlideIn(bubble, new Vector2(0f, 140f), 0.3f);
        }

        private static Sprite _arrowSprite;

        /// <summary>A plain gold arrow pointing down, drawn in code.</summary>
        private static Sprite ArrowSprite()
        {
            if (_arrowSprite != null) return _arrowSprite;
            const int width = 54;
            const int height = 66;
            var pixels = new Color32[width * height];
            var gold = new Color32(255, 214, 92, 255);
            var edge = new Color32(92, 46, 10, 255);
            for (var y = 0; y < height; y++)
            for (var x = 0; x < width; x++)
            {
                var cx = Mathf.Abs(x - (width - 1) * 0.5f);
                // Head: a triangle in the lower half; shaft: a bar in the upper half.
                var headHalf = y < 34 ? y * 0.78f : -1f;
                var inHead = cx <= headHalf;
                var inShaft = y >= 30 && cx <= 9f;
                var inside = inHead || inShaft;
                var nearEdge = inside && ((inHead && cx > headHalf - 3.5f && !inShaft) || (inShaft && (cx > 6f || y > height - 4) && !(inHead && cx <= headHalf - 3.5f)) || y < 3);
                pixels[y * width + x] = !inside ? new Color32(0, 0, 0, 0) : nearEdge ? edge : gold;
            }
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            texture.SetPixels32(pixels);
            texture.Apply();
            _arrowSprite = Sprite.Create(texture, new Rect(0f, 0f, width, height), new Vector2(0.5f, 0f));
            return _arrowSprite;
        }
    }
}
