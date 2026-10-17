"""
Murs et sols du donjon, dessinés de zéro pour chaque ambiance d'étage (pixel art, lumière en haut à gauche).

Murs : même format que l'ancien atlas (AutoWallTile, 128 variantes, vue de 3/4 : dessus + face). On dessine un
       « bloc source » de 48 x 40 px par ambiance — bandes T (bord supérieur), D (dessus), BT (bas du dessus + lèvre
       + début de la face), F (face, 16 px, pied compris) ; colonnes L, IL, IR, R — puis make_walls.compose()
       assemble les 128 cas.
Sols : texture raccordable de 48 x 48 px découpée en 9 « phases » (PhaseTile) : aucune répétition visible.

Sortie : Assets/Art/Generated/Walls/walls_<id>.png, Assets/Art/Generated/Dungeon/floor_<id>.png (9 x 16 px)
Usage  : python "Tools~/make_dungeon.py"   (puis DungeonCore/6. Murs, sols et arbres, et 2. Catalogue)
"""
import math
import os
import sys

import numpy as np
from PIL import Image

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from make_walls import Style, compose, SAMPLE  # noqa: E402

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
WALLS = os.path.join(ROOT, 'Assets', 'Art', 'Generated', 'Walls')
FLOORS = os.path.join(ROOT, 'Assets', 'Art', 'Generated', 'Dungeon')
P = 48


def hexc(h):
    h = h.lstrip('#')
    return np.array([int(h[i:i + 2], 16) for i in (0, 2, 4)], float)


def pnoise(w, h, cells_x, cells_y, seed, octaves=2):
    """Bruit de valeur raccordable (période w x h)."""
    rng = np.random.default_rng(seed)
    out = np.zeros((h, w))
    amp, tot = 1.0, 0.0
    for o in range(octaves):
        gx, gy = cells_x * 2 ** o, cells_y * 2 ** o
        g = rng.random((gy + 1, gx + 1))
        g[-1, :] = g[0, :]
        g[:, -1] = g[:, 0]
        ty = np.arange(h) * gy / h
        tx = np.arange(w) * gx / w
        y0, x0 = ty.astype(int), tx.astype(int)
        fy, fx = ty - y0, tx - x0
        fy = (fy * fy * (3 - 2 * fy))[:, None]
        fx = (fx * fx * (3 - 2 * fx))[None, :]
        a = g[y0][:, x0]; b = g[y0][:, x0 + 1]; c = g[y0 + 1][:, x0]; d = g[y0 + 1][:, x0 + 1]
        out += amp * ((a * (1 - fx) + b * fx) * (1 - fy) + (c * (1 - fx) + d * fx) * fy)
        tot += amp
        amp *= 0.5
    out /= tot
    return (out - out.min()) / (out.max() - out.min() + 1e-9)


def ramp(n, cols):
    idx = np.clip((n * len(cols)).astype(int), 0, len(cols) - 1)
    pal = np.array([hexc(c) for c in cols])
    return pal[idx]


# ─────────────────────────────────────────────
# AMBIANCES
# ─────────────────────────────────────────────

