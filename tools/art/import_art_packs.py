"""Imports the owner's two art packs into Unity Resources (run with Blender's Python, numpy is bundled).

  blender -b --factory-startup --python tools/art/import_art_packs.py -- <ui_pack> <building_pack> <banner_sheet> <repo>

UI pack (ShinobiDominion_UI_Buttons_Icons_Complete):
  - medallion buttons, five states each  -> Resources/UI/medal_<family>_<state>.png  (192 px, attack 256 px)
  - icons                                -> Resources/UI/icon_<name>.png             (128 px)
  - hud pieces                           -> Resources/UI/hud_<name>.png
  - badges                               -> Resources/UI/badge_<name>.png            (128 px)
  The pack's sprites carry a magenta fringe left from keying; edge pixels close to magenta are turned into the
  dark outline colour. Chakra is shown purple, as in the owner's mockup, so its drop is hue-shifted.
Banner sheet (owner's P3 sheet on green): crystal gem and three original clan banners -> Resources/UI/banner_*.png
Building pack: every level sprite cropped to its silhouette -> Resources/Buildings/<id>_NN.png. The wall's
  V-shaped piece is cut into its two arms (one per grid axis) and an end post, so walls join on the grid.
"""
import bpy, sys, os, glob, json
import numpy as np

args = sys.argv[sys.argv.index('--') + 1:]
UI_PACK, BUILD_PACK, BANNERS, REPO = args[:4]
UI_OUT = os.path.join(REPO, 'unity/Assets/_Game/Resources/UI')
B_OUT = os.path.join(REPO, 'unity/Assets/_Game/Resources/Buildings')
os.makedirs(B_OUT, exist_ok=True)


def load(path):
    img = bpy.data.images.load(path)
    w, h = img.size
    px = np.array(img.pixels[:], dtype=np.float32).reshape(h, w, 4)
    bpy.data.images.remove(img)
    return px  # bottom row first, as Blender stores it


def save(px, path):
    h, w = px.shape[:2]
    img = bpy.data.images.new('out', w, h, alpha=True)
    img.pixels = np.clip(px, 0, 1).ravel().tolist()
    img.filepath_raw = path
    img.file_format = 'PNG'
    img.save()
    bpy.data.images.remove(img)


def resize(px, w, h):
    """Box-filtered downscale with premultiplied alpha, so edges stay clean."""
    sh, sw = px.shape[:2]
    pm = px.copy()
    pm[..., :3] *= pm[..., 3:4]
    ys = (np.arange(h + 1) * sh / h).astype(int)
    xs = (np.arange(w + 1) * sw / w).astype(int)
    csum = np.cumsum(np.cumsum(np.pad(pm, ((1, 0), (1, 0), (0, 0))), 0), 1)
    y0, y1 = ys[:-1], np.maximum(ys[1:], ys[:-1] + 1)
    x0, x1 = xs[:-1], np.maximum(xs[1:], xs[:-1] + 1)
    s = (csum[y1][:, x1] - csum[y0][:, x1] - csum[y1][:, x0] + csum[y0][:, x0])
    area = ((y1 - y0)[:, None] * (x1 - x0)[None, :])[..., None]
    out = s / area
    a = out[..., 3:4]
    out[..., :3] = np.where(a > 1e-4, out[..., :3] / np.maximum(a, 1e-4), 0)
    return out


def fit(px, size):
    h, w = px.shape[:2]
    s = size / max(w, h)
    if s >= 1:
        return px
    return resize(px, max(1, round(w * s)), max(1, round(h * s)))


def defringe(px, reach=6):
    """Magenta keying residue on the rim becomes the dark outline colour."""
    a = px[..., 3]
    clear = a < 0.5
    near = clear.copy()
    for _ in range(reach):
        n = near.copy()
        n[1:] |= near[:-1]; n[:-1] |= near[1:]; n[:, 1:] |= near[:, :-1]; n[:, :-1] |= near[:, 1:]
        near = n
    r, g, b = px[..., 0], px[..., 1], px[..., 2]
    magenta = (r > g + 0.1) & (b > g + 0.1) & (b > 0.12)
    hit = magenta & (near | (a < 0.98))
    px[hit, 0] = 0.11; px[hit, 1] = 0.06; px[hit, 2] = 0.03
    return px


