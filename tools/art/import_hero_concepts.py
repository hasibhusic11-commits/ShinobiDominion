"""Imports the owner's transparent hero paintings as the game's hero concept art (cards, altar, summon reveal).

  blender -b --factory-startup --python tools/art/import_hero_concepts.py -- <repo> <heroId>=<image> [...]

Each painting is halved with a premultiplied 2x2 box filter (clean edges, sharp on a phone) and written to
Resources/HeroConcepts/<heroId>.png. HeroConcepts crops cards from the top of the picture at any size.
"""
import bpy, sys, os
import numpy as np

args = sys.argv[sys.argv.index('--') + 1:]
OUT = os.path.join(args[0], 'unity/Assets/_Game/Resources/HeroConcepts')
for pair in args[1:]:
    hero, src = pair.split('=', 1)
    img = bpy.data.images.load(src)
    w, h = img.size
    px = np.array(img.pixels[:], dtype=np.float32).reshape(h, w, 4)
    bpy.data.images.remove(img)
    px = px[:h - h % 2, :w - w % 2]
    pm = px.copy()
    pm[..., :3] *= pm[..., 3:4]
    half = (pm[0::2, 0::2] + pm[1::2, 0::2] + pm[0::2, 1::2] + pm[1::2, 1::2]) / 4
    a = half[..., 3:4]
    half[..., :3] = np.where(a > 1e-4, half[..., :3] / np.maximum(a, 1e-4), 0)
    oh, ow = half.shape[:2]
    o = bpy.data.images.new('o', ow, oh, alpha=True)
    o.pixels = np.clip(half, 0, 1).ravel().tolist()
    o.filepath_raw = os.path.join(OUT, hero + '.png')
    o.file_format = 'PNG'
    o.save()
    print('HERO', hero, ow, oh)
