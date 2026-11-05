using System;
using System.Collections;
using System.IO;
using System.Linq;
using NUnit.Framework;
using ShinobiDominion.Core;
using ShinobiDominion.Game;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace ShinobiDominion.Tests
{
    /// <summary>
    /// Renders real screens to PNG for human review. Skipped unless SD_CAPTURE_DIR is set.
    /// The overlay canvas is switched to camera space so it lands in the render texture.
    /// </summary>
    public class ScreenCaptureTests
    {
        [UnityTest]
        public IEnumerator CaptureKeyScreens()
        {
            var dir = Environment.GetEnvironmentVariable("SD_CAPTURE_DIR");
            if (string.IsNullOrEmpty(dir)) Assert.Ignore("SD_CAPTURE_DIR not set.");
            Directory.CreateDirectory(dir);

            var savePath = Path.Combine(Application.temporaryCachePath, "capture_" + Guid.NewGuid().ToString("N") + ".json");
            VillageBootstrap.SavePathOverride = savePath;
            var root = new GameObject("VillageBootstrapUnderTest");
            var game = root.AddComponent<VillageBootstrap>();
            yield return null;

            var controller = game.Controller;
            var camera = game.IsoCamera.Camera;
            var canvas = root.GetComponentInChildren<Canvas>();
            var texture = new RenderTexture(1600, 900, 24);
            camera.targetTexture = texture;
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = camera;
            canvas.planeDistance = 5f;
            yield return null;

            Func<string, IEnumerator> shot = name => Capture(camera, texture, Path.Combine(dir, name + ".png"));
            BuildingInstance First(string id) => controller.Village.Buildings.First(b => b.Definition.id == id);

            yield return shot("01_home");

            controller.Select(First("builder_hall").Id);
            yield return shot("02_builder_hall_panel");

            controller.OpenCatalog();
            yield return shot("03_build_catalog");

            controller.BeginPlacement("defense_tower");
            controller.SetGhostOrigin(new GridCoord(32, 26));
            yield return shot("04_placing_valid");

            controller.SetGhostOrigin(First("hero_altar").Origin);
            yield return shot("05_placing_blocked");
            controller.ExitPlacement();

            controller.BeginPlacement(Village.WallId);
            controller.SetWallRun(new GridCoord(14, 14), new GridCoord(38, 14));
            controller.ConfirmWallRun();
            controller.SetWallRun(new GridCoord(38, 15), new GridCoord(38, 34));
            controller.ConfirmWallRun();
            controller.SetWallRun(new GridCoord(14, 15), new GridCoord(14, 20));
            controller.ConfirmWallRun();
            controller.SetWallRun(new GridCoord(26, 18), new GridCoord(26, 30));
            yield return shot("06_wall_preview_blocked");
            controller.ExitPlacement();
            controller.CloseAll();

            var village = controller.Village;
            village.TryPlace("hero_base", new GridCoord(17, 33), Rotation.R0, 0, out _);
            village.TryPlace("hero_base", new GridCoord(23, 36), Rotation.R0, 0, out _);
            village.TryPlace("hero_base", new GridCoord(29, 34), Rotation.R0, 0, out _);
            var last = village.TryPlace("hero_base", new GridCoord(33, 31), Rotation.R0, 0, out _);
            Assert.AreEqual(VillageError.None, last);
            village.TryPlace("defense_tower", new GridCoord(35, 16), Rotation.R0, 0, out _);

            // One Legendary in each of the four forms, on a base, to show the visual progression.
            var roster = controller.Roster;
            var bases = village.Buildings.Where(b => b.Definition.function == BuildingFunction.HeroBase).ToList();
            for (var e = 0; e <= 3; e++)
                controller.AssignHero(bases[e].Id, roster.AddHero("thunder_empress", e, 50).InstanceId);
            First("gold_mine").LastCollectedUtcTicks = DateTime.UtcNow.Ticks - TimeSpan.TicksPerHour;
            controller.RefreshCollectMarkers();
            yield return shot("07_home_six_bases_and_walls");

            game.IsoCamera.ZoomAt(0.4f, new Vector2(texture.width * 0.5f, texture.height * 0.62f));
            yield return shot("09_four_forms_close_up");
            game.IsoCamera.ZoomAt(2.5f, new Vector2(texture.width * 0.5f, texture.height * 0.62f));

            controller.Select(First("hero_altar").Id);
            yield return shot("10_hero_altar");

            var evolving = roster.Heroes.First(h => h.ArchetypeId == "solar_beast_sage" && h.Level == HeroMath.MaxLevel);
            controller.ShowHeroDetail(evolving.InstanceId);
            yield return shot("12_hero_detail");
            controller.ShowEvolution(evolving.InstanceId);
            yield return shot("11_evolution_requirements");
            controller.CloseAll();

            game.IsoCamera.ZoomAt(5f, new Vector2(texture.width * 0.5f, texture.height * 0.5f));
            yield return shot("08_zoomed_out_full_map");

            camera.targetTexture = null;
            Object.Destroy(texture);
            Object.Destroy(root);
            VillageBootstrap.SavePathOverride = null;
            yield return null;
            if (File.Exists(savePath)) File.Delete(savePath);
        }

        internal static IEnumerator Capture(Camera camera, RenderTexture texture, string path)
        {
            yield return null;
            Canvas.ForceUpdateCanvases();
            camera.Render();

            var previous = RenderTexture.active;
            RenderTexture.active = texture;
            var image = new Texture2D(texture.width, texture.height, TextureFormat.RGB24, false);
            image.ReadPixels(new Rect(0, 0, texture.width, texture.height), 0, 0);
            image.Apply();
            RenderTexture.active = previous;
            File.WriteAllBytes(path, image.EncodeToPNG());
            Object.Destroy(image);
        }
    }
}
