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


BUILDERS = {**{k: build_spider for k in SPIDERS}}

if __name__ == '__main__':
    for cid in sys.argv[1:] or list(BUILDERS):
        BUILDERS[cid](cid)
