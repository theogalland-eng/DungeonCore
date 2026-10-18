"""
Pièges dessinés pour le projet (pixel art, 16 px par case, contour sombre teinté, lumière en haut à gauche).
Chaque piège tient dans UNE case (les pièges muraux et les hauts dépassent seulement vers le mur ou le haut) ;
les images d'une animation sont en bande horizontale : <nom>_<L>x<H>.png (L × H par image).
Pivot à l'import (TrapArt) : (8/L, 8/H) — la case du piège est le coin bas-gauche de 16 × 16.

Sortie : Assets/Art/Generated/Traps/  (+ Tools~/traps_preview.png, planche agrandie pour vérifier)
Usage  : python "Tools~/make_traps.py"
"""
import math
import os

import numpy as np
from PIL import Image

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
OUT = os.path.join(ROOT, 'Assets', 'Art', 'Generated', 'Traps')


def H(h, a=255):
    h = h.lstrip('#')
    return (int(h[0:2], 16), int(h[2:4], 16), int(h[4:6], 16), a)


CLEAR = (0, 0, 0, 0)
OL = H('120F1A')                                        # contour
IRON = [H('2C2832'), H('4A4550'), H('6B6470'), H('8D8590'), H('C9C4CC')]
WOOD = [H('2E2016'), H('4A3424'), H('6E4E30'), H('9A7046'), H('C49A64')]
STONE = [H('231F2B'), H('35303F'), H('4A4456'), H('625B70'), H('857D92')]
FIRE = [H('5A1408'), H('A8361C'), H('FF7A2A'), H('FFC75A'), H('FFF2B0')]
RUNE = [H('2A1446'), H('5A2FA0'), H('8E5CF0'), H('C9A8FF'), H('F0E4FF')]
RED = [H('4A0E12'), H('8F2F35'), H('D9534A'), H('FF8A6A'), H('FFD2B8')]
ICE = [H('1E3460'), H('3E6AB0'), H('6EA6E8'), H('B4DCFF'), H('F0FAFF')]
SPORE = [H('22361A'), H('3F6A2C'), H('6E9E3C'), H('A8D460'), H('E2F7A0')]
WATER = [H('0C2238'), H('16405E'), H('2A6A92'), H('5AA8D0'), H('B4E6F8')]
CRYS = [H('2A1650'), H('5A34A8'), H('8E62F0'), H('C6A8FF'), H('F4EEFF')]
BONE = [H('5E5040'), H('8F7A55'), H('C8B48A'), H('E6D8B8'), H('FFF8EA')]
OIL = [H('0A0A12'), H('1C1A2A'), H('2E2A40'), H('5A5478'), H('B8B0E0')]


