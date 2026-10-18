"""
Voies de rôle des monstres (stades 2 et 3) : gobelin archer, squelette mage... dérivées des calques craftpix de la
famille (mêmes proportions, mêmes animations), puis assemblées directement en 8 directions :
  - l'arme d'origine (lance, épée, traînées) est retirée ;
  - les étoffes prennent la couleur du rôle (fenêtres de teinte propres à chaque famille, peau et os conservés) ;
  - un couvre-chef facultatif (capuche, chapeau pointu) est peint sur la tête ;
  - l'arme du rôle est dessinée image par image selon la direction (arc tendu puis relâché, bâton levé...),
    derrière le corps quand le monstre regarde vers le haut.
L'attaque est refaite à partir des images de repos : bander, viser, lâcher, revenir (6 images).
Sortie : Assets/Art/Characters8/<famille>_<rôle>_<stade>/<id>_<État>.png (8 lignes S, SE, E, NE, N, NW, W, SW).
Usage : python "Tools~/make_monster_roles.py" [id...]      (sans argument : toutes les voies)
"""
import colorsys
import importlib.util
import math
import os
import sys

import numpy as np
from PIL import Image

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
OUT = os.path.join(ROOT, 'Assets', 'Art', 'Characters8')
_spec = importlib.util.spec_from_file_location('m8', os.path.join(ROOT, 'Tools~', 'make_8dir.py'))
M8 = importlib.util.module_from_spec(_spec)
_spec.loader.exec_module(M8)

DIRS = M8.DIRS
# direction de regard à l'écran (y vers le bas)
FACE = {'S': (0, 1), 'SE': (0.7071, 0.7071), 'E': (1, 0), 'NE': (0.7071, -0.7071),
        'N': (0, -1), 'NW': (-0.7071, -0.7071), 'W': (-1, 0), 'SW': (-0.7071, 0.7071)}
OUTLINE = (24, 14, 22)

WOOD = [(58, 34, 20), (112, 72, 38), (164, 116, 64)]
WOOD_DEFAULT = WOOD
STRING = (222, 214, 190)
SHAFT = (150, 112, 70)
TIP = (200, 206, 214)


def R(*hexes):
    return [tuple(int(h[i:i + 2], 16) for i in (0, 2, 4)) for h in hexes]


