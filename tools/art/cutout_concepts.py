"""Cuts the plain grey background out of each hero concept painting, so the hero can stand on any scene.

Run through Blender, which brings Python and numpy:
    blender -b --factory-startup --python tools/art/cutout_concepts.py -- <source folder> <target folder>
Source files are <heroId>_base.png. The background is found by flooding inward from the edges across
pixels close to the edge colour, so grey clothing inside the outline is kept.
"""
import os
import sys

import bpy
import numpy as np

TOLERANCE = 0.075  # How far from the edge colour still counts as background (0 to 1 per channel).
SCALE = 0.5        # Half size keeps faces sharp on a phone and the files small.


def cut(path, target):
    image = bpy.data.images.load(path)
    width, height = image.size
    image.scale(int(width * SCALE), int(height * SCALE))
    width, height = image.size
    pixels = np.array(image.pixels[:], dtype=np.float32).reshape(height, width, 4)

    rgb = pixels[:, :, :3]
    border = np.concatenate([rgb[0], rgb[-1], rgb[:, 0], rgb[:, -1]])
    background = np.median(border, axis=0)
    # Soft shading on the backdrop drifts a little, so compare against a slightly blurred local colour too.
    close = np.max(np.abs(rgb - background), axis=2) < TOLERANCE

    region = np.zeros_like(close)
    region[0, :] = close[0, :]
    region[-1, :] = close[-1, :]
    region[:, 0] = close[:, 0]
    region[:, -1] = close[:, -1]
    while True:
        grown = region.copy()
        grown[1:, :] |= region[:-1, :]
        grown[:-1, :] |= region[1:, :]
        grown[:, 1:] |= region[:, :-1]
        grown[:, :-1] |= region[:, 1:]
        grown &= close
        if np.array_equal(grown, region):
            break
        region = grown

    # The painted floor shadow is grey and joins the background near the feet. Flood into it from the
    # background through colourless, fairly light pixels, only in the bottom of the picture so grey
    # clothing higher up is never touched, and turn it into a soft dark shadow instead.
    lum = rgb.mean(axis=2)
    colourless = (rgb.max(axis=2) - rgb.min(axis=2)) < 0.05
    floor = np.zeros_like(close)
    floor[: int(height * 0.16), :] = True  # Blender rows start at the bottom of the picture.
    shadowy = colourless & floor & (lum > 0.38) & ~region
    shadow = region.copy()
    while True:
        grown = shadow.copy()
        grown[1:, :] |= shadow[:-1, :]
        grown[:-1, :] |= shadow[1:, :]
        grown[:, 1:] |= shadow[:, :-1]
        grown[:, :-1] |= shadow[:, 1:]
        grown &= (shadowy | region)
        if np.array_equal(grown, shadow):
            break
        shadow = grown
    shadow &= ~region
    # Background enclosed between the legs is reached only through the shadow, so flood once more.
    while True:
        grown = region | shadow
        spread = grown.copy()
        spread[1:, :] |= grown[:-1, :]
        spread[:-1, :] |= grown[1:, :]
        spread[:, 1:] |= grown[:, :-1]
        spread[:, :-1] |= grown[:, 1:]
        added = spread & close & ~grown
        if not added.any():
            break
        region |= added
    background_lum = float(background.mean())
    shadow_alpha = np.clip((background_lum - lum) * 2.2, 0.0, 0.55)
    rgb[shadow] = 0.0

    alpha = np.where(region, 0.0, 1.0).astype(np.float32)
    alpha[shadow] = shadow_alpha[shadow]
    # A one-pixel soft edge so the outline does not look cut with scissors.
    soft = alpha.copy()
    soft[1:-1, 1:-1] = (alpha[1:-1, 1:-1] * 4 + alpha[:-2, 1:-1] + alpha[2:, 1:-1] + alpha[1:-1, :-2] + alpha[1:-1, 2:]) / 8.0
    pixels[:, :, 3] = soft
    # The paintings load without an alpha channel, so write into a new image that has one.
    out = bpy.data.images.new(os.path.basename(target), width, height, alpha=True)
    out.pixels[:] = pixels.ravel()
    out.filepath_raw = target
    out.file_format = 'PNG'
    out.save()
    kept = float(np.mean(alpha))
    print('CUT', os.path.basename(target), width, height, 'background', np.round(background, 3), 'kept %.2f' % kept)


def main(source, target):
    os.makedirs(target, exist_ok=True)
    for name in sorted(os.listdir(source)):
        if name.endswith('_base.png'):
            cut(os.path.join(source, name), os.path.join(target, name[:-len('_base.png')] + '.png'))
    print('CUTOUT_DONE')


if __name__ == '__main__':
    args = sys.argv[sys.argv.index('--') + 1:]
    main(args[0], args[1])
