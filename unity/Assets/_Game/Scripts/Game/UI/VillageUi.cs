using System;
using LitMotion;
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
            var pixels = new Color[size * size];
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
                pixels[y * size + x] = color;
            }
            texture.SetPixels(pixels);
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
                var pixels = new Color[size * size];
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
                    pixels[y * size + x] = color;
                }
                texture.SetPixels(pixels);
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
            var pixels = new Color[size * size];
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
                pixels[y * size + x] = color;
            }
            texture.SetPixels(pixels);
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
            var pixels = new Color[size * size];
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
                pixels[y * size + x] = color;
            }
            texture.SetPixels(pixels);
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
            text.font = HudArt.Numbers;
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

        /// <summary>The plate for a button's role colour: gold for the main choice, crimson for attack or danger, lacquer otherwise.</summary>
        private static string PlateFor(Color color)
        {
            if (color.r > 0.75f && color.g > 0.4f && color.b < 0.4f) return "btn_gold_s24";
            if (color.r > 0.55f && color.g < 0.32f && color.b < 0.3f) return "btn_crimson_s24";
            return "btn_lacquer_s24";
        }

        public static Button Button(Transform parent, string name, string caption, Action onClick, bool interactable = true, Color? color = null, int fontSize = 22)
        {
            var rect = Rect(name, parent);
            var plate = interactable ? PlateFor(color ?? ButtonColor) : "btn_charcoal_s24";
            var image = Framed(rect, HudArt.Sprite(plate), true);
            image.pixelsPerUnitMultiplier = 1.4f;
            var button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            button.interactable = interactable;
            var colors = button.colors;
            colors.pressedColor = new Color(0.8f, 0.8f, 0.8f, 1f);
            colors.disabledColor = Color.white;
            button.colors = colors;
            button.onClick.AddListener(() => GameSounds.Play(GameSound.Click));
            if (onClick != null) button.onClick.AddListener(() => onClick());

            var layout = rect.gameObject.AddComponent<LayoutElement>();
            layout.minHeight = MinTouchHeight;
            layout.preferredHeight = MinTouchHeight;

            var dark = plate == "btn_gold_s24";
            var label = HudArt.Label(rect, caption, HudArt.Numbers, fontSize, TextAnchor.MiddleCenter,
                dark ? new Color32(0x2A, 0x1A, 0x08, 0xFF) : (interactable ? HudArt.Ivory : HudArt.IvoryMuted),
                dark ? new Color(1f, 0.92f, 0.7f, 0.5f) : HudArt.LacquerDeep);
            Stretch(label.rectTransform, 12f, 9f, 12f, 4f);
            UiMotion.Pressable(button);
            rect.gameObject.AddComponent<DeniedShake>();
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
        private readonly Image _portrait;
        private readonly Text _monogram;
        private readonly Text _identity;
        private readonly Text _level;
        // Shortcuts and chests belong to the home screen; raids and full screens hide them with ATTACK.
        private readonly System.Collections.Generic.List<GameObject> _homeOnly = new System.Collections.Generic.List<GameObject>();
        private readonly Text _gold;
        private readonly Text _mana;
        private readonly Text _crystals;
        private readonly RectTransform _goldFill;
        private readonly Image _goldFillImage;

        // Layout from the owner's newest mockup (docs/ui/target_home_mockup_v2.webp), in reference units (1280 x 720):
        // portrait and name top left, three currencies top centre, clan banner and settings top right, a column of
        // round shortcuts down the left, chests down the right, and ATTACK alone at the bottom left.
        private const float Margin = 12f;
        private const float BarWidth = 210f;
        private const float BarHeight = 46f;
        private const float BarGap = 22f;
        private const float AttackSize = 176f;
        private const float RoundIcon = 58f;
        private const float ColumnTop = 150f;
        private const float ColumnStep = 70f;

        public HudView(RectTransform parent, Action onAttack, Action onSettings, Action<string> onShortcut, Action<string> onShop)
        {
            Root = Ui.Rect("Hud", parent);
            Ui.Stretch(Root);

            // Player: name on the pack's nameplate, a star and level on a second plate under it, the portrait ring over both.
            var plate = Ui.Rect("TopLeft", Root);
            Ui.Anchor(plate, new Vector2(0f, 1f), new Vector2(232f, 50f), new Vector2(100f, -Margin - 4f));
            var plateImage = plate.gameObject.AddComponent<Image>();
            plateImage.sprite = HudArt.Sprite("hud_player_nameplate");
            plateImage.raycastTarget = false;
            HudArt.DropShadow(plate);
            _identity = HudArt.Label(plate, string.Empty, HudArt.Display, 23, TextAnchor.MiddleLeft, HudArt.Ivory, HudArt.LacquerDeep);
            Ui.Stretch(_identity.rectTransform, 34f, 2f, 30f, 0f);
            _identity.resizeTextForBestFit = true;
            _identity.resizeTextMinSize = 14;
            _identity.resizeTextMaxSize = 23;

            var levelPlate = Ui.Rect("LevelPlate", Root);
            Ui.Anchor(levelPlate, new Vector2(0f, 1f), new Vector2(176f, 40f), new Vector2(104f, -Margin - 58f));
            var levelImage = levelPlate.gameObject.AddComponent<Image>();
            levelImage.sprite = HudArt.Sprite("hud_player_nameplate");
            levelImage.raycastTarget = false;
            HudArt.Picture(levelPlate, "Star", "icon_star", new Vector2(0f, 0.5f), new Vector2(30f, 30f), new Vector2(28f, 1f)).preserveAspect = true;
            _level = HudArt.Label(levelPlate, string.Empty, HudArt.Display, 20, TextAnchor.MiddleLeft, HudArt.BrassLight, HudArt.LacquerDeep);
            Ui.Stretch(_level.rectTransform, 62f, 1f, 20f, 0f);

            var avatar = Ui.Rect("Avatar", Root);
            Ui.Anchor(avatar, new Vector2(0f, 1f), new Vector2(124f, 124f), new Vector2(4f, -4f));
            HudArt.Picture(avatar, "Shadow", "shadow", new Vector2(0.5f, 0f), new Vector2(124f, 36f), new Vector2(0f, -12f));
            // The pack's ring has a navy middle, so it goes first and the portrait (or the initial) sits on it.
            HudArt.Picture(avatar, "Ring", "hud_portrait_ring", new Vector2(0.5f, 0.5f), new Vector2(124f, 124f), Vector2.zero).preserveAspect = true;
            var portraitMask = Ui.Rect("PortraitMask", avatar);
            Ui.Anchor(portraitMask, new Vector2(0.5f, 0.5f), new Vector2(92f, 92f), Vector2.zero);
            portraitMask.gameObject.AddComponent<Image>().sprite = UiSkin.Disc(Color.white, Color.white, Color.white, 0f);
            portraitMask.gameObject.AddComponent<Mask>().showMaskGraphic = false;
            _portrait = HudArt.Picture(portraitMask, "Portrait", "medal_face_lacquer", new Vector2(0.5f, 1f), new Vector2(92f, 120f), new Vector2(0f, 6f));
            _portrait.enabled = false;
            _monogram = HudArt.Label(avatar, "P", HudArt.Display, 40, TextAnchor.MiddleCenter, HudArt.Ivory, HudArt.LacquerDeep);
            Ui.Stretch(_monogram.rectTransform);

            // Top right: the gear, and the clan banner beside it.
            var settings = Medal.Create(Root, "SettingsButton", MedalKind.IconOnly, "settings", null, onSettings, true, 62f);
            Ui.Anchor((RectTransform)settings.transform, new Vector2(1f, 1f), new Vector2(62f, 62f), new Vector2(-Margin, -Margin));
            SettingsButton = settings;
            var banner = HudArt.Picture(Root, "ClanBanner", "banner_tomoe", new Vector2(1f, 1f), new Vector2(64f, 100f), new Vector2(-Margin - 78f, -2f));
            banner.preserveAspect = true;

            // Currencies: gold, gems and mana, each on the pack's bar with its picture on the left and a "+".
            var barsRight = -(Margin + 78f + 64f + 22f);
            // The picture is still named icon_chakra; the currency is Mana (owner, 2026-11-08).
            _mana = Counter(Root, "Mana", "chakra", barsRight, onShop, out _, out _);
            _crystals = Counter(Root, "Crystals", "crystals", barsRight - (BarWidth + BarGap), onShop, out _, out _);
            _gold = Counter(Root, "Gold", "gold", barsRight - 2f * (BarWidth + BarGap), onShop, out _goldFill, out _goldFillImage);

            // The left column of round shortcuts (gift, rankings, quests, friends, mail).
            var names = new[] { ("GiftButton", "gift"), ("TrophyButton", "trophy"), ("QuestsButton", "quests"), ("FriendsButton", "friends"), ("MailButton", "mail") };
            for (var i = 0; i < names.Length; i++)
            {
                var (name, icon) = names[i];
                var button = Medal.Create(Root, name, MedalKind.IconOnly, icon, null, () => onShortcut(icon), true, RoundIcon);
                _homeOnly.Add(button.gameObject);
                Ui.Anchor((RectTransform)button.transform, new Vector2(0f, 1f), new Vector2(RoundIcon, RoundIcon), new Vector2(Margin + 14f, -(ColumnTop + i * ColumnStep)));
            }

            // The right column: the two chests from the mockup. There is no chest feature yet, so they carry no
            // timer and open the "coming in a later update" window like the other unbuilt shortcuts.
            var chests = new[] { ("ChestButton", "chest"), ("CrystalChestButton", "crystal_chest") };
            for (var i = 0; i < chests.Length; i++)
            {
                var (name, key) = chests[i];
                var chest = Medal.Create(Root, name, MedalKind.IconOnly, i == 0 ? "chest" : "crystals", null, () => onShortcut(key), true, 66f);
                _homeOnly.Add(chest.gameObject);
                Ui.Anchor((RectTransform)chest.transform, new Vector2(1f, 1f), new Vector2(66f, 66f), new Vector2(-Margin - 10f, -(ColumnTop + 40f + i * 96f)));
            }

            // ATTACK: the pack's painted crimson medallion, alone at the bottom left.
            AttackButton = Medal.Create(Root, "AttackButton", MedalKind.Attack, "attack", "Attack", onAttack, true, AttackSize);
            var attackRect = (RectTransform)AttackButton.transform;
            // The kit's Attack button is a wide banner (about 3:2); the older medallion was round.
            var attackSize = Medal.WholeFor(MedalKind.Attack, "attack") != null ? new Vector2(AttackSize * 1.3f, AttackSize * 1.3f / 1.49f) : new Vector2(AttackSize, AttackSize);
            Ui.Anchor(attackRect, new Vector2(0f, 0f), attackSize, new Vector2(10f, 8f));
            var glow = HudArt.Picture(attackRect, "Glow", "glow", new Vector2(0.5f, 0.5f), attackSize * 1.2f, Vector2.zero);
            glow.color = new Color(1f, 0.8f, 0.45f, 0f);
            glow.transform.SetAsFirstSibling();
            UiMotion.Breathe(glow, 0.5f, 2.4f);
        }

        private static Text Counter(RectTransform root, string name, string icon, float right, Action<string> onShop, out RectTransform fill, out Image fillImage)
        {
            var plate = Ui.Rect(name + "Pill", root);
            Ui.Anchor(plate, new Vector2(1f, 1f), new Vector2(BarWidth, BarHeight), new Vector2(right, -Margin - 8f));
            var bar = plate.gameObject.AddComponent<Image>();
            bar.sprite = HudArt.Sprite("hud_resource_bar");
            bar.raycastTarget = false;
            HudArt.DropShadow(plate);
            // The bar's own socket holds a painted coin; a round navy socket covers it so each currency shows its own picture.
            HudArt.Picture(plate, "Socket", "hud_small_round_button", new Vector2(0f, 0.5f), new Vector2(56f, 56f), new Vector2(-6f, 0f)).preserveAspect = true;
            var picture = HudArt.Picture(plate, "Icon", icon == "crystals" ? "gem_crystal" : "icon_" + icon, new Vector2(0f, 0.5f), new Vector2(48f, 48f), new Vector2(-2f, 1f));
            picture.preserveAspect = true;
            var text = HudArt.Label(plate, string.Empty, HudArt.Numbers, 24, TextAnchor.MiddleRight, HudArt.Ivory, HudArt.LacquerDeep);
            Ui.Stretch(text.rectTransform, 48f, 4f, 50f, 2f);
            var plus = Ui.Rect("Plus", plate);
            Ui.Anchor(plus, new Vector2(1f, 0.5f), new Vector2(46f, 46f), new Vector2(10f, 0f));
            var plusImage = plus.gameObject.AddComponent<Image>();
            plusImage.sprite = HudArt.Sprite("hud_plus_button");
            plusImage.preserveAspect = true;
            var plusButton = plus.gameObject.AddComponent<Button>();
            plusButton.name = name + "ShopButton";
            plusButton.targetGraphic = plusImage;
            plusButton.onClick.AddListener(() => GameSounds.Play(GameSound.Click));
            plusButton.onClick.AddListener(() => onShop(icon));
            UiMotion.Pressable(plusButton);
            fill = null;
            fillImage = null;
            if (name != "Gold") return text;

            var track = Ui.Rect("CapacityTrack", plate);
            track.anchorMin = new Vector2(0f, 0f);
            track.anchorMax = new Vector2(1f, 0f);
            track.pivot = new Vector2(0.5f, 0f);
            track.offsetMin = new Vector2(50f, 6f);
            track.offsetMax = new Vector2(-48f, 11f);
            var trackImage = track.gameObject.AddComponent<Image>();
            trackImage.sprite = HudArt.Sprite("track_s8");
            trackImage.type = Image.Type.Sliced;
            trackImage.raycastTarget = false;
            fill = Ui.Rect("CapacityFill", track);
            fill.anchorMin = Vector2.zero;
            fill.anchorMax = new Vector2(0f, 1f);
            fill.offsetMin = new Vector2(1f, 1f);
            fill.offsetMax = new Vector2(-1f, -1f);
            fillImage = fill.gameObject.AddComponent<Image>();
            fillImage.sprite = HudArt.Sprite("fill_s8");
            fillImage.type = Image.Type.Sliced;
            fillImage.raycastTarget = false;
            fillImage.color = HudArt.BrassLight;
            return text;
        }

        private long _shownGold = -1;
        private long _shownMana = -1;
        private long _shownCrystals = -1;
        private long _shownCapacity = -1;

        /// <summary>Numbers roll to their new value; a gain bounces the bar, a loss flashes it red once.</summary>
        public void SetWallet(long gold, long crystals, long goldCapacity = 0, long mana = 0)
        {
            if (gold != _shownGold)
            {
                Roll(_gold, _shownGold, gold);
                _shownGold = gold;
            }
            if (mana != _shownMana)
            {
                Roll(_mana, _shownMana, mana);
                _shownMana = mana;
            }
            if (crystals != _shownCrystals)
            {
                Roll(_crystals, _shownCrystals, crystals);
                _shownCrystals = crystals;
            }
            if (_goldFill != null && goldCapacity != _shownCapacity)
                _shownCapacity = goldCapacity;
            if (_goldFill != null)
            {
                var share = goldCapacity > 0 ? Mathf.Clamp01((float)gold / goldCapacity) : 0f;
                _goldFill.anchorMax = new Vector2(share, 1f);
                _goldFillImage.color = share >= 0.95f ? HudArt.CrimsonLight : HudArt.BrassLight;
            }
        }

        private readonly Dictionary<Text, MotionHandle> _rolling = new Dictionary<Text, MotionHandle>();
        private readonly Dictionary<Text, long> _onScreen = new Dictionary<Text, long>();

        private void Roll(Text text, long from, long to)
        {
            // A new change takes over from the one still rolling, starting at the number on screen, so two
            // counts never fight over the same text and the number never jumps back.
            if (_rolling.TryGetValue(text, out var running) && running.IsActive())
            {
                running.Cancel();
                if (_onScreen.TryGetValue(text, out var current)) from = current;
            }
            if (from < 0)
            {
                text.text = to.ToString("N0");
                _onScreen[text] = to;
                return;
            }
            _rolling[text] = UiMotion.Count(text.gameObject, from, to, 0.4f, value =>
            {
                _onScreen[text] = value;
                text.text = value.ToString("N0");
            });
            if (to > from) UiMotion.Punch(text.transform.parent, 0.12f);
            else UiMotion.Flash(text, HudArt.CrimsonLight, HudArt.Ivory);
        }

        private string _shownName;
        private int _shownLevel = -1;

        public void SetIdentity(string displayName, int level)
        {
            if (displayName != _shownName)
            {
                _identity.text = displayName;
                _monogram.text = string.IsNullOrEmpty(displayName) ? "?" : displayName.Substring(0, 1);
                _shownName = displayName;
            }
            if (level != _shownLevel)
            {
                _level.text = "Lv. " + level;
                _shownLevel = level;
            }
        }

        /// <summary>
        /// Game feel for collecting: a handful of coins (or crystals) burst out where the building is on screen,
        /// then swoop into their counter, which bounces as they land. Purely visual; the wallet already changed.
        /// </summary>
        public void FlyCoins(Vector2 screen, bool crystals)
        {
            var counter = (crystals ? _crystals : _gold).transform.parent as RectTransform;
            if (counter == null) return;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(Root, screen, null, out var start);
            var icon = counter.Find("Icon") as RectTransform;
            var end = (Vector2)Root.InverseTransformPoint((icon != null ? icon : counter).position);
            var count = 7;
            for (var i = 0; i < count; i++)
            {
                var coin = Ui.Rect("FlyingCoin", Root);
                coin.anchorMin = coin.anchorMax = coin.pivot = new Vector2(0.5f, 0.5f);
                coin.sizeDelta = new Vector2(34f, 34f);
                coin.anchoredPosition = start;
                var image = coin.gameObject.AddComponent<Image>();
                image.sprite = HudArt.Sprite(crystals ? "gem_crystal" : "icon_gold");
                image.preserveAspect = true;
                image.raycastTarget = false;
                var angle = (i / (float)count) * Mathf.PI * 2f + UnityEngine.Random.value * 0.6f;
                var scatter = start + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle) * 0.7f + 0.4f) * UnityEngine.Random.Range(40f, 75f);
                var control = (scatter + end) * 0.5f + new Vector2(0f, 140f);
                var last = i == count - 1;
                var delay = i * 0.035f;
                LMotion.Create(0f, 1f, 0.75f).WithDelay(delay).WithScheduler(MotionScheduler.UpdateIgnoreTimeScale)
                    .WithOnComplete(() =>
                    {
                        if (coin != null) UnityEngine.Object.Destroy(coin.gameObject);
                        UiMotion.Punch(counter, 0.1f, 0.25f);
                        if (last) GameSounds.Play(GameSound.Coin, 0.5f);
                    })
                    .Bind(t =>
                    {
                        if (coin == null) return;
                        // Burst out for the first quarter, then swoop along a curve into the counter.
                        if (t < 0.25f)
                        {
                            var k = 1f - Mathf.Pow(1f - t / 0.25f, 3f);
                            coin.anchoredPosition = Vector2.Lerp(start, scatter, k);
                            coin.localScale = Vector3.one * Mathf.Lerp(0.4f, 1.15f, k);
                        }
                        else
                        {
                            var u = (t - 0.25f) / 0.75f;
                            u *= u;
                            var a = Vector2.Lerp(scatter, control, u);
                            var b = Vector2.Lerp(control, end, u);
                            coin.anchoredPosition = Vector2.Lerp(a, b, u);
                            coin.localScale = Vector3.one * Mathf.Lerp(1.15f, 0.7f, u);
                        }
                    }).AddTo(coin.gameObject);
            }
        }

        /// <summary>The player's portrait: the guarding hero's painting when there is one, otherwise the initial.</summary>
        public void SetPortrait(Sprite sprite)
        {
            _portrait.enabled = sprite != null;
            if (sprite != null) _portrait.sprite = sprite;
            _monogram.enabled = sprite == null;
        }

        public void SetAttackVisible(bool visible)
        {
            foreach (var piece in _homeOnly)
                if (piece.activeSelf != visible) piece.SetActive(visible);
            if (AttackButton.gameObject.activeSelf != visible) AttackButton.gameObject.SetActive(visible);
        }
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
            var opening = !IsOpen;
            Hide();
            IsFullScreen = false;
            _card = Ui.Rect("Panel", _parent);
            _card.anchorMin = new Vector2(0f, 0f);
            _card.anchorMax = new Vector2(0f, 1f);
            _card.pivot = new Vector2(0f, 0.5f);
            _card.sizeDelta = new Vector2(450f, -104f);
            _card.anchoredPosition = new Vector2(16f, -44f);
            Ui.Framed(_card, HudArt.Sprite("frame_s80")).pixelsPerUnitMultiplier = 1.3f;

            var header = HudArt.Label(_card, title, HudArt.Display, 25, TextAnchor.MiddleLeft, HudArt.BrassLight, HudArt.LacquerDeep);
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

            var close = Medal.Create(_card, "CloseButton", MedalKind.Destructive, "close", null, onClose, true, 54f);
            Ui.Anchor((RectTransform)close.transform, new Vector2(1f, 1f), new Vector2(54f, 54f), new Vector2(-12f, -10f));

            BuildScrollArea(14f, 16f, 14f, 78f);
            fill(this);
            if (opening)
            {
                UiMotion.SlideIn(_card, new Vector2(-70f, 0f));
                UiMotion.FadeIn(_card.gameObject);
            }
        }

        public void ShowScreen(string title, string headerInfo, Action onBack, Action<PanelView> fill)
        {
            var opening = !IsOpen;
            Hide();
            IsFullScreen = true;
            _card = Ui.Rect("Screen", _parent);
            Ui.Stretch(_card);
            Ui.Framed(_card, HudArt.Sprite("frame_s80")).pixelsPerUnitMultiplier = 1.1f;

            var back = Medal.Create(_card, "CloseButton", MedalKind.IconOnly, "back", null, onBack, true, 60f);
            Ui.Anchor((RectTransform)back.transform, new Vector2(0f, 1f), new Vector2(60f, 60f), new Vector2(20f, -10f));

            var header = HudArt.Label(_card, title, HudArt.Display, 30, TextAnchor.MiddleLeft, HudArt.BrassLight, HudArt.LacquerDeep);
            header.rectTransform.anchorMin = new Vector2(0f, 1f);
            header.rectTransform.anchorMax = new Vector2(1f, 1f);
            header.rectTransform.pivot = new Vector2(0f, 1f);
            header.rectTransform.sizeDelta = new Vector2(-132f, 72f);
            header.rectTransform.anchoredPosition = new Vector2(132f, -4f);

            if (!string.IsNullOrEmpty(headerInfo))
            {
                var info = HudArt.Label(_card, headerInfo, HudArt.Numbers, 19, TextAnchor.MiddleRight, HudArt.Ivory, HudArt.LacquerDeep);
                info.rectTransform.anchorMin = new Vector2(0.5f, 1f);
                info.rectTransform.anchorMax = new Vector2(1f, 1f);
                info.rectTransform.pivot = new Vector2(1f, 1f);
                info.rectTransform.sizeDelta = new Vector2(-24f, 72f);
                info.rectTransform.anchoredPosition = new Vector2(-20f, -4f);
            }

            BuildScrollArea(24f, 20f, 24f, 82f);
            fill(this);
            if (opening)
            {
                UiMotion.FadeIn(_card.gameObject, 0.2f);
                UiMotion.PopIn(_card, 0f, 0.96f, 0.24f);
            }
        }

        /// <summary>
        /// A full-screen scene with no scrolling list: the caller draws freely into the whole screen,
        /// and the title and Back button sit on top. Line, Action and the other list calls do not apply.
        /// </summary>
        public void ShowScene(string title, string headerInfo, Action onBack, Action<RectTransform> fill)
        {
            var opening = !IsOpen;
            Hide();
            IsFullScreen = true;
            _card = Ui.Rect("Screen", _parent);
            Ui.Stretch(_card);
            Ui.Background(_card, UiSkin.Ink);

            var scene = Ui.Rect("Scene", _card);
            Ui.Stretch(scene);
            fill(scene);

            var back = Medal.Create(_card, "CloseButton", MedalKind.IconOnly, "back", null, onBack, true, 60f);
            Ui.Anchor((RectTransform)back.transform, new Vector2(0f, 1f), new Vector2(60f, 60f), new Vector2(20f, -10f));
            var header = HudArt.Label(_card, title, HudArt.Display, 30, TextAnchor.MiddleLeft, HudArt.BrassLight, HudArt.LacquerDeep);
            Ui.Anchor(header.rectTransform, new Vector2(0f, 1f), new Vector2(560f, 72f), new Vector2(96f, -4f));
            if (!string.IsNullOrEmpty(headerInfo))
            {
                var info = HudArt.Label(_card, headerInfo, HudArt.Numbers, 19, TextAnchor.MiddleRight, HudArt.Ivory, HudArt.LacquerDeep);
                Ui.Anchor(info.rectTransform, new Vector2(1f, 1f), new Vector2(560f, 72f), new Vector2(-20f, -4f));
            }
            if (opening) UiMotion.FadeIn(_card.gameObject, 0.2f);
        }

        /// <summary>
        /// A modal window framed in brass over the dimmed village, with a crimson title ribbon and a Close medallion.
        /// The caller lays out its contents inside the returned area. Counts as full screen: the world is blocked.
        /// </summary>
        public void ShowWindow(string title, Vector2 size, Action onClose, Action<RectTransform> fill)
        {
            var opening = !IsOpen;
            Hide();
            IsFullScreen = true;
            _card = Ui.Rect("Window", _parent);
            Ui.Stretch(_card);
            Ui.Background(_card, new Color(0f, 0f, 0f, 0.45f));

            var frame = Ui.Rect("Frame", _card);
            Ui.Anchor(frame, new Vector2(0.5f, 0.5f), size, new Vector2(0f, -24f));
            Ui.Framed(frame, HudArt.Sprite("frame_s80"));
            HudArt.DropShadow(frame, 12f, 22f, 0.65f);
            var fret = Ui.Rect("Fret", frame);
            Ui.Stretch(fret, 34f, 34f, 34f, 34f);
            var fretImage = fret.gameObject.AddComponent<Image>();
            fretImage.sprite = HudArt.Sprite("kenney_fret_s16");
            fretImage.type = Image.Type.Sliced;
            fretImage.pixelsPerUnitMultiplier = 1.4f;
            fretImage.color = new Color(HudArt.Brass.r, HudArt.Brass.g, HudArt.Brass.b, 0.45f);
            fretImage.raycastTarget = false;

            var content = Ui.Rect("Content", frame);
            Ui.Stretch(content, 44f, 40f, 44f, 56f);
            fill(content);

            var ribbon = HudArt.Picture(frame, "Ribbon", "ribbon", new Vector2(0.5f, 1f), new Vector2(400f, 70f), new Vector2(0f, 34f));
            var heading = HudArt.Label(ribbon.transform, title.ToUpperInvariant(), HudArt.Display, 28, TextAnchor.MiddleCenter, HudArt.Ivory, HudArt.CrimsonDark);
            Ui.Stretch(heading.rectTransform, 30f, 4f, 30f, 2f);
            var close = Medal.Create(frame, "CloseButton", MedalKind.Destructive, "close", null, onClose, true, 70f);
            Ui.Anchor((RectTransform)close.transform, new Vector2(1f, 1f), new Vector2(70f, 70f), new Vector2(24f, 24f));

            if (opening)
            {
                UiMotion.FadeIn(_card.gameObject, 0.18f);
                UiMotion.PopIn(frame, 0f, 0.92f, 0.24f);
            }
        }

        private void BuildScrollArea(float left, float bottom, float right, float top)
        {
            var viewport = Ui.Rect("Viewport", _card);
            Ui.Stretch(viewport, left, bottom, right, top);
            Ui.Background(viewport, new Color(0f, 0f, 0f, 0f));
            viewport.gameObject.AddComponent<RectMask2D>().softness = new Vector2Int(0, 22);

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
