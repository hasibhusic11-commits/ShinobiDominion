using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using ShinobiDominion.Core;
using UnityEngine;

namespace ShinobiDominion.Game
{
    /// <summary>What a hero looks like, in one of its forms. Read from hero_designs.json.</summary>
    public sealed class HeroFormDesign
    {
        public string name;
        public string summary;
        public string garment;
        public string headgear;
        public string primary;
        public string secondary;
        public string accent;
        public string weapon;
        public string back;
        public List<string> extras = new List<string>();
    }

    public sealed class HeroDesign
    {
        public string id;
        public string concept;
        public string build = "standard";
        public string skin = "#E0BC9A";
        public string hair = "short";
        public string hairColor = "#2A2420";
        public string headgear = "none";
        public string garment = "tunic";
        public string primary = "#808080";
        public string secondary = "#404040";
        public string accent = "#C0C0C0";
        public string weapon = "none";
        public string back = "none";
        public List<HeroFormDesign> forms = new List<HeroFormDesign>();
    }

    public static class HeroDesigns
    {
        private sealed class Document
        {
            public int schemaVersion;
            public List<HeroDesign> heroes = new List<HeroDesign>();
        }

        private static Dictionary<string, HeroDesign> _byId;

        public static IReadOnlyCollection<HeroDesign> All
        {
            get
            {
                Load();
                return _byId.Values;
            }
        }

        public static HeroDesign Find(string id)
        {
            Load();
            return id != null && _byId.TryGetValue(id, out var design) ? design : null;
        }

        private static void Load()
        {
            if (_byId != null) return;
            var config = Resources.Load<TextAsset>("Config/hero_designs");
            var document = config != null ? JsonConvert.DeserializeObject<Document>(config.text) : new Document();
            _byId = document.heroes.ToDictionary(h => h.id);
        }

        public static Color Parse(string hex)
        {
            return ColorUtility.TryParseHtmlString(hex, out var color) ? color : Color.magenta;
        }
    }

    /// <summary>
    /// Original shinobi characters, built from a kit of parts: a stylised large head for reading at
    /// a distance, and clear outlines per hero. Faces, costumes and symbols are this game's own.
    /// </summary>
    public static partial class BuildingMeshes
    {
        private sealed class HeroKit
        {
            public float Width;
            public float Height;
            public Color Skin;
            public Color Hair;
            public Color Primary;
            public Color Secondary;
            public Color Accent;
            public string HairStyle;
            public string Headgear;
            public string Garment;
            public string Weapon;
            public string Back;
            public HashSet<string> Extras;
            public int Evolution;
        }

        private static HeroKit KitFor(HeroLook hero)
        {
            var design = HeroDesigns.Find(hero.DesignId);
            var form = design != null && design.forms.Count > 0 ? design.forms[Mathf.Clamp(hero.Evolution, 0, design.forms.Count - 1)] : null;
            if (design == null) design = new HeroDesign { primary = "#" + ColorUtility.ToHtmlStringRGB(hero.Color) };

            float width = 1f, height = 1f;
            switch (design.build)
            {
                case "slim": width = 0.86f; height = 1.02f; break;
                case "small": width = 0.86f; height = 0.86f; break;
                case "heavy": width = 1.24f; height = 1.04f; break;
                case "giant": width = 1.42f; height = 1.2f; break;
            }
            string Pick(string over, string fallback) => string.IsNullOrEmpty(over) ? fallback : over;
            return new HeroKit
            {
                Width = width,
                Height = height,
                Skin = HeroDesigns.Parse(design.skin),
                Hair = HeroDesigns.Parse(design.hairColor),
                Primary = HeroDesigns.Parse(Pick(form?.primary, design.primary)),
                Secondary = HeroDesigns.Parse(Pick(form?.secondary, design.secondary)),
                Accent = HeroDesigns.Parse(Pick(form?.accent, design.accent)),
                HairStyle = design.hair,
                Headgear = Pick(form?.headgear, design.headgear),
                Garment = Pick(form?.garment, design.garment),
                Weapon = Pick(form?.weapon, design.weapon),
                Back = Pick(form?.back, design.back),
                Extras = new HashSet<string>(form?.extras ?? new List<string>()),
                Evolution = hero.Evolution
            };
        }

        private static void HeroFigure(MeshBuilder mesh, Vector3 feet, HeroLook hero)
        {
            const float sway = 0.25f;
            var kit = KitFor(hero);
            var h = kit.Height;

            var hip = feet + Vector3.up * 0.3f * h;
            var neck = hip + Vector3.up * 0.34f * h;
            var head = neck + Vector3.up * 0.17f;
            var dark = Color.Lerp(kit.Secondary, Color.black, 0.25f);

            HeroAuraBelow(mesh, feet, kit);
            HeroLegs(mesh, feet, hip, kit, dark, sway);
            HeroTorso(mesh, hip, neck, kit, sway);
            HeroArms(mesh, neck, kit, sway);
            HeroBackItems(mesh, neck, hip, kit);
            HeroHeadParts(mesh, head, kit, sway);
            HeroWeapon(mesh, feet, neck, kit);
            HeroExtras(mesh, feet, neck, head, kit);
        }

        private static void HeroLegs(MeshBuilder mesh, Vector3 feet, Vector3 hip, HeroKit kit, Color dark, float sway)
        {
            var spread = 0.09f * kit.Width;
            foreach (var side in new[] { -spread, spread })
            {
                mesh.Cylinder(feet + new Vector3(side, 0f, 0f), 0.06f * kit.Width, 0.08f * kit.Width, (hip.y - feet.y), 6, kit.Garment == "armor" ? kit.Secondary : dark, ArtSurface.Plain, sway * 0.2f);
                mesh.Box(feet + new Vector3(side, 0f, -0.03f), new Vector3(0.11f, 0.05f, 0.17f) * kit.Width, new Color(0.18f, 0.14f, 0.12f), ArtSurface.Plain, sway * 0.2f);
                if (kit.Garment == "gi" || kit.Garment == "tunic")
                    mesh.Cylinder(feet + new Vector3(side, 0.05f, 0f), 0.072f * kit.Width, 0.07f * kit.Width, 0.05f, 6, kit.Accent, ArtSurface.Plain, sway * 0.2f, 0f, false);
            }
        }

