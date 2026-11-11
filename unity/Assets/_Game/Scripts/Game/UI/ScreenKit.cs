using System;
using UnityEngine;
using UnityEngine.UI;
using Text = ShinobiDominion.Game.GameText;

namespace ShinobiDominion.Game
{
    /// <summary>
    /// The parts every list screen is built from (ADR-055): a framed row with a lead picture, a title, one line of
    /// detail and, on its right, reward chips, stars, a progress bar and at most one button. A screen says what each
    /// row holds; how a row looks is decided here, once, so every screen reads as the same game.
    /// </summary>
    public static class ScreenKit
    {
        public const float RowHeight = 74f;
        private const float Edge = 14f;
        private const float Gap = 10f;

        private static readonly Color Dimmed = new Color(0.62f, 0.62f, 0.68f, 1f);
        private static readonly Color Gold = new Color(1f, 0.87f, 0.45f);

        /// <summary>One row of a list. Parts are added from the right edge inward, in the order they are called.</summary>
        public sealed class Row
        {
            public RectTransform Root;
            public Image Plate;
            public Text Title;
            public Text Detail;
            private float _right = Edge;
            private float _left;

            internal Row(RectTransform root, Image plate, float left)
            {
                Root = root;
                Plate = plate;
                _left = left;
            }

            private RectTransform Slot(string name, float width, float height)
            {
                var rect = Ui.Rect(name, Root);
                Ui.Anchor(rect, new Vector2(1f, 0.5f), new Vector2(width, height), new Vector2(-_right, 0f));
                _right += width + Gap;
                FitText();
                return rect;
            }

            /// <summary>The words keep clear of whatever has been added on the right.</summary>
            private void FitText()
            {
                if (Title != null) Title.rectTransform.offsetMax = new Vector2(-_right, Title.rectTransform.offsetMax.y);
                if (Detail != null) Detail.rectTransform.offsetMax = new Vector2(-_right, Detail.rectTransform.offsetMax.y);
            }

            /// <summary>The row's one action. Its name is what tests and the guide look for.</summary>
            public Row Button(string name, string caption, Action onClick, bool enabled = true, Color? color = null, float width = 168f)
            {
                var button = Ui.Button(Root, name, caption, onClick, enabled, color, 19);
                var rect = (RectTransform)button.transform;
                var layout = button.GetComponent<LayoutElement>();
                if (layout != null) layout.ignoreLayout = true;
                Ui.Anchor(rect, new Vector2(1f, 0.5f), new Vector2(width, 54f), new Vector2(-_right, 0f));
                _right += width + Gap;
                FitText();
                return this;
            }

            /// <summary>An amount of something with its picture: 300 gold, 40 gems.</summary>
            public Row Reward(string icon, string amount)
            {
                Chip(Slot("Reward_" + icon, ChipWidth(amount), 40f), icon, amount);
                return this;
            }

            /// <summary>Three stars, the earned ones lit.</summary>
            public Row Stars(int earned)
            {
                var slot = Slot("Stars", 96f, 30f);
                for (var i = 0; i < 3; i++)
                {
                    var star = HudArt.Picture(slot, "Star" + (i + 1), "icon_star", new Vector2(0f, 0.5f), new Vector2(28f, 28f), new Vector2(i * 32f + 2f, 0f));
                    star.preserveAspect = true;
                    star.color = i < earned ? Color.white : new Color(0.18f, 0.17f, 0.2f, 0.9f);
                }
                return this;
            }

            /// <summary>A bar with its count: 3 of 5.</summary>
            public Row Progress(long value, long target)
            {
                var slot = Slot("Progress", 190f, 40f);
                var track = Ui.Rect("Track", slot);
                Ui.Anchor(track, new Vector2(0f, 0.5f), new Vector2(120f, 16f), Vector2.zero);
                var back = track.gameObject.AddComponent<Image>();
                back.sprite = HudArt.Sprite("track_s8");
                back.type = Image.Type.Sliced;
                back.raycastTarget = false;
                var share = target <= 0 ? 1f : Mathf.Clamp01((float)value / target);
                if (share > 0f)
                {
                    var fill = Ui.Rect("Fill", track);
                    fill.anchorMin = Vector2.zero;
                    fill.anchorMax = new Vector2(Mathf.Max(share, 0.08f), 1f);
                    fill.offsetMin = new Vector2(1f, 1f);
                    fill.offsetMax = new Vector2(-1f, -1f);
                    var image = fill.gameObject.AddComponent<Image>();
                    image.sprite = HudArt.Sprite("fill_s8");
                    image.type = Image.Type.Sliced;
                    image.color = share >= 1f ? new Color(0.45f, 0.9f, 0.35f) : new Color(0.95f, 0.72f, 0.25f);
                    image.raycastTarget = false;
                }
                var count = HudArt.Label(slot, $"{Math.Min(value, target):N0}/{target:N0}", HudArt.Numbers, 18, TextAnchor.MiddleRight, HudArt.Ivory, HudArt.LacquerDeep);
                count.name = "ProgressCount";
                Ui.Stretch(count.rectTransform, 124f, 0f, 0f, 0f);
                return this;
            }

