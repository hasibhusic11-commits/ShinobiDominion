# Genre gap review: Shinobi Dominion against Castle Clash

Written 2026-11-09 at the owner's request: "Everything should be the exact same as Castle Clash. Go through the entire game and match it to Castle Clash."

**What "match" means here.** The game copies how the genre plays: its systems, flow, pacing and screen layout. It does not copy Castle Clash's names, characters, art, look, levels, text or exact numbers. Copying those would put the game at legal risk, and Shinobi Dominion needs its own identity. Castle Clash names appear below only to say which system is meant.

**Sources.** `CASTLE_CLASH_REFERENCE.md` (the fan handbook and guides) and general knowledge of the genre. Castle Clash changes often, so a detail here may be out of date. Check a system against the live game before its numbers are tuned.

**Owner's decisions, 2026-11-09** (these settle the four **Decide** rows below):
- **Heroes:** up to 6 heroes in a battle and on defense, replacing the one-hero rule.
- **Deploying:** real time, dropping cards during the battle, as in Castle Clash. The server must still decide every result, so it re-runs the battle from the timed list of drops (see ADR to come).
- **Raids:** attacking a real player takes part of their stored gold and Mana, and the defender then gets a shield and a revenge option.
- **Pets:** still none.

Status: **Have** (built), **Partial** (built, but differs in a way that matters), **Missing**, or **Decide** (the owner set a rule that differs from Castle Clash, so it needs the owner's call before changing).

## 1. Base and economy
| Castle Clash system | Shinobi Dominion | Status | Proposed |
|---|---|---|---|
| Town Hall gates building counts and levels | Town Hall 1–15 gates counts and levels | Have | — |
| Gold Mines, Mana Mills, collect by tapping | Same | Have | — |
| Gold and Mana storage (Vaults, Warehouse) | Same, plus a Mana cap (ADR-046) | Have | — |
| Builders; most upgrades take time | Builder Hall, 2–4 builders, timed upgrades, finish for gems | Have | — |
| Walls dragged in runs, upgraded as a group | Same | Have | — |
| Traps hidden from attackers | Bomb and Hero Traps | Have | — |
| Arrow, Magic and Cannon Towers | One switchable tower (owner's design) | Have | — |
| Decorations shop (flags, statues) | None | Missing | A small decoration catalog, cosmetic only |
| Shards, Honor Badges, Merits and other secondary currencies | Gold, Mana, gems | Partial | Add currencies only when a system needs one (e.g. a badge currency for the Arena shop) |

## 2. Heroes
| Castle Clash system | Shinobi Dominion | Status | Proposed |
|---|---|---|---|
| Summon heroes at the Heroes Altar | Summoning Gate, no pity, server-side odds | Have | — |
| Several heroes fight together in raids and defend on several bases | Up to six (ADR-048) | Have | — |
| Levels with Mana, from battle XP | Same (Mana training and battle XP) | Have | — |
| Evolution / awakening with duplicates and catalysts | Evolution with one duplicate and a catalyst | Have | — |
| Talents (a random passive per hero, rerollable) | None | Missing | One talent slot per hero, rolled on the server, rerolled with a currency |
| Crests (socketed passive bonuses) | None | Missing | Could share the gear screen |
| Skill levels raised with an item | Specials scale by rarity only | Missing | Skill books from dungeons and quests |
| Hero gear / armory | Weapon, Armor, Charm, levels 0–10 | Have | — |
| Pets | None (owner said no pets) | **Decide** | Leave out unless the owner changes that |

## 3. Troops and spells
| Castle Clash system | Shinobi Dominion | Status | Proposed |
|---|---|---|---|
| Several troop types, trained into Army Camps | Troop list, camps, Training Center | Have | — |
| Troop upgrades through research | Relic Hall research | Have | — |
| Spells cast during a raid (heal, damage, haste) | None | Missing | 3–4 spells bought with Mana, placed before the battle like troops (see 4) |

## 4. Raids and battles
| Castle Clash system | Shinobi Dominion | Status | Proposed |
|---|---|---|---|
| Deploy heroes and troops **during** the battle, by tapping cards | Everything is placed **before** the battle; the server fights it, then it plays back | **Decide** | Real-time deployment needs the server to run the battle live or check the timing of every tap. It is a large, risky change to the anti-cheat design (the server decides every result). Keep the current model unless the owner wants this badly |
| Attack screen: card row, End Battle, time, loot, damage % | Same layout (done today) | Have | — |
| Available loot is gold and Mana | Same (done today) | Have | — |
| Raiding another player takes part of their stored resources; the victim gets a shield | 20% of their gold and Mana by damage, 12-hour shield (ADR-049) | Have | — |
| Trophies / might ranking, matched by strength | Rivals matched by Town Hall level; rankings exist | Partial | Add a trophy score won and lost in raids, shown on the rankings |
| Search for an opponent costs gold; skip to the next | One rival offered at a time | Partial | A "Next" button that costs a little gold |
| Revenge on whoever raided you | Once per raid, from Mail, through the attacker's shield (ADR-049) | Have | — |
| Replays of attacks on your village | None | Missing | Battles are already recorded on the server, so a defense log with replays is cheap to add |

## 5. Modes
| Castle Clash mode | Shinobi Dominion | Status | Proposed |
|---|---|---|---|
| Dungeons, three difficulties, farmable | 15 stages, one difficulty; gold and Mana only for new stars | Partial | Replays pay a share of the loot up to a daily limit (the owner asked for farming); add a harder second difficulty later |
| Tower defense against waves (Here Be Monsters) | Village Siege, 10 levels | Have | Team version later |
| Hero trials against strong AI heroes | Trial Tower, 30 floors | Have | — |
| Arena, heroes charge lanes, earn badges | Arena duels for points, rankings | Partial | Add a badge currency and an Arena shop |
| Guild boss, guild wars, territory modes | Guild chat only | Missing | Guild boss first (shared damage, rewards by contribution); guild wars much later |
| Server-wide events | None | Missing | A rotating weekly event mode, after the core is solid |

## 6. Social and daily habits
| Castle Clash system | Shinobi Dominion | Status | Proposed |
|---|---|---|---|
| Guilds with chat, donations, guild buildings | Create, join, chat, leave | Partial | Donations of troops or resources between members |
| Daily quests and a daily reward track | Quests and a daily gift | Have | — |
| Login calendar, events, a pass | None; the new button kit has Events, Pass and Rewards buttons | Missing | A 7-day login calendar first, then an event screen; a pass only with the owner's monetisation plan |
| Mail | Mail shortcut | Partial | Check it delivers system rewards |
| Rankings | Rankings | Have | Add trophies (see 4) |

## Suggested order
1. **Owner decisions first** (the four **Decide** rows): several heroes per battle, real-time deployment, raids taking loot, pets. Each changes a core rule the owner set earlier.
2. **Farmable dungeons**: replays pay part of the loot, up to a daily limit. The owner already asked for farming, and it is small.
3. **Trophies and opponent search** ("Next"), so raids feel like the genre.
4. **Spells**, placed before the battle.
5. **Talents and skill books**, the next layer of hero progression.
6. **Defense log with replays.**
7. **Login calendar and events**, using the kit's Events, Rewards and Pass buttons.
8. **Guild boss**, then guild donations.
9. **Decorations**, a second dungeon difficulty, an Arena shop.