def crop(px, pad=2, thresh=0.04):
    a = px[..., 3] > thresh
    rows = np.where(a.any(1))[0]
    cols = np.where(a.any(0))[0]
    y0, y1 = max(rows[0] - pad, 0), min(rows[-1] + pad + 1, px.shape[0])
    x0, x1 = max(cols[0] - pad, 0), min(cols[-1] + pad + 1, px.shape[1])
    return px[y0:y1, x0:x1]


def hue_rotate(px, degrees):
    t = np.radians(degrees)
    c, s = np.cos(t), np.sin(t)
    m = np.array([
        [0.299 + 0.701 * c + 0.168 * s, 0.587 - 0.587 * c + 0.330 * s, 0.114 - 0.114 * c - 0.497 * s],
        [0.299 - 0.299 * c - 0.328 * s, 0.587 + 0.413 * c + 0.035 * s, 0.114 - 0.114 * c + 0.292 * s],
        [0.299 - 0.300 * c + 1.250 * s, 0.587 - 0.588 * c - 1.050 * s, 0.114 + 0.886 * c - 0.203 * s]])
    px[..., :3] = np.clip(px[..., :3] @ m.T, 0, 1)
    return px


def largest_shape(px):
    """Keeps the shape under the column with the most ink, dropping slivers of neighbouring art."""
    solid = px[..., 3] > 0.2
    col = int(np.argmax(solid.sum(0)))
    row = int(np.where(solid[:, col])[0].mean())
    rows = np.where(solid[:, col])[0]
    row = int(rows[np.argmin(np.abs(rows - row))])
    keep = np.zeros_like(solid)
    keep[row, col] = True
    while True:
        grown = keep.copy()
        grown[1:] |= keep[:-1]; grown[:-1] |= keep[1:]; grown[:, 1:] |= keep[:, :-1]; grown[:, :-1] |= keep[:, 1:]
        grown &= solid
        if grown.sum() == keep.sum():
            break
        keep = grown
    # Soft edge pixels next to the kept shape stay.
    halo = keep.copy()
    halo[1:] |= keep[:-1]; halo[:-1] |= keep[1:]; halo[:, 1:] |= keep[:, :-1]; halo[:, :-1] |= keep[:, 1:]
    px[~halo, 3] = 0
    return px


def shear(px, k, from_right):
    """Lifts each column by k px per px of distance from the junction edge (rows are bottom-first)."""
    h, w = px.shape[:2]
    lift = int(np.ceil(k * w)) + 1
    out = np.zeros((h + lift, w, 4), dtype=np.float32)
    for x in range(w):
        d = (w - 1 - x) if from_right else x
        s = d * k
        i, f = int(s), s - int(s)
        out[i:i + h, x] += px[:, x] * (1 - f)
        out[i + 1:i + 1 + h, x] += px[:, x] * f
    return out


count = 0
# Medallion buttons.
for family_dir in sorted(glob.glob(os.path.join(UI_PACK, 'buttons/medallions/*'))):
    family = os.path.basename(family_dir)
    for state in ('normal', 'hover', 'pressed', 'disabled', 'selected'):
        src = os.path.join(family_dir, '%s_%s.png' % (family, state))
        px = fit(defringe(crop(load(src))), 256 if family == 'attack' else 192)
        save(px, os.path.join(UI_OUT, 'medal_%s_%s.png' % (family, state)))
        count += 1

# Icons. icon_chakra turns purple to match the mockup's third currency.
for src in sorted(glob.glob(os.path.join(UI_PACK, 'icons/*/icon_*.png'))):
    name = os.path.basename(src)
    px = defringe(crop(load(src)))
    if name == 'icon_chakra.png':
        px = hue_rotate(px, -68)
    save(fit(px, 128), os.path.join(UI_OUT, name))
    count += 1

