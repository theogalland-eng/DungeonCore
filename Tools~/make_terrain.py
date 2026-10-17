"""
Terrain de l'île : tuiles auto-raccordées « blob » (47 cas de voisinage) en pixel art, dessinées de zéro.
Palette : maquette Design « Matériaux du terrain » (sable noir volcanique, herbe des clairières, jungle,
roche du volcan, lave, eau, sentiers).

Chaque matériau est une couche posée par-dessus la précédente (eau < sable < roche < herbe < jungle < lave,
puis sentiers foulés / terre / pavés). Les bords sont organiques : la limite est perturbée par un bruit
global, et la texture a une période de 48 px (3 x 3 cases) : chaque tuile existe en 9 « phases »,
choisies par la position de la case, donc aucun raccord visible et une répétition 3 fois moins serrée.

Sortie : Assets/Art/Generated/Terrain/terrain_<nom>.png (lignes = 9 phases, colonnes = 47 masques)
         + terrain_<nom>.json, water.png (9 phases), cliff.png, decor_*.png
Usage  : python "Tools~/make_terrain.py"   (puis menu DungeonCore/3. Générer l'île)
"""
import json
import math
import os

import numpy as np
from PIL import Image
from scipy import ndimage, special

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
OUT = os.path.join(ROOT, 'Assets', 'Art', 'Generated', 'Terrain')
T = 16          # taille d'une case
P = 48          # période de la texture (3 cases)
SIGMA = 3.0     # arrondi des coins (px)

# Bits de voisinage (y image vers le bas) : N=1 NE=2 E=4 SE=8 S=16 SW=32 W=64 NW=128
DIRS = {1: (0, -1), 2: (1, -1), 4: (1, 0), 8: (1, 1), 16: (0, 1), 32: (-1, 1), 64: (-1, 0), 128: (-1, -1)}


def hexc(h):
    h = h.lstrip('#')
    return tuple(int(h[i:i + 2], 16) for i in (0, 2, 4))


def blob47():
    out = set()
    for m in range(256):
        r = m
        for corner, (a, b) in {2: (1, 4), 8: (4, 16), 32: (16, 64), 128: (64, 1)}.items():
            if not (m & a and m & b):
                r &= ~corner
        out.add(r)
    return sorted(out)


MASKS = blob47()


def noise(scale, seed, octaves=2):
    """Bruit de valeur lissé, raccordable sur P x P."""
    rng = np.random.default_rng(seed)
    out = np.zeros((P, P))
    amp, tot = 1.0, 0.0
    for o in range(octaves):
        g = max(1, int(round(P / scale * 2 ** o)))
        grid = rng.random((g + 1, g + 1))
        grid[-1, :] = grid[0, :]
        grid[:, -1] = grid[:, 0]
        t = np.arange(P) * g / P
        i0 = t.astype(int)
        f = t - i0
        f = f * f * (3 - 2 * f)
        a = grid[i0][:, i0]; b = grid[i0][:, i0 + 1]; c = grid[i0 + 1][:, i0]; d = grid[i0 + 1][:, i0 + 1]
        fy, fx = f[:, None], f[None, :]
        out += amp * ((a * (1 - fx) + b * fx) * (1 - fy) + (c * (1 - fx) + d * fx) * fy)
        tot += amp
        amp *= 0.5
    out /= tot
    lo, hi = out.min(), out.max()
    return (out - lo) / (hi - lo + 1e-9)


def grain(seed):
    return np.random.default_rng(seed).random((P, P))


def voronoi(n, seed):
    """Cellules de Voronoï raccordables : (identifiant, distance au bord de la cellule)."""
    rng = np.random.default_rng(seed)
    pts = rng.random((n, 2)) * P
    yy, xx = np.mgrid[0:P, 0:P] + 0.5
    best = np.full((P, P), 1e9); second = np.full((P, P), 1e9); ident = np.zeros((P, P), int)
    for i, (px, py) in enumerate(pts):
        for ox in (-P, 0, P):
            for oy in (-P, 0, P):
                d = np.hypot(xx - px - ox, yy - py - oy)
                closer = d < best
                second = np.where(closer, best, np.minimum(second, d))
                ident = np.where(closer, i, ident)
                best = np.where(closer, d, best)
    return ident, second - best


