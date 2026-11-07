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


def button_plate(face_top, face_bottom, folder, name, w=160, h=64, radius=14):
    """A raised plate: brass rim, a face lit from the top, a gloss band, and a darker lip along the bottom."""
    x, y = grid(w, h)
    img = np.zeros(x.shape + (4,), np.float32)
    sd = rounded_rect_sd(x, y, 0, 0, w, h, radius)
    depth = -sd
    v = y / h
    paint(img, lerp(BRASS_LIGHT, BRASS_DARK, v * 1.1), smooth(sd))
    over(img, hexc('3A2810'), smooth(sd) * np.clip(1.4 - depth, 0, 1))
    face_sd = rounded_rect_sd(x, y, 3, 3, w - 3, h - 7, radius - 3)
    paint(img, lerp(face_top, face_bottom, (y - 3) / (h - 10)), smooth(face_sd))
    over(img, hexc('3A2810'), smooth(face_sd) * np.clip(1.4 + face_sd, 0, 1) * (face_sd > -1.5))
    gloss = smooth(rounded_rect_sd(x, y, 7, 6, w - 7, h * 0.42, radius - 6))
    over(img, hexc('FFFFFF', 0.22), gloss)
    save(name, img, folder)


def frame(size, thick, corner, folder, name):
    """A heavy brass window frame with riveted corner caps and a lacquer inside."""
    x, y = grid(size, size)
    img = np.zeros(x.shape + (4,), np.float32)
    sd = rounded_rect_sd(x, y, 0, 0, size, size, 10)
    depth = -sd
    v = y / size
    across = np.clip(depth / thick, 0, 1)
    metal = lerp(lerp(BRASS_LIGHT, BRASS, across * 1.6), BRASS_DARK, np.clip((across - 0.6) * 2.5, 0, 1))
    metal = lerp(metal, BRASS_DARK, v * 0.35)
    paint(img, metal, smooth(sd))
    over(img, hexc('3A2810'), smooth(sd) * np.clip(1.6 - depth, 0, 1))
    inner = smooth(sd + thick)
    lac = lerp(LACQUER_LIGHT, LACQUER_DEEP, np.clip(v * 1.2, 0, 1))
    paint(img, lac, inner)
    over(img, hexc('3A2810'), inner * np.clip(2.0 - (depth - thick), 0, 1))
    over(img, hexc('000000', 0.5), inner * np.clip(1 - (depth - thick) / 10, 0, 1))
    for cx, cy in ((corner / 2, corner / 2), (size - corner / 2, corner / 2), (corner / 2, size - corner / 2), (size - corner / 2, size - corner / 2)):
        cap = rounded_rect_sd(x, y, cx - corner / 2, cy - corner / 2, cx + corner / 2, cy + corner / 2, 6)
        paint(img, lerp(BRASS_LIGHT, BRASS_DARK, np.clip((y - (cy - corner / 2)) / corner, 0, 1)), smooth(cap))
        over(img, hexc('3A2810'), smooth(cap) * np.clip(1.6 + cap, 0, 1))
        d = np.sqrt((x - cx) ** 2 + (y - cy) ** 2)
        over(img, hexc('3A2810'), smooth(d - 5.5))
        paint(img, lerp(BRASS_LIGHT, BRASS_DARK, np.clip(((x - cx) * 0.6 + (y - cy) * 0.8) / 5 * 0.5 + 0.5, 0, 1)), smooth(d - 4.5))
    save(name, img, folder)


