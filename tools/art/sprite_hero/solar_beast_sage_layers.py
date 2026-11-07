"""Blender script. Splits the Solar Beast Sage concept into animation layers and rebuilds what other layers hide.
A layer keeps its own pixels. Where another part covered it, or where a joint needs overlap, colour is grown in
from the layer's own neighbouring pixels. Those areas are approximate: nearly all stay hidden behind other layers.
Coordinates are pixels from the top left of the 1024x1536 concept.
Usage: blender -b --factory-startup -P solar_beast_sage_layers.py -- concept.png out_dir"""
import json
import os
import sys

import bpy
import numpy as np

inp, out_dir = sys.argv[sys.argv.index("--") + 1:]
os.makedirs(out_dir, exist_ok=True)
SCALE = 2  # Layers are saved at half size for phones.

image = bpy.data.images.load(inp)
image.colorspace_settings.name = "Non-Color"
W, H = image.size
src = np.empty(W * H * 4, dtype=np.float32)
image.pixels.foreach_get(src)
src = src.reshape(H, W, 4)[::-1].astype(float)
yy, xx = np.mgrid[:H, :W]
print("SOURCE", W, H)


def grow(mask, n=1):
    m = mask.copy()
    for _ in range(n):
        g = m.copy()
        g[1:] |= m[:-1]
        g[:-1] |= m[1:]
        g[:, 1:] |= m[:, :-1]
        g[:, :-1] |= m[:, 1:]
        m = g
    return m


def shrink(mask, n=1):
    return ~grow(~mask, n)


def poly(points):
    m = np.zeros((H, W), bool)
    for (x0, y0), (x1, y1) in zip(points, points[1:] + points[:1]):
        if y0 == y1:
            continue
        c = (yy < max(y0, y1)) & (yy >= min(y0, y1))
        m ^= c & (xx < x0 + (yy - y0) * (x1 - x0) / (y1 - y0))
    return m


def disc(cx, cy, r):
    return np.hypot(xx - cx, yy - cy) < r


# ---------- Remove the grey background and the floor shadow ----------
rgb = src[..., :3]
sat = rgb.max(-1) - rgb.min(-1)
lum = rgb.mean(-1)
candidate = (sat < .055) & (lum > .28)
background = np.zeros((H, W), bool)
background[0, :] = background[-1, :] = True
background[:, 0] = background[:, -1] = True
background &= candidate
for _ in range(3000):
    next_bg = grow(background) & candidate
    if next_bg.sum() == background.sum():
        break
    background = next_bg
# Grey trapped between limbs and body is background too. Light low-colour areas in the face are not.
FACE_GUARD = (xx > 395) & (xx < 685) & (yy < 300)
background |= candidate & (lum > .55) & (sat < .04) & ~FACE_GUARD
solid = shrink(~background, 1)
print("SOLID", int(solid.sum()))

