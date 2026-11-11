using UnityEditor;
using UnityEngine;

namespace ShinobiDominion.Editor
{
    /// <summary>
    /// The Altar should not continue displaying generic proxy figures simply
    /// because the owner has not clicked an art utility menu. On a normal
    /// Unity Editor session, bake missing portraits once after imports settle.
    ///
    /// This only creates art for existing approved Heroes3D prefabs. It never
    /// overwrites concept paintings, previously baked PNGs, or gameplay data.
    /// Batch/CI mode must opt in through HeroPortraitBaker.BakeForCI.
    /// </summary>
    [InitializeOnLoad]
    internal static class HeroPortraitAutoBake
    {
        private const string SessionKey = "ShinobiDominion.HeroPortraits.AutoBakedThisSession";

        static HeroPortraitAutoBake()
        {
            if (!Application.isBatchMode)
                EditorApplication.delayCall += OnEditorReady;
        }

        private static void OnEditorReady()
        {
            if (Application.isBatchMode || EditorApplication.isPlayingOrWillChangePlaymode)
                return;
            if (SessionState.GetBool(SessionKey, false))
                return;
            if (EditorApplication.isCompiling || EditorApplication.isUpdating)
            {
                EditorApplication.delayCall += OnEditorReady;
                return;
            }

            // Mark first to avoid AssetDatabase.Refresh/reimport recursively
            // triggering another bake pass in the same Editor session.
            SessionState.SetBool(SessionKey, true);
            Debug.Log("[Shinobi Dominion] Checking existing 3D hero portraits...");
            HeroPortraitBaker.BakeMissing();
        }
    }
}
