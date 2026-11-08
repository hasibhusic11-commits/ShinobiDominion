using UnityEngine;

namespace ShinobiDominion.Game
{
    /// <summary>
    /// Hero and troop figures drawn in code, for battles and portraits (HeroArt.cs, TroopArt.cs). Buildings and
    /// walls are the owner's painted sprites (BuildingSprites).
    /// </summary>
    public static partial class BuildingMeshes
    {
        /// <summary>How high a hero stands when it fights from its Hero Base in a battle.</summary>
        public const float PedestalTop = 0.39f;

        private static readonly Color Burlap = new Color(0.72f, 0.60f, 0.42f);
        private static readonly Color Paper = new Color(0.96f, 0.94f, 0.88f);

        /// <summary>A hero on its own, for walking around in battle.</summary>
        public static MeshBuilder Figure(HeroLook hero)
        {
            var mesh = new MeshBuilder { ContactShade = false };
            HeroFigure(mesh, Vector3.zero, hero);
            return mesh;
        }

        /// <summary>A flat strip standing just proud of a surface: straps, sashes and rails on the figures.</summary>
        private static void Strip(MeshBuilder mesh, Vector3 a, Vector3 b, float width, Vector3 facing, Color color, ArtSurface surface = ArtSurface.Plain, float sway = 0f)
        {
            var side = Vector3.Cross((b - a).normalized, facing).normalized * width * 0.5f;
            var lift = facing.normalized * 0.012f;
            mesh.Quad(a - side + lift, b - side + lift, b + side + lift, a + side + lift, facing, color, surface, sway);
        }
    }
}