# ---------- Part outlines ----------
HAND_R_AREA = poly([(625, 700), (695, 745), (665, 825), (555, 825), (550, 760), (590, 710)])
skin = (rgb[..., 1] / np.maximum(rgb[..., 0], .01) > .58) & (rgb[..., 0] > rgb[..., 2]) & (rgb[..., 0] > .35)
HAND_R = grow(shrink(grow(HAND_R_AREA & skin & solid, 3), 3), 1) & HAND_R_AREA
HAND_L = poly([(205, 722), (300, 722), (300, 860), (205, 860)])
SASH = poly([(532, 700), (603, 700), (618, 912), (572, 914), (570, 880), (533, 880)])
HEAD = poly([(395, 0), (685, 0), (685, 292), (600, 292), (576, 225), (545, 268), (505, 296), (470, 284), (440, 242), (420, 205), (395, 205)])
TORSO = poly([(455, 225), (566, 225), (586, 320), (656, 385), (628, 525), (608, 650), (610, 700), (592, 800), (530, 836), (400, 826), (333, 806), (375, 695), (385, 650), (385, 530), (365, 385), (440, 318)])
UPPER_L = poly([(365, 382), (300, 480), (272, 615), (348, 628), (387, 532)])
FORE_L = poly([(272, 615), (348, 628), (332, 662), (326, 737), (232, 737), (228, 700)])
UPPER_R = poly([(656, 382), (745, 485), (752, 612), (690, 628), (626, 527)])
FORE_R = poly([(690, 628), (752, 612), (732, 685), (697, 748), (625, 722), (636, 680)])
PELVIS = poly([(400, 760), (592, 760), (592, 905), (520, 905), (492, 885), (465, 905), (400, 905)])
THIGH_L = poly([(335, 760), (505, 760), (505, 885), (432, 1055), (280, 1055), (288, 1000), (335, 830)])
THIGH_R = poly([(490, 760), (602, 760), (602, 850), (700, 1055), (558, 1055), (490, 885)])
SHIN_L = poly([(280, 1040), (435, 1040), (405, 1215), (375, 1352), (278, 1352), (268, 1150)])
SHIN_R = poly([(556, 1040), (700, 1040), (722, 1200), (695, 1352), (622, 1352), (582, 1200)])
FOOT_L = poly([(212, 1342), (350, 1342), (350, 1495), (212, 1495)])
FOOT_R = poly([(615, 1342), (705, 1342), (770, 1495), (615, 1495)])

# name, keep, cover (occluded here, to rebuild), extra (joint overlap to grow), pivot, parent, draw order
LAYERS = [
    ("foot_l", FOOT_L, None, disc(306, 1352, 22), (300, 1345), "shin_l", 10),
    ("shin_l", SHIN_L, None, disc(358, 1050, 56), (358, 1050), "thigh_l", 20),
    ("foot_r", FOOT_R, None, disc(662, 1352, 22), (665, 1345), "shin_r", 12),
    ("shin_r", SHIN_R, None, disc(628, 1050, 56), (628, 1050), "thigh_r", 22),
    ("pelvis", PELVIS, TORSO | SASH | HAND_R, None, (497, 800), "hips", 30),
    ("thigh_l", THIGH_L, TORSO | SASH, disc(440, 830, 66), (440, 830), "hips", 32),
    ("thigh_r", THIGH_R, TORSO | SASH | HAND_R, disc(552, 830, 60), (552, 830), "hips", 34),
    ("torso", TORSO, HEAD | HAND_R | SASH, disc(395, 425, 44) | disc(625, 425, 44) | poly([(466, 215), (558, 215), (558, 300), (466, 300)]), (497, 780), "hips", 40),
    ("sash", SASH, HAND_R, None, (568, 702), "torso", 44),
    ("head", HEAD, None, None, (510, 290), "torso", 50),
    ("upper_l", UPPER_L, None, disc(384, 442, 38), (362, 428), "torso", 60),
    ("fore_l", FORE_L, None, disc(312, 624, 28), (310, 620), "upper_l", 62),
    ("hand_l", HAND_L, None, disc(264, 728, 16), (268, 730), "fore_l", 64),
    ("upper_r", UPPER_R, None, disc(640, 442, 38), (662, 428), "torso", 66),
    ("fore_r", FORE_R, None, disc(708, 628, 28), (718, 622), "upper_r", 68),
    ("hand_r", HAND_R, None, disc(648, 724, 16), (650, 722), "fore_r", 70),
]


def fill(color, have, target):
    """Grows colour from pixels in have into target by averaging neighbours, then softens the new pixels."""
    color = color.copy()
    have = have.copy()
    target = target & ~have
    weights = np.zeros((H, W))
    for _ in range(600):
        if not target.any():
            break
        total = np.zeros((H, W, 3))
        count = np.zeros((H, W))
        for dy, dx in ((1, 0), (-1, 0), (0, 1), (0, -1)):
            shifted = np.roll(have, (dy, dx), (0, 1))
            total += np.roll(color * have[..., None], (dy, dx), (0, 1))
            count += shifted
        front = target & (count > 0)
        if not front.any():
            break
        color[front] = total[front] / count[front][..., None]
        have |= front
        target &= ~front
        weights[front] = 1
    return color, have, weights > 0


