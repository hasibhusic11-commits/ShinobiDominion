using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using NUnit.Framework;
using ShinobiDominion.Core;
using ShinobiDominion.Game;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;
using Text = ShinobiDominion.Game.GameText;

namespace ShinobiDominion.Tests
{
    /// <summary>
    /// The modes beyond raids and dungeons (ADR-039), through the real screens and against the real local
    /// server. Skipped unless SD_SERVER_KEY is set.
    /// </summary>
    public class ModeTests
    {
        private string _savePath;
        private GameObject _root;
        private VillageController Controller;
        private NakamaGameServer _direct;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            var key = Environment.GetEnvironmentVariable("SD_SERVER_KEY");
            if (string.IsNullOrEmpty(key)) Assert.Ignore("SD_SERVER_KEY not set, so no server to test against.");
            var config = new ServerConfig { serverKey = key };
            var deviceId = "sd-modes-" + Guid.NewGuid().ToString("N");
            _direct = new NakamaGameServer(config, deviceId);
            VillageBootstrap.ServerOverride = () => new NakamaGameServer(config, deviceId);
            _savePath = Path.Combine(Application.temporaryCachePath, "modes_village_" + Guid.NewGuid().ToString("N") + ".json");
            VillageBootstrap.SavePathOverride = _savePath;
            _root = new GameObject("VillageBootstrapUnderTest");
            Controller = _root.AddComponent<VillageBootstrap>().Controller;
            yield return WaitFor(() => Controller.ConnectTask.IsCompleted);
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

