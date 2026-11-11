using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;

namespace ShinobiDominion.Game
{
    /// <summary>
    /// Every word on screen (ADR-054). Text is drawn from a distance field, so its edges stay sharp at any size and on
    /// any screen, and its outline and drop shadow are part of the letter's own shader: one quad per letter, where
    /// the old outline and shadow components drew ten.
    ///
    /// It keeps the names the screens already use (alignment as a TextAnchor, best fit, wrap and overflow), so a
    /// screen is written the same way as before. Files reach it as <c>Text</c> through a using alias.
    /// </summary>
    [AddComponentMenu("")]
    public sealed class GameText : TextMeshProUGUI
    {
        private Font _font;
        private Color _edgeColor;
        private EdgeKind _edge;

        /// <summary>The bundled font this text is set in. Its distance-field copy is made once, on first use.</summary>
        public new Font font
        {
            get => _font;
            set
            {
                _font = value;
                var asset = GameFonts.For(value);
                if (asset == null) return;
                base.font = asset;
                ApplyEdge();
            }
        }

        public new int fontSize
        {
            get => Mathf.RoundToInt(base.fontSize);
            set => base.fontSize = value;
        }

        public new TextAnchor alignment
        {
            get => ToAnchor(base.alignment);
            set => base.alignment = ToOptions(value);
        }

        public new FontStyle fontStyle
        {
            get => (base.fontStyle & FontStyles.Bold) != 0
                ? ((base.fontStyle & FontStyles.Italic) != 0 ? FontStyle.BoldAndItalic : FontStyle.Bold)
                : ((base.fontStyle & FontStyles.Italic) != 0 ? FontStyle.Italic : FontStyle.Normal);
            set
            {
                var style = base.fontStyle & ~(FontStyles.Bold | FontStyles.Italic);
                if (value == FontStyle.Bold || value == FontStyle.BoldAndItalic) style |= FontStyles.Bold;
                if (value == FontStyle.Italic || value == FontStyle.BoldAndItalic) style |= FontStyles.Italic;
                base.fontStyle = style;
            }
        }

        /// <summary>Shrinks the text to fit its box, between the two sizes.</summary>
        public bool resizeTextForBestFit
        {
            get => enableAutoSizing;
            set
            {
                if (value && !enableAutoSizing)
                {
                    fontSizeMax = base.fontSize;
                    fontSizeMin = Mathf.Min(fontSizeMin, base.fontSize);
                }
                enableAutoSizing = value;
            }
        }

        public int resizeTextMinSize
        {
            get => Mathf.RoundToInt(fontSizeMin);
            set => fontSizeMin = value;
        }

        public int resizeTextMaxSize
        {
            get => Mathf.RoundToInt(fontSizeMax);
            set => fontSizeMax = value;
        }

        public HorizontalWrapMode horizontalOverflow
        {
            get => textWrappingMode == TextWrappingModes.NoWrap ? HorizontalWrapMode.Overflow : HorizontalWrapMode.Wrap;
            set => textWrappingMode = value == HorizontalWrapMode.Wrap ? TextWrappingModes.Normal : TextWrappingModes.NoWrap;
        }

        public VerticalWrapMode verticalOverflow
        {
            get => overflowMode == TextOverflowModes.Truncate ? VerticalWrapMode.Truncate : VerticalWrapMode.Overflow;
            set => overflowMode = value == VerticalWrapMode.Truncate ? TextOverflowModes.Truncate : TextOverflowModes.Overflow;
        }

        /// <summary>False for anything a player typed, so tags in it are shown as written and never obeyed.</summary>
        public bool supportRichText
        {
            get => richText;
            set => richText = value;
        }

        /// <summary>A dark edge around every letter, with or without a drop shadow under it.</summary>
        public void SetEdge(Color color, EdgeKind kind)
        {
            _edgeColor = color;
            _edge = kind;
            ApplyEdge();
        }

        private void ApplyEdge()
        {
            if (base.font == null) return;
            fontSharedMaterial = GameFonts.Material(base.font, _edge, _edgeColor);
        }

        private static TextAlignmentOptions ToOptions(TextAnchor anchor)
        {
            switch (anchor)
            {
                case TextAnchor.UpperLeft: return TextAlignmentOptions.TopLeft;
                case TextAnchor.UpperCenter: return TextAlignmentOptions.Top;
                case TextAnchor.UpperRight: return TextAlignmentOptions.TopRight;
                case TextAnchor.MiddleLeft: return TextAlignmentOptions.Left;
                case TextAnchor.MiddleCenter: return TextAlignmentOptions.Center;
                case TextAnchor.MiddleRight: return TextAlignmentOptions.Right;
                case TextAnchor.LowerLeft: return TextAlignmentOptions.BottomLeft;
                case TextAnchor.LowerCenter: return TextAlignmentOptions.Bottom;
                default: return TextAlignmentOptions.BottomRight;
            }
        }

        private static TextAnchor ToAnchor(TextAlignmentOptions options)
        {
            var column = (options & (TextAlignmentOptions)HorizontalAlignmentOptions.Right) != 0 ? 2
                : (options & (TextAlignmentOptions)HorizontalAlignmentOptions.Center) != 0 ? 1 : 0;
            var row = (options & (TextAlignmentOptions)VerticalAlignmentOptions.Bottom) != 0 ? 2
                : (options & (TextAlignmentOptions)VerticalAlignmentOptions.Middle) != 0 ? 1 : 0;
            return (TextAnchor)(row * 3 + column);
        }
    }

