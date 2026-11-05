using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace ShinobiDominion.Game
{
    /// <summary>Small helpers for building uGUI from code. Uses the built-in font: see ADR-004.</summary>
    public static class Ui
    {
        public static readonly Color PanelColor = new Color(0.11f, 0.13f, 0.15f, 0.94f);
        public static readonly Color PillColor = new Color(0.08f, 0.10f, 0.12f, 0.62f);
        public static readonly Color ButtonColor = new Color(0.27f, 0.32f, 0.36f, 1f);
        public static readonly Color AccentColor = new Color(0.70f, 0.52f, 0.17f, 1f);
        public static readonly Color AttackColor = new Color(0.66f, 0.17f, 0.13f, 1f);
        public static readonly Color TextColor = new Color(0.96f, 0.95f, 0.91f, 1f);
        public static readonly Color MutedText = new Color(0.76f, 0.77f, 0.76f, 1f);

        private const float MinTouchHeight = 52f;
        private static Font _font;

        public static Font Font => _font != null ? _font : (_font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"));

        public static RectTransform Rect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        public static void Stretch(RectTransform rect, float left = 0f, float bottom = 0f, float right = 0f, float top = 0f)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(left, bottom);
            rect.offsetMax = new Vector2(-right, -top);
        }

        public static void Anchor(RectTransform rect, Vector2 anchorAndPivot, Vector2 size, Vector2 offset)
        {
            rect.anchorMin = anchorAndPivot;
            rect.anchorMax = anchorAndPivot;
            rect.pivot = anchorAndPivot;
            rect.sizeDelta = size;
            rect.anchoredPosition = offset;
        }

        public static Image Background(RectTransform rect, Color color, bool blocksPointer = true)
        {
            var image = rect.gameObject.AddComponent<Image>();
            image.color = color;
            image.raycastTarget = blocksPointer;
            return image;
        }

        public static Text Label(Transform parent, string content, int size = 22, TextAnchor alignment = TextAnchor.MiddleLeft, Color? color = null)
        {
            var rect = Rect("Label", parent);
            var text = rect.gameObject.AddComponent<Text>();
            text.font = Font;
            text.text = content;
            text.fontSize = size;
            text.alignment = alignment;
            text.color = color ?? TextColor;
            text.raycastTarget = false;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            return text;
        }

        public static Button Button(Transform parent, string name, string caption, Action onClick, bool interactable = true, Color? color = null, int fontSize = 22)
        {
            var rect = Rect(name, parent);
            var image = Background(rect, color ?? ButtonColor);
            var button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            button.interactable = interactable;
            var colors = button.colors;
            colors.disabledColor = new Color(0.62f, 0.62f, 0.62f, 0.45f);
            button.colors = colors;
            if (onClick != null) button.onClick.AddListener(() => onClick());

            var layout = rect.gameObject.AddComponent<LayoutElement>();
            layout.minHeight = MinTouchHeight;
            layout.preferredHeight = MinTouchHeight;

            var label = Label(rect, caption, fontSize, TextAnchor.MiddleCenter);
            Stretch(label.rectTransform, 8f, 2f, 8f, 2f);
            return button;
        }
    }

    /// <summary>Canvas root, event system and safe-area container.</summary>
    public sealed class UiRoot
    {
        public readonly Canvas Canvas;
        public readonly RectTransform Safe;

        public UiRoot(Transform parent)
        {
            var canvasObject = new GameObject("Canvas", typeof(RectTransform));
            canvasObject.transform.SetParent(parent, false);
            Canvas = canvasObject.AddComponent<Canvas>();
            Canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280f, 720f);
            scaler.matchWidthOrHeight = 0.5f;
            canvasObject.AddComponent<GraphicRaycaster>();

            Safe = Ui.Rect("SafeArea", canvasObject.transform);
            ApplySafeArea();

            if (EventSystem.current == null)
            {
                var eventSystem = new GameObject("EventSystem", typeof(EventSystem));
                eventSystem.transform.SetParent(parent, false);
                var module = eventSystem.AddComponent<InputSystemUIInputModule>();
                module.AssignDefaultActions();
            }
        }

        public void ApplySafeArea()
        {
            var safe = Screen.safeArea;
            var width = Mathf.Max(1f, Screen.width);
            var height = Mathf.Max(1f, Screen.height);
            Safe.anchorMin = new Vector2(safe.xMin / width, safe.yMin / height);
            Safe.anchorMax = new Vector2(safe.xMax / width, safe.yMax / height);
            Safe.offsetMin = Vector2.zero;
            Safe.offsetMax = Vector2.zero;
        }
    }

    /// <summary>
    /// The entire idle screen: identity at top left, currencies at top right, and ATTACK as the
    /// only thing docked to the bottom. Adding anything here needs a very good reason.
    /// </summary>
    public sealed class HudView
    {
        public readonly RectTransform Root;
        public readonly Button AttackButton;
        public readonly Button SettingsButton;
        private readonly Text _identity;
        private readonly Text _wallet;

        public HudView(RectTransform parent, Action onAttack, Action onSettings)
        {
            Root = Ui.Rect("Hud", parent);
            Ui.Stretch(Root);

            var left = Ui.Rect("TopLeft", Root);
            Ui.Anchor(left, new Vector2(0f, 1f), new Vector2(330f, 52f), new Vector2(16f, -12f));
            Ui.Background(left, Ui.PillColor);
            _identity = Ui.Label(left, string.Empty, 22);
            Ui.Stretch(_identity.rectTransform, 16f, 0f, 92f, 0f);
            SettingsButton = Ui.Button(left, "SettingsButton", "Menu", onSettings, true, Ui.ButtonColor, 18);
            var settingsRect = (RectTransform)SettingsButton.transform;
            Ui.Anchor(settingsRect, new Vector2(1f, 0.5f), new Vector2(84f, 44f), new Vector2(-4f, 0f));

            var right = Ui.Rect("TopRight", Root);
            Ui.Anchor(right, new Vector2(1f, 1f), new Vector2(380f, 52f), new Vector2(-16f, -12f));
            Ui.Background(right, Ui.PillColor);
            _wallet = Ui.Label(right, string.Empty, 22, TextAnchor.MiddleCenter);
            Ui.Stretch(_wallet.rectTransform, 8f, 0f, 8f, 0f);

            AttackButton = Ui.Button(Root, "AttackButton", "ATTACK", onAttack, true, Ui.AttackColor, 34);
            Ui.Anchor((RectTransform)AttackButton.transform, new Vector2(1f, 0f), new Vector2(230f, 88f), new Vector2(-24f, 20f));
        }

        public void SetWallet(long gold, long crystals) => _wallet.text = $"Gold {gold:N0}     Crystals {crystals:N0}";
        public void SetIdentity(string displayName, int level) => _identity.text = $"{displayName}  ·  Lv {level}";
        public void SetAttackVisible(bool visible) => AttackButton.gameObject.SetActive(visible);
    }

    /// <summary>A dismissable overlay card docked left, clear of ATTACK. Rebuilt from scratch each time it is shown.</summary>
    public sealed class PanelView
    {
        private readonly RectTransform _parent;
        private RectTransform _card;
        private RectTransform _content;

        public bool IsOpen => _card != null;
        public RectTransform Card => _card;

        public PanelView(RectTransform parent)
        {
            _parent = parent;
        }

        public void Show(string title, Action onClose, Action<PanelView> fill)
        {
            Hide();
            _card = Ui.Rect("Panel", _parent);
            _card.anchorMin = new Vector2(0f, 0f);
            _card.anchorMax = new Vector2(0f, 1f);
            _card.pivot = new Vector2(0f, 0.5f);
            _card.sizeDelta = new Vector2(450f, -92f);
            _card.anchoredPosition = new Vector2(16f, -38f);
            Ui.Background(_card, Ui.PanelColor);

            var header = Ui.Label(_card, title, 28);
            header.rectTransform.anchorMin = new Vector2(0f, 1f);
            header.rectTransform.anchorMax = new Vector2(1f, 1f);
            header.rectTransform.pivot = new Vector2(0f, 1f);
            header.rectTransform.sizeDelta = new Vector2(-96f, 64f);
            header.rectTransform.anchoredPosition = new Vector2(20f, -4f);

            var close = Ui.Button(_card, "CloseButton", "Close", onClose, true, Ui.ButtonColor, 18);
            Ui.Anchor((RectTransform)close.transform, new Vector2(1f, 1f), new Vector2(76f, 52f), new Vector2(-8f, -8f));

            var viewport = Ui.Rect("Viewport", _card);
            Ui.Stretch(viewport, 14f, 14f, 14f, 68f);
            Ui.Background(viewport, new Color(0f, 0f, 0f, 0f));
            viewport.gameObject.AddComponent<RectMask2D>();

            _content = Ui.Rect("Content", viewport);
            _content.anchorMin = new Vector2(0f, 1f);
            _content.anchorMax = new Vector2(1f, 1f);
            _content.pivot = new Vector2(0.5f, 1f);
            _content.sizeDelta = Vector2.zero;
            var layout = _content.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 8f;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            _content.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var scroll = _card.gameObject.AddComponent<ScrollRect>();
            scroll.viewport = viewport;
            scroll.content = _content;
            scroll.horizontal = false;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 30f;

            fill(this);
        }

        public void Hide()
        {
            if (_card == null) return;
            UnityEngine.Object.Destroy(_card.gameObject);
            _card = null;
            _content = null;
        }

        public Text Line(string text, int size = 21, Color? color = null)
        {
            return Ui.Label(_content, text, size, TextAnchor.UpperLeft, color ?? Ui.MutedText);
        }

        public Button Action(string name, string caption, Action onClick, bool interactable = true, Color? color = null, int fontSize = 22)
        {
            return Ui.Button(_content, name, caption, onClick, interactable, color, fontSize);
        }

        /// <summary>A row of equal-width small buttons, used for the catalog's category tabs.</summary>
        public RectTransform Row()
        {
            var row = Ui.Rect("Row", _content);
            var layout = row.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 4f;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            return row;
        }
    }

    /// <summary>Exists only during placement, moving or wall painting. Destroyed on exit.</summary>
    public sealed class PlacementToolbar
    {
        private readonly RectTransform _root;
        private readonly RectTransform _row;
        private readonly Text _status;

        public RectTransform Root => _root;

        public PlacementToolbar(RectTransform parent)
        {
            _root = Ui.Rect("PlacementToolbar", parent);
            Ui.Anchor(_root, new Vector2(0.5f, 0f), new Vector2(760f, 112f), new Vector2(0f, 12f));
            Ui.Background(_root, Ui.PanelColor);

            _status = Ui.Label(_root, string.Empty, 21, TextAnchor.MiddleCenter);
            _status.rectTransform.anchorMin = new Vector2(0f, 1f);
            _status.rectTransform.anchorMax = new Vector2(1f, 1f);
            _status.rectTransform.pivot = new Vector2(0.5f, 1f);
            _status.rectTransform.sizeDelta = new Vector2(-20f, 42f);
            _status.rectTransform.anchoredPosition = new Vector2(0f, -2f);

            _row = Ui.Rect("Buttons", _root);
            Ui.Stretch(_row, 10f, 8f, 10f, 46f);
            var layout = _row.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 8f;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = true;
        }

        public void SetStatus(string status) => _status.text = status;

        public Button Add(string name, string caption, Action onClick, Color? color = null) =>
            Ui.Button(_row, name, caption, onClick, true, color);

        public void Destroy() => UnityEngine.Object.Destroy(_root.gameObject);
    }
}
