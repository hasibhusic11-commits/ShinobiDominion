using ShinobiDominion.Core;
using UnityEngine;

namespace ShinobiDominion.Game
{
    /// <summary>Small soldiers and the camps they live in. Original, and generated in code.</summary>
    public static partial class BuildingMeshes
    {
        private static readonly Color Skin = new Color(0.94f, 0.78f, 0.64f);

        /// <summary>Each troop has its own outline, so an army can be read at a glance.</summary>
        public static MeshBuilder Troop(string troopId)
        {
            var mesh = new MeshBuilder { ContactShade = false };
            switch (troopId)
            {
                case "kunai_thrower":
                    Soldier(mesh, new Color(0.30f, 0.46f, 0.36f), new Color(0.20f, 0.28f, 0.24f), 1f);
                    Strip(mesh, new Vector3(0.14f, 0.42f, -0.06f), new Vector3(0.24f, 0.62f, -0.06f), 0.03f, Vector3.back, new Color(0.78f, 0.80f, 0.84f));
                    mesh.Box(new Vector3(-0.1f, 0.3f, 0.12f), new Vector3(0.12f, 0.2f, 0.06f), new Color(0.45f, 0.30f, 0.18f));
                    break;
                case "shadow_runner":
                    Soldier(mesh, new Color(0.20f, 0.19f, 0.27f), new Color(0.11f, 0.10f, 0.16f), 0.95f);
                    mesh.Box(new Vector3(0f, 0.68f, -0.13f), new Vector3(0.22f, 0.06f, 0.02f), new Color(0.11f, 0.10f, 0.16f));
                    Strip(mesh, new Vector3(-0.1f, 0.62f, 0.14f), new Vector3(0.12f, 0.2f, 0.14f), 0.025f, Vector3.back, new Color(0.78f, 0.80f, 0.84f));
                    break;
                case "shield_bearer":
                    Soldier(mesh, new Color(0.46f, 0.46f, 0.50f), new Color(0.26f, 0.26f, 0.30f), 1.15f);
                    mesh.Blob(new Vector3(0.05f, 0.42f, -0.2f), new Vector3(0.26f, 0.3f, 0.04f), new Color(0.56f, 0.24f, 0.18f), ArtSurface.Plain, 0f, 0f, 8, 4);
                    mesh.Blob(new Vector3(0.05f, 0.42f, -0.24f), new Vector3(0.08f, 0.08f, 0.02f), new Color(0.90f, 0.72f, 0.28f), ArtSurface.Plain, 0f, 0f, 6, 3);
                    break;
                case "iron_ox_rider":
                    var hide = new Color(0.34f, 0.28f, 0.26f);
                    mesh.Blob(new Vector3(0f, 0.42f, 0f), new Vector3(0.28f, 0.28f, 0.52f), hide, ArtSurface.Plain, 0f, 0f, 8, 5);
                    mesh.Blob(new Vector3(0f, 0.5f, -0.52f), new Vector3(0.19f, 0.18f, 0.2f), hide, ArtSurface.Plain, 0f, 0f, 7, 4);
                    foreach (var side in new[] { -1f, 1f })
                    {
                        mesh.Cylinder(new Vector3(side * 0.17f, 0f, -0.28f), 0.06f, 0.07f, 0.24f, 5, hide, ArtSurface.Plain, 0f, 0f, false);
                        mesh.Cylinder(new Vector3(side * 0.17f, 0f, 0.28f), 0.06f, 0.07f, 0.24f, 5, hide, ArtSurface.Plain, 0f, 0f, false);
                        mesh.Cylinder(new Vector3(side * 0.16f, 0.58f, -0.56f), 0.05f, 0f, 0.2f, 5, new Color(0.86f, 0.84f, 0.76f), ArtSurface.Plain, 0f, 0f, false);
                    }
                    mesh.Box(new Vector3(0f, 0.68f, 0f), new Vector3(0.42f, 0.04f, 0.5f), new Color(0.32f, 0.33f, 0.38f));
                    mesh.Cylinder(new Vector3(0f, 0.72f, 0f), 0.12f, 0.11f, 0.3f, 7, new Color(0.56f, 0.20f, 0.16f), ArtSurface.Plain, 0f, 0f, false);
                    mesh.Blob(new Vector3(0f, 1.1f, 0f), Vector3.one * 0.12f, Skin, ArtSurface.Plain, 0f, 0f, 7, 4);
                    mesh.Blob(new Vector3(0f, 1.15f, 0f), new Vector3(0.13f, 0.09f, 0.13f), new Color(0.30f, 0.31f, 0.36f), ArtSurface.Plain, 0f, 0f, 7, 3);
                    break;
                default: // Militia
                    Soldier(mesh, new Color(0.62f, 0.46f, 0.26f), new Color(0.36f, 0.25f, 0.15f), 1f);
                    mesh.Cylinder(new Vector3(0f, 0.74f, 0f), 0.2f, 0f, 0.1f, 8, new Color(0.84f, 0.72f, 0.42f), ArtSurface.Plain, 0f, 0f, false);
                    Strip(mesh, new Vector3(0.16f, 0.1f, -0.04f), new Vector3(0.2f, 0.78f, -0.04f), 0.025f, Vector3.back, new Color(0.45f, 0.30f, 0.18f));
                    Strip(mesh, new Vector3(0.2f, 0.78f, -0.04f), new Vector3(0.205f, 0.92f, -0.04f), 0.04f, Vector3.back, new Color(0.72f, 0.74f, 0.78f));
                    break;
            }
            return mesh;
        }

