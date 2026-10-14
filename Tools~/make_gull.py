"""Mouette vue de dessus (bec vers le haut) pour la cinématique d'intro.
Sortie : Assets/Resources/FX/gull_32x32.png (6 images)
  0-3 : battement d'ailes (haut, mi-haut, planée, bas)
  4   : écrasée dans le sable (ailes en vrac)
  5   : posée (ailes repliées)
La rotation (cap) et l'ombre sont gérées en jeu."""
from PIL import Image, ImageDraw
import os

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
OUT = os.path.join(ROOT, 'Assets', 'Resources', 'FX', 'gull_32x32.png')

WHITE = (244, 244, 238, 255)
SHADE = (214, 216, 214, 255)
WING = (156, 166, 178, 255)
WING_D = (124, 133, 147, 255)
TIP = (34, 34, 40, 255)
BEAK = (236, 190, 62, 255)
SPOT = (205, 60, 50, 255)
EYE = (30, 28, 34, 255)
LINE = (58, 60, 70, 255)
S = 32


def outline(img):
    """Contour sombre d'un pixel autour de la silhouette."""
    px = img.load()
    out = img.copy()
    po = out.load()
    for y in range(S):
        for x in range(S):
            if px[x, y][3] > 0:
                continue
            for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1)):
                nx, ny = x + dx, y + dy
                if 0 <= nx < S and 0 <= ny < S and px[nx, ny][3] > 0 and px[nx, ny] != LINE:
                    po[x, y] = LINE
                    break
    return out


def wing(d, side, span, sweep, lift):
    """Une aile effilée : 'span' = envergure (px), 'sweep' = recul de la pointe, 'lift' = pointe vers l'avant (ailes levées)."""
    def X(f):
        return 16 + side * (2 + span * f)
    tipy = 16 + sweep - lift
    wrist = (X(0.5), 12 - lift * 0.6)
    poly = [(16 + side * 2, 13), wrist, (X(1.0), tipy), (X(0.78), tipy + 1.5), (X(0.4), 18), (16 + side * 2, 19)]
    d.polygon(poly, fill=WING)
    d.line([(X(0.78), tipy + 1.5), (X(0.4), 18), (16 + side * 2, 19)], fill=WING_D)
    # pointe noire (au-delà du poignet)
    d.polygon([(X(0.72), wrist[1] + (tipy - wrist[1]) * 0.45), (X(1.0), tipy), (X(0.8), tipy + 1.2)], fill=TIP)


def body(d, folded=False):
    # queue
    d.polygon([(14, 21), (18, 21), (19, 26), (16, 27), (13, 26)], fill=WHITE)
    if folded:
        d.polygon([(14, 24), (18, 24), (17, 27), (15, 27)], fill=TIP)
    # corps
    d.ellipse([13, 9, 19, 23], fill=WHITE)
    d.line([(14, 17), (14, 21)], fill=SHADE)
    if folded:
        d.ellipse([13, 13, 19, 25], fill=WING)
        d.line([(16, 15), (16, 24)], fill=WING_D)
    # tête
    d.ellipse([14, 6, 18, 11], fill=WHITE)
    d.point((14, 8), fill=EYE)
    d.point((18, 8), fill=EYE)
    # bec
    d.line([(16, 3), (16, 5)], fill=BEAK)
    d.point((16, 4), fill=SPOT)


def frame(span, sweep, lift):
    img = Image.new('RGBA', (S, S), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)
    wing(d, -1, span, sweep, lift)
    wing(d, 1, span, sweep, lift)
    body(d)
    return outline(img)


def crashed():
    img = Image.new('RGBA', (S, S), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)
    wing(d, -1, 13, 5, 0)        # une aile étalée
    wing(d, 1, 7, 9, -2)         # l'autre à moitié repliée
    body(d)
    img = img.rotate(28, resample=Image.NEAREST, center=(16, 16))
    return outline(img)


def standing():
    img = Image.new('RGBA', (S, S), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)
    body(d, folded=True)
    return outline(img)


frames = [frame(9, 0, 4), frame(12, 1, 2), frame(14, 2, 0), frame(11, 5, -2), crashed(), standing()]
sheet = Image.new('RGBA', (S * len(frames), S), (0, 0, 0, 0))
for i, f in enumerate(frames):
    sheet.paste(f, (i * S, 0))
os.makedirs(os.path.dirname(OUT), exist_ok=True)
sheet.save(OUT)
sheet.resize((sheet.width * 6, S * 6), Image.NEAREST).save(os.path.join(os.environ.get('TEMP', '.'), 'gull_preview.png'))
print('ok', OUT)
