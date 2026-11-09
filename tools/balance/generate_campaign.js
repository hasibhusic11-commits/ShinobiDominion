// Writes the dungeon campaign into raids.json. Stages 1 and 2 are hand-made and only gain rewards and strength here.
// All numbers and names are proposed test values. Edit the tables below and rerun:
//     node tools/balance/generate_campaign.js unity/Assets/_Game/Resources/Config/raids.json
//
// Dungeons are not player villages (owner, 2026-11-09): like Castle Clash's, they may hold many guard heroes and
// many towers. To keep them farmable for Common and Elite heroes, each stage has a strength percent that scales the
// HP and attack of everything in it. The strengths come from the balance tuner (BalanceReportTests.TuneCampaign),
// which writes tools/balance/campaign_strength.json; without that file every stage is at 100%.
const fs = require('fs');
const path = require('path');

const [raidsPath] = process.argv.slice(2);
if (!raidsPath) throw new Error('usage: node generate_campaign.js <raids.json>');
const config = path.dirname(raidsPath);
const catalog = JSON.parse(fs.readFileSync(path.join(config, 'buildings.json'), 'utf8')).buildings;
const SIZE = Object.fromEntries(catalog.map(b => [b.id, [b.width, b.depth]]));
const strengthFile = path.join(__dirname, 'campaign_strength.json');
const STRENGTH = fs.existsSync(strengthFile) ? JSON.parse(fs.readFileSync(strengthFile, 'utf8')) : {};

const CHAPTERS = { 1: 'Cinder Ridge', 2: 'Mist Hollow', 3: 'Ashen Peak' };

const STAGES = {
  3: ['Charcoal Pass', 'A narrow camp that guards the road through the ridge.'],
  4: ['Kiln Yard', 'Smoke hides the towers here until you are close.'],
  5: ['Ember Keep', 'The first true fortress. Its keeper holds an Awakening Scroll.'],
  6: ['Reed Marsh Camp', 'Raiders hide among the reeds at the edge of the hollow.'],
  7: ['Lantern Ford', 'Magic towers light the only crossing.'],
  8: ['Fogbound Shrine', 'Its guards know every path through the mist.'],
  9: ['Willow Stockade', 'Thick walls, and a garrison that does not retreat.'],
  10: ['Hollow Citadel', 'The heart of the hollow. Its lord holds an Ascension Stone.'],
  11: ['Slate Stair', 'The climb begins, under fire the whole way.'],
  12: ['Cold Forge', 'Weapons for the peak are made here, and tested on visitors.'],
  13: ['Whitecrag Bastion', 'Veteran heroes hold the last wall before the summit.'],
  14: ['Sky Gate', 'Nothing passes the gate without a fight.'],
  15: ['Summit Sanctum', 'The final stronghold. Its master holds a Sovereign Scroll.'],
};

const BOSS_ITEMS = { 5: 'awakening_scroll', 10: 'ascension_stone', 15: 'sovereign_scroll' };
// Each chapter's last stage is led by a Legendary.
const BOSSES = { 5: 'solar_beast_sage', 10: 'thunder_empress', 15: 'stone_titan' };
const COMMONS = ['ember_recruit', 'reed_archer', 'stone_apprentice', 'talisman_novice', 'bell_guard', 'mist_courier', 'spark_tinkerer', 'ash_monk', 'gale_runner', 'moth_herbalist'];
const ELITES = ['lantern_warden', 'scarlet_fan_dancer', 'twin_chain_reaper', 'frost_lake_monk', 'boulder_fist_elder', 'mask_carver', 'thunder_drum_monk', 'salt_wind_nomad', 'silver_moon_archer', 'cinder_shield_captain'];

// Raids pay Mana as well as gold (owner, 2026-11-09). Mills make Mana at about a third of the rate mines make gold.
const manaFor = gold => Math.round(gold / 3 / 10) * 10;

function rewards(stage) {
  const chapter = 1 + Math.floor((stage - 1) / 5);
  return {
    chapter,
    chapterName: CHAPTERS[chapter],
    stage,
    xpPerWin: 30 + 25 * stage,
    firstClearCrystals: 50 + 10 * stage + (stage % 5 === 0 ? 150 : 0),
    firstClearItem: BOSS_ITEMS[stage] || '',
  };
}

/** A guard for slot i: Commons early, Commons and Elites in the middle, Elites late; the boss leads on chapter ends. */
function guardHero(stage, i) {
  if (i === 0 && BOSSES[stage]) return BOSSES[stage];
  const pick = list => list[(stage * 3 + i) % list.length];
  if (stage <= 6) return pick(COMMONS);
  if (stage <= 11) return i % 2 === 0 ? pick(ELITES) : pick(COMMONS);
  return pick(ELITES);
}

