"""
Assemble les images PixelLab (Assets/Art/PixelLab/Characters/<nom>/...) en planches régulières
lues par CharacterImporter : Assets/imports/PixelLab/Characters/<nom>_<État>.png
  - lignes : bas, haut, gauche, droite (ordre « DULR ») ; colonnes : images de l'animation
  - la gauche est le miroir de la droite quand PixelLab n'a pas généré l'ouest
  - États : Idle (vues fixes), Walk, Attack (Cast / Shoot ; à défaut, marche)
Usage : python "Tools~/build_pixellab_sheets.py"
"""
import glob, os
from PIL import Image, ImageOps

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
SRC = os.path.join(ROOT, 'Assets', 'Art', 'PixelLab', 'Characters')
OUT = os.path.join(ROOT, 'Assets', 'imports', 'PixelLab', 'Characters')
DIRS = ['south', 'north', 'west', 'east']   # D U L R
ATTACK = {'mage': 'Cast', 'healer': 'Cast', 'archer': 'Shoot'}


def frames(name, anim, d):
    fs = sorted(glob.glob(os.path.join(SRC, name, 'Idle', 'animations', anim, d, '*.png')))
    return [Image.open(f).convert('RGBA') for f in fs]


def rotation(name, d):
    p = os.path.join(SRC, name, 'Idle', 'rotations', d + '.png')
    return Image.open(p).convert('RGBA') if os.path.exists(p) else None


def diff(a, b):
    """Écart moyen entre deux images (même cadre, centrées)."""
    size = max(a.width, a.height, b.width, b.height)
    ca = Image.new('RGBA', (size, size)); ca.alpha_composite(a, ((size - a.width) // 2, (size - a.height) // 2))
    cb = Image.new('RGBA', (size, size)); cb.alpha_composite(b, ((size - b.width) // 2, (size - b.height) // 2))
    # la tête suffit à distinguer une vue de face d'une vue de dos (visage / capuche)
    head = (0, 0, size, int(size * 0.45))
    pa, pb = list(ca.crop(head).getdata()), list(cb.crop(head).getdata())
    return sum(abs(x[0] - y[0]) + abs(x[1] - y[1]) + abs(x[2] - y[2]) + abs(x[3] - y[3]) for x, y in zip(pa, pb)) / len(pa)


def skin(im):
    """Pixels couleur peau dans le haut de l'image : un visage visible = vue de face."""
    w, h = im.size
    n = 0
    for (r, g, b, a) in im.crop((0, 0, w, int(h * 0.5))).getdata():
        if a > 200 and r > 150 and 90 < g < r and b < g and r - b > 40:
            n += 1
    return n


def clean(name, d, imgs):
    """Écarte les images générées dans la mauvaise orientation (vue de dos dans une marche de face...)."""
    if d not in ('south', 'north') or len(imgs) < 4:
        return imgs
    face = rotation(name, 'south')
    if face is None:
        return imgs
    ref = max(1, skin(face))
    if d == 'south':
        keep = [im for im in imgs if skin(im) >= ref * 0.45]
    else:
        keep = [im for im in imgs if skin(im) < ref * 0.45]
    return keep if len(keep) >= 3 else imgs


def anim_rows(name, anim):
    rows = {d: clean(name, d, frames(name, anim, d)) for d in DIRS}
    if not rows['west'] and rows['east']:
        rows['west'] = [ImageOps.mirror(im) for im in rows['east']]
    if not rows['east'] and rows['west']:
        rows['east'] = [ImageOps.mirror(im) for im in rows['west']]
    # directions manquantes (ex. tir de l'archer, seulement de profil)
    for d in ('south', 'north'):
        if not rows[d]:
            rows[d] = rows['east'] or rows['west']
    return rows


def sheet(rows, path, size):
    n = max(len(r) for r in rows.values())
    out = Image.new('RGBA', (size * n, size * 4), (0, 0, 0, 0))
    for y, d in enumerate(DIRS):
        r = rows[d]
        for x in range(n):
            im = r[x % len(r)]
            # PixelLab centre le personnage dans chaque image : on centre aussi
            out.alpha_composite(im, (x * size + (size - im.width) // 2, y * size + (size - im.height) // 2))
    out.save(path)
    return size, n


def main():
    os.makedirs(OUT, exist_ok=True)
    for name in sorted(os.listdir(SRC)):
        if not os.path.isdir(os.path.join(SRC, name)):
            continue
        idle = {d: [rotation(name, d)] for d in DIRS}
        if idle['west'][0] is None:
            idle['west'] = [ImageOps.mirror(idle['east'][0])]
        walk = anim_rows(name, 'Walk')
        atk = anim_rows(name, ATTACK.get(name, 'Cast'))
        if not any(atk.values()):
            atk = walk
        # même taille de cadre pour tous les états (l'importeur découpe à taille fixe)
        s = max(max(im.size) for rows in (idle, walk, atk) for r in rows.values() for im in r)
        sheet(idle, os.path.join(OUT, f'{name}_Idle.png'), s)
        sheet(walk, os.path.join(OUT, f'{name}_Walk.png'), s)
        sheet(atk, os.path.join(OUT, f'{name}_Attack.png'), s)
        print(name, 'cadre', s, 'px')


if __name__ == '__main__':
    main()