# Une voie : personnage de base (calques), règles de recoloration (teinte en degrés, saturation, valeur mini),
# couvre-chef et arme.
ROLES = {
    'goblin_archer_2': dict(base='goblin_2', weapon='bow', bow=5,
        body=[(190, 300, 0.05, 0.08, R('16261a', '24432a', '3d6b3a', '6a9a55'))],
        head=[(0, 50, 0.0, 0.3, R('1c3020', '2e5232', '4c7a44', '7aa860'), 0.2)]),
    'goblin_archer_3': dict(base='goblin_3', weapon='bow', bow=6, quiver=True,
        body=[(330, 360, 0.3, 0.2, R('142418', '22402a', '36603c', '5a8a52')),
              (0, 12, 0.3, 0.2, R('142418', '22402a', '36603c', '5a8a52'))],
        head=[]),
    'goblin_shaman_2': dict(base='goblin_2', weapon='staff', orb=R('a83010', 'f07a20', 'ffe0a0'),
        body=[(190, 300, 0.05, 0.08, R('2a120c', '4e2414', '7a3c1e', 'a8642e'))],
        head=[], feathers=[(200, 50, 40), (240, 236, 220), (40, 150, 140)]),
    'goblin_shaman_3': dict(base='goblin_3', weapon='staff', orb=R('a83010', 'f07a20', 'ffe0a0'), skull=True,
        body=[(330, 360, 0.3, 0.2, R('1a1030', '302050', '4c3478', '7058a8')),
              (0, 12, 0.3, 0.2, R('1a1030', '302050', '4c3478', '7058a8'))],
        head=[], feathers=[(220, 60, 40), (250, 200, 60), (240, 236, 220), (40, 150, 140), (220, 60, 40)]),
    'skeleton_archer_2': dict(base='skeleton_2', weapon='bow', bow=5, wood=R('8a8270', 'd2cebc', 'f4f0e2'),
        body=[(190, 260, 0.2, 0.1, R('16201a', '263a2c', '3e5a44', '628266'))],
        head=[(190, 260, 0.2, 0.1, R('16201a', '263a2c', '3e5a44', '628266'))]),
    'skeleton_archer_3': dict(base='skeleton_3', weapon='bow', bow=6, quiver=True, wood=R('8a8270', 'd2cebc', 'f4f0e2'),
        body=[(330, 360, 0.3, 0.1, R('0e0e12', '1e1e26', '32323e', '50505e')),
              (0, 30, 0.45, 0.1, R('0e0e12', '1e1e26', '32323e', '50505e'))],
        head=[(330, 360, 0.3, 0.1, R('0e0e12', '1e1e26', '32323e', '50505e')),
              (0, 30, 0.45, 0.1, R('0e0e12', '1e1e26', '32323e', '50505e'))]),
    'orc_berserker_2': dict(base='orc_2', weapon='keep',
        body=[], head=[(0, 32, 0.55, 0.2, R('3a0606', '7a0e0e', 'b81c1c', 'ee4a3a'))]),
    'orc_berserker_3': dict(base='orc_3', weapon='keep',
        body=[(340, 360, 0.6, 0.2, R('121014', '24202a', '3a3442', '5a5266')), (0, 12, 0.6, 0.2, R('121014', '24202a', '3a3442', '5a5266'))],
        head=[(200, 260, 0.1, 0.1, R('3a0606', '7a0e0e', 'b81c1c', 'ee4a3a'), 0.4)]),
    'orc_shaman_2': dict(base='orc_2', weapon='staff', orb=R('7a0a0a', 'd02020', 'ffb0a0'), skull=True,
        body=[(20, 50, 0.45, 0.25, R('3a0a0e', '5e1218', '8a2026', 'b83a3a'))],
        head=[(0, 32, 0.55, 0.2, R('6a6e78', 'a8acb4', 'd8dce0', 'ffffff'))]),
    'orc_shaman_3': dict(base='orc_3', weapon='staff', orb=R('7a0a0a', 'd02020', 'ffb0a0'), skull=True,
        body=[(340, 360, 0.6, 0.2, R('2a0408', '520a12', '8a1420', 'c42a32')), (0, 12, 0.6, 0.2, R('2a0408', '520a12', '8a1420', 'c42a32'))],
        head=[(200, 260, 0.1, 0.1, R('6a6e78', 'a8acb4', 'd8dce0', 'ffffff'), 0.4)],
        feathers=[(30, 20, 20), (200, 30, 40), (240, 236, 220), (200, 30, 40), (30, 20, 20)]),
    'skeleton_mage_2': dict(base='skeleton_2', weapon='staff', orb=R('2a7a3a', '5fe07a', 'd8ffe0'),
        body=[(190, 260, 0.2, 0.1, R('1e1036', '3a2066', '5e3a9a', '8c66cc'))],
        head=[(190, 260, 0.2, 0.1, R('1e1036', '3a2066', '5e3a9a', '8c66cc'))],
        hat=R('1e1036', '3a2066', '5e3a9a', 'b89aff')),
    'skeleton_mage_3': dict(base='skeleton_3', weapon='staff', orb=R('1c6a40', '4ce08c', 'e0fff0'), skull=True,
        body=[(330, 360, 0.3, 0.1, R('120a1c', '261638', '3e2658', '5e3e80')),
              (0, 30, 0.45, 0.1, R('120a1c', '261638', '3e2658', '5e3e80'))],
        head=[(330, 360, 0.3, 0.1, R('120a1c', '261638', '3e2658', '5e3e80')),
              (0, 30, 0.45, 0.1, R('120a1c', '261638', '3e2658', '5e3e80'))],
        hat=R('0e0816', '261638', '3e2658', '7cf0a8')),
}


