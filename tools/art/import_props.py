"""Imports the owner's painted scenery props into Resources/Props (run with Blender's Python).

  blender -b --factory-startup --python tools/art/import_props.py -- <SD_Props folder> <repo>

Only the painted props are taken; the pack's flat vector placeholders are left out. Each is cropped to its
silhouette, its magenta keying fringe is turned into the dark outline colour, and it is scaled to at most 384 px.
"""
import bpy, sys, os
import numpy as np

args = sys.argv[sys.argv.index('--') + 1:]
SRC, REPO = args[0], args[1]
OUT = os.path.join(REPO, 'unity/Assets/_Game/Resources/Props')
os.makedirs(OUT, exist_ok=True)

PAINTED = ['tree_blossom_1', 'bush_1', 'bush_2', 'rock_1', 'rock_2', 'rock_3', 'stone_lantern', 'fox_statue']


def load(path):
    img = bpy.data.images.load(path)
    w, h = img.size
    px = np.array(img.pixels[:], dtype=np.float32).reshape(h, w, 4)
    bpy.data.images.remove(img)
    return px


def save(px, path):
    h, w = px.shape[:2]
    img = bpy.data.images.new('out', w, h, alpha=True)
    img.pixels = np.clip(px, 0, 1).ravel().tolist()
    img.filepath_raw = path
    img.file_format = 'PNG'
    img.save()
    bpy.data.images.remove(img)


def defringe(px, reach=6):
    a = px[..., 3]
    near = a < 0.5
    for _ in range(reach):
        n = near.copy()
        n[1:] |= near[:-1]; n[:-1] |= near[1:]; n[:, 1:] |= near[:, :-1]; n[:, :-1] |= near[:, 1:]
        near = n
    r, g, b = px[..., 0], px[..., 1], px[..., 2]
    hit = (r > g + 0.1) & (b > g + 0.1) & (b > 0.12) & (near | (a < 0.98))
    px[hit, 0] = 0.11; px[hit, 1] = 0.06; px[hit, 2] = 0.03
    return px


def crop(px):
    a = px[..., 3] > 0.04
    rows, cols = np.where(a.any(1))[0], np.where(a.any(0))[0]
    return px[max(rows[0] - 2, 0):rows[-1] + 3, max(cols[0] - 2, 0):cols[-1] + 3]


def fit(px, size):
    h, w = px.shape[:2]
    s = size / max(w, h)
    if s >= 1:
        return px
    nw, nh = max(1, round(w * s)), max(1, round(h * s))
    pm = px.copy()
    pm[..., :3] *= pm[..., 3:4]
    ys = (np.arange(nh + 1) * h / nh).astype(int)
    xs = (np.arange(nw + 1) * w / nw).astype(int)
    c = np.cumsum(np.cumsum(np.pad(pm, ((1, 0), (1, 0), (0, 0))), 0), 1)
    y0, y1 = ys[:-1], np.maximum(ys[1:], ys[:-1] + 1)
    x0, x1 = xs[:-1], np.maximum(xs[1:], xs[:-1] + 1)
    s = c[y1][:, x1] - c[y0][:, x1] - c[y1][:, x0] + c[y0][:, x0]
    out = s / ((y1 - y0)[:, None] * (x1 - x0)[None, :])[..., None]
    a = out[..., 3:4]
    out[..., :3] = np.where(a > 1e-4, out[..., :3] / np.maximum(a, 1e-4), 0)
    return out


for name in PAINTED:
    px = fit(defringe(crop(load(os.path.join(SRC, name + '.png')))), 384)
    save(px, os.path.join(OUT, name + '.png'))
    print('PROP', name, px.shape[1], px.shape[0])
