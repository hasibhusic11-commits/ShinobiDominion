"""Draws the Shinobi Dominion HUD kit: brass medallion rings, lacquer faces, nine-sliced plates, and icons.

Run through Blender, which brings Python and numpy:
    blender -b --factory-startup --python tools/art/ui_kit.py -- <unity Resources/UI folder>

Everything is drawn at 4x and averaged down, so edges are smooth. Plates are named name_s<border>.png and the
editor importer turns that number into the sprite's nine-slice border. Palette: docs/ui/HUD_SPEC.md.
"""
import math
import os
import sys

import bpy
import numpy as np

SS = 4  # supersampling


def hexc(h, a=1.0):
    h = h.lstrip('#')
    return np.array([int(h[0:2], 16) / 255, int(h[2:4], 16) / 255, int(h[4:6], 16) / 255, a], dtype=np.float32)


LACQUER, LACQUER_DEEP, LACQUER_LIGHT = hexc('16203A'), hexc('0C1222'), hexc('2B3B63')
BRASS, BRASS_LIGHT, BRASS_DARK = hexc('B08A47'), hexc('E8C77E'), hexc('6B4F22')
CHARCOAL = hexc('2A2A30')
CRIMSON, CRIMSON_LIGHT, CRIMSON_DARK = hexc('8E2A26'), hexc('C2483C'), hexc('4E1310')
IVORY, INK = hexc('EFE6D0'), hexc('0C1222')
GOLD_TOP, GOLD_MID, GOLD_BOT = hexc('F7DC93'), hexc('C99A45'), hexc('7A5523')


def grid(w, h):
    """Pixel centres at supersampled resolution, in output-pixel units, y down."""
    ys, xs = np.mgrid[0:h * SS, 0:w * SS].astype(np.float32)
    return (xs + 0.5) / SS, (ys + 0.5) / SS


def save(name, rgba, folder):
    """rgba is supersampled, y down, straight alpha. Average down with alpha weighting and save."""
    h, w = rgba.shape[0] // SS, rgba.shape[1] // SS
    a = rgba[..., 3:4]
    pre = np.concatenate([rgba[..., :3] * a, a], axis=2)
    pre = pre.reshape(h, SS, w, SS, 4).mean(axis=(1, 3))
    alpha = pre[..., 3:4]
    rgb = np.where(alpha > 1e-4, pre[..., :3] / np.maximum(alpha, 1e-4), 0)
    out = np.concatenate([rgb, alpha], axis=2)[::-1]  # Blender rows start at the bottom
    img = bpy.data.images.new(name, w, h, alpha=True)
    img.pixels[:] = np.clip(out, 0, 1).ravel()
    img.filepath_raw = os.path.join(folder, name + '.png')
    img.file_format = 'PNG'
    img.save()
    print('UI', name, w, h)


def over(dst, color, mask):
    """Paints color (rgba) over dst where mask (0..1) says."""
    m = mask[..., None] * color[3]
    dst[..., :3] = color[:3] * m + dst[..., :3] * (1 - m)
    dst[..., 3:4] = m + dst[..., 3:4] * (1 - m)


def lerp(a, b, t):
    t = np.clip(t, 0, 1)[..., None]
    return a * (1 - t) + b * t


def paint(dst, rgba_field, mask):
    m = (mask * rgba_field[..., 3])[..., None]
    dst[..., :3] = rgba_field[..., :3] * m + dst[..., :3] * (1 - m)
    dst[..., 3:4] = m + dst[..., 3:4] * (1 - m)


def smooth(d):
    """Signed distance (negative inside) to a soft 1-pixel edge."""
    return np.clip(0.5 - d, 0, 1)


def rounded_rect_sd(x, y, x0, y0, x1, y1, r):
    cx, cy = (x0 + x1) / 2, (y0 + y1) / 2
    hx, hy = (x1 - x0) / 2 - r, (y1 - y0) / 2 - r
    qx, qy = np.abs(x - cx) - hx, np.abs(y - cy) - hy
    outside = np.sqrt(np.maximum(qx, 0) ** 2 + np.maximum(qy, 0) ** 2)
    return outside + np.minimum(np.maximum(qx, qy), 0) - r


