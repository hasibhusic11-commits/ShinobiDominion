using ShinobiDominion.Core;
using UnityEngine;

namespace ShinobiDominion.Game
{
    /// <summary>
    /// Original ninja-village architecture, generated in code. Models are centred on their footprint
    /// with the ground at y = 0. Entrances face -Z and -X, the sides the camera sees. Game rules
    /// know nothing about this file, so any building can be replaced with real art later.
    /// </summary>
    public static class BuildingMeshes
    {
        public static MeshBuilder For(BuildingDefinition definition, HeroLook hero)
        {
            var mesh = new MeshBuilder();
            var w = definition.width * GridSpace.CellSize;
            var d = definition.depth * GridSpace.CellSize;

            switch (definition.function)
            {
                case BuildingFunction.Headquarters: Dojo(mesh, w, d); break;
                case BuildingFunction.BuilderHall: Workshop(mesh, w, d); break;
                case BuildingFunction.HeroAltar: Altar(mesh, w, d); break;
                case BuildingFunction.HeroBase: Pedestal(mesh, w, hero); break;
                case BuildingFunction.SummoningGate: Gate(mesh, w, d); break;
                case BuildingFunction.Training: TrainingYard(mesh, w, d); break;
                case BuildingFunction.Research: ResearchHut(mesh, w, d); break;
                case BuildingFunction.Collector: Mine(mesh, w, definition.produces); break;
                case BuildingFunction.Storage: Vault(mesh, w, d); break;
                case BuildingFunction.Defense:
                    if (definition.id == "spirit_turret") SpiritTurret(mesh, w);
                    else WatchTower(mesh, w);
                    break;
                default: WallPiece(mesh); break;
            }
            return mesh;
        }

        // ---------- Shared parts ----------

        private static float Foundation(MeshBuilder mesh, float w, float d, float height = 0.28f)
        {
            mesh.Frustum(Vector3.zero, w - 0.08f, d - 0.08f, w - 0.2f, d - 0.2f, height, Palette.Stone, ArtSurface.Stone);
            return height;
        }

        private static void Pillar(MeshBuilder mesh, Vector3 at, float height, float radius, Color color)
        {
            mesh.Cylinder(at, radius * 1.3f, radius * 1.3f, 0.08f, 8, Palette.DarkStone, ArtSurface.Stone);
            mesh.Cylinder(at + Vector3.up * 0.08f, radius, radius * 0.9f, height - 0.08f, 8, color, ArtSurface.Wood);
        }

        private static void Lantern(MeshBuilder mesh, Vector3 at, float scale = 1f)
        {
            mesh.Cylinder(at, 0.05f * scale, 0.04f * scale, 0.55f * scale, 6, Palette.DarkStone, ArtSurface.Stone);
            var head = at + Vector3.up * 0.55f * scale;
            mesh.Box(head, new Vector3(0.2f, 0.18f, 0.2f) * scale, Palette.WindowGlow, ArtSurface.Plain, 0f, 1f);
            mesh.Frustum(head + Vector3.up * 0.18f * scale, 0.34f * scale, 0.34f * scale, 0.04f * scale, 0.04f * scale, 0.14f * scale, Palette.DarkWood, ArtSurface.Wood);
        }

        /// <summary>A hanging paper lantern, for under eaves.</summary>
        private static void PaperLantern(MeshBuilder mesh, Vector3 at, Color color)
        {
            mesh.Blob(at, new Vector3(0.11f, 0.14f, 0.11f), color, ArtSurface.Plain, 0.25f, 0.9f, 6, 4);
        }

        private static void Window(MeshBuilder mesh, Vector3 at, Vector3 facing, float width, float height)
        {
            var side = Vector3.Cross(Vector3.up, facing).normalized * width * 0.5f;
            var up = Vector3.up * height;
            var offset = facing.normalized * 0.015f;
            mesh.Quad(at - side + offset, at + side + offset, at + side + up + offset, at - side + up + offset, facing, Palette.WindowGlow, ArtSurface.Plain, 0f, 0.95f);
            // Lattice bars and a frame, which are what stop it reading as a yellow rectangle.
            var bar = facing.normalized * 0.03f;
            var thick = 0.025f;
            var center = at + up * 0.5f + bar;
            mesh.Quad(center - side - Vector3.up * thick, center + side - Vector3.up * thick, center + side + Vector3.up * thick, center - side + Vector3.up * thick, facing, Palette.DarkWood);
            var sideDir = side.normalized;
            var mid = at + bar;
            mesh.Quad(mid - sideDir * thick, mid + sideDir * thick, mid + sideDir * thick + up, mid - sideDir * thick + up, facing, Palette.DarkWood);
            mesh.Quad(at - side + bar - Vector3.up * thick, at + side + bar - Vector3.up * thick, at + side + bar + Vector3.up * thick, at - side + bar + Vector3.up * thick, facing, Palette.DarkWood);
            mesh.Quad(at - side + up + bar - Vector3.up * thick, at + side + up + bar - Vector3.up * thick, at + side + up + bar + Vector3.up * thick, at - side + up + bar + Vector3.up * thick, facing, Palette.DarkWood);
            mesh.Quad(at - side + bar - sideDir * thick, at - side + bar + sideDir * thick, at - side + bar + sideDir * thick + up, at - side + bar - sideDir * thick + up, facing, Palette.DarkWood);
            mesh.Quad(at + side + bar - sideDir * thick, at + side + bar + sideDir * thick, at + side + bar + sideDir * thick + up, at + side + bar - sideDir * thick + up, facing, Palette.DarkWood);
        }

        private static void Door(MeshBuilder mesh, Vector3 at, Vector3 facing, float width, float height, Color color)
        {
            var side = Vector3.Cross(Vector3.up, facing).normalized * width * 0.5f;
            var up = Vector3.up * height;
            var offset = facing.normalized * 0.02f;
            mesh.Quad(at - side + offset, at + side + offset, at + side + up + offset, at - side + up + offset, facing, color, ArtSurface.Wood);
            var frame = 0.05f;
            var sideDir = side.normalized;
            var front = facing.normalized * 0.04f;
            mesh.Quad(at - side - sideDir * frame + front, at - side + front, at - side + up + Vector3.up * frame + front, at - side - sideDir * frame + up + Vector3.up * frame + front, facing, Palette.DarkWood, ArtSurface.Wood);
            mesh.Quad(at + side + front, at + side + sideDir * frame + front, at + side + sideDir * frame + up + Vector3.up * frame + front, at + side + up + Vector3.up * frame + front, facing, Palette.DarkWood, ArtSurface.Wood);
            mesh.Quad(at - side - sideDir * frame + up + front, at + side + sideDir * frame + up + front, at + side + sideDir * frame + up + Vector3.up * frame * 1.6f + front, at - side - sideDir * frame + up + Vector3.up * frame * 1.6f + front, facing, Palette.DarkWood, ArtSurface.Wood);
            // Centre seam and brass handles.
            mesh.Quad(at - sideDir * 0.012f + front, at + sideDir * 0.012f + front, at + sideDir * 0.012f + up + front, at - sideDir * 0.012f + up + front, facing, Palette.DarkWood);
            var handle = at + up * 0.45f + front * 1.4f;
            mesh.Quad(handle - sideDir * 0.09f, handle - sideDir * 0.03f, handle - sideDir * 0.03f + Vector3.up * 0.06f, handle - sideDir * 0.09f + Vector3.up * 0.06f, facing, Palette.Amber);
            mesh.Quad(handle + sideDir * 0.03f, handle + sideDir * 0.09f, handle + sideDir * 0.09f + Vector3.up * 0.06f, handle + sideDir * 0.03f + Vector3.up * 0.06f, facing, Palette.Amber);
        }

