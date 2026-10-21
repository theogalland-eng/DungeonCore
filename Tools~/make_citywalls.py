"""
ENCEINTE DE LA CITÉ : pièces de rempart découpées dans les tuiles du pack du manoir (même pierre que les scènes
des bâtiments), et palissade de rondins dessinée ici (stade Village).

Une case de jeu = 2 x 2 tuiles de 16 px (32 px/unité). Pièces (pivot au milieu du bord inférieur) :
  wall_h       : rempart est-ouest vu de face (créneaux, parement)            32 x 60
  wall_v       : rempart nord-sud vu de dessus (chemin de ronde)               32 x 32
  wall_v_cap   : bout nord d'un rempart nord-sud (créneaux du bout)            32 x 32
  wall_v_face  : bout sud d'un rempart nord-sud (parement qui fait face)       32 x 48
  wall_tower   : tour de porte                                                 32 x 80
  palisade_h   : palissade est-ouest                                           32 x 40
  palisade_v   : palissade nord-sud (têtes de pieux vues de dessus)            32 x 32
Sortie : Assets/Art/Settlement/Walls/*.png (hors du dépôt public : tuiles du pack).
"""
import os
import random
from PIL import Image, ImageDraw

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
SRC = None
for d in os.listdir(os.path.join(ROOT, 'Assets', 'imports', 'new')):
    if d.startswith('craftpix-net-653272') and not d.endswith('.meta'):
        SRC = os.path.join(ROOT, 'Assets', 'imports', 'new', d, 'PNG', 'Walls_street.png')
OUT = os.path.join(ROOT, 'Assets', 'Art', 'Settlement', 'Walls')
T, COLS = 16, 21


