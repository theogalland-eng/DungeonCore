"""
Archer, mage et soigneuse sur les 9 paliers d'armure, dérivés des épéistes craftpix (mêmes proportions, mêmes
animations). On travaille calque par calque (ombre, épée dans le dos, corps, tête, épée, traînée...) :
  - les étoffes colorées prennent la couleur du rôle ; métal, or des paliers lourds, peau et yeux sont conservés ;
  - l'épée est remplacée, image par image, par l'arme du rôle (arc, bâton à orbe) orientée comme elle ;
    les images où l'épée n'est qu'une traînée de mouvement sont simplement teintées à la couleur du rôle ;
  - mage et soigneuse changent de cheveux (paliers 1-3) ; le mage porte un chapeau pointu quand la tête est nue ;
  - les paliers 4-9 utilisent les attaques « normales » (sans l'onde dorée du guerrier).
Sortie : Assets/imports/Roles/Parts/<rôle>_<n>/<rôle>_<n>_<Anim>_<calque>.png, assemblés ensuite en 8 directions
par Tools~/make_8dir.py (personnages adv_<rôle>_<n>).
Usage : python "Tools~/make_roles.py" [rôle...]
"""
import colorsys
import importlib.util
import math
import os
import sys

import numpy as np
from PIL import Image

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
OUT = os.path.join(ROOT, 'Assets', 'imports', 'Roles', 'Parts')
F = 64
_spec = importlib.util.spec_from_file_location('m8', os.path.join(ROOT, 'Tools~', 'make_8dir.py'))
M8 = importlib.util.module_from_spec(_spec)
_spec.loader.exec_module(M8)

ANIMS = ['Idle', 'Walk', 'Run', 'attack', 'Walk_Attack', 'Run_Attack', 'Hurt', 'Death']
WEAPON_LAYERS = ('sword', 'sword_back')

# couleurs de peau et des yeux (jamais recolorées)
SKIN = [(225, 178, 110), (190, 134, 95), (164, 111, 89), (246, 202, 116), (121, 80, 72), (85, 45, 36), (90, 50, 41)]
EYES = {(63, 106, 212), (55, 74, 143), (210, 221, 232), (17, 11, 0)}

ROLES = {
    'archer': dict(
        tunic=[(18, 34, 22), (34, 66, 36), (58, 104, 50), (104, 150, 72)],
        accent=[(44, 26, 18), (88, 54, 32), (138, 94, 56)],
        legs=[(30, 22, 18), (58, 42, 30), (88, 66, 44)],
        hair=None, weapon='bow', swing=(196, 230, 160)),
    'mage': dict(
        tunic=[(24, 16, 44), (50, 32, 96), (86, 58, 158), (138, 112, 214)],
        accent=[(70, 40, 18), (150, 104, 30), (226, 184, 72)],
        legs=[(22, 14, 40), (44, 28, 84), (74, 50, 136)],
        hair=[(70, 70, 86), (130, 130, 150), (188, 188, 204), (232, 232, 240)], hat=True,
        weapon='staff', orb=[(40, 90, 200), (90, 190, 255), (220, 250, 255)], swing=(170, 120, 255)),
    'healer': dict(
        tunic=[(96, 88, 84), (178, 168, 154), (228, 222, 206), (252, 249, 238)],
        accent=[(112, 74, 18), (196, 146, 38), (248, 212, 96)],
        legs=[(120, 108, 96), (190, 178, 160), (232, 224, 206)],
        hair=[(96, 62, 22), (170, 122, 48), (222, 182, 88), (250, 232, 150)],
        weapon='staff', orb=[(170, 120, 20), (250, 210, 80), (255, 250, 210)], swing=(255, 220, 120)),
}


def ramp(stops, t):
    t = min(max(t, 0.0), 1.0) * (len(stops) - 1)
    i = min(int(t), len(stops) - 2)
    k = t - i
    a, b = stops[i], stops[i + 1]
    return tuple(int(round(a[j] + (b[j] - a[j]) * k)) for j in range(3))


def lum(c):
    return (0.3 * c[0] + 0.59 * c[1] + 0.11 * c[2]) / 255.0


def is_skin(c):
    return c in EYES or any(abs(c[0] - s[0]) + abs(c[1] - s[1]) + abs(c[2] - s[2]) < 18 for s in SKIN)


# ─────────────────────────────────────────────
# Recoloration
# ─────────────────────────────────────────────

def recolor_cloth(im, role, tier):
    """Étoffes colorées → couleurs du rôle. Métal (peu saturé), or (paliers 7+), peau et contours restent."""
    a = np.array(im)
    out = a.copy()
    ys, xs = np.nonzero(a[:, :, 3] > 0)
    cache = {}
    for y, x in zip(ys, xs):
        c = tuple(int(v) for v in a[y, x, :3])
        if c not in cache:
            cache[c] = cloth_color(c, role, tier)
        n = cache[c]
        if n is not None:
            out[y, x, :3] = n
    return Image.fromarray(out)