        /// <summary>The village crest: a sun disc over twin peaks. Original to this game.</summary>
        private static void Emblem(MeshBuilder mesh, Vector3 center, Vector3 facing, float size)
        {
            var side = Vector3.Cross(Vector3.up, facing).normalized;
            var f = facing.normalized;
            Vector3 P(float x, float y, float depth) => center + side * (x * size) + Vector3.up * (y * size) + f * depth;
            const int points = 12;
            for (var i = 0; i < points; i++)
            {
                var a0 = (float)i / points * Mathf.PI * 2f;
                var a1 = (float)(i + 1) / points * Mathf.PI * 2f;
                mesh.Quad(P(0f, 0f, 0.012f), P(0f, 0f, 0.012f), P(Mathf.Cos(a1), Mathf.Sin(a1), 0.012f), P(Mathf.Cos(a0), Mathf.Sin(a0), 0.012f), f, Palette.AccentRed);
            }
            for (var i = 0; i < points; i++)
            {
                var a0 = (float)i / points * Mathf.PI * 2f;
                var a1 = (float)(i + 1) / points * Mathf.PI * 2f;
                mesh.Quad(P(0f, 0.22f, 0.02f), P(0f, 0.22f, 0.02f), P(Mathf.Cos(a1) * 0.34f, 0.22f + Mathf.Sin(a1) * 0.34f, 0.02f), P(Mathf.Cos(a0) * 0.34f, 0.22f + Mathf.Sin(a0) * 0.34f, 0.02f), f, Palette.Amber);
            }
            mesh.Quad(P(-0.72f, -0.5f, 0.024f), P(0.08f, -0.5f, 0.024f), P(-0.3f, 0.12f, 0.024f), P(-0.32f, 0.12f, 0.024f), f, Palette.Plaster);
            mesh.Quad(P(-0.1f, -0.5f, 0.026f), P(0.72f, -0.5f, 0.026f), P(0.32f, -0.02f, 0.026f), P(0.3f, -0.02f, 0.026f), f, Palette.Plaster);
        }

        /// <summary>A hanging cloth banner carrying the crest, which moves slightly in the wind.</summary>
        private static void Banner(MeshBuilder mesh, Vector3 top, Vector3 facing, float width, float height, Color color)
        {
            var side = Vector3.Cross(Vector3.up, facing).normalized * width * 0.5f;
            var f = facing.normalized * 0.02f;
            var bottom = top + Vector3.down * height;
            mesh.Quad(top - side + f, top + side + f, bottom + side + f, bottom - side + f, facing, color, ArtSurface.Plain, 0.45f);
            mesh.Quad(top - side * 1.2f + f * 2f, top + side * 1.2f + f * 2f, top + side * 1.2f + f * 2f + Vector3.up * 0.035f, top - side * 1.2f + f * 2f + Vector3.up * 0.035f, facing, Palette.DarkWood, ArtSurface.Wood);
            Emblem(mesh, top + Vector3.down * height * 0.42f + f, facing, width * 0.3f);
        }

        private static void Crates(MeshBuilder mesh, Vector3 at)
        {
            mesh.Box(at, new Vector3(0.3f, 0.26f, 0.3f), Palette.Wood, ArtSurface.Wood);
            mesh.Box(at + new Vector3(0.28f, 0f, 0.1f), new Vector3(0.22f, 0.2f, 0.22f), Palette.DarkWood, ArtSurface.Wood);
            mesh.Box(at + new Vector3(0.05f, 0.26f, 0.02f), new Vector3(0.18f, 0.16f, 0.18f), Palette.Wood, ArtSurface.Wood);
        }

        private static void Steps(MeshBuilder mesh, Vector3 at, float width, int count, float rise, float run)
        {
            for (var i = 0; i < count; i++)
                mesh.Box(at + new Vector3(0f, i * rise, i * run), new Vector3(width, rise, run * (count - i)), Palette.Stone, ArtSurface.Stone);
        }

        // ---------- Dojo HQ: the centrepiece ----------

