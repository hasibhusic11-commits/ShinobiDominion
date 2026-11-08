"""Cuts a hero painting out of a soft grey backdrop that has a gradient and a floor shadow.

  blender -b --factory-startup --python tools/art/cutout_heroes_v2.py -- <source folder> <target folder>

Source files are <heroId>_base.png; output is <heroId>.png at half size, transparent. Better than
cutout_concepts.py for backdrops that are not one flat colour:
1. The background grows in from the edges in small steps (each pixel joins only if it is low in colour and
   close to its background neighbour), so it follows gradients and the floor shadow.
2. A smooth background colour is estimated everywhere from the found background.
3. Near the figure's edge, each pixel's opacity is how far it is from that background colour, and the
   background is unmixed from its colour, so glows and wisps become see-through instead of keeping grey.
"""
import bpy, sys, os, glob
import numpy as np

args = sys.argv[sys.argv.index('--') + 1:]
SRC, OUT = args[0], args[1]
os.makedirs(OUT, exist_ok=True)
SCALE = 0.5
STEP = 0.035     # how much the backdrop may change between neighbouring pixels
CHROMA = 0.10    # how colourful a backdrop pixel may be (the grey has a slight pink tint)
BAND = 26        # how far from the backdrop (in pixels, after scaling) edges may turn see-through


def shifted(a, dy, dx):
    out = np.zeros_like(a)
    h, w = a.shape[:2]
    ys, yd = (slice(dy, h), slice(0, h - dy)) if dy >= 0 else (slice(0, h + dy), slice(-dy, h))
    xs, xd = (slice(dx, w), slice(0, w - dx)) if dx >= 0 else (slice(0, w + dx), slice(-dx, w))
    out[ys, xs] = a[yd, xd]
    return out


def box_blur(a, r):
    """Mean over a (2r+1)^2 window, via summed-area tables."""
    pad = np.pad(a, ((r + 1, r), (r + 1, r)) + (((0, 0),) if a.ndim == 3 else ()), mode='edge')
    c = pad.cumsum(0).cumsum(1)
    k = 2 * r + 1
    return (c[k:, k:] - c[:-k, k:] - c[k:, :-k] + c[:-k, :-k]) / (k * k)


def cut(path, target):
    image = bpy.data.images.load(path)
    w, h = image.size
    image.scale(int(w * SCALE), int(h * SCALE))
    w, h = image.size
    px = np.array(image.pixels[:], dtype=np.float32).reshape(h, w, 4)
    bpy.data.images.remove(image)
    rgb = px[..., :3]
    chroma = rgb.max(-1) - rgb.min(-1)
    calm = chroma < CHROMA

    bg = np.zeros((h, w), bool)
    bg[0, :] = calm[0, :]; bg[-1, :] = calm[-1, :]; bg[:, 0] = calm[:, 0]; bg[:, -1] = calm[:, -1]
    for _ in range(4000):
        grown = bg.copy()
        for dy, dx in ((1, 0), (-1, 0), (0, 1), (0, -1)):
            near = shifted(bg, dy, dx)
            similar = np.abs(rgb - shifted(rgb, dy, dx)).max(-1) < STEP
            grown |= near & similar & calm
        if grown.sum() == bg.sum():
            break
        bg = grown

    # Smooth backdrop colour everywhere, from the pixels known to be backdrop.
    def backdrop_field(mask):
        weight = box_blur(mask.astype(np.float32), 40)
        f = box_blur(rgb * mask[..., None], 40) / np.maximum(weight[..., None], 1e-4)
        return np.where(weight[..., None] > 1e-3, f, np.median(rgb[mask], axis=0) if mask.any() else rgb.mean((0, 1)))

    field = backdrop_field(bg)
    # Enclosed pockets: backdrop trapped inside a ring of effects (rocks, feathers, chains). A flat patch of
    # backdrop-coloured, colourless pixels, at least 13 px across, seeds its own fill.
    like = calm & (np.abs(rgb - field).max(-1) < 0.06)
    seeds = like & (box_blur(like.astype(np.float32), 6) > 0.97) & ~bg
    pocket = seeds
    for _ in range(2000):
        grown = pocket.copy()
        for dy, dx in ((1, 0), (-1, 0), (0, 1), (0, -1)):
            grown |= shifted(pocket, dy, dx) & (np.abs(rgb - shifted(rgb, dy, dx)).max(-1) < STEP) & like
        if grown.sum() == pocket.sum():
            break
        pocket = grown
    bg = bg | pocket
    field = backdrop_field(bg)

    # Distance (in pixels) from the backdrop, by repeated growing, capped at BAND.
    dist = np.full((h, w), BAND + 1, np.float32)
    front = bg.copy()
    dist[bg] = 0
    for d in range(1, BAND + 1):
        front = front | shifted(front, 1, 0) | shifted(front, -1, 0) | shifted(front, 0, 1) | shifted(front, 0, -1)
        newly = front & (dist > d)
        dist[newly] = d

    diff = np.abs(rgb - field).max(-1)
    soft = np.clip((diff - 0.03) / 0.22, 0, 1)
    edge = (~bg) & (dist <= BAND)
    alpha = np.where(bg, 0.0, 1.0)
    # Within the band, opacity follows how unlike the backdrop the pixel is, rising to solid at the band's edge.
    ramp = np.clip(dist / BAND, 0, 1)
    alpha = np.where(edge, np.maximum(soft, ramp ** 2), alpha).astype(np.float32)
    a = alpha[..., None]
    colour = np.where(a > 0.02, (rgb - (1 - a) * field) / np.maximum(a, 0.02), 0)
    out = np.concatenate([np.clip(colour, 0, 1), a], -1)
    o = bpy.data.images.new('o', w, h, alpha=True)
    o.pixels = out.ravel().tolist()
    o.filepath_raw = target
    o.file_format = 'PNG'
    o.save()
    print('CUT', os.path.basename(target), w, h, 'backdrop %.2f' % bg.mean())


for path in sorted(glob.glob(os.path.join(SRC, '*_base.png'))):
    name = os.path.basename(path)[:-len('_base.png')]
    cut(path, os.path.join(OUT, name + '.png'))
print('CUTOUT_DONE')
