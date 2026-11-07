using System;
using System.Collections.Generic;
using ShinobiDominion.Core;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace ShinobiDominion.Game
{
    /// <summary>
    /// Procedural UI sprites, so the chunky framed look needs no imported art. The style is
    /// this game's own: dark lacquer panels with brass trim and vermilion accents.
    /// </summary>
    public static class UiSkin
    {
        public static readonly Color Lacquer = new Color(0.10f, 0.11f, 0.17f, 1f);
        public static readonly Color LacquerLight = new Color(0.19f, 0.20f, 0.29f, 1f);
        public static readonly Color Brass = new Color(0.80f, 0.62f, 0.26f, 1f);
        public static readonly Color Vermilion = new Color(0.72f, 0.18f, 0.13f, 1f);
        public static readonly Color Ink = new Color(0.04f, 0.04f, 0.07f, 1f);

        private static readonly Dictionary<string, Sprite> Cache = new Dictionary<string, Sprite>();

        // Sprites are drawn at twice the size they appear, so they stay crisp on high-resolution phones.
        private const float PixelsPerUnit = 200f;

        private static Color Shade(Color color, float amount) =>
            new Color(Mathf.Clamp01(color.r * amount), Mathf.Clamp01(color.g * amount), Mathf.Clamp01(color.b * amount), color.a);

        /// <summary>Steps from one band to the next over one pixel, so edges are clean and not jagged.</summary>
        private static Color Band(Color outer, Color inner, float depth, float edge) =>
            Color.Lerp(outer, inner, Mathf.Clamp01(depth - edge + 0.5f));

        /// <summary>
        /// A sliceable frame for panels and cards: a dark outline, a metal band lit from above, a dark
        /// inner line, then the fill with a thin highlight along its top.
        /// </summary>
        public static Sprite Frame(Color top, Color bottom, Color border, int borderPixels = 4)
        {
            var key = $"frame{top}{bottom}{border}{borderPixels}";
            if (Cache.TryGetValue(key, out var cached) && cached != null) return cached;
            const int size = 112;
            const float radius = 13f;
            const float outline = 3f;
            var band = borderPixels * 1.6f;
            const float line = 2.5f;
            var texture = NewTexture(size);
            for (var y = 0; y < size; y++)
            for (var x = 0; x < size; x++)
            {
                var distance = RoundedDistance(x + 0.5f, y + 0.5f, size, radius);
                var depth = -distance;
                var height = (float)y / (size - 1);
                var metal = Shade(border, Mathf.Lerp(0.72f, 1.22f, height));
                var fill = Color.Lerp(bottom, top, height);
                var inner = Band(fill, fill, depth, 0f);
                // A hairline of light where the fill meets the frame's top edge.
                if (y > size * 0.5f) inner = Band(Shade(fill, 1.5f), fill, depth, outline + band + line + 2f);

                var color = Band(Ink, metal, depth, outline);
                color = Band(color, Ink, depth, outline + band);
                color = Band(color, inner, depth, outline + band + line);
                color.a *= Mathf.Clamp01(0.5f - distance);
                texture.SetPixel(x, y, color);
            }
            texture.Apply();
            var slice = Mathf.Ceil(radius + outline + band + line + 4f);
            var sprite = Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), PixelsPerUnit, 0, SpriteMeshType.FullRect, new Vector4(slice, slice, slice, slice));
            Cache[key] = sprite;
            return sprite;
        }

        /// <summary>A white button that takes its colour from the Image tint: a raised face with a dark lip.</summary>
        public static Sprite ButtonFace
        {
            get
            {
                const string key = "buttonface";
                if (Cache.TryGetValue(key, out var cached) && cached != null) return cached;
                const int size = 96;
                const float radius = 11f;
                const float outline = 3f;
                const float lip = 7f;
                var texture = NewTexture(size);
                for (var y = 0; y < size; y++)
                for (var x = 0; x < size; x++)
                {
                    var distance = RoundedDistance(x + 0.5f, y + 0.5f, size, radius);
                    var depth = -distance;
                    var height = (float)y / (size - 1);
                    var face = Color.Lerp(new Color(0.74f, 0.74f, 0.74f), Color.white, height);
                    // The raised face is inset from the bottom, leaving a darker lip beneath it.
                    var fromBottom = y + 0.5f;
                    var color = Band(new Color(0.42f, 0.42f, 0.42f), face, fromBottom, outline + lip);
                    if (y > size - outline - 5f) color = Band(color, new Color(1f, 1f, 1f), size - y, outline + 5f);
                    color = Band(Ink, color, depth, outline);
                    color.a = Mathf.Clamp01(0.5f - distance);
                    texture.SetPixel(x, y, color);
                }
                texture.Apply();
                var slice = radius + outline + lip + 3f;
                var sprite = Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), PixelsPerUnit, 0, SpriteMeshType.FullRect, new Vector4(slice, slice, slice, slice));
                Cache[key] = sprite;
                return sprite;
            }
        }

        /// <summary>A round badge: a dark edge, a metal ring lit from above, and a glossy centre.</summary>
        public static Sprite Disc(Color fillTop, Color fillBottom, Color ring, float ringFraction = 0.11f)
        {
            var key = $"disc{fillTop}{fillBottom}{ring}{ringFraction}";
            if (Cache.TryGetValue(key, out var cached) && cached != null) return cached;
            const int size = 256;
            var texture = NewTexture(size);
            var center = (size - 1) * 0.5f;
            var outer = size * 0.5f - 1f;
            var ringEdge = outer * (1f - ringFraction);
            for (var y = 0; y < size; y++)
            for (var x = 0; x < size; x++)
            {
                var distance = Mathf.Sqrt((x - center) * (x - center) + (y - center) * (y - center));
                var height = (float)y / (size - 1);
                var metal = Shade(ring, Mathf.Lerp(0.68f, 1.25f, height));
                var fill = Color.Lerp(fillBottom, fillTop, height);
                // A soft gloss across the top half of the centre.
                var glossX = (x - center) / outer;
                var glossY = (y - center * 1.42f) / (outer * 0.62f);
                var gloss = Mathf.Clamp01(1f - (glossX * glossX + glossY * glossY)) * 0.22f;
                fill = Color.Lerp(fill, Color.white, gloss);

                var depth = outer - distance;
                var color = Band(Ink, metal, depth, 5f);
                color = Band(color, Ink, depth, outer - ringEdge);
                color = Band(color, fill, depth, outer - ringEdge + 3f);
                color.a *= Mathf.Clamp01(depth + 0.5f);
                texture.SetPixel(x, y, color);
            }
            texture.Apply();
            var sprite = Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), PixelsPerUnit);
            Cache[key] = sprite;
            return sprite;
        }

        /// <summary>A cut gem, with a lighter facet on the upper left and a dark edge.</summary>
        public static Sprite Diamond(Color top, Color bottom)
        {
            var key = $"diamond{top}{bottom}";
            if (Cache.TryGetValue(key, out var cached) && cached != null) return cached;
            const int size = 128;
            var texture = NewTexture(size);
            var center = (size - 1) * 0.5f;
            for (var y = 0; y < size; y++)
            for (var x = 0; x < size; x++)
            {
                var distance = Mathf.Abs(x - center) + Mathf.Abs(y - center);
                var depth = center - distance;
                var color = Color.Lerp(bottom, top, (float)y / (size - 1));
                var facet = x < center ? (y > center ? 1.25f : 0.9f) : (y > center ? 1.05f : 0.72f);
                color = Shade(color, facet);
                color = Band(Ink, color, depth, 5f);
                color.a *= Mathf.Clamp01(depth + 0.5f);
                texture.SetPixel(x, y, color);
            }
            texture.Apply();
            var sprite = Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), PixelsPerUnit);
            Cache[key] = sprite;
            return sprite;
        }

        /// <summary>A flat line with soft ends, for dividing panel sections.</summary>
        public static Sprite Divider
        {
            get
            {
                const string key = "divider";
                if (Cache.TryGetValue(key, out var cached) && cached != null) return cached;
                const int width = 256;
                const int height = 8;
                var texture = new Texture2D(width, height, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
                for (var y = 0; y < height; y++)
                for (var x = 0; x < width; x++)
                {
                    var along = Mathf.Min(x, width - 1 - x) / 40f;
                    var color = y >= 4 ? Brass : Shade(Ink, 1f);
                    color.a = Mathf.Clamp01(along) * (y == 0 || y == height - 1 ? 0.3f : 1f);
                    texture.SetPixel(x, y, color);
                }
                texture.Apply();
                var sprite = Sprite.Create(texture, new Rect(0, 0, width, height), new Vector2(0.5f, 0.5f), PixelsPerUnit);
                Cache[key] = sprite;
                return sprite;
            }
        }

        private static Texture2D NewTexture(int size) =>
            new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear
            };

        private static float RoundedDistance(float x, float y, int size, float radius)
        {
            var half = size * 0.5f;
            var qx = Mathf.Abs(x - half) - (half - radius);
            var qy = Mathf.Abs(y - half) - (half - radius);
            var outside = new Vector2(Mathf.Max(qx, 0f), Mathf.Max(qy, 0f)).magnitude;
            return outside + Mathf.Min(Mathf.Max(qx, qy), 0f) - radius;
        }
    }

    /// <summary>Small helpers for building uGUI from code. Uses the built-in font: see ADR-004.</summary>
    public static class Ui
    {
        public static readonly Color ButtonColor = new Color(0.33f, 0.37f, 0.48f, 1f);
        public static readonly Color AccentColor = new Color(0.82f, 0.58f, 0.18f, 1f);
        public static readonly Color AttackColor = UiSkin.Vermilion;
        public static readonly Color TextColor = new Color(0.98f, 0.96f, 0.90f, 1f);
        public static readonly Color MutedText = new Color(0.86f, 0.85f, 0.82f, 1f);

        private const float MinTouchHeight = 56f;
        private static Font _font;

        public static Font Font => _font != null ? _font : (_font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"));

        public static Sprite PanelSprite => UiSkin.Frame(UiSkin.LacquerLight, UiSkin.Lacquer, UiSkin.Brass);
        public static Sprite PillSprite => UiSkin.Frame(new Color(0.16f, 0.17f, 0.24f, 0.94f), new Color(0.07f, 0.08f, 0.12f, 0.94f), UiSkin.Brass, 3);

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

        public static Image Framed(RectTransform rect, Sprite sprite, bool blocksPointer = true, Color? tint = null)
        {
            var image = Background(rect, tint ?? Color.white, blocksPointer);
            image.sprite = sprite;
            image.type = Image.Type.Sliced;
            return image;
        }

        public static Image Icon(Transform parent, Sprite sprite, Vector2 anchorAndPivot, Vector2 size, Vector2 offset)
        {
            var rect = Rect("Icon", parent);
            Anchor(rect, anchorAndPivot, size, offset);
            var image = Background(rect, Color.white, false);
            image.sprite = sprite;
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

        /// <summary>Bold with a dark edge, so game text stays readable over busy art.</summary>
        public static Text Strong(Text text)
        {
            text.fontStyle = FontStyle.Bold;
            var outline = text.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color(0f, 0f, 0f, 0.92f);
            outline.effectDistance = new Vector2(1.4f, -1.4f);
            return text;
        }

        public static Button Button(Transform parent, string name, string caption, Action onClick, bool interactable = true, Color? color = null, int fontSize = 22)
        {
            var rect = Rect(name, parent);
            var image = Framed(rect, UiSkin.ButtonFace, true, color ?? ButtonColor);
            var button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            button.interactable = interactable;
            var colors = button.colors;
            colors.disabledColor = new Color(0.55f, 0.55f, 0.55f, 0.75f);
            button.colors = colors;
            button.onClick.AddListener(() => GameSounds.Play(GameSound.Click));
            if (onClick != null) button.onClick.AddListener(() => onClick());

            var layout = rect.gameObject.AddComponent<LayoutElement>();
            layout.minHeight = MinTouchHeight;
            layout.preferredHeight = MinTouchHeight;

            var label = Strong(Label(rect, caption, fontSize, TextAnchor.MiddleCenter));
            Stretch(label.rectTransform, 8f, 3f, 8f, 3f);
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
            Canvas.pixelPerfect = true;
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
        private readonly Text _gold;
        private readonly Text _crystals;

        public HudView(RectTransform parent, Action onAttack, Action onSettings)
        {
            Root = Ui.Rect("Hud", parent);
            Ui.Stretch(Root);

            var left = Ui.Rect("TopLeft", Root);
            Ui.Anchor(left, new Vector2(0f, 1f), new Vector2(340f, 56f), new Vector2(46f, -14f));
            Ui.Framed(left, Ui.PillSprite);
            _identity = Ui.Strong(Ui.Label(left, string.Empty, 22));
            Ui.Stretch(_identity.rectTransform, 44f, 0f, 92f, 0f);
            SettingsButton = Ui.Button(left, "SettingsButton", "Menu", onSettings, true, Ui.ButtonColor, 18);
            Ui.Anchor((RectTransform)SettingsButton.transform, new Vector2(1f, 0.5f), new Vector2(84f, 46f), new Vector2(-5f, 0f));

            // The avatar badge overlaps the bar's left end, giving the bar a heavier look.
            var avatar = Ui.Icon(Root, UiSkin.Disc(UiSkin.LacquerLight, UiSkin.Lacquer, UiSkin.Brass), new Vector2(0f, 1f), new Vector2(76f, 76f), new Vector2(10f, -6f));
            var monogram = Ui.Strong(Ui.Label(avatar.transform, "P", 34, TextAnchor.MiddleCenter));
            Ui.Stretch(monogram.rectTransform);

            _gold = Pill(Root, "Gold", new Vector2(-226f, -14f), UiSkin.Disc(new Color(1f, 0.86f, 0.42f), new Color(0.78f, 0.54f, 0.14f), new Color(0.55f, 0.36f, 0.08f)));
            _crystals = Pill(Root, "Crystals", new Vector2(-14f, -14f), UiSkin.Diamond(new Color(0.72f, 0.95f, 1f), new Color(0.22f, 0.55f, 0.82f)));

            // A big round badge: the one prominent control on the idle screen.
            AttackButton = Ui.Button(Root, "AttackButton", "ATTACK", onAttack, true, Color.white, 28);
            var attackRect = (RectTransform)AttackButton.transform;
            Ui.Anchor(attackRect, new Vector2(1f, 0f), new Vector2(158f, 158f), new Vector2(-18f, 14f));
            var attackImage = AttackButton.GetComponent<Image>();
            attackImage.sprite = UiSkin.Disc(new Color(0.88f, 0.28f, 0.18f), new Color(0.50f, 0.09f, 0.08f), UiSkin.Brass, 0.09f);
            attackImage.type = Image.Type.Simple;
        }

        private static Text Pill(RectTransform root, string name, Vector2 offset, Sprite icon)
        {
            var pill = Ui.Rect(name + "Pill", root);
            Ui.Anchor(pill, new Vector2(1f, 1f), new Vector2(204f, 50f), offset);
            Ui.Framed(pill, Ui.PillSprite);
            Ui.Icon(pill, icon, new Vector2(0f, 0.5f), new Vector2(38f, 38f), new Vector2(7f, 0f));
            var text = Ui.Strong(Ui.Label(pill, string.Empty, 21, TextAnchor.MiddleRight));
            Ui.Stretch(text.rectTransform, 50f, 0f, 14f, 0f);
            return text;
        }

        public void SetWallet(long gold, long crystals)
        {
            _gold.text = gold.ToString("N0");
            _crystals.text = crystals.ToString("N0");
        }

        public void SetIdentity(string displayName, int level) => _identity.text = $"{displayName}  ·  Lv {level}";
        public void SetAttackVisible(bool visible) => AttackButton.gameObject.SetActive(visible);
    }

    /// <summary>
    /// Dismissable overlays, rebuilt from scratch each time. Show is a side card over the village
    /// for quick actions. ShowScreen fills the display for catalog, roster and hero screens, and
    /// is still opened only from a building and fully removed on exit.
    /// </summary>
    public sealed class PanelView
    {
        private readonly RectTransform _parent;
        private RectTransform _card;
        private RectTransform _content;

        public bool IsOpen => _card != null;
        public bool IsFullScreen { get; private set; }
        public RectTransform Card => _card;

        public PanelView(RectTransform parent)
        {
            _parent = parent;
        }

        public void Show(string title, Action onClose, Action<PanelView> fill)
        {
            Hide();
            IsFullScreen = false;
            _card = Ui.Rect("Panel", _parent);
            _card.anchorMin = new Vector2(0f, 0f);
            _card.anchorMax = new Vector2(0f, 1f);
            _card.pivot = new Vector2(0f, 0.5f);
            _card.sizeDelta = new Vector2(450f, -104f);
            _card.anchoredPosition = new Vector2(16f, -44f);
            Ui.Framed(_card, Ui.PanelSprite);

            var header = Ui.Strong(Ui.Label(_card, title, 27, TextAnchor.MiddleLeft, new Color(1f, 0.9f, 0.66f)));
            header.rectTransform.anchorMin = new Vector2(0f, 1f);
            header.rectTransform.anchorMax = new Vector2(1f, 1f);
            header.rectTransform.pivot = new Vector2(0f, 1f);
            header.rectTransform.sizeDelta = new Vector2(-92f, 60f);
            header.rectTransform.anchoredPosition = new Vector2(20f, -8f);

            var divider = Ui.Rect("HeaderDivider", _card);
            divider.anchorMin = new Vector2(0f, 1f);
            divider.anchorMax = new Vector2(1f, 1f);
            divider.pivot = new Vector2(0.5f, 1f);
            divider.sizeDelta = new Vector2(-28f, 4f);
            divider.anchoredPosition = new Vector2(0f, -68f);
            Ui.Background(divider, Color.white, false).sprite = UiSkin.Divider;

            var close = Ui.Button(_card, "CloseButton", "X", onClose, true, UiSkin.Vermilion, 24);
            Ui.Anchor((RectTransform)close.transform, new Vector2(1f, 1f), new Vector2(56f, 56f), new Vector2(-12f, -10f));

            BuildScrollArea(14f, 16f, 14f, 78f);
            fill(this);
        }

        public void ShowScreen(string title, string headerInfo, Action onBack, Action<PanelView> fill)
        {
            Hide();
            IsFullScreen = true;
            _card = Ui.Rect("Screen", _parent);
            Ui.Stretch(_card);
            Ui.Framed(_card, UiSkin.Frame(new Color(0.15f, 0.15f, 0.25f), new Color(0.06f, 0.06f, 0.10f), UiSkin.Brass));

            var back = Ui.Button(_card, "CloseButton", "Back", onBack, true, UiSkin.Vermilion, 20);
            Ui.Anchor((RectTransform)back.transform, new Vector2(0f, 1f), new Vector2(104f, 56f), new Vector2(14f, -12f));

            var header = Ui.Strong(Ui.Label(_card, title, 32));
            header.rectTransform.anchorMin = new Vector2(0f, 1f);
            header.rectTransform.anchorMax = new Vector2(1f, 1f);
            header.rectTransform.pivot = new Vector2(0f, 1f);
            header.rectTransform.sizeDelta = new Vector2(-132f, 72f);
            header.rectTransform.anchoredPosition = new Vector2(132f, -4f);

            if (!string.IsNullOrEmpty(headerInfo))
            {
                var info = Ui.Strong(Ui.Label(_card, headerInfo, 19, TextAnchor.MiddleRight));
                info.rectTransform.anchorMin = new Vector2(0.5f, 1f);
                info.rectTransform.anchorMax = new Vector2(1f, 1f);
                info.rectTransform.pivot = new Vector2(1f, 1f);
                info.rectTransform.sizeDelta = new Vector2(-24f, 72f);
                info.rectTransform.anchoredPosition = new Vector2(-20f, -4f);
            }

            BuildScrollArea(18f, 14f, 18f, 80f);
            fill(this);
        }

        private void BuildScrollArea(float left, float bottom, float right, float top)
        {
            var viewport = Ui.Rect("Viewport", _card);
            Ui.Stretch(viewport, left, bottom, right, top);
            Ui.Background(viewport, new Color(0f, 0f, 0f, 0f));
            viewport.gameObject.AddComponent<RectMask2D>();

            _content = Ui.Rect("Content", viewport);
            _content.anchorMin = new Vector2(0f, 1f);
            _content.anchorMax = new Vector2(1f, 1f);
            _content.pivot = new Vector2(0.5f, 1f);
            _content.sizeDelta = Vector2.zero;
            VerticalList(_content);
            _content.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var scroll = _card.gameObject.AddComponent<ScrollRect>();
            scroll.viewport = viewport;
            scroll.content = _content;
            scroll.horizontal = false;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 30f;
        }

        private static void VerticalList(RectTransform rect)
        {
            var layout = rect.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 8f;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
        }

        public void Hide()
        {
            if (_card == null) return;
            UnityEngine.Object.Destroy(_card.gameObject);
            _card = null;
            _content = null;
            IsFullScreen = false;
        }

        public Text Line(string text, int size = 21, Color? color = null)
        {
            return Ui.Label(_content, text, size, TextAnchor.UpperLeft, color ?? Ui.MutedText);
        }

        public Button Action(string name, string caption, Action onClick, bool interactable = true, Color? color = null, int fontSize = 22)
        {
            return Ui.Button(_content, name, caption, onClick, interactable, color, fontSize);
        }

        /// <summary>An empty area of a fixed height, for drawing something custom into.</summary>
        public RectTransform Block(float height)
        {
            var block = Ui.Rect("Block", _content);
            var layout = block.gameObject.AddComponent<LayoutElement>();
            layout.minHeight = height;
            layout.preferredHeight = height;
            return block;
        }

        /// <summary>A row of equal-width small buttons, used for tabs and filters.</summary>
        public RectTransform Row()
        {
            var row = Ui.Rect("Row", _content);
            var layout = row.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 6f;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            return row;
        }

        /// <summary>A wrapping grid of fixed-size cards.</summary>
        public RectTransform Grid(Vector2 cellSize)
        {
            var grid = Ui.Rect("Grid", _content);
            var layout = grid.gameObject.AddComponent<GridLayoutGroup>();
            layout.cellSize = cellSize;
            layout.spacing = new Vector2(12f, 12f);
            layout.startAxis = GridLayoutGroup.Axis.Horizontal;
            layout.childAlignment = TextAnchor.UpperLeft;
            return grid;
        }

        /// <summary>
        /// Two columns: a fixed-width left one and a right one that takes the rest. Calls made
        /// inside each callback add to that column.
        /// </summary>
        public void Columns(float leftWidth, Action<RectTransform> left, Action right)
        {
            var row = Ui.Rect("Columns", _content);
            var layout = row.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 16f;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;

            var leftColumn = Ui.Rect("LeftColumn", row);
            var leftLayout = leftColumn.gameObject.AddComponent<LayoutElement>();
            leftLayout.minWidth = leftWidth;
            leftLayout.preferredWidth = leftWidth;
            leftLayout.minHeight = 420f;
            left(leftColumn);

            var rightColumn = Ui.Rect("RightColumn", row);
            rightColumn.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1f;
            VerticalList(rightColumn);
            var previous = _content;
            _content = rightColumn;
            right();
            _content = previous;
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
            Ui.Anchor(_root, new Vector2(0.5f, 0f), new Vector2(760f, 118f), new Vector2(0f, 12f));
            Ui.Framed(_root, Ui.PanelSprite);

            _status = Ui.Strong(Ui.Label(_root, string.Empty, 21, TextAnchor.MiddleCenter));
            _status.rectTransform.anchorMin = new Vector2(0f, 1f);
            _status.rectTransform.anchorMax = new Vector2(1f, 1f);
            _status.rectTransform.pivot = new Vector2(0.5f, 1f);
            _status.rectTransform.sizeDelta = new Vector2(-20f, 44f);
            _status.rectTransform.anchoredPosition = new Vector2(0f, -4f);

            _row = Ui.Rect("Buttons", _root);
            Ui.Stretch(_row, 12f, 10f, 12f, 50f);
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