        private static void Dojo(MeshBuilder mesh, float w, float d)
        {
            var y = Foundation(mesh, w, d, 0.3f);
            mesh.Frustum(new Vector3(0, y, 0), w - 0.42f, d - 0.42f, w - 0.62f, d - 0.62f, 0.18f, Palette.Stone, ArtSurface.Stone);
            y += 0.18f;
            Steps(mesh, new Vector3(0f, 0f, -(d * 0.5f - 0.05f)), 1.7f, 4, 0.12f, 0.12f);

            // Ground floor: tall enough that its door and windows clear the eaves from this camera.
            var body0 = 3.5f;
            var h0 = 1.55f;
            mesh.Box(new Vector3(0, y, 0), new Vector3(body0, h0, body0), Palette.Plaster, ArtSurface.Plaster);
            mesh.Box(new Vector3(0, y, 0), new Vector3(body0 + 0.05f, 0.24f, body0 + 0.05f), Palette.Stone, ArtSurface.Stone);
            var front = -body0 * 0.5f;
            Door(mesh, new Vector3(0f, y, front), Vector3.back, 1.05f, 1.05f, Palette.AccentRed);
            foreach (var x in new[] { -1.2f, 1.2f }) Window(mesh, new Vector3(x, y + 0.6f, front), Vector3.back, 0.58f, 0.5f);
            foreach (var z in new[] { -0.95f, 0f, 0.95f }) Window(mesh, new Vector3(front, y + 0.6f, z), Vector3.left, 0.58f, 0.5f);
            foreach (var cx in new[] { -1f, 1f })
            foreach (var cz in new[] { -1f, 1f })
                Pillar(mesh, new Vector3(cx * (body0 * 0.5f + 0.03f), y, cz * (body0 * 0.5f + 0.03f)), h0, 0.13f, Palette.AccentRed);
            foreach (var x in new[] { -0.72f, 0.72f })
                Pillar(mesh, new Vector3(x, y, front - 0.05f), h0, 0.09f, Palette.AccentRed);

            // A crest plaque above the door, with banners either side.
            mesh.Box(new Vector3(0f, y + 1.14f, front - 0.04f), new Vector3(0.92f, 0.38f, 0.03f), Palette.DarkWood, ArtSurface.Wood);
            Emblem(mesh, new Vector3(0f, y + 1.33f, front - 0.06f), Vector3.back, 0.17f);
            foreach (var x in new[] { -1.52f, 1.52f })
                Banner(mesh, new Vector3(x, y + h0 - 0.04f, front - 0.16f), Vector3.back, 0.24f, 0.9f, Palette.AccentRed);
            foreach (var z in new[] { -1.45f, 1.45f })
                Banner(mesh, new Vector3(front - 0.16f, y + h0 - 0.04f, z), Vector3.left, 0.24f, 0.9f, Palette.RoofTeal);

            y += h0;
            mesh.Box(new Vector3(0, y - 0.1f, 0), new Vector3(body0 + 0.24f, 0.14f, body0 + 0.24f), Palette.DarkWood, ArtSurface.Wood);
            mesh.Roof(new Vector3(0, y, 0), 2.28f, 2.28f, 1.15f, 1.15f, 0.56f, 0.36f, Palette.Slate, Palette.Amber);
            foreach (var x in new[] { -1.75f, 1.75f }) PaperLantern(mesh, new Vector3(x, y - 0.25f, -2.0f), Palette.WindowGlow);
            foreach (var z in new[] { -1.75f, 1.75f }) PaperLantern(mesh, new Vector3(-2.0f, y - 0.25f, z), Palette.WindowGlow);

            // Second floor.
            y += 0.52f;
            var body1 = 2.25f;
            var h1 = 1.15f;
            mesh.Box(new Vector3(0, y, 0), new Vector3(body1, h1, body1), Palette.Plaster, ArtSurface.Plaster);
            foreach (var x in new[] { -0.62f, 0f, 0.62f }) Window(mesh, new Vector3(x, y + 0.42f, -body1 * 0.5f), Vector3.back, 0.4f, 0.46f);
            foreach (var z in new[] { -0.62f, 0f, 0.62f }) Window(mesh, new Vector3(-body1 * 0.5f, y + 0.42f, z), Vector3.left, 0.4f, 0.46f);
            foreach (var cx in new[] { -1f, 1f })
            foreach (var cz in new[] { -1f, 1f })
                Pillar(mesh, new Vector3(cx * (body1 * 0.5f + 0.02f), y, cz * (body1 * 0.5f + 0.02f)), h1, 0.09f, Palette.AccentRed);
            // A balcony rail ties the floors together.
            mesh.Box(new Vector3(0f, y + 0.2f, -body1 * 0.5f - 0.12f), new Vector3(body1 + 0.2f, 0.05f, 0.05f), Palette.DarkWood, ArtSurface.Wood);
            mesh.Box(new Vector3(-body1 * 0.5f - 0.12f, y + 0.2f, 0f), new Vector3(0.05f, 0.05f, body1 + 0.2f), Palette.DarkWood, ArtSurface.Wood);
            y += h1;
            mesh.Box(new Vector3(0, y - 0.08f, 0), new Vector3(body1 + 0.2f, 0.12f, body1 + 0.2f), Palette.DarkWood, ArtSurface.Wood);
            mesh.Roof(new Vector3(0, y, 0), 1.62f, 1.62f, 0.72f, 0.72f, 0.5f, 0.3f, Palette.Slate, Palette.Amber);

            // Top floor.
            y += 0.48f;
            var body2 = 1.35f;
            var h2 = 0.85f;
            mesh.Box(new Vector3(0, y, 0), new Vector3(body2, h2, body2), Palette.Plaster, ArtSurface.Plaster);
            Window(mesh, new Vector3(0f, y + 0.3f, -body2 * 0.5f), Vector3.back, 0.5f, 0.4f);
            Window(mesh, new Vector3(-body2 * 0.5f, y + 0.3f, 0f), Vector3.left, 0.5f, 0.4f);
            y += h2;
            mesh.Box(new Vector3(0, y - 0.07f, 0), new Vector3(body2 + 0.16f, 0.1f, body2 + 0.16f), Palette.DarkWood, ArtSurface.Wood);
            mesh.Roof(new Vector3(0, y, 0), 1.08f, 1.08f, 0.14f, 0.14f, 0.72f, 0.26f, Palette.Slate, Palette.Amber);
            y += 0.7f;

            // A brass spire with stacked rings makes the Dojo identifiable from any zoom.
            mesh.Cylinder(new Vector3(0, y, 0), 0.07f, 0.03f, 0.8f, 6, Palette.Amber);
            foreach (var ring in new[] { 0.12f, 0.32f, 0.52f })
                mesh.Cylinder(new Vector3(0, y + ring, 0), 0.16f - ring * 0.14f, 0.16f - ring * 0.14f, 0.05f, 8, Palette.Amber);

            foreach (var x in new[] { -1f, 1f }) Lantern(mesh, new Vector3(x * 1.08f, 0.3f, -(d * 0.5f - 0.2f)));
            foreach (var z in new[] { -1f, 1f }) Lantern(mesh, new Vector3(-(w * 0.5f - 0.2f), 0.3f, z * 1.08f));
        }

        // ---------- Builder Hall: a working timber yard ----------

        private static void Workshop(MeshBuilder mesh, float w, float d)
        {
            var y = Foundation(mesh, w, d, 0.2f);
            var bodyX = w - 1.0f;
            var bodyZ = d - 1.15f;
            var center = new Vector3(0.1f, y, 0.2f);
            mesh.Box(center, new Vector3(bodyX, 0.95f, bodyZ), Palette.Wood, ArtSurface.Wood);
            // Exposed timber frame.
            foreach (var cx in new[] { -1f, 1f })
            foreach (var cz in new[] { -1f, 1f })
                mesh.Box(center + new Vector3(cx * bodyX * 0.5f, 0f, cz * bodyZ * 0.5f), new Vector3(0.11f, 0.97f, 0.11f), Palette.DarkWood, ArtSurface.Wood);
            mesh.Box(center + new Vector3(0f, 0.47f, -bodyZ * 0.5f), new Vector3(bodyX, 0.06f, 0.05f), Palette.DarkWood, ArtSurface.Wood);
            mesh.Box(center + new Vector3(-bodyX * 0.5f, 0.47f, 0f), new Vector3(0.05f, 0.06f, bodyZ), Palette.DarkWood, ArtSurface.Wood);

            var doorAt = center + new Vector3(0f, 0f, -bodyZ * 0.5f);
            Door(mesh, doorAt, Vector3.back, 0.7f, 0.72f, Palette.DarkWood);
            Window(mesh, center + new Vector3(-bodyX * 0.5f, 0.42f, 0f), Vector3.left, 0.42f, 0.34f);

            var roofY = y + 0.95f;
            mesh.Roof(center + Vector3.up * 0.95f, bodyX * 0.5f + 0.42f, bodyZ * 0.5f + 0.42f, 0.14f, bodyZ * 0.5f + 0.18f, 0.72f, 0.2f, Palette.RoofRed, Palette.DarkWood);

            // A crane with a hanging load: the Builder Hall's unmistakable silhouette.
            var craneBase = new Vector3(w * 0.5f - 0.32f, y, -(d * 0.5f - 0.32f));
            mesh.Cylinder(craneBase, 0.09f, 0.06f, 2.2f, 6, Palette.DarkWood, ArtSurface.Wood);
            var arm = craneBase + Vector3.up * 2.1f;
            mesh.Box(arm + new Vector3(-0.72f, 0f, 0f), new Vector3(1.75f, 0.09f, 0.09f), Palette.Wood, ArtSurface.Wood);
            mesh.Box(arm + new Vector3(0.2f, -0.18f, 0f), new Vector3(0.05f, 0.2f, 0.05f), Palette.DarkWood);
            var hook = arm + new Vector3(-1.35f, -0.62f, 0f);
            mesh.Box(hook, new Vector3(0.02f, 0.62f, 0.02f), Palette.Iron, ArtSurface.Plain, 0.2f);
            mesh.Box(hook + new Vector3(0f, -0.28f, 0f), new Vector3(0.32f, 0.28f, 0.32f), Palette.Wood, ArtSurface.Wood, 0.2f);

            // Timber stack and tools out front.
            var logs = new Vector3(-w * 0.5f + 0.42f, y, -d * 0.5f + 0.3f);
            for (var row = 0; row < 3; row++)
            for (var i = 0; i < 3 - row; i++)
                mesh.Box(logs + new Vector3(i * 0.15f + row * 0.07f, row * 0.11f, 0f), new Vector3(0.12f, 0.11f, 0.5f), row == 2 ? Palette.Plaster : Palette.Wood, ArtSurface.Wood);
            Crates(mesh, new Vector3(w * 0.5f - 0.75f, y, -d * 0.5f + 0.2f));
            Lantern(mesh, new Vector3(-w * 0.5f + 0.2f, y, d * 0.5f - 0.3f), 0.8f);
        }

