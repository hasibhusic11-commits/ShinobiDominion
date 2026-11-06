using UnityEngine;

namespace ShinobiDominion.Game
{
    /// <summary>
    /// A building's texture pack: its colours and surface materials at one visual tier. Each building
    /// has its own colour, and each of the five tiers changes what it is made of. All art is generated.
    /// </summary>
    public sealed class BuildingSkin
    {
        public const int Tiers = 5;

        public Color Stone = Palette.Stone;
        public Color DarkStone = Palette.DarkStone;
        public Color Slate = Palette.Slate;
        public Color RoofRed = Palette.RoofRed;
        public Color RoofTeal = Palette.RoofTeal;
        public Color Wood = Palette.Wood;
        public Color DarkWood = Palette.DarkWood;
        public Color Plaster = Palette.Plaster;
        public Color Amber = Palette.Amber;
        public Color AccentRed = Palette.AccentRed;
        public Color AccentBlue = Palette.AccentBlue;
        public Color Crystal = Palette.Crystal;
        public Color WindowGlow = Palette.WindowGlow;
        public Color Portal = Palette.Portal;
        public Color Flame = Palette.Flame;
        public Color Iron = Palette.Iron;

        public ArtSurface StoneSurface = ArtSurface.Stone;
        public ArtSurface WallSurface = ArtSurface.Plaster;
        public ArtSurface RoofSurface = ArtSurface.Tile;
        public int Tier;

        /// <summary>Levels 1-3, 4-6, 7-9, 10-12 and 13-15.</summary>
        public static int TierForLevel(int level) => Mathf.Clamp((level - 1) / 3, 0, Tiers - 1);

        private static Color C(float r, float g, float b) => new Color(r, g, b);

        /// <summary>The colour that identifies each building, whatever it is made of.</summary>
        private static Color Signature(string buildingId)
        {
            switch (buildingId)
            {
                case "dojo_hq": return C(0.30f, 0.38f, 0.52f);
                case "builder_hall": return C(0.70f, 0.24f, 0.18f);
                case "hero_altar": return C(0.20f, 0.50f, 0.50f);
                case "summoning_gate": return C(0.38f, 0.26f, 0.62f);
                case "training_grounds": return C(0.26f, 0.46f, 0.28f);
                case "troop_camp": return C(0.52f, 0.34f, 0.22f);
                case "research_hut": return C(0.72f, 0.56f, 0.30f);
                case "defense_tower": return C(0.66f, 0.16f, 0.12f);
                case "spirit_turret": return C(0.20f, 0.42f, 0.86f);
                case "gold_mine": return C(0.76f, 0.46f, 0.12f);
                case "gold_vault": return C(0.22f, 0.26f, 0.36f);
                case "crystal_mine": return C(0.16f, 0.56f, 0.70f);
                case "hero_base": return C(0.28f, 0.30f, 0.58f);
                default: return C(0.40f, 0.42f, 0.44f);
            }
        }

        /// <summary>Stone and metal buildings are never roofed in straw.</summary>
        private static bool CanBeThatched(string buildingId) =>
            buildingId != "gold_vault" && buildingId != "spirit_turret" && buildingId != "hero_base" && buildingId != "summoning_gate";

        public static BuildingSkin For(string buildingId, int level)
        {
            var tier = TierForLevel(level);
            var signature = Signature(buildingId);
            var skin = new BuildingSkin { Tier = tier };
            var roof = signature;

            switch (tier)
            {
                case 0: // Timber and straw: a young village, built from what's to hand.
                    skin.Plaster = C(0.78f, 0.66f, 0.48f);
                    skin.Stone = C(0.62f, 0.58f, 0.52f);
                    skin.DarkStone = C(0.46f, 0.43f, 0.40f);
                    skin.StoneSurface = ArtSurface.Rock;
                    skin.Wood = C(0.60f, 0.42f, 0.26f);
                    skin.DarkWood = C(0.40f, 0.27f, 0.17f);
                    skin.AccentRed = Color.Lerp(Palette.AccentRed, skin.Wood, 0.55f);
                    skin.Amber = C(0.72f, 0.55f, 0.32f);
                    if (CanBeThatched(buildingId))
                    {
                        roof = Color.Lerp(C(0.74f, 0.60f, 0.34f), signature, 0.32f);
                        skin.RoofSurface = ArtSurface.Thatch;
                    }
                    else
                    {
                        roof = signature * 0.85f;
                    }
                    break;

                case 1: // Plaster and fired tile: the village's standard palette.
                    break;

                case 2: // Cut stone: walls become masonry, timber darkens and brass appears.
                    skin.Plaster = C(0.84f, 0.84f, 0.82f);
                    skin.WallSurface = ArtSurface.Stone;
                    skin.Stone = C(0.74f, 0.75f, 0.78f);
                    skin.DarkStone = C(0.50f, 0.52f, 0.57f);
                    skin.Wood = C(0.40f, 0.25f, 0.16f);
                    skin.DarkWood = C(0.22f, 0.14f, 0.11f);
                    roof = Color.Lerp(signature, Color.white, 0.1f);
                    break;

                case 3: // Lacquer and iron: dark, heavy and fortified, with metal roofs.
                    skin.Plaster = Color.Lerp(signature, Color.black, 0.58f);
                    skin.WallSurface = ArtSurface.Wood;
                    skin.Stone = C(0.32f, 0.33f, 0.38f);
                    skin.DarkStone = C(0.18f, 0.18f, 0.22f);
                    skin.Wood = C(0.24f, 0.12f, 0.11f);
                    skin.DarkWood = C(0.11f, 0.08f, 0.09f);
                    skin.AccentRed = C(0.56f, 0.09f, 0.09f);
                    skin.Amber = C(1f, 0.78f, 0.26f);
                    skin.WindowGlow = C(1f, 0.56f, 0.18f);
                    skin.Iron = C(0.22f, 0.23f, 0.28f);
                    roof = Color.Lerp(signature, Color.black, 0.22f);
                    skin.RoofSurface = ArtSurface.Metal;
                    break;

                default: // Marble and gold: the mark of a finished building.
                    skin.Plaster = C(0.96f, 0.94f, 0.90f);
                    skin.WallSurface = ArtSurface.Marble;
                    skin.Stone = C(0.92f, 0.90f, 0.86f);
                    skin.DarkStone = C(0.72f, 0.70f, 0.68f);
                    skin.StoneSurface = ArtSurface.Marble;
                    skin.Wood = C(0.78f, 0.60f, 0.32f);
                    skin.DarkWood = C(0.60f, 0.44f, 0.18f);
                    skin.AccentRed = signature;
                    skin.Amber = C(1f, 0.84f, 0.30f);
                    skin.WindowGlow = C(0.80f, 0.96f, 1f);
                    skin.Iron = C(0.78f, 0.64f, 0.30f);
                    roof = Color.Lerp(signature, Color.white, 0.14f);
                    break;
            }

            skin.Slate = roof;
            skin.RoofRed = roof;
            skin.RoofTeal = roof;
            return skin;
        }
    }
}
