"""Détails de sol du donjon (procéduraux, sans asset tiers) : fissures, cailloux, ossements, mousse, flaques, gravats.
Semés au hasard (mais toujours au même endroit pour une case donnée) sur les cases creusées par DungeonDecals,
ils cassent la répétition des tuiles. Images de 16 px (une case), discrètes : elles s'accordent à tous les thèmes.
Sortie : Assets/Resources/Decals/decal_<nom>.png  (PPU 16 réglé par DungeonDecals à l'import)
Usage : python "Tools~/make_decals.py"
"""
import math
import os
import random

from PIL import Image, ImageDraw

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
OUT = os.path.join(ROOT, 'Assets', 'Resources', 'Decals')
S = 16
DARK = (24, 20, 28)
SHADE = (40, 34, 44)
BONE = [(214, 206, 186), (176, 166, 146), (122, 112, 98)]
STONE = [(108, 104, 112), (84, 80, 90), (60, 56, 66), (136, 132, 140)]
MOSS = [(52, 84, 46), (74, 112, 58), (104, 140, 72)]
WATER = [(30, 44, 70), (46, 66, 100), (90, 120, 160)]


def new():
    return Image.new('RGBA', (S, S), (0, 0, 0, 0))


def crack(seed):
    r = random.Random(seed)
    im = new()
    d = ImageDraw.Draw(im)
    x, y = r.randint(3, 12), r.randint(3, 12)
    ang = r.uniform(0, math.tau)
    pts = [(x, y)]
    for _ in range(r.randint(4, 7)):
        ang += r.uniform(-0.9, 0.9)
        x = max(0, min(S - 1, x + math.cos(ang) * 2.2))
        y = max(0, min(S - 1, y + math.sin(ang) * 2.2))
        pts.append((round(x), round(y)))
    d.line(pts, fill=DARK + (200,), width=1)
    # petite branche
    if len(pts) > 3:
        bx, by = pts[2]
        d.line([(bx, by), (bx + r.choice((-2, 2)), by + r.choice((-2, 2)))], fill=DARK + (170,))
    return im


def pebbles(seed):
    r = random.Random(seed)
    im = new()
    px = im.load()
    for _ in range(r.randint(3, 5)):
        x, y = r.randint(2, 13), r.randint(2, 13)
        w = r.choice((1, 2))
        c = r.choice(STONE[:3])
        for dx in range(w):
            for dy in range(w):
                px[x + dx, y + dy] = c + (230,)
        px[x, y] = STONE[3] + (230,)            # éclat de lumière en haut à gauche
        if y + w < S:
            for dx in range(w):
                px[x + dx, y + w] = DARK + (120,)   # ombre portée
    return im


def bones(seed):
    r = random.Random(seed)
    im = new()
    d = ImageDraw.Draw(im)
    for _ in range(r.randint(1, 2)):
        x, y = r.randint(3, 11), r.randint(4, 11)
        ang = r.uniform(0, math.pi)
        L = r.randint(3, 5)
        x2, y2 = x + math.cos(ang) * L, y + math.sin(ang) * L
        d.line([(x, y + 1), (x2, y2 + 1)], fill=DARK + (110,))
        d.line([(x, y), (x2, y2)], fill=BONE[1] + (255,))
        for (ex, ey) in ((x, y), (x2, y2)):
            d.point((round(ex), round(ey)), fill=BONE[0] + (255,))
            d.point((round(ex) + 1, round(ey)), fill=BONE[2] + (255,))
    return im


def skull(seed):
    im = new()
    d = ImageDraw.Draw(im)
    x, y = 6, 6
    d.ellipse([x, y + 1, x + 5, y + 6], fill=DARK + (110,))
    d.ellipse([x, y, x + 5, y + 5], fill=BONE[1] + (255,))
    d.point((x + 1, y + 1), fill=BONE[0] + (255,))
    d.point((x + 1, y + 3), fill=DARK + (255,))
    d.point((x + 3, y + 3), fill=DARK + (255,))
    d.line([(x + 1, y + 5), (x + 4, y + 5)], fill=BONE[2] + (255,))
    return im


def moss(seed):
    r = random.Random(seed)
    im = new()
    px = im.load()
    cx, cy = r.randint(5, 10), r.randint(5, 10)
    for y in range(S):
        for x in range(S):
            dd = math.hypot((x - cx) * 0.9, (y - cy) * 1.2) + r.uniform(-1.2, 1.2)
            if dd < 4.2:
                k = 2 if dd < 1.6 else 1 if dd < 3 else 0
                px[x, y] = MOSS[k] + (210 if k else 160,)
    return im


def puddle(seed):
    r = random.Random(seed)
    im = new()
    d = ImageDraw.Draw(im)
    x, y = r.randint(2, 5), r.randint(4, 8)
    w, h = r.randint(7, 10), r.randint(4, 5)
    d.ellipse([x, y, x + w, y + h], fill=WATER[0] + (190,))
    d.ellipse([x + 1, y + 1, x + w - 1, y + h - 1], fill=WATER[1] + (190,))
    d.line([(x + 2, y + 1), (x + 4, y + 1)], fill=WATER[2] + (220,))
    return im


def rubble(seed):
    r = random.Random(seed)
    im = new()
    px = im.load()
    for _ in range(r.randint(10, 16)):
        x, y = r.randint(1, 14), r.randint(1, 14)
        px[x, y] = r.choice(STONE) + (r.randint(140, 220),)
    return im


KINDS = {'crack': (crack, 4), 'pebbles': (pebbles, 3), 'bones': (bones, 3), 'skull': (skull, 1),
         'moss': (moss, 3), 'puddle': (puddle, 2), 'rubble': (rubble, 3)}

if __name__ == '__main__':
    os.makedirs(OUT, exist_ok=True)
    sheet = Image.new('RGBA', (S * 8, S * 4), (70, 66, 76, 255))
    i = 0
    for name, (fn, n) in KINDS.items():
        for k in range(n):
            im = fn(1000 + 37 * k + len(name))
            im.save(os.path.join(OUT, f'decal_{name}_{k}.png'))
            sheet.alpha_composite(im, ((i % 8) * S, (i // 8) * S))
            i += 1
    sheet.resize((sheet.width * 8, sheet.height * 8), Image.NEAREST).save(os.path.join(os.environ.get('TEMP', '.'), 'decals_preview.png'))
    print('ok', i, 'détails')