def cloth_color(c, role, tier):
    if is_skin(c):
        return None
    hh, ss, vv = colorsys.rgb_to_hsv(c[0] / 255, c[1] / 255, c[2] / 255)
    if vv < 0.17:
        return ramp(role['tunic'], 0.0) if tier <= 3 else None     # contours de toile teintés, contours d'armure gardés
    if tier >= 4 and ss < 0.28:
        return None                                                 # métal
    if tier >= 7 and 0.08 < hh < 0.17 and ss > 0.5 and vv > 0.55:
        return None                                                 # dorures des armures lourdes
    L = lum(c)
    if 0.55 < hh < 0.85:
        return ramp(role['legs'], (L - 0.15) / 0.3)
    if hh < 0.04 or hh > 0.95:
        return ramp(role['accent'], (L - 0.12) / 0.3)
    return ramp(role['tunic'], (L - 0.12) / 0.4)


def recolor_hair(im, stops):
    a = np.array(im)
    out = a.copy()
    ys, xs = np.nonzero(a[:, :, 3] > 0)
    for y, x in zip(ys, xs):
        c = tuple(int(v) for v in a[y, x, :3])
        if is_skin(c):
            continue
        out[y, x, :3] = ramp(stops, (lum(c) - 0.08) / 0.42)
    return Image.fromarray(out)


def tint(im, col):
    a = np.array(im)
    m = a[:, :, 3] > 0
    a[m, 0], a[m, 1], a[m, 2] = col
    return Image.fromarray(a)


# ─────────────────────────────────────────────
# Armes
# ─────────────────────────────────────────────

def cell_points(im, r, c):
    a = np.array(im.crop((c * F, r * F, (c + 1) * F, (r + 1) * F)))[:, :, 3]
    ys, xs = np.nonzero(a > 0)
    return list(zip(xs.tolist(), ys.tolist()))


def put(px, x, y, col, w, h):
    if 0 <= x < w and 0 <= y < h:
        px[x, y] = col + (255,)


def line(px, p0, p1, col, w, h):
    n = int(max(abs(p1[0] - p0[0]), abs(p1[1] - p0[1]))) + 1
    for i in range(n + 1):
        t = i / max(1, n)
        put(px, int(round(p0[0] + (p1[0] - p0[0]) * t)), int(round(p0[1] + (p1[1] - p0[1]) * t)), col, w, h)


def draw_weapon(canvas, cell, pts, body_c, role):
    """Remplace l'épée (pixels pts dans la case) par l'arme du rôle, orientée comme elle."""
    if len(pts) < 2:
        return
    w, h = canvas.size
    px = canvas.load()
    ox, oy = cell
    hilt = min(pts, key=lambda p: (p[0] - body_c[0]) ** 2 + (p[1] - body_c[1]) ** 2)
    tip = max(pts, key=lambda p: (p[0] - hilt[0]) ** 2 + (p[1] - hilt[1]) ** 2)
    dx, dy = tip[0] - hilt[0], tip[1] - hilt[1]
    ln = math.hypot(dx, dy) or 1.0
    ux, uy = dx / ln, dy / ln
    H = (ox + hilt[0], oy + hilt[1])
    if role['weapon'] == 'staff':
        wood = [(52, 32, 20), (104, 68, 38), (150, 104, 60)] if role is not ROLES['healer'] else [(110, 76, 24), (190, 146, 60), (236, 204, 120)]
        a = (H[0] - ux * 3, H[1] - uy * 3)
        b = (H[0] + ux * (ln + 3), H[1] + uy * (ln + 3))
        line(px, (a[0] + 1, a[1]), (b[0] + 1, b[1]), wood[0], w, h)
        line(px, a, b, wood[1], w, h)
        put(px, int(round(b[0])), int(round(b[1])), wood[2], w, h)
        orb = role['orb']
        cx, cy = int(round(b[0] + ux * 1.5)), int(round(b[1] + uy * 1.5))
        for ddx in range(-1, 2):
            for ddy in range(-1, 2):
                put(px, cx + ddx, cy + ddy, orb[0] if abs(ddx) + abs(ddy) == 2 else orb[1], w, h)
        put(px, cx - 1, cy - 1, orb[2], w, h)
    else:
        nx, ny = -uy, ux
        R, bend = 5, 2.5
        prev = None
        ends = []
        for i in range(-R, R + 1):
            t = i / R
            off = bend * (1 - t * t)
            p = (H[0] + nx * i + ux * off, H[1] + ny * i + uy * off)
            q = (int(round(p[0])), int(round(p[1])))
            if prev is not None:
                line(px, prev, q, (92, 58, 32), w, h)
            prev = q
            if i in (-R, R):
                ends.append(q)
        line(px, ends[0], ends[1], (214, 206, 186), w, h)
        for q in ends:
            put(px, q[0], q[1], (54, 34, 20), w, h)