THEMES = {
    'volcanic': dict(top=['#3d3739', '#4a4448', '#4a4448', '#57504f'], hi='#6b625d', out='#141015',
                     face=['#2e292c', '#3d3739', '#3d3739', '#4a4448'], seam='#1f1b1e', pattern='columns',
                     floor=['#2a2528', '#332d30', '#332d30', '#3b3437'], fdetail='#4a4448', fstyle='gravel'),
    'lava': dict(top=['#2c2832', '#36313c', '#36313c', '#2c2832'], hi='#4a4550', out='#0f0c12',
                 face=['#221e26', '#2c2832', '#2c2832'], seam='#160f14', pattern='embers', glow=['#a8361c', '#ff7a2a', '#ffb347'],
                 floor=['#261f22', '#2e2629', '#2e2629', '#352c2e'], fdetail='#5a2416', fstyle='cracks_hot'),
    'aqua': dict(top=['#2c5560', '#356a72', '#356a72', '#3f7a7e'], hi='#5aa3a3', out='#0e1c22', moss='#46753a',
                 face=['#1f4650', '#28535c', '#28535c'], seam='#16323a', pattern='bricks', drip='#5cc4c0',
                 floor=['#22404a', '#284a52', '#284a52', '#2e555c'], fdetail='#3f7a7e', fstyle='wet_tiles'),
    'ice': dict(top=['#9cc9de', '#b0d6e8', '#b0d6e8', '#c6e4f2'], hi='#eaf8ff', out='#2a4a62',
                face=['#6fa3c4', '#7cb0cf', '#86bbd8'], seam='#4f7f9f', pattern='ice', glint='#eaf8ff',
                floor=['#86b2c9', '#93bed3', '#93bed3', '#9fc8db'], fdetail='#6f9ab4', fstyle='frost'),
    'fungal': dict(top=['#3a2f45', '#463a52', '#463a52', '#3a2f45'], hi='#5e4f6e', out='#120c18', moss='#46753a',
                   face=['#2c2436', '#352c40', '#352c40'], seam='#1c1624', pattern='fungus', caps=['#7fe0c0', '#e88aa8', '#ffd45c'],
                   floor=['#28222e', '#2e2734', '#2e2734', '#342c3a'], fdetail='#2f5a26', fstyle='moss'),
    'crystal': dict(top=['#3e3256', '#4a3c66', '#4a3c66', '#57487a'], hi='#7a68a8', out='#120c1e',
                    face=['#30264a', '#3a2f58', '#3a2f58'], seam='#1f1832', pattern='crystals', shard=['#8e7cff', '#c9a8ff', '#efe6ff'],
                    floor=['#2b2440', '#312948', '#312948', '#382f50'], fdetail='#8e7cff', fstyle='polished'),
    'crypt': dict(top=['#4f564f', '#5a625a', '#5a625a', '#666f66'], hi='#7f897f', out='#151815',
                  face=['#3f453f', '#4a514a', '#4a514a'], seam='#2a2e2a', pattern='blocks',
                  floor=['#3a403a', '#424842', '#424842', '#4a514a'], fdetail='#2a2e2a', fstyle='flagstones'),
    'fortress': dict(top=['#4f5161', '#5b5d6e', '#5b5d6e', '#676a7c'], hi='#8a8da0', out='#15161c',
                     face=['#40424f', '#4a4c5c', '#4a4c5c'], seam='#2a2b35', pattern='bricks',
                     floor=['#3a3c48', '#414350', '#414350', '#484a58'], fdetail='#2a2b35', fstyle='squares'),
    'catacomb': dict(top=['#4d3d32', '#5a4638', '#5a4638', '#66513f'], hi='#80684f', out='#140e0a',
                     face=['#3f3128', '#4a392e', '#4a392e'], seam='#2a201a', pattern='niches', bone=['#d8ccb0', '#a89c84'],
                     floor=['#382c24', '#40332a', '#40332a', '#473930'], fdetail='#c8bca0', fstyle='earth_bones'),
}