def lum(c):
    return (0.3 * c[0] + 0.59 * c[1] + 0.11 * c[2]) / 255.0


def ramp(stops, t):
    t = min(max(t, 0.0), 1.0) * (len(stops) - 1)
    i = min(int(t), len(stops) - 2)
    k = t - i
    a, b = stops[i], stops[i + 1]
    return tuple(int(round(a[j] + (b[j] - a[j]) * k)) for j in range(3))


def match(c, rule):
    lo, hi, smin, vmin = rule[0], rule[1], rule[2], rule[3]
    smax = rule[5] if len(rule) > 5 else 1.0
    h, s, v = colorsys.rgb_to_hsv(c[0] / 255, c[1] / 255, c[2] / 255)
    return lo <= h * 360 <= hi and smin <= s <= smax and v >= vmin


def recolor(im, rules):
    """Couleurs prises par une règle → rampe de la règle, selon leur luminosité relative (contraste conservé)."""
    if not rules:
        return im
    a = np.array(im)
    ys, xs = np.nonzero(a[:, :, 3] > 0)
    cols = {tuple(int(v) for v in a[y, x, :3]) for y, x in zip(ys, xs)}
    mapping = {}
    for rule in rules:
        hit = [c for c in cols if c not in mapping and match(c, rule)]
        if not hit:
            continue
        ls = [lum(c) for c in hit]
        lo, hi = min(ls), max(ls)
        for c in hit:
            mapping[c] = ramp(rule[4], (lum(c) - lo) / (hi - lo) if hi > lo else 0.5)
    out = a.copy()
    for y, x in zip(ys, xs):
        c = tuple(int(v) for v in a[y, x, :3])
        if c in mapping:
            out[y, x, :3] = mapping[c]
    return Image.fromarray(out)


# ─────────────────────────────────────────────
# Dessin au pixel
# ─────────────────────────────────────────────

class Ink:
    def __init__(self, size):
        self.im = Image.new('RGBA', size)
        self.px = self.im.load()
        self.w, self.h = size

    def put(self, x, y, c):
        x, y = int(round(x)), int(round(y))
        if 0 <= x < self.w and 0 <= y < self.h:
            self.px[x, y] = tuple(c) + (255,)

    def line(self, a, b, c):
        n = int(max(abs(b[0] - a[0]), abs(b[1] - a[1]))) + 1
        for i in range(n + 1):
            t = i / max(1, n)
            self.put(a[0] + (b[0] - a[0]) * t, a[1] + (b[1] - a[1]) * t, c)

    def disc(self, cx, cy, r, c):
        for y in range(int(cy - r - 1), int(cy + r + 2)):
            for x in range(int(cx - r - 1), int(cx + r + 2)):
                if (x - cx) ** 2 + (y - cy) ** 2 <= r * r + 0.3:
                    self.put(x, y, c)

    def outlined(self):
        """Contour sombre d'un pixel autour de l'arme (lisible sur tous les sols)."""
        a = np.array(self.im)
        m = a[:, :, 3] > 0
        ring = np.zeros_like(m)
        for dy, dx in ((1, 0), (-1, 0), (0, 1), (0, -1)):
            ring |= np.roll(np.roll(m, dy, 0), dx, 1)
        ring &= ~m
        a[ring] = OUTLINE + (255,)
        return Image.fromarray(a)


def body_frame(frame):
    bb = frame.getbbox()
    if bb is None:
        return 32, 30, 20, 12
    x0, y0, x1, y1 = bb
    return (x0 + x1) / 2, y0, y1, x1 - x0


def torso(frame):
    """Buste : bande 55-80 % de la hauteur (les oreilles et le casque ne comptent pas) → (x0, x1, centre, haut, bas)."""
    bb = frame.getbbox()
    if bb is None:
        return 26, 38, 32, 20, 44
    top, bot = bb[1], bb[3]
    a = np.array(frame)[:, :, 3] > 0
    y0, y1 = int(top + (bot - top) * 0.55), int(top + (bot - top) * 0.8)
    xs = np.nonzero(a[y0:y1 + 1].any(axis=0))[0]
    if len(xs) == 0:
        return bb[0], bb[2], (bb[0] + bb[2]) / 2, top, bot
    return xs[0], xs[-1], (xs[0] + xs[-1]) / 2, top, bot


