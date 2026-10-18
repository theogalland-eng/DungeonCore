"""
Nouvelles familles de monstres dessinées pour le projet (aucun pack tiers) : araignées... Chaque créature est
décrite en petites formes 3D (ellipsoïdes, pattes articulées) posées dans un repère local (avant = +y), tournée
vers chacune des 8 directions puis projetée en vue de trois-quarts plongeante, comme les sprites craftpix :
  - rendu par échantillonnage de surface + tampon de profondeur (les pattes passent devant / derrière le corps) ;
  - éclairage directionnel (haut-gauche) sur une rampe de 4 teintes, motifs peints sur la surface ;
  - traits intérieurs là où deux formes se recouvrent, contour sombre teinté, ombre portée au sol ;
  - animations : repos, marche, course, attaque, blessure, mort (pose, allure et cadence propres à chaque espèce).
Sortie : Assets/Art/Characters8/<id>/<id>_<État>.png — 8 lignes (S, SE, E, NE, N, NW, W, SW), images de 64 px,
sol à y = 44 (mêmes repères que les monstres des packs).
Usage : python "Tools~/make_creatures.py" [id...]      (sans argument : toutes)
"""
import math
import os
import sys

import numpy as np
from PIL import Image

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
OUT = os.path.join(ROOT, 'Assets', 'Art', 'Characters8')
F = 64
GROUND = 44
DIRS = ['S', 'SE', 'E', 'NE', 'N', 'NW', 'W', 'SW']
# regard au sol (x droite, y vers le fond de l'écran)
GFACE = {'S': (0, -1), 'SE': (0.7071, -0.7071), 'E': (1, 0), 'NE': (0.7071, 0.7071),
         'N': (0, 1), 'NW': (-0.7071, 0.7071), 'W': (-1, 0), 'SW': (-0.7071, -0.7071)}
# caméra : plongée de trois-quarts
SY_Y, SY_Z = 0.55, 0.85          # écran y = -Y·0,55 - Z·0,85
DP_Y, DP_Z = -0.85, 0.55         # profondeur vers la caméra
LIGHT = np.array([-0.55, -0.45, 0.70])
LIGHT = LIGHT / np.linalg.norm(LIGHT)


def R(*hexes):
    return [tuple(int(h[i:i + 2], 16) for i in (0, 2, 4)) for h in hexes]


class Scene:
    """Tampon de couleur + profondeur + identifiant de forme (pour les traits intérieurs)."""

    def __init__(self):
        self.col = np.zeros((F, F, 3), np.uint8)
        self.dep = np.full((F, F), -1e9)
        self.pid = np.full((F, F), -1, np.int32)
        self.n = 0

    def splat(self, P, colors, pid):
        """P : points monde (N×3) déjà orientés ; colors : N×3."""
        sx = np.round(32 + P[:, 0]).astype(int)
        sy = np.round(GROUND - P[:, 1] * SY_Y - P[:, 2] * SY_Z).astype(int)
        d = P[:, 1] * DP_Y + P[:, 2] * DP_Z
        ok = (sx >= 0) & (sx < F) & (sy >= 0) & (sy < F)
        sx, sy, d, colors = sx[ok], sy[ok], d[ok], colors[ok]
        front = d > self.dep[sy, sx]
        sx, sy, d, colors = sx[front], sy[front], d[front], colors[front]
        o = np.argsort(d, kind='stable')            # le plus proche écrit en dernier
        sx, sy, d, colors = sx[o], sy[o], d[o], colors[o]
        self.dep[sy, sx] = d
        self.col[sy, sx] = colors
        self.pid[sy, sx] = pid

    def new_id(self):
        self.n += 1
        return self.n


def rot(P, ang):
    """Rotation autour de l'axe vertical (repère local avant = +y → regard ang)."""
    c, s = math.cos(ang), math.sin(ang)
    out = P.copy()
    out[:, 0] = P[:, 0] * c - P[:, 1] * s
    out[:, 1] = P[:, 0] * s + P[:, 1] * c
    return out


def shade_ramp(ramp, lam):
    idx = np.clip(np.round(lam * (len(ramp) - 1) - 0.1), 0, len(ramp) - 1).astype(int)
    return np.array(ramp, np.uint8)[idx]


def ellipsoid(scene, ang, center, radii, ramp, pattern=None, pitch=0.0, res=1.0):
    """Ellipsoïde (rx côté, ry avant, rz hauteur) ; pitch : bascule avant/arrière (cabré)."""
    rx, ry, rz = radii
    nu = int(max(24, 10 * max(radii) * res))
    nv = int(max(12, 5 * max(radii) * res))
    u = np.linspace(0, 2 * math.pi, nu, endpoint=False)
    v = np.linspace(-math.pi / 2, math.pi / 2, nv)
    U, V = np.meshgrid(u, v)
    U, V = U.ravel(), V.ravel()
    # coquilles imbriquées : pas de trous à l'échelle du pixel
    pts, nrm = [], []
    for k in (1.0, 0.8, 0.6):
        x = np.cos(V) * np.sin(U) * rx * k
        y = np.cos(V) * np.cos(U) * ry * k
        z = np.sin(V) * rz * k
        pts.append(np.stack([x, y, z], 1))
        n = np.stack([x / rx ** 2, y / ry ** 2, z / rz ** 2], 1)
        nrm.append(n / (np.linalg.norm(n, axis=1, keepdims=True) + 1e-9))
    L = np.concatenate(pts)
    N = np.concatenate(nrm)
    local = L.copy()                                # motifs en coordonnées propres à la forme
    if pitch:
        c, s = math.cos(pitch), math.sin(pitch)
        for A in (L, N):
            y, z = A[:, 1].copy(), A[:, 2].copy()
            A[:, 1], A[:, 2] = y * c - z * s, y * s + z * c
    L = L + np.array(center)
    P = rot(L, ang)
    Nw = rot(N, ang)
    lam = np.clip(Nw @ LIGHT, 0, 1)
    lam = lam ** 0.9
    cols = shade_ramp(ramp, lam)
    if pattern is not None:
        pattern(local, lam, cols)
    scene.splat(P, cols, scene.new_id())


