using UnityEngine;

namespace ShinobiDominion.Game
{
    public enum VisualQuality
    {
        Low = 0,
        High = 1
    }

    /// <summary>
    /// One setting that scales visual cost for weaker phones. Low stops wind and water movement,
    /// drops particles and turns off shadows, and needs no change to the game's logic.
    /// </summary>
    public static class GameQuality
    {
        private const string Key = "sd_visual_quality";
        private static readonly int AnimStrength = Shader.PropertyToID("_SD_AnimStrength");

        public static VisualQuality Current { get; private set; } = VisualQuality.High;
        public static event System.Action Changed;

        /// <summary>URP reads shadows from the light, so this is what quality has to switch.</summary>
        public static Light Sun { get; set; }

        public static void Load()
        {
            Apply((VisualQuality)PlayerPrefs.GetInt(Key, (int)VisualQuality.High));
        }

        public static void Set(VisualQuality quality)
        {
            PlayerPrefs.SetInt(Key, (int)quality);
            PlayerPrefs.Save();
            Apply(quality);
        }

        private static void Apply(VisualQuality quality)
        {
            Current = quality;
            Shader.SetGlobalFloat(AnimStrength, quality == VisualQuality.High ? 1f : 0f);
            if (Sun != null) Sun.shadows = quality == VisualQuality.High ? LightShadows.Soft : LightShadows.None;
            foreach (var camera in Camera.allCameras) camera.allowMSAA = quality == VisualQuality.High;
            foreach (var system in Object.FindObjectsByType<ParticleSystem>(FindObjectsSortMode.None))
            {
                if (quality == VisualQuality.High) system.Play();
                else system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            }
            Changed?.Invoke();
        }
    }

    /// <summary>A few small, cheap particle systems. They are decoration and do nothing to game logic.</summary>
    public static class WorldEffects
    {
        private static Material _soft;

        private static Material SoftMaterial()
        {
            if (_soft != null) return _soft;
            const int size = 32;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            var center = (size - 1) * 0.5f;
            for (var y = 0; y < size; y++)
            for (var x = 0; x < size; x++)
            {
                var distance = Mathf.Sqrt((x - center) * (x - center) + (y - center) * (y - center)) / center;
                texture.SetPixel(x, y, new Color(1f, 1f, 1f, Mathf.Clamp01(1f - distance) * Mathf.Clamp01(1f - distance)));
            }
            texture.Apply();
            _soft = new Material(Mats.Particle) { mainTexture = texture };
            return _soft;
        }

        public static ParticleSystem Create(Transform parent, string name, Vector3 localPosition, Color start, Color end,
            float rate, float lifetime, float speed, float size, float radius, float gravity = 0f)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPosition;
            go.transform.localRotation = Quaternion.Euler(-90f, 0f, 0f);
            var system = go.AddComponent<ParticleSystem>();
            system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            var main = system.main;
            main.loop = true;
            main.startLifetime = lifetime;
            main.startSpeed = speed;
            main.startSize = new ParticleSystem.MinMaxCurve(size * 0.6f, size);
            main.startColor = start;
            main.gravityModifier = gravity;
            main.maxParticles = Mathf.CeilToInt(rate * lifetime) + 2;
            main.simulationSpace = ParticleSystemSimulationSpace.World;

            var emission = system.emission;
            emission.rateOverTime = rate;

            var shape = system.shape;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = radius;

            var colorOverLife = system.colorOverLifetime;
            colorOverLife.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(
                new[] { new GradientColorKey(start, 0f), new GradientColorKey(end, 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(start.a, 0.2f), new GradientAlphaKey(0f, 1f) });
            colorOverLife.color = gradient;

            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = SoftMaterial();
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;

            if (GameQuality.Current == VisualQuality.High) system.Play();
            return system;
        }
    }
}