        private static void HeroTorso(MeshBuilder mesh, Vector3 hip, Vector3 neck, HeroKit kit, float sway)
        {
            var w = kit.Width;
            var length = neck.y - hip.y;
            switch (kit.Garment)
            {
                case "robe":
                case "cloak":
                    mesh.Cylinder(hip + Vector3.down * 0.28f * kit.Height, 0.25f * w, 0.17f * w, 0.3f * kit.Height, 8, kit.Primary, ArtSurface.Plain, sway, 0f, false);
                    mesh.Cylinder(hip + Vector3.up * 0.02f, 0.17f * w, 0.15f * w, length - 0.02f, 8, kit.Primary, ArtSurface.Plain, sway);
                    mesh.Cylinder(hip + Vector3.down * 0.28f * kit.Height, 0.255f * w, 0.25f * w, 0.05f, 8, kit.Secondary, ArtSurface.Plain, sway, 0f, false);
                    // A centre panel down the front of the robe.
                    Strip(mesh, neck + new Vector3(0f, -0.02f, -0.155f * w), hip + new Vector3(0f, -0.24f * kit.Height, -0.25f * w), 0.08f * w, Vector3.back, kit.Secondary, ArtSurface.Plain, sway);
                    break;
                case "armor":
                    mesh.Cylinder(hip, 0.18f * w, 0.17f * w, length, 8, kit.Secondary, ArtSurface.Plain, sway);
                    mesh.Frustum(hip + new Vector3(0f, length * 0.28f, -0.03f), 0.34f * w, 0.26f * w, 0.36f * w, 0.28f * w, length * 0.66f, kit.Primary, ArtSurface.Metal, sway);
                    foreach (var side in new[] { -1f, 1f })
                        mesh.Frustum(hip + new Vector3(side * 0.1f * w, -0.16f * kit.Height, -0.02f), 0.14f * w, 0.05f, 0.14f * w, 0.14f, 0.18f * kit.Height, kit.Primary, ArtSurface.Metal, sway);
                    Strip(mesh, hip + new Vector3(-0.12f * w, length * 0.62f, -0.17f * w), hip + new Vector3(0.12f * w, length * 0.62f, -0.17f * w), 0.03f, Vector3.back, kit.Accent, ArtSurface.Plain, sway);
                    break;
                case "gi":
                    mesh.Cylinder(hip + Vector3.down * 0.03f, 0.18f * w, 0.16f * w, length + 0.03f, 8, kit.Primary, ArtSurface.Plain, sway);
                    // Crossed collar.
                    Strip(mesh, neck + new Vector3(0.08f * w, -0.01f, -0.145f * w), hip + new Vector3(-0.05f * w, length * 0.42f, -0.17f * w), 0.05f, Vector3.back, kit.Accent, ArtSurface.Plain, sway);
                    Strip(mesh, neck + new Vector3(-0.08f * w, -0.02f, -0.15f * w), hip + new Vector3(0.05f * w, length * 0.42f, -0.172f * w), 0.05f, Vector3.back, kit.Accent, ArtSurface.Plain, sway);
                    break;
                default: // tunic
                    mesh.Cylinder(hip + Vector3.down * 0.08f, 0.2f * w, 0.15f * w, length + 0.08f, 8, kit.Primary, ArtSurface.Plain, sway);
                    Strip(mesh, neck + new Vector3(0f, -0.02f, -0.15f * w), hip + new Vector3(0f, 0.02f, -0.18f * w), 0.05f * w, Vector3.back, kit.Secondary, ArtSurface.Plain, sway);
                    break;
            }
            // The belt, which every shinobi here wears.
            mesh.Cylinder(hip + Vector3.up * 0.02f, 0.185f * w, 0.18f * w, 0.05f, 8, kit.Secondary, ArtSurface.Plain, sway, 0f, false);
            mesh.Box(hip + new Vector3(0f, 0.015f, -0.18f * w), new Vector3(0.07f, 0.065f, 0.02f), kit.Accent, ArtSurface.Plain, sway);
        }

        private static void HeroArms(MeshBuilder mesh, Vector3 neck, HeroKit kit, float sway)
        {
            var w = kit.Width;
            foreach (var side in new[] { -1f, 1f })
            {
                var shoulder = neck + new Vector3(side * 0.2f * w, -0.03f, 0f);
                var hand = shoulder + new Vector3(side * 0.05f, -0.3f * kit.Height, -0.04f);
                mesh.Rod(shoulder, hand, 0.06f * w, 6, kit.Garment == "robe" || kit.Garment == "cloak" ? kit.Primary : kit.Primary, ArtSurface.Plain, sway);
                mesh.Blob(hand, Vector3.one * 0.055f * w, kit.Skin, ArtSurface.Plain, sway, 0f, 6, 3);
                if (kit.Extras.Contains("bracers"))
                    mesh.Rod(Vector3.Lerp(shoulder, hand, 0.68f), Vector3.Lerp(shoulder, hand, 0.92f), 0.07f * w, 6, kit.Accent, ArtSurface.Metal, sway);
            }
        }

        private static void HeroHeadParts(MeshBuilder mesh, Vector3 head, HeroKit kit, float sway)
        {
            mesh.Cylinder(head + Vector3.down * 0.18f, 0.06f, 0.06f, 0.05f, 6, kit.Skin, ArtSurface.Plain, sway, 0f, false);
            mesh.Blob(head, new Vector3(0.17f, 0.17f, 0.165f), kit.Skin, ArtSurface.Plain, sway, 0.55f, 9, 6);

            var covered = kit.Headgear == "blindfold" || kit.Headgear == "spirit_mask" || kit.Headgear == "beak_mask";
            if (!covered)
            {
                // Large, simple eyes and brows.
                foreach (var side in new[] { -1f, 1f })
                {
                    var eye = head + new Vector3(side * 0.07f, 0f, -0.16f);
                    mesh.Blob(eye, new Vector3(0.024f, 0.034f, 0.012f), new Color(0.09f, 0.08f, 0.10f), ArtSurface.Plain, sway, 0f, 6, 3);
                    Strip(mesh, eye + new Vector3(-0.035f, 0.05f, -0.006f), eye + new Vector3(0.035f, 0.05f + side * -0.008f, -0.006f), 0.012f, Vector3.back, Color.Lerp(kit.Hair, Color.black, 0.3f), ArtSurface.Plain, sway);
                }
            }
            HeroHair(mesh, head, kit, sway);
            HeroHeadgear(mesh, head, kit, sway);
        }

