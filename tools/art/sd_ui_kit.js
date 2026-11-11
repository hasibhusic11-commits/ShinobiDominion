// Shinobi Dominion UI kit v3: the shared frames, ribbons and button plates of the overhaul (docs/ui/overhaul).
// Every piece is ORIGINAL and drawn here as vector art (SVG), then rendered to PNG at 2x the size it is shown at,
// with transparent edges and a single light from the upper left. Unity slices them (UiSpriteImporter reads the
// "_s<border>" suffix as the 9-slice border, in source pixels), so the engraved corners never stretch.
//
// Usage (needs the npm package "sharp"):  node tools/art/sd_ui_kit.js <output folder>
// Output goes to unity/Assets/_Game/Resources/UI/ when that folder is passed.
const fs = require('fs');
const path = require('path');
const sharp = require('sharp');

const out = process.argv[2];
if (!out) throw new Error('usage: node sd_ui_kit.js <output folder>');
fs.mkdirSync(out, { recursive: true });

// Tokens: the approved docs/ui/UI_TOKENS.md values, refined by docs/ui/overhaul/02_UI_DESIGN_SYSTEM.md.
const T = {
  ink: '#0B0B12', lacquerDeep: '#0E1527', lacquer: '#1A2741', lacquerHigh: '#314363',
  brassShadow: '#5E4318', brass: '#B08A47', brassMid: '#D2AE66', brassHigh: '#F2D28A',
  crimsonDark: '#4A110E', crimson: '#92332F', crimsonHigh: '#D35743',
  goldTop: '#F7DC93', goldBottom: '#8A5E22', navyTop: '#33507F', navyBottom: '#122038',
  slateTop: '#6A707D', slateBottom: '#2A2D35', parchment: '#E9D4A7', parchmentDark: '#C9AE78',
};

async function save(name, width, height, body) {
  const svg = `<svg xmlns="http://www.w3.org/2000/svg" width="${width}" height="${height}" viewBox="0 0 ${width} ${height}">${body}</svg>`;
  const file = path.join(out, name + '.png');
  await sharp(Buffer.from(svg)).png().toFile(file);
  console.log('wrote', name, width + 'x' + height);
}

// A brass bar's gradient: bright along the top-left edge, dark in its recess, lit from the upper left.
const brassGradient = (id, vertical = true) => `
  <linearGradient id="${id}" x1="0" y1="0" x2="${vertical ? 0 : 1}" y2="${vertical ? 1 : 0}">
    <stop offset="0" stop-color="${T.brassHigh}"/><stop offset="0.35" stop-color="${T.brassMid}"/>
    <stop offset="0.7" stop-color="${T.brass}"/><stop offset="1" stop-color="${T.brassShadow}"/>
  </linearGradient>`;

const rivet = (x, y, r) => `
  <circle cx="${x}" cy="${y + 1.5}" r="${r}" fill="${T.ink}" opacity="0.55"/>
  <circle cx="${x}" cy="${y}" r="${r}" fill="url(#rivet)"/>
  <circle cx="${x - r * 0.3}" cy="${y - r * 0.35}" r="${r * 0.35}" fill="#FFF3C8" opacity="0.85"/>`;

const rivetDef = `<radialGradient id="rivet" cx="0.35" cy="0.3" r="0.8">
  <stop offset="0" stop-color="${T.brassHigh}"/><stop offset="0.6" stop-color="${T.brass}"/><stop offset="1" stop-color="${T.brassShadow}"/></radialGradient>`;