class C:
    """Petite toile RGBA (origine en haut à gauche, comme une image)."""

    def __init__(self, w, h):
        self.w, self.h = w, h
        self.a = np.zeros((h, w, 4), np.uint8)

    def set(self, x, y, c):
        x, y = int(round(x)), int(round(y))
        if 0 <= x < self.w and 0 <= y < self.h and c is not None:
            if len(c) == 4 and c[3] < 255 and c[3] > 0:
                base = self.a[y, x].astype(float)
                a = c[3] / 255.0
                rgb = np.array(c[:3]) * a + base[:3] * (1 - a)
                self.a[y, x] = [*rgb.astype(np.uint8), max(int(base[3]), c[3])]
            else:
                self.a[y, x] = c

    def get(self, x, y):
        if 0 <= x < self.w and 0 <= y < self.h:
            return tuple(self.a[y, x])
        return CLEAR

    def rect(self, x0, y0, x1, y1, c):
        for y in range(y0, y1 + 1):
            for x in range(x0, x1 + 1):
                self.set(x, y, c)

    def line(self, x0, y0, x1, y1, c):
        n = int(max(abs(x1 - x0), abs(y1 - y0))) + 1
        for i in range(n + 1):
            t = i / max(1, n)
            self.set(x0 + (x1 - x0) * t, y0 + (y1 - y0) * t, c)

    def disc(self, cx, cy, r, c, ry=None):
        ry = r if ry is None else ry
        for y in range(int(cy - ry - 1), int(cy + ry + 2)):
            for x in range(int(cx - r - 1), int(cx + r + 2)):
                if ((x - cx) / max(r, 0.01)) ** 2 + ((y - cy) / max(ry, 0.01)) ** 2 <= 1.0:
                    self.set(x, y, c)

    def shaded_disc(self, cx, cy, r, ramp, ry=None, light=(-0.6, -0.8)):
        ry = r if ry is None else ry
        for y in range(int(cy - ry - 1), int(cy + ry + 2)):
            for x in range(int(cx - r - 1), int(cx + r + 2)):
                dx, dy = (x - cx) / max(r, 0.01), (y - cy) / max(ry, 0.01)
                d = dx * dx + dy * dy
                if d <= 1.0:
                    nz = math.sqrt(max(0.0, 1 - d))
                    lum = (-dx * light[0] - dy * light[1]) * 0.55 + nz * 0.6
                    k = int(np.clip(lum * (len(ramp) - 1) + 0.6, 0, len(ramp) - 1))
                    self.set(x, y, ramp[k])

    def outline(self, c=OL):
        m = self.a[:, :, 3] > 0
        out = np.zeros_like(m)
        out[1:, :] |= m[:-1, :]
        out[:-1, :] |= m[1:, :]
        out[:, 1:] |= m[:, :-1]
        out[:, :-1] |= m[:, 1:]
        out &= ~m
        self.a[out] = c
        return self

    def blit(self, o, ox, oy):
        for y in range(o.h):
            for x in range(o.w):
                if o.a[y, x, 3] > 0:
                    self.set(ox + x, oy + y, tuple(o.a[y, x]))

    def img(self):
        return Image.fromarray(self.a, 'RGBA')


def strip(name, frames):
    w, h = frames[0].w, frames[0].h
    im = Image.new('RGBA', (w * len(frames), h), CLEAR)
    for i, f in enumerate(frames):
        im.paste(f.img(), (i * w, 0))
    os.makedirs(OUT, exist_ok=True)
    im.save(os.path.join(OUT, f'{name}_{w}x{h}.png'))
    SHEETS.append((name, frames))


SHEETS = []


# ─────────────────────────────────────────────
# Socles communs
# ─────────────────────────────────────────────

def floor_plate(c, x0=1, y0=1, x1=14, y1=14, ramp=STONE, bolts=True):
    """Dalle de pierre taillée, biseau clair en haut à gauche, sombre en bas à droite."""
    c.rect(x0, y0, x1, y1, ramp[2])
    c.line(x0, y0, x1, y0, ramp[3]); c.line(x0, y0, x0, y1, ramp[3])
    c.line(x0, y1, x1, y1, ramp[1]); c.line(x1, y0, x1, y1, ramp[1])
    if bolts:
        for bx, by in ((x0 + 1, y0 + 1), (x1 - 1, y0 + 1), (x0 + 1, y1 - 1), (x1 - 1, y1 - 1)):
            c.set(bx, by, IRON[3])


def groove(c, y, x0=1, x1=14):
    """Fente horizontale dans le sol (rail de lame)."""
    c.line(x0, y - 1, x1, y - 1, STONE[1])
    c.line(x0, y, x1, y, OL)
    c.line(x0, y + 1, x1, y + 1, H('08060C'))
    c.line(x0, y + 2, x1, y + 2, STONE[3])


# ─────────────────────────────────────────────
# 1. LAME PENDULAIRE (16 × 24) : chaîne vers le plafond, lame en croissant qui balaie la case
# ─────────────────────────────────────────────

