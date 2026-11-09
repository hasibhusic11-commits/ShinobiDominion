using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace ShinobiDominion.Game
{
    /// <summary>The HUD's painted kit (Resources/UI, made by tools/art/ui_kit.py) and its two bundled fonts.</summary>
    public static class HudArt
    {
        public static readonly Color Ivory = new Color32(0xEF, 0xE6, 0xD0, 0xFF);
        public static readonly Color IvoryMuted = new Color32(0xB9, 0xAF, 0x98, 0xFF);
        public static readonly Color BrassLight = new Color32(0xE8, 0xC7, 0x7E, 0xFF);
        public static readonly Color Brass = new Color32(0xB0, 0x8A, 0x47, 0xFF);
        public static readonly Color LacquerDeep = new Color32(0x0C, 0x12, 0x22, 0xFF);
        public static readonly Color CrimsonDark = new Color32(0x4E, 0x13, 0x10, 0xFF);
        public static readonly Color CrimsonLight = new Color32(0xC2, 0x48, 0x3C, 0xFF);

        private static readonly Dictionary<string, Sprite> Sprites = new Dictionary<string, Sprite>();
        private static Font _display;
        private static Font _numbers;

        /// <summary>A sprite from Resources/UI. Missing art logs once and falls back to the plain skin, never to nothing.</summary>
        public static Sprite Sprite(string name)
        {
            if (Sprites.TryGetValue(name, out var sprite) && sprite != null) return sprite;
            sprite = Resources.Load<Sprite>("UI/" + name);
            if (sprite == null)
            {
                Debug.LogWarning($"HUD art missing: UI/{name}");
                sprite = UiSkin.Disc(Color.grey, Color.black, Color.grey);
            }
            Sprites[name] = sprite;
            return sprite;
        }

        /// <summary>Cinzel (SIL OFL) for names and the ATTACK word.</summary>
        public static Font Display => _display != null ? _display : (_display = Resources.Load<Font>("UI/Fonts/Cinzel") ?? Ui.Font);

        /// <summary>Rubik (SIL OFL) for numbers and short labels.</summary>
        public static Font Numbers => _numbers != null ? _numbers : (_numbers = Resources.Load<Font>("UI/Fonts/Rubik") ?? Ui.Font);

        /// <summary>Text with a dark outline and a soft drop shadow, readable over the village.</summary>
        public static Text Label(Transform parent, string content, Font font, int size, TextAnchor alignment, Color color, Color outline)
        {
            var text = Ui.Label(parent, content, size, alignment, color);
            text.font = font;
            text.fontStyle = FontStyle.Bold;
            var edge = text.gameObject.AddComponent<Outline>();
            edge.effectColor = outline;
            edge.effectDistance = new Vector2(2.2f, -2.2f);
            var drop = text.gameObject.AddComponent<Shadow>();
            drop.effectColor = new Color(0f, 0f, 0f, 0.55f);
            drop.effectDistance = new Vector2(0f, -2.5f);
            return text;
        }

        public static Image Picture(Transform parent, string name, string sprite, Vector2 anchor, Vector2 size, Vector2 offset)
        {
            var image = Ui.Icon(parent, Sprite(sprite), anchor, size, offset);
            image.name = name;
            image.raycastTarget = false;
            return image;
        }

        /// <summary>A heavy brass-framed lacquer plate, nine-sliced. Pass "counter_s30" for the deep-blue resource well.</summary>
        public static Image Plate(RectTransform rect, string sprite = "plate_s30")
        {
            var image = rect.gameObject.AddComponent<Image>();
            image.sprite = Sprite(sprite);
            image.type = Image.Type.Sliced;
            image.pixelsPerUnitMultiplier = 1.25f;
            image.raycastTarget = false;
            return image;
        }

        /// <summary>
        /// A soft shadow under a framed part, placed just behind it with the same anchors, a little larger and lower.
        /// Call after the part is positioned. Not for children of layout groups, which would move without it.
        /// </summary>
        public static Image DropShadow(RectTransform target, float down = 7f, float spread = 12f, float alpha = 0.5f)
        {
            var shadow = Ui.Rect("Shadow", target.parent);
            shadow.anchorMin = target.anchorMin;
            shadow.anchorMax = target.anchorMax;
            shadow.pivot = target.pivot;
            shadow.sizeDelta = target.sizeDelta + new Vector2(spread * 2f, spread * 2f);
            shadow.anchoredPosition = target.anchoredPosition + new Vector2((target.pivot.x - 0.5f) * 2f * spread, (target.pivot.y - 0.5f) * 2f * spread - down);
            shadow.SetSiblingIndex(target.GetSiblingIndex());
            var image = shadow.gameObject.AddComponent<Image>();
            image.sprite = Sprite("shadow_s32");
            image.type = Image.Type.Sliced;
            image.color = new Color(1f, 1f, 1f, alpha);
            image.raycastTarget = false;
            return image;
        }
    }

    /// <summary>The button hierarchy from docs/ui/HUD_SPEC.md. Each kind has its own face; all share the brass ring.</summary>
    public enum MedalKind
    {
        Attack,
        Primary,
        Secondary,
        Destructive,
        IconOnly
    }

    public static class Medal
    {
        private static string FaceFor(MedalKind kind)
        {
            switch (kind)
            {
                case MedalKind.Attack: return "medal_face_crimson";
                case MedalKind.Primary: return "medal_face_gold";
                case MedalKind.Destructive: return "medal_face_crimson_small";
                default: return "medal_face_lacquer";
            }
        }

        /// <summary>The owner's painted medallion family (UI pack) for an icon name, or null when it has none.</summary>
        public static string FamilyFor(string icon)
        {
            switch (icon)
            {
                case null: return null;
                default: return Resources.Load<Sprite>($"UI/medal_{icon}_normal") != null ? icon : null;
            }
        }

        /// <summary>
        /// A round medallion button. With a painted family from the owner's UI pack, the picture swaps between
        /// its normal, hover, pressed and disabled paintings (selected uses its own). Without one, it is a round
        /// frame with the icon on top. Captions sit on a small plate under the medallion; ATTACK writes its word
        /// across the bottom. Disabled medallions shake when tapped.
        /// </summary>
        public static Button Create(Transform parent, string name, MedalKind kind, string icon, string caption, Action onClick, bool interactable, float size, bool selected = false)
        {
            // The owner's newer button kit (2026-11-09) paints each button whole, frame and picture together.
            var whole = WholeFor(kind, icon);
            if (whole != null) return Whole(parent, name, kind, whole, kind == MedalKind.Attack ? null : caption, onClick, interactable, size);
            var family = FamilyFor(icon);
            return family != null
                ? Painted(parent, name, kind, family, caption, onClick, interactable, size, selected)
                : Framed(parent, name, kind, icon, caption, onClick, interactable, size, selected);
        }

        /// <summary>
        /// The kit's whole button for this kind and icon: the Attack button, or hud_btn_&lt;icon&gt; for round and close
        /// buttons. Null when the kit has none, so older art is used.
        /// </summary>
        public static string WholeFor(MedalKind kind, string icon)
        {
            string name;
            if (kind == MedalKind.Attack) name = "hud_attack_button";
            else if (string.IsNullOrEmpty(icon) || (kind != MedalKind.IconOnly && kind != MedalKind.Destructive)) return null;
            else name = "hud_btn_" + icon;
            return Resources.Load<Sprite>("UI/" + name) != null ? name : null;
        }

        /// <summary>A button painted whole by the kit. It darkens when pressed and greys out when disabled.</summary>
        private static Button Whole(Transform parent, string name, MedalKind kind, string sprite, string caption, Action onClick, bool interactable, float size)
        {
            var rect = Ui.Rect(name, parent);
            rect.sizeDelta = new Vector2(size, size);
            HudArt.Picture(rect, "Shadow", "shadow", new Vector2(0.5f, 0f), new Vector2(size * 0.95f, size * 0.28f), new Vector2(0f, -size * 0.08f));
            var faceRect = Ui.Rect("Face", rect);
            Ui.Stretch(faceRect);
            var face = faceRect.gameObject.AddComponent<Image>();
            face.sprite = HudArt.Sprite(sprite);
            face.preserveAspect = true;
            if (!interactable) face.color = new Color(0.5f, 0.5f, 0.53f, 1f);
            if (!string.IsNullOrEmpty(caption)) Caption(rect, caption, size, interactable);
            var button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = face;
            button.interactable = interactable;
            var colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1.08f, 1.08f, 1.08f, 1f);
            colors.selectedColor = Color.white;
            colors.pressedColor = new Color(0.78f, 0.78f, 0.78f, 1f);
            colors.disabledColor = Color.white;
            colors.fadeDuration = 0.06f;
            button.colors = colors;
            Finish(button, onClick);
            return button;
        }

        private static Button Painted(Transform parent, string name, MedalKind kind, string family, string caption, Action onClick, bool interactable, float size, bool selected)
        {
            var rect = Ui.Rect(name, parent);
            rect.sizeDelta = new Vector2(size, size);
            if (selected && interactable)
            {
                var halo = HudArt.Picture(rect, "Glow", "medal_halo", new Vector2(0.5f, 0.5f), new Vector2(size * 1.22f, size * 1.22f), Vector2.zero);
                UiMotion.Breathe(halo, 0.95f, 1.8f);
            }
            var shadow = HudArt.Picture(rect, "Shadow", "shadow", new Vector2(0.5f, 0f), new Vector2(size * 1.0f, size * 0.3f), new Vector2(0f, -size * 0.08f));
            shadow.color = new Color(1f, 1f, 1f, 0.85f);

            var faceRect = Ui.Rect("Face", rect);
            Ui.Stretch(faceRect);
            var face = faceRect.gameObject.AddComponent<Image>();
            var normal = HudArt.Sprite($"medal_{family}_{(selected ? "selected" : "normal")}");
            face.sprite = normal;
            face.preserveAspect = true;

            if (kind == MedalKind.Attack)
            {
                var word = HudArt.Label(rect, caption.ToUpperInvariant(), HudArt.Display, Mathf.RoundToInt(size * 0.17f), TextAnchor.MiddleCenter, HudArt.Ivory, HudArt.CrimsonDark);
                Ui.Anchor(word.rectTransform, new Vector2(0.5f, 0f), new Vector2(size * 1.05f, size * 0.24f), new Vector2(0f, size * 0.1f));
            }
            else if (!string.IsNullOrEmpty(caption))
            {
                Caption(rect, caption, size, interactable);
            }

            var button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = face;
            button.interactable = interactable;
            button.transition = Selectable.Transition.SpriteSwap;
            button.spriteState = new SpriteState
            {
                highlightedSprite = HudArt.Sprite($"medal_{family}_hover"),
                pressedSprite = HudArt.Sprite($"medal_{family}_pressed"),
                selectedSprite = normal,
                disabledSprite = HudArt.Sprite($"medal_{family}_disabled")
            };
            Finish(button, onClick);
            return button;
        }

        /// <summary>A caption on a small dark plate under a medallion, as in the owner's mockup.</summary>
        private static void Caption(RectTransform medal, string caption, float size, bool interactable)
        {
            var plate = Ui.Rect("Caption", medal);
            Ui.Anchor(plate, new Vector2(0.5f, 0f), new Vector2(size * 1.12f, Mathf.Max(26f, size * 0.27f)), new Vector2(0f, -size * 0.16f));
            var image = plate.gameObject.AddComponent<Image>();
            image.sprite = HudArt.Sprite("hud_building_nameplate");
            image.raycastTarget = false;
            var label = HudArt.Label(plate, caption, HudArt.Display, Mathf.RoundToInt(Mathf.Clamp(size * 0.17f, 14f, 20f)), TextAnchor.MiddleCenter,
                interactable ? HudArt.Ivory : HudArt.IvoryMuted, HudArt.LacquerDeep);
            Ui.Stretch(label.rectTransform, 10f, 1f, 10f, 0f);
            label.resizeTextForBestFit = true;
            label.resizeTextMinSize = 11;
            label.resizeTextMaxSize = label.fontSize;
        }

        private static void Finish(Button button, Action onClick)
        {
            button.onClick.AddListener(() => GameSounds.Play(GameSound.Click));
            if (onClick != null) button.onClick.AddListener(() => onClick());
            UiMotion.Pressable(button);
            button.gameObject.AddComponent<DeniedShake>();
        }

        /// <summary>A round frame (the pack's small round button) with a painted icon, for buttons with no medallion family.</summary>
        private static Button Framed(Transform parent, string name, MedalKind kind, string icon, string caption, Action onClick, bool interactable, float size, bool selected)
        {
            if (kind != MedalKind.IconOnly) return Classic(parent, name, kind, icon, caption, onClick, interactable, size, selected);
            var rect = Ui.Rect(name, parent);
            rect.sizeDelta = new Vector2(size, size);
            HudArt.Picture(rect, "Shadow", "shadow", new Vector2(0.5f, 0f), new Vector2(size * 1.0f, size * 0.3f), new Vector2(0f, -size * 0.08f));
            var faceRect = Ui.Rect("Face", rect);
            Ui.Stretch(faceRect);
            var face = faceRect.gameObject.AddComponent<Image>();
            face.sprite = HudArt.Sprite("hud_small_round_button");
            if (!string.IsNullOrEmpty(icon))
            {
                var picture = HudArt.Picture(rect, "Icon", "icon_" + icon, new Vector2(0.5f, 0.5f), new Vector2(size * 0.6f, size * 0.6f), Vector2.zero);
                picture.preserveAspect = true;
                if (!interactable) picture.color = new Color(1f, 1f, 1f, 0.45f);
            }
            if (!interactable) face.color = new Color(0.55f, 0.55f, 0.58f, 1f);
            if (!string.IsNullOrEmpty(caption)) Caption(rect, caption, size, interactable);
            var button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = face;
            button.interactable = interactable;
            var colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1.08f, 1.08f, 1.08f, 1f);
            colors.selectedColor = Color.white;
            colors.pressedColor = new Color(0.78f, 0.78f, 0.78f, 1f);
            colors.disabledColor = Color.white;
            colors.fadeDuration = 0.06f;
            button.colors = colors;
            Finish(button, onClick);
            return button;
        }

        /// <summary>The earlier code-drawn medallion, kept for kinds the painted pack does not cover.</summary>
        private static Button Classic(Transform parent, string name, MedalKind kind, string icon, string caption, Action onClick, bool interactable, float size, bool selected)
        {
            var rect = Ui.Rect(name, parent);
            rect.sizeDelta = new Vector2(size, size);
            if (selected && interactable)
            {
                var halo = HudArt.Picture(rect, "Glow", "medal_halo", new Vector2(0.5f, 0.5f), new Vector2(size * 1.22f, size * 1.22f), Vector2.zero);
                UiMotion.Breathe(halo, 0.95f, 1.8f);
            }

            var shadow = HudArt.Picture(rect, "Shadow", "shadow", new Vector2(0.5f, 0f), new Vector2(size * 1.05f, size * 0.34f), new Vector2(0f, -size * 0.1f));
            shadow.color = new Color(1f, 1f, 1f, 0.9f);

            var faceRect = Ui.Rect("Face", rect);
            if (kind == MedalKind.Attack) Ui.Anchor(faceRect, new Vector2(0.5f, 0.5f), new Vector2(size * 0.66f, size * 0.66f), Vector2.zero);
            else Ui.Stretch(faceRect);
            var face = faceRect.gameObject.AddComponent<Image>();
            face.sprite = HudArt.Sprite(interactable ? FaceFor(kind) : "medal_face_charcoal");

            var ring = HudArt.Picture(rect, "Ring", kind == MedalKind.Attack ? "medal_attack_spikes" : kind == MedalKind.IconOnly ? "medal_ring_thin" : "medal_ring",
                new Vector2(0.5f, 0.5f), new Vector2(size, size), Vector2.zero);
            if (!interactable) ring.color = new Color(0.75f, 0.75f, 0.75f, 1f);

            var iconSize = kind == MedalKind.Attack ? size * 0.32f : size * 0.5f;
            if (!string.IsNullOrEmpty(icon))
            {
                var picture = HudArt.Picture(rect, "Icon", "icon_" + icon, new Vector2(0.5f, 0.5f), new Vector2(iconSize, iconSize), new Vector2(0f, kind == MedalKind.Attack ? size * 0.13f : 0f));
                if (!interactable) picture.color = new Color(1f, 1f, 1f, 0.45f);
            }

            if (kind == MedalKind.Attack)
            {
                var word = HudArt.Label(rect, caption, HudArt.Display, Mathf.RoundToInt(size * 0.15f), TextAnchor.MiddleCenter, HudArt.Ivory, HudArt.CrimsonDark);
                Ui.Anchor(word.rectTransform, new Vector2(0.5f, 0f), new Vector2(size * 0.9f, size * 0.2f), new Vector2(0f, size * 0.2f));
            }
            else if (!string.IsNullOrEmpty(caption))
            {
                var label = HudArt.Label(rect, caption, HudArt.Display, 16, TextAnchor.UpperCenter, interactable ? HudArt.Ivory : HudArt.IvoryMuted, HudArt.LacquerDeep);
                // Smaller medallions sit centred on the row, so their caption drops to line up with the others.
                Ui.Anchor(label.rectTransform, new Vector2(0.5f, 0f), new Vector2(size + 40f, 24f), new Vector2(0f, kind == MedalKind.IconOnly ? -38f : -26f));
            }

            var button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = face;
            button.interactable = interactable;
            var colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = Color.white;
            colors.selectedColor = Color.white;
            colors.pressedColor = new Color(0.78f, 0.78f, 0.78f, 1f);
            colors.disabledColor = Color.white;
            colors.fadeDuration = 0.06f;
            button.colors = colors;
            button.onClick.AddListener(() => GameSounds.Play(GameSound.Click));
            if (onClick != null) button.onClick.AddListener(() => onClick());
            UiMotion.Pressable(button);
            rect.gameObject.AddComponent<DeniedShake>();
            return button;
        }
    }

    /// <summary>A disabled button gives a short shake and a dull click when tapped, so it never feels dead.</summary>
    public sealed class DeniedShake : MonoBehaviour, UnityEngine.EventSystems.IPointerClickHandler
    {
        public void OnPointerClick(UnityEngine.EventSystems.PointerEventData eventData)
        {
            var button = GetComponent<Selectable>();
            if (button == null || button.interactable) return;
            GameSounds.Play(GameSound.Click, 0.35f);
            UiMotion.Shake((RectTransform)transform);
        }
    }
}