// ---------- Window: brass rim, angular corner fittings with rivets, inset bevel, lacquer face ----------
// 256 x 256, slice border 88 (source px) = 44 on screen at 1x.
async function windowFrame() {
  // 256 x 256, slice border 96. A heavy brass band (34 px) with an engraved groove, a deep recess, the lacquer face,
  // and angular corner fittings with two rivets. Shown at 1.5x down, so the band is about 23 units on screen.
  const S = 256, B = 96;
  const fitting = `
    <path d="M4 4 H92 L72 24 H24 V72 L4 92 Z" fill="url(#brassV)" stroke="${T.ink}" stroke-width="3.5" stroke-linejoin="round"/>
    <path d="M12 12 H76 L64 24 H24 V64 L12 76 Z" fill="none" stroke="${T.brassHigh}" stroke-width="2" opacity="0.85"/>
    <path d="M30 30 L52 30 L30 52 Z" fill="${T.crimson}" stroke="${T.ink}" stroke-width="2.5"/>
    <path d="M33 33 L45 33 L33 45 Z" fill="${T.crimsonHigh}" opacity="0.7"/>
    ${rivet(66, 14, 5)}${rivet(14, 66, 5)}`;
  const corner = (sx, sy) => `<g transform="translate(${sx === 1 ? 0 : S} ${sy === 1 ? 0 : S}) scale(${sx} ${sy})">${fitting}</g>`;
  await save("sd_frame_window_s96", S, S, `
    <defs>${brassGradient("brassV")}${rivetDef}
      <linearGradient id="face" x1="0" y1="0" x2="0" y2="1"><stop offset="0" stop-color="${T.lacquer}"/><stop offset="1" stop-color="${T.lacquerDeep}"/></linearGradient>
      <linearGradient id="rimShade" x1="0" y1="0" x2="1" y2="1"><stop offset="0" stop-color="#FFFFFF" stop-opacity="0.25"/><stop offset="0.5" stop-color="#FFFFFF" stop-opacity="0"/><stop offset="1" stop-color="#000000" stop-opacity="0.4"/></linearGradient>
    </defs>
    <rect x="2" y="2" width="${S - 4}" height="${S - 4}" rx="16" fill="${T.ink}"/>
    <rect x="6" y="6" width="${S - 12}" height="${S - 12}" rx="13" fill="url(#brassV)"/>
    <rect x="6" y="6" width="${S - 12}" height="${S - 12}" rx="13" fill="url(#rimShade)"/>
    <rect x="9" y="9" width="${S - 18}" height="${S - 18}" rx="11" fill="none" stroke="${T.brassHigh}" stroke-width="2" opacity="0.75"/>
    <rect x="22" y="22" width="${S - 44}" height="${S - 44}" rx="8" fill="none" stroke="${T.brassShadow}" stroke-width="2.5" opacity="0.9"/>
    <rect x="23.5" y="23.5" width="${S - 47}" height="${S - 47}" rx="7" fill="none" stroke="${T.brassHigh}" stroke-width="1" opacity="0.5"/>
    <rect x="40" y="40" width="${S - 80}" height="${S - 80}" rx="6" fill="${T.brassShadow}"/>
    <rect x="43" y="43" width="${S - 86}" height="${S - 86}" rx="5" fill="${T.ink}"/>
    <rect x="46" y="46" width="${S - 92}" height="${S - 92}" rx="4" fill="url(#face)"/>
    <rect x="46" y="46" width="${S - 92}" height="12" fill="#000" opacity="0.4"/>
    <rect x="46" y="46" width="10" height="${S - 92}" fill="#000" opacity="0.25"/>
    ${corner(1, 1)}${corner(-1, 1)}${corner(1, -1)}${corner(-1, -1)}`);
  return B;
}

// ---------- Card frame: brass with an open picture window (transparent centre) and an inner shadow ----------
// 192 x 256, slice border 48. Placed OVER the illustration, so its centre must stay empty.
async function cardFrame() {
  const W = 192, H = 256;
  await save('sd_frame_card_s48', W, H, `
    <defs>${brassGradient('brassV')}${rivetDef}
      <linearGradient id="rimShade" x1="0" y1="0" x2="1" y2="1"><stop offset="0" stop-color="#FFF" stop-opacity="0.25"/><stop offset="0.5" stop-color="#FFF" stop-opacity="0"/><stop offset="1" stop-color="#000" stop-opacity="0.35"/></linearGradient>
      <mask id="hole"><rect width="${W}" height="${H}" fill="white"/><rect x="22" y="22" width="${W - 44}" height="${H - 44}" rx="4" fill="black"/></mask>
      <linearGradient id="inShadowTop" x1="0" y1="0" x2="0" y2="1"><stop offset="0" stop-color="#000" stop-opacity="0.6"/><stop offset="1" stop-color="#000" stop-opacity="0"/></linearGradient>
      <linearGradient id="inShadowLeft" x1="0" y1="0" x2="1" y2="0"><stop offset="0" stop-color="#000" stop-opacity="0.45"/><stop offset="1" stop-color="#000" stop-opacity="0"/></linearGradient>
    </defs>
    <g mask="url(#hole)">
      <rect x="2" y="2" width="${W - 4}" height="${H - 4}" rx="12" fill="${T.ink}"/>
      <rect x="5" y="5" width="${W - 10}" height="${H - 10}" rx="10" fill="url(#brassV)"/>
      <rect x="5" y="5" width="${W - 10}" height="${H - 10}" rx="10" fill="url(#rimShade)"/>
      <rect x="8" y="8" width="${W - 16}" height="${H - 16}" rx="8" fill="none" stroke="${T.brassHigh}" stroke-width="1.4" opacity="0.75"/>
      <rect x="18" y="18" width="${W - 36}" height="${H - 36}" rx="5" fill="${T.ink}"/>
    </g>
    <rect x="22" y="22" width="${W - 44}" height="14" fill="url(#inShadowTop)"/>
    <rect x="22" y="22" width="10" height="${H - 44}" fill="url(#inShadowLeft)"/>
    ${rivet(13, 13, 3.6)}${rivet(W - 13, 13, 3.6)}${rivet(13, H - 13, 3.6)}${rivet(W - 13, H - 13, 3.6)}`);
}

