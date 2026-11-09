using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using ShinobiDominion.Core;

namespace ShinobiDominion.Tests
{
    public class ServerModelTests
    {
        private static HeroCatalog Catalog()
        {
            var candidates = new[]
            {
                Path.Combine(AppContext.BaseDirectory, "Config", "heroes.json"),
                Path.Combine("Assets", "_Game", "Resources", "Config", "heroes.json")
            };
            return HeroCatalog.FromJson(File.ReadAllText(candidates.First(File.Exists)));
        }

        private const string StateJson =
            "{\"schemaVersion\":1,\"crystals\":2900,\"heroes\":[" +
            "{\"instanceId\":\"aaaa\",\"archetypeId\":\"stone_titan\",\"evolutionIndex\":1,\"level\":5,\"experience\":0,\"locked\":true,\"baseSlot\":3}," +
            "{\"instanceId\":\"bbbb\",\"archetypeId\":\"river_scout\",\"evolutionIndex\":0,\"level\":1,\"experience\":0,\"locked\":false,\"baseSlot\":null}" +
            "],\"items\":{\"awakening_scroll\":2}}";

        [Test]
        public void State_ParsesCrystalsAndSlots()
        {
            var state = ServerState.Parse(StateJson);
            Assert.AreEqual(2900, state.Crystals);
            Assert.AreEqual(1, state.HeroBySlot.Count);
            Assert.AreEqual("aaaa", state.HeroBySlot[3]);
        }

        [Test]
        public void Roster_ReplacesEverythingFromServer()
        {
            var roster = new HeroRoster(Catalog());
            var stale = roster.AddHero("thunder_empress");
            roster.AddItem("ascension_stone", 9);

            roster.ReplaceWithServerState(ServerState.Parse(StateJson).RosterJson);

            Assert.AreEqual(2, roster.HeroCount);
            Assert.IsNull(roster.Find(stale.InstanceId), "Anything the server does not report is gone");
            Assert.AreEqual(0, roster.ItemCount("ascension_stone"));
            Assert.AreEqual(2, roster.ItemCount("awakening_scroll"));
            Assert.AreEqual(1, roster.Find("aaaa").EvolutionIndex);
            Assert.IsTrue(roster.Find("aaaa").Locked);
        }

        [Test]
        public void Roster_BadSnapshot_LeavesRosterUntouched()
        {
            var roster = new HeroRoster(Catalog());
            var kept = roster.AddHero("thunder_empress");
            var bad = "{\"schemaVersion\":1,\"heroes\":[{\"instanceId\":\"x\",\"archetypeId\":\"river_scout\",\"level\":999}]}";
            Assert.Throws<VillageSaveException>(() => roster.ReplaceWithServerState(bad));
            Assert.IsNotNull(roster.Find(kept.InstanceId));
        }

        [Test]
        public void State_RejectsNegativeCrystalsAndGarbage()
        {
            Assert.Throws<FormatException>(() => ServerState.Parse("{\"crystals\":-1}"));
            Assert.Throws<FormatException>(() => ServerState.Parse("not json"));
        }

        [Test]
        public void SummonResult_ParsesRollsAndNestedState()
        {
            var json = "{\"poolVersion\":4,\"rolls\":[{\"rollIndex\":0,\"instanceId\":\"aaaa\",\"archetypeId\":\"stone_titan\",\"rarity\":\"Legendary\",\"copiesOwned\":2}],\"state\":" + StateJson + "}";
            var result = ServerSummonResult.Parse(json);
            Assert.AreEqual(4, result.PoolVersion);
            Assert.AreEqual(1, result.Rolls.Count);
            Assert.AreEqual(2, result.Rolls[0].copiesOwned);
            Assert.AreEqual(2900, result.State.Crystals);
        }

        [Test]
        public void Odds_KeepTheServersExactValues()
        {
            var odds = ServerOdds.Parse("{\"poolVersion\":2,\"status\":\"TEST\",\"priceCrystalsPerRoll\":100,\"totalWeight\":10,\"items\":[{\"archetypeId\":\"a\",\"displayName\":\"A\",\"rarity\":\"Common\",\"weight\":10,\"chance\":\"100.000%\"}],\"rarityChance\":{\"Common\":\"100.000%\"}}");
            Assert.AreEqual("100.000%", odds.items[0].chance);
            Assert.AreEqual(2, odds.poolVersion);
        }

        [Test]
        public void UnknownServerErrors_TellThePlayerARetryIsSafe()
        {
            StringAssert.Contains("retry", ServerErrorText.Describe("SOMETHING_NEW"));
            Assert.AreEqual("Not enough gems", ServerErrorText.Describe("INSUFFICIENT_FUNDS"));
        }
    }
}
