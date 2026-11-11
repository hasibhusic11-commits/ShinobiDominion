# Screen inventory (overhaul status, 2026-11-11)

Every screen is built in code by a `Show*` method on `VillageController`. Status meanings:
- **v3 done**: redesigned in UI kit v3 and screenshot-reviewed.
- **legacy**: still the older v2 look.

| Screen | Entry (file : method) | Data it shows | Status |
|---|---|---|---|
| Home HUD | `VillageController.Home.cs`, `VillageUi.cs` | gold, Mana, gems, player level, power, ATTACK, shortcuts (ADR-033) | legacy (owner mockup is target) |
| **Battle menu** | `VillageController.BattleMenu.cs : ShowBattleHub` | heroes on bases, server online, dungeon stars, last message | **v3 done (M2)** |
| Raid: choose rival | `Raid.cs : ShowRaidList` | server rival list, loot, shields, revenge | legacy |
| Raid: prepare | `Raid.cs : ShowPreparePanel` | heroes, troops, spells | legacy |
| Deploy / battle | `BattleScreen.cs : ShowDeployScreen` | live battle | legacy |
| Raid result | `Raid.cs : ShowRaidResult / ShowOutcome` | stars, loot | legacy |
| Dungeons map | `Campaign.cs : ShowCampaign` | 15 stages, stars | legacy |
| Village Siege | `Modes.cs : ShowSiege` | siege levels | legacy |
| Arena | `Modes.cs : ShowArena` | rank, opponents | legacy |
| Mode result | `Modes.cs : ShowModeResult` | outcome, rewards | legacy |
| Rankings | `Modes.cs : ShowRankings` | leaderboard | legacy |
| Trial Tower | `Trials.cs : ShowTrials` | floors | legacy |
| Quests / Gift / Mail | `Modes.cs : ShowQuests / ShowGift / ShowMail` | server lists | legacy |
| Shop / Chest | `Home.cs : ShowShop / ShowChest` | offers, chest timer | legacy |
| Hero window (tabs) | `HeroPage.cs : OpenHeroWindow` | roster, stats | legacy |
| Hero detail / mastery / gear | `HeroScreens.cs`, `Mastery.cs`, `Gear.cs` | hero data | legacy |
| Hero altar / picker / evolution | `Heroes.cs` | roster, evolution costs | legacy |
| Summoning gate / reveal / rates | `Server.cs : ShowSummoningGate`, `SummonReveal.cs`, `HeroScreens.cs : ShowSummonRates` | server summon results, published rates | legacy |
| Guild | `Guild.cs : ShowGuild / ShowNameWindow` | guild data | legacy |
| Building panel / action bar | `VillageController.cs : ShowBuildingDetails`, `ActionBar.cs` | building level, costs | legacy |
| Build catalog (Builder Hall only) | `VillageController.cs : ShowCatalog` | building list | legacy |
| Settings | `VillageController.cs : OpenSettings` | options | legacy |
| Loading | `Loading.cs : ShowLoading` | connect state | legacy |

The next screens follow the owner's priority. The suggested order is: Arena and Siege (owner concepts exist in `references/redesign_concepts/`), then Rankings, then Quests and Gift, then the Summon reveal and results.