// ---------- Title ribbon: crimson satin plaque with original angular swallow-cut ends and brass edging ----------
// 512 x 112, slice border 120 left/right (the ends), 40 top/bottom.
async function ribbon() {
  const W = 512, H = 112;
  const body = `M64 14 H${W - 64} L${W - 26} 30 L${W - 6} 56 L${W - 26} 82 L${W - 64} 98 H64 L26 82 L6 56 L26 30 Z`;
  await save('sd_ribbon_title_s80x40', W, H, `
    <defs>${brassGradient('brassV')}
      <linearGradient id="satin" x1="0" y1="0" x2="0" y2="1"><stop offset="0" stop-color="${T.crimsonHigh}"/><stop offset="0.45" stop-color="${T.crimson}"/><stop offset="1" stop-color="${T.crimsonDark}"/></linearGradient>
      <pattern id="weave" width="6" height="6" patternUnits="userSpaceOnUse"><path d="M0 6 L6 0" stroke="#000" stroke-width="1" opacity="0.12"/></pattern>
    </defs>
    <path d="${body}" fill="${T.ink}" transform="translate(0 3)" opacity="0.6"/>
    <path d="${body}" fill="${T.ink}"/>
    <path d="M66 18 H${W - 66} L${W - 30} 33 L${W - 12} 56 L${W - 30} 79 L${W - 66} 94 H66 L30 79 L12 56 L30 33 Z" fill="url(#brassV)"/>
    <path d="M72 25 H${W - 72} L${W - 38} 38 L${W - 22} 56 L${W - 38} 74 L${W - 72} 87 H72 L38 74 L22 56 L38 38 Z" fill="url(#satin)"/>
    <path d="M72 25 H${W - 72} L${W - 38} 38 L${W - 22} 56 L${W - 38} 74 L${W - 72} 87 H72 L38 74 L22 56 L38 38 Z" fill="url(#weave)"/>
    <path d="M72 30 H${W - 72}" stroke="#FFFFFF" stroke-width="2" opacity="0.25"/>
    <path d="M72 82 H${W - 72}" stroke="#000" stroke-width="2" opacity="0.35"/>
    <path d="M44 56 l10 -10 l10 10 l-10 10 Z" fill="url(#brassV)" stroke="${T.ink}" stroke-width="2"/>
    <path d="M${W - 64} 56 l10 -10 l10 10 l-10 10 Z" fill="url(#brassV)" stroke="${T.ink}" stroke-width="2"/>`);
}

// ---------- Button plates: bevelled face with a gloss band and a darker lip, on a brass rim ----------
// 192 x 96, slice border 40. One per family.
async function button(name, top, bottom, rimLight = T.brassHigh) {
  const W = 192, H = 96;
  await save(name, W, H, `
    <defs>${brassGradient('brassV')}
      <linearGradient id="face" x1="0" y1="0" x2="0" y2="1"><stop offset="0" stop-color="${top}"/><stop offset="1" stop-color="${bottom}"/></linearGradient>
      <linearGradient id="gloss" x1="0" y1="0" x2="0" y2="1"><stop offset="0" stop-color="#FFF" stop-opacity="0.38"/><stop offset="1" stop-color="#FFF" stop-opacity="0.04"/></linearGradient>
    </defs>
    <rect x="3" y="7" width="${W - 6}" height="${H - 8}" rx="18" fill="${T.ink}" opacity="0.55"/>
    <rect x="2" y="2" width="${W - 4}" height="${H - 8}" rx="18" fill="${T.ink}"/>
    <rect x="5" y="5" width="${W - 10}" height="${H - 14}" rx="15" fill="url(#brassV)"/>
    <rect x="7" y="7" width="${W - 14}" height="${H - 18}" rx="13" fill="none" stroke="${rimLight}" stroke-width="1.4" opacity="0.7"/>
    <rect x="12" y="12" width="${W - 24}" height="${H - 28}" rx="10" fill="${T.ink}"/>
    <rect x="14" y="14" width="${W - 28}" height="${H - 32}" rx="9" fill="url(#face)"/>
    <rect x="16" y="16" width="${W - 32}" height="${(H - 32) * 0.42}" rx="8" fill="url(#gloss)"/>
    <rect x="14" y="${H - 26}" width="${W - 28}" height="8" rx="4" fill="#000" opacity="0.28"/>`);
}

