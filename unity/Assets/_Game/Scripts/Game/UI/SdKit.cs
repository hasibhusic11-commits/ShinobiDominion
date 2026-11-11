using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace ShinobiDominion.Game
{
    /// <summary>
    /// UI kit v3 (docs/ui/overhaul): the shared theme. One place for the colours, type sizes, spacing and the
    /// authored art every redesigned screen uses, so screens stop carrying their own colour literals. The values
    /// evolve docs/ui/UI_TOKENS.md (brass, lacquer and crimson are the approved ones) with the overhaul's palette.
    /// </summary>
    public static class SdTheme
    {
        // ---------- Colour roles ----------
        public static readonly Color Ink = new Color32(0x0B, 0x0B, 0x12, 0xFF);
        public static readonly Color Lacquer = new Color32(0x1A, 0x27, 0x41, 0xFF);
        public static readonly Color LacquerDeep = new Color32(0x0E, 0x15, 0x27, 0xFF);
        public static readonly Color Brass = new Color32(0xB0, 0x8A, 0x47, 0xFF);
        public static readonly Color BrassLight = new Color32(0xF2, 0xD2, 0x8A, 0xFF);
        public static readonly Color Crimson = new Color32(0x92, 0x33, 0x2F, 0xFF);
        public static readonly Color CrimsonDark = new Color32(0x4A, 0x11, 0x0E, 0xFF);
        public static readonly Color Ivory = new Color32(0xF4, 0xE9, 0xD2, 0xFF);
        public static readonly Color IvoryMuted = new Color32(0xC6, 0xB9, 0xA3, 0xFF);
        public static readonly Color GoldInk = new Color32(0x3A, 0x22, 0x08, 0xFF);
        public static readonly Color Jade = new Color32(0x44, 0xB1, 0x7F, 0xFF);
        public static readonly Color Slate = new Color32(0x51, 0x58, 0x67, 0xFF);
        /// <summary>How a locked picture is greyed: dim and cool, never hidden.</summary>
        public static readonly Color LockedTint = new Color(0.42f, 0.44f, 0.5f, 1f);
        /// <summary>The village behind a window: dimmed, still visible.</summary>
        public static readonly Color Dimmer = new Color(0f, 0f, 0f, 0.62f);

        // ---------- Type (reference units at 1280 x 720) ----------
        public const int TitleSize = 34;
        public const int CardTitleSize = 22;
        public const int ButtonSize = 21;
        public const int BodySize = 17;
        public const int NoteSize = 18;

        // ---------- Spacing (8-unit rhythm) ----------
        public const float Gap = 16f;
        public const float HudBand = 118f;   // the top HUD row a window never covers
        public const float FrameInset = 40f; // the window frame's brass, on screen

        // ---------- Motion ----------
        public const float OpenSeconds = 0.2f;

        // ---------- Art (Resources/UI). All drawn at 2x, so sliced images use a multiplier of 2. ----------
        public const string WindowFrame = "sd_frame_window_s96";
        public const string CardFrame = "sd_frame_card_s48";
        public const string Ribbon = "sd_ribbon_title_s80x40";
        public const string PlateDark = "sd_plate_dark_s24";
        public const string Badge = "sd_badge_s26";
        public const string Pattern = "sd_pattern_lacquer";
        public const float ArtScale = 2f;
        /// <summary>The window frame is drawn at 1.5x its reference size, so its brass reads heavy on a phone.</summary>
        public const float WindowFrameScale = 1.5f;
    }

    /// <summary>The button plate families of the kit (02_UI_DESIGN_SYSTEM.md, "Button component family").</summary>
    public enum SdButtonKind
    {
        /// <summary>Crimson: committing to a fight (Find a Rival, Defend, Enter, Climb).</summary>
        War,
        /// <summary>Gold: affirmative actions (Claim, Open, Upgrade, Continue).</summary>
        Gold,
        /// <summary>Navy: secondary navigation (Rankings, Back, Details).</summary>
        Navy
    }

    /// <summary>
    /// The reusable pieces of UI kit v3, built from the authored art. Each takes real data from its caller and
    /// draws nothing it is not given.
    /// </summary>
    public static class SdKit
    {
        private static Image Sliced(RectTransform rect, string sprite, bool blocksPointer = false)
        {
            var image = Ui.Framed(rect, HudArt.Sprite(sprite), blocksPointer);
            image.pixelsPerUnitMultiplier = SdTheme.ArtScale;
            return image;
        }

        /// <summary>
        /// The window: the village dimmed behind, the brass-framed lacquer window below the HUD row, the title on the
        /// crimson ribbon over its top edge, the cross on the top right and, when reached from another screen, a Back
        /// arrow on the top left. Returns the area inside the frame.
        /// </summary>
        public static RectTransform Window(RectTransform screen, string title, Vector2 maxSize, Action onClose, Action onBack, out RectTransform frame)
        {
            var dim = Ui.Rect("Dimmer", screen);
            Ui.Stretch(dim);
            Ui.Background(dim, SdTheme.Dimmer);

            // As large as asked, but never under the HUD row and never past the screen's sides.
            var available = screen.rect.size;
            var size = new Vector2(Mathf.Min(maxSize.x, available.x - 32f), Mathf.Min(maxSize.y, available.y - SdTheme.HudBand - 16f));
            frame = Ui.Rect("Frame", screen);
            frame.anchorMin = frame.anchorMax = new Vector2(0.5f, 1f);
            frame.pivot = new Vector2(0.5f, 1f);
            frame.sizeDelta = size;
            frame.anchoredPosition = new Vector2(0f, -SdTheme.HudBand - Mathf.Max(0f, (available.y - SdTheme.HudBand - 16f - size.y) * 0.5f));
            HudArt.DropShadow(frame, 12f, 22f, 0.65f);
            Sliced(frame, SdTheme.WindowFrame, true).pixelsPerUnitMultiplier = SdTheme.WindowFrameScale;

            // A faint lacquer lattice inside the brass, so the face is never a flat plane.
            var face = Ui.Rect("Pattern", frame);
            Ui.Stretch(face, 31f, 31f, 31f, 31f);
            var pattern = face.gameObject.AddComponent<RawImage>();
            pattern.texture = HudArt.Sprite(SdTheme.Pattern).texture;
            pattern.texture.wrapMode = TextureWrapMode.Repeat;
            pattern.uvRect = new Rect(0f, 0f, (size.x - 62f) / 64f, (size.y - 62f) / 64f);
            pattern.color = new Color(1f, 1f, 1f, 0.55f);
            pattern.raycastTarget = false;

            Title(frame, title);

            var close = Medal.Create(frame, "CloseButton", MedalKind.Destructive, "close", null, onClose, true, 62f);
            Ui.Anchor((RectTransform)close.transform, new Vector2(1f, 1f), new Vector2(62f, 62f), new Vector2(18f, 18f));
            if (onBack != null)
            {
                var back = Medal.Create(frame, "BackButton", MedalKind.IconOnly, "back", null, onBack, true, 58f);
                Ui.Anchor((RectTransform)back.transform, new Vector2(0f, 1f), new Vector2(58f, 58f), new Vector2(-16f, 16f));
            }

            var content = Ui.Rect("Content", frame);
            Ui.Stretch(content, SdTheme.FrameInset, SdTheme.FrameInset - 6f, SdTheme.FrameInset, SdTheme.FrameInset + 22f);
            UiMotion.PopIn(frame, 0f, 0.96f, SdTheme.OpenSeconds);
            return content;
        }

        /// <summary>The crimson title ribbon, centred over a frame's top edge.</summary>
        public static RectTransform Title(RectTransform frame, string title)
        {
            var ribbon = Ui.Rect("TitleRibbon", frame);
            var width = Mathf.Clamp(120f + title.Length * 22f, 300f, 520f);
            Ui.Anchor(ribbon, new Vector2(0.5f, 1f), new Vector2(width, 60f), new Vector2(0f, 26f));
            Sliced(ribbon, SdTheme.Ribbon);
            var label = HudArt.Label(ribbon, title.ToUpperInvariant(), HudArt.Display, SdTheme.TitleSize, TextAnchor.MiddleCenter, SdTheme.Ivory, SdTheme.CrimsonDark);
            Ui.Stretch(label.rectTransform, 50f, 4f, 50f, 6f);
            return ribbon;
        }

        /// <summary>
        /// A plate button: authored crimson, gold or navy face, its label dynamic. Disabled shows the slate plate,
        /// muted text and, when given, a padlock; it shakes and clicks dully when tapped. Pressing darkens it at once.
        /// </summary>
        public static Button Button(RectTransform parent, string name, string label, SdButtonKind kind, Action onClick, bool interactable, string lockReason = null)
        {
            var rect = Ui.Rect(name, parent);
            var sprite = !interactable ? "sd_btn_slate_s40" : kind == SdButtonKind.Gold ? "sd_btn_gold_s40" : kind == SdButtonKind.Navy ? "sd_btn_navy_s40" : "sd_btn_war_s40";
            var face = Sliced(rect, sprite, true);
            var button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = face;
            button.interactable = interactable;
            var colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1.06f, 1.06f, 1.06f, 1f);
            colors.pressedColor = new Color(0.78f, 0.78f, 0.78f, 1f);
            colors.selectedColor = Color.white;
            colors.disabledColor = Color.white;
            colors.fadeDuration = 0.05f;
            button.colors = colors;

            var ink = kind == SdButtonKind.Gold && interactable ? SdTheme.GoldInk : interactable ? SdTheme.Ivory : SdTheme.IvoryMuted;
            var text = HudArt.Label(rect, label, HudArt.Numbers, SdTheme.ButtonSize, TextAnchor.MiddleCenter, ink, SdTheme.Ink);
            // Dark brown on gold reads by itself; the heavy edge and its shadow would wash it out on the bright face.
            if (kind == SdButtonKind.Gold && interactable) text.SetEdge(Color.clear, EdgeKind.None);
            Ui.Stretch(text.rectTransform, interactable || lockReason == null ? 14f : 40f, 9f, 14f, 13f);
            text.resizeTextForBestFit = true;
            text.resizeTextMinSize = 14;
            text.resizeTextMaxSize = SdTheme.ButtonSize;
            if (!interactable && lockReason != null)
            {
                var padlock = HudArt.Picture(rect, "Lock", "icon_lock", new Vector2(0f, 0.5f), new Vector2(26f, 26f), new Vector2(14f, 2f));
                padlock.preserveAspect = true;
            }

            button.onClick.AddListener(() => GameSounds.Play(GameSound.Click));
            if (onClick != null) button.onClick.AddListener(() => onClick());
            UiMotion.Pressable(button);
            rect.gameObject.AddComponent<DeniedShake>();
            return button;
        }

        /// <summary>Reusable tactile section tabs for roster rarity filters and the hero detail pages.
        /// These use the shared nine-sliced UI art, not runtime-drawn generic rectangles.</summary>
        public static void Tabs(RectTransform parent, IList<WindowTab> tabs, float leftInset = 0f, float rightInset = 0f)
        {
            if (tabs == null || tabs.Count == 0) return;
            var strip = Ui.Rect("SectionTabs", parent);
            strip.anchorMin = new Vector2(0f, 1f);
            strip.anchorMax = new Vector2(1f, 1f);
            strip.pivot = new Vector2(0.5f, 1f);
            strip.offsetMin = new Vector2(leftInset, -52f);
            strip.offsetMax = new Vector2(-rightInset, 0f);
            for (var i = 0; i < tabs.Count; i++)
            {
                var tab = tabs[i];
                var caption = string.IsNullOrEmpty(tab.Count) ? tab.Caption : tab.Caption + "  " + tab.Count;
                var button = Button(strip, tab.Name, caption,
                    tab.Active ? SdButtonKind.Gold : SdButtonKind.Navy, tab.OnClick, true);
                var rect = (RectTransform)button.transform;
                rect.anchorMin = new Vector2(i / (float)tabs.Count, 0f);
                rect.anchorMax = new Vector2((i + 1f) / tabs.Count, 1f);
                rect.offsetMin = new Vector2(i == 0 ? 0f : 5f, 0f);
                rect.offsetMax = new Vector2(i == tabs.Count - 1 ? 0f : -5f, 0f);
            }
        }

        /// <summary>One blue star for each earned evolution; no Roman-numeral emblems or empty slots.</summary>
        public static void EvolutionStars(RectTransform parent, int evolutionCount, Vector2 anchor,
            Vector2 offset, float size = 24f)
        {
            var count = Mathf.Clamp(evolutionCount, 0, 3);
            if (count == 0) return;
            var spacing = size + 3f;
            var strip = Ui.Rect("EvolutionStars", parent);
            Ui.Anchor(strip, anchor, new Vector2(count * spacing + 10f, size + 14f), offset);
            for (var i = 0; i < count; i++)
            {
                var x = (i - (count - 1) * 0.5f) * spacing;
                var glow = Ui.Icon(strip, HeroArt.Glow, new Vector2(0.5f, 0.5f),
                    new Vector2(size + 15f, size + 15f), new Vector2(x, 0f));
                glow.color = new Color(0.21f, 0.72f, 1f, 0.42f);
                glow.raycastTarget = false;
                var star = Ui.Icon(strip, HeroArt.Star, new Vector2(0.5f, 0.5f),
                    new Vector2(size, size), new Vector2(x, 0f));
                star.name = "EvolutionStar";
                star.color = new Color(0.55f, 0.93f, 1f, 1f);
                star.raycastTarget = false;
            }
        }

        /// <summary>A small crimson pill carrying one live fact (stars, a floor, a count), with an optional icon.</summary>
        public static RectTransform Badge(RectTransform parent, string text, string icon)
        {
            var rect = Ui.Rect("Badge", parent);
            Sliced(rect, SdTheme.Badge);
            var label = HudArt.Label(rect, text, HudArt.Numbers, 17, TextAnchor.MiddleCenter, SdTheme.Ivory, SdTheme.Ink);
            Ui.Stretch(label.rectTransform, icon != null ? 30f : 12f, 4f, 12f, 6f);
            if (icon != null)
            {
                var picture = HudArt.Picture(rect, "Icon", icon, new Vector2(0f, 0.5f), new Vector2(24f, 24f), new Vector2(10f, 1f));
                picture.preserveAspect = true;
            }
            return rect;
        }

        /// <summary>A dark lacquer plate so type stays readable over a painting.</summary>
        public static Image Plate(RectTransform rect) => Sliced(rect, SdTheme.PlateDark);

        /// <summary>
        /// A painting that fills its area without stretching: it is cropped to the area's shape, inside the authored
        /// card frame (a brass rim with an open window, laid over the picture).
        /// </summary>
        public static Image FramedPicture(RectTransform area, string sprite, bool locked)
        {
            var mask = Ui.Rect("Picture", area);
            Ui.Stretch(mask, 6f, 6f, 6f, 6f);
            mask.gameObject.AddComponent<RectMask2D>();
            var holder = Ui.Rect("Art", mask);
            Ui.Stretch(holder);
            var image = holder.gameObject.AddComponent<Image>();
            image.sprite = HudArt.Sprite(sprite);
            image.raycastTarget = false;
            image.color = locked ? SdTheme.LockedTint : Color.white;
            var fitter = holder.gameObject.AddComponent<AspectRatioFitter>();
            fitter.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
            fitter.aspectRatio = image.sprite.rect.width / Mathf.Max(1f, image.sprite.rect.height);

            var frame = Ui.Rect("CardFrame", area);
            Ui.Stretch(frame);
            Sliced(frame, SdTheme.CardFrame);
            return image;
        }
    }
}