        private static IEnumerator WaitFor(Func<bool> condition, float seconds = 15f)
        {
            var deadline = Time.realtimeSinceStartup + seconds;
            while (!condition() && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.IsTrue(condition(), "Timed out waiting");
        }

        private Button Find(string name) => _root.GetComponentsInChildren<Button>(false).FirstOrDefault(b => b.name == name);

        private string AllText => string.Join(" ", _root.GetComponentsInChildren<Text>().Select(t => t.text));

        private IEnumerator ClickWhenReady(string name)
        {
            yield return WaitFor(() => Find(name) != null && Find(name).interactable);
            Find(name).onClick.Invoke();
        }

        private IEnumerator Await<T>(Task<T> task, Action<T> use)
        {
            yield return WaitFor(() => task.IsCompleted);
            Assert.IsFalse(task.IsFaulted, task.Exception?.InnerException?.Message);
            use(task.Result);
        }

        private IEnumerator Refused(Task task, string code)
        {
            yield return WaitFor(() => task.IsCompleted);
            Assert.IsTrue(task.IsFaulted, "The server should have refused");
            Assert.AreEqual(code, (task.Exception.InnerException as GameServerException)?.Code);
        }

        private IEnumerator PutFirstHeroOnBase()
        {
            var heroBase = Controller.Village.Buildings.First(b => b.Definition.id == "hero_base").Id;
            var hero = Controller.Roster.Heroes.First();
            Controller.AssignHero(heroBase, hero.InstanceId);
            yield return WaitFor(() => Controller.Village.Find(heroBase).AssignedHeroInstanceId == hero.InstanceId);
        }

        /// <summary>The fight playing now must be exactly the one the client's own rules give.</summary>
        private void CheckFightMatchesLocalRules()
        {
            var fight = Controller.LastModeFight;
            var local = BattleSim.Run(fight.Session.buildings, fight.ToUnits(Controller.TroopData), fight.Session.ToDefenderInputs());
            Assert.AreEqual(local.checksum, fight.Result.checksum, "Server and client battles must be identical");
        }

        [UnityTest]
        public IEnumerator BattleMenu_ShowsAllFiveModes()
        {
            Controller.OpenAttack();
            yield return WaitFor(() => Find("OpenSiegeButton") != null);
            foreach (var name in new[] { "FindRivalButton", "OpenDungeonsButton", "OpenSiegeButton", "OpenArenaButton", "OpenTrialsButton" })
                Assert.IsNotNull(Find(name), name + " should be on the Battle menu");
            Assert.IsTrue(Find("OpenSiegeButton").interactable, "A village defends itself even without a hero");
        }

        [UnityTest]
        public IEnumerator Siege_ThroughTheScreens_PlaysTheServersBattle_AndPays()
        {
            yield return PutFirstHeroOnBase();
            long gold = 0, mana = 0;
            yield return Await(_direct.GetStateAsync(), s => { gold = s.Village.gold; mana = s.Village.mana; });

            Controller.OpenAttack();
            yield return ClickWhenReady("OpenSiegeButton");
            yield return WaitFor(() => Find("SiegeLevel1") != null);
            Assert.IsFalse(Find("SiegeLevel2").interactable, "Level 2 opens after level 1 is held");
            Find("SiegeLevel1").onClick.Invoke();
            yield return WaitFor(() => Controller.CurrentRaidPhase == RaidPhase.Battle);

            var fight = Controller.LastModeFight;
            Assert.IsTrue(fight.IsSiege);
            Assert.Greater(fight.Troops.Count, 0, "Attackers should land");
            Assert.AreEqual(0, fight.Placements.Count, "The player lands nothing in a siege");
            CheckFightMatchesLocalRules();

            Controller.SkipBattle();
            Assert.AreEqual(RaidPhase.Resolved, Controller.CurrentRaidPhase);
            Assert.IsNotNull(Find(fight.Won ? "SiegeAgainButton" : "ReturnHomeButton"));
            yield return Await(_direct.GetStateAsync(), s =>
            {
                Assert.AreEqual(gold + fight.GoldEarned, s.Village.gold, "Gold must match what the server says it paid");
                Assert.AreEqual(mana + fight.ManaEarned, s.Village.mana);
                Assert.AreEqual(s.Village.gold, Controller.Village.Gold, "The client shows the server's gold");
            });
            if (fight.Won)
            {
                Assert.Greater(fight.GoldEarned + fight.ManaEarned, 0, "A first hold pays");
                yield return Await(_direct.QuestBoardAsync(), b => Assert.AreEqual(1, b.Quests.First(q => q.id == "siege").progress));
            }
            Controller.FinishModeFight();
            yield return null;
            Assert.AreEqual(VillageScreen.Home, Controller.UiState.Screen);
        }

        [UnityTest]
        public IEnumerator Siege_LockedLevel_IsRefusedByTheServer()
        {
            yield return Refused(_direct.SiegeFightAsync("locked-" + Guid.NewGuid().ToString("N"), 3), "SIEGE_LOCKED");
            yield return Refused(_direct.SiegeFightAsync("missing-" + Guid.NewGuid().ToString("N"), 99), "SIEGE_NOT_FOUND");
        }

        [UnityTest]
        public IEnumerator Arena_ThroughTheScreens_DuelsAndMovesPoints()
        {
            yield return PutFirstHeroOnBase();
            Controller.OpenAttack();
            yield return ClickWhenReady("OpenArenaButton");
            yield return ClickWhenReady("ArenaFightButton");
            yield return WaitFor(() => Controller.CurrentRaidPhase == RaidPhase.Battle);

            var fight = Controller.LastModeFight;
            Assert.IsTrue(fight.IsArena);
            Assert.AreEqual(1, fight.Placements.Count, "Only the hero fights in the Arena");
            Assert.AreEqual(1, fight.Session.defenders.Count, "One hero guards the shrine");
            Assert.IsFalse(string.IsNullOrEmpty(fight.OpponentName));
            CheckFightMatchesLocalRules();
            Assert.AreEqual(fight.Won, fight.Result.dojoDestroyed, "A duel is won by toppling the shrine");
            Assert.AreEqual(fight.Won, fight.PointsDelta > 0);

            Controller.SkipBattle();
            yield return WaitFor(() => Find("ArenaAgainButton") != null);
            StringAssert.Contains("points", AllText);
            yield return Await(_direct.ArenaBoardAsync(), board =>
            {
                Assert.AreEqual(fight.Points, board.points);
                Assert.AreEqual(fight.Won ? 1 : 0, board.wins);
                Assert.GreaterOrEqual(board.rank, 1, "The player is ranked");
                // Ties share a rank, so a tied player can be ranked within the top yet listed below it.
                foreach (var mine in board.top.Where(e => e.isMe)) Assert.AreEqual(fight.Points, mine.points);
            });
            yield return Await(_direct.QuestBoardAsync(), b => Assert.AreEqual(1, b.Quests.First(q => q.id == "arena").progress));
        }

        [UnityTest]
        public IEnumerator Arena_WithoutAHero_IsRefused()
        {
            yield return Refused(_direct.ArenaFightAsync("nohero-" + Guid.NewGuid().ToString("N")), "NO_TEAM");
        }

        [UnityTest]
        public IEnumerator DailyGift_FromItsShortcut_OpensOnce()
        {
            long crystals = 0;
            yield return Await(_direct.GetStateAsync(), s => crystals = s.Crystals);
            Controller.OpenShortcut("gift");
            yield return ClickWhenReady("OpenGiftButton");
            yield return WaitFor(() => Controller.ServerCrystals > crystals);
            QuestBoard board = null;
            yield return Await(_direct.QuestBoardAsync(), b => board = b);
            Assert.IsFalse(board.GiftReady);
            Assert.AreEqual(crystals + board.GiftCrystals, Controller.ServerCrystals);
            yield return Refused(_direct.GiftOpenAsync("again-" + Guid.NewGuid().ToString("N")), "GIFT_ALREADY_OPENED");
        }

        [UnityTest]
        public IEnumerator Quests_CountProgress_AndOnlyPayWhenDone()
        {
            yield return Refused(_direct.QuestClaimAsync("early-" + Guid.NewGuid().ToString("N"), "upgrade"), "QUEST_NOT_DONE");
            yield return Refused(_direct.QuestClaimAsync("fake-" + Guid.NewGuid().ToString("N"), "free_gems"), "QUEST_NOT_FOUND");

            // Upgrading counts. The server decides, so the upgrade goes through it.
            var mine = Controller.Village.Buildings.First(b => b.Definition.id == "gold_mine").Id;
            yield return Await(_direct.UpgradeAsync("up-" + Guid.NewGuid().ToString("N"), mine, false), _ => { });
            long crystals = 0;
            yield return Await(_direct.GetStateAsync(), s => crystals = s.Crystals);

            Controller.OpenShortcut("quests");
            yield return ClickWhenReady("Quest_upgrade");
            yield return WaitFor(() => Controller.ServerCrystals > crystals);
            yield return Await(_direct.QuestBoardAsync(), b =>
            {
                var quest = b.Quests.First(q => q.id == "upgrade");
                Assert.IsTrue(quest.claimed);
                Assert.AreEqual(crystals + quest.crystals, Controller.ServerCrystals);
            });
            yield return Refused(_direct.QuestClaimAsync("twice-" + Guid.NewGuid().ToString("N"), "upgrade"), "QUEST_ALREADY_CLAIMED");
        }

        [UnityTest]
        public IEnumerator Rankings_AndMail_OpenFromTheirShortcuts()
        {
            Controller.OpenShortcut("trophy");
            yield return WaitFor(() => AllText.Contains("Rankings") && AllText.Contains("points"));
            Controller.CloseAll();
            Controller.OpenShortcut("mail");
            yield return WaitFor(() => AllText.Contains("No one has raided your village yet."));
        }
        /// <summary>Pictures of the new screens for human review. Needs SD_CAPTURE_DIR as well as the server.</summary>
        [UnityTest]
        public IEnumerator CaptureModeScreens()
        {
            var dir = Environment.GetEnvironmentVariable("SD_CAPTURE_DIR");
            if (string.IsNullOrEmpty(dir)) Assert.Ignore("SD_CAPTURE_DIR not set.");
            Directory.CreateDirectory(dir);
            LogAssert.ignoreFailingMessages = true;
            var game = _root.GetComponent<VillageBootstrap>();
            var camera = game.IsoCamera.Camera;
            var canvas = _root.GetComponentInChildren<Canvas>();
            var texture = new RenderTexture(1600, 900, 24) { antiAliasing = 4 };
            camera.targetTexture = texture;
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = camera;
            canvas.planeDistance = 5f;
            yield return null;
            Func<string, IEnumerator> shot = name => ScreenCaptureTests.Capture(camera, texture, Path.Combine(dir, name + ".png"));

            yield return PutFirstHeroOnBase();
            Controller.OpenAttack();
            yield return WaitFor(() => Find("OpenSiegeButton") != null);
            yield return shot("40_battle_menu");
            Find("OpenSiegeButton").onClick.Invoke();
            yield return WaitFor(() => Find("SiegeLevel1") != null);
            yield return shot("41_siege_levels");
            Find("SiegeLevel1").onClick.Invoke();
            yield return WaitFor(() => Controller.CurrentRaidPhase == RaidPhase.Battle);
            yield return new WaitForSeconds(11f);
            yield return shot("42_siege_battle");
            Controller.SkipBattle();
            // The earned stars land one after another; the picture is taken once the last has landed.
            yield return new WaitForSecondsRealtime(1.6f);
            yield return shot("43_siege_result");
            Controller.FinishModeFight();

            Controller.OpenAttack();
            yield return ClickWhenReady("OpenArenaButton");
            yield return WaitFor(() => Find("ArenaFightButton") != null);
            yield return shot("44_arena");
            Find("ArenaFightButton").onClick.Invoke();
            yield return WaitFor(() => Controller.CurrentRaidPhase == RaidPhase.Battle);
            yield return new WaitForSeconds(4f);
            yield return shot("45_arena_battle");
            Controller.SkipBattle();
            // The earned stars land one after another; the picture is taken once the last has landed.
            yield return new WaitForSecondsRealtime(1.6f);
            yield return shot("46_arena_result");
            Controller.FinishModeFight();

            Controller.OpenShortcut("trophy");
            yield return WaitFor(() => AllText.Contains("Rankings") && AllText.Contains("points"));
            yield return shot("47_rankings");
            Controller.OpenShortcut("quests");
            yield return WaitFor(() => _root.GetComponentsInChildren<RectTransform>(false).Any(r => r.name == "QuestRow_collect"));
            yield return shot("48_quests");
            Controller.OpenShortcut("gift");
            yield return WaitFor(() => Find("OpenGiftButton") != null);
            yield return shot("49_gift");
            Controller.OpenShortcut("mail");
            yield return WaitFor(() => AllText.Contains("No one has raided your village yet.") || _root.GetComponentsInChildren<RectTransform>(false).Any(r => r.name.StartsWith("MailRow_")));
            yield return shot("52_mail");
            Controller.OpenShop("mana");
            yield return WaitFor(() => Find("Shop_mana_flask") != null);
            yield return shot("53_shop");
            Controller.OpenChest();
            yield return WaitFor(() => _root.GetComponentsInChildren<Text>(false).Any(t => t.name == "ChestWords"));
            yield return shot("54_chest");
            Controller.CloseAll();

            var gate = Controller.Village.Buildings.First(b => b.Definition.id == "summoning_gate").Id;
            Controller.Select(gate);
            Controller.ShowSummoningGate();
            yield return ClickWhenReady("SummonOneButton");
            yield return WaitFor(() => Controller.RevealShowing);
            yield return new WaitForSecondsRealtime(1.2f);
            yield return shot("50_summon_reveal");
            Controller.CloseReveal();
            yield return shot("51_summon_results");
            camera.targetTexture = null;
        }
        [UnityTest]
        public IEnumerator Guild_FoundChatAndLeave_ThroughTheScreens()
        {
            Controller.OpenShortcut("friends");
            yield return WaitFor(() => Find("FoundGuildButton") != null);
            var guildName = "Test" + Guid.NewGuid().ToString("N").Substring(0, 6);
            _root.GetComponentsInChildren<InputField>().First(f => f.name == "GuildNameInput").text = guildName;
            long gems = 0;
            yield return Await(_direct.GetStateAsync(), s => gems = s.Crystals);
            Find("FoundGuildButton").onClick.Invoke();
            yield return WaitFor(() => Find("SendChatButton") != null);
            Assert.AreEqual(guildName, Controller.CurrentGuild.guild.name);
            Assert.AreEqual(gems - 300, Controller.ServerCrystals, "Founding costs 300 gems");

            _root.GetComponentsInChildren<InputField>().First(f => f.name == "GuildChatInput").text = "hello <b>guild</b>";
            Find("SendChatButton").onClick.Invoke();
            yield return WaitFor(() => Controller.CurrentGuild.chat.Count == 1);
            Assert.AreEqual("hello <b>guild</b>", Controller.CurrentGuild.chat[0].text, "Text is shown as typed, never as markup");
            Assert.IsTrue(_root.GetComponentsInChildren<Text>().Where(t => t.text.Contains("hello")).All(t => !t.supportRichText));

            Find("LeaveGuildButton").onClick.Invoke();
            yield return null;
            Assert.IsNotNull(Controller.CurrentGuild.guild, "The first tap only asks");
            Find("LeaveGuildButton").onClick.Invoke();
            yield return WaitFor(() => Find("FoundGuildButton") != null);
            Assert.IsNull(Controller.CurrentGuild.guild);
        }

        [UnityTest]
        public IEnumerator Name_SetInTheMenu_ShowsOnTheHud()
        {
            Controller.SetPlayerName("  Night Owl ");
            yield return WaitFor(() => Controller.PlayerName == "Night Owl");
            StringAssert.Contains("Night Owl", AllText);
            Controller.SetPlayerName("<x>");
            yield return WaitFor(() => Controller.LastMessage != null);
            Assert.AreEqual("Night Owl", Controller.PlayerName, "A refused name changes nothing");
        }

        [UnityTest]
        public IEnumerator CaptureGuildScreens()
        {
            var dir = Environment.GetEnvironmentVariable("SD_CAPTURE_DIR");
            if (string.IsNullOrEmpty(dir)) Assert.Ignore("SD_CAPTURE_DIR not set.");
            Directory.CreateDirectory(dir);
            LogAssert.ignoreFailingMessages = true;
            var game = _root.GetComponent<VillageBootstrap>();
            var camera = game.IsoCamera.Camera;
            var canvas = _root.GetComponentInChildren<Canvas>();
            var texture = new RenderTexture(1600, 900, 24) { antiAliasing = 4 };
            camera.targetTexture = texture;
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = camera;
            canvas.planeDistance = 5f;
            Controller.SetPlayerName("Captain Ash");
            yield return WaitFor(() => Controller.PlayerName == "Captain Ash");
            Controller.OpenShortcut("friends");
            yield return WaitFor(() => Find("FoundGuildButton") != null);
            yield return ScreenCaptureTests.Capture(camera, texture, Path.Combine(dir, "70_guild_browse.png"));
            _root.GetComponentsInChildren<InputField>().First(f => f.name == "GuildNameInput").text = "Ash" + Guid.NewGuid().ToString("N").Substring(0, 4);
            _root.GetComponentsInChildren<InputField>().First(f => f.name == "GuildMottoInput").text = "We hold the ridge";
            Find("FoundGuildButton").onClick.Invoke();
            yield return WaitFor(() => Find("SendChatButton") != null);
            _root.GetComponentsInChildren<InputField>().First(f => f.name == "GuildChatInput").text = "Welcome to the guild!";
            Find("SendChatButton").onClick.Invoke();
            yield return WaitFor(() => Controller.CurrentGuild.chat.Count == 1);
            yield return ScreenCaptureTests.Capture(camera, texture, Path.Combine(dir, "71_guild.png"));
            Find("LeaveGuildButton").onClick.Invoke();
            yield return null;
            Find("LeaveGuildButton").onClick.Invoke();
            yield return WaitFor(() => Find("FoundGuildButton") != null);
            camera.targetTexture = null;
        }
        [UnityTest]
        public IEnumerator TreasureChest_AndShop_ThroughTheirButtons()
        {
            long gold = 0, mana = 0, gems = 0;
            yield return Await(_direct.GetStateAsync(), s => { gold = s.Village.gold; mana = s.Village.mana; gems = s.Crystals; });
            Controller.OpenShortcut("chest");
            yield return ClickWhenReady("OpenChestButton");
            yield return WaitFor(() => Controller.CurrentChest != null && !Controller.CurrentChest.Ready);
            yield return Await(_direct.GetStateAsync(), s =>
            {
                Assert.AreEqual(gold + 500, s.Village.gold, "A level 1 Town Hall's chest holds 500 gold");
                Assert.AreEqual(mana + 150, s.Village.mana);
                mana = s.Village.mana;
            });

            Controller.OpenShop("mana");
            yield return ClickWhenReady("Shop_mana_flask");
            yield return WaitFor(() => Controller.Village.Mana == mana + 1200);
            Assert.AreEqual(gems - 50, Controller.ServerCrystals);
        }
        [UnityTest]
        public IEnumerator Gear_FromTheHeroPage_RaisesTheStatOnTheServer()
        {
            var hero = Controller.Roster.Heroes.First();
            var before = Controller.Roster.StatsOf(hero);
            Controller.ShowHeroDetail(hero.InstanceId);
            yield return ClickWhenReady("GearButton");
            yield return ClickWhenReady("Gear_0");
            yield return WaitFor(() => Controller.Roster.Find(hero.InstanceId).Gear[0] == 1);
            var after = Controller.Roster.StatsOf(Controller.Roster.Find(hero.InstanceId));
            Assert.AreEqual(before.Attack * 105 / 100, after.Attack, "A level 1 weapon adds 5% attack");
            Assert.AreEqual(before.Hp, after.Hp);
            yield return Await(_direct.GetStateAsync(), s => StringAssert.Contains("\"gear\":[1,0,0]", s.RosterJson.Replace(" ", "").Replace("\n", "").Replace("\r", "")));
        }
        [UnityTest]
        public IEnumerator Mastery_FromTheHeroPage_RollsATalentAndBuysARankOnTheServer()
        {
            var hero = Controller.Roster.Heroes.First();
            var tokens = Controller.Roster.ItemCount("talent_token");
            var manuals = Controller.Roster.ItemCount("technique_manual");
            Assert.GreaterOrEqual(tokens, 1, "A dev account starts with Talent Tokens");
            Assert.GreaterOrEqual(manuals, 2, "A dev account starts with Technique Manuals");

            Controller.ShowHeroDetail(hero.InstanceId);
            yield return ClickWhenReady("MasteryButton");
            yield return ClickWhenReady("TalentRollButton");
            yield return WaitFor(() => Controller.Roster.Find(hero.InstanceId).TalentLevel > 0);
            var rolled = Controller.Roster.Find(hero.InstanceId);
            Assert.IsNotNull(Controller.Roster.Catalog.Talents.Find(rolled.TalentId), "The server gave a talent the catalog knows");
            Assert.AreEqual(tokens - 1, Controller.Roster.ItemCount("talent_token"));
            yield return WaitFor(() => Object.FindObjectsByType<Text>(FindObjectsSortMode.None).Any(t => t.name == "MasteryNote"));

            // Rank 2 needs hero level 16.
            Controller.ShowHeroDetail(hero.InstanceId);
            yield return ClickWhenReady("DevLevelButton");
            yield return WaitFor(() => Controller.Roster.Find(hero.InstanceId).Level == HeroMath.MaxLevel);
            Controller.ShowMastery(hero.InstanceId);
            yield return ClickWhenReady("RankUpButton");
            yield return WaitFor(() => Controller.Roster.Find(hero.InstanceId).TechniqueRank == 2);
            Assert.AreEqual(manuals - 2, Controller.Roster.ItemCount("technique_manual"));
            yield return Await(_direct.GetStateAsync(), s => StringAssert.Contains("\"techniqueRank\":2", s.RosterJson.Replace(" ", "").Replace("\n", "").Replace("\r", "")));
        }
        [UnityTest]
        public IEnumerator TrialTower_FirstFloor_ThroughTheScreens()
        {
            yield return PutFirstHeroOnBase();
            Controller.OpenAttack();
            yield return ClickWhenReady("OpenTrialsButton");
            yield return WaitFor(() => Find("TrialFloor1") != null);
            Assert.IsFalse(Find("TrialFloor2").interactable, "Floor 2 opens after floor 1");
            Find("TrialFloor1").onClick.Invoke();
            yield return WaitFor(() => Controller.CurrentRaidPhase == RaidPhase.Battle);
            var fight = Controller.LastModeFight;
            Assert.IsTrue(fight.IsTrial);
            Assert.AreEqual(1, fight.Level);
            CheckFightMatchesLocalRules();
            Controller.SkipBattle();
            yield return WaitFor(() => Find("TrialAgainButton") != null);
            if (fight.Won) Assert.Greater(fight.CrystalsEarned, 0, "The first clear pays");
        }
        [UnityTest]
        public IEnumerator LoadingScreen_LiftsOnceTheServerAnswers()
        {
            Assert.IsFalse(Controller.LoadingShowing, "Connected, so the loading screen has gone");
            yield return new WaitForSecondsRealtime(0.5f);
            Assert.IsFalse(_root.GetComponentsInChildren<RectTransform>(true).Any(r => r.name == "LoadingScreen"), "It fades out and is removed");
        }
    }
}
