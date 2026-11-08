using System.Collections.Generic;
using ShinobiDominion.Core;
using UnityEngine;

namespace ShinobiDominion.Game
{
    /// <summary>
    /// The owner's painted scenery (Resources/Props): cherry trees, rocks, bushes, lanterns and fox statues scattered
    /// over the open ground, thicker towards the map's edges and in a band just outside it. Decoration only: props
    /// take no grid cells, block nothing and are never tapped. A prop hides while any building or wall stands on or
    /// next to its cell, so the village always has room and props reappear when a building moves away.
    /// </summary>
    public sealed class VillageProps
    {
        private sealed class Prop
        {
            public GridCoord Cell;
            public GameObject Go;
        }

        private struct Kind
        {
            public string Sprite;
            public float Width;
            public float Weight;
            public float Shadow;
        }

        // Screen widths in world units. Trees are the biggest; lanterns and statues are small accents.
        private static readonly Kind[] Kinds =
        {
            new Kind { Sprite = "tree_blossom_1", Width = 1.7f, Weight = 5f, Shadow = 1.3f },
            new Kind { Sprite = "bush_1", Width = 1.0f, Weight = 3f, Shadow = 0.9f },
            new Kind { Sprite = "rock_1", Width = 0.55f, Weight = 2f, Shadow = 0.5f },
            new Kind { Sprite = "rock_2", Width = 0.95f, Weight = 1.5f, Shadow = 0.8f },
            new Kind { Sprite = "rock_3", Width = 1.1f, Weight = 1.5f, Shadow = 0.9f },
            new Kind { Sprite = "stone_lantern", Width = 0.45f, Weight = 0.8f, Shadow = 0.4f },
            new Kind { Sprite = "fox_statue", Width = 0.55f, Weight = 0.5f, Shadow = 0.45f },
        };

        private const int Seed = 7331;
        private const int Margin = 6;

        private readonly Village _village;
        private readonly List<Prop> _props = new List<Prop>();

        public int Count => _props.Count;

        private Transform _root;

        public void SetVisible(bool visible) { if (_root != null) _root.gameObject.SetActive(visible); }

        public VillageProps(Village village, Transform parent)
        {
            _village = village;
            var root = new GameObject("Props").transform;
            _root = root;
            root.SetParent(parent, false);
            if (Resources.Load<Sprite>("Props/" + Kinds[0].Sprite) == null) return;

            var random = new System.Random(Seed);
            var total = 0f;
            foreach (var kind in Kinds) total += kind.Weight;
            var size = VillageGrid.Size;
            // A jittered lattice: one candidate every 3 cells, kept more often near and beyond the map edge.
            for (var gx = -Margin; gx < size + Margin; gx += 3)
            for (var gz = -Margin; gz < size + Margin; gz += 3)
            {
                var x = gx + random.Next(0, 3);
                var z = gz + random.Next(0, 3);
                var edge = Mathf.Min(Mathf.Min(x, z), Mathf.Min(size - 1 - x, size - 1 - z));
                var chance = edge < 0 ? 0.85f : Mathf.Lerp(0.6f, 0.18f, Mathf.Clamp01(edge / 12f));
                if (random.NextDouble() > chance) continue;

                var pick = (float)random.NextDouble() * total;
                var kind = Kinds[0];
                foreach (var k in Kinds)
                {
                    if (pick < k.Weight)
                    {
                        kind = k;
                        break;
                    }
                    pick -= k.Weight;
                }
                var sprite = Resources.Load<Sprite>("Props/" + kind.Sprite);
                if (sprite == null) continue;

                var cell = new GridCoord(x, z);
                var go = new GameObject(kind.Sprite);
                go.transform.SetParent(root, false);
                var jitter = new Vector3((float)random.NextDouble() * 0.5f - 0.25f, 0f, (float)random.NextDouble() * 0.5f - 0.25f);
                go.transform.localPosition = GridSpace.CellCenter(cell) + jitter;
                var scale = 0.85f + (float)random.NextDouble() * 0.3f;
                BuildingSprites.GroundShadow(go.transform, kind.Shadow * scale, kind.Shadow * scale * 0.8f, 0.4f);
                var standing = BuildingSprites.Stand(go.transform, "Art", sprite, go.transform.position, kind.Width * scale,
                    BuildingSprites.Order(go.transform.position), Color.white);
                if (random.NextDouble() < 0.5)
                {
                    var flip = standing.transform.localScale;
                    standing.transform.localScale = new Vector3(-flip.x, flip.y, flip.z);
                }
                _props.Add(new Prop { Cell = cell, Go = go });
            }
            Refresh(0);
        }

        /// <summary>Hides each prop with a building or wall on or beside its cell. <paramref name="hiddenId"/> is being moved.</summary>
        public void Refresh(int hiddenId)
        {
            foreach (var prop in _props)
            {
                var free = true;
                for (var dx = -1; dx <= 1 && free; dx++)
                for (var dz = -1; dz <= 1 && free; dz++)
                {
                    var cell = new GridCoord(prop.Cell.X + dx, prop.Cell.Z + dz);
                    if (!VillageGrid.InBounds(cell.X, cell.Z)) continue;
                    var building = _village.BuildingAt(cell);
                    if (building != null && building.Id != hiddenId) free = false;
                }
                if (prop.Go.activeSelf != free) prop.Go.SetActive(free);
            }
        }
    }
}