def segment(scene, ang, a, b, ramp, width=0.55, pid=None):
    """Membre fin (patte) de a à b : un pixel d'épaisseur, plus clair sur le dessus."""
    a, b = np.array(a, float), np.array(b, float)
    n = int(max(6, np.linalg.norm(b - a) * 3))
    t = np.linspace(0, 1, n)[:, None]
    P = a + (b - a) * t
    offs = [np.zeros(3)]
    if width > 0.6:
        offs += [np.array([0.4, 0, 0]), np.array([-0.4, 0, 0]), np.array([0, 0, 0.4])]
    pts = np.concatenate([P + o for o in offs])
    Pw = rot(pts, ang)
    up = (b - a)[2]
    lam = np.full(len(pts), 0.55 + 0.25 * np.sign(up) * min(1, abs(up) / 3))
    cols = shade_ramp(ramp, lam)
    scene.splat(Pw, cols, pid if pid is not None else scene.new_id())


def dot(scene, ang, p, color, size=0):
    P = np.array([p], float)
    if size:
        P = np.array([[p[0] + dx * 0.5, p[1], p[2] + dz * 0.5] for dx in (-1, 0, 1) for dz in (-1, 0, 1)], float)
    scene.splat(rot(P, ang), np.array([color] * len(P), np.uint8), scene.new_id())


def finish(scene, outline, shadow_r, ang, lift=0.0):
    """Traits intérieurs, contour, ombre portée → image RGBA."""
    img = np.zeros((F, F, 4), np.uint8)
    m = scene.pid >= 0
    img[m, :3] = scene.col[m]
    img[m, 3] = 255
    # traits intérieurs : bord d'une forme posée devant une autre nettement plus loin
    dep, pid = scene.dep, scene.pid
    line = np.zeros_like(m)
    for dy, dx in ((1, 0), (0, 1), (-1, 0), (0, -1)):
        d2 = np.roll(np.roll(dep, dy, 0), dx, 1)
        p2 = np.roll(np.roll(pid, dy, 0), dx, 1)
        line |= m & (p2 >= 0) & (p2 != pid) & (dep - d2 > 2.2)
    dark = (img[..., :3].astype(int) * 0.55).astype(np.uint8)
    img[line, :3] = dark[line]
    # contour extérieur
    ring = np.zeros_like(m)
    for dy, dx in ((1, 0), (0, 1), (-1, 0), (0, -1)):
        ring |= np.roll(np.roll(m, dy, 0), dx, 1)
    ring &= ~m
    img[ring, :3] = outline
    img[ring, 3] = 255
    out = Image.fromarray(img)
    # ombre au sol
    sh = np.zeros((F, F, 4), np.uint8)
    yy, xx = np.mgrid[0:F, 0:F]
    rx, ry = shadow_r * (1 - 0.25 * lift), shadow_r * 0.45 * (1 - 0.25 * lift)
    e = ((xx + 0.5 - 32) / rx) ** 2 + ((yy + 0.5 - GROUND) / ry) ** 2 <= 1
    sh[e] = (20, 14, 24, 90)
    base = Image.fromarray(sh)
    base.alpha_composite(out)
    return base


# ─────────────────────────────────────────────
# Araignées
# ─────────────────────────────────────────────

SPIDERS = {
    'spider_1': dict(scale=1.0, body=R('241610', '4a2c1c', '7a4e30', 'a8784e'), legs=R('2a1a10', '5a3a24', '8a6040', 'b08860'),
                     eyes=(240, 70, 50), mark='stripes', markc=(196, 160, 110), outline=(20, 12, 10)),
    'spider_2': dict(scale=1.2, body=R('1c1826', '363048', '585070', '8680a2'), legs=R('221e2e', '45405a', '6e6888', '9a94b4'),
                     eyes=(120, 255, 170), mark='chevrons', markc=(232, 232, 244), outline=(14, 10, 20), silk=True),
    'spider_3': dict(scale=1.5, body=R('120a16', '28142e', '442452', '6c3e86'), legs=R('1a0e20', '3a1c48', '5a3470', '7e52a0'),
                     eyes=(255, 60, 90), mark='hourglass', markc=(230, 40, 60), outline=(10, 6, 14), crown=True),
}


def spider_pattern(kind, color):
    color = np.array(color, np.uint8)

    def pat(local, lam, cols):
        x, y, z = local[:, 0], local[:, 1], local[:, 2]
        top = z > 0.2
        if kind == 'stripes':
            m = top & (np.abs(np.sin(y * 1.6)) > 0.82)
        elif kind == 'chevrons':
            m = top & (np.abs(((y + np.abs(x) * 0.9) * 1.1) % 2.2 - 1.1) < 0.28) & (np.abs(x) < 2.6)
        else:   # sablier rouge
            m = top & (np.abs(x) < 0.35 + np.abs(y) * 0.45) & (np.abs(y) < 1.9)
        shade = np.where(lam[m] > 0.5, 1.0, 0.75)[:, None]
        cols[m] = (color[None, :] * shade).astype(np.uint8)
    return pat


