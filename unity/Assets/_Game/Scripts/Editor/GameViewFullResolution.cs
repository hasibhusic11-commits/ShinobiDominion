using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace ShinobiDominion.EditorTools
{
    /// <summary>
    /// The owner's rule (2026-11-09): the game is always shown in full definition, in the editor too. Every time Play
    /// starts, each Game view turns off "Low Resolution Aspect Ratios" (which draws at half the pixels on a high-DPI
    /// screen) and zooms to fit, so it never magnifies a smaller picture. Builds already draw at native resolution.
    /// The Game view keeps these settings private, so they are reached by name; Check reports if Unity renames them.
    /// </summary>
    [InitializeOnLoad]
    public static class GameViewFullResolution
    {
        private const BindingFlags Any = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

        static GameViewFullResolution()
        {
            EditorApplication.playModeStateChanged += state =>
            {
                if (state == PlayModeStateChange.EnteredPlayMode) Apply();
            };
        }

        private static Type GameViewType => Type.GetType("UnityEditor.GameView,UnityEditor");

        [MenuItem("Shinobi Dominion/Game View: Full Resolution")]
        public static void Apply()
        {
            var type = GameViewType;
            if (type == null) return;
            var lowResolution = type.GetProperty("lowResolutionForAspectRatios", Any);
            var minScale = type.GetProperty("minScale", Any);
            var snapZoom = type.GetMethod("SnapZoom", Any, null, new[] { typeof(float) }, null);
            foreach (var view in Resources.FindObjectsOfTypeAll(type))
            {
                try
                {
                    if (lowResolution != null && lowResolution.CanWrite) lowResolution.SetValue(view, false);
                    // Fit, never magnify: 1 for Free Aspect (one screen pixel per game pixel), smaller to fit a fixed size.
                    if (minScale != null && snapZoom != null)
                        snapZoom.Invoke(view, new object[] { Mathf.Min(1f, (float)minScale.GetValue(view)) });
                    ((EditorWindow)view).Repaint();
                }
                catch (Exception exception)
                {
                    Debug.LogWarning("Could not set the Game view to full resolution: " + exception.Message);
                }
            }
        }

        /// <summary>For batch runs: exits 0 when every Game view setting this needs is still there, 1 otherwise.</summary>
        public static void Check()
        {
            var type = GameViewType;
            var missing = type == null ? "UnityEditor.GameView"
                : type.GetProperty("lowResolutionForAspectRatios", Any) == null ? "lowResolutionForAspectRatios"
                : type.GetProperty("minScale", Any) == null ? "minScale"
                : type.GetMethod("SnapZoom", Any, null, new[] { typeof(float) }, null) == null ? "SnapZoom(float)"
                : null;
            Debug.Log(missing == null ? "GAME_VIEW_CHECK ok" : "GAME_VIEW_CHECK missing " + missing);
            EditorApplication.Exit(missing == null ? 0 : 1);
        }
    }
}