# ---------- Medallions ----------

def ring(size, outer, inner, rivets, folder, name):
    x, y = grid(size, size)
    c = size / 2
    dx, dy = x - c, y - c
    r = np.sqrt(dx * dx + dy * dy)
    img = np.zeros(x.shape + (4,), np.float32)
    band = np.clip(np.minimum(outer - r, r - inner) + 0.5, 0, 1)
    # Bevel: a ridge in the middle of the band, lit from the top left.
    mid = (outer + inner) / 2
    across = (r - mid) / ((outer - inner) / 2)  # -1 inner edge .. 1 outer edge
    nx, ny = dx / np.maximum(r, 1e-3), dy / np.maximum(r, 1e-3)
    sign = np.where(across > 0, 1.0, -1.0)
    light = -(nx * -0.6 + ny * -0.8) * sign * np.abs(across)  # facing the light from the top left
    shade = 0.92 + 0.5 * light
    metal = np.where((shade > 1.0)[..., None], lerp(BRASS, BRASS_LIGHT, (shade - 1.0) * 2.2), lerp(BRASS_DARK, BRASS, (shade - 0.45) / 0.55))
    paint(img, metal, band)
    # Dark hairlines on both edges.
    edge = np.clip(1.6 - np.minimum(np.abs(r - outer), np.abs(r - inner)), 0, 1) * band
    over(img, hexc('3A2810'), edge)
    for i in range(rivets):
        angle = i * 2 * math.pi / rivets + math.pi / rivets
        rx, ry = c + mid * math.cos(angle), c + mid * math.sin(angle)
        d = np.sqrt((x - rx) ** 2 + (y - ry) ** 2)
        rr = (outer - inner) * 0.2
        over(img, hexc('3A2810'), smooth(d - rr - 0.8))
        hl = lerp(BRASS_LIGHT, BRASS_DARK, np.clip(((x - rx) * 0.6 + (y - ry) * 0.8) / rr * 0.5 + 0.5, 0, 1))
        paint(img, hl, smooth(d - rr))
    save(name, img, folder)


def face(size, radius, top, mid, bottom, folder, name, gloss=0.30):
    x, y = grid(size, size)
    c = size / 2
    dx, dy = x - c, y - c
    r = np.sqrt(dx * dx + dy * dy)
    img = np.zeros(x.shape + (4,), np.float32)
    # Light falls from above: bright near the top centre, deep at the bottom edge.
    t = np.clip(np.sqrt((dx / radius) ** 2 + ((dy + radius * 0.45) / (radius * 1.45)) ** 2), 0, 1)
    col = np.where((t < 0.5)[..., None], lerp(top, mid, t * 2), lerp(mid, bottom, (t - 0.5) * 2))
    paint(img, col, smooth(r - radius))
    # Inner shadow just inside the rim.
    over(img, hexc('000000', 0.55), smooth(r - radius) * np.clip((r - (radius - 10)) / 10, 0, 1))
    # Gloss over the top: a soft ellipse.
    g = ((dx / (radius * 0.72)) ** 2 + ((dy + radius * 0.48) / (radius * 0.36)) ** 2)
    over(img, hexc('FFFFFF', gloss), np.clip(1 - g, 0, 1) ** 0.8 * smooth(r - radius))
    save(name, img, folder)


def glow(size, folder, name):
    x, y = grid(size, size)
    c = size / 2
    r = np.sqrt((x - c) ** 2 + (y - c) ** 2) / c
    img = np.zeros(x.shape + (4,), np.float32)
    over(img, hexc('FFFFFF'), np.clip(1 - r, 0, 1) ** 2.2)
    save(name, img, folder)


