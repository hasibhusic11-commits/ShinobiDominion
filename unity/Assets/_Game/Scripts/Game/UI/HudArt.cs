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
            edge.effectDistance = new Vector2(1.6f, -1.6f);
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

        /// <summary>A brass-rimmed lacquer plate, nine-sliced.</summary>
        public static Image Plate(RectTransform rect)
        {
            var image = rect.gameObject.AddComponent<Image>();
            image.sprite = Sprite("plate_s26");
            image.type = Image.Type.Sliced;
            image.pixelsPerUnitMultiplier = 1.6f;
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

        /// <summary>
        /// A round medallion button: shadow, face (the part that darkens when pressed), brass ring, icon, and a
        /// caption under it. Disabled medallions get a charcoal face and a faded icon, and shake when tapped.
        /// </summary>
        public static Button Create(Transform parent, string name, MedalKind kind, string icon, string caption, Action onClick, bool interactable, float size)
        {
            var rect = Ui.Rect(name, parent);
            rect.sizeDelta = new Vector2(size, size);

            var shadow = HudArt.Picture(rect, "Shadow", "shadow", new Vector2(0.5f, 0f), new Vector2(size * 1.05f, size * 0.34f), new Vector2(0f, -size * 0.1f));
            shadow.color = new Color(1f, 1f, 1f, 0.9f);

            var faceRect = Ui.Rect("Face", rect);
            Ui.Stretch(faceRect);
            var face = faceRect.gameObject.AddComponent<Image>();
            face.sprite = HudArt.Sprite(interactable ? FaceFor(kind) : "medal_face_charcoal");

            var ring = HudArt.Picture(rect, "Ring", kind == MedalKind.Attack ? "medal_ring_attack" : kind == MedalKind.IconOnly ? "medal_ring_thin" : "medal_ring",
                new Vector2(0.5f, 0.5f), new Vector2(size, size), Vector2.zero);
            if (!interactable) ring.color = new Color(0.75f, 0.75f, 0.75f, 1f);

            var iconSize = kind == MedalKind.Attack ? size * 0.4f : size * 0.54f;
            var picture = HudArt.Picture(rect, "Icon", "icon_" + icon, new Vector2(0.5f, 0.5f), new Vector2(iconSize, iconSize), new Vector2(0f, kind == MedalKind.Attack ? size * 0.13f : 0f));
            if (!interactable) picture.color = new Color(1f, 1f, 1f, 0.45f);

            if (kind == MedalKind.Attack)
            {
                var word = HudArt.Label(rect, caption, HudArt.Display, Mathf.RoundToInt(size * 0.14f), TextAnchor.MiddleCenter, HudArt.Ivory, HudArt.CrimsonDark);
                Ui.Anchor(word.rectTransform, new Vector2(0.5f, 0f), new Vector2(size * 0.7f, size * 0.2f), new Vector2(0f, size * 0.24f));
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