def spider_frame(spec, d, state, t, n):
    """t : image courante, n : nombre d'images de l'animation."""
    s = spec['scale']
    gx, gy = GFACE[d]
    ang = math.atan2(-gx, gy)      # repère local avant +y tourné vers (gx, gy)
    sc = Scene()
    ph = 2 * math.pi * t / n
    h = 2.6 * s                     # hauteur du corps
    pitch, lunge, curl, lift, sink, legs_up = 0.0, 0.0, 0.0, 0.0, 0.0, 0.0
    stride, step_h, breathe = 0.0, 0.0, 0.0
    if state == 'Idle':
        breathe = 0.05 * math.sin(ph)
    elif state in ('Walk', 'Run'):
        stride = (1.3 if state == 'Walk' else 2.0) * s
        step_h = (1.1 if state == 'Walk' else 1.6) * s
        h += 0.3 * s * abs(math.sin(ph * 2)) if state == 'Run' else 0
    elif state == 'Attack':
        k = [0.0, 0.5, 1.0, 1.0, 0.4, 0.0][t]
        pitch = 0.45 * k if t < 3 else 0.2 * k
        lunge = [0, -0.6, -0.4, 2.2, 1.0, 0][t] * s
        legs_up = k
    elif state == 'Hurt':
        k = [1.0, 0.7, 0.35, 0.0][t]
        lunge = -1.2 * s * k
        pitch = -0.15 * k
        sink = 0.4 * k
    elif state == 'Death':
        k = t / (n - 1)
        curl = k
        sink = 1.9 * s * k
        pitch = -0.1 * k
    ceph_c = (0, 2.2 * s + lunge, h - sink + 0.6 * s * math.sin(pitch))
    abd_c = (0, -3.0 * s + lunge * 0.6, h + 0.6 * s - sink - 0.8 * s * math.sin(pitch))
    # pattes : 4 paires, attachées autour du céphalothorax
    hip_y = [1.2, 0.5, -0.2, -0.9]
    spread = [math.radians(a) for a in (55, 22, -12, -42)]
    feet = []
    for side in (-1, 1):
        for i in range(4):
            group = (i + (0 if side < 0 else 1)) % 2
            gph = ph + (math.pi if group else 0)
            a = spread[i]
            dirx, diry = side * math.cos(a), math.sin(a)
            hip = np.array([side * 1.1 * s, ceph_c[1] + hip_y[i] * s, ceph_c[2] - 0.2 * s])
            reach = (7.2 - 0.5 * abs(i - 1.5)) * s
            fx = hip[0] + dirx * reach
            fy = hip[1] + diry * reach * 1.05 + stride * math.sin(gph)
            fz = max(0.0, step_h * math.cos(gph)) if stride else 0.0
            if state == 'Idle':
                fz = 0.25 * s * max(0, math.sin(ph * 2 + i + side)) if (i + side) % 3 == 0 else 0
            if legs_up and i == 0:
                fy += 1.2 * s * legs_up
                fz += 3.0 * s * legs_up               # pattes avant levées, prêtes à frapper
            foot = np.array([fx, fy, fz])
            if curl:
                # pattes repliées sur le ventre
                foot = hip + (foot - hip) * (1 - 0.65 * curl) + np.array([0, 0, 2.5 * s * curl])
            knee = hip + (foot - hip) * 0.45 + np.array([0, 0, (3.6 + 0.5 * (i % 2)) * s * (1 - 0.5 * curl)])
            feet.append((hip, knee, foot, i))
    # ordre : tout passe par le tampon de profondeur
    pid_legs = sc.new_id()
    for hip, knee, foot, i in feet:
        segment(sc, ang, hip, knee, spec['legs'], 0.7 if s > 1.1 else 0.5, pid_legs)
        segment(sc, ang, knee, foot, spec['legs'], 0.7 if s > 1.4 else 0.5, pid_legs)
    br = 1 + breathe
    ellipsoid(sc, ang, abd_c, (2.7 * s * br, 3.3 * s * br, 2.3 * s * br), spec['body'],
              spider_pattern(spec['mark'], spec['markc']), pitch=-pitch * 0.5)
    ellipsoid(sc, ang, ceph_c, (1.8 * s, 2.0 * s, 1.4 * s), spec['body'], pitch=pitch)
    # yeux et crochets
    ex = 0.6 * s
    for side in (-1, 1):
        dot(sc, ang, (side * ex, ceph_c[1] + 1.9 * s, ceph_c[2] + 0.7 * s + 0.6 * s * math.sin(pitch)), spec['eyes'])
        dot(sc, ang, (side * ex * 1.9, ceph_c[1] + 1.5 * s, ceph_c[2] + 0.9 * s + 0.6 * s * math.sin(pitch)), spec['eyes'])
        fang = 0.5 * s if state == 'Attack' and t == 3 else 0
        segment(sc, ang, (side * 0.5 * s, ceph_c[1] + 2.0 * s, ceph_c[2] - 0.2 * s),
                (side * (0.3 * s - fang * 0.3), ceph_c[1] + 2.9 * s + fang, ceph_c[2] - 0.9 * s), [(10, 8, 8), (40, 30, 30), (200, 190, 170), (240, 236, 220)], 0.5)
    if spec.get('crown'):
        for k, sx in enumerate((-1.4, -0.5, 0.5, 1.4)):
            base = (sx * s, ceph_c[1] + 0.2 * s, ceph_c[2] + 1.2 * s)
            tip = (sx * 1.2 * s, ceph_c[1] - 0.2 * s, ceph_c[2] + (2.6 if k in (1, 2) else 2.0) * s)
            segment(sc, ang, base, tip, R('5a3a10', 'a8781e', 'e8c050', 'fff0a0'), 0.5)
    if spec.get('silk') and state in ('Idle', 'Walk', 'Run'):
        # fil de soie qui traîne derrière l'abdomen
        a = (0, abd_c[1] - 3.6 * s, abd_c[2] - 0.5 * s)
        b = (0.4 * math.sin(ph), abd_c[1] - 6.5 * s, 0.2)
        segment(sc, ang, a, b, [(150, 150, 170), (190, 190, 210), (225, 225, 240), (250, 250, 255)], 0.3)
    return finish(sc, spec['outline'], 4.2 * s + 2, ang, lift=0.0)


SPIDER_ANIMS = {'Idle': 4, 'Walk': 6, 'Run': 6, 'Attack': 6, 'Hurt': 4, 'Death': 6}


def save(rows, path):
    n = max(len(r) for r in rows)
    sheet = Image.new('RGBA', (n * F, len(rows) * F))
    for r, frames in enumerate(rows):
        for c, fr in enumerate(frames):
            sheet.alpha_composite(fr, (c * F, r * F))
    os.makedirs(os.path.dirname(path), exist_ok=True)
    sheet.save(path)


def build_spider(cid):
    spec = SPIDERS[cid]
    for state, n in SPIDER_ANIMS.items():
        rows = [[spider_frame(spec, d, state, t, n) for t in range(n)] for d in DIRS]
        save(rows, os.path.join(OUT, cid, f'{cid}_{state}.png'))
    print('ok', cid)


# ─────────────────────────────────────────────
# Mimiques : coffres qui mordent
# ─────────────────────────────────────────────

def pitch_about(P, ang, pivot_y, pivot_z):
    """Rotation autour d'un axe parallèle à x passant par (y, z) = pivot (charnière du couvercle)."""
    c, s_ = math.cos(ang), math.sin(ang)
    out = P.copy()
    y, z = P[:, 1] - pivot_y, P[:, 2] - pivot_z
    out[:, 1] = pivot_y + y * c - z * s_
    out[:, 2] = pivot_z + y * s_ + z * c
    return out


