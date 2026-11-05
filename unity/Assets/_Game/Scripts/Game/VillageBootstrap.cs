using System;
using System.IO;
using ShinobiDominion.Core;
using UnityEngine;

namespace ShinobiDominion.Game
{
    /// <summary>
    /// Composition root and the only component the Village scene needs. Everything else,
    /// including camera, light and UI, is created here as a child, so destroying this one
    /// object removes the whole game.
    /// </summary>
    public sealed class VillageBootstrap : MonoBehaviour
    {
        public const string SaveFileName = "village_v1.json";

        /// <summary>Lets tests run against a temp file instead of the player's save.</summary>
        public static string SavePathOverride;

        public VillageController Controller { get; private set; }
        public IsoCamera IsoCamera { get; private set; }

        private void Awake()
        {
            var config = Resources.Load<TextAsset>("Config/buildings");
            if (config == null)
                throw new InvalidOperationException("Resources/Config/buildings.json is missing.");
            var catalog = BuildingCatalog.FromJson(config.text);

            var savePath = SavePathOverride ?? Path.Combine(Application.persistentDataPath, SaveFileName);
            var store = new FileVillageStore(savePath);
            var village = store.LoadOrCreate(catalog, DateTime.UtcNow.Ticks);

            var heroConfig = Resources.Load<TextAsset>("Config/heroes");
            if (heroConfig == null)
                throw new InvalidOperationException("Resources/Config/heroes.json is missing.");
            var heroCatalog = HeroCatalog.FromJson(heroConfig.text);
            var rosterStore = new FileRosterStore(savePath + ".heroes");
            var roster = rosterStore.LoadOrCreate(heroCatalog);

            // A base can only point at a hero the player still owns.
            foreach (var building in new System.Collections.Generic.List<BuildingInstance>(village.Buildings))
                if (building.AssignedHeroInstanceId != null && !roster.Owns(building.AssignedHeroInstanceId))
                    village.TryUnassignHero(building.Id);

            var world = new GameObject("World").transform;
            world.SetParent(transform, false);
            VillageScenery.Create(world);
            CreateLight(world);

            var cameraObject = new GameObject("IsoCamera", typeof(Camera), typeof(AudioListener));
            cameraObject.transform.SetParent(transform, false);
            cameraObject.tag = "MainCamera";
            IsoCamera = cameraObject.AddComponent<IsoCamera>();
            IsoCamera.Initialize(new Vector3(25f, 0f, 25f), 12f);

            var ui = new UiRoot(transform);
            Controller = gameObject.AddComponent<VillageController>();
            Controller.Initialize(village, store, roster, rosterStore, IsoCamera, world, ui);

            gameObject.AddComponent<WorldInput>().Initialize(IsoCamera, Controller);
        }

        private static void CreateLight(Transform parent)
        {
            var go = new GameObject("Sun", typeof(Light));
            go.transform.SetParent(parent, false);
            go.transform.rotation = Quaternion.Euler(50f, 200f, 0f);
            var light = go.GetComponent<Light>();
            light.type = LightType.Directional;
            light.color = new Color(1f, 0.96f, 0.88f);
            light.intensity = 1.2f;
            light.shadows = LightShadows.Soft;
            light.shadowStrength = 0.55f;
            light.shadowBias = 0.12f;
            light.shadowNormalBias = 0.9f;
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.52f, 0.55f, 0.58f);
        }
    }
}
