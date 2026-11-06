using ShinobiDominion.Core;
using UnityEngine;

namespace ShinobiDominion.Game
{
    /// <summary>
    /// Small things that make each building feel lived in: timber framing, props and wear. Decoration
    /// only, and all of it stays on the building's own cells. Most of it sits on the two sides the
    /// camera sees, so nothing is spent on faces no one looks at.
    /// </summary>
    public static partial class BuildingMeshes
    {
        private static void Details(MeshBuilder mesh, BuildingDefinition definition, float w, float d)
        {
            switch (definition.function)
            {
                case BuildingFunction.Headquarters: DojoDetails(mesh); break;
                case BuildingFunction.BuilderHall: WorkshopDetails(mesh); break;
                case BuildingFunction.HeroAltar: AltarDetails(mesh); break;
                case BuildingFunction.HeroBase: PedestalDetails(mesh, w); break;
                case BuildingFunction.SummoningGate: GateDetails(mesh); break;
                case BuildingFunction.Training: TrainingDetails(mesh); break;
                case BuildingFunction.TroopCamp: break;
                case BuildingFunction.Research: ResearchDetails(mesh); break;
                case BuildingFunction.Collector: MineDetails(mesh, definition.produces); break;
                case BuildingFunction.Storage: VaultDetails(mesh); break;
                case BuildingFunction.Defense:
                    if (definition.id == "spirit_turret") TurretDetails(mesh);
                    else TowerDetails(mesh);
                    break;
                default: return;
            }

            var seed = 0;
            foreach (var letter in definition.id) seed = seed * 31 + letter;
            Wear(mesh, w, d, seed);
        }

        // ---------- Shared parts ----------

        /// <summary>A flat strip standing just proud of a surface: beams, straps, ropes and rails.</summary>
        private static void Strip(MeshBuilder mesh, Vector3 a, Vector3 b, float width, Vector3 facing, Color color, ArtSurface surface = ArtSurface.Plain, float sway = 0f)
        {
            var side = Vector3.Cross((b - a).normalized, facing).normalized * width * 0.5f;
            var lift = facing.normalized * 0.012f;
            mesh.Quad(a - side + lift, b - side + lift, b + side + lift, a + side + lift, facing, color, surface, sway);
        }

        /// <summary>Moss and loose stones at the foot of the walls. Upgrades clean them away.</summary>
        private static void Wear(MeshBuilder mesh, float w, float d, int seed)
        {
            var random = new System.Random(seed);
            var count = (BuildingSkin.Tiers - S.Tier) * 2;
            for (var i = 0; i < count; i++)
            {
                var t = (float)random.NextDouble() - 0.5f;
                var onFront = random.NextDouble() < 0.5;
                var at = onFront ? new Vector3(t * w * 0.9f, 0.01f, -d * 0.5f + 0.1f) : new Vector3(-w * 0.5f + 0.1f, 0.01f, t * d * 0.9f);
                if (random.NextDouble() < 0.6)
                    mesh.Blob(at, new Vector3(0.11f, 0.05f, 0.09f) * (0.7f + (float)random.NextDouble() * 0.6f), Palette.Leaf, ArtSurface.Grass, 0f, 0f, 5, 3);
                else
                    mesh.Blob(at + Vector3.up * 0.02f, new Vector3(0.06f, 0.04f, 0.05f), S.DarkStone, ArtSurface.Plain, 0f, 0f, 5, 3);
            }
        }

        private static void Barrel(MeshBuilder mesh, Vector3 at, float scale)
        {
            var r = 0.15f * scale;
            var h = 0.36f * scale;
            mesh.Cylinder(at, r * 0.82f, r, h * 0.5f, 7, S.Wood, ArtSurface.Wood, 0f, 0f, false);
            mesh.Cylinder(at + Vector3.up * h * 0.5f, r, r * 0.82f, h * 0.5f, 7, S.Wood, ArtSurface.Wood);
            foreach (var band in new[] { 0.2f, 0.74f })
                mesh.Cylinder(at + Vector3.up * h * band, r * (band < 0.5f ? 0.93f : 0.92f) + 0.004f, r * 0.96f + 0.004f, h * 0.07f, 7, S.Iron, ArtSurface.Plain, 0f, 0f, false);
        }