def shadow(size, folder, name):
    x, y = grid(size, size)
    c = size / 2
    r = np.sqrt((x - c) ** 2 + (y - c) ** 2) / c
    img = np.zeros(x.shape + (4,), np.float32)
    over(img, hexc('000000', 0.55), np.clip((1 - r) * 2.2, 0, 1) ** 1.5)
    save(name, img, folder)


# ---------- Plates (nine-sliced) ----------

def plate(w, h, radius, rim, folder, name):
    x, y = grid(w, h)
    img = np.zeros(x.shape + (4,), np.float32)
    sd = rounded_rect_sd(x, y, 0, 0, w, h, radius)
    depth = -sd
    # Brass rim, lit from the top.
    vertical = y / h
    metal = lerp(BRASS_LIGHT, BRASS_DARK, vertical * 1.1)
    paint(img, metal, smooth(sd))
    over(img, hexc('3A2810'), smooth(sd) * np.clip(1.4 - depth, 0, 1))
    # Lacquer inside the rim: light at the top, deep at the bottom, with a shadow under the top rim.
    inner = smooth(sd + rim)
    lac = np.where((vertical < 0.4)[..., None], lerp(LACQUER_LIGHT, LACQUER, vertical / 0.4), lerp(LACQUER, LACQUER_DEEP, (vertical - 0.4) / 0.6))
    paint(img, lac, inner)
    over(img, hexc('3A2810'), inner * np.clip(1.4 - (depth - rim), 0, 1))
    over(img, hexc('000000', 0.45), inner * np.clip(1 - (depth - rim) / 7, 0, 1) * (vertical < 0.5))
    save(name, img, folder)


def pill(w, h, fill, edge, folder, name):
    x, y = grid(w, h)
    img = np.zeros(x.shape + (4,), np.float32)
    sd = rounded_rect_sd(x, y, 0, 0, w, h, h / 2)
    paint(img, np.broadcast_to(edge, x.shape + (4,)).copy(), smooth(sd))
    paint(img, np.broadcast_to(fill, x.shape + (4,)).copy(), smooth(sd + 1.2))
    save(name, img, folder)


# ---------- Icons ----------

def poly_mask(x, y, points):
    inside = np.zeros(x.shape, bool)
    n = len(points)
    for i in range(n):
        x0, y0 = points[i]
        x1, y1 = points[(i + 1) % n]
        cond = (y0 > y) != (y1 > y)
        xi = (x1 - x0) * (y - y0) / ((y1 - y0) + 1e-9) + x0
        inside ^= cond & (x < xi)
    return inside.astype(np.float32)


def circle_mask(x, y, cx, cy, r):
    return (np.sqrt((x - cx) ** 2 + (y - cy) ** 2) <= r).astype(np.float32)


def ellipse_mask(x, y, cx, cy, rx, ry):
    return (((x - cx) / rx) ** 2 + ((y - cy) / ry) ** 2 <= 1).astype(np.float32)


def rot(points, angle, cx=64, cy=64):
    ca, sa = math.cos(angle), math.sin(angle)
    return [(cx + (px - cx) * ca - (py - cy) * sa, cy + (px - cx) * sa + (py - cy) * ca) for px, py in points]


def dilate(mask, steps):
    m = mask > 0.5
    for i in range(steps):
        g = m.copy()
        g[1:, :] |= m[:-1, :]
        g[:-1, :] |= m[1:, :]
        g[:, 1:] |= m[:, :-1]
        g[:, :-1] |= m[:, 1:]
        if i % 2:
            g[1:, 1:] |= m[:-1, :-1]
            g[:-1, :-1] |= m[1:, 1:]
            g[1:, :-1] |= m[:-1, 1:]
            g[:-1, 1:] |= m[1:, :-1]
        m = g
    return m.astype(np.float32)


def icon(name, layers, folder, outline=5):
    """layers: list of (mask_fn(x, y) -> 0/1, rgba colour), painted in order, all outlined together."""
    x, y = grid(128, 128)
    img = np.zeros(x.shape + (4,), np.float32)
    union = np.zeros(x.shape, np.float32)
    masks = []
    for fn, color in layers:
        m = fn(x, y)
        masks.append((m, color))
        union = np.maximum(union, m)
    over(img, INK, dilate(union, outline * SS))
    for m, color in masks:
        over(img, color, m)
    save('icon_' + name, img, folder)


