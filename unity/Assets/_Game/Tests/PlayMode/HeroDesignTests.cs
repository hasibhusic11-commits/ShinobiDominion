using System.Collections;
using System.IO;
using System.Linq;
using NUnit.Framework;
using ShinobiDominion.Core;
using ShinobiDominion.Game;
using UnityEngine;
using UnityEngine.TestTools;

namespace ShinobiDominion.Tests
{
    public class HeroDesignTests
    {
        private static HeroCatalog Catalog() => HeroCatalog.FromJson(Resources.Load<TextAsset>("Config/heroes").text);

        private static string ColourKey(Mesh mesh) =>
            string.Join(",", mesh.colors.Select(c => ColorUtility.ToHtmlStringRGB(c)).Distinct().OrderBy(s => s));

        [Test]
        public void Roster_HasTheTargetCountOfEachRarity()
        {
            var all = Catalog().All;
            Assert.AreEqual(12, all.Count(h => h.rarity == HeroRarity.Common));
            Assert.AreEqual(10, all.Count(h => h.rarity == HeroRarity.Elite));
            Assert.AreEqual(8, all.Count(h => h.rarity == HeroRarity.Legendary));
        }

        [Test]
        public void EveryHero_HasADesign_AndOnlyLegendariesHaveSkins()
        {
            foreach (var hero in Catalog().All)
            {
                var design = HeroDesigns.Find(hero.id);
                Assert.IsNotNull(design, hero.id);
                Assert.IsNotEmpty(design.concept, hero.id);
                Assert.AreEqual(hero.rarity == HeroRarity.Legendary ? 4 : 0, design.forms.Count, hero.id);
                if (hero.rarity == HeroRarity.Legendary)
                    Assert.AreEqual(4, hero.forms.Count, hero.id + " must list its forms for the game");
            }
        }

        [Test]
        public void EveryLegendaryForm_LooksDifferent_AndStaysAffordable()
        {
            foreach (var hero in Catalog().All.Where(h => h.rarity == HeroRarity.Legendary))
            {
                var looks = Enumerable.Range(0, 4).Select(form => BuildingMeshes.Figure(new HeroLook { DesignId = hero.id, Evolution = form })).ToList();
                foreach (var look in looks) Assert.Less(look.VertexCount, 6000, hero.id);
                var keys = looks.Select(look => ColourKey(look.Build("t"))).Distinct().Count();
                Assert.AreEqual(4, keys, hero.id);
                // Each evolution should add to the outline, as the brief asks.
                Assert.Greater(looks[3].VertexCount, looks[0].VertexCount, hero.id);
            }
        }

        [Test]
        public void EveryHero_LooksDifferentFromEveryOther()
        {
            var keys = Catalog().All.Select(h => ColourKey(BuildingMeshes.Figure(new HeroLook { DesignId = h.id }).Build("t"))).ToList();
            Assert.AreEqual(keys.Count, keys.Distinct().Count());
        }

        /// <summary>Non-negotiable rule 9: nothing borrowed from another series, even in the data.</summary>
        [Test]
        public void Designs_CarryNoBorrowedNames()
        {
            var text = (Resources.Load<TextAsset>("Config/hero_designs").text + Resources.Load<TextAsset>("Config/heroes").text).ToLowerInvariant();
            foreach (var banned in new[] { "naruto", "sasuke", "kakashi", "itachi", "sakura", "konoha", "leaf village", "sharingan", "rinnegan", "byakugan", "akatsuki", "hokage", "uchiha", "uzumaki", "susanoo", "rasengan", "chidori", "castle clash" })
                Assert.IsFalse(text.Contains(banned), banned);
        }

        [UnityTest]
        public IEnumerator CaptureDesignSheets()
        {
            var dir = System.Environment.GetEnvironmentVariable("SD_CAPTURE_DIR");
            if (string.IsNullOrEmpty(dir)) Assert.Ignore("SD_CAPTURE_DIR not set.");
            dir = Path.Combine(dir, "heroes");
            Directory.CreateDirectory(dir);
            LogAssert.ignoreFailingMessages = true;

            var savePath = Path.Combine(Application.temporaryCachePath, "designs_" + System.Guid.NewGuid().ToString("N") + ".json");
            VillageBootstrap.SavePathOverride = savePath;
            var root = new GameObject("VillageBootstrapUnderTest");
            root.AddComponent<VillageBootstrap>();
            yield return null;

            var portraits = new HeroPortraits(root.transform);
            foreach (var hero in Catalog().All)
            {
                var forms = hero.rarity == HeroRarity.Legendary ? 4 : 1;
                for (var form = 0; form < forms; form++)
                {
                    var texture = portraits.For(new HeroLook { DesignId = hero.id, Evolution = form }, 512);
                    var previous = RenderTexture.active;
                    RenderTexture.active = texture;
                    var image = new Texture2D(texture.width, texture.height, TextureFormat.RGBA32, false);
                    image.ReadPixels(new Rect(0, 0, texture.width, texture.height), 0, 0);
                    image.Apply();
                    RenderTexture.active = previous;
                    File.WriteAllBytes(Path.Combine(dir, $"{hero.id}_{form}.png"), image.EncodeToPNG());
                    Object.Destroy(image);
                    yield return null;
                }
            }
            portraits.Dispose();
            Object.Destroy(root);
            VillageBootstrap.SavePathOverride = null;
            yield return null;
            if (File.Exists(savePath)) File.Delete(savePath);
        }
    }
}
