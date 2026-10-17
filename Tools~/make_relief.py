"""
Relief 2,5D de la montagne-volcan (vue « île »).
Entrée  : Temp/relief_mask.json (exporté par l'éditeur : cases de la montagne, entrée de la grotte)
Sortie  : Assets/Art/Generated/Relief/band_XX.png + manifest.json
Principe :
  - hauteur = distance au bord de la montagne (crête), + cônes volcaniques, + cratère actif au sommet ;
  - hauteur arrondie en terrasses (lecture « pixel art ») ;
  - projection oblique : chaque point du sol est dessiné décalé vers le haut de sa hauteur,
    les rangées du nord vers le sud (le sud passe devant) : sommets éclairés + falaises côté sud ;
  - l'image est découpée en bandes selon la rangée d'origine, pour que Unity trie correctement
    personnages et arbres devant / derrière la montagne.
Usage : python "Tools~/make_relief.py"
"""
import json, math, os, sys
# Lancé depuis Unity, Python peut ignorer le dossier de paquets de l'utilisateur : on l'ajoute
_user_site = os.path.join(os.path.expanduser('~'), 'AppData', 'Roaming', 'Python',
                          f'Python{sys.version_info.major}{sys.version_info.minor}', 'site-packages')
if os.path.isdir(_user_site) and _user_site not in sys.path:
    sys.path.append(_user_site)
try:
    import numpy as np
except ImportError:
    sys.stderr.write(f'python={sys.executable} {sys.version} site={_user_site} exists={os.path.isdir(_user_site)} home={os.path.expanduser("~")}' + chr(10))
    raise
from PIL import Image
from scipy import ndimage

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
MASK = os.path.join(ROOT, 'Temp', 'relief_mask.json')
OUT = os.path.join(ROOT, 'Assets', 'Art', 'Generated', 'Relief')
PX = 16                 # pixels par case (comme les tuiles)
STEP = 14               # hauteur d'une corniche (pixels) : peu de falaises, bien marquées
BAND_ROWS = 3           # rangées de cases par bande de tri
rng = np.random.default_rng(7)


def smooth_noise(h, w, scale, octaves=3, seed=0):
    r = np.random.default_rng(seed)
    total = np.zeros((h, w))
    amp, s = 1.0, scale
    norm = 0
    for _ in range(octaves):
        gh, gw = max(2, int(h / s) + 2), max(2, int(w / s) + 2)
        g = r.random((gh, gw))
        total += amp * ndimage.zoom(g, (h / (gh - 1), w / (gw - 1)), order=3)[:h, :w]
        norm += amp
        amp *= 0.5
        s /= 2
    return total / norm


