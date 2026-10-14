"""
Archer, mage et soigneuse dessinés à partir de l'épéiste niveau 2 (mêmes proportions, même visage, mêmes
animations que les aventuriers 1-9) : on recompose ses calques (ombre, épée dans le dos, corps, tête, épée,
traînée) en recolorant la tenue et en remplaçant l'épée, image par image, par l'arme du rôle :
  - archer   : tunique vert forêt, écharpe de cuir, arc (tracé à la place de l'épée)
  - mage     : robe indigo, chapeau pointu, bâton surmonté d'un orbe bleu, traînée magique violette
  - soigneuse: robe blanche et or, cheveux blonds, bâton à l'orbe doré, traînée dorée
Sortie : Assets/imports/Roles/<rôle>_<État>_with_shadow.png (planches 64×64, rangées bas/gauche/droite/haut)
Usage : python "Tools~/make_roles.py"   puis   CharacterImporter.ImportRoles (menu DungeonCore/4)
"""
import colorsys, math, os
import numpy as np
from PIL import Image

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
BASE = os.path.join(ROOT, 'Assets', 'imports', 'craftpix-net-180537-free-swordsman-1-3-level-pixel-top-down-sprite-character',
                    'PNG', 'Swordsman_lvl2', 'Parts', 'Swordsman_lvl2_')
OUT = os.path.join(ROOT, 'Assets', 'imports', 'Roles')
STATES = {'Idle': 'Idle', 'Walk': 'Walk', 'Run': 'Run', 'Attack': 'attack', 'Hurt': 'Hurt', 'Death': 'Death'}
ORDER = ['shadow', 'sword_back', 'body', 'head', 'sword', 'swing', 'red']
F = 64

# couleurs de peau (jamais recolorées)
SKIN = {(225, 178, 110), (190, 134, 95), (164, 111, 89), (246, 202, 116), (121, 80, 72), (85, 45, 36), (90, 50, 41)}
EYES = {(63, 106, 212), (55, 74, 143), (210, 221, 232), (17, 11, 0)}