def pendulum(angle):
    c = C(16, 24)
    # la lame balaie la case de gauche à droite, suspendue à une chaîne qui monte hors de l'image
    bx = 8 + math.sin(angle) * 4.4
    by = 15 - (1 - math.cos(angle)) * 6
    c.disc(bx, 21, 3.4, H('000000', 70), 1.2)
    for i in range(0, 40):
        t = i / 40
        x, y = 8 + (bx - 8) * t, -2 + (by - 1.5 + 2) * t
        if y >= 0 and i % 3 != 2:
            c.set(x, y, IRON[2] if i % 2 else IRON[3])
    # lame en croissant, perpendiculaire à la chaîne
    dx, dy = bx - 8, by + 2
    n = math.hypot(dx, dy)
    nx, ny = dy / n, -dx / n
    blade = C(16, 24)
    for k in range(-6, 7):
        cx = bx + nx * k * 0.8
        cy = by + ny * k * 0.8 + abs(k) * 0.25
        blade.disc(cx, cy, max(0.6, 2.1 - abs(k) * 0.22), IRON[2])
    for k in range(-5, 6):
        blade.set(bx + nx * k * 0.8, by + ny * k * 0.8 + abs(k) * 0.25 + 1.1, IRON[4])
    blade.outline()
    c.blit(blade, 0, 0)
    c.disc(bx, by - 1.6, 1.1, IRON[1])
    return c


strip('pendulum', [pendulum(a) for a in [math.radians(x) for x in (-60, -42, -15, 15, 42, 60, 42, 15, -15, -42)]])


# ─────────────────────────────────────────────
# 2. SCIE AU SOL (16 × 16) : disque denté qui court dans une fente
# ─────────────────────────────────────────────

def saw(t):
    c = C(16, 16)
    floor_plate(c, 0, 3, 15, 12, bolts=False)
    groove(c, 7, 1, 14)
    x = 8 + math.sin(t * 2 * math.pi) * 4.5
    d = C(16, 16)
    d.shaded_disc(x, 7, 4.2, IRON[1:])
    rot = t * 8
    for i in range(10):
        a = rot + i * math.pi / 5
        d.set(x + math.cos(a) * 5, 7 + math.sin(a) * 5, IRON[3])
    d.disc(x, 7, 1.2, IRON[0])
    d.outline()
    # traînée d'étincelles
    for k in range(3):
        d.set(x - 5 - k * 1.5 if math.cos(t * 2 * math.pi) > 0 else x + 5 + k * 1.5, 9 - k, FIRE[3 - k % 2])
    c.blit(d, 0, 0)
    return c


strip('saw', [saw(i / 8) for i in range(8)])


# ─────────────────────────────────────────────
# 3. CAGE TOMBANTE (16 × 24) : marque au sol, la cage tombe, enferme, remonte
# ─────────────────────────────────────────────

def cage(drop, show=True):
    """drop : 0 = au plafond (invisible), 1 = posée."""
    c = C(16, 24)
    # repères au sol : équerres de fer aux quatre coins et rainure carrée (là où la cage se pose)
    for (x, y, sx, sy) in ((1, 22, 1, -1), (14, 22, -1, -1), (1, 9, 1, 1), (14, 9, -1, 1)):
        c.set(x, y, IRON[3]); c.set(x + sx, y, IRON[2]); c.set(x + 2 * sx, y, IRON[1])
        c.set(x, y + sy, IRON[2]); c.set(x, y + 2 * sy, IRON[1])
    for x in range(4, 12):
        c.set(x, 9, (*STONE[0][:3], 200)); c.set(x, 22, (*STONE[0][:3], 200))
    for y in range(12, 20):
        c.set(1, y, (*STONE[0][:3], 200)); c.set(14, y, (*STONE[0][:3], 200))
    c.disc(7.5, 18.5, 5.5 * max(0.25, drop), H('000000', int(40 + 60 * drop)), 2.5 * max(0.3, drop))
    if show and drop > 0.05:
        top = int(round(-14 + drop * 18))   # sommet de la cage
        g = C(16, 24)
        for x in (2, 5, 8, 11, 13):
            g.line(x, top + 2, x, top + 14, IRON[2])
            g.set(x, top + 3, IRON[4])
        g.rect(1, top + 1, 14, top + 2, IRON[3])
        g.rect(1, top + 14, 14, top + 15, IRON[1])
        g.line(7, top - 4, 7, top, IRON[2])      # anneau / chaîne
        g.set(7, top - 5, IRON[3])
        g.outline()
        c.blit(g, 0, 0)
    return c


