"""
Portes et escaliers du donjon, tirés des packs craftpix :
  - door_wood_32x48 : porte en bois voûtée qui s'ouvre (4 images)       — pack 298079 doors.png
  - door_gate_32x48 : herse de fer qui se lève (4 images)                — pack 298079 doors.png
  - door_skull      : gueule d'ossements (porte de crypte)               — pack undead 695666
  - stairs_down     : escalier qui s'enfonce dans l'obscurité            — pack 298079 stairs.png
  - stairs_up       : escalier qui remonte vers un palier                — pack 298079 stairs.png
Sortie : Assets/Art/Dungeon (puis menu DungeonCore/2 ou ArtImporter.ImportAll).
Usage : python "Tools~/crop_doors.py"
"""
import glob, os
from PIL import Image

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
IMP = os.path.join(ROOT, 'Assets', 'imports')
OUT = os.path.join(ROOT, 'Assets', 'Art', 'Dungeon')


def sheet(pattern):
    return Image.open(glob.glob(os.path.join(IMP, pattern), recursive=True)[0]).convert('RGBA')


def strip(frames):
    w, h = frames[0].size
    s = Image.new('RGBA', (w * len(frames), h))
    for i, f in enumerate(frames):
        s.paste(f, (i * w, 0))
    return s


# ── Portes vues de profil (couloirs est-ouest), dessinées avec les couleurs des portes du pack ──
STONE = [(28, 25, 29), (41, 36, 41), (52, 44, 50), (63, 52, 57), (79, 68, 68), (93, 77, 78)]
WOOD = [(24, 22, 25), (57, 38, 36), (67, 48, 43), (82, 60, 54), (93, 70, 63)]
IRON = [(24, 24, 34), (35, 38, 57), (56, 66, 84), (69, 84, 99), (89, 125, 142)]
SW, SH, CX, HINGE_Y = 64, 48, 32, 17


def stone_block(px, x0, y0, x1, y1):
    for y in range(y0, y1):
        for x in range(x0, x1):
            edge = x in (x0, x1 - 1) or y in (y0, y1 - 1)
            c = STONE[0] if edge else (STONE[5] if y == y0 + 1 else STONE[3] if (x + y // 4) % 5 else STONE[2])
            if not edge and (y - y0) % 5 == 0: c = STONE[1]   # joints
            px[x, y] = c + (255,)


def frame_stones(px):
    stone_block(px, CX - 7, 3, CX + 7, HINGE_Y + 1)   # linteau (l'arche vue de profil)
    stone_block(px, CX - 7, SH - 5, CX + 7, SH)       # seuil


def side_wood(angle_deg):
    im = Image.new('RGBA', (SW, SH))
    px = im.load()
    frame_stones(px)
    import math
    a = math.radians(angle_deg)
    d = (math.sin(a), math.cos(a))          # 0° : vers le bas, 90° : contre le mur du haut, vers l'est
    n = (-d[1], d[0])
    L = SH - 5 - HINGE_Y
    for t in range(L):
        for w in range(-4, 4):
            x = int(round(CX + d[0] * t + n[0] * w))
            y = int(round(HINGE_Y + d[1] * t + n[1] * w))
            if not (0 <= x < SW and 0 <= y < SH): continue
            if w in (-4, 3) or t in (0, L - 1): c = WOOD[0]
            elif 8 <= t <= 9 or 19 <= t <= 20: c = IRON[2] if w < 0 else IRON[1]
            elif w == 0: c = WOOD[1]                       # joint entre deux planches
            else: c = WOOD[4] if w == -3 else WOOD[3] if w < 0 else WOOD[2]
            px[x, y] = c + (255,)
    # anneau de la poignée
    t = 14
    hx, hy = int(round(CX + d[0] * t + n[0] * 4)), int(round(HINGE_Y + d[1] * t + n[1] * 4))
    if 0 <= hx < SW and 0 <= hy < SH: px[hx, hy] = IRON[4] + (255,)
    frame_stones(px) if angle_deg > 60 else None
    return im


def side_gate(length):
    im = Image.new('RGBA', (SW, SH))
    px = im.load()
    x0, x1 = CX - 4, CX + 4                 # herse vue de profil : une grille épaisse, cerclée de noir
    for y in range(HINGE_Y, HINGE_Y + length):
        for x in range(x0, x1 + 1):
            if x in (x0, x1):
                c = IRON[0]
            elif (y - HINGE_Y) % 5 == 2:
                c = IRON[3] if x < CX else IRON[2]           # traverses
            elif (x - x0) % 2 == 1:
                c = IRON[4] if x < CX else IRON[3]           # barreaux
            else:
                c = IRON[1]
            px[x, y] = c + (255,)
    # pointes
    for x in range(x0 + 1, x1, 2):
        for k in range(2):
            y = HINGE_Y + length + k
            if y < SH - 5: px[x, y] = (IRON[4] if k == 0 else IRON[2]) + (255,)
    frame_stones(px)
    return im


def make_side_doors():
    strip([side_wood(a) for a in (0, 32, 62, 84)]).save(os.path.join(OUT, f'door_wood_side_{SW}x{SH}.png'))
    strip([side_gate(L) for L in (SH - 5 - HINGE_Y, 18, 9, 2)]).save(os.path.join(OUT, f'door_gate_side_{SW}x{SH}.png'))


def main():
    os.makedirs(OUT, exist_ok=True)
    doors = sheet('new/craftpix-net-298079*/PNG/doors.png')
    # planche 2 colonnes × 6 rangées de 64×48 ; chaque case montre deux embrasures de 32 px
    strip([doors.crop((32, 48 * r, 64, 48 * r + 48)) for r in range(4)]).save(os.path.join(OUT, 'door_wood_32x48.png'))
    strip([doors.crop((96, 48 * r, 128, 48 * r + 48)) for r in range(4)]).save(os.path.join(OUT, 'door_gate_32x48.png'))
    sheet('craftpix-net-695666*/PNG/Objects_separately/Scull_door_shadow1.png').save(os.path.join(OUT, 'door_skull.png'))
    stairs = sheet('new/craftpix-net-298079*/PNG/stairs.png')
    stairs.crop((0, 48, 47, 99)).save(os.path.join(OUT, 'stairs_down.png'))
    stairs.crop((0, 0, 47, 36)).save(os.path.join(OUT, 'stairs_up.png'))
    make_side_doors()
    print('ok')


if __name__ == '__main__':
    main()