        private static void HeroHair(MeshBuilder mesh, Vector3 head, HeroKit kit, float sway)
        {
            var hair = kit.Hair;
            var top = Color.Lerp(hair, Color.white, 0.12f);
            void Cap() => mesh.Blob(head + new Vector3(0f, 0.065f, 0.03f), new Vector3(0.18f, 0.14f, 0.175f), hair, ArtSurface.Plain, sway, 0f, 9, 5, 0f, 0, top);

            switch (kit.HairStyle)
            {
                case "shaved":
                    break;
                case "spiky":
                    Cap();
                    foreach (var spike in new[] { new Vector3(-0.12f, 0.13f, 0.02f), new Vector3(-0.05f, 0.19f, -0.03f), new Vector3(0.05f, 0.2f, 0.03f), new Vector3(0.12f, 0.14f, -0.01f), new Vector3(0f, 0.13f, 0.13f), new Vector3(-0.1f, 0.09f, 0.11f), new Vector3(0.1f, 0.09f, 0.11f) })
                        mesh.Rod(head + spike, head + spike + new Vector3(spike.x * 0.7f, 0.14f, spike.z * 0.5f), 0.05f, 5, hair, ArtSurface.Plain, sway, 0f);
                    break;
                case "wild":
                    Cap();
                    for (var i = 0; i < 11; i++)
                    {
                        var a = i * 0.9f;
                        var root = head + new Vector3(Mathf.Cos(a) * 0.14f, 0.08f + (i % 3) * 0.04f, Mathf.Sin(a) * 0.13f + 0.03f);
                        mesh.Rod(root, root + new Vector3(Mathf.Cos(a) * 0.14f, 0.1f + (i % 2) * 0.05f, Mathf.Sin(a) * 0.12f), 0.055f, 5, hair, ArtSurface.Plain, sway * 1.5f);
                    }
                    break;
                case "ponytail":
                    Cap();
                    mesh.Blob(head + new Vector3(0f, 0.11f, 0.15f), Vector3.one * 0.045f, kit.Accent, ArtSurface.Plain, sway, 0f, 5, 3);
                    mesh.Rod(head + new Vector3(0f, 0.1f, 0.16f), head + new Vector3(0.01f, -0.26f, 0.24f), 0.065f, 6, hair, ArtSurface.Plain, 0.8f);
                    break;
                case "topknot":
                    Cap();
                    mesh.Cylinder(head + new Vector3(0f, 0.18f, 0.03f), 0.035f, 0.04f, 0.04f, 6, kit.Accent, ArtSurface.Plain, sway, 0f, false);
                    mesh.Blob(head + new Vector3(0f, 0.27f, 0.03f), new Vector3(0.07f, 0.07f, 0.07f), hair, ArtSurface.Plain, sway, 0f, 7, 4, 0f, 0, top);
                    break;
                case "bun":
                    Cap();
                    mesh.Blob(head + new Vector3(0f, 0.21f, 0.09f), Vector3.one * 0.09f, hair, ArtSurface.Plain, sway, 0f, 7, 4, 0f, 0, top);
                    break;
                case "braid":
                    Cap();
                    for (var i = 0; i < 5; i++)
                        mesh.Blob(head + new Vector3(0f, 0.03f - i * 0.075f, 0.18f + i * 0.012f), Vector3.one * (0.05f - i * 0.004f), hair, ArtSurface.Plain, 0.7f, 0f, 6, 3);
                    break;
                case "long":
                    Cap();
                    foreach (var x in new[] { -0.12f, 0f, 0.12f })
                        mesh.Rod(head + new Vector3(x, 0.03f, 0.12f), head + new Vector3(x * 1.2f, -0.4f, 0.2f), 0.075f, 6, hair, ArtSurface.Plain, 0.7f);
                    foreach (var x in new[] { -0.155f, 0.155f })
                        mesh.Rod(head + new Vector3(x, 0.03f, -0.05f), head + new Vector3(x * 1.1f, -0.16f, -0.05f), 0.035f, 5, hair, ArtSurface.Plain, 0.5f);
                    break;
                default: // short
                    Cap();
                    break;
            }
        }