strip('cage', [cage(0, False), cage(0.35), cage(0.75), cage(1.0), cage(1.0), cage(0.6)])


# ─────────────────────────────────────────────
# 4. RUNE EXPLOSIVE (16 × 16) : glyphe violet, vire au rouge, éclate, reste fissuré
# ─────────────────────────────────────────────

def rune(state):
    c = C(16, 16)
    ramp = RUNE if state in ('idle0', 'idle1') else RED
    glow = {'idle0': 1, 'idle1': 2, 'arm0': 2, 'arm1': 3, 'burst': 4, 'spent': 0}[state]
    if state == 'spent':
        ramp = STONE
    if glow >= 3:
        c.disc(7.5, 7.5, 7, (*ramp[2][:3], 90))
    # cercle et glyphe
    for i in range(36):
        a = i / 36 * 2 * math.pi
        c.set(7.5 + math.cos(a) * 6, 7.5 + math.sin(a) * 6, ramp[min(4, glow + 1)] if i % 3 else ramp[max(0, glow)])
    for i in range(20):
        a = i / 20 * 2 * math.pi
        c.set(7.5 + math.cos(a) * 3.6, 7.5 + math.sin(a) * 3.6, ramp[max(0, glow)])
    # triangle intérieur
    pts = [(7.5 + math.cos(a) * 3.4, 7.5 + math.sin(a) * 3.4) for a in (-math.pi / 2, math.pi / 6, 5 * math.pi / 6)]
    for i in range(3):
        c.line(*pts[i], *pts[(i + 1) % 3], ramp[min(4, glow + 1)])
    c.set(7, 7, ramp[min(4, glow + 2)]); c.set(8, 8, ramp[min(4, glow + 2)])
    if state == 'burst':
        c.disc(7.5, 7.5, 3, FIRE[4])
    if state == 'spent':
        c.line(3, 4, 7, 8, OL); c.line(7, 8, 12, 9, OL); c.line(7, 8, 6, 13, OL)
    return c


strip('rune', [rune(s) for s in ('idle0', 'idle1', 'arm0', 'arm1', 'burst', 'spent')])


# ─────────────────────────────────────────────
# 5. LANCES MURALES (32 × 16) : plaque au mur (à gauche), lances qui traversent le couloir vers la droite
# ─────────────────────────────────────────────

def wall_spears(ext):
    c = C(32, 16)
    # plaque de fer scellée au mur
    c.rect(0, 2, 3, 13, IRON[1]); c.line(0, 2, 0, 13, IRON[3]); c.line(3, 2, 3, 13, IRON[0])
    for y in (4, 8, 12):
        c.set(2, y - 1, OL)
    length = int(ext * 26)
    if length > 0:
        s = C(32, 16)
        for y in (3, 7, 11):
            s.line(3, y, 3 + length, y, WOOD[3])
            s.line(3, y + 1, 3 + length, y + 1, WOOD[1])
            tip = 3 + length
            s.set(tip + 1, y, IRON[4]); s.set(tip + 1, y + 1, IRON[3]); s.set(tip + 2, y, IRON[3]); s.set(tip + 2, y + 1, IRON[2]); s.set(tip + 3, y, IRON[4])
        s.outline()
        c.blit(s, 0, 0)
    return c


strip('wallspears', [wall_spears(e) for e in (0, 0.35, 0.8, 1.0, 1.0, 0.5)])


# ─────────────────────────────────────────────
# 6. LANCEUR DE FLÉCHETTES (16 × 16) et ARBALÈTE CACHÉE (16 × 16) : au mur, à gauche, tirent vers la droite
# ─────────────────────────────────────────────

def darts(fire):
    c = C(16, 16)
    c.rect(0, 3, 4, 12, STONE[2]); c.line(0, 3, 4, 3, STONE[3]); c.line(4, 3, 4, 12, STONE[1])
    for y in (5, 8, 11):
        c.set(4, y, OL); c.set(3, y, OL)
        if fire:
            c.set(5, y, FIRE[3]); c.set(6, y, FIRE[2])
    c.set(1, 4, IRON[3])
    return c


strip('darts', [darts(False), darts(True)])