def box_points(size, step=0.3):
    """Points et normales des 6 faces d'une boîte centrée (sx côté, sy profondeur, sz hauteur)."""
    sx, sy, sz = size
    pts, nrm = [], []
    def grid(a, b):
        return np.arange(-a / 2, a / 2 + 1e-6, step), np.arange(-b / 2, b / 2 + 1e-6, step)
    for axis, half, n_sign in ((0, sx / 2, 1), (0, sx / 2, -1), (1, sy / 2, 1), (1, sy / 2, -1), (2, sz / 2, 1), (2, sz / 2, -1)):
        others = [i for i in range(3) if i != axis]
        u, v = grid(size[others[0]], size[others[1]])
        U, V = np.meshgrid(u, v)
        P = np.zeros((U.size, 3))
        P[:, axis] = half * n_sign
        P[:, others[0]] = U.ravel()
        P[:, others[1]] = V.ravel()
        N = np.zeros_like(P)
        N[:, axis] = n_sign
        pts.append(P); nrm.append(N)
    return np.concatenate(pts), np.concatenate(nrm)


def lid_points(w, d, h, step=0.3):
    """Couvercle bombé de coffre : demi-cylindre aplati (axe x), deux joues en demi-disque et le dessous plat."""
    r = d / 2
    th = np.linspace(0, math.pi, int(max(14, math.pi * r / step * 1.5)))
    xs = np.arange(-w / 2, w / 2 + 1e-6, step)
    TH, X = np.meshgrid(th, xs)
    TH, X = TH.ravel(), X.ravel()
    P = np.stack([X, r * np.cos(TH), h * np.sin(TH)], 1)
    N = np.stack([np.zeros_like(X), np.cos(TH) / r, np.sin(TH) / h], 1)
    pts, nrm = [P], [N]
    for sgn in (-1, 1):
        for k in np.arange(0, 1.0001, step / max(r, 0.1)):
            pts.append(np.stack([np.full_like(th, sgn * w / 2), k * r * np.cos(th), k * h * np.sin(th)], 1))
            nrm.append(np.tile([sgn, 0.0, 0.0], (len(th), 1)))
    gx_, gy_ = np.meshgrid(xs, np.arange(-r, r + 1e-6, step))
    pts.append(np.stack([gx_.ravel(), gy_.ravel(), np.zeros(gx_.size)], 1))
    nrm.append(np.tile([0.0, 0.0, -1.0], (gx_.size, 1)))
    N = np.concatenate(nrm)
    N = N / (np.linalg.norm(N, axis=1, keepdims=True) + 1e-9)
    return np.concatenate(pts), N


def solid(scene, ang, P, N, base_cols, pid=None):
    Pw, Nw = rot(P, ang), rot(N, ang)
    lam = np.clip(Nw @ LIGHT, 0, 1)
    cols = base_cols(P, N, lam)
    scene.splat(Pw, cols, pid if pid is not None else scene.new_id())


MIMICS = {
    'mimic_1': dict(scale=1.25, wood=R('3a2212', '5e3a1e', '8a5a30', 'b07a44'), band=R('2a2a30', '4a4a54', '7a7a86', 'b0b0bc'),
                    lock=R('6a4a10', 'a8801e', 'e0b840', 'fff0a0'), mouth=(46, 8, 14), tongue=R('5a0e1a', '9a1e2e', 'd04050', 'f08090'),
                    eye=(255, 220, 90), outline=(22, 12, 8)),
    'mimic_2': dict(scale=1.5, wood=R('2a160e', '4a2616', '723c22', '9a5832'), band=R('22222a', '3e3e4a', '6a6a78', 'a0a0b0'),
                    lock=R('6a4a10', 'a8801e', 'e0b840', 'fff0a0'), mouth=(30, 4, 14), tongue=R('3e0a3a', '74185e', 'b03a8a', 'e080c0'),
                    eye=(160, 255, 120), outline=(16, 8, 6), teeth_big=True),
    'mimic_3': dict(scale=1.8, wood=R('3a0a10', '6a1420', '9a2430', 'c84448'), band=R('6a4a10', 'a8801e', 'e0b840', 'fff0a0'),
                    lock=R('1a3a6a', '2a6ab0', '58a8f0', 'c0e8ff'), mouth=(20, 2, 10), tongue=R('3e0a3a', '74185e', 'b03a8a', 'e080c0'),
                    eye=(255, 80, 60), outline=(18, 6, 6), teeth_big=True, crown=True, gems=True),
}