        private static void Sack(MeshBuilder mesh, Vector3 at, float scale, Color color)
        {
            mesh.Blob(at + Vector3.up * 0.1f * scale, new Vector3(0.13f, 0.12f, 0.11f) * scale, color, ArtSurface.Plain, 0f, 0f, 6, 4);
            mesh.Cylinder(at + Vector3.up * 0.2f * scale, 0.035f * scale, 0.05f * scale, 0.05f * scale, 5, Color.Lerp(color, Color.black, 0.2f), ArtSurface.Plain, 0f, 0f, false);
        }

        private static readonly Color Burlap = new Color(0.72f, 0.60f, 0.42f);
        private static readonly Color Straw = new Color(0.88f, 0.74f, 0.42f);
        private static readonly Color Paper = new Color(0.96f, 0.94f, 0.88f);

        private static void PottedPlant(MeshBuilder mesh, Vector3 at, float scale)
        {
            mesh.Cylinder(at, 0.07f * scale, 0.1f * scale, 0.12f * scale, 7, new Color(0.60f, 0.34f, 0.22f), ArtSurface.Plain);
            mesh.Cylinder(at + Vector3.up * 0.12f * scale, 0.02f * scale, 0.012f * scale, 0.12f * scale, 4, S.DarkWood, ArtSurface.Plain, 0f, 0f, false);
            mesh.Blob(at + new Vector3(0.02f, 0.28f, 0f) * scale, new Vector3(0.14f, 0.09f, 0.14f) * scale, Palette.Leaf, ArtSurface.Grass, 0.5f, 0f, 6, 3, 0f, 0, Palette.LeafLight);
            mesh.Blob(at + new Vector3(-0.08f, 0.2f, -0.04f) * scale, new Vector3(0.08f, 0.05f, 0.08f) * scale, Palette.Leaf, ArtSurface.Grass, 0.5f, 0f, 5, 3, 0f, 0, Palette.LeafLight);
        }

        /// <summary>A short curtain over a doorway, split into panels that move in the wind.</summary>
        private static void Curtain(MeshBuilder mesh, Vector3 topCenter, Vector3 facing, float width, float height, Color color)
        {
            var along = Vector3.Cross(Vector3.up, facing).normalized;
            const int panels = 3;
            var panel = width / panels;
            for (var i = 0; i < panels; i++)
            {
                var c = topCenter + along * ((i - 1) * panel);
                Strip(mesh, c, c + Vector3.down * height * (i == 1 ? 1f : 0.9f), panel * 0.94f, facing, color, ArtSurface.Plain, 0.5f);
            }
            Strip(mesh, topCenter - along * width * 0.52f, topCenter + along * width * 0.52f, 0.035f, facing, S.DarkWood);
        }

        /// <summary>A sagging rope with paper tags hanging from it, as seen on shrines and gateways.</summary>
        private static void RopeWithTags(MeshBuilder mesh, Vector3 a, Vector3 b, float sag, Vector3 facing, int tags)
        {
            const int segments = 6;
            Vector3 At(float t) => Vector3.Lerp(a, b, t) + Vector3.down * sag * 4f * t * (1f - t);
            for (var i = 0; i < segments; i++)
                Strip(mesh, At((float)i / segments), At((float)(i + 1) / segments), 0.04f, facing, Straw);
            for (var i = 1; i <= tags; i++)
            {
                var top = At((float)i / (tags + 1));
                Strip(mesh, top, top + Vector3.down * 0.17f, 0.055f, facing, Paper, ArtSurface.Plain, 0.7f);
            }
        }

        private static void WeaponRack(MeshBuilder mesh, Vector3 at, Vector3 along, Vector3 facing)
        {
            foreach (var side in new[] { -0.3f, 0.3f })
                mesh.Box(at + along * side, new Vector3(0.04f, 0.5f, 0.04f), S.DarkWood, ArtSurface.Wood);
            Strip(mesh, at - along * 0.34f + Vector3.up * 0.42f, at + along * 0.34f + Vector3.up * 0.42f, 0.035f, facing, S.Wood);
            Strip(mesh, at - along * 0.34f + Vector3.up * 0.16f, at + along * 0.34f + Vector3.up * 0.16f, 0.035f, facing, S.Wood);
            for (var i = -1; i <= 1; i++)
            {
                var foot = at + along * (i * 0.17f) + Vector3.up * 0.05f + facing * 0.012f;
                Strip(mesh, foot, foot + Vector3.up * 0.26f, 0.02f, facing, S.DarkWood);
                Strip(mesh, foot + Vector3.up * 0.26f, foot + Vector3.up * 0.54f, 0.024f, facing, new Color(0.78f, 0.80f, 0.84f));
            }
        }