def save_png(pixels, path):
    h, w = pixels.shape[:2]
    o = bpy.data.images.new("o", w, h, alpha=True)
    o.colorspace_settings.name = "Non-Color"
    o.pixels.foreach_set(pixels[::-1].ravel().astype(np.float32))
    o.filepath_raw = path
    o.file_format = "PNG"
    o.save()
    bpy.data.images.remove(o)


layout = []
assembled = np.zeros((H, W, 4))
exploded = np.zeros((H, W * 2, 4))
exploded[..., :3] = (.55, .15, .55)
exploded[..., 3] = 1
rebuilt_total = 0
for index, (name, keep, cover, extra, pivot, parent, order) in enumerate(sorted(LAYERS, key=lambda l: l[6])):
    own = keep & solid
    target = np.zeros((H, W), bool)
    if cover is not None:
        own = own & ~cover
        target |= keep & cover & grow(solid, 2)
    if extra is not None:
        target |= extra
    color, have, new = fill(rgb, own, target)
    have &= (own | target)
    new &= have
    rebuilt_total += int(new.sum())
    soft = np.clip((grow(have, 1).astype(float) + have.astype(float)) / 2, 0, 1)
    ys, xs = np.nonzero(have)
    x0, x1, y0, y1 = xs.min() - 4, xs.max() + 5, ys.min() - 4, ys.max() + 5
    x0, y0 = max(x0, 0) // SCALE * SCALE, max(y0, 0) // SCALE * SCALE
    x1, y1 = min(x1, W), min(y1, H)
    w = (x1 - x0) // SCALE
    h = (y1 - y0) // SCALE
    rgba = np.concatenate([color, soft[..., None]], -1)[y0:y0 + h * SCALE, x0:x0 + w * SCALE]
    premult = rgba.copy()
    premult[..., :3] *= rgba[..., 3:4]
    small = premult.reshape(h, SCALE, w, SCALE, 4).mean((1, 3))
    alpha = small[..., 3:4]
    small[..., :3] = np.where(alpha > 1e-3, small[..., :3] / np.maximum(alpha, 1e-3), 0)
    save_png(small, f"{out_dir}/{name}.png")
    layout.append({
        "name": name, "parent": parent, "order": order,
        "pivotX": float(pivot[0] - x0) / SCALE, "pivotY": float(y0 + h * SCALE - pivot[1]) / SCALE,
        "width": int(w), "height": int(h),
        "worldX": pivot[0] / SCALE, "worldY": (H - pivot[1]) / SCALE,
    })
    a = soft[..., None]
    assembled[..., :3] = color * a + assembled[..., :3] * (1 - a)
    assembled[..., 3:4] = a + assembled[..., 3:4] * (1 - a)
    col, row = index % 8, index // 8
    px, py = W * col // 4, row * H // 2
    tile = np.concatenate([color, soft[..., None]], -1)[::3, ::3]
    th, tw = tile.shape[:2]
    py2, px2 = min(py, H - th), min(px, W * 2 - tw)
    region = exploded[py2:py2 + th, px2:px2 + tw]
    region[..., :3] = tile[..., :3] * tile[..., 3:4] + region[..., :3] * (1 - tile[..., 3:4])

with open(f"{out_dir}/layout.json", "w") as handle:
    json.dump({"hero": "solar_beast_sage", "form": 0, "scale": SCALE, "canvasWidth": W / SCALE, "canvasHeight": H / SCALE,
               "feetX": 490 / SCALE, "feetY": (H - 1480) / SCALE, "layers": layout}, handle, indent=1)
back = np.zeros((H, W, 4))
back[..., :3] = (.55, .15, .55)
back[..., 3] = 1
a = assembled[..., 3:4]
back[..., :3] = assembled[..., :3] * a + back[..., :3] * (1 - a)
os.makedirs(out_dir + "/_review", exist_ok=True)
save_png(back, f"{out_dir}/_review/assembled.png")
save_png(exploded, f"{out_dir}/_review/layers.png")
print("REBUILT_PIXELS", rebuilt_total, "LAYERS", len(layout))
print("LAYERS_DONE")