def kunai(angle):
    """A long leaf blade, a short brass guard and a wrapped grip; no ring, so the pair never reads as scissors."""
    blade = [(64, 4), (75, 30), (69, 74), (59, 74), (53, 30)]
    guard = [(52, 74), (76, 74), (76, 81), (52, 81)]
    grip = [(59, 81), (69, 81), (68, 112), (60, 112)]
    return rot(blade, angle), rot(guard, angle), rot(grip, angle)


def build_icons(folder):
    ivory = IVORY
    shade = hexc('C9BFA8')
    # Crossed kunai for ATTACK.
    b1, d1, g1 = kunai(math.radians(-34))
    b2, d2, g2 = kunai(math.radians(34))
    icon('attack', [
        (lambda x, y: poly_mask(x, y, g1), hexc('5A3418')), (lambda x, y: poly_mask(x, y, g2), hexc('5A3418')),
        (lambda x, y: poly_mask(x, y, b1), ivory), (lambda x, y: poly_mask(x, y, d1), BRASS_LIGHT),
        (lambda x, y: poly_mask(x, y, b2), ivory), (lambda x, y: poly_mask(x, y, d2), BRASS_LIGHT),
    ], folder)
    icon('info', [(lambda x, y: circle_mask(x, y, 64, 30, 11), ivory),
                  (lambda x, y: (rounded_rect_sd(x, y, 54, 50, 74, 106, 5) <= 0).astype(np.float32), ivory)], folder)
    icon('upgrade', [(lambda x, y: poly_mask(x, y, [(64, 12), (106, 58), (80, 58), (80, 110), (48, 110), (48, 58), (22, 58)]), ivory)], folder)
    arrow = [(64, 8), (84, 30), (72, 30), (72, 56), (56, 56), (56, 30), (44, 30)]
    icon('move', [(lambda x, y, a=a: poly_mask(x, y, rot(arrow, a)), ivory) for a in (0, math.pi / 2, math.pi, -math.pi / 2)]
         + [(lambda x, y: (rounded_rect_sd(x, y, 52, 52, 76, 76, 3) <= 0).astype(np.float32), ivory)], folder)
    head = rot([(30, 22), (86, 22), (86, 50), (30, 50)], math.radians(-30))
    handle = rot([(56, 46), (68, 46), (68, 116), (56, 116)], math.radians(-30))
    icon('build', [(lambda x, y: poly_mask(x, y, handle), hexc('8B5A2B')), (lambda x, y: poly_mask(x, y, head), shade)], folder)
    icon('close', [(lambda x, y: poly_mask(x, y, rot([(56, 14), (72, 14), (72, 114), (56, 114)], math.radians(45))), ivory),
                   (lambda x, y: poly_mask(x, y, rot([(56, 14), (72, 14), (72, 114), (56, 114)], math.radians(-45))), ivory)], folder)
    icon('menu', [(lambda x, y, t=t: (rounded_rect_sd(x, y, 20, t, 108, t + 16, 6) <= 0).astype(np.float32), ivory) for t in (26, 56, 86)], folder)
    icon('roster', [(lambda x, y: (rounded_rect_sd(x, y, 30, 22, 98, 106, 4) <= 0).astype(np.float32), hexc('E9DDB8')),
                    (lambda x, y: ellipse_mask(x, y, 64, 22, 40, 10), hexc('8B5A2B')),
                    (lambda x, y: ellipse_mask(x, y, 64, 106, 40, 10), hexc('8B5A2B'))]
         + [(lambda x, y, t=t: (rounded_rect_sd(x, y, 42, t, 86, t + 6, 3) <= 0).astype(np.float32), hexc('6B4F22')) for t in (42, 58, 74)], folder)
    icon('gate', [(lambda x, y: np.clip(circle_mask(x, y, 64, 64, 46) - circle_mask(x, y, 64, 64, 30), 0, 1), BRASS_LIGHT),
                  (lambda x, y: circle_mask(x, y, 64, 64, 22), hexc('7FE6FF'))], folder)
    sword = [(64, 6), (74, 20), (72, 84), (56, 84), (54, 20)]
    guard = [(38, 84), (90, 84), (90, 94), (38, 94)]
    hilt = [(59, 94), (69, 94), (69, 120), (59, 120)]
    icon('train', [(lambda x, y: poly_mask(x, y, rot(sword, math.radians(30))), ivory),
                   (lambda x, y: poly_mask(x, y, rot(guard, math.radians(30))), BRASS_LIGHT),
                   (lambda x, y: poly_mask(x, y, rot(hilt, math.radians(30))), hexc('8B5A2B'))], folder)
    icon('hero', [(lambda x, y: circle_mask(x, y, 64, 40, 22), ivory),
                  (lambda x, y: ellipse_mask(x, y, 64, 112, 44, 42) * (y < 112), ivory)], folder)
    bricks = []
    for row, y0 in enumerate((30, 58, 86)):
        off = 0 if row % 2 == 0 else 18
        for x0 in range(10 + off - 36, 120, 36):
            bricks.append((max(x0, 12), y0, min(x0 + 32, 116), y0 + 24))
    icon('walls', [(lambda x, y, b=b: (rounded_rect_sd(x, y, *b, 3) <= 0).astype(np.float32), hexc('B9AF98')) for b in bricks if b[2] - b[0] > 8], folder)
    # Currency medallion pictures, in colour.
    coins = []
    for i, cy in enumerate((96, 80, 64, 48)):
        top = hexc(['C99A45', 'E0B25A', 'F2CF7A', 'FFE7A6'][i])
        coins.append((lambda x, y, cy=cy: ellipse_mask(x, y, 64, cy, 40, 15), top))
    icon('gold', coins, folder, outline=4)
    crystal = [(64, 10), (102, 46), (64, 118), (26, 46)]
    icon('crystals', [(lambda x, y: poly_mask(x, y, crystal), hexc('7FD8F5')),
                      (lambda x, y: poly_mask(x, y, [(64, 10), (80, 46), (64, 118), (48, 46)]), hexc('C8F3FF')),
                      (lambda x, y: poly_mask(x, y, [(26, 46), (102, 46), (98, 50), (30, 50)]), hexc('2C7FA8'))], folder, outline=4)


