using System;
using UnityEngine;
using UnityEngine.UI;
using Text = ShinobiDominion.Game.GameText;

namespace ShinobiDominion.Game
{
    /// <summary>
    /// The parts every list screen is built from (ADR-055, restyled to the owner's mockups in ADR-056): a framed row
    /// with a lead picture or a numbered banner, a title, one line of detail and, on its right, square reward tiles,
    /// stars, a progress bar and at most one button. The row that is open to the player lies on parchment with dark
    /// words; the others are dark plates. A screen says what each row holds; how a row looks is decided here, once.
    /// </summary>
    public static class ScreenKit
    {
        public const float RowHeight = 72f;
        public const float LitRowHeight = 86f;
        private const float Edge = 18f;
        private const float Gap = 8f;

        private static readonly Color Dimmed = new Color(0.62f, 0.62f, 0.68f, 1f);
        private static readonly Color Gold = new Color(1f, 0.87f, 0.45f);
        /// <summary>Words on parchment.</summary>
        public static readonly Color InkBrown = new Color32(0x38, 0x22, 0x10, 0xFF);
        public static readonly Color InkBrownSoft = new Color32(0x5E, 0x46, 0x2A, 0xFF);
        public static readonly Color ParchmentEdge = new Color(1f, 0.95f, 0.8f, 0.55f);

        private static readonly Color PennantTop = new Color(0.8f, 0.22f, 0.16f);
        private static readonly Color PennantBottom = new Color(0.5f, 0.1f, 0.08f);
        private static readonly Color PennantOffTop = new Color(0.36f, 0.38f, 0.45f);
        private static readonly Color PennantOffBottom = new Color(0.2f, 0.21f, 0.27f);

        /// <summary>One row of a list. Parts are added from the right edge inward, in the order they are called.</summary>
        public sealed class Row
        {
            public RectTransform Root;
            public Image Plate;
            public Text Title;
            public Text Detail;
            public bool Lit;
            internal Image Banner;
            private float _right = Edge;