def mimic_frame(spec, d, state, t, n):
    s = spec['scale']
    gx, gy = GFACE[d]
    ang = math.atan2(-gx, gy)
    sc = Scene()
    W, D, H, LH = 8.0 * s, 5.0 * s, 3.6 * s, 2.0 * s
    lid, hop, lunge, tilt, roll = 0.0, 0.0, 0.0, 0.0, 0.0
    coins = 0
    if state == 'Idle':
        lid = [0.0, 0.0, 0.24, 0.0][t]                      # entrouvert un instant : un œil luit
    elif state in ('Walk', 'Run'):
        ph = t / n
        amp = 2.2 if state == 'Walk' else 3.2
        hop = amp * s * math.sin(math.pi * ph)
        lid = 0.25 * math.sin(math.pi * ph) + (0.15 if ph < 0.2 else 0)
    elif state == 'Attack':
        lid = [0.3, 0.95, 1.25, 0.15, 0.35, 0.05][t]
        lunge = [0, -0.5, -0.3, 2.4, 1.2, 0][t] * s
        hop = [0, 0.4, 1.2, 0.2, 0, 0][t] * s
    elif state == 'Hurt':
        lid = [0.5, 0.3, 0.15, 0.0][t]
        tilt = [-0.3, -0.2, -0.08, 0][t]
    elif state == 'Death':
        k = t / (n - 1)
        lid = 0.3 + 1.4 * k
        roll = 0.9 * k
        coins = int(k * 7)
    z0 = hop
    y0 = lunge

    def wood_cols(P, N, lam, part):
        cols = shade_ramp(spec['wood'], lam)
        x, y, z = P[:, 0], P[:, 1], P[:, 2]
        # ferrures : coins et bande centrale verticale
        side = np.abs(N[:, 0]) > 0.5
        band = ~side & ((np.abs(np.abs(x) - W / 2) < 0.75 * s) | (np.abs(x) < 0.5 * s))
        band |= side & (np.abs(np.abs(y) - D / 2) < 0.75 * s)
        if part == 'base':
            band |= (z < 0.7 * s)
        cols[band] = shade_ramp(spec['band'], lam[band])
        # planches : une ligne sombre toutes les 1,4 unité de hauteur sur les flancs
        plank = (np.abs(N[:, 2]) < 0.5) & ~band & (np.abs(((z if part == 'base' else z + 7) / (1.4 * s)) % 1) < 0.14)
        cols[plank] = (cols[plank].astype(int) * 0.7).astype(np.uint8)
        if spec.get('gems'):
            gem = band & (np.abs(x) < 0.5 * s) & (N[:, 2] > 0.5)
            cols[gem] = shade_ramp(spec['lock'], lam[gem])
        return cols

    # caisse
    P, N = box_points((W, D, H))
    P[:, 2] += H / 2
    mouth_top = (N[:, 2] > 0.5)
    def base_cols(P_, N_, lam):
        c = wood_cols(P_, N_, lam, 'base')
        c[mouth_top] = spec['mouth']                          # intérieur sombre : la gueule
        return c
    P = pitch_about(P, tilt, -D / 2, 0)
    if roll:
        c, s_ = math.cos(roll), math.sin(roll)
        x, z = P[:, 0].copy(), P[:, 2].copy()
        P[:, 0], P[:, 2] = x * c - z * s_ + W / 2 * s_ * 0.3, x * s_ + z * c
    Pb = P + np.array([0, y0, z0])
    solid(sc, ang, Pb, N, base_cols)
    # langue (gueule ouverte)
    if lid > 0.35:
        ellipsoid(sc, ang, (0, y0 + 0.4 * s, z0 + H - 0.3 * s), (2.0 * s, 2.0 * s, 0.6 * s), spec['tongue'])
    # dents : sur le bord avant de la caisse
    if lid > 0.1:
        nt = 6
        for i in range(nt):
            x = -W / 2 + 0.9 * s + i * (W - 1.8 * s) / (nt - 1)
            ln = (0.9 if spec.get('teeth_big') else 0.6) * s
            segment(sc, ang, (x, y0 + D / 2 - 0.3, z0 + H), (x, y0 + D / 2 - 0.3, z0 + H + ln),
                    [(170, 160, 140), (214, 206, 186), (240, 236, 224), (255, 252, 244)], 0.5)
    # couvercle articulé à l'arrière
    Pl, Nl = lid_points(W + 0.4 * s, D + 0.4 * s, LH)
    Pl[:, 2] += H
    Pl = pitch_about(Pl, -lid, -D / 2 - 0.2 * s, H)
    Nl = pitch_about(Nl, -lid, 0, 0)
    Pl = pitch_about(Pl, tilt, -D / 2, 0)
    if roll:
        c, s_ = math.cos(roll), math.sin(roll)
        x, z = Pl[:, 0].copy(), Pl[:, 2].copy()
        Pl[:, 0], Pl[:, 2] = x * c - z * s_ + W / 2 * s_ * 0.3, x * s_ + z * c
    Pl = Pl + np.array([0, y0, z0])
    lid_under = Nl[:, 2] < -0.5
    def lid_cols(P_, N_, lam):
        c = wood_cols(P_ - np.array([0, y0, z0 + H]), N_, lam, 'lid')
        if lid > 0.1:
            c[lid_under] = spec['mouth']
        return c
    solid(sc, ang, Pl, Nl, lid_cols)
    # serrure (plaque dorée sur l'avant du couvercle)
    lock_p = np.array([[0, D / 2 + 0.25 * s, H + 0.3 * s]])
    lock_p = pitch_about(lock_p, -lid, -D / 2 - 0.2 * s, H) + np.array([0, y0, z0])
    for dx in (-0.5, 0, 0.5):
        for dz in (-0.5, 0):
            dot(sc, ang, (lock_p[0, 0] + dx * s, lock_p[0, 1] + 0.05, lock_p[0, 2] + dz * s), spec['lock'][2 if dz == 0 else 1])
    # yeux : deux lueurs sous le couvercle quand il s'entrouvre
    if lid > 0.08:
        for sx_ in (-1, 1):
            e = np.array([[sx_ * 1.6 * s, D / 2 + 0.1, H + 0.1 * s]])
            e = pitch_about(e, -lid * 0.6, -D / 2, H) + np.array([0, y0, z0])
            dot(sc, ang, tuple(e[0]), spec['eye'])
            dot(sc, ang, (e[0, 0] + 0.5 * sx_, e[0, 1], e[0, 2]), spec['eye'])
    if spec.get('crown'):
        for k, cx_ in enumerate((-2.2, 0, 2.2)):
            b = np.array([[cx_ * s, 0, H + LH - 0.2 * s]])
            tip = np.array([[cx_ * s, -0.3 * s, H + LH + (1.6 if k == 1 else 1.1) * s]])
            b = pitch_about(b, -lid, -D / 2 - 0.2 * s, H) + np.array([0, y0, z0])
            tip = pitch_about(tip, -lid, -D / 2 - 0.2 * s, H) + np.array([0, y0, z0])
            segment(sc, ang, tuple(b[0]), tuple(tip[0]), spec['band'], 0.7)
    for k in range(coins):
        a_ = k * 2.1
        dot(sc, ang, (math.cos(a_) * (3 + k % 3) * s, D / 2 + 2 + (k % 2) * 1.5, 0.1), (240, 200, 70) if k % 2 else (200, 160, 40))
    return finish(sc, spec['outline'], 5.0 * s + 1, ang, lift=min(1.0, hop / 4))


MIMIC_ANIMS = {'Idle': 4, 'Walk': 6, 'Run': 6, 'Attack': 6, 'Hurt': 4, 'Death': 6}


def build_mimic(cid):
    spec = MIMICS[cid]
    for state, n in MIMIC_ANIMS.items():
        rows = [[mimic_frame(spec, d, state, t, n) for t in range(n)] for d in DIRS]
        save(rows, os.path.join(OUT, cid, f'{cid}_{state}.png'))
    print('ok', cid)


# ─────────────────────────────────────────────
# Chauves-souris : vol stationnaire, ailes membraneuses
# ─────────────────────────────────────────────

def triangle_points(a, b, c, step=0.28):
    a, b, c = np.array(a, float), np.array(b, float), np.array(c, float)
    n = int(max(np.linalg.norm(b - a), np.linalg.norm(c - a), np.linalg.norm(c - b)) / step) + 2
    pts = []
    for i in range(n + 1):
        for j in range(n + 1 - i):
            u, v = i / n, j / n
            pts.append(a + (b - a) * u + (c - a) * v)
    return np.array(pts)


def flap(P, ang, side):
    """Rotation de l'aile autour de l'axe avant (y), à l'épaule (x = 0)."""
    c, s_ = math.cos(ang), math.sin(ang)
    out = P.copy()
    x, z = P[:, 0] * side, P[:, 2]
    out[:, 0] = (x * c - z * s_) * side
    out[:, 2] = x * s_ + z * c
    return out