// ---------- Dark text plate: lacquer well with a thin brass line, for type over illustrations ----------
// 128 x 64, slice border 24.
async function plate() {
  const W = 128, H = 64;
  await save('sd_plate_dark_s24', W, H, `
    <defs><linearGradient id="face" x1="0" y1="0" x2="0" y2="1"><stop offset="0" stop-color="#121A2E" stop-opacity="0.94"/><stop offset="1" stop-color="#070A14" stop-opacity="0.96"/></linearGradient></defs>
    <rect x="1" y="1" width="${W - 2}" height="${H - 2}" rx="10" fill="${T.ink}" opacity="0.85"/>
    <rect x="3" y="3" width="${W - 6}" height="${H - 6}" rx="8" fill="url(#face)"/>
    <rect x="5" y="5" width="${W - 10}" height="${H - 10}" rx="7" fill="none" stroke="${T.brass}" stroke-width="1.5" opacity="0.8"/>`);
}

// ---------- Progress badge: small brass-rimmed pill for one live fact (stars, rank, floor) ----------
async function badge() {
  const W = 128, H = 56;
  await save('sd_badge_s26', W, H, `
    <defs>${brassGradient('brassV')}<linearGradient id="face" x1="0" y1="0" x2="0" y2="1"><stop offset="0" stop-color="${T.crimson}"/><stop offset="1" stop-color="${T.crimsonDark}"/></linearGradient></defs>
    <rect x="2" y="4" width="${W - 4}" height="${H - 6}" rx="${(H - 6) / 2}" fill="${T.ink}" opacity="0.6"/>
    <rect x="2" y="2" width="${W - 4}" height="${H - 6}" rx="${(H - 6) / 2}" fill="${T.ink}"/>
    <rect x="4" y="4" width="${W - 8}" height="${H - 10}" rx="${(H - 10) / 2}" fill="url(#brassV)"/>
    <rect x="9" y="9" width="${W - 18}" height="${H - 20}" rx="${(H - 20) / 2}" fill="url(#face)"/>
    <rect x="12" y="11" width="${W - 24}" height="${(H - 20) * 0.4}" rx="${(H - 20) * 0.2}" fill="#FFF" opacity="0.18"/>`);
}

// ---------- Lacquer backing with a faint original wave-and-diamond lattice, tiled behind window content ----------
async function pattern() {
  const S = 128;
  await save('sd_pattern_lacquer', S, S, `
    <rect width="${S}" height="${S}" fill="${T.lacquer}"/>
    <g stroke="#3A5480" stroke-width="1.3" fill="none" opacity="0.32">
      <path d="M0 32 Q16 20 32 32 T64 32 T96 32 T128 32"/><path d="M0 96 Q16 84 32 96 T64 96 T96 96 T128 96"/>
      <path d="M64 48 l16 16 l-16 16 l-16 -16 Z"/><path d="M0 112 l16 16 M128 112 l-16 16 M0 16 l16 -16 M128 16 l-16 -16"/>
    </g>`);
}

(async () => {
  await windowFrame();
  await cardFrame();
  await ribbon();
  await button('sd_btn_war_s40', '#C4483B', '#5A1714');
  await button('sd_btn_gold_s40', T.goldTop, T.goldBottom);
  await button('sd_btn_navy_s40', T.navyTop, T.navyBottom);
  await button('sd_btn_slate_s40', T.slateTop, T.slateBottom, '#9A9AA2');
  await plate();
  await badge();
  await pattern();
})();
