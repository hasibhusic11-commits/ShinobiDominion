"""Removes a flat white (or near-white) background from owner art that came without transparency.

  blender -b --factory-startup --python tools/art/key_white.py -- <out_folder> <name>=<image> [<name>=<image> ...]

Only white connected to the image border is removed (a flood fill), so white highlights inside the art stay.
The edge is softened by how close each border pixel is to white, and the white fringe is taken back out of the
colour, so the cut-out has no pale halo over the green village.
"""
import bpy, sys, os
import numpy as np

args = sys.argv[sys.argv.index('--') + 1:]
OUT = args[0]
os.makedirs(OUT, exist_ok=True)

for pair in args[1:]:
    name, src = pair.split('=', 1)
    img = bpy.data.images.load(src)
    w, h = img.size
    px = np.array(img.pixels[:], dtype=np.float32).reshape(h, w, 4)
    bpy.data.images.remove(img)
    rgb = px[..., :3]
    lo, hi = rgb.min(-1), rgb.max(-1)
    whiteish = (lo > 0.86) & (hi - lo < 0.08)
    # Flood fill from the border through white-ish pixels.
    seed = np.zeros_like(whiteish)
    seed[0, :] = whiteish[0, :]; seed[-1, :] = whiteish[-1, :]; seed[:, 0] = whiteish[:, 0]; seed[:, -1] = whiteish[:, -1]
    bg = seed
    while True:
        grown = bg.copy()
        grown[1:] |= bg[:-1]; grown[:-1] |= bg[1:]; grown[:, 1:] |= bg[:, :-1]; grown[:, :-1] |= bg[:, 1:]
        grown &= whiteish
        if grown.sum() == bg.sum():
            break
        bg = grown
    alpha = np.where(bg, 0.0, px[..., 3]).astype(np.float32)
    # Soft edge: pixels next to the background fade by how white they are.
    near = bg.copy()
    near[1:] |= bg[:-1]; near[:-1] |= bg[1:]; near[:, 1:] |= bg[:, :-1]; near[:, :-1] |= bg[:, 1:]
    edge = near & ~bg
    soft = np.clip((1.0 - lo) / 0.25, 0.0, 1.0)
    alpha = np.where(edge, np.minimum(alpha, soft), alpha)
    # Take the white back out of the edge colour: c = a * colour + (1 - a) * white.
    a = alpha[..., None]
    colour = np.where(a > 0.02, (rgb - (1.0 - a)) / np.maximum(a, 0.02), 0.0)
    out = np.concatenate([np.clip(colour, 0, 1), a], -1)
    o = bpy.data.images.new('o', w, h, alpha=True)
    o.pixels = out.ravel().tolist()
    o.filepath_raw = os.path.join(OUT, name + '.png')
    o.file_format = 'PNG'
    o.save()
    print('KEYED', name, w, h, 'background %.0f%%' % (100.0 * bg.mean()))