BATS = {
    'bat_1': dict(scale=1.45, fur=R('2a1e1c', '4a3630', '6e544a', '947868'), wing=R('2a1c20', '4e383e', '7a5a60', 'a88488'),
                  eye=(255, 210, 120), outline=(16, 10, 12)),
    'bat_2': dict(scale=1.75, fur=R('1a1016', '30202a', '4a3442', '6a4e5e'), wing=R('3a0c14', '64162a', '962a3c', 'c8485a'),
                  eye=(255, 50, 50), outline=(12, 6, 10), fangs=True),
    'bat_3': dict(scale=2.0, fur=R('120a18', '24142e', '3a2248', '58386a'), wing=R('24102e', '42205a', '663488', ' 94 5ab8'.replace(' ', '')),
                  eye=(200, 120, 255), outline=(10, 6, 14), fangs=True, swarm=True),
}


def bat_body(sc, ang, spec, s, ox, oy, z, ph, mode, k=1.0):
    """Une chauve-souris complète en (ox, oy, z) ; ph = phase de battement ; mode : vol, attaque, chute."""
    sw = s * k
    lean = 0.0
    if mode == 'attack':
        lean = 0.5
    # ailes : épaule → coude → pointe, membrane en éventail
    wing_ang = {'fly': 0.55 * math.sin(ph) + 0.15, 'attack': 0.9 - 0.4 * math.sin(ph), 'fold': 1.25, 'hurt': -0.4 + 0.3 * math.sin(ph)}[mode]
    pts = {'sh': (0.7, 0.3, 0.2), 'el': (3.2, 0.9, 0.6), 'tip': (6.2, -0.2, 0.1), 'f1': (5.2, -1.9, 0), 'f2': (3.4, -2.1, 0), 'f3': (1.4, -1.3, 0)}
    if mode == 'fold':
        pts = {k_: (v[0] * 0.45, v[1] * 0.6, v[2]) for k_, v in pts.items()}
    base = np.array([ox, oy, z])
    for side in (-1, 1):
        P = [np.array(pts[n_]) * sw for n_ in ('sh', 'el', 'tip', 'f1', 'f2', 'f3')]
        tris = [(P[0], P[1], P[2]), (P[0], P[2], P[3]), (P[0], P[3], P[4]), (P[0], P[4], P[5])]
        mem = np.concatenate([triangle_points(*t) for t in tris])
        mem[:, 0] *= side
        mem = flap(mem, wing_ang, side)
        if lean:
            c_, s_ = math.cos(lean), math.sin(lean)
            y_, z_ = mem[:, 1].copy(), mem[:, 2].copy()
            mem[:, 1], mem[:, 2] = y_ * c_ - z_ * s_, y_ * s_ + z_ * c_
        mem = mem + base
        # membrane : plus claire vers le bord, nervures sombres
        Pw = rot(mem, ang)
        loc = mem - base
        r = np.hypot(loc[:, 0], loc[:, 1]) / (6 * sw)
        lam = np.clip(0.35 + 0.5 * r, 0, 1)
        cols = shade_ramp(spec['wing'], lam)
        sc.splat(Pw, cols, sc.new_id())
        for a_, b_ in (('sh', 'el'), ('el', 'tip'), ('el', 'f1'), ('el', 'f2')):
            A = np.array([pts[a_]]) * sw; B = np.array([pts[b_]]) * sw
            A[:, 0] *= side; B[:, 0] *= side
            A = flap(A, wing_ang, side)[0] + base; B = flap(B, wing_ang, side)[0] + base
            segment(sc, ang, tuple(A), tuple(B), spec['fur'], 0.5)
    ellipsoid(sc, ang, (ox, oy, z), (1.4 * sw, 1.7 * sw, 1.6 * sw), spec['fur'], pitch=lean)
    hy, hz = oy + 1.5 * sw * math.cos(lean), z + 0.9 * sw + 0.6 * sw * math.sin(lean)
    ellipsoid(sc, ang, (ox, hy, hz), (1.1 * sw, 1.0 * sw, 1.0 * sw), spec['fur'])
    for side in (-1, 1):
        segment(sc, ang, (ox + side * 0.6 * sw, hy, hz + 0.6 * sw), (ox + side * 1.0 * sw, hy - 0.2 * sw, hz + 2.0 * sw), spec['fur'], 0.6)
        dot(sc, ang, (ox + side * 0.45 * sw, hy + 0.95 * sw, hz + 0.25 * sw), spec['eye'], size=1 if sw > 1.2 else 0)
    if spec.get('fangs') and mode in ('attack', 'fly'):
        for side in (-1, 1):
            dot(sc, ang, (ox + side * 0.3 * sw, hy + 1.0 * sw, hz - 0.45 * sw), (240, 236, 224))


def bat_frame(spec, d, state, t, n):
    s = spec['scale']
    gx, gy = GFACE[d]
    ang = math.atan2(-gx, gy)
    sc = Scene()
    ph = 2 * math.pi * t / n
    z = 4.5 * s + 0.8 * math.sin(ph)
    oy, mode = 0.0, 'fly'
    if state == 'Run':
        ph *= 2
        z = 4.2 * s
    elif state == 'Attack':
        mode = 'attack'
        dive = [0, 0.3, 1.0, 1.0, 0.5, 0.1][t]
        z = 4.5 * s - 2.2 * s * dive
        oy = 2.5 * s * dive
    elif state == 'Hurt':
        mode = 'hurt'
        z = 4.5 * s - [1.2, 0.8, 0.4, 0.1][t] * s
        oy = -[1.0, 0.7, 0.4, 0.1][t] * s
    elif state == 'Death':
        k = t / (n - 1)
        mode = 'fold' if k > 0.3 else 'hurt'
        z = max(0.9 * s, 4.5 * s * (1 - k) ** 1.6)
    bat_body(sc, ang, spec, s, 0, oy, z, ph, mode)
    if spec.get('swarm') and state != 'Death':
        # deux petites chauves-souris tournent autour du seigneur
        for i in range(2):
            a_ = ph + i * math.pi
            bat_body(sc, ang, spec, s, math.cos(a_) * 6.5 * s, math.sin(a_) * 3.5 * s, z + 1.5 * s + math.sin(a_ * 2) * 0.6 * s, ph * 1.7 + i, 'fly', 0.45)
    return finish(sc, spec['outline'], 3.0 * s, ang, lift=0.6)


BAT_ANIMS = {'Idle': 4, 'Walk': 6, 'Run': 6, 'Attack': 6, 'Hurt': 4, 'Death': 6}


