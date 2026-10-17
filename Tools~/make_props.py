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
    'K': '#2C2832', 'k': '#4A4550', 'M': '#6B6470', 'm': '#8D8590', 'A': '#FF7A2A', 'a': '#A8361C', 'Y': '#FFE2A0',
    'V': '#8E7CFF', 'v': '#5A48C0', 'U': '#C9A8FF', 'E': '#E8DCC0', 'e': '#B8A888', 'T': '#5A4A3A',
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


NEST = [
    '.....OOOOOOOOO.....',
    '...OOEEOOOOOOEEOO..',
    '..OETeOEEOOEEOTeEO.',
    '.OTEESEOSEEOSEEETTO',
    '.OTeEEEOeEEOeEEeTTO',
    'OTTeeeOOOeeOOeeTTTO',
    'OTTTTTTTTTTTTTTTTTO',
    'OwTwTwTwTwTwTwTwTwO',
    '.OwwwwwwwwwwwwwwwO.',
    '..OOOOOOOOOOOOOOO..',
]

ANVIL = [
    '.OOOOOOOOOOOOOOO....',
    'OmmmmmmmmmmmmmmMO...',
    'OMMMMMMMMMMMMMMkkOO.',
    '.OkkkkkkkkkkkkkkKKKO',
    '...OOkkkkkkkkOOOOOO.',
    '.....OkkkkkkO.......',
    '.....OKkkkkKO.......',
    '....OKKKKKKKKO......',
    '..OOOOOOOOOOOOOO....',
    '..OwwwwwwwwwwwwO....',
    '..OOOOOOOOOOOOOO....',
]

FORGE = [
    '....OOOOOOOOOO....',
    '...OkMMMMMMMMkO...',
    '..OkMkkkkkkkkMkO..',
    '..OMkOOOOOOOOkMO..',
    '..OMkOaAYYAaOkMO..',
    '..OMkOAYYYYAOkMO..',
    '..OMkOaAAAAaOkMO..',
    '..OMkOOOOOOOOkMO..',
    '..OkMMMMMMMMMMkO..',
    '..OkkkkkkkkkkkkO..',
    '..OKKKKKKKKKKKKO..',
    '..OOOOOOOOOOOOOO..',
]

LEVER_OFF = [
    '..OO.......',
    '.ORRO......',
    '.ORrO......',
    '..OwO......',
    '...OwO.....',
    '....OwO....',
    '.....OwO...',
    '..OOOOOOOO.',
    '..OmmmmmMO.',
    '..OkkkkkkO.',
    '..OOOOOOOO.',
]
LEVER_ON = [
    '.......OO..',
    '......OGGO.',
    '......OgGO.',
    '......OwO..',
    '.....OwO...',
    '....OwO....',
    '...OwO.....',
    '..OOOOOOOO.',
    '..OmmmmmMO.',
    '..OkkkkkkO.',
    '..OOOOOOOO.',
]

RUNE_OFF = [
    '....OOOOO....',
    '...OkMMMkO...',
    '..OkMkkkMkO..',
    '..OMkkKkkMO..',
    '..OMkKKKkMO..',
    '..OMkkKkkMO..',
    '..OMkKkKkMO..',
    '..OMkkkkkMO..',
    '..OkMMMMMkO..',
    '.OOOOOOOOOOO.',
    '.OKKKKKKKKKO.',
    '.OOOOOOOOOOO.',
]
RUNE_ON = [
    '....OOOOO....',
    '...OkMMMkO...',
    '..OkMkkkMkO..',
    '..OMkkUkkMO..',
    '..OMkUVUkMO..',
    '..OMkkUkkMO..',
    '..OMkUkUkMO..',
    '..OMkkkkkMO..',
    '..OkMMMMMkO..',
    '.OOOOOOOOOOO.',
    '.OKKKKKKKKKO.',
    '.OOOOOOOOOOO.',
]

PLATE = [
    '..OOOOOOOOOO..',
    '.OmmmmmmmmmMO.',
    '.OmkkkkkkkkMO.',
    '.OmkKkkkkKkMO.',
    '.OmkkkkkkkkMO.',
    '.OMMMMMMMMMMO.',
    '..OOOOOOOOOO..',
]
PLATE_DOWN = [
    '..............',
    '..OOOOOOOOOO..',
    '.OKkkkkkkkkKO.',
    '.OKkKkkkkKkKO.',
    '.OKkkkkkkkkKO.',
    '.OKKKKKKKKKKO.',
    '..OOOOOOOOOO..',
]

SEAL = [
    '...OOOOOOO...',
    '..OVVVVVVVO..',
    '.OVvUUUUUvVO.',
    '.OVUvvUvvUVO.',
    '.OVUvUUUvUVO.',
    '.OVUvvUvvUVO.',
    '.OVvUUUUUvVO.',
    '..OVVVVVVVO..',
    '...OOOOOOO...',
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
    for name, rows in (('nest', NEST), ('anvil', ANVIL), ('forge', FORGE), ('lever_off', LEVER_OFF), ('lever_on', LEVER_ON),
                       ('rune_off', RUNE_OFF), ('rune_on', RUNE_ON), ('plate', PLATE), ('plate_down', PLATE_DOWN), ('seal', SEAL)):
        draw(rows, os.path.join(OUT, f'{name}.png'))
    sheet = Image.new('RGBA', (11 * 26, 26), (40, 36, 52, 255))
    for i, n in enumerate(['food_cache', 'nest', 'anvil', 'forge', 'lever_off', 'lever_on', 'rune_off', 'rune_on', 'plate', 'plate_down', 'seal']):
        im = Image.open(os.path.join(OUT, f'{n}.png'))
        sheet.alpha_composite(im, (i * 26 + 1, 1))
    sheet.resize((sheet.width * 5, sheet.height * 5), Image.NEAREST).save(os.path.join(ROOT, 'Temp', 'props_preview.png'))
    print('props ok')


if __name__ == '__main__':
    main()