def main(folder):
    os.makedirs(folder, exist_ok=True)
    ring(320, 160, 136, 8, folder, 'medal_ring_attack')
    ring(256, 128, 108, 0, folder, 'medal_ring')
    ring(256, 128, 114, 0, folder, 'medal_ring_thin')
    face(320, 138, CRIMSON_LIGHT, CRIMSON, CRIMSON_DARK, folder, 'medal_face_crimson')
    face(256, 110, GOLD_TOP, GOLD_MID, GOLD_BOT, folder, 'medal_face_gold')
    face(256, 110, LACQUER_LIGHT * 1.25, LACQUER, LACQUER_DEEP, folder, 'medal_face_lacquer', gloss=0.22)
    face(256, 110, hexc('5A5A62'), CHARCOAL, hexc('141418'), folder, 'medal_face_charcoal', gloss=0.12)
    face(256, 110, CRIMSON_LIGHT, CRIMSON, CRIMSON_DARK, folder, 'medal_face_crimson_small')
    glow(128, folder, 'glow')
    shadow(128, folder, 'shadow')
    plate(128, 72, 14, 4, folder, 'plate_s26')
    pill(48, 16, hexc('05080F'), BRASS_DARK, folder, 'track_s8')
    pill(48, 16, hexc('FFFFFF'), hexc('FFFFFF'), folder, 'fill_s8')
    build_icons(folder)
    print('UI_KIT_DONE')


if __name__ == '__main__':
    main(sys.argv[sys.argv.index('--') + 1])