class Ctx:
    """Voisinage 3x3 d'une case : masque plein, distance au bord, champs globaux échantillonnés."""

    def __init__(self, bits, phase, fields):
        px, py = phase
        m = np.zeros((3 * T, 3 * T), bool)
        m[T:2 * T, T:2 * T] = True
        for b, (dx, dy) in DIRS.items():
            if bits & b:
                m[(1 + dy) * T:(2 + dy) * T, (1 + dx) * T:(2 + dx) * T] = True
        self.m = m
        # distance (px) au bord du matériau ; une case pleine entourée de plein : très loin
        # flou gaussien de la forme puis inversion : distance signée aux coins arrondis (pas de marches)
        sm = ndimage.gaussian_filter(m.astype(float), SIGMA, mode='nearest')
        self.d = SIGMA * special.ndtri(np.clip(sm, 1e-5, 1 - 1e-5)) + 0.5
        gy = (np.arange(3 * T) + (py - 1) * T) % P
        gx = (np.arange(3 * T) + (px - 1) * T) % P
        self.f = {k: v[np.ix_(gy, gx)] for k, v in fields.items()}
        # direction de la normale (vers l'extérieur) : sert à l'éclairage des bords (lumière en haut à gauche)
        gyd, gxd = np.gradient(np.minimum(self.d, 8))
        self.lit = (gxd + gyd) > 0.05     # le bord est en haut / à gauche
        self.img = np.zeros((3 * T, 3 * T, 4), np.uint8)

    def put(self, mask, col, alpha=255):
        self.img[mask] = (*col, alpha)

    def s(self, amp, key='edge'):
        """Distance « organique » au bord : distance + bruit."""
        return self.d + (self.f[key] - 0.5) * 2 * amp


def pick(n, cols):
    idx = np.clip((n * len(cols)).astype(int), 0, len(cols) - 1)
    return idx


def fill(c, mask, n, cols):
    idx = pick(n, cols)
    for i, col in enumerate(cols):
        c.put(mask & (idx == i), hexc(col))


def drop_shadow(c, vis, alpha=70):
    """Ombre portée d'un pixel sous le bord bas / droit (dans la case, sur la couche du dessous)."""
    below = np.zeros_like(vis); below[1:, :] = vis[:-1, :]
    right = np.zeros_like(vis); right[:, 1:] = vis[:, :-1]
    sh = c.m & ~vis & (below | right)
    c.put(sh, (12, 10, 16), alpha)


# ── Styles ───────────────────────────────────────────────────

def style_sand(c):
    s = c.s(3.0)
    vis = c.m & (s > 3.0)
    wet = vis & (s < 5.6 + c.f['tex'] * 1.5)
    dry = vis & ~wet
    fill(c, dry, c.f['tex'] * 0.9 + c.f['grain'] * 0.1, ['#2e292c', '#3d3739', '#3d3739', '#3d3739', '#57504f'])
    c.put(dry & (c.f['grain'] > 0.975), hexc('#7d746c'))
    c.put(dry & (c.f['grain'] < 0.02), hexc('#1f1b1e'))
    fill(c, wet, c.f['tex2'], ['#1f1b1e', '#2e292c', '#2e292c'])
    c.put(wet & (c.f['grain'] > 0.985), hexc('#57504f'))
    # écume : liseré clair juste devant le sable mouillé, avec des trous
    foam = c.m & ~vis & (s > 0.9) & (c.f['foam'] > 0.32)
    c.put(foam, hexc('#d8f1ee'), 225)
    foam2 = c.m & ~vis & ~foam & (s > -0.3) & (c.f['foam'] > 0.62)
    c.put(foam2, hexc('#9fd8d2'), 150)


def style_grass(c):
    s = c.s(3.2)
    vis = c.m & (s > 3.2)
    # brins qui débordent sur le sol du dessous
    blades = c.m & ~vis & (s > 1.2) & (c.f['grain'] > 0.72)
    drop_shadow(c, vis | blades, 80)
    fill(c, vis, c.f['tex'] * 0.8 + c.f['grain'] * 0.2, ['#3f6a2c', '#578a35', '#578a35', '#79a844'])
    c.put(vis & (c.f['tex2'] < 0.16), hexc('#3f6a2c'))
    c.put(vis & (c.f['grain'] > 0.965), hexc('#a8c868'))
    c.put(vis & (c.f['grain'] < 0.03), hexc('#2c4a22'))
    rim = vis & (s < 4.3)
    c.put(rim & ~c.lit, hexc('#2c4a22'))
    c.put(rim & c.lit, hexc('#3f6a2c'))
    c.put(blades, hexc('#578a35'))