def build_bat(cid):
    spec = BATS[cid]
    for state, n in BAT_ANIMS.items():
        rows = [[bat_frame(spec, d, state, t, n) for t in range(n)] for d in DIRS]
        save(rows, os.path.join(OUT, cid, f'{cid}_{state}.png'))
    print('ok', cid)


# ─────────────────────────────────────────────
# Objets posés par les monstres (importés comme les pièges : Art/Generated/Traps/<nom>_LxH.png)
# ─────────────────────────────────────────────

TRAPS_DIR = os.path.join(ROOT, 'Assets', 'Art', 'Generated', 'Traps')


def build_war_totem(cid):
    """Totem de guerre du chaman de sang : poteau, crâne cornu, bandes rouges ; image 2 = runes allumées."""
    W, H = 16, 32
    frames = []
    for glow in (0, 1):
        a = np.zeros((H, W, 4), np.uint8)
        def put(x, y, c):
            if 0 <= x < W and 0 <= y < H:
                a[y, x] = tuple(c) + (255,)
        wood = [(46, 28, 18), (84, 52, 30), (122, 82, 46)]
        for y in range(9, 30):
            put(7, y, wood[1]); put(8, y, wood[2] if y % 5 else wood[0])
        for x in range(4, 12):
            put(x, 12, wood[1]); put(x, 13, wood[0])
        bone = [(150, 140, 120), (214, 206, 186), (246, 242, 228)]
        for y in range(4, 10):
            for x in range(5, 11):
                if (x - 7.5) ** 2 / 9 + (y - 6.5) ** 2 / 7 <= 1:
                    put(x, y, bone[1] if x < 8 else bone[0])
        put(6, 5, bone[2])
        eye = (255, 60, 40) if glow else (30, 10, 10)
        put(6, 7, eye); put(9, 7, eye)
        put(7, 9, (40, 30, 30)); put(8, 9, (40, 30, 30))
        for x, y in ((4, 4), (3, 3), (3, 2), (11, 4), (12, 3), (12, 2)):
            put(x, y, bone[0] if y > 2 else bone[2])
        red = [(90, 10, 16), (150, 20, 30), (200, 44, 50)]
        for k, x in enumerate((4, 5, 10, 11)):
            for y in range(14, 19 + k % 2 * 2):
                put(x, y, red[1] if y % 3 else red[2])
            put(x, 19 + k % 2 * 2, red[0])
        if glow:
            for y in (17, 21, 25):
                put(7, y, (255, 90, 60)); put(8, y, (255, 160, 120))
        # contour sombre
        m = a[:, :, 3] > 0
        ring = np.zeros_like(m)
        for dy, dx in ((1, 0), (-1, 0), (0, 1), (0, -1)):
            ring |= np.roll(np.roll(m, dy, 0), dx, 1)
        ring &= ~m
        a[ring] = (20, 10, 12, 255)
        frames.append(Image.fromarray(a))
    sheet = Image.new('RGBA', (W * len(frames), H))
    for i, f in enumerate(frames):
        sheet.alpha_composite(f, (i * W, 0))
    os.makedirs(TRAPS_DIR, exist_ok=True)
    sheet.save(os.path.join(TRAPS_DIR, f'war_totem_{W}x{H}.png'))
    print('ok', cid)


# ─────────────────────────────────────────────
# Loups de givre : quadrupèdes (allure en diagonale, tête basse à l'attaque, hurlement)
# ─────────────────────────────────────────────

WOLVES = {
    'wolf_1': dict(scale=1.6, fur=R('5a6a82', '8a9cb6', 'bccbe0', 'eef4fc'), dark=R('2a3446', '46546c', '6a7a94', '96a6c0'),
                   eye=(120, 220, 255), outline=(22, 26, 40)),
    'wolf_2': dict(scale=1.95, fur=R('3e4a62', '62728e', '8c9cb8', 'c0cee2'), dark=R('1e2638', '343e56', '505c78', '7a88a6'),
                   eye=(140, 240, 255), outline=(16, 20, 32), frost=True),
    'wolf_3': dict(scale=2.3, fur=R('8090a8', 'b4c2d6', 'dce6f2', 'ffffff'), dark=R('4a5a74', '6e7e9a', '98a8c2', 'c8d4e6'),
                   eye=(80, 200, 255), outline=(26, 30, 46), frost=True, mane=True),
}


def wolf_pattern(spec):
    dark = spec['dark']

    def pat(local, lam, cols):
        # dos plus sombre (selle), ventre clair
        z = local[:, 2]
        m = z > np.percentile(z, 70)
        cols[m] = shade_ramp(dark, lam[m])
    return pat


