"""Bannière du README (docs/banner.png) : nuit sur la mer, volcan, orbe et anneaux de mana, titre en Cinzel.
Entièrement procédurale (aucun asset tiers). Usage : python "Tools~/make_banner.py"
"""
import math
import os
import random

from PIL import Image, ImageDraw, ImageFilter, ImageFont

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
OUT = os.path.join(ROOT, 'docs', 'banner.png')
FONTS = os.path.join(ROOT, 'Assets', 'Fonts')
W, H = 1600, 520
rng = random.Random(1337)


def lerp(a, b, t):
    return tuple(int(a[i] + (b[i] - a[i]) * t) for i in range(len(a)))


# ── Ciel : dégradé nuit profonde → horizon violacé ──
img = Image.new('RGB', (W, H))
px = img.load()
top, mid, horizon = (6, 8, 22), (20, 18, 48), (58, 34, 70)
for y in range(H):
    t = y / (H * 0.62)
    c = lerp(top, mid, min(1, t)) if t < 1 else lerp(mid, horizon, min(1, (y - H * 0.62) / (H * 0.1)))
    for x in range(W):
        px[x, y] = c

draw = ImageDraw.Draw(img, 'RGBA')

# étoiles
for _ in range(260):
    x, y = rng.randrange(W), rng.randrange(int(H * 0.62))
    a = rng.randint(60, 220)
    r = 1 if rng.random() < 0.85 else 2
    draw.ellipse([x - r / 2, y - r / 2, x + r / 2, y + r / 2], fill=(220, 225, 255, a))

# ── Volcan (silhouette) avec lueur du cratère ──
glow = Image.new('RGBA', (W, H), (0, 0, 0, 0))
gd = ImageDraw.Draw(glow)
cx, cy = int(W * 0.875), int(H * 0.42)
gd.ellipse([cx - 120, cy - 70, cx + 120, cy + 50], fill=(255, 110, 40, 120))
glow = glow.filter(ImageFilter.GaussianBlur(40))
img.paste(glow, (0, 0), glow)
draw = ImageDraw.Draw(img, 'RGBA')

sea_y = int(H * 0.72)
mountain = [(int(W * 0.62), sea_y), (int(W * 0.72), int(H * 0.62)), (int(W * 0.80), int(H * 0.50)),
            (cx - 40, cy + 6), (cx - 18, cy + 14), (cx + 18, cy + 14), (cx + 40, cy + 4),
            (int(W * 0.93), int(H * 0.50)), (int(W * 0.97), int(H * 0.56)), (W, int(H * 0.60)), (W, sea_y)]
draw.polygon(mountain, fill=(14, 12, 24))
# coulée de lave
draw.line([(cx - 4, cy + 14), (cx - 10, cy + 40), (cx - 26, cy + 70), (cx - 30, cy + 98)], fill=(255, 120, 40, 200), width=3)
# fumée
for i in range(16):
    r = 18 + i * 5
    sx = cx + math.sin(i * 0.7) * 14 + i * 4
    sy = cy - 10 - i * 14
    draw.ellipse([sx - r, sy - r * 0.7, sx + r, sy + r * 0.7], fill=(90, 80, 100, max(0, 46 - i * 3)))

# ── Mer : reflets ──
draw.rectangle([0, sea_y, W, H], fill=(10, 16, 40))
for i in range(140):
    y = rng.randrange(sea_y + 4, H)
    x = rng.randrange(W)
    l = rng.randint(10, 60)
    a = int(30 + 60 * (y - sea_y) / (H - sea_y))
    draw.line([(x, y), (x + l, y)], fill=(70, 110, 200, a), width=1)

# ── Orbe : halo, anneaux de mana, gemme ──
ox, oy = int(W * 0.17), int(H * 0.46)
halo = Image.new('RGBA', (W, H), (0, 0, 0, 0))
hd = ImageDraw.Draw(halo)
for r, a in ((190, 40), (120, 70), (70, 120)):
    hd.ellipse([ox - r, oy - r, ox + r, oy + r], fill=(60, 190, 255, a))
halo = halo.filter(ImageFilter.GaussianBlur(38))
img.paste(halo, (0, 0), halo)
draw = ImageDraw.Draw(img, 'RGBA')

# reflet de l'orbe dans la mer
refl = Image.new('RGBA', (W, H), (0, 0, 0, 0))
rd = ImageDraw.Draw(refl)
rd.ellipse([ox - 60, sea_y + 30, ox + 60, H - 20], fill=(60, 190, 255, 60))
refl = refl.filter(ImageFilter.GaussianBlur(18))
img.paste(refl, (0, 0), refl)
draw = ImageDraw.Draw(img, 'RGBA')


