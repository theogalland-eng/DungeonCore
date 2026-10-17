"""
Objets dessinés pour le projet (pixel art, palette de la maquette) :
  - food_cache : garde-manger (appât des intrus affamés) — caisse ouverte, jambon, pains, pommes.
Sortie : Assets/Art/Generated/Props/<nom>.png (PPU 16, pivot en bas)
Usage  : python "Tools~/make_props.py"
"""
import os

from PIL import Image

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
OUT = os.path.join(ROOT, 'Assets', 'Art', 'Generated', 'Props')

PAL = {
    'O': '#120F1A', 'W': '#8A6A44', 'w': '#5A4228', 'L': '#B08A5A', 'd': '#3A2A1C',
    'H': '#C8644A', 'h': '#8F3A2E', 'P': '#F2D7C0', 'B': '#D9A45C', 'b': '#9C6A30', 'Y': '#F2D27A',
    'R': '#D9534A', 'r': '#8F2F35', 'G': '#79A844', 'g': '#3F6A2C', 'S': '#EFE9DC',
}

FOOD_CACHE = [
    '.........OOOO...........',
    '.......OOHHHHOO.........',
    '......OHHHHHHhhO..OOO...',
    '.....OHPHHHHHhhhOOBBBO..',
    '.....OHHHHHHHhhhOBYBBbO.',
    '..OO.OhHHHHHhhhOOBBBBbO.',
    '.ORROOOhhhhhhhOOOObbbbO.',
    'ORSRrOOOOSSOOOORROOOOO..',
    'ORRRrOGOOSSOOORSRrO.....',
    '.OrrOGgO.OOO..ORRRrO....',
    'OOOOOOOOOOOOOOOOOOOOOOO.',
    'OLLLLLLLLLLLLLLLLLLLLLO.',
    'OWWWWWWWWWWWWWWWWWWWWWO.',
    'OWwwwwwwwwwwwwwwwwwwwWO.',
    'OWWWWWWWWWWWWWWWWWWWWWO.',
    'OWwOWWWWWWWWWWWWWWWOwWO.',
    'OWWWWWWWWWWWWWWWWWWWWWO.',
    'OWwwwwwwwwwwwwwwwwwwwWO.',
    'OWWWWWWWWWWWWWWWWWWWWWO.',
    'OdddddddddddddddddddddO.',
    '.OOOOOOOOOOOOOOOOOOOOO..',
]


def draw(rows, path):
    h, w = len(rows), max(len(r) for r in rows)
    img = Image.new('RGBA', (w + 2, h + 1), (0, 0, 0, 0))
    for y, row in enumerate(rows):
        for x, ch in enumerate(row):
            if ch in PAL:
                c = PAL[ch].lstrip('#')
                img.putpixel((x + 1, y), tuple(int(c[k:k + 2], 16) for k in (0, 2, 4)) + (255,))
    img.save(path)
    img.resize((img.width * 8, img.height * 8), Image.NEAREST).save(os.path.join(ROOT, 'Temp', os.path.basename(path).replace('.png', '_preview.png')))


def main():
    os.makedirs(OUT, exist_ok=True)
    draw(FOOD_CACHE, os.path.join(OUT, 'food_cache.png'))
    print('props ok')


if __name__ == '__main__':
    main()