            internal Row(RectTransform root, Image plate, bool lit)
            {
                Root = root;
                Plate = plate;
                Lit = lit;
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
            public Row Button(string name, string caption, Action onClick, bool enabled = true, Color? color = null, float width = 160f)
            {
                var button = Ui.Button(Root, name, caption, onClick, enabled, color, 20);
                var rect = (RectTransform)button.transform;
                var layout = button.GetComponent<LayoutElement>();
                if (layout != null) layout.ignoreLayout = true;
                Ui.Anchor(rect, new Vector2(1f, 0.5f), new Vector2(width, 54f), new Vector2(-_right, 0f));
                _right += width + Gap + 4f;
                FitText();
                return this;
            }

            /// <summary>The button of a row that is not open yet: it cannot be pressed, and carries a lock in place of a word.</summary>
            public Row Locked(string name, float width = 160f)
            {
                Button(name, "", null, false, null, width);
                var button = Root.Find(name);
                var word = button.GetComponentInChildren<Text>();
                if (word != null) word.gameObject.SetActive(false);
                var locked = HudArt.Picture(button, "Lock", "icon_lock", new Vector2(0.5f, 0.5f), new Vector2(30f, 30f), new Vector2(0f, 2f));
                locked.preserveAspect = true;
                locked.color = new Color(0.72f, 0.7f, 0.66f);
                return this;
            }

            /// <summary>An amount of something as a square tile: its picture, with the amount across the foot.</summary>
            public Row Reward(string icon, string amount)
            {
                Tile(Slot("Reward_" + icon, 54f, 54f), icon, amount);
                return this;
            }

            /// <summary>Three stars, the earned ones lit.</summary>
            public Row Stars(int earned)
            {
                var slot = Slot("Stars", 110f, 34f);
                for (var i = 0; i < 3; i++)
                {
                    var star = HudArt.Picture(slot, "Star" + (i + 1), "icon_star", new Vector2(0f, 0.5f), new Vector2(32f, 32f), new Vector2(i * 37f + 2f, 0f));
                    star.preserveAspect = true;
                    star.color = i < earned ? Color.white : (Lit ? new Color(0.36f, 0.27f, 0.16f, 0.85f) : new Color(0.16f, 0.16f, 0.2f, 0.95f));
                }
                // A little air between the stars and what stands to their right.
                _right += 10f;
                return this;
            }

            /// <summary>A bar with its count written on it: 3 / 5.</summary>
            public Row Progress(long value, long target)
            {
                var slot = Slot("Progress", 190f, 26f);
                var back = slot.gameObject.AddComponent<Image>();
                back.sprite = HudArt.Sprite("track_s8");
                back.type = Image.Type.Sliced;
                back.color = new Color(0.1f, 0.11f, 0.18f, 1f);
                back.raycastTarget = false;
                var share = target <= 0 ? 1f : Mathf.Clamp01((float)value / target);
                if (share > 0f)
                {
                    var fill = Ui.Rect("Fill", slot);
                    fill.anchorMin = Vector2.zero;
                    fill.anchorMax = new Vector2(Mathf.Max(share, 0.06f), 1f);
                    fill.offsetMin = new Vector2(2f, 2f);
                    fill.offsetMax = new Vector2(-2f, -2f);
                    var image = fill.gameObject.AddComponent<Image>();
                    image.sprite = HudArt.Sprite("fill_s8");
                    image.type = Image.Type.Sliced;
                    image.color = share >= 1f ? new Color(1f, 0.8f, 0.25f) : new Color(0.95f, 0.7f, 0.22f);
                    image.raycastTarget = false;
                }
                var count = HudArt.Label(slot, $"{Math.Min(value, target):N0} / {target:N0}", HudArt.Numbers, 17, TextAnchor.MiddleCenter, HudArt.Ivory, HudArt.LacquerDeep);
                count.name = "ProgressCount";
                Ui.Stretch(count.rectTransform);
                _right += 8f;
                return this;
            }

            /// <summary>A small picture that says the row's state at a glance: a tick.</summary>
            public Row Mark(string icon, Color? tint = null)
            {
                var slot = Slot("Mark_" + icon, 40f, 40f);
                var image = HudArt.Picture(slot, "Icon", icon, new Vector2(0.5f, 0.5f), new Vector2(34f, 34f), Vector2.zero);
                image.preserveAspect = true;
                if (tint.HasValue) image.color = tint.Value;
                return this;
            }

            /// <summary>A few words on the right where a button would be: Claimed, All stars won.</summary>
            public Row Note(string words, Color? color = null)
            {
                var slot = Slot("Note", Mathf.Max(96f, words.Length * 11f + 16f), 40f);
                var label = Lit
                    ? HudArt.Label(slot, words, HudArt.Numbers, 18, TextAnchor.MiddleRight, color ?? InkBrownSoft, ParchmentEdge)
                    : HudArt.Label(slot, words, HudArt.Numbers, 18, TextAnchor.MiddleRight, color ?? HudArt.IvoryMuted, HudArt.LacquerDeep);
                Ui.Stretch(label.rectTransform);
                return this;
            }

            /// <summary>A row the player cannot use yet: everything on it is drawn back.</summary>
            public Row Dim()
            {
                Plate.color = Dimmed;
                if (Title != null && !Lit) Title.color = HudArt.IvoryMuted;
                if (Banner != null) Banner.sprite = UiSkin.Pennant(PennantOffTop, PennantOffBottom, new Color(0.62f, 0.6f, 0.56f));
                return this;
            }
        }

        /// <summary>
        /// A row whose lead is one of the kit's pictures, or a numbered banner when <paramref name="lead"/> is digits
        /// (a level, a floor). <paramref name="lit"/> lays the row on parchment: the row that is open to the player.
        /// </summary>
        public static Row AddRow(PanelView panel, string name, string lead, string title, string detail = null, bool lit = false, float height = 0f)
        {
            if (height <= 0f) height = lit ? LitRowHeight : RowHeight;
            return Fill(panel.Block(height), name, lead, title, detail, lit);
        }