        private static void Soldier(MeshBuilder mesh, Color cloth, Color dark, float scale)
        {
            foreach (var side in new[] { -0.06f, 0.06f })
                mesh.Cylinder(new Vector3(side, 0f, 0f) * scale, 0.045f * scale, 0.05f * scale, 0.22f * scale, 5, dark, ArtSurface.Plain, 0f, 0f, false);
            mesh.Cylinder(Vector3.up * 0.22f * scale, 0.12f * scale, 0.1f * scale, 0.26f * scale, 7, cloth, ArtSurface.Plain);
            foreach (var side in new[] { -0.15f, 0.15f })
                mesh.Cylinder(new Vector3(side, 0.26f, 0f) * scale, 0.035f * scale, 0.04f * scale, 0.2f * scale, 5, cloth, ArtSurface.Plain, 0f, 0f, false);
            mesh.Blob(Vector3.up * 0.6f * scale, Vector3.one * 0.13f * scale, Skin, ArtSurface.Plain, 0f, 0f, 7, 4);
        }

        /// <summary>Tents around a fire, with a small flag: a camp, not a house.</summary>
        private static void Camp(MeshBuilder mesh, float w, float d)
        {
            var y = Foundation(mesh, w, d, 0.1f);
            mesh.Box(new Vector3(0f, y, 0f), new Vector3(w - 0.32f, 0.02f, d - 0.32f), new Color(0.72f, 0.60f, 0.42f), ArtSurface.Dirt);
            y += 0.02f;

            void Tent(Vector3 at, float size)
            {
                var canvas = Color.Lerp(S.Plaster, S.Slate, 0.28f);
                var half = size * 0.5f;
                var top = at + Vector3.up * size * 0.8f;
                mesh.Quad(at + new Vector3(-half, 0f, -half), at + new Vector3(half, 0f, -half), top + new Vector3(half * 0.8f, 0f, 0f), top + new Vector3(-half * 0.8f, 0f, 0f), Vector3.back + Vector3.up, canvas, S.WallSurface, 0.15f);
                mesh.Quad(at + new Vector3(-half, 0f, half), at + new Vector3(half, 0f, half), top + new Vector3(half * 0.8f, 0f, 0f), top + new Vector3(-half * 0.8f, 0f, 0f), Vector3.forward + Vector3.up, canvas, S.WallSurface, 0.15f);
                mesh.Quad(at + new Vector3(-half, 0f, -half), at + new Vector3(-half, 0f, half), top + new Vector3(-half * 0.8f, 0f, 0f), top + new Vector3(-half * 0.8f, 0f, 0f), Vector3.left, S.Slate, S.RoofSurface);
                mesh.Quad(at + new Vector3(half, 0f, -half), at + new Vector3(half, 0f, half), top + new Vector3(half * 0.8f, 0f, 0f), top + new Vector3(half * 0.8f, 0f, 0f), Vector3.right, S.Slate, S.RoofSurface);
                Strip(mesh, top + new Vector3(-half * 0.8f, 0f, 0f), top + new Vector3(half * 0.8f, 0f, 0f), 0.05f, Vector3.up, S.DarkWood);
                // The dark opening that says someone sleeps here.
                mesh.Quad(at + new Vector3(-half - 0.012f, 0f, -half * 0.35f), at + new Vector3(-half - 0.012f, 0f, half * 0.35f), at + new Vector3(-half * 0.92f, size * 0.42f, 0f), at + new Vector3(-half * 0.92f, size * 0.42f, 0f), Vector3.left, new Color(0.08f, 0.07f, 0.08f));
            }

            Tent(new Vector3(0.62f, y, 0.62f), 1.05f);
            Tent(new Vector3(-0.62f, y, 0.68f), 0.85f);
            Tent(new Vector3(0.72f, y, -0.62f), 0.85f);

            // A ring of stones around the fire.
            var fire = new Vector3(-0.42f, y, -0.42f);
            for (var i = 0; i < 7; i++)
            {
                var a = i * Mathf.PI * 2f / 7f;
                mesh.Blob(fire + new Vector3(Mathf.Cos(a), 0.03f, Mathf.Sin(a)) * 0.23f, new Vector3(0.06f, 0.04f, 0.06f), S.DarkStone, ArtSurface.Plain, 0f, 0f, 5, 3);
            }
            mesh.Cylinder(fire, 0.13f, 0f, 0.34f, 6, S.Flame, ArtSurface.Plain, 0.5f, 1f);
            mesh.Box(fire + new Vector3(0f, 0f, 0f), new Vector3(0.28f, 0.03f, 0.05f), S.DarkWood);

            // Weapons stacked ready, and a camp flag.
            var pole = new Vector3(-w * 0.5f + 0.32f, y, d * 0.5f - 0.3f);
            mesh.Cylinder(pole, 0.03f, 0.022f, 1.45f, 5, S.DarkWood, ArtSurface.Wood);
            mesh.Quad(pole + Vector3.up * 1.42f, pole + new Vector3(0.48f, 1.34f, 0f), pole + new Vector3(0.48f, 1.1f, 0f), pole + Vector3.up * 1.04f, Vector3.back, S.Slate, ArtSurface.Plain, 0.8f);
            foreach (var offset in new[] { -0.06f, 0.06f })
                Strip(mesh, new Vector3(0.06f + offset, y, -1.1f), new Vector3(0.06f - offset, y + 0.55f, -1.1f), 0.022f, Vector3.back, S.Wood);
            Barrel(mesh, new Vector3(-1.05f, y, 0.08f), 0.8f);
            Sack(mesh, new Vector3(-0.62f, y, -1.1f), 0.8f, Burlap);
        }
    }
}