def main():
    d = json.load(open(MASK))
    cw, ch = d['w'], d['h']
    rows = d['rows']                     # rangée 0 = la plus au sud (y minimal)
    cell = np.zeros((ch, cw), bool)
    for y, row in enumerate(rows):
        for x, c in enumerate(row):
            cell[y, x] = c == '1'
    # --- masque au pixel, image « du nord vers le sud » (ligne 0 = nord)
    cellN = cell[::-1]
    W, Hf = cw * PX, ch * PX
    mask = np.kron(cellN, np.ones((PX, PX), bool))
    # bords adoucis (pas d'escalier de cases) : petit flou puis seuil
    soft = ndimage.gaussian_filter(mask.astype(float), 4.5)
    edge_noise = smooth_noise(Hf, W, 14, 2, 31)
    mask = soft + (edge_noise - 0.5) * 0.35 > 0.5

    # --- hauteur
    dist = ndimage.distance_transform_edt(mask)          # pixels jusqu'au bord
    dmax = dist.max()
    ridge = np.minimum(dist, 11 * PX) / (11 * PX)          # crête qui monte puis plafonne
    elev = ridge ** 0.8 * 58.0
    # cônes volcaniques : maxima de distance éloignés les uns des autres
    peaks = []
    dd = dist.copy()
    for _ in range(4):
        iy, ix = np.unravel_index(np.argmax(dd), dd.shape)
        if dd[iy, ix] < 6 * PX:
            break
        peaks.append((iy, ix, dd[iy, ix]))
        yy, xx = np.ogrid[:Hf, :W]
        dd[(yy - iy) ** 2 + (xx - ix) ** 2 < (22 * PX) ** 2] = 0
    yy, xx = np.mgrid[:Hf, :W]
    crater = None
    for i, (iy, ix, dv) in enumerate(peaks):
        r = np.sqrt((yy - iy) ** 2 + (xx - ix) ** 2)
        rad = (13 if i == 0 else 9) * PX
        cone = np.clip(1 - r / rad, 0, 1) ** 1.3 * (46 if i == 0 else 26)
        elev += cone * mask
        if i == 0:
            crater = (iy, ix)
    # relief irrégulier
    n1 = smooth_noise(Hf, W, 90, 3, 1)
    elev += (n1 - 0.5) * 14 * np.clip(dist / (3 * PX), 0, 1)
    # cratère : cuvette au sommet principal
    lava = np.zeros_like(mask)
    rim = np.zeros_like(mask)
    if crater:
        cy, cx = crater
        r = np.sqrt((yy - cy) ** 2 + ((xx - cx) * 0.85) ** 2)
        R = 3.2 * PX
        top = elev[cy, cx]
        inside = r < R
        elev = np.where(inside, np.minimum(elev, top - 10 - (R - r) * 0.35), elev)
        lava = (r < R * 0.62) & mask
        rim = (r >= R * 0.8) & (r < R * 1.15) & mask
    elev = np.where(mask, np.maximum(elev, 0), 0)
    # corniches : pentes douces entre des falaises espacées
    terr = np.floor(elev / STEP) * STEP + np.mod(elev, STEP) * 0.3
    terr = np.where(mask, np.floor(terr), -1)

    # --- éclairage (pente de la hauteur lissée, lumière nord-ouest)
    es = ndimage.gaussian_filter(elev, 3)
    gy, gx = np.gradient(es)
    # lumière venant du nord-ouest : les pentes tournées vers elle sont claires
    # (le flanc sud, face au joueur, reste lisible : lumière surtout zénithale)
    shade = np.clip(1.0 + gx * 0.45 + gy * 0.5, 0.68, 1.3)
    shade = np.round(shade * 6) / 6                           # paliers (pixel art)

    # --- palette
    tex = rng.random((Hf, W))
    speck = smooth_noise(Hf, W, 18, 2, 5)
    moss = smooth_noise(Hf, W, 40, 2, 9)
    hn = np.clip(elev / (elev.max() + 1e-6), 0, 1)
    rock_low = np.array([96, 82, 70])      # roche brune, chaude
    rock_mid = np.array([74, 66, 66])      # basalte
    rock_high = np.array([52, 46, 50])     # cendres sombres près du sommet
    base = np.where(hn[..., None] < 0.5,
                    rock_low + (rock_mid - rock_low) * (hn[..., None] / 0.5),
                    rock_mid + (rock_high - rock_mid) * ((hn[..., None] - 0.5) / 0.5))
    # végétation au pied des pentes (herbes, buissons), lichens plus haut
    green = (hn < 0.3) & (moss > 0.5 + hn)
    base = np.where(green[..., None], np.array([58, 96, 50]) * (0.85 + 0.3 * moss[..., None]), base)
    lichen = (hn >= 0.3) & (hn < 0.55) & (moss > 0.7)
    base = np.where(lichen[..., None], base * 0.7 + np.array([88, 100, 70]) * 0.3, base)
    # traînées de cendre claire sur les hauteurs
    ash = (hn > 0.6) & (speck_ash := smooth_noise(Hf, W, 10, 2, 17)) .__gt__(0.66)
    base = np.where(ash[..., None], base * 0.6 + np.array([128, 120, 116]) * 0.4, base)
    # grain de roche (3 nuances) + taches sombres
    grain = np.where(tex < 0.12, 0.84, np.where(tex > 0.9, 1.1, 1.0))
    # grain des vraies tuiles de l'île (16 px, aligné sur la grille du monde)
    def tile_detail(name):
        path = os.path.join(ROOT, 'Temp', f'relief_tex_{name}.png')
        if not os.path.exists(path):
            return None
        t = np.asarray(Image.open(path).convert('L'), float)
        t = (t - t.mean()) / (t.std() + 1e-6)
        reps = (Hf // t.shape[0] + 1, W // t.shape[1] + 1)
        return np.tile(t, reps)[:Hf, :W]
    dv = [x for x in (tile_detail('volcanic_a'), tile_detail('volcanic_b'), tile_detail('scree'), tile_detail('gravel_a')) if x is not None]
    if dv:
        patch = smooth_noise(Hf, W, 30, 2, 41)
        det = np.where(hn > 0.45, np.where(patch > 0.5, dv[0], dv[1 % len(dv)]), dv[min(2, len(dv) - 1)])
        grain = np.clip(1.0 + det * 0.13, 0.7, 1.3) * np.where(tex < 0.05, 0.9, 1.0)
    top_col = base * shade[..., None] * grain[..., None]
    top_col = np.where((speck > 0.72)[..., None], top_col * 0.8, top_col)
    # fissures de lave près du sommet
    cracks = smooth_noise(Hf, W, 12, 2, 13)
    hot = (hn > 0.72) & (np.abs(cracks - 0.5) < 0.018)
    top_col = np.where(hot[..., None], np.array([235, 96, 34]), top_col)
    # cratère : bord clair, fond de lave
    top_col = np.where(rim[..., None], top_col * 1.15 + 12, top_col)
    lava_col = np.array([255, 120, 30]) * (0.85 + 0.3 * smooth_noise(Hf, W, 6, 2, 21))[..., None]
    lava_col = np.where((smooth_noise(Hf, W, 5, 1, 22) > 0.62)[..., None], np.array([255, 214, 90]), lava_col)
    top_col = np.where(lava[..., None], lava_col, top_col)
    top_col = np.clip(top_col, 0, 255)

    face_noise = smooth_noise(Hf, W, 6, 2, 3)

    # --- projection oblique
    extra = int(terr.max()) + 8
    H = Hf + extra
    img = np.zeros((H, W, 4), np.uint8)
    src = np.full((H, W), -1, np.int32)        # rangée de case d'origine (0 = sud)
    maxE = int(terr.max())
    for y in range(Hf):
        row = terr[y]
        valid = row >= 0
        if not valid.any():
            continue
        e = np.where(valid, row, 0).astype(int)
        south = terr[y + 1] if y + 1 < Hf else np.full(W, -1)
        cell_row = ch - 1 - y // PX
        # colonne : du sommet (y + extra - e) jusqu'au pied (y + extra)
        for k in range(0, maxE + 1):
            sel = valid & (e >= k)
            if not sel.any():
                continue
            sy = y + extra - k
            xs = np.nonzero(sel)[0]
            if k == e[xs].max() and False:
                pass
            depth = e[xs] - k                    # 0 = dessus, >0 = face de falaise
            # une « falaise » d'un ou deux pixels n'est qu'une pente douce : on la colore comme le dessus
            drop0 = e[xs] - np.where(south[xs] >= 0, south[xs], 0)
            is_top = (depth == 0) | (drop0 <= 2)
            # face : plus sombre vers le bas, stries verticales
            f = 0.46 + 0.16 * face_noise[y, xs] - 0.008 * np.minimum(depth, 30)
            if dv:
                # falaises : grain d'éboulis étiré verticalement
                fd = dv[min(2, len(dv) - 1)][(sy // 2) % Hf, xs]
                f = f * np.clip(1.0 + fd * 0.12, 0.75, 1.25)
            face = rock_mid * 0.95 * f[:, None]
            # liseré clair en haut des vraies falaises seulement
            drop = e[xs] - np.where(south[xs] >= 0, south[xs], 0)
            face = np.where(((depth == 1) & (drop >= 4))[:, None], face * 1.4 + 10, face)
            col = np.where(is_top[:, None], top_col[y, xs], face)
            img[sy, xs, :3] = np.clip(col, 0, 255).astype(np.uint8)
            img[sy, xs, 3] = 255
            src[sy, xs] = cell_row
        # ombre portée au pied, côté sud (légère)
    # contour sombre (lisibilité pixel art)
    alpha = img[..., 3] > 0
    edge = alpha & ~ndimage.binary_erosion(alpha)
    img[edge, :3] = (img[edge, :3] * 0.55).astype(np.uint8)

    # --- bouche de la grotte (entrées)
    ents = d.get('entrances', [])
    for i in range(0, len(ents), 2):
        ex, ey = ents[i] - d['minX'], ents[i + 1] - d['minY']
        px = ex * PX + PX // 2
        # pied de la falaise au-dessus de la case d'entrée (image : rangée nord→sud)
        fy = (ch - 1 - ey) * PX + extra + PX // 2
        # une faille étroite et irrégulière au pied de la falaise, à peine visible : mousse et éboulis autour
        frng = np.random.default_rng(77 + i)
        top = -30
        for oy in range(top, 6):
            t = (oy - top) / (6 - top)                       # 0 en haut, 1 au pied
            half = 0.6 + 4.2 * math.sin(math.pi * min(1.0, t * 1.15)) ** 0.8
            shift = int(round(2.2 * math.sin(oy * 0.45)))
            for ox in range(-8, 9):
                X, Y = px + ox + shift, fy + oy
                if not (0 <= X < W and 0 <= Y < H) or img[Y, X, 3] == 0:
                    continue
                dd = abs(ox) - half
                if dd < 0:
                    dark = 10 + int(10 * t)
                    img[Y, X, :3] = (dark, dark - 3, dark + 5)
                elif dd < 1.0:
                    img[Y, X, :3] = (46, 41, 44) if ox < 0 else (87, 80, 79)
        # éboulis et touffes de mousse qui cachent à moitié la faille
        for k in range(16):
            ox, oy = int(frng.integers(-12, 13)), int(frng.integers(-20, 7))
            if abs(ox) < 3 and oy < 2:
                continue
            X, Y = px + ox, fy + oy
            if 0 <= X < W - 1 and 0 <= Y < H - 1 and img[Y, X, 3] > 0:
                col = (70, 117, 58) if k % 3 else (87, 80, 79)
                img[Y:Y + 2, X:X + 2, :3] = col
                img[Y, X, :3] = (121, 168, 68) if k % 3 else (125, 116, 108)

    # --- découpe en bandes
    os.makedirs(OUT, exist_ok=True)
    for f in os.listdir(OUT):
        if f.startswith('band_') or f == 'manifest.json':
            os.remove(os.path.join(OUT, f))
    bands = []
    nb = (ch + BAND_ROWS - 1) // BAND_ROWS
    for b in range(nb):
        r0, r1 = b * BAND_ROWS, min(ch, (b + 1) * BAND_ROWS)
        sel = (src >= r0) & (src < r1)
        if not sel.any():
            continue
        ys, xs = np.nonzero(sel)
        y0, y1, x0, x1 = ys.min(), ys.max() + 1, xs.min(), xs.max() + 1
        part = np.zeros((y1 - y0, x1 - x0, 4), np.uint8)
        m = sel[y0:y1, x0:x1]
        part[m] = img[y0:y1, x0:x1][m]
        name = f'band_{b:02d}.png'
        Image.fromarray(part, 'RGBA').save(os.path.join(OUT, name))
        bands.append({
            'file': name, 'x0': int(x0), 'w': int(x1 - x0), 'h': int(y1 - y0),
            # position du bas de l'image, en pixels depuis le bas du canevas
            'bottom': int(H - y1),
            # pied de la bande (bas de sa rangée la plus au sud), en pixels depuis le bas du canevas
            'pivot': int(r0 * PX),
        })
    man = {
        'minX': d['minX'], 'minY': d['minY'], 'px': PX, 'canvasH': H, 'extra': extra, 'bands': bands,
        'crater': None, 'peaks': [],
    }
    if crater:
        cy, cx = crater
        man['crater'] = [cx / PX + d['minX'], (Hf - cy) / PX + d['minY'] + float(elev[cy, cx]) / PX]
    for (iy, ix, dv) in peaks[1:]:
        man['peaks'].append([ix / PX + d['minX'], (Hf - iy) / PX + d['minY'] + float(elev[iy, ix]) / PX])
    json.dump(man, open(os.path.join(OUT, 'manifest.json'), 'w'), indent=1)
    Image.fromarray(img, 'RGBA').save(os.path.join(ROOT, 'Temp', 'relief_full.png'))
    print(f'relief {W}x{H}, {len(bands)} bandes, sommets {len(peaks)}, hauteur max {terr.max():.0f}px')


if __name__ == '__main__':
    main()