def crossbow(state):
    c = C(16, 16)
    c.rect(0, 5, 2, 10, WOOD[1])
    s = C(16, 16)
    s.rect(2, 7, 11, 8, WOOD[3]); s.line(2, 9, 11, 9, WOOD[1])           # fût
    bend = {0: 0, 1: 2, 2: 1}[state]
    s.line(9 - bend, 2, 11, 7, IRON[3]); s.line(9 - bend, 13, 11, 9, IRON[3])   # arc
    s.line(9 - bend, 2, 5 + (2 if state else 0), 8, IRON[4] if state == 0 else IRON[2])
    s.line(9 - bend, 13, 5 + (2 if state else 0), 8, IRON[2])
    if state == 0:
        s.line(5, 6, 13, 6, WOOD[4]); s.set(14, 6, IRON[4])                    # carreau armé
    s.outline()
    c.blit(s, 0, 0)
    return c


strip('crossbow', [crossbow(i) for i in range(3)])


# ─────────────────────────────────────────────
# 7. TRAPPE (16 × 16) : planches et charnières ; s'ouvre sur le vide
# ─────────────────────────────────────────────

def trapdoor(open_):
    c = C(16, 16)
    c.rect(1, 1, 14, 14, H('050308'))
    c.rect(1, 1, 14, 2, STONE[1])
    if open_ < 1:
        h = int(round(13 * (1 - open_)))
        for x in range(1, 15):
            for y in range(2, 2 + h):
                band = (x - 1) // 4
                k = 3 if band % 2 == 0 else 2
                c.set(x, y, WOOD[k] if (x - 1) % 4 else WOOD[1])
        if h > 3:
            c.line(1, 2, 14, 2, WOOD[4])
            for hx in (3, 11):
                c.rect(hx, 3, hx + 1, 4, IRON[3]); c.set(hx, 3, IRON[4])
            c.set(7, 1 + h - 1, IRON[2]); c.set(8, 1 + h - 1, IRON[2])
    c.line(0, 0, 15, 0, STONE[3]); c.line(0, 0, 0, 15, STONE[3]); c.line(15, 0, 15, 15, STONE[1]); c.line(0, 15, 15, 15, STONE[1])
    return c


strip('trapdoor', [trapdoor(o) for o in (0, 0.45, 0.85, 1.0, 1.0, 0.4)])


# ─────────────────────────────────────────────
# 8. DALLE HUILÉE (16 × 16) : flaque noire irisée qui miroite
# ─────────────────────────────────────────────

def oil(t):
    c = C(16, 16)
    for y in range(16):
        for x in range(16):
            d = ((x - 7.5) / 6.8) ** 2 + ((y - 8) / 5.6) ** 2 + 0.1 * math.sin(x * 1.3) * math.cos(y * 1.1)
            if d < 1:
                c.set(x, y, OIL[3] if d > 0.82 else OIL[1] if d > 0.55 else OIL[2])
    # reflets irisés qui glissent
    for k in range(6):
        a = t * 2 * math.pi + k * 1.05
        x, y = 7.5 + math.cos(a) * (1.5 + k * 0.7), 8 + math.sin(a * 0.7) * 2.2
        c.set(x, y, [H('7A6AD0'), H('4AA0B4'), H('C09048'), OIL[4], H('9A5AB0'), OIL[3]][k])
    c.line(4, 5, 6, 4, OIL[4])
    return c


strip('oil', [oil(i / 4) for i in range(4)])


# ─────────────────────────────────────────────
# 9. ROCHER (16 × 16) : bloc rond qui roule (fissures qui tournent)
# ─────────────────────────────────────────────

def boulder(t):
    c = C(16, 16)
    c.disc(8, 13, 5.5, H('000000', 70), 1.6)
    b = C(16, 16)
    b.shaded_disc(8, 7.5, 6, STONE)
    rot = t * 2 * math.pi
    for k in range(3):
        a = rot + k * 2.1
        x0, y0 = 8 + math.cos(a) * 1.5, 7.5 + math.sin(a) * 1.5
        b.line(x0, y0, 8 + math.cos(a + 0.4) * 4.5, 7.5 + math.sin(a + 0.4) * 4.5, STONE[0])
    b.set(5, 4, STONE[4]); b.set(6, 4, STONE[4])
    b.outline()
    c.blit(b, 0, 0)
    return c