ROLES = {
    'archer': dict(
        tunic=[(18, 34, 22), (34, 66, 36), (58, 104, 50), (104, 150, 72)],
        accent=[(44, 26, 18), (88, 54, 32), (138, 94, 56)],
        legs=[(30, 22, 18), (58, 42, 30), (88, 66, 44)],
        hair=None, weapon='bow', swing=None),
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


def load(state, layer, width):
    p = BASE + state + '_' + layer + '.png'
    if not os.path.exists(p):
        return None
    im = Image.open(p).convert('RGBA')
    if im.width != width:   # quelques calques (ombre) sont plus courts
        pad = Image.new('RGBA', (width, im.height))
        pad.paste(im, (0, 0))
        im = pad
    return im


def lum(c):
    return (0.3 * c[0] + 0.59 * c[1] + 0.11 * c[2]) / 255.0


def recolor_body(im, role):
    """Tunique (bruns) → couleur du rôle, écharpe (rouges) → accent, pantalon (bleu-violet) → jambes."""
    a = np.array(im)
    out = a.copy()
    h, w = a.shape[:2]
    for y in range(h):
        for x in range(w):
            r, g, b, al = a[y, x]
            if al == 0:
                continue
            c = (int(r), int(g), int(b))
            if c in SKIN or c in EYES:
                continue
            hh, ss, vv = colorsys.rgb_to_hsv(r / 255, g / 255, b / 255)
            L = lum(c)
            if vv < 0.17:            # contours très sombres : on les assombrit dans la teinte du rôle
                out[y, x, :3] = ramp(role['tunic'], 0.0)
                continue
            if 0.55 < hh < 0.85:     # pantalon
                out[y, x, :3] = ramp(role['legs'], (L - 0.15) / 0.3)
            elif hh < 0.04 or hh > 0.95:   # écharpe rouge
                out[y, x, :3] = ramp(role['accent'], (L - 0.12) / 0.3)
            else:                    # tunique
                out[y, x, :3] = ramp(role['tunic'], (L - 0.12) / 0.4)
    return Image.fromarray(out)


def recolor_hair(im, stops):
    a = np.array(im)
    out = a.copy()
    for y in range(a.shape[0]):
        for x in range(a.shape[1]):
            r, g, b, al = a[y, x]
            if al == 0:
                continue
            c = (int(r), int(g), int(b))
            if c in SKIN or c in EYES:
                continue
            out[y, x, :3] = ramp(stops, (lum(c) - 0.08) / 0.42)
    return Image.fromarray(out)


def blobs(im):
    """Pixels opaques de chaque image (liste par case 64×64)."""
    a = np.array(im)[:, :, 3]
    cols, rows = im.width // F, im.height // F
    res = {}
    for r in range(rows):
        for c in range(cols):
            ys, xs = np.nonzero(a[r * F:(r + 1) * F, c * F:(c + 1) * F] > 0)
            res[(r, c)] = list(zip(xs.tolist(), ys.tolist()))
    return res


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
        line(px, (a[0] + 1, a[1]), (b[0] + 1, b[1]), wood[0], w, h)   # ombre du bâton
        line(px, a, b, wood[1], w, h)
        put(px, int(round(b[0])), int(round(b[1])), wood[2], w, h)
        # orbe au bout
        orb = role['orb']
        cx, cy = int(round(b[0] + ux * 1.5)), int(round(b[1] + uy * 1.5))
        for ddx in range(-1, 2):
            for ddy in range(-1, 2):
                put(px, cx + ddx, cy + ddy, orb[0] if abs(ddx) + abs(ddy) == 2 else orb[1], w, h)
        put(px, cx - 1, cy - 1, orb[2], w, h)
    else:
        # arc : courbe perpendiculaire à l'épée, centrée sur la poignée, corde claire
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
    for x in range(cx - half - 1, cx + half + 2):   # bord
        put(px, ox + x, oy + brim_y, stops[0], w, h)
        put(px, ox + x, oy + brim_y - 1, stops[1], w, h)
    height = 8
    for k in range(height):                          # cône, pointe légèrement rejetée en arrière
        y = brim_y - 2 - k
        hw = max(0, int(round((half - 1) * (1 - k / height))))
        shift = int(k * 0.35)
        for x in range(cx - hw + shift, cx + hw + 1 + shift):
            edge = x in (cx - hw + shift, cx + hw + shift)
            put(px, ox + x, oy + y, stops[0] if edge else stops[2 if x < cx + shift else 1], w, h)
    # bande dorée
    for x in range(cx - half + 1, cx + half):
        put(px, ox + x, oy + brim_y - 2, role['accent'][2], w, h)


def tint_swing(im, col):
    a = np.array(im)
    m = a[:, :, 3] > 0
    a[m, 0], a[m, 1], a[m, 2] = col
    return Image.fromarray(a)


def build(role_name):
    role = ROLES[role_name]
    os.makedirs(OUT, exist_ok=True)
    for st, src in STATES.items():
        ref = Image.open(BASE + src + '_body.png')
        W, Hh = ref.size
        layers = {l: load(src, l, W) for l in ORDER}
        body = layers['body']
        body_blobs = blobs(body)
        canvas = Image.new('RGBA', (W, Hh))
        if layers['shadow'] is not None:
            canvas = Image.alpha_composite(canvas, layers['shadow'])
        # arme dans le dos (derrière le corps)
        for key in ('sword_back',):
            lay = layers[key]
            if lay is None:
                continue
            wl = Image.new('RGBA', (W, Hh))
            for cell, pts in blobs(lay).items():
                bp = body_blobs.get(cell) or [(32, 40)]
                bc = (sum(p[0] for p in bp) / len(bp), sum(p[1] for p in bp) / len(bp))
                draw_weapon(wl, (cell[1] * F, cell[0] * F), pts, bc, role)
            canvas = Image.alpha_composite(canvas, wl)
        canvas = Image.alpha_composite(canvas, recolor_body(body, role))
        head = layers['head']
        if head is not None:
            hd = recolor_hair(head, role['hair']) if role.get('hair') else head
            if role.get('hat'):
                hd = hd.copy()
                for cell, pts in blobs(head).items():
                    draw_hat(hd, pts, (cell[1] * F, cell[0] * F), role)
            canvas = Image.alpha_composite(canvas, hd)
        lay = layers['sword']
        if lay is not None:
            wl = Image.new('RGBA', (W, Hh))
            for cell, pts in blobs(lay).items():
                bp = body_blobs.get(cell) or [(32, 40)]
                bc = (sum(p[0] for p in bp) / len(bp), sum(p[1] for p in bp) / len(bp))
                draw_weapon(wl, (cell[1] * F, cell[0] * F), pts, bc, role)
            canvas = Image.alpha_composite(canvas, wl)
        if layers['swing'] is not None and role.get('swing'):
            canvas = Image.alpha_composite(canvas, tint_swing(layers['swing'], role['swing']))
        if layers['red'] is not None:
            canvas = Image.alpha_composite(canvas, layers['red'])
        canvas.save(os.path.join(OUT, f'{role_name}_{st}_with_shadow.png'))
    print('ok', role_name)


if __name__ == '__main__':
    for r in ROLES:
        build(r)