def tile(sheet, i):
    x, y = (i % COLS) * T, (i // COLS) * T
    return sheet.crop((x, y, x + T, y + T))


def stack(sheet, rows):
    """rows : liste de (gauche, droite) de haut en bas → image 32 x 16*len(rows)."""
    im = Image.new('RGBA', (2 * T, T * len(rows)), (0, 0, 0, 0))
    for r, (a, b) in enumerate(rows):
        im.alpha_composite(tile(sheet, a), (0, r * T))
        im.alpha_composite(tile(sheet, b), (T, r * T))
    return im


def palisade_h():
    rnd = random.Random(3)
    im = Image.new('RGBA', (32, 40), (0, 0, 0, 0))
    d = ImageDraw.Draw(im)
    dark, mid, light, outline = (88, 58, 36), (122, 82, 50), (156, 110, 66), (44, 28, 20)
    x = 0
    while x < 32:
        w = 6
        top = 4 + rnd.randint(0, 3)
        d.rectangle([x, top + 3, x + w - 1, 37], fill=mid)
        d.polygon([(x, top + 3), (x + w // 2, top - 1), (x + w - 1, top + 3)], fill=mid)
        d.line([(x + 1, top + 4), (x + 1, 36)], fill=light)
        d.line([(x + w - 2, top + 4), (x + w - 2, 36)], fill=dark)
        d.line([(x, top + 3), (x, 37)], fill=outline)
        d.line([(x, top + 3), (x + w // 2, top - 1), (x + w - 1, top + 3)], fill=outline)
        x += w
    # traverses
    for y in (14, 28):
        d.rectangle([0, y, 31, y + 2], fill=dark)
        d.line([(0, y), (31, y)], fill=light)
    d.line([(0, 38), (31, 38)], fill=(30, 22, 16, 140))
    d.line([(0, 39), (31, 39)], fill=(30, 22, 16, 80))
    return im


def palisade_v():
    """Palissade nord-sud vue de 3/4 : rangée de pieux serrés, pointes vers le haut, flanc ombré à droite."""
    im = Image.new('RGBA', (32, 32), (0, 0, 0, 0))
    d = ImageDraw.Draw(im)
    dark, mid, light, outline = (88, 58, 36), (122, 82, 50), (156, 110, 66), (44, 28, 20)
    x0, x1 = 12, 20
    for y in range(-4, 32, 5):
        d.polygon([(x0, y + 6), (16, y), (x1, y + 6), (x1, y + 9), (x0, y + 9)], fill=mid, outline=outline)
        d.line([(x0 + 1, y + 6), (15, y + 2)], fill=light)
        d.line([(x1 - 1, y + 6), (x1 - 1, y + 8)], fill=dark)
    d.line([(x1 + 1, 0), (x1 + 1, 31)], fill=(30, 22, 16, 120))
    return im


def wall_front():
    """Rempart vu de face : un tronçon de 2 tuiles pris au milieu du grand bastion de la tour des mages
    (chemin de ronde, créneaux, parement), raccordable bout à bout."""
    import importlib.util
    spec = importlib.util.spec_from_file_location('ms', os.path.join(ROOT, 'Tools~', 'make_stamps.py'))
    ms = importlib.util.module_from_spec(spec); spec.loader.exec_module(ms)
    tw, th, tilesets, layers = ms.load_map(ms.pack(289481, 'Tiled_files', 'Exterior.tmx'))
    cells = dict(next(c for n, c in layers if n == 'Walls'))
    xs = [x for x, y in cells]; ys = [y for x, y in cells]
    W, H = (max(xs) - min(xs) + 1) * tw, (max(ys) - min(ys) + 1) * th
    img = Image.new('RGBA', (W, H))
    for (x, y), gid in cells.items():
        ts, local, flags = ms.resolve(tilesets, gid)
        if ts is None or ts.im is None:
            continue
        img.alpha_composite(ts.tile(local, flags), ((x - min(xs)) * tw, (y - min(ys)) * th))
    # colonne au milieu du bastion : on cherche le bas du parement (première ligne d'herbe sous le mur)
    x0 = (W // 2 // tw) * tw
    col = [img.getpixel((x0 + 8, y)) for y in range(H)]
    bottom = next(y for y in range(H - 1, 0, -1) if col[y][3] > 0 and col[y][2] > col[y][1] and col[y - 1][2] > col[y - 1][1])
    b = bottom - 2   # dernière rangée du parement
    # rempart plus haut (2 cases) : deux rangées de briques du parement sont répétées
    out = Image.new('RGBA', (32, 60))
    out.alpha_composite(img.crop((x0, b - 43, x0 + 32, b - 7)), (0, 0))
    out.alpha_composite(img.crop((x0, b - 23, x0 + 32, b - 7)), (0, 36))
    out.alpha_composite(img.crop((x0, b - 7, x0 + 32, b + 1)), (0, 52))
    return out


def warm(img):
    """Pierre bleutée des packs → grès chaud, accordé aux rues de briques et aux cours des bâtiments."""
    img = img.convert('RGBA')
    px = img.load()
    ramp = [(40, 33, 30), (74, 62, 54), (118, 100, 84), (160, 140, 116), (200, 182, 152), (226, 212, 184)]
    for y in range(img.height):
        for x in range(img.width):
            r, g, b, a = px[x, y]
            if a == 0:
                continue
            l = (0.3 * r + 0.59 * g + 0.11 * b) / 255.0
            l = min(1.0, max(0.0, (l - 0.12) / 0.62))
            t = l * (len(ramp) - 1)
            i = min(int(t), len(ramp) - 2)
            f = t - i
            c = tuple(int(ramp[i][k] * (1 - f) + ramp[i + 1][k] * f) for k in range(3))
            px[x, y] = (c[0], c[1], c[2], a)
    return img


def tower_big():
    """Tour octogonale de la prison (couche « Prison_tower »)."""
    import importlib.util
    spec = importlib.util.spec_from_file_location('ms', os.path.join(ROOT, 'Tools~', 'make_stamps.py'))
    ms = importlib.util.module_from_spec(spec); spec.loader.exec_module(ms)
    tw, th, tilesets, layers = ms.load_map(ms.pack(659586, 'Tiled_files', 'Exterior.tmx'))
    cells = dict(next(c for n, c in layers if n == 'Prison_tower'))
    xs = [x for x, y in cells]; ys = [y for x, y in cells]
    img = Image.new('RGBA', ((max(xs) - min(xs) + 1) * tw, (max(ys) - min(ys) + 1) * th))
    for (x, y), gid in cells.items():
        ts, local, flags = ms.resolve(tilesets, gid)
        if ts is None or ts.im is None:
            continue
        img.alpha_composite(ts.tile(local, flags), ((x - min(xs)) * tw, (y - min(ys)) * th))
    return img.crop(img.getbbox())


def main():
    os.makedirs(OUT, exist_ok=True)
    sheet = Image.open(SRC).convert('RGBA')
    # rempart vu de dessus : le pilier du pack (26 px de large) recadré sur 32 px
    def px(x0, y0, x1, y1):
        return sheet.crop((x0, y0, x1, y1))
    body = px(8, 16, 40, 32)
    wv = Image.new('RGBA', (32, 32)); wv.alpha_composite(body, (0, 0)); wv.alpha_composite(body, (0, 16))
    pieces = {
        'wall_h': wall_front(),
        'wall_v': wv,
        'wall_v_cap': px(8, 0, 40, 32),
        'wall_v_face': px(8, 32, 40, 80),
        'wall_tower': px(8, 0, 40, 80),
        'wall_tower_big': tower_big(),
        'palisade_h': palisade_h(),
        'palisade_v': palisade_v(),
    }
    for name, im in pieces.items():
        if name.startswith('wall'):
            im = warm(im)
            pieces[name] = im
        im.save(os.path.join(OUT, name + '.png'))
    prev = Image.new('RGBA', (sum(p.width for p in pieces.values()) + 8 * len(pieces), 110), (60, 90, 50, 255))
    x = 0
    for p in pieces.values():
        prev.alpha_composite(p, (x, 110 - p.height))
        x += p.width + 8
    os.makedirs(os.path.join(ROOT, 'Temp'), exist_ok=True)
    prev.resize((prev.width * 4, prev.height * 4), Image.NEAREST).save(os.path.join(ROOT, 'Temp', 'citywalls.png'))
    print('enceinte :', ', '.join(pieces))


if __name__ == '__main__':
    main()