def face_layer(th, seed):
    """Face du mur : 16 px de large (périodique), 19 px de haut (3 rangs sous la lèvre + 16)."""
    W, H = 16, 19
    n = pnoise(W, H + 13, 2, 2, seed)[:H]
    img = ramp(n * 0.85 + np.random.default_rng(seed + 1).random((H, W)) * 0.15, th['face'])
    seam = hexc(th['seam'])
    pat = th['pattern']
    yy, xx = np.mgrid[0:H, 0:W]
    if pat == 'columns':
        # orgues basaltiques : colonnes de 4 px, arête claire à gauche, joint sombre à droite
        img[(xx % 4) == 3] = seam
        img[(xx % 4) == 0] = img[(xx % 4) == 0] * 1.18
    elif pat in ('bricks', 'blocks', 'niches'):
        bh = 4 if pat == 'bricks' else 6
        row = yy // bh
        off = (row % 2) * (4 if pat == 'bricks' else 8)
        bw = 8 if pat == 'bricks' else 16
        joint_v = ((xx + off) % bw) == 0
        joint_h = (yy % bh) == 0
        img[joint_v | joint_h] = seam
        # arête claire en haut de chaque pierre
        img[((yy % bh) == 1) & ~joint_v] = img[((yy % bh) == 1) & ~joint_v] * 1.15
        if pat == 'niches':
            # niche à crâne au centre de la face
            for (x, y, c) in [(6, 8, 1), (7, 8, 0), (8, 8, 0), (9, 8, 1), (6, 9, 0), (7, 9, 1), (8, 9, 1), (9, 9, 0), (7, 10, 0), (8, 10, 0)]:
                img[y, x] = hexc(th['bone'][c]) if (x, y) not in ((7, 9), (8, 9)) else seam
            img[7:12, 5] = seam; img[7:12, 10] = seam; img[7, 5:11] = seam
    elif pat == 'embers':
        glow = [hexc(c) for c in th['glow']]
        for x0, y0, ln in ((4, 7, 7), (12, 10, 5)):
            x = x0
            for k in range(ln):
                y = y0 + k
                if y < H:
                    img[y, x] = glow[1] if k % 3 else glow[2]
                    if x + 1 < W: img[y, x + 1] = glow[0]
                x += (1 if k % 3 == 2 else 0)
    elif pat == 'ice':
        img[(xx % 5) == 2] = img[(xx % 5) == 2] * 1.1
        g = np.random.default_rng(seed + 3).random((H, W))
        img[g > 0.96] = hexc(th['glint'])
        img[(xx % 7) == 6] = seam
    elif pat == 'fungus':
        rng = np.random.default_rng(seed + 5)
        for _ in range(3):
            x, y = int(rng.integers(1, 15)), int(rng.integers(6, 16))
            c = hexc(th['caps'][int(rng.integers(0, len(th['caps'])))])
            img[y, x] = c
            if x + 1 < W: img[y, x + 1] = c * 0.8
            img[min(H - 1, y + 1), x] = hexc('#d8c8b0') * 0.7
        img[(yy < 3)] = np.where(pnoise(W, 32, 2, 2, seed + 9)[:3, :, None] > 0.5, hexc(th['moss']), img[(yy < 3)].reshape(3, W, 3)).reshape(-1, 3)
    elif pat == 'crystals':
        rng = np.random.default_rng(seed + 11)
        for _ in range(2):
            x, y = int(rng.integers(2, 14)), int(rng.integers(7, 15))
            for k in range(4):
                if y - k >= 0:
                    img[y - k, x] = hexc(th['shard'][1])
                    if x + 1 < W: img[y - k, x + 1] = hexc(th['shard'][0])
            img[max(0, y - 3), x] = hexc(th['shard'][2])
    if 'drip' in th:
        rng = np.random.default_rng(seed + 13)
        for _ in range(2):
            x = int(rng.integers(1, 15))
            for y in range(int(rng.integers(3, 9))):
                img[y, x] = hexc(th['drip']) * (1 - y * 0.06)
    # dégradé vertical : la face s'assombrit vers le pied
    shade = np.linspace(1.08, 0.72, H)[:, None, None]
    return np.clip(img * shade, 0, 255)


def top_layer(th, seed):
    """Dessus du mur (la masse rocheuse, la majorité de l'écran) : sombre et calme, juste un grain fin,
    pour ne pas faire apparaître la répétition des cases (16 x 8 px périodique)."""
    W, H = 16, 8
    base = hexc(th['top'][1]) * 0.6
    g = np.random.default_rng(seed + 21).random((H, W))
    n = pnoise(W, H, 2, 1, seed + 20)
    img = base[None, None, :] * (0.96 + 0.04 * n[:, :, None] + 0.05 * (g[:, :, None] - 0.5))
    if th['pattern'] == 'embers':
        img[g > 0.985] = hexc(th['glow'][0]) * 0.8
    return img