            /// <summary>A small picture that says the row's state at a glance: a lock, a tick.</summary>
            public Row Mark(string icon, Color? tint = null)
            {
                var slot = Slot("Mark_" + icon, 40f, 40f);
                var image = HudArt.Picture(slot, "Icon", icon, new Vector2(0.5f, 0.5f), new Vector2(34f, 34f), Vector2.zero);
                image.preserveAspect = true;
                if (tint.HasValue) image.color = tint.Value;
                return this;
            }

            /// <summary>A few words on the right where a button would be: Claimed, Full.</summary>
            public Row Note(string words, Color? color = null)
            {
                var slot = Slot("Note", Mathf.Max(96f, words.Length * 11f + 16f), 40f);
                var label = HudArt.Label(slot, words, HudArt.Numbers, 18, TextAnchor.MiddleRight, color ?? HudArt.IvoryMuted, HudArt.LacquerDeep);
                Ui.Stretch(label.rectTransform);
                return this;
            }

            /// <summary>A row the player cannot use yet: everything on it is drawn back.</summary>
            public Row Dim()
            {
                Plate.color = Dimmed;
                if (Title != null) Title.color = HudArt.IvoryMuted;
                return this;
            }
        }

        /// <summary>
        /// A row whose lead is one of the kit's pictures, or a number when <paramref name="lead"/> is digits (a level,
        /// a floor, a rank). <paramref name="lit"/> draws the brighter plate, for the row that wants the player's eye.
        /// </summary>
        public static Row AddRow(PanelView panel, string name, string lead, string title, string detail = null, bool lit = false, float height = RowHeight)
        {
            var root = panel.Block(height);
            root.name = name;
            var plate = root.gameObject.AddComponent<Image>();
            plate.sprite = HudArt.Sprite(lit ? "counter_s30" : "plate_s30");
            plate.type = Image.Type.Sliced;
            plate.pixelsPerUnitMultiplier = 1.5f;
            plate.raycastTarget = false;

            const float medal = 54f;
            var left = Edge + medal + 14f;
            if (!string.IsNullOrEmpty(lead))
            {
                var disc = HudArt.Picture(root, "Lead", "medal_face_lacquer", new Vector2(0f, 0.5f), new Vector2(medal, medal), new Vector2(Edge, 0f));
                disc.preserveAspect = true;
                if (int.TryParse(lead, out _))
                {
                    var number = HudArt.Label(disc.transform, lead, HudArt.Display, 24, TextAnchor.MiddleCenter, HudArt.BrassLight, HudArt.LacquerDeep);
                    Ui.Stretch(number.rectTransform, 4f, 4f, 4f, 4f);
                    number.resizeTextForBestFit = true;
                    number.resizeTextMinSize = 13;
                    number.resizeTextMaxSize = 24;
                }
                else
                {
                    var icon = HudArt.Picture(disc.transform, "Icon", lead, new Vector2(0.5f, 0.5f), new Vector2(medal * 0.6f, medal * 0.6f), Vector2.zero);
                    icon.preserveAspect = true;
                }
            }
            else left = Edge + 6f;

            var row = new Row(root, plate, left);
            var hasDetail = !string.IsNullOrEmpty(detail);
            row.Title = HudArt.Label(root, title, HudArt.Display, 21, TextAnchor.MiddleLeft, lit ? Gold : HudArt.BrassLight, HudArt.LacquerDeep);
            row.Title.name = "RowTitle";
            // With a line of detail under it the title sits just above the middle, and the two read as one block.
            row.Title.rectTransform.anchorMin = new Vector2(0f, hasDetail ? 0.44f : 0f);
            row.Title.rectTransform.anchorMax = Vector2.one;
            row.Title.rectTransform.offsetMin = new Vector2(left, hasDetail ? -2f : 8f);
            row.Title.rectTransform.offsetMax = new Vector2(-Edge, hasDetail ? -13f : -8f);
            row.Title.horizontalOverflow = HorizontalWrapMode.Overflow;
            row.Title.verticalOverflow = VerticalWrapMode.Truncate;
            row.Title.resizeTextForBestFit = true;
            row.Title.resizeTextMinSize = 14;
            row.Title.resizeTextMaxSize = 21;
            if (hasDetail)
            {
                row.Detail = Ui.Label(root, detail, 16, TextAnchor.MiddleLeft, HudArt.IvoryMuted);
                row.Detail.name = "RowDetail";
                row.Detail.rectTransform.anchorMin = Vector2.zero;
                row.Detail.rectTransform.anchorMax = new Vector2(1f, 0.44f);
                row.Detail.rectTransform.offsetMin = new Vector2(left, 12f);
                row.Detail.rectTransform.offsetMax = new Vector2(-Edge, 0f);
                row.Detail.horizontalOverflow = HorizontalWrapMode.Overflow;
                row.Detail.verticalOverflow = VerticalWrapMode.Truncate;
                row.Detail.resizeTextForBestFit = true;
                row.Detail.resizeTextMinSize = 12;
                row.Detail.resizeTextMaxSize = 16;
            }
            return row;
        }