def weapon_layer(src, body, role):
    """Calque d'arme du rôle à partir du calque d'épée (traînées teintées, épées remplacées)."""
    W, Hh = src.size
    out = Image.new('RGBA', (W, Hh))
    rows, cols = Hh // F, W // F
    for r in range(rows):
        counts = [len(cell_points(src, r, c)) for c in range(cols)]
        nz = sorted(n for n in counts if n > 0)
        med = nz[len(nz) // 2] if nz else 0
        for c in range(cols):
            pts = cell_points(src, r, c)
            if not pts:
                continue
            if med and len(pts) > 1.8 * med and len(pts) > 40:
                # traînée de mouvement : teintée à la couleur du rôle
                piece = tint(src.crop((c * F, r * F, (c + 1) * F, (r + 1) * F)), role['swing'])
                out.alpha_composite(piece, (c * F, r * F))
                continue
            bp = cell_points(body, r, c) if body is not None else []
            bc = (sum(p[0] for p in bp) / len(bp), sum(p[1] for p in bp) / len(bp)) if bp else (32, 40)
            draw_weapon(out, (c * F, r * F), pts, bc, role)
    return out


def draw_hat(canvas, head_pts, cell, role):
    """Chapeau pointu de mage sur le haut de la tête."""
    if not head_pts:
        return
    w, h = canvas.size
    px = canvas.load()
    ox, oy = cell
    top = min(p[1] for p in head_pts)
    xs = [p[0] for p in head_pts if p[1] <= top + 2]
    cx = (min(xs) + max(xs)) // 2 if xs else head_pts[0][0]
    allx = [p[0] for p in head_pts]
    half = max(4, (max(allx) - min(allx)) // 2 + 1)
    stops = role['tunic']
    brim_y = top + 2
    for x in range(cx - half - 1, cx + half + 2):
        put(px, ox + x, oy + brim_y, stops[0], w, h)
        put(px, ox + x, oy + brim_y - 1, stops[1], w, h)
    height = 8
    for k in range(height):
        y = brim_y - 2 - k
        hw = max(0, int(round((half - 1) * (1 - k / height))))
        shift = int(k * 0.35)
        for x in range(cx - hw + shift, cx + hw + 1 + shift):
            edge = x in (cx - hw + shift, cx + hw + shift)
            put(px, ox + x, oy + y, stops[0] if edge else stops[2 if x < cx + shift else 1], w, h)
    for x in range(cx - half + 1, cx + half):
        put(px, ox + x, oy + brim_y - 2, role['accent'][2], w, h)


# ─────────────────────────────────────────────
# Construction
# ─────────────────────────────────────────────

def source_layers(n, anim):
    """Calques de l'épéiste n pour une animation (variante « normale » des attaques des paliers 4-9).
    Le pack range par erreur la blessure du niveau 2 dans le dossier du niveau 3 : on la reprend au niveau 2."""
    ch = M8.CHARACTERS[f'swordsman_{n}']
    layers = {}
    normal = {}
    for f in os.listdir(ch['parts']):
        if not f.endswith('.png') or not f.startswith(ch['prefix']):
            continue
        rest = f[len(ch['prefix']):-4]
        a = next((x for x in M8.ANIM_NAMES if rest.startswith(x + '_')), None)
        if a != anim:
            continue
        part = rest[len(a) + 1:]
        img = Image.open(os.path.join(ch['parts'], f)).convert('RGBA')
        for pre in ('normal_', 'nornal_'):
            if part.startswith(pre):
                normal[part[len(pre):]] = img
                break
        else:
            layers[part] = img
    if n >= 4 and 'body' in normal:
        layers = {k: v for k, v in layers.items() if k in ('red',)}
        layers.update(normal)
    if 'body' not in layers and n == 3:
        return source_layers(2, anim)
    return layers


def build(role_name, n):
    role = ROLES[role_name]
    cid = f'{role_name}_{n}'
    out_dir = os.path.join(OUT, cid)
    os.makedirs(out_dir, exist_ok=True)
    for anim in ANIMS:
        L = source_layers(n, anim)
        if 'body' not in L:
            continue
        body = L['body']
        res = {}
        if 'shadow' in L:
            res['shadow'] = L['shadow']
        res['body'] = recolor_cloth(body, role, n)
        if 'head' in L:
            head = L['head']
            if n <= 3 and role.get('hair'):
                hd = recolor_hair(head, role['hair'])
            else:
                hd = recolor_cloth(head, role, n)
            if role.get('hat') and n <= 3:
                hd = hd.copy()
                for r in range(head.height // F):
                    for c in range(head.width // F):
                        pts = cell_points(head, r, c)
                        draw_hat(hd, pts, (c * F, r * F), role)
            res['head'] = hd
        for wl in WEAPON_LAYERS:
            if wl in L:
                res[wl] = weapon_layer(L[wl], body, role)
        for fx in ('swing', 'aoe'):
            if fx in L:
                res[fx] = tint(L[fx], role['swing'])
        if 'red' in L:
            res['red'] = L['red']
        for part, img in res.items():
            img.save(os.path.join(out_dir, f'{cid}_{anim}_{part}.png'))
    print('ok', cid)


if __name__ == '__main__':
    roles = sys.argv[1:] or list(ROLES)
    for r in roles:
        for n in range(1, 10):
            build(r, n)