        private static void HeroHeadgear(MeshBuilder mesh, Vector3 head, HeroKit kit, float sway)
        {
            switch (kit.Headgear)
            {
                case "headband":
                    mesh.Cylinder(head + Vector3.up * 0.05f, 0.178f, 0.178f, 0.05f, 10, kit.Secondary, ArtSurface.Plain, sway, 0f, false);
                    // The plate is a plain sun over two peaks, this village's own mark.
                    mesh.Box(head + new Vector3(0f, 0.048f, -0.175f), new Vector3(0.14f, 0.055f, 0.015f), kit.Accent, ArtSurface.Metal, sway);
                    mesh.Rod(head + new Vector3(0.12f, 0.07f, 0.16f), head + new Vector3(0.2f, -0.05f, 0.2f), 0.02f, 4, kit.Secondary, ArtSurface.Plain, 0.9f);
                    break;
                case "hairpin":
                    mesh.Rod(head + new Vector3(-0.12f, 0.22f, 0.09f), head + new Vector3(0.14f, 0.26f, 0.09f), 0.012f, 4, kit.Accent, ArtSurface.Metal, sway);
                    mesh.Blob(head + new Vector3(0.15f, 0.265f, 0.09f), Vector3.one * 0.025f, kit.Accent, ArtSurface.Plain, sway, 0.3f, 5, 3);
                    break;
                case "lower_mask":
                    mesh.Blob(head + new Vector3(0f, -0.07f, -0.02f), new Vector3(0.165f, 0.095f, 0.16f), kit.Secondary, ArtSurface.Plain, sway, 0f, 8, 4);
                    break;
                case "scarf":
                    mesh.Cylinder(head + Vector3.down * 0.17f, 0.16f, 0.14f, 0.09f, 8, kit.Accent, ArtSurface.Plain, sway);
                    mesh.Rod(head + new Vector3(0.08f, -0.14f, 0.12f), head + new Vector3(0.22f, -0.4f, 0.26f), 0.045f, 5, kit.Accent, ArtSurface.Plain, 0.9f);
                    break;
                case "goggles":
                    mesh.Cylinder(head + Vector3.up * 0.075f, 0.18f, 0.18f, 0.028f, 10, kit.Secondary, ArtSurface.Plain, sway, 0f, false);
                    foreach (var side in new[] { -0.07f, 0.07f })
                        mesh.Blob(head + new Vector3(side, 0.09f, -0.165f), new Vector3(0.05f, 0.04f, 0.022f), kit.Accent, ArtSurface.Plain, sway, 0.4f, 6, 3);
                    break;
                case "straw_hat":
                    mesh.Cylinder(head + Vector3.up * 0.1f, 0.4f, 0.03f, 0.16f, 12, new Color(0.82f, 0.70f, 0.42f), ArtSurface.Thatch, sway * 0.5f);
                    break;
                case "helmet":
                    mesh.Blob(head + Vector3.up * 0.06f, new Vector3(0.195f, 0.15f, 0.19f), kit.Primary, ArtSurface.Metal, sway, 0f, 9, 5);
                    mesh.Box(head + new Vector3(0f, 0.07f, -0.19f), new Vector3(0.22f, 0.035f, 0.05f), kit.Accent, ArtSurface.Metal, sway);
                    mesh.Rod(head + new Vector3(0f, 0.2f, 0f), head + new Vector3(0f, 0.3f, 0.02f), 0.018f, 4, kit.Accent, ArtSurface.Metal, sway);
                    break;
                case "hood":
                    mesh.Blob(head + new Vector3(0f, 0.05f, 0.045f), new Vector3(0.21f, 0.2f, 0.2f), kit.Primary, ArtSurface.Plain, sway, 0f, 9, 5);
                    mesh.Cylinder(head + new Vector3(0f, -0.19f, 0f), 0.2f, 0.14f, 0.06f, 8, kit.Primary, ArtSurface.Plain, sway, 0f, false);
                    break;
                case "blindfold":
                    mesh.Cylinder(head + Vector3.down * 0.03f, 0.176f, 0.176f, 0.075f, 10, kit.Secondary, ArtSurface.Plain, sway, 0f, false);
                    mesh.Rod(head + new Vector3(0.1f, 0.01f, 0.15f), head + new Vector3(0.22f, -0.2f, 0.24f), 0.022f, 4, kit.Secondary, ArtSurface.Plain, 0.9f);
                    break;
                case "spirit_mask":
                    mesh.Blob(head + new Vector3(0f, 0f, -0.05f), new Vector3(0.15f, 0.19f, 0.13f), kit.Accent, ArtSurface.Wood, sway, 0f, 8, 5);
                    foreach (var side in new[] { -0.06f, 0.06f })
                        mesh.Blob(head + new Vector3(side, 0.02f, -0.176f), new Vector3(0.03f, 0.016f, 0.008f), new Color(0.05f, 0.04f, 0.06f), ArtSurface.Plain, sway, 0f, 5, 3);
                    Strip(mesh, head + new Vector3(0f, 0.15f, -0.13f), head + new Vector3(0f, -0.14f, -0.15f), 0.018f, Vector3.back, kit.Primary, ArtSurface.Plain, sway);
                    break;
                case "beak_mask":
                    mesh.Blob(head + new Vector3(0f, 0.01f, -0.04f), new Vector3(0.15f, 0.14f, 0.14f), Color.Lerp(kit.Primary, Color.black, 0.4f), ArtSurface.Metal, sway, 0f, 8, 4);
                    mesh.Rod(head + new Vector3(0f, -0.01f, -0.16f), head + new Vector3(0f, -0.08f, -0.34f), 0.05f, 6, kit.Accent, ArtSurface.Metal, sway, 0.02f);
                    foreach (var side in new[] { -0.07f, 0.07f })
                        mesh.Blob(head + new Vector3(side, 0.03f, -0.17f), new Vector3(0.024f, 0.015f, 0.008f), kit.Accent, ArtSurface.Plain, sway, 1f, 5, 3);
                    break;
                case "prayer_beads":
                    for (var i = 0; i < 10; i++)
                    {
                        var a = i * Mathf.PI * 2f / 10f;
                        mesh.Blob(head + new Vector3(Mathf.Cos(a) * 0.14f, -0.2f, Mathf.Sin(a) * 0.14f), Vector3.one * 0.028f, kit.Accent, ArtSurface.Plain, sway, 0f, 5, 3);
                    }
                    break;
                case "beard":
                    mesh.Blob(head + new Vector3(0f, -0.14f, -0.08f), new Vector3(0.11f, 0.16f, 0.08f), kit.Hair, ArtSurface.Plain, sway, 0f, 7, 4);
                    break;
                case "crown":
                    mesh.Cylinder(head + Vector3.up * 0.2f, 0.12f, 0.15f, 0.08f, 8, kit.Accent, ArtSurface.Metal, sway, 0.2f, false);
                    for (var i = 0; i < 6; i++)
                    {
                        var a = i * Mathf.PI * 2f / 6f;
                        var at = head + new Vector3(Mathf.Cos(a) * 0.14f, 0.27f, Mathf.Sin(a) * 0.14f);
                        mesh.Rod(at, at + new Vector3(Mathf.Cos(a) * 0.05f, 0.12f, Mathf.Sin(a) * 0.05f), 0.02f, 4, kit.Accent, ArtSurface.Plain, sway, 0.5f);
                    }
                    break;
                case "crescent_crown":
                    mesh.Cylinder(head + Vector3.up * 0.17f, 0.15f, 0.15f, 0.03f, 10, kit.Accent, ArtSurface.Metal, sway, 0.2f, false);
                    foreach (var side in new[] { -1f, 1f })
                    {
                        var root = head + new Vector3(side * 0.13f, 0.18f, 0f);
                        var mid = root + new Vector3(side * 0.09f, 0.1f, 0f);
                        mesh.Rod(root, mid, 0.028f, 5, kit.Accent, ArtSurface.Plain, sway, 0.5f);
                        mesh.Rod(mid, mid + new Vector3(-side * 0.05f, 0.12f, 0f), 0.02f, 5, kit.Accent, ArtSurface.Plain, sway, 0.7f);
                    }
                    break;
                case "broken_crown":
                    for (var i = 0; i < 7; i++)
                    {
                        if (i == 2) continue;
                        var a = i * Mathf.PI * 2f / 7f;
                        var at = head + new Vector3(Mathf.Cos(a) * 0.17f, 0.3f + (i % 2) * 0.03f, Mathf.Sin(a) * 0.17f);
                        mesh.Frustum(at, 0.05f, 0.03f, 0f, 0f, 0.13f, kit.Accent, ArtSurface.Plain, 0.3f, 0.9f);
                    }
                    break;
                case "horned_helm":
                    mesh.Blob(head + Vector3.up * 0.06f, new Vector3(0.195f, 0.155f, 0.19f), kit.Primary, ArtSurface.Metal, sway, 0f, 9, 5);
                    foreach (var side in new[] { -1f, 1f })
                    {
                        var root = head + new Vector3(side * 0.15f, 0.12f, 0f);
                        var mid = root + new Vector3(side * 0.13f, 0.1f, 0f);
                        mesh.Rod(root, mid, 0.045f, 6, Color.Lerp(kit.Primary, Color.black, 0.5f), ArtSurface.Plain, sway, 0.015f);
                        mesh.Rod(mid, mid + new Vector3(side * 0.02f, 0.17f, 0f), 0.028f, 5, kit.Accent, ArtSurface.Plain, sway, 0f, 0.3f);
                    }
                    break;
            }
        }