        /// <summary>The same row, placed by hand in a window: across its full width, a given way down from its top.</summary>
        public static Row AddRowAt(RectTransform parent, float y, string name, string lead, string title, string detail = null, bool lit = false, float height = 0f)
        {
            if (height <= 0f) height = lit ? LitRowHeight : RowHeight;
            var root = Ui.Rect(name, parent);
            root.anchorMin = new Vector2(0f, 1f);
            root.anchorMax = new Vector2(1f, 1f);
            root.pivot = new Vector2(0.5f, 1f);
            root.sizeDelta = new Vector2(0f, height);
            root.anchoredPosition = new Vector2(0f, y);
            return Fill(root, name, lead, title, detail, lit);
        }

        private static Row Fill(RectTransform root, string name, string lead, string title, string detail, bool lit)
        {
            root.name = name;
            var plate = root.gameObject.AddComponent<Image>();
            plate.sprite = HudArt.Sprite(lit ? "parchment_s40" : "plate_s30");
            plate.type = Image.Type.Sliced;
            plate.pixelsPerUnitMultiplier = lit ? 1.7f : 1.5f;
            plate.raycastTarget = false;
            var row = new Row(root, plate, lit);

            const float medal = 52f;
            var left = Edge + medal + 16f;
            if (string.IsNullOrEmpty(lead)) left = Edge + 8f;
            else if (int.TryParse(lead, out _))
            {
                // A banner that hangs from the top of the row and ends a little below its middle.
                var banner = Ui.Rect("Lead", root);
                Ui.Anchor(banner, new Vector2(0f, 1f), new Vector2(50f, 62f), new Vector2(Edge + 4f, 4f));
                row.Banner = banner.gameObject.AddComponent<Image>();
                row.Banner.sprite = UiSkin.Pennant(PennantTop, PennantBottom, new Color(0.86f, 0.68f, 0.3f));
                row.Banner.raycastTarget = false;
                var number = HudArt.Label(banner, lead, HudArt.Display, 25, TextAnchor.MiddleCenter, HudArt.Ivory, HudArt.CrimsonDark);
                Ui.Stretch(number.rectTransform, 6f, 16f, 6f, 6f);
                number.resizeTextForBestFit = true;
                number.resizeTextMinSize = 13;
                number.resizeTextMaxSize = 25;
            }
            else
            {
                var disc = HudArt.Picture(root, "Lead", "medal_face_lacquer", new Vector2(0f, 0.5f), new Vector2(medal, medal), new Vector2(Edge, 0f));
                disc.preserveAspect = true;
                var icon = HudArt.Picture(disc.transform, "Icon", lead, new Vector2(0.5f, 0.5f), new Vector2(medal * 0.6f, medal * 0.6f), Vector2.zero);
                icon.preserveAspect = true;
            }

            var hasDetail = !string.IsNullOrEmpty(detail);
            row.Title = lit
                ? HudArt.Label(root, title, HudArt.Display, 23, TextAnchor.MiddleLeft, InkBrown, ParchmentEdge)
                : HudArt.Label(root, title, HudArt.Display, 21, TextAnchor.MiddleLeft, HudArt.BrassLight, HudArt.LacquerDeep);
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
            row.Title.resizeTextMaxSize = lit ? 23 : 21;
            if (hasDetail)
            {
                row.Detail = Ui.Label(root, detail, 16, TextAnchor.MiddleLeft, lit ? InkBrownSoft : HudArt.IvoryMuted);
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

        /// <summary>A square reward tile: the picture, with the amount across its foot.</summary>
        public static void Tile(RectTransform rect, string icon, string amount)
        {
            var well = rect.gameObject.AddComponent<Image>();
            well.sprite = HudArt.Sprite("well_s18");
            well.type = Image.Type.Sliced;
            well.pixelsPerUnitMultiplier = 1.5f;
            well.raycastTarget = false;
            var picture = HudArt.Picture(rect, "Icon", icon, new Vector2(0.5f, 1f), new Vector2(34f, 34f), new Vector2(0f, -4f));
            picture.preserveAspect = true;
            var label = HudArt.Label(rect, amount, HudArt.Numbers, 15, TextAnchor.LowerRight, HudArt.Ivory, HudArt.LacquerDeep);
            label.name = "Amount";
            Ui.Stretch(label.rectTransform, 2f, 1f, 5f, 0f);
            label.horizontalOverflow = HorizontalWrapMode.Overflow;
            label.resizeTextForBestFit = true;
            label.resizeTextMinSize = 10;
            label.resizeTextMaxSize = 15;
        }

        /// <summary>A picture and an amount side by side in a dark well, for a result or a gift.</summary>
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

        /// <summary>
        /// One wide button in the middle of the screen, for a screen's main action: Fight. Its word is in the display
        /// face, with a picture before it when one is given.
        /// </summary>
        public static Button Feature(PanelView panel, string name, string caption, Action onClick, bool enabled, Color? color, float width = 340f, float height = 70f, string icon = null)
        {
            var block = panel.Block(height + 14f);
            var button = Ui.Button(block, name, caption, onClick, enabled, color, 28);
            Ui.Anchor((RectTransform)button.transform, new Vector2(0.5f, 0.5f), new Vector2(width, height), Vector2.zero);
            var word = button.GetComponentInChildren<Text>();
            if (word != null)
            {
                word.font = HudArt.Display;
                word.fontSize = Mathf.RoundToInt(height * 0.42f);
            }
            if (!string.IsNullOrEmpty(icon))
            {
                var picture = HudArt.Picture(button.transform, "Icon", icon, new Vector2(0.5f, 0.5f), new Vector2(height * 0.5f, height * 0.5f), new Vector2(-(caption.Length * height * 0.15f + height * 0.4f), 3f));
                picture.preserveAspect = true;
            }
            return button;
        }

        /// <summary>A short line in the display face, to lead what follows: a building's level.</summary>
        public static Text Heading(PanelView panel, string words)
        {
            var block = panel.Block(32f);
            block.name = "Heading";
            var label = HudArt.Label(block, words, HudArt.Display, 22, TextAnchor.MiddleLeft, Gold, HudArt.LacquerDeep);
            label.name = "HeadingText";
            Ui.Stretch(label.rectTransform);
            return label;
        }

        /// <summary>One stat: its name on the left, its value on the right, and after an arrow what the next level makes it.</summary>
        public static void StatLine(PanelView panel, string name, string now, string next)
        {
            var block = panel.Block(28f);
            block.name = "Stat_" + name;
            var label = Ui.Label(block, name, 18, TextAnchor.MiddleLeft, HudArt.IvoryMuted);
            label.rectTransform.anchorMin = Vector2.zero;
            label.rectTransform.anchorMax = new Vector2(0.52f, 1f);
            label.rectTransform.offsetMin = Vector2.zero;
            label.rectTransform.offsetMax = Vector2.zero;
            label.horizontalOverflow = HorizontalWrapMode.Overflow;
            label.resizeTextForBestFit = true;
            label.resizeTextMinSize = 12;
            label.resizeTextMaxSize = 18;
            var hasNext = !string.IsNullOrEmpty(next);
            var value = HudArt.Label(block, now, HudArt.Numbers, 19, TextAnchor.MiddleRight, HudArt.Ivory, HudArt.LacquerDeep);
            value.name = "Now";
            value.rectTransform.anchorMin = new Vector2(0.52f, 0f);
            value.rectTransform.anchorMax = new Vector2(hasNext ? 0.74f : 1f, 1f);
            value.rectTransform.offsetMin = Vector2.zero;
            value.rectTransform.offsetMax = Vector2.zero;
            value.horizontalOverflow = HorizontalWrapMode.Overflow;
            if (!hasNext) return;
            var arrow = HudArt.Picture(block, "Arrow", "icon_next", new Vector2(0.79f, 0.5f), new Vector2(15f, 15f), Vector2.zero);
            arrow.preserveAspect = true;
            arrow.color = new Color(0.55f, 0.92f, 0.45f);
            var after = HudArt.Label(block, next, HudArt.Numbers, 19, TextAnchor.MiddleRight, new Color(0.62f, 0.95f, 0.5f), HudArt.LacquerDeep);
            after.name = "Next";
            after.rectTransform.anchorMin = new Vector2(0.82f, 0f);
            after.rectTransform.anchorMax = Vector2.one;
            after.rectTransform.offsetMin = Vector2.zero;
            after.rectTransform.offsetMax = Vector2.zero;
            after.horizontalOverflow = HorizontalWrapMode.Overflow;
        }

        /// <summary>A button that carries what it costs or gives as a picture and an amount on its right, not in its words.</summary>
        public static Button CostButton(PanelView panel, string name, string caption, string icon, string amount, Action onClick, bool enabled, Color? color = null)
        {
            var button = panel.Action(name, caption, onClick, enabled, color, 22);
            AttachCost(button, icon, amount);
            return button;
        }

        /// <summary>Puts a picture and an amount on the right of a button, and centres its word in the room that leaves.</summary>
        public static void AttachCost(Button button, string icon, string amount)
        {
            var wide = 54f + amount.Length * 11f;
            var chip = Ui.Rect("Cost", button.transform);
            Ui.Anchor(chip, new Vector2(1f, 0.5f), new Vector2(wide, 38f), new Vector2(-12f, 2f));
            Chip(chip, icon, amount);
            var word = button.GetComponentInChildren<Text>();
            if (word != null) word.rectTransform.offsetMax = new Vector2(-(wide + 18f), word.rectTransform.offsetMax.y);
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

        /// <summary>A strip of parchment across a window, for the few facts a result or a gift is about.</summary>
        public static RectTransform Parchment(RectTransform parent, Vector2 anchor, Vector2 size, Vector2 offset)
        {
            var strip = Ui.Rect("Parchment", parent);
            Ui.Anchor(strip, anchor, size, offset);
            var image = strip.gameObject.AddComponent<Image>();
            image.sprite = HudArt.Sprite("parchment_s40");
            image.type = Image.Type.Sliced;
            image.pixelsPerUnitMultiplier = 1.6f;
            image.raycastTarget = false;
            return strip;
        }

        /// <summary>One fact on parchment: a picture, a small name and a large value, in dark ink.</summary>
        public static void Fact(RectTransform strip, float fromLeft, float toRight, string icon, string name, string value, Color? valueColor = null)
        {
            var cell = Ui.Rect("Fact_" + name, strip);
            cell.anchorMin = new Vector2(fromLeft, 0f);
            cell.anchorMax = new Vector2(toRight, 1f);
            cell.offsetMin = new Vector2(20f, 8f);
            cell.offsetMax = new Vector2(-24f, -8f);
            var picture = HudArt.Picture(cell, "Icon", icon, new Vector2(0f, 0.5f), new Vector2(36f, 36f), Vector2.zero);
            picture.preserveAspect = true;
            picture.color = InkBrown;
            var label = Ui.Label(cell, name, 17, TextAnchor.MiddleLeft, InkBrownSoft);
            label.rectTransform.anchorMin = Vector2.zero;
            label.rectTransform.anchorMax = new Vector2(0.5f, 1f);
            label.rectTransform.offsetMin = new Vector2(46f, 0f);
            label.rectTransform.offsetMax = Vector2.zero;
            label.horizontalOverflow = HorizontalWrapMode.Overflow;
            label.resizeTextForBestFit = true;
            label.resizeTextMinSize = 12;
            label.resizeTextMaxSize = 17;
            var number = HudArt.Label(cell, value, HudArt.Display, 28, TextAnchor.MiddleRight, valueColor ?? InkBrown, ParchmentEdge);
            number.name = "FactValue";
            number.rectTransform.anchorMin = new Vector2(0.5f, 0f);
            number.rectTransform.anchorMax = Vector2.one;
            number.rectTransform.offsetMin = Vector2.zero;
            number.rectTransform.offsetMax = Vector2.zero;
            number.horizontalOverflow = HorizontalWrapMode.Overflow;
            number.resizeTextForBestFit = true;
            number.resizeTextMinSize = 14;
            number.resizeTextMaxSize = 28;
        }
    }
}