        // ---------- One function per building ----------

        private static void DojoDetails(MeshBuilder mesh)
        {
            const float y = 0.48f;
            const float front = -1.75f;

            // Timber framing on the two faces the camera sees.
            foreach (var height in new[] { 0.55f, 1.18f })
            {
                Strip(mesh, new Vector3(-1.75f, y + height, front), new Vector3(1.75f, y + height, front), 0.05f, Vector3.back, S.DarkWood, ArtSurface.Wood);
                Strip(mesh, new Vector3(front, y + height, -1.75f), new Vector3(front, y + height, 1.75f), 0.05f, Vector3.left, S.DarkWood, ArtSurface.Wood);
            }
            foreach (var z in new[] { -0.48f, 0.48f })
                Strip(mesh, new Vector3(front, y + 0.24f, z), new Vector3(front, y + 1.45f, z), 0.05f, Vector3.left, S.DarkWood, ArtSurface.Wood);
            Curtain(mesh, new Vector3(0f, y + 1.02f, front - 0.1f), Vector3.back, 1.0f, 0.34f, S.Slate);

            // Balcony balusters on the second floor.
            const float second = 2.55f;
            const float reach = -1.245f;
            for (var i = -3; i <= 3; i++)
            {
                var t = i * 0.37f;
                Strip(mesh, new Vector3(t, second, reach), new Vector3(t, second + 0.22f, reach), 0.025f, Vector3.back, S.DarkWood);
                Strip(mesh, new Vector3(reach, second, t), new Vector3(reach, second + 0.22f, t), 0.025f, Vector3.left, S.DarkWood);
            }

            // A drum, a weapon rack and a small tree on the terrace.
            var drum = new Vector3(1.98f, y, -1.98f);
            mesh.Cylinder(drum, 0.15f, 0.17f, 0.14f, 8, S.AccentRed, ArtSurface.Wood, 0f, 0f, false);
            mesh.Cylinder(drum + Vector3.up * 0.14f, 0.17f, 0.15f, 0.14f, 8, S.AccentRed, ArtSurface.Wood, 0f, 0f, false);
            mesh.Cylinder(drum + Vector3.up * 0.28f, 0.15f, 0.15f, 0.005f, 8, Paper);
            mesh.Cylinder(drum + Vector3.up * 0.13f, 0.178f, 0.178f, 0.025f, 8, S.Amber, ArtSurface.Plain, 0f, 0f, false);
            WeaponRack(mesh, new Vector3(-1.98f, y, 0f), Vector3.forward, Vector3.left);
            PottedPlant(mesh, new Vector3(-1.98f, y, -1.98f), 1.1f);
        }

        private static void WorkshopDetails(MeshBuilder mesh)
        {
            const float y = 0.2f;

            // A brick chimney at the back of the roof.
            var chimney = new Vector3(0.6f, y + 1.2f, 0.75f);
            mesh.Box(chimney, new Vector3(0.24f, 0.7f, 0.24f), S.DarkStone, S.StoneSurface);
            mesh.Box(chimney + Vector3.up * 0.7f, new Vector3(0.3f, 0.05f, 0.3f), S.Stone, S.StoneSurface);

            // Tools hung on the wall.
            const float wall = -0.9f;
            Strip(mesh, new Vector3(wall, y + 0.8f, -0.55f), new Vector3(wall, y + 0.8f, -0.15f), 0.03f, Vector3.left, S.DarkWood);
            Strip(mesh, new Vector3(wall, y + 0.78f, -0.47f), new Vector3(wall, y + 0.32f, -0.47f), 0.025f, Vector3.left, S.Wood);
            Strip(mesh, new Vector3(wall, y + 0.78f, -0.43f), new Vector3(wall, y + 0.7f, -0.53f), 0.05f, Vector3.left, S.Iron);
            Strip(mesh, new Vector3(wall, y + 0.78f, -0.25f), new Vector3(wall, y + 0.4f, -0.25f), 0.08f, Vector3.left, S.Iron);

            // Planks leaning on the front wall, a sawhorse and a barrel.
            const float wallFront = -0.725f;
            for (var i = 0; i < 3; i++)
            {
                var x = 0.62f + i * 0.09f;
                Strip(mesh, new Vector3(x, y, wallFront - 0.2f), new Vector3(x + 0.06f, y + 0.72f, wallFront - 0.01f), 0.075f, Vector3.back + Vector3.up * 0.25f, i == 1 ? S.Plaster : S.Wood, ArtSurface.Wood);
            }
            var horse = new Vector3(-0.55f, y, -1.18f);
            foreach (var side in new[] { -0.2f, 0.2f })
                mesh.Box(horse + new Vector3(side, 0f, 0f), new Vector3(0.05f, 0.24f, 0.2f), S.DarkWood, ArtSurface.Wood);
            mesh.Box(horse + Vector3.up * 0.24f, new Vector3(0.62f, 0.05f, 0.09f), S.Plaster, ArtSurface.Wood);
            Barrel(mesh, new Vector3(-1.17f, y, -0.2f), 0.9f);
            Barrel(mesh, new Vector3(-1.17f, y, 0.16f), 0.75f);
        }

