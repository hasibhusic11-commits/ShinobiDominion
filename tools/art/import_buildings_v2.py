"""Imports the owner's second-round building art (ChatGPT, 1024 x 1024, framed to docs/art/tower_wall_prompts.md).

  blender -b --factory-startup --python tools/art/import_buildings_v2.py -- <folder> <repo> [--fill]

Each <id>_level_NN.png is cropped symmetrically around the footprint's front corner (the pivot), so the picture
stands on that corner, and its base diamond width is measured and written to Resources/Buildings/art_meta.json:
the game sizes the picture by its base, so a crossbow or lantern may stick out past the footprint.
--fill copies the nearest delivered level into missing levels, for checking a partial set in the game.
"""
import bpy, sys, os, re, glob, json
import numpy as np

args = sys.argv[sys.argv.index('--') + 1:]
SRC, REPO = args[0], args[1]
FILL = '--fill' in args
OUT = os.path.join(REPO, 'unity/Assets/_Game/Resources/Buildings')
META = os.path.join(OUT, 'art_meta.json')


def load(path):
    img = bpy.data.images.load(path)
    w, h = img.size
    px = np.array(img.pixels[:], dtype=np.float32).reshape(h, w, 4)[::-1]  # top row first
    bpy.data.images.remove(img)
    return px


def save(px, path):
    h, w = px.shape[:2]
    img = bpy.data.images.new('out', w, h, alpha=True)
    img.pixels = np.clip(px[::-1], 0, 1).ravel().tolist()
    img.filepath_raw = path
    img.file_format = 'PNG'
    img.save()
    bpy.data.images.remove(img)


meta = json.load(open(META)) if os.path.exists(META) else {}
found = {}
for path in sorted(glob.glob(os.path.join(SRC, '*_level_*.png'))):
    m = re.match(r'(.+)_level_(\d+)\.png$', os.path.basename(path))
    if not m:
        continue
    bid, level = m.group(1), int(m.group(2))
    px = load(path)
    alpha = px[..., 3] > 0.08
    rows = np.where(alpha.any(1))[0]
    cols = np.where(alpha.any(0))[0]
    h, w = px.shape[:2]
    # The front corner of the footprint is the lowest opaque point near the middle column.
    centre = w // 2
    pivot_y = int(rows.max())
    # The base is widest at its side corners, a quarter of its width above the front corner; search low rows only.
    spans = []
    for y in range(max(pivot_y - int(h * 0.3), 0), pivot_y + 1):
        xs = np.where(alpha[y])[0]
        if len(xs):
            spans.append((xs.max() - xs.min(), xs.min(), xs.max()))
    base = max(spans)
    base_width = int(base[0])
    pivot_x = int(round((base[1] + base[2]) / 2))
    half = max(pivot_x - cols.min(), cols.max() - pivot_x) + 4
    x0, x1 = max(pivot_x - half, 0), min(pivot_x + half, w)
    y0 = max(rows.min() - 4, 0)
    crop = px[y0:pivot_y + 1, x0:x1]
    name = '%s_%02d' % (bid, level)
    save(crop, os.path.join(OUT, name + '.png'))
    meta[name] = base_width
    found.setdefault(bid, []).append(level)
    print('IMPORTED', name, 'base', base_width, 'pivot', pivot_x, pivot_y, 'size', crop.shape[1], crop.shape[0])

if FILL:
    import shutil
    for bid, levels in found.items():
        for level in range(1, 11):
            if level in levels:
                continue
            nearest = min(levels, key=lambda l: (abs(l - level), l))
            src = os.path.join(OUT, '%s_%02d.png' % (bid, nearest))
            shutil.copyfile(src, os.path.join(OUT, '%s_%02d.png' % (bid, level)))
            meta['%s_%02d' % (bid, level)] = meta['%s_%02d' % (bid, nearest)]
            print('FILLED', bid, level, 'from', nearest)

json.dump(meta, open(META, 'w'), indent=1, sort_keys=True)
print('META', len(meta))