def source_block(th, seed):
    can = np.zeros((40, 48, 3))
    top = top_layer(th, seed)
    face = face_layer(th, seed)
    out = hexc(th['out'])
    hi = hexc(th['hi'])
    for y in range(40):
        for x in range(48):
            # coordonnée horizontale de texture : L = 0..7, IL = 0..7, IR = 8..15, R = 8..15
            u = x if x < 8 else (x - 8 if x < 16 else (x - 8 if x < 24 else x - 32))
            u %= 16
            if y < 21:
                c = top[y % 8, u].copy()
                if y <= 2:
                    c = c * (1.35 if y == 1 else 1.15) if y > 0 else out
                if 16 <= y <= 19:
                    c = c * (1.0 - (y - 15) * 0.07)
                if y == 20:
                    c = hi           # lèvre éclairée au bord du dessus
            else:
                v = y - 21 if y < 24 else y - 21
                v = min(18, v)
                c = face[v, u].copy()
                if y == 21:
                    c = c * 0.65     # ombre sous la lèvre
                if y == 39:
                    c = out
                elif y == 38:
                    c = c * 0.6
            can[y, x] = c
    # bords : contour, côté gauche éclairé, côté droit ombré
    for y in range(40):
        can[y, 0] = out
        can[y, 47] = out
        if 0 < y < 39:
            can[y, 1] = np.clip(can[y, 1] * 1.3, 0, 255)
            can[y, 46] = can[y, 46] * 0.75
    img = Image.fromarray(np.clip(can, 0, 255).astype(np.uint8), 'RGB').convert('RGBA')
    return Style(img, {"T": 0, "D": 8, "BT": 16, "F": 24}, {"L": 0, "IL": 8, "IR": 16, "R": 40})


# ─────────────────────────────────────────────
# SOLS
# ─────────────────────────────────────────────

def floor_texture(th, seed):
    st = th['fstyle']
    n = pnoise(P, P, 6, 6, seed + 40, 3)
    g = np.random.default_rng(seed + 41).random((P, P))
    img = ramp(n * 0.75 + g * 0.25, th['floor'])
    det = hexc(th['fdetail'])
    yy, xx = np.mgrid[0:P, 0:P]
    if st == 'gravel':
        img[g > 0.965] = det
        img[g < 0.02] = img[g < 0.02] * 0.75
    elif st == 'cracks_hot':
        r = np.abs(pnoise(P, P, 4, 4, seed + 42) - 0.5)
        img[r < 0.025] = det
        img[g > 0.985] = hexc('#a8361c')
    elif st == 'wet_tiles':
        seam = (xx % 16 == 0) | (yy % 16 == 0)
        img[seam] = img[seam] * 0.65
        p = pnoise(P, P, 3, 3, seed + 43)
        img[(p > 0.8) & ~seam] = img[(p > 0.8) & ~seam] * 0.6 + det * 0.4
        img[(p > 0.86) & (g > 0.85) & ~seam] = hexc('#5cc4c0') * 0.8
    elif st == 'frost':
        r = np.abs(pnoise(P, P, 3, 3, seed + 44) - 0.5)
        img[r < 0.02] = det
        img[g > 0.98] = hexc('#eaf8ff')
    elif st == 'moss':
        p = pnoise(P, P, 4, 4, seed + 45)
        img[p > 0.74] = img[p > 0.74] * 0.5 + det * 0.5
        img[(p > 0.82) & (g > 0.7)] = det
        img[(g > 0.993)] = hexc('#7fe0c0')
    elif st == 'polished':
        seam = (xx % 16 == 0) | (yy % 16 == 0)
        img[seam] = img[seam] * 0.7
        img[(xx % 16 == 1) | (yy % 16 == 1)] = img[(xx % 16 == 1) | (yy % 16 == 1)] * 1.12
        img[g > 0.988] = hexc('#c9a8ff')
    elif st == 'flagstones':
        # dalles irrégulières : rangées de 12 px, joints décalés
        row = yy // 12
        off = (row * 7) % 16
        seam = ((xx + off) % 16 == 0) | (yy % 12 == 0)
        img[seam] = det
        img[(yy % 12 == 1) & ~seam] = img[(yy % 12 == 1) & ~seam] * 1.12
        r = np.abs(pnoise(P, P, 3, 3, seed + 46) - 0.5)
        img[(r < 0.012) & ~seam] = det
    elif st == 'squares':
        seam = (xx % 8 == 0) | (yy % 8 == 0)
        img[seam] = det
        hi = ((xx % 8 == 1) | (yy % 8 == 1)) & ~seam
        img[hi] = img[hi] * 1.12
        lo = ((xx % 8 == 7) | (yy % 8 == 7)) & ~seam
        img[lo] = img[lo] * 0.85
    elif st == 'earth_bones':
        img[g > 0.994] = det
        img[(g > 0.985) & (g <= 0.994)] = det * 0.6
    return np.clip(img, 0, 255).astype(np.uint8)


