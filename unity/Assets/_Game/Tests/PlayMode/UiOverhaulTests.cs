using System;
using System.Collections;
using System.IO;
using System.Linq;
using NUnit.Framework;
using ShinobiDominion.Game;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;
using Text = ShinobiDominion.Game.GameText;

namespace ShinobiDominion.Tests
{
    /// <summary>
    /// The UI overhaul (docs/ui/overhaul): the Battle menu in UI kit v3, checked through the real screen against the
    /// real local server, and captured at the four required screen shapes plus a gallery of the kit's pieces.
    /// Skipped unless SD_SERVER_KEY is set; the captures also need SD_CAPTURE_DIR.
    /// </summary>
    public class UiOverhaulTests
    {
        private static readonly Vector2Int[] Shapes =
        {
            new Vector2Int(1280, 720), new Vector2Int(1600, 900), new Vector2Int(2340, 1080), new Vector2Int(1600, 1200)
        };

        private static readonly string[] ModeButtons = { "FindRivalButton", "OpenDungeonsButton", "OpenSiegeButton", "OpenArenaButton", "OpenTrialsButton" };

        private string _savePath;
        private GameObject _root;
        private VillageController Controller;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            var key = Environment.GetEnvironmentVariable("SD_SERVER_KEY");
            if (string.IsNullOrEmpty(key)) Assert.Ignore("SD_SERVER_KEY not set, so no server to test against.");
            var config = new ServerConfig { serverKey = key };
            var deviceId = "sd-ui-" + Guid.NewGuid().ToString("N");
            VillageBootstrap.ServerOverride = () => new NakamaGameServer(config, deviceId);
            _savePath = Path.Combine(Application.temporaryCachePath, "ui_village_" + Guid.NewGuid().ToString("N") + ".json");
            VillageBootstrap.SavePathOverride = _savePath;
            _root = new GameObject("VillageBootstrapUnderTest");
            Controller = _root.AddComponent<VillageBootstrap>().Controller;
            yield return WaitFor(() => Controller.ConnectTask.IsCompleted, "connect");
            Assert.IsTrue(Controller.IsOnline, "Could not connect to the local server");
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (_root != null) Object.Destroy(_root);
            VillageBootstrap.ServerOverride = null;
            VillageBootstrap.SavePathOverride = null;
            yield return null;
            foreach (var file in new[] { _savePath, _savePath + ".heroes" })
                if (file != null && File.Exists(file)) File.Delete(file);
        }