def style_jungle(c):
    s = c.s(3.4)
    vis = c.m & (s > 3.4)
    leaves = c.m & ~vis & (s > 1.3) & (c.f['grain'] > 0.7)
    drop_shadow(c, vis | leaves, 90)
    fill(c, vis, c.f['tex'] * 0.85 + c.f['grain'] * 0.15, ['#1f3d1d', '#2f5a26', '#2f5a26', '#46753a'])
    c.put(vis & (c.f['tex2'] < 0.2), hexc('#142a17'))
    c.put(vis & (c.f['tex2'] > 0.86), hexc('#6b5a3a'))
    c.put(vis & (c.f['grain'] > 0.97), hexc('#79a844'))
    rim = vis & (s < 4.4)
    c.put(rim, hexc('#142a17'))
    c.put(leaves, hexc('#2f5a26'))


def style_rock(c):
    """Roche des hautes terres : gris chaud (accordé au relief du volcan), grandes plaques, peu de grain."""
    s = c.s(1.8)
    vis = c.m & (s > 2.2)
    drop_shadow(c, vis, 110)
    band = np.floor(c.f['tex'] * 4).astype(int)
    fill(c, vis, c.f['tex'], ['#3d3739', '#4a4448', '#4a4448', '#5d5553'])
    edge = np.zeros_like(vis)
    edge[1:, :] |= band[1:, :] != band[:-1, :]
    edge[:, 1:] |= band[:, 1:] != band[:, :-1]
    c.put(vis & edge & (c.f['grain'] > 0.45), hexc('#2e292c'))
    up = np.zeros_like(edge); up[:-1, :] = edge[1:, :]
    c.put(vis & up & ~edge & (c.f['grain'] > 0.5), hexc('#6b625d'))
    c.put(vis & (c.f['grain'] > 0.988), hexc('#7d746c'))
    rim = vis & (s < 3.3)
    c.put(rim & ~c.lit, hexc('#1f1b1e'))
    c.put(rim & c.lit, hexc('#6b625d'))


def style_lava(c):
    s = c.s(2.0)
    vis = c.m & (s > 2.6)
    crust = vis & (s < 4.6)
    core = vis & ~crust
    fill(c, core, c.f['tex'], ['#a8361c', '#ff7a2a', '#ff7a2a', '#ffb347'])
    c.put(core & (c.f['tex'] > 0.9), hexc('#ffe2a0'))
    plates = core & (c.f['tex2'] > 0.7)
    c.put(plates, hexc('#5a2416'))
    c.put(plates & (c.f['tex2'] > 0.82), hexc('#2c2832'))
    fill(c, crust, c.f['grain'], ['#2c2832', '#5a2416', '#5a2416', '#a8361c'])
    rim = vis & (s < 3.4)
    c.put(rim, hexc('#1c1920'))


def style_trampled(c):
    """Herbe foulée : premier stade d'un sentier né du passage."""
    s = c.s(3.2)
    vis = c.m & (s > 3.0) & (c.f['grain'] > np.clip(1.0 - (s - 3.0) * 0.35, 0, 1))
    fill(c, vis, c.f['tex'], ['#6f8a3e', '#7d8a4a', '#8a8a55', '#6b5a3a'])
    c.put(vis & (c.f['grain'] > 0.97), hexc('#a8c868'))


def style_dirt(c):
    s = c.s(2.4)
    vis = c.m & (s > 2.6)
    soft = c.m & ~vis & (s > 1.4) & (c.f['grain'] > 0.55)
    fill(c, vis, c.f['tex'] * 0.8 + c.f['grain'] * 0.2, ['#57472f', '#6b5a3a', '#6b5a3a', '#7d6a48'])
    c.put(vis & (c.f['grain'] > 0.975), hexc('#8d8590'))
    c.put(vis & (c.f['grain'] < 0.025), hexc('#46392a'))
    c.put(vis & (s < 3.6), hexc('#57472f'))
    c.put(soft, hexc('#6b5a3a'), 170)