        private static float ChipWidth(string amount) => 50f + amount.Length * 11f;

        /// <summary>A picture and an amount in a dark well.</summary>
        public static void Chip(RectTransform rect, string icon, string amount)
        {
            var well = rect.gameObject.AddComponent<Image>();
            well.sprite = HudArt.Sprite("well_s18");
            well.type = Image.Type.Sliced;
            well.pixelsPerUnitMultiplier = 1.4f;
            well.raycastTarget = false;
            var picture = HudArt.Picture(rect, "Icon", icon, new Vector2(0f, 0.5f), new Vector2(28f, 28f), new Vector2(8f, 0f));
            picture.preserveAspect = true;
            var label = HudArt.Label(rect, amount, HudArt.Numbers, 18, TextAnchor.MiddleRight, HudArt.Ivory, HudArt.LacquerDeep);
            label.name = "Amount";
            Ui.Stretch(label.rectTransform, 38f, 0f, 10f, 0f);
        }

        /// <summary>A band of equal tiles, each a picture, a large value and a small name: wins, losses, rank.</summary>
        public static RectTransform Stats(PanelView panel, params (string icon, string value, string name)[] tiles)
        {
            var band = panel.Block(104f);
            band.name = "Stats";
            for (var i = 0; i < tiles.Length; i++)
            {
                var tile = Ui.Rect("Stat" + i, band);
                tile.anchorMin = new Vector2((float)i / tiles.Length, 0f);
                tile.anchorMax = new Vector2((float)(i + 1) / tiles.Length, 1f);
                tile.offsetMin = new Vector2(i == 0 ? 0f : 6f, 0f);
                tile.offsetMax = new Vector2(i == tiles.Length - 1 ? 0f : -6f, 0f);
                var plate = tile.gameObject.AddComponent<Image>();
                plate.sprite = HudArt.Sprite("plate_s30");
                plate.type = Image.Type.Sliced;
                plate.pixelsPerUnitMultiplier = 1.5f;
                plate.raycastTarget = false;
                var picture = HudArt.Picture(tile, "Icon", tiles[i].icon, new Vector2(0f, 0.5f), new Vector2(46f, 46f), new Vector2(22f, 0f));
                picture.preserveAspect = true;
                var value = HudArt.Label(tile, tiles[i].value, HudArt.Display, 30, TextAnchor.LowerLeft, Gold, HudArt.LacquerDeep);
                value.name = "Value";
                value.rectTransform.anchorMin = new Vector2(0f, 0.42f);
                value.rectTransform.anchorMax = Vector2.one;
                value.rectTransform.offsetMin = new Vector2(84f, 0f);
                value.rectTransform.offsetMax = new Vector2(-12f, -10f);
                value.resizeTextForBestFit = true;
                value.resizeTextMinSize = 16;
                value.resizeTextMaxSize = 30;
                var name = Ui.Label(tile, tiles[i].name, 16, TextAnchor.UpperLeft, HudArt.IvoryMuted);
                name.rectTransform.anchorMin = Vector2.zero;
                name.rectTransform.anchorMax = new Vector2(1f, 0.42f);
                name.rectTransform.offsetMin = new Vector2(86f, 10f);
                name.rectTransform.offsetMax = new Vector2(-12f, 0f);
            }
            return band;
        }

        /// <summary>One wide button in the middle of the screen, for a screen's main action: Fight.</summary>
        public static Button Feature(PanelView panel, string name, string caption, Action onClick, bool enabled, Color color, float width = 340f)
        {
            var block = panel.Block(86f);
            var button = Ui.Button(block, name, caption, onClick, enabled, color, 28);
            Ui.Anchor((RectTransform)button.transform, new Vector2(0.5f, 0.5f), new Vector2(width, 70f), Vector2.zero);
            return button;
        }

        /// <summary>A message the player has to see, such as why the last tap was refused: one framed line, not a paragraph.</summary>
        public static void Notice(PanelView panel, string words)
        {
            if (string.IsNullOrEmpty(words)) return;
            var block = panel.Block(44f);
            block.name = "Notice";
            var well = block.gameObject.AddComponent<Image>();
            well.sprite = HudArt.Sprite("well_s18");
            well.type = Image.Type.Sliced;
            well.pixelsPerUnitMultiplier = 1.4f;
            well.raycastTarget = false;
            var picture = HudArt.Picture(block, "Icon", "icon_warning", new Vector2(0f, 0.5f), new Vector2(26f, 26f), new Vector2(12f, 0f));
            picture.preserveAspect = true;
            var label = Ui.Label(block, words, 18, TextAnchor.MiddleLeft, Ui.AccentColor);
            label.name = "NoticeText";
            Ui.Stretch(label.rectTransform, 48f, 0f, 12f, 0f);
            label.verticalOverflow = VerticalWrapMode.Truncate;
            label.resizeTextForBestFit = true;
            label.resizeTextMinSize = 13;
            label.resizeTextMaxSize = 18;
        }
    }
}