def ring(rx, ry, n, back):
    for _ in range(n):
        a = rng.uniform(0, math.tau)
        if (math.sin(a) < 0) != back:
            continue
        rr = rng.uniform(0.85, 1.15)
        x = ox + math.cos(a) * rx * rr
        y = oy + math.sin(a) * ry * rr
        s = rng.uniform(1.0, 2.6)
        draw.ellipse([x - s, y - s, x + s, y + s], fill=(225, 240, 255, rng.randint(120, 255)))


ring(150, 34, 900, True)       # moitié arrière des anneaux (derrière la gemme)
ring(105, 24, 500, True)

R = 46
gem = Image.new('RGBA', (R * 2 + 4, R * 2 + 4), (0, 0, 0, 0))
gp = gem.load()
for y in range(R * 2 + 4):
    for x in range(R * 2 + 4):
        dx, dy = (x - R - 2) / R, (y - R - 2) / R
        d = dx * dx + dy * dy
        if d <= 1:
            shade = 1 - 0.55 * ((dx + 0.35) ** 2 + (dy + 0.4) ** 2)
            c = lerp((20, 80, 160), (120, 235, 255), max(0, min(1, shade)))
            gp[x, y] = c + (255,)
gd2 = ImageDraw.Draw(gem)
# éclairs internes (comme le cristal du jeu)
pts = [(R - 4, 10), (R + 8, R - 14), (R - 6, R + 2), (R + 10, R * 2 - 12)]
gd2.line(pts, fill=(235, 255, 255), width=4)
gd2.ellipse([R - 26, R - 30, R - 10, R - 18], fill=(255, 255, 255, 200))
img.paste(gem, (ox - R - 2, oy - R - 2), gem)
draw = ImageDraw.Draw(img, 'RGBA')

ring(150, 34, 900, False)      # moitié avant
ring(105, 24, 500, False)

# poussières libres qui affluent
for _ in range(120):
    a = rng.uniform(0, math.tau)
    d = rng.uniform(180, 420)
    x, y = ox + math.cos(a) * d, oy + math.sin(a) * d * 0.6
    if y > sea_y:
        continue
    s = rng.uniform(0.8, 2)
    draw.ellipse([x - s, y - s, x + s, y + s], fill=(200, 230, 255, rng.randint(40, 140)))

# mouette (silhouette) au loin
gx, gy = int(W * 0.40), int(H * 0.11)
draw.line([(gx - 22, gy - 6), (gx - 8, gy + 2), (gx, gy - 2), (gx + 8, gy + 2), (gx + 22, gy - 6)], fill=(230, 230, 240, 200), width=3)

# ── Titre ──
title_font = ImageFont.truetype(os.path.join(FONTS, 'CinzelDecorative-Bold.ttf'), 88)
sub_font = ImageFont.truetype(os.path.join(FONTS, 'Cinzel-Variable.ttf'), 26)
title = 'Dungeon Core'
tx, ty = int(W * 0.30), int(H * 0.24)

shadow = Image.new('RGBA', (W, H), (0, 0, 0, 0))
sd = ImageDraw.Draw(shadow)
sd.text((tx, ty), title, font=title_font, fill=(80, 200, 255, 200))
shadow = shadow.filter(ImageFilter.GaussianBlur(14))
img.paste(shadow, (0, 0), shadow)
draw = ImageDraw.Draw(img, 'RGBA')
draw.text((tx + 3, ty + 4), title, font=title_font, fill=(5, 5, 15, 200))
draw.text((tx, ty), title, font=title_font, fill=(255, 226, 150))

sub = 'Réincarné en cœur de donjon sur une île volcanique'
draw.text((tx + 6, ty + 118), sub, font=sub_font, fill=(205, 215, 245))
# ornement
oy2 = ty + 166
draw.line([(tx + 8, oy2), (tx + 520, oy2)], fill=(255, 210, 130, 160), width=2)
draw.polygon([(tx + 264, oy2 - 7), (tx + 271, oy2), (tx + 264, oy2 + 7), (tx + 257, oy2)], fill=(255, 210, 130))

# vignette
vig = Image.new('L', (W, H), 0)
vd = ImageDraw.Draw(vig)
vd.rectangle([0, 0, W, H], fill=255)
vd.ellipse([-W * 0.15, -H * 0.4, W * 1.15, H * 1.4], fill=0)
vig = vig.filter(ImageFilter.GaussianBlur(80))
dark = Image.new('RGB', (W, H), (0, 0, 0))
img = Image.composite(dark, img, vig.point(lambda v: int(v * 0.55)))

os.makedirs(os.path.dirname(OUT), exist_ok=True)
img.save(OUT, optimize=True)
print('ok', OUT)