def draw_bow(ink, d, frame, draw, size, wood=None):
    """Arc perpendiculaire au regard ; draw ∈ [0, 1] = tension de la corde (flèche encochée si > 0)."""
    WOOD = wood or WOOD_DEFAULT
    fx, fy = FACE[d]
    nx, ny = -fy, fx
    tx0, tx1, tcx, top, bot = torso(frame)
    half = (tx1 - tx0) / 2
    ty = top + (bot - top) * 0.62
    if draw is not None:
        # arc pointé vers la cible, tenu à bout de bras juste devant le buste (draw = 0 : corde relâchée)
        hx, hy = tcx + fx * (half * abs(fx) + 2), ty + fy * 2
    else:
        # au repos : l'arc est tenu debout contre le flanc, ventre tourné vers l'extérieur
        side = nx if abs(nx) > 0.3 else (1 if fx >= 0 else -1)
        sgn = 1 if side > 0 else -1
        hx, hy = (tx1 if sgn > 0 else tx0), ty            # la poignée dans la main, au bord du buste
        fx, fy = float(sgn), 0.0
        nx, ny = 0.0, 1.0
        size = size - 1
        draw = 0.0
    bend = 2.0
    pts = []
    for i in range(-size, size + 1):
        t = i / size
        pts.append((hx + nx * i + fx * bend * (1 - t * t), hy + ny * i + fy * bend * (1 - t * t)))
    for a, b in zip(pts, pts[1:]):
        ink.line(a, b, WOOD[1])
    for p in pts[size - 1:size + 2]:
        ink.put(p[0], p[1], WOOD[2])                       # poignée claire
    e0, e1 = pts[0], pts[-1]
    pull = 3.2 * draw
    mid = (hx - fx * pull, hy - fy * pull)
    ink.line(e0, mid, STRING)
    ink.line(mid, e1, STRING)
    ink.put(e0[0], e0[1], WOOD[0]); ink.put(e1[0], e1[1], WOOD[0])
    if draw > 0.05:
        tip = (mid[0] + fx * 8, mid[1] + fy * 8)
        ink.line(mid, tip, SHAFT)
        ink.put(tip[0], tip[1], TIP)
        ink.put(mid[0], mid[1], (230, 230, 230))           # empennage


def draw_quiver(ink, d, frame):
    """Carquois porté dans le dos, en travers des épaules : les empennages dépassent à hauteur d'épaule (pas de la tête)."""
    fx, fy = FACE[d]
    tx0, tx1, tcx, top, bot = torso(frame)
    h = bot - top
    # vu de dos il est au centre ; de profil, il dépasse derrière l'épaule
    bx = tcx - fx * ((tx1 - tx0) / 2 - 1)
    lo = (bx - 1.5, top + h * 0.82)
    hi = (bx + 1.5, top + h * 0.5)
    leather = [(70, 40, 22), (110, 66, 36), (150, 98, 54)]
    ink.line((lo[0] + 1, lo[1]), (hi[0] + 1, hi[1]), leather[0])
    ink.line(lo, hi, leather[1])
    ink.put(hi[0], hi[1] + 1, leather[2])
    for k, c in enumerate(((236, 232, 220), (200, 60, 50), (236, 232, 220))):
        ink.put(hi[0] - 1 + k, hi[1] - 1 - (k % 2), c)