def style_paved(c):
    s = c.s(1.2)
    vis = c.m & (s > 2.0)
    drop_shadow(c, vis, 90)
    ident, border = c.f['cell_id'], c.f['cell_border']
    shade = (ident * 0.37) % 1.0
    stone = vis & (border > 1.1)
    fill(c, stone, shade * 0.6 + c.f['grain'] * 0.4, ['#6b6470', '#8d8590', '#7d746c'])
    # éclairage de chaque pavé : arête claire en haut à gauche, sombre en bas à droite
    gy, gx = np.gradient(border)
    c.put(stone & (border < 2.2) & ((gx + gy) > 0), hexc('#a59fb0'))
    c.put(stone & (border < 2.2) & ((gx + gy) <= 0), hexc('#4a4550'))
    c.put(vis & ~stone, hexc('#2c2832'))
    rim = vis & (s < 3.0)
    c.put(rim, hexc('#4a4550'))


def build(name, style, fields):
    sheet = Image.new('RGBA', (T * len(MASKS), T * 9))
    for py in range(3):
        for px in range(3):
            row = py * 3 + px
            for i, bits in enumerate(MASKS):
                c = Ctx(bits, (px, py), fields)
                style(c)
                tile = Image.fromarray(c.img[T:2 * T, T:2 * T], 'RGBA')
                sheet.paste(tile, (i * T, row * T))
    path = os.path.join(OUT, f'terrain_{name}.png')
    sheet.save(path)
    with open(path.replace('.png', '.json'), 'w') as fp:
        json.dump({'tile': T, 'phases': 9, 'masks': MASKS}, fp)
    return sheet


def demo(sheets, layout_layers, path, scale=3):
    """Aperçu : plusieurs couches superposées sur une petite carte."""
    h, w = len(layout_layers[0][1]), len(layout_layers[0][1][0])
    img = Image.new('RGBA', (w * T, h * T), (11, 36, 64, 255))
    water = Image.open(os.path.join(OUT, 'water.png'))
    for y in range(h):
        for x in range(w):
            ph = (x % 3) + 3 * (y % 3)
            t = water.crop((ph * T, 0, ph * T + T, T))
            tint = Image.new('RGBA', (T, T), (57, 168, 166, 255) if any(
                layout_layers[0][1][yy][xx] == '#' for yy in range(max(0, y - 1), min(h, y + 2))
                for xx in range(max(0, x - 1), min(w, x + 2))) else (19, 67, 107, 255))
            img.alpha_composite(Image.fromarray((np.array(t, float) * np.array(tint, float) / 255).astype(np.uint8)), (x * T, y * T))
    for name, rows in layout_layers:
        sh = sheets[name]
        solid = lambda xx, yy: 0 <= yy < h and 0 <= xx < w and rows[yy][xx] == '#'
        for y in range(h):
            for x in range(w):
                if not solid(x, y):
                    continue
                bits = 0
                for b, (dx, dy) in DIRS.items():
                    if solid(x + dx, y + dy):
                        bits |= b
                for corner, (a, bb) in {2: (1, 4), 8: (4, 16), 32: (16, 64), 128: (64, 1)}.items():
                    if not (bits & a and bits & bb):
                        bits &= ~corner
                i = MASKS.index(bits)
                ph = (x % 3) + 3 * (y % 3)
                img.alpha_composite(sh.crop((i * T, ph * T, i * T + T, ph * T + T)), (x * T, y * T))
    img.resize((img.width * scale, img.height * scale), Image.NEAREST).save(path)


# ── Eau, falaise, décors ─────────────────────────────────────