strip('boulder', [boulder(i / 6) for i in range(6)])


# ─────────────────────────────────────────────
# 10. PIÈGES DE THÈME
# ─────────────────────────────────────────────

def geyser(phase):
    """16 × 32 : évent de magma ; bouillonne, puis jet de lave."""
    c = C(16, 32)
    c.shaded_disc(8, 26, 6, STONE, 4)
    c.disc(8, 26, 3.5, FIRE[1], 2.2)
    c.disc(8, 26, 2.2, FIRE[2] if phase else FIRE[1], 1.3)
    if phase >= 1:
        c.set(6, 25, FIRE[4]); c.set(9, 26, FIRE[3])
    if phase >= 2:
        hgt = {2: 10, 3: 22, 4: 16}.get(phase, 0)
        j = C(16, 32)
        for y in range(26 - hgt, 26):
            w = 1.6 + (26 - y) * 0.06
            for x in range(int(8 - w), int(8 + w) + 1):
                j.set(x, y, FIRE[2] if abs(x - 8) > w * 0.5 else FIRE[4])
        for k in range(6):
            j.set(8 + math.sin(k * 2.3 + phase) * 4, 26 - hgt + k * 2, FIRE[3])
        c.blit(j, 0, 0)
    return c


strip('geyser', [geyser(p) for p in (0, 1, 2, 3, 4, 0)])


def whirlpool(t):
    c = C(16, 16)
    c.disc(7.5, 7.5, 7.4, WATER[1])
    for k in range(3):
        for i in range(40):
            s = i / 40
            a = t * 2 * math.pi + k * 2.09 + s * 5
            r = 7 * (1 - s)
            c.set(7.5 + math.cos(a) * r, 7.5 + math.sin(a) * r, WATER[3] if s < 0.6 else WATER[4])
    c.disc(7.5, 7.5, 1.4, WATER[0])
    return c


strip('whirlpool', [whirlpool(i / 6) for i in range(6)])


def stalactite(phase):
    """16 × 32 : glaçon au plafond (en haut), son ombre grandit au sol, il tombe et éclate."""
    c = C(16, 32)
    shadow = {0: 2, 1: 3.5, 2: 5, 3: 5, 4: 3}[phase]
    c.disc(8, 25, shadow, H('1E3460', 110), shadow * 0.45)
    y = {0: 2, 1: 2, 2: 12, 3: None, 4: None}[phase]
    if y is not None:
        s = C(16, 32)
        for i in range(10):
            w = max(0, 3 - i * 0.32)
            s.line(8 - w, y + i, 8 + w, y + i, ICE[2] if i < 6 else ICE[3])
        s.line(7, y + 1, 7, y + 6, ICE[4])
        s.outline(ICE[0])
        c.blit(s, 0, 0)
    if phase == 3:
        for (dx, dy) in ((-4, 0), (-2, -2), (1, -1), (3, 1), (5, -1), (0, 2)):
            c.set(8 + dx, 25 + dy, ICE[4]); c.set(8 + dx + 1, 25 + dy, ICE[2])
    if phase == 4:
        for (dx, dy) in ((-3, 1), (2, 0), (4, 1)):
            c.set(8 + dx, 25 + dy, ICE[3])
    return c


strip('stalactite', [stalactite(p) for p in range(5)])


CAP = [H('3A1428'), H('7A2A44'), H('B4485A'), H('E07A7A'), H('FFD0C0')]