function buildStage(stage) {
  const half = Math.min(16, 9 + Math.floor(stage / 2));
  const cx = 25, cz = 25;
  const x1 = cx - half, x2 = cx + half, z1 = cz - half, z2 = cz + half;
  // Cells in use, with a one-cell gap kept round every building so attackers always have a path.
  const taken = new Set();
  const key = (x, z) => x * 100 + z;
  const buildings = [];

  function place(def, level, x, z) {
    const [w, d] = SIZE[def];
    if (x <= x1 || z <= z1 || x + w > x2 || z + d > z2) return false;
    for (let px = x - 1; px <= x + w; px++)
      for (let pz = z - 1; pz <= z + d; pz++)
        if (taken.has(key(px, pz))) return false;
    for (let px = x; px < x + w; px++)
      for (let pz = z; pz < z + d; pz++) taken.add(key(px, pz));
    buildings.push({ def, level, x, z });
    return true;
  }

  /** Tries spots in order until one fits. */
  function placeAt(def, level, spots) {
    for (const [x, z] of spots) if (place(def, level, x, z)) return true;
    return false;
  }

  const towerLevel = Math.min(15, Math.ceil((stage + 1) / 2));
  const structureLevel = Math.min(15, 1 + Math.floor(stage / 3));
  const wallLevel = Math.min(15, 1 + Math.floor(stage / 4));
  const guardLevel = Math.min(200, 8 + 9 * stage);

  place('dojo_hq', Math.min(15, 2 + Math.floor(stage / 3)), cx - 2, cz - 2);

  // Towers stand in a band just inside the walls, spread round the ring by angle.
  const band = [];
  for (let x = x1 + 2; x <= x2 - 4; x++)
    for (let z = z1 + 2; z <= z2 - 4; z++) {
      const edge = Math.min(x - x1, z - z1, x2 - (x + 2), z2 - (z + 2));
      if (edge >= 2 && edge <= 3) band.push([x, z, Math.atan2(z + 1 - cz, x + 1 - cx)]);
    }
  const arrows = Math.min(9, 2 + Math.floor(stage / 2));
  const magic = Math.min(5, Math.floor(stage / 3));
  const cannons = stage >= 4 ? Math.min(4, Math.floor((stage - 1) / 3)) : 0;
  const towerKinds = [];
  for (let i = 0; i < Math.max(arrows, magic, cannons); i++) {
    if (i < arrows) towerKinds.push('defense_tower');
    if (i < cannons) towerKinds.push('cannon_tower');
    if (i < magic) towerKinds.push('spirit_turret');
  }
  towerKinds.forEach((def, i) => {
    const angle = -Math.PI + (2 * Math.PI * (i + 0.5)) / towerKinds.length + stage * 0.37;
    const wrap = a => Math.atan2(Math.sin(a), Math.cos(a));
    const spots = band.slice().sort((a, b) => Math.abs(wrap(a[2] - angle)) - Math.abs(wrap(b[2] - angle))).map(s => [s[0], s[1]]);
    placeAt(def, towerLevel, spots);
  });

  // Inner spots, nearest the Town Hall first: guards' Hero Bases, then the economy buildings.
  const inner = [];
  for (let x = x1 + 2; x <= x2 - 3; x++)
    for (let z = z1 + 2; z <= z2 - 3; z++) inner.push([x, z, Math.hypot(x + 1 - cx, z + 1 - cz) + ((x * 7 + z * 13 + stage) % 5) * 0.15]);
  inner.sort((a, b) => a[2] - b[2]);
  const innerSpots = inner.map(s => [s[0], s[1]]);

  const guards = Math.min(5, 1 + Math.floor((stage - 1) / 3));
  const defenders = [];
  for (let i = 0; i < guards; i++) {
    const before = buildings.length;
    if (!placeAt('hero_base', structureLevel, innerSpots)) break;
    const base = buildings[before];
    defenders.push({ heroId: guardHero(stage, i), evolution: 0, level: guardLevel, x: base.x, z: base.z });
  }

  const economy = ['gold_vault', 'gold_mine', 'builder_hall', 'mana_mill', 'mana_vault', 'troop_camp', 'gold_mine', 'warehouse'];
  for (const def of economy.slice(0, 3 + Math.floor(stage / 4))) placeAt(def, structureLevel, innerSpots);

  const walls = [
    { level: wallLevel, x1, z1, x2, z2: z1 },
    { level: wallLevel, x1: x2, z1, x2, z2 },
    { level: wallLevel, x1: x2, z1: z2, x2: x1, z2 },
    { level: wallLevel, x1, z1: z2, x2: x1, z2: z1 },
  ];
  const [name, description] = STAGES[stage];
  const id = 'stage_' + String(stage).padStart(2, '0');
  return Object.assign(
    { id, displayName: name, description, version: 2, goldPerStar: 300 + 150 * stage, manaPerStar: manaFor(300 + 150 * stage) },
    rewards(stage),
    {
      requires: stage === 3 ? 'cinder_garrison' : 'stage_' + String(stage - 1).padStart(2, '0'),
      strength: STRENGTH[id] || 100,
      buildings,
      walls,
      defenders,
    });
}

const document = JSON.parse(fs.readFileSync(raidsPath, 'utf8'));
const handMade = document.raids.filter(r => !r.id.startsWith('stage_'));
handMade.forEach((raid, i) => {
  Object.assign(raid, rewards(i + 1));
  raid.requires = '';
  raid.strength = STRENGTH[raid.id] || 100;
  raid.manaPerStar = manaFor(raid.goldPerStar);
});
// Stage 2 was guarded by a Legendary, which only Legendary players could beat; a Common guards it now.
const garrison = handMade.find(r => r.id === 'cinder_garrison');
if (garrison) for (const d of garrison.defenders || []) d.heroId = 'ember_recruit';
document.raids = handMade.concat(Object.keys(STAGES).map(Number).sort((a, b) => a - b).map(buildStage));
fs.writeFileSync(raidsPath, JSON.stringify(document, null, 2) + '\n');
for (const r of document.raids)
  console.log(r.id.padEnd(16), 'strength', String(r.strength).padStart(3), 'towers', r.buildings.filter(b => /tower|turret/.test(b.def)).length,
    'guards', (r.defenders || []).length, 'buildings', r.buildings.length);
console.log('CAMPAIGN_DONE', document.raids.length);