def water(frames=4):
    """Tuile d'eau claire (teintée case par case dans Unity), 9 phases x 4 images, vaguelettes raccordables."""
    n = noise(12, 501, 3)
    g = grain(502)
    yy, xx = np.mgrid[0:P, 0:P]
    out = Image.new('RGBA', (T * 9, T * frames))
    for f in range(frames):
        t = f * 2 * math.pi / frames
        wave = np.sin((xx * 2 * math.pi / P) * 2 + n * 7.0 + (yy * 2 * math.pi / P) * 3 + t)
        # calque de vaguelettes seulement : la couleur de l'eau vient d'une texture lissée sous la mer
        img = np.zeros((P, P, 4), np.uint8)
        patches = noise(16, 503 + 0, 2)               # vaguelettes par plaques, pas partout
        crest = (wave > 0.95) & (g > 0.3) & (patches > 0.5)
        trough = (wave < -0.97) & (g > 0.55) & (patches > 0.4)
        img[crest] = (235, 250, 248, 70)
        img[trough] = (5, 20, 40, 30)
        for py in range(3):
            for px in range(3):
                tile = Image.fromarray(img[py * T:(py + 1) * T, px * T:(px + 1) * T], 'RGBA')
                out.paste(tile, ((py * 3 + px) * T, f * T))
    out.save(os.path.join(OUT, 'water.png'))


def cliff():
    """Front de falaise (case de grève sous l'herbe) : lèvre herbeuse, roche en strates, pied sombre.
    Colonnes : milieu A, milieu B, bout gauche, bout droit, seul."""
    out = Image.new('RGBA', (T * 5, T))
    tex = noise(6, 701, 2)
    g = grain(702)
    for v in range(5):
        img = np.zeros((T, T, 4), np.uint8)
        for y in range(T):
            for x in range(T):
                gx = (x + v * 7) % P
                n = tex[y, gx]
                if y <= 2:
                    col = ['#578a35', '#79a844', '#3f6a2c'][min(2, int(n * 3))] if y < 2 else '#2c4a22'
                    if y == 2 and g[y, gx] > 0.6:
                        col = '#3f6a2c'
                elif y >= 14:
                    col = '#1f1b1e' if y == 14 else '#2e292c'
                else:
                    strata = int((n * 4 + y * 0.18)) % 3
                    col = ['#3d3739', '#57504f', '#3d3739'][strata]
                    if (x + int(n * 5)) % 5 == 0:
                        col = '#2e292c'
                    if x < 2 or g[y, gx] > 0.93:
                        col = '#57504f' if x < 2 else '#7d746c'
                img[y, x] = (*hexc(col), 255)
        # bouts arrondis : la roche se retire, l'herbe dessous se voit
        if v in (2, 4):
            for y in range(3, T):
                cut = int(3 + (y - 3) * 0.45)
                img[y, :max(0, 6 - cut)] = 0
        if v in (3, 4):
            for y in range(3, T):
                cut = int(3 + (y - 3) * 0.45)
                img[y, T - max(0, 6 - cut):] = 0
        out.paste(Image.fromarray(img, 'RGBA'), (v * T, 0))
    out.save(os.path.join(OUT, 'cliff.png'))


def sprite(name, draw):
    img = Image.new('RGBA', (T, T), (0, 0, 0, 0))
    px = img.load()

    def p(x, y, col, a=255):
        if 0 <= x < T and 0 <= y < T:
            px[x, y] = (*hexc(col), a)
    draw(p)
    img.save(os.path.join(OUT, f'decor_{name}.png'))
    return img