        private static void AltarDetails(MeshBuilder mesh)
        {
            const float y = 0.64f;
            const float reach = 1.05f;

            // Brass rings on the pillars and a rope across the front.
            foreach (var cx in new[] { -1f, 1f })
            foreach (var cz in new[] { -1f, 1f })
            foreach (var height in new[] { 0.34f, 1.42f })
                mesh.Cylinder(new Vector3(cx * reach, y + height, cz * reach), 0.115f, 0.115f, 0.05f, 8, S.Amber, ArtSurface.Plain, 0f, 0f, false);
            RopeWithTags(mesh, new Vector3(-reach, y + 1.46f, -reach - 0.1f), new Vector3(reach, y + 1.46f, -reach - 0.1f), 0.1f, Vector3.back, 4);
            RopeWithTags(mesh, new Vector3(-reach - 0.1f, y + 1.46f, -reach), new Vector3(-reach - 0.1f, y + 1.46f, reach), 0.1f, Vector3.left, 4);

            // An incense burner and offering bowls before the crystal.
            var burner = new Vector3(0f, y, -0.72f);
            mesh.Cylinder(burner, 0.06f, 0.1f, 0.08f, 7, S.Iron);
            mesh.Cylinder(burner + Vector3.up * 0.08f, 0.1f, 0.05f, 0.06f, 7, S.Iron, ArtSurface.Plain, 0f, 0f, false);
            mesh.Blob(burner + Vector3.up * 0.14f, Vector3.one * 0.03f, S.Flame, ArtSurface.Plain, 0.3f, 1f, 5, 3);
            foreach (var at in new[] { new Vector3(-0.55f, y, -0.62f), new Vector3(0.55f, y, -0.62f), new Vector3(-0.62f, y, 0f) })
            {
                mesh.Cylinder(at, 0.04f, 0.09f, 0.05f, 7, S.Plaster);
                mesh.Blob(at + Vector3.up * 0.06f, new Vector3(0.05f, 0.03f, 0.05f), Palette.Sakura, ArtSurface.Plain, 0f, 0f, 5, 3);
            }
        }

        private static void PedestalDetails(MeshBuilder mesh, float w)
        {
            // Carved marks around the rim, and a step on the side facing the camera.
            const int marks = 12;
            for (var i = 0; i < marks; i++)
            {
                var a = i * Mathf.PI * 2f / marks;
                var dir = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                var at = dir * w * 0.405f + Vector3.up * 0.161f;
                Strip(mesh, at, at + dir * 0.05f, 0.04f, Vector3.up, S.Amber);
            }
            mesh.Box(new Vector3(0f, 0f, -w * 0.43f), new Vector3(0.5f, 0.08f, 0.13f), S.Stone, S.StoneSurface);
        }

        private static void GateDetails(MeshBuilder mesh)
        {
            const float y = 0.32f;
            const float span = 1.2f;

            // Bands on the pillars, and a rope with tags beneath the lintel.
            foreach (var side in new[] { -1f, 1f })
            {
                foreach (var height in new[] { 0.5f, 1.3f, 2.05f })
                    mesh.Cylinder(new Vector3(side * span, y + height, 0f), 0.155f, 0.15f, 0.06f, 8, S.Amber, ArtSurface.Plain, 0f, 0f, false);

                // Rune stones standing at the corners of the approach.
                var stone = new Vector3(side * 1.52f, 0.2f, -1.42f);
                mesh.Frustum(stone, 0.26f, 0.2f, 0.12f, 0.09f, 0.62f, S.DarkStone, S.StoneSurface);
                mesh.Quad(stone + new Vector3(-0.05f, 0.22f, -0.075f), stone + new Vector3(0.05f, 0.22f, -0.075f), stone + new Vector3(0.04f, 0.46f, -0.056f), stone + new Vector3(-0.04f, 0.46f, -0.056f), Vector3.back, Palette.Portal, ArtSurface.Plain, 0f, 1f);
            }
            RopeWithTags(mesh, new Vector3(-span, y + 2.2f, -0.13f), new Vector3(span, y + 2.2f, -0.13f), 0.05f, Vector3.back, 5);
        }