        private static void HeroWeapon(MeshBuilder mesh, Vector3 feet, Vector3 neck, HeroKit kit)
        {
            var w = kit.Width;
            var right = neck + new Vector3(0.25f * w, -0.33f * kit.Height, -0.04f);
            var left = neck + new Vector3(-0.25f * w, -0.33f * kit.Height, -0.04f);
            var steel = new Color(0.78f, 0.80f, 0.84f);
            var wood = new Color(0.40f, 0.27f, 0.17f);
            const float sway = 0.25f;

            void Blade(Vector3 hand, Vector3 tipOffset, float length)
            {
                var tip = hand + tipOffset.normalized * length;
                mesh.Rod(hand + tipOffset.normalized * 0.06f, tip, 0.016f, 4, kit.Extras.Contains("sparks") ? kit.Accent : steel, ArtSurface.Plain, sway, 0.004f, kit.Extras.Contains("sparks") ? 0.6f : 0f);
                mesh.Rod(hand - tipOffset.normalized * 0.05f, hand + tipOffset.normalized * 0.06f, 0.02f, 4, wood, ArtSurface.Plain, sway);
            }

            switch (kit.Weapon)
            {
                case "katana":
                    Blade(right, new Vector3(0.2f, 0.75f, -0.45f), 0.68f);
                    if (kit.Extras.Contains("second_blade"))
                    {
                        var ghost = left;
                        mesh.Rod(ghost, ghost + new Vector3(-0.25f, 0.5f, -0.3f), 0.018f, 4, kit.Accent, ArtSurface.Plain, sway, 0.004f, 1f);
                    }
                    break;
                case "dagger":
                    Blade(right, new Vector3(0.3f, 0.25f, -0.6f), 0.28f);
                    break;
                case "twin_daggers":
                    Blade(right, new Vector3(0.45f, 0.1f, -0.7f), 0.3f);
                    Blade(left, new Vector3(-0.45f, 0.1f, -0.7f), 0.3f);
                    break;
                case "spear":
                    mesh.Rod(feet + new Vector3(0.28f * w, 0.02f, -0.06f), feet + new Vector3(0.3f * w, 1.55f, -0.06f), 0.018f, 5, wood, ArtSurface.Plain, sway);
                    mesh.Rod(feet + new Vector3(0.3f * w, 1.55f, -0.06f), feet + new Vector3(0.3f * w, 1.82f, -0.06f), 0.04f, 5, steel, ArtSurface.Plain, sway, 0.003f);
                    break;
                case "staff":
                    mesh.Rod(feet + new Vector3(0.28f * w, 0.02f, -0.06f), feet + new Vector3(0.3f * w, 1.4f, -0.08f), 0.02f, 5, wood, ArtSurface.Wood, sway);
                    break;
                case "crescent_staff":
                {
                    var top = feet + new Vector3(0.3f * w, 1.5f, -0.08f);
                    mesh.Rod(feet + new Vector3(0.28f * w, 0.02f, -0.06f), top, 0.02f, 5, Color.Lerp(wood, kit.Accent, 0.4f), ArtSurface.Wood, sway);
                    mesh.Rod(top, top + new Vector3(0.12f, 0.12f, 0f), 0.024f, 5, kit.Accent, ArtSurface.Plain, sway, 0.006f, 0.4f);
                    mesh.Rod(top, top + new Vector3(-0.12f, 0.12f, 0f), 0.024f, 5, kit.Accent, ArtSurface.Plain, sway, 0.006f, 0.4f);
                    break;
                }
                case "lantern_staff":
                {
                    var top = feet + new Vector3(0.3f * w, 1.42f, -0.08f);
                    mesh.Rod(feet + new Vector3(0.28f * w, 0.02f, -0.06f), top, 0.024f, 5, wood, ArtSurface.Wood, sway);
                    mesh.Rod(top, top + new Vector3(0.14f, 0.02f, 0f), 0.016f, 4, wood, ArtSurface.Wood, sway);
                    mesh.Blob(top + new Vector3(0.15f, -0.1f, 0f), new Vector3(0.075f, 0.1f, 0.075f), kit.Accent, ArtSurface.Plain, 0.5f, 1f, 7, 4);
                    break;
                }
                case "flame_staff":
                {
                    var top = feet + new Vector3(0.3f * w, 1.5f, -0.08f);
                    mesh.Rod(feet + new Vector3(0.28f * w, 0.02f, -0.06f), top, 0.024f, 5, new Color(0.12f, 0.10f, 0.09f), ArtSurface.Plain, sway);
                    mesh.Cylinder(top, 0.09f, 0f, 0.3f, 6, kit.Accent, ArtSurface.Plain, 0.6f, 1f);
                    break;
                }
                case "glaive":
                {
                    var top = feet + new Vector3(0.3f * w, 1.35f, -0.08f);
                    mesh.Rod(feet + new Vector3(0.28f * w, 0.02f, -0.06f), top, 0.02f, 5, wood, ArtSurface.Wood, sway);
                    mesh.Rod(top, top + new Vector3(0.1f, 0.4f, -0.02f), 0.05f, 5, steel, ArtSurface.Plain, sway, 0.004f);
                    break;
                }
                case "maul":
                {
                    var top = feet + new Vector3(0.34f * w, 1.3f, -0.08f);
                    mesh.Rod(feet + new Vector3(0.3f * w, 0.02f, -0.06f), top, 0.03f, 5, wood, ArtSurface.Wood, sway);
                    mesh.Blob(top + Vector3.up * 0.1f, new Vector3(0.24f, 0.17f, 0.17f), new Color(0.56f, 0.53f, 0.50f), ArtSurface.Rock, sway, 0f, 8, 4, 0.12f, 7);
                    break;
                }
                case "bow":
                {
                    var top = left + new Vector3(-0.05f, 0.42f, -0.05f);
                    var bottom = left + new Vector3(-0.05f, -0.28f, -0.05f);
                    var mid = left + new Vector3(-0.14f, 0.07f, -0.05f);
                    mesh.Rod(bottom, mid, 0.018f, 4, wood, ArtSurface.Wood, sway);
                    mesh.Rod(mid, top, 0.018f, 4, wood, ArtSurface.Wood, sway);
                    mesh.Rod(bottom, top, 0.004f, 3, Color.white, ArtSurface.Plain, sway);
                    break;
                }
                case "shield":
                    mesh.Frustum(left + new Vector3(-0.06f, -0.12f, -0.1f), 0.34f, 0.05f, 0.38f, 0.05f, 0.56f, kit.Primary, ArtSurface.Metal, sway);
                    mesh.Box(left + new Vector3(-0.06f, 0.1f, -0.13f), new Vector3(0.12f, 0.12f, 0.02f), kit.Accent, ArtSurface.Metal, sway);
                    Blade(right, new Vector3(0.2f, 0.5f, -0.5f), 0.34f);
                    break;
                case "fans":
                    foreach (var hand in new[] { (right, 1f), (left, -1f) })
                    {
                        var center = hand.Item1 + Vector3.up * 0.04f;
                        for (var i = -2; i <= 2; i++)
                        {
                            var a = i * 0.34f;
                            var tip = center + new Vector3(hand.Item2 * Mathf.Sin(a + 0.7f) * 0.26f, Mathf.Cos(a + 0.7f) * 0.26f, -0.03f);
                            var next = center + new Vector3(hand.Item2 * Mathf.Sin(a + 1.04f) * 0.26f, Mathf.Cos(a + 1.04f) * 0.26f, -0.03f);
                            mesh.Quad(center, center, next, tip, Vector3.back, i % 2 == 0 ? kit.Accent : kit.Primary, ArtSurface.Plain, sway);
                        }
                    }
                    break;
                case "chain_sickle":
                {
                    var hook = right + new Vector3(0.14f, 0.26f, -0.1f);
                    mesh.Rod(right, hook, 0.016f, 4, wood, ArtSurface.Plain, sway);
                    mesh.Rod(hook, hook + new Vector3(-0.2f, 0.05f, -0.03f), 0.022f, 4, steel, ArtSurface.Plain, sway, 0.003f);
                    for (var i = 0; i < 8; i++)
                    {
                        var t = i / 7f;
                        var at = Vector3.Lerp(left, feet + new Vector3(-0.44f, 0.08f, -0.2f), t) + Vector3.down * Mathf.Sin(t * Mathf.PI) * 0.08f;
                        mesh.Blob(at, Vector3.one * 0.02f, steel, ArtSurface.Plain, 0.4f, 0f, 4, 2);
                    }
                    break;
                }
                case "stone_fists":
                    mesh.Blob(right, Vector3.one * 0.12f, new Color(0.56f, 0.53f, 0.50f), ArtSurface.Rock, sway, 0f, 7, 4, 0.14f, 3);
                    mesh.Blob(left, Vector3.one * 0.12f, new Color(0.56f, 0.53f, 0.50f), ArtSurface.Rock, sway, 0f, 7, 4, 0.14f, 4);
                    break;
                case "fists":
                    foreach (var hand in new[] { right, left })
                        mesh.Blob(hand, Vector3.one * 0.075f, kit.Accent, ArtSurface.Plain, sway, 0f, 6, 3);
                    break;
                case "lightning_whip":
                    for (var i = 0; i < 6; i++)
                    {
                        var a = right + new Vector3(0.04f + i * 0.09f, -0.03f + (i % 2) * 0.09f - i * 0.03f, -0.06f - i * 0.05f);
                        var b = right + new Vector3(0.04f + (i + 1) * 0.09f, -0.03f + ((i + 1) % 2) * 0.09f - (i + 1) * 0.03f, -0.06f - (i + 1) * 0.05f);
                        mesh.Rod(a, b, 0.017f, 4, kit.Accent, ArtSurface.Plain, 0.5f, 0f, 1f);
                    }
                    break;
                case "orb":
                    mesh.Blob(right + new Vector3(0.03f, 0.17f, -0.1f), Vector3.one * 0.09f, kit.Accent, ArtSurface.Plain, 0.4f, 0.9f, 7, 4);
                    break;
                case "talismans":
                    for (var i = 0; i < 3; i++)
                        Strip(mesh, right + new Vector3(0.02f + i * 0.05f, 0.05f, -0.08f), right + new Vector3(0.02f + i * 0.05f, -0.14f, -0.08f), 0.042f, Vector3.back, Paper, ArtSurface.Plain, 0.7f);
                    break;
                case "wrench":
                    mesh.Rod(right, right + new Vector3(0.06f, 0.26f, -0.1f), 0.02f, 4, steel, ArtSurface.Plain, sway);
                    mesh.Blob(right + new Vector3(0.07f, 0.28f, -0.11f), Vector3.one * 0.04f, kit.Accent, ArtSurface.Plain, sway, 0.3f, 5, 3);
                    break;
                case "satchel":
                    mesh.Blob(left + new Vector3(-0.02f, 0.05f, -0.03f), new Vector3(0.09f, 0.08f, 0.06f), Burlap, ArtSurface.Plain, sway, 0f, 6, 3);
                    break;
            }
        }