        // ---------- Hero Altar: a sacred open-sided shrine ----------

        private static void Altar(MeshBuilder mesh, float w, float d)
        {
            var y = Foundation(mesh, w, d, 0.22f);
            mesh.Frustum(new Vector3(0, y, 0), w - 0.5f, d - 0.5f, w - 0.82f, d - 0.82f, 0.22f, Palette.Plaster, ArtSurface.Stone);
            y += 0.22f;
            mesh.Frustum(new Vector3(0, y, 0), w - 1.05f, d - 1.05f, w - 1.3f, d - 1.3f, 0.2f, Palette.Stone, ArtSurface.Stone);
            y += 0.2f;
            Steps(mesh, new Vector3(0f, 0f, -(d * 0.5f - 0.05f)), 0.95f, 4, 0.13f, 0.12f);

            // Four pillars carry a curved canopy, leaving the glowing crystal visible beneath it.
            var reach = 1.05f;
            foreach (var cx in new[] { -1f, 1f })
            foreach (var cz in new[] { -1f, 1f })
                Pillar(mesh, new Vector3(cx * reach, y, cz * reach), 1.6f, 0.1f, Palette.DarkWood);
            mesh.Box(new Vector3(0f, y + 1.56f, 0f), new Vector3(reach * 2f + 0.3f, 0.1f, reach * 2f + 0.3f), Palette.DarkWood, ArtSurface.Wood);
            mesh.Roof(new Vector3(0, y + 1.64f, 0), 1.7f, 1.7f, 0.16f, 0.16f, 0.9f, 0.34f, Palette.RoofTeal, Palette.Amber);
            mesh.Cylinder(new Vector3(0, y + 2.52f, 0), 0.05f, 0.02f, 0.45f, 6, Palette.Amber);

            mesh.Cylinder(new Vector3(0, y, 0), 0.34f, 0.26f, 0.42f, 8, Palette.DarkStone, ArtSurface.Stone);
            var crystal = new Vector3(0, y + 0.5f, 0);
            mesh.Frustum(crystal, 0f, 0f, 0.28f, 0.28f, 0.24f, Palette.AccentBlue, ArtSurface.Plain, 0.25f, 0.85f);
            mesh.Frustum(crystal + Vector3.up * 0.24f, 0.28f, 0.28f, 0f, 0f, 0.5f, Palette.Crystal, ArtSurface.Plain, 0.25f, 0.95f);

            foreach (var cx in new[] { -1f, 1f })
            {
                Brazier(mesh, new Vector3(cx * (w * 0.5f - 0.3f), 0.22f, -(d * 0.5f - 0.3f)));
                Banner(mesh, new Vector3(cx * 0.62f, y + 1.45f, -reach - 0.02f), Vector3.back, 0.28f, 0.6f, Palette.RoofTeal);
            }
            Brazier(mesh, new Vector3(-(w * 0.5f - 0.3f), 0.22f, d * 0.5f - 0.3f));
        }

        private static void Brazier(MeshBuilder mesh, Vector3 at)
        {
            mesh.Cylinder(at, 0.09f, 0.06f, 0.3f, 6, Palette.DarkStone, ArtSurface.Stone);
            mesh.Cylinder(at + Vector3.up * 0.3f, 0.09f, 0.17f, 0.1f, 8, Palette.Iron);
            mesh.Cylinder(at + Vector3.up * 0.38f, 0.12f, 0f, 0.34f, 6, Palette.Flame, ArtSurface.Plain, 0.5f, 1f);
        }

        // ---------- Hero Base ----------

        private static void Pedestal(MeshBuilder mesh, float w, HeroLook hero)
        {
            mesh.Cylinder(Vector3.zero, w * 0.47f, w * 0.44f, 0.16f, 8, Palette.Stone, ArtSurface.Stone);
            mesh.Cylinder(Vector3.up * 0.16f, w * 0.38f, w * 0.34f, 0.2f, 8, Palette.Plaster, ArtSurface.Stone);
            mesh.Cylinder(Vector3.up * 0.36f, w * 0.345f, w * 0.34f, 0.02f, 8, Palette.Amber);
            mesh.Cylinder(Vector3.up * 0.37f, w * 0.31f, w * 0.3f, 0.02f, 8, Palette.Stone, ArtSurface.Stone);
            var top = Vector3.up * 0.39f;

            // Corner posts with small flames mark it as a hero's station.
            foreach (var corner in new[] { new Vector3(-1, 0, -1), new Vector3(1, 0, -1), new Vector3(-1, 0, 1), new Vector3(1, 0, 1) })
            {
                var at = corner * (w * 0.4f);
                mesh.Box(at, new Vector3(0.12f, 0.52f, 0.12f), Palette.DarkStone, ArtSurface.Stone);
                mesh.Cylinder(at + Vector3.up * 0.52f, 0.05f, 0f, 0.16f, 5, hero != null ? Palette.Flame : Palette.DarkStone, ArtSurface.Plain, 0.3f, hero != null ? 1f : 0f);
            }

            if (hero == null)
            {
                // A vacant base: a plain crest stone shows that no hero stands here.
                mesh.Frustum(top, 0.36f, 0.36f, 0.2f, 0.2f, 0.2f, Palette.DarkStone, ArtSurface.Stone);
                return;
            }

            AffinityEmblem(mesh, top + new Vector3(0f, 0f, -w * 0.3f), hero);
            HeroFigure(mesh, top, hero);
        }

        private static Color AffinityColor(HeroAffinity affinity)
        {
            switch (affinity)
            {
                case HeroAffinity.Solar: return new Color(1f, 0.70f, 0.20f);
                case HeroAffinity.Lightning: return new Color(0.70f, 0.60f, 1f);
                case HeroAffinity.Earth: return new Color(0.68f, 0.50f, 0.28f);
                case HeroAffinity.Lunar: return new Color(0.72f, 0.88f, 1f);
                case HeroAffinity.Void: return new Color(0.58f, 0.28f, 0.82f);
                case HeroAffinity.Fire: return new Color(1f, 0.36f, 0.18f);
                case HeroAffinity.Storm: return new Color(0.42f, 0.76f, 0.80f);
                case HeroAffinity.Water: return new Color(0.26f, 0.60f, 0.90f);
                default: return new Color(0.70f, 0.72f, 0.74f);
            }
        }

        /// <summary>Colour shows affinity. Shape shows role, so it never depends on colour alone.</summary>
        private static void AffinityEmblem(MeshBuilder mesh, Vector3 at, HeroLook hero)
        {
            var color = AffinityColor(hero.Affinity);
            mesh.Box(at, new Vector3(0.42f, 0.02f, 0.2f), Palette.DarkWood);
            var y = at + Vector3.up * 0.02f;
            switch (hero.Role)
            {
                case HeroRole.Tank: mesh.Box(y, new Vector3(0.14f, 0.1f, 0.14f), color, ArtSurface.Plain, 0f, 0.6f); break;
                case HeroRole.Fighter: mesh.Frustum(y, 0.2f, 0.06f, 0f, 0f, 0.14f, color, ArtSurface.Plain, 0f, 0.6f); break;
                case HeroRole.Assassin: mesh.Frustum(y, 0.08f, 0.08f, 0f, 0f, 0.2f, color, ArtSurface.Plain, 0f, 0.6f); break;
                case HeroRole.Mage: mesh.Blob(y + Vector3.up * 0.08f, Vector3.one * 0.08f, color, ArtSurface.Plain, 0f, 0.7f, 6, 4); break;
                case HeroRole.Control: mesh.Cylinder(y, 0.1f, 0.1f, 0.05f, 6, color, ArtSurface.Plain, 0f, 0.6f); break;
                default: mesh.Frustum(y, 0.14f, 0.14f, 0.05f, 0.05f, 0.14f, color, ArtSurface.Plain, 0f, 0.6f); break;
            }
        }