def ribbon(folder, name, w=360, h=62):
    """A crimson title ribbon with brass edges and swallow-tail ends."""
    x, y = grid(w, h)
    img = np.zeros(x.shape + (4,), np.float32)
    tail = 34
    body = (x > tail * 0.55) & (x < w - tail * 0.55) & (y > 6) & (y < h - 6)
    notch = np.abs(y - h / 2) / (h / 2 - 6) * tail * 0.55
    left_tail = (x <= tail * 0.55 + 1) & (x > notch * 0.0 + (tail * 0.55 - (tail * 0.55 - notch))) & (y > 12) & (y < h - 12)
    right_tail = (x >= w - tail * 0.55 - 1) & (x < w - (tail * 0.55 - (tail * 0.55 - notch))) & (y > 12) & (y < h - 12)
    shape = (body | left_tail | right_tail).astype(np.float32)
    v = (y - 6) / (h - 12)
    col = lerp(CRIMSON_LIGHT, CRIMSON_DARK, np.clip(v, 0, 1))
    over(img, INK, dilate(shape, 3 * SS))
    paint(img, col, shape)
    rim = shape * ((np.abs(y - 9) < 2.2) | (np.abs(y - (h - 9)) < 2.2)) * body
    over(img, BRASS_LIGHT, rim.astype(np.float32))
    save(name, img, folder)


# ---------- Heavier parts (docs/ui/UI_TOKENS.md, v2) ----------

INK_DARK = hexc('0C0806')
BRASS_HI, BRASS_LO = hexc('F2D28A'), hexc('5E4318')


def brass_band(across, v):
    """Brass seen across a bevelled band (0 outer edge .. 1 inner edge), lit from above (v 0 top .. 1 bottom)."""
    col = np.where((across < 0.3)[..., None], lerp(BRASS_HI, BRASS, across / 0.3),
                   lerp(BRASS, BRASS_LO, (across - 0.3) / 0.7))
    shade = (1.12 - 0.32 * v)[..., None]
    out = col.copy()
    out[..., :3] = np.clip(col[..., :3] * shade, 0, 1)
    return out


def framed(w, h, radius, outline, rim, inner_line, interior, folder, name, gloss=0.0, lip=0, well=False):
    """The shared heavy frame: ink outline, bevelled brass rim, ink line, then the interior (a function of x, y)."""
    x, y = grid(w, h)
    img = np.zeros(x.shape + (4,), np.float32)
    sd = rounded_rect_sd(x, y, 0, 0, w, h, radius)
    depth = -sd
    v = y / h
    paint(img, np.broadcast_to(INK_DARK, x.shape + (4,)).copy(), smooth(sd))
    across = np.clip((depth - outline) / rim, 0, 1)
    paint(img, brass_band(across, v), smooth(sd + outline))
    inset = outline + rim
    paint(img, np.broadcast_to(INK_DARK, x.shape + (4,)).copy(), smooth(sd + inset))
    body_sd = rounded_rect_sd(x, y, inset + inner_line, inset + inner_line, w - inset - inner_line, h - inset - inner_line - lip, max(radius - inset, 3))
    paint(img, interior(x, y), smooth(body_sd))
    if lip:
        lip_sd = rounded_rect_sd(x, y, inset + inner_line, h - inset - inner_line - lip - 2, w - inset - inner_line, h - inset - inner_line, max(radius - inset, 3))
        over(img, hexc('000000', 0.38), smooth(lip_sd) * (1 - smooth(body_sd)))
    inner_depth = -body_sd
    shadow_strength = 0.75 if well else 0.42
    over(img, hexc('000000', shadow_strength), smooth(body_sd) * np.clip(1 - inner_depth / (9 if well else 6), 0, 1) * (y < h * 0.6))
    if gloss:
        g = smooth(rounded_rect_sd(x, y, inset + 5, inset + 4, w - inset - 5, inset + (h - 2 * inset) * 0.45, max(radius - inset - 2, 2)))
        over(img, hexc('FFFFFF', gloss), g * smooth(body_sd))
    save(name, img, folder)


def vertical(top, bottom, y0, y1):
    return lambda x, y: lerp(top, bottom, (y - y0) / max(y1 - y0, 1))