        private static IEnumerator WaitFor(Func<bool> condition, string what = "condition", float seconds = 15f)
        {
            var deadline = Time.realtimeSinceStartup + seconds;
            while (!condition() && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.IsTrue(condition(), "Timed out waiting for " + what);
        }

        private Button Find(string name) => _root.GetComponentsInChildren<Button>(false).FirstOrDefault(b => b.name == name);

        private string AllText => string.Join(" ", _root.GetComponentsInChildren<Text>().Select(t => t.text));

        private IEnumerator PutFirstHeroOnBase()
        {
            var heroBase = Controller.Village.Buildings.First(b => b.Definition.id == "hero_base").Id;
            var hero = Controller.Roster.Heroes.First();
            Controller.AssignHero(heroBase, hero.InstanceId);
            yield return WaitFor(() => Controller.Village.Find(heroBase).AssignedHeroInstanceId == hero.InstanceId, "hero on base");
        }

        private IEnumerator OpenMenu()
        {
            Controller.CloseAll();
            Controller.OpenAttack();
            yield return WaitFor(() => Find("OpenSiegeButton") != null, "Battle menu");
        }

        /// <summary>The whole-card tap target of a mode: the painting area of its card.</summary>
        private Button CardTap(string modeButton)
        {
            var card = _root.GetComponentsInChildren<RectTransform>(false).First(r => r.name == modeButton.Replace("Button", "Card"));
            return card.GetComponentsInChildren<Button>(false).First(b => b.name == "PictureArea");
        }

        [UnityTest]
        public IEnumerator BattleMenu_ShowsFiveIllustratedModes_AndEveryRouteStillWorks()
        {
            yield return PutFirstHeroOnBase();
            yield return OpenMenu();

            // Five modes, each with its own painting, a name, a line and its action, all open with a hero on a base.
            foreach (var name in ModeButtons)
            {
                Assert.IsNotNull(Find(name), name);
                Assert.IsTrue(Find(name).interactable, name + " should be open with a hero on a base");
            }
            var paintings = _root.GetComponentsInChildren<Image>(false).Where(i => i.sprite != null && i.sprite.name.StartsWith("mode_")).Select(i => i.sprite.name).ToList();
            CollectionAssert.AreEquivalent(new[] { "mode_raid", "mode_dungeons", "mode_siege", "mode_arena", "mode_trials" }, paintings, "Each mode shows its own painting");
            Assert.IsNull(_root.GetComponentsInChildren<Text>(false).FirstOrDefault(t => t.name == "BattleNote"), "No caption over the cards while all is well");
            StringAssert.Contains("0 / 45", AllText, "Dungeons shows the real star count (15 stages, CampaignTests)");

            // The routes: the button and the whole card both open the mode, and each way back returns here.
            Find("OpenSiegeButton").onClick.Invoke();
            yield return WaitFor(() => Find("SiegeLevel1") != null, "Siege levels");
            yield return OpenMenu();
            CardTap("OpenSiegeButton").onClick.Invoke();
            yield return WaitFor(() => Find("SiegeLevel1") != null, "Siege levels");
            yield return OpenMenu();
            Find("OpenArenaButton").onClick.Invoke();
            yield return WaitFor(() => Find("ArenaRankingsButton") != null, "the Arena");
            yield return OpenMenu();
            Find("OpenTrialsButton").onClick.Invoke();
            yield return WaitFor(() => Find("TrialFloor1") != null, "the Trial Tower");
            yield return OpenMenu();
            CardTap("OpenDungeonsButton").onClick.Invoke();
            yield return WaitFor(() => Find("OpenSiegeButton") == null && AllText.Contains("Cinder Ridge"), "Dungeon map");
        }

        [UnityTest]
        public IEnumerator BattleMenu_WithNoHero_LocksHeroModes_AndSaysWhy()
        {
            yield return OpenMenu();
            Assert.IsFalse(Find("FindRivalButton").interactable, "Raids need a hero");
            Assert.IsFalse(Find("OpenArenaButton").interactable, "The Arena needs a hero");
            Assert.IsFalse(Find("OpenTrialsButton").interactable, "The Trial Tower needs a hero");
            Assert.IsTrue(Find("OpenSiegeButton").interactable, "Village Siege needs no hero");
            Assert.IsTrue(Find("OpenDungeonsButton").interactable, "Dungeons need no hero");
            StringAssert.Contains("Put a hero on a Hero Base first", AllText);
        }

        /// <summary>The Battle menu at the four required shapes, with and without a hero, and the kit gallery.</summary>
        [UnityTest]
        public IEnumerator CaptureBattleMenuAndKit()
        {
            var dir = Environment.GetEnvironmentVariable("SD_CAPTURE_DIR");
            if (string.IsNullOrEmpty(dir)) Assert.Ignore("SD_CAPTURE_DIR not set.");
            Directory.CreateDirectory(dir);
            LogAssert.ignoreFailingMessages = true;
            var game = _root.GetComponent<VillageBootstrap>();
            var camera = game.IsoCamera.Camera;
            var canvas = _root.GetComponentInChildren<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = camera;
            canvas.planeDistance = 5f;

            IEnumerator At(Vector2Int shape, string name, Func<IEnumerator> show)
            {
                var texture = new RenderTexture(shape.x, shape.y, 24) { antiAliasing = 4 };
                camera.targetTexture = texture;
                // Let the canvas take the new shape before the window measures the screen.
                yield return null;
                yield return null;
                yield return show();
                yield return ScreenCaptureTests.Capture(camera, texture, Path.Combine(dir, $"{name}_{shape.x}x{shape.y}.png"));
                camera.targetTexture = null;
                Object.Destroy(texture);
            }

            foreach (var shape in Shapes) yield return At(shape, "battle_menu_nohero", OpenMenu);
            yield return PutFirstHeroOnBase();
            foreach (var shape in Shapes) yield return At(shape, "battle_menu_default", OpenMenu);

            // The kit's pieces in every state, on one window, at the canonical shape.
            Controller.CloseAll();
            GameObject gallery = null;
            yield return At(new Vector2Int(1600, 900), "M1_COMPONENT_GALLERY", () => Gallery(canvas, g => gallery = g));
            if (gallery != null) Object.Destroy(gallery);
        }

        private static IEnumerator Gallery(Canvas canvas, Action<GameObject> made)
        {
            var screen = Ui.Rect("KitGallery", canvas.transform);
            Ui.Stretch(screen);
            made(screen.gameObject);
            yield return null;
            var content = SdKit.Window(screen, "UI Kit", new Vector2(1180f, 600f), () => { }, () => { }, out _);
            void Label(string text, float x, float y)
            {
                var label = HudArt.Label(content, text, HudArt.Numbers, 16, TextAnchor.MiddleLeft, SdTheme.IvoryMuted, SdTheme.Ink);
                label.rectTransform.anchorMin = label.rectTransform.anchorMax = new Vector2(0f, 1f);
                label.rectTransform.pivot = new Vector2(0f, 1f);
                label.rectTransform.sizeDelta = new Vector2(320f, 24f);
                label.rectTransform.anchoredPosition = new Vector2(x, y);
            }
            void Place(Component part, float x, float y, float w, float h)
            {
                var rect = (RectTransform)part.transform;
                rect.anchorMin = rect.anchorMax = new Vector2(0f, 1f);
                rect.pivot = new Vector2(0f, 1f);
                rect.sizeDelta = new Vector2(w, h);
                rect.anchoredPosition = new Vector2(x, y);
            }

            Label("War (fight)", 0f, -4f);
            Place(SdKit.Button(content, "War", "Find a Rival", SdButtonKind.War, null, true), 0f, -30f, 200f, 62f);
            Label("Gold (affirm)", 220f, -4f);
            Place(SdKit.Button(content, "Gold", "Claim", SdButtonKind.Gold, null, true), 220f, -30f, 200f, 62f);
            Label("Navy (secondary)", 440f, -4f);
            Place(SdKit.Button(content, "Navy", "Rankings", SdButtonKind.Navy, null, true), 440f, -30f, 200f, 62f);
            Label("Locked, with reason", 660f, -4f);
            Place(SdKit.Button(content, "Locked", "Climb", SdButtonKind.War, null, false, "Needs a hero"), 660f, -30f, 200f, 62f);
            Label("Disabled", 880f, -4f);
            Place(SdKit.Button(content, "Disabled", "Defend", SdButtonKind.War, null, false), 880f, -30f, 200f, 62f);

            Label("Badge (live fact)", 0f, -112f);
            Place(SdKit.Badge(content, "12 / 45", "icon_star"), 0f, -138f, 120f, 44f);
            Label("Text plate", 220f, -112f);
            var plate = Ui.Rect("Plate", content);
            Place(plate, 220f, -138f, 300f, 66f);
            SdKit.Plate(plate);
            var words = HudArt.Label(plate, "Readable over any painting.", HudArt.Numbers, SdTheme.BodySize, TextAnchor.MiddleCenter, SdTheme.Ivory, SdTheme.Ink);
            Ui.Stretch(words.rectTransform, 10f, 6f, 10f, 6f);

            Label("Painting: open / locked", 560f, -112f);
            var open = Ui.Rect("Open", content);
            Place(open, 560f, -138f, 180f, 240f);
            SdKit.FramedPicture(open, "mode_arena", false);
            var locked = Ui.Rect("Locked", content);
            Place(locked, 760f, -138f, 180f, 240f);
            SdKit.FramedPicture(locked, "mode_trials", true);

            Label("Title ribbon", 0f, -232f);
            var ribbon = Ui.Rect("RibbonHost", content);
            Place(ribbon, 0f, -300f, 420f, 1f);
            SdKit.Title(ribbon, "Village Siege");
            yield return null;
        }
    }
}