def draw_staff(ink, d, frame, raise_, orb, skull=False):
    """Bâton tenu à côté du corps ; raise_ ∈ [0, 1] : le bâton bascule vers la cible, l'orbe flamboie."""
    fx, fy = FACE[d]
    nx, ny = -fy, fx
    tx0, tx1, cx, top, bot = torso(frame)
    ty = top + (bot - top) * 0.62
    raise_ = raise_ or 0.0
    side = nx if abs(nx) > 0.3 else (1 if fx >= 0 else -1)
    hx, hy = (tx1 if side > 0 else tx0), ty
    L = 15
    ux, uy = 0.0, -1.0
    ux, uy = ux + fx * 0.8 * raise_, uy + fy * 0.8 * raise_ - 0.0
    ln = math.hypot(ux, uy) or 1
    ux, uy = ux / ln, uy / ln
    a = (hx - ux * 4, hy - uy * 4)
    b = (hx + ux * (L - 4), hy + uy * (L - 4))
    ink.line((a[0] + 1, a[1]), (b[0] + 1, b[1]), WOOD[0])
    ink.line(a, b, WOOD[1])
    ox, oy = b[0] + ux * 1.5, b[1] + uy * 1.5
    if skull:
        bone = [(150, 140, 120), (214, 206, 186), (246, 242, 228)]
        ink.disc(ox, oy, 2, bone[1]); ink.put(ox - 1, oy - 1, bone[2])
        ink.put(ox - 1, oy, (20, 14, 20)); ink.put(ox + 1, oy, (20, 14, 20))
        ink.put(ox - 1, oy, orb[1] if raise_ > 0.5 else (20, 14, 20)); ink.put(ox + 1, oy, orb[1] if raise_ > 0.5 else (20, 14, 20))
        if raise_ > 0.5:
            ink.disc(ox, oy - 3.5, 1.2, orb[2])
    else:
        r = 1.5 + 1.2 * raise_
        ink.disc(ox, oy, r, orb[0])
        ink.disc(ox - 0.3, oy - 0.3, r - 0.8, orb[1])
        ink.put(ox - 1, oy - 1, orb[2])
    if raise_ > 0.9:
        for k in range(6):
            ang = k * math.pi / 3 + 0.4
            ink.put(ox + math.cos(ang) * 4, oy + math.sin(ang) * 4, orb[2] if k % 2 else orb[1])


def draw_hat(frame, cx, top, w, stops):
    """Chapeau pointu sur le haut de la tête (une image déjà assemblée)."""
    ink = Ink(frame.size)
    half = max(4, int(w * 0.32))
    brim = top + 2
    for x in range(int(cx - half - 1), int(cx + half + 2)):
        ink.put(x, brim, stops[0]); ink.put(x, brim - 1, stops[1])
    for k in range(8):
        y = brim - 2 - k
        hw = max(0, int(round((half - 1) * (1 - k / 8))))
        sh = int(k * 0.35)
        for x in range(int(cx - hw + sh), int(cx + hw + 1 + sh)):
            edge = x in (int(cx - hw + sh), int(cx + hw + sh))
            ink.put(x, y, stops[0] if edge else stops[2 if x < cx + sh else 1])
    for x in range(int(cx - half + 1), int(cx + half)):
        ink.put(x, brim - 2, stops[3])
    return ink


# ─────────────────────────────────────────────
# Assemblage
# ─────────────────────────────────────────────

WEAPON_WORDS = ('spear', 'sword', 'swing', 'axe', 'club', 'bow', 'staff', 'weapon', 'mace', 'dagger')


def role_layers(ch, anim, role):
    L = M8.load_monster_layers(ch, anim)
    out = {}
    for k, v in L.items():
        if role['weapon'] != 'keep' and (k == 'back' or any(w in k for w in WEAPON_WORDS)):
            continue                                        # arme d'origine retirée (sauf voies qui la gardent)
        if k == 'body':
            v = recolor(v, role['body'])
        elif k == 'head':
            v = recolor(v, role['head'])
            if role.get('nocrest'):
                v = trim_crest(v, M8.F)
        out[k] = v
    return out