        private static void HeroBackItems(MeshBuilder mesh, Vector3 neck, Vector3 hip, HeroKit kit)
        {
            var w = kit.Width;
            var backZ = 0.17f * w;
            switch (kit.Back)
            {
                case "scroll":
                    mesh.Rod(neck + new Vector3(-0.2f, -0.2f, backZ), neck + new Vector3(0.2f, 0.06f, backZ), 0.05f, 6, Paper, ArtSurface.Plain, 0.2f);
                    break;
                case "quiver":
                    mesh.Rod(hip + new Vector3(0.12f, 0.05f, backZ), neck + new Vector3(0.14f, 0.12f, backZ + 0.02f), 0.05f, 6, kit.Secondary, ArtSurface.Plain, 0.2f);
                    break;
                case "bundle":
                    mesh.Blob(neck + new Vector3(0f, -0.12f, backZ + 0.05f), new Vector3(0.16f, 0.17f, 0.1f), Burlap, ArtSurface.Plain, 0.2f, 0f, 7, 4);
                    break;
                case "bell":
                    mesh.Cylinder(neck + new Vector3(0f, -0.2f, backZ + 0.1f), 0.2f * w, 0.09f, 0.34f, 9, kit.Accent, ArtSurface.Metal, 0.2f);
                    break;
                case "masks":
                    foreach (var side in new[] { -0.13f, 0f, 0.13f })
                        mesh.Blob(hip + new Vector3(side, 0.08f, -0.2f * w), new Vector3(0.055f, 0.075f, 0.018f), kit.Accent, ArtSurface.Wood, 0.2f, 0f, 6, 3);
                    break;
                case "drums":
                    for (var i = 0; i < 5; i++)
                    {
                        var a = Mathf.PI * (0.1f + i * 0.2f);
                        var at = neck + new Vector3(Mathf.Cos(a) * 0.42f, Mathf.Sin(a) * 0.42f - 0.05f, backZ + 0.03f);
                        mesh.Cylinder(at + Vector3.down * 0.055f, 0.085f, 0.085f, 0.11f, 8, kit.Accent, ArtSurface.Wood, 0.2f);
                    }
                    break;
                case "cape":
                    HeroCape(mesh, neck, kit.Primary, 0.75f, 0.24f * w, backZ);
                    break;
            }
        }

        private static void HeroCape(MeshBuilder mesh, Vector3 neck, Color color, float length, float halfWidth, float backZ)
        {
            var a = neck + new Vector3(-halfWidth * 0.8f, 0f, backZ);
            var b = neck + new Vector3(halfWidth * 0.8f, 0f, backZ);
            var c = neck + new Vector3(halfWidth * 1.3f, -length, backZ + 0.1f);
            var d = neck + new Vector3(-halfWidth * 1.3f, -length, backZ + 0.1f);
            mesh.Quad(a, b, c, d, Vector3.forward, color, ArtSurface.Plain, 0.55f);
            mesh.Quad(a, b, c, d, Vector3.back, Color.Lerp(color, Color.black, 0.3f), ArtSurface.Plain, 0.55f);
        }

        private static void HeroAuraBelow(MeshBuilder mesh, Vector3 feet, HeroKit kit)
        {
            if (kit.Extras.Contains("flame_field"))
                for (var i = 0; i < 9; i++)
                {
                    var a = i * Mathf.PI * 2f / 9f;
                    mesh.Cylinder(feet + new Vector3(Mathf.Cos(a) * 0.52f, 0.005f, Mathf.Sin(a) * 0.52f), 0.08f, 0f, 0.28f + (i % 3) * 0.08f, 5, kit.Accent, ArtSurface.Plain, 0.6f, 1f);
                }
            if (kit.Extras.Contains("aura_low")) mesh.Cylinder(feet + Vector3.up * 0.004f, 0.36f, 0.36f, 0.01f, 12, kit.Accent, ArtSurface.Plain, 0f, 0.55f);
            if (kit.Extras.Contains("aura_high"))
            {
                mesh.Cylinder(feet + Vector3.up * 0.004f, 0.52f, 0.52f, 0.012f, 14, kit.Accent, ArtSurface.Plain, 0f, 0.85f);
                for (var i = 0; i < 8; i++)
                {
                    var a = i * Mathf.PI * 2f / 8f;
                    mesh.Blob(feet + new Vector3(Mathf.Cos(a) * 0.5f, 0.05f + (i % 3) * 0.12f, Mathf.Sin(a) * 0.5f), Vector3.one * 0.022f, kit.Accent, ArtSurface.Plain, 0.6f, 1f, 4, 2);
                }
            }
        }

