using System.IO;
using ShinobiDominion.Game;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace ShinobiDominion.EditorTools
{
    /// <summary>
    /// Generates the assets that are awkward to hand-write: URP pipeline assets, placeholder
    /// materials and the Village scene. Safe to re-run, and runs by itself on first import.
    /// Batch use: Unity -batchmode -quit -executeMethod ShinobiDominion.EditorTools.ProjectSetup.Run
    /// </summary>
    [InitializeOnLoad]
    public static class ProjectSetup
    {
        private const string SettingsFolder = "Assets/_Game/Config";
        private const string MaterialsFolder = "Assets/_Game/Resources/Materials";
        private const string ScenesFolder = "Assets/_Game/Scenes";
        private const string ScenePath = ScenesFolder + "/Village.unity";
        private const string PipelinePath = SettingsFolder + "/URP_Pipeline.asset";
        private const string RendererPath = SettingsFolder + "/URP_Renderer.asset";

        static ProjectSetup()
        {
            EditorApplication.delayCall += () =>
            {
                if (EditorApplication.isPlayingOrWillChangePlaymode) return;
                if (!File.Exists(ScenePath) || GraphicsSettings.defaultRenderPipeline == null) Run();
            };
        }

        [MenuItem("Shinobi Dominion/Run Project Setup")]
        public static void Run()
        {
            Directory.CreateDirectory(SettingsFolder);
            Directory.CreateDirectory(MaterialsFolder);
            Directory.CreateDirectory(ScenesFolder);
            AssetDatabase.Refresh();

            ConfigurePipeline();
            CreateMaterials();
            ConfigurePlayer();
            CreateScene();

            AssetDatabase.SaveAssets();
            Debug.Log("[Shinobi Dominion] Project setup complete.");
        }

        /// <summary>Batch use: -executeMethod ShinobiDominion.EditorTools.ProjectSetup.BuildWindows</summary>
        [MenuItem("Shinobi Dominion/Build Windows Player")]
        public static void BuildWindows()
        {
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = new[] { ScenePath },
                locationPathName = "Builds/Windows/ShinobiDominion.exe",
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.None
            });
            Debug.Log($"[Shinobi Dominion] Build result: {report.summary.result}, {report.summary.totalSize / (1024 * 1024)} MB, {report.summary.totalErrors} errors.");
            if (report.summary.result != UnityEditor.Build.Reporting.BuildResult.Succeeded && Application.isBatchMode)
                EditorApplication.Exit(1);
        }

        private static void ConfigurePipeline()
        {
            var pipeline = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(PipelinePath);
            if (pipeline == null)
            {
                var renderer = ScriptableObject.CreateInstance<UniversalRendererData>();
                AssetDatabase.CreateAsset(renderer, RendererPath);
                pipeline = UniversalRenderPipelineAsset.Create(renderer);
                AssetDatabase.CreateAsset(pipeline, PipelinePath);
            }

            GraphicsSettings.defaultRenderPipeline = pipeline;
            for (var level = 0; level < QualitySettings.names.Length; level++)
            {
                QualitySettings.SetQualityLevel(level, false);
                QualitySettings.renderPipeline = pipeline;
            }
        }

        private static void CreateMaterials()
        {
            var litPath = MaterialsFolder + "/PlaceholderLit.mat";
            if (AssetDatabase.LoadAssetAtPath<Material>(litPath) == null)
            {
                var lit = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                lit.SetFloat("_Smoothness", 0.15f);
                AssetDatabase.CreateAsset(lit, litPath);
            }

            var overlayPath = MaterialsFolder + "/PlaceholderOverlay.mat";
            if (AssetDatabase.LoadAssetAtPath<Material>(overlayPath) == null)
            {
                var overlay = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
                Mats.MakeTransparent(overlay);
                AssetDatabase.CreateAsset(overlay, overlayPath);
            }

            var onTopPath = MaterialsFolder + "/PlaceholderOnTop.mat";
            if (AssetDatabase.LoadAssetAtPath<Material>(onTopPath) == null)
                AssetDatabase.CreateAsset(new Material(Shader.Find("ShinobiDominion/OverlayOnTop")), onTopPath);
        }

        private static void ConfigurePlayer()
        {
            PlayerSettings.companyName = "ShinobiDominionDev";
            PlayerSettings.productName = "Shinobi Dominion";
            PlayerSettings.colorSpace = ColorSpace.Linear;
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.AutoRotation;
            PlayerSettings.allowedAutorotateToPortrait = false;
            PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
            PlayerSettings.allowedAutorotateToLandscapeLeft = true;
            PlayerSettings.allowedAutorotateToLandscapeRight = true;
        }

        private static void CreateScene()
        {
            if (!File.Exists(ScenePath))
            {
                var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                var root = new GameObject("VillageBootstrap");
                root.AddComponent<VillageBootstrap>();
                EditorSceneManager.SaveScene(scene, ScenePath);
            }
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
        }
    }
}