def spores(phase):
    c = C(16, 16)
    squash = {0: 0, 1: 0.4, 2: 1.2, 3: 0.8, 4: 0}[phase]
    m = C(16, 16)
    for (cx, cy, r) in ((5, 9, 3.0), (10.5, 7.5, 3.6), (8, 12, 2.4)):
        m.rect(int(cx) - 1, int(cy), int(cx), int(cy + 3), BONE[3])
        m.set(int(cx) - 1, int(cy) + 1, BONE[4])
        m.shaded_disc(cx, cy - 0.5 + squash * 0.5, r, CAP[1:], r * (0.68 - squash * 0.08))
        # taches pâles du chapeau
        m.set(cx - 1, cy - 1.5, CAP[4]); m.set(cx + 1, cy - 0.5, CAP[4])
    m.outline(CAP[0])
    c.blit(m, 0, 0)
    if phase >= 1:
        n = 4 + phase * 3
        for k in range(n):
            a = k * 2.4 + phase
            r = 1.5 + phase * 1.5
            c.set(8 + math.cos(a) * r, 8 + math.sin(a) * r * 0.8, (*SPORE[4][:3], 220 - phase * 25))
    return c


strip('spores', [spores(p) for p in range(5)])


def prism(charge):
    """16 × 24 : cristal sur un socle ; il se charge avant de tirer."""
    c = C(16, 24)
    c.rect(4, 18, 11, 21, STONE[2]); c.line(4, 18, 11, 18, STONE[3]); c.line(4, 21, 11, 21, STONE[1])
    s = C(16, 24)
    pts = [(8, 2), (12, 9), (10, 17), (6, 17), (4, 9)]
    for y in range(2, 18):
        for x in range(3, 13):
            inside = True
            for i in range(5):
                (x0, y0), (x1, y1) = pts[i], pts[(i + 1) % 5]
                if (x1 - x0) * (y - y0) - (y1 - y0) * (x - x0) < 0:
                    inside = False
                    break
            if inside:
                k = 3 if x < 8 else 2
                s.set(x, y, CRYS[min(4, k + (1 if charge >= 2 and (x + y) % 3 == 0 else 0))])
    s.line(8, 3, 8, 16, CRYS[4] if charge else CRYS[3])
    s.outline(CRYS[0])
    c.blit(s, 0, 0)
    if charge >= 2:
        c.disc(8, 9, 2, (*CRYS[4][:3], 200))
    return c


strip('prism', [prism(p) for p in (0, 1, 2, 3)])


def grasp(phase):
    c = C(16, 16)
    # fissures dans le sol
    c.line(2, 9, 6, 8, STONE[0]); c.line(6, 8, 9, 10, STONE[0]); c.line(9, 10, 13, 8, STONE[0]); c.line(6, 8, 7, 4, STONE[0])
    rise = {0: 0, 1: 3, 2: 6, 3: 7, 4: 7, 5: 3}[phase]
    if rise:
        h = C(16, 16)
        for (bx, by, lean) in ((5, 10, -1), (10, 10, 1)):
            top = by - rise
            h.line(bx, by, bx + lean, top + 2, BONE[2])
            h.line(bx + 1, by, bx + 1 + lean, top + 2, BONE[1])
            closed = phase >= 3
            for f in range(3):
                fx = bx - 1 + f + lean
                h.line(fx, top + 2, fx + (lean if closed else 0), top - (0 if closed else 2), BONE[3])
            h.set(bx + lean, top + 2, BONE[4])
        h.outline()
        c.blit(h, 0, 0)
    return c


strip('grasp', [grasp(p) for p in range(6)])


# ─────────────────────────────────────────────
# 11. PIÈGES CONSERVÉS, REDESSINÉS
# ─────────────────────────────────────────────

def spikes(phase):
    c = C(16, 16)
    floor_plate(c)
    holes = [(4, 4), (8, 4), (12, 4), (6, 8), (10, 8), (4, 12), (8, 12), (12, 12)]
    up = {0: 0, 1: 0, 2: 2, 3: 4, 4: 2}[phase]
    for (x, y) in holes:
        c.set(x, y, OL)
        if up:
            for k in range(up):
                c.set(x, y - k, IRON[4] if k == up - 1 else IRON[2])
            c.set(x + 1, y, IRON[1])
    if phase == 1:   # avertissement : les trous s'éclairent
        for (x, y) in holes:
            c.set(x, y, RED[2])
    return c


strip('spikes', [spikes(p) for p in range(5)])