def trim_crest(im, F, cut=4):
    """Retire la crête de métal du casque (elle passait pour une flèche plantée dans la tête) :
    les `cut` rangées du haut de chaque image, puis le nouveau sommet du crâne reprend la couleur du contour."""
    a = np.array(im)
    rows, cols = a.shape[0] // F, a.shape[1] // F
    for r in range(rows):
        for c in range(cols):
            cell = a[r * F:(r + 1) * F, c * F:(c + 1) * F]
            ys = np.nonzero(cell[:, :, 3].max(axis=1) > 0)[0]
            if len(ys) < cut + 4:
                continue
            y0 = ys[0]
            cell[y0:y0 + cut, :, 3] = 0
            op = cell[:, :, 3] > 0
            if not op.any():
                continue
            lum = cell[:, :, :3].astype(int).sum(axis=2)
            lum[~op] = 9999
            oy, ox = np.unravel_index(np.argmin(lum), lum.shape)
            outline = cell[oy, ox, :3].copy()
            y1 = y0 + cut
            for x in range(F):
                if op[y1, x]:
                    cell[y1, x, :3] = outline
    return Image.fromarray(a)


def head_top(layers, d, col):
    """Haut de la tête dans l'image (pour poser le chapeau) : calque de tête de la direction du corps."""
    if 'head' not in layers:
        return None
    hd = M8.cell(layers['head'], M8.RECIPE[d][1], col % max(1, M8.row_frames(layers['head'], M8.RECIPE[d][1])))
    bb = hd.getbbox() if hd is not None else None
    return bb


def frames_for(layers, d, side_only):
    b, h = (M8.SIDE_ONLY[d], M8.SIDE_ONLY[d]) if side_only else M8.RECIPE[d]
    n = M8.frame_count(layers)
    res = []
    for c in range(n):
        shadow = M8.cell(layers.get('shadow'), b, c) if 'shadow' in layers else None
        body = M8.compose({k: v for k, v in layers.items() if k != 'shadow'}, b, h, c, with_shadow=False)
        res.append((shadow, body))
    return res


def assemble(shadow, body, weapon, back, hat=None, extra=None):
    out = Image.new('RGBA', body.size)
    if shadow is not None:
        out.alpha_composite(shadow)
    if extra is not None and extra[1]:
        out.alpha_composite(extra[0])                       # carquois derrière le corps (vu de face)
    if weapon is not None and back:
        out.alpha_composite(weapon)
    out.alpha_composite(body)
    if hat is not None:
        out.alpha_composite(hat)
    if extra is not None and not extra[1]:
        out.alpha_composite(extra[0])                       # carquois sur le dos (vu de dos)
    if weapon is not None and not back:
        out.alpha_composite(weapon)
    return out


def quiver_layer(role, d, body):
    if not role.get('quiver'):
        return None
    ink = Ink(body.size)
    draw_quiver(ink, d, body)
    return ink.outlined(), FACE[d][1] > -0.1


def weapon_ink(role, d, body, phase):
    ink = Ink(body.size)
    if role['weapon'] == 'bow':
        draw_bow(ink, d, body, phase, role.get('bow', 5), role.get('wood'))
    elif role['weapon'] == 'staff':
        draw_staff(ink, d, body, phase, role['orb'], role.get('skull', False))
    return ink.outlined()


def feathers_for(role, body):
    """Coiffe de plumes en éventail plantée au sommet du crâne."""
    cols = role.get('feathers')
    fb = body.getbbox()
    if not cols or fb is None:
        return None
    ink = Ink(body.size)
    a = np.array(body)[:, :, 3] > 0
    top = fb[1]
    xs = np.nonzero(a[top + 2])[0]
    cx = (xs[0] + xs[-1]) / 2 if len(xs) else (fb[0] + fb[2]) / 2
    n = len(cols)
    for i, c in enumerate(cols):
        t = (i - (n - 1) / 2) / max(1, (n - 1) / 2)
        ang = math.radians(90 + 38 * t)
        L = 6 - abs(t) * 1.5
        base = (cx + t * 2, top + 2)
        tip = (base[0] + math.cos(ang) * L, base[1] - math.sin(ang) * L)
        ink.line(base, tip, c)
        ink.put(tip[0], tip[1], tuple(min(255, int(v * 1.2)) for v in c))
    return ink.outlined()