def heavy_ring(size, folder, name, rivets=0, band=0.2):
    """A thick medallion ring: ink outline, bevelled brass, ink inner line; optional rivets."""
    x, y = grid(size, size)
    c = size / 2
    dx, dy = x - c, y - c
    r = np.sqrt(dx * dx + dy * dy)
    img = np.zeros(x.shape + (4,), np.float32)
    outer = c - 1
    ink = max(3.0, size * 0.016)
    width = size * band
    inner = outer - ink - width
    paint(img, np.broadcast_to(INK_DARK, x.shape + (4,)).copy(), smooth(r - outer) * (1 - smooth(r - inner + ink)))
    across = np.clip((outer - ink - r) / width, 0, 1)
    v = (y - (c - outer)) / (2 * outer)
    # Top-left light across the whole ring, on top of the bevel.
    lit = np.clip(0.5 - (dx * 0.6 + dy * 0.8) / outer * 0.5, 0, 1)
    metal = brass_band(across, 1 - lit)
    paint(img, metal, smooth(r - (outer - ink)) * (1 - smooth(r - inner)))
    for i in range(rivets):
        angle = i * 2 * math.pi / rivets + math.pi / rivets
        mid = inner + width / 2
        rx, ry = c + mid * math.cos(angle), c + mid * math.sin(angle)
        d = np.sqrt((x - rx) ** 2 + (y - ry) ** 2)
        rr = width * 0.17
        over(img, INK_DARK, smooth(d - rr - 1.2))
        paint(img, lerp(BRASS_HI, BRASS_LO, np.clip(((x - rx) * 0.6 + (y - ry) * 0.8) / rr * 0.5 + 0.5, 0, 1)), smooth(d - rr))
    save(name, img, folder)


def halo(size, folder, name):
    """A bright gold ring with a soft outer glow, for a selected round button."""
    x, y = grid(size, size)
    c = size / 2
    r = np.sqrt((x - c) ** 2 + (y - c) ** 2)
    img = np.zeros(x.shape + (4,), np.float32)
    over(img, hexc('FFD978', 0.65), np.clip(1 - np.abs(r - (c - 10)) / 10, 0, 1) ** 1.5)
    over(img, hexc('FFF1C2'), np.clip(1 - np.abs(r - (c - 10)) / 2.5, 0, 1))
    save(name, img, folder)


def soft_shadow(folder, name, size=96, radius=20, blur=14):
    x, y = grid(size, size)
    img = np.zeros(x.shape + (4,), np.float32)
    sd = rounded_rect_sd(x, y, blur, blur, size - blur, size - blur, radius)
    over(img, hexc('000000'), np.clip(0.5 - sd / blur, 0, 1) ** 1.6)
    save(name, img, folder)


def gem_frame(size, thick, cap, folder, name):
    """The window frame: heavy brass with crimson gems set in square caps at the corners."""
    x, y = grid(size, size)
    img = np.zeros(x.shape + (4,), np.float32)
    sd = rounded_rect_sd(x, y, 0, 0, size, size, 12)
    depth = -sd
    v = y / size
    paint(img, np.broadcast_to(INK_DARK, x.shape + (4,)).copy(), smooth(sd))
    across = np.clip((depth - 4) / thick, 0, 1)
    paint(img, brass_band(across, v), smooth(sd + 4))
    paint(img, np.broadcast_to(INK_DARK, x.shape + (4,)).copy(), smooth(sd + 4 + thick))
    body = smooth(sd + 7 + thick)
    paint(img, lerp(LACQUER_LIGHT, LACQUER_DEEP, np.clip(v * 1.3, 0, 1)), body)
    over(img, hexc('000000', 0.55), body * np.clip(1 - (depth - 7 - thick) / 12, 0, 1))
    for cx, cy in ((cap / 2, cap / 2), (size - cap / 2, cap / 2), (cap / 2, size - cap / 2), (size - cap / 2, size - cap / 2)):
        box = rounded_rect_sd(x, y, cx - cap / 2, cy - cap / 2, cx + cap / 2, cy + cap / 2, 7)
        paint(img, np.broadcast_to(INK_DARK, x.shape + (4,)).copy(), smooth(box))
        inner_box = rounded_rect_sd(x, y, cx - cap / 2 + 3, cy - cap / 2 + 3, cx + cap / 2 - 3, cy + cap / 2 - 3, 5)
        paint(img, brass_band(np.clip(-inner_box / 8, 0, 1), np.clip((y - (cy - cap / 2)) / cap, 0, 1)), smooth(inner_box))
        gem = [(cx, cy - cap * 0.26), (cx + cap * 0.26, cy), (cx, cy + cap * 0.26), (cx - cap * 0.26, cy)]
        gm = poly_mask(x, y, gem)
        over(img, INK_DARK, dilate(gm, 2 * SS))
        paint(img, lerp(CRIMSON_LIGHT * 1.15, CRIMSON_DARK, np.clip((y - (cy - cap * 0.26)) / (cap * 0.52), 0, 1)), gm)
        over(img, hexc('FFFFFF', 0.55), poly_mask(x, y, [(cx, cy - cap * 0.2), (cx + cap * 0.1, cy - cap * 0.05), (cx - cap * 0.1, cy - cap * 0.05)]))
    save(name, img, folder)