        private static void TrainingDetails(MeshBuilder mesh)
        {
            const float y = 0.13f;

            // Training mats, a straw target, a straw pile and a water barrel.
            foreach (var at in new[] { new Vector3(-0.95f, y + 0.005f, -0.8f), new Vector3(0.1f, y + 0.005f, -0.95f) })
            {
                mesh.Box(at, new Vector3(0.74f, 0.012f, 0.42f), new Color(0.74f, 0.74f, 0.46f), ArtSurface.Plain);
                Strip(mesh, at + new Vector3(-0.37f, 0.012f, 0.19f), at + new Vector3(0.37f, 0.012f, 0.19f), 0.03f, Vector3.up, S.DarkWood);
                Strip(mesh, at + new Vector3(-0.37f, 0.012f, -0.19f), at + new Vector3(0.37f, 0.012f, -0.19f), 0.03f, Vector3.up, S.DarkWood);
            }
            var target = new Vector3(1.45f, y, 0.42f);
            mesh.Box(target, new Vector3(0.05f, 0.34f, 0.05f), S.Wood, ArtSurface.Wood);
            var center = target + new Vector3(-0.04f, 0.5f, 0f);
            const int points = 10;
            float[] radii = { 0.22f, 0.15f, 0.07f };
            Color[] rings = { Straw, Paper, S.AccentRed };
            for (var ring = 0; ring < radii.Length; ring++)
            for (var i = 0; i < points; i++)
            {
                var a0 = i * Mathf.PI * 2f / points;
                var a1 = (i + 1) * Mathf.PI * 2f / points;
                var c = center + Vector3.left * (0.006f * ring);
                mesh.Quad(c, c, c + new Vector3(0f, Mathf.Sin(a1), Mathf.Cos(a1)) * radii[ring], c + new Vector3(0f, Mathf.Sin(a0), Mathf.Cos(a0)) * radii[ring], Vector3.left, rings[ring]);
            }
            mesh.Blob(new Vector3(-1.42f, y + 0.08f, 0.62f), new Vector3(0.24f, 0.17f, 0.22f), Straw, ArtSurface.Wood, 0.2f, 0f, 6, 4, 0.12f, 91);
            Barrel(mesh, new Vector3(1.5f, y, -0.98f), 0.85f);
        }

        private static void ResearchDetails(MeshBuilder mesh)
        {
            const float y = 0.2f;

            // Herbs drying beneath the roof's edge.
            for (var i = 0; i < 5; i++)
            {
                var a = -Mathf.PI * 0.5f - 0.2f - i * 0.36f;
                var at = new Vector3(Mathf.Cos(a) * 1.14f, y + 0.68f, Mathf.Sin(a) * 1.14f);
                mesh.Blob(at, new Vector3(0.05f, 0.09f, 0.05f), i % 2 == 0 ? Palette.Leaf : Palette.Autumn, ArtSurface.Plain, 0.4f, 0f, 5, 3);
            }

            // Scrolls on the table, a star-gazing tube on a stand, pots and stacked books.
            var table = new Vector3(0.82f, y + 0.19f, -0.78f);
            mesh.Box(table + new Vector3(-0.12f, 0f, 0.03f), new Vector3(0.08f, 0.05f, 0.2f), Paper);
            mesh.Box(table + new Vector3(-0.12f, 0.05f, 0.03f), new Vector3(0.06f, 0.04f, 0.18f), Straw);

            var stand = new Vector3(1.22f, y, 0.02f);
            mesh.Cylinder(stand, 0.08f, 0.015f, 0.55f, 5, S.DarkWood, ArtSurface.Wood, 0f, 0f, false);
            Strip(mesh, stand + new Vector3(-0.14f, 0.5f, 0f), stand + new Vector3(0.12f, 0.8f, 0f), 0.07f, Vector3.back + Vector3.left * 0.5f, S.Amber);
            Strip(mesh, stand + new Vector3(0.1f, 0.77f, 0f), stand + new Vector3(0.15f, 0.83f, 0f), 0.09f, Vector3.back + Vector3.left * 0.5f, S.Iron);

            PottedPlant(mesh, new Vector3(-1.22f, y, -1.22f), 0.9f);
            PottedPlant(mesh, new Vector3(-0.92f, y, -1.26f), 0.7f);
            var books = new Vector3(-1.22f, y, -0.9f);
            mesh.Box(books, new Vector3(0.22f, 0.05f, 0.16f), S.AccentRed);
            mesh.Box(books + new Vector3(0.01f, 0.05f, 0f), new Vector3(0.2f, 0.04f, 0.15f), S.AccentBlue);
            mesh.Box(books + new Vector3(-0.01f, 0.09f, 0f), new Vector3(0.18f, 0.04f, 0.14f), Straw);
        }