        /// <summary>
        /// A small stylised ninja. Temporary art, but built to read at isometric distance. The same
        /// body and face carry through every form, and each evolution adds to the silhouette.
        /// </summary>
        private static void HeroFigure(MeshBuilder mesh, Vector3 feet, HeroLook hero)
        {
            const float sway = 0.3f;
            var cloth = hero.Color;
            var dark = Color.Lerp(cloth, Color.black, 0.45f);

            foreach (var side in new[] { -0.09f, 0.09f })
            {
                mesh.Cylinder(feet + new Vector3(side, 0f, 0f), 0.065f, 0.075f, 0.3f, 6, dark, ArtSurface.Plain, sway * 0.2f);
                mesh.Box(feet + new Vector3(side, 0f, -0.03f), new Vector3(0.1f, 0.05f, 0.17f), Palette.DarkWood, ArtSurface.Plain, sway * 0.2f);
            }

            var waist = feet + Vector3.up * 0.3f;
            mesh.Cylinder(waist, 0.17f, 0.15f, 0.34f, 8, cloth, ArtSurface.Plain, sway);
            mesh.Cylinder(waist + Vector3.up * 0.13f, 0.165f, 0.165f, 0.05f, 8, Palette.DarkWood, ArtSurface.Plain, sway);
            foreach (var side in new[] { -1f, 1f })
            {
                mesh.Cylinder(waist + new Vector3(side * 0.2f, 0.02f, 0f), 0.045f, 0.06f, 0.3f, 6, cloth, ArtSurface.Plain, sway);
                mesh.Blob(waist + new Vector3(side * 0.2f, 0f, 0f), Vector3.one * 0.05f, Palette.Plaster, ArtSurface.Plain, sway, 0f, 6, 3);
            }

            // Scarf and a tail that catches the wind.
            var neck = waist + Vector3.up * 0.34f;
            mesh.Cylinder(neck - Vector3.up * 0.02f, 0.15f, 0.12f, 0.07f, 8, Palette.AccentRed, ArtSurface.Plain, sway);
            mesh.Box(neck + new Vector3(0.08f, -0.22f, 0.13f), new Vector3(0.09f, 0.24f, 0.03f), Palette.AccentRed, ArtSurface.Plain, 0.9f);

            // A large head and spiky hair: the chibi proportions keep it readable when zoomed out.
            var head = neck + Vector3.up * 0.17f;
            mesh.Blob(head, new Vector3(0.17f, 0.17f, 0.17f), new Color(0.96f, 0.80f, 0.66f), ArtSurface.Plain, sway, 0f, 8, 5);
            mesh.Box(head + new Vector3(0f, 0.02f, -0.15f), new Vector3(0.2f, 0.035f, 0.035f), new Color(0.12f, 0.11f, 0.12f), ArtSurface.Plain, sway);
            var hair = dark;
            mesh.Blob(head + new Vector3(0f, 0.07f, 0.03f), new Vector3(0.18f, 0.14f, 0.18f), hair, ArtSurface.Plain, sway, 0f, 8, 4);
            foreach (var spike in new[] { new Vector3(-0.11f, 0.15f, 0f), new Vector3(0f, 0.2f, 0.04f), new Vector3(0.11f, 0.15f, 0f), new Vector3(0f, 0.12f, 0.14f) })
                mesh.Cylinder(head + spike, 0.06f, 0f, 0.15f, 5, hair, ArtSurface.Plain, sway);
            mesh.Box(head + new Vector3(0f, 0.07f, -0.163f), new Vector3(0.33f, 0.05f, 0.02f), hero.Evolution >= 2 ? Palette.Amber : Palette.Iron, ArtSurface.Plain, sway);

            if (hero.Evolution >= 1)
            {
                // Evo I: a refined cape.
                mesh.Quad(neck + new Vector3(-0.15f, 0f, 0.14f), neck + new Vector3(0.15f, 0f, 0.14f), neck + new Vector3(0.2f, -0.56f, 0.2f), neck + new Vector3(-0.2f, -0.56f, 0.2f), Vector3.forward, dark, ArtSurface.Plain, 0.55f);
                mesh.Quad(neck + new Vector3(-0.15f, 0f, 0.14f), neck + new Vector3(0.15f, 0f, 0.14f), neck + new Vector3(0.2f, -0.56f, 0.2f), neck + new Vector3(-0.2f, -0.56f, 0.2f), Vector3.back, dark, ArtSurface.Plain, 0.55f);
            }
            if (hero.Evolution >= 2)
            {
                // Evo II: intricate shoulder guards.
                foreach (var side in new[] { -1f, 1f })
                    mesh.Frustum(neck + new Vector3(side * 0.2f, -0.05f, 0f), 0.2f, 0.18f, 0.1f, 0.1f, 0.07f, Palette.Amber, ArtSurface.Plain, sway);
            }
            if (hero.Evolution >= 3)
            {
                // Evo III: a crest, an energy halo and a ring at the feet.
                mesh.Frustum(head + new Vector3(0f, 0.2f, 0f), 0.2f, 0.07f, 0f, 0f, 0.26f, Palette.Amber, ArtSurface.Plain, sway, 0.35f);
                for (var i = 0; i < 10; i++)
                {
                    var a = i / 10f * Mathf.PI * 2f;
                    var p = head + new Vector3(Mathf.Cos(a) * 0.3f, 0.02f + Mathf.Sin(a) * 0.3f, 0.2f);
                    mesh.Blob(p, Vector3.one * 0.028f, cloth, ArtSurface.Plain, 0.6f, 1f, 4, 2);
                }
                mesh.Cylinder(feet + Vector3.up * 0.004f, 0.42f, 0.42f, 0.012f, 12, cloth, ArtSurface.Plain, 0f, 0.8f);
            }
        }

        // ---------- Summoning Gate ----------