def heavy_kit(folder):
    navy = vertical(LACQUER_LIGHT, LACQUER_DEEP, 10, 74)
    framed(128, 84, 16, 3, 9, 2, navy, folder, 'plate_s30', gloss=0.10)
    framed(128, 84, 16, 3, 9, 2, vertical(hexc('2A52A0'), hexc('0D1D42'), 10, 74), folder, 'counter_s30', gloss=0.16)
    framed(160, 200, 14, 3, 10, 2, vertical(hexc('22305A'), hexc('0E1530'), 14, 186), folder, 'card_s34', gloss=0.06)
    framed(96, 96, 10, 2, 3, 1, vertical(hexc('0B1020'), hexc('1A2546'), 6, 90), folder, 'well_s18', well=True)
    framed(160, 70, 14, 3, 5, 2, vertical(LACQUER_LIGHT * 1.45, LACQUER_DEEP, 10, 62), folder, 'btn_lacquer_s24', gloss=0.20, lip=7)
    framed(160, 70, 14, 3, 5, 2, vertical(GOLD_TOP, hexc('8A5E22'), 10, 62), folder, 'btn_gold_s24', gloss=0.28, lip=7)
    framed(160, 70, 14, 3, 5, 2, vertical(hexc('D2503F'), hexc('4A110E'), 10, 62), folder, 'btn_crimson_s24', gloss=0.22, lip=7)
    framed(160, 70, 14, 3, 5, 2, vertical(hexc('5A5A62'), hexc('1C1C21'), 10, 62), folder, 'btn_charcoal_s24', gloss=0.10, lip=7)
    gem_frame(208, 20, 40, folder, 'frame_s80')
    soft_shadow(folder, 'shadow_s32')
    heavy_ring(320, folder, 'medal_ring_attack', rivets=10, band=0.13)
    heavy_ring(256, folder, 'medal_ring', band=0.13)
    heavy_ring(256, folder, 'medal_ring_thin', band=0.09)
    halo(288, folder, 'medal_halo')


def main(folder):
    main_v1(folder)
    heavy_kit(folder)
    print('HEAVY_KIT_DONE')


def main_v1(folder):
    ribbon(folder, 'ribbon')
    icon('lock', [(lambda x, y: np.clip(circle_mask(x, y, 64, 52, 26) - circle_mask(x, y, 64, 52, 14), 0, 1) * (y < 60), hexc('C9BFA8')),
                  (lambda x, y: (rounded_rect_sd(x, y, 30, 54, 98, 112, 8) <= 0).astype(np.float32), BRASS_LIGHT),
                  (lambda x, y: circle_mask(x, y, 64, 78, 8), hexc('3A2810'))], folder)
    icon('back', [(lambda x, y: poly_mask(x, y, [(16, 64), (60, 20), (60, 46), (110, 46), (110, 82), (60, 82), (60, 108)]), IVORY)], folder)
    main_kit(folder)


def main_kit(folder):
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
    pill(48, 16, hexc('05080F'), BRASS_DARK, folder, 'track_s8')
    pill(48, 16, hexc('FFFFFF'), hexc('FFFFFF'), folder, 'fill_s8')
    build_icons(folder)
    print('UI_KIT_DONE')


if __name__ == '__main__':
    main(sys.argv[sys.argv.index('--') + 1])