        private static void MineDetails(MeshBuilder mesh, ResourceKind produces)
        {
            const float y = 0.1f;
            var shaft = new Vector3(-0.1f, y, -0.62f);

            // Extra props holding up the shaft, and a pick leaning beside it.
            foreach (var side in new[] { -1f, 1f })
                Strip(mesh, shaft + new Vector3(side * 0.55f, 0f, -0.14f), shaft + new Vector3(side * 0.42f, 0.8f, -0.14f), 0.06f, Vector3.back, S.DarkWood, ArtSurface.Wood);
            var pick = shaft + new Vector3(-0.72f, 0f, -0.2f);
            Strip(mesh, pick, pick + new Vector3(0.1f, 0.6f, 0f), 0.03f, Vector3.back, S.Wood);
            Strip(mesh, pick + new Vector3(-0.08f, 0.56f, 0f), pick + new Vector3(0.28f, 0.62f, 0f), 0.04f, Vector3.back, S.Iron);

            // Sacks of ore, a barrel and a lit string across the entrance.
            var fill = produces == ResourceKind.Crystals ? Color.Lerp(Burlap, Palette.Crystal, 0.25f) : Burlap;
            Sack(mesh, new Vector3(-1.08f, y, -1.15f), 1f, fill);
            Sack(mesh, new Vector3(-0.8f, y, -1.25f), 0.85f, fill);
            Sack(mesh, new Vector3(-1.2f, y, -0.85f), 0.8f, fill);
            Barrel(mesh, new Vector3(1.14f, y, -1.12f), 0.85f);
            for (var i = -1; i <= 1; i++)
                PaperLantern(mesh, shaft + new Vector3(i * 0.28f, 0.72f, -0.18f), S.WindowGlow);
        }

        private static void VaultDetails(MeshBuilder mesh)
        {
            const float y = 0.2f;
            const float front = -1.125f;

            // Iron straps studded with rivets across the walls the camera sees.
            foreach (var height in new[] { 0.14f, 0.9f })
            {
                Strip(mesh, new Vector3(-1.05f, y + height, front - 0.01f), new Vector3(1.05f, y + height, front - 0.01f), 0.07f, Vector3.back, S.Iron);
                Strip(mesh, new Vector3(front - 0.01f, y + height, -1.05f), new Vector3(front - 0.01f, y + height, 1.05f), 0.07f, Vector3.left, S.Iron);
                for (var i = -4; i <= 4; i++)
                {
                    if (i == 0 && height < 0.5f) continue;
                    var t = i * 0.23f;
                    mesh.Quad(new Vector3(t - 0.012f, y + height - 0.012f, front - 0.03f), new Vector3(t + 0.012f, y + height - 0.012f, front - 0.03f), new Vector3(t + 0.012f, y + height + 0.012f, front - 0.03f), new Vector3(t - 0.012f, y + height + 0.012f, front - 0.03f), Vector3.back, S.Amber);
                    mesh.Quad(new Vector3(front - 0.03f, y + height - 0.012f, t - 0.012f), new Vector3(front - 0.03f, y + height - 0.012f, t + 0.012f), new Vector3(front - 0.03f, y + height + 0.012f, t + 0.012f), new Vector3(front - 0.03f, y + height + 0.012f, t - 0.012f), Vector3.left, S.Amber);
                }
            }

            // A strongbox and full sacks.
            var chest = new Vector3(-0.6f, y, -1.24f);
            mesh.Box(chest, new Vector3(0.3f, 0.16f, 0.2f), S.DarkWood, ArtSurface.Wood);
            mesh.Frustum(chest + Vector3.up * 0.16f, 0.3f, 0.2f, 0.26f, 0.1f, 0.06f, S.Wood, ArtSurface.Wood);
            Strip(mesh, chest + new Vector3(0f, 0.04f, -0.1f), chest + new Vector3(0f, 0.2f, -0.1f), 0.05f, Vector3.back, S.Amber);
            Sack(mesh, new Vector3(0.42f, y, -1.24f), 0.85f, Burlap);
            Sack(mesh, new Vector3(-1.24f, y, -0.3f), 0.9f, Burlap);
            Sack(mesh, new Vector3(-1.24f, y, 0.05f), 0.75f, Burlap);
        }