        private static void Gate(MeshBuilder mesh, float w, float d)
        {
            var y = Foundation(mesh, w, d, 0.2f);
            mesh.Cylinder(new Vector3(0, y, 0), 1.55f, 1.42f, 0.12f, 12, Palette.Plaster, ArtSurface.Stone);
            y += 0.12f;
            Steps(mesh, new Vector3(0f, 0f, -(d * 0.5f - 0.05f)), 1.3f, 3, 0.1f, 0.12f);

            var span = 1.2f;
            var height = 2.95f;
            foreach (var side in new[] { -1f, 1f })
            {
                var at = new Vector3(side * span, y, 0f);
                mesh.Cylinder(at, 0.22f, 0.22f, 0.26f, 8, Palette.Stone, ArtSurface.Stone);
                mesh.Cylinder(at + Vector3.up * 0.26f, 0.15f, 0.13f, height - 0.26f, 8, Palette.AccentRed, ArtSurface.Wood);
                Lantern(mesh, new Vector3(side * (span + 0.42f), y, -0.7f));
            }

            // A double lintel, with the top beam upswept at both ends for a silhouette of its own.
            mesh.Box(new Vector3(0f, y + height - 0.68f, 0f), new Vector3(span * 2f + 0.5f, 0.16f, 0.18f), Palette.AccentRed, ArtSurface.Wood);
            mesh.Roof(new Vector3(0f, y + height - 0.04f, 0f), span + 0.75f, 0.42f, span + 0.2f, 0.06f, 0.26f, 0.42f, Palette.Slate, Palette.Amber);
            mesh.Box(new Vector3(0f, y + height - 0.14f, 0f), new Vector3(span * 2f + 0.42f, 0.14f, 0.2f), Palette.DarkWood, ArtSurface.Wood);
            Emblem(mesh, new Vector3(0f, y + height - 0.44f, -0.1f), Vector3.back, 0.2f);

            // The portal: stacked shimmering rings that drift, so it feels alive but stays cheap.
            var center = new Vector3(0f, y + 1.15f, 0.03f);
            const int points = 18;
            float[] radii = { 1.02f, 0.82f, 0.58f, 0.32f };
            Color[] tones = { new Color(0.28f, 0.14f, 0.62f), Palette.Portal, new Color(0.45f, 0.62f, 1f), new Color(0.82f, 0.92f, 1f) };
            for (var layer = 0; layer < radii.Length; layer++)
            {
                var depth = -0.012f * (layer + 1);
                for (var i = 0; i < points; i++)
                {
                    var a0 = (float)i / points * Mathf.PI * 2f;
                    var a1 = (float)(i + 1) / points * Mathf.PI * 2f;
                    var c = center + new Vector3(0f, 0f, depth);
                    var p0 = c + new Vector3(Mathf.Cos(a0), Mathf.Sin(a0) * 1.08f, 0f) * radii[layer];
                    var p1 = c + new Vector3(Mathf.Cos(a1), Mathf.Sin(a1) * 1.08f, 0f) * radii[layer];
                    mesh.Quad(c, c, p1, p0, Vector3.back, tones[layer], ArtSurface.Plain, 0.2f, 1f);
                }
            }
            for (var i = 0; i < 9; i++)
            {
                var a = i / 9f * Mathf.PI * 2f;
                var at = new Vector3(Mathf.Cos(a) * 1.3f, y + 0.02f, Mathf.Sin(a) * 1.3f - 0.05f);
                mesh.Box(at, new Vector3(0.12f, 0.03f, 0.12f), Palette.Crystal, ArtSurface.Plain, 0f, 0.9f);
            }
        }

        // ---------- Training Grounds ----------

        private static void TrainingYard(MeshBuilder mesh, float w, float d)
        {
            var y = Foundation(mesh, w, d, 0.1f);
            mesh.Box(new Vector3(0, y, 0), new Vector3(w - 0.3f, 0.03f, d - 0.3f), new Color(0.86f, 0.74f, 0.54f), ArtSurface.Dirt);
            y += 0.03f;
            var x = w * 0.5f - 0.22f;
            var z = d * 0.5f - 0.22f;
            for (var i = -3; i <= 3; i++)
            {
                mesh.Cylinder(new Vector3(i * x / 3f, y, z), 0.045f, 0.04f, 0.5f, 5, Palette.Wood, ArtSurface.Wood);
                if (i != 0) mesh.Cylinder(new Vector3(i * x / 3f, y, -z), 0.045f, 0.04f, 0.42f, 5, Palette.Wood, ArtSurface.Wood);
            }
            for (var i = -2; i <= 2; i++)
            {
                mesh.Cylinder(new Vector3(x, y, i * z / 2f), 0.045f, 0.04f, 0.42f, 5, Palette.Wood, ArtSurface.Wood);
                mesh.Cylinder(new Vector3(-x, y, i * z / 2f), 0.045f, 0.04f, 0.42f, 5, Palette.Wood, ArtSurface.Wood);
            }
            mesh.Box(new Vector3(0f, y + 0.4f, z), new Vector3(x * 2f, 0.05f, 0.05f), Palette.DarkWood, ArtSurface.Wood);
            mesh.Box(new Vector3(x, y + 0.34f, 0f), new Vector3(0.05f, 0.05f, z * 2f), Palette.DarkWood, ArtSurface.Wood);
            mesh.Box(new Vector3(-x, y + 0.34f, 0f), new Vector3(0.05f, 0.05f, z * 2f), Palette.DarkWood, ArtSurface.Wood);

            // Straw training dummies, one with a target ring.
            foreach (var dummy in new[] { new Vector3(-1.05f, 0, -0.2f), new Vector3(0f, 0, -0.4f), new Vector3(1.05f, 0, -0.2f) })
            {
                var at = dummy + Vector3.up * y;
                mesh.Cylinder(at, 0.05f, 0.04f, 0.7f, 5, Palette.Wood, ArtSurface.Wood);
                mesh.Box(at + Vector3.up * 0.45f, new Vector3(0.6f, 0.05f, 0.05f), Palette.Wood, ArtSurface.Wood);
                mesh.Cylinder(at + Vector3.up * 0.26f, 0.16f, 0.15f, 0.34f, 7, new Color(0.88f, 0.74f, 0.40f), ArtSurface.Wood);
                mesh.Blob(at + Vector3.up * 0.78f, Vector3.one * 0.11f, new Color(0.90f, 0.78f, 0.46f), ArtSurface.Wood, 0f, 0f, 6, 4);
            }

            // A small open shelter with a weapon rack gives the yard a back wall and some height.
            var shelter = new Vector3(0f, y, z - 0.32f);
            foreach (var side in new[] { -1f, 1f })
                Pillar(mesh, shelter + new Vector3(side * 1.05f, 0f, 0f), 0.95f, 0.05f, Palette.DarkWood);
            mesh.Roof(shelter + Vector3.up * 0.95f, 1.4f, 0.4f, 1.0f, 0.1f, 0.34f, 0.18f, Palette.RoofTeal, Palette.DarkWood);
            mesh.Box(shelter + new Vector3(0f, 0.5f, 0.02f), new Vector3(1.0f, 0.05f, 0.04f), Palette.DarkWood, ArtSurface.Wood);
            for (var i = -2; i <= 2; i++)
                mesh.Box(shelter + new Vector3(i * 0.2f, 0.12f, 0f), new Vector3(0.02f, 0.66f, 0.02f), i % 2 == 0 ? Palette.Iron : Palette.Wood);
        }

        // ---------- Research Hut ----------