def hat_for(role, layers, d, col, body):
    if role.get('feathers'):
        return feathers_for(role, body)
    stops = role.get('hat') or role.get('hood')
    if not stops:
        return None
    bb = head_top(layers, d, col)
    fb = body.getbbox()
    if bb is None or fb is None:
        return None
    # la tête recalée par compose : on prend le haut réel de l'image assemblée
    top = fb[1]
    cx = (fb[0] + fb[2]) / 2
    w = bb[2] - bb[0]
    if role.get('hat'):
        return draw_hat(body, cx, top, w, stops).outlined()
    # capuche : calotte arrondie qui couvre le haut du crâne
    ink = Ink(body.size)
    half = max(4, w * 0.45)
    for y in range(int(top - 1), int(top + 5)):
        for x in range(int(cx - half), int(cx + half + 1)):
            dx = (x - cx) / half
            dy = (y - (top + 4)) / 5.0
            if dx * dx + dy * dy <= 1.0 and body.getpixel((min(63, max(0, x)), min(63, max(0, y))))[3] > 0 or (dx * dx + dy * dy <= 0.8 and y < top + 1):
                k = 3 if dx < -0.3 and dy < -0.4 else 2 if dx < 0.2 else 1
                ink.put(x, y, stops[k])
    return ink.outlined()


ATTACK_PHASES = [0.25, 0.6, 1.0, 1.0, 0.0, 0.0]
REST = None


def build(rid):
    role = ROLES[rid]
    ch = M8.CHARACTERS[role['base']]
    M8.SRC_ROW = ch.get('rows', {'S': 0, 'W': 1, 'E': 2, 'N': 3})
    report = []
    for state, anim in M8.MON_STATES.items():
        layers = role_layers(ch, anim, role)
        if 'body' not in layers:
            continue
        M8.F = layers['body'].height // 4
        side = state in ('Hurt', 'Death') or 'head' not in layers
        keep = role['weapon'] == 'keep'
        if state in ('Attack', 'WalkAttack', 'RunAttack') and not keep:
            src = role_layers(ch, {'Attack': 'idle', 'WalkAttack': 'walk', 'RunAttack': 'run'}[state], role)
            if 'body' not in src:
                src = role_layers(ch, 'idle', role)
            layers = src
        rows = []
        for d in DIRS:
            fr = frames_for(layers, d, side)
            back = FACE[d][1] < -0.1
            row = []
            if keep:
                for k, (sh, body) in enumerate(fr):
                    hat = hat_for(role, layers, d, k, body) if state != 'Death' else None
                    row.append(assemble(sh, body, None, back, hat))
            elif state == 'Attack':
                for k, ph in enumerate(ATTACK_PHASES):
                    sh, body = fr[k % len(fr)]
                    row.append(assemble(sh, body, weapon_ink(role, d, body, ph), back, hat_for(role, layers, d, k % len(fr), body),
                                        quiver_layer(role, d, body)))
            else:
                for k, (sh, body) in enumerate(fr):
                    if state in ('WalkAttack', 'RunAttack'):
                        ph = ATTACK_PHASES[k % len(ATTACK_PHASES)]
                    else:
                        ph = REST
                    wpn = weapon_ink(role, d, body, ph) if state not in ('Hurt', 'Death') else None
                    hat = hat_for(role, layers, d, k, body) if state != 'Death' else None
                    row.append(assemble(sh, body, wpn, back, hat, quiver_layer(role, d, body) if state != 'Death' else None))
            rows.append(row)
        M8.save(rows, os.path.join(OUT, rid, f'{rid}_{state}.png'))
        report.append(f'{state}:{len(rows[0])}')
    M8.F = 64
    print(rid, ' '.join(report))


if __name__ == '__main__':
    for rid in sys.argv[1:] or list(ROLES):
        build(rid)