    /// <summary>How a text's letters are edged.</summary>
    public enum EdgeKind
    {
        /// <summary>Plain letters.</summary>
        None,
        /// <summary>A thin dark edge, for text on panels.</summary>
        Thin,
        /// <summary>A heavier edge and a drop shadow, for text over the village and over art.</summary>
        Heavy
    }

    /// <summary>
    /// The distance-field copies of the bundled fonts, and the few materials every text shares. Made in code on first
    /// use, like the rest of the game's look: there is no font asset to keep in step with the font file.
    /// </summary>
    public static class GameFonts
    {
        /// <summary>The size letters are drawn into the atlas at, and the room kept around each for the edge and shadow.</summary>
        private const int SamplingSize = 64;
        private const int Padding = 16;
        private const int AtlasSize = 2048;

        private static readonly Dictionary<Font, TMP_FontAsset> Assets = new Dictionary<Font, TMP_FontAsset>();
        private static readonly Dictionary<(int, EdgeKind, Color32), Material> Materials = new Dictionary<(int, EdgeKind, Color32), Material>();
        private static readonly Dictionary<(int, Color32), Material> WorldMaterials = new Dictionary<(int, Color32), Material>();

        /// <summary>How many edge materials exist: a handful, however much text is on screen.</summary>
        public static int MaterialCount => Materials.Count;

        public static TMP_FontAsset For(Font font)
        {
            if (font == null) return null;
            if (Assets.TryGetValue(font, out var asset) && asset != null) return asset;
            asset = TMP_FontAsset.CreateFontAsset(font, SamplingSize, Padding, GlyphRenderMode.SDFAA, AtlasSize, AtlasSize, AtlasPopulationMode.Dynamic, true);
            if (asset == null)
            {
                Debug.LogWarning($"No distance-field font could be made from {font.name}.");
                return null;
            }
            asset.name = font.name + " SDF";
            asset.hideFlags = HideFlags.DontSave;
            // Weight comes from each material, so a bold word is not also spread out or thickened a second time.
            asset.boldSpacing = 0f;
            asset.boldStyle = 0f;
            asset.normalStyle = 0f;
            // The letters every screen uses, drawn up front so the first frame of a screen does not pay for them.
            asset.TryAddCharacters(" 0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz.,:;!?'\"()+-/%&*#=<>[]_@$");
            Assets[font] = asset;
            // A letter one font lacks is drawn from the others.
            foreach (var other in Assets.Values)
            {
                if (other == asset) continue;
                if (asset.fallbackFontAssetTable == null) asset.fallbackFontAssetTable = new List<TMP_FontAsset>();
                if (!asset.fallbackFontAssetTable.Contains(other)) asset.fallbackFontAssetTable.Add(other);
                if (other.fallbackFontAssetTable == null) other.fallbackFontAssetTable = new List<TMP_FontAsset>();
                if (!other.fallbackFontAssetTable.Contains(asset)) other.fallbackFontAssetTable.Add(asset);
            }
            return asset;
        }

        /// <summary>The shared material for a font with an edge of a kind and colour. Texts that match draw in one batch.</summary>
        public static Material Material(TMP_FontAsset asset, EdgeKind kind, Color edge)
        {
            if (kind == EdgeKind.None) return asset.material;
            var key = (asset.GetInstanceID(), kind, (Color32)edge);
            if (Materials.TryGetValue(key, out var material) && material != null) return material;
            material = new Material(asset.material) { name = $"{asset.name} {kind}", hideFlags = HideFlags.DontSave };
            var heavy = kind == EdgeKind.Heavy;
            material.EnableKeyword(ShaderUtilities.Keyword_Outline);
            material.SetColor(ShaderUtilities.ID_OutlineColor, edge);
            material.SetFloat(ShaderUtilities.ID_OutlineWidth, heavy ? 0.2f : 0.13f);
            // The edge straddles the letter's outline, so the face is widened to keep the letter its own weight.
            material.SetFloat(ShaderUtilities.ID_FaceDilate, heavy ? 0.17f : 0.11f);
            if (heavy)
            {
                material.EnableKeyword(ShaderUtilities.Keyword_Underlay);
                material.SetColor(ShaderUtilities.ID_UnderlayColor, new Color(0f, 0f, 0f, 0.55f));
                material.SetFloat(ShaderUtilities.ID_UnderlayOffsetX, 0f);
                material.SetFloat(ShaderUtilities.ID_UnderlayOffsetY, -0.4f);
                material.SetFloat(ShaderUtilities.ID_UnderlayDilate, 0.22f);
                material.SetFloat(ShaderUtilities.ID_UnderlaySoftness, 0.2f);
            }
            // Fits the edge, the widened face and the shadow inside the room each letter has in the atlas.
            ShaderUtilities.UpdateShaderRatios(material);
            Materials[key] = material;
            return material;
        }

        /// <summary>
        /// The heavy edge for text that floats in the world (damage numbers). It ignores depth, so a number is never
        /// hidden behind the hero or building it rose from.
        /// </summary>
        public static Material WorldMaterial(TMP_FontAsset asset, Color edge)
        {
            var key = (asset.GetInstanceID(), (Color32)edge);
            if (WorldMaterials.TryGetValue(key, out var material) && material != null) return material;
            material = new Material(Material(asset, EdgeKind.Heavy, edge)) { name = asset.name + " World", hideFlags = HideFlags.DontSave };
            material.SetInt("unity_GUIZTestMode", (int)UnityEngine.Rendering.CompareFunction.Always);
            WorldMaterials[key] = material;
            return material;
        }
    }
}
