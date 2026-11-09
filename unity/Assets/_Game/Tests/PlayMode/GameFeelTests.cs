using System.Collections;
using System.IO;
using System.Linq;
using NUnit.Framework;
using ShinobiDominion.Game;
using UnityEngine;
using UnityEngine.TestTools;

namespace ShinobiDominion.Tests
{
    /// <summary>Game feel always returns to rest: pops settle, freezes restore the speed, flying coins clean up.</summary>
    public class GameFeelTests
    {
        private string _savePath;
        private GameObject _root;
        private VillageController Controller;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            _savePath = Path.Combine(Application.temporaryCachePath, "feel_" + System.Guid.NewGuid().ToString("N") + ".json");
            VillageBootstrap.SavePathOverride = _savePath;
            _root = new GameObject("VillageBootstrapUnderTest");
            Controller = _root.AddComponent<VillageBootstrap>().Controller;
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            Time.timeScale = 1f;
            Object.Destroy(_root);
            VillageBootstrap.SavePathOverride = null;
            yield return null;
            foreach (var file in new[] { _savePath, _savePath + ".heroes" })
                if (File.Exists(file)) File.Delete(file);
        }

        [UnityTest]
        public IEnumerator Music_Plays_AndItsSwitchIsRemembered()
        {
            var wasMuted = GameMusic.Muted;
            GameMusic.Muted = false;
            yield return null;
            Assert.IsTrue(GameMusic.Playing, "Music starts with the game");
            GameMusic.Muted = true;
            yield return null;
            Assert.IsFalse(GameMusic.Playing);
            GameMusic.Muted = wasMuted;
        }

        [Test]
        public void Music_IsAudible_NeverClips_AndIsSavedForListening()
        {
            var go = new GameObject("MusicProbe");
            var music = go.AddComponent<GameMusic>();
            var fill = typeof(GameMusic).GetMethod("Fill", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            foreach (var battle in new[] { false, true })
            {
                typeof(GameMusic).GetField("_targetIntensity", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).SetValue(music, battle ? 1f : 0f);
                typeof(GameMusic).GetField("_intensity", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).SetValue(music, battle ? 1f : 0f);
                var data = new float[22050 * 8];
                fill.Invoke(music, new object[] { data });
                var sum = 0.0;
                var peak = 0f;
                foreach (var s in data)
                {
                    sum += s * s;
                    peak = Mathf.Max(peak, Mathf.Abs(s));
                }
                var rms = System.Math.Sqrt(sum / data.Length);
                Assert.Greater(rms, 0.02, "The music should be heard");
                Assert.Less(rms, 0.4, "The music should stay in the background");
                Assert.Less(peak, 1f, "The music must never clip");
                var dir = System.Environment.GetEnvironmentVariable("SD_CAPTURE_DIR");
                if (!string.IsNullOrEmpty(dir)) WriteWav(Path.Combine(dir, battle ? "music_battle.wav" : "music_village.wav"), data);
            }
            Object.DestroyImmediate(go);
        }

        private static void WriteWav(string path, float[] samples)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            using (var writer = new BinaryWriter(File.Create(path)))
            {
                writer.Write(System.Text.Encoding.ASCII.GetBytes("RIFF"));
                writer.Write(36 + samples.Length * 2);
                writer.Write(System.Text.Encoding.ASCII.GetBytes("WAVEfmt "));
                writer.Write(16);
                writer.Write((short)1);
                writer.Write((short)1);
                writer.Write(22050);
                writer.Write(22050 * 2);
                writer.Write((short)2);
                writer.Write((short)16);
                writer.Write(System.Text.Encoding.ASCII.GetBytes("data"));
                writer.Write(samples.Length * 2);
                foreach (var s in samples) writer.Write((short)(Mathf.Clamp(s, -1f, 1f) * 32767f));
            }
        }

        [UnityTest]
        public IEnumerator Pop_SquashesTheBuilding_ThenSettlesAtItsNormalSize()
        {
            var stand = _root.GetComponentsInChildren<IsoStand>().First();
            var restScale = stand.transform.localScale;
            Feel.Pop(stand, Feel.Tier.Medium);
            yield return null;
            Assert.AreNotEqual(Vector2.one, stand.Juice, "The pop squashes the picture");
            yield return new WaitForSecondsRealtime(0.8f);
            Assert.AreEqual(Vector2.one, stand.Juice, "and it comes back to rest");
            Assert.That(Vector3.Distance(restScale, stand.transform.localScale), Is.LessThan(0.001f));
        }

        [UnityTest]
        public IEnumerator HitStop_FreezesBriefly_AndAlwaysRestoresTheSpeed()
        {
            Time.timeScale = 1f;
            Feel.HitStop(Feel.Tier.Large);
            Feel.HitStop(Feel.Tier.Medium);
            yield return null;
            Assert.Less(Time.timeScale, 0.2f, "Time nearly stops on impact");
            yield return new WaitForSecondsRealtime(0.4f);
            Assert.AreEqual(1f, Time.timeScale, "Overlapping freezes still end at full speed");
        }

        [UnityTest]
        public IEnumerator CollectCoins_FlyIntoTheCounter_AndCleanUp()
        {
            Controller.Hud.FlyCoins(new Vector2(Screen.width * 0.5f, Screen.height * 0.4f), false);
            yield return null;
            Assert.Greater(Controller.Hud.Root.GetComponentsInChildren<RectTransform>().Count(t => t.name == "FlyingCoin"), 0);
            yield return new WaitForSecondsRealtime(1.3f);
            Assert.AreEqual(0, Controller.Hud.Root.GetComponentsInChildren<RectTransform>().Count(t => t.name == "FlyingCoin"), "Every coin is gone once it lands");
        }

        [UnityTest]
        public IEnumerator ScreenShake_DecaysBackToTheSameView()
        {
            var camera = Object.FindAnyObjectByType<IsoCamera>();
            var rest = camera.transform.position;
            camera.AddTrauma(0.8f);
            yield return null;
            yield return null;
            Assert.Greater(Vector3.Distance(rest, camera.transform.position), 0.0001f, "The view shakes");
            yield return new WaitForSecondsRealtime(1f);
            Assert.Less(Vector3.Distance(rest, camera.transform.position), 0.001f, "and settles exactly where it was");
        }
        [UnityTest]
        public IEnumerator CaptureLoadingScreen()
        {
            var dir = System.Environment.GetEnvironmentVariable("SD_CAPTURE_DIR");
            if (string.IsNullOrEmpty(dir)) Assert.Ignore("SD_CAPTURE_DIR not set.");
            Directory.CreateDirectory(dir);
            LogAssert.ignoreFailingMessages = true;
            var game = _root.GetComponent<VillageBootstrap>();
            var camera = game.IsoCamera.Camera;
            var canvas = _root.GetComponentInChildren<Canvas>();
            var texture = new RenderTexture(1600, 900, 24) { antiAliasing = 4 };
            camera.targetTexture = texture;
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = camera;
            canvas.planeDistance = 5f;
            typeof(VillageController).GetMethod("ShowLoading", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).Invoke(Controller, null);
            yield return ScreenCaptureTests.Capture(camera, texture, Path.Combine(dir, "00_loading.png"));
            camera.targetTexture = null;
        }
    }
}