        private static void TowerDetails(MeshBuilder mesh)
        {
            const float y = 0.14f;

            // Dark bands of stone up the tower, with a ladder on the side.
            foreach (var height in new[] { 0.36f, 0.86f })
            {
                var size = Mathf.Lerp(1.5f, 1.18f, height / 1.25f) + 0.03f;
                mesh.Box(new Vector3(0f, y + height, 0f), new Vector3(size, 0.07f, size), S.DarkStone, S.StoneSurface);
            }
            const float bottom = -0.76f;
            const float top = -0.61f;
            foreach (var z in new[] { -0.13f, 0.13f })
                Strip(mesh, new Vector3(bottom, y, z), new Vector3(top, y + 1.25f, z), 0.03f, Vector3.left + Vector3.up * 0.12f, S.Wood, ArtSurface.Wood);
            for (var i = 1; i <= 6; i++)
            {
                var t = i / 7f;
                var x = Mathf.Lerp(bottom, top, t) - 0.015f;
                Strip(mesh, new Vector3(x, y + 1.25f * t, -0.13f), new Vector3(x, y + 1.25f * t, 0.13f), 0.026f, Vector3.left + Vector3.up * 0.12f, S.DarkWood);
            }

            // A round shield on the front and a bundle of arrows at the foot.
            var shield = new Vector3(-0.3f, y + 0.72f, -0.7f);
            mesh.Blob(shield, new Vector3(0.14f, 0.14f, 0.025f), S.AccentRed, ArtSurface.Plain, 0f, 0f, 8, 4);
            mesh.Blob(shield + Vector3.back * 0.02f, new Vector3(0.05f, 0.05f, 0.015f), S.Amber, ArtSurface.Plain, 0f, 0f, 6, 3);
            var bundle = new Vector3(0.68f, y, -0.74f);
            mesh.Cylinder(bundle, 0.07f, 0.08f, 0.2f, 6, S.Wood, ArtSurface.Wood);
            for (var i = -1; i <= 1; i++)
                Strip(mesh, bundle + new Vector3(i * 0.03f, 0.2f, 0f), bundle + new Vector3(i * 0.05f, 0.42f, 0f), 0.012f, Vector3.back, S.Plaster);
        }

        private static void TurretDetails(MeshBuilder mesh)
        {
            const float y = 0.48f;

            // Glowing tiles in a ring on the ground and small shards hanging in the air.
            const int tiles = 10;
            for (var i = 0; i < tiles; i++)
            {
                var a = i * Mathf.PI * 2f / tiles + 0.2f;
                var dir = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                var at = dir * 0.75f + Vector3.up * 0.145f;
                Strip(mesh, at - dir * 0.04f, at + dir * 0.04f, 0.05f, Vector3.up, S.AccentBlue);
            }
            foreach (var cx in new[] { -1f, 1f })
            foreach (var cz in new[] { -1f, 1f })
            {
                mesh.Box(new Vector3(cx * 0.45f, y + 0.18f, cz * 0.45f), new Vector3(0.17f, 0.04f, 0.17f), S.Amber);
                var shard = new Vector3(cx * 0.3f, y + 0.72f, cz * 0.3f);
                mesh.Frustum(shard, 0f, 0f, 0.08f, 0.08f, 0.07f, S.Crystal, ArtSurface.Plain, 0.35f, 1f);
                mesh.Frustum(shard + Vector3.up * 0.07f, 0.08f, 0.08f, 0f, 0f, 0.15f, S.Crystal, ArtSurface.Plain, 0.35f, 1f);
            }
        }
    }
}