def bear(phase):
    c = C(16, 16)
    c.disc(8, 9, 6.5, H('000000', 60), 3)
    s = C(16, 16)
    s.disc(8, 8.5, 5.5, IRON[1], 3.4)
    s.disc(8, 8.5, 3.6, H('00000000') if False else STONE[1], 2)
    gap = {0: 3, 1: 1.5, 2: 0}[phase]
    for side in (-1, 1):
        for i in range(-5, 6, 2):
            x = 8 + i
            y0 = 8.5 + side * (gap + 0.5)
            s.set(x, y0, IRON[4])
            s.set(x, y0 + side, IRON[3])
    s.line(2, 8, 14, 8, IRON[2])
    s.disc(8, 8.5, 1.2, IRON[3])
    s.outline()
    c.blit(s, 0, 0)
    return c


strip('bear', [bear(p) for p in range(3)])


def gas(t):
    c = C(16, 16)
    floor_plate(c, 2, 2, 13, 13)
    for x in range(4, 12, 2):
        c.line(x, 4, x, 11, OL)
    for k in range(6):
        a = t * 2 * math.pi + k
        c.disc(8 + math.cos(a) * 3, 6 + math.sin(a * 1.3) * 2 - t * 3, 1.6, (*SPORE[3][:3], 110))
    return c


strip('gas', [gas(i / 4) for i in range(4)])


def ghost_plate(phase):
    c = C(16, 16)
    floor_plate(c, 1, 1, 14, 14, STONE)
    glow = {0: 2, 1: 3, 2: 4, 3: 2}[phase]
    # crâne gravé
    c.disc(8, 7, 3.3, ICE[glow - 1], 3)
    c.set(6, 7, OL); c.set(10, 7, OL); c.set(8, 9, OL)
    c.line(6, 10, 10, 10, ICE[glow - 1])
    for x in (6, 8, 10):
        c.set(x, 11, ICE[glow - 1])
    return c


strip('ghostplate', [ghost_plate(p) for p in range(4)])


def flame_vent(on):
    c = C(16, 16)
    floor_plate(c, 2, 2, 13, 13)
    c.disc(8, 8, 3, OL)
    c.disc(8, 8, 2, FIRE[2] if on else FIRE[0])
    if on:
        c.set(7, 7, FIRE[4])
    return c


strip('flamevent', [flame_vent(False), flame_vent(True)])


def web():
    c = C(16, 16)
    col = (*BONE[4][:3], 200)
    for a in range(8):
        ang = a * math.pi / 4 + 0.2
        c.line(8, 8, 8 + math.cos(ang) * 8, 8 + math.sin(ang) * 8, col)
    for r in (2.5, 4.5, 6.5):
        pts = [(8 + math.cos(a * math.pi / 4 + 0.2) * r, 8 + math.sin(a * math.pi / 4 + 0.2) * r) for a in range(9)]
        for i in range(8):
            c.line(*pts[i], *pts[i + 1], (*BONE[3][:3], 170))
    return c


strip('web', [web()])


# ─────────────────────────────────────────────
# Planche d'aperçu (×6)
# ─────────────────────────────────────────────
def preview():
    scale = 6
    rows = []
    for name, frames in SHEETS:
        w, h = frames[0].w, frames[0].h
        row = Image.new('RGBA', (max(w * len(frames), 16) * scale + 4, h * scale + 4), (40, 36, 52, 255))
        for i, f in enumerate(frames):
            im = f.img().resize((w * scale, h * scale), Image.NEAREST)
            bg = Image.new('RGBA', im.size, (58, 52, 70, 255))
            bg.alpha_composite(im)
            row.paste(bg, (i * w * scale + 2, 2))
        rows.append(row)
    W = max(r.width for r in rows)
    Ht = sum(r.height for r in rows) + 4 * len(rows)
    sheet = Image.new('RGBA', (W, Ht), (30, 27, 40, 255))
    y = 0
    for r in rows:
        sheet.paste(r, (0, y))
        y += r.height + 4
    sheet.save(os.path.join(ROOT, 'Tools~', 'traps_preview.png'))


preview()
print(len(SHEETS), 'pièges')
