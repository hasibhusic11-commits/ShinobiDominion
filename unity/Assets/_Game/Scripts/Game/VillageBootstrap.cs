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

        /// <summary>Lets tests supply a server. Unset, tests run offline and the game uses its local config.</summary>
        public static Func<IGameServer> ServerOverride;

        /// <summary>
        /// Editor and development builds may play from a local file. Release builds never do, so a
        /// tampered save cannot become a real village. Tests can turn this off to check that path.
        /// </summary>
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        public static bool AllowOfflinePlay = true;
#else
        public static bool AllowOfflinePlay = false;
#endif

        public VillageController Controller { get; private set; }
        public IsoCamera IsoCamera { get; private set; }

        private void Awake()
        {
            // Phones default to 30 frames a second, which makes panning and every animation look choppy.
            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = 60;

            var config = Resources.Load<TextAsset>("Config/buildings");
            if (config == null)
                throw new InvalidOperationException("Resources/Config/buildings.json is missing.");
            var catalog = BuildingCatalog.FromJson(config.text);

            var savePath = SavePathOverride ?? Path.Combine(Application.persistentDataPath, SaveFileName);
            var store = new FileVillageStore(savePath);
            var village = AllowOfflinePlay
                ? store.LoadOrCreate(catalog, DateTime.UtcNow.Ticks)
                : StarterVillage.Create(catalog, DateTime.UtcNow.Ticks);

            var heroConfig = Resources.Load<TextAsset>("Config/heroes");
            if (heroConfig == null)
                throw new InvalidOperationException("Resources/Config/heroes.json is missing.");
            var heroCatalog = HeroCatalog.FromJson(heroConfig.text);
            var rosterStore = new FileRosterStore(savePath + ".heroes");
            var roster = AllowOfflinePlay ? rosterStore.LoadOrCreate(heroCatalog) : new HeroRoster(heroCatalog);

            // A base can only point at a hero the player still owns.
            foreach (var building in new System.Collections.Generic.List<BuildingInstance>(village.Buildings))
                if (building.AssignedHeroInstanceId != null && !roster.Owns(building.AssignedHeroInstanceId))
                    village.TryUnassignHero(building.Id);

            var world = new GameObject("World").transform;
            world.SetParent(transform, false);
            GameQuality.Load();
            VillageScenery.Create(world);
            GameQuality.Sun = CreateLight(world);
            GameQuality.Load();

            var cameraObject = new GameObject("IsoCamera", typeof(Camera), typeof(AudioListener));
            cameraObject.transform.SetParent(transform, false);
            cameraObject.tag = "MainCamera";
            IsoCamera = cameraObject.AddComponent<IsoCamera>();
            IsoCamera.Initialize(new Vector3(25f, 0f, 25f), 7f);

            var ui = new UiRoot(transform);
            Controller = gameObject.AddComponent<VillageController>();
            Controller.AllowOfflinePlay = AllowOfflinePlay;
            Controller.Initialize(village, AllowOfflinePlay ? store : null, roster, AllowOfflinePlay ? rosterStore : null, IsoCamera, world, ui);

            if (ServerOverride != null)
            {
                Controller.ConnectToServer(ServerOverride());
            }
            else if (SavePathOverride == null)
            {
                var serverConfig = ServerConfig.Load();
                if (serverConfig != null) Controller.ConnectToServer(new NakamaGameServer(serverConfig));
            }

            gameObject.AddComponent<WorldInput>().Initialize(IsoCamera, Controller);
        }

        private static Light CreateLight(Transform parent)
        {
            var go = new GameObject("Sun", typeof(Light));
            go.transform.SetParent(parent, false);
            // Warm light from the camera's left, which keeps entrances lit and casts shadows up-right.
            go.transform.rotation = Quaternion.Euler(50f, 18f, 0f);
            var light = go.GetComponent<Light>();
            light.type = LightType.Directional;
            light.color = new Color(1f, 0.95f, 0.82f);
            light.intensity = 1.25f;
            light.shadows = LightShadows.Soft;
            light.shadowStrength = 0.8f;
            light.shadowBias = 0.25f;
            light.shadowNormalBias = 0.6f;
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.52f, 0.58f, 0.66f);
            return light;
        }
    }
}