        private static void ResearchHut(MeshBuilder mesh, float w, float d)
        {
            var y = Foundation(mesh, w, d, 0.2f);
            mesh.Cylinder(new Vector3(0, y, 0), 1.05f, 1.0f, 1.0f, 12, Palette.Plaster, ArtSurface.Plaster);
            for (var i = 0; i < 6; i++)
            {
                var a = i / 6f * Mathf.PI * 2f + 0.26f;
                mesh.Cylinder(new Vector3(Mathf.Cos(a) * 1.03f, y, Mathf.Sin(a) * 1.03f), 0.05f, 0.05f, 1.0f, 5, Palette.DarkWood, ArtSurface.Wood);
            }
            Door(mesh, new Vector3(0f, y, -1.02f), Vector3.back, 0.55f, 0.68f, Palette.DarkWood);
            Window(mesh, new Vector3(-1.02f, y + 0.36f, 0f), Vector3.left, 0.36f, 0.34f);

            var roof = new Vector3(0, y + 1f, 0);
            mesh.Cylinder(roof, 1.38f, 0.36f, 0.72f, 12, new Color(0.72f, 0.56f, 0.30f), ArtSurface.Wood, 0f, 0f, false);
            mesh.Cylinder(roof + Vector3.up * 0.72f, 0.36f, 0f, 0.52f, 12, new Color(0.80f, 0.64f, 0.36f), ArtSurface.Wood);
            mesh.Cylinder(roof + Vector3.up * 1.12f, 0.03f, 0f, 0.42f, 5, Palette.Amber);

            // A stone chimney, which also anchors the smoke effect.
            var chimney = new Vector3(0.62f, y + 1f, 0.3f);
            mesh.Box(chimney, new Vector3(0.24f, 0.72f, 0.24f), Palette.DarkStone, ArtSurface.Stone);

            // Scrolls and a low reading table out front.
            var table = new Vector3(0.82f, y, -0.78f);
            mesh.Box(table + Vector3.up * 0.14f, new Vector3(0.5f, 0.05f, 0.3f), Palette.Wood, ArtSurface.Wood);
            mesh.Box(table, new Vector3(0.42f, 0.14f, 0.22f), Palette.DarkWood, ArtSurface.Wood);
            mesh.Box(table + new Vector3(0.08f, 0.19f, 0f), new Vector3(0.3f, 0.05f, 0.08f), Palette.Plaster);
            Lantern(mesh, new Vector3(-0.8f, y, -0.8f), 0.8f);
        }

        // ---------- Mines ----------

        private static void Mine(MeshBuilder mesh, float w, ResourceKind produces)
        {
            var crystal = produces == ResourceKind.Crystals;
            var ore = crystal ? Palette.Crystal : Palette.Amber;
            mesh.Frustum(Vector3.zero, w - 0.08f, w - 0.08f, w - 0.2f, w - 0.2f, 0.1f, Palette.DarkStone, ArtSurface.Stone);
            var y = 0.1f;

            // A rock outcrop with the shaft cut into its face.
            var rock = new Color(0.56f, 0.52f, 0.48f);
            mesh.Blob(new Vector3(0.2f, y + 0.32f, 0.3f), new Vector3(1.2f, 0.98f, 1.0f), rock, ArtSurface.Stone, 0f, 0f, 9, 5, 0.1f, 11, new Color(0.70f, 0.66f, 0.60f));
            mesh.Blob(new Vector3(-0.62f, y + 0.14f, 0.5f), new Vector3(0.58f, 0.55f, 0.6f), rock, ArtSurface.Stone, 0f, 0f, 7, 4, 0.1f, 12, new Color(0.66f, 0.62f, 0.56f));

            var shaft = new Vector3(-0.1f, y, -0.62f);
            mesh.Box(shaft + new Vector3(0f, 0f, -0.02f), new Vector3(0.72f, 0.72f, 0.16f), new Color(0.07f, 0.06f, 0.07f));
            foreach (var side in new[] { -1f, 1f })
                mesh.Box(shaft + new Vector3(side * 0.4f, 0f, -0.08f), new Vector3(0.12f, 0.82f, 0.12f), Palette.Wood, ArtSurface.Wood);
            mesh.Box(shaft + new Vector3(0f, 0.8f, -0.08f), new Vector3(1.0f, 0.13f, 0.14f), Palette.Wood, ArtSurface.Wood);
            mesh.Frustum(shaft + new Vector3(0f, 0.93f, -0.08f), 1.18f, 0.42f, 0.2f, 0.1f, 0.2f, crystal ? Palette.RoofTeal : Palette.RoofRed, ArtSurface.Tile);
            Lantern(mesh, shaft + new Vector3(0.58f, 0f, -0.16f), 0.7f);

            // Rails and a loaded cart.
            var rail = new Vector3(-0.1f, y, -1.12f);
            foreach (var side in new[] { -0.15f, 0.15f })
                mesh.Box(rail + new Vector3(side, 0f, 0f), new Vector3(0.025f, 0.02f, 0.78f), Palette.Iron);
            for (var i = -1; i <= 1; i++)
                mesh.Box(rail + new Vector3(0f, 0f, i * 0.26f), new Vector3(0.42f, 0.012f, 0.07f), Palette.DarkWood);
            var cart = new Vector3(-0.1f, y + 0.04f, -1.15f);
            mesh.Frustum(cart, 0.32f, 0.38f, 0.42f, 0.5f, 0.22f, Palette.DarkWood, ArtSurface.Wood);
            OreCluster(mesh, cart + Vector3.up * 0.22f, ore, crystal, 0.13f, 3, 21);

            OreCluster(mesh, new Vector3(0.8f, y + 0.1f, -0.42f), ore, crystal, 0.2f, 5, 22);
            OreCluster(mesh, new Vector3(0.45f, y + 1.05f, -0.1f), ore, crystal, 0.24f, 4, 23);
            OreCluster(mesh, new Vector3(-0.82f, y + 0.42f, 0.1f), ore, crystal, 0.16f, 3, 24);
        }

        private static void OreCluster(MeshBuilder mesh, Vector3 at, Color color, bool crystal, float size, int count, int seed)
        {
            var random = new System.Random(seed);
            for (var i = 0; i < count; i++)
            {
                var offset = new Vector3((float)(random.NextDouble() - 0.5) * size * 2f, 0f, (float)(random.NextDouble() - 0.5) * size * 2f);
                var scale = size * (0.55f + (float)random.NextDouble() * 0.6f);
                if (crystal)
                    mesh.Frustum(at + offset, scale * 0.55f, scale * 0.55f, 0f, 0f, scale * 2.2f, color, ArtSurface.Plain, 0f, 0.75f);
                else
                    mesh.Blob(at + offset + Vector3.up * scale * 0.3f, Vector3.one * scale * 0.5f, color, ArtSurface.Plain, 0f, 0.45f, 5, 3, 0.15f, seed + i);
            }
        }

        // ---------- Gold Vault ----------