def wolf_frame(spec, d, state, t, n):
    s = spec['scale']
    gx, gy = GFACE[d]
    ang = math.atan2(-gx, gy)
    sc = Scene()
    ph = 2 * math.pi * t / n
    h = 3.6 * s                     # hauteur du dos
    head_dz, head_dy, lunge, mouth, sink, tail_up, howl = 0.0, 0.0, 0.0, 0.0, 0.0, 0.4, 0.0
    stride, lift = 0.0, 0.0
    if state == 'Idle':
        h += 0.15 * s * math.sin(ph)
    elif state in ('Walk', 'Run'):
        stride = (1.4 if state == 'Walk' else 2.4) * s
        lift = (0.8 if state == 'Walk' else 1.3) * s
        h += (0.15 if state == 'Walk' else 0.5) * s * abs(math.sin(ph))
    elif state == 'Attack':
        k = [0.2, 0.6, 1.0, 0.4, 0.1, 0.0][t]
        lunge = [0, -0.6, -0.3, 2.4, 1.0, 0][t] * s
        head_dz = -1.0 * s * k
        mouth = [0, 0.3, 1.0, 0.2, 0, 0][t]
    elif state == 'Hurt':
        k = [1, 0.6, 0.3, 0][t]
        lunge = -1.0 * s * k
        head_dz = 0.6 * s * k
        tail_up = -0.3
    elif state == 'Death':
        k = t / (n - 1)
        sink = 2.4 * s * k
        head_dz = -1.2 * s * k
        tail_up = -0.5 * k
    if state == 'Howl':
        howl = 1.0
    body_c = (0, lunge, h - sink)
    ellipsoid(sc, ang, body_c, (2.5 * s, 3.4 * s, 1.8 * s), spec['fur'], wolf_pattern(spec))
    chest = (0, lunge + 2.6 * s, h - sink + 0.3 * s)
    ellipsoid(sc, ang, chest, (2.4 * s, 1.6 * s, 1.9 * s), spec['fur'])
    if spec.get('mane'):
        ellipsoid(sc, ang, (0, lunge + 2.2 * s, h - sink + 1.2 * s), (1.9 * s, 1.6 * s, 1.4 * s), spec['fur'])
    # tête et museau
    hc = (0, lunge + 4.4 * s + head_dy, h - sink + 1.1 * s + head_dz + howl * 1.0 * s)
    ellipsoid(sc, ang, hc, (1.75 * s, 1.35 * s, 1.35 * s), spec['fur'])
    sn_pitch = -0.6 * howl
    snout = (0, hc[1] + 1.5 * s, hc[2] - 0.35 * s + howl * 0.9 * s)
    ellipsoid(sc, ang, snout, (0.65 * s, 1.0 * s, 0.55 * s), spec['fur'], pitch=sn_pitch)
    dot(sc, ang, (0, snout[1] + 0.95 * s, snout[2] + 0.15 * s), (20, 20, 30))      # truffe
    if mouth > 0.3:
        segment(sc, ang, (-0.3 * s, snout[1] + 0.4 * s, snout[2] - 0.6 * s), (0.3 * s, snout[1] + 0.4 * s, snout[2] - 0.6 * s),
                [(120, 20, 30), (180, 40, 50), (220, 80, 90), (240, 120, 130)], 0.5)
        for sx in (-0.35, 0.35):
            dot(sc, ang, (sx * s, snout[1] + 0.7 * s, snout[2] - 0.45 * s), (250, 250, 245))
    for side in (-1, 1):
        dot(sc, ang, (side * 0.55 * s, hc[1] + 1.0 * s, hc[2] + 0.3 * s), spec['eye'])
        segment(sc, ang, (side * 0.7 * s, hc[1] - 0.2 * s, hc[2] + 0.8 * s), (side * 0.95 * s, hc[1] - 0.4 * s, hc[2] + 2.2 * s), spec['dark'], 0.9)
    # pattes : diagonales opposées (allure du trot)
    legs = [(-1, 2.2), (1, 2.2), (-1, -2.4), (1, -2.4)]
    for i, (side, ly) in enumerate(legs):
        gph = ph + (0 if i in (0, 3) else math.pi)
        hip = np.array([side * 1.25 * s, lunge + ly * s, h - sink - 0.6 * s])
        fy = lunge + ly * s + stride * math.sin(gph)
        fz = max(0.0, lift * math.cos(gph)) if stride else 0.0
        if state == 'Death':
            fz = 0.3 + 1.2 * s * (t / (n - 1))
        foot = np.array([side * 1.4 * s, fy, fz])
        knee = (hip + foot) / 2 + np.array([0, (0.5 if ly > 0 else -0.5) * s, 0])
        segment(sc, ang, hip, knee, spec['dark'], 0.9)
        segment(sc, ang, knee, foot, spec['dark'], 0.7)
    # queue en panache
    tb = np.array([0, lunge - 3.4 * s, h - sink + 0.4 * s])
    tt = tb + np.array([0.3 * math.sin(ph) * s, -2.4 * s, (tail_up + 0.3) * 2.2 * s])
    ellipsoid(sc, ang, tuple((tb + tt) / 2), (0.6 * s, 1.5 * s, 0.6 * s), spec['fur'], pitch=-0.5 + tail_up)
    if spec.get('frost') and state in ('Idle', 'Walk', 'Run', 'Howl'):
        # éclats de givre qui flottent autour du loup
        for k in range(3):
            a_ = ph + k * 2.1
            dot(sc, ang, (math.cos(a_) * 3.5 * s, math.sin(a_) * 3.0 * s, h + 2 * s + math.sin(a_ * 2) * s), (220, 245, 255))
    if howl:
        for k in range(6):
            a_ = k / 6 * 2 * math.pi + ph
            r_ = (2.5 + t) * s
            dot(sc, ang, (math.cos(a_) * r_, hc[1] + math.sin(a_) * r_ * 0.5, hc[2] + 2.5 * s), (180, 230, 255))
    return finish(sc, spec['outline'], 3.8 * s + 1, ang)


WOLF_ANIMS = {'Idle': 4, 'Walk': 6, 'Run': 6, 'Attack': 6, 'Hurt': 4, 'Death': 6}


def build_wolf(cid):
    spec = WOLVES[cid]
    for state, n in WOLF_ANIMS.items():
        rows = [[wolf_frame(spec, d, state, t, n) for t in range(n)] for d in DIRS]
        save(rows, os.path.join(OUT, cid, f'{cid}_{state}.png'))
    print('ok', cid)


FX_DIR = os.path.join(ROOT, 'Assets', 'Resources', 'FX')


def build_projectiles(cid):
    """Projectiles des monstres (pointe en haut, comme la flèche) : javelot du gnoll, dard de sarbacane."""
    def sheet(rows, pal, name):
        h, w = len(rows), len(rows[0])
        im = Image.new('RGBA', (w, h))
        for y, row in enumerate(rows):
            for x, ch in enumerate(row):
                if ch != '.':
                    im.putpixel((x, y), pal[ch] + (255,))
        os.makedirs(FX_DIR, exist_ok=True)
        im.save(os.path.join(FX_DIR, name + '.png'))
    jav = ['..o..', '.oto.', '.ttt.', '.oto.', '..m..'] + ['.olo.' if i % 6 == 0 else '..l..' for i in range(17)] + ['.oro.', '.rrr.', '..o..']
    sheet(jav, {'o': (30, 22, 18), 't': (226, 232, 240), 'm': (150, 156, 168), 'l': (132, 92, 52), 'r': (180, 40, 40)}, 'javelin')
    dart = ['.g.', 'ggg', '.n.', '.n.', '.n.', '.n.', 'fff', 'f.f']
    sheet(dart, {'g': (140, 230, 80), 'n': (200, 190, 150), 'f': (170, 60, 160)}, 'dart')
    print('ok', cid)


BUILDERS = {**{k: build_spider for k in SPIDERS}, **{k: build_mimic for k in MIMICS}, **{k: build_bat for k in BATS}, 'war_totem': build_war_totem, **{k: build_wolf for k in WOLVES}, 'projectiles': build_projectiles}

if __name__ == '__main__':
    for cid in sys.argv[1:] or list(BUILDERS):
        BUILDERS[cid](cid)
