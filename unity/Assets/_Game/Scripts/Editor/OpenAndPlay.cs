using System;
using UnityEditor;
using UnityEditor.SceneManagement;

namespace ShinobiDominion.EditorTools
{
    /// <summary>
    /// Started with -sdPlay on the command line, the editor opens the village scene and presses Play, so the game
    /// can be shown with one command. Does nothing otherwise.
    /// </summary>
    [InitializeOnLoad]
    public static class OpenAndPlay
    {
        private const string Scene = "Assets/_Game/Scenes/Village.unity";
        private const string DoneKey = "ShinobiDominion.OpenAndPlay.Done";

        static OpenAndPlay()
        {
            if (Array.IndexOf(Environment.GetCommandLineArgs(), "-sdPlay") < 0) return;
            if (SessionState.GetBool(DoneKey, false)) return;
            EditorApplication.delayCall += () =>
            {
                SessionState.SetBool(DoneKey, true);
                EditorSceneManager.OpenScene(Scene);
                EditorApplication.isPlaying = true;
            };
        }
    }
}
