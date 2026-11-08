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
    }
}
