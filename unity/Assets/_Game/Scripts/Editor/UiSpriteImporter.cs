using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

namespace ShinobiDominion.EditorTools
{
    /// <summary>
    /// Imports everything under Resources/UI as crisp UI sprites. A file named name_s26.png gets a 26 pixel
    /// nine-slice border on every side, so panels and plates stretch without smearing their rims.
    /// </summary>
    public sealed class UiSpriteImporter : AssetPostprocessor
    {
        private static readonly Regex Border = new Regex(@"_s(\d+)$");

        private void OnPreprocessTexture()
        {
            if (!assetPath.Replace('\\', '/').Contains("/Resources/UI/")) return;
            var importer = (TextureImporter)assetImporter;
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.filterMode = FilterMode.Bilinear;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.spritePixelsPerUnit = 100f;

            var name = System.IO.Path.GetFileNameWithoutExtension(assetPath);
            var match = Border.Match(name);
            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            settings.spriteMeshType = SpriteMeshType.FullRect;
            if (match.Success)
            {
                var border = float.Parse(match.Groups[1].Value);
                settings.spriteBorder = new Vector4(border, border, border, border);
            }
            importer.SetTextureSettings(settings);
        }
    }
}
