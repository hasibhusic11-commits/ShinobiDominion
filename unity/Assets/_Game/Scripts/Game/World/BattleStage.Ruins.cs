using ShinobiDominion.Core;
using UnityEngine;
using UnityEngine.Rendering;

namespace ShinobiDominion.Game
{
    /// <summary>
    /// What a fallen building leaves behind: burnt ground, the broken foot of its own walls and a few loose
    /// stones. The walls are cut from the building's own picture, so every ruin is the ruin of that building.
    /// </summary>
    public sealed partial class BattleStage
    {
        /// <summary>How many slices the broken wall line is cut into.</summary>
        private const int RuinColumns = 16;

        /// <summary>Soot over what is left of the walls.</summary>
        private static readonly Color Scorched = new Color(0.76f, 0.7f, 0.66f);

        private void AddRubble(BattleBuildingInput building)
        {
            var root = new GameObject("Rubble");
            root.transform.SetParent(transform, false);
            root.transform.localPosition = CenterOf(building);
            AddScorch(root.transform, building);
            var stand = _view != null ? _view.StandOf(building.id) : null;
            var walls = stand != null && AddStump(root.transform, building, stand);
            AddStones(root.transform, building, walls ? building.width * 2 : building.width * 3);
        }

        /// <summary>Burnt ground the size of the footprint, lying under every standing picture.</summary>
        private static void AddScorch(Transform root, BattleBuildingInput building)
        {
            var go = new GameObject("Scorch");
            go.transform.SetParent(root, false);
            go.transform.localPosition = new Vector3(0f, 0.02f, 0f);
            go.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            go.transform.localScale = new Vector3(building.width * 0.95f, building.depth * 0.95f, 1f);
            var renderer = go.AddComponent<SpriteRenderer>();
            renderer.sprite = BuildingSprites.ShadowSprite;
            renderer.color = new Color(1f, 1f, 1f, 0.85f);
            renderer.sortingOrder = BuildingSprites.ShadowOrder + 1;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
        }

        /// <summary>
        /// The foot of the two front walls, with a broken top. In the picture the footprint is a diamond as wide
        /// as the building's base whose front corner is the picture's pivot, so its front edges rise one pixel
        /// for every two across. The broken blocks stand along those edges. False when the picture cannot be cut.
        /// </summary>
        private bool AddStump(Transform root, BattleBuildingInput building, IsoStand stand)
        {
            var source = stand.Renderer;
            var art = source != null ? source.sprite : null;
            if (art == null || art.packed) return false;
            var rect = art.rect;
            var foot = BuildingSprites.BaseWidth(art) * art.pixelsPerUnit;
            if (foot < 16f || rect.width < 16f || rect.height < 16f) return false;

            var mid = art.pivot.x;
            var ground = art.pivot.y;
            var left = Mathf.Max(0f, mid - foot * 0.5f);
            var right = Mathf.Min(rect.width, mid + foot * 0.5f);
            var random = new System.Random(building.id * 53 + 7);
            // Each slice is a broken block whose top runs with the wall, so the line reads as masonry and not as
            // teeth. Low at the two side corners, ragged in between; some blocks stand as high as their neighbour.
            var points = new Vector2[RuinColumns * 4];
            var triangles = new ushort[RuinColumns * 6];
            var rise = 0f;
            for (var i = 0; i < RuinColumns; i++)
            {
                var end = i == 0 || i == RuinColumns - 1;
                var roll = 0.07f + (float)random.NextDouble() * 0.13f;
                if (end) rise = 0.04f;
                else if (i % 3 != 2) rise = roll;
                var v = i * 4;
                for (var side = 0; side < 2; side++)
                {
                    var x = Mathf.Lerp(left, right, (i + side) / (float)RuinColumns);
                    var edge = ground + Mathf.Abs(x - mid) * 0.5f;
                    points[v + side * 2] = new Vector2(x, Mathf.Clamp(edge - foot * 0.05f, 0f, rect.height));
                    points[v + side * 2 + 1] = new Vector2(x, Mathf.Clamp(edge + foot * rise, 0f, rect.height));
                }
                var t = i * 6;
                triangles[t] = (ushort)v;
                triangles[t + 1] = (ushort)(v + 1);
                triangles[t + 2] = (ushort)(v + 3);
                triangles[t + 3] = (ushort)v;
                triangles[t + 4] = (ushort)(v + 3);
                triangles[t + 5] = (ushort)(v + 2);
            }

            var pivot = new Vector2(art.pivot.x / rect.width, art.pivot.y / rect.height);
            var cut = Sprite.Create(art.texture, rect, pivot, art.pixelsPerUnit, 0, SpriteMeshType.FullRect);
            cut.name = art.name;
            cut.OverrideGeometry(points, triangles);
            _fxOwned.Add(cut);

            var go = new GameObject("Stump");
            go.transform.SetParent(root, false);
            go.transform.SetPositionAndRotation(source.transform.position, source.transform.rotation);
            // The picture's size at rest, without any squash it was in the middle of when it fell.
            var size = stand.Footprint() * GridSpace.CellSize * stand.ArtSize * BuildingSprites.PictureShare;
            var scale = (size.x + size.y) * 0.70710678f / Mathf.Max(BuildingSprites.BaseWidth(art), 0.001f);
            go.transform.localScale = new Vector3(scale, scale * BuildingSprites.Lift, 1f);
            var renderer = go.AddComponent<SpriteRenderer>();
            renderer.sprite = cut;
            renderer.color = Scorched;
            renderer.flipX = source.flipX;
            // One step under the picture it came from, so the falling building uncovers it as it sinks.
            renderer.sortingOrder = source.sortingOrder - 1;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            return true;
        }

        /// <summary>Loose stones over the burnt ground.</summary>
        private void AddStones(Transform root, BattleBuildingInput building, int count)
        {
            var builder = new MeshBuilder { ContactShade = false };
            var dark = new Color(0.26f, 0.24f, 0.23f);
            var light = new Color(0.46f, 0.43f, 0.4f);
            var random = new System.Random(building.id * 31);
            for (var i = 0; i < count; i++)
            {
                var offset = new Vector3(((float)random.NextDouble() - 0.5f) * building.width * 0.7f, 0.07f, ((float)random.NextDouble() - 0.5f) * building.depth * 0.7f);
                var size = 0.08f + (float)random.NextDouble() * 0.12f;
                builder.Blob(offset, new Vector3(size, size * 0.75f, size), i % 3 == 0 ? dark : light, ArtSurface.Rock, 0f, 0f, 5, 3, 0.15f, i);
            }
            var go = new GameObject("Stones");
            go.transform.SetParent(root, false);
            var mesh = builder.Build("Stones");
            _ownedMeshes.Add(mesh);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterials = builder.LitMaterials();
            renderer.shadowCastingMode = ShadowCastingMode.Off;
        }
    }
}