# HUD pieces and badges.
for src in sorted(glob.glob(os.path.join(UI_PACK, 'hud/*.png'))):
    px = defringe(crop(load(src)))
    if os.path.basename(src) == 'resource_bar.png':
        # The bar carries a small pointer tick under it; rows that thin (bottom-first) are dropped.
        width = px.shape[1]
        solid = (px[..., 3] > 0.3).sum(1)
        first = int(np.argmax(solid > width * 0.3))
        px = px[first:]
    save(fit(px, 400), os.path.join(UI_OUT, 'hud_' + os.path.basename(src)))
    count += 1
for src in sorted(glob.glob(os.path.join(UI_PACK, 'badges/badge_*.png'))):
    save(fit(defringe(crop(load(src))), 128), os.path.join(UI_OUT, os.path.basename(src)))
    count += 1

# The owner's banner sheet on green: gem, red tomoe, navy pagoda, ivory blossom.
sheet = load(BANNERS)
r, g, b = sheet[..., 0], sheet[..., 1], sheet[..., 2]
green = (g > 0.55) & (r < 0.45) & (b < 0.45) & (g > r + 0.25) & (g > b + 0.25)
sheet[green, 3] = 0
spill = (g > np.maximum(r, b)) & ~green
sheet[spill, 1] = np.maximum(r, b)[spill]
w = sheet.shape[1]
for name, (x0, x1) in {'gem_crystal': (0, 0.215), 'banner_tomoe': (0.195, 0.47), 'banner_pagoda': (0.47, 0.725),
                       'banner_blossom': (0.725, 1.0)}.items():
    part = crop(largest_shape(sheet[:, int(x0 * w):int(x1 * w)].copy()))
    save(fit(part, 256), os.path.join(UI_OUT, name + '.png'))
    count += 1

# Buildings.
metrics = {}
for family_dir in sorted(glob.glob(os.path.join(BUILD_PACK, 'Sprites/*'))):
    bid = os.path.basename(family_dir)
    for level in range(1, 11):
        px = load(os.path.join(family_dir, '%s_level_%02d.png' % (bid, level)))
        if bid != 'wall':
            save(crop(px), os.path.join(B_OUT, '%s_%02d.png' % (bid, level)))
            count += 1
            continue
        px = crop(px, pad=0)
        save(px, os.path.join(B_OUT, "wall_%02d.png" % level))  # whole V, for the catalog card
        h, w = px.shape[:2]
        alpha = px[..., 3] > 0.3
        # Rows are bottom-first: the highest opaque row of each column marks the posts, the lowest the junction.
        top = np.array([np.where(alpha[:, x])[0].max() if alpha[:, x].any() else -1 for x in range(w)])
        bottom = np.array([np.where(alpha[:, x])[0].min() if alpha[:, x].any() else h for x in range(w)])
        low = np.where(bottom <= bottom.min() + 1)[0]
        cx = int(round(low.mean()))
        left_peak = np.where(top[:cx] >= top[:cx].max() - 2)[0]
        right_peak = np.where(top[cx:] >= top[cx:].max() - 2)[0] + cx
        post_l, post_r = int(round(left_peak.mean())), int(round(right_peak.mean()))
        post_half = 44
        # The painted V is flatter than the game's true isometric grid (a run rises 0.577 px per px across).
        # Shearing each arm's columns up keeps verticals vertical and lands the end post on the next cell.
        span = cx - post_l
        rise = bottom[post_l] - bottom[cx]
        k = max((0.5774 * span - rise) / span, 0.0)
        save(shear(px[:, :cx + 1], k, from_right=True), os.path.join(B_OUT, 'wall_arm_z_%02d.png' % level))
        save(shear(px[:, cx:], k, from_right=False), os.path.join(B_OUT, 'wall_arm_x_%02d.png' % level))
        post = px[:, max(post_l - post_half, 0):post_l + post_half]
        save(crop(post, pad=0), os.path.join(B_OUT, 'wall_post_%02d.png' % level))
        metrics[level + 100] = dict(span=int(span), shear=round(float(k), 3))
        metrics[level] = dict(width=w, height=h, junction=cx, post_left=post_l, post_right=post_r,
                              post_bottom_left=int(bottom[post_l]))
        count += 3
print('METRICS', json.dumps(metrics))
print('IMPORTED', count)
