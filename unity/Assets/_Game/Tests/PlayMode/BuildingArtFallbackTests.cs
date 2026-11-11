using System.Collections.Generic;
using NUnit.Framework;
using ShinobiDominion.Game;
using UnityEngine;

namespace ShinobiDominion.Tests
{
    public class BuildingArtFallbackTests
    {
        [Test]
        public void Building_UsesSameBuildingEarlierTierBeforeAnotherTier()
        {
            var texture = new Texture2D(2, 2);
            var sprite = Sprite.Create(texture, new Rect(0, 0, 2, 2), Vector2.one * 0.5f);
            try
            {
                var requested = new List<string>();
                Sprite Load(string path)
                {
                    requested.Add(path);
                    return path == "dojo_hq_03" ? sprite : null;
                }
                Assert.AreSame(sprite, BuildingSprites.NearestAvailableArt("dojo_hq", 5, Load));
                CollectionAssert.AreEqual(
                    new[] { "dojo_hq_05", "dojo_hq_04", "dojo_hq_03" },
                    requested);
            }
            finally
            {
                Object.DestroyImmediate(sprite);
                Object.DestroyImmediate(texture);
            }
        }

        [Test]
        public void Building_UsesLaterTierOnlyIfNoEarlierTierExists()
        {
            var texture = new Texture2D(2, 2);
            var sprite = Sprite.Create(texture, new Rect(0, 0, 2, 2), Vector2.one * 0.5f);
            try
            {
                Sprite Load(string path) => path == "builder_hall_04" ? sprite : null;
                Assert.AreSame(sprite, BuildingSprites.NearestAvailableArt("builder_hall", 1, Load));
            }
            finally
            {
                Object.DestroyImmediate(sprite);
                Object.DestroyImmediate(texture);
            }
        }

        [Test]
        public void MissingAllBuildingArt_DoesNotSubstituteOtherBuildingIdentity()
        {
            var requested = new List<string>();
            Sprite Load(string path)
            {
                requested.Add(path);
                return null;
            }
            Assert.IsNull(BuildingSprites.NearestAvailableArt("hero_base", 10, Load));
            Assert.IsTrue(requested.TrueForAll(path => path.StartsWith("hero_base_")));
        }
    }
}