        private static void HeroExtras(MeshBuilder mesh, Vector3 feet, Vector3 neck, Vector3 head, HeroKit kit)
        {
            var w = kit.Width;
            const float sway = 0.25f;
            var extras = kit.Extras;
            var backZ = 0.17f * w;

            if (extras.Contains("sash"))
                Strip(mesh, neck + new Vector3(0.14f * w, -0.03f, -0.15f * w), neck + new Vector3(-0.14f * w, -0.3f, -0.17f * w), 0.06f, Vector3.back, kit.Accent, ArtSurface.Plain, sway);
            if (extras.Contains("shoulders"))
                foreach (var side in new[] { -1f, 1f })
                    mesh.Frustum(neck + new Vector3(side * 0.22f * w, -0.07f, 0f), 0.23f * w, 0.2f * w, 0.11f, 0.11f, 0.08f, kit.Accent, ArtSurface.Metal, sway);
            if (extras.Contains("rock_shoulders"))
                foreach (var side in new[] { -1f, 1f })
                    mesh.Blob(neck + new Vector3(side * 0.27f * w, 0.02f, 0f), new Vector3(0.16f, 0.14f, 0.15f) * w, new Color(0.55f, 0.52f, 0.48f), ArtSurface.Rock, sway, 0f, 8, 4, 0.14f, 5 + (int)side, new Color(0.70f, 0.67f, 0.60f));
            if (extras.Contains("rock_crown"))
                for (var i = 0; i < 6; i++)
                {
                    var a = i * Mathf.PI * 2f / 6f;
                    mesh.Frustum(head + new Vector3(Mathf.Cos(a) * 0.17f, 0.11f, Mathf.Sin(a) * 0.17f), 0.1f, 0.1f, 0.02f, 0.02f, 0.19f, new Color(0.60f, 0.57f, 0.52f), ArtSurface.Rock, sway);
                }
            if (extras.Contains("runes"))
                foreach (var y in new[] { -0.08f, -0.2f })
                    Strip(mesh, neck + new Vector3(-0.1f * w, y, -0.19f * w), neck + new Vector3(0.1f * w, y, -0.19f * w), 0.022f, Vector3.back, kit.Accent, ArtSurface.Plain, sway);

            if (extras.Contains("mantle"))
            {
                HeroCape(mesh, neck, kit.Primary, 0.82f, 0.32f * w, backZ);
                mesh.Frustum(neck + new Vector3(0f, -0.05f, backZ * 0.3f), 0.5f * w, 0.42f * w, 0.28f * w, 0.26f * w, 0.1f, kit.Accent, ArtSurface.Metal, sway);
                foreach (var side in new[] { -1f, 1f })
                    mesh.Rod(neck + new Vector3(side * 0.24f * w, 0.05f, backZ), neck + new Vector3(side * 0.3f * w, 0.24f, backZ + 0.05f), 0.05f, 5, kit.Accent, ArtSurface.Plain, sway, 0.012f);
            }
            if (extras.Contains("cape")) HeroCape(mesh, neck, kit.Secondary, 0.72f, 0.24f * w, backZ);
            if (extras.Contains("burning_cape"))
            {
                HeroCape(mesh, neck, Color.Lerp(kit.Secondary, kit.Accent, 0.15f), 0.72f, 0.26f * w, backZ + 0.01f);
                for (var i = 0; i < 6; i++)
                    mesh.Cylinder(neck + new Vector3(-0.27f + i * 0.11f, -0.76f, backZ + 0.12f), 0.04f, 0f, 0.16f + (i % 2) * 0.08f, 4, kit.Accent, ArtSurface.Plain, 0.6f, 1f);
            }
            if (extras.Contains("feather_capelet")) Feathers(mesh, neck, kit, 5, 0.3f, 0.28f, backZ);
            if (extras.Contains("feather_cloak")) Feathers(mesh, neck, kit, 7, 0.65f, 0.3f, backZ);
            if (extras.Contains("wings"))
                foreach (var side in new[] { -1f, 1f })
                    for (var i = 0; i < 6; i++)
                    {
                        var root = neck + new Vector3(side * 0.12f, -0.02f, backZ + 0.03f);
                        var tip = root + new Vector3(side * (0.86f - i * 0.1f), 0.5f - i * 0.2f, 0.12f);
                        mesh.Rod(root, tip, 0.065f - i * 0.004f, 4, i % 2 == 0 ? kit.Primary : Color.Lerp(kit.Primary, kit.Accent, 0.3f), ArtSurface.Plain, 0.45f, 0.01f);
                    }

            if (extras.Contains("halo") || extras.Contains("halo_low"))
            {
                var radius = extras.Contains("halo") ? 0.36f : 0.27f;
                var center = head + new Vector3(0f, 0.04f, 0.2f);
                const int points = 16;
                for (var i = 0; i < points; i++)
                {
                    var a0 = i * Mathf.PI * 2f / points;
                    var a1 = (i + 1) * Mathf.PI * 2f / points;
                    mesh.Rod(center + new Vector3(Mathf.Cos(a0), Mathf.Sin(a0), 0f) * radius, center + new Vector3(Mathf.Cos(a1), Mathf.Sin(a1), 0f) * radius, 0.016f, 3, kit.Accent, ArtSurface.Plain, 0.3f, 0.016f, 1f);
                }
            }
            if (extras.Contains("sparks"))
                for (var i = 0; i < 7; i++)
                {
                    var a = i * 2.2f;
                    var at = neck + new Vector3(Mathf.Cos(a) * 0.38f, -0.3f + i * 0.13f, Mathf.Sin(a) * 0.32f);
                    mesh.Rod(at, at + new Vector3(0.05f, 0.08f, 0f), 0.009f, 3, kit.Accent, ArtSurface.Plain, 0.6f, 0.009f, 1f);
                }
            if (extras.Contains("embers"))
                for (var i = 0; i < 8; i++)
                {
                    var a = i * 1.9f;
                    mesh.Blob(neck + new Vector3(Mathf.Cos(a) * 0.3f, -0.45f + i * 0.13f, Mathf.Sin(a) * 0.27f), Vector3.one * 0.018f, kit.Accent, ArtSurface.Plain, 0.6f, 1f, 4, 2);
                }
            if (extras.Contains("glass_eye")) GlassEye(mesh, head + new Vector3(0.34f, 0.12f, -0.12f), kit);
            if (extras.Contains("glass_eyes"))
                for (var i = 0; i < (kit.Evolution >= 3 ? 6 : 3); i++)
                {
                    var a = i * Mathf.PI * 2f / (kit.Evolution >= 3 ? 6 : 3) + 0.4f;
                    GlassEye(mesh, head + new Vector3(Mathf.Cos(a) * 0.42f, 0.14f + Mathf.Sin(i * 1.7f) * 0.1f, Mathf.Sin(a) * 0.42f), kit);
                }
            if (extras.Contains("talismans"))
                for (var i = 0; i < 5; i++)
                {
                    var a = i * Mathf.PI * 2f / 5f;
                    var at = neck + new Vector3(Mathf.Cos(a) * 0.46f, -0.05f + (i % 2) * 0.16f, Mathf.Sin(a) * 0.42f);
                    Strip(mesh, at, at + Vector3.down * 0.2f, 0.07f, Vector3.back, Paper, ArtSurface.Plain, 0.7f);
                }
            if (extras.Contains("shards_low") || extras.Contains("shards"))
            {
                var count = extras.Contains("shards") ? 9 : 4;
                for (var i = 0; i < count; i++)
                {
                    var a = i * Mathf.PI * 2f / count;
                    var at = neck + new Vector3(Mathf.Cos(a) * 0.46f, -0.14f + (i % 3) * 0.2f, Mathf.Sin(a) * 0.42f);
                    mesh.Frustum(at, 0f, 0f, 0.07f, 0.05f, 0.08f, new Color(0.06f, 0.05f, 0.1f), ArtSurface.Plain, 0.4f);
                    mesh.Frustum(at + Vector3.up * 0.08f, 0.07f, 0.05f, 0f, 0f, 0.14f, kit.Accent, ArtSurface.Plain, 0.4f, 0.8f);
                }
            }
            if (extras.Contains("dark_door"))
            {
                var bottom = feet + new Vector3(0f, 0.04f, 0.42f);
                mesh.Box(bottom, new Vector3(0.9f, 1.75f, 0.03f), new Color(0.05f, 0.04f, 0.08f), ArtSurface.Plain, 0f);
                foreach (var side in new[] { -0.47f, 0.47f })
                    mesh.Box(bottom + new Vector3(side, 0f, -0.02f), new Vector3(0.05f, 1.8f, 0.05f), kit.Accent, ArtSurface.Plain, 0f, 0.8f);
                mesh.Box(bottom + new Vector3(0f, 1.78f, -0.02f), new Vector3(1f, 0.05f, 0.05f), kit.Accent, ArtSurface.Plain, 0f, 0.8f);
            }
            if (extras.Contains("afterimage"))
                foreach (var offset in new[] { 0.3f, 0.56f })
                {
                    var ghost = Color.Lerp(kit.Accent, kit.Primary, offset);
                    var at = feet + new Vector3(offset, 0f, offset * 0.9f);
                    mesh.Cylinder(at + Vector3.up * 0.25f, 0.16f, 0.11f, 0.42f, 6, ghost, ArtSurface.Plain, 0.5f, 0.5f);
                    mesh.Blob(at + Vector3.up * 0.82f, Vector3.one * 0.15f, ghost, ArtSurface.Plain, 0.5f, 0.5f, 6, 4);
                }
            if (extras.Contains("crow"))
            {
                var perch = neck + new Vector3(-0.24f * w, 0.05f, 0f);
                mesh.Blob(perch + Vector3.up * 0.06f, new Vector3(0.06f, 0.07f, 0.1f), new Color(0.07f, 0.08f, 0.11f), ArtSurface.Plain, 0.3f, 0f, 6, 4);
                mesh.Rod(perch + new Vector3(0f, 0.09f, -0.09f), perch + new Vector3(0f, 0.08f, -0.17f), 0.016f, 4, kit.Accent, ArtSurface.Plain, 0.3f, 0.003f);
            }
            if (extras.Contains("spirit_beast")) SpiritBeast(mesh, feet, kit, kit.Evolution >= 3 ? 1.9f : 1f, new Color(1f, 0.72f, 0.22f));
            if (extras.Contains("spirit_wolf")) SpiritBeast(mesh, feet, kit, kit.Evolution >= 3 ? 1.6f : 1f, new Color(0.84f, 0.92f, 1f));
        }

