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
    /// Battle effect pictures under Resources/Effects (shots, sparks, bursts) turn about their middle.
    /// On a desktop every picture stays uncompressed. On Android and iOS each kind is compressed with ASTC (ADR-054):
    /// uncompressed, the building pictures alone are about 160 MB of memory, more than a modest phone can give.
    /// The phone settings have not been looked at on a phone yet.
    /// </summary>
    public sealed class UiSpriteImporter : AssetPostprocessor
    {
        // "_s24" slices 24 px on every side; "_s80x40" slices 80 on the left and right and 40 on the top and bottom
        // (a ribbon with wide ends and a short height, UI kit v3, docs/ui/overhaul).
        private static readonly Regex Border = new Regex(@"_s(\d+)(?:x(\d+))?$");

        /// <summary>Raising this reimports every texture the rules apply to.</summary>
        public override uint GetVersion() => 5;

        private static readonly string[] Phones = { "Android", "iPhone" };

        /// <summary>The same picture on a phone: compressed, and no larger than it is ever shown.</summary>
        private static void OnPhones(TextureImporter importer, int maxSize, TextureImporterFormat format)
        {
            foreach (var platform in Phones)
            {
                var settings = importer.GetPlatformTextureSettings(platform);
                settings.overridden = true;
                settings.maxTextureSize = maxSize;
                settings.format = format;
                settings.compressionQuality = 100;
                importer.SetPlatformTextureSettings(settings);
            }
        }

        private void OnPreprocessTexture()
        {
            var path = assetPath.Replace('\\', '/');
            var ui = path.Contains("/Resources/UI/");
            var building = path.Contains("/Resources/Buildings/") || path.Contains("/Resources/Props/");
            var effect = path.Contains("/Resources/Effects/");
            if (path.Contains("/Resources/Map/"))
            {
                // The owner's painted village map, laid on the ground (VillageMap): a plain texture, full quality.
                var map = (TextureImporter)assetImporter;
                map.textureType = TextureImporterType.Default;
                map.npotScale = TextureImporterNPOTScale.None;
                map.mipmapEnabled = true;
                map.wrapMode = TextureWrapMode.Clamp;
                map.filterMode = FilterMode.Trilinear;
                map.anisoLevel = 4;
                map.maxTextureSize = 2048;
                map.textureCompression = TextureImporterCompression.Uncompressed;
                OnPhones(map, 2048, TextureImporterFormat.ASTC_6x6);
                return;
            }
            if (path.Contains("/Resources/HeroConcepts/"))
            {
                // The hero paintings (HeroConcepts), cropped into cards at run time: full quality, as the UI art is.
                // Compressed, their fine painted detail went blocky and grainy on the cards (owner, 2026-11-09).
                var painting = (TextureImporter)assetImporter;
                painting.mipmapEnabled = true;
                painting.mipmapFilter = TextureImporterMipFilter.KaiserFilter;
                painting.filterMode = FilterMode.Trilinear;
                painting.anisoLevel = 2;
                painting.textureCompression = TextureImporterCompression.Uncompressed;
                // The finest block size: these went blocky when compressed harder.
                OnPhones(painting, 1024, TextureImporterFormat.ASTC_4x4);
                return;
            }
            if (!ui && !building && !effect) return;
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
            // Interface art has hard edges and thin rims, so it keeps the finest block size; buildings are painted and
            // shown small, effects are small and brief.
            OnPhones(importer, effect ? 256 : ui ? 2048 : 1024, ui ? TextureImporterFormat.ASTC_4x4 : TextureImporterFormat.ASTC_6x6);

            var name = System.IO.Path.GetFileNameWithoutExtension(path);
            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            settings.spriteMeshType = SpriteMeshType.FullRect;
            if (effect)
            {
                // Drawn small and often, so half size is plenty; the pivot stays in the middle so they can spin.
                importer.maxTextureSize = 256;
                settings.spriteAlignment = (int)SpriteAlignment.Center;
            }
            else if (ui)
            {
                var match = Border.Match(name);
                if (match.Success)
                {
                    var border = float.Parse(match.Groups[1].Value);
                    var vertical = match.Groups[2].Success ? float.Parse(match.Groups[2].Value) : border;
                    settings.spriteBorder = new Vector4(border, vertical, border, vertical);
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
