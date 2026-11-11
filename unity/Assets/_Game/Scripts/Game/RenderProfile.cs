using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace ShinobiDominion.Game
{
    /// <summary>
    /// How hard the renderer works on the device it finds itself on (ADR-054). The project has one render pipeline
    /// asset, tuned for a desktop: 4x anti-aliasing, a 4096 shadow map, HDR. A phone gets a lighter set of the same
    /// settings, chosen once at start from its memory and its screen. The numbers are first guesses that have not
    /// been measured on a phone; they are here so there is one place to change when they are.
    /// </summary>
    public static class RenderProfile
    {
        public readonly struct Settings
        {
            /// <summary>Share of the screen's pixels the world is drawn at; the interface is always drawn at full size.</summary>
            public readonly float RenderScale;
            public readonly int Msaa;
            public readonly int ShadowMap;
            public readonly bool Hdr;
            public readonly int ExtraLights;

            public Settings(float renderScale, int msaa, int shadowMap, bool hdr, int extraLights)
            {
                RenderScale = renderScale;
                Msaa = msaa;
                ShadowMap = shadowMap;
                Hdr = hdr;
                ExtraLights = extraLights;
            }

            public override string ToString() =>
                $"render scale {RenderScale:0.00}, {Msaa}x anti-aliasing, {ShadowMap} shadow map, HDR {(Hdr ? "on" : "off")}, {ExtraLights} extra lights";
        }

        /// <summary>The desktop settings, as ProjectSetup writes them into the pipeline asset.</summary>
        public static readonly Settings Desktop = new Settings(1f, 4, 4096, true, 4);

        /// <summary>A phone with less memory than this is treated as a weak one.</summary>
        public const int WeakMemoryMb = 3500;

        /// <summary>The world is drawn no taller than this on a phone; a sharper screen scales it up.</summary>
        public const int PhoneHeight = 1080;
        public const int WeakPhoneHeight = 810;

        /// <summary>The settings for a device: a desktop keeps everything, a phone gets a lighter set by memory and screen.</summary>
        public static Settings For(bool mobile, int memoryMb, int screenHeight)
        {
            if (!mobile) return Desktop;
            var weak = memoryMb > 0 && memoryMb < WeakMemoryMb;
            var tallest = weak ? WeakPhoneHeight : PhoneHeight;
            var scale = screenHeight <= tallest ? 1f : Mathf.Clamp((float)tallest / screenHeight, 0.6f, 1f);
            return weak ? new Settings(scale, 1, 1024, false, 1) : new Settings(scale, 2, 2048, false, 2);
        }

        /// <summary>
        /// Puts a phone's settings on the pipeline. Does nothing on a desktop, and nothing in the editor, where
        /// changing the pipeline asset would rewrite the project's own file.
        /// </summary>
        public static void Apply()
        {
            if (Application.isEditor || !Application.isMobilePlatform) return;
            if (!(GraphicsSettings.currentRenderPipeline is UniversalRenderPipelineAsset pipeline)) return;
            // In landscape the shorter side is the height.
            var settings = For(true, SystemInfo.systemMemorySize, Mathf.Min(Screen.width, Screen.height));
            pipeline.renderScale = settings.RenderScale;
            pipeline.msaaSampleCount = settings.Msaa;
            pipeline.mainLightShadowmapResolution = settings.ShadowMap;
            pipeline.supportsHDR = settings.Hdr;
            pipeline.maxAdditionalLightsCount = settings.ExtraLights;
            Debug.Log($"[Shinobi Dominion] Phone render profile: {settings} ({SystemInfo.systemMemorySize} MB, {Screen.width}x{Screen.height}).");
        }
    }
}
