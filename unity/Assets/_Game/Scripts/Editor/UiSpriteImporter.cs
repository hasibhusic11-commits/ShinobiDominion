using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

namespace ShinobiDominion.EditorTools
{
    /// <summary>
    /// Imports everything under Resources/UI and Resources/Buildings as smooth sprites. A UI file named
    /// name_s26.png gets a 26 pixel nine-slice border on every side, so panels and plates stretch without
    /// smearing their rims. Mipmaps are on: the owner's art is painted large and shown small, and without them
    /// it shimmers and looks pixelated.
    /// Building sprites stand on their bottom edge (the front corner of the footprint). The two wall arms stand
    /// on the corner where they meet: the Z arm on its bottom right, the X arm on its bottom left.
    /// </summary>
    public sealed class UiSpriteImporter : AssetPostprocessor
    {
        private static readonly Regex Border = new Regex(@"_s(\d+)$");

        /// <summary>Raising this reimports every texture the rules apply to.</summary>
        public override uint GetVersion() => 2;

        private void OnPreprocessTexture()
        {
            var path = assetPath.Replace('\\', '/');
            var ui = path.Contains("/Resources/UI/");
            var building = path.Contains("/Resources/Buildings/");
            if (!ui && !building) return;
            var importer = (TextureImporter)assetImporter;
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.mipmapEnabled = true;
            importer.mipmapFilter = TextureImporterMipFilter.KaiserFilter;
            importer.alphaIsTransparency = true;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.filterMode = FilterMode.Trilinear;
            importer.anisoLevel = 2;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.spritePixelsPerUnit = 100f;

            var name = System.IO.Path.GetFileNameWithoutExtension(path);
            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            settings.spriteMeshType = SpriteMeshType.FullRect;
            if (ui)
            {
                var match = Border.Match(name);
                if (match.Success)
                {
                    var border = float.Parse(match.Groups[1].Value);
                    settings.spriteBorder = new Vector4(border, border, border, border);
                }
            }
            else
            {
                settings.spriteAlignment = (int)SpriteAlignment.Custom;
                settings.spritePivot = name.StartsWith("wall_arm_z") ? new Vector2(1f, 0f)
                    : name.StartsWith("wall_arm_x") ? new Vector2(0f, 0f)
                    : new Vector2(0.5f, 0f);
            }
            importer.SetTextureSettings(settings);
        }
    }
}