        private static void Vault(MeshBuilder mesh, float w, float d)
        {
            var y = Foundation(mesh, w, d, 0.2f);
            var bx = w - 0.75f;
            var bz = d - 0.75f;
            mesh.Frustum(new Vector3(0, y, 0), bx, bz, bx - 0.14f, bz - 0.14f, 1.05f, Palette.DarkStone, ArtSurface.Stone);
            foreach (var cx in new[] { -1f, 1f })
            foreach (var cz in new[] { -1f, 1f })
                mesh.Box(new Vector3(cx * bx * 0.46f, y, cz * bz * 0.46f), new Vector3(0.18f, 1.05f, 0.18f), Palette.Iron);

            // A heavy round vault door is what makes this building read as a treasury.
            var front = -(bz - 0.14f) * 0.5f;
            var center = new Vector3(0f, y + 0.5f, front - 0.04f);
            const int points = 14;
            for (var i = 0; i < points; i++)
            {
                var a0 = (float)i / points * Mathf.PI * 2f;
                var a1 = (float)(i + 1) / points * Mathf.PI * 2f;
                mesh.Quad(center, center, center + new Vector3(Mathf.Cos(a1), Mathf.Sin(a1), 0f) * 0.42f, center + new Vector3(Mathf.Cos(a0), Mathf.Sin(a0), 0f) * 0.42f, Vector3.back, Palette.Amber);
                var c2 = center + Vector3.back * 0.012f;
                mesh.Quad(c2, c2, c2 + new Vector3(Mathf.Cos(a1), Mathf.Sin(a1), 0f) * 0.33f, c2 + new Vector3(Mathf.Cos(a0), Mathf.Sin(a0), 0f) * 0.33f, Vector3.back, Palette.Iron);
            }
            var hub = center + Vector3.back * 0.03f;
            mesh.Quad(hub + new Vector3(-0.28f, -0.025f, 0f), hub + new Vector3(0.28f, -0.025f, 0f), hub + new Vector3(0.28f, 0.025f, 0f), hub + new Vector3(-0.28f, 0.025f, 0f), Vector3.back, Palette.Amber);
            mesh.Quad(hub + new Vector3(-0.025f, -0.28f, 0f), hub + new Vector3(0.025f, -0.28f, 0f), hub + new Vector3(0.025f, 0.28f, 0f), hub + new Vector3(-0.025f, 0.28f, 0f), Vector3.back, Palette.Amber);

            y += 1.05f;
            mesh.Box(new Vector3(0, y - 0.05f, 0), new Vector3(bx + 0.1f, 0.12f, bz + 0.1f), Palette.Amber);
            mesh.Roof(new Vector3(0, y + 0.05f, 0), bx * 0.5f + 0.4f, bz * 0.5f + 0.4f, 0.2f, 0.2f, 0.7f, 0.22f, Palette.Slate, Palette.Amber);
            mesh.Blob(new Vector3(0, y + 0.82f, 0), new Vector3(0.14f, 0.14f, 0.14f), Palette.Amber, ArtSurface.Plain, 0f, 0.3f, 6, 4);

            OreCluster(mesh, new Vector3(0.82f, 0.2f, -1.02f), Palette.Amber, false, 0.14f, 4, 31);
            Lantern(mesh, new Vector3(-0.92f, 0.2f, -1.15f), 0.8f);
        }

        // ---------- Defense ----------

        private static void WatchTower(MeshBuilder mesh, float w)
        {
            var y = Foundation(mesh, w, w, 0.14f);
            mesh.Frustum(new Vector3(0, y, 0), w - 0.5f, w - 0.5f, w - 0.82f, w - 0.82f, 1.25f, Palette.Stone, ArtSurface.Stone);
            mesh.Quad(new Vector3(-0.08f, y + 0.72f, -(w - 0.68f) * 0.5f - 0.02f), new Vector3(0.08f, y + 0.72f, -(w - 0.68f) * 0.5f - 0.02f), new Vector3(0.08f, y + 1.0f, -(w - 0.68f) * 0.5f - 0.02f), new Vector3(-0.08f, y + 1.0f, -(w - 0.68f) * 0.5f - 0.02f), Vector3.back, new Color(0.08f, 0.07f, 0.08f));
            y += 1.25f;

            // An overhanging timber platform with a railing.
            mesh.Box(new Vector3(0, y, 0), new Vector3(w - 0.2f, 0.1f, w - 0.2f), Palette.DarkWood, ArtSurface.Wood);
            y += 0.1f;
            var edge = (w - 0.32f) * 0.5f;
            foreach (var cx in new[] { -1f, 1f })
            foreach (var cz in new[] { -1f, 1f })
                Pillar(mesh, new Vector3(cx * edge, y, cz * edge), 0.65f, 0.045f, Palette.AccentRed);
            mesh.Box(new Vector3(0f, y + 0.22f, -edge), new Vector3(edge * 2f, 0.04f, 0.04f), Palette.Wood, ArtSurface.Wood);
            mesh.Box(new Vector3(-edge, y + 0.22f, 0f), new Vector3(0.04f, 0.04f, edge * 2f), Palette.Wood, ArtSurface.Wood);
            mesh.Box(new Vector3(0, y, 0), new Vector3(w - 0.75f, 0.45f, w - 0.75f), Palette.Plaster, ArtSurface.Plaster);

            // A mounted ballista, so a tower is visibly a weapon.
            mesh.Box(new Vector3(-0.08f, y + 0.45f, -0.08f), new Vector3(0.1f, 0.06f, 0.1f), Palette.DarkWood);
            mesh.Box(new Vector3(-0.08f, y + 0.51f, -0.3f), new Vector3(0.05f, 0.05f, 0.5f), Palette.Wood, ArtSurface.Wood);
            mesh.Box(new Vector3(-0.08f, y + 0.5f, -0.46f), new Vector3(0.5f, 0.03f, 0.03f), Palette.Iron);

            y += 0.65f;
            mesh.Roof(new Vector3(0, y, 0), w * 0.5f + 0.1f, w * 0.5f + 0.1f, 0.08f, 0.08f, 0.62f, 0.2f, Palette.RoofRed, Palette.Amber);
            mesh.Cylinder(new Vector3(0, y + 0.6f, 0), 0.02f, 0.015f, 0.4f, 5, Palette.DarkWood);
            mesh.Quad(new Vector3(0f, y + 0.98f, 0f), new Vector3(-0.28f, y + 0.92f, 0f), new Vector3(-0.28f, y + 0.8f, 0f), new Vector3(0f, y + 0.82f, 0f), Vector3.back, Palette.AccentRed, ArtSurface.Plain, 0.9f);
            Banner(mesh, new Vector3(0.36f, 1.2f, -(w - 0.72f) * 0.5f - 0.03f), Vector3.back, 0.2f, 0.55f, Palette.AccentRed);
        }

        private static void SpiritTurret(MeshBuilder mesh, float w)
        {
            var y = Foundation(mesh, w, w, 0.14f);
            mesh.Cylinder(new Vector3(0, y, 0), 0.62f, 0.52f, 0.34f, 8, Palette.DarkStone, ArtSurface.Stone);
            y += 0.34f;
            foreach (var cx in new[] { -1f, 1f })
            foreach (var cz in new[] { -1f, 1f })
            {
                var at = new Vector3(cx * 0.45f, y - 0.1f, cz * 0.45f);
                mesh.Frustum(at, 0.2f, 0.2f, 0.03f, 0.03f, 1.1f, Palette.Stone, ArtSurface.Stone);
                mesh.Box(at + Vector3.up * 0.52f, new Vector3(0.13f, 0.1f, 0.13f), Palette.AccentBlue, ArtSurface.Plain, 0f, 0.8f);
            }
            mesh.Cylinder(new Vector3(0, y, 0), 0.32f, 0.2f, 0.5f, 8, Palette.Iron);
            mesh.Blob(new Vector3(0, y + 1.02f, 0), Vector3.one * 0.3f, Palette.Crystal, ArtSurface.Plain, 0.35f, 1f, 8, 5);
            mesh.Cylinder(new Vector3(0, y + 0.98f, 0), 0.5f, 0.5f, 0.03f, 12, Palette.AccentBlue, ArtSurface.Plain, 0.35f, 0.9f);
        }

        private static void WallPiece(MeshBuilder mesh)
        {
            mesh.Box(Vector3.zero, new Vector3(0.6f, 0.45f, 0.6f), Palette.Stone, ArtSurface.Stone);
            mesh.Box(Vector3.up * 0.45f, new Vector3(0.54f, 0.4f, 0.54f), Palette.Plaster, ArtSurface.Plaster);
            mesh.Frustum(Vector3.up * 0.85f, 0.82f, 0.82f, 0.28f, 0.28f, 0.2f, Palette.Slate, ArtSurface.Tile);
        }
    }
}
