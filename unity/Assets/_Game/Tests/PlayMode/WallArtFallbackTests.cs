using System.Collections.Generic;
using NUnit.Framework;
using ShinobiDominion.Game;
using UnityEngine;

namespace ShinobiDominion.Tests
{
    public class WallArtFallbackTests
    {
        [Test]
        public void MissingWallTier_UsesNearestEarlierSpriteOfSameKind()
        {
            var texture = new Texture2D(2, 2);
            var sprite = Sprite.Create(texture, new Rect(0, 0, 2, 2), Vector2.one * 0.5f);
            try
            {
                var available = new Dictionary<string, Sprite>
                {
                    { "wall_seg_x_03", sprite }
                };
                var sought = new List<string>();
                Sprite Load(string name)
                {
                    sought.Add(name);
                    return available.TryGetValue(name, out var result) ? result : null;
                }

                var result = WallView.FindAvailableSprite("wall_seg_x_05", 5, Load, out var tier);
                Assert.AreSame(sprite, result);
                Assert.AreEqual(3, tier);
                CollectionAssert.AreEqual(new[] { "wall_seg_x_05", "wall_seg_x_04", "wall_seg_x_03" }, sought);
            }
            finally
            {
                Object.DestroyImmediate(sprite);
                Object.DestroyImmediate(texture);
            }
        }

        [Test]
        public void MissingAllWallArt_DoesNotInventGeometry()
        {
            var result = WallView.FindAvailableSprite("wall_post_v2_10", 10, _ => null, out var tier);
            Assert.IsNull(result);
            Assert.AreEqual(10, tier);
        }
    }
}
