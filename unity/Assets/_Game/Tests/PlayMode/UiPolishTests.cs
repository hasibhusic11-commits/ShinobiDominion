using System.Collections;
using NUnit.Framework;
using ShinobiDominion.Game;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace ShinobiDominion.Tests
{
    /// <summary>The interface motions settle where they should, and the shine really draws light.</summary>
    public class UiPolishTests
    {
        private GameObject _root;
        private Camera _camera;
        private RenderTexture _texture;

        [SetUp]
        public void SetUp()
        {
            _root = new GameObject("UiPolishTestRoot");
            _camera = new GameObject("UiPolishCamera").AddComponent<Camera>();
            _camera.transform.SetParent(_root.transform);
            _camera.clearFlags = CameraClearFlags.SolidColor;
            _camera.backgroundColor = Color.black;
            _texture = new RenderTexture(256, 256, 24);
            _camera.targetTexture = _texture;
        }

        [TearDown]
        public void TearDown()
        {
            Object.Destroy(_root);
            _texture.Release();
        }

        private Image MakeImage()
        {
            var canvas = new GameObject("Canvas", typeof(RectTransform)).AddComponent<Canvas>();
            canvas.transform.SetParent(_root.transform, false);
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = _camera;
            canvas.planeDistance = 5f;
            var image = new GameObject("Picture", typeof(RectTransform)).AddComponent<Image>();
            image.transform.SetParent(canvas.transform, false);
            image.rectTransform.anchorMin = Vector2.zero;
            image.rectTransform.anchorMax = Vector2.one;
            image.rectTransform.sizeDelta = Vector2.zero;
            image.color = new Color(0.3f, 0.3f, 0.35f);
            return image;
        }

        private float Brightness()
        {
            _camera.Render();
            var previous = RenderTexture.active;
            RenderTexture.active = _texture;
            var read = new Texture2D(_texture.width, _texture.height, TextureFormat.RGB24, false);
            read.ReadPixels(new Rect(0, 0, _texture.width, _texture.height), 0, 0);
            RenderTexture.active = previous;
            var sum = 0f;
            foreach (var pixel in read.GetPixels()) sum += pixel.grayscale;
            Object.Destroy(read);
            return sum / (_texture.width * _texture.height);
        }

        [UnityTest]
        public IEnumerator PopIn_SettlesAtFullSize()
        {
            var image = MakeImage();
            UiMotion.PopIn(image.transform, 0f, 0.2f, 0.2f);
            Assert.Less(image.transform.localScale.x, 0.5f);
            yield return new WaitForSecondsRealtime(0.4f);
            Assert.AreEqual(1f, image.transform.localScale.x, 0.001f);
        }

        [UnityTest]
        public IEnumerator Shine_SweepsLightAcrossThePicture()
        {
            var image = MakeImage();
            yield return null;
            var plain = Brightness();
            UiMotion.Shine(image, Color.white, 0.1f, 0.4f);
            var brightest = plain;
            for (var i = 0; i < 40; i++)
            {
                yield return new WaitForSecondsRealtime(0.05f);
                brightest = Mathf.Max(brightest, Brightness());
            }
            Assert.Greater(brightest, plain + 0.02f, $"The shine should brighten the picture as it passes (plain {plain:F3}, brightest {brightest:F3})");
        }

        [Test]
        public void MobileSafeArea_NormalizesCutoutsAndClampsToScreen()
        {
            var safe = UiRoot.NormalizedSafeArea(new Rect(80f, 0f, 1120f, 690f), 1280, 720);
            Assert.AreEqual(0.0625f, safe.xMin, 0.0001f);
            Assert.AreEqual(0.9375f, safe.xMax, 0.0001f);
            Assert.AreEqual(0f, safe.yMin, 0.0001f);
            Assert.AreEqual(690f / 720f, safe.yMax, 0.0001f);

            var invalid = UiRoot.NormalizedSafeArea(new Rect(-50f, -50f, 2000f, 1800f), 1280, 720);
            Assert.GreaterOrEqual(invalid.xMin, 0f);
            Assert.GreaterOrEqual(invalid.yMin, 0f);
            Assert.LessOrEqual(invalid.xMax, 1f);
            Assert.LessOrEqual(invalid.yMax, 1f);
        }

        [Test]
        public void EveryButton_SinksWhenPressed()
        {
            var button = Ui.Button(_root.transform, "Test", "Test", null);
            Assert.IsNotNull(button.GetComponent<UiPress>());
        }
    }
}