def save_floor(tid, th, seed):
    tex = floor_texture(th, seed)
    out = Image.new('RGBA', (16 * 9, 16))
    for py in range(3):
        for px in range(3):
            t = Image.fromarray(tex[py * 16:(py + 1) * 16, px * 16:(px + 1) * 16], 'RGB').convert('RGBA')
            out.paste(t, ((py * 3 + px) * 16, 0))
    out.save(os.path.join(FLOORS, f'floor_{tid}.png'))
    return Image.fromarray(tex, 'RGB').convert('RGBA')


def preview(atlas, floor_tex, path):
    H, W = len(SAMPLE), len(SAMPLE[0])
    img = Image.new('RGBA', (W * 16, H * 16))
    for y in range(H):
        for x in range(W):
            fx, fy = (x % 3) * 16, (y % 3) * 16
            img.paste(floor_tex.crop((fx, fy, fx + 16, fy + 16)), (x * 16, y * 16))

    def wall(x, y):
        if x < 0 or y < 0 or x >= W or y >= H:
            return True
        return SAMPLE[y][x] == '#'
    for y in range(H):
        for x in range(W):
            if not wall(x, y):
                continue
            m = (wall(x, y - 1) << 0) | (wall(x + 1, y) << 1) | (wall(x, y + 1) << 2) | (wall(x - 1, y) << 3) \
                | (wall(x, y + 2) << 4) | (wall(x - 1, y + 1) << 5) | (wall(x + 1, y + 1) << 6)
            t = atlas.crop(((m % 16) * 16, (m // 16) * 16, (m % 16) * 16 + 16, (m // 16) * 16 + 16))
            img.alpha_composite(t, (x * 16, y * 16))
    return img


def main():
    os.makedirs(WALLS, exist_ok=True)
    os.makedirs(FLOORS, exist_ok=True)
    previews = []
    for i, (tid, th) in enumerate(THEMES.items()):
        seed = 1000 + i * 17
        st = source_block(th, seed)
        atlas = Image.new('RGBA', (256, 128))
        for m in range(128):
            atlas.paste(compose(st, m), ((m % 16) * 16, (m // 16) * 16))
        atlas.save(os.path.join(WALLS, f'walls_{tid}.png'))
        ft = save_floor(tid, th, seed)
        previews.append(preview(atlas, ft, None))
    w, h = previews[0].size
    sheet = Image.new('RGBA', (w * 3 + 8, h * 3 + 8), (20, 18, 26, 255))
    for i, p in enumerate(previews):
        sheet.paste(p, ((i % 3) * (w + 4), (i // 3) * (h + 4)))
    sheet.resize((sheet.width * 2, sheet.height * 2), Image.NEAREST).save(os.path.join(ROOT, 'Temp', 'dungeon_preview.png'))
    print('donjon ok :', ', '.join(THEMES))


if __name__ == '__main__':
    main()