        private static void Feathers(MeshBuilder mesh, Vector3 neck, HeroKit kit, int count, float length, float halfWidth, float backZ)
        {
            for (var i = 0; i < count; i++)
            {
                var x = Mathf.Lerp(-halfWidth, halfWidth, count == 1 ? 0.5f : (float)i / (count - 1));
                var root = neck + new Vector3(x * 0.75f, 0f, backZ);
                mesh.Rod(root, root + new Vector3(x * 0.45f, -length * (1f - Mathf.Abs(x) * 0.5f), 0.1f), 0.06f, 4, i % 2 == 0 ? kit.Secondary : kit.Primary, ArtSurface.Plain, 0.5f, 0.012f);
            }
        }

        private static void GlassEye(MeshBuilder mesh, Vector3 at, HeroKit kit)
        {
            mesh.Blob(at, Vector3.one * 0.065f, new Color(0.92f, 0.94f, 0.96f), ArtSurface.Plain, 0.4f, 0.3f, 6, 4);
            mesh.Blob(at + new Vector3(-0.022f, 0f, -0.048f), new Vector3(0.03f, 0.03f, 0.018f), kit.Primary, ArtSurface.Plain, 0.4f, 1f, 5, 3);
        }

        /// <summary>A companion made of glowing spirit: four legs, a blunt head and a raised tail.</summary>
        private static void SpiritBeast(MeshBuilder mesh, Vector3 feet, HeroKit kit, float size, Color color)
        {
            var at = feet + new Vector3(-0.55f - size * 0.12f, 0f, 0.3f + size * 0.12f);
            const float glow = 0.7f;
            var body = at + Vector3.up * 0.2f * size;
            mesh.Blob(body, new Vector3(0.14f, 0.11f, 0.2f) * size, color, ArtSurface.Plain, 0.4f, glow, 7, 4);
            mesh.Blob(body + new Vector3(0f, 0.09f, -0.2f) * size, new Vector3(0.1f, 0.095f, 0.1f) * size, color, ArtSurface.Plain, 0.4f, glow, 7, 4);
            foreach (var side in new[] { -1f, 1f })
            {
                mesh.Rod(at + new Vector3(side * 0.08f, 0f, -0.12f) * size, at + new Vector3(side * 0.08f, 0.17f, -0.12f) * size, 0.03f * size, 5, color, ArtSurface.Plain, 0.4f, -1f, glow);
                mesh.Rod(at + new Vector3(side * 0.08f, 0f, 0.13f) * size, at + new Vector3(side * 0.08f, 0.17f, 0.13f) * size, 0.03f * size, 5, color, ArtSurface.Plain, 0.4f, -1f, glow);
                mesh.Rod(body + new Vector3(side * 0.06f, 0.16f, -0.2f) * size, body + new Vector3(side * 0.08f, 0.25f, -0.2f) * size, 0.024f * size, 4, color, ArtSurface.Plain, 0.4f, 0.004f, glow);
            }
            mesh.Rod(body + new Vector3(0f, 0.02f, 0.18f) * size, body + new Vector3(0f, 0.27f, 0.32f) * size, 0.03f * size, 4, color, ArtSurface.Plain, 0.7f, 0.006f, glow);
        }
    }
}