def decors():
    rng = np.random.default_rng(900)
    made = []

    def tuft(seed, tall):
        def d(p):
            r = np.random.default_rng(seed)
            cx = 8
            for i in range(6 + tall * 2):
                x = cx + int(r.integers(-4, 5))
                h = int(r.integers(2, 4 + tall * 2))
                lean = int(r.integers(-1, 2))
                for k in range(h):
                    col = '#2c4a22' if k == 0 else ('#79a844' if k == h - 1 else '#578a35')
                    p(x + (lean if k > h // 2 else 0), 12 - k, col)
        return d
    for i in range(3):
        made.append(sprite(f'tuft_{i}', tuft(910 + i, i % 2)))

    def flowers(seed, petal, centre):
        def d(p):
            r = np.random.default_rng(seed)
            for _ in range(3):
                x, y = int(r.integers(3, 13)), int(r.integers(5, 12))
                p(x, y + 1, '#3f6a2c'); p(x, y + 2, '#2c4a22')
                p(x, y, centre)
                for dx, dy in ((1, 0), (-1, 0), (0, -1)):
                    p(x + dx, y + dy, petal)
        return d
    for i, (pc, cc) in enumerate([('#efe9dc', '#ffb347'), ('#ffd45c', '#ff7a2a'), ('#e88aa8', '#ffe2a0'), ('#8fb8ff', '#efe9dc')]):
        made.append(sprite(f'flowers_{i}', flowers(920 + i, pc, cc)))

    def pebbles(seed):
        def d(p):
            r = np.random.default_rng(seed)
            for _ in range(4):
                x, y = int(r.integers(2, 13)), int(r.integers(3, 13))
                w = int(r.integers(1, 3))
                for dx in range(w + 1):
                    p(x + dx, y, '#57504f'); p(x + dx, y + 1, '#2e292c')
                p(x, y, '#7d746c')
        return d
    for i in range(2):
        made.append(sprite(f'pebbles_{i}', pebbles(930 + i)))

    def reeds(seed):
        def d(p):
            r = np.random.default_rng(seed)
            for _ in range(5):
                x = int(r.integers(3, 13))
                h = int(r.integers(6, 12))
                for k in range(h):
                    p(x, 14 - k, '#46753a' if k < h - 1 else '#79a844')
                if r.random() < 0.6:
                    p(x, 14 - h, '#6b5a3a'); p(x, 13 - h, '#6b5a3a'); p(x, 15 - h, '#57472f')
            for x in range(3, 13):
                if r.random() < 0.5:
                    p(x, 15, '#2c4a22', 200)
        return d
    for i in range(3):
        made.append(sprite(f'reeds_{i}', reeds(940 + i)))

    def lily(seed, flower):
        def d(p):
            r = np.random.default_rng(seed)
            cx, cy = int(r.integers(5, 11)), int(r.integers(5, 11))
            for y in range(-3, 4):
                for x in range(-3, 4):
                    if x * x + y * y <= 10 and not (x > 0 and abs(y) <= x * 0.5):
                        p(cx + x, cy + y, '#578a35' if (x + y) < 0 else '#3f6a2c')
            p(cx - 2, cy - 2, '#79a844')
            if flower:
                p(cx - 1, cy, '#e88aa8'); p(cx, cy - 1, '#e88aa8'); p(cx - 1, cy - 1, '#ffe2a0')
        return d
    for i in range(2):
        made.append(sprite(f'lily_{i}', lily(950 + i, i == 1)))

    def reef(seed):
        def d(p):
            r = np.random.default_rng(seed)
            for _ in range(2):
                cx, cy, rr = int(r.integers(5, 11)), int(r.integers(5, 11)), int(r.integers(2, 4))
                for y in range(-rr - 1, rr + 2):
                    for x in range(-rr - 1, rr + 2):
                        dd = math.hypot(x, y * 1.2)
                        if dd <= rr:
                            p(cx + x, cy + y, '#7d746c' if x + y < -1 else ('#57504f' if x + y < 1 else '#3d3739'))
                        elif dd <= rr + 1.2 and r.random() < 0.7:
                            p(cx + x, cy + y, '#d8f1ee', 190)
                p(cx - 1, cy - 1, '#8d8590')
        return d
    for i in range(3):
        made.append(sprite(f'reef_{i}', reef(960 + i)))

    def fern(seed):
        def d(p):
            r = np.random.default_rng(seed)
            for a in range(7):
                ang = math.pi * (0.1 + 0.8 * a / 6)
                L = 5 + r.integers(0, 3)
                for k in range(int(L)):
                    x = 8 + int(round(math.cos(ang) * k * 1.1))
                    y = 13 - int(round(math.sin(ang) * k * 0.9))
                    p(x, y, '#2f5a26' if k < 2 else ('#46753a' if k < L - 1 else '#79a844'))
        return d
    for i in range(2):
        made.append(sprite(f'fern_{i}', fern(970 + i)))

    def shell(seed):
        def d(p):
            r = np.random.default_rng(seed)
            for _ in range(2):
                x, y = int(r.integers(3, 12)), int(r.integers(4, 12))
                p(x, y, '#efe9dc'); p(x + 1, y, '#d8c8b0'); p(x, y + 1, '#d8c8b0'); p(x + 1, y + 1, '#a59f96')
        return d
    made.append(sprite('shells_0', shell(980)))

    def drift(p):
        for x in range(3, 13):
            p(x, 9 + (x > 8), '#6b5a3a'); p(x, 10 + (x > 8), '#46392a')
        p(4, 8, '#7d6a48'); p(11, 8, '#7d6a48'); p(12, 7, '#6b5a3a')
    made.append(sprite('driftwood_0', drift))

    def fissure(p):
        """Entrée de la grotte : une faille sombre à peine visible, mousse et éboulis autour."""
        for y in range(2, 16):
            w = 1 + int(2.2 * math.sin(math.pi * (y - 2) / 14))
            for x in range(8 - w, 8 + w):
                p(x, y, '#0d0b10')
            p(8 - w - 1, y, '#2c2832'); p(8 + w, y, '#4a4550')
        for x, y in ((4, 14), (5, 15), (11, 14), (12, 15), (3, 15)):
            p(x, y, '#57504f')
        for x, y in ((5, 6), (4, 7), (11, 5), (12, 6), (5, 11)):
            p(x, y, '#46753a')
    made.append(sprite('fissure', fissure))

    def vent(p):
        """Évent géothermique : trou sombre, bord rougeoyant, cendre autour."""
        for y in range(16):
            for x in range(16):
                d = math.hypot(x - 7.5, (y - 8.5) * 1.3)
                if d < 2.2:
                    p(x, y, '#1c1920')
                elif d < 3.2:
                    p(x, y, '#ff7a2a' if (x + y) % 3 else '#a8361c')
                elif d < 4.4:
                    p(x, y, '#5a2416' if (x * 7 + y) % 4 else '#2c2832')
                elif d < 6 and (x * 13 + y * 7) % 5 == 0:
                    p(x, y, '#8d8590', 200)
    made.append(sprite('vent', vent))

    sheet = Image.new('RGBA', (len(made) * (T + 2), T), (40, 36, 52, 255))
    for i, im in enumerate(made):
        sheet.alpha_composite(im, (i * (T + 2), 0))
    sheet.resize((sheet.width * 5, sheet.height * 5), Image.NEAREST).save(os.path.join(OUT, '_preview_decor.png'))


def main():
    os.makedirs(OUT, exist_ok=True)
    cid, cb = voronoi(22, 31)
    fields = {
        'edge': noise(10, 11, 2), 'tex': noise(5, 12, 3), 'tex2': noise(8, 13, 2),
        'grain': grain(14), 'foam': noise(4, 15, 2),
        'cell_id': cid.astype(float), 'cell_border': cb,
    }
    styles = {'sand': style_sand, 'rock': style_rock, 'grass': style_grass, 'jungle': style_jungle,
              'lava': style_lava, 'trampled': style_trampled, 'dirt': style_dirt, 'paved': style_paved}
    water()
    cliff()
    decors()
    sheets = {}
    for i, (name, st) in enumerate(styles.items()):
        f = dict(fields)
        # chaque matériau a sa propre texture (mais la même forme de bord, pour des lisières qui s'épousent)
        f['tex'] = noise(5 if name != 'rock' else 9, 100 + i, 3)
        f['tex2'] = noise(8, 200 + i, 2)
        f['grain'] = grain(300 + i)
        sheets[name] = build(name, st, f)
    land = ['..............',
            '...########...',
            '..###########.',
            '.############.',
            '.###########..',
            '.##########...',
            '..########....',
            '...#####......',
            '..............']
    grass = ['..............',
             '..............',
             '...#######....',
             '..#########...',
             '..########....',
             '..#######.....',
             '...####.......',
             '..............',
             '..............']
    jung = ['..............',
            '..............',
            '..............',
            '...####.......',
            '...#####......',
            '....##........',
            '..............',
            '..............',
            '..............']
    rock = ['..............',
            '.......####...',
            '.......######.',
            '.........####.',
            '..........##..',
            '..............',
            '..............',
            '..............',
            '..............']
    trail = ['..............',
             '..............',
             '..............',
             '..............',
             '..............',
             '.....######...',
             '..........#...',
             '..............',
             '..............']
    demo(sheets, [('sand', land), ('rock', rock), ('grass', grass), ('jungle', jung), ('dirt', trail)],
         os.path.join(OUT, '_preview_demo.png'))
    print('terrain ok :', ', '.join(sheets))


if __name__ == '__main__':
    main()
